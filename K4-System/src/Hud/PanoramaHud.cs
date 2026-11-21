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

/// <summary>
/// The screens of the panel, in tab order, then the gear's Settings. 8.0 merged Stats into Profile and Ranks into the
/// Leaderboard; Ranks stays as a name for !ranks, which opens the Leaderboard on its rank list.
/// </summary>
public enum HudTab
{
	Home,
	Missions,
	Achievements,
	Leaderboard,
	Duels,
	Profile,
	Settings,

	/// <summary>The Leaderboard on its rank list (!ranks).</summary>
	Ranks
}

/// <summary>The 6.x toast kinds; new code sends a <see cref="Toast"/> instead.</summary>
public enum ToastKind
{
	Success,
	Gold,
	Warning
}

/// <summary>What a "do this next" card on Home opens, in the order they are offered.</summary>
internal enum NextKind
{
	Duel,
	Swap,
	Showcase,
	Achievement,
	Season
}

/// <summary>
/// The K4 Panorama panel and toasts. The layout ships to players in the K4 workshop addon
/// (panorama/layout/custom_game/k4_ui.xml); the server only sets text and style classes per player
/// and receives button clicks. Every id and class used here is listed in docs/panel-contract.md. Game thread only.
/// </summary>
internal sealed class PanoramaHud
{
	public const string LayoutPath = "panorama/layout/custom_game/k4_ui.xml";

	/// <summary>The panel contract this server fills; the server sets k4-c{N} on k4-panel and the layout hides its out-of-date bar under it.</summary>
	public const int ContractVersion = 8;

	public const string IconsCompetitive = "competitive";
	public const string IconsWingman = "wingman";
	public const string IconsText = "text";

	private const string Panel = "k4-panel";

	// Row pools of the layout (docs/panel-contract.md, "Pools"); PanoramaLayoutTests checks the layout against these.
	public const int LeaderboardRows = 100;
	public const int RankRows = 40;
	public const int AchievementRows = 64;
	public const int DuelPlayerRows = 64;
	public const int SeasonRows = 24;
	public const int LiveDuelRows = 4;

	// Fixed cards of the layout.
	internal const int MissionCards = 3;
	internal const int ProfileTiles = 8;
	internal const int WeaponBars = 3;
	internal const int StreakDays = 7;
	internal const int NextCards = 3;
	// The Home podium (owner 2026-11-21, was a top 5 list).
	internal const int HomeTop = 3;
	// The big numbers under Home: K/D, headshots, accuracy, playtime and kills.
	internal const int HomeStats = 5;
	private const int DuelTypeCards = 6;
	private const int DuelStakeButtons = 5;


	// "2d, 23h, 59m, 48s" does not fit a tile; two units do.
	private const int StatTimeUnits = 2;

	private const float PromotionSeconds = 4f;

	// CS2 can kick a player when a chat command and a HUD update share a frame, so drawing waits a
	// moment, and mouse capture is switched on a moment after the panel is drawn.
	private const float CommandDelaySeconds = 0.2f;
	private const float CaptureDelaySeconds = 0.2f;

	/// <summary>The server cannot see scrolling, so a panel closes itself only after this long without a click.</summary>
	internal const float IdleCloseSeconds = 120f;

	// Database-backed views are cached briefly, so spamming commands or buttons cannot flood the database.
	private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

	/// <summary>The screen ids (k4-view-*, k4-nav-*), in <see cref="HudTab"/> order.</summary>
	internal static readonly string[] PageIds = ["home", "missions", "achievements", "leaderboard", "duels", "profile", "settings"];

	/// <summary>The screens with a top tab; Settings has the gear.</summary>
	private const int TopTabs = 6;

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

	// The Home "do this next" pictures; the duel card shows its duel type.
	private static readonly string[] NextIcons = ["ic-trade_up", "ic-winner_ribbon", "ic-clock"];

	private enum AchievementFilter
	{
		All,
		Progress,
		Unlocked,
		Shown
	}

	/// <summary>Where the player is: a screen and what it shows. Back returns to an earlier one.</summary>
	private sealed record View(HudTab Tab, bool RankList, ulong ProfileOf, string ProfileName);

	private sealed class HudState
	{
		public HudTab Tab { get; set; }
		public int Version { get; set; }

		/// <summary>Drawn at least once since it opened, so the toasts already moved under the window.</summary>
		public bool Drawn { get; set; }

		/// <summary>The last open or click; the panel closes itself <see cref="IdleCloseSeconds"/> after it.</summary>
		public DateTime ActiveUtc { get; set; } = DateTime.UtcNow;

		public BackHistory<View> History { get; } = new();

		/// <summary>The Leaderboard shows the rank list instead of the players.</summary>
		public bool RankList { get; set; }

		/// <summary>0 shows the current season; 1 and up the finished seasons, newest first.</summary>
		public int Season { get; set; }

		public AchievementFilter Filter { get; set; }

		/// <summary>Whose profile the Profile screen shows; 0 for the player's own.</summary>
		public ulong ProfileOf { get; set; }

		/// <summary>That player's name for the crumbs, known from the click or once the profile loads.</summary>
		public string ProfileName { get; set; } = "";

		/// <summary>The daily mission whose card asks "swap?", or null.</summary>
		public int? SwapAsk { get; set; }

		/// <summary>The result of the last click, shown in the footer until the player moves on.</summary>
		public (string Text, bool Ok)? Status { get; set; }

		// What each pooled row and card means, so a click finds it again.
		public ulong[] LeaderboardIds { get; } = new ulong[LeaderboardRows];
		public bool[] LeaderboardDuel { get; } = new bool[LeaderboardRows];
		public string?[] AchievementIds { get; } = new string?[AchievementRows];
		public string?[] ShowcaseIds { get; } = new string?[PlayerProfile.ShowcaseSize];
		public NextKind?[] Next { get; } = new NextKind?[NextCards];

		// The challenge being built on the Duels screen: the picked player, type and stake.
		public ulong DuelTarget { get; set; }
		public DuelType? DuelType { get; set; }
		public int? DuelStake { get; set; }
		public ulong[] DuelPlayerIds { get; } = new ulong[DuelPlayerRows];
		public DuelType?[] DuelTypeIds { get; } = new DuelType?[DuelTypeCards];
		public int?[] DuelStakeValues { get; } = new int?[DuelStakeButtons];
	}

	// Versions come from one counter that never restarts, so a timer or database draw from an earlier
	// opening, an earlier map, or the slot's previous player can never match the current panel.
	private int lastVersion;

	private int NextVersion() => ++lastVersion;

	private sealed record Cached<T>(T Value, DateTime LoadedUtc)
	{
		public bool Fresh => DateTime.UtcNow - LoadedUtc < CacheFor;
	}

	// Matches the slide-out transition of .k4-toast.k4-out and .k4-promo.k4-out in the stylesheet.
	private const float LeaveSeconds = 0.3f;

	private readonly BasePlugin plugin;
	private readonly PluginConfig config;
	private readonly PlayerRegistry players;
	private readonly RankService? ranks;
	private readonly MissionService? missions;
	private readonly DuelService? duels;
	private readonly Achievements.AchievementService? achievements;
	private readonly EventService? events;
	private readonly SeasonService? seasons;
	private readonly PlaytimeService playtime;
	private readonly SqlPlayerStore store;
	private readonly PlayerService settings;
	private readonly GameThread gameThread;
	private readonly BackgroundWork background;
	private readonly Chat chat;
	private readonly ILogger logger;

	private readonly Dictionary<int, HudState> open = [];

	// Players told once per connection how to fix a menu that looks empty (a 7.x addon on an 8.0 server: the server
	// cannot see which addon a player has, and a 7.x layout has no out-of-date bar to show).
	private readonly HashSet<ulong> hinted = [];

	private readonly Dictionary<int, List<ToastEntry>> toasts = [];
	private readonly Dictionary<int, int> promotions = [];

	private readonly HudWrites sent;

	private readonly Dictionary<int, Cached<List<TopEntry>>> topCache = [];
	private readonly Dictionary<ulong, Cached<(int Placement, int Total)>> placementCache = [];
	private readonly Dictionary<ulong, Cached<ProfileRecord?>> profileCache = [];

	// Finished seasons never change, so these stay until the next season ends.
	private List<string>? finishedSeasons;
	private readonly Dictionary<string, (List<TopEntry> Top, int Players)> seasonTopCache = [];

	// The one shared custom_hud_layout entity. shortcut: every client receives every player's panel state; if the
	// in-game network test fails, swap this for one entity per player with a CheckTransmit filter (only EnsureLayout,
	// the write delegates and Describe know about the entity).
	private CCSCustomHudLayout? layout;

	public PanoramaHud(
		BasePlugin plugin,
		PluginConfig config,
		PlayerRegistry players,
		RankService? ranks,
		MissionService? missions,
		DuelService? duels,
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
		this.plugin = plugin;
		this.config = config;
		this.players = players;
		this.ranks = ranks;
		this.missions = missions;
		this.duels = duels;
		this.achievements = achievements;
		this.events = events;
		this.seasons = seasons;
		this.playtime = playtime;
		this.store = store;
		this.settings = settings;
		this.gameThread = gameThread;
		this.background = background;
		this.chat = chat;
		this.logger = logger;
		sent = new HudWrites(
			(controller, panel, name, value) => Timed(() => layout!.SetDialogVariableStringForPlayer(controller!, panel, name, value)),
			(controller, panel, name, on) => Timed(() => layout!.SetHasClassForPlayer(controller!, panel, name, on)));
	}

	public bool IsOpen(K4Player player) => open.ContainsKey(player.Slot);

	private string IconSet => config.Panorama.RankIcons?.Trim().ToLowerInvariant() ?? IconsCompetitive;

	// ---------------------------------------------------------------- layout entity

	/// <summary>Creates the layout entity early, so clients have loaded it before the first !k4.</summary>
	public void Warmup()
	{
		gameThread.Post(() => EnsureLayout());
		plugin.AddTimer(1f, CloseIdlePanels, TimerFlags.REPEAT);

		// The duel bar and the Duels screen show countdowns, so their timers move every second while a duel or a
		// challenge is live. Only the timers are redrawn; anything else changes through a full redraw.
		if (duels is not null)
			plugin.AddTimer(1f, () =>
			{
				if (!duels.Live || layout is not { IsValid: true })
					return;
				foreach ((int slot, HudState state) in open.ToArray())
				{
					if (players.All.FirstOrDefault(player => player.Slot == slot) is K4Player player && Usable(player))
						DrawTimers(player, state);
				}
			}, TimerFlags.REPEAT);
	}

	/// <summary>Entities are destroyed on map change.</summary>
	public void OnMapStart()
	{
		layout = null;
		open.Clear();
		toasts.Clear();
		promotions.Clear();
		sent.Forget();
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
			// A different entity holds a different state, so nothing sent to the old one counts.
			sent.Forget();
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

	/// <summary>
	/// A disconnect without a known player. The slot's remembered state stays: <see cref="HudWrites.Claim"/> puts it
	/// back to the layout's defaults when the next player draws there. Only per-player panel state of slots that have
	/// no player any more is dropped.
	/// </summary>
	public void ForgetAllSent()
	{
		HashSet<int> taken = players.ValidHumans.Select(player => player.Slot).ToHashSet();
		foreach (int slot in open.Keys.Concat(toasts.Keys).Concat(promotions.Keys).Where(slot => !taken.Contains(slot)).ToList())
		{
			open.Remove(slot);
			toasts.Remove(slot);
			promotions.Remove(slot);
		}
	}

	private HudPen Pen(K4Player player)
	{
		sent.Claim(player.Slot, player.SteamId, player.Controller);
		return new(sent, player.Slot, player.Controller);
	}

	private static HudPen Pen(HudWrites writes, CCSPlayerController controller) => new(writes, controller.Slot, controller);

	/// <summary>Diagnostics for css_k4_hud: the layout entities and each player's panel state.</summary>
	public IEnumerable<string> Describe()
	{
		yield return $"Layout field: {(layout is null ? "none" : $"#{layout.Index} valid={layout.IsValid} path={(layout.IsValid ? layout.StrLayout : "?")}")}";
		yield return $"Panel contract {ContractVersion}; writes since load: {sent.Sent} sent, {sent.Skipped} skipped as unchanged";
		foreach ((HudTab tab, (int draws, long ticks, long native, long max, long writes, long maxWrites)) in drawTimes)
			yield return FormattableString.Invariant($"Draw {tab}: {draws}x, average {Ms(ticks) / draws:0.00} ms (layout writes {Ms(native) / draws:0.00} ms), slowest {Ms(max):0.00} ms, writes average {writes / (double)draws:0.0}, most {maxWrites}");
		foreach (CCSCustomHudLayout entity in Utilities.FindAllEntitiesByDesignerName<CCSCustomHudLayout>("custom_hud_layout"))
			yield return $"custom_hud_layout #{entity.Index} valid={entity.IsValid} path='{entity.StrLayout}' observable={entity.Observable} playerStates={entity.PlayerLayoutStates.Count}";
		foreach (K4Player player in players.ValidHumans)
		{
			bool capture = layout is { IsValid: true } && layout.IsInputCaptureEnabled(player.Controller);
			string tab = open.TryGetValue(player.Slot, out HudState? state) ? $"{state.Tab} v{state.Version} back {state.History.Count}" : "closed";
			List<ToastEntry>? stack = toasts.GetValueOrDefault(player.Slot);
			yield return $"{player.Name} slot {player.Slot}: panel {tab}, mouse capture {capture}, toasts {stack?.Count(entry => entry.Showing) ?? 0} shown, {stack?.Count(entry => !entry.Showing) ?? 0} waiting";
		}
	}

	// ---------------------------------------------------------------- open, close, clicks

	/// <summary>
	/// Opens the panel on a screen. Returns false when the panel cannot be shown, so the command can
	/// fall back to chat. Called from chat commands, so drawing waits a short moment.
	/// </summary>
	public bool Open(K4Player player, HudTab tab)
	{
		if (!Usable(player))
			return false;

		logger.LogDebug("K4 panel: opening {Tab} for {Player} (layout #{Index}).", tab, player.Name, layout!.Index);
		HudState state = open.TryGetValue(player.Slot, out HudState? existing) ? existing : new HudState();
		HudTab screen = Screen(tab);
		Apply(state, new View(IsScreenAvailable(screen) ? screen : HudTab.Home, tab == HudTab.Ranks, 0, ""));
		state.History.Clear();
		state.ActiveUtc = DateTime.UtcNow;
		open[player.Slot] = state;
		ShowLater(player, state, enableCapture: true);
		return true;
	}

	/// <summary>Opens the Profile screen on a player's profile, the caller's own for their own Steam id.</summary>
	public bool OpenProfile(K4Player player, ulong steamId)
	{
		if (!config.Modules.Profiles || !Open(player, HudTab.Profile))
			return false;

		HudState state = open[player.Slot];
		state.ProfileOf = steamId == player.SteamId ? 0 : steamId;
		state.ProfileName = state.ProfileOf == 0 ? "" : players.Get(steamId)?.Name ?? "";
		return true;
	}

	/// <summary>The screen a tab name opens: Ranks is the Leaderboard on its rank list.</summary>
	internal static HudTab Screen(HudTab tab) => tab == HudTab.Ranks ? HudTab.Leaderboard : tab;

	/// <summary>Redraws the panel if it is open on this screen, for changes made outside the panel.</summary>
	public void Refresh(K4Player player, HudTab tab)
	{
		if (open.TryGetValue(player.Slot, out HudState? state) && state.Tab == Screen(tab))
			ShowLater(player, state, enableCapture: false);
	}

	/// <summary>Redraws every open panel on its current screen, for changes that concern everyone, such as a points event or a duel.</summary>
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
			if (!gameThread.IsActive || !open.TryGetValue(player.Slot, out HudState? current) || current.Version != version)
				return;
			if (Usable(player))
			{
				Show(player, current, enableCapture);
				HintOutdatedOnce(player);
			}
			else
				// No window came up, so it must not count as open: toasts would wait under an invisible window and
				// click results would never reach chat.
				open.Remove(player.Slot);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	/// <summary>The first time a player's menu opens on this connection, one chat line on what to do if it looks empty.</summary>
	private void HintOutdatedOnce(K4Player player)
	{
		if (hinted.Add(player.SteamId))
			chat.Tell(player.Controller, "hud.outdated-hint");
	}

	public void Close(K4Player player)
	{
		open.Remove(player.Slot);

		if (layout is not { IsValid: true } || !player.HasController)
			return;

		Hide(player.Controller);
		// Toasts move back under the kill feed, and up to three show again, waiting ones first in line.
		DrawToasts(player);
	}

	private void Hide(CCSPlayerController controller)
	{
		Pen(sent, controller).Flag(Panel, "k4-closed", true);
		layout!.SetInputCaptureEnabled(controller, false);
	}

	/// <summary>
	/// Puts the leaving player's panel, rows, toasts and mouse capture back to the layout's defaults. The layout keeps
	/// these per slot, so without this the next player in the slot could start with a captured mouse or old rows.
	/// </summary>
	public void OnDisconnect(CCSPlayerController controller)
	{
		if (layout is { IsValid: true } && controller.IsValid)
		{
			Hide(controller);
			sent.Reset(controller.Slot, controller);
		}
		else
			sent.Forget(controller.Slot);

		open.Remove(controller.Slot);
		toasts.Remove(controller.Slot);
		promotions.Remove(controller.Slot);
		if (controller.IsValid)
			hinted.Remove(controller.SteamID);
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

		// The footer shows the result of the last click only, so any click clears it.
		state.ActiveUtc = DateTime.UtcNow;
		bool hadStatus = state.Status is not null;
		state.Status = null;
		if ((!HandleClick(player, state, buttonId) && !hadStatus) || !open.ContainsKey(player.Slot))
			return;

		state.Version = NextVersion();
		Show(player, state, enableCapture: false);
	}

	/// <summary>The result of a click in the footer while the panel is open; in chat for a player whose panel just closed.</summary>
	private void Say(K4Player player, HudState state, bool ok, string key, params object[] args)
	{
		if (open.ContainsKey(player.Slot))
			state.Status = (StripColors(Text(player, key, args)), ok);
		else
			chat.Tell(player.Controller, key, args);
	}

	/// <summary>Applies a click to the panel state. False when nothing changed and no redraw is needed.</summary>
	private bool HandleClick(K4Player player, HudState state, string id)
	{
		switch (id)
		{
			case "k4-close":
				Close(player);
				return false;
			case "k4-back":
				return Back(state);
			case "k4-nav-settings":
				return Go(state, HudTab.Settings, push: true);
			case "k4-duel-bar-accept" when duels is not null:
				Answer(player, state, duels.Accept(player), "toast.duel-started");
				return true;
			case "k4-duel-bar-decline" when duels is not null:
				Answer(player, state, duels.Decline(player), "hud.st.declined");
				return true;

			// Home
			case "k4-h-rank":
				return Go(state, HudTab.Leaderboard, push: true, rankList: true);
			case "k4-h-top":
				return Go(state, HudTab.Leaderboard, push: true);

			// Achievements
			case "k4-a-show-unlocked":
				state.Filter = AchievementFilter.Unlocked;
				return true;

			// Leaderboard
			case "k4-lb-tab-players":
				return SwitchList(state, rankList: false);
			case "k4-lb-tab-ranks":
				return SwitchList(state, rankList: true);
			case "k4-lb-newer" when state.Season > 0:
				state.Season--;
				return true;
			case "k4-lb-older" when finishedSeasons is not null && state.Season < finishedSeasons.Count:
				state.Season++;
				return true;

			// Profile
			case "k4-pf-challenge" when state.ProfileOf != 0:
				return Challenge(player, state, state.ProfileOf);
		}

		if (id.StartsWith("k4-nav-", StringComparison.Ordinal) && Array.IndexOf(PageIds, id["k4-nav-".Length..]) is int page and >= 0 and < TopTabs)
			return Go(state, (HudTab)page, push: false);

		if (Row(id, "k4-h-m", "", MissionCards + 1) is not null)
			return Go(state, HudTab.Missions, push: true);

		if (Row(id, "k4-h-n", "", NextCards) is int card && state.Next[card] is NextKind next)
			return OpenNext(state, next);

		if (missions is not null && HandleMissionClick(player, state, id) is bool missionChanged)
			return missionChanged;

		if (HandleAchievementClick(player, state, id) is bool achievementChanged)
			return achievementChanged;

		if (Row(id, "k4-lb-r", "-profile", LeaderboardRows) is int profileRow && config.Modules.Profiles && state.LeaderboardIds[profileRow] is ulong target and not 0)
		{
			string name = topCache.GetValueOrDefault(LeaderboardRows)?.Value.Find(entry => entry.SteamId == target)?.Name ?? players.Get(target)?.Name ?? "";
			return Go(state, HudTab.Profile, push: true, profileOf: target == player.SteamId ? 0 : target, profileName: name);
		}

		if (Row(id, "k4-lb-r", "-duel", LeaderboardRows) is int duelRow && state.LeaderboardDuel[duelRow])
			return Challenge(player, state, state.LeaderboardIds[duelRow]);

		if (id.StartsWith("k4-pf-sc", StringComparison.Ordinal) && state.ProfileOf == 0 && Go(state, HudTab.Achievements, push: true))
		{
			state.Filter = AchievementFilter.Shown;
			return true;
		}

		if (duels is not null && HandleDuelClick(player, state, id) is bool duelChanged)
			return duelChanged;

		return HandleSettingsClick(player, state, id);
	}

	/// <summary>
	/// The row index in a pooled id such as "k4-lb-r12-profile" (prefix "k4-lb-r", suffix "-profile"), or null for any
	/// other id, a row past the pool, or a number with a leading zero. One parser for every pool, so a click always
	/// maps back to the row the server filled.
	/// </summary>
	internal static int? Row(string id, string prefix, string suffix, int pool)
	{
		if (id.Length <= prefix.Length + suffix.Length || !id.StartsWith(prefix, StringComparison.Ordinal) || !id.EndsWith(suffix, StringComparison.Ordinal))
			return null;

		ReadOnlySpan<char> digits = id.AsSpan(prefix.Length, id.Length - prefix.Length - suffix.Length);
		if (digits.Length > 4 || (digits.Length > 1 && digits[0] == '0'))
			return null;
		foreach (char digit in digits)
		{
			if (!char.IsAsciiDigit(digit))
				return null;
		}

		int row = int.Parse(digits, CultureInfo.InvariantCulture);
		return row < pool ? row : null;
	}

	private View Current(HudState state) => new(state.Tab, state.RankList, state.ProfileOf, state.ProfileName);

	private static void Apply(HudState state, View view)
	{
		state.Tab = view.Tab;
		state.RankList = view.RankList;
		state.ProfileOf = view.ProfileOf;
		state.ProfileName = view.ProfileName;
		state.SwapAsk = null;
		state.Status = null;
	}

	/// <summary>Moves to a screen. A top tab starts over; anything else remembers where the player came from for Back.</summary>
	private bool Go(HudState state, HudTab tab, bool push, bool rankList = false, ulong profileOf = 0, string profileName = "")
	{
		if (!IsScreenAvailable(tab))
			return false;

		View next = new(tab, rankList, profileOf, profileName);
		View current = Current(state);
		if (next == current)
			return false;

		if (push)
			state.History.Push(current);
		else
			state.History.Clear();
		Apply(state, next);
		return true;
	}

	private bool Back(HudState state)
	{
		while (state.History.TryPop(out View? view))
		{
			if (!IsScreenAvailable(view.Tab))
				continue;
			Apply(state, view);
			return true;
		}
		return false;
	}

	private static bool SwitchList(HudState state, bool rankList)
	{
		if (state.RankList == rankList)
			return false;
		state.RankList = rankList;
		return true;
	}

	private bool OpenNext(HudState state, NextKind next)
	{
		switch (next)
		{
			case NextKind.Duel:
				return Go(state, HudTab.Duels, push: true);
			case NextKind.Swap:
				return Go(state, HudTab.Missions, push: true);
			case NextKind.Showcase when Go(state, HudTab.Achievements, push: true):
				state.Filter = AchievementFilter.Unlocked;
				return true;
			case NextKind.Achievement when Go(state, HudTab.Achievements, push: true):
				state.Filter = AchievementFilter.Progress;
				return true;
			case NextKind.Season:
				return Go(state, HudTab.Leaderboard, push: true);
			default:
				return false;
		}
	}

	/// <summary>Opens Duels with this player picked, from a leaderboard row or a profile.</summary>
	private bool Challenge(K4Player player, HudState state, ulong target)
	{
		if (duels is null || target == 0 || target == player.SteamId)
			return false;
		if (players.Get(target) is not { IsHuman: true, IsValid: true })
		{
			Say(player, state, false, "hud.st.left");
			return true;
		}
		if (!Go(state, HudTab.Duels, push: true))
			return false;
		state.DuelTarget = target;
		state.DuelStake = null;
		return true;
	}

	private void Answer(K4Player player, HudState state, DuelService.Refusal? refusal, string doneKey)
	{
		if (refusal is DuelService.Refusal reason)
			Say(player, state, false, reason.Key, reason.Args);
		else
			Say(player, state, true, doneKey);
	}

	// ---------------------------------------------------------------- drawing

	private void Show(K4Player player, HudState state, bool enableCapture)
	{
		logger.LogDebug("K4 panel: drawing {Tab} for {Player} (capture {Capture}).", state.Tab, player.Name, enableCapture);
		long drawStarted = System.Diagnostics.Stopwatch.GetTimestamp();
		long nativeBefore = nativeTicks;
		long writesBefore = sent.Sent;
		try
		{
			DrawScreen(player, state, enableCapture);
		}
		finally
		{
			RecordDraw(state.Tab, System.Diagnostics.Stopwatch.GetTimestamp() - drawStarted, nativeTicks - nativeBefore, sent.Sent - writesBefore);
		}
	}

	// Draw timings for css_k4_hud: how long panel draws take on the game thread, how much of that the layout writes are, and how many writes go out.
	private long nativeTicks;
	private readonly Dictionary<HudTab, (int Draws, long Ticks, long NativeTicks, long MaxTicks, long Writes, long MaxWrites)> drawTimes = [];

	private void RecordDraw(HudTab tab, long ticks, long native, long writes)
	{
		(int draws, long total, long nativeTotal, long max, long writesTotal, long maxWrites) = drawTimes.GetValueOrDefault(tab);
		drawTimes[tab] = (draws + 1, total + ticks, nativeTotal + native, Math.Max(max, ticks), writesTotal + writes, Math.Max(maxWrites, writes));
	}

	private void Timed(Action write)
	{
		long started = System.Diagnostics.Stopwatch.GetTimestamp();
		write();
		nativeTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
	}

	private static double Ms(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

	/// <summary>The frame and the open screen only: other screens fill when they are shown.</summary>
	private void DrawScreen(K4Player player, HudState state, bool enableCapture)
	{
		HudPen pen = Pen(player);
		bool opening = !state.Drawn;
		state.Drawn = true;
		DrawFrame(player, state);

		switch (state.Tab)
		{
			case HudTab.Missions:
				DrawMissions(player, state);
				break;
			case HudTab.Achievements:
				DrawAchievements(player, state);
				break;
			case HudTab.Leaderboard:
				DrawLeaderboard(player, state);
				break;
			case HudTab.Duels:
				DrawDuels(player, state);
				break;
			case HudTab.Profile:
				DrawProfile(player, state);
				break;
			case HudTab.Settings:
				DrawSettings(player);
				break;
			default:
				DrawHome(player, state);
				break;
		}

		pen.Flag(Panel, "k4-closed", false);
		if (opening)
			DrawToasts(player);

		if (enableCapture)
		{
			plugin.AddTimer(CaptureDelaySeconds, () =>
			{
				if (gameThread.IsActive && layout is { IsValid: true } && player.HasController && open.TryGetValue(player.Slot, out HudState? current) && ReferenceEquals(current, state))
					layout.SetInputCaptureEnabled(player.Controller, true);
			}, TimerFlags.STOP_ON_MAPCHANGE);
		}
	}

	private void CloseIdlePanels()
	{
		DateTime now = DateTime.UtcNow;
		foreach ((int slot, HudState state) in open.ToArray())
		{
			if ((now - state.ActiveUtc).TotalSeconds >= IdleCloseSeconds && players.All.FirstOrDefault(player => player.Slot == slot) is K4Player player)
				Close(player);
		}
	}

	private bool IsScreenAvailable(HudTab tab) => Screen(tab) switch
	{
		HudTab.Leaderboard => ranks is not null,
		HudTab.Duels => duels is not null,
		HudTab.Missions => missions is not null,
		HudTab.Achievements => achievements is not null,
		HudTab.Profile => config.Modules.Profiles || config.Modules.Stats || config.Modules.Playtime,
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

	private void SetColor(HudPen pen, string panel, string colorName)
		=> pen.Swap(panel, "color", $"col-{(ColorNames.Contains(colorName) ? colorName : "default")}");

	/// <summary>CS2's own rank icons, numbered like the scoreboard: the default rank is 0, then 1 to 18.</summary>
	private void SetEmblem(HudPen pen, string panel, RankDefinition rank) => pen.Swap(panel, "icon", Emblem(rank));

	private string Emblem(RankDefinition rank) => RankIcon(rank) ?? $"sg-{RankNumber(rank)}";

	private static void SetPicture(HudPen pen, string panel, string? picture) => pen.Swap(panel, "picture", picture);

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
		foreach (string icon in NextIcons)
			yield return icon;
		foreach (string icon in new[] { "ic-bullet", "ic-check", "ic-hot_trend", "ic-cold_trend", "ic-trade_up", "ic-mission_star", "ic-trophy", "ic-watch", "tp-t", "tp-ct" })
			yield return icon;
		foreach (Achievements.AchievementType type in Enum.GetValues<Achievements.AchievementType>())
			yield return AchievementIcon(type);
	}

	/// <summary>The medal ring class of an achievement: harder ones (more points) get a better metal.</summary>
	internal static string MedalTier(Achievements.AchievementDefinition achievement) => $"t-{Achievements.AchievementRules.Tier(achievement)}";

	private string Text(K4Player player, string key, params object[] args) => chat.For(player.Controller, key, args);

	private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

	/// <summary>Reward descriptions and chat lines may contain chat color codes; the panel shows plain text.</summary>
	private static string StripColors(string text) => new(text.Where(character => character >= ' ').ToArray());

	/// <summary>The numbers of "#12 of 340", or null while the position is unknown. A total below the placement (a stale count) shows the placement.</summary>
	internal static (string Placement, string Total)? Place(int placement, int total)
		=> placement <= 0 ? null : (Number(placement), Number(Math.Max(placement, total)));

	private string PlaceText(K4Player player, int placement, int total)
		=> Place(placement, total) is (string place, string of) ? Text(player, "hud.position", place, of) : "";

	/// <summary>The "12 more not shown" line of a list longer than its pool, or empty.</summary>
	internal static int NotShown(int count, int pool) => Math.Max(0, count - pool);

	private (int Placement, int Total)? CachedPlacement(ulong steamId)
		=> placementCache.TryGetValue(steamId, out Cached<(int, int)>? cached) && cached.Fresh ? cached.Value : null;

	private void LoadPlacement(K4Player player, HudState state, Action<(int Placement, int Total)> draw, string description)
	{
		ulong steamId = player.SteamId;
		Later(player, state, () => store.GetPlacementAndTotalAsync(steamId),
			result => placementCache[steamId] = new Cached<(int, int)>(result, DateTime.UtcNow), draw, description);
	}

	// ---------------------------------------------------------------- frame

	/// <summary>Everything the frame shows, already in the reader's language, so <see cref="PaintFrame"/> is tested without the game.</summary>
	internal sealed record FrameView(
		HudTab Tab,
		bool[] Available,
		string[] Labels,
		int DuelsWaiting,
		string? Event,
		string Crumb,
		string? Back,
		SeasonClockView? Season,
		DuelBarView? DuelBar,
		(string Text, bool Ok)? Status,
		string Keys);

	internal sealed record SeasonClockView(string Title, string Left, long Done, long Total);

	internal sealed record DuelBarView(string Title, string Text, string Left, string Icon, int SecondsLeft, int Seconds, string Accept, string Decline);

	private void DrawFrame(K4Player player, HudState state)
	{
		HudPen pen = Pen(player);
		pen.Flag(Panel, $"k4-c{ContractVersion}", true);

		bool[] available = new bool[TopTabs];
		string[] labels = new string[TopTabs];
		for (int i = 0; i < TopTabs; i++)
		{
			available[i] = IsScreenAvailable((HudTab)i);
			labels[i] = Text(player, $"hud.page.{PageIds[i]}");
		}

		SeasonClockView? season = null;
		if (seasons?.Current is string current)
		{
			CultureInfo language = player.Controller.GetLanguage();
			long total = 0, done = 0;
			if (SeasonClock.TryParse(current, out _))
			{
				total = (long)(SeasonClock.EndsUtc(current, seasons.Zone) - SeasonClock.StartsUtc(current, seasons.Zone)).TotalSeconds;
				done = total - (long)SeasonClock.Left(current, DateTime.UtcNow, seasons.Zone).TotalSeconds;
			}
			season = new SeasonClockView(Text(player, "hud.season-card", SeasonClock.Name(current, language)),
				Text(player, "hud.season-left", seasons.TimeLeft(player.Controller, current)), done, total);
		}

		PaintFrame(pen, new FrameView(
			state.Tab,
			available,
			labels,
			duels?.IncomingFor(player.SteamId) is null ? 0 : 1,
			events?.Current is ActiveEvent active ? Text(player, "hud.event-banner", PointEvents.Format(active.Multiplier), PointEvents.MinutesLeft(active, DateTime.UtcNow)) : null,
			Crumb(player, state),
			state.History.Count > 0 ? Text(player, "hud.back") : null,
			season,
			DuelBar(player),
			state.Status,
			Text(player, "hud.keys")));
	}

	private string Crumb(K4Player player, HudState state)
	{
		string screen = Text(player, $"hud.page.{PageIds[(int)state.Tab]}");
		if (state.Tab == HudTab.Leaderboard && state.RankList)
			return $"{screen} / {Text(player, "hud.rank-list")}";
		if (state.Tab == HudTab.Profile && state.ProfileOf != 0 && state.ProfileName.Length > 0)
			return $"{screen} / {state.ProfileName}";
		return screen;
	}

	private DuelBarView? DuelBar(K4Player player)
	{
		if (duels?.IncomingFor(player.SteamId) is not DuelOffer incoming)
			return null;
		return new DuelBarView(
			Text(player, "hud.duel.in-title"),
			Text(player, "hud.duel.in-text", incoming.ChallengerName, DuelTypeName(player, incoming.Type), Number(incoming.Stake)),
			Text(player, "hud.duel.seconds", incoming.SecondsLeft),
			DuelService.TypeVisual(incoming.Type),
			incoming.SecondsLeft,
			Math.Max(1, duels.AcceptSeconds),
			Text(player, "hud.sd-accept"),
			Text(player, "hud.sd-decline"));
	}

	/// <summary>The tabs, crumbs, season clock, duel bar and footer around the open screen.</summary>
	internal static void PaintFrame(HudPen pen, FrameView view)
	{
		for (int i = 0; i < TopTabs; i++)
		{
			string nav = $"k4-nav-{PageIds[i]}";
			pen.Gone(nav, !view.Available[i]);
			if (!view.Available[i])
				continue;
			pen.Var(nav, "label", view.Labels[i]);
			pen.Flag(nav, "k4-active", (int)view.Tab == i);
		}
		pen.Flag("k4-nav-settings", "k4-active", view.Tab == HudTab.Settings);
		for (int i = 0; i < PageIds.Length; i++)
			pen.Flag($"k4-view-{PageIds[i]}", "k4-show", (int)view.Tab == i);

		pen.Show("k4-nav-duels-badge", view.DuelsWaiting > 0);
		if (view.DuelsWaiting > 0)
			pen.Var("k4-nav-duels-badge", "count", view.DuelsWaiting.ToString(CultureInfo.InvariantCulture));

		pen.Show("k4-event", view.Event is not null);
		if (view.Event is string banner)
			pen.Var("k4-event", "event", banner);

		pen.Var("k4-crumbs", "crumb", view.Crumb);
		pen.Show("k4-back", view.Back is not null);
		if (view.Back is string back)
			pen.Var("k4-back", "label", back);

		pen.Show("k4-season", view.Season is not null);
		if (view.Season is SeasonClockView season)
		{
			pen.Var("k4-season", "title", season.Title);
			pen.Var("k4-season", "left", season.Left);
			pen.Bar("k4-season-bar", season.Done, season.Total);
		}

		PaintDuelBar(pen, view.DuelBar);

		pen.Show("k4-status", view.Status is not null);
		if (view.Status is (string status, bool ok))
		{
			pen.Var("k4-status", "status", status);
			pen.Swap("k4-status", "status", ok ? "k4-ok" : "k4-bad");
		}

		pen.Var("k4-keys", "keys", view.Keys);
	}

	internal static void PaintDuelBar(HudPen pen, DuelBarView? bar)
	{
		pen.Show("k4-duel-bar", bar is not null);
		if (bar is null)
			return;
		pen.Var("k4-duel-bar", "title", bar.Title);
		pen.Var("k4-duel-bar", "text", bar.Text);
		pen.Var("k4-duel-bar", "left", bar.Left);
		SetPicture(pen, "k4-duel-bar-icon", bar.Icon);
		pen.Bar("k4-duel-bar-timer", bar.SecondsLeft, bar.Seconds);
		pen.Var("k4-duel-bar-accept", "label", bar.Accept);
		pen.Var("k4-duel-bar-decline", "label", bar.Decline);
	}

	/// <summary>The 1 s refresh while a duel or challenge is live: countdown texts and bars only.</summary>
	private void DrawTimers(K4Player player, HudState state)
	{
		HudPen pen = Pen(player);
		PaintDuelBar(pen, DuelBar(player));
		if (state.Tab == HudTab.Duels)
			DrawDuelProgress(player, pen);
	}

	// ---------------------------------------------------------------- home

	/// <summary>The "do this next" cards, from real state only, in the order the owner picked; at most three.</summary>
	internal static List<NextKind> PickNext(bool duel, bool swap, bool showcase, bool achievement, bool season)
	{
		List<NextKind> picked = [];
		foreach ((NextKind kind, bool offered) in new[] { (NextKind.Duel, duel), (NextKind.Swap, swap), (NextKind.Showcase, showcase), (NextKind.Achievement, achievement), (NextKind.Season, season) })
		{
			if (offered && picked.Count < NextCards)
				picked.Add(kind);
		}
		return picked;
	}

	internal static string NextClass(NextKind kind) => kind switch
	{
		NextKind.Duel => "k4-n-duel",
		NextKind.Swap => "k4-n-swap",
		NextKind.Showcase => "k4-n-showcase",
		NextKind.Achievement => "k4-n-ach",
		_ => "k4-n-season"
	};

	private void DrawHome(K4Player player, HudState state)
	{
		HudPen pen = Pen(player);

		// The rank hero: emblem, rank, points, position, and the way to the next rank.
		pen.Gone("k4-h-rank", ranks is null || player.Rank is null);
		pen.Gone("k4-h-top", ranks is null);
		if (ranks is not null && player.Rank is RankState rank)
		{
			RankDefinition current = rank.Rank;
			RankDefinition? next = RankLadder.Next(ranks.Progression(player), rank.Points);
			SetEmblem(pen, "k4-h-rank-icon", current);
			pen.Var("k4-h-rank", "rank", current.Name);
			SetColor(pen, "k4-h-rank-name", current.ColorName);
			pen.Var("k4-h-rank", "points", Text(player, "hud.points", Number(rank.Points)));
			if (next is null)
			{
				pen.Var("k4-h-rank", "next", "");
				pen.Var("k4-h-rank", "togo", Text(player, "hud.max-rank"));
				pen.Bar("k4-h-bar", 1, 1);
			}
			else
			{
				int from = Math.Max(0, current.Point);
				pen.Var("k4-h-rank", "next", Text(player, "hud.next", next.Name));
				pen.Var("k4-h-rank", "togo", Text(player, "hud.to-go", Number(next.Point - rank.Points)));
				pen.Bar("k4-h-bar", rank.Points - from, next.Point - from);
			}

			void DrawPlace((int Placement, int Total) placed)
			{
				pen.Var("k4-h-rank", "place", PlaceText(player, placed.Placement, placed.Total));
				DrawHomeMe(player, pen, placed);
			}
			if (CachedPlacement(player.SteamId) is { } known)
				DrawPlace(known);
			else
				LoadPlacement(player, state, DrawPlace, "Loading the home rank");
		}

		// Top 5 of the current season, shared with the Leaderboard's cache.
		if (ranks is not null)
		{
			pen.Var("k4-h-top", "title", Text(player, "hud.h-top", HomeTop));
			void Top(List<TopEntry> top)
			{
				for (int i = 0; i < HomeTop; i++)
				{
					string row = $"k4-h-p{i}";
					pen.Show(row, i < top.Count);
					if (i >= top.Count)
						continue;
					pen.Var(row, "pos", Number(i + 1));
					pen.Var(row, "name", top[i].Name);
					pen.Var(row, "points", Number(top[i].Points));
					SetEmblem(pen, $"{row}-icon", ResolveRank(top[i]));
				}
			}

			if (topCache.TryGetValue(LeaderboardRows, out Cached<List<TopEntry>>? cached) && cached.Fresh)
				Top(cached.Value);
			else
				Later(player, state, () => store.GetTopAsync(LeaderboardRows),
					result => topCache[LeaderboardRows] = new Cached<List<TopEntry>>(result, DateTime.UtcNow), Top, "Loading the home top 5");
		}

		// Today: three daily missions and the weekly one, with their text, and the streak week.
		pen.Gone("k4-h-today", missions is null || player.Missions is null);
		List<MissionProgress> today = [];
		if (missions is not null && player.Missions is not null)
		{
			missions.EnsureToday(player);
			today = [.. player.Missions.Slots.Take(MissionCards)];
			pen.Var("k4-h-today", "title", Text(player, "hud.h-today"));
			pen.Var("k4-h-today", "reset", missions.Footer(player));
		}
		for (int i = 0; i <= MissionCards; i++)
		{
			string card = $"k4-h-m{i}";
			MissionProgress? mission = i < MissionCards ? (i < today.Count ? today[i] : null) : player.Missions?.Weekly;
			pen.Show(card, missions is not null && mission is not null);
			if (missions is null || mission is null)
				continue;
			DrawMissionCard(pen, card, player, mission);
		}
		if (missions is not null && player.Missions is PlayerMissions mine)
			DrawStreakDays(player, pen, mine, "k4-h-sd", tips: false);

		// Do this next.
		DrawNext(player, state, pen);

		// The big numbers: the player's own stats, as on the profile.
		List<(string Label, string Value, string Icon)> numbers = HomeNumbers(player);
		pen.Gone("k4-h-stats", numbers.Count == 0);
		for (int i = 0; i < HomeStats; i++)
		{
			string tile = $"k4-h-s{i}";
			pen.Show(tile, i < numbers.Count);
			if (i >= numbers.Count)
				continue;
			pen.Var(tile, "label", numbers[i].Label);
			pen.Var(tile, "value", numbers[i].Value);
			SetPicture(pen, $"{tile}-icon", numbers[i].Icon);
		}
	}

	private List<(string Label, string Value, string Icon)> HomeNumbers(K4Player player)
	{
		CultureInfo invariant = CultureInfo.InvariantCulture;
		List<(string Label, string Value, string Icon)> numbers = [];
		if (player.Stats?.ToArray() is int[] stats)
		{
			int kills = stats[(int)StatField.Kills];
			int shots = stats[(int)StatField.Shoots];
			numbers.Add((Text(player, "hud.stat.kd"), StatCounters.KillDeathRatio(kills, stats[(int)StatField.Deaths]).ToString("0.00", invariant), "ic-stats"));
			numbers.Add((Text(player, "hud.stat.headshots"), $"{StatService.HeadshotPercentage(stats[(int)StatField.Headshots], kills).ToString("0", invariant)}%", "ic-stat_headshots"));
			numbers.Add((Text(player, "hud.stat.accuracy"), $"{(shots > 0 ? stats[(int)StatField.HitsGiven] * 100.0 / shots : 0).ToString("0", invariant)}%", "ic-crosshair_circle"));
		}
		if (player.Playtime is not null && playtime.CurrentSeconds(player) is int[] seconds)
			numbers.Add((Text(player, "hud.stat.playtime"), playtime.Format(seconds[(int)PlaytimeField.All], StatTimeUnits), "ic-clock"));
		if (player.Stats?.ToArray() is int[] counted)
			numbers.Add((Text(player, "hud.stat.kills"), Number(counted[(int)StatField.Kills]), "ic-kill"));
		return numbers;
	}

	private void DrawHomeMe(K4Player player, HudPen pen, (int Placement, int Total) placed)
	{
		bool show = player.Rank is not null && placed.Placement > HomeTop;
		pen.Show("k4-h-me", show);
		if (!show)
			return;
		pen.Var("k4-h-me", "pos", Number(placed.Placement));
		pen.Var("k4-h-me", "points", Text(player, "hud.points", Number(player.Rank!.Points)));
	}

	private void DrawMissionCard(HudPen pen, string card, K4Player player, MissionProgress mission)
	{
		pen.Flag(card, "k4-done", mission.Completed);
		pen.Var(card, "desc", missions!.DescribeHere(player.Controller, mission));
		pen.Var(card, "progress", mission.Completed ? "✓" : $"{mission.Progress}/{mission.Mission.Target}");
		pen.Var(card, "reward", $"+{Number(missions.RewardOf(mission.Mission))}");
		SetPicture(pen, $"{card}-icon", MissionIcon(mission.Mission));
		pen.Bar($"{card}-bar", mission.Completed ? 1 : mission.Progress, mission.Completed ? 1 : mission.Mission.Target);
	}

	private void DrawNext(K4Player player, HudState state, HudPen pen)
	{
		DuelOffer? incoming = duels?.IncomingFor(player.SteamId);
		PlayerMissions? today = player.Missions;
		int swapsLeft = missions is null || today is null ? 0 : Math.Max(0, config.Missions.RerollsPerDay - today.RerollsUsed);
		bool swap = swapsLeft > 0 && today!.Slots.Any(slot => slot.Untouched);
		int unlocked = UnlockedCount(player);
		int shown = ShowcaseOf(player).Count;
		bool showcase = achievements is not null && player.Profile is not null && unlocked > shown && shown < PlayerProfile.ShowcaseSize;
		(Achievements.AchievementDefinition Achievement, int Progress, bool Done)? near = AchievementOrder(player)
			// The closest unfinished one the player has started (the order is closest first), so the card is never empty
			// while there is something to work toward.
			.Where(item => !item.Done && item.Progress > 0)
			.Select(item => ((Achievements.AchievementDefinition, int, bool)?)item).FirstOrDefault();
		bool seasonEnding = seasons?.Current is string season && SeasonClock.EndsSoon(season, DateTime.UtcNow, seasons.Zone);

		List<NextKind> cards = PickNext(incoming is not null, swap, showcase, near is not null, seasonEnding);
		pen.Gone("k4-h-next", cards.Count == 0);
		if (cards.Count > 0)
			pen.Var("k4-h-next", "title", Text(player, "hud.h-next"));

		for (int i = 0; i < NextCards; i++)
		{
			string card = $"k4-h-n{i}";
			state.Next[i] = i < cards.Count ? cards[i] : null;
			pen.Show(card, i < cards.Count);
			if (i >= cards.Count)
				continue;

			NextKind kind = cards[i];
			(string title, string text, string icon) = kind switch
			{
				NextKind.Duel => (Text(player, "hud.next.duel"), Text(player, "hud.duel.summary", incoming!.ChallengerName, DuelTypeName(player, incoming.Type), Number(incoming.Stake)), DuelService.TypeVisual(incoming.Type)),
				NextKind.Swap => (Text(player, "hud.next.swap"), Text(player, "hud.m-swaps", swapsLeft), "ic-trade_up"),
				NextKind.Showcase => (Text(player, "hud.next.showcase", shown, PlayerProfile.ShowcaseSize), Text(player, "hud.next.showcase-text"), "ic-winner_ribbon"),
				NextKind.Achievement => (achievements!.NameFor(near!.Value.Achievement, player.Controller), $"{Number(near.Value.Progress)}/{Number(near.Value.Achievement.Goal)}", AchievementIcon(near.Value.Achievement.Type)),
				_ => (Text(player, "hud.next.season"), Text(player, "hud.season-left", seasons!.TimeLeft(player.Controller, seasons.Current!)), "ic-clock")
			};
			pen.Var(card, "title", title);
			pen.Var(card, "text", text);
			SetPicture(pen, $"{card}-icon", icon);
			pen.Swap(card, "next", NextClass(kind));
		}
	}

	private RankDefinition ResolveRank(TopEntry entry)
		=> ranks!.Ladder.FindByName(entry.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(entry.Points);

	// ---------------------------------------------------------------- missions

	private bool? HandleMissionClick(K4Player player, HudState state, string id)
	{
		if (Row(id, "k4-reroll-", "", MissionCards) is int ask)
		{
			state.SwapAsk = ask;
			return true;
		}
		if (Row(id, "k4-m", "-no", MissionCards) is not null)
		{
			state.SwapAsk = null;
			return true;
		}
		if (Row(id, "k4-m", "-yes", MissionCards) is int yes && state.SwapAsk == yes)
		{
			state.SwapAsk = null;
			(ChatText result, bool swapped) = missions!.Swap(player, (yes + 1).ToString(CultureInfo.InvariantCulture));
			Say(player, state, swapped, result.Key, result.Args);
			return true;
		}
		return null;
	}

	private void DrawMissions(K4Player player, HudState state)
	{
		const string view = "k4-view-missions";
		HudPen pen = Pen(player);
		pen.Var(view, "title", Text(player, "hud.page.missions"));
		if (missions is null || player.Missions is null)
		{
			for (int i = 0; i < MissionCards; i++)
				pen.Show($"k4-m{i}", false);
			pen.Show("k4-mw", false);
			pen.Var(view, "reset", StripColors(Text(player, "error.loading")));
			return;
		}

		// Start today's missions if midnight passed since the last round start.
		missions.EnsureToday(player);
		PlayerMissions today = player.Missions;
		int swapsLeft = Math.Max(0, config.Missions.RerollsPerDay - today.RerollsUsed);
		if (state.SwapAsk is int asked && (asked >= today.Slots.Count || swapsLeft == 0 || !today.Slots[asked].Untouched))
			state.SwapAsk = null;

		for (int i = 0; i < MissionCards; i++)
		{
			string card = $"k4-m{i}";
			bool has = i < today.Slots.Count;
			pen.Show(card, has);
			if (!has)
				continue;

			MissionProgress slot = today.Slots[i];
			MissionDefinition mission = slot.Mission;
			DrawMissionCard(pen, card, player, slot);
			pen.Swap(card, "difficulty", $"k4-{mission.Difficulty.ToString().ToLowerInvariant()}");
			pen.Var(card, "diff", missions.DifficultyLabel(player.Controller, mission));
			pen.Var(card, "paid", slot.Completed ? Text(player, "hud.done") : "");

			bool asking = state.SwapAsk == i;
			bool canSwap = swapsLeft > 0 && slot.Untouched;
			pen.Show($"k4-reroll-{i}", canSwap && !asking);
			if (canSwap)
				pen.Var($"k4-reroll-{i}", "label", Text(player, "hud.reroll"));
			pen.Show($"k4-m{i}-ask", asking);
			if (asking)
			{
				pen.Var($"k4-m{i}-ask", "ask", Text(player, "hud.m-ask"));
				pen.Var($"k4-m{i}-yes", "label", Text(player, "hud.yes"));
				pen.Var($"k4-m{i}-no", "label", Text(player, "hud.no"));
			}
		}

		pen.Show("k4-mw", today.Weekly is not null);
		if (today.Weekly is MissionProgress weekly)
		{
			DrawMissionCard(pen, "k4-mw", player, weekly);
			pen.Var("k4-mw", "left", missions.WeekLeft());
		}

		int streak = MissionRules.CurrentStreak(today.Streak, today.LastCompletedDay, today.Day);
		pen.Var(view, "streak", Number(streak));
		pen.Var(view, "reset", missions.Footer(player));
		pen.Var(view, "swaps", Text(player, "hud.m-swaps", swapsLeft));
		pen.Var(view, "streakhint", Text(player, "hud.streak-hint"));
		DrawStreakDays(player, pen, today, "k4-sd", tips: true);
	}

	/// <summary>The streak's current week: finished days lit, today marked, each with its bonus.</summary>
	private void DrawStreakDays(K4Player player, HudPen pen, PlayerMissions today, string prefix, bool tips)
	{
		int streak = MissionRules.CurrentStreak(today.Streak, today.LastCompletedDay, today.Day);
		bool doneToday = today.LastCompletedDay == today.Day;
		(int firstDay, int lit, int todayIndex) = StreakWindow(streak, doneToday);
		int dayTotal = (int)Math.Min(int.MaxValue, today.Slots.Sum(slot => (long)missions!.RewardOf(slot.Mission)));

		for (int i = 0; i < StreakDays; i++)
		{
			int day = firstDay + i;
			int bonus = MissionRules.StreakBonus(day, dayTotal, config.Missions.StreakBonusPercent, config.Missions.StreakMaxDays);
			string slot = $"{prefix}{i}";
			pen.Flag(slot, "k4-on", i < lit);
			pen.Flag(slot, "k4-today", i == todayIndex);
			pen.Var(slot, "day", Text(player, "hud.streak-day", day));
			if (!tips)
				continue;
			pen.Var(slot, "bonus", $"+{Number(bonus)}");
			pen.Var(slot, "tip", Text(player, i < lit ? "hud.tip.streak-done" : i == todayIndex ? "hud.tip.streak-today" : "hud.tip.streak-next", day, Number(bonus)));
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

	private int UnlockedCount(K4Player player)
		=> achievements is not null && player.Achievements is Achievements.PlayerAchievements mine ? achievements.All.Count(achievement => mine.Unlocked.Contains(achievement.Id)) : 0;

	/// <summary>The achievements on the player's profile that still exist and are unlocked, in showcase order.</summary>
	private List<Achievements.AchievementDefinition> ShowcaseOf(K4Player player)
	{
		if (achievements is null || player.Profile is not PlayerProfile profile || player.Achievements is not Achievements.PlayerAchievements mine)
			return [];
		return [.. profile.Showcase
			.Where(mine.Unlocked.Contains)
			.Select(id => achievements.All.FirstOrDefault(achievement => achievement.Id == id))
			.OfType<Achievements.AchievementDefinition>()];
	}

	private bool? HandleAchievementClick(K4Player player, HudState state, string id)
	{
		if (id.StartsWith("k4-af-", StringComparison.Ordinal))
		{
			if (!Enum.TryParse(id["k4-af-".Length..], ignoreCase: true, out AchievementFilter filter) || state.Filter == filter)
				return false;
			state.Filter = filter;
			return true;
		}

		if (Row(id, "k4-a-sc", "", PlayerProfile.ShowcaseSize) is int slot)
			return state.ShowcaseIds[slot] is string shown && ToggleShowcase(player, state, shown);

		if (Row(id, "k4-a", "", AchievementRows) is int card)
			return state.AchievementIds[card] is string achievement && ToggleShowcase(player, state, achievement);

		return null;
	}

	/// <summary>Shows an unlocked achievement on the player's profile, or takes it off again; the footer says which.</summary>
	private bool ToggleShowcase(K4Player player, HudState state, string achievement)
	{
		if (achievements is null || player.Profile is not PlayerProfile profile || player.Achievements?.Unlocked.Contains(achievement) != true)
			return false;

		string name = achievements.All.FirstOrDefault(item => item.Id == achievement) is { } definition ? achievements.NameFor(definition, player.Controller) : achievement;
		switch (profile.Toggle(achievement))
		{
			case PlayerProfile.ShowcaseChange.Full:
				Say(player, state, false, "profile.showcase-full", PlayerProfile.ShowcaseSize);
				break;
			case PlayerProfile.ShowcaseChange.Removed:
				Say(player, state, true, "hud.st.unpinned", name);
				break;
			default:
				Say(player, state, true, "hud.st.pinned", name);
				break;
		}
		return true;
	}

	private void DrawAchievements(K4Player player, HudState state)
	{
		const string view = "k4-view-achievements";
		HudPen pen = Pen(player);
		List<(Achievements.AchievementDefinition Achievement, int Progress, bool Done)> list = FilteredAchievements(player, state.Filter);
		IReadOnlyList<string> showcase = player.Profile?.Showcase ?? [];
		int unlocked = UnlockedCount(player);

		pen.Var(view, "count", Text(player, "hud.ach-summary", Number(unlocked), Number(achievements?.All.Count ?? 0)));

		// The showcase strip: the three slots on the player's profile.
		List<Achievements.AchievementDefinition> shown = ShowcaseOf(player);
		pen.Var(view, "showcase", Text(player, "hud.a-showcase", shown.Count, PlayerProfile.ShowcaseSize));
		for (int i = 0; i < PlayerProfile.ShowcaseSize; i++)
		{
			string slot = $"k4-a-sc{i}";
			bool filled = i < shown.Count;
			state.ShowcaseIds[i] = filled ? shown[i].Id : null;
			pen.Flag(slot, "k4-empty", !filled);
			pen.Var(slot, "name", filled ? achievements!.NameFor(shown[i], player.Controller) : "");
			pen.Var(slot, "empty", filled ? "" : Text(player, "hud.a-empty"));
			pen.Swap($"{slot}-ring", "tier", filled ? MedalTier(shown[i]) : "t-locked");
			SetPicture(pen, $"{slot}-icon", filled ? AchievementIcon(shown[i].Type) : null);
		}

		foreach (AchievementFilter filter in Enum.GetValues<AchievementFilter>())
		{
			string name = filter.ToString().ToLowerInvariant();
			string button = $"k4-af-{name}";
			pen.Var(button, "label", Text(player, $"hud.ach-filter.{name}"));
			pen.Flag(button, "k4-active", state.Filter == filter);
		}

		for (int i = 0; i < AchievementRows; i++)
		{
			string card = $"k4-a{i}";
			bool has = i < list.Count;
			pen.Show(card, has);
			state.AchievementIds[i] = null;
			if (!has)
				continue;

			(Achievements.AchievementDefinition achievement, int progress, bool done) = list[i];
			bool pinned = done && showcase.Contains(achievement.Id);
			state.AchievementIds[i] = done && player.Profile is not null ? achievement.Id : null;
			pen.Flag(card, "k4-done", done);
			pen.Flag(card, "k4-pinned", pinned);
			string description = achievements!.DescriptionFor(achievement, player.Controller);
			pen.Var(card, "name", achievements.NameFor(achievement, player.Controller));
			pen.Var(card, "desc", description);
			pen.Var(card, "goal", done ? $"{Number(achievement.Goal)} ✓" : $"{Number(progress)}/{Number(achievement.Goal)}");
			string status = done
				? Text(player, "hud.tip.unlocked", description, Number(achievement.Points))
				: Text(player, "hud.tip.progress", description, Number(progress), Number(achievement.Goal), Number(achievement.Points));
			pen.Var(card, "tip", done && player.Profile is not null
				? $"{status} · {Text(player, pinned ? "hud.tip.unpin" : "hud.tip.pin")}"
				: status);
			pen.Swap($"{card}-ring", "tier", done ? MedalTier(achievement) : "t-locked");
			SetPicture(pen, $"{card}-icon", AchievementIcon(achievement.Type));
			pen.Bar($"{card}-bar", progress, achievement.Goal);
		}

		int more = NotShown(list.Count, AchievementRows);
		pen.Show("k4-a-more", more > 0);
		if (more > 0)
			pen.Var("k4-a-more", "more", Text(player, "hud.more", Number(more)));

		pen.Show("k4-a-empty", list.Count == 0);
		if (list.Count == 0)
		{
			pen.Var("k4-a-empty", "none", Text(player, $"hud.a-none.{state.Filter.ToString().ToLowerInvariant()}"));
			bool offer = state.Filter != AchievementFilter.Unlocked && unlocked > 0;
			pen.Gone("k4-a-show-unlocked", !offer);
			if (offer)
				pen.Var("k4-a-show-unlocked", "label", Text(player, "hud.a-show-unlocked"));
		}
	}

	// ---------------------------------------------------------------- leaderboard

	/// <summary>One leaderboard row as the layout shows it, already in the reader's language.</summary>
	internal readonly record struct LeaderboardRow(string Pos, string Name, string Rank, string Points, string Emblem, int Place, bool Me, bool CanDuel);

	private void DrawLeaderboard(K4Player player, HudState state)
	{
		if (ranks is null)
			return;

		const string view = "k4-view-leaderboard";
		HudPen pen = Pen(player);
		pen.Var("k4-lb-tab-players", "label", Text(player, "hud.lb-players"));
		pen.Var("k4-lb-tab-ranks", "label", Text(player, "hud.rank-list"));
		pen.Flag("k4-lb-tab-players", "k4-active", !state.RankList);
		pen.Flag("k4-lb-tab-ranks", "k4-active", state.RankList);
		pen.Set("k4-lb-players", "k4-gone", state.RankList);
		pen.Flag("k4-view-leaderboard", "k4-lb-ranklist", state.RankList);
		pen.Set("k4-lb-ranks", "k4-gone", !state.RankList);

		if (state.RankList)
			DrawRankList(player, pen);
		else
			DrawPlayers(player, state, pen, view);
	}

	private void DrawPlayers(K4Player player, HudState state, HudPen pen, string view)
	{
		CultureInfo language = player.Controller.GetLanguage();
		pen.Var("k4-lb-list", "profile", Text(player, "hud.lb-profile"));
		pen.Var("k4-lb-list", "duel", Text(player, "hud.challenge"));

		DrawSeasonSwitch(player, state, pen, view, language);
		if (seasons?.Current is not null && finishedSeasons is null)
		{
			// Only the switch waits for this list (the live season is open until it arrives), so only the switch redraws:
			// redrawing the rows would start a second load of the live standings while the first is still on its way.
			Later(player, state, () => store.GetFinishedSeasonsAsync(SeasonRows), result => finishedSeasons = result,
				_ => DrawSeasonSwitch(player, state, pen, view, language), "Loading the finished seasons");
		}
		DrawStandings(player, state, pen, view);
	}

	private void DrawSeasonSwitch(K4Player player, HudState state, HudPen pen, string view, CultureInfo language)
	{
		if (seasons?.Current is string current)
		{
			int count = finishedSeasons?.Count ?? 0;
			state.Season = Math.Clamp(state.Season, 0, count);
			pen.Var(view, "season", state.Season == 0 ? SeasonClock.Name(current, language) : SeasonClock.Name(finishedSeasons![state.Season - 1], language));
			pen.Var(view, "status", state.Season == 0
				? Text(player, "hud.season-live", seasons.TimeLeft(player.Controller, current))
				: Text(player, "hud.season-final"));
			pen.Flag("k4-lb-newer", "k4-disabled", state.Season == 0);
			pen.Flag("k4-lb-older", "k4-disabled", state.Season >= count);
		}
		else
		{
			state.Season = 0;
			pen.Var(view, "season", Text(player, "hud.page.leaderboard"));
			pen.Var(view, "status", "");
			pen.Flag("k4-lb-newer", "k4-disabled", true);
			pen.Flag("k4-lb-older", "k4-disabled", true);
		}
	}

	private void DrawStandings(K4Player player, HudState state, HudPen pen, string view)
	{
		void Draw(List<TopEntry> top, int total)
		{
			List<LeaderboardRow> rows = new(Math.Min(top.Count, LeaderboardRows));
			for (int i = 0; i < LeaderboardRows; i++)
			{
				bool has = i < top.Count;
				state.LeaderboardIds[i] = has ? top[i].SteamId : 0;
				state.LeaderboardDuel[i] = has && state.Season == 0 && CanChallengeNow(player, top[i].SteamId);
				if (!has)
					continue;
				TopEntry entry = top[i];
				RankDefinition rank = ResolveRank(entry);
				rows.Add(new LeaderboardRow(Number(i + 1), entry.Name, rank.Name, Number(entry.Points), Emblem(rank), i + 1,
					state.Season == 0 && entry.SteamId == player.SteamId, state.LeaderboardDuel[i]));
			}
			int more = NotShown(total, LeaderboardRows);
			PaintLeaderboard(pen, rows, config.Modules.Profiles, more > 0 ? Text(player, "hud.more", Number(more)) : "");
			pen.Var(view, "count", Text(player, "hud.lb-count", Number(total)));
		}

		// A finished season: its final standings, without the player's own live row.
		if (state.Season > 0 && finishedSeasons is { } finished && state.Season <= finished.Count)
		{
			string season = finished[state.Season - 1];
			pen.Show("k4-lb-me", false);
			if (seasonTopCache.TryGetValue(season, out (List<TopEntry> Top, int Players) past))
				Draw(past.Top, past.Players);
			else
			{
				Later(player, state, () => store.GetSeasonTopAsync(season, LeaderboardRows),
					result => seasonTopCache[season] = result, result => Draw(result.Top, result.Players), "Loading a past season's leaderboard");
			}
			return;
		}

		ulong steamId = player.SteamId;
		void DrawLive(List<TopEntry> top, (int Placement, int Total) me)
		{
			Draw(top, me.Total);
			bool pinned = player.Rank is not null && me.Placement > 0;
			pen.Show("k4-lb-me", pinned);
			if (pinned && player.Rank is RankState own)
			{
				SetEmblem(pen, "k4-lb-me-icon", own.Rank);
				pen.Var("k4-lb-me", "pos", Number(me.Placement));
				pen.Var("k4-lb-me", "name", player.Name);
				pen.Var("k4-lb-me", "rank", own.Rank.Name);
				pen.Var("k4-lb-me", "points", Number(own.Points));
			}
		}

		if (topCache.TryGetValue(LeaderboardRows, out Cached<List<TopEntry>>? top) && top.Fresh && CachedPlacement(steamId) is { } placed)
		{
			DrawLive(top.Value, placed);
			return;
		}

		Later(player, state, async () =>
		{
			List<TopEntry> entries = await store.GetTopAsync(LeaderboardRows);
			(int placement, int total) = await store.GetPlacementAndTotalAsync(steamId);
			return (entries, (placement, total));
		}, result =>
		{
			topCache[LeaderboardRows] = new Cached<List<TopEntry>>(result.entries, DateTime.UtcNow);
			placementCache[steamId] = new Cached<(int, int)>(result.Item2, DateTime.UtcNow);
		}, result => DrawLive(result.entries, result.Item2), "Loading the leaderboard");
	}

	/// <summary>Another player who is online now and can be challenged by this one: the leaderboard and profile Challenge buttons.</summary>
	private bool CanChallengeNow(K4Player player, ulong target)
		=> duels is not null && target != 0 && target != player.SteamId && players.Get(target) is { IsHuman: true, IsValid: true } other
			&& duels.CanChallenge(player, other) is null;

	/// <summary>Fills the leaderboard pool: one row per entry, medals for the top three, the reader's own row marked; rows past the list hide.</summary>
	internal static void PaintLeaderboard(HudPen pen, IReadOnlyList<LeaderboardRow> rows, bool profiles, string more)
	{
		for (int i = 0; i < LeaderboardRows; i++)
		{
			string row = $"k4-lb-r{i}";
			bool has = i < rows.Count;
			pen.Show(row, has);
			pen.Show($"{row}-duel", has && rows[i].CanDuel);
			if (!has)
				continue;

			LeaderboardRow entry = rows[i];
			pen.Var(row, "pos", entry.Pos);
			pen.Var(row, "name", entry.Name);
			pen.Var(row, "rank", entry.Rank);
			pen.Var(row, "points", entry.Points);
			pen.Swap($"{row}-icon", "icon", entry.Emblem);
			pen.Swap(row, "place", entry.Place is >= 1 and <= 3 ? $"k4-p{entry.Place}" : null);
			pen.Flag(row, "k4-me", entry.Me);
			pen.Gone($"{row}-profile", !profiles);
		}

		pen.Show("k4-lb-more", more.Length > 0);
		if (more.Length > 0)
			pen.Var("k4-lb-more", "more", more);
	}

	private void DrawRankList(K4Player player, HudPen pen)
	{
		IReadOnlyList<RankDefinition> all = ranks!.Ladder.Ranks;

		// The pinned "you" card, because the server cannot scroll the list to the player's rank.
		if (player.Rank is RankState mine)
		{
			string rewards = RankLadder.Next(ranks.Progression(player), mine.Points) is RankDefinition next
				? Text(player, "hud.lb-you", mine.Rank.Name, next.Name, Number(next.Point - mine.Points))
				: Text(player, "hud.lb-you-top", mine.Rank.Name);
			pen.Var("k4-lb-you", "text", rewards);
		}
		else
			pen.Var("k4-lb-you", "text", "");

		for (int i = 0; i < RankRows; i++)
		{
			string row = $"k4-rk{i}";
			bool has = i < all.Count;
			pen.Show(row, has);
			if (!has)
				continue;

			RankDefinition rank = all[i];
			bool current = player.Rank?.Rank.Key == rank.Key;
			pen.Flag(row, "k4-current", current);
			SetEmblem(pen, $"{row}-icon", rank);
			pen.Var(row, "name", rank.Name);
			SetColor(pen, $"{row}-name", rank.ColorName);
			pen.Var(row, "points", rank.IsDefault ? Text(player, "hud.rank-start") : Number(rank.Point));
			IReadOnlyList<string> rewards = RankRewards.DescriptionLines(rank);
			string reward = StripColors(string.Join(" · ", rewards));
			pen.Var(row, "reward", reward);
			pen.Show($"{row}-gift", rewards.Count > 0);
			pen.Var(row, "tip", rewards.Count > 0 ? Text(player, "hud.tip.rank-reward", reward) : Text(player, "hud.tip.rank-none"));
			pen.Show($"{row}-you", current);
		}

		int more = NotShown(all.Count, RankRows);
		pen.Show("k4-rk-more", more > 0);
		if (more > 0)
			pen.Var("k4-rk-more", "more", Text(player, "hud.more", Number(more)));
	}

	// ---------------------------------------------------------------- duels

	/// <summary>A click on the Duels screen, or null when the button is not one of its own.</summary>
	private bool? HandleDuelClick(K4Player player, HudState state, string id)
	{
		switch (id)
		{
			case "k4-d-send":
				if (state.DuelType is not DuelType type || state.DuelStake is not int stake)
					return false;
				if (players.Get(state.DuelTarget) is not { IsHuman: true, IsValid: true } target)
				{
					state.DuelTarget = 0;
					Say(player, state, false, "hud.st.left");
					return true;
				}
				if (duels!.Challenge(player, target, stake, type) is DuelService.Refusal refusal)
					Say(player, state, false, refusal.Key, refusal.Args);
				else
					Say(player, state, true, "hud.st.sent", target.Name);
				state.DuelStake = null;
				return true;
			case "k4-d-takeback":
				// Decline also takes back the player's own challenge; a player is in at most one challenge at a time.
				if (duels!.OutgoingFor(player.SteamId) is null)
					return false;
				Answer(player, state, duels.Decline(player), "hud.st.taken-back");
				return true;
		}

		if (Row(id, "k4-dp", "", DuelPlayerRows) is int row && state.DuelPlayerIds[row] is ulong picked and not 0)
		{
			if (state.DuelTarget == picked)
				return false;
			state.DuelTarget = picked;
			state.DuelStake = null;
			return true;
		}

		if (Row(id, "k4-dt", "", DuelTypeCards) is int card && state.DuelTypeIds[card] is DuelType chosen)
		{
			state.DuelType = chosen;
			return true;
		}

		if (Row(id, "k4-ds", "", DuelStakeButtons) is int button && state.DuelStakeValues[button] is int value)
		{
			state.DuelStake = value;
			return true;
		}

		return null;
	}

	/// <summary>The index in "k4-dp3", "k4-dt0" or "k4-ds4"; null for anything else. Kept for callers; <see cref="Row"/> is the parser.</summary>
	internal static int? DuelSlot(string id, string prefix) => Row(id, prefix, "", DuelPlayerRows);

	private string DuelTypeName(K4Player player, DuelType type) => Text(player, $"duel.type.{DuelRules.Name(type)}");

	/// <summary>Why another player cannot be challenged right now, in two or three words; empty when they can.</summary>
	private string DuelState(K4Player player, K4Player other, DuelService service)
	{
		if (service.RunningFor(other.SteamId) is not null)
			return Text(player, "hud.duel.state.busy");
		if (service.IncomingFor(other.SteamId) is not null || service.OutgoingFor(other.SteamId) is not null)
			return Text(player, "hud.duel.state.challenged");
		if (service.CooldownLeft(other.SteamId) > TimeSpan.Zero)
			return Text(player, "hud.duel.state.cooldown");
		if (service.MaxStake(player, other) == 0)
			return Text(player, "hud.duel.state.points");
		return "";
	}

	private void DrawDuels(K4Player player, HudState state)
	{
		const string view = "k4-view-duels";
		DuelService service = duels!;
		HudPen pen = Pen(player);

		pen.Var(view, "pick", Text(player, "hud.duel.pick"));
		pen.Var(view, "types", Text(player, "hud.duel.types"));
		pen.Var(view, "stakes", Text(player, "hud.duel.stakes"));
		pen.Var(view, "live", Text(player, "hud.duel.live"));
		pen.Var("k4-d-send", "label", Text(player, "hud.duel.send"));
		pen.Var("k4-d-takeback", "label", Text(player, "hud.duel.takeback"));

		// Step 1: every other player on the server, by name. A picked player who left is unpicked.
		List<K4Player> others = [.. players.ValidHumans.Where(other => other.SteamId != player.SteamId).OrderBy(other => other.Name, StringComparer.CurrentCultureIgnoreCase)];
		if (others.All(other => other.SteamId != state.DuelTarget))
			state.DuelTarget = 0;
		K4Player? target = state.DuelTarget == 0 ? null : players.Get(state.DuelTarget);

		pen.Var(view, "online", Text(player, "hud.duel.online", others.Count));
		for (int i = 0; i < DuelPlayerRows; i++)
		{
			string slot = $"k4-dp{i}";
			bool has = i < others.Count;
			pen.Show(slot, has);
			state.DuelPlayerIds[i] = has ? others[i].SteamId : 0;
			if (!has)
				continue;

			K4Player other = others[i];
			pen.Var(slot, "name", other.Name);
			pen.Var(slot, "points", other.Rank is RankState rank ? Text(player, "hud.points", Number(rank.Points)) : "");
			pen.Var(slot, "state", DuelState(player, other, service));
			if (other.Rank is RankState shownRank)
				SetEmblem(pen, $"{slot}-icon", shownRank.Rank);
			pen.Flag(slot, "k4-active", other.SteamId == state.DuelTarget);
			pen.Flag(slot, "k4-disabled", service.CanChallenge(player, other) is not null);
		}
		int more = NotShown(others.Count, DuelPlayerRows);
		pen.Show("k4-dp-more", more > 0);
		if (more > 0)
			pen.Var("k4-dp-more", "more", Text(player, "hud.more", Number(more)));
		pen.Show("k4-dp-none", others.Count == 0);
		if (others.Count == 0)
			pen.Var("k4-dp-none", "none", Text(player, "hud.duel.nobody"));

		// Step 2: the types this map allows.
		IReadOnlyList<DuelType> types = service.AvailableTypes;
		if (state.DuelType is DuelType picked && !types.Contains(picked))
			state.DuelType = null;
		state.DuelType ??= types.Count > 0 ? types[0] : null;
		for (int i = 0; i < DuelTypeCards; i++)
		{
			string card = $"k4-dt{i}";
			bool has = i < types.Count;
			pen.Show(card, has);
			state.DuelTypeIds[i] = has ? types[i] : null;
			if (!has)
				continue;

			DuelType type = types[i];
			pen.Var(card, "name", DuelTypeName(player, type));
			pen.Var(card, "tip", Text(player, $"duel.type-tip.{DuelRules.Name(type)}"));
			SetPicture(pen, $"{card}-icon", DuelService.TypeVisual(type));
			pen.Flag(card, "k4-active", type == state.DuelType);
		}

		// Step 3: the stake presets and the most both players can bet.
		int max = target is null ? 0 : service.MaxStake(player, target);
		List<(string Label, int Value)> stakes = [.. service.StakePresets.Select(value => (Number(value), value))];
		if (max > 0)
			stakes.Add((Text(player, "hud.duel.max", Number(max)), max));
		if (state.DuelStake is int chosenStake && (target is null || chosenStake > max))
			state.DuelStake = null;
		for (int i = 0; i < DuelStakeButtons; i++)
		{
			string button = $"k4-ds{i}";
			bool has = i < stakes.Count;
			pen.Show(button, has);
			state.DuelStakeValues[i] = has && target is not null && stakes[i].Value <= max ? stakes[i].Value : null;
			if (!has)
				continue;

			pen.Var(button, "label", stakes[i].Label);
			pen.Flag(button, "k4-disabled", state.DuelStakeValues[i] is null);
			pen.Flag(button, "k4-active", state.DuelStake == stakes[i].Value && state.DuelStakeValues[i] is not null);
		}

		// The challenge so far, and why it cannot be sent yet.
		DuelOffer? outgoing = service.OutgoingFor(player.SteamId);
		DuelService.Refusal? refusal = target is null ? null : service.CanChallenge(player, target);
		bool ready = target is not null && state.DuelType is not null && state.DuelStake is not null && refusal is null && outgoing is null;
		pen.Var(view, "summary", target is null || state.DuelType is not DuelType summaryType
			? ""
			: Text(player, "hud.duel.summary", target.Name, DuelTypeName(player, summaryType), state.DuelStake is int stake ? Number(stake) : "?"));
		pen.Var(view, "status", outgoing is not null
			? ""
			: refusal is DuelService.Refusal reason
				? StripColors(Text(player, reason.Key, reason.Args))
				: target is null
					? others.Count == 0 ? "" : Text(player, "hud.duel.pick-player") // the empty player list already says nobody is here
					: state.DuelStake is null ? Text(player, "hud.duel.pick-stake") : "");
		pen.Flag("k4-d-send", "k4-disabled", !ready);

		DrawDuelProgress(player, pen);
	}

	/// <summary>The player's waiting challenge, their running duel, and everyone else's: the parts with countdowns.</summary>
	private void DrawDuelProgress(K4Player player, HudPen pen)
	{
		DuelService service = duels!;

		DuelOffer? outgoing = service.OutgoingFor(player.SteamId);
		pen.Show("k4-d-wait", outgoing is not null);
		if (outgoing is not null)
		{
			pen.Var("k4-d-wait", "text", Text(player, "hud.duel.waiting", outgoing.OpponentName, outgoing.SecondsLeft));
			pen.Bar("k4-d-wait-bar", outgoing.SecondsLeft, Math.Max(1, service.AcceptSeconds));
		}

		DuelView? mine = service.RunningFor(player.SteamId);
		pen.Show("k4-d-mine", mine is not null);
		if (mine is not null)
		{
			bool challenger = mine.Challenger == player.SteamId;
			string opponent = challenger ? mine.OpponentName : mine.ChallengerName;
			(int own, int theirs) = challenger ? (mine.ChallengerKills, mine.OpponentKills) : (mine.OpponentKills, mine.ChallengerKills);
			pen.Var("k4-d-mine", "title", Text(player, "hud.duel.mine-title", opponent));
			pen.Var("k4-d-mine", "score", $"{own} : {theirs}");
			pen.Var("k4-d-mine", "text", Text(player, "hud.duel.mine-text", DuelTypeName(player, mine.Type), mine.Target, Number(mine.Stake),
				playtime.Format(mine.SecondsLeft, StatTimeUnits)));
			SetPicture(pen, "k4-d-mine-icon", DuelService.TypeVisual(mine.Type));
			pen.Bar("k4-d-mine-bar", mine.SecondsLeft, Math.Max(1, config.Duels.Minutes * 60));
		}

		List<DuelView> live = [.. service.Running.Where(duel => duel.Challenger != player.SteamId && duel.Opponent != player.SteamId).Take(LiveDuelRows)];
		for (int i = 0; i < LiveDuelRows; i++)
		{
			string row = $"k4-dl{i}";
			pen.Show(row, i < live.Count);
			if (i >= live.Count)
				continue;

			DuelView duel = live[i];
			pen.Var(row, "names", $"{duel.ChallengerName}  vs  {duel.OpponentName}");
			pen.Var(row, "score", $"{duel.ChallengerKills} : {duel.OpponentKills}");
			pen.Var(row, "info", Text(player, "hud.duel.live-info", DuelTypeName(player, duel.Type), Number(duel.Stake), playtime.Format(duel.SecondsLeft, StatTimeUnits)));
			SetPicture(pen, $"{row}-icon", DuelService.TypeVisual(duel.Type));
		}
		pen.Show("k4-dl-none", live.Count == 0);
		if (live.Count == 0)
			pen.Var("k4-dl-none", "none", Text(player, "hud.duel.none"));
	}

	// ---------------------------------------------------------------- profile

	/// <summary>A profile as the panel shows it: stored data, with live values when the player is on this server.</summary>
	internal sealed record ProfileData(
		string Name,
		int Points,
		RankDefinition? Rank,
		int Placement,
		int Total,
		int[]? Stats,
		int[]? Playtime,
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
		int[]? time = live is not null
			? live.Playtime is null ? null : playtime.CurrentSeconds(live)
			: config.Modules.Playtime ? stored!.Record.Playtime : null;
		(int Placement, int Total)? cachedPlace = live is null ? null : CachedPlacement(live.SteamId);

		return new ProfileData(
			live?.Name ?? stored!.Name,
			live?.Rank?.Points ?? stored?.Record.Points ?? 0,
			rank,
			stored?.Record.Placement ?? cachedPlace?.Placement ?? 0,
			stored?.Total ?? cachedPlace?.Total ?? 0,
			stats,
			time,
			live?.Achievements?.Unlocked ?? stored?.Record.Achievements ?? [],
			live?.Profile?.Showcase ?? stored?.Record.Showcase ?? [],
			live?.Profile?.WeaponKills ?? stored?.Record.WeaponKills ?? new Dictionary<string, int>(),
			stored?.Seasons ?? []);
	}

	private void DrawProfile(K4Player player, HudState state)
	{
		ulong target = state.ProfileOf == 0 ? player.SteamId : state.ProfileOf;
		bool own = state.ProfileOf == 0;
		HudPen pen = Pen(player);

		bool challenge = !own && CanChallengeNow(player, target);
		pen.Show("k4-pf-challenge", challenge);
		if (challenge)
			pen.Var("k4-pf-challenge", "label", Text(player, "hud.challenge"));

		// The slot may still show the last profile drawn in it (often the viewer's own), so nothing of it may stay while
		// another player's profile loads or when that player is not found.
		void Draw(ProfileRecord? stored, bool loaded)
		{
			K4Player? live = players.Get(target) is { IsHuman: true } online && (online.Rank is not null || online.Stats is not null) ? online : null;
			if (BuildProfile(stored, live) is not ProfileData profile)
			{
				DrawProfileData(player, pen, BlankProfile(loaded ? Text(player, "hud.pf-missing") : state.ProfileName), own);
				pen.Var("k4-view-profile", "points", "");
				return;
			}
			if (!own && state.ProfileName != profile.Name)
			{
				state.ProfileName = profile.Name;
				pen.Var("k4-crumbs", "crumb", Crumb(player, state));
			}
			DrawProfileData(player, pen, profile, own);
		}

		if (profileCache.TryGetValue(target, out Cached<ProfileRecord?>? cached) && cached.Fresh)
		{
			Draw(cached.Value, loaded: true);
			return;
		}

		// A player in the server draws at once from memory, anyone else as a blank profile with their name; finished
		// seasons and the position follow from the database.
		Draw(null, loaded: false);
		Later(player, state, () => store.GetProfileAsync(target, SeasonRows),
			result => profileCache[target] = new Cached<ProfileRecord?>(result, DateTime.UtcNow), result => Draw(result, loaded: true), "Loading a profile");
	}

	/// <summary>A profile with only a name: what shows while a profile loads, or when the player is not found.</summary>
	internal static ProfileData BlankProfile(string name) => new(name, 0, null, 0, 0, null, null, [], [], new Dictionary<string, int>(), []);

	private void DrawProfileData(K4Player player, HudPen pen, ProfileData profile, bool own)
	{
		const string view = "k4-view-profile";
		CultureInfo invariant = CultureInfo.InvariantCulture;
		bool textBadge = IconSet == IconsText;

		pen.Var(view, "name", profile.Name);
		if (profile.Rank is RankDefinition rank)
		{
			SetEmblem(pen, "k4-pf-icon", rank);
			pen.Var("k4-pf-badge", "tag", string.IsNullOrWhiteSpace(rank.Tag) ? rank.Name : rank.Tag.Trim('[', ']'));
			pen.Var("k4-pf-rank", "rank", rank.Name);
			SetColor(pen, "k4-pf-rank", rank.ColorName);
			SetColor(pen, "k4-pf-badge", rank.ColorName);
			RankDefinition? next = ranks is null ? null : RankLadder.Next(own ? ranks.Progression(player) : ranks.Ladder.Ranks, profile.Points);
			int from = Math.Max(0, rank.Point);
			pen.Var(view, "next", next is null ? Text(player, "hud.max-rank") : Text(player, "hud.next-in", next.Name, Number(next.Point - profile.Points)));
			pen.Bar("k4-pf-bar", next is null ? 1 : profile.Points - from, next is null ? 1 : next.Point - from);
		}
		else
		{
			pen.Var("k4-pf-rank", "rank", "");
			pen.Var(view, "next", "");
		}
		pen.Set("k4-pf-icon", "k4-gone", textBadge || profile.Rank is null);
		pen.Set("k4-pf-badge", "k4-gone", !textBadge || profile.Rank is null);

		pen.Var(view, "points", ranks is null ? "" : Text(player, "hud.points", Number(profile.Points)));
		pen.Var(view, "place", PlaceText(player, profile.Placement, profile.Total));

		// Showcase: up to three chosen achievements, as medals.
		List<Achievements.AchievementDefinition> shown = achievements is null ? [] : [.. profile.Showcase
			.Where(profile.Achievements.Contains)
			.Select(id => achievements.All.FirstOrDefault(achievement => achievement.Id == id))
			.OfType<Achievements.AchievementDefinition>()];
		for (int i = 0; i < PlayerProfile.ShowcaseSize; i++)
		{
			string slot = $"k4-pf-sc{i}";
			pen.Show(slot, i < shown.Count);
			if (i >= shown.Count)
				continue;
			string description = achievements!.DescriptionFor(shown[i], player.Controller);
			pen.Var(slot, "name", achievements.NameFor(shown[i], player.Controller));
			pen.Var(slot, "desc", description);
			pen.Var(slot, "tip", own
				? Text(player, "hud.tip.showcase-own", description, Number(shown[i].Points))
				: Text(player, "hud.tip.unlocked", description, Number(shown[i].Points)));
			pen.Swap($"{slot}-ring", "tier", MedalTier(shown[i]));
			SetPicture(pen, $"{slot}-icon", AchievementIcon(shown[i].Type));
		}
		pen.Var(view, "hint", Text(player, own ? "hud.pf-showcase-hint" : "hud.pf-showcase-none"));
		pen.Show("k4-pf-sc-hint", achievements is not null && shown.Count == 0);

		// The stats, which were their own page before 8.0.
		List<(string Label, string Value, string Icon)> tiles = [];
		if (profile.Stats is int[] stats)
		{
			int kills = stats[(int)StatField.Kills];
			int deaths = stats[(int)StatField.Deaths];
			int shots = stats[(int)StatField.Shoots];
			int rounds = stats[(int)StatField.RoundWin] + stats[(int)StatField.RoundLose];
			tiles.Add((Text(player, "hud.stat.kills"), Number(kills), "ic-kill"));
			tiles.Add((Text(player, "hud.stat.deaths"), Number(deaths), "ic-map_death"));
			tiles.Add((Text(player, "hud.stat.kd"), StatCounters.KillDeathRatio(kills, deaths).ToString("0.00", invariant), "ic-stats"));
			tiles.Add((Text(player, "hud.stat.headshots"), $"{StatService.HeadshotPercentage(stats[(int)StatField.Headshots], kills).ToString("0", invariant)}%", "ic-stat_headshots"));
			tiles.Add((Text(player, "hud.stat.assists"), Number(stats[(int)StatField.Assists]), "ic-stat_assists"));
			tiles.Add((Text(player, "hud.stat.mvps"), Number(stats[(int)StatField.Mvp]), "ic-stat_mvps"));
			tiles.Add((Text(player, "hud.stat.accuracy"), $"{(shots > 0 ? stats[(int)StatField.HitsGiven] * 100.0 / shots : 0).ToString("0", invariant)}%", "ic-crosshair_circle"));
			tiles.Add((Text(player, "hud.stat.winrate"), $"{(rounds > 0 ? stats[(int)StatField.RoundWin] * 100.0 / rounds : 0).ToString("0", invariant)}%", "ic-trophy"));
		}
		for (int i = 0; i < ProfileTiles; i++)
		{
			string tile = $"k4-pf-st{i}";
			pen.Show(tile, i < tiles.Count);
			if (i >= tiles.Count)
				continue;
			pen.Var(tile, "label", tiles[i].Label);
			pen.Var(tile, "value", tiles[i].Value);
			pen.Var(tile, "tip", tiles[i].Label);
			SetPicture(pen, $"{tile}-icon", tiles[i].Icon);
		}

		// The top three weapons under a plain caption: the first row already names the favorite and its kills.
		(string Weapon, int Kills)? favorite = profile.Stats is null ? null : PlayerProfile.Favorite(profile.WeaponKills);
		pen.Var(view, "fav", favorite is null ? "" : Text(player, "hud.pf-favorite"));
		pen.Var(view, "favkills", "");
		List<KeyValuePair<string, int>> weapons = profile.Stats is null ? [] : [.. profile.WeaponKills
			.Where(pair => pair.Value > 0)
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key, StringComparer.Ordinal)
			.Take(WeaponBars)];
		for (int i = 0; i < WeaponBars; i++)
		{
			string row = $"k4-pf-sw{i}";
			pen.Show(row, i < weapons.Count);
			if (i >= weapons.Count)
				continue;
			SetPicture(pen, $"{row}-icon", WeaponIcon(weapons[i].Key));
			pen.Var(row, "weapon", WeaponCatalog.DisplayName(weapons[i].Key));
			pen.Var(row, "kills", Number(weapons[i].Value));
			pen.Bar($"{row}-bar", weapons[i].Value, weapons[0].Value);
		}

		// Playtime per side.
		pen.Show("k4-pf-pt", profile.Playtime is not null);
		if (profile.Playtime is int[] seconds)
		{
			int all = seconds[(int)PlaytimeField.All];
			pen.Var(view, "total", playtime.Format(all, StatTimeUnits));
			foreach ((string id, PlaytimeField field, string key) in new[] { ("k4-pf-pt-t", PlaytimeField.T, "hud.team.t"), ("k4-pf-pt-ct", PlaytimeField.Ct, "hud.team.ct"), ("k4-pf-pt-spec", PlaytimeField.Spec, "hud.team.spec") })
			{
				pen.Var(id, "name", Text(player, key));
				pen.Var(id, "time", playtime.Format(seconds[(int)field], StatTimeUnits));
				pen.Bar($"{id}-bar", seconds[(int)field], all);
			}
		}
		else
			pen.Var(view, "total", "");

		// Finished seasons.
		bool showSeasons = seasons is not null && ranks is not null;
		CultureInfo language = player.Controller.GetLanguage();
		pen.Var(view, "none", Text(player, "hud.pf-seasons-none"));
		pen.Show("k4-pf-ss-none", showSeasons && profile.Seasons.Count == 0);
		for (int i = 0; i < SeasonRows; i++)
		{
			string slot = $"k4-pf-ss{i}";
			bool has = showSeasons && i < profile.Seasons.Count;
			pen.Show(slot, has);
			if (!has)
				continue;
			SeasonResult result = profile.Seasons[i];
			RankDefinition final = ranks!.Ladder.FindByName(result.Rank) ?? ranks.Ladder.ResolveIgnoringPermissions(result.Points);
			SetEmblem(pen, $"{slot}-icon", final);
			pen.Var(slot, "season", SeasonClock.Name(result.Season, language));
			pen.Var(slot, "pos", PlaceText(player, result.Placement, result.Players));
		}
	}

	// ---------------------------------------------------------------- settings

	/// <summary>The player has a tag from their permissions, which the tag row turns off and on (!toggletag).</summary>
	private bool HasPermissionTag(K4Player player)
		=> ranks is not null && player.Rank is not null && config.Tags.Admins.Exists(admin => admin.Tag is not null && Permissions.Has(player, admin.Permission));

	private bool HandleSettingsClick(K4Player player, HudState state, string id)
	{
		switch (id)
		{
			case "k4-set-mode-panel":
				return SetMode(player, state, ToastMode.Panel);
			case "k4-set-mode-chat":
				return SetMode(player, state, ToastMode.Chat);
			case "k4-set-mode-both":
				return SetMode(player, state, ToastMode.Both);
			case "k4-set-points-on" or "k4-set-points-off" when player.Rank is RankState rank:
				bool on = id == "k4-set-points-on";
				if (rank.MuteMessages == !on)
					return false;
				settings.SetPointMessages(player, on);
				Say(player, state, true, "toggle.point-messages", Text(player, on ? "hud.on" : "hud.off"));
				return true;
			case "k4-set-tag-on" or "k4-set-tag-off" when HasPermissionTag(player):
				bool shown = id == "k4-set-tag-on";
				if (player.Rank!.HideAdminTag == !shown)
					return false;
				player.Rank.HideAdminTag = !shown;
				ranks!.RefreshClanTag(player);
				Say(player, state, true, "toggle.tag", Text(player, shown ? "hud.on" : "hud.off"));
				return true;
			default:
				return false;
		}
	}

	private bool SetMode(K4Player player, HudState state, ToastMode mode)
	{
		if (player.ToastMode == mode)
			return false;
		string name = Text(player, $"toast-mode.{mode.ToString().ToLowerInvariant()}");
		if (settings.SetToastMode(player, mode))
			Say(player, state, true, "toggle.toasts", name);
		else
			Say(player, state, false, "toggle.toasts-locked", Text(player, $"toast-mode.{player.ToastMode.ToString().ToLowerInvariant()}"));
		return true;
	}

	private void DrawSettings(K4Player player)
	{
		const string view = "k4-view-settings";
		HudPen pen = Pen(player);
		pen.Var(view, "title", Text(player, "hud.page.settings"));
		pen.Var(view, "mode", Text(player, "hud.set-mode"));
		pen.Var(view, "points", Text(player, "hud.set-points"));
		pen.Var(view, "tag", Text(player, "hud.set-tag"));

		bool locked = !config.Panorama.Toasts.PlayersCanChange;
		foreach ((string id, ToastMode mode, string key) in new[] { ("k4-set-mode-panel", ToastMode.Panel, "toast-mode.panel"), ("k4-set-mode-chat", ToastMode.Chat, "toast-mode.chat"), ("k4-set-mode-both", ToastMode.Both, "hud.set-both") })
		{
			pen.Var(id, "label", Text(player, key));
			pen.Flag(id, "k4-active", player.ToastMode == mode);
			pen.Flag(id, "k4-disabled", locked);
		}
		pen.Show("k4-set-mode-lock", locked);
		if (locked)
			pen.Var("k4-set-mode-lock", "lock", Text(player, "hud.set-lock"));

		bool points = ranks is not null && player.Rank is not null;
		pen.Show("k4-set-points", points);
		if (points)
		{
			bool on = !player.Rank!.MuteMessages;
			pen.Var("k4-set-points-on", "label", Text(player, "hud.on"));
			pen.Var("k4-set-points-off", "label", Text(player, "hud.off"));
			pen.Flag("k4-set-points-on", "k4-active", on);
			pen.Flag("k4-set-points-off", "k4-active", !on);
		}

		bool tag = HasPermissionTag(player);
		pen.Show("k4-set-tag", tag);
		if (tag)
		{
			bool on = !player.Rank!.HideAdminTag;
			pen.Var("k4-set-tag-on", "label", Text(player, "hud.on"));
			pen.Var("k4-set-tag-off", "label", Text(player, "hud.off"));
			pen.Flag("k4-set-tag-on", "k4-active", on);
			pen.Flag("k4-set-tag-off", "k4-active", !on);
		}
	}

	// ---------------------------------------------------------------- toasts

	/// <summary>
	/// Shows a toast (docs/panel-contract.md). With the window closed: newest on top, at most three at once. With the
	/// window open: one under it, the rest wait and show one after another. Players who chose chat get nothing.
	/// </summary>
	public void Toast(K4Player player, Toast toast)
	{
		if (player.ToastsOff || !Usable(player))
			return;

		if (!toasts.TryGetValue(player.Slot, out List<ToastEntry>? stack))
			toasts[player.Slot] = stack = [];

		stack.Insert(0, new ToastEntry(toast));
		DrawToasts(player);
	}

	/// <summary>The 6.x toast call: a title key, one line of text and a kind.</summary>
	public void ShowToast(K4Player player, string titleKey, string text, ToastKind kind, string? icon = null)
		=> Toast(player, new Toast(Text(player, titleKey), StripColors(text), "", icon ?? kind switch
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

	/// <summary>Arranges the player's toasts for the window's state, draws them, and starts the timers of the ones that just appeared.</summary>
	private void DrawToasts(K4Player player)
	{
		List<ToastEntry> stack = toasts.GetValueOrDefault(player.Slot) ?? [];
		bool windowOpen = IsOpen(player);
		(List<ToastEntry> visible, List<ToastEntry> started) = ToastRules.Arrange(stack, windowOpen ? ToastRules.OpenSlots : ToastRules.ClosedSlots);
		int waiting = stack.Count - visible.Count;
		HudPen pen = Pen(player);
		List<int> restart = PaintToasts(pen, visible, started, waiting, windowOpen);

		// The timer bar starts over the frame after its class was switched off.
		foreach (int slot in restart)
		{
			string timer = $"k4-t{slot}-timer";
			CCSPlayerController controller = player.Controller;
			Server.NextFrame(() =>
			{
				if (layout is { IsValid: true } && controller.IsValid)
					Pen(sent, controller).Flag(timer, "k4-run", true);
			});
		}

		foreach (ToastEntry entry in started)
			ScheduleLeave(player, entry);
	}

	private void ScheduleLeave(K4Player player, ToastEntry entry)
	{
		int run = entry.Run;
		plugin.AddTimer(global::K4System.Hud.Toast.Seconds(entry.Toast.Length), () =>
		{
			if (!gameThread.IsActive || entry.Run != run || !entry.Showing || !toasts.TryGetValue(player.Slot, out List<ToastEntry>? current)
				|| !current.Contains(entry) || layout is not { IsValid: true } || !player.HasController)
				return;

			// Slide out first, then free the slot for the next one.
			entry.Leaving = true;
			DrawToasts(player);
			plugin.AddTimer(LeaveSeconds, () =>
			{
				if (!gameThread.IsActive || entry.Run != run || !toasts.TryGetValue(player.Slot, out List<ToastEntry>? now) || !now.Remove(entry))
					return;
				if (layout is { IsValid: true } && player.HasController)
					DrawToasts(player);
			}, TimerFlags.STOP_ON_MAPCHANGE);
		}, TimerFlags.STOP_ON_MAPCHANGE);
	}

	/// <summary>
	/// Draws the toast slots: <paramref name="visible"/> newest first, a "+N" line for the waiting ones, and k4-open
	/// while the window is open. Returns the slots whose timer bar must start over on the next frame.
	/// </summary>
	internal static List<int> PaintToasts(HudPen pen, IReadOnlyList<ToastEntry> visible, IReadOnlyCollection<ToastEntry> started, int waiting, bool windowOpen)
	{
		pen.Flag("k4-toasts", "k4-open", windowOpen);
		List<int> restart = [];
		for (int i = 0; i < global::K4System.Hud.Toast.Slots; i++)
		{
			string slot = $"k4-t{i}";
			if (i >= visible.Count)
			{
				pen.Show(slot, false);
				continue;
			}

			// The class follows the toast, so a toast leaving while a new one pushes it down keeps sliding out.
			ToastEntry entry = visible[i];
			Toast toast = entry.Toast;
			pen.Flag(slot, "k4-out", entry.Leaving);
			pen.Var(slot, "kind", toast.Kind);
			pen.Var(slot, "text", StripColors(toast.Text));
			pen.Var(slot, "value", toast.Value);
			pen.Swap(slot, "toast", $"k4-tc-{toast.Color.ToString().ToLowerInvariant()}");
			pen.Swap(slot, "length", $"k4-len-{toast.Length.ToString().ToLowerInvariant()}");
			SetPicture(pen, $"{slot}-icon", toast.Visual);
			pen.Set($"{slot}-ring", "k4-gone", toast.Tier is null);
			pen.Flag(slot, "k4-wide", toast.Tier is null);
			if (toast.Tier is string tier)
				pen.Swap($"{slot}-ring", "tier", $"t-{tier}");
			pen.Set($"{slot}-prog", "k4-gone", toast.BarPercent is null);
			if (toast.BarPercent is int percent)
				pen.Bar($"{slot}-bar", percent, 100);
			pen.Show(slot, true);

			// shortcut: a toast that moves to another slot keeps that slot's timer bar where it was; only new toasts restart it.
			if (started.Contains(entry))
			{
				pen.Flag($"{slot}-timer", "k4-run", false);
				restart.Add(i);
			}
		}

		pen.Show("k4-t-more", waiting > 0);
		if (waiting > 0)
			pen.Var("k4-t-more", "more", $"+{waiting}");
		return restart;
	}

	/// <summary>The large promotion banner at the top of the screen.</summary>
	public void Promote(K4Player player, RankDefinition rank)
	{
		if (player.ToastsOff || !Usable(player))
			return;

		HudPen pen = Pen(player);
		int version = promotions[player.Slot] = NextVersion();
		pen.Flag("k4-promo", "k4-out", false);
		pen.Var("k4-promo", "title", Text(player, "hud.toast.promoted"));
		pen.Var("k4-promo", "rank", rank.Name);
		SetEmblem(pen, "k4-promo-icon", rank);
		pen.Show("k4-promo", true);

		plugin.AddTimer(PromotionSeconds, () =>
		{
			if (!gameThread.IsActive || promotions.GetValueOrDefault(player.Slot) != version || layout is not { IsValid: true } || !player.HasController)
				return;

			Pen(player).Flag("k4-promo", "k4-out", true);
			plugin.AddTimer(LeaveSeconds, () =>
			{
				if (gameThread.IsActive && promotions.GetValueOrDefault(player.Slot) == version && layout is { IsValid: true } && player.HasController)
				{
					HudPen later = Pen(player);
					later.Show("k4-promo", false);
					later.Flag("k4-promo", "k4-out", false);
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

	/// <summary>On unload: release every player's mouse, put every slot back to the layout's defaults, and remove the entity.</summary>
	public void Shutdown()
	{
		if (layout is { IsValid: true })
		{
			foreach (CCSPlayerController controller in Utilities.GetPlayers())
			{
				if (controller.IsValid && !controller.IsBot)
				{
					Hide(controller);
					sent.Reset(controller.Slot, controller);
				}
			}

			layout.Remove();
		}

		layout = null;
		open.Clear();
		toasts.Clear();
		sent.Forget();
	}
}

/// <summary>
/// Where the panel's Back button goes: the views the player came from, newest last, at most <see cref="Max"/>.
/// Pure, so it is tested without the game.
/// </summary>
internal sealed class BackHistory<T>
{
	public const int Max = 16;

	private readonly List<T> views = [];

	public int Count => views.Count;

	public void Push(T view)
	{
		views.Add(view);
		if (views.Count > Max)
			views.RemoveAt(0);
	}

	public bool TryPop([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T view)
	{
		if (views.Count == 0)
		{
			view = default;
			return false;
		}
		view = views[^1];
		views.RemoveAt(views.Count - 1);
		return true;
	}

	public void Clear() => views.Clear();
}

/// <summary>
/// Lets rank and mission code send toasts without depending on the panel being built first.
/// Null when the Panorama panel is off; callers then use chat.
/// </summary>
internal sealed class HudBridge
{
	public PanoramaHud? Hud { get; set; }
}
