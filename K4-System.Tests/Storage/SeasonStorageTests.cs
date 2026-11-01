namespace K4System.Tests.Storage;

using K4System.Storage;

/// <summary>Seasons, weapon kills and the achievement showcase in both databases.</summary>
public sealed class SeasonStorageTests
{
	private const ulong Alice = 76561198000000001;
	private const ulong Bob = 76561198000000002;
	private const ulong Carol = 76561198000000003;
	private const string November = "2026-11-01";
	private const string December = "2026-12-01";

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	private static PlayerSnapshot Snap(ulong steamId, string name, int points, string? season, string rank = "Silver I")
		=> new(steamId, name, rank, 0, points, null, null, null, Season: season);

	private static async Task<SqlPlayerStore> ReadyAsync(TestDatabase db, params ulong[] players)
	{
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		foreach (ulong player in players)
			await store.LoadOrCreateAsync(player, player.ToString(), "None", 0);
		return store;
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task The_first_season_is_written_once_and_kept(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db);

		Assert.Equal(November, await store.EnsureSeasonAsync(November));
		// Another server starting a month later must not move the stored season; ending it is the season check's job.
		Assert.Equal(November, await store.EnsureSeasonAsync(December));
		Assert.Equal(November, await store.GetSeasonAsync());
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Loading_reports_the_stored_season(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db);
		await store.EnsureSeasonAsync(November);

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		Assert.Equal(November, record!.Season);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Ending_a_season_keeps_final_results_and_resets_points(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice, Bob, Carol);
		await store.EnsureSeasonAsync(November);

		// Alice peaked at 900 and ended at 700; Bob ended at 400. Carol has points from before seasons and never played.
		await store.SaveAsync([Snap(Alice, "Alice", 900, November, "Gold Nova")]);
		await store.SaveAsync([Snap(Alice, "Alice", 700, November, "Silver Elite"), Snap(Bob, "Bob", 400, November)]);
		await db.ExecuteAsync($"UPDATE `{db.Tables.Ranks}` SET `points` = 250 WHERE `steam_id` = @Id;", new { Id = Carol.ToString() });
		await db.ExecuteAsync($"INSERT INTO `{db.Tables.Rewards}` (`steam_id`, `reward_id`, `rank_name`, `command`, `claimed_at`) VALUES (@Id, 'r1', 'Gold Nova', 'say hi', CURRENT_TIMESTAMP);", new { Id = Alice.ToString() });

		Assert.True(await store.EndSeasonAsync(November, December, 0, "None"));

		Assert.Equal(December, await store.GetSeasonAsync());
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Ranks}` WHERE `points` <> 0 OR `rank` <> 'None';"));
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Rewards}`;"));

		Assert.Equal([November], await store.GetFinishedSeasonsAsync(10));
		(List<TopEntry> top, int players) = await store.GetSeasonTopAsync(November, 10);
		Assert.Equal(3, players);
		Assert.Equal(["Alice", "Bob", Carol.ToString()], top.Select(entry => entry.Name));
		Assert.Equal([700, 400, 250], top.Select(entry => entry.Points));
		Assert.Equal(Alice, top[0].SteamId);

		SeasonResult alice = Assert.Single(await store.GetPlayerSeasonsAsync(Alice, 5));
		Assert.Equal(new SeasonResult(November, 700, "Silver Elite", 900, 1, 3), alice);
		SeasonResult bob = Assert.Single(await store.GetPlayerSeasonsAsync(Bob, 5));
		Assert.Equal(2, bob.Placement);
		Assert.Equal(400, bob.PeakPoints);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task The_LevelRanks_mirror_is_reset_with_the_season(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore(levelRanks: true);
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.EnsureSeasonAsync(November);
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Gold Nova", 4, 1600, null, null, null, Season: November)]);

		await store.EndSeasonAsync(November, December, 0, "None");

		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT `value` FROM `{db.Tables.LevelRanks}`;"));
		Assert.Equal(-1, await db.ScalarAsync<int>($"SELECT `rank` FROM `{db.Tables.LevelRanks}`;"));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task A_season_ends_only_once_when_two_servers_try(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);
		await store.EnsureSeasonAsync(November);
		await store.SaveAsync([Snap(Alice, "Alice", 500, November)]);

		Assert.True(await store.EndSeasonAsync(November, December, 0, "None"));
		await store.SaveAsync([Snap(Alice, "Alice", 120, December)]);
		// The second server still thinks it is November: nothing may change.
		Assert.False(await store.EndSeasonAsync(November, December, 0, "None"));

		Assert.Equal(120, (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!.Points);
		Assert.Equal(500, Assert.Single(await store.GetPlayerSeasonsAsync(Alice, 5)).Points);
	}

	// Regression guard: a save captured before the season ended must not write the old points over the reset.
	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Points_saved_for_an_ended_season_do_not_undo_the_reset(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);
		await store.EnsureSeasonAsync(November);
		await store.SaveAsync([Snap(Alice, "Alice", 800, November)]);
		await store.EndSeasonAsync(November, December, 0, "None");

		await store.SaveAsync([Snap(Alice, "Alice", 850, November) with { Stats = Enumerable.Repeat(3, Players.StatColumns.Names.Count).ToArray() }]);

		PlayerRecord record = (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!;
		Assert.Equal(0, record.Points);
		// Everything that is not points is still saved.
		Assert.Equal(3, record.Stats[0]);
		Assert.Equal(800, Assert.Single(await store.GetPlayerSeasonsAsync(Alice, 5)).Points);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Without_seasons_points_save_as_before(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);
		await store.EnsureSeasonAsync(November);

		await store.SaveAsync([Snap(Alice, "Alice", 300, season: null)]);

		Assert.Equal(300, (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!.Points);
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Seasons}`;"));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Weapon_kills_and_the_showcase_load_back(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);

		await store.SaveAsync([Snap(Alice, "Alice", 10, null) with { WeaponKills = new Dictionary<string, int> { ["ak47"] = 5, ["awp"] = 2 }, Showcase = "kills-100,mvps-50" }]);
		await store.SaveAsync([Snap(Alice, "Alice", 10, null) with { WeaponKills = new Dictionary<string, int> { ["ak47"] = 6 } }]);

		PlayerRecord record = (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!;
		Assert.Equal(6, record.WeaponKills!["ak47"]);
		Assert.Equal(2, record.WeaponKills["awp"]);
		Assert.Equal(["kills-100", "mvps-50"], record.Showcase);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Profiles_of_unknown_players_are_null_and_create_nothing(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);
		await store.SaveAsync([Snap(Alice, "Alice", 42, null, "Gold Nova") with { Showcase = "kills-100" }]);

		Assert.Null(await store.GetProfileAsync(Bob, 3));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Ranks}`;"));

		ProfileRecord profile = (await store.GetProfileAsync(Alice, 3))!;
		Assert.Equal("Alice", profile.Name);
		Assert.Equal("Gold Nova", profile.Rank);
		Assert.Equal(42, profile.Record.Points);
		Assert.Equal(1, profile.Total);
		Assert.Equal(["kills-100"], profile.Record.Showcase);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Purged_players_lose_weapon_kills_and_showcase_but_seasons_stay(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice);
		await store.EnsureSeasonAsync(November);
		await store.SaveAsync([Snap(Alice, "Alice", 90, November) with { WeaponKills = new Dictionary<string, int> { ["ak47"] = 1 }, Showcase = "kills-100" }]);
		await store.EndSeasonAsync(November, December, 0, "None");
		foreach (string table in new[] { db.Tables.Ranks, db.Tables.Stats, db.Tables.Times })
			await db.AgeLastSeenAsync(table, Alice.ToString(), 60);

		await store.PurgeAsync(30);

		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.WeaponKills}`;"));
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Profiles}`;"));
		// Past leaderboards keep everyone who played.
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Seasons}`;"));
	}
}
