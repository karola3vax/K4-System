namespace K4System.Services;

using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using K4System.Players;
using K4System.Ranks;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// One-time rank reward commands. A reward is claimed in the database before its command runs and
/// marked complete afterwards, so an interrupted reward resumes on the next connect.
/// </summary>
internal sealed class RewardService(
	RankLadder ladder,
	PlayerRegistry players,
	SqlPlayerStore store,
	GameThread gameThread,
	BackgroundWork background,
	ILogger logger)
{
	private const int Attempts = 4;

	// Rewards being claimed or run right now, so a retry cannot start a second copy.
	private readonly ConcurrentDictionary<string, byte> active = new(StringComparer.Ordinal);

	public void QueuePromotion(K4Player player, RankDefinition oldRank, RankDefinition newRank)
	{
		foreach (RewardCommand reward in RankRewards.ForPromotion(oldRank, newRank, ContextFor(player, newRank)))
		{
			player.PendingRewards[reward.RewardId] = reward;
			Start(player.SteamId, reward);
		}
	}

	/// <summary>Retries rewards whose claim or command failed earlier.</summary>
	public void RetryPending(K4Player player)
	{
		foreach (RewardCommand reward in player.PendingRewards.Values.ToArray())
			Start(player.SteamId, reward);
	}

	/// <summary>A personal rank reset forgets unfinished rewards; the save then deletes their claims.</summary>
	public void CancelPending(K4Player player)
	{
		player.PendingRewards.Clear();
		player.RewardResetVersion++;
	}

	/// <summary>Resumes rewards that were claimed but never finished, for example after a crash or disconnect.</summary>
	public void ResumeStored(K4Player player)
	{
		ulong steamId = player.SteamId;

		background.Run(async () =>
		{
			List<StoredReward> stored = await store.GetPendingRewardsAsync(steamId);
			if (stored.Count == 0)
				return;

			gameThread.Post(() =>
			{
				K4Player? current = players.Get(steamId);
				if (current is null)
					return;

				foreach (StoredReward reward in stored)
				{
					RewardCommand? command = RankRewards.Rebuild(ladder, reward.RankName, reward.RewardId, rank => ContextFor(current, rank));

					if (command is null)
					{
						// The reward was removed from ranks.jsonc; close the claim so it stops coming back.
						background.Run(() => store.MarkRewardExecutedAsync(steamId, reward.RewardId), "Closing a removed rank reward");
						continue;
					}

					current.PendingRewards[reward.RewardId] = command.Value;
					Start(steamId, command.Value);
				}
			});
		}, $"Loading pending rank rewards for {steamId}");
	}

	private static RewardContext ContextFor(K4Player player, RankDefinition rank) => new(
		player.SteamId.ToString(),
		player.Controller.UserId?.ToString() ?? string.Empty,
		player.Name,
		rank.Name,
		rank.Id.ToString(),
		player.Rank?.Points.ToString() ?? "0");

	private static string Key(ulong steamId, string rewardId) => $"{steamId}:{rewardId}";

	private void Start(ulong steamId, RewardCommand reward)
	{
		string key = Key(steamId, reward.RewardId);
		if (!active.TryAdd(key, 0))
			return;

		int resetVersion = players.Get(steamId)?.RewardResetVersion ?? 0;

		background.Run(async () =>
		{
			bool handedOff = false;
			try
			{
				if (!await ClaimAsync(steamId, reward))
				{
					// Already delivered earlier: stop retrying it.
					gameThread.Post(() => players.Get(steamId)?.PendingRewards.Remove(reward.RewardId));
					return;
				}

				handedOff = true;
				gameThread.Post(() => Execute(steamId, reward, key, resetVersion));
			}
			finally
			{
				if (!handedOff)
					active.TryRemove(key, out _);
			}
		}, $"Rank reward '{reward.Command}'");
	}

	private async Task<bool> ClaimAsync(ulong steamId, RewardCommand reward)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return await store.TryClaimRewardAsync(steamId, reward.RewardId, reward.RankName, reward.CommandTemplate);
			}
			catch (Exception ex) when (attempt < Attempts)
			{
				logger.LogWarning("Unable to claim rank reward '{Command}' on attempt {Attempt}: {ErrorMessage}", reward.Command, attempt, ex.Message);
				await Task.Delay(SaveQueue.RetryDelay(attempt));
			}
		}
	}

	private void Execute(ulong steamId, RewardCommand reward, string key, int resetVersion)
	{
		K4Player? player = players.Get(steamId);

		if (player is not null && player.RewardResetVersion != resetVersion)
		{
			// The player reset their rank while this claim was being written. The reset's save
			// deletes claims, but this one may have landed after it, so remove it explicitly.
			background.Run(async () =>
			{
				try
				{
					await store.DeleteRewardClaimAsync(steamId, reward.RewardId);
				}
				finally
				{
					active.TryRemove(key, out _);
				}
			}, $"Discarding rank reward '{reward.Command}' after a reset");
			return;
		}

		if (player is null || !player.PendingRewards.ContainsKey(reward.RewardId))
		{
			// The player left or reset meanwhile. The claim stays pending and resumes on reconnect.
			active.TryRemove(key, out _);
			return;
		}

		try
		{
			Server.ExecuteCommand(reward.Command);
		}
		catch (Exception ex)
		{
			active.TryRemove(key, out _);
			logger.LogError("Rank reward command '{Command}' failed and remains pending: {ErrorMessage}", reward.Command, ex.Message);
			return;
		}

		// The command ran. It must not run again this session, even if saving its completion fails.
		player.PendingRewards.Remove(reward.RewardId);

		background.Run(async () =>
		{
			try
			{
				await MarkExecutedAsync(steamId, reward);
			}
			finally
			{
				active.TryRemove(key, out _);
			}
		}, $"Completing rank reward '{reward.Command}'");
	}

	private async Task MarkExecutedAsync(ulong steamId, RewardCommand reward)
	{
		for (int attempt = 1; attempt <= Attempts; attempt++)
		{
			try
			{
				await store.MarkRewardExecutedAsync(steamId, reward.RewardId);
				return;
			}
			catch (Exception ex)
			{
				if (attempt == Attempts)
				{
					logger.LogError("Rank reward command '{Command}' ran, but completion could not be saved. It may run again after reconnect: {ErrorMessage}", reward.Command, ex.Message);
					return;
				}

				await Task.Delay(SaveQueue.RetryDelay(attempt));
			}
		}
	}
}
