namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using K4System.Config;
using K4System.Missions;
using K4System.Players;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Loads players when they join, and captures and saves their data.</summary>
internal sealed class PlayerService(
	BasePlugin plugin,
	PluginConfig config,
	PlayerRegistry players,
	SqlPlayerStore store,
	SaveQueue saves,
	Task storageReady,
	RankService? ranks,
	RewardService? rewards,
	MissionService? missions,
	PlaytimeService playtime,
	GameState game,
	GameThread gameThread,
	BackgroundWork background,
	CountryLookup countries,
	Chat chat,
	ILogger logger)
{
	// CS2 reports this disconnect reason for players moving to the next map.
	private const int MapChangeDisconnectReason = 1;
	private const int LoadAttempts = 10;

	private PluginConfig General => config;

	// ---------------------------------------------------------------- loading

	public void QueueConnectedPlayersRefresh(float delaySeconds, int attempts = LoadAttempts)
	{
		if (gameThread.IsActive)
			plugin.AddTimer(delaySeconds, () => gameThread.PostWorldUpdate(() => RefreshConnectedPlayers(attempts)));
	}

	private void RefreshConnectedPlayers(int attempts)
	{
		try
		{
			foreach (CCSPlayerController controller in Utilities.GetPlayers())
			{
				if (controller?.IsValid == true && controller.Connected == PlayerConnectedState.Connected && !controller.IsHLTV)
					TryLoad(controller, attempts);
			}
		}
		catch (Exception ex) when (GameState.IsEntitySystemUnavailable(ex))
		{
			if (attempts > 0)
				plugin.AddTimer(1.0f, () => gameThread.PostWorldUpdate(() => RefreshConnectedPlayers(attempts - 1)));
			else
				logger.LogWarning("Connected-player refresh is waiting for the CS2 entity system: {ErrorMessage}", ex.Message);
		}
	}

	public void TryLoad(CCSPlayerController? controller, int attempts = LoadAttempts)
	{
		if (!gameThread.IsActive)
			return;

		try
		{
			if (controller is null || !controller.IsValid || controller.IsHLTV || controller.Connected != PlayerConnectedState.Connected)
				return;

			if (!controller.PlayerPawn.IsValid)
			{
				if (attempts > 0)
					plugin.AddTimer(1.0f, () => gameThread.PostWorldUpdate(() => TryLoad(controller, attempts - 1)));
				else
					logger.LogWarning("Player activation is waiting for a valid pawn for {PlayerName}.", controller.PlayerName);
				return;
			}

			if (players.IsLoaded(controller) || players.GetLoading(controller) is not null)
				return;

			K4Player player = new(controller);

			if (controller.IsBot)
			{
				AddToRegistry(player);
				return;
			}

			// An older copy of this player that was never saved (left without a normal disconnect) is saved
			// first; LoadAsync waits for that save before reading the database.
			if (players.Get(player.SteamId) is K4Player stale && !ReferenceEquals(stale, player))
			{
				if (General.Modules.Playtime && stale.HasController)
					playtime.Settle(stale);
				Save(stale);
			}

			players.BeginLoading(player);
			string name = player.Name;
			ulong steamId = player.SteamId;
			background.Run(() => LoadAsync(player, steamId, name), $"Loading player {steamId}");

			if (!game.HasRules)
				gameThread.PostWorldUpdate(game.TryUpdate);
		}
		catch (Exception ex) when (GameState.IsEntitySystemUnavailable(ex))
		{
			if (attempts > 0)
				plugin.AddTimer(1.0f, () => gameThread.PostWorldUpdate(() => TryLoad(controller, attempts - 1)));
			else
				logger.LogWarning("Player activation is waiting for the CS2 entity system: {ErrorMessage}", ex.Message);
		}
	}

	private async Task LoadAsync(K4Player player, ulong steamId, string name)
	{
		// Startup failures were already logged. The tables may still exist, so try anyway.
		await storageReady.ContinueWith(_ => { }, TaskScheduler.Default);

		// A reconnecting player's last snapshot must be saved before it is read back.
		if (!await saves.FlushAsync(steamId))
		{
			gameThread.Post(() =>
			{
				players.EndLoading(player);
				logger.LogError("Player {SteamId} was not loaded because their newer unsaved data could not be saved first. Retrying shortly.", steamId);
				RetryLoadLater(player);
			});
			return;
		}

		PlayerRecord? record;
		try
		{
			record = await store.LoadOrCreateAsync(steamId, name, ranks?.Ladder.None.Name ?? "none", config.Ranks.StartPoints);
		}
		catch (Exception ex)
		{
			gameThread.Post(() =>
			{
				players.EndLoading(player);
				logger.LogError("An error occurred while loading player {SteamId}: {ErrorMessage}. Retrying shortly.", steamId, ex.Message);
				RetryLoadLater(player);
			});
			return;
		}

		gameThread.Post(() => Apply(player, record));
	}

	private void Apply(K4Player player, PlayerRecord? record)
	{
		if (!players.IsLoading(player))
			return;

		players.EndLoading(player);

		if (record is null || !player.IsHuman)
			return;

		// The pawn can be missing for a moment (for example while respawning); try again instead of dropping the player.
		if (!player.IsValid)
		{
			if (player.HasController)
				RetryLoadLater(player);
			return;
		}

		DateTime now = DateTime.UtcNow;

		player.Rank = ranks is null ? null : new RankState(record.Points, ranks.ResolveRank(player, record.Points), record.Placement);
		player.Stats = General.Modules.Stats ? new StatCounters(record.Stats) : null;
		player.Playtime = General.Modules.Playtime ? new PlaytimeCounters(record.Playtime, now) : null;
		player.LastPersonalRankResetUtc = record.LastPersonalRankResetUtc;

		AddToRegistry(player);

		if (ranks is not null)
		{
			// Loading never announces a rank or backfills rewards; it only resumes unfinished ones.
			ranks.RefreshRank(player, announce: false, allowRewards: false);
			rewards?.ResumeStored(player);
		}

		missions?.Load(player);

		if (config.Messages.Connect)
			Announce(player, "announce.connect");
	}

	private const float LoadRetrySeconds = 15.0f;

	private void RetryLoadLater(K4Player player)
	{
		CCSPlayerController controller = player.Controller;
		plugin.AddTimer(LoadRetrySeconds, () => gameThread.PostWorldUpdate(() => TryLoad(controller)));
	}

	// Another player's stale entry on the same slot gives back the permissions K4 granted it.
	private void AddToRegistry(K4Player player)
	{
		foreach (K4Player displaced in players.Add(player))
		{
			if (displaced.IsHuman)
				ranks?.ReleasePlayer(displaced);
		}
	}

	// ---------------------------------------------------------------- leaving

	public void OnDisconnect(CCSPlayerController? controller, int reason)
	{
		K4Player? player = players.Get(controller);

		if (player is null)
		{
			if (players.GetLoading(controller) is K4Player loading)
				players.EndLoading(loading);
			return;
		}

		if (!player.IsHuman)
		{
			players.Remove(player);
			return;
		}

		if (General.Modules.Playtime)
			playtime.Settle(player);

		// On a map change everyone is saved together at map end, and reloaded on the next map.
		if (reason == MapChangeDisconnectReason)
			return;

		ranks?.ReleasePlayer(player);
		Save(player);
		players.Remove(player);

		if (config.Messages.Disconnect)
		{
			try
			{
				Announce(player, "announce.disconnect");
			}
			catch (Exception ex)
			{
				logger.LogError("Unable to announce that {PlayerName} left: {ErrorMessage}", player.Name, ex.Message);
			}
		}
	}

	/// <summary>
	/// Drops players who left without a normal disconnect, such as during a map change.
	/// Their data was already saved at map end.
	/// </summary>
	public void PruneDisconnected()
	{
		foreach (K4Player player in players.Disconnected())
		{
			if (player.IsHuman)
				ranks?.ReleasePlayer(player);

			players.Remove(player);
		}
	}

	// ---------------------------------------------------------------- saving

	public PlayerSnapshot? Capture(K4Player player, DateTime? personalRankResetUtc = null)
	{
		if (!player.IsHuman)
			return null;

		player.RefreshName();

		return new PlayerSnapshot(
			player.SteamId,
			player.Name,
			player.Rank?.Rank.Name,
			player.Rank?.Rank.Id ?? -1,
			player.Rank?.Points ?? 0,
			player.Stats?.ToArray(),
			player.Playtime?.ToArray(),
			personalRankResetUtc,
			player.Missions?.Snapshot());
	}

	/// <summary>Saves one player, then refreshes their leaderboard placement while they are still online.</summary>
	public Task<bool> Save(K4Player player, DateTime? personalRankResetUtc = null)
	{
		PlayerSnapshot? snapshot = Capture(player, personalRankResetUtc);
		if (snapshot is null)
			return Task.FromResult(true);

		Task<bool> saved = saves.Enqueue(snapshot);

		if (ranks is not null && config.Tags.TopPlacement.Enabled)
		{
			saved.ContinueWith(task =>
			{
				if (task.IsCompletedSuccessfully && task.Result)
					gameThread.Post(() => ranks.RefreshPlacement(snapshot.SteamId));
			}, TaskScheduler.Default);
		}

		return saved;
	}

	public Task<bool> SaveAll()
	{
		List<PlayerSnapshot> snapshots = [];
		foreach (K4Player player in players.All)
		{
			if (Capture(player) is PlayerSnapshot snapshot)
				snapshots.Add(snapshot);
		}

		return saves.Enqueue(snapshots);
	}

	public void SettleAllPlaytime()
	{
		if (!General.Modules.Playtime)
			return;

		foreach (K4Player player in players.All)
		{
			if (player.IsHuman)
				playtime.Settle(player);
		}
	}

	// ---------------------------------------------------------------- announcements

	private void Announce(K4Player player, string key)
	{
		Dictionary<string, string> values = new()
		{
			["name"] = player.Name,
			["steamid"] = player.SteamId.ToString(),
			["clantag"] = player.IsValid ? player.ClanTag : string.Empty,
			["rank"] = player.Rank?.Rank.Name ?? "Unranked",
			["points"] = player.Rank?.Points.ToString() ?? "0",
			["topplacement"] = player.Rank?.TopPlacement.ToString() ?? "0",
			["playtime"] = player.Playtime?[PlaytimeField.All].ToString() ?? "0"
		};

		if (!chat[key].Contains("{country}", StringComparison.Ordinal))
		{
			SendAnnouncement(key, values);
			return;
		}

		string? ip = player.IsValid ? player.Controller.IpAddress : null;
		countries.GetCountryCodeAsync(ip).ContinueWith(lookup =>
		{
			values["country"] = lookup.IsCompletedSuccessfully ? lookup.Result : CountryLookup.Unknown;
			gameThread.PostWorldUpdate(() => SendAnnouncement(key, values));
		}, TaskScheduler.Default);
	}

	// Each player gets the announcement in their own language.
	private void SendAnnouncement(string key, Dictionary<string, string> values)
	{
		foreach (CCSPlayerController reader in Utilities.GetPlayers())
		{
			if (reader.IsValid && !reader.IsBot && !reader.IsHLTV)
				reader.PrintToChat(" " + Fill(chat.For(reader, key), values));
		}
	}

	private static string Fill(string text, Dictionary<string, string> values)
	{
		foreach ((string key, string value) in values)
			text = text.Replace($"{{{key}}}", value);
		return text;
	}
}
