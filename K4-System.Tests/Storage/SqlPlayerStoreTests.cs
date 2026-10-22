namespace K4System.Tests.Storage;

using K4System.Players;
using K4System.Storage;

/// <summary>
/// Runs every storage test against SQLite, and against MySQL when K4_TEST_MYSQL is set.
/// </summary>
public sealed class SqlPlayerStoreTests
{
	private const ulong Alice = 76561198000000001;
	private const ulong Bob = 76561198000000002;
	private const ulong Carol = 76561198000000003;

	public static TheoryData<string> Databases()
	{
		TheoryData<string> data = ["sqlite"];
		if (!string.IsNullOrWhiteSpace(MySqlTestDatabase.ConnectionString))
			data.Add("mysql");
		return data;
	}

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	private static int[] Stats(params (StatField Field, int Value)[] values)
	{
		int[] stats = new int[StatColumns.Names.Count];
		foreach ((StatField field, int value) in values)
			stats[(int)field] = value;
		return stats;
	}

	private static PlayerSnapshot Snapshot(ulong steamId, int points, string rank = "Silver I", int[]? stats = null, int[]? playtime = null, DateTime? reset = null)
		=> new(steamId, $"Player {steamId % 10}", rank, 0, points, stats ?? new int[StatColumns.Names.Count], playtime ?? new int[PlaytimeColumns.Names.Count], reset);

	[Theory, MemberData(nameof(Databases))]
	public async Task New_player_gets_start_points_default_rank_and_empty_counters(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice", "None", 250);

		Assert.NotNull(record);
		Assert.Equal(250, record.Points);
		Assert.All(record.Stats, value => Assert.Equal(0, value));
		Assert.All(record.Playtime, value => Assert.Equal(0, value));
		Assert.Null(record.LastPersonalRankResetUtc);
		Assert.Equal(1, record.Placement);
		Assert.Equal("None", await db.ScalarAsync<string>($"SELECT `rank` FROM `{db.Tables.Ranks}` WHERE `steam_id` = @Id;", new { Id = Alice.ToString() }));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Saved_data_is_loaded_back_and_name_follows_the_player(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		int[] stats = Stats((StatField.Kills, 12), (StatField.AssistFlash, 3), (StatField.Headshots, 5));
		int[] playtime = [600, 300, 200, 100, 450, 150];
		await store.SaveAsync([Snapshot(Alice, 4200, "Silver IV", stats, playtime)]);

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice Renamed", "None", 0);

		Assert.NotNull(record);
		Assert.Equal(4200, record.Points);
		Assert.Equal(stats, record.Stats);
		Assert.Equal(playtime, record.Playtime);
		Assert.Equal("Alice Renamed", await db.ScalarAsync<string>($"SELECT `name` FROM `{db.Tables.Ranks}` WHERE `steam_id` = @Id;", new { Id = Alice.ToString() }));
		Assert.Equal("Silver IV", await db.ScalarAsync<string>($"SELECT `rank` FROM `{db.Tables.Ranks}` WHERE `steam_id` = @Id;", new { Id = Alice.ToString() }));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Disabled_modules_leave_their_rows_untouched(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.SaveAsync([Snapshot(Alice, 900, stats: Stats((StatField.Kills, 7)))]);

		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", null, -1, 0, null, null, null)]);

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		Assert.Equal(900, record!.Points);
		Assert.Equal(7, record.Stats[(int)StatField.Kills]);
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Placement_top_list_and_rank_share_follow_points(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();

		foreach (ulong id in new[] { Alice, Bob, Carol })
			await store.LoadOrCreateAsync(id, id.ToString(), "None", 0);

		await store.SaveAsync([Snapshot(Alice, 100, "Silver I"), Snapshot(Bob, 300, "Gold"), Snapshot(Carol, 200, "Silver I")]);

		Assert.Equal((3, 3), await store.GetPlacementAndTotalAsync(Alice));
		Assert.Equal((1, 3), await store.GetPlacementAndTotalAsync(Bob));
		Assert.Equal((1, 3), await store.GetPlacementAndTotalAsync(76561198999999999));

		Dictionary<ulong, int> placements = await store.GetPlacementsAsync([Alice, Bob, Carol]);
		Assert.Equal(3, placements[Alice]);
		Assert.Equal(1, placements[Bob]);
		Assert.Equal(2, placements[Carol]);

		List<TopEntry> top = await store.GetTopAsync(2);
		Assert.Equal([300, 200], top.Select(entry => entry.Points));
		Assert.Equal("Gold", top[0].Rank);

		(int players, float percentage) = await store.GetRankShareAsync("Silver I");
		Assert.Equal(2, players);
		Assert.Equal(66.67f, percentage, 0.01f);
		Assert.Equal((0, 0f), await store.GetRankShareAsync("Global Elite"));

		// Players tied on points share a placement.
		await store.SaveAsync([Snapshot(Carol, 300, "Gold")]);
		Assert.Equal(1, (await store.GetPlacementAndTotalAsync(Carol)).Placement);
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Reward_claims_run_once_and_resume_until_completed(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		Assert.True(await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi"));

		// Claimed but never completed: it may run again, and it is listed as pending.
		Assert.True(await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi"));
		StoredReward pending = Assert.Single(await store.GetPendingRewardsAsync(Alice));
		Assert.Equal(("reward-1", "Gold"), (pending.RewardId, pending.RankName));

		await store.MarkRewardExecutedAsync(Alice, "reward-1");

		Assert.False(await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi"));
		Assert.Empty(await store.GetPendingRewardsAsync(Alice));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Personal_rank_reset_clears_reward_claims_and_records_the_time(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.LoadOrCreateAsync(Bob, "Bob", "None", 0);
		await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi");
		await store.TryClaimRewardAsync(Bob, "reward-1", "Gold", "say hi");
		await store.MarkRewardExecutedAsync(Alice, "reward-1");

		// The claims above were made before this reset, so it clears them.
		DateTime resetAt = DateTime.UtcNow.AddSeconds(3);
		resetAt = new DateTime(resetAt.Ticks - resetAt.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
		await store.SaveAsync([Snapshot(Alice, 0, "None", reset: resetAt)]);

		Assert.True(await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi"));
		Assert.Single(await store.GetPendingRewardsAsync(Bob));

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		Assert.Equal(resetAt, record!.LastPersonalRankResetUtc);
		Assert.Equal(DateTimeKind.Utc, record.LastPersonalRankResetUtc!.Value.Kind);

		// A later reset replaces the stored time.
		DateTime secondReset = resetAt.AddDays(2);
		await store.SaveAsync([Snapshot(Alice, 0, "None", reset: secondReset)]);
		Assert.Equal(secondReset, (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!.LastPersonalRankResetUtc);
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task A_delayed_reset_save_keeps_rewards_claimed_after_the_reset(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.TryClaimRewardAsync(Alice, "reward-2", "Gold", "say hi");
		await store.MarkRewardExecutedAsync(Alice, "reward-2");

		// The reset happened an hour ago, but its save only reaches the database now.
		await store.SaveAsync([Snapshot(Alice, 0, "None", reset: DateTime.UtcNow.AddHours(-1))]);

		Assert.False(await store.TryClaimRewardAsync(Alice, "reward-2", "Gold", "say hi"));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task LevelRanks_mirror_keeps_columns_of_disabled_modules(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore(levelRanks: true);
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		int[] stats = Stats((StatField.Kills, 9));
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Silver II", 1, 1600, stats, [3600, 0, 0, 0, 0, 0], null)]);
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", null, -1, 0, null, null, null)]);

		string steam2 = new CounterStrikeSharp.API.Modules.Entities.SteamID(Alice).SteamId2.Replace("STEAM_0", "STEAM_1");
		var steam = new { Steam = steam2 };
		Assert.Equal(9, await db.ScalarAsync<int>($"SELECT `kills` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", steam));
		Assert.Equal(1600, await db.ScalarAsync<int>($"SELECT `value` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", steam));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT `rank` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", steam));
		Assert.Equal(3600, await db.ScalarAsync<int>($"SELECT `playtime` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", steam));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Purge_removes_inactive_players_and_their_orphaned_rows(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore(levelRanks: true);
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.LoadOrCreateAsync(Bob, "Bob", "None", 0);
		await store.SaveAsync([Snapshot(Alice, 10, reset: DateTime.UtcNow), Snapshot(Bob, 20)]);
		await store.TryClaimRewardAsync(Alice, "reward-1", "Gold", "say hi");

		foreach (string table in new[] { db.Tables.Ranks, db.Tables.Stats, db.Tables.Times })
			await db.AgeLastSeenAsync(table, Alice.ToString(), 45);

		string aliceSteam2 = new CounterStrikeSharp.API.Modules.Entities.SteamID(Alice).SteamId2.Replace("STEAM_0", "STEAM_1");
		await db.ExecuteAsync($"UPDATE `{db.Tables.LevelRanks}` SET `lastconnect` = 1000 WHERE `steam` = @Steam;", new { Steam = aliceSteam2 });
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Rewards}`;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Resets}`;"));

		await store.PurgeAsync(30);

		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Ranks}`;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Stats}`;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Times}`;"));
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Rewards}`;"));
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Resets}`;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.LevelRanks}`;"));

		// Zero disables purging.
		await db.AgeLastSeenAsync(db.Tables.Ranks, Bob.ToString(), 400);
		await store.PurgeAsync(0);
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Ranks}`;"));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task LevelRanks_mirror_uses_steam2_ids_and_k4_values(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore(levelRanks: true);
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		int[] stats = Stats((StatField.Kills, 9), (StatField.Deaths, 4), (StatField.HitsGiven, 30), (StatField.RoundWin, 6));
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Silver II", 1, 1600, stats, [3600, 0, 0, 0, 0, 0], null)]);

		string steam2 = new CounterStrikeSharp.API.Modules.Entities.SteamID(Alice).SteamId2.Replace("STEAM_0", "STEAM_1");
		Assert.StartsWith("STEAM_1:", steam2);
		Assert.Equal(1600, await db.ScalarAsync<int>($"SELECT `value` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", new { Steam = steam2 }));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT `rank` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", new { Steam = steam2 }));
		Assert.Equal(30, await db.ScalarAsync<int>($"SELECT `hits` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", new { Steam = steam2 }));
		Assert.Equal(3600, await db.ScalarAsync<int>($"SELECT `playtime` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", new { Steam = steam2 }));
		Assert.True(await db.ScalarAsync<long>($"SELECT `lastconnect` FROM `{db.Tables.LevelRanks}` WHERE `steam` = @Steam;", new { Steam = steam2 }) > 1_700_000_000);
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Initialization_is_repeatable_and_records_the_schema_version(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore(levelRanks: true);

		await store.InitializeAsync();
		await store.InitializeAsync();

		Assert.Equal(2, await db.ScalarAsync<int>($"SELECT `version` FROM `{db.Tables.Schema}` WHERE `id` = 1;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Schema}`;"));
	}

	[Theory, MemberData(nameof(Databases))]
	public async Task Player_names_with_quotes_and_unicode_round_trip(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();

		const string name = "O'Brien \"; DROP TABLE x; -- 🎯 Ünal";
		await store.LoadOrCreateAsync(Alice, name, "None", 0);

		Assert.Equal(name, await db.ScalarAsync<string>($"SELECT `name` FROM `{db.Tables.Ranks}` WHERE `steam_id` = @Id;", new { Id = Alice.ToString() }));
	}
}
