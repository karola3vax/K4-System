namespace K4System.Tests.Storage;

using K4System.Missions;
using K4System.Storage;

/// <summary>Mission rows and streak state, against SQLite and (with K4_TEST_MYSQL) MySQL.</summary>
public sealed class MissionStorageTests
{
	private const ulong Alice = 76561198000000001;

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	private static PlayerSnapshot WithMissions(MissionSnapshot missions)
		=> new(Alice, "Alice", "Silver I", 0, 100, null, null, null, missions);

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Missions_and_streak_round_trip(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		MissionRecord empty = await store.LoadMissionsAsync(Alice, "2026-10-01");
		Assert.Empty(empty.Slots);
		Assert.Equal((0, 0, (string?)null), (empty.RerollsUsed, empty.Streak, empty.LastCompletedDay));

		await store.SaveAsync([WithMissions(new MissionSnapshot("2026-10-01",
			[new(0, "kills-15", 15, true), new(1, "mvps-3", 1, false), new(2, "awp-kills-10", 0, false)],
			RerollsUsed: 1, Streak: 3, LastCompletedDay: "2026-09-30"))]);

		MissionRecord today = await store.LoadMissionsAsync(Alice, "2026-10-01");
		Assert.Equal(["kills-15", "mvps-3", "awp-kills-10"], today.Slots.Select(slot => slot.MissionId));
		Assert.True(today.Slots[0].Completed);
		Assert.Equal(1, today.Slots[1].Progress);
		Assert.Equal((1, 3, "2026-09-30"), (today.RerollsUsed, today.Streak, today.LastCompletedDay));

		// The next day has no rows yet, keeps the streak, and gets its reroll back.
		MissionRecord tomorrow = await store.LoadMissionsAsync(Alice, "2026-10-02");
		Assert.Empty(tomorrow.Slots);
		Assert.Equal((0, 3), (tomorrow.RerollsUsed, tomorrow.Streak));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Later_saves_update_progress_in_place(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		await store.SaveAsync([WithMissions(new MissionSnapshot("2026-10-01", [new(0, "kills-15", 3, false)], 0, 0, null))]);
		await store.SaveAsync([WithMissions(new MissionSnapshot("2026-10-01", [new(0, "kills-15", 9, false)], 0, 0, null))]);

		MissionSlotSnapshot slot = Assert.Single((await store.LoadMissionsAsync(Alice, "2026-10-01")).Slots);
		Assert.Equal(9, slot.Progress);
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Missions}`;"));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Purge_drops_old_mission_days_and_removed_players(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		string today = DateTime.UtcNow.ToString(MissionRules.DayFormat, System.Globalization.CultureInfo.InvariantCulture);
		await store.SaveAsync([WithMissions(new MissionSnapshot("2020-01-01", [new(0, "kills-15", 1, false)], 0, 0, null))]);
		await store.SaveAsync([WithMissions(new MissionSnapshot(today, [new(0, "kills-15", 1, false)], 0, 0, null))]);

		await store.PurgeAsync(30);
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Missions}`;"));

		await db.AgeLastSeenAsync(db.Tables.Ranks, Alice.ToString(), 45);
		await store.PurgeAsync(30);
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Missions}`;"));
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.MissionState}`;"));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Old_mission_days_are_purged_even_when_inactive_players_are_kept(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.SaveAsync([WithMissions(new MissionSnapshot("2020-01-01", [new(0, "kills-15", 1, false)], 0, 0, null))]);

		await store.PurgeAsync(0);

		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Missions}`;"));
		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Ranks}`;"));
	}
}
