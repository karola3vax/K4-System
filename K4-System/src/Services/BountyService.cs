namespace K4System.Services;

using CounterStrikeSharp.API.Core;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Players;

/// <summary>
/// "Wanted": a long kill streak in one life puts a bounty on the player. The enemy who ends it gets
/// bonus points; nobody loses anything extra. One wanted player at a time, each player once per map.
/// </summary>
internal sealed class BountyService(PluginConfig config, PlayerRegistry players, RankService ranks, Chat chat, HudBridge hud)
{
	private BountySettings Settings => config.Bounty;

	private readonly HashSet<ulong> wantedThisMap = [];

	/// <summary>The wanted player's SteamID, or 0 while nobody is wanted.</summary>
	private ulong wanted;
	private string wantedName = string.Empty;

	/// <summary>The wanted player and the bounty on them, for the panel.</summary>
	public (string Name, int Reward)? Wanted => wanted == 0 ? null : (wantedName, Settings.Reward);

	public void OnMapStart()
	{
		wanted = 0;
		wantedThisMap.Clear();
		foreach (K4Player player in players.All)
			player.LifeKills = 0;
	}

	public void OnPlayerDeath(EventPlayerDeath @event, DeathActor? victim, DeathActor? attacker)
	{
		if (players.Get(@event.Userid) is K4Player dead)
		{
			dead.LifeKills = 0;
			if (wanted != 0 && dead.SteamId == wanted)
				EndBounty(victim, attacker, players.Get(@event.Attacker));
		}

		// Bot kills neither build nor break a streak.
		DeathDecision decision = DeathRules.Classify(victim, attacker, null, includeBots: false, config.Tracking.FreeForAll);
		if (decision.AttackerPoint != AttackerPointAction.Kill || !ranks.IsPointsAllowed()
			|| players.Get(@event.Attacker) is not { IsValid: true, IsHuman: true, Rank: not null } killer)
			return;

		killer.LifeKills = killer.LifeKills == int.MaxValue ? int.MaxValue : killer.LifeKills + 1;
		if (wanted == 0 && killer.LifeKills >= Settings.KillStreak && wantedThisMap.Add(killer.SteamId))
			StartBounty(killer);
	}

	private void StartBounty(K4Player player)
	{
		wanted = player.SteamId;
		wantedName = player.Controller.PlayerName;
		chat.TellAll("bounty.wanted", wantedName, player.LifeKills, Settings.Reward);

		if (hud.Hud is PanoramaHud panel)
		{
			foreach (K4Player viewer in players.ValidHumans.ToArray())
				panel.ShowToast(viewer, "hud.toast.wanted", wantedName, ToastKind.Warning);
		}
		RefreshPanels();
	}

	private void RefreshPanels()
	{
		if (hud.Hud is PanoramaHud panel)
		{
			foreach (K4Player viewer in players.ValidHumans.ToArray())
				panel.Refresh(viewer, HudTab.Rank);
		}
	}

	// An enemy kill pays the bounty; a suicide, team kill, or bot kill only ends it.
	private void EndBounty(DeathActor? victim, DeathActor? attacker, K4Player? hunter)
	{
		wanted = 0;
		DeathDecision decision = DeathRules.Classify(victim, attacker, null, includeBots: false, config.Tracking.FreeForAll);

		if (decision.AttackerPoint == AttackerPointAction.Kill && hunter is { IsValid: true, IsHuman: true })
		{
			ranks.GrantPoints(hunter, Settings.Reward);
			chat.TellAll("bounty.claimed", hunter.Controller.PlayerName, wantedName, Settings.Reward);
		}
		else
			chat.TellAll("bounty.ended", wantedName);
		RefreshPanels();
	}

	/// <summary>Leaving ends the bounty. A map change is not leaving: the wanted player survived it.</summary>
	public void OnDisconnect(CCSPlayerController? controller, int reason)
	{
		if (reason != PlayerService.MapChangeDisconnectReason && wanted != 0 && players.Get(controller)?.SteamId == wanted)
		{
			wanted = 0;
			chat.TellAll("bounty.ended", wantedName);
			RefreshPanels();
		}
	}

	/// <summary>A wanted player who is still wanted when the match ends survived the bounty.</summary>
	public void OnMatchEnd() => PaySurvivor(announce: true);

	/// <summary>The map changed without a match end. Paid quietly before the map-end save.</summary>
	public void OnMapEnd() => PaySurvivor(announce: false);

	private void PaySurvivor(bool announce)
	{
		if (wanted == 0)
			return;

		K4Player? survivor = players.Get(wanted);
		wanted = 0;
		if (survivor is not { IsHuman: true } || Settings.SurvivorReward <= 0)
			return;

		ranks.CreditPoints(survivor, Settings.SurvivorReward, refresh: announce);
		if (announce)
			chat.TellAll("bounty.survived", wantedName, Settings.SurvivorReward);
	}
}
