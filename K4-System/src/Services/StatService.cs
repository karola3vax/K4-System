namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Config;
using K4System.Domain;
using K4System.Players;

/// <summary>Statistic counters for every stat-related game event.</summary>
internal sealed class StatService(PluginConfig config, PlayerRegistry players, GameState game)
{
	private bool firstBloodTaken;

	public bool IsStatsAllowed()
		=> game.IsTrackingAllowed(config.Tracking.CountWarmup, config.Tracking.MinimumPlayers);

	private static void Count(K4Player? player, StatField field) => player?.Stats?.Increment(field);

	/// <summary>Counts only for a loaded, valid human.</summary>
	private static void CountValidHuman(K4Player? player, StatField field)
	{
		if (player is { IsValid: true, IsHuman: true })
			Count(player, field);
	}

	public void OnRoundStart() => firstBloodTaken = false;

	public void OnPlayerDeath(EventPlayerDeath @event, DeathActor? victimActor, DeathActor? attackerActor, DeathActor? assisterActor)
	{
		if (!victimActor.HasValue || !IsStatsAllowed())
			return;

		DeathDecision decision = DeathRules.Classify(victimActor, attackerActor, assisterActor, config.Tracking.CountBots, config.Tracking.FreeForAll);

		if (decision.CountVictim)
			Count(players.Get(@event.Userid), StatField.Deaths);

		K4Player? attacker = players.Get(@event.Attacker);
		if (decision.CountAttacker && attacker is { IsValid: true, IsHuman: true })
		{
			Count(attacker, StatField.Kills);

			if (!firstBloodTaken)
			{
				firstBloodTaken = true;
				Count(attacker, StatField.FirstBlood);
			}

			if (@event.Noscope)
				Count(attacker, StatField.NoscopeKill);
			if (@event.Penetrated > 0)
				Count(attacker, StatField.PenetratedKill);
			if (@event.Thrusmoke)
				Count(attacker, StatField.ThrusmokeKill);
			if (@event.Attackerblind)
				Count(attacker, StatField.FlashedKill);
			if (@event.Dominated > 0)
				Count(attacker, StatField.DominatedKill);
			if (@event.Revenge > 0)
				Count(attacker, StatField.RevengeKill);
			if (@event.Headshot)
				Count(attacker, StatField.Headshots);
		}

		K4Player? assister = players.Get(@event.Assister);
		if (decision.CountAssister && assister is { IsValid: true, IsHuman: true })
		{
			Count(assister, StatField.Assists);

			if (@event.Assistedflash)
				Count(assister, StatField.AssistFlash);
		}
	}

	public void OnPlayerHurt(EventPlayerHurt @event, DeathActor? victimActor, DeathActor? attackerActor)
	{
		if (!victimActor.HasValue || !IsStatsAllowed())
			return;

		if (!victimActor.Value.IsHuman && !config.Tracking.CountBots)
			return;

		Count(players.Get(@event.Userid), StatField.HitsTaken);

		if (attackerActor is not { IsHuman: true } attackerValue || DeathRules.IsSameActor(attackerValue, victimActor.Value))
			return;

		K4Player? attacker = players.Get(@event.Attacker);
		if (attacker is not { IsValid: true, IsHuman: true })
			return;

		Count(attacker, StatField.HitsGiven);

		if (HitGroupField(@event.Hitgroup) is StatField hitField)
			Count(attacker, hitField);
	}

	internal static StatField? HitGroupField(int hitGroup) => hitGroup switch
	{
		(int)HitGroup_t.HITGROUP_CHEST => StatField.ChestHits,
		(int)HitGroup_t.HITGROUP_STOMACH => StatField.StomachHits,
		(int)HitGroup_t.HITGROUP_LEFTARM => StatField.LeftArmHits,
		(int)HitGroup_t.HITGROUP_RIGHTARM => StatField.RightArmHits,
		(int)HitGroup_t.HITGROUP_LEFTLEG => StatField.LeftLegHits,
		(int)HitGroup_t.HITGROUP_RIGHTLEG => StatField.RightLegHits,
		(int)HitGroup_t.HITGROUP_NECK => StatField.NeckHits,
		(int)HitGroup_t.HITGROUP_UNUSED => StatField.UnusedHits,
		(int)HitGroup_t.HITGROUP_GEAR => StatField.GearHits,
		(int)HitGroup_t.HITGROUP_SPECIAL => StatField.SpecialHits,
		_ => null
	};

	/// <summary>A simple event that counts one stat for its player.</summary>
	public void OnPlayerEvent(CCSPlayerController? controller, StatField field)
	{
		if (IsStatsAllowed())
			CountValidHuman(players.Get(controller), field);
	}

	public void OnWeaponFire(CCSPlayerController? controller, string weapon)
	{
		if (weapon.Contains("knife") || weapon.Contains("bayonet"))
			return;

		OnPlayerEvent(controller, StatField.Shoots);
	}

	/// <summary>Round wins and losses, then rounds played per team. Runs before the round-end save.</summary>
	public void OnRoundEnd(int winnerTeam)
	{
		if (!IsStatsAllowed())
			return;

		foreach (K4Player player in players.ValidHumans)
		{
			CsTeam team = player.Controller.Team;
			if (team <= CsTeam.Spectator)
				continue;

			if (winnerTeam > (int)CsTeam.Spectator)
				Count(player, (int)team == winnerTeam ? StatField.RoundWin : StatField.RoundLose);

			Count(player, StatField.RoundsOverall);
			Count(player, team == CsTeam.Terrorist ? StatField.RoundsT : StatField.RoundsCt);
		}
	}

	/// <summary>Match wins and losses. In FFA mode the player with the most match kills wins.</summary>
	public void OnMatchEnd()
	{
		if (!IsStatsAllowed())
			return;

		(List<K4Player> played, HashSet<K4Player> winners, bool hasResult) = MatchResults.Get(players, config.Tracking.FreeForAll);
		if (!hasResult)
			return;

		foreach (K4Player player in played)
			Count(player, winners.Contains(player) ? StatField.GameWin : StatField.GameLose);
	}

	public static float HeadshotPercentage(int headshots, int kills) => kills > 0 ? (float)headshots / kills * 100 : 0;
}
