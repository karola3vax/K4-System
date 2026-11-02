namespace K4System.Services;

using System.Globalization;
using CounterStrikeSharp.API.Core;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Missions;
using K4System.Players;

/// <summary>
/// Point wagers: a challenge with a type, an accept, then only kills between the two players that fit the type
/// count. First to the target wins at once; at the time limit the leader wins and a tie returns both stakes.
/// Stakes are taken at the accept, so a player who leaves has already paid. After a duel both players wait a
/// cooldown. Several duels can run at once, each player in at most one. Game thread only.
/// </summary>
/// <param name="map">What the current map offers (MissionService.Map); null or a null result rules no type out.</param>
internal sealed class DuelService(PluginConfig config, PlayerRegistry players, RankService ranks, GameState game, Chat chat, HudBridge hud, Action<float, Action> addTimer, Func<MapAbilities?>? map = null)
{
	private sealed record Offer(int Id, ulong Challenger, string ChallengerName, ulong Opponent, string OpponentName, int Stake, DuelType Type, DateTime ExpiresUtc);

	private sealed class Duel(Offer terms, int target, DateTime endsUtc)
	{
		public Offer Terms { get; } = terms;
		public int Target { get; } = target;
		public DateTime EndsUtc { get; } = endsUtc;
		public int ChallengerKills { get; set; }
		public int OpponentKills { get; set; }
	}

	/// <summary>Why a duel command did nothing: a language key and its arguments for the reply.</summary>
	public readonly record struct Refusal(string Key, object[] Args);

	private DuelSettings Settings => config.Duels;

	private readonly List<Offer> pending = [];
	private readonly List<Duel> running = [];
	private readonly Dictionary<ulong, DateTime> lastChallengeUtc = [];
	private readonly Dictionary<ulong, DateTime> lastDuelEndUtc = [];
	private int nextId;
	private bool ticking;

	/// <summary>Set at the match end: no new duels in the intermission before the map changes.</summary>
	private bool matchOver;

	public int AcceptSeconds => Settings.AcceptSeconds;

	// ---------------------------------------------------------------- what the panel reads

	/// <summary>Enabled types the current map can offer, in panel order.</summary>
	public IReadOnlyList<DuelType> AvailableTypes => DuelRules.Available(Settings.Types, map?.Invoke()?.Weapons);

	/// <summary>The stake buttons; "max" is MaxStake.</summary>
	public IReadOnlyList<int> StakePresets => Settings.Stakes;

	/// <summary>The largest stake these two can put up now, or 0 when they cannot duel for points.</summary>
	public int MaxStake(K4Player challenger, K4Player opponent)
		=> challenger.Rank is null || opponent.Rank is null ? 0
			: DuelRules.MaxStake(challenger.Rank.Points, opponent.Rank.Points, Settings.MaxStakePercent, Settings.MinStake);

	/// <summary>A duel is running or a challenge waits for an answer, so countdowns are moving.</summary>
	public bool Live => running.Count > 0 || pending.Count > 0;

	public IReadOnlyList<DuelView> Running
	{
		get
		{
			DateTime now = DateTime.UtcNow;
			return [.. running.Select(duel => View(duel, now))];
		}
	}

	public DuelView? RunningFor(ulong steamId)
		=> FindRunning(steamId) is Duel duel ? View(duel, DateTime.UtcNow) : null;

	/// <summary>A challenge waiting for this player's answer.</summary>
	public DuelOffer? IncomingFor(ulong steamId)
		=> pending.Find(offer => offer.Opponent == steamId) is Offer offer ? View(offer, DateTime.UtcNow) : null;

	/// <summary>A challenge this player sent that is still waiting for an answer.</summary>
	public DuelOffer? OutgoingFor(ulong steamId)
		=> pending.Find(offer => offer.Challenger == steamId) is Offer offer ? View(offer, DateTime.UtcNow) : null;

	/// <summary>Time until this player may start or accept a duel again.</summary>
	public TimeSpan CooldownLeft(ulong steamId)
		=> ResetPolicy.CommandCooldownRemaining(DateTime.UtcNow, lastDuelEndUtc.TryGetValue(steamId, out DateTime ended) ? ended : null, Settings.CooldownSeconds);

	private static DuelView View(Duel duel, DateTime now)
	{
		Offer terms = duel.Terms;
		return new DuelView(terms.Id, terms.Challenger, terms.ChallengerName, terms.Opponent, terms.OpponentName,
			duel.ChallengerKills, duel.OpponentKills, terms.Stake, terms.Type, duel.Target, DuelRules.WholeSeconds(duel.EndsUtc - now));
	}

	private static DuelOffer View(Offer offer, DateTime now)
		=> new(offer.Id, offer.Challenger, offer.ChallengerName, offer.Opponent, offer.OpponentName, offer.Stake, offer.Type, DuelRules.WholeSeconds(offer.ExpiresUtc - now));

	private Duel? FindRunning(ulong steamId) => running.Find(duel => duel.Terms.Challenger == steamId || duel.Terms.Opponent == steamId);

	private void RefreshPanels() => hud.Hud?.RefreshOpenPanels();

	// ---------------------------------------------------------------- commands

	/// <summary>
	/// Null when the challenger may challenge now (and, when given, this opponent): for greying out the panel.
	/// Stake and type are checked by Challenge.
	/// </summary>
	public Refusal? CanChallenge(K4Player challenger, K4Player? opponent)
	{
		if (matchOver || !ranks.IsPointsAllowed())
			return new Refusal("duel.error.not-now", []);
		if (opponent is not null && challenger.SteamId == opponent.SteamId)
			return new Refusal("duel.error.self", []);
		if (Cooldown(challenger, opponent) is Refusal cooldown)
			return cooldown;
		if (Involved(challenger.SteamId))
			return new Refusal("duel.error.busy", [challenger.Controller.PlayerName]);
		if (opponent is not null && Involved(opponent.SteamId))
			return new Refusal("duel.error.busy", [opponent.Controller.PlayerName]);
		// Every challenge is announced to everyone, so sending and taking back challenges cannot flood the chat.
		TimeSpan wait = ResetPolicy.CommandCooldownRemaining(DateTime.UtcNow, lastChallengeUtc.TryGetValue(challenger.SteamId, out DateTime last) ? last : null, Settings.AcceptSeconds);
		if (wait > TimeSpan.Zero)
			return new Refusal("error.cooldown", [DuelRules.WholeSeconds(wait)]);
		if (opponent is not null && Settings.BlockSameIp && Showdowns.SameAddress(challenger.Controller.IpAddress, opponent.Controller.IpAddress))
			return new Refusal("duel.error.same-ip", []);
		return null;
	}

	private Refusal? Cooldown(K4Player player, K4Player? other)
	{
		int mine = DuelRules.WholeSeconds(CooldownLeft(player.SteamId));
		if (mine > 0)
			return new Refusal("duel.error.cooldown", [mine]);
		int theirs = other is null ? 0 : DuelRules.WholeSeconds(CooldownLeft(other.SteamId));
		return theirs > 0 ? new Refusal("duel.error.target-cooldown", [other!.Controller.PlayerName, theirs]) : null;
	}

	public Refusal? Challenge(K4Player challenger, K4Player opponent, int stake) => Challenge(challenger, opponent, stake, DuelType.Any);

	public Refusal? Challenge(K4Player challenger, K4Player opponent, int stake, DuelType type)
	{
		if (CanChallenge(challenger, opponent) is Refusal refusal)
			return refusal;

		IReadOnlyList<DuelType> types = AvailableTypes;
		if (!types.Contains(type))
			return new Refusal("duel.error.type", [DuelRules.Name(type), string.Join(", ", types.Select(DuelRules.Name))]);

		int max = MaxStake(challenger, opponent);
		if (max == 0)
			return new Refusal("duel.error.no-points", []);
		if (!DuelRules.StakeAllowed(stake, Settings.MinStake, max))
			return new Refusal("duel.error.stake", [Settings.MinStake, max]);

		DateTime now = DateTime.UtcNow;
		Offer challenge = new(++nextId, challenger.SteamId, challenger.Controller.PlayerName, opponent.SteamId, opponent.Controller.PlayerName,
			stake, type, now.AddSeconds(Settings.AcceptSeconds));
		pending.Add(challenge);
		lastChallengeUtc[challenger.SteamId] = now;

		foreach (K4Player reader in players.ValidHumans)
		{
			chat.Tell(reader.Controller, "duel.challenge-typed", challenge.ChallengerName, challenge.OpponentName, TypeName(reader, type), stake,
				Settings.TargetKills, Settings.Minutes);
		}

		string command = config.Commands.Duel.FirstOrDefault() ?? "duel";
		if (!ToastTo(opponent, chat.For(opponent.Controller, "toast.duel", command), $"{challenge.ChallengerName} · {TypeName(opponent, type)}",
			Number(stake), ToastColor.Purple, type, ToastLength.Long))
		{
			chat.Tell(opponent.Controller, "duel.how-to", command, Settings.AcceptSeconds);
		}

		RefreshPanels();

		addTimer(Settings.AcceptSeconds, () =>
		{
			if (!pending.Remove(challenge))
				return;
			TellBoth(challenge, "duel.expired", challenge.ChallengerName, challenge.OpponentName);
			RefreshPanels();
		});

		return null;
	}

	public Refusal? Accept(K4Player opponent)
	{
		if (pending.Find(challenge => challenge.Opponent == opponent.SteamId) is not Offer challenge)
			return new Refusal("duel.error.no-challenge", []);

		if (matchOver || !ranks.IsPointsAllowed())
			return new Refusal("duel.error.not-now", []);

		K4Player? challenger = players.Get(challenge.Challenger);
		if (challenger is not null && Cooldown(opponent, challenger) is Refusal cooldown)
			return cooldown;

		pending.Remove(challenge);
		// Points may have dropped since the challenge, so the stake must still be within the cap now.
		if (challenger is not { IsHuman: true, Rank: not null } || opponent.Rank is null
			|| challenge.Stake > Showdowns.MaxStake(challenger.Rank.Points, opponent.Rank.Points, Settings.MaxStakePercent))
		{
			TellBoth(challenge, "duel.declined", challenge.ChallengerName, challenge.OpponentName);
			RefreshPanels();
			return new Refusal("duel.error.no-points", []);
		}

		Hold(challenger, challenge.Stake);
		Hold(opponent, challenge.Stake);
		running.Add(new Duel(challenge, Settings.TargetKills, DateTime.UtcNow.AddMinutes(Settings.Minutes)));

		foreach (K4Player reader in players.ValidHumans)
			chat.Tell(reader.Controller, "duel.started-typed", challenge.ChallengerName, challenge.OpponentName, TypeName(reader, challenge.Type), challenge.Stake, Settings.TargetKills);

		ToastTo(challenger, chat.For(challenger.Controller, "toast.duel-started"), chat.For(challenger.Controller, "toast.duel-vs", challenge.OpponentName, TypeName(challenger, challenge.Type), Settings.TargetKills),
			Number(challenge.Stake), ToastColor.Purple, challenge.Type);
		ToastTo(opponent, chat.For(opponent.Controller, "toast.duel-started"), chat.For(opponent.Controller, "toast.duel-vs", challenge.ChallengerName, TypeName(opponent, challenge.Type), Settings.TargetKills),
			Number(challenge.Stake), ToastColor.Purple, challenge.Type);

		RefreshPanels();
		EnsureTicking();
		return null;
	}

	/// <summary>Turns down a challenge, or takes back one the player sent.</summary>
	public Refusal? Decline(K4Player player)
	{
		if (pending.Find(challenge => challenge.Opponent == player.SteamId || challenge.Challenger == player.SteamId) is not Offer challenge)
			return new Refusal("duel.error.no-challenge", []);

		pending.Remove(challenge);
		TellBoth(challenge, "duel.declined", challenge.ChallengerName, challenge.OpponentName);
		RefreshPanels();
		return null;
	}

	private bool Involved(ulong steamId)
		=> pending.Exists(challenge => challenge.Challenger == steamId || challenge.Opponent == steamId) || FindRunning(steamId) is not null;

	private void Hold(K4Player player, int stake) => ranks.DebitPoints(player, stake);

	private void TellBoth(Offer challenge, string key, params object[] args)
	{
		foreach (ulong steamId in new[] { challenge.Challenger, challenge.Opponent })
		{
			if (players.Get(steamId) is { HasController: true } player)
				chat.Tell(player.Controller, key, args);
		}
	}

	private string TypeName(K4Player reader, DuelType type) => chat.For(reader.Controller, "duel.type." + DuelRules.Name(type));

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>Picture for each type, from docs/panel-contract.md.</summary>
	public static string TypeVisual(DuelType type) => type switch
	{
		DuelType.Noscope => "dn-noscope",
		DuelType.Zeus => "eq-taser",
		DuelType.Knife => "eq-knife",
		DuelType.Scout => "eq-ssg08",
		DuelType.Headshot => "ic-kill_headshot",
		_ => "ic-kill"
	};

	/// <summary>Shows a duel toast; false when the player has no panel or turned toasts off, so the caller uses chat.</summary>
	private bool ToastTo(K4Player player, string kind, string text, string value, ToastColor color, DuelType type, ToastLength length = ToastLength.Normal)
	{
		if (hud.Hud is not PanoramaHud panel || player.ToastsOff || !player.HasController)
			return false;

		panel.Toast(player, new Toast(kind, text, value, TypeVisual(type), color, length));
		return true;
	}

	// ---------------------------------------------------------------- game events

	/// <summary>Counts a kill when the killer and the victim are the two players of one duel and the kill fits its type.</summary>
	public void OnPlayerDeath(DeathActor? victim, DeathActor? attacker, string? weapon, bool headshot, bool noscope)
	{
		if (running.Count == 0 || game.IsWarmup || attacker is not DeathActor killer || victim is not DeathActor killed)
			return;

		if (DeathRules.Classify(victim, attacker, null, config.Tracking.CountBots, config.Tracking.FreeForAll).AttackerPoint != AttackerPointAction.Kill)
			return;

		if (FindRunning(killer.SteamId) is not Duel duel)
			return;

		bool challengerKilled = duel.Terms.Challenger == killer.SteamId && duel.Terms.Opponent == killed.SteamId;
		bool opponentKilled = duel.Terms.Opponent == killer.SteamId && duel.Terms.Challenger == killed.SteamId;
		if (!(challengerKilled || opponentKilled) || !DuelRules.Counts(duel.Terms.Type, weapon ?? string.Empty, headshot, noscope))
			return;

		if (challengerKilled)
			duel.ChallengerKills++;
		else
			duel.OpponentKills++;

		if (DuelRules.Decide(true, duel.ChallengerKills, true, duel.OpponentKills, duel.Target, over: false) is null)
			ShowScore(duel);
		else
			Finish(duel, announce: true);

		RefreshPanels();
	}

	/// <summary>"3 : 2" from each player's side, as a short toast or a chat line.</summary>
	private void ShowScore(Duel duel)
	{
		Offer terms = duel.Terms;
		ShowScore(Present(terms.Challenger), terms.OpponentName, duel.ChallengerKills, duel.OpponentKills, terms.Type);
		ShowScore(Present(terms.Opponent), terms.ChallengerName, duel.OpponentKills, duel.ChallengerKills, terms.Type);
	}

	private void ShowScore(K4Player? player, string other, int mine, int theirs, DuelType type)
	{
		if (player is not { HasController: true })
			return;

		if (!ToastTo(player, chat.For(player.Controller, "toast.duel-score"), chat.For(player.Controller, "toast.duel-score-text", other), $"{mine} : {theirs}",
			ToastColor.Purple, type, ToastLength.Short))
		{
			chat.Tell(player.Controller, "duel.score", other, mine, theirs);
		}
	}

	/// <summary>An open challenge ends when either player leaves. A running duel waits: they may reconnect.</summary>
	public void OnDisconnect(CCSPlayerController? controller, int reason)
	{
		// A map change is not leaving; open challenges end with the map anyway.
		if (reason == PlayerService.MapChangeDisconnectReason || players.Get(controller) is not K4Player player)
			return;

		bool changed = false;
		foreach (Offer challenge in pending.Where(challenge => challenge.Challenger == player.SteamId || challenge.Opponent == player.SteamId).ToArray())
		{
			pending.Remove(challenge);
			TellBoth(challenge, "duel.cancelled-left", player.Controller.PlayerName);
			changed = true;
		}

		if (changed)
			RefreshPanels();
	}

	/// <summary>Pays every running duel and closes duels until the next map.</summary>
	public void OnMatchEnd()
	{
		matchOver = true;
		Settle(announce: true);
	}

	/// <summary>The map changed without a match end. Settled quietly before the map-end save.</summary>
	public void OnMapEnd() => Settle(announce: false);

	/// <summary>A new match on the same map (mp_match_end_restart, mp_restartgame) opens duels again.</summary>
	public void OnMatchStart() => matchOver = false;

	public void OnMapStart()
	{
		running.Clear();
		pending.Clear();
		lastChallengeUtc.Clear();
		matchOver = false;
	}

	// The accept timer is one-shot, so the time-limit check re-arms itself while duels run.
	private void EnsureTicking()
	{
		if (ticking)
			return;

		ticking = true;
		addTimer(1f, () =>
		{
			ticking = false;
			DateTime now = DateTime.UtcNow;
			Duel[] timeUp = [.. running.Where(duel => duel.EndsUtc <= now)];
			foreach (Duel duel in timeUp)
				Finish(duel, announce: true);
			if (timeUp.Length > 0)
				RefreshPanels();
			if (running.Count > 0)
				EnsureTicking();
		});
	}

	private void Settle(bool announce)
	{
		bool any = running.Count > 0 || pending.Count > 0;
		foreach (Duel duel in running.ToArray())
			Finish(duel, announce);

		pending.Clear();
		if (any && announce)
			RefreshPanels();
	}

	/// <summary>Ends one duel and pays it out. A player who has left the server loses; leavers are no longer in the player list.</summary>
	private void Finish(Duel duel, bool announce)
	{
		running.Remove(duel);
		Offer terms = duel.Terms;
		K4Player? challenger = Present(terms.Challenger);
		K4Player? opponent = Present(terms.Opponent);
		int pot = PointMath.AddClamped(terms.Stake, terms.Stake);

		DateTime now = DateTime.UtcNow;
		lastDuelEndUtc[terms.Challenger] = now;
		lastDuelEndUtc[terms.Opponent] = now;

		switch (DuelRules.Decide(challenger is not null, duel.ChallengerKills, opponent is not null, duel.OpponentKills, duel.Target, over: true))
		{
			case 1:
				Pay(challenger!, opponent, terms.ChallengerName, terms.OpponentName, pot, terms.Stake, duel.ChallengerKills, duel.OpponentKills, terms.Type, announce);
				break;
			case -1:
				Pay(opponent!, challenger, terms.OpponentName, terms.ChallengerName, pot, terms.Stake, duel.OpponentKills, duel.ChallengerKills, terms.Type, announce);
				break;
			default:
				if (challenger is null || opponent is null)
					break;
				ranks.CreditPoints(challenger, terms.Stake, refresh: announce);
				ranks.CreditPoints(opponent, terms.Stake, refresh: announce);
				if (!announce)
					break;
				chat.TellAll("duel.tie", terms.ChallengerName, terms.OpponentName, duel.ChallengerKills);
				ToastTo(challenger, chat.For(challenger.Controller, "toast.duel-tie"), chat.For(challenger.Controller, "toast.duel-result", terms.OpponentName, duel.ChallengerKills, duel.OpponentKills),
					Number(terms.Stake), ToastColor.Purple, terms.Type);
				ToastTo(opponent, chat.For(opponent.Controller, "toast.duel-tie"), chat.For(opponent.Controller, "toast.duel-result", terms.ChallengerName, duel.OpponentKills, duel.ChallengerKills),
					Number(terms.Stake), ToastColor.Purple, terms.Type);
				break;
		}
	}

	private void Pay(K4Player winner, K4Player? loser, string winnerName, string loserName, int pot, int stake, int winnerKills, int loserKills, DuelType type, bool announce)
	{
		ranks.CreditPoints(winner, pot, refresh: announce);
		if (!announce)
			return;

		if (loser is null)
			chat.TellAll("duel.forfeit", winnerName, loserName, pot);
		else
			chat.TellAll("duel.won", winnerName, loserName, winnerKills, loserKills, pot);

		ToastTo(winner, chat.For(winner.Controller, "toast.duel-won"), chat.For(winner.Controller, "toast.duel-result", loserName, winnerKills, loserKills),
			$"+{Number(pot)}", ToastColor.Gold, type);
		if (loser is not null)
		{
			ToastTo(loser, chat.For(loser.Controller, "toast.duel-lost"), chat.For(loser.Controller, "toast.duel-result", winnerName, loserKills, winnerKills),
				$"-{Number(stake)}", ToastColor.Red, type);
		}
	}

	private K4Player? Present(ulong steamId)
		=> players.Get(steamId) is { IsHuman: true } player ? player : null;

	/// <summary>
	/// K4 unloads: duels cannot finish, so both players get their stake back. A player who already left has lost,
	/// as at the match end, so the one still here gets both stakes. Touches no entities.
	/// </summary>
	public void ReturnStakes()
	{
		foreach (Duel duel in running)
		{
			K4Player? challenger = Present(duel.Terms.Challenger);
			K4Player? opponent = Present(duel.Terms.Opponent);
			(int toChallenger, int toOpponent) = Showdowns.UnloadRefund(challenger is not null, opponent is not null, duel.Terms.Stake);

			if (challenger is not null)
				ranks.CreditPoints(challenger, toChallenger, refresh: false);
			if (opponent is not null)
				ranks.CreditPoints(opponent, toOpponent, refresh: false);
		}

		running.Clear();
		pending.Clear();
	}
}
