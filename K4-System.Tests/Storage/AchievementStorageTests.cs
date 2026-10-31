namespace K4System.Tests.Storage;

using K4System.Storage;

/// <summary>Unlocked achievements are stored with the player's save and come back on load.</summary>
public sealed class AchievementStorageTests
{
	private const ulong Alice = 76561198000000001;

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Unlocks_saved_with_a_snapshot_load_back_once_each(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Silver I", 0, 100, null, null, null, NewAchievements: ["kills-100"])]);
		// The same unlock again (a retried save) must not fail or duplicate.
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Silver I", 0, 600, null, null, null, NewAchievements: ["kills-100", "kills-1000"])]);

		PlayerRecord? record = await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);

		Assert.NotNull(record);
		Assert.Equal(["kills-100", "kills-1000"], record.Achievements.Order());
		Assert.Equal(600, record.Points);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Purged_players_lose_their_unlocks(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.SaveAsync([new PlayerSnapshot(Alice, "Alice", "Silver I", 0, 100, null, null, null, NewAchievements: ["kills-100"])]);
		foreach (string table in new[] { db.Tables.Ranks, db.Tables.Stats, db.Tables.Times })
			await db.AgeLastSeenAsync(table, Alice.ToString(), 60);

		await store.PurgeAsync(30);

		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Achievements}`;"));
	}
}
