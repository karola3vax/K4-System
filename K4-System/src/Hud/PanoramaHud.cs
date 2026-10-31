namespace K4System.Hud;

using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Timers;
using K4System.Config;
using K4System.Domain;
using K4System.Missions;
using K4System.Players;
using K4System.Ranks;
using K4System.Services;
using K4System.Storage;
using Microsoft.Extensions.Logging;

public enum HudTab
{
	Rank,
	Missions,
	Top,
	Ranks,
	Stats,
	Achievements
}

public enum ToastKind
{
	Success,
	Gold,
	Warning
}

/// <summary>
/// The K4 Panorama panel and toasts. The layout ships to players in the K4 workshop addon
/// (panorama/layout/custom_game/k4_ui.xml); the server only sets text and style classes per player
/// and receives button clicks. Game thread only.
/// </summary>
internal sealed class PanoramaHud(
	BasePlugin plugin,
	PluginConfig config,
	PlayerRegistry players,
	RankService? ranks,
	MissionService? missions,
	BountyService? bounty,
	DuelService? duels,
	Achievements.AchievementService? achievements,
	EventService? events,
	PlaytimeService playtime,
	SqlPlayerStore store,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	ILogger logger)
{
	public const string LayoutPath = "panorama/layout/custom_game/k4_ui.xml";

	public const string IconsCompetitive = "competitive";
	public const string IconsWingman = "wingman";
	public const string IconsText = "text";

	private const string Panel = "k4-panel";
	private const string Toast = "k4-toast";
	private const int TopRows = 10;
	private const int TopPages = 10;
	private const int RankRows = 10;
	private const int StatTiles = 9;
	private const float ToastSeconds = 3.5f;

	// CS2 can kick a player when a chat command and a HUD update share a frame, so drawing waits a
	// moment, and mouse capture is switched on a moment after the panel is drawn.
	private const float CommandDelaySeconds = 0.2f;
	private const float CaptureDelaySeconds = 0.2f;

	// The mouse aims nowhere while the panel is open, so an untouched panel closes itself.
	private const float IdleCloseSeconds = 45f;

	// Database-backed views are cached briefly, so spamming commands or page buttons cannot flood the database.
	private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

	private static readonly string[] TabIds = ["rank", "missions", "top", "ranks", "stats", "achievements"];
	private static readonly string[] Views = ["k4-view-rank", "k4-view-missions", "k4-view-top", "k4-view-ranks", "k4-view-stats", "k4-view-achievements"];

	/// <summary>Achievement cards on one page of the Achievements tab.</summary>
	internal const int AchievementCards = 4;
	private static readonly HashSet<string> ColorNames =
	[
		"default", "white", "darkred", "green", "lightyellow", "lightblue", "olive", "lime", "red", "lightpurple",
		"purple", "grey", "yellow", "gold", "silver", "blue", "darkblue", "bluegrey", "magenta", "lightred", "orange"
	];

	// CS2 killfeed icons (panorama/images/icons/equipment) that k4_ui.css defines as eq-<name>.
	private static readonly HashSet<string> WeaponIcons =
	[
		"knife", "hegrenade", "flashbang_assist", "planted_c4", "defuser", "taser", "molotov", "incgrenade",
		"ak47", "aug", "awp", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar", "glock",
		"hkp2000", "m249", "m4a1", "m4a1_silencer", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev", "nova", "p2000",
		"p250", "p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer", "xm1014"
	];

	/// <summary>Mission cards in the panel: three daily missions and the weekly one.</summary>
	internal const int MissionCards = 4;

	private static readonly string[] StatIcons =
	[
		"ic-kill", "ic-map_death", "ic-stats", "ic-stat_headshots", "ic-stat_assists", "ic-trophy", "ic-stat_mvps",
		"ic-crosshair_circle", "ic-clock"
	];

	private sealed class HudState
	{
		public HudTab Tab { get; set; }
		public int TopPage { get; set; }
		public int TopPageCount { get; set; } = 1;
		public int RanksPage { get; set; }
		public int AchievementsPage { get; set; }
		public int Version { get; set; }
	}

	// Versions come from one counter that never restarts, so a timer or database draw from an earlier
	// opening, an earlier map, or the slot's previous player can never match the current panel.
	private int lastVersion;

	private int NextVersion() => ++lastVersion;

	private sealed record Cached<T>(T Value, DateTime LoadedUtc)
	{
		public bool Fresh => DateTime.UtcNow - LoadedUtc < CacheFor;
	}

	private readonly Dictionary<int, HudState> open = [];
	private readonly Dictionary<int, int> toastVersions = [];

	// The class currently applied per player, panel, and class group, so only changes are sent.
	private readonly Dictionary<int, Dictionary<(string Panel, string Group), string>> applied = [];

	private readonly Dictionary<int, Cached<List<TopEntry>>> topCache = [];
	private readonly Dictionary<ulong, Cached<(int Placement, int Total)>> placementCache = [];
	private Cached<(Dictionary<string, int> Counts, int Total)>? rankCountCache;

	private CCSCustomHudLayout? layout;

	public bool IsOpen(K4Player player) => open.ContainsKey(player.Slot);

	private string IconSet => config.Panorama.RankIcons?.Trim().ToLowerInvariant() ?? IconsCompetitive;

	// ---------------------------------------------------------------- layout entity

	/// <summary>Creates the layout entity early, so clients have loaded it before the first !k4.</summary>
	public void Warmup() => gameThread.Post(() => EnsureLayout());

	/// <summary>Entities are destroyed on map change.</summary>
	public void OnMapStart()
	{
		layout = null;
		open.Clear();
		toastVersions.Clear();
		applied.Clear();
		placementCache.Clear();
		plugin.AddTimer(1.0f, () => gameThread.PostWorldUpdate(() => EnsureLayout()));
	}

	public bool EnsureLayout()
	{
		if (layout is { IsValid: true })
			return true;

		try
		{
			// Reuse a leftover layout entity (for example after an unload that could not remove it) instead of adding another.
			layout = Utilities.FindAllEntitiesByDesignerName<CCSCustomHudLayout>("custom_hud_layout")
				.FirstOrDefault(entity => entity.IsValid && entity.StrLayout == LayoutPath);
			if (layout is not null)
			{
				logger.LogDebug("K4 panel: reusing custom_hud_layout #{Index} ({Layout}).", layout.Index, layout.StrLayout);
				return true;
			}

			CCSCustomHudLayout? created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
			if (created is null || created.Handle == IntPtr.Zero)
			{
				logger.LogError("The K4 Panorama panel could not be created (custom_hud_layout). Commands fall back to chat.");
				return false;
			}

			created.StrLayout = LayoutPath;
			created.DispatchSpawn();

			if (!created.IsValid)
			{
				logger.LogError("The K4 Panorama panel did not spawn. Commands fall back to chat.");
				return false;
			}

			layout = created;
			logger.LogDebug("K4 panel: created custom_hud_layout #{Index} ({Layout}).", created.Index, created.StrLayout);
			return true;
		}
		catch (Exception ex) when (GameState.IsEntitySystemUnavailable(ex))
		{
			return false;
		}
	}

	private bool Usable(K4Player player) => player.IsValid && player.IsHuman && EnsureLayout();

	/// <summary>Diagnostics for css_k4_hud: the layout entities and each player's panel state.</summary>
	public IEnumerable<string> Describe()
	{
		yield return $"Layout field: {(layout is null ? "none" : $"#{layout.Index} valid={layout.IsValid} path={(layout.IsValid ? layout.StrLayout : "?")}")}";
		foreach (CCSCustomHudLayout entity in Utilities.FindAllEntitiesByDesignerName<CCSCustomHudLayout>("custom_hud_layout"))
			yield return $"custom_hud_layout #{entity.Index} valid={entity.IsValid} path='{entity.StrLayout}' observable={entity.Observable} playerStates={entity.PlayerLayoutStates.Count}";
		foreach (K4Player player in players.ValidHumans)
		{
			bool capture = layout is { IsValid: true } && layout.IsInputCaptureEnabled(player.Controller);
			string tab = open.TryGetValue(player.Slot, out HudState? state) ? $"{state.Tab} v{state.Version}" : "closed";
			yield return $"{player.Name} slot {player.Slot}: panel {tab}, mouse capture {capture}";
		}
	}

	// ---------------------------------------------------------------- open, close, clicks

	/// <summary>
	/// Opens the panel on a tab. Returns false when the panel cannot be shown, so the command can
	/// fall back to chat. Called from chat commands, so drawing waits a short moment.
	/// </summary>
	public bool Open(K4Player player, HudTab tab)
	{
		if (!Usable(player))
			return false;

		logger.LogDebug("K4 panel: opening {Tab} for {Player} (layout #{Index}).", tab, player.Name, layout!.Index);
		HudState state = open.TryGetValue(player.Slot, out HudState? existing) ? existing : new HudState();
		state.Tab = tab;
		open[player.Slot] = state;
		ShowLater(player, state, enableCapture: true);
		return true;
	}

	/// <summary>Redraws the panel if it is open on this tab, for changes made outside the panel.</summary>
	public void Refresh(K4Player player, HudTab tab)
	{
		if (open.TryGetValue(player.Slot, out HudState? state) && state.Tab == tab)
			ShowLater(player, state, enableCapture: false);
	}

	/// <summary>Redraws every open panel on its current tab, for changes that concern everyone, such as a points event.</summary>
	public void RefreshOpenPanels()
	{
		foreach ((int slot, HudState state) in open.ToArray())
		{
			if (players.All.FirstOrDefault(player => player.Slot == slot) is K4Player player)
				ShowLater(player, state, enableCapture: false);
		}
	}

	// Waits a moment, because a HUD update in the same frame as a chat command can kick the player.
	private void ShowLater(K4Player player, HudState state, bool enableCapture)
	{
		int version = state.Version = NextVersion();
		plugin.AddTimer(CommandDelaySeconds, () =>
		{
			if (gameThread.IsActive && open.TryGetValue(player.Slot, out HudState? current) && current.Version == version && Usable(player))
				Show(player, current, enableCapture);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	public void Close(K4Player player)
	{
		open.Remove(player.Slot);

		if (layout is { IsValid: true } && player.HasController)
			Hide(player.Controller);
	}

	private void Hide(CCSPlayerController controller)
	{
		CCSCustomHudLayout hud = layout!;
		hud.SetHasClassForPlayer(controller, Panel, "k4-closed", true);
		hud.SetInputCaptureEnabled(controller, false);
	}

	/// <summary>
	/// Clears the leaving player's panel, toast, and mouse capture. The layout keeps these per slot,
	/// so without this the next player in the slot could start with a captured mouse.
	/// </summary>
	public void OnDisconnect(CCSPlayerController controller)
	{
		if (layout is { IsValid: true } && controller.IsValid)
		{
			Hide(controller);
			layout.SetHasClassForPlayer(controller, Toast, "k4-gone", true);

			// Put swapped classes back to the layout's defaults for the next player in this slot.
			if (applied.TryGetValue(controller.Slot, out Dictionary<(string Panel, string Group), string>? classes))
			{
				foreach (((string panel, string group), string name) in classes)
				{
					layout.SetHasClassForPlayer(controller, panel, name, false);
					if (DefaultClass(group) is string fallback)
						layout.SetHasClassForPlayer(controller, panel, fallback, true);
				}
			}
		}

		open.Remove(controller.Slot);
		toastVersions.Remove(controller.Slot);
		applied.Remove(controller.Slot);
	}

	/// <summary>The scoreboard key closes the panel, because the mouse is captured while it is open.</summary>
	public void OnButtonsPressed(K4Player player, PlayerButtons pressed)
	{
		if ((pressed & PlayerButtons.Scoreboard) != 0 && IsOpen(player))
			Close(player);
	}

	public void OnClick(CCSPlayerController controller, CCSCustomHudLayout clicked, string buttonId)
	{
		if (layout is not { IsValid: true } || clicked.Handle != layout.Handle || players.Get(controller) is not K4Player player
			|| !open.TryGetValue(player.Slot, out HudState? state))
			return;

		switch (buttonId)
		{
			case "k4-close":
				Close(player);
				return;
			case "k4-duel-accept" when duels is not null:
				Tell(player, duels.Accept(player));
				break;
			case "k4-duel-decline" when duels is not null:
				Tell(player, duels.Decline(player));
				break;
			case "k4-top-prev" when state.TopPage > 0:
				state.TopPage--;
				break;
			case "k4-top-next" when state.TopPage < state.TopPageCount - 1:
				state.TopPage++;
				break;
			case "k4-rk-prev" when state.RanksPage > 0:
				state.RanksPage--;
				break;
			case "k4-rk-next" when ranks is not null && (state.RanksPage + 1) * RankRows < ranks.Ladder.Ranks.Count:
				state.RanksPage++;
				break;
			case "k4-ach-prev" when state.AchievementsPage > 0:
				state.AchievementsPage--;
				break;
			case "k4-ach-next" when (state.AchievementsPage + 1) * AchievementCards < AchievementOrder(player).Count:
				state.AchievementsPage++;
				break;
			default:
				if (buttonId.StartsWith("k4-tab-", StringComparison.Ordinal) && Array.IndexOf(TabIds, buttonId["k4-tab-".Length..]) is int tab and >= 0)
				{
					state.Tab = (HudTab)tab;
					break;
				}

				if (buttonId.StartsWith("k4-reroll-", StringComparison.Ordinal) && missions is not null
					&& int.TryParse(buttonId["k4-reroll-".Length..], out int index))
				{
					missions.Reroll(player, (index + 1).ToString(CultureInfo.InvariantCulture));
					break;
				}

				return;
		}

		state.Version = NextVersion();
		Show(player, state, enableCapture: false);
	}

	// ---------------------------------------------------------------- drawing

	private void Show(K4Player player, HudState state, bool enableCapture)
	{
		logger.LogDebug("K4 panel: drawing {Tab} for {Player} (capture {Capture}).", state.Tab, player.Name, enableCapture);
		CCSPlayerController controller = player.Controller;

		SetVar(player, "k4-subtitle", "subtitle", player.Name);
		DrawEventBanner(player);

		for (int i = 0; i < TabIds.Length; i++)
		{
			string tabId = $"k4-tab-{TabIds[i]}";
			SetVar(player, tabId, "label", Text(player, $"hud.tab.{TabIds[i]}"));
			SetClass(player, tabId, "k4-active", (int)state.Tab == i);
			SetClass(player, tabId, "k4-gone", !IsTabAvailable((HudTab)i));
			SetClass(player, Views[i], "k4-show", (int)state.Tab == i);
		}

		switch (state.Tab)
		{
			case HudTab.Rank:
				DrawRank(player, state);
				break;
			case HudTab.Missions:
				DrawMissions(player);
				break;
			case HudTab.Top:
				DrawTop(player, state);
				break;
			case HudTab.Ranks:
				DrawRanks(player, state);
				break;
			case HudTab.Achievements:
				DrawAchievements(player, state);
				break;
			default:
				DrawStats(player);
				break;
		}

		layout!.SetHasClassForPlayer(controller, Panel, "k4-closed", false);

		int version = state.Version;
		if (enableCapture)
		{
			plugin.AddTimer(CaptureDelaySeconds, () =>
			{
				if (gameThread.IsActive && layout is { IsValid: true } && player.HasController && open.TryGetValue(player.Slot, out HudState? current) && ReferenceEquals(current, state))
					layout.SetInputCaptureEnabled(player.Controller, true);
			}, TimerFlags.STOP_ON_MAPCHANGE);
		}

		plugin.AddTimer(IdleCloseSeconds, () =>
		{
			if (gameThread.IsActive && open.TryGetValue(player.Slot, out HudState? current) && current.Version == version)
				Close(player);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	private bool IsTabAvailable(HudTab tab) => tab switch
	{
		HudTab.Rank or HudTab.Top or HudTab.Ranks => ranks is not null,
		HudTab.Missions => missions is not null,
		HudTab.Achievements => achievements is not null,
		_ => config.Modules.Stats || config.Modules.Playtime
	};

	/// <summary>
	/// Runs a database read, caches the result, then draws on the game thread if the panel still shows the same view.
	/// The cache is written even when the player clicked on meanwhile, so fast clicking reuses it instead of reading again.
	/// </summary>
	private void Later<T>(K4Player player, HudState state, Func<Task<T>> load, Action<T> remember, Action<T> draw, string description)
	{
		int version = state.Version;
		ulong steamId = player.SteamId;

		background.Run(async () =>
		{
			T result = await load();
			gameThread.Post(() =>
			{
				remember(result);
				if (players.Get(steamId) is K4Player current && open.TryGetValue(current.Slot, out HudState? now) && now.Version == version && layout is { IsValid: true })
					draw(result);
			});
		}, description);
	}

	private void SetVar(K4Player player, string panel, string name, string value)
		=> layout!.SetDialogVariableStringForPlayer(player.Controller, panel, name, value);

	private void SetClass(K4Player player, string panel, string name, bool on)
		=> layout!.SetHasClassForPlayer(player.Controller, panel, name, on);

	/// <summary>Applies one class out of a group (bar width, color, icon), sending only what changed.</summary>
	private void SwapClass(K4Player player, string panel, string group, string? wanted)
	{
		if (!applied.TryGetValue(player.Slot, out Dictionary<(string, string), string>? classes))
			applied[player.Slot] = classes = [];

		classes.TryGetValue((panel, group), out string? current);

		// The layout starts some panels with a default class; clear it the first time.
		current ??= DefaultClass(group);

		if (current == wanted)
			return;

		if (current is not null)
			SetClass(player, panel, current, false);
		if (wanted is not null)
			SetClass(player, panel, wanted, true);

		if (wanted is null)
			classes.Remove((panel, group));
		else
			classes[(panel, group)] = wanted;
	}

	internal static string? DefaultClass(string group) => group switch
	{
		"icon" => "sg-0",
		"bar" => "w0",
		"toast" => "k4-toast-green",
		"toast-icon" => "ic-check",
		_ => null
	};

	private void SetBar(K4Player player, string panel, int value, int target)
	{
		int step = target <= 0 ? 20 : (int)Math.Clamp((long)value * 20 / target, 0, 20);
		SwapClass(player, panel, "bar", $"w{step}");
	}

	private void SetColor(K4Player player, string panel, string colorName)
		=> SwapClass(player, panel, "color", $"col-{(ColorNames.Contains(colorName) ? colorName : "default")}");

	/// <summary>CS2's own rank icons, numbered like the scoreboard: the default rank is 0, then 1 to 18.</summary>
	private void SetIcon(K4Player player, string panel, RankDefinition rank)
		=> SwapClass(player, panel, "icon", RankIcon(rank));

	/// <summary>The rank icon class for this rank, or null when the server shows rank tags instead.</summary>
	public string? RankIcon(RankDefinition rank)
	{
		int number = Math.Clamp(rank.Id + 1, 0, 18);
		return IconSet switch
		{
			IconsText => null,
			IconsWingman => $"wg-{number}",
			_ => $"sg-{number}"
		};
	}

	/// <summary>A CS2 icon for what the mission counts; weapon missions show the weapon's killfeed icon.</summary>
	internal static string MissionIcon(MissionDefinition mission) => mission.Type switch
	{
		MissionType.Kills => "ic-kill",
		MissionType.Headshots => "ic-kill_headshot",
		MissionType.KnifeKills => "eq-knife",
		MissionType.GrenadeKills => "eq-hegrenade",
		MissionType.NoscopeKills => "eq-awp",
		MissionType.WeaponKills => WeaponIcon(mission.Weapon),
		MissionType.Assists => "ic-stat_assists",
		MissionType.FlashAssists => "eq-flashbang_assist",
		MissionType.Mvps => "ic-stat_mvps",
		MissionType.RoundWins => "ic-trophy",
		MissionType.BombPlants => "ic-bomb_c4",
		MissionType.BombDefuses => "eq-defuser",
		MissionType.HostageRescues => "ic-hostage_rescued",
		MissionType.PlaytimeMinutes => "ic-clock",
		MissionType.MatchesPlayed => "ic-play",
		MissionType.MatchWins => "ic-winner_ribbon",
		MissionType.WallbangKills => "dn-penetrate",
		MissionType.SmokeKills => "dn-smoke_kill",
		MissionType.JumpKills => "dn-inairkill",
		MissionType.BlindKills => "dn-blind_kill",
		MissionType.LongRangeKills => "ic-crosshair",
		MissionType.ZeusKills => "eq-taser",
		MissionType.OpeningKills => "ic-first_person",
		MissionType.ClutchWins => "ic-multipleenemieshere",
		MissionType.RoundKills => "ic-icon_star_multi",
		MissionType.SurviveRounds => "ic-shield",
		MissionType.PistolKills => "eq-deagle",
		MissionType.SmgKills => "eq-mp9",
		MissionType.ShotgunKills => "eq-nova",
		MissionType.RifleKills => "eq-ak47",
		MissionType.SniperKills => "eq-awp",
		MissionType.Damage => "ic-health",
		MissionType.LifeKills => "dn-domination",
		MissionType.RevengeKills => "dn-revenge",
		_ => "ic-mission_star"
	};

	internal static string WeaponIcon(string? weapon)
	{
		string name = WeaponCatalog.Normalize(weapon);
		return WeaponIcons.Contains(name) ? $"eq-{name}" : "ic-bullet";
	}

	/// <summary>Every icon class the server can apply, for the layout tests.</summary>
	internal static IEnumerable<string> IconClasses()
	{
		foreach (MissionType type in Enum.GetValues<MissionType>())
			yield return MissionIcon(new MissionDefinition { Type = type });
		foreach (string weapon in WeaponIcons)
			yield return $"eq-{weapon}";
		foreach (string icon in StatIcons)
			yield return icon;
		foreach (string icon in new[] { "ic-bullet", "ic-check", "ic-hot_trend", "ic-cold_trend", "ic-trade_up", "ic-mission_star", "ic-trophy" })
			yield return icon;
		foreach (Achievements.AchievementType type in Enum.GetValues<Achievements.AchievementType>())
			yield return AchievementIcon(type);
	}

	private string Text(K4Player player, string key, params object[] args) => chat.For(player.Controller, key, args);

	private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

	private void Tell(K4Player player, DuelService.Refusal? refusal)
	{
		if (refusal is DuelService.Refusal reason)
			chat.Tell(player.Controller, reason.Key, reason.Args);
	}

	/// <summary>The bounty and duel lines under the rank: who is wanted, the player's duel, a challenge to answer.</summary>
	private void DrawShowdown(K4Player player)
	{
		const string view = "k4-view-rank";
		SetClass(player, "k4-showdown", "k4-gone", bounty is null && duels is null);
		if (bounty is null && duels is null)
			return;

		(string Name, int Reward)? wanted = bounty?.Wanted;
		(string Opponent, int Mine, int Theirs, int Stake)? duel = duels?.RunningFor(player.SteamId);
		(string Challenger, int Stake)? incoming = duels?.IncomingFor(player.SteamId);

		SetVar(player, view, "wanted", wanted is { } w ? Text(player, "hud.sd-wanted", StripColors(w.Name), Number(w.Reward)) : "");
		SetClass(player, "k4-sd-wanted", "k4-gone", wanted is null);

		SetVar(player, view, "duel", duel is { } d ? Text(player, "hud.sd-duel", StripColors(d.Opponent), d.Mine, d.Theirs, Number(d.Stake)) : "");
		SetClass(player, "k4-sd-duel", "k4-gone", duel is null);

		SetVar(player, view, "challenge", incoming is { } c ? Text(player, "hud.sd-challenge", StripColors(c.Challenger), Number(c.Stake)) : "");
		SetVar(player, view, "accept", Text(player, "hud.sd-accept"));
		SetVar(player, view, "decline", Text(player, "hud.sd-decline"));
		SetClass(player, "k4-sd-challenge", "k4-gone", incoming is null);

		// With nothing going on, a hint says the features exist.
		bool idle = wanted is null && duel is null && incoming is null;
		List<string> hint = [];
		if (bounty is not null)
			hint.Add(Text(player, "hud.sd-no-bounty"));
		if (duels is not null && config.Commands.Duel.Count > 0)
			hint.Add(Text(player, "hud.sd-duel-hint", config.Commands.Duel[0]));
		SetVar(player, view, "hint", idle ? string.Join(" · ", hint) : "");
		SetClass(player, "k4-sd-hint", "k4-gone", !idle);
	}

	private void DrawRank(K4Player player, HudState state)
	{
		if (ranks is null || player.Rank is not RankState rank)
			return;

		DrawShowdown(player);

		const string view = "k4-view-rank";
		RankDefinition current = rank.Rank;
		IReadOnlyList<RankDefinition> progression = ranks.Progression(player);
		RankDefinition? next = RankLadder.Next(progression, rank.Points);
		bool textBadge = IconSet == IconsText;

		SetIcon(player, "k4-rank-icon", current);
		SetClass(player, "k4-rank-icon", "k4-gone", textBadge);
		SetClass(player, "k4-rank-badge", "k4-gone", !textBadge);
		SetVar(player, view, "tag", string.IsNullOrWhiteSpace(current.Tag) ? current.Name : current.Tag.Trim('[', ']'));
		SetVar(player, view, "rank", current.Name);
		SetVar(player, view, "caption", Text(player, "hud.tab.rank"));
		SetColor(player, "k4-rank-name", current.ColorName);
		SetColor(player, "k4-rank-badge", current.ColorName);

		string points = Text(player, "hud.points", Number(rank.Points));
		SetVar(player, view, "summary", points);

		if (next is null)
		{
			SetVar(player, view, "next", Text(player, "hud.max-rank"));
			SetVar(player, view, "togo", "");
			SetBar(player, "k4-rank-bar", 1, 1);
			SetVar(player, view, "reward", "");
			SetClass(player, "k4-rank-reward", "k4-gone", true);
		}
		else
		{
			int from = Math.Max(0, current.Point);
			SetVar(player, view, "next", Text(player, "hud.next", next.Name));
			SetVar(player, view, "togo", Text(player, "hud.to-go", Number(next.Point - rank.Points)));
			SetBar(player, "k4-rank-bar", rank.Points - from, next.Point - from);

			IReadOnlyList<string> reward = RankRewards.DescriptionLines(next);
			SetVar(player, view, "reward", reward.Count > 0 ? Text(player, "hud.reward", StripColors(string.Join(" · ", reward))) : "");
			SetClass(player, "k4-rank-reward", "k4-gone", reward.Count == 0);
		}

		void ShowPlacement((int Placement, int Total) result)
			=> SetVar(player, view, "summary", $"{points} · {Text(player, "hud.position", Number(result.Placement), Number(result.Total))}");

		ulong steamId = player.SteamId;
		if (placementCache.TryGetValue(steamId, out Cached<(int, int)>? cached) && cached.Fresh)
		{
			ShowPlacement(cached.Value);
			return;
		}

		Later(player, state, () => store.GetPlacementAndTotalAsync(steamId),
			result => placementCache[steamId] = new Cached<(int, int)>(result, DateTime.UtcNow),
			ShowPlacement, "Loading the rank panel");
	}

	private void DrawMissions(K4Player player)
	{
		const string view = "k4-view-missions";
		if (missions is null || player.Missions is null)
		{
			for (int i = 0; i < MissionCards; i++)
				SetClass(player, $"k4-m{i}", "k4-gone", true);
			SetVar(player, view, "footer", StripColors(Text(player, "error.loading")));
			return;
		}

		// Start today's missions if midnight passed since the last round start.
		missions.EnsureToday(player);
		PlayerMissions today = player.Missions;
		bool canReroll = today.RerollsUsed < config.Missions.RerollsPerDay;
		List<MissionProgress> all = today.All.ToList();

		// Cards 0-2 are the daily missions, card 3 the weekly one.
		for (int i = 0; i < MissionCards; i++)
		{
			string card = $"k4-m{i}";
			if (i >= all.Count)
			{
				SetClass(player, card, "k4-gone", true);
				continue;
			}

			MissionProgress slot = all[i];
			MissionDefinition mission = slot.Mission;
			string difficulty = mission.Difficulty.ToString().ToLowerInvariant();
			bool weekly = mission.Difficulty == MissionDifficulty.Weekly;

			SetClass(player, card, "k4-gone", false);
			SetClass(player, card, "k4-done", slot.Completed);
			SetClass(player, card, "k4-blocked", !slot.Completed && !missions.IsDoable(mission));
			SetVar(player, card, "diff", missions.DifficultyLabel(player.Controller, mission));
			SwapClass(player, card, "difficulty", $"k4-{difficulty}");
			SwapClass(player, $"{card}-icon", "mission-icon", MissionIcon(mission));

			SetVar(player, card, "desc", missions.DescribeHere(player.Controller, slot));
			SetVar(player, card, "progress", slot.Completed
				? $"{Text(player, "hud.done")} · +{missions.RewardOf(mission)}"
				: $"{slot.Progress}/{mission.Target} · +{missions.RewardOf(mission)}");
			SetBar(player, $"{card}-bar", slot.Progress, mission.Target);

			// The weekly mission cannot be rerolled; it is only swapped when the map cannot offer it.
			SetVar(player, $"k4-reroll-{i}", "reroll", Text(player, "hud.reroll"));
			SetClass(player, $"k4-reroll-{i}", "k4-gone", weekly || !canReroll || !slot.Untouched);
		}

		SetVar(player, view, "footer", missions.Footer(player));
	}

	/// <summary>The gold line under the header while a points event is on.</summary>
	private void DrawEventBanner(K4Player player)
	{
		if (events?.Current is ActiveEvent active)
		{
			SetVar(player, "k4-event", "event", Text(player, "hud.event-banner", PointEvents.Format(active.Multiplier), PointEvents.MinutesLeft(active, DateTime.UtcNow)));
			SetClass(player, "k4-event", "k4-gone", false);
		}
		else
			SetClass(player, "k4-event", "k4-gone", true);
	}

	/// <summary>Unfinished achievements closest to done first, then the unlocked ones in file order.</summary>
	private List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> AchievementOrder(K4Player player)
	{
		if (achievements is null || player.Achievements is not Achievements.PlayerAchievements mine)
			return [];

		Achievements.AchievementCounters counters = Achievements.AchievementService.CountersOf(player);
		List<(Achievements.AchievementDefinition, int, bool)> order = [.. Achievements.AchievementRules
			.Closest(achievements.All, mine.Unlocked, counters, int.MaxValue)
			.Select(item => (item.Achievement, item.Progress, false))];
		order.AddRange(achievements.All.Where(achievement => mine.Unlocked.Contains(achievement.Id)).Select(achievement => (achievement, achievement.Goal, true)));
		return order;
	}

	private void DrawAchievements(K4Player player, HudState state)
	{
		const string view = "k4-view-achievements";
		List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> order = AchievementOrder(player);
		int pages = Math.Max(1, (order.Count + AchievementCards - 1) / AchievementCards);
		state.AchievementsPage = Math.Clamp(state.AchievementsPage, 0, pages - 1);
		var rows = order.Skip(state.AchievementsPage * AchievementCards).Take(AchievementCards).ToList();

		for (int i = 0; i < AchievementCards; i++)
		{
			string card = $"k4-ach-{i}";
			if (i >= rows.Count)
			{
				SetClass(player, card, "k4-gone", true);
				continue;
			}

			(Achievements.AchievementDefinition achievement, int progress, bool done) = rows[i];
			SetClass(player, card, "k4-gone", false);
			SetClass(player, card, "k4-done", done);
			SetVar(player, card, "name", achievements!.NameFor(achievement, player.Controller));
			SetVar(player, card, "desc", achievements.DescriptionFor(achievement, player.Controller));
			SetVar(player, card, "progress", done
				? $"{Text(player, "hud.done")} · +{Number(achievement.Points)}"
				: $"{Number(progress)}/{Number(achievement.Goal)} · +{Number(achievement.Points)}");
			SetBar(player, $"{card}-bar", progress, achievement.Goal);
			SwapClass(player, $"{card}-icon", "mission-icon", AchievementIcon(achievement.Type));
		}

		int unlocked = player.Achievements is Achievements.PlayerAchievements mine ? achievements?.All.Count(achievement => mine.Unlocked.Contains(achievement.Id)) ?? 0 : 0;
		SetVar(player, view, "summary", Text(player, "hud.ach-summary", unlocked, achievements?.All.Count ?? 0));
		SetVar(player, "k4-ach-page", "page", Text(player, "hud.page", state.AchievementsPage + 1, pages));
		SetClass(player, "k4-ach-prev", "k4-disabled", state.AchievementsPage == 0);
		SetClass(player, "k4-ach-next", "k4-disabled", state.AchievementsPage + 1 >= pages);
	}

	/// <summary>The mission icon of the same kind of goal; the streak gets the hot-trend icon of the mission footer.</summary>
	internal static string AchievementIcon(Achievements.AchievementType type) => type switch
	{
		Achievements.AchievementType.PlaytimeHours => "ic-clock",
		Achievements.AchievementType.MissionStreak => "ic-hot_trend",
		_ => Enum.TryParse(type.ToString(), out MissionType mission) ? MissionIcon(new MissionDefinition { Type = mission }) : "ic-mission_star"
	};

	private void DrawTop(K4Player player, HudState state)
	{
		if (ranks is null)
			return;

		const string view = "k4-view-top";
		int page = state.TopPage;
		ulong steamId = player.SteamId;

		void Draw(List<TopEntry> top, (int Placement, int Total) me)
		{
			List<TopEntry> rows = top.Skip(page * TopRows).Take(TopRows).ToList();

			for (int i = 0; i < TopRows; i++)
			{
				string row = $"k4-top-{i}";
				if (i >= rows.Count)
				{
					SetClass(player, row, "k4-gone", true);
					continue;
				}

				TopEntry entry = rows[i];
				RankDefinition rank = ranks.Ladder.FindByName(entry.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(entry.Points);
				SetClass(player, row, "k4-gone", false);
				SetIcon(player, $"{row}-icon", rank);
				int position = page * TopRows + i + 1;
				SetPlace(player, $"{row}-pos", position);
				SetVar(player, row, "pos", $"#{position}");
				SetVar(player, row, "name", entry.Name);
				SetVar(player, row, "info", Text(player, "hud.top-info", rank.Name, Number(entry.Points)));
			}

			if (player.Rank is RankState own)
				SetIcon(player, "k4-top-me-icon", own.Rank);
			SetPlace(player, "k4-top-me-pos", me.Placement);
			SetVar(player, "k4-top-me", "pos", $"#{Number(me.Placement)}");
			SetVar(player, "k4-top-me", "name", Text(player, "hud.you"));
			SetVar(player, "k4-top-me", "info", Text(player, "hud.top-info", player.Rank?.Rank.Name ?? "", Number(player.Rank?.Points ?? 0)));

			int pages = Math.Clamp((me.Total + TopRows - 1) / TopRows, 1, TopPages);
			state.TopPageCount = pages;
			SetVar(player, view, "page", Text(player, "hud.page", page + 1, pages));
			SetClass(player, "k4-top-prev", "k4-disabled", page == 0);
			SetClass(player, "k4-top-next", "k4-disabled", page + 1 >= pages);
		}

		int needed = (page + 1) * TopRows;
		if (topCache.TryGetValue(needed, out Cached<List<TopEntry>>? top) && top.Fresh
			&& placementCache.TryGetValue(steamId, out Cached<(int, int)>? me) && me.Fresh)
		{
			Draw(top.Value, me.Value);
			return;
		}

		for (int i = 0; i < TopRows; i++)
			SetClass(player, $"k4-top-{i}", "k4-gone", true);
		SetVar(player, view, "page", Text(player, "hud.page", page + 1, "…"));

		Later(player, state, async () =>
		{
			List<TopEntry> entries = await store.GetTopAsync(needed);
			(int placement, int total) = await store.GetPlacementAndTotalAsync(steamId);
			return (entries, (placement, total));
		}, result =>
		{
			topCache[needed] = new Cached<List<TopEntry>>(result.entries, DateTime.UtcNow);
			placementCache[steamId] = new Cached<(int, int)>(result.Item2, DateTime.UtcNow);
		}, result => Draw(result.entries, result.Item2), "Loading the top list panel");
	}

	/// <summary>CS2's gold, silver and bronze laurels around the top three positions.</summary>
	private void SetPlace(K4Player player, string panel, int position)
		=> SwapClass(player, panel, "place", position is >= 1 and <= 3 ? $"k4-place-{position}" : null);

	private void DrawRanks(K4Player player, HudState state)
	{
		if (ranks is null)
			return;

		const string view = "k4-view-ranks";
		IReadOnlyList<RankDefinition> all = ranks.Ladder.Ranks;
		int pages = Math.Max(1, (all.Count + RankRows - 1) / RankRows);
		int page = Math.Clamp(state.RanksPage, 0, pages - 1);
		List<RankDefinition> shown = all.Skip(page * RankRows).Take(RankRows).ToList();

		SetVar(player, view, "page", Text(player, "hud.page", page + 1, pages));
		SetClass(player, "k4-rk-prev", "k4-disabled", page == 0);
		SetClass(player, "k4-rk-next", "k4-disabled", page + 1 >= pages);

		void Draw(Dictionary<string, int>? counts, int total)
		{
			for (int i = 0; i < RankRows; i++)
			{
				string row = $"k4-rk-{i}";
				if (i >= shown.Count)
				{
					SetClass(player, row, "k4-gone", true);
					continue;
				}

				RankDefinition rank = shown[i];
				string share = counts is null || total == 0
					? "…"
					: (counts.GetValueOrDefault(rank.Name) * 100.0 / total).ToString("0.#", CultureInfo.InvariantCulture);
				bool isCurrent = player.Rank?.Rank.Key == rank.Key;

				SetClass(player, row, "k4-gone", false);
				SetClass(player, row, "k4-current", isCurrent);
				SetIcon(player, $"{row}-icon", rank);
				SetVar(player, row, "name", isCurrent ? $"{rank.Name} · {Text(player, "hud.current")}" : rank.Name);
				SetColor(player, $"{row}-name", rank.ColorName);
				SetVar(player, row, "info", rank.IsDefault
					? Text(player, "hud.rank-default", share)
					: Text(player, "hud.rank-info", Number(rank.Point), share));
			}
		}

		if (rankCountCache is { Fresh: true } cached)
		{
			Draw(cached.Value.Counts, cached.Value.Total);
			return;
		}

		Draw(null, 0);
		Later(player, state, store.GetRankCountsAsync,
			result => rankCountCache = new Cached<(Dictionary<string, int>, int)>(result, DateTime.UtcNow),
			result => Draw(result.Counts, result.Total), "Loading the rank list panel");
	}

	private void DrawStats(K4Player player)
	{
		List<(string Label, string Value, string Icon)> tiles = [];
		CultureInfo invariant = CultureInfo.InvariantCulture;

		if (player.Stats is StatCounters stats)
		{
			int kills = stats[StatField.Kills];
			int deaths = stats[StatField.Deaths];
			int shots = stats[StatField.Shoots];
			int rounds = stats[StatField.RoundWin] + stats[StatField.RoundLose];

			tiles.Add((Text(player, "hud.stat.kills"), Number(kills), "ic-kill"));
			tiles.Add((Text(player, "hud.stat.deaths"), Number(deaths), "ic-map_death"));
			tiles.Add((Text(player, "hud.stat.kd"), StatCounters.KillDeathRatio(kills, deaths).ToString("0.00", invariant), "ic-stats"));
			tiles.Add((Text(player, "hud.stat.headshots"), $"{StatService.HeadshotPercentage(stats[StatField.Headshots], kills).ToString("0", invariant)}%", "ic-stat_headshots"));
			tiles.Add((Text(player, "hud.stat.assists"), Number(stats[StatField.Assists]), "ic-stat_assists"));
			tiles.Add((Text(player, "hud.stat.winrate"), $"{(rounds > 0 ? stats[StatField.RoundWin] * 100.0 / rounds : 0).ToString("0", invariant)}%", "ic-trophy"));
			tiles.Add((Text(player, "hud.stat.mvps"), Number(stats[StatField.Mvp]), "ic-stat_mvps"));
			tiles.Add((Text(player, "hud.stat.accuracy"), $"{(shots > 0 ? stats[StatField.HitsGiven] * 100.0 / shots : 0).ToString("0", invariant)}%", "ic-crosshair_circle"));
		}

		if (player.Playtime is not null)
			tiles.Add((Text(player, "hud.stat.playtime"), playtime.Format(playtime.CurrentSeconds(player)[(int)PlaytimeField.All]), "ic-clock"));

		for (int i = 0; i < StatTiles; i++)
		{
			string tile = $"k4-st-{i}";
			SetClass(player, tile, "k4-gone", i >= tiles.Count);
			if (i < tiles.Count)
			{
				SetVar(player, tile, "label", tiles[i].Label);
				SetVar(player, tile, "value", tiles[i].Value);
				SwapClass(player, $"{tile}-icon", "stat-icon", tiles[i].Icon);
			}
		}
	}

	/// <summary>Reward descriptions may contain chat color codes; the panel shows plain text.</summary>
	private static string StripColors(string text) => new(text.Where(character => character >= ' ').ToArray());

	// ---------------------------------------------------------------- toasts

	/// <summary>Shows a toast. Without an icon class it uses the kind's icon: a check, a star or a falling trend.</summary>
	public void ShowToast(K4Player player, string titleKey, string text, ToastKind kind, string? icon = null)
	{
		if (!Usable(player))
			return;

		CCSPlayerController controller = player.Controller;
		int version = toastVersions[player.Slot] = NextVersion();

		CCSCustomHudLayout hud = layout!;
		hud.SetDialogVariableStringForPlayer(controller, Toast, "title", chat.For(controller, titleKey));
		hud.SetDialogVariableStringForPlayer(controller, Toast, "text", StripColors(text));
		SwapClass(player, Toast, "toast", kind switch
		{
			ToastKind.Gold => "k4-toast-gold",
			ToastKind.Warning => "k4-toast-red",
			_ => "k4-toast-green"
		});
		SwapClass(player, "k4-toast-icon", "toast-icon", icon ?? kind switch
		{
			ToastKind.Gold => "ic-mission_star",
			ToastKind.Warning => "ic-cold_trend",
			_ => "ic-check"
		});
		hud.SetHasClassForPlayer(controller, Toast, "k4-gone", false);

		plugin.AddTimer(ToastSeconds, () =>
		{
			if (gameThread.IsActive && toastVersions.GetValueOrDefault(player.Slot) == version && layout is { IsValid: true } && player.HasController)
				layout.SetHasClassForPlayer(player.Controller, Toast, "k4-gone", true);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	/// <summary>Closes every panel when freeze time ends, so nobody starts a live round unable to aim.</summary>
	public void CloseAll()
	{
		foreach (K4Player player in players.All.ToArray())
		{
			if (IsOpen(player))
				Close(player);
		}
	}

	/// <summary>On unload: release every player's mouse, hide toasts, and remove the entity.</summary>
	public void Shutdown()
	{
		if (layout is { IsValid: true })
		{
			foreach (CCSPlayerController controller in Utilities.GetPlayers())
			{
				if (controller.IsValid && !controller.IsBot)
				{
					Hide(controller);
					layout.SetHasClassForPlayer(controller, Toast, "k4-gone", true);
				}
			}

			layout.Remove();
		}

		layout = null;
		open.Clear();
		applied.Clear();
	}
}

/// <summary>
/// Lets rank and mission code send toasts without depending on the panel being built first.
/// Null when the Panorama panel is off; callers then use chat.
/// </summary>
internal sealed class HudBridge
{
	public PanoramaHud? Hud { get; set; }
}
