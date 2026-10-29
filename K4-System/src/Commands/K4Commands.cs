namespace K4System.Commands;

using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Missions;
using K4System.Players;
using K4System.Ranks;
using K4System.Services;
using K4System.Storage;

/// <summary>Every chat and console command. Aliases come from the configuration.</summary>
internal sealed class K4Commands(
	BasePlugin plugin,
	PluginConfig config,
	PlayerRegistry players,
	PlayerService playerService,
	RankService? ranks,
	RewardService? rewards,
	MissionService? missions,
	DuelService? duels,
	PlaytimeService playtime,
	SqlPlayerStore store,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	CommandGuard guard,
	PanoramaHud? hud)
{
	private enum PointCommand
	{
		Set,
		Give,
		Remove
	}

	private CommandSettings Aliases => config.Commands;
	private PluginConfig General => config;

	public void Register()
	{
		foreach (string alias in Aliases.Main)
			plugin.AddCommand($"css_{alias}", "More information about K4-System", OnHelp);
		plugin.AddCommand("css_k4_hud", "Panorama panel diagnostics (server console)", (player, info) =>
		{
			if (player is not null)
				return;
			if (hud is null)
			{
				info.ReplyToCommand("K4 panel: panorama.enabled is false.");
				return;
			}
			foreach (string line in hud.Describe())
				info.ReplyToCommand("K4 panel: " + line);
		});

		foreach (string alias in Aliases.Reset)
			plugin.AddCommand($"css_{alias}", "Resets the player's own data", OnResetMyData);

		plugin.AddCommand("css_resetdata", "Resets the targeted player's data", OnResetData);

		if (ranks is not null)
		{
			foreach (string alias in Aliases.Rank)
				plugin.AddCommand($"css_{alias}", "Check the current rank and points", OnRank);
			foreach (string alias in Aliases.Ranks)
				plugin.AddCommand($"css_{alias}", "Check the available ranks and their data", OnRanks);
			foreach (string alias in Aliases.Top)
				plugin.AddCommand($"css_{alias}", "Check the top players by points", OnTop);

			plugin.AddCommand("css_setpoints", "Set the targeted player's points", (player, info) => OnPoints(player, info, PointCommand.Set));
			plugin.AddCommand("css_givepoints", "Give points to the targeted player", (player, info) => OnPoints(player, info, PointCommand.Give));
			plugin.AddCommand("css_removepoints", "Remove points from the targeted player", (player, info) => OnPoints(player, info, PointCommand.Remove));
			plugin.AddCommand("css_toggletag", "Toggles the tag assigned by permissions", OnToggleTag);
			plugin.AddCommand("css_togglepointmsg", "Toggles the chat messages of point changes", OnTogglePointMessages);
		}

		if (General.Modules.Stats)
		{
			foreach (string alias in Aliases.Stats)
				plugin.AddCommand($"css_{alias}", "Check your statistics", OnStats);
		}

		if (General.Modules.Playtime)
		{
			foreach (string alias in Aliases.Playtime)
				plugin.AddCommand($"css_{alias}", "Check your playtime", OnTime);
		}

		if (missions is not null)
		{
			foreach (string alias in Aliases.Missions)
				plugin.AddCommand($"css_{alias}", "Show your daily missions", OnMissions);
		}

		if (General.Modules.AdminList)
		{
			foreach (string alias in Aliases.Admins)
				plugin.AddCommand($"css_{alias}", "Check online admins", OnAdmins);
		}

		if (duels is not null)
		{
			foreach (string alias in Aliases.Duel)
				plugin.AddCommand($"css_{alias}", "Bet points on who gets more kills this map", OnDuel);
		}
	}

	/// <summary>The command's player once loaded. Replies with a loading message otherwise.</summary>
	private K4Player? LoadedPlayer(CCSPlayerController player, CommandInfo info)
	{
		K4Player? k4player = players.Get(player);
		if (k4player is null)
			info.ReplyToCommand(chat.Prefixed("error.loading"));
		return k4player;
	}

	/// <summary>Shared cooldown of the database-backed !rank and !top commands.</summary>
	private bool TryStartExpensiveCommand(K4Player player, CommandInfo info)
	{
		DateTime now = DateTime.UtcNow;
		TimeSpan remaining = ResetPolicy.CommandCooldownRemaining(now, player.LastExpensiveCommandUtc, General.Commands.CooldownSeconds);

		if (remaining > TimeSpan.Zero)
		{
			info.ReplyToCommand(chat.Prefixed("error.cooldown", (int)Math.Ceiling(remaining.TotalSeconds)));
			return false;
		}

		player.LastExpensiveCommandUtc = now;
		return true;
	}

	// ---------------------------------------------------------------- help

	/// <summary>With the Panorama panel on, commands open its tab instead of printing to chat.</summary>
	private bool TryOpenPanel(CCSPlayerController? player, HudTab tab)
	{
		if (hud is null || !CommandGuard.IsInGamePlayer(player) || players.Get(player) is not K4Player k4player)
			return false;

		// False when the panel cannot be created; the command then prints to chat as usual.
		return hud.Open(k4player, tab);
	}

	private void OnHelp(CCSPlayerController? player, CommandInfo info)
	{
		if (!CommandGuard.IsInGamePlayer(player) || TryOpenPanel(player, ranks is not null ? HudTab.Rank : HudTab.Stats))
			return;

		info.ReplyToCommand(chat.Prefixed("help.title"));

		Dictionary<string, List<string>> categories = [];

		if (ranks is not null)
			categories[chat["help.ranks"]] = [.. Aliases.Rank, .. Aliases.Top, .. Aliases.Ranks];
		if (General.Modules.Stats)
			categories[chat["help.stats"]] = [.. Aliases.Stats];
		if (General.Modules.Playtime)
			categories[chat["help.playtime"]] = [.. Aliases.Playtime];
		if (missions is not null)
			categories[chat["help.missions"]] = [.. Aliases.Missions];

		List<string> other = [.. Aliases.Reset];
		if (General.Modules.AdminList)
			other.AddRange(Aliases.Admins);
		if (duels is not null)
			other.AddRange(Aliases.Duel);
		categories[chat["help.other"]] = other;

		StringBuilder line = new();

		foreach ((string category, List<string> commands) in categories)
		{
			if (commands.Count == 0)
				continue;

			line.Append($" {ChatColors.Silver}{category}: {ChatColors.Lime}!{commands[0]}");

			for (int i = 1; i < commands.Count; i++)
			{
				if (i % 6 == 0)
				{
					info.ReplyToCommand(line.ToString());
					line.Clear();
				}

				if (line.Length > 0)
					line.Append($"{ChatColors.Silver}, ");

				line.Append($"{ChatColors.Lime}!{commands[i]}");
			}

			info.ReplyToCommand(line.ToString());
			line.Clear();
		}
	}

	// ---------------------------------------------------------------- duels

	/// <summary>!duel &lt;player&gt; &lt;points&gt;, !duel accept, and !duel decline (which also takes back your own challenge).</summary>
	private void OnDuel(CCSPlayerController? player, CommandInfo info)
	{
		const string Help = "<player> <points> | accept | decline";
		if (duels is null || !guard.Check(player, info, CommandUsage.CLIENT_ONLY, 1, Help) || LoadedPlayer(player!, info) is not K4Player caller)
			return;

		string first = info.ArgByIndex(1);
		DuelService.Refusal? refusal;

		if (first.Equals("accept", StringComparison.OrdinalIgnoreCase))
			refusal = duels.Accept(caller);
		else if (first.Equals("decline", StringComparison.OrdinalIgnoreCase))
			refusal = duels.Decline(caller);
		else
		{
			if (info.ArgCount < 3 || !int.TryParse(info.ArgByIndex(2), out int stake) || stake < 0)
			{
				info.ReplyToCommand(chat.Prefixed("usage", CommandGuard.CommandName(info), Help));
				return;
			}

			List<K4Player> targets = info.GetArgTargetResult(1).Players
				.Select(target => players.Get(target))
				.OfType<K4Player>()
				.Where(target => target.IsHuman && target.IsValid)
				.ToList();

			if (targets.Count != 1)
			{
				info.ReplyToCommand(chat.Prefixed(targets.Count == 0 ? "error.target-not-found" : "duel.error.one-player"));
				return;
			}

			refusal = duels.Challenge(caller, targets[0], stake);
			if (refusal is null)
				chat.Tell(targets[0].Controller, "duel.how-to", CommandGuard.CommandName(info), duels.AcceptSeconds);
		}

		if (refusal is DuelService.Refusal reason)
			info.ReplyToCommand(chat.Prefixed(reason.Key, reason.Args));
	}

	// ---------------------------------------------------------------- resets

	/// <summary>Resets the chosen sections. Returns false when the player had none of them loaded.</summary>
	private bool ResetSections(K4Player player, string target, DateTime nowUtc, bool cancelRewards)
	{
		bool resetAny = false;

		if (ResetPolicy.Includes(target, ResetPolicy.Time) && player.Playtime is not null)
		{
			player.Playtime.Reset(nowUtc);
			resetAny = true;
		}

		if (ResetPolicy.Includes(target, ResetPolicy.Rank) && player.Rank is not null && ranks is not null)
		{
			if (cancelRewards)
				rewards?.CancelPending(player);

			ranks.SetPoints(player, config.Ranks.StartPoints, 0);
			resetAny = true;
		}

		if (ResetPolicy.Includes(target, ResetPolicy.Stat) && player.Stats is not null)
		{
			player.Stats.Reset();
			resetAny = true;
		}

		return resetAny;
	}

	private void OnResetMyData(CCSPlayerController? player, CommandInfo info)
	{
		const string usage = "[all|time|stat|rank]";
		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY, 1, usage))
			return;

		if (!ResetPolicy.TryNormalizeTarget(info.ArgByIndex(1), out string target))
		{
			info.ReplyToCommand(chat.Prefixed("usage", CommandGuard.CommandName(info), usage));
			return;
		}

		if (LoadedPlayer(player!, info) is not K4Player k4player)
			return;

		DateTime now = DateTime.UtcNow;
		TimeSpan commandCooldown = ResetPolicy.CommandCooldownRemaining(now, k4player.LastPersonalResetCommandUtc, General.Commands.CooldownSeconds);
		if (commandCooldown > TimeSpan.Zero)
		{
			info.ReplyToCommand(chat.Prefixed("error.cooldown", (int)Math.Ceiling(commandCooldown.TotalSeconds)));
			return;
		}

		DateTime? rankResetAt = null;

		if (ResetPolicy.ClearsRewardClaims(target))
		{
			TimeSpan rankCooldown = ResetPolicy.RankResetCooldownRemaining(now, k4player.LastPersonalRankResetUtc, config.Ranks.ResetCooldownHours);
			if (rankCooldown > TimeSpan.Zero)
			{
				info.ReplyToCommand(chat.Prefixed("reset.cooldown", ResetPolicy.FormatRemaining(rankCooldown)));
				return;
			}

			rankResetAt = now;
		}

		if (!ResetSections(k4player, target, now, cancelRewards: true))
			return;

		k4player.LastPersonalResetCommandUtc = now;
		if (rankResetAt is not null)
			k4player.LastPersonalRankResetUtc = rankResetAt;

		chat.TellAll("reset.self", player!.PlayerName);
		playerService.Save(k4player, rankResetAt);
	}

	private void OnResetData(CCSPlayerController? player, CommandInfo info)
	{
		const string usage = "<target> [all|time|stat|rank]";
		if (!guard.Check(player, info, CommandUsage.CLIENT_AND_SERVER, 1, usage, Permissions.Admin))
			return;

		if (!ResetPolicy.TryGetAdminTarget(info.ArgCount, info.ArgByIndex, out string target))
		{
			info.ReplyToCommand(chat.Prefixed("usage", CommandGuard.CommandName(info), usage));
			return;
		}

		string callerName = CommandGuard.CallerName(player);

		foreach (K4Player k4player in ResolveTargets(player, info))
		{
			// An admin reset keeps reward claims, so rewards already earned are not paid out again.
			if (!ResetSections(k4player, target, DateTime.UtcNow, cancelRewards: false))
				continue;

			if (callerName != "SERVER")
				chat.TellAll("reset.by-admin", k4player.Controller.PlayerName, callerName);

			playerService.Save(k4player);
		}
	}

	/// <summary>Loaded human targets the caller may target. Replies for every skipped target.</summary>
	private List<K4Player> ResolveTargets(CCSPlayerController? caller, CommandInfo info)
	{
		List<K4Player> result = [];
		TargetResult targets = info.GetArgTargetResult(1);

		if (!targets.Any())
		{
			info.ReplyToCommand(chat.Prefixed("error.target-not-found"));
			return result;
		}

		foreach (CCSPlayerController target in targets.Players)
		{
			K4Player? k4player = players.Get(target);

			if (k4player is null || !k4player.IsValid)
				info.ReplyToCommand(chat.Prefixed("error.target-loading", target.PlayerName));
			else if (!k4player.IsHuman)
				info.ReplyToCommand(chat.Prefixed("error.target-bot", target.PlayerName));
			else if (caller is not null && !AdminManager.CanPlayerTarget(caller, target))
				info.ReplyToCommand(chat.Prefixed("error.target-immunity", target.PlayerName));
			else
				result.Add(k4player);
		}

		return result;
	}

	// ---------------------------------------------------------------- ranks

	private void OnPoints(CCSPlayerController? player, CommandInfo info, PointCommand command)
	{
		if (ranks is null || !guard.Check(player, info, CommandUsage.CLIENT_AND_SERVER, 2, "<target> <amount>", Permissions.Admin))
			return;

		if (!int.TryParse(info.ArgByIndex(2), out int amount) || amount < 0)
		{
			info.ReplyToCommand(chat.Prefixed("error.invalid-amount"));
			return;
		}

		string callerName = CommandGuard.CallerName(player);

		foreach (K4Player target in ResolveTargets(player, info))
		{
			RankState? state = target.Rank;
			if (state is null)
				continue;

			string targetName = target.Controller.PlayerName;

			switch (command)
			{
				case PointCommand.Set:
					ranks.SetPoints(target, amount, state.RoundPoints);
					if (callerName != "SERVER")
						chat.TellAll("admin.set-points", targetName, amount, callerName);
					break;
				case PointCommand.Give:
					ranks.SetPoints(target, PointMath.AddToStored(state.Points, amount), state.RoundPoints);
					if (callerName != "SERVER")
						chat.TellAll("admin.give-points", callerName, amount, targetName);
					break;
				default:
					ranks.SetPoints(target, PointMath.AddToStored(state.Points, -amount), state.RoundPoints);
					if (callerName != "SERVER")
						chat.TellAll("admin.remove-points", callerName, amount, targetName);
					break;
			}

			playerService.Save(target);
		}
	}

	private void OnToggleTag(CCSPlayerController? player, CommandInfo info)
	{
		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || k4player.Rank is null)
			return;

		k4player.Rank.HideAdminTag = !k4player.Rank.HideAdminTag;
		ranks!.RefreshClanTag(k4player);
		info.ReplyToCommand(chat.Prefixed("toggle.tag", chat[k4player.Rank.HideAdminTag ? "state.off" : "state.on"]));
	}

	private void OnTogglePointMessages(CCSPlayerController? player, CommandInfo info)
	{
		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || k4player.Rank is null)
			return;

		k4player.Rank.MuteMessages = !k4player.Rank.MuteMessages;
		info.ReplyToCommand(chat.Prefixed("toggle.point-messages", chat[k4player.Rank.MuteMessages ? "state.off" : "state.on"]));
	}

	private void OnRank(CCSPlayerController? player, CommandInfo info)
	{
		// With "rank-to-everyone", !rank only posts to chat, like LVL Ranks; !k4 still opens the panel.
		bool everyone = General.Commands.RankToEveryone;
		if (!everyone && TryOpenPanel(player, HudTab.Rank))
			return;

		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || !TryStartExpensiveCommand(k4player, info))
			return;

		ulong steamId = k4player.SteamId;

		background.Run(async () =>
		{
			(int placement, int total) = await store.GetPlacementAndTotalAsync(steamId);
			gameThread.Post(() =>
			{
				// With "rank-to-everyone" the player reads the same public line as everyone else, like LVL Ranks.
				if (everyone)
					AnnounceRank(steamId, placement, total);
				else
					PrintRank(steamId, placement, total);
			});
		}, "Preparing rank information");
	}

	/// <summary>The LVL Ranks style line for everyone: place, points and, with the stats module, kills, deaths and KDR.</summary>
	private void AnnounceRank(ulong steamId, int placement, int total)
	{
		K4Player? player = players.Get(steamId);
		if (player is null || player.Rank is not RankState state || !player.IsValid || !player.IsHuman)
			return;

		if (player.Stats is StatCounters stats)
		{
			int kills = stats[StatField.Kills];
			int deaths = stats[StatField.Deaths];
			chat.TellAll("rank.public", player.Name, placement, total, state.Points, kills, deaths, StatCounters.KillDeathRatio(kills, deaths));
		}
		else
		{
			chat.TellAll("rank.public-points", player.Name, placement, total, state.Points);
		}
	}

	private void PrintRank(ulong steamId, int placement, int total)
	{
		K4Player? player = players.Get(steamId);
		RankState? state = player?.Rank;
		if (player is null || state is null || !player.IsValid || !player.IsHuman || ranks is null)
			return;

		ranks.RefreshRank(player, announce: false, allowRewards: true);

		IReadOnlyList<RankDefinition> progression = ranks.Progression(player);
		CCSPlayerController controller = player.Controller;

		chat.Tell(controller, "rank.title", player.Name);
		chat.TellLine(controller, "rank.current", state.Points, state.Rank.Color, state.Rank.Name, RankLadder.Position(progression, state.Rank), progression.Count);

		if (RankLadder.Next(progression, state.Points) is RankDefinition next)
		{
			chat.TellLine(controller, "rank.next", next.Color, next.Name);
			chat.TellLine(controller, "rank.points-needed", next.Point - state.Points);

			foreach (string line in RankRewards.DescriptionLines(next))
				chat.TellLine(controller, "rank.reward", line);
		}

		chat.TellLine(controller, "rank.position", placement, total);
	}

	private ChatMenu BuildRanksMenu(CCSPlayerController viewer)
	{
		ChatMenu menu = new(chat.For(viewer, "ranks.menu-title"));

		foreach (RankDefinition rank in ranks!.Ladder.Ranks)
		{
			string title = rank.IsDefault
				? chat.For(viewer, "ranks.default-item", rank.Color, rank.Name)
				: chat.For(viewer, "ranks.item", rank.Color, rank.Name, rank.Point);

			menu.AddMenuOption(title, (player, _) =>
			{
				ulong steamId = player.SteamID;
				background.Run(async () =>
				{
					(int count, float percentage) = await store.GetRankShareAsync(rank.Name);
					gameThread.Post(() => PrintRankDetails(steamId, rank, count, percentage));
				}, "Preparing rank menu details");
			});
		}

		return menu;
	}

	private void OnRanks(CCSPlayerController? player, CommandInfo info)
	{
		if (TryOpenPanel(player, HudTab.Ranks))
			return;

		if (ranks is not null && guard.Check(player, info, CommandUsage.CLIENT_ONLY))
			MenuManager.OpenChatMenu(player!, BuildRanksMenu(player!));
	}

	private void PrintRankDetails(ulong steamId, RankDefinition rank, int count, float percentage)
	{
		K4Player? player = players.Get(steamId);
		RankState? state = player?.Rank;
		if (player is null || state is null || !player.IsValid || !player.IsHuman)
			return;

		CCSPlayerController controller = player.Controller;
		long difference = Math.Abs((long)(rank.IsDefault ? 0 : rank.Point) - state.Points);

		chat.Tell(controller, "ranks.selected", rank.Color, rank.Name);
		chat.TellLine(controller, "ranks.share", count, percentage);

		if (rank.Name == state.Rank.Name)
			chat.TellLine(controller, "ranks.current", rank.Point);
		else
			chat.TellLine(controller, rank.Point > state.Rank.Point ? "ranks.required" : "ranks.passed", rank.IsDefault ? 0 : rank.Point, difference);

		foreach (string line in RankRewards.DescriptionLines(rank))
			chat.TellLine(controller, "rank.reward", line);

		if (rank.Permissions is not { Count: > 0 })
			return;

		chat.TellLine(controller, "ranks.benefits");

		foreach (RankPermission[] group in rank.Permissions.Chunk(3))
			controller.PrintToChat($"  {string.Join($"{ChatColors.Silver}, ", group.Select(permission => $"{ChatColors.Lime}{permission.DisplayName}"))}");
	}

	private void OnTop(CCSPlayerController? player, CommandInfo info)
	{
		if (TryOpenPanel(player, HudTab.Top))
			return;

		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || !TryStartExpensiveCommand(k4player, info))
			return;

		int count = int.TryParse(info.ArgByIndex(1), out int parsed) ? Math.Clamp(parsed, 1, 25) : 5;
		ulong steamId = k4player.SteamId;

		// Save everyone first so the list includes the latest points.
		Task<bool> saved = playerService.SaveAll();

		background.Run(async () =>
		{
			await saved;
			List<TopEntry> top = await store.GetTopAsync(count);
			gameThread.Post(() => PrintTop(steamId, count, top));
		}, "Preparing the top list");
	}

	private void PrintTop(ulong steamId, int count, List<TopEntry> top)
	{
		K4Player? player = players.Get(steamId);
		if (player is null || !player.IsValid || !player.IsHuman || ranks is null)
			return;

		if (top.Count == 0)
		{
			chat.Tell(player.Controller, "top.empty");
			return;
		}

		chat.Tell(player.Controller, "top.title", top.Count);

		for (int i = 0; i < top.Count; i++)
		{
			TopEntry entry = top[i];
			RankDefinition rank = ranks.Ladder.FindByName(entry.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(entry.Points);
			chat.TellLine(player.Controller, "top.line", i + 1, rank.Color, rank.Name, entry.Name, entry.Points);
		}
	}

	// ---------------------------------------------------------------- stats and playtime

	private void OnStats(CCSPlayerController? player, CommandInfo info)
	{
		if (TryOpenPanel(player, HudTab.Stats))
			return;

		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || k4player.Stats is not StatCounters stats)
			return;

		int kills = stats[StatField.Kills];
		int assists = stats[StatField.Assists];
		int deaths = stats[StatField.Deaths];
		int roundWin = stats[StatField.RoundWin];
		int roundLose = stats[StatField.RoundLose];
		int gameWin = stats[StatField.GameWin];
		int gameLose = stats[StatField.GameLose];
		int shoots = stats[StatField.Shoots];
		int hitsGiven = stats[StatField.HitsGiven];

		float roundChance = roundWin + roundLose > 0 ? (float)roundWin / (roundWin + roundLose) * 100 : 0;
		float gameChance = gameWin + gameLose > 0 ? (float)gameWin / (gameWin + gameLose) * 100 : 0;
		float accuracy = shoots > 0 ? (float)hitsGiven / shoots * 100 : 0;
		float kda = deaths > 0 ? (float)(kills + assists) / deaths : 0;

		info.ReplyToCommand(chat.Prefixed("stats.title", player!.PlayerName));
		info.ReplyToCommand(chat["stats.line1", kills, stats[StatField.FirstBlood], assists]);
		info.ReplyToCommand(chat["stats.line2", hitsGiven, stats[StatField.HitsTaken], deaths]);
		info.ReplyToCommand(chat["stats.line3", stats[StatField.Headshots], StatService.HeadshotPercentage(stats[StatField.Headshots], kills), stats[StatField.Grenades]]);
		info.ReplyToCommand(chat["stats.line4", roundWin, roundLose, roundChance]);
		info.ReplyToCommand(chat["stats.line5", gameWin, gameLose, gameChance]);
		info.ReplyToCommand(chat["stats.line6", shoots, accuracy]);
		info.ReplyToCommand(chat["stats.line7", kda, stats[StatField.Mvp]]);
	}

	private void OnTime(CCSPlayerController? player, CommandInfo info)
	{
		if (TryOpenPanel(player, HudTab.Stats))
			return;

		if (!guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player || k4player.Playtime is null)
			return;

		int[] seconds = playtime.CurrentSeconds(k4player);
		string Time(PlaytimeField field) => playtime.Format(seconds[(int)field]);

		info.ReplyToCommand(chat.Prefixed("time.title", player!.PlayerName));
		info.ReplyToCommand(chat["time.line1", Time(PlaytimeField.All)]);
		info.ReplyToCommand(chat["time.line2", Time(PlaytimeField.Ct), Time(PlaytimeField.T)]);
		info.ReplyToCommand(chat["time.line3", Time(PlaytimeField.Spec)]);
		info.ReplyToCommand(chat["time.line4", Time(PlaytimeField.Alive), Time(PlaytimeField.Dead)]);
	}

	// ---------------------------------------------------------------- missions

	private void OnMissions(CCSPlayerController? player, CommandInfo info)
	{
		if (missions is null || !guard.Check(player, info, CommandUsage.CLIENT_ONLY) || LoadedPlayer(player!, info) is not K4Player k4player)
			return;

		string command = info.ArgByIndex(0).Replace("css_", "", StringComparison.OrdinalIgnoreCase);

		if (string.Equals(info.ArgByIndex(1), "reroll", StringComparison.OrdinalIgnoreCase))
			missions.Reroll(k4player, info.ArgByIndex(2));
		else if (hud is null || !hud.Open(k4player, HudTab.Missions))
			missions.Show(k4player, command);
	}

	// ---------------------------------------------------------------- utilities

	private void OnAdmins(CCSPlayerController? player, CommandInfo info)
	{
		if (!CommandGuard.IsInGamePlayer(player))
			return;

		List<string> admins = [];

		foreach (K4Player k4player in players.ValidHumans)
		{
			AdminTag? entry = General.Tags.Admins.FirstOrDefault(candidate => candidate.ListColor is not null && Permissions.Has(k4player, candidate.Permission));
			if (entry is not null)
				admins.Add($"{ChatColorText.Apply(entry.ListColor!)}{k4player.Name}");
		}

		if (admins.Count == 0)
		{
			info.ReplyToCommand(chat.Prefixed("admins.none"));
			return;
		}

		info.ReplyToCommand(chat.Prefixed("admins.title"));
		info.ReplyToCommand($" {string.Join($"{ChatColors.Silver}, ", admins)}");
	}
}
