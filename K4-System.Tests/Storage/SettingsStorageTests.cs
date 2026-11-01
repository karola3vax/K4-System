namespace K4System.Tests.Storage;

using K4System.Storage;

/// <summary>The per-player settings in k4settings (panel pop-ups, point chat messages) in both databases.</summary>
public sealed class SettingsStorageTests
{
	private const ulong Alice = 76561198000000001;
	private const ulong Bob = 76561198000000002;

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	private static PlayerSnapshot Snap(ulong steamId, bool? toasts = null, bool? pointMessages = null)
		=> new(steamId, steamId.ToString(), "None", -1, 0, null, null, null, Toasts: toasts, PointMessages: pointMessages);

	private static async Task<SqlPlayerStore> ReadyAsync(TestDatabase db, params ulong[] players)
	{
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		foreach (ulong player in players)
			await store.LoadOrCreateAsync(player, player.ToString(), "None", 0);
		return store;
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task New_players_have_both_settings_on_and_no_row(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db);

		PlayerRecord record = (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!;
		await store.SaveAsync([Snap(Alice)]);

		Assert.True(record.Toasts);
		Assert.True(record.PointMessages);
		Assert.Equal(0, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Settings}`;"));
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Settings_round_trip_and_an_unchanged_setting_keeps_its_value(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice, Bob);

		await store.SaveAsync([Snap(Alice, toasts: false), Snap(Bob, pointMessages: false)]);
		// Changing only the point messages must not turn toasts back on.
		await store.SaveAsync([Snap(Alice, pointMessages: false)]);

		PlayerRecord alice = (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!;
		PlayerRecord bob = (await store.LoadOrCreateAsync(Bob, "Bob", "None", 0))!;
		Assert.False(alice.Toasts);
		Assert.False(alice.PointMessages);
		Assert.True(bob.Toasts);
		Assert.False(bob.PointMessages);

		await store.SaveAsync([Snap(Alice, toasts: true)]);
		alice = (await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!;
		Assert.True(alice.Toasts);
		Assert.False(alice.PointMessages);
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Purged_players_lose_their_settings_and_active_players_keep_them(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = await ReadyAsync(db, Alice, Bob);
		await store.SaveAsync([Snap(Alice, toasts: false), Snap(Bob, toasts: false)]);
		foreach (string table in new[] { db.Tables.Ranks, db.Tables.Stats, db.Tables.Times })
			await db.AgeLastSeenAsync(table, Alice.ToString(), 60);

		await store.PurgeAsync(30);

		Assert.Equal(1, await db.ScalarAsync<int>($"SELECT COUNT(*) FROM `{db.Tables.Settings}`;"));
		Assert.False((await store.LoadOrCreateAsync(Bob, "Bob", "None", 0))!.Toasts);
		Assert.True((await store.LoadOrCreateAsync(Alice, "Alice", "None", 0))!.Toasts);
	}

	[Fact]
	public void A_newer_snapshot_keeps_an_older_unsaved_setting()
	{
		PlayerSnapshot older = Snap(Alice, toasts: false, pointMessages: true);
		PlayerSnapshot newer = Snap(Alice, pointMessages: false);

		PlayerSnapshot merged = newer.MergeOnto(older);

		Assert.False(merged.Toasts);
		Assert.False(merged.PointMessages);
	}
}
