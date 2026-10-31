namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Players;
using K4System.Ranks;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>Points, rank changes, rank-granted permissions, and every point-awarding game event.</summary>
internal sealed class RankService(
	PluginConfig config,
	RankLadder ladder,
	PlayerRegistry players,
	GameState game,
	RewardService rewards,
	ClanTagService clanTags,
	SqlPlayerStore store,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	HudBridge hud,
	ILogger logger)
{
	private RankSettings Settings => config.Ranks;
	private PointValues Points => config.PointValues;

	public RankLadder Ladder => ladder;

	public bool IsPointsAllowed() => game.IsTrackingAllowed(config.Tracking.CountWarmup, config.Tracking.MinimumPlayers);

	public RankDefinition ResolveRank(K4Player player, int points)
		=> ladder.Resolve(points, permission => Permissions.Has(player, permission));

	public IReadOnlyList<RankDefinition> Progression(K4Player player)
		=> ladder.Progression(permission => Permissions.Has(player, permission));

	// ---------------------------------------------------------------- point changes

	/// <summary>An automatic point change from a game event. Ignored while points are not allowed.</summary>
	public void ModifyPoints(K4Player? player, int amount, string reason, ChatText? extra = null)
	{
		if (player is null || !IsPointsAllowed())
			return;

		ApplyPoints(player, amount, reason, extra);
	}

	/// <summary>
	/// An automatic point change after the caller checked <see cref="IsPointsAllowed"/>.
	/// Pass refreshRank: false to combine several bonuses into one rank change.
	/// </summary>
	public void ApplyPoints(K4Player player, int amount, string reason, ChatText? extra = null, bool refreshRank = true)
	{
		RankState? state = player.Rank;
		if (state is null || amount == 0 || !player.IsValid || !player.IsHuman)
			return;

		if (!ladder.IsAutomaticScoringAllowed(permission => Permissions.Has(player, permission)))
		{
			RefreshRank(player, announce: false, allowRewards: false);
			return;
		}

		if (amount > 0 && AdminManager.PlayerHasPermissions(player.Controller, Permissions.VipMultiplier))
			amount = PointMath.Scale(amount, Settings.VipMultiplier);

		int oldPoints = state.Points;
		state.Points = PointMath.AddToStored(oldPoints, amount);
		int applied = state.Points - oldPoints;

		// Points after the summary was printed (MVP, exit frags) get their own message instead of waiting a round.
		if (Settings.RoundSummary && game.HasRules && !game.IsWarmup && !roundSummarized)
		{
			state.RoundPoints = PointMath.AddClamped(state.RoundPoints, applied);
			if (applied != 0)
			{
				(int count, int points) = state.RoundReasons.GetValueOrDefault(reason);
				state.RoundReasons[reason] = (count + 1, PointMath.AddClamped(points, applied));
			}
		}
		else if (applied != 0)
			AnnouncePointChange(player, state, applied, reason, extra);

		if (refreshRank)
			RefreshRank(player, announce: true, allowRewards: true);
	}

	private void AnnouncePointChange(K4Player player, RankState state, int applied, string reason, ChatText? extra)
	{
		gameThread.Post(() =>
		{
			if (state.MuteMessages || !player.IsValid)
				return;

			CCSPlayerController controller = player.Controller;
			string key = applied > 0 ? "points.gain" : "points.loss";
			string message = chat.PrefixedFor(controller, key, state.Points, Math.Abs((long)applied), chat.For(controller, reason));

			if (extra is ChatText text)
				message += " " + chat.For(controller, text.Key, text.Args);

			controller.PrintToChat(message);
		});
	}

	/// <summary>
	/// A manual change from an admin command, a reset, or the shared API. It ignores the game-mode
	/// scoring rules and never prints a point message.
	/// </summary>
	public void SetPoints(K4Player player, int points, int? roundPoints = null)
	{
		RankState? state = player.Rank;
		if (state is null || !player.IsValid || !player.IsHuman)
			return;

		state.Points = PointMath.ClampStored(points);

		if (roundPoints.HasValue)
			state.RoundPoints = roundPoints.Value;

		RefreshRank(player, announce: false, allowRewards: true);
	}

	/// <summary>
	/// Adds earned reward points, such as mission rewards. Unlike event points these ignore the
	/// rank mode, minimum players, and VIP multiplier, and the caller announces them.
	/// </summary>
	public void GrantPoints(K4Player player, int amount)
	{
		RankState? state = player.Rank;
		if (state is null || amount <= 0 || !player.IsValid || !player.IsHuman)
			return;

		state.Points = PointMath.AddToStored(state.Points, amount);
		RefreshRank(player, announce: true, allowRewards: true);
	}

	/// <summary>
	/// Pays out held points, such as a duel pot or a returned stake. Unlike <see cref="GrantPoints"/> it also
	/// works when the player has no pawn (spectating, or between maps); the rank catches up on the next spawn.
	/// Pass refresh: false at map end or unload, when the player's entities must not be touched.
	/// </summary>
	public void CreditPoints(K4Player player, int amount, bool refresh = true)
	{
		if (player.Rank is not RankState state || amount <= 0 || !player.IsHuman)
			return;

		state.Points = PointMath.AddToStored(state.Points, amount);
		if (refresh && player.IsValid)
			RefreshRank(player, announce: true, allowRewards: true);
	}

	/// <summary>Holds points back, such as a duel stake. Like <see cref="CreditPoints"/>, it works without a pawn.</summary>
	public void DebitPoints(K4Player player, int amount)
	{
		if (player.Rank is not RankState state || amount <= 0 || !player.IsHuman)
			return;

		state.Points = PointMath.AddToStored(state.Points, -amount);
		if (player.IsValid)
			RefreshRank(player, announce: false, allowRewards: false);
	}

	/// <summary>Recomputes the rank, then syncs rank permissions and the clan tag.</summary>
	public void RefreshRank(K4Player player, bool announce, bool allowRewards)
	{
		RankState? state = player.Rank;
		if (state is null)
			return;

		if (config.Scoreboard.ScoreShowsPoints && player.Controller.Score != state.Points)
		{
			player.Controller.Score = state.Points;
			Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_iScore");
		}

		RankDefinition oldRank = state.Rank;
		RankDefinition newRank = ResolveRank(player, state.Points);

		if (!ReferenceEquals(oldRank, newRank) && oldRank.Key != newRank.Key)
		{
			if (announce)
			{
				bool demoted = oldRank.Point > newRank.Point;
				if (hud.Hud is PanoramaHud panel)
					panel.ShowToast(player, demoted ? "hud.toast.demoted" : "hud.toast.promoted", newRank.Name,
						demoted ? ToastKind.Warning : ToastKind.Gold, demoted ? null : panel.RankIcon(newRank) ?? "ic-trade_up");
				else
					chat.Tell(player.Controller, demoted ? "rank.demoted" : "rank.promoted", newRank.Color, newRank.Name);
			}

			state.Rank = newRank;

			if (allowRewards)
				rewards.QueuePromotion(player, oldRank, newRank);
		}

		SyncRankPermissions(player, newRank);
		clanTags.Apply(player);
	}

	public void RefreshClanTag(K4Player player) => clanTags.Apply(player);

	// Grants only K4-owned @k4system/ permissions, and never claims one the player already had.
	private static void SyncRankPermissions(K4Player player, RankDefinition rank)
	{
		SteamID steamId = new(player.SteamId);
		HashSet<string> wanted = rank.Permissions?
			.Select(permission => permission.PermissionName.Trim())
			.Where(permission => permission.Length > 0)
			.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string permission in player.OwnedPermissions.Except(wanted, StringComparer.OrdinalIgnoreCase).ToArray())
		{
			AdminManager.RemovePlayerPermissions(steamId, permission);
			player.OwnedPermissions.Remove(permission);
		}

		foreach (string permission in wanted)
		{
			if (player.OwnedPermissions.Contains(permission) || AdminManager.PlayerHasPermissions(steamId, permission))
				continue;

			AdminManager.AddPlayerPermissions(steamId, permission);
			player.OwnedPermissions.Add(permission);
		}
	}

	/// <summary>Removes K4-granted permissions and K4's clan tag when the player leaves or the plugin unloads.</summary>
	public void ReleasePlayer(K4Player player)
	{
		SteamID steamId = new(player.SteamId);
		foreach (string permission in player.OwnedPermissions)
			AdminManager.RemovePlayerPermissions(steamId, permission);

		player.OwnedPermissions.Clear();
		clanTags.Release(player);
	}

	public int DynamicPoints(K4Player from, K4Player to, int amount)
	{
		if (!Settings.DynamicDeathPoints.Enabled || !from.IsHuman || !to.IsHuman || from.Rank is null || to.Rank is null)
			return amount;

		return PointMath.Dynamic(amount, from.Rank.Points, to.Rank.Points, Settings.DynamicDeathPoints.MinMultiplier, Settings.DynamicDeathPoints.MaxMultiplier);
	}

	// ---------------------------------------------------------------- game events

	public void OnPlayerDeath(EventPlayerDeath @event, DeathActor? victimActor, DeathActor? attackerActor, DeathActor? assisterActor)
	{
		if (!victimActor.HasValue || !IsPointsAllowed())
			return;

		DeathDecision decision = DeathRules.Classify(victimActor, attackerActor, assisterActor, config.Tracking.CountBots, config.Tracking.FreeForAll);

		K4Player? victim = players.Get(@event.Userid);
		K4Player? attacker = players.Get(@event.Attacker);

		// Anti-farm: a kill on someone from the same connection, or the same victim over and over, is worth less to both.
		double share = decision.AttackerPoint == AttackerPointAction.Kill ? KillShare(attacker, victim) : 1;
		ChatText? farmNote = share < 1 && share > 0 ? new ChatText("reason.repeat-kill", [(int)Math.Round(share * 100)]) : null;

		if (decision.VictimPoint != VictimPointAction.None && victim is { IsHuman: true, Rank: not null })
		{
			victim.KillStreak = 0;
			victim.LastKillUtc = DateTime.UtcNow;

			if (decision.VictimPoint == VictimPointAction.Suicide)
				ApplyPoints(victim, Points.Suicide, "reason.suicide");
			else
			{
				string attackerName = @event.Attacker?.PlayerName ?? attacker?.Name ?? string.Empty;
				ChatText? extra = Settings.NamesInKillMessages ? new ChatText("reason.death-by", [attackerName, attacker?.Rank?.Points ?? 0]) : null;
				int deathPoints = attacker?.IsValid == true ? DynamicPoints(attacker, victim, Points.Death) : Points.Death;
				ApplyPoints(victim, PointMath.Scale(deathPoints, share), "reason.death", extra);
			}
		}

		if (decision.AttackerPoint != AttackerPointAction.None && attacker is { IsValid: true, IsHuman: true })
		{
			if (decision.AttackerPoint == AttackerPointAction.TeamKill)
				ApplyPoints(attacker, Points.TeamKill, "reason.team-kill", refreshRank: false);
			else if (share > 0)
				ApplyKillPoints(@event, attacker, victim, share, farmNote);

			// All bonuses of one kill end in a single rank change.
			RefreshRank(attacker, announce: true, allowRewards: true);
		}

		K4Player? assister = players.Get(@event.Assister);
		if (decision.CountAssister && assister is { IsValid: true, IsHuman: true })
		{
			bool enemy = decision.AssisterVictimIsEnemy;
			bool flash = @event.Assistedflash;

			// An assist on a farmed kill is worth the same share as the kill.
			int Fair(int amount) => amount > 0 ? PointMath.Scale(amount, share) : amount;

			if (enemy && !flash)
				ApplyPoints(assister, Fair(Points.Assist), "reason.assist");
			else if (!enemy && !flash)
				ApplyPoints(assister, Points.TeamAssist, "reason.team-assist");
			else if (enemy)
				ApplyPoints(assister, Fair(Points.AssistFlash), "reason.flash-assist");
			else
				ApplyPoints(assister, Points.TeamAssistFlash, "reason.team-flash-assist");
		}
	}

	/// <summary>The anti-farm share of a kill's points; recording the kill for the repeat-kill rule.</summary>
	private double KillShare(K4Player? attacker, K4Player? victim)
	{
		AntiFarmSettings farm = config.AntiFarm;
		if (attacker is not { IsValid: true, IsHuman: true } || victim is not { IsValid: true, IsHuman: true } || ReferenceEquals(attacker, victim))
			return 1;

		bool sameAddress = farm.SameIp && Showdowns.SameAddress(attacker.Controller.IpAddress, victim.Controller.IpAddress);
		int earlier = 0;
		if (farm.RepeatKills > 0)
		{
			if (!killHistories.TryGetValue(attacker.SteamId, out KillHistory? history))
				killHistories[attacker.SteamId] = history = new KillHistory();
			earlier = history.Record(victim.SteamId, DateTime.UtcNow, TimeSpan.FromMinutes(farm.WindowMinutes));
		}
		return FarmGuard.KillShare(sameAddress, earlier, farm.RepeatKills, farm.ReducedPercent);
	}

	// By SteamID, not on the player, so reconnecting does not start the repeat-kill count again.
	private readonly Dictionary<ulong, KillHistory> killHistories = [];

	private void PruneKillHistories(DateTime nowUtc)
	{
		TimeSpan window = TimeSpan.FromMinutes(config.AntiFarm.WindowMinutes);
		foreach ((ulong killer, KillHistory history) in killHistories.ToArray())
		{
			history.Prune(nowUtc, window);
			if (history.VictimCount == 0)
				killHistories.Remove(killer);
		}
	}

	private void ApplyKillPoints(EventPlayerDeath @event, K4Player attacker, K4Player? victim, double share, ChatText? farmNote)
	{
		int Fair(int amount) => share >= 1 ? amount : PointMath.Scale(amount, share);

		string victimName = @event.Userid?.PlayerName ?? victim?.Name ?? string.Empty;
		ChatText? extra = farmNote ?? (Settings.NamesInKillMessages ? new ChatText("reason.kill-of", [victimName, victim?.Rank?.Points ?? 0]) : null);
		int killPoints = victim is not null ? DynamicPoints(attacker, victim, Points.Kill) : Points.Kill;
		ApplyPoints(attacker, Fair(killPoints), "reason.kill", extra, refreshRank: false);

		if (@event.Headshot)
			ApplyPoints(attacker, Fair(Points.Headshot), "reason.headshot", refreshRank: false);

		if (@event.Penetrated > 0 && Points.Penetrated > 0)
			ApplyPoints(attacker, Fair(PointMath.Scale(Points.Penetrated, @event.Penetrated)), "reason.penetrated", refreshRank: false);

		if (@event.Noscope)
			ApplyPoints(attacker, Fair(Points.NoScope), "reason.noscope", refreshRank: false);

		if (@event.Thrusmoke)
			ApplyPoints(attacker, Fair(Points.Thrusmoke), "reason.through-smoke", refreshRank: false);

		if (@event.Attackerblind)
			ApplyPoints(attacker, Fair(Points.BlindKill), "reason.blind-kill", refreshRank: false);

		if (@event.Distance >= Points.LongDistance)
			ApplyPoints(attacker, Fair(Points.LongDistanceKill), "reason.long-distance", refreshRank: false);

		if (KillBonuses.ForWeapon(@event.Weapon, Points) is PointBonus weaponBonus)
			ApplyPoints(attacker, Fair(weaponBonus.Points), weaponBonus.Phrase, refreshRank: false);

		DateTime now = DateTime.UtcNow;
		(int streak, PointBonus? streakBonus) = KillBonuses.NextStreak(attacker.KillStreak, attacker.LastKillUtc, now, Settings.KillstreakWindowSeconds, Points);
		attacker.KillStreak = streak;
		attacker.LastKillUtc = now;

		if (streakBonus is PointBonus bonus)
			ApplyPoints(attacker, Fair(bonus.Points), bonus.Phrase, refreshRank: false);
	}

	public void OnTeamChange(K4Player player, bool disconnect, int team, int oldTeam)
	{
		if (player.Rank is null)
			return;

		if (!disconnect && team != oldTeam)
			player.Rank.PlayedRound = false;

		RefreshRank(player, announce: false, allowRewards: true);
	}

	public void OnBombDefused(K4Player? defuser)
	{
		if (defuser is not { IsValid: true, IsHuman: true } || !IsPointsAllowed())
			return;

		ApplyPoints(defuser, Points.BombDefused, "reason.bomb-defused");

		foreach (K4Player teammate in players.ValidHumans.ToArray())
		{
			if (!ReferenceEquals(teammate, defuser) && teammate.Controller.Team == CsTeam.CounterTerrorist)
				ApplyPoints(teammate, Points.BombDefusedOthers, "reason.bomb-defused");
		}
	}

	/// <summary>Gives every valid player on one team the same points, for bomb and hostage objectives.</summary>
	public void RewardTeam(CsTeam team, int amount, string reason)
	{
		if (!IsPointsAllowed())
			return;

		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			if (player.Controller.Team == team)
				ApplyPoints(player, amount, reason);
		}
	}

	public void OnMvp(K4Player? player)
	{
		if (player is not null && IsPointsAllowed())
			ApplyPoints(player, Points.MVP, "reason.mvp");
	}

	private bool roundSummarized;

	public void OnRoundStart()
	{
		roundSummarized = false;
		PruneKillHistories(DateTime.UtcNow);

		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			if (player.Rank is null)
				continue;

			player.Rank.PlayedRound = true;
			RefreshRank(player, announce: false, allowRewards: true);
			rewards.RetryPending(player);
		}

		if (GameState.TryCountEligibleHumans(out int humans) && humans < config.Tracking.MinimumPlayers)
			chat.TellAll("rank.not-enough-players", config.Tracking.MinimumPlayers);
	}

	public void OnRoundEnd(int winnerTeam)
	{
		bool pointsAllowed = IsPointsAllowed();
		DateTime now = DateTime.UtcNow;

		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			RankState? state = player.Rank;

			if (!pointsAllowed)
			{
				if (state is not null)
				{
					state.RoundPoints = 0;
					state.RoundReasons.Clear();
				}
				continue;
			}

			if (!player.Controller.PawnIsAlive || Settings.ResetKillstreakOnRoundEnd)
			{
				player.KillStreak = 0;
				player.LastKillUtc = now;
			}

			if (state is null)
				continue;

			if (!state.PlayedRound || player.Controller.TeamNum <= (int)CsTeam.Spectator)
			{
				state.RoundPoints = 0;
				state.RoundReasons.Clear();
				continue;
			}

			// A draw (no winning team) is neither a win nor a loss.
			if (winnerTeam > (int)CsTeam.Spectator)
			{
				if (winnerTeam == player.Controller.TeamNum)
					ApplyPoints(player, Points.RoundWin, "reason.round-win");
				else
					ApplyPoints(player, Points.RoundLose, "reason.round-loss");
			}

			if (!state.MuteMessages && Settings.RoundSummary)
			{
				if (state.RoundPoints > 0)
					chat.Tell(player.Controller, "points.gain", state.Points, state.RoundPoints, chat.For(player.Controller, "reason.round-summary"));
				else if (state.RoundPoints < 0)
					chat.Tell(player.Controller, "points.loss", state.Points, Math.Abs((long)state.RoundPoints), chat.For(player.Controller, "reason.round-summary"));

				// What the total is made of, such as "5× Kill: +100", when it came from more than one reason.
				if (state.RoundPoints != 0 && state.RoundReasons.Count > 1)
				{
					foreach ((string reason, (int count, int points)) in state.RoundReasons)
					{
						if (points != 0)
							player.Controller.PrintToChat(SummaryLine(player.Controller, reason, count, points));
					}
				}
			}

			state.RoundPoints = 0;
			state.RoundReasons.Clear();
		}

		roundSummarized = Settings.RoundSummary;
	}

	/// <summary>" 5× Kill: +100" in the player's language; the count and sign need no translation.</summary>
	private string SummaryLine(CCSPlayerController controller, string reason, int count, int points)
		=> $" {ChatColors.Silver}{count}× {chat.For(controller, reason)}: {(points > 0 ? ChatColors.Green : ChatColors.Red)}{(points > 0 ? "+" : "-")}{Math.Abs((long)points)}";

	public void BalanceTeams()
	{
		if (!Settings.TeamBalance.Enabled)
			return;

		try
		{
			List<BalanceMember<K4Player>> members = players.ValidHumans
				.Where(player => player.Controller.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
				.Select(player => new BalanceMember<K4Player>(player, player.Controller.Team == CsTeam.Terrorist, player.Rank?.Points ?? 0))
				.ToList();

			(List<BalanceMove<K4Player>> moves, bool swapLimitReached) = TeamBalancer.Balance(members, Settings.TeamBalance.MaxDifference);

			foreach (BalanceMove<K4Player> move in moves)
			{
				CsTeam destination = move.ToTerrorist ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
				if (move.Player.Controller.Team != destination)
					move.Player.Controller.SwitchTeam(destination);
			}

			if (moves.Count > 0)
				chat.TellAll("teams.balanced");

			if (swapLimitReached)
				logger.LogWarning("Max team switch attempts reached, team balance may not be perfect.");
		}
		catch (Exception ex)
		{
			logger.LogError("Error balancing teams: {ErrorMessage}", ex.Message);
		}
	}

	// ---------------------------------------------------------------- playtime points

	public void AwardPlaytimePoints()
	{
		double intervalSeconds = Settings.PlaytimeReward.EveryMinutes * 60d;
		DateTime now = DateTime.UtcNow;

		foreach (K4Player player in players.ValidHumans.ToArray())
		{
			if (!IsPlaytimeRewardDue(now, player.LastPlaytimeRewardUtc, intervalSeconds))
				continue;

			bool eligible = IsPlaytimeRewardEligible(
				now,
				player.LastActivityUtc,
				player.Controller.Buttons != 0,
				player.Controller.Team >= CsTeam.Terrorist,
				Settings.PlaytimeReward.IncludeAfk,
				intervalSeconds);
			player.LastPlaytimeRewardUtc = now;

			if (eligible)
				ModifyPoints(player, Points.PlaytimePoints, "reason.playtime");
		}
	}

	internal static bool IsPlaytimeRewardDue(DateTime nowUtc, DateTime lastRewardUtc, double intervalSeconds)
		=> intervalSeconds > 0 && nowUtc - lastRewardUtc >= TimeSpan.FromSeconds(intervalSeconds);

	/// <summary>Without AFK rewards, the player must be on a team and have pressed a button within the interval.</summary>
	internal static bool IsPlaytimeRewardEligible(DateTime nowUtc, DateTime lastActivityUtc, bool hasPressedButtons, bool isPlaying, bool allowAfk, double intervalSeconds)
		=> allowAfk || (isPlaying && (hasPressedButtons || nowUtc - lastActivityUtc <= TimeSpan.FromSeconds(intervalSeconds)));

	// ---------------------------------------------------------------- leaderboard placement

	public void RefreshPlacements()
	{
		ulong[] steamIds = players.ValidHumans
			.Where(player => player.Rank is not null && player.SteamId.ToString().Length == 17)
			.Select(player => player.SteamId)
			.ToArray();

		if (steamIds.Length == 0)
			return;

		background.Run(async () =>
		{
			Dictionary<ulong, int> placements = await store.GetPlacementsAsync(steamIds);
			gameThread.Post(() =>
			{
				foreach ((ulong steamId, int placement) in placements)
					SetPlacement(steamId, placement);
			});
		}, "Refreshing leaderboard placements");
	}

	public void RefreshPlacement(ulong steamId)
	{
		background.Run(async () =>
		{
			(int placement, int total) = await store.GetPlacementAndTotalAsync(steamId);
			if (placement > 0 && total > 0)
				gameThread.Post(() => SetPlacement(steamId, placement));
		}, "Refreshing a leaderboard placement");
	}

	private void SetPlacement(ulong steamId, int placement)
	{
		K4Player? player = players.Get(steamId);
		if (player?.Rank is null || !player.IsValid || !player.IsHuman)
			return;

		player.Rank.TopPlacement = placement;
		clanTags.Apply(player);
	}
}
