namespace K4System.Storage;

using System.Data;
using System.Data.Common;
using CounterStrikeSharp.API.Modules.Entities;
using Dapper;
using K4System.Missions;
using K4System.Players;
using Microsoft.Extensions.Logging;

/// <summary>
/// All database access. Every method opens its own pooled connection and is safe to call from
/// any thread. Steam ids are always bound as text so MySQL can use the primary key index.
/// </summary>
internal sealed class SqlPlayerStore
{
	private const int SchemaVersion = 2;

	private readonly SqlDialect dialect;
	private readonly TableNames tables;
	private readonly bool levelRanks;
	private readonly ILogger logger;

	private readonly string statsUpsert;
	private readonly string timesUpsert;
	private readonly string loadSelect;

	public SqlPlayerStore(SqlDialect dialect, TableNames tables, bool levelRanks, ILogger logger)
	{
		this.dialect = dialect;
		this.tables = tables;
		this.levelRanks = levelRanks;
		this.logger = logger;

		statsUpsert = BuildCounterUpsert(tables.Stats, StatColumns.Names, "s");
		timesUpsert = BuildCounterUpsert(tables.Times, PlaytimeColumns.Names, "t");
		loadSelect = $@"SELECT
				r.`points`,
				{string.Join(", ", StatColumns.Names.Select(column => $"s.`{column}`"))},
				{string.Join(", ", PlaytimeColumns.Names.Select(column => $"t.`{column}`"))},
				pr.`last_reset_at`,
				(SELECT COUNT(*) FROM `{tables.Ranks}` better WHERE better.`points` > r.`points`) AS placement
			FROM `{tables.Ranks}` AS r
			LEFT JOIN `{tables.Stats}` AS s ON r.`steam_id` = s.`steam_id`
			LEFT JOIN `{tables.Times}` AS t ON r.`steam_id` = t.`steam_id`
			LEFT JOIN `{tables.Resets}` AS pr ON r.`steam_id` = pr.`steam_id`
			WHERE r.`steam_id` = @SteamId;";
	}

	public string DialectName => dialect.Name;

	private string BuildCounterUpsert(string table, IReadOnlyList<string> columns, string parameterPrefix)
	{
		string names = string.Join(", ", columns.Select(column => $"`{column}`"));
		string values = string.Join(", ", columns.Select((_, index) => $"@{parameterPrefix}{index}"));
		string updates = string.Join(", ", columns.Select((column, index) => $"`{column}` = @{parameterPrefix}{index}"));

		return $@"INSERT INTO `{table}` (`name`, `steam_id`, `lastseen`, {names})
			VALUES (@Name, @SteamId, CURRENT_TIMESTAMP, {values})
			{dialect.UpsertClause("`steam_id`")} `name` = @Name, `lastseen` = CURRENT_TIMESTAMP, {updates};";
	}

	private async Task<DbConnection> OpenAsync()
	{
		DbConnection connection = dialect.CreateConnection();
		try
		{
			await connection.OpenAsync();
			return connection;
		}
		catch
		{
			await connection.DisposeAsync();
			throw;
		}
	}

	private static string Id(ulong steamId) => steamId.ToString(System.Globalization.CultureInfo.InvariantCulture);

	// ---------------------------------------------------------------- schema

	/// <summary>Creates missing tables, then applies schema migrations newer than the stored version.</summary>
	public async Task InitializeAsync()
	{
		await using DbConnection connection = await OpenAsync();
		await dialect.PrepareAsync(connection);

		// Each table on its own: one the database refuses no longer keeps the others from being created.
		List<string> failed = [];
		foreach (string statement in dialect.CreateTables(tables))
		{
			try
			{
				await connection.ExecuteAsync(statement);
			}
			catch (Exception ex)
			{
				string table = StatementTarget(statement);
				failed.Add(table);
				logger.LogError("Could not create the K4 table {Table}: {ErrorMessage}", table, ex.Message);
			}
		}
		if (failed.Count > 0)
			throw new InvalidOperationException($"these tables could not be created: {string.Join(", ", failed)} (the reason is logged above)");

		if (levelRanks)
		{
			try
			{
				await connection.ExecuteAsync(dialect.CreateLevelRanksTable(tables.LevelRanks));
			}
			catch (Exception ex)
			{
				logger.LogError("K4 tables are ready, but the optional LevelRanks table could not be created: {ErrorMessage}", ex.Message);
			}
		}

		await connection.ExecuteAsync(dialect.CreateSchemaTable(tables.Schema));
		object? storedVersion = await connection.ExecuteScalarAsync($"SELECT `version` FROM `{tables.Schema}` WHERE `id` = 1;");
		long version = storedVersion is null or DBNull ? 0 : Convert.ToInt64(storedVersion);

		if (version < 1)
			await MigrateRewardCompletionAsync(connection);

		bool indexReady = true;
		if (version < 2)
		{
			try
			{
				await MigratePointsIndexAsync(connection);
			}
			catch (Exception ex)
			{
				// Only a speed-up. Another server sharing this database may have just created it.
				indexReady = false;
				logger.LogWarning("Could not add the points index to the ranks table; it will be retried on the next start: {ErrorMessage}", ex.Message);
			}
		}

		if (version < SchemaVersion && indexReady)
		{
			await connection.ExecuteAsync(
				$"{dialect.InsertIgnore} `{tables.Schema}` (`id`, `version`) VALUES (1, @Version);",
				new { Version = SchemaVersion });
			await connection.ExecuteAsync(
				$"UPDATE `{tables.Schema}` SET `version` = @Version WHERE `id` = 1;",
				new { Version = SchemaVersion });
		}
	}

	// 5.1: rewards remember whether their command ran. Claims from before 5.1 count as completed.
	private async Task MigrateRewardCompletionAsync(DbConnection connection)
	{
		if (await dialect.ColumnExistsAsync(connection, tables.Rewards, "executed_at"))
			return;

		try
		{
			await connection.ExecuteAsync(dialect.AddRewardExecutedColumn(tables.Rewards));
		}
		catch (Exception ex) when (dialect.IsDuplicateColumn(ex))
		{
			// Another server sharing this database added it at the same moment.
		}

		await connection.ExecuteAsync($"UPDATE `{tables.Rewards}` SET `executed_at` = `claimed_at` WHERE `executed_at` IS NULL;");
		logger.LogInformation("Updated the rank reward table to track completed reward commands.");
	}

	// 6.0: leaderboard positions count players with more points, which needs an index on points.
	private async Task MigratePointsIndexAsync(DbConnection connection)
	{
		string index = IndexName(tables.Ranks, "points");
		if (await dialect.IndexExistsAsync(connection, tables.Ranks, index))
			return;

		await connection.ExecuteAsync(dialect.CreateIndex(tables.Ranks, index, "points"));
		logger.LogInformation("Added a points index to the ranks table for faster leaderboard lookups.");
	}

	// The table (or index) a CREATE statement makes: its first `quoted` name.
	internal static string StatementTarget(string statement)
	{
		int start = statement.IndexOf('`');
		int end = start < 0 ? -1 : statement.IndexOf('`', start + 1);
		return end > start ? statement[(start + 1)..end] : "?";
	}

	// MySQL limits identifiers to 64 characters; long table prefixes get a short hashed name.
	internal static string IndexName(string table, string column)
	{
		string name = $"idx_{table}_{column}";
		if (name.Length <= 64)
			return name;

		string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name)))[..16].ToLowerInvariant();
		return $"idx_k4_{column}_{hash}";
	}

	// ---------------------------------------------------------------- players

	/// <summary>Creates missing rows for the player, refreshes name and last-seen, and returns the stored data.</summary>
	public async Task<PlayerRecord?> LoadOrCreateAsync(ulong steamId, string name, string defaultRankName, int startPoints)
	{
		await using DbConnection connection = await OpenAsync();
		await using DbTransaction transaction = await connection.BeginTransactionAsync();

		var parameters = new { SteamId = Id(steamId), Name = name, RankName = defaultRankName, StartPoints = startPoints };

		await connection.ExecuteAsync($@"INSERT INTO `{tables.Ranks}` (`name`, `steam_id`, `rank`, `points`, `lastseen`)
			VALUES (@Name, @SteamId, @RankName, @StartPoints, CURRENT_TIMESTAMP)
			{dialect.UpsertClause("`steam_id`")} `name` = @Name, `lastseen` = CURRENT_TIMESTAMP;", parameters, transaction);

		foreach (string table in new[] { tables.Stats, tables.Times })
		{
			await connection.ExecuteAsync($@"INSERT INTO `{table}` (`name`, `steam_id`, `lastseen`)
				VALUES (@Name, @SteamId, CURRENT_TIMESTAMP)
				{dialect.UpsertClause("`steam_id`")} `name` = @Name, `lastseen` = CURRENT_TIMESTAMP;", parameters, transaction);
		}

		PlayerRecord? record = null;

		await using (DbDataReader reader = await connection.ExecuteReaderAsync(loadSelect, parameters, transaction))
		{
			if (await reader.ReadAsync())
				record = ReadRecord(reader);
		}

		if (record is not null)
			record = await WithProfileDataAsync(connection, transaction, record, parameters.SteamId);

		await transaction.CommitAsync();
		return record;
	}

	/// <summary>Adds what the main load query does not read: achievements, weapon kills, the showcase and the season.</summary>
	private async Task<PlayerRecord> WithProfileDataAsync(DbConnection connection, DbTransaction? transaction, PlayerRecord record, string steamId)
	{
		var parameters = new { SteamId = steamId };
		IEnumerable<string> achievements = await connection.QueryAsync<string>(
			$"SELECT `achievement_id` FROM `{tables.Achievements}` WHERE `steam_id` = @SteamId;", parameters, transaction);
		IEnumerable<(string Weapon, long Kills)> weapons = await connection.QueryAsync<(string, long)>(
			$"SELECT `weapon`, `kills` FROM `{tables.WeaponKills}` WHERE `steam_id` = @SteamId;", parameters, transaction);
		string? showcase = await connection.ExecuteScalarAsync<string?>(
			$"SELECT `showcase` FROM `{tables.Profiles}` WHERE `steam_id` = @SteamId;", parameters, transaction);
		string? season = await connection.ExecuteScalarAsync<string?>(
			$"SELECT `season` FROM `{tables.SeasonState}` WHERE `id` = 1;", transaction: transaction);

		return record with
		{
			Achievements = achievements.ToArray(),
			WeaponKills = weapons.ToDictionary(row => row.Weapon, row => (int)Math.Clamp(row.Kills, 0, int.MaxValue), StringComparer.Ordinal),
			Showcase = ParseShowcase(showcase),
			Season = season
		};
	}

	internal static IReadOnlyList<string> ParseShowcase(string? stored)
		=> (stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private PlayerRecord ReadRecord(DbDataReader reader)
	{
		int ordinal = 0;
		int points = ReadInt(reader, ordinal++);

		int[] stats = new int[StatColumns.Names.Count];
		for (int i = 0; i < stats.Length; i++)
			stats[i] = ReadInt(reader, ordinal++);

		int[] playtime = new int[PlaytimeColumns.Names.Count];
		for (int i = 0; i < playtime.Length; i++)
			playtime[i] = ReadInt(reader, ordinal++);

		DateTime? lastReset = dialect.ReadUtcDateTime(reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal));
		ordinal++;

		int placement = ReadInt(reader, ordinal) + 1;
		return new PlayerRecord(points, stats, playtime, lastReset, placement, []);
	}

	private static int ReadInt(DbDataReader reader, int ordinal)
		=> reader.IsDBNull(ordinal) ? 0 : (int)Math.Clamp(Convert.ToInt64(reader.GetValue(ordinal)), int.MinValue, int.MaxValue);

	/// <summary>Saves snapshots in one transaction. The LevelRanks mirror runs afterwards and cannot roll it back.</summary>
	public async Task SaveAsync(IReadOnlyCollection<PlayerSnapshot> snapshots)
	{
		if (snapshots.Count == 0)
			return;

		await using DbConnection connection = await OpenAsync();

		await using (DbTransaction transaction = await connection.BeginTransactionAsync())
		{
			// Read once per save: points captured before a season ended must not undo its reset.
			string? storedSeason = snapshots.Any(snapshot => snapshot.Season is not null)
				? await connection.ExecuteScalarAsync<string?>($"SELECT `season` FROM `{tables.SeasonState}` WHERE `id` = 1;", transaction: transaction)
				: null;

			foreach (PlayerSnapshot snapshot in snapshots)
			{
				string steamId = Id(snapshot.SteamId);
				bool currentSeason = snapshot.Season is null || storedSeason is null || snapshot.Season == storedSeason;

				if (snapshot.RankName is not null && currentSeason)
				{
					await connection.ExecuteAsync($@"INSERT INTO `{tables.Ranks}` (`name`, `steam_id`, `rank`, `points`, `lastseen`)
						VALUES (@Name, @SteamId, @Rank, @Points, CURRENT_TIMESTAMP)
						{dialect.UpsertClause("`steam_id`")} `name` = @Name, `points` = @Points, `lastseen` = CURRENT_TIMESTAMP, `rank` = @Rank;",
						new { snapshot.Name, SteamId = steamId, Rank = snapshot.RankName, snapshot.Points }, transaction);

					// The season row marks the player as part of this season and remembers their best points in it.
					if (snapshot.Season is not null)
					{
						await connection.ExecuteAsync($@"INSERT INTO `{tables.Seasons}` (`season`, `steam_id`, `name`, `peak_points`)
							VALUES (@Season, @SteamId, @Name, @Points)
							{dialect.UpsertClause("`season`, `steam_id`")} `name` = @Name, `peak_points` = {dialect.Greatest}(`peak_points`, @Points);",
							new { snapshot.Season, SteamId = steamId, snapshot.Name, snapshot.Points }, transaction);
					}
				}

				foreach ((string weapon, int kills) in snapshot.WeaponKills ?? new Dictionary<string, int>())
				{
					await connection.ExecuteAsync($@"INSERT INTO `{tables.WeaponKills}` (`steam_id`, `weapon`, `kills`)
						VALUES (@SteamId, @Weapon, @Kills)
						{dialect.UpsertClause("`steam_id`, `weapon`")} `kills` = @Kills;",
						new { SteamId = steamId, Weapon = Truncate(weapon, 64), Kills = kills }, transaction);
				}

				if (snapshot.Showcase is not null)
				{
					await connection.ExecuteAsync($@"INSERT INTO `{tables.Profiles}` (`steam_id`, `showcase`)
						VALUES (@SteamId, @Showcase)
						{dialect.UpsertClause("`steam_id`")} `showcase` = @Showcase;",
						new { SteamId = steamId, Showcase = Truncate(snapshot.Showcase, 255) }, transaction);
				}

				if (snapshot.Stats is not null)
					await connection.ExecuteAsync(statsUpsert, CounterParameters(steamId, snapshot.Name, snapshot.Stats, "s"), transaction);

				if (snapshot.Playtime is not null)
					await connection.ExecuteAsync(timesUpsert, CounterParameters(steamId, snapshot.Name, snapshot.Playtime, "t"), transaction);

				if (snapshot.Missions is MissionSnapshot missions)
					await SaveMissionsAsync(connection, transaction, steamId, missions);

				// Saved with the points they gave, so an unlock and its reward are never stored apart.
				foreach (string achievement in snapshot.NewAchievements ?? [])
				{
					await connection.ExecuteAsync($@"{dialect.InsertIgnore} `{tables.Achievements}` (`steam_id`, `achievement_id`, `unlocked_at`)
						VALUES (@SteamId, @Achievement, CURRENT_TIMESTAMP);", new { SteamId = steamId, Achievement = achievement }, transaction);
				}

				if (snapshot.PersonalRankResetUtc is DateTime resetAt)
				{
					// Only claims from before the reset: a delayed save must not also wipe rewards earned after it.
					// claimed_at uses the database clock, so the cut-off is computed there too.
					// Negative when the reset time is ahead of this clock; the cut-off is then in the future.
					long secondsAgo = (long)Math.Ceiling((DateTime.UtcNow - resetAt).TotalSeconds);
					await connection.ExecuteAsync($"DELETE FROM `{tables.Rewards}` WHERE `steam_id` = @SteamId AND `claimed_at` < {dialect.SecondsAgo};",
						new { SteamId = steamId, Seconds = secondsAgo }, transaction);
					await connection.ExecuteAsync($@"INSERT INTO `{tables.Resets}` (`steam_id`, `last_reset_at`)
						VALUES (@SteamId, @ResetAt)
						{dialect.UpsertClause("`steam_id`")} `last_reset_at` = @ResetAt;",
						new { SteamId = steamId, ResetAt = DateTime.SpecifyKind(resetAt, DateTimeKind.Unspecified) }, transaction);
				}
			}

			await transaction.CommitAsync();
		}

		if (!levelRanks)
			return;

		foreach (PlayerSnapshot snapshot in snapshots)
		{
			try
			{
				await MirrorToLevelRanksAsync(connection, snapshot);
			}
			catch (Exception ex)
			{
				logger.LogError("K4 player data was saved, but the optional LevelRanks mirror failed for {SteamId}: {ErrorMessage}", snapshot.SteamId, ex.Message);
			}
		}
	}

	private async Task SaveMissionsAsync(DbConnection connection, DbTransaction transaction, string steamId, MissionSnapshot missions)
	{
		foreach (MissionSlotSnapshot slot in missions.Slots)
		{
			await connection.ExecuteAsync($@"INSERT INTO `{tables.Missions}` (`steam_id`, `day`, `slot`, `mission_id`, `progress`, `completed`)
				VALUES (@SteamId, @Day, @Slot, @MissionId, @Progress, @Completed)
				{dialect.UpsertClause("`steam_id`, `day`, `slot`")} `mission_id` = @MissionId, `progress` = @Progress, `completed` = @Completed;",
				new { SteamId = steamId, missions.Day, slot.Slot, slot.MissionId, slot.Progress, Completed = slot.Completed ? 1 : 0 }, transaction);
		}

		// The weekly mission is one row under the Monday of its week, in its own slot so it never meets a daily row.
		if (missions is { Weekly: MissionSlotSnapshot weekly, Week: string week })
		{
			await connection.ExecuteAsync($@"INSERT INTO `{tables.Missions}` (`steam_id`, `day`, `slot`, `mission_id`, `progress`, `completed`)
				VALUES (@SteamId, @Day, @Slot, @MissionId, @Progress, @Completed)
				{dialect.UpsertClause("`steam_id`, `day`, `slot`")} `mission_id` = @MissionId, `progress` = @Progress, `completed` = @Completed;",
				new { SteamId = steamId, Day = week, weekly.Slot, weekly.MissionId, weekly.Progress, Completed = weekly.Completed ? 1 : 0 }, transaction);
		}

		await connection.ExecuteAsync($@"INSERT INTO `{tables.MissionState}` (`steam_id`, `streak`, `last_completed_day`, `reroll_day`, `rerolls_used`)
			VALUES (@SteamId, @Streak, @LastCompletedDay, @Day, @RerollsUsed)
			{dialect.UpsertClause("`steam_id`")} `streak` = @Streak, `last_completed_day` = @LastCompletedDay, `reroll_day` = @Day, `rerolls_used` = @RerollsUsed;",
			new { SteamId = steamId, missions.Streak, missions.LastCompletedDay, missions.Day, missions.RerollsUsed }, transaction);
	}

	/// <summary>Today's mission rows, this week's row, and the player's streak. Rerolls only count when they were used today.</summary>
	public async Task<MissionRecord> LoadMissionsAsync(ulong steamId, string day, string? week = null)
	{
		await using DbConnection connection = await OpenAsync();
		var parameters = new { SteamId = Id(steamId), Day = day, Week = week ?? day, WeeklySlot = MissionRules.WeeklySlot };

		IEnumerable<(long Slot, string MissionId, long Progress, long Completed)> rows = await connection.QueryAsync<(long, string, long, long)>(
			$"SELECT `slot`, `mission_id`, `progress`, `completed` FROM `{tables.Missions}` WHERE `steam_id` = @SteamId AND `day` = @Day AND `slot` < @WeeklySlot ORDER BY `slot`;",
			parameters);

		List<(long Slot, string MissionId, long Progress, long Completed)> weeklyRows = week is null ? [] : (await connection.QueryAsync<(long, string, long, long)>(
			$"SELECT `slot`, `mission_id`, `progress`, `completed` FROM `{tables.Missions}` WHERE `steam_id` = @SteamId AND `day` = @Week AND `slot` = @WeeklySlot;",
			parameters)).ToList();

		MissionStateRow? state = await connection.QueryFirstOrDefaultAsync<MissionStateRow>(
			$"SELECT `streak` AS Streak, `last_completed_day` AS LastCompletedDay, `reroll_day` AS RerollDay, `rerolls_used` AS RerollsUsed FROM `{tables.MissionState}` WHERE `steam_id` = @SteamId;",
			parameters);

		List<MissionSlotSnapshot> slots = rows
			.Select(row => new MissionSlotSnapshot((int)row.Slot, row.MissionId, (int)Math.Clamp(row.Progress, 0, int.MaxValue), row.Completed != 0))
			.ToList();

		MissionSlotSnapshot? weekly = weeklyRows.Count > 0 && weeklyRows[0] is var found
			? new MissionSlotSnapshot((int)found.Slot, found.MissionId, (int)Math.Clamp(found.Progress, 0, int.MaxValue), found.Completed != 0)
			: null;

		int rerolls = state is not null && state.RerollDay == day ? (int)state.RerollsUsed : 0;
		return new MissionRecord(slots, rerolls, (int)(state?.Streak ?? 0), state?.LastCompletedDay, weekly);
	}

	private sealed class MissionStateRow
	{
		public long Streak { get; set; }
		public string? LastCompletedDay { get; set; }
		public string? RerollDay { get; set; }
		public long RerollsUsed { get; set; }
	}

	private static DynamicParameters CounterParameters(string steamId, string name, int[] values, string prefix)
	{
		DynamicParameters parameters = new();
		parameters.Add("Name", name);
		parameters.Add("SteamId", steamId);
		for (int i = 0; i < values.Length; i++)
			parameters.Add($"{prefix}{i}", values[i]);
		return parameters;
	}

	private async Task MirrorToLevelRanksAsync(DbConnection connection, PlayerSnapshot snapshot)
	{
		int Stat(StatField field) => snapshot.Stats?[(int)field] ?? 0;

		// A new row starts at zero, but an existing row keeps the columns of modules that are turned off.
		List<string> updates = ["`name` = @Name", $"`lastconnect` = {dialect.UnixTimestampNow}"];
		if (snapshot.Stats is not null)
		{
			updates.AddRange(["`kills` = @Kills", "`deaths` = @Deaths", "`shoots` = @Shoots", "`hits` = @Hits", "`headshots` = @Headshots",
				"`assists` = @Assists", "`round_win` = @RoundWin", "`round_lose` = @RoundLose"]);
		}
		if (snapshot.RankName is not null)
			updates.AddRange(["`value` = @Points", "`rank` = @Rank"]);
		if (snapshot.Playtime is not null)
			updates.Add("`playtime` = @Playtime");

		await connection.ExecuteAsync($@"INSERT INTO `{tables.LevelRanks}`
				(`steam`, `name`, `kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`, `lastconnect`, `value`, `rank`, `playtime`)
			VALUES
				(@SteamId, @Name, @Kills, @Deaths, @Shoots, @Hits, @Headshots, @Assists, @RoundWin, @RoundLose, {dialect.UnixTimestampNow}, @Points, @Rank, @Playtime)
			{dialect.UpsertClause("`steam`")}
				{string.Join(", ", updates)};",
			new
			{
				SteamId = new SteamID(snapshot.SteamId).SteamId2.Replace("STEAM_0", "STEAM_1"),
				snapshot.Name,
				Kills = Stat(StatField.Kills),
				Deaths = Stat(StatField.Deaths),
				Shoots = Stat(StatField.Shoots),
				Hits = Stat(StatField.HitsGiven),
				Headshots = Stat(StatField.Headshots),
				Assists = Stat(StatField.Assists),
				RoundWin = Stat(StatField.RoundWin),
				RoundLose = Stat(StatField.RoundLose),
				snapshot.Points,
				Rank = snapshot.RankId,
				Playtime = snapshot.Playtime?[(int)PlaytimeField.All] ?? 0
			});
	}

	/// <summary>Deletes players not seen for the given number of days, plus their reward and reset rows.</summary>
	public async Task PurgeAsync(int days)
	{
		await using DbConnection connection = await OpenAsync();

		// Mission rows are only needed for today; keep two weeks for support questions. This runs even when
		// inactive players are kept, or the table would grow by three rows per player per day forever.
		string oldestMissionDay = DateTime.UtcNow.AddDays(-14).ToString(MissionRules.DayFormat, System.Globalization.CultureInfo.InvariantCulture);
		await connection.ExecuteAsync($"DELETE FROM `{tables.Missions}` WHERE `day` < @Day;", new { Day = oldestMissionDay });

		// The point audit log keeps 90 days, like a support ticket's memory; it is never needed for gameplay.
		await connection.ExecuteAsync($"DELETE FROM `{tables.PointLog}` WHERE {dialect.OlderThanDays("changed_at")};", new { Days = PointLogDays });

		if (days <= 0)
			return;

		var parameters = new { Days = days };

		// One transaction: a player who joins while the purge runs is either purged everywhere or kept everywhere.
		// Separately, the times and stats rows could go while the join refreshed the ranks row, wiping only half.
		await using (DbTransaction transaction = await connection.BeginTransactionAsync())
		{
			foreach (string table in new[] { tables.Times, tables.Stats, tables.Ranks })
				await connection.ExecuteAsync($"DELETE FROM `{table}` WHERE {dialect.OlderThanDays("lastseen")};", parameters, transaction);

			foreach (string table in new[] { tables.Rewards, tables.Resets, tables.Missions, tables.MissionState, tables.PointLog, tables.Achievements, tables.WeaponKills, tables.Profiles })
			{
				await connection.ExecuteAsync($@"DELETE FROM `{table}`
					WHERE NOT EXISTS (SELECT 1 FROM `{tables.Ranks}` r WHERE r.`steam_id` = `{table}`.`steam_id`);", transaction: transaction);
			}

			await transaction.CommitAsync();
		}

		if (!levelRanks)
			return;

		try
		{
			await connection.ExecuteAsync($"DELETE FROM `{tables.LevelRanks}` WHERE {dialect.LevelRanksOlderThanDays};", parameters);
		}
		catch (Exception ex)
		{
			logger.LogError("K4 data was purged, but the optional LevelRanks purge failed: {ErrorMessage}", ex.Message);
		}
	}

	// ---------------------------------------------------------------- point audit log

	internal const int PointLogDays = 90;

	public async Task LogPointChangesAsync(IReadOnlyCollection<PointChange> changes)
	{
		if (changes.Count == 0)
			return;

		await using DbConnection connection = await OpenAsync();
		await using DbTransaction transaction = await connection.BeginTransactionAsync();
		foreach (PointChange change in changes)
		{
			await connection.ExecuteAsync($@"INSERT INTO `{tables.PointLog}` (`steam_id`, `changed_at`, `delta`, `points_after`, `source`, `actor`)
				VALUES (@SteamId, CURRENT_TIMESTAMP, @Delta, @PointsAfter, @Source, @Actor);",
				new { SteamId = Id(change.SteamId), change.Delta, change.PointsAfter, Source = Truncate(change.Source, 32), Actor = Truncate(change.Actor, 255) }, transaction);
		}
		await transaction.CommitAsync();
	}

	/// <summary>The player's newest logged point changes first.</summary>
	public async Task<List<PointHistoryEntry>> GetPointHistoryAsync(ulong steamId, int count)
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<(object ChangedAt, long Delta, long PointsAfter, string Source, string Actor)> rows = await connection.QueryAsync<(object, long, long, string, string)>(
			$"SELECT `changed_at`, `delta`, `points_after`, `source`, `actor` FROM `{tables.PointLog}` WHERE `steam_id` = @SteamId ORDER BY `changed_at` DESC, `id` DESC LIMIT @Count;",
			new { SteamId = Id(steamId), Count = count });

		return rows.Select(row => new PointHistoryEntry(
			dialect.ReadUtcDateTime(row.ChangedAt) ?? DateTime.MinValue,
			(int)Math.Clamp(row.Delta, int.MinValue, int.MaxValue),
			(int)Math.Clamp(row.PointsAfter, int.MinValue, int.MaxValue),
			row.Source,
			row.Actor)).ToList();
	}

	private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

	// ---------------------------------------------------------------- leaderboard

	public async Task<(int Placement, int Total)> GetPlacementAndTotalAsync(ulong steamId)
	{
		await using DbConnection connection = await OpenAsync();
		(long better, long total) = await connection.QuerySingleAsync<(long, long)>($@"SELECT
				(SELECT COUNT(*) FROM `{tables.Ranks}` WHERE `points` > (SELECT `points` FROM `{tables.Ranks}` WHERE `steam_id` = @SteamId)),
				(SELECT COUNT(*) FROM `{tables.Ranks}`);",
			new { SteamId = Id(steamId) });

		return ((int)Math.Min(better + 1, int.MaxValue), (int)Math.Min(total, int.MaxValue));
	}

	public async Task<Dictionary<ulong, int>> GetPlacementsAsync(IReadOnlyCollection<ulong> steamIds)
	{
		Dictionary<ulong, int> placements = [];
		if (steamIds.Count == 0)
			return placements;

		await using DbConnection connection = await OpenAsync();
		IEnumerable<(string SteamId, long Better)> rows = await connection.QueryAsync<(string, long)>($@"SELECT t.`steam_id`,
				(SELECT COUNT(*) FROM `{tables.Ranks}` better WHERE better.`points` > t.`points`)
			FROM `{tables.Ranks}` t
			WHERE t.`steam_id` IN @SteamIds;",
			new { SteamIds = steamIds.Select(Id).ToArray() });

		foreach ((string steamId, long better) in rows)
		{
			if (ulong.TryParse(steamId, out ulong id))
				placements[id] = (int)Math.Min(better + 1, int.MaxValue);
		}

		return placements;
	}

	public async Task<List<TopEntry>> GetTopAsync(int count)
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<(long Points, string Name, string Rank, string SteamId)> rows = await connection.QueryAsync<(long, string, string, string)>(
			$"SELECT `points`, `name`, `rank`, `steam_id` FROM `{tables.Ranks}` ORDER BY `points` DESC LIMIT @Count;",
			new { Count = count });

		return rows.Select(ToTopEntry).ToList();
	}

	private static TopEntry ToTopEntry((long Points, string Name, string Rank, string SteamId) row)
		=> new((int)Math.Clamp(row.Points, int.MinValue, int.MaxValue), row.Name, row.Rank, ulong.TryParse(row.SteamId, out ulong id) ? id : 0);

	// ---------------------------------------------------------------- profiles

	/// <summary>A stored player's profile for the panel, or null for an unknown player. Creates no rows.</summary>
	public async Task<ProfileRecord?> GetProfileAsync(ulong steamId, int seasons)
	{
		await using DbConnection connection = await OpenAsync();
		var parameters = new { SteamId = Id(steamId) };

		PlayerRecord? record = null;
		await using (DbDataReader reader = await connection.ExecuteReaderAsync(loadSelect, parameters))
		{
			if (await reader.ReadAsync())
				record = ReadRecord(reader);
		}

		if (record is null)
			return null;

		record = await WithProfileDataAsync(connection, null, record, parameters.SteamId);
		(string name, string rank) = await connection.QuerySingleAsync<(string, string)>(
			$"SELECT `name`, `rank` FROM `{tables.Ranks}` WHERE `steam_id` = @SteamId;", parameters);
		long total = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM `{tables.Ranks}`;");

		return new ProfileRecord(steamId, name, rank, record, (int)Math.Min(total, int.MaxValue), await ReadSeasonResultsAsync(connection, steamId, seasons));
	}

	// ---------------------------------------------------------------- seasons

	/// <summary>The season stored in the database, writing <paramref name="fresh"/> when there is none yet.</summary>
	public async Task<string> EnsureSeasonAsync(string fresh)
	{
		await using DbConnection connection = await OpenAsync();
		await connection.ExecuteAsync($"{dialect.InsertIgnore} `{tables.SeasonState}` (`id`, `season`) VALUES (1, @Season);", new { Season = fresh });
		return await connection.ExecuteScalarAsync<string?>($"SELECT `season` FROM `{tables.SeasonState}` WHERE `id` = 1;") ?? fresh;
	}

	public async Task<string?> GetSeasonAsync()
	{
		await using DbConnection connection = await OpenAsync();
		return await connection.ExecuteScalarAsync<string?>($"SELECT `season` FROM `{tables.SeasonState}` WHERE `id` = 1;");
	}

	/// <summary>
	/// Ends season <paramref name="from"/> and starts <paramref name="to"/>: stores everyone's final points and rank,
	/// puts all points back to the start, and clears rank reward claims so rewards can be earned again. Returns false
	/// when the stored season is no longer <paramref name="from"/>, such as when another server sharing the database
	/// ended it first; nothing is changed then.
	/// </summary>
	public async Task<bool> EndSeasonAsync(string from, string to, int startPoints, string startRank)
	{
		await using DbConnection connection = await OpenAsync();
		await using DbTransaction transaction = await connection.BeginTransactionAsync();
		var parameters = new { From = from, To = to, Start = startPoints, Rank = startRank };

		int moved = await connection.ExecuteAsync(
			$"UPDATE `{tables.SeasonState}` SET `season` = @To WHERE `id` = 1 AND `season` = @From;", parameters, transaction);
		if (moved == 0)
		{
			await transaction.RollbackAsync();
			return false;
		}

		// Players with points who did not play this season (such as everyone's points from before seasons) are kept too,
		// so every reset can be undone from this table.
		await connection.ExecuteAsync($@"{dialect.InsertIgnore} `{tables.Seasons}` (`season`, `steam_id`, `name`, `peak_points`, `points`, `rank`)
			SELECT @From, `steam_id`, `name`, `points`, `points`, `rank` FROM `{tables.Ranks}` WHERE `points` <> @Start;", parameters, transaction);

		string season = $"`{tables.Seasons}`";
		await connection.ExecuteAsync($@"UPDATE {season} SET
				`points` = (SELECT r.`points` FROM `{tables.Ranks}` r WHERE r.`steam_id` = {season}.`steam_id`),
				`rank` = (SELECT r.`rank` FROM `{tables.Ranks}` r WHERE r.`steam_id` = {season}.`steam_id`)
			WHERE `season` = @From;", parameters, transaction);

		// Purged players have no ranks row left to finish with.
		await connection.ExecuteAsync($"DELETE FROM {season} WHERE `season` = @From AND `points` IS NULL;", parameters, transaction);
		await connection.ExecuteAsync($"UPDATE {season} SET `peak_points` = `points` WHERE `season` = @From AND `points` > `peak_points`;", parameters, transaction);

		await connection.ExecuteAsync($"UPDATE `{tables.Ranks}` SET `points` = @Start, `rank` = @Rank;", parameters, transaction);
		await connection.ExecuteAsync($"DELETE FROM `{tables.Rewards}`;", transaction: transaction);

		await transaction.CommitAsync();

		if (levelRanks)
		{
			// Like every save's mirror, this runs after the commit and cannot undo it. -1 is the starting rank's id there.
			try
			{
				await connection.ExecuteAsync($"UPDATE `{tables.LevelRanks}` SET `value` = @Start, `rank` = -1;", parameters);
			}
			catch (Exception ex)
			{
				logger.LogError("The season ended, but the optional LevelRanks table could not be reset: {ErrorMessage}", ex.Message);
			}
		}

		return true;
	}

	/// <summary>Finished seasons, newest first.</summary>
	public async Task<List<string>> GetFinishedSeasonsAsync(int count)
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<string> seasons = await connection.QueryAsync<string>(
			$"SELECT DISTINCT `season` FROM `{tables.Seasons}` WHERE `points` IS NOT NULL ORDER BY `season` DESC LIMIT @Count;",
			new { Count = count });
		return seasons.ToList();
	}

	/// <summary>A finished season's top players and how many players it had.</summary>
	public async Task<(List<TopEntry> Top, int Players)> GetSeasonTopAsync(string season, int count)
	{
		await using DbConnection connection = await OpenAsync();
		var parameters = new { Season = season, Count = count };
		IEnumerable<(long Points, string Name, string Rank, string SteamId)> rows = await connection.QueryAsync<(long, string, string, string)>(
			$"SELECT `points`, `name`, `rank`, `steam_id` FROM `{tables.Seasons}` WHERE `season` = @Season AND `points` IS NOT NULL ORDER BY `points` DESC LIMIT @Count;",
			parameters);
		long players = await connection.ExecuteScalarAsync<long>(
			$"SELECT COUNT(*) FROM `{tables.Seasons}` WHERE `season` = @Season AND `points` IS NOT NULL;", parameters);
		return (rows.Select(ToTopEntry).ToList(), (int)Math.Min(players, int.MaxValue));
	}

	/// <summary>The player's finished seasons, newest first.</summary>
	public async Task<List<SeasonResult>> GetPlayerSeasonsAsync(ulong steamId, int count)
	{
		await using DbConnection connection = await OpenAsync();
		return await ReadSeasonResultsAsync(connection, steamId, count);
	}

	private async Task<List<SeasonResult>> ReadSeasonResultsAsync(DbConnection connection, ulong steamId, int count)
	{
		string season = $"`{tables.Seasons}`";
		IEnumerable<(string Season, long Points, string? Rank, long Peak, long Better, long Players)> rows = await connection.QueryAsync<(string, long, string?, long, long, long)>(
			$@"SELECT s.`season`, s.`points`, s.`rank`, s.`peak_points`,
					(SELECT COUNT(*) FROM {season} b WHERE b.`season` = s.`season` AND b.`points` > s.`points`),
					(SELECT COUNT(*) FROM {season} c WHERE c.`season` = s.`season` AND c.`points` IS NOT NULL)
				FROM {season} s
				WHERE s.`steam_id` = @SteamId AND s.`points` IS NOT NULL
				ORDER BY s.`season` DESC LIMIT @Count;",
			new { SteamId = Id(steamId), Count = count });

		return rows.Select(row => new SeasonResult(
			row.Season,
			(int)Math.Clamp(row.Points, int.MinValue, int.MaxValue),
			row.Rank ?? "",
			(int)Math.Clamp(row.Peak, int.MinValue, int.MaxValue),
			(int)Math.Min(row.Better + 1, int.MaxValue),
			(int)Math.Min(row.Players, int.MaxValue))).ToList();
	}

	public async Task<(int Players, float Percentage)> GetRankShareAsync(string rankName)
	{
		await using DbConnection connection = await OpenAsync();
		(long players, long total) = await connection.QuerySingleAsync<(long, long)>($@"SELECT
				(SELECT COUNT(*) FROM `{tables.Ranks}` WHERE `rank` = @RankName),
				(SELECT COUNT(*) FROM `{tables.Ranks}`);",
			new { RankName = rankName });

		float percentage = total > 0 ? (float)Math.Round(players * 100.0 / total, 2) : 0f;
		return ((int)Math.Min(players, int.MaxValue), percentage);
	}

	/// <summary>How many stored players hold each rank name, and how many players there are in total.</summary>
	public async Task<(Dictionary<string, int> Counts, int Total)> GetRankCountsAsync()
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<(string Rank, long Players)> rows = await connection.QueryAsync<(string, long)>(
			$"SELECT `rank`, COUNT(*) FROM `{tables.Ranks}` GROUP BY `rank`;");

		Dictionary<string, int> counts = new(StringComparer.Ordinal);
		long total = 0;
		foreach ((string rank, long players) in rows)
		{
			counts[rank] = (int)Math.Min(players, int.MaxValue);
			total += players;
		}

		return (counts, (int)Math.Min(total, int.MaxValue));
	}

	// ---------------------------------------------------------------- rewards

	/// <summary>
	/// Records a reward claim. Returns true when the command should run now: the claim is new,
	/// or an earlier claim never finished running.
	/// </summary>
	public async Task<bool> TryClaimRewardAsync(ulong steamId, string rewardId, string rankName, string command)
	{
		await using DbConnection connection = await OpenAsync();
		var parameters = new { SteamId = Id(steamId), RewardId = rewardId, RankName = rankName, Command = command };

		int inserted = await connection.ExecuteAsync($@"{dialect.InsertIgnore} `{tables.Rewards}`
				(`steam_id`, `reward_id`, `rank_name`, `command`, `claimed_at`)
			VALUES (@SteamId, @RewardId, @RankName, @Command, CURRENT_TIMESTAMP);", parameters);

		if (inserted > 0)
			return true;

		object? pending = await connection.ExecuteScalarAsync(
			$"SELECT CASE WHEN `executed_at` IS NULL THEN 1 ELSE 0 END FROM `{tables.Rewards}` WHERE `steam_id` = @SteamId AND `reward_id` = @RewardId;",
			parameters);

		return pending is not null and not DBNull && Convert.ToInt64(pending) == 1;
	}

	public async Task MarkRewardExecutedAsync(ulong steamId, string rewardId)
	{
		await using DbConnection connection = await OpenAsync();
		await connection.ExecuteAsync(
			$"UPDATE `{tables.Rewards}` SET `executed_at` = CURRENT_TIMESTAMP WHERE `steam_id` = @SteamId AND `reward_id` = @RewardId;",
			new { SteamId = Id(steamId), RewardId = rewardId });
	}

	public async Task DeleteRewardClaimAsync(ulong steamId, string rewardId)
	{
		await using DbConnection connection = await OpenAsync();
		await connection.ExecuteAsync(
			$"DELETE FROM `{tables.Rewards}` WHERE `steam_id` = @SteamId AND `reward_id` = @RewardId;",
			new { SteamId = Id(steamId), RewardId = rewardId });
	}

	public async Task<List<StoredReward>> GetPendingRewardsAsync(ulong steamId)
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<(string RewardId, string RankName)> rows = await connection.QueryAsync<(string, string)>(
			$"SELECT `reward_id`, `rank_name` FROM `{tables.Rewards}` WHERE `steam_id` = @SteamId AND `executed_at` IS NULL;",
			new { SteamId = Id(steamId) });

		return rows.Select(row => new StoredReward(row.RewardId, row.RankName)).ToList();
	}
}
