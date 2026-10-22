namespace K4System.Storage;

using System.Data.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes player snapshots from one background worker, so saves never overlap and never block
/// the game thread. A newer snapshot replaces an older unsaved one. When a batch keeps failing,
/// each snapshot is tried on its own so one bad row cannot hold back everyone else. Failed
/// snapshots stay queued and are retried with the next save or after a short wait.
/// </summary>
internal sealed class SaveQueue
{
	private static readonly int[] RetryDelaysMilliseconds = [250, 1000, 3000];
	private static readonly TimeSpan IdleRetryDelay = TimeSpan.FromSeconds(30);

	private readonly Func<IReadOnlyCollection<PlayerSnapshot>, Task> save;
	private readonly ILogger logger;
	private readonly int[] retryDelays;
	private readonly object gate = new();
	private readonly SemaphoreSlim signal = new(0);
	private readonly Task worker;

	private Dictionary<ulong, Entry> pending = [];
	private Dictionary<ulong, Task<bool>> inFlight = [];
	private bool completing;
	private bool finalAttemptMade;
	private bool retryScheduled;

	/// <summary>The background writer. Finished once the queue has drained after <see cref="Complete"/>.</summary>
	public Task Worker => worker;

	private sealed class Entry(PlayerSnapshot snapshot)
	{
		public PlayerSnapshot Snapshot { get; set; } = snapshot;
		public TaskCompletionSource<bool> Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	/// <param name="retryDelaysMilliseconds">Waits between attempts of one batch; tests pass short ones.</param>
	public SaveQueue(Func<IReadOnlyCollection<PlayerSnapshot>, Task> save, ILogger logger, int[]? retryDelaysMilliseconds = null)
	{
		this.save = save;
		this.logger = logger;
		retryDelays = retryDelaysMilliseconds ?? RetryDelaysMilliseconds;
		worker = Task.Run(RunAsync);
	}

	internal static int RetryDelay(int failedAttempt)
		=> RetryDelaysMilliseconds[Math.Clamp(failedAttempt, 1, RetryDelaysMilliseconds.Length) - 1];

	/// <summary>Queues snapshots. The task completes with true once all of them are saved.</summary>
	public Task<bool> Enqueue(IEnumerable<PlayerSnapshot> snapshots)
	{
		List<Task<bool>> waits = [];

		lock (gate)
		{
			foreach (PlayerSnapshot snapshot in snapshots)
			{
				if (pending.TryGetValue(snapshot.SteamId, out Entry? entry))
					entry.Snapshot = snapshot.MergeOnto(entry.Snapshot);
				else
					pending[snapshot.SteamId] = entry = new Entry(snapshot);

				waits.Add(entry.Saved.Task);
			}
		}

		if (waits.Count == 0)
			return Task.FromResult(true);

		signal.Release();
		return AllSucceeded(waits);
	}

	public Task<bool> Enqueue(PlayerSnapshot snapshot) => Enqueue([snapshot]);

	/// <summary>Waits until nothing for this player is queued or being written. False when that save failed.</summary>
	public Task<bool> FlushAsync(ulong steamId)
	{
		List<Task<bool>> waits = [];

		lock (gate)
		{
			if (inFlight.TryGetValue(steamId, out Task<bool>? writing))
				waits.Add(writing);
			if (pending.TryGetValue(steamId, out Entry? entry))
				waits.Add(entry.Saved.Task);
		}

		if (waits.Count == 0)
			return Task.FromResult(true);

		signal.Release();
		return AllSucceeded(waits);
	}

	/// <summary>Stops accepting work and waits for the queue to drain. Used when the plugin unloads.</summary>
	public bool Complete(TimeSpan timeout)
	{
		lock (gate)
			completing = true;

		signal.Release();

		try
		{
			return worker.Wait(timeout);
		}
		catch (AggregateException ex)
		{
			logger.LogError("The save worker stopped with an error: {ErrorMessage}", ex.InnerException?.Message ?? ex.Message);
			return false;
		}
	}

	private static async Task<bool> AllSucceeded(List<Task<bool>> waits)
	{
		bool[] results = await Task.WhenAll(waits);
		return results.All(result => result);
	}

	private async Task RunAsync()
	{
		while (true)
		{
			await signal.WaitAsync();

			Dictionary<ulong, Entry> batch;

			lock (gate)
			{
				batch = pending;
				pending = [];
				inFlight = batch.ToDictionary(item => item.Key, item => item.Value.Saved.Task);

				if (batch.Count == 0)
				{
					if (completing)
						return;

					continue;
				}
			}

			HashSet<ulong> failed = await SaveBatchAsync(batch);

			lock (gate)
			{
				inFlight = [];

				// Keep failed snapshots queued unless a newer one arrived meanwhile.
				foreach (ulong steamId in failed)
				{
					PlayerSnapshot snapshot = batch[steamId].Snapshot;
					if (pending.TryGetValue(steamId, out Entry? newer))
						newer.Snapshot = newer.Snapshot.MergeOnto(snapshot);
					else
						pending[steamId] = new Entry(snapshot);
				}
			}

			foreach ((ulong steamId, Entry entry) in batch)
				entry.Saved.TrySetResult(!failed.Contains(steamId));

			lock (gate)
			{
				if (completing)
				{
					if (pending.Count == 0)
						return;

					// Snapshots queued while unloading get one full attempt even when an earlier batch failed.
					if (failed.Count == 0 || !finalAttemptMade)
					{
						finalAttemptMade |= failed.Count > 0;
						signal.Release();
						continue;
					}

					logger.LogError("{Count} player snapshot(s) could not be saved before K4-System unloaded.", pending.Count);

					// Nothing runs after this, so release everyone still waiting.
					foreach (Entry entry in pending.Values)
						entry.Saved.TrySetResult(false);
					pending.Clear();
					return;
				}

				if (failed.Count > 0)
					ScheduleRetry();
			}
		}
	}

	// Without this, failed snapshots would wait for the next save request, which may never come on an empty server.
	private void ScheduleRetry()
	{
		if (retryScheduled)
			return;

		retryScheduled = true;
		_ = Task.Delay(IdleRetryDelay).ContinueWith(_ =>
		{
			lock (gate)
				retryScheduled = false;
			signal.Release();
		}, TaskScheduler.Default);
	}

	/// <summary>Saves a batch and returns the players whose snapshot could not be saved.</summary>
	private async Task<HashSet<ulong>> SaveBatchAsync(Dictionary<ulong, Entry> batch)
	{
		Exception? error = await SaveWithRetryAsync(batch.Values.Select(entry => entry.Snapshot).ToList());
		if (error is null)
			return [];

		// A connection problem fails every row alike; splitting would only multiply timeouts.
		if (batch.Count == 1 || error is DbException { IsTransient: true } or TimeoutException)
			return [.. batch.Keys];

		// One bad row fails the whole transaction, so find it by saving each player on their own.
		HashSet<ulong> failed = [];
		bool anySaved = false;
		foreach ((ulong steamId, Entry entry) in batch)
		{
			// Three failures in a row and nothing saved looks like an outage, not bad rows.
			if (!anySaved && failed.Count >= 3)
			{
				failed.Add(steamId);
				continue;
			}

			try
			{
				await save([entry.Snapshot]);
				anySaved = true;
			}
			catch (Exception ex)
			{
				failed.Add(steamId);
				logger.LogError("Unable to save player {SteamId}: {ErrorMessage}", steamId, ex.Message);
			}
		}

		return failed;
	}

	/// <summary>Null when saved, otherwise the last error.</summary>
	private async Task<Exception?> SaveWithRetryAsync(IReadOnlyCollection<PlayerSnapshot> snapshots)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				await save(snapshots);
				return null;
			}
			catch (Exception ex)
			{
				if (attempt > retryDelays.Length)
				{
					logger.LogError("Unable to save {Count} player snapshot(s) after {Attempts} attempts. They stay queued in memory: {ErrorMessage}", snapshots.Count, attempt, ex.Message);
					return ex;
				}

				int delay = retryDelays[attempt - 1];
				logger.LogWarning("Unable to save {Count} player snapshot(s) on attempt {Attempt}. Retrying in {Delay} ms: {ErrorMessage}", snapshots.Count, attempt, delay, ex.Message);
				await Task.Delay(delay);
			}
		}
	}
}
