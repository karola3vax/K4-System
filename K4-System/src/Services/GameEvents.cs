namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Config;
using K4System.Domain;
using K4System.Missions;
using K4System.Players;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registers every game event once and routes it to the enabled modules in a fixed order.
/// Each death is read once and shared by the rank, stat, and playtime modules.
/// </summary>
internal sealed class GameEvents(
	BasePlugin plugin,
	PluginConfig config,
	PlayerRegistry players,
	PlayerService playerService,
	RankService? ranks,
	StatService? stats,
	PlaytimeService? playtime,
	ScoreboardService? scoreboard,
	MissionService? missions,
	BountyService? bounty,
	DuelService? duels,
	SqlPlayerStore store,
	GameState game,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	ILogger logger)
{
	private const double DuplicateRoundStartSeconds = 3;
	private DateTime lastSpawnMessageUtc = DateTime.MinValue;

	public void Register()
	{
		RegisterLifecycle();
		RegisterRoundFlow();
		RegisterPlayerState();
		RegisterDeathAndDamage();
		RegisterObjectives();
		RegisterTimers();
	}

	private static DeathActor? Actor(CCSPlayerController? controller)
	{
		if (controller?.IsValid != true || controller.Connected != PlayerConnectedState.Connected)
			return null;

		return new DeathActor(controller.Slot, controller.SteamID, !controller.IsBot && !controller.IsHLTV, controller.TeamNum);
	}

	/// <summary>A valid, loaded human for events that only concern real players.</summary>
	private K4Player? ValidHuman(CCSPlayerController? controller)
		=> players.Get(controller) is { IsValid: true, IsHuman: true } player ? player : null;

	// ---------------------------------------------------------------- connect, disconnect, maps

	private void RegisterLifecycle()
	{
		plugin.RegisterEventHandler<EventPlayerActivate>((@event, _) =>
		{
			CCSPlayerController? controller = @event.Userid;
			gameThread.PostWorldUpdate(() => playerService.TryLoad(controller));
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
		{
			bounty?.OnDisconnect(@event.Userid, @event.Reason);
			duels?.OnDisconnect(@event.Userid, @event.Reason);
			playerService.OnDisconnect(@event.Userid, @event.Reason);
			return HookResult.Continue;
		});

		plugin.RegisterListener<Listeners.OnMapStart>(_ =>
		{
			game.Clear();
			foreach (K4Player player in players.All)
				player.MatchKills = 0;
			missions?.OnMapStart();
			bounty?.OnMapStart();
			duels?.OnMapStart();

			plugin.AddTimer(5.0f, () => gameThread.PostWorldUpdate(game.TryUpdate));
			// Missions read the map at the end of freeze time; this covers modes that have none.
			if (missions is not null)
				plugin.AddTimer(30.0f, () => gameThread.PostWorldUpdate(missions.ReadMapIfNeeded));
			playerService.QueueConnectedPlayersRefresh(2.0f);
			playerService.QueueConnectedPlayersRefresh(6.0f);
			plugin.AddTimer(15.0f, () => gameThread.PostWorldUpdate(playerService.PruneDisconnected));
		});

		plugin.RegisterListener<Listeners.OnMapEnd>(() =>
		{
			playerService.SettleAllPlaytime();
			// Paid before the save: a map change without a match end still settles bounties and duels.
			bounty?.OnMapEnd();
			duels?.OnMapEnd();
			Task<bool> saved = playerService.SaveAll();
			game.Clear();

			int purgeDays = config.Storage.PurgeAfterDays;
			background.Run(async () =>
			{
				await saved;
				await store.PurgeAsync(purgeDays);
			}, "Purging inactive players");
		});

		if (ranks is not null)
		{
			plugin.RegisterListener<Listeners.OnPlayerButtonsChanged>((controller, _, _) =>
			{
				if (players.Get(controller) is K4Player player)
					player.LastActivityUtc = DateTime.UtcNow;
			});
		}
	}

	// ---------------------------------------------------------------- rounds

	private void RegisterRoundFlow()
	{
		plugin.RegisterEventHandler<EventRoundStart>((_, _) =>
		{
			SendSpawnMessage();
			stats?.OnRoundStart();
			scoreboard?.AllowUpdates(true);
			ranks?.OnRoundStart();
			missions?.OnRoundStart();
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventRoundEnd>((@event, _) =>
		{
			if (!game.HasRules)
			{
				gameThread.PostWorldUpdate(game.TryUpdate);
				playerService.SaveAll();
				return HookResult.Continue;
			}

			stats?.OnRoundEnd(@event.Winner);
			ranks?.OnRoundEnd(@event.Winner);
			if (missions?.IsTrackingAllowed() == true)
				missions.OnRoundEnd(@event.Winner);
			playerService.SaveAll();
			return HookResult.Continue;
		});

		if (missions is not null)
		{
			plugin.RegisterEventHandler<EventRoundFreezeEnd>((_, _) =>
			{
				missions.OnFreezeEnd();
				return HookResult.Continue;
			});
		}

		plugin.RegisterEventHandler<EventRoundPrestart>((_, _) =>
		{
			ranks?.BalanceTeams();
			return HookResult.Continue;
		});

		// mp_restartgame and a new match start the free-for-all count again.
		plugin.RegisterEventHandler<EventBeginNewMatch>((_, _) =>
		{
			foreach (K4Player player in players.All)
				player.MatchKills = 0;
			missions?.OnMatchStart();
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventCsWinPanelMatch>((_, _) =>
		{
			scoreboard?.AllowUpdates(false);
			stats?.OnMatchEnd();
			missions?.OnMatchEnd();
			bounty?.OnMatchEnd();
			duels?.OnMatchEnd();
			playerService.SaveAll();
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventRoundMvp>((@event, _) =>
		{
			CCSPlayerController? controller;
			try
			{
				controller = @event.Userid;
			}
			catch (NativeException ex) when (ex.Message.Contains("Invalid game event", StringComparison.OrdinalIgnoreCase))
			{
				return HookResult.Continue;
			}

			ranks?.OnMvp(players.Get(controller));
			stats?.OnPlayerEvent(controller, StatField.Mvp);
			RecordMission(controller, MissionType.Mvps);
			return HookResult.Continue;
		});
	}

	// The game can fire round start twice; the message is sent once.
	private void SendSpawnMessage()
	{
		if (!config.Messages.JoinHint)
			return;

		DateTime now = DateTime.UtcNow;
		if ((now - lastSpawnMessageUtc).TotalSeconds < DuplicateRoundStartSeconds)
			return;

		lastSpawnMessageUtc = now;
		foreach (K4Player player in players.ValidHumans)
			chat.Tell(player.Controller, "hint.join");
	}

	// ---------------------------------------------------------------- team and spawn

	private void RegisterPlayerState()
	{
		plugin.RegisterEventHandler<EventPlayerTeam>((@event, _) =>
		{
			if (ValidHuman(@event.Userid) is not K4Player player)
				return HookResult.Continue;

			ranks?.OnTeamChange(player, @event.Disconnect, @event.Team, @event.Oldteam);
			playtime?.OnTeamChange(player, @event.Oldteam);
			if (!@event.Disconnect)
				missions?.OnTeamJoined(player, @event.Team);
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventPlayerSpawn>((@event, _) =>
		{
			if (missions is not null && @event.Userid is { IsValid: true } spawned)
				missions.OnSpawn(spawned.Slot);

			if (ValidHuman(@event.Userid) is not K4Player player)
				return HookResult.Continue;

			ranks?.RefreshRank(player, announce: false, allowRewards: true);
			playtime?.OnSpawn(player);
			return HookResult.Continue;
		});
	}

	// ---------------------------------------------------------------- deaths and damage

	private void RegisterDeathAndDamage()
	{
		plugin.RegisterEventHandler<EventPlayerDeath>((@event, _) =>
		{
			DeathActor? victim = Actor(@event.Userid);
			DeathActor? attacker = Actor(@event.Attacker);
			DeathActor? assister = Actor(@event.Assister);

			ranks?.OnPlayerDeath(@event, victim, attacker, assister);
			stats?.OnPlayerDeath(@event, victim, attacker, assister);
			bounty?.OnPlayerDeath(@event, victim, attacker);
			duels?.OnPlayerDeath(victim, attacker);

			if (!game.IsWarmup
				&& DeathRules.Classify(victim, attacker, assister, includeBots: true, config.Tracking.FreeForAll).AttackerPoint == AttackerPointAction.Kill
				&& ValidHuman(@event.Attacker) is K4Player matchKiller)
			{
				matchKiller.MatchKills = matchKiller.MatchKills == int.MaxValue ? int.MaxValue : matchKiller.MatchKills + 1;
			}

			if (playtime is not null && ValidHuman(@event.Userid) is K4Player dead)
				playtime.OnDeath(dead);

			if (missions is not null)
				OnMissionDeath(missions, @event, victim, attacker, assister);

			return HookResult.Continue;
		});

		if (stats is null && missions is null)
			return;

		plugin.RegisterEventHandler<EventPlayerHurt>((@event, _) =>
		{
			DeathActor? victim = Actor(@event.Userid);
			DeathActor? attacker = Actor(@event.Attacker);
			stats?.OnPlayerHurt(@event, victim, attacker);

			if (missions is not null && victim is DeathActor hurt)
			{
				int taken = missions.TrackHealth(hurt.Slot, @event.DmgHealth, @event.Health);

				if (missions.IsTrackingAllowed() && attacker is DeathActor { IsHuman: true } shooter
					&& !DeathRules.IsSameActor(shooter, hurt) && (hurt.IsHuman || config.Missions.CountBots)
					&& (hurt.Team != shooter.Team || config.Tracking.FreeForAll || missions.Map is { Teams: false })
					&& ValidHuman(@event.Attacker) is K4Player damager)
				{
					missions.Record(damager, MissionType.Damage, taken);
				}
			}

			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventGrenadeThrown>((@event, _) =>
		{
			stats?.OnPlayerEvent(@event.Userid, StatField.Grenades);
			missions?.SawWeapon(@event.Weapon);
			return HookResult.Continue;
		});

		if (stats is null)
			return;

		plugin.RegisterEventHandler<EventWeaponFire>((@event, _) =>
		{
			stats.OnWeaponFire(@event.Userid, @event.Weapon);
			return HookResult.Continue;
		});

	}

	/// <summary>Mission progress from a death: kills, assists, kill streaks, opening kills, and clutches.</summary>
	private void OnMissionDeath(MissionService missions, EventPlayerDeath @event, DeathActor? victim, DeathActor? attacker, DeathActor? assister)
	{
		bool opening = missions.TakeOpening();
		if (victim is DeathActor dead)
			missions.OnLifeEnded(dead.SteamId);

		if (!missions.IsTrackingAllowed())
			return;

		DeathDecision decision = DeathRules.Classify(victim, attacker, assister, config.Missions.CountBots, config.Tracking.FreeForAll);

		if (decision.AttackerPoint == AttackerPointAction.Kill && ValidHuman(@event.Attacker) is K4Player killer)
		{
			missions.OnKill(killer, new KillFacts(
				@event.Weapon,
				@event.Headshot,
				@event.Noscope,
				Wallbang: @event.Penetrated > 0,
				ThroughSmoke: @event.Thrusmoke,
				Blind: @event.Attackerblind,
				InAir: @event.Attackerinair,
				@event.Distance,
				Revenge: @event.Revenge > 0,
				opening));
		}

		if (decision.CountAssister && decision.AssisterVictimIsEnemy && ValidHuman(@event.Assister) is K4Player helper)
			missions.OnAssist(helper, @event.Assistedflash);

		if (victim is DeathActor lost)
			missions.CheckClutch(lost.Team, lost.Slot);
	}

	// ---------------------------------------------------------------- bomb and hostages

	private void RegisterObjectives()
	{
		PointValues points = config.PointValues;

		plugin.RegisterEventHandler<EventBombPlanted>((@event, _) =>
		{
			ranks?.ModifyPoints(players.Get(@event.Userid), points.BombPlant, "reason.bomb-planted");
			stats?.OnPlayerEvent(@event.Userid, StatField.BombPlanted);
			RecordMission(@event.Userid, MissionType.BombPlants);
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventBombDefused>((@event, _) =>
		{
			ranks?.OnBombDefused(players.Get(@event.Userid));
			stats?.OnPlayerEvent(@event.Userid, StatField.BombDefused);
			RecordMission(@event.Userid, MissionType.BombDefuses);
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventHostageRescued>((@event, _) =>
		{
			ranks?.ModifyPoints(players.Get(@event.Userid), points.HostageRescue, "reason.hostage-rescued");
			stats?.OnPlayerEvent(@event.Userid, StatField.HostageRescued);
			RecordMission(@event.Userid, MissionType.HostageRescues);
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventHostageKilled>((@event, _) =>
		{
			ranks?.ModifyPoints(players.Get(@event.Userid), points.HostageKill, "reason.hostage-killed");
			stats?.OnPlayerEvent(@event.Userid, StatField.HostageKilled);
			return HookResult.Continue;
		});

		if (ranks is null)
			return;

		plugin.RegisterEventHandler<EventHostageHurt>((@event, _) =>
		{
			ranks.ModifyPoints(players.Get(@event.Userid), points.HostageHurt, "reason.hostage-hurt");
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventHostageRescuedAll>((_, _) =>
		{
			ranks.RewardTeam(CsTeam.CounterTerrorist, points.HostageRescueAll, "reason.all-hostages-rescued");
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventBombPickup>((@event, _) =>
		{
			ranks.ModifyPoints(players.Get(@event.Userid), points.BombPickup, "reason.bomb-picked-up");
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventBombDropped>((@event, _) =>
		{
			ranks.ModifyPoints(players.Get(@event.Userid), points.BombDrop, "reason.bomb-dropped");
			return HookResult.Continue;
		});

		plugin.RegisterEventHandler<EventBombExploded>((_, _) =>
		{
			ranks.RewardTeam(CsTeam.Terrorist, points.BombExploded, "reason.bomb-exploded");
			return HookResult.Continue;
		});
	}

	private void RecordMission(CCSPlayerController? controller, MissionType type)
	{
		if (missions?.IsTrackingAllowed() == true)
			missions.Record(ValidHuman(controller), type);
	}

	// ---------------------------------------------------------------- timers

	private void RegisterTimers()
	{
		if (missions is not null)
			plugin.AddTimer(60, missions.OnPlaytimeMinute, TimerFlags.REPEAT);

		if (ranks is null)
			return;

		float playtimeMinutes = config.Ranks.PlaytimeReward.EveryMinutes;
		if (playtimeMinutes > 0)
		{
			float checkSeconds = (float)Math.Clamp(playtimeMinutes * 60d, 1d, 30d);
			plugin.AddTimer(checkSeconds, ranks.AwardPlaytimePoints, TimerFlags.REPEAT);
		}

		if (config.Tags.TopPlacement.Enabled)
			plugin.AddTimer(300, ranks.RefreshPlacements, TimerFlags.REPEAT);

		logger.LogDebug("K4-System timers registered.");
	}
}
