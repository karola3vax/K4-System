namespace K4System.Tests.Storage;

using System.Text.Json;
using K4System.Storage;
using K4System.Tests.Domain;

/// <summary>The point audit log behind css_k4_history, on SQLite and (with K4_TEST_MYSQL) MySQL.</summary>
public sealed class PointAuditStorageTests
{
	private const ulong Alice = 76561198000000001;
	private const ulong Bob = 76561198000000002;

	private static TestDatabase Create(string kind) => kind == "mysql" ? new MySqlTestDatabase() : new SqliteTestDatabase();

	private sealed record LoggedChange(int Delta, int PointsAfter, string Source, string Actor);

	private sealed record AuditCase(string Name, LoggedChange[] Changes, int Count, string[] ExpectedSources);

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Eval_histories_come_back_newest_first_and_only_for_that_player(string kind)
	{
		AuditCase[] cases = JsonSerializer.Deserialize<AuditCase[]>(File.ReadAllText(TestData.EvalPath("point-audit.json")))!;
		Assert.NotEmpty(cases);

		foreach (AuditCase test in cases)
		{
			await using TestDatabase db = Create(kind);
			SqlPlayerStore store = db.CreateStore();
			await store.InitializeAsync();

			// Bob's change between Alice's must never show in Alice's history.
			foreach (LoggedChange change in test.Changes)
			{
				await store.LogPointChangesAsync([new PointChange(Alice, change.Delta, change.PointsAfter, change.Source, change.Actor)]);
				await store.LogPointChangesAsync([new PointChange(Bob, 1, 1, "api", "shared API")]);
			}

			List<PointHistoryEntry> history = await store.GetPointHistoryAsync(Alice, test.Count);

			Assert.True(test.ExpectedSources.SequenceEqual(history.Select(entry => entry.Source)), $"{test.Name}: got [{string.Join(", ", history.Select(entry => entry.Source))}]");
			Assert.All(history, entry => Assert.True(entry.Actor.Length <= 255));
			Assert.All(history, entry => Assert.True(Math.Abs((DateTime.UtcNow - entry.ChangedUtc).TotalMinutes) < 10, $"{test.Name}: {entry.ChangedUtc:o} is not stored as UTC"));
		}
	}

	[Theory, MemberData(nameof(SqlPlayerStoreTests.Databases), MemberType = typeof(SqlPlayerStoreTests))]
	public async Task Purge_drops_old_entries_and_entries_of_purged_players(string kind)
	{
		await using TestDatabase db = Create(kind);
		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();
		await store.LoadOrCreateAsync(Alice, "Alice", "None", 0);
		await store.LoadOrCreateAsync(Bob, "Bob", "None", 0);
		await store.LogPointChangesAsync([new PointChange(Alice, 5, 5, "give", "console"), new PointChange(Bob, 5, 5, "give", "console")]);
		await store.LogPointChangesAsync([new PointChange(Alice, 7, 12, "give", "console")]);

		// Alice's first entry is older than the log keeps; Bob was not seen for longer than the purge allows.
		string oldStamp = kind == "mysql" ? "NOW() - INTERVAL 100 DAY" : "datetime('now', '-100 days')";
		await db.ExecuteAsync($"UPDATE `{db.Tables.PointLog}` SET `changed_at` = {oldStamp} WHERE `steam_id` = @Id AND `delta` = 5;", new { Id = Alice.ToString() });
		foreach (string table in new[] { db.Tables.Ranks, db.Tables.Stats, db.Tables.Times })
			await db.AgeLastSeenAsync(table, Bob.ToString(), 60);

		await store.PurgeAsync(30);

		Assert.Equal([7], (await store.GetPointHistoryAsync(Alice, 10)).Select(entry => entry.Delta));
		Assert.Empty(await store.GetPointHistoryAsync(Bob, 10));
	}
}
