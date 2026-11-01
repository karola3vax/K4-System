namespace K4System.Tests.Domain;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using K4System.Hud;

/// <summary>
/// The Panorama files ship in the workshop addon and cannot be tested in-game here, so these tests check the
/// layout and stylesheet against docs/panel-contract.md: every id, dialog variable and class the server uses.
/// The lists are literal on purpose: they are the contract, not whatever the server code happens to hold.
/// </summary>
public partial class PanoramaLayoutTests
{
	private static string AddonRoot => Path.Combine(TestData.RepositoryRoot(), "panorama", "k4system");

	private static XDocument Layout => XDocument.Load(Path.Combine(AddonRoot, "panorama", "layout", "custom_game", "k4_ui.xml"));

	private static string Stylesheet => File.ReadAllText(Path.Combine(AddonRoot, "panorama", "styles", "custom_game", "k4_ui.css"));

	private static Dictionary<string, XElement> ById => Layout.Descendants()
		.Where(element => element.Attribute("id") is not null)
		.ToDictionary(element => (string)element.Attribute("id")!);

	private static string[] Classes(XElement element) => ((string?)element.Attribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

	private static IEnumerable<string> Range(string pattern, int count) => Enumerable.Range(0, count).Select(i => pattern.Replace("{i}", i.ToString()));

	private static readonly string[] Pages = ["home", "profile", "missions", "achievements", "leaderboard", "ranks", "stats"];

	private static List<string> ContractIds()
	{
		List<string> ids =
		[
			// Frame
			"k4-panel", "k4-bg", "k4-bg-map", "k4-me", "k4-me-icon", "k4-season", "k4-season-bar", "k4-event", "k4-close",
			// Home
			"k4-h-rank-icon", "k4-h-rank-name", "k4-h-bar", "k4-h-missions", "k4-h-ach", "k4-h-top",
			// Profile
			"k4-pf-icon", "k4-pf-badge", "k4-pf-rank", "k4-pf-sc-hint", "k4-pf-fav", "k4-pf-fav-icon", "k4-pf-ss-none",
			"k4-pf-back", "k4-pf-toasts", "k4-pf-chat",
			// Missions
			"k4-mw", "k4-mw-icon", "k4-mw-bar",
			// Achievements
			"k4-af-all", "k4-af-progress", "k4-af-unlocked", "k4-af-shown", "k4-ach-prev", "k4-ach-next",
			// Leaderboard
			"k4-lb-newer", "k4-lb-older", "k4-lb-prev", "k4-lb-next", "k4-lb-me", "k4-lb-me-icon",
			// Ranks
			"k4-rk-prev", "k4-rk-next",
			// Stats
			"k4-pt", "k4-pt-t", "k4-pt-ct", "k4-pt-spec", "k4-pt-t-bar", "k4-pt-ct-bar", "k4-pt-spec-bar",
			// Toasts
			"k4-toasts", "k4-t-more", "k4-promo", "k4-promo-icon",
		];
		ids.AddRange(Pages.Select(page => $"k4-nav-{page}"));
		ids.AddRange(Pages.Select(page => $"k4-view-{page}"));
		foreach (string pattern in new[] { "k4-h-m{i}", "k4-h-m{i}-icon" })
			ids.AddRange(Range(pattern, 4));
		foreach (string pattern in new[] { "k4-h-a{i}", "k4-h-a{i}-ring", "k4-h-a{i}-icon", "k4-h-a{i}-bar", "k4-h-p{i}", "k4-h-p{i}-icon" })
			ids.AddRange(Range(pattern, 3));
		foreach (string pattern in new[] { "k4-h-q{i}", "k4-h-q{i}-icon" })
			ids.AddRange(Range(pattern, 5));
		foreach (string pattern in new[] { "k4-pf-sc{i}", "k4-pf-sc{i}-ring", "k4-pf-sc{i}-icon", "k4-pf-ss{i}", "k4-pf-ss{i}-icon" })
			ids.AddRange(Range(pattern, 3));
		foreach (string pattern in new[] { "k4-pf-st{i}", "k4-pf-st{i}-icon" })
			ids.AddRange(Range(pattern, 6));
		foreach (string pattern in new[] { "k4-m{i}", "k4-m{i}-icon", "k4-m{i}-bar", "k4-reroll-{i}" })
			ids.AddRange(Range(pattern, 3));
		ids.AddRange(Range("k4-sd{i}", 7));
		foreach (string pattern in new[] { "k4-a{i}", "k4-a{i}-ring", "k4-a{i}-icon", "k4-a{i}-bar" })
			ids.AddRange(Range(pattern, 15));
		foreach (string pattern in new[] { "k4-lb-p{i}", "k4-lb-p{i}-icon" })
			ids.AddRange(Range(pattern, 3));
		foreach (string pattern in new[] { "k4-lb-{i}", "k4-lb-{i}-icon" })
			ids.AddRange(Range(pattern, 7));
		foreach (string pattern in new[] { "k4-rk{i}", "k4-rk{i}-name", "k4-rk{i}-icon", "k4-rk{i}-gift", "k4-rk{i}-you" })
			ids.AddRange(Range(pattern, 20));
		foreach (string pattern in new[] { "k4-st{i}", "k4-st{i}-icon" })
			ids.AddRange(Range(pattern, 8));
		foreach (string pattern in new[] { "k4-sw{i}", "k4-sw{i}-icon", "k4-sw{i}-bar" })
			ids.AddRange(Range(pattern, 3));
		foreach (string pattern in new[] { "k4-t{i}", "k4-t{i}-icon", "k4-t{i}-ring", "k4-t{i}-prog", "k4-t{i}-bar", "k4-t{i}-timer" })
			ids.AddRange(Range(pattern, 3));
		return ids;
	}

	private static List<string> ContractButtons()
	{
		List<string> buttons =
		[
			"k4-me", "k4-close", "k4-h-missions", "k4-h-ach", "k4-h-top", "k4-pf-back", "k4-pf-toasts", "k4-pf-chat",
			"k4-af-all", "k4-af-progress", "k4-af-unlocked", "k4-af-shown", "k4-ach-prev", "k4-ach-next",
			"k4-lb-newer", "k4-lb-older", "k4-lb-prev", "k4-lb-next", "k4-rk-prev", "k4-rk-next",
		];
		buttons.AddRange(Pages.Select(page => $"k4-nav-{page}"));
		buttons.AddRange(Range("k4-h-q{i}", 5));
		buttons.AddRange(Range("k4-reroll-{i}", 3));
		buttons.AddRange(Range("k4-a{i}", 15));
		buttons.AddRange(Range("k4-lb-p{i}", 3));
		buttons.AddRange(Range("k4-lb-{i}", 7));
		return buttons;
	}

	// Players update the addon before or after their server: an older server never touches what a newer one
	// fills, so those parts must start hidden, or an older server would show them empty.
	private static List<string> ContractStartsGone()
	{
		List<string> gone =
		[
			"k4-season", "k4-event", "k4-pf-sc-hint", "k4-pf-fav", "k4-pf-ss-none", "k4-pf-back", "k4-pf-toasts", "k4-pf-chat",
			"k4-mw", "k4-lb-me", "k4-rk-prev", "k4-rk-next", "k4-pt", "k4-t-more", "k4-promo",
		];
		gone.AddRange(Range("k4-h-m{i}", 4));
		gone.AddRange(Range("k4-h-a{i}", 3));
		gone.AddRange(Range("k4-h-p{i}", 3));
		gone.AddRange(Range("k4-h-q{i}", 5));
		gone.AddRange(Range("k4-pf-sc{i}", 3));
		gone.AddRange(Range("k4-pf-ss{i}", 3));
		gone.AddRange(Range("k4-m{i}", 3));
		gone.AddRange(Range("k4-reroll-{i}", 3));
		gone.AddRange(Range("k4-a{i}", 15));
		gone.AddRange(Range("k4-lb-p{i}", 3));
		gone.AddRange(Range("k4-lb-{i}", 7));
		gone.AddRange(Range("k4-rk{i}", 20));
		gone.AddRange(Range("k4-rk{i}-gift", 20));
		gone.AddRange(Range("k4-rk{i}-you", 20));
		gone.AddRange(Range("k4-st{i}", 8));
		gone.AddRange(Range("k4-sw{i}", 3));
		gone.AddRange(Range("k4-t{i}", 3));
		return gone;
	}

	[Fact]
	public void Layout_path_matches_the_addon_file()
	{
		Assert.Equal("panorama/layout/custom_game/k4_ui.xml", PanoramaHud.LayoutPath);
		Assert.True(File.Exists(Path.Combine(AddonRoot, PanoramaHud.LayoutPath.Replace('/', Path.DirectorySeparatorChar))));
	}

	[Fact]
	public void Layout_ids_are_unique()
	{
		List<string> ids = Layout.Descendants().Select(element => (string?)element.Attribute("id")).OfType<string>().ToList();
		List<string> twice = ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
		Assert.True(twice.Count == 0, $"Used more than once: {string.Join(", ", twice)}");
	}

	[Fact]
	public void Every_id_in_the_contract_exists()
	{
		Dictionary<string, XElement> ids = ById;
		List<string> missing = ContractIds().Where(id => !ids.ContainsKey(id)).ToList();
		Assert.True(missing.Count == 0, $"Missing in k4_ui.xml: {string.Join(", ", missing)}");
	}

	[Fact]
	public void Every_clickable_id_is_a_button()
	{
		Dictionary<string, XElement> ids = ById;
		List<string> wrong = ContractButtons().Where(id => ids[id].Name.LocalName != "Button").ToList();
		Assert.True(wrong.Count == 0, $"Not a Button: {string.Join(", ", wrong)}");
	}

	[Fact]
	public void Parts_newer_servers_fill_start_hidden()
	{
		Dictionary<string, XElement> ids = ById;
		List<string> shown = ContractStartsGone().Where(id => !Classes(ids[id]).Contains("k4-gone")).ToList();
		Assert.True(shown.Count == 0, $"Does not start with k4-gone: {string.Join(", ", shown)}");
	}

	// The server assumes these classes are already set, and removes them the first time it swaps.
	[Theory]
	[InlineData("k4-panel", "k4-closed")]
	[InlineData("k4-bg", "map-default")]
	[InlineData("k4-me-icon", "sg-0")]
	[InlineData("k4-h-rank-icon", "sg-0")]
	[InlineData("k4-pf-icon", "sg-0")]
	[InlineData("k4-promo-icon", "sg-0")]
	[InlineData("k4-h-bar", "w0")]
	[InlineData("k4-season-bar", "w0")]
	[InlineData("k4-t0", "k4-tc-green")]
	[InlineData("k4-t0", "k4-len-normal")]
	public void Layout_starts_with_the_contract_defaults(string id, string expected)
		=> Assert.Contains(expected, Classes(ById[id]));

	[Fact]
	public void Repeated_parts_start_with_the_contract_defaults()
	{
		Dictionary<string, XElement> ids = ById;
		List<(string Id, string Class)> expected = [];
		expected.AddRange(Range("k4-h-p{i}-icon", 3).Concat(Range("k4-lb-p{i}-icon", 3)).Concat(Range("k4-lb-{i}-icon", 7))
			.Concat(Range("k4-rk{i}-icon", 20)).Concat(Range("k4-pf-ss{i}-icon", 3)).Append("k4-lb-me-icon").Select(id => (id, "sg-0")));
		expected.AddRange(Range("k4-h-a{i}-ring", 3).Concat(Range("k4-pf-sc{i}-ring", 3)).Concat(Range("k4-a{i}-ring", 15))
			.Concat(Range("k4-t{i}-ring", 3)).Select(id => (id, "t-locked")));
		// Every bar in the contract is an id ending in -bar, and its fill starts empty.
		expected.AddRange(ContractIds().Where(id => id.EndsWith("-bar", StringComparison.Ordinal)).Select(id => (id, "w0")));
		expected.AddRange(Range("k4-t{i}", 3).SelectMany(id => new[] { (id, "k4-tc-green"), (id, "k4-len-normal") }));

		List<string> wrong = expected.Where(item => !Classes(ids[item.Id]).Contains(item.Class)).Select(item => $"{item.Id} ({item.Class})").ToList();
		Assert.True(wrong.Count == 0, $"Missing the default class: {string.Join(", ", wrong)}");
	}

	// k4-bg always draws the fallback; the server puts the map on k4-bg-map, so a map without a screenshot
	// (a class the stylesheet does not define) draws nothing and the fallback shows through.
	[Fact]
	public void The_map_background_sits_inside_the_fallback_without_a_map_class()
	{
		XElement map = ById["k4-bg-map"];
		Assert.Equal("k4-bg", (string?)map.Parent!.Attribute("id"));
		Assert.DoesNotContain(Classes(map), name => name.StartsWith("map-", StringComparison.Ordinal));
		Assert.Contains(".k4-shade", Stylesheet);
		// The dark gradient is drawn after (above) the background.
		List<XElement> panel = ById["k4-panel"].Elements().ToList();
		Assert.True(panel.FindIndex(element => Classes(element).Contains("k4-shade")) > panel.FindIndex(element => (string?)element.Attribute("id") == "k4-bg"));
	}

	[Theory]
	[InlineData("k4-me", "name", "meta")]
	[InlineData("k4-season", "title", "left")]
	[InlineData("k4-event", "event")]
	[InlineData("k4-view-home", "points", "place", "next")]
	[InlineData("k4-h-rank-name", "rank")]
	[InlineData("k4-h-m0", "value")]
	[InlineData("k4-h-a0", "name")]
	[InlineData("k4-h-p0", "name", "pos")]
	[InlineData("k4-h-q0", "value")]
	[InlineData("k4-view-profile", "name", "points", "place", "hint", "fav", "favkills", "none")]
	[InlineData("k4-pf-badge", "tag")]
	[InlineData("k4-pf-rank", "rank")]
	[InlineData("k4-pf-sc0", "name")]
	[InlineData("k4-pf-sc-hint", "hint")]
	[InlineData("k4-pf-st0", "value", "label")]
	[InlineData("k4-pf-ss0", "season", "pos")]
	[InlineData("k4-pf-ss-none", "none")]
	[InlineData("k4-pf-back", "back")]
	[InlineData("k4-pf-toasts", "label")]
	[InlineData("k4-pf-chat", "label")]
	[InlineData("k4-view-missions", "reset", "streak", "bonus")]
	[InlineData("k4-m0", "diff", "reward", "desc", "progress")]
	[InlineData("k4-reroll-0", "label")]
	[InlineData("k4-mw", "left", "desc", "progress", "reward")]
	[InlineData("k4-view-achievements", "count", "page")]
	[InlineData("k4-af-all", "label")]
	[InlineData("k4-af-shown", "label")]
	[InlineData("k4-a0", "name", "goal")]
	[InlineData("k4-view-leaderboard", "season", "status", "page")]
	[InlineData("k4-lb-p0", "name", "points", "pos")]
	[InlineData("k4-lb-0", "pos", "name", "points")]
	[InlineData("k4-lb-me", "pos", "name", "points")]
	[InlineData("k4-view-ranks", "page")]
	[InlineData("k4-rk0", "name", "points")]
	[InlineData("k4-rk0-name", "name")]
	[InlineData("k4-view-stats", "total")]
	[InlineData("k4-st0", "value", "label")]
	[InlineData("k4-sw0", "kills")]
	[InlineData("k4-pt-t", "name", "time")]
	[InlineData("k4-pt-spec", "name", "time")]
	[InlineData("k4-t0", "kind", "text", "value")]
	[InlineData("k4-t-more", "more")]
	[InlineData("k4-promo", "title", "rank")]
	public void Dialog_variables_reach_a_label_inside_their_panel(string id, params string[] variables)
	{
		// A dialog variable set on a panel reaches every label inside it (and the label itself).
		XElement panel = ById[id];
		HashSet<string> texts = panel.DescendantsAndSelf("Label").Select(label => (string?)label.Attribute("text")).OfType<string>().ToHashSet();
		List<string> missing = variables.Where(variable => !texts.Contains($"{{s:{variable}}}")).ToList();
		Assert.True(missing.Count == 0, $"{id} has no label for: {string.Join(", ", missing)}");
	}

	[Fact]
	public void Every_nav_button_has_its_label_and_its_view()
	{
		Dictionary<string, XElement> ids = ById;
		foreach (string page in Pages)
		{
			Assert.Contains(ids[$"k4-nav-{page}"].Descendants("Label"), label => (string?)label.Attribute("text") == "{s:label}");
			Assert.Contains("k4-view", Classes(ids[$"k4-view-{page}"]));
		}
	}

	[Fact]
	public void Medal_icons_and_bars_sit_inside_their_parts()
	{
		Dictionary<string, XElement> ids = ById;
		List<(string Inner, string Outer)> pairs = [];
		pairs.AddRange(Enumerable.Range(0, 15).SelectMany(i => new[] { ($"k4-a{i}-icon", $"k4-a{i}-ring"), ($"k4-a{i}-ring", $"k4-a{i}"), ($"k4-a{i}-bar", $"k4-a{i}") }));
		pairs.AddRange(Enumerable.Range(0, 3).SelectMany(i => new[] { ($"k4-h-a{i}-icon", $"k4-h-a{i}-ring"), ($"k4-pf-sc{i}-icon", $"k4-pf-sc{i}-ring") }));
		pairs.AddRange(Enumerable.Range(0, 3).SelectMany(i => new[] { ($"k4-t{i}-bar", $"k4-t{i}-prog"), ($"k4-t{i}-timer", $"k4-t{i}"), ($"k4-t{i}-icon", $"k4-t{i}") }));
		pairs.AddRange(Enumerable.Range(0, 20).Select(i => ($"k4-rk{i}-gift", $"k4-rk{i}")));
		pairs.Add(("k4-season-bar", "k4-season"));
		pairs.Add(("k4-me-icon", "k4-me"));

		List<string> wrong = pairs.Where(pair => !ids[pair.Inner].Ancestors().Contains(ids[pair.Outer])).Select(pair => $"{pair.Inner} not in {pair.Outer}").ToList();
		Assert.True(wrong.Count == 0, string.Join(", ", wrong));
		// The toast icon must not be inside the ring: a toast without a medal hides only the ring.
		Assert.All(Enumerable.Range(0, 3), i => Assert.DoesNotContain(ids[$"k4-t{i}-ring"], ids[$"k4-t{i}-icon"].Ancestors()));
	}

	// The root, the toast stack and the banner cover the screen above every other HUD; they must let the mouse
	// through, or they take the hover from other plugins' mouse menus. The panel itself captures the mouse.
	[Fact]
	public void Only_the_panel_takes_the_mouse()
	{
		XElement root = Layout.Root!.Elements("Panel").Single();
		Assert.Equal("false", (string?)root.Attribute("hittest"));
		foreach (string id in new[] { "k4-toasts", "k4-promo" })
		{
			XElement part = ById[id];
			Assert.Equal("false", (string?)part.Attribute("hittest"));
			Assert.All(part.Descendants(), element => Assert.Equal("false", (string?)element.Attribute("hittest")));
		}
		Assert.Null(ById["k4-panel"].Attribute("hittest"));
	}

	[Fact]
	public void The_panel_covers_the_full_screen()
	{
		string rule = Rule("k4-panel");
		Assert.Contains("width: 100%;", rule, StringComparison.Ordinal);
		Assert.Contains("height: 100%;", rule, StringComparison.Ordinal);
	}

	[Fact]
	public void The_sidebar_carries_the_copyright()
	{
		XElement copyright = Layout.Descendants("Label").Single(label => (string?)label.Attribute("text") == "© 2026 karola3vax");
		Assert.Contains(copyright.Ancestors(), element => Classes(element).Contains("k4-sidebar"));
		string rule = Rule("k4-copyright");
		Assert.Contains("font-size: 18px;", rule, StringComparison.Ordinal);
		Assert.Contains("horizontal-align: center;", rule, StringComparison.Ordinal);
		Assert.Contains("stratum-bold-tf", Classes(copyright));
	}

	[Fact]
	public void Every_class_group_in_the_contract_is_defined()
	{
		List<string> classes =
		[
			"tp-t", "tp-ct", "ic-watch", "t-gold", "t-silver", "t-bronze", "t-locked", "map-default",
			"k4-tc-green", "k4-tc-gold", "k4-tc-red", "k4-tc-blue", "k4-tc-purple", "k4-len-short", "k4-len-normal", "k4-len-long",
			"k4-closed", "k4-show", "k4-gone", "k4-active", "k4-done", "k4-disabled", "k4-current", "k4-pinned", "k4-on", "k4-today",
			"k4-easy", "k4-medium", "k4-hard", "k4-run",
			// Pictures the design uses
			"ic-icon_star", "ic-gift", "dn-penetrate", "dn-noscope", "ic-kill", "ic-stat_headshots", "ic-stat_mvps", "ic-stat_assists",
			"ic-clock", "ic-hot_trend", "ic-check", "ic-map_death", "ic-crosshair_circle", "ic-stats", "ic-trophy", "ic-leader",
			"ic-winner_ribbon", "ic-quest", "ic-home", "ic-profile", "ic-refresh_skinny", "eq-ak47", "eq-awp", "eq-knife",
			// Maps CS2 has screenshots for
			"map-de_dust2", "map-de_mirage", "map-de_inferno", "map-de_nuke", "map-de_ancient", "map-de_anubis", "map-de_overpass",
			"map-de_train", "map-de_vertigo", "map-cs_office", "map-cs_italy",
		];
		classes.AddRange(Enumerable.Range(0, 21).Select(step => $"w{step}"));
		classes.AddRange(Enumerable.Range(0, 19).SelectMany(number => new[] { $"sg-{number}", $"wg-{number}" }));
		classes.AddRange(new[] { "default", "white", "darkred", "green", "lightyellow", "lightblue", "olive", "lime", "red", "lightpurple",
			"purple", "grey", "yellow", "gold", "silver", "blue", "darkblue", "bluegrey", "magenta", "lightred", "orange" }.Select(color => $"col-{color}"));

		string css = Stylesheet;
		List<string> missing = classes.Where(name => !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}(?![\w-])")).ToList();
		Assert.True(missing.Count == 0, $"Missing in k4_ui.css: {string.Join(", ", missing)}");
	}

	// Every class the layout starts with is styled, so a typo in the generator does not silently draw nothing.
	[Fact]
	public void Every_class_in_the_layout_is_defined()
	{
		string css = Stylesheet;
		HashSet<string> builtIn = ["stratum-regular-tf", "stratum-medium-tf", "stratum-bold-tf"];
		List<string> missing = Layout.Descendants().SelectMany(Classes).Distinct()
			.Where(name => !builtIn.Contains(name) && !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}(?![\w-])"))
			.ToList();
		Assert.True(missing.Count == 0, $"Not styled in k4_ui.css: {string.Join(", ", missing)}");
	}

	[Fact]
	public void Map_classes_point_at_that_maps_screenshot()
	{
		List<Match> maps = MapPattern().Matches(Stylesheet).ToList();
		Assert.True(maps.Count >= 20, $"Only {maps.Count} map backgrounds");
		Assert.All(maps, map => Assert.Equal(map.Groups[1].Value, map.Groups[2].Value));
		Assert.Contains(".map-default { background-image: url(\"s2r://panorama/images/backgrounds/season_stats_background_png.vtex\"); }", Stylesheet);
	}

	[Theory]
	[InlineData("short", "2.5s")]
	[InlineData("normal", "3.5s")]
	[InlineData("long", "5s")]
	public void Toast_lengths_set_the_timer_bars_run_out_time(string length, string duration)
		=> Assert.Contains($".k4-len-{length} .k4-toast-timer.k4-run {{ transition-duration: {duration}; }}", Stylesheet);

	[Fact]
	public void The_toast_timer_shrinks_from_full_to_empty_in_a_straight_line()
	{
		string timer = Rule("k4-toast-timer");
		Assert.Contains("width: 100%;", timer, StringComparison.Ordinal);
		Assert.Contains("transition-property: width;", timer, StringComparison.Ordinal);
		Assert.Contains("transition-timing-function: linear;", timer, StringComparison.Ordinal);
		Assert.Contains("width: 0%;", Regex.Match(Stylesheet, @"\.k4-toast-timer\.k4-run\s*\{[^}]*\}").Value, StringComparison.Ordinal);
	}

	// 7.0 played these when a toast appeared; the color class carries the sound since 7.1.
	[Theory]
	[InlineData("green", "UI.Mission.Complete")]
	[InlineData("gold", "UIPanorama.EOM.CardReveal")]
	public void Toast_colors_play_cs2_sounds(string color, string sound)
		=> Assert.Contains($".k4-toast.k4-tc-{color}:not(.k4-gone) {{ sound: \"{sound}\"; }}", Stylesheet);

	[Fact]
	public void Every_toast_color_has_a_sound()
		=> Assert.All(new[] { "green", "gold", "red", "blue", "purple" },
			color => Assert.Matches($@"\.k4-toast\.k4-tc-{color}:not\(\.k4-gone\) \{{ sound: ""[\w.]+""; \}}", Stylesheet));

	[Theory]
	[InlineData("ak47", "eq-ak47")]
	[InlineData("weapon_AWP", "eq-awp")]
	[InlineData("m4a1_silencer", "eq-m4a1_silencer")]
	[InlineData("m4a4", "ic-bullet")]
	[InlineData(null, "ic-bullet")]
	public void Weapon_missions_use_the_killfeed_icon_when_there_is_one(string? weapon, string expected)
		=> Assert.Equal(expected, PanoramaHud.WeaponIcon(weapon));

	// Regression: CS2 draws an SVG at its written size (about 32px for icons) and stretches it, so the game's own
	// icons were blurry on the full-screen panel. Every icon must use the addon's sharp copy (gen_k4_icons.py).
	[Fact]
	public void Icons_use_the_sharp_copies()
	{
		string css = Stylesheet;
		Assert.DoesNotMatch(@"s2r://panorama/images/(icons/ui|icons/equipment|hud/deathnotice)/", css);

		List<string> copies = Regex.Matches(css, @"s2r://panorama/images/custom_game/k4/icons/([a-z]+/[a-z0-9_]+)\.vsvg").Select(match => match.Groups[1].Value).Distinct().ToList();
		Assert.NotEmpty(copies);
		foreach (string copy in copies)
		{
			string svg = File.ReadAllText(Path.Combine(AddonRoot, "panorama", "images", "custom_game", "k4", "icons", $"{copy}.svg"));
			Match root = Regex.Match(svg, @"<svg\b[^>]*>");
			Match width = Regex.Match(root.Value, @"width=""([\d.]+)px""");
			Match height = Regex.Match(root.Value, @"height=""([\d.]+)px""");
			Assert.True(width.Success && height.Success, copy);
			Assert.Equal(256d, Math.Max(double.Parse(width.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(height.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), 3);
			Assert.Contains("viewBox=", root.Value);
		}
	}

	[Fact]
	public void Rank_icons_use_the_sharp_copies()
	{
		// CS2's own emblems are 32x13 and looked blurry at the panel's size; the addon ships copies
		// drawn ten times larger (panorama/tools/gen_k4_ranks.py).
		string css = Stylesheet;
		for (int number = 0; number <= 18; number++)
		{
			foreach (string kind in new[] { "skillgroup", "wingman" })
			{
				string prefix = kind == "skillgroup" ? "sg" : "wg";
				Assert.Contains($".{prefix}-{number} {{ background-image: url(\"s2r://panorama/images/custom_game/k4/ranks/{kind}{number}.vsvg\"); }}", css);
				string svg = File.ReadAllText(Path.Combine(AddonRoot, "panorama", "images", "custom_game", "k4", "ranks", $"{kind}{number}.svg"));
				Match root = Regex.Match(svg, @"<svg\b[^>]*>");
				Assert.Contains("width=\"320px\"", root.Value);
				Assert.Contains("height=\"130px\"", root.Value);
				Assert.Contains("viewBox=\"0 0 32 13\"", root.Value);
			}
		}
	}

	[Fact]
	public void Images_come_from_the_game_or_the_addon()
	{
		// Every picture is one CS2 already has under panorama/images, except the rank emblems, which the
		// addon ships. Those must exist in it, under panorama/images/custom_game: the only image folder
		// CS2's Workshop Manager packs (gameinfo.gi AddonConfig/VpkDirectories). Anything elsewhere is
		// silently left out of the upload.
		string text = Stylesheet + Layout.ToString();
		List<string> paths = ImagePattern().Matches(text).Select(match => match.Groups[1].Value).ToList();
		List<string> outside = paths.Where(path => !path.StartsWith("panorama/images/", StringComparison.Ordinal)).ToList();
		Assert.True(outside.Count == 0, $"Not a game image: {string.Join(", ", outside)}");
		List<string> missing = paths.Where(path => path.StartsWith("panorama/images/custom_game/", StringComparison.Ordinal))
			.Select(path => Path.ChangeExtension(path, ".svg"))
			.Where(path => !File.Exists(Path.Combine(AddonRoot, path.Replace('/', Path.DirectorySeparatorChar))))
			.ToList();
		Assert.True(missing.Count == 0, $"Missing in the addon: {string.Join(", ", missing)}");
	}

	[Fact]
	public void Layout_passes_the_cs2_custom_hud_validation()
	{
		// CS2 rejects a server-driven HUD layout as a whole ("Layout xml did not pass CustomHud validation")
		// when it has any other panel type or attribute. Allowed (client.dll 2000927): Panel {id, class,
		// hittest}, Label {id, class, hittest, text}, Button {id, class}; hittest on a Button is rejected.
		HashSet<string> panels = ["Panel", "Label", "Button"];
		Dictionary<string, HashSet<string>> attributes = new()
		{
			["Panel"] = ["id", "class", "hittest"],
			["Label"] = ["id", "class", "hittest", "text"],
			["Button"] = ["id", "class"],
		};

		XElement root = Layout.Root!;
		List<string> problems = root.Descendants()
			.Where(element => element.Ancestors("styles").Any() is false && element.Name.LocalName is not "styles")
			.SelectMany(element => (panels.Contains(element.Name.LocalName) ? [] : new[] { $"<{element.Name.LocalName}>" })
				.Concat(element.Attributes().Where(attribute => !attributes.TryGetValue(element.Name.LocalName, out HashSet<string>? allowed)
						|| !allowed.Contains(attribute.Name.LocalName))
					.Select(attribute => $"{element.Name.LocalName} {attribute.Name.LocalName}=")))
			.Distinct()
			.ToList();

		Assert.True(problems.Count == 0, $"Not allowed in a CS2 custom HUD layout: {string.Join(", ", problems)}");
	}

	[Fact]
	public void Layout_includes_its_compiled_stylesheet()
	{
		Assert.Contains(Layout.Descendants("include"), include => (string?)include.Attribute("src") == "s2r://panorama/styles/custom_game/k4_ui.vcss_c");
	}

	// Regression: CSS uppercase ran in the client's locale, so a Turkish CS2 showed English tabs as "MİSSİONS".
	[Fact]
	public void Stylesheet_never_uppercases_text()
		=> Assert.DoesNotContain("text-transform", Stylesheet, StringComparison.Ordinal);

	// Regression: "2d, 23h, 59m, 48s" wrapped onto a second line that the stat tile cut off.
	[Fact]
	public void Tile_values_stay_on_one_line()
	{
		string rule = Rule("k4-tile-value");
		Assert.Contains("white-space: nowrap;", rule, StringComparison.Ordinal);
		Assert.Contains("text-overflow: shrink;", rule, StringComparison.Ordinal);
		Assert.All(Range("k4-st{i}", 8).Concat(Range("k4-pf-st{i}", 6)).Concat(Range("k4-h-q{i}", 5)),
			id => Assert.Contains(ById[id].Descendants("Label"), label => Classes(label).Contains("k4-tile-value") && (string?)label.Attribute("text") == "{s:value}"));
	}

	[Theory]
	[InlineData("en", "Missions", "MISSIONS")]
	[InlineData("en", "Achievements", "ACHIEVEMENTS")]
	[InlineData("tr", "Görevler", "GÖREVLER")]
	[InlineData("tr", "İstatistik", "İSTATİSTİK")]
	public void Labels_are_capitalized_in_the_readers_language(string language, string text, string expected)
		=> Assert.Equal(expected, PanoramaHud.Upper(System.Globalization.CultureInfo.GetCultureInfo(language), text));

	[Fact]
	public void Stylesheet_braces_are_balanced()
	{
		string css = CommentPattern().Replace(Stylesheet, "");
		Assert.Equal(css.Count(character => character == '{'), css.Count(character => character == '}'));
	}

	/// <summary>The first rule whose selector is exactly this one class.</summary>
	private static string Rule(string name) => Regex.Match(Stylesheet, $@"(?m)^\.{Regex.Escape(name)}\s*\{{[^}}]*\}}").Value;

	[GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
	private static partial Regex CommentPattern();

	[GeneratedRegex(@"s2r://(panorama/(?!styles/)[^""]+)")]
	private static partial Regex ImagePattern();

	[GeneratedRegex(@"\.map-(\w+) \{ background-image: url\(""s2r://panorama/images/map_icons/screenshots/1080p/(\w+)_png\.vtex""\); \}")]
	private static partial Regex MapPattern();
}
