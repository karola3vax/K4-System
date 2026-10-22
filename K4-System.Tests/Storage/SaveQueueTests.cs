namespace K4System.Tests.Storage;

using K4System.Storage;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class SaveQueueTests
{
	private static PlayerSnapshot Snapshot(ulong steamId, int points, DateTime? reset = null)
		=> new(steamId, "Player", "Rank", 0, points, null, null, reset);

	[Fact]
	public async Task Saves_the_newest_snapshot_per_player()
	{
		List<PlayerSnapshot> written = [];
		TaskCompletionSource gate = new();
		SaveQueue queue = new(async batch =>
		{
			await gate.Task;
			lock (written)
				written.AddRange(batch);
		}, NullLogger.Instance);

		Task<bool> first = queue.Enqueue(Snapshot(1, 10));
		Task<bool> second = queue.Enqueue([Snapshot(1, 20), Snapshot(2, 5)]);
		gate.SetResult();

		Assert.True(await first);
		Assert.True(await second);
		Assert.True(queue.Complete(TimeSpan.FromSeconds(5)));

		// Player 1 is written at most twice, and the last write has the newest points.
		Assert.Equal(20, written.Last(snapshot => snapshot.SteamId == 1).Points);
		Assert.Contains(written, snapshot => snapshot.SteamId == 2);
	}

	[Fact]
	public async Task Failed_saves_stay_queued_and_keep_reset_metadata()
	{
		int calls = 0;
		List<PlayerSnapshot> written = [];
		SaveQueue queue = new(batch =>
		{
			// Fail every attempt of the first save, succeed afterwards.
			if (Interlocked.Increment(ref calls) <= 4)
				throw new InvalidOperationException("database down");

			lock (written)
				written.AddRange(batch);
			return Task.CompletedTask;
		}, NullLogger.Instance);

		DateTime resetAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
		Assert.False(await queue.Enqueue(Snapshot(7, 0, resetAt)));

		// A newer snapshot without reset metadata must not drop the pending reset.
		Assert.True(await queue.Enqueue(Snapshot(7, 50)));

		PlayerSnapshot saved = Assert.Single(written);
		Assert.Equal(50, saved.Points);
		Assert.Equal(resetAt, saved.PersonalRankResetUtc);
		Assert.True(queue.Complete(TimeSpan.FromSeconds(5)));
	}

	private static readonly int[] FastRetries = [1, 1, 1];

	[Fact]
	public async Task One_bad_snapshot_does_not_block_the_others()
	{
		List<ulong> written = [];
		SaveQueue queue = new(batch =>
		{
			if (batch.Any(snapshot => snapshot.SteamId == 99))
				throw new InvalidOperationException("bad row");

			lock (written)
				written.AddRange(batch.Select(snapshot => snapshot.SteamId));
			return Task.CompletedTask;
		}, NullLogger.Instance, FastRetries);

		Task<bool> good = queue.Enqueue(Snapshot(1, 10));
		Task<bool> both = queue.Enqueue([Snapshot(2, 20), Snapshot(99, 5)]);

		Assert.True(await good);
		Assert.False(await both);
		Assert.True(await queue.FlushAsync(2));
		lock (written)
			Assert.Equal([1UL, 2UL], written.Order());

		// The bad one keeps failing; unloading still finishes.
		Assert.True(queue.Complete(TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task Snapshots_queued_while_unloading_get_an_attempt_after_a_failed_batch()
	{
		int calls = 0;
		TaskCompletionSource gate = new();
		List<ulong> written = [];
		SaveQueue queue = new(async batch =>
		{
			// The first batch fails every attempt; the database is back afterwards.
			if (Interlocked.Increment(ref calls) <= FastRetries.Length + 1)
			{
				await gate.Task;
				throw new InvalidOperationException("database down");
			}

			lock (written)
				written.AddRange(batch.Select(snapshot => snapshot.SteamId));
		}, NullLogger.Instance, FastRetries);

		Task<bool> first = queue.Enqueue(Snapshot(1, 10));
		await Task.Delay(50);
		Task<bool> last = queue.Enqueue(Snapshot(2, 20));
		Task<bool> completed = Task.Run(() => queue.Complete(TimeSpan.FromSeconds(5)));
		await Task.Delay(50);
		gate.SetResult();

		Assert.True(await completed);
		Assert.True(await last);
		Assert.False(await first);
		lock (written)
			Assert.Equal([1UL, 2UL], written.Order());
	}

	[Fact]
	public async Task Flush_waits_for_a_players_pending_save()
	{
		TaskCompletionSource gate = new();
		SaveQueue queue = new(_ => gate.Task, NullLogger.Instance);

		Task<bool> saving = queue.Enqueue(Snapshot(3, 1));
		Task<bool> flush = queue.FlushAsync(3);

		Assert.False(flush.IsCompleted);
		Assert.True(await queue.FlushAsync(4));

		gate.SetResult();
		Assert.True(await saving);
		Assert.True(await flush);
		Assert.True(queue.Complete(TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public void Complete_drains_queued_snapshots()
	{
		List<PlayerSnapshot> written = [];
		SaveQueue queue = new(async batch =>
		{
			await Task.Delay(50);
			lock (written)
				written.AddRange(batch);
		}, NullLogger.Instance);

		for (ulong id = 1; id <= 20; id++)
			queue.Enqueue(Snapshot(id, (int)id));

		Assert.True(queue.Complete(TimeSpan.FromSeconds(10)));
		Assert.Equal(20, written.Select(snapshot => snapshot.SteamId).Distinct().Count());
	}

	[Fact]
	public void Retry_delays_are_bounded()
	{
		Assert.Equal(250, SaveQueue.RetryDelay(1));
		Assert.Equal(1000, SaveQueue.RetryDelay(2));
		Assert.Equal(3000, SaveQueue.RetryDelay(3));
		Assert.Equal(3000, SaveQueue.RetryDelay(99));
	}
}
