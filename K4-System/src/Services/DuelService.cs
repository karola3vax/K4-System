namespace K4System.Services;

using CounterStrikeSharp.API.Core;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Players;

/// <summary>
/// Point wagers: a challenge, an accept, then most kills from that moment until the match ends wins
/// both stakes. Stakes are taken at the accept, so a player who leaves has already paid. Each player
/// can duel once per map. Game thread only.
/// </summary>
internal sealed class DuelService(PluginConfig config, PlayerRegistry players, RankService ranks, GameState game, Chat chat, HudBridge hud, Action<float, Action> addTimer)
{
	private sealed record Offer(int Id, ulong Challenger, string ChallengerName, ulong Opponent, string OpponentName, int Stake);

	private sealed class Duel(Offer terms)
	{
		public Offer Terms { get; } = terms;
		public int ChallengerKills { get; set; }
		public int OpponentKills { get; set; }
	}

	/// <summary>Why a duel command did nothing: a language key and its arguments for the reply.</summary>
	public readonly record struct Refusal(string Key, object[] Args);

	private DuelSettings Settings => config.Duels;

	private readonly List<Offer> pending = [];
	private readonly List<Duel> running = [];
	private readonly HashSet<ulong> dueledThisMap = [];
	private readonly Dictionary<ulong, DateTime> lastChallengeUtc = [];
	private int nextId;

	/// <summary>Set at the match end: no new duels in the intermission before the map changes.</summary>
	private bool matchOver;

	public int AcceptSeconds => Settings.AcceptSeconds;

	/// <summary>The player's running duel, for the panel: the other player, both kill counts, and the stake.</summary>
	public (string Opponent, int Mine, int Theirs, int Stake)? RunningFor(ulong steamId)
	{
		foreach (Duel duel in running)
		{
			if (duel.Terms.Challenger == steamId)
				return (duel.Terms.OpponentName, duel.ChallengerKills, duel.OpponentKills, duel.Terms.Stake);
			if (duel.Terms.Opponent == steamId)
				return (duel.Terms.ChallengerName, duel.OpponentKills, duel.ChallengerKills, duel.Terms.Stake);
		}
		return null;
	}

	/// <summary>A challenge waiting for this player's answer, for the panel.</summary>
	public (string Challenger, int Stake)? IncomingFor(ulong steamId)
		=> pending.Find(challenge => challenge.Opponent == steamId) is Offer offer ? (offer.ChallengerName, offer.Stake) : null;

	private void RefreshPanels(Offer terms)
	{
		if (hud.Hud is not PanoramaHud panel)
			return;

		foreach (ulong steamId in new[] { terms.Challenger, terms.Opponent })
		{
			if (players.Get(steamId) is { IsValid: true } player)
				panel.Refresh(player, HudTab.Rank);
		}
	}

	// ---------------------------------------------------------------- commands

	public Refusal? Challenge(K4Player challenger, K4Player opponent, int stake)
	{
		if (matchOver || !ranks.IsPointsAllowed())
			return new Refusal("duel.error.not-now", []);
		if (challenger.SteamId == opponent.SteamId)
			return new Refusal("duel.error.self", []);
		if (dueledThisMap.Contains(challenger.SteamId))
			return new Refusal("duel.error.used", []);
		if (dueledThisMap.Contains(opponent.SteamId))
			return new Refusal("duel.error.target-used", [opponent.Controller.PlayerName]);
		if (Involved(challenger.SteamId))
			return new Refusal("duel.error.busy", [challenger.Controller.PlayerName]);
		if (Involved(opponent.SteamId))
			return new Refusal("duel.error.busy", [opponent.Controller.PlayerName]);
		// Every challenge is announced to everyone, so sending and taking back challenges cannot flood the chat.
		DateTime now = DateTime.UtcNow;
		TimeSpan wait = ResetPolicy.CommandCooldownRemaining(now, lastChallengeUtc.TryGetValue(challenger.SteamId, out DateTime last) ? last : (DateTime?)null, Settings.AcceptSeconds);
		if (wait > TimeSpan.Zero)
			return new Refusal("error.cooldown", [(int)Math.Ceiling(wait.TotalSeconds)]);
		if (Settings.BlockSameIp && Showdowns.SameAddress(challenger.Controller.IpAddress, opponent.Controller.IpAddress))
			return new Refusal("duel.error.same-ip", []);

		int max = Showdowns.MaxStake(challenger.Rank?.Points ?? 0, opponent.Rank?.Points ?? 0, Settings.MaxStakePercent);
		if (max < Settings.MinStake)
			return new Refusal("duel.error.no-points", []);
		if (stake < Settings.MinStake || stake > max)
			return new Refusal("duel.error.stake", [Settings.MinStake, max]);

		Offer challenge = new(++nextId, challenger.SteamId, challenger.Controller.PlayerName, opponent.SteamId, opponent.Controller.PlayerName, stake);
		pending.Add(challenge);
		lastChallengeUtc[challenger.SteamId] = now;
		chat.TellAll("duel.challenge", challenge.ChallengerName, challenge.OpponentName, stake);
		RefreshPanels(challenge);

		addTimer(Settings.AcceptSeconds, () =>
		{
			if (!pending.Remove(challenge))
				return;
			TellBoth(challenge, "duel.expired", challenge.ChallengerName, challenge.OpponentName);
			RefreshPanels(challenge);
		});

		return null;
	}

	public Refusal? Accept(K4Player opponent)
	{
		if (pending.Find(challenge => challenge.Opponent == opponent.SteamId) is not Offer challenge)
			return new Refusal("duel.error.no-challenge", []);

		if (matchOver || !ranks.IsPointsAllowed())
			return new Refusal("duel.error.not-now", []);

		pending.Remove(challenge);
		K4Player? challenger = players.Get(challenge.Challenger);
		if (challenger is not { IsHuman: true, Rank: not null } || opponent.Rank is null
			|| challenger.Rank.Points < challenge.Stake || opponent.Rank.Points < challenge.Stake)
		{
			TellBoth(challenge, "duel.declined", challenge.ChallengerName, challenge.OpponentName);
			return new Refusal("duel.error.no-points", []);
		}

		Hold(challenger, challenge.Stake);
		Hold(opponent, challenge.Stake);
		dueledThisMap.Add(challenge.Challenger);
		dueledThisMap.Add(challenge.Opponent);
		running.Add(new Duel(challenge));
		chat.TellAll("duel.started", challenge.ChallengerName, challenge.OpponentName, challenge.Stake);
		RefreshPanels(challenge);
		return null;
	}

	/// <summary>Turns down a challenge, or takes back one the player sent.</summary>
	public Refusal? Decline(K4Player player)
	{
		if (pending.Find(challenge => challenge.Opponent == player.SteamId || challenge.Challenger == player.SteamId) is not Offer challenge)
			return new Refusal("duel.error.no-challenge", []);

		pending.Remove(challenge);
		TellBoth(challenge, "duel.declined", challenge.ChallengerName, challenge.OpponentName);
		RefreshPanels(challenge);
		return null;
	}

	private bool Involved(ulong steamId)
		=> pending.Exists(challenge => challenge.Challenger == steamId || challenge.Opponent == steamId)
			|| running.Exists(duel => duel.Terms.Challenger == steamId || duel.Terms.Opponent == steamId);

	private void Hold(K4Player player, int stake) => ranks.DebitPoints(player, stake);

	private void TellBoth(Offer challenge, string key, params object[] args)
	{
		foreach (ulong steamId in new[] { challenge.Challenger, challenge.Opponent })
		{
			if (players.Get(steamId) is { HasController: true } player)
				chat.Tell(player.Controller, key, args);
		}
	}

	// ---------------------------------------------------------------- game events

	public void OnPlayerDeath(DeathActor? victim, DeathActor? attacker)
	{
		if (running.Count == 0 || game.IsWarmup || attacker is not DeathActor killer)
			return;

		if (DeathRules.Classify(victim, attacker, null, config.Tracking.CountBots, config.Tracking.FreeForAll).AttackerPoint != AttackerPointAction.Kill)
			return;

		foreach (Duel duel in running)
		{
			if (duel.Terms.Challenger == killer.SteamId)
				duel.ChallengerKills++;
			else if (duel.Terms.Opponent == killer.SteamId)
				duel.OpponentKills++;
			else
				continue;
			RefreshPanels(duel.Terms);
		}
	}

	/// <summary>An open challenge ends when either player leaves. A running duel waits: they may reconnect.</summary>
	public void OnDisconnect(CCSPlayerController? controller, int reason)
	{
		// A map change is not leaving; open challenges end with the map anyway.
		if (reason == PlayerService.MapChangeDisconnectReason || players.Get(controller) is not K4Player player)
			return;

		foreach (Offer challenge in pending.Where(challenge => challenge.Challenger == player.SteamId || challenge.Opponent == player.SteamId).ToArray())
		{
			pending.Remove(challenge);
			TellBoth(challenge, "duel.cancelled-left", player.Controller.PlayerName);
		}
	}

	/// <summary>Pays every running duel and closes duels until the next map.</summary>
	public void OnMatchEnd()
	{
		matchOver = true;
		Settle(announce: true);
	}

	/// <summary>The map changed without a match end. Settled quietly before the map-end save.</summary>
	public void OnMapEnd() => Settle(announce: false);

	public void OnMapStart()
	{
		running.Clear();
		pending.Clear();
		dueledThisMap.Clear();
		lastChallengeUtc.Clear();
		matchOver = false;
	}

	/// <summary>A player who has left the server loses; leavers are no longer in the player list.</summary>
	private void Settle(bool announce)
	{
		foreach (Duel duel in running)
		{
			Offer terms = duel.Terms;
			K4Player? challenger = Present(terms.Challenger);
			K4Player? opponent = Present(terms.Opponent);
			int pot = PointMath.AddClamped(terms.Stake, terms.Stake);

			switch (Showdowns.Winner(challenger is not null, duel.ChallengerKills, opponent is not null, duel.OpponentKills))
			{
				case 1:
					Pay(challenger!, opponent is null, terms.ChallengerName, terms.OpponentName, pot, duel.ChallengerKills, duel.OpponentKills, announce);
					break;
				case -1:
					Pay(opponent!, challenger is null, terms.OpponentName, terms.ChallengerName, pot, duel.OpponentKills, duel.ChallengerKills, announce);
					break;
				default:
					if (challenger is null || opponent is null)
						break;
					ranks.CreditPoints(challenger, terms.Stake, refresh: announce);
					ranks.CreditPoints(opponent, terms.Stake, refresh: announce);
					if (announce)
						chat.TellAll("duel.tie", terms.ChallengerName, terms.OpponentName, duel.ChallengerKills);
					break;
			}
		}

		running.Clear();
		pending.Clear();
	}

	private void Pay(K4Player winner, bool loserLeft, string winnerName, string loserName, int pot, int winnerKills, int loserKills, bool announce)
	{
		ranks.CreditPoints(winner, pot, refresh: announce);
		if (!announce)
			return;

		if (loserLeft)
			chat.TellAll("duel.forfeit", winnerName, loserName, pot);
		else
			chat.TellAll("duel.won", winnerName, loserName, winnerKills, loserKills, pot);
	}

	private K4Player? Present(ulong steamId)
		=> players.Get(steamId) is { IsHuman: true } player ? player : null;

	/// <summary>K4 unloads: duels cannot finish, so players still here get their stake back. Touches no entities.</summary>
	public void ReturnStakes()
	{
		foreach (Duel duel in running)
		{
			foreach (ulong steamId in new[] { duel.Terms.Challenger, duel.Terms.Opponent })
			{
				if (Present(steamId) is K4Player player)
					ranks.CreditPoints(player, duel.Terms.Stake, refresh: false);
			}
		}

		running.Clear();
		pending.Clear();
	}
}
