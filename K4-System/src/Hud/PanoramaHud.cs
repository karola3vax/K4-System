namespace K4System.Hud;

using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Timers;
using K4System.Config;
using K4System.Domain;
using K4System.Missions;
using K4System.Players;
using K4System.Ranks;
using K4System.Seasons;
using K4System.Services;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>The pages of the full-screen panel, in sidebar order.</summary>
public enum HudTab
{
	Home,
	Profile,
	Missions,
	Achievements,
	Leaderboard,
	Ranks,
	Stats,

	/// <summary>The rank now lives on Home (6.x name, kept for callers).</summary>
	Rank = Home,

	/// <summary>The top list is now the Leaderboard (6.x name, kept for callers).</summary>
	Top = Leaderboard
}

/// <summary>The 6.x toast kinds; new code sends a <see cref="Toast"/> instead.</summary>
public enum ToastKind
{
	Success,
	Gold,
	Warning
}

/// <summary>
/// The K4 Panorama panel and toasts. The layout ships to players in the K4 workshop addon
/// (panorama/layout/custom_game/k4_ui.xml); the server only sets text and style classes per player
/// and receives button clicks. Every id and class used here is listed in docs/panel-contract.md. Game thread only.
/// </summary>
internal sealed class PanoramaHud(
	BasePlugin plugin,
	PluginConfig config,
	PlayerRegistry players,
	RankService? ranks,
	MissionService? missions,
	Achievements.AchievementService? achievements,
	EventService? events,
	SeasonService? seasons,
	PlaytimeService playtime,
	SqlPlayerStore store,
	PlayerService settings,
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

	// Page sizes of the layout (docs/panel-contract.md).
	internal const int MissionCards = 3;
	internal const int AchievementCards = 15;
	internal const int ProfileTiles = 6;
	internal const int ProfileSeasons = 3;
	private const int LeaderboardRows = 7;
	private const int LeaderboardPages = 10;
	private const int RankTiles = 20;
	private const int StatTiles = 8;
	private const int HomeAchievements = 3;
	private const int HomeQuick = 5;
	private const int WeaponBars = 3;
	private const int StreakDays = 7;

	// "2d, 23h, 59m, 48s" does not fit a tile; two units do.
	private const int StatTimeUnits = 2;

	// Finished seasons the Leaderboard can switch through.
	private const int SeasonsShown = 24;

	private const float PromotionSeconds = 4f;

	// CS2 can kick a player when a chat command and a HUD update share a frame, so drawing waits a
	// moment, and mouse capture is switched on a moment after the panel is drawn.
	private const float CommandDelaySeconds = 0.2f;
	private const float CaptureDelaySeconds = 0.2f;

	// The mouse aims nowhere while the panel is open, so an untouched panel closes itself.
	private const float IdleCloseSeconds = 45f;

	// Database-backed views are cached briefly, so spamming commands or page buttons cannot flood the database.
	private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

	private static readonly string[] PageIds = ["home", "profile", "missions", "achievements", "leaderboard", "ranks", "stats"];

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

	private static readonly string[] StatIcons =
	[
		"ic-kill", "ic-map_death", "ic-stats", "ic-stat_headshots", "ic-stat_assists", "ic-trophy", "ic-stat_mvps",
		"ic-crosshair_circle", "ic-clock", "ic-winner_ribbon", "ic-leader"
	];

	private enum AchievementFilter
	{
		All,
		Progress,
		Unlocked,
		Shown
	}

	private sealed class HudState
	{
		public HudTab Tab { get; set; }
		public int Version { get; set; }

		public int LeaderboardPage { get; set; }
		public int LeaderboardPageCount { get; set; } = 1;

		/// <summary>0 shows the current season; 1 and up the finished seasons, newest first.</summary>
		public int Season { get; set; }

		public int RanksPage { get; set; }
		public int AchievementsPage { get; set; }
		public AchievementFilter Filter { get; set; }

		/// <summary>The players on the drawn Leaderboard (podium 0-2, rows 3-9), so a click can open their profile.</summary>
		public ulong[] LeaderboardIds { get; } = new ulong[3 + LeaderboardRows];

		/// <summary>The unlocked achievements on the drawn cards, so a click can add one to the showcase.</summary>
		public string?[] AchievementIds { get; } = new string?[AchievementCards];

		/// <summary>Whose profile the Profile page shows; 0 for the player's own.</summary>
		public ulong ProfileOf { get; set; }
	}

	// Versions come from one counter that never restarts, so a timer or database draw from an earlier
	// opening, an earlier map, or the slot's previous player can never match the current panel.
	private int lastVersion;

	private int NextVersion() => ++lastVersion;

	private sealed record Cached<T>(T Value, DateTime LoadedUtc)
	{
		public bool Fresh => DateTime.UtcNow - LoadedUtc < CacheFor;
	}

	private sealed record ShownToast(Toast Toast, int Id)
	{
		/// <summary>Sliding out: still drawn in its slot until the slide-out ends.</summary>
		public bool Leaving { get; set; }
	}

	// Matches the slide-out transition of .k4-toast.k4-out and .k4-promo.k4-out in the stylesheet.
	private const float LeaveSeconds = 0.3f;

	private readonly Dictionary<int, HudState> open = [];
	private readonly Dictionary<int, List<ShownToast>> toasts = [];
	private readonly Dictionary<int, int> promotions = [];

	// The class currently applied per player, panel, and class group, so only changes are sent.
	private readonly Dictionary<int, Dictionary<(string Panel, string Group), string>> applied = [];

	private readonly Dictionary<int, Cached<List<TopEntry>>> topCache = [];
	private readonly Dictionary<ulong, Cached<(int Placement, int Total)>> placementCache = [];
	private readonly Dictionary<ulong, Cached<ProfileRecord?>> profileCache = [];

	// Finished seasons never change, so these stay until the next season ends.
	private List<string>? finishedSeasons;
	private readonly Dictionary<(string Season, int Count), (List<TopEntry> Top, int Players)> seasonTopCache = [];

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
		toasts.Clear();
		promotions.Clear();
		applied.Clear();
		placementCache.Clear();
		profileCache.Clear();
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
			yield return $"{player.Name} slot {player.Slot}: panel {tab}, mouse capture {capture}, toasts {toasts.GetValueOrDefault(player.Slot)?.Count ?? 0}";
		}
	}

	// ---------------------------------------------------------------- open, close, clicks

	/// <summary>
	/// Opens the panel on a page. Returns false when the panel cannot be shown, so the command can
	/// fall back to chat. Called from chat commands, so drawing waits a short moment.
	/// </summary>
	public bool Open(K4Player player, HudTab tab)
	{
		if (!Usable(player))
			return false;

		logger.LogDebug("K4 panel: opening {Tab} for {Player} (layout #{Index}).", tab, player.Name, layout!.Index);
		HudState state = open.TryGetValue(player.Slot, out HudState? existing) ? existing : new HudState();
		state.Tab = IsPageAvailable(tab) ? tab : HudTab.Home;
		if (state.Tab == HudTab.Profile)
			state.ProfileOf = 0;
		open[player.Slot] = state;
		ShowLater(player, state, enableCapture: true);
		return true;
	}

	/// <summary>Opens the Profile page on a player's profile, the caller's own for their own Steam id.</summary>
	public bool OpenProfile(K4Player player, ulong steamId)
	{
		if (!config.Modules.Profiles || !Open(player, HudTab.Profile))
			return false;

		open[player.Slot].ProfileOf = steamId == player.SteamId ? 0 : steamId;
		return true;
	}

	/// <summary>Redraws the panel if it is open on this page, for changes made outside the panel.</summary>
	public void Refresh(K4Player player, HudTab tab)
	{
		if (open.TryGetValue(player.Slot, out HudState? state) && state.Tab == tab)
			ShowLater(player, state, enableCapture: false);
	}

	/// <summary>Redraws every open panel on its current page, for changes that concern everyone, such as a points event.</summary>
	public void RefreshOpenPanels()
	{
		foreach ((int slot, HudState state) in open.ToArray())
		{
			if (players.All.FirstOrDefault(player => player.Slot == slot) is K4Player player)
				ShowLater(player, state, enableCapture: false);
		}
	}

	/// <summary>A season ended: every cached leaderboard is out of date.</summary>
	public void SeasonChanged()
	{
		topCache.Clear();
		placementCache.Clear();
		profileCache.Clear();
		finishedSeasons = null;
		seasonTopCache.Clear();
		foreach (HudState state in open.Values)
			state.Season = 0;
		RefreshOpenPanels();
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
	/// Clears the leaving player's panel, toasts, and mouse capture. The layout keeps these per slot,
	/// so without this the next player in the slot could start with a captured mouse.
	/// </summary>
	public void OnDisconnect(CCSPlayerController controller)
	{
		if (layout is { IsValid: true } && controller.IsValid)
		{
			Hide(controller);
			for (int i = 0; i < global::K4System.Hud.Toast.Slots; i++)
				layout.SetHasClassForPlayer(controller, $"k4-t{i}", "k4-gone", true);
			layout.SetHasClassForPlayer(controller, "k4-t-more", "k4-gone", true);
			layout.SetHasClassForPlayer(controller, "k4-promo", "k4-gone", true);

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
		toasts.Remove(controller.Slot);
		promotions.Remove(controller.Slot);
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

		if (!HandleClick(player, state, buttonId))
			return;

		state.Version = NextVersion();
		Show(player, state, enableCapture: false);
	}

	/// <summary>Applies a click to the panel state. False when nothing changed and no redraw is needed.</summary>
	private bool HandleClick(K4Player player, HudState state, string id)
	{
		switch (id)
		{
			case "k4-close":
				Close(player);
				return false;
			case "k4-me":
				return Go(state, HudTab.Profile);
			case "k4-h-missions":
				return Go(state, HudTab.Missions);
			case "k4-h-ach":
				return Go(state, HudTab.Achievements);
			case "k4-h-top":
				return Go(state, HudTab.Leaderboard);
			case "k4-pf-back":
				state.ProfileOf = 0;
				return true;
			case "k4-pf-toasts" when state.ProfileOf == 0:
				settings.SetToasts(player, player.ToastsOff);
				return true;
			case "k4-pf-chat" when state.ProfileOf == 0 && player.Rank is RankState rank:
				settings.SetPointMessages(player, rank.MuteMessages);
				return true;
			case "k4-lb-newer" when state.Season > 0:
				state.Season--;
				state.LeaderboardPage = 0;
				return true;
			case "k4-lb-older" when finishedSeasons is not null && state.Season < finishedSeasons.Count:
				state.Season++;
				state.LeaderboardPage = 0;
				return true;
			case "k4-lb-prev" when state.LeaderboardPage > 0:
				state.LeaderboardPage--;
				return true;
			case "k4-lb-next" when state.LeaderboardPage < state.LeaderboardPageCount - 1:
				state.LeaderboardPage++;
				return true;
			case "k4-rk-prev" when state.RanksPage > 0:
				state.RanksPage--;
				return true;
			case "k4-rk-next" when ranks is not null && (state.RanksPage + 1) * RankTiles < ranks.Ladder.Ranks.Count:
				state.RanksPage++;
				return true;
			case "k4-ach-prev" when state.AchievementsPage > 0:
				state.AchievementsPage--;
				return true;
			case "k4-ach-next" when (state.AchievementsPage + 1) * AchievementCards < FilteredAchievements(player, state.Filter).Count:
				state.AchievementsPage++;
				return true;
		}

		if (id.StartsWith("k4-nav-", StringComparison.Ordinal) && Array.IndexOf(PageIds, id["k4-nav-".Length..]) is int page and >= 0)
			return Go(state, (HudTab)page);

		if (id.StartsWith("k4-h-q", StringComparison.Ordinal))
			return Go(state, HudTab.Stats);

		if (id.StartsWith("k4-pf-sc", StringComparison.Ordinal) && state.ProfileOf == 0 && Go(state, HudTab.Achievements))
		{
			state.Filter = AchievementFilter.Shown;
			state.AchievementsPage = 0;
			return true;
		}

		if (id.StartsWith("k4-af-", StringComparison.Ordinal) && Enum.TryParse(id["k4-af-".Length..], ignoreCase: true, out AchievementFilter filter))
		{
			state.Filter = filter;
			state.AchievementsPage = 0;
			return true;
		}

		if (id.StartsWith("k4-reroll-", StringComparison.Ordinal) && missions is not null && int.TryParse(id["k4-reroll-".Length..], out int reroll))
		{
			missions.Reroll(player, (reroll + 1).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (id.StartsWith("k4-a", StringComparison.Ordinal) && int.TryParse(id["k4-a".Length..], out int card)
			&& card is >= 0 and < AchievementCards && state.AchievementIds[card] is string achievement)
		{
			ToggleShowcase(player, achievement);
			return true;
		}

		if (config.Modules.Profiles && LeaderboardSlot(id) is int slot && state.LeaderboardIds[slot] is ulong target and not 0)
		{
			state.Tab = HudTab.Profile;
			state.ProfileOf = target == player.SteamId ? 0 : target;
			return true;
		}

		return false;
	}

	/// <summary>The index in <see cref="HudState.LeaderboardIds"/> of a podium or row button, or null.</summary>
	internal static int? LeaderboardSlot(string id)
	{
		if (id.StartsWith("k4-lb-p", StringComparison.Ordinal) && int.TryParse(id["k4-lb-p".Length..], out int podium) && podium is >= 0 and < 3)
			return podium;
		if (id.StartsWith("k4-lb-", StringComparison.Ordinal) && int.TryParse(id["k4-lb-".Length..], out int row) && row is >= 0 and < LeaderboardRows)
			return 3 + row;
		return null;
	}

	private bool Go(HudState state, HudTab tab)
	{
		if (!IsPageAvailable(tab))
			return false;
		state.Tab = tab;
		if (tab == HudTab.Profile)
			state.ProfileOf = 0;
		return true;
	}

	// ---------------------------------------------------------------- drawing

	private void Show(K4Player player, HudState state, bool enableCapture)
	{
		logger.LogDebug("K4 panel: drawing {Tab} for {Player} (capture {Capture}).", state.Tab, player.Name, enableCapture);
		CCSPlayerController controller = player.Controller;

		DrawFrame(player, state);

		switch (state.Tab)
		{
			case HudTab.Profile:
				DrawProfile(player, state);
				break;
			case HudTab.Missions:
				DrawMissions(player);
				break;
			case HudTab.Achievements:
				DrawAchievements(player, state);
				break;
			case HudTab.Leaderboard:
				DrawLeaderboard(player, state);
				break;
			case HudTab.Ranks:
				DrawRanks(player, state);
				break;
			case HudTab.Stats:
				DrawStats(player);
				break;
			default:
				DrawHome(player, state);
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

	private bool IsPageAvailable(HudTab tab) => tab switch
	{
		HudTab.Leaderboard or HudTab.Ranks => ranks is not null,
		HudTab.Missions => missions is not null,
		HudTab.Achievements => achievements is not null,
		HudTab.Profile => config.Modules.Profiles,
		HudTab.Stats => config.Modules.Stats || config.Modules.Playtime,
		_ => true
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

	private void Gone(K4Player player, string panel, bool gone) => SetClass(player, panel, "k4-gone", gone);

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

	/// <summary>The class a layout panel of this group starts with (docs/panel-contract.md), or null.</summary>
	internal static string? DefaultClass(string group) => group switch
	{
		"icon" => "sg-0",
		"bar" => "w0",
		"tier" => "t-locked",
		"toast" => "k4-tc-green",
		"length" => "k4-len-normal",
		_ => null
	};

	private void SetBar(K4Player player, string panel, long value, long target)
	{
		int step = target <= 0 ? 20 : (int)Math.Clamp(value * 20 / target, 0, 20);
		SwapClass(player, panel, "bar", $"w{step}");
	}

	private void SetColor(K4Player player, string panel, string colorName)
		=> SwapClass(player, panel, "color", $"col-{(ColorNames.Contains(colorName) ? colorName : "default")}");

	/// <summary>CS2's own rank icons, numbered like the scoreboard: the default rank is 0, then 1 to 18.</summary>
	private void SetEmblem(K4Player player, string panel, RankDefinition rank)
		=> SwapClass(player, panel, "icon", RankIcon(rank) ?? $"sg-{RankNumber(rank)}");

	private void SetPicture(K4Player player, string panel, string picture) => SwapClass(player, panel, "picture", picture);

	private static int RankNumber(RankDefinition rank) => Math.Clamp(rank.Id + 1, 0, 18);

	/// <summary>The rank icon class for this rank, or null when the server shows rank tags instead.</summary>
	public string? RankIcon(RankDefinition rank)
	{
		int number = RankNumber(rank);
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

	/// <summary>The mission icon of the same kind of goal; the streak gets the hot-trend icon of the mission footer.</summary>
	internal static string AchievementIcon(Achievements.AchievementType type) => type switch
	{
		Achievements.AchievementType.PlaytimeHours => "ic-clock",
		Achievements.AchievementType.MissionStreak => "ic-hot_trend",
		_ => Enum.TryParse(type.ToString(), out MissionType mission) ? MissionIcon(new MissionDefinition { Type = mission }) : "ic-mission_star"
	};

	/// <summary>Every picture class the server can apply, for the layout tests.</summary>
	internal static IEnumerable<string> IconClasses()
	{
		foreach (MissionType type in Enum.GetValues<MissionType>())
			yield return MissionIcon(new MissionDefinition { Type = type });
		foreach (string weapon in WeaponIcons)
			yield return $"eq-{weapon}";
		foreach (string icon in StatIcons)
			yield return icon;
		foreach (string icon in new[] { "ic-bullet", "ic-check", "ic-hot_trend", "ic-cold_trend", "ic-trade_up", "ic-mission_star", "ic-trophy", "ic-watch", "tp-t", "tp-ct" })
			yield return icon;
		foreach (Achievements.AchievementType type in Enum.GetValues<Achievements.AchievementType>())
			yield return AchievementIcon(type);
	}

	/// <summary>The medal ring class of an achievement: harder ones (more points) get a better metal.</summary>
	internal static string MedalTier(Achievements.AchievementDefinition achievement) => $"t-{Achievements.AchievementRules.Tier(achievement)}";

	private string Text(K4Player player, string key, params object[] args) => chat.For(player.Controller, key, args);

	// Capital letters follow the reader's K4 language, not the game's: CS2's CSS uppercase used the client's own
	// locale, so a Turkish client turned English "Missions" into "MİSSİONS".
	private static string Upper(K4Player player, string text) => Upper(player.Controller.GetLanguage(), text);

	internal static string Upper(CultureInfo language, string text) => text.ToUpper(language);

	private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

	/// <summary>Reward descriptions may contain chat color codes; the panel shows plain text.</summary>
	private static string StripColors(string text) => new(text.Where(character => character >= ' ').ToArray());

	private (int Placement, int Total)? CachedPlacement(ulong steamId)
		=> placementCache.TryGetValue(steamId, out Cached<(int, int)>? cached) && cached.Fresh ? cached.Value : null;

	// ---------------------------------------------------------------- frame

	/// <summary>Sidebar, background, event line and page switching: everything around the open page.</summary>
	private void DrawFrame(K4Player player, HudState state)
	{
		SwapClass(player, "k4-bg-map", "map", $"map-{Server.MapName.ToLowerInvariant()}");

		SetVar(player, "k4-me", "name", player.Name);
		if (player.Rank is RankState rank)
		{
			SetEmblem(player, "k4-me-icon", rank.Rank);
			(int Placement, int Total)? placed = CachedPlacement(player.SteamId);
			string points = Text(player, "hud.points", Number(rank.Points));
			SetVar(player, "k4-me", "meta", placed is { } p ? $"{points} · #{Number(p.Placement)}" : points);
		}
		else
			SetVar(player, "k4-me", "meta", "");

		for (int i = 0; i < PageIds.Length; i++)
		{
			string nav = $"k4-nav-{PageIds[i]}";
			SetVar(player, nav, "label", Upper(player, Text(player, $"hud.page.{PageIds[i]}")));
			SetClass(player, nav, "k4-active", (int)state.Tab == i);
			Gone(player, nav, !IsPageAvailable((HudTab)i));
			SetClass(player, $"k4-view-{PageIds[i]}", "k4-show", (int)state.Tab == i);
		}

		if (seasons?.Current is string season)
		{
			CultureInfo language = player.Controller.GetLanguage();
			SetVar(player, "k4-season", "title", Upper(player, Text(player, "hud.season-card", SeasonClock.Name(season, language))));
			SetVar(player, "k4-season", "left", Text(player, "hud.season-left", seasons.TimeLeft(player.Controller, season)));
			if (SeasonClock.TryParse(season, out DateTime start))
			{
				double total = (SeasonClock.EndsUtc(season, seasons.Zone) - TimeZoneInfo.ConvertTimeToUtc(start, seasons.Zone)).TotalSeconds;
				double left = SeasonClock.Left(season, DateTime.UtcNow, seasons.Zone).TotalSeconds;
				SetBar(player, "k4-season-bar", (long)(total - left), (long)total);
			}
			Gone(player, "k4-season", false);
		}
		else
			Gone(player, "k4-season", true);

		if (events?.Current is ActiveEvent active)
		{
			SetVar(player, "k4-event", "event", Upper(player, Text(player, "hud.event-banner", PointEvents.Format(active.Multiplier), PointEvents.MinutesLeft(active, DateTime.UtcNow))));
			Gone(player, "k4-event", false);
		}
		else
			Gone(player, "k4-event", true);
	}

	// ---------------------------------------------------------------- home

	private void DrawHome(K4Player player, HudState state)
	{
		const string view = "k4-view-home";

		if (ranks is not null && player.Rank is RankState rank)
		{
			RankDefinition current = rank.Rank;
			RankDefinition? next = RankLadder.Next(ranks.Progression(player), rank.Points);
			SetEmblem(player, "k4-h-rank-icon", current);
			SetVar(player, "k4-h-rank-name", "rank", current.Name);
			SetColor(player, "k4-h-rank-name", current.ColorName);
			SetVar(player, view, "points", Text(player, "hud.points", Number(rank.Points)));
			SetVar(player, view, "place", CachedPlacement(player.SteamId) is { } placed ? $"#{Number(placed.Placement)}" : "");

			if (next is null)
			{
				SetVar(player, view, "next", Upper(player, Text(player, "hud.max-rank")));
				SetBar(player, "k4-h-bar", 1, 1);
			}
			else
			{
				int from = Math.Max(0, current.Point);
				SetVar(player, view, "next", Text(player, "hud.next-in", next.Name, Number(next.Point - rank.Points)));
				SetBar(player, "k4-h-bar", rank.Points - from, next.Point - from);
			}

			if (CachedPlacement(player.SteamId) is null)
			{
				ulong steamId = player.SteamId;
				Later(player, state, () => store.GetPlacementAndTotalAsync(steamId),
					result => placementCache[steamId] = new Cached<(int, int)>(result, DateTime.UtcNow),
					result => SetVar(player, view, "place", $"#{Number(result.Placement)}"), "Loading the home page");
			}
		}

		// Missions: three daily ones and the weekly one, each its icon and progress.
		List<MissionProgress> today = [];
		if (missions is not null && player.Missions is not null)
		{
			missions.EnsureToday(player);
			today = [.. player.Missions.All];
		}
		Gone(player, "k4-h-missions", missions is null);
		for (int i = 0; i < 4; i++)
		{
			string slot = $"k4-h-m{i}";
			Gone(player, slot, i >= today.Count);
			if (i >= today.Count)
				continue;
			MissionProgress mission = today[i];
			SetPicture(player, $"{slot}-icon", MissionIcon(mission.Mission));
			SetClass(player, slot, "k4-done", mission.Completed);
			SetVar(player, slot, "value", mission.Completed ? "✓" : $"{mission.Progress}/{mission.Mission.Target}");
			SetVar(player, slot, "tip", Text(player, "hud.tip.mission", missions!.DescribeHere(player.Controller, mission), Number(missions.RewardOf(mission.Mission))));
		}

		// The three unfinished achievements closest to done.
		List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> closest = [.. AchievementOrder(player).Where(item => !item.Done).Take(HomeAchievements)];
		Gone(player, "k4-h-ach", achievements is null);
		for (int i = 0; i < HomeAchievements; i++)
		{
			string slot = $"k4-h-a{i}";
			Gone(player, slot, i >= closest.Count);
			if (i >= closest.Count)
				continue;
			(Achievements.AchievementDefinition achievement, int progress, _) = closest[i];
			SetVar(player, slot, "name", achievements!.NameFor(achievement, player.Controller));
			SetVar(player, slot, "tip", Text(player, "hud.tip.progress", achievements.DescriptionFor(achievement, player.Controller),
				Number(progress), Number(achievement.Goal), Number(achievement.Points)));
			SwapClass(player, $"{slot}-ring", "tier", "t-locked");
			SetPicture(player, $"{slot}-icon", AchievementIcon(achievement.Type));
			SetBar(player, $"{slot}-bar", progress, achievement.Goal);
		}

		// Podium of the current leaderboard.
		Gone(player, "k4-h-top", ranks is null);
		if (ranks is not null)
		{
			void Podium(List<TopEntry> top)
			{
				for (int i = 0; i < 3; i++)
				{
					int position = i switch { 0 => 2, 1 => 1, _ => 3 };
					string slot = $"k4-h-p{i}";
					Gone(player, slot, position > top.Count);
					if (position > top.Count)
						continue;
					TopEntry entry = top[position - 1];
					SetVar(player, slot, "name", entry.Name);
					SetVar(player, slot, "pos", position.ToString(CultureInfo.InvariantCulture));
					SetEmblem(player, $"{slot}-icon", ResolveRank(entry));
				}
			}

			int needed = LeaderboardNeeded(0);
			if (topCache.TryGetValue(needed, out Cached<List<TopEntry>>? cached) && cached.Fresh)
				Podium(cached.Value);
			else
			{
				Later(player, state, () => store.GetTopAsync(needed),
					result => topCache[needed] = new Cached<List<TopEntry>>(result, DateTime.UtcNow), Podium, "Loading the home podium");
			}
		}

		// Quick stats.
		List<(string Value, string Icon, string Tip)> quick = [];
		if (player.Stats is StatCounters stats)
		{
			int kills = stats[StatField.Kills];
			int shots = stats[StatField.Shoots];
			quick.Add((StatCounters.KillDeathRatio(kills, stats[StatField.Deaths]).ToString("0.00", CultureInfo.InvariantCulture), "ic-stats", Text(player, "hud.stat.kd")));
			quick.Add(($"{StatService.HeadshotPercentage(stats[StatField.Headshots], kills).ToString("0", CultureInfo.InvariantCulture)}%", "ic-stat_headshots", Text(player, "hud.stat.headshots")));
			quick.Add(($"{(shots > 0 ? stats[StatField.HitsGiven] * 100.0 / shots : 0).ToString("0", CultureInfo.InvariantCulture)}%", "ic-crosshair_circle", Text(player, "hud.stat.accuracy")));
		}
		if (player.Playtime is not null)
			quick.Add((playtime.Format(playtime.CurrentSeconds(player)[(int)PlaytimeField.All], StatTimeUnits), "ic-clock", Text(player, "hud.stat.playtime")));
		if (player.Stats is not null && player.Profile is PlayerProfile profile && PlayerProfile.Favorite(profile.WeaponKills) is (string weapon, int weaponKills))
			quick.Add((Number(weaponKills), WeaponIcon(weapon), Text(player, "hud.tip.favorite", WeaponCatalog.DisplayName(weapon), Number(weaponKills))));

		for (int i = 0; i < HomeQuick; i++)
		{
			string tile = $"k4-h-q{i}";
			Gone(player, tile, i >= quick.Count);
			if (i >= quick.Count)
				continue;
			SetVar(player, tile, "value", quick[i].Value);
			SetVar(player, tile, "tip", quick[i].Tip);
			SetPicture(player, $"{tile}-icon", quick[i].Icon);
		}
	}

	private RankDefinition ResolveRank(TopEntry entry)
		=> ranks!.Ladder.FindByName(entry.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(entry.Points);

	// ---------------------------------------------------------------- profile

	/// <summary>Shows an unlocked achievement on the player's profile, or takes it off again.</summary>
	private void ToggleShowcase(K4Player player, string achievement)
	{
		if (player.Profile is not PlayerProfile profile || player.Achievements?.Unlocked.Contains(achievement) != true)
			return;

		if (profile.Toggle(achievement) == PlayerProfile.ShowcaseChange.Full)
			chat.Tell(player.Controller, "profile.showcase-full", PlayerProfile.ShowcaseSize);
	}

	/// <summary>A profile as the panel shows it: stored data, with live values when the player is on this server.</summary>
	internal sealed record ProfileData(
		string Name,
		int Points,
		RankDefinition? Rank,
		int Placement,
		int Total,
		int[]? Stats,
		int? PlaytimeSeconds,
		IReadOnlyCollection<string> Achievements,
		IReadOnlyList<string> Showcase,
		IReadOnlyDictionary<string, int> WeaponKills,
		IReadOnlyList<SeasonResult> Seasons);

	private ProfileData? BuildProfile(ProfileRecord? stored, K4Player? live)
	{
		if (stored is null && live is null)
			return null;

		RankDefinition? rank = live?.Rank?.Rank
			?? (ranks is null || stored is null ? null : ranks.Ladder.FindByName(stored.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(stored.Record.Points));

		int[]? stats = live is not null ? live.Stats?.ToArray() : config.Modules.Stats ? stored!.Record.Stats : null;
		int? seconds = live is not null
			? live.Playtime is null ? null : playtime.CurrentSeconds(live)[(int)PlaytimeField.All]
			: config.Modules.Playtime ? stored!.Record.Playtime[(int)PlaytimeField.All] : null;
		(int Placement, int Total)? cachedPlace = live is null ? null : CachedPlacement(live.SteamId);

		return new ProfileData(
			live?.Name ?? stored!.Name,
			live?.Rank?.Points ?? stored?.Record.Points ?? 0,
			rank,
			stored?.Record.Placement ?? cachedPlace?.Placement ?? 0,
			stored?.Total ?? cachedPlace?.Total ?? 0,
			stats,
			seconds,
			live?.Achievements?.Unlocked ?? stored?.Record.Achievements ?? [],
			live?.Profile?.Showcase ?? stored?.Record.Showcase ?? [],
			live?.Profile?.WeaponKills ?? stored?.Record.WeaponKills ?? new Dictionary<string, int>(),
			stored?.Seasons ?? []);
	}

	private void DrawProfile(K4Player player, HudState state)
	{
		ulong target = state.ProfileOf == 0 ? player.SteamId : state.ProfileOf;
		bool own = state.ProfileOf == 0;
		Gone(player, "k4-pf-back", own);
		SetVar(player, "k4-pf-back", "back", Upper(player, Text(player, "hud.pf-back")));

		// The player's own settings, only on their own profile.
		Gone(player, "k4-pf-toasts", !own);
		Gone(player, "k4-pf-chat", !own || player.Rank is null);
		if (own)
		{
			SetVar(player, "k4-pf-toasts", "label", Upper(player, Text(player, player.ToastsOff ? "hud.pf-toasts-off" : "hud.pf-toasts-on")));
			SetClass(player, "k4-pf-toasts", "k4-on", !player.ToastsOff);
			bool chatOn = player.Rank is { MuteMessages: false };
			SetVar(player, "k4-pf-chat", "label", Upper(player, Text(player, chatOn ? "hud.pf-chat-on" : "hud.pf-chat-off")));
			SetClass(player, "k4-pf-chat", "k4-on", chatOn);
		}

		void Draw(ProfileRecord? stored)
		{
			K4Player? live = players.Get(target) is { IsHuman: true } online && (online.Rank is not null || online.Stats is not null) ? online : null;
			if (BuildProfile(stored, live) is ProfileData profile)
				DrawProfileData(player, profile, own);
			else
				SetVar(player, "k4-view-profile", "name", StripColors(Text(player, "error.loading")));
		}

		if (profileCache.TryGetValue(target, out Cached<ProfileRecord?>? cached) && cached.Fresh)
		{
			Draw(cached.Value);
			return;
		}

		// The own profile draws at once from memory; finished seasons and the position follow from the database.
		if (own)
			Draw(null);
		Later(player, state, () => store.GetProfileAsync(target, ProfileSeasons),
			result => profileCache[target] = new Cached<ProfileRecord?>(result, DateTime.UtcNow), Draw, "Loading a profile");
	}

	private void DrawProfileData(K4Player player, ProfileData profile, bool own)
	{
		const string view = "k4-view-profile";
		CultureInfo invariant = CultureInfo.InvariantCulture;
		bool textBadge = IconSet == IconsText;

		SetVar(player, view, "name", profile.Name);
		if (profile.Rank is RankDefinition rank)
		{
			SetEmblem(player, "k4-pf-icon", rank);
			SetVar(player, "k4-pf-badge", "tag", string.IsNullOrWhiteSpace(rank.Tag) ? rank.Name : rank.Tag.Trim('[', ']'));
			SetVar(player, "k4-pf-rank", "rank", rank.Name);
			SetColor(player, "k4-pf-rank", rank.ColorName);
			SetColor(player, "k4-pf-badge", rank.ColorName);
		}
		else
			SetVar(player, "k4-pf-rank", "rank", "");
		Gone(player, "k4-pf-icon", textBadge || profile.Rank is null);
		Gone(player, "k4-pf-badge", !textBadge || profile.Rank is null);

		SetVar(player, view, "points", ranks is null ? "" : Text(player, "hud.points", Number(profile.Points)));
		SetVar(player, view, "place", profile.Placement > 0 ? $"#{Number(profile.Placement)}" : "");

		// Showcase: up to three chosen achievements, as medals.
		List<Achievements.AchievementDefinition> shown = achievements is null ? [] : [.. profile.Showcase
			.Where(profile.Achievements.Contains)
			.Select(id => achievements.All.FirstOrDefault(achievement => achievement.Id == id))
			.OfType<Achievements.AchievementDefinition>()];
		for (int i = 0; i < PlayerProfile.ShowcaseSize; i++)
		{
			string slot = $"k4-pf-sc{i}";
			Gone(player, slot, i >= shown.Count);
			if (i >= shown.Count)
				continue;
			string description = achievements!.DescriptionFor(shown[i], player.Controller);
			SetVar(player, slot, "name", achievements.NameFor(shown[i], player.Controller));
			SetVar(player, slot, "desc", description);
			SetVar(player, slot, "tip", own
				? Text(player, "hud.tip.showcase-own", description, Number(shown[i].Points))
				: Text(player, "hud.tip.unlocked", description, Number(shown[i].Points)));
			SwapClass(player, $"{slot}-ring", "tier", MedalTier(shown[i]));
			SetPicture(player, $"{slot}-icon", AchievementIcon(shown[i].Type));
		}
		SetVar(player, view, "hint", Text(player, own ? "hud.pf-showcase-hint" : "hud.pf-showcase-none"));
		Gone(player, "k4-pf-sc-hint", achievements is null || shown.Count > 0);

		// Key stats.
		List<(string Label, string Value, string Icon)> tiles = [];
		if (profile.Stats is int[] stats)
		{
			int kills = stats[(int)StatField.Kills];
			tiles.Add((Text(player, "hud.stat.kills"), Number(kills), "ic-kill"));
			tiles.Add((Text(player, "hud.stat.kd"), StatCounters.KillDeathRatio(kills, stats[(int)StatField.Deaths]).ToString("0.00", invariant), "ic-stats"));
			tiles.Add((Text(player, "hud.stat.headshots"), $"{StatService.HeadshotPercentage(stats[(int)StatField.Headshots], kills).ToString("0", invariant)}%", "ic-stat_headshots"));
			tiles.Add((Text(player, "hud.stat.mvps"), Number(stats[(int)StatField.Mvp]), "ic-stat_mvps"));
		}
		if (profile.PlaytimeSeconds is int seconds)
			tiles.Add((Text(player, "hud.stat.playtime"), playtime.Format(seconds, StatTimeUnits), "ic-clock"));
		if (achievements is not null)
			tiles.Add((Text(player, "hud.page.achievements"), $"{achievements.All.Count(achievement => profile.Achievements.Contains(achievement.Id))}/{achievements.All.Count}", "ic-winner_ribbon"));

		for (int i = 0; i < ProfileTiles; i++)
		{
			string tile = $"k4-pf-st{i}";
			Gone(player, tile, i >= tiles.Count);
			if (i >= tiles.Count)
				continue;
			SetVar(player, tile, "label", Upper(player, tiles[i].Label));
			SetVar(player, tile, "value", tiles[i].Value);
			SetPicture(player, $"{tile}-icon", tiles[i].Icon);
		}

		// Favorite weapon.
		(string Weapon, int Kills)? favorite = profile.Stats is null ? null : PlayerProfile.Favorite(profile.WeaponKills);
		Gone(player, "k4-pf-fav", favorite is null);
		if (favorite is (string weapon, int weaponKills))
		{
			SetPicture(player, "k4-pf-fav-icon", WeaponIcon(weapon));
			SetVar(player, view, "fav", WeaponCatalog.DisplayName(weapon));
			SetVar(player, view, "favkills", Number(weaponKills));
		}

		// Finished seasons.
		bool showSeasons = seasons is not null && ranks is not null;
		SetVar(player, view, "none", Text(player, "hud.pf-seasons-none"));
		Gone(player, "k4-pf-ss-none", !showSeasons || profile.Seasons.Count > 0);
		CultureInfo language = player.Controller.GetLanguage();
		for (int i = 0; i < ProfileSeasons; i++)
		{
			string slot = $"k4-pf-ss{i}";
			bool has = showSeasons && i < profile.Seasons.Count;
			Gone(player, slot, !has);
			if (!has)
				continue;
			SeasonResult result = profile.Seasons[i];
			RankDefinition final = ranks!.Ladder.FindByName(result.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(result.Points);
			SetEmblem(player, $"{slot}-icon", final);
			SetVar(player, slot, "season", Upper(player, SeasonClock.Name(result.Season, language)));
			SetVar(player, slot, "pos", $"#{Number(result.Placement)}");
			SetPlace(player, slot, result.Placement);
		}
	}

	/// <summary>Gold, silver and bronze for the top three positions.</summary>
	private void SetPlace(K4Player player, string panel, int position)
		=> SwapClass(player, panel, "place", position is >= 1 and <= 3 ? $"k4-place-{position}" : null);

	// ---------------------------------------------------------------- missions

	private void DrawMissions(K4Player player)
	{
		const string view = "k4-view-missions";
		if (missions is null || player.Missions is null)
		{
			for (int i = 0; i < MissionCards; i++)
				Gone(player, $"k4-m{i}", true);
			Gone(player, "k4-mw", true);
			SetVar(player, view, "reset", StripColors(Text(player, "error.loading")));
			return;
		}

		// Start today's missions if midnight passed since the last round start.
		missions.EnsureToday(player);
		PlayerMissions today = player.Missions;
		bool canReroll = today.RerollsUsed < config.Missions.RerollsPerDay;

		for (int i = 0; i < MissionCards; i++)
		{
			string card = $"k4-m{i}";
			Gone(player, card, i >= today.Slots.Count);
			Gone(player, $"k4-reroll-{i}", true);
			if (i >= today.Slots.Count)
				continue;

			MissionProgress slot = today.Slots[i];
			MissionDefinition mission = slot.Mission;
			SetClass(player, card, "k4-done", slot.Completed);
			SetClass(player, card, "k4-blocked", !slot.Completed && !missions.IsDoable(mission));
			SwapClass(player, card, "difficulty", $"k4-{mission.Difficulty.ToString().ToLowerInvariant()}");
			SetVar(player, card, "diff", Upper(player, missions.DifficultyLabel(player.Controller, mission)));
			SetVar(player, card, "reward", $"+{Number(missions.RewardOf(mission))}");
			SetVar(player, card, "desc", missions.DescribeHere(player.Controller, slot));
			SetVar(player, card, "progress", slot.Completed ? "✓" : $"{slot.Progress}/{mission.Target}");
			SetPicture(player, $"{card}-icon", MissionIcon(mission));
			SetBar(player, $"{card}-bar", slot.Completed ? 1 : slot.Progress, slot.Completed ? 1 : mission.Target);

			SetVar(player, $"k4-reroll-{i}", "label", Upper(player, Text(player, "hud.reroll")));
			Gone(player, $"k4-reroll-{i}", !canReroll || !slot.Untouched);
		}

		Gone(player, "k4-mw", today.Weekly is null);
		if (today.Weekly is MissionProgress weekly)
		{
			SetClass(player, "k4-mw", "k4-done", weekly.Completed);
			SetVar(player, "k4-mw", "left", Upper(player, missions.DifficultyLabel(player.Controller, weekly.Mission)));
			SetVar(player, "k4-mw", "desc", missions.DescribeHere(player.Controller, weekly));
			SetVar(player, "k4-mw", "progress", weekly.Completed ? "✓" : $"{weekly.Progress}/{weekly.Mission.Target}");
			SetVar(player, "k4-mw", "reward", $"+{Number(missions.RewardOf(weekly.Mission))}");
			SetPicture(player, "k4-mw-icon", MissionIcon(weekly.Mission));
			SetBar(player, "k4-mw-bar", weekly.Completed ? 1 : weekly.Progress, weekly.Completed ? 1 : weekly.Mission.Target);
		}

		// The streak: the seven days of its current week, finished days lit, today marked, each with its bonus.
		int streak = MissionRules.CurrentStreak(today.Streak, today.LastCompletedDay, today.Day);
		bool doneToday = today.LastCompletedDay == today.Day;
		(int firstDay, int lit, int todayIndex) = StreakWindow(streak, doneToday);
		int dayTotal = (int)Math.Min(int.MaxValue, today.Slots.Sum(slot => (long)missions.RewardOf(slot.Mission)));

		SetVar(player, view, "streak", Number(streak));
		SetVar(player, view, "reset", missions.Footer(player));
		SetVar(player, view, "streakhint", Text(player, "hud.streak-hint"));

		for (int i = 0; i < StreakDays; i++)
		{
			int day = firstDay + i;
			int bonus = MissionRules.StreakBonus(day, dayTotal, config.Missions.StreakBonusPercent, config.Missions.StreakMaxDays);
			string slot = $"k4-sd{i}";
			SetClass(player, slot, "k4-on", i < lit);
			SetClass(player, slot, "k4-today", i == todayIndex);
			SetVar(player, slot, "day", Upper(player, Text(player, "hud.streak-day", day)));
			SetVar(player, slot, "bonus", $"+{Number(bonus)}");
			SetVar(player, slot, "tip", Text(player, i < lit ? "hud.tip.streak-done" : i == todayIndex ? "hud.tip.streak-today" : "hud.tip.streak-next", day, Number(bonus)));
		}
	}

	/// <summary>
	/// The streak's current week: the number of its first day, how many of its days are finished, and which one is
	/// today. Today is the day being played for (the streak plus one) until all missions are done, then the streak.
	/// </summary>
	internal static (int FirstDay, int Lit, int TodayIndex) StreakWindow(int streak, bool doneToday)
	{
		int todayNumber = doneToday ? Math.Max(1, streak) : streak + 1;
		int firstDay = (todayNumber - 1) / StreakDays * StreakDays + 1;
		return (firstDay, Math.Clamp(streak - firstDay + 1, 0, StreakDays), todayNumber - firstDay);
	}

	// ---------------------------------------------------------------- achievements

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

	private List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> FilteredAchievements(K4Player player, AchievementFilter filter)
	{
		IReadOnlyList<string> showcase = player.Profile?.Showcase ?? [];
		return [.. AchievementOrder(player).Where(item => filter switch
		{
			AchievementFilter.Progress => !item.Done,
			AchievementFilter.Unlocked => item.Done,
			AchievementFilter.Shown => item.Done && showcase.Contains(item.Achievement.Id),
			_ => true
		})];
	}

	private void DrawAchievements(K4Player player, HudState state)
	{
		const string view = "k4-view-achievements";
		List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> list = FilteredAchievements(player, state.Filter);
		int pages = Math.Max(1, (list.Count + AchievementCards - 1) / AchievementCards);
		state.AchievementsPage = Math.Clamp(state.AchievementsPage, 0, pages - 1);
		var rows = list.Skip(state.AchievementsPage * AchievementCards).Take(AchievementCards).ToList();
		IReadOnlyList<string> showcase = player.Profile?.Showcase ?? [];

		foreach (AchievementFilter filter in Enum.GetValues<AchievementFilter>())
		{
			string name = filter.ToString().ToLowerInvariant();
			string button = $"k4-af-{name}";
			SetVar(player, button, "label", Upper(player, Text(player, $"hud.ach-filter.{name}")));
			SetClass(player, button, "k4-active", state.Filter == filter);
		}

		for (int i = 0; i < AchievementCards; i++)
		{
			string card = $"k4-a{i}";
			if (i >= rows.Count)
			{
				Gone(player, card, true);
				state.AchievementIds[i] = null;
				continue;
			}

			(Achievements.AchievementDefinition achievement, int progress, bool done) = rows[i];
			state.AchievementIds[i] = done && player.Profile is not null ? achievement.Id : null;
			Gone(player, card, false);
			SetClass(player, card, "k4-locked", !done);
			SetClass(player, card, "k4-pinned", done && showcase.Contains(achievement.Id));
			string description = achievements!.DescriptionFor(achievement, player.Controller);
			bool pinned = done && showcase.Contains(achievement.Id);
			SetVar(player, card, "name", achievements.NameFor(achievement, player.Controller));
			SetVar(player, card, "desc", description);
			SetVar(player, card, "goal", done ? $"{Number(achievement.Goal)} ✓" : $"{Number(progress)}/{Number(achievement.Goal)}");
			string status = done
				? Text(player, "hud.tip.unlocked", description, Number(achievement.Points))
				: Text(player, "hud.tip.progress", description, Number(progress), Number(achievement.Goal), Number(achievement.Points));
			SetVar(player, card, "tip", done && player.Profile is not null
				? $"{status} · {Text(player, pinned ? "hud.tip.unpin" : "hud.tip.pin")}"
				: status);
			SwapClass(player, $"{card}-ring", "tier", done ? MedalTier(achievement) : "t-locked");
			SetPicture(player, $"{card}-icon", AchievementIcon(achievement.Type));
			SetBar(player, $"{card}-bar", progress, achievement.Goal);
		}

		int unlocked = player.Achievements is Achievements.PlayerAchievements mine ? achievements?.All.Count(achievement => mine.Unlocked.Contains(achievement.Id)) ?? 0 : 0;
		SetVar(player, view, "count", $"{unlocked} / {achievements?.All.Count ?? 0}");
		SetVar(player, view, "page", Text(player, "hud.page", state.AchievementsPage + 1, pages));
		SetClass(player, "k4-ach-prev", "k4-disabled", state.AchievementsPage == 0);
		SetClass(player, "k4-ach-next", "k4-disabled", state.AchievementsPage + 1 >= pages);
	}

	// ---------------------------------------------------------------- leaderboard

	/// <summary>Entries a leaderboard page needs: the podium, then seven rows per page.</summary>
	private static int LeaderboardNeeded(int page) => 3 + (page + 1) * LeaderboardRows;

	private void DrawLeaderboard(K4Player player, HudState state)
	{
		if (ranks is null)
			return;

		const string view = "k4-view-leaderboard";
		int page = state.LeaderboardPage;
		int needed = LeaderboardNeeded(page);
		CultureInfo language = player.Controller.GetLanguage();

		// The season switch.
		if (seasons?.Current is string current)
		{
			if (finishedSeasons is null)
			{
				Later(player, state, () => store.GetFinishedSeasonsAsync(SeasonsShown), result => finishedSeasons = result,
					_ => DrawLeaderboard(player, state), "Loading the finished seasons");
			}

			int count = finishedSeasons?.Count ?? 0;
			state.Season = Math.Clamp(state.Season, 0, count);
			SetVar(player, view, "season", state.Season == 0 ? SeasonClock.Name(current, language) : SeasonClock.Name(finishedSeasons![state.Season - 1], language));
			SetVar(player, view, "status", Upper(player, state.Season == 0
				? Text(player, "hud.season-live", seasons.TimeLeft(player.Controller, current))
				: Text(player, "hud.season-final")));
			SetClass(player, "k4-lb-newer", "k4-disabled", state.Season == 0);
			SetClass(player, "k4-lb-older", "k4-disabled", state.Season >= count);
		}
		else
		{
			state.Season = 0;
			SetVar(player, view, "season", Upper(player, Text(player, "hud.page.leaderboard")));
			SetVar(player, view, "status", "");
			SetClass(player, "k4-lb-newer", "k4-disabled", true);
			SetClass(player, "k4-lb-older", "k4-disabled", true);
		}

		void Pages(int total)
		{
			int pages = Math.Clamp((Math.Max(0, total - 3) + LeaderboardRows - 1) / LeaderboardRows, 1, LeaderboardPages);
			state.LeaderboardPageCount = pages;
			SetVar(player, view, "page", Text(player, "hud.page", page + 1, pages));
			SetClass(player, "k4-lb-prev", "k4-disabled", page == 0);
			SetClass(player, "k4-lb-next", "k4-disabled", page + 1 >= pages);
		}

		void Draw(List<TopEntry> top)
		{
			for (int i = 0; i < 3; i++)
			{
				int position = i switch { 0 => 2, 1 => 1, _ => 3 };
				string slot = $"k4-lb-p{i}";
				bool has = position <= top.Count;
				Gone(player, slot, !has);
				state.LeaderboardIds[i] = has ? top[position - 1].SteamId : 0;
				if (!has)
					continue;
				TopEntry entry = top[position - 1];
				SetVar(player, slot, "name", entry.Name);
				SetVar(player, slot, "points", Number(entry.Points));
				SetVar(player, slot, "pos", position.ToString(CultureInfo.InvariantCulture));
				SetEmblem(player, $"{slot}-icon", ResolveRank(entry));
			}

			int first = 3 + page * LeaderboardRows;
			for (int i = 0; i < LeaderboardRows; i++)
			{
				string row = $"k4-lb-{i}";
				int index = first + i;
				bool has = index < top.Count;
				Gone(player, row, !has);
				state.LeaderboardIds[3 + i] = has ? top[index].SteamId : 0;
				if (!has)
					continue;
				TopEntry entry = top[index];
				SetVar(player, row, "pos", Number(index + 1));
				SetVar(player, row, "name", entry.Name);
				SetVar(player, row, "points", Number(entry.Points));
				SetEmblem(player, $"{row}-icon", ResolveRank(entry));
			}
		}

		for (int i = 0; i < 3 + LeaderboardRows; i++)
			state.LeaderboardIds[i] = 0;

		// A finished season: its final standings, without the player's own live row.
		if (state.Season > 0 && finishedSeasons is { } finished && state.Season <= finished.Count)
		{
			string season = finished[state.Season - 1];
			Gone(player, "k4-lb-me", true);
			void DrawPast((List<TopEntry> Top, int Players) result)
			{
				Draw(result.Top);
				Pages(result.Players);
			}

			if (seasonTopCache.TryGetValue((season, needed), out (List<TopEntry>, int) past))
				DrawPast(past);
			else
			{
				Later(player, state, () => store.GetSeasonTopAsync(season, needed),
					result => seasonTopCache[(season, needed)] = result, DrawPast, "Loading a past season's leaderboard");
			}
			return;
		}

		ulong steamId = player.SteamId;
		void DrawLive(List<TopEntry> top, (int Placement, int Total) me)
		{
			Draw(top);
			Gone(player, "k4-lb-me", player.Rank is null);
			if (player.Rank is RankState own)
			{
				SetEmblem(player, "k4-lb-me-icon", own.Rank);
				SetVar(player, "k4-lb-me", "pos", Number(me.Placement));
				SetVar(player, "k4-lb-me", "name", Text(player, "hud.you"));
				SetVar(player, "k4-lb-me", "points", Number(own.Points));
			}
			Pages(me.Total);
		}

		if (topCache.TryGetValue(needed, out Cached<List<TopEntry>>? top) && top.Fresh && CachedPlacement(steamId) is { } placed)
		{
			DrawLive(top.Value, placed);
			return;
		}

		Later(player, state, async () =>
		{
			List<TopEntry> entries = await store.GetTopAsync(needed);
			(int placement, int total) = await store.GetPlacementAndTotalAsync(steamId);
			return (entries, (placement, total));
		}, result =>
		{
			topCache[needed] = new Cached<List<TopEntry>>(result.entries, DateTime.UtcNow);
			placementCache[steamId] = new Cached<(int, int)>(result.Item2, DateTime.UtcNow);
		}, result => DrawLive(result.entries, result.Item2), "Loading the leaderboard");
	}

	// ---------------------------------------------------------------- ranks

	private void DrawRanks(K4Player player, HudState state)
	{
		if (ranks is null)
			return;

		const string view = "k4-view-ranks";
		IReadOnlyList<RankDefinition> all = ranks.Ladder.Ranks;
		int pages = Math.Max(1, (all.Count + RankTiles - 1) / RankTiles);
		int page = state.RanksPage = Math.Clamp(state.RanksPage, 0, pages - 1);
		List<RankDefinition> shown = [.. all.Skip(page * RankTiles).Take(RankTiles)];

		SetVar(player, view, "page", Text(player, "hud.page", page + 1, pages));
		Gone(player, "k4-rk-prev", pages == 1);
		Gone(player, "k4-rk-next", pages == 1);
		SetClass(player, "k4-rk-prev", "k4-disabled", page == 0);
		SetClass(player, "k4-rk-next", "k4-disabled", page + 1 >= pages);

		for (int i = 0; i < RankTiles; i++)
		{
			string tile = $"k4-rk{i}";
			Gone(player, tile, i >= shown.Count);
			if (i >= shown.Count)
				continue;

			RankDefinition rank = shown[i];
			bool mine = player.Rank?.Rank.Key == rank.Key;
			SetClass(player, tile, "k4-current", mine);
			SetEmblem(player, $"{tile}-icon", rank);
			SetVar(player, tile, "name", rank.Name);
			SetColor(player, $"{tile}-name", rank.ColorName);
			SetVar(player, tile, "points", rank.IsDefault ? Text(player, "hud.rank-start") : Number(rank.Point));
			IReadOnlyList<string> rewards = RankRewards.DescriptionLines(rank);
			Gone(player, $"{tile}-gift", rewards.Count == 0);
			SetVar(player, tile, "tip", rewards.Count > 0
				? Text(player, "hud.tip.rank-reward", StripColors(string.Join(" · ", rewards)))
				: Text(player, "hud.tip.rank-none"));
			Gone(player, $"{tile}-you", !mine);
		}
	}

	// ---------------------------------------------------------------- stats

	private void DrawStats(K4Player player)
	{
		const string view = "k4-view-stats";
		CultureInfo invariant = CultureInfo.InvariantCulture;
		List<(string Label, string Value, string Icon)> tiles = [];

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
			tiles.Add((Text(player, "hud.stat.mvps"), Number(stats[StatField.Mvp]), "ic-stat_mvps"));
			tiles.Add((Text(player, "hud.stat.accuracy"), $"{(shots > 0 ? stats[StatField.HitsGiven] * 100.0 / shots : 0).ToString("0", invariant)}%", "ic-crosshair_circle"));
			tiles.Add((Text(player, "hud.stat.winrate"), $"{(rounds > 0 ? stats[StatField.RoundWin] * 100.0 / rounds : 0).ToString("0", invariant)}%", "ic-trophy"));
		}

		for (int i = 0; i < StatTiles; i++)
		{
			string tile = $"k4-st{i}";
			Gone(player, tile, i >= tiles.Count);
			if (i >= tiles.Count)
				continue;
			SetVar(player, tile, "label", Upper(player, tiles[i].Label));
			SetVar(player, tile, "value", tiles[i].Value);
			SetPicture(player, $"{tile}-icon", tiles[i].Icon);
		}

		// Kills by weapon: the three with the most kills, bars relative to the first.
		List<KeyValuePair<string, int>> weapons = player.Stats is null || player.Profile is null ? [] : [.. player.Profile.WeaponKills
			.Where(pair => pair.Value > 0)
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key, StringComparer.Ordinal)
			.Take(WeaponBars)];
		for (int i = 0; i < WeaponBars; i++)
		{
			string row = $"k4-sw{i}";
			Gone(player, row, i >= weapons.Count);
			if (i >= weapons.Count)
				continue;
			SetPicture(player, $"{row}-icon", WeaponIcon(weapons[i].Key));
			SetVar(player, row, "weapon", WeaponCatalog.DisplayName(weapons[i].Key));
			SetVar(player, row, "kills", Number(weapons[i].Value));
			SetBar(player, $"{row}-bar", weapons[i].Value, weapons[0].Value);
		}

		// Playtime per team.
		Gone(player, "k4-pt", player.Playtime is null);
		if (player.Playtime is not null)
		{
			int[] seconds = playtime.CurrentSeconds(player);
			int all = seconds[(int)PlaytimeField.All];
			SetVar(player, view, "total", playtime.Format(all, StatTimeUnits));
			foreach ((string id, PlaytimeField field, string key) in new[] { ("k4-pt-t", PlaytimeField.T, "hud.team.t"), ("k4-pt-ct", PlaytimeField.Ct, "hud.team.ct"), ("k4-pt-spec", PlaytimeField.Spec, "hud.team.spec") })
			{
				SetVar(player, id, "name", Upper(player, Text(player, key)));
				SetVar(player, id, "time", playtime.Format(seconds[(int)field], StatTimeUnits));
				SetBar(player, $"{id}-bar", seconds[(int)field], all);
			}
		}
	}

	// ---------------------------------------------------------------- toasts

	/// <summary>Shows a toast (docs/panel-contract.md): newest on top, at most three at once. Players who turned toasts off get nothing.</summary>
	public void Toast(K4Player player, Toast toast)
	{
		if (player.ToastsOff || !Usable(player))
			return;

		if (!toasts.TryGetValue(player.Slot, out List<ShownToast>? stack))
			toasts[player.Slot] = stack = [];

		ShownToast shown = new(toast, NextVersion());
		stack.Insert(0, shown);
		DrawToasts(player, restart: true);

		plugin.AddTimer(global::K4System.Hud.Toast.Seconds(toast.Length), () =>
		{
			if (!gameThread.IsActive || !toasts.TryGetValue(player.Slot, out List<ShownToast>? current) || !current.Contains(shown)
				|| layout is not { IsValid: true } || !player.HasController)
				return;

			// Slide out first, then free the slot.
			shown.Leaving = true;
			DrawToasts(player, restart: false);
			plugin.AddTimer(LeaveSeconds, () =>
			{
				if (!gameThread.IsActive || !toasts.TryGetValue(player.Slot, out List<ShownToast>? now) || !now.Remove(shown))
					return;
				if (layout is { IsValid: true } && player.HasController)
					DrawToasts(player, restart: false);
			}, TimerFlags.STOP_ON_MAPCHANGE);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	/// <summary>The 6.x toast call: a title key, one line of text and a kind.</summary>
	public void ShowToast(K4Player player, string titleKey, string text, ToastKind kind, string? icon = null)
		=> Toast(player, new Toast(Upper(player, Text(player, titleKey)), StripColors(text), "", icon ?? kind switch
		{
			ToastKind.Gold => "ic-mission_star",
			ToastKind.Warning => "ic-cold_trend",
			_ => "ic-check"
		}, kind switch
		{
			ToastKind.Gold => ToastColor.Gold,
			ToastKind.Warning => ToastColor.Red,
			_ => ToastColor.Green
		}));

	private void DrawToasts(K4Player player, bool restart)
	{
		List<ShownToast> stack = toasts.GetValueOrDefault(player.Slot) ?? [];
		for (int i = 0; i < global::K4System.Hud.Toast.Slots; i++)
		{
			string slot = $"k4-t{i}";
			if (i >= stack.Count)
			{
				Gone(player, slot, true);
				SetClass(player, slot, "k4-out", false);
				continue;
			}

			// The class follows the toast, so a toast leaving while a new one pushes it down keeps sliding out.
			SetClass(player, slot, "k4-out", stack[i].Leaving);
			Toast toast = stack[i].Toast;
			SetVar(player, slot, "kind", toast.Kind);
			SetVar(player, slot, "text", StripColors(toast.Text));
			SetVar(player, slot, "value", toast.Value);
			SwapClass(player, slot, "toast", $"k4-tc-{toast.Color.ToString().ToLowerInvariant()}");
			SwapClass(player, slot, "length", $"k4-len-{toast.Length.ToString().ToLowerInvariant()}");
			SetPicture(player, $"{slot}-icon", toast.Visual);
			Gone(player, $"{slot}-ring", toast.Tier is null);
			if (toast.Tier is string tier)
				SwapClass(player, $"{slot}-ring", "tier", $"t-{tier}");
			Gone(player, $"{slot}-prog", toast.BarPercent is null);
			if (toast.BarPercent is int percent)
				SetBar(player, $"{slot}-bar", percent, 100);
			Gone(player, slot, false);

			// shortcut: the timer bar restarts for the newest toast only; older ones keep their bar where it was.
			if (restart && i == 0)
			{
				string timer = $"{slot}-timer";
				SetClass(player, timer, "k4-run", false);
				CCSPlayerController controller = player.Controller;
				Server.NextFrame(() =>
				{
					if (layout is { IsValid: true } && controller.IsValid)
						layout.SetHasClassForPlayer(controller, timer, "k4-run", true);
				});
			}
		}

		int more = Math.Max(0, stack.Count - global::K4System.Hud.Toast.Slots);
		SetVar(player, "k4-t-more", "more", $"+{more}");
		Gone(player, "k4-t-more", more == 0);
	}

	/// <summary>The large promotion banner at the top of the screen.</summary>
	public void Promote(K4Player player, RankDefinition rank)
	{
		if (player.ToastsOff || !Usable(player))
			return;

		int version = promotions[player.Slot] = NextVersion();
		SetClass(player, "k4-promo", "k4-out", false);
		SetVar(player, "k4-promo", "title", Upper(player, Text(player, "hud.toast.promoted")));
		SetVar(player, "k4-promo", "rank", rank.Name);
		SetEmblem(player, "k4-promo-icon", rank);
		Gone(player, "k4-promo", false);

		plugin.AddTimer(PromotionSeconds, () =>
		{
			if (!gameThread.IsActive || promotions.GetValueOrDefault(player.Slot) != version || layout is not { IsValid: true } || !player.HasController)
				return;

			layout.SetHasClassForPlayer(player.Controller, "k4-promo", "k4-out", true);
			plugin.AddTimer(LeaveSeconds, () =>
			{
				if (gameThread.IsActive && promotions.GetValueOrDefault(player.Slot) == version && layout is { IsValid: true } && player.HasController)
				{
					layout.SetHasClassForPlayer(player.Controller, "k4-promo", "k4-gone", true);
					layout.SetHasClassForPlayer(player.Controller, "k4-promo", "k4-out", false);
				}
			}, TimerFlags.STOP_ON_MAPCHANGE);
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
					for (int i = 0; i < global::K4System.Hud.Toast.Slots; i++)
						layout.SetHasClassForPlayer(controller, $"k4-t{i}", "k4-gone", true);
					layout.SetHasClassForPlayer(controller, "k4-promo", "k4-gone", true);
				}
			}

			layout.Remove();
		}

		layout = null;
		open.Clear();
		toasts.Clear();
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
