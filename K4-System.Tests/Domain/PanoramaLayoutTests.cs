namespace K4System.Tests.Domain;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using K4System.Hud;
using Xunit.Abstractions;

/// <summary>
/// The Panorama files ship in the workshop addon and cannot be tested in-game here, so these tests check the
/// layout and stylesheet against docs/panel-contract.md (contract 8): every id, its element kind, dialog
/// variables, classes, pool sizes and the rules CS2 enforces. The lists are literal on purpose: they are the
/// contract, not whatever the server code happens to hold. The C# pool constants are checked on the driver side.
/// </summary>
public partial class PanoramaLayoutTests(ITestOutputHelper output)
{
	private static string AddonRoot => Path.Combine(TestData.RepositoryRoot(), "panorama", "k4system");

	private static readonly Lazy<XDocument> LayoutFile = new(() => XDocument.Load(Path.Combine(AddonRoot, "panorama", "layout", "custom_game", "k4_ui.xml")));

	private static readonly Lazy<string> StylesheetFile = new(() => File.ReadAllText(Path.Combine(AddonRoot, "panorama", "styles", "custom_game", "k4_ui.css")));

	private static XDocument Layout => LayoutFile.Value;

	private static string Stylesheet => StylesheetFile.Value;

	private static Dictionary<string, XElement> ById => Layout.Descendants()
		.Where(element => element.Attribute("id") is not null)
		.ToDictionary(element => (string)element.Attribute("id")!);

	private static string[] Classes(XElement element) => ((string?)element.Attribute("class") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

	private static IEnumerable<string> Range(string pattern, int count) => Enumerable.Range(0, count).Select(i => pattern.Replace("{i}", i.ToString()));

	private static readonly string[] Tabs = ["home", "missions", "achievements", "leaderboard", "duels", "profile"];

	private static readonly string[] Views = [.. Tabs, "settings"];

	// The contract's pool table (docs/panel-contract.md, "Pools").
	public static readonly TheoryData<string, int> Pools = new()
	{
		{ "k4-lb-r{i}", 100 },
		{ "k4-rk{i}", 40 },
		{ "k4-a{i}", 64 },
		{ "k4-dp{i}", 64 },
		{ "k4-pf-ss{i}", 24 },
		{ "k4-dl{i}", 4 },
	};

	/// <summary>Every contract id with the element it must be.</summary>
	private static Dictionary<string, string> ContractIds()
	{
		Dictionary<string, string> ids = [];
		void Add(string kind, params IEnumerable<string>[] groups)
		{
			foreach (string id in groups.SelectMany(group => group))
				ids.Add(id, kind);
		}

		const string P = "Panel", L = "Label", B = "Button";

		// Frame
		Add(P, ["k4-panel", "k4-outdated", "k4-event", "k4-crumbs", "k4-season", "k4-season-bar", "k4-duel-bar",
			"k4-duel-bar-icon", "k4-duel-bar-timer", "k4-status", "k4-keys"], Views.Select(view => $"k4-view-{view}"));
		Add(B, Tabs.Select(tab => $"k4-nav-{tab}"), ["k4-nav-settings", "k4-close", "k4-back", "k4-duel-bar-accept", "k4-duel-bar-decline"]);
		Add(L, ["k4-nav-duels-badge"]);

		// Home
		Add(B, ["k4-h-rank", "k4-h-top"], Range("k4-h-m{i}", 4), Range("k4-h-n{i}", 3));
		Add(L, ["k4-h-rank-name"]);
		Add(P, ["k4-h-rank-icon", "k4-h-bar", "k4-h-me", "k4-h-today", "k4-h-next", "k4-h-stats"], Range("k4-h-p{i}", 3), Range("k4-h-p{i}-icon", 3), Range("k4-h-s{i}", 5), Range("k4-h-s{i}-icon", 5),
			Range("k4-h-m{i}-icon", 4), Range("k4-h-m{i}-bar", 4), Range("k4-h-sd{i}", 7), Range("k4-h-n{i}-icon", 3));

		// Missions
		Add(P, ["k4-mw", "k4-mw-icon", "k4-mw-bar"], Range("k4-m{i}", 3), Range("k4-m{i}-icon", 3), Range("k4-m{i}-bar", 3),
			Range("k4-m{i}-ask", 3), Range("k4-sd{i}", 7));
		Add(B, Range("k4-reroll-{i}", 3), Range("k4-m{i}-yes", 3), Range("k4-m{i}-no", 3));

		// Achievements
		Add(B, ["k4-af-all", "k4-af-progress", "k4-af-unlocked", "k4-af-shown", "k4-a-show-unlocked"], Range("k4-a-sc{i}", 3), Range("k4-a{i}", 64));
		Add(P, ["k4-a-list", "k4-a-empty"], Range("k4-a-sc{i}-ring", 3), Range("k4-a-sc{i}-icon", 3),
			Range("k4-a{i}-ring", 64), Range("k4-a{i}-icon", 64), Range("k4-a{i}-bar", 64));
		Add(L, ["k4-a-more"]);

		// Leaderboard
		Add(B, ["k4-lb-tab-players", "k4-lb-tab-ranks", "k4-lb-newer", "k4-lb-older"], Range("k4-lb-r{i}-profile", 100), Range("k4-lb-r{i}-duel", 100));
		Add(P, ["k4-lb-players", "k4-lb-list", "k4-lb-me", "k4-lb-me-icon", "k4-lb-ranks", "k4-lb-you", "k4-rk-list"],
			Range("k4-lb-r{i}", 100), Range("k4-lb-r{i}-icon", 100),
			Range("k4-rk{i}", 40), Range("k4-rk{i}-icon", 40), Range("k4-rk{i}-gift", 40), Range("k4-rk{i}-you", 40));
		Add(L, ["k4-lb-more", "k4-rk-more"], Range("k4-rk{i}-name", 40));

		// Duels
		Add(P, ["k4-dp-list", "k4-d-pick", "k4-d-pick-icon", "k4-d-pick-type", "k4-d-wait", "k4-d-wait-bar", "k4-d-mine", "k4-d-mine-icon", "k4-d-mine-bar"],
			Range("k4-dp{i}-icon", 64), Range("k4-dt{i}-icon", 6), Range("k4-dl{i}", 4), Range("k4-dl{i}-icon", 4));
		Add(B, ["k4-d-send", "k4-d-takeback"], Range("k4-dp{i}", 64), Range("k4-dt{i}", 6), Range("k4-ds{i}", 5));
		Add(L, ["k4-dp-more", "k4-dp-none", "k4-dl-none"]);

		// Profile
		Add(P, ["k4-pf-icon", "k4-pf-badge", "k4-pf-bar", "k4-pf-sc-hint", "k4-pf-pt", "k4-pf-pt-t", "k4-pf-pt-ct", "k4-pf-pt-spec",
			"k4-pf-pt-t-bar", "k4-pf-pt-ct-bar", "k4-pf-pt-spec-bar", "k4-pf-ss-list", "k4-pf-ss-none"],
			Range("k4-pf-sc{i}-ring", 3), Range("k4-pf-sc{i}-icon", 3), Range("k4-pf-st{i}", 8), Range("k4-pf-st{i}-icon", 8),
			Range("k4-pf-sw{i}", 3), Range("k4-pf-sw{i}-icon", 3), Range("k4-pf-sw{i}-bar", 3), Range("k4-pf-ss{i}", 24), Range("k4-pf-ss{i}-icon", 24));
		Add(L, ["k4-pf-rank"]);
		Add(B, ["k4-pf-challenge"], Range("k4-pf-sc{i}", 3));

		// Settings
		Add(B, ["k4-set-mode-panel", "k4-set-mode-chat", "k4-set-mode-both", "k4-set-points-on", "k4-set-points-off", "k4-set-tag-on", "k4-set-tag-off"]);
		Add(L, ["k4-set-mode-lock"]);
		Add(P, ["k4-set-points", "k4-set-tag"]);

		// Toasts
		Add(P, ["k4-toasts", "k4-promo", "k4-promo-icon"], Range("k4-t{i}", 3), Range("k4-t{i}-icon", 3), Range("k4-t{i}-ring", 3),
			Range("k4-t{i}-prog", 3), Range("k4-t{i}-bar", 3), Range("k4-t{i}-timer", 3));
		Add(L, ["k4-t-more"]);
		return ids;
	}

	// Players update the addon before or after their server: an older server never touches what a newer one
	// fills, so those parts must start hidden, or an older server would show them empty. Every pooled row too.
	private static List<string> ContractStartsGone()
	{
		List<string> gone =
		[
			"k4-nav-duels-badge", "k4-event", "k4-back", "k4-season", "k4-duel-bar", "k4-status", "k4-h-me", "k4-mw", "k4-a-more", "k4-a-empty",
			"k4-lb-more", "k4-lb-me", "k4-lb-ranks", "k4-rk-more", "k4-dp-more", "k4-dp-none", "k4-d-pick", "k4-d-wait", "k4-d-mine", "k4-dl-none",
			"k4-pf-challenge", "k4-pf-sc-hint", "k4-pf-pt", "k4-pf-ss-none", "k4-set-mode-lock", "k4-set-points", "k4-set-tag",
			"k4-t-more", "k4-promo",
		];
		gone.AddRange(new[] { "k4-h-stats" }.Concat(Range("k4-h-p{i}", 3)).Concat(Range("k4-h-s{i}", 5)).Concat(Range("k4-h-m{i}", 4)).Concat(Range("k4-h-n{i}", 3)));
		gone.AddRange(Range("k4-m{i}", 3).Concat(Range("k4-reroll-{i}", 3)).Concat(Range("k4-m{i}-ask", 3)));
		gone.AddRange(Range("k4-a{i}", 64));
		gone.AddRange(Range("k4-lb-r{i}", 100).Concat(Range("k4-lb-r{i}-duel", 100)));
		gone.AddRange(Range("k4-rk{i}", 40).Concat(Range("k4-rk{i}-gift", 40)).Concat(Range("k4-rk{i}-you", 40)));
		gone.AddRange(Range("k4-dp{i}", 64).Concat(Range("k4-dt{i}", 6)).Concat(Range("k4-ds{i}", 5)).Concat(Range("k4-dl{i}", 4)));
		gone.AddRange(Range("k4-pf-sc{i}", 3).Concat(Range("k4-pf-st{i}", 8)).Concat(Range("k4-pf-sw{i}", 3)).Concat(Range("k4-pf-ss{i}", 24)));
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
	public void Every_contract_id_exists_as_the_right_element()
	{
		Dictionary<string, XElement> ids = ById;
		List<string> missing = ContractIds().Keys.Where(id => !ids.ContainsKey(id)).ToList();
		Assert.True(missing.Count == 0, $"Missing in k4_ui.xml: {string.Join(", ", missing)}");
		List<string> wrong = ContractIds().Where(pair => ids[pair.Key].Name.LocalName != pair.Value)
			.Select(pair => $"{pair.Key} is {ids[pair.Key].Name.LocalName}, not {pair.Value}").ToList();
		Assert.True(wrong.Count == 0, string.Join(", ", wrong));
	}

	// An id the server does not know is harmless, but an id outside the contract is usually a typo on one side.
	[Fact]
	public void Every_layout_id_is_in_the_contract()
	{
		Dictionary<string, string> contract = ContractIds();
		List<string> extra = ById.Keys.Where(id => !contract.ContainsKey(id)).ToList();
		Assert.True(extra.Count == 0, $"Not in docs/panel-contract.md: {string.Join(", ", extra)}");
	}

	[Fact]
	public void Parts_newer_servers_fill_start_hidden()
	{
		Dictionary<string, XElement> ids = ById;
		List<string> shown = ContractStartsGone().Where(id => !Classes(ids[id]).Contains("k4-gone")).ToList();
		Assert.True(shown.Count == 0, $"Does not start with k4-gone: {string.Join(", ", shown)}");
	}

	[Theory]
	[MemberData(nameof(Pools))]
	public void Pools_have_the_contract_row_count_and_start_hidden(string pattern, int count)
	{
		Regex row = new("^" + Regex.Escape(pattern).Replace(@"\{i}", @"(\d+)") + "$");
		List<XElement> rows = Layout.Descendants().Where(element => row.IsMatch((string?)element.Attribute("id") ?? "")).ToList();
		Assert.Equal(count, rows.Count);
		Assert.Equal(Range(pattern, count), rows.Select(element => (string)element.Attribute("id")!));
		Assert.All(rows, element => Assert.Contains("k4-gone", Classes(element)));
	}

	[Theory]
	[InlineData("k4-lb-r{i}", 100, "k4-lb-list")]
	[InlineData("k4-rk{i}", 40, "k4-rk-list")]
	[InlineData("k4-a{i}", 64, "k4-a-list")]
	[InlineData("k4-dp{i}", 64, "k4-dp-list")]
	[InlineData("k4-pf-ss{i}", 24, "k4-pf-ss-list")]
	public void Scrolled_rows_sit_in_their_scroll_box(string pattern, int count, string list)
	{
		Dictionary<string, XElement> ids = ById;
		Assert.All(Range(pattern, count), id => Assert.Contains(ids[list], ids[id].Ancestors()));
	}

	// The wheel scrolls a box only when it takes the mouse; the class carries overflow: squish scroll.
	[Fact]
	public void Every_scroll_box_takes_the_mouse_and_scrolls()
	{
		string[] lists = ["k4-a-list", "k4-lb-list", "k4-rk-list", "k4-dp-list", "k4-pf-ss-list"];
		List<XElement> scrolls = Layout.Descendants().Where(element => Classes(element).Contains("k4-scroll")).ToList();
		Assert.Equal(lists.Order(), scrolls.Select(element => (string)element.Attribute("id")!).Order());
		Assert.All(scrolls, element => Assert.Equal("true", (string?)element.Attribute("hittest")));
		Assert.All(Layout.Descendants().Where(element => Regex.IsMatch((string?)element.Attribute("id") ?? "", "^k4-.*-list$")),
			element => Assert.Contains("k4-scroll", Classes(element)));
		Assert.Contains("overflow: squish scroll;", Rule("k4-scroll"), StringComparison.Ordinal);
		Assert.Contains(".k4-scroll VerticalScrollBar .ScrollThumb", Stylesheet, StringComparison.Ordinal);
	}

	// The server cannot scroll, so the player's own row and the "you" card sit outside the scroll box.
	[Fact]
	public void The_players_own_rows_sit_outside_the_scroll_boxes()
	{
		Dictionary<string, XElement> ids = ById;
		Assert.DoesNotContain(ids["k4-lb-list"], ids["k4-lb-me"].Ancestors());
		Assert.DoesNotContain(ids["k4-rk-list"], ids["k4-lb-you"].Ancestors());
		Assert.Contains(ids["k4-lb-players"], ids["k4-lb-me"].Ancestors());
		Assert.Contains(ids["k4-lb-ranks"], ids["k4-lb-you"].Ancestors());
	}

	// The server assumes these classes are already set, and removes them the first time it swaps.
	[Theory]
	[InlineData("k4-panel", "k4-closed")]
	[InlineData("k4-h-rank-icon", "sg-0")]
	[InlineData("k4-pf-icon", "sg-0")]
	[InlineData("k4-promo-icon", "sg-0")]
	[InlineData("k4-lb-me-icon", "sg-0")]
	[InlineData("k4-d-pick-icon", "sg-0")]
	[InlineData("k4-h-bar", "w0")]
	[InlineData("k4-season-bar", "w0")]
	[InlineData("k4-duel-bar-timer", "w0")]
	[InlineData("k4-t0", "k4-tc-green")]
	[InlineData("k4-t0", "k4-len-normal")]
	public void Layout_starts_with_the_contract_defaults(string id, string expected)
		=> Assert.Contains(expected, Classes(ById[id]));

	[Fact]
	public void Repeated_parts_start_with_the_contract_defaults()
	{
		Dictionary<string, XElement> ids = ById;
		List<(string Id, string Class)> expected = [];
		expected.AddRange(Range("k4-h-p{i}-icon", 3).Concat(Range("k4-lb-r{i}-icon", 100)).Concat(Range("k4-rk{i}-icon", 40))
			.Concat(Range("k4-pf-ss{i}-icon", 24)).Concat(Range("k4-dp{i}-icon", 64)).Select(id => (id, "sg-0")));
		expected.AddRange(Range("k4-a-sc{i}-ring", 3).Concat(Range("k4-pf-sc{i}-ring", 3)).Concat(Range("k4-a{i}-ring", 64))
			.Concat(Range("k4-t{i}-ring", 3)).Select(id => (id, "t-locked")));
		// Every bar fill in the contract is an id ending in -bar (k4-duel-bar is the panel, not a fill); it starts empty.
		expected.AddRange(ContractIds().Keys.Where(id => id.EndsWith("-bar", StringComparison.Ordinal) && id != "k4-duel-bar").Select(id => (id, "w0")));
		expected.AddRange(Range("k4-t{i}", 3).SelectMany(id => new[] { (id, "k4-tc-green"), (id, "k4-len-normal") }));

		List<string> wrong = expected.Where(item => !Classes(ids[item.Id]).Contains(item.Class)).Select(item => $"{item.Id} ({item.Class})").ToList();
		Assert.True(wrong.Count == 0, $"Missing the default class: {string.Join(", ", wrong)}");
		// No screen starts open: the server shows one with k4-show.
		Assert.All(Views, view => Assert.DoesNotContain("k4-show", Classes(ids[$"k4-view-{view}"])));
	}

	[Theory]
	[InlineData("k4-nav-home", "label")]
	[InlineData("k4-nav-duels", "label", "count")]
	[InlineData("k4-nav-duels-badge", "count")]
	[InlineData("k4-event", "event")]
	[InlineData("k4-crumbs", "crumb")]
	[InlineData("k4-back", "label")]
	[InlineData("k4-season", "title", "left")]
	[InlineData("k4-duel-bar", "title", "text", "left")]
	[InlineData("k4-duel-bar-accept", "label")]
	[InlineData("k4-duel-bar-decline", "label")]
	[InlineData("k4-status", "status")]
	[InlineData("k4-keys", "keys")]
	[InlineData("k4-h-rank", "rank", "points", "place", "next", "togo")]
	[InlineData("k4-h-rank-name", "rank")]
	[InlineData("k4-h-top", "title")]
	[InlineData("k4-h-p0", "pos", "name", "points")]
	[InlineData("k4-h-me", "pos", "points")]
	[InlineData("k4-h-today", "title", "reset")]
	[InlineData("k4-h-m3", "desc", "progress", "reward")]
	[InlineData("k4-h-sd6", "day")]
	[InlineData("k4-h-next", "title")]
	[InlineData("k4-h-n2", "title", "text")]
	[InlineData("k4-view-missions", "title", "reset", "swaps", "streak", "streakhint")]
	[InlineData("k4-m0", "diff", "reward", "desc", "progress", "paid")]
	[InlineData("k4-reroll-2", "label")]
	[InlineData("k4-m1-ask", "ask")]
	[InlineData("k4-m1-yes", "label")]
	[InlineData("k4-m1-no", "label")]
	[InlineData("k4-mw", "title", "left", "desc", "progress", "reward")]
	[InlineData("k4-sd0", "day", "bonus", "tip")]
	[InlineData("k4-view-achievements", "count", "showcase")]
	[InlineData("k4-a-sc0", "name", "empty")]
	[InlineData("k4-af-all", "label")]
	[InlineData("k4-af-shown", "label")]
	[InlineData("k4-a63", "name", "goal", "desc", "tip")]
	[InlineData("k4-a-more", "more")]
	[InlineData("k4-a-empty", "none", "label")]
	[InlineData("k4-view-leaderboard", "season", "status", "count")]
	[InlineData("k4-lb-tab-players", "label")]
	[InlineData("k4-lb-tab-ranks", "label")]
	[InlineData("k4-lb-list", "profile", "duel")]
	[InlineData("k4-lb-r99", "pos", "name", "rank", "points")]
	[InlineData("k4-lb-more", "more")]
	[InlineData("k4-lb-me", "pos", "name", "rank", "points")]
	[InlineData("k4-lb-you", "text")]
	[InlineData("k4-rk39", "name", "points", "reward", "tip")]
	[InlineData("k4-rk0-name", "name")]
	[InlineData("k4-rk-more", "more")]
	[InlineData("k4-view-duels", "pick", "types", "stakes", "online", "status", "live")]
	[InlineData("k4-d-pick", "target", "stake")]
	[InlineData("k4-dp63", "name", "points", "state")]
	[InlineData("k4-dp-more", "more")]
	[InlineData("k4-dp-none", "none")]
	[InlineData("k4-dt5", "name", "tip")]
	[InlineData("k4-ds4", "label")]
	[InlineData("k4-d-send", "label")]
	[InlineData("k4-d-wait", "text", "label")]
	[InlineData("k4-d-takeback", "label")]
	[InlineData("k4-d-mine", "title", "score", "text")]
	[InlineData("k4-dl3", "names", "score", "info")]
	[InlineData("k4-dl-none", "none")]
	[InlineData("k4-view-profile", "name", "points", "place", "next", "fav", "favkills", "none", "hint", "total")]
	[InlineData("k4-pf-badge", "tag")]
	[InlineData("k4-pf-rank", "rank")]
	[InlineData("k4-pf-challenge", "label")]
	[InlineData("k4-pf-sc2", "name", "desc", "tip")]
	[InlineData("k4-pf-sc-hint", "hint")]
	[InlineData("k4-pf-st7", "value", "label", "tip")]
	[InlineData("k4-pf-sw2", "weapon", "kills")]
	[InlineData("k4-pf-pt-t", "name", "time")]
	[InlineData("k4-pf-pt-spec", "name", "time")]
	[InlineData("k4-pf-ss23", "season", "pos")]
	[InlineData("k4-pf-ss-none", "none")]
	[InlineData("k4-view-settings", "title", "mode", "points", "tag")]
	[InlineData("k4-set-mode-both", "label")]
	[InlineData("k4-set-mode-lock", "lock")]
	[InlineData("k4-set-points-on", "label")]
	[InlineData("k4-set-tag-off", "label")]
	[InlineData("k4-t2", "kind", "text", "value")]
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

	// The six tabs sit along the top in this order, then the gear.
	[Fact]
	public void The_top_bar_lists_the_tabs_in_order()
	{
		List<string> nav = Layout.Descendants("Button").Select(button => (string?)button.Attribute("id")).OfType<string>()
			.Where(id => id.StartsWith("k4-nav-", StringComparison.Ordinal)).ToList();
		Assert.Equal([.. Tabs.Select(tab => $"k4-nav-{tab}"), "k4-nav-settings"], nav);
		Dictionary<string, XElement> ids = ById;
		Assert.All(Views, view => Assert.Contains("k4-view", Classes(ids[$"k4-view-{view}"])));
		Assert.Contains(ids["k4-nav-duels"], ids["k4-nav-duels-badge"].Ancestors());
	}

	// The frame parts sit where the server and the player expect them, in this order from top to bottom.
	[Fact]
	public void The_frame_is_tabs_crumbs_duel_bar_screens_footer()
	{
		Dictionary<string, XElement> ids = ById;
		List<XElement> frame = ids["k4-panel"].Elements().Single(element => Classes(element).Contains("k4-frame")).Elements().ToList();
		int Index(string id) => frame.FindIndex(element => element == ids[id] || ids[id].Ancestors().Contains(element));
		Assert.True(Index("k4-nav-home") < Index("k4-crumbs"));
		Assert.True(Index("k4-crumbs") < Index("k4-duel-bar"));
		Assert.True(Index("k4-duel-bar") < Index("k4-view-home"));
		Assert.True(Index("k4-view-home") < Index("k4-status"));
		Assert.Equal(Index("k4-status"), Index("k4-outdated"));
		Assert.Equal(Index("k4-status"), Index("k4-keys"));
		Assert.Contains(ids["k4-crumbs"], ids["k4-back"].Ancestors());
		Assert.Contains(ids["k4-crumbs"], ids["k4-season"].Ancestors());
		Assert.All(["k4-duel-bar-accept", "k4-duel-bar-decline", "k4-duel-bar-icon", "k4-duel-bar-timer"], id => Assert.Contains(ids["k4-duel-bar"], ids[id].Ancestors()));
		Assert.All(Views, view => Assert.Equal(Index("k4-view-home"), Index($"k4-view-{view}")));
	}

	// Cards that show a picture or number without words explain themselves on hover (contract "Explanations").
	[Fact]
	public void Picture_cards_carry_a_hover_explanation()
	{
		IEnumerable<string> cards = Range("k4-a{i}", 64).Concat(Range("k4-rk{i}", 40)).Concat(Range("k4-sd{i}", 7))
			.Concat(Range("k4-pf-sc{i}", 3)).Concat(Range("k4-pf-st{i}", 8)).Concat(Range("k4-dt{i}", 6));
		Assert.All(cards, id =>
		{
			XElement card = ById[id];
			Assert.Contains("k4-has-tip", Classes(card));
			XElement tip = Assert.Single(card.Elements(), element => Classes(element).Contains("k4-tip"));
			Assert.Equal("false", (string?)tip.Attribute("hittest"));
			Assert.Contains(tip.Descendants("Label"), label => (string?)label.Attribute("text") == "{s:tip}" && (string?)label.Attribute("hittest") == "false");
		});
	}

	// The simple look moves a card's sentence the server already sends into its hover explanation (contract "Explanations").
	[Theory]
	[InlineData("k4-h-m{i}", 4, "desc")]
	[InlineData("k4-h-n{i}", 3, "text")]
	[InlineData("k4-h-s{i}", 5, "label")]
	[InlineData("k4-m{i}", 3, "desc")]
	[InlineData("k4-mw", 1, "desc")]
	[InlineData("k4-dt{i}", 6, "name")]
	[InlineData("k4-dl{i}", 4, "info")]
	public void Sentences_move_into_the_hover_explanation(string pattern, int count, string variable)
		=> Assert.All(Range(pattern, count), id =>
		{
			XElement card = ById[id];
			Assert.Contains("k4-has-tip", Classes(card));
			XElement tip = Assert.Single(card.Elements(), element => Classes(element).Contains("k4-tip"));
			Assert.Equal("false", (string?)tip.Attribute("hittest"));
			Assert.Contains(tip.Descendants("Label"), label => (string?)label.Attribute("text") == $"{{s:{variable}}}");
			Assert.All(tip.Descendants(), element => Assert.Equal("false", (string?)element.Attribute("hittest")));
		});

	[Fact]
	public void Medal_icons_and_bars_sit_inside_their_parts()
	{
		Dictionary<string, XElement> ids = ById;
		List<(string Inner, string Outer)> pairs = [];
		pairs.AddRange(Enumerable.Range(0, 64).SelectMany(i => new[] { ($"k4-a{i}-icon", $"k4-a{i}-ring"), ($"k4-a{i}-ring", $"k4-a{i}"), ($"k4-a{i}-bar", $"k4-a{i}") }));
		pairs.AddRange(Enumerable.Range(0, 3).SelectMany(i => new[] { ($"k4-a-sc{i}-icon", $"k4-a-sc{i}-ring"), ($"k4-a-sc{i}-ring", $"k4-a-sc{i}"),
			($"k4-pf-sc{i}-icon", $"k4-pf-sc{i}-ring"), ($"k4-pf-sc{i}-ring", $"k4-pf-sc{i}") }));
		pairs.AddRange(Enumerable.Range(0, 3).SelectMany(i => new[] { ($"k4-t{i}-bar", $"k4-t{i}-prog"), ($"k4-t{i}-timer", $"k4-t{i}"), ($"k4-t{i}-icon", $"k4-t{i}"),
			($"k4-m{i}-icon", $"k4-m{i}"), ($"k4-m{i}-bar", $"k4-m{i}"), ($"k4-m{i}-ask", $"k4-m{i}"), ($"k4-m{i}-yes", $"k4-m{i}-ask"), ($"k4-m{i}-no", $"k4-m{i}-ask"),
			($"k4-reroll-{i}", $"k4-m{i}"), ($"k4-pf-sw{i}-icon", $"k4-pf-sw{i}"), ($"k4-pf-sw{i}-bar", $"k4-pf-sw{i}"), ($"k4-h-p{i}-icon", $"k4-h-p{i}"),
			($"k4-h-n{i}-icon", $"k4-h-n{i}") }));
		pairs.AddRange(Enumerable.Range(0, 4).SelectMany(i => new[] { ($"k4-h-m{i}-icon", $"k4-h-m{i}"), ($"k4-h-m{i}-bar", $"k4-h-m{i}"), ($"k4-dl{i}-icon", $"k4-dl{i}") }));
		pairs.AddRange(Enumerable.Range(0, 100).SelectMany(i => new[] { ($"k4-lb-r{i}-icon", $"k4-lb-r{i}"), ($"k4-lb-r{i}-profile", $"k4-lb-r{i}"), ($"k4-lb-r{i}-duel", $"k4-lb-r{i}") }));
		pairs.AddRange(Enumerable.Range(0, 40).SelectMany(i => new[] { ($"k4-rk{i}-icon", $"k4-rk{i}"), ($"k4-rk{i}-name", $"k4-rk{i}"), ($"k4-rk{i}-gift", $"k4-rk{i}"), ($"k4-rk{i}-you", $"k4-rk{i}") }));
		pairs.AddRange(Enumerable.Range(0, 64).Select(i => ($"k4-dp{i}-icon", $"k4-dp{i}")));
		pairs.AddRange(Enumerable.Range(0, 6).Select(i => ($"k4-dt{i}-icon", $"k4-dt{i}")));
		pairs.AddRange(Enumerable.Range(0, 100).Select(i => ($"k4-lb-r{i}-icon", $"k4-lb-r{i}-profile"))); // the whole row opens the profile
		pairs.AddRange(Enumerable.Range(0, 8).Select(i => ($"k4-pf-st{i}-icon", $"k4-pf-st{i}")));
		pairs.AddRange(Enumerable.Range(0, 24).Select(i => ($"k4-pf-ss{i}-icon", $"k4-pf-ss{i}")));
		pairs.AddRange(new[] { "t", "ct", "spec" }.SelectMany(side => new[] { ($"k4-pf-pt-{side}-bar", $"k4-pf-pt-{side}"), ($"k4-pf-pt-{side}", "k4-pf-pt") }));
		pairs.AddRange(new (string, string)[]
		{
			("k4-h-rank-icon", "k4-h-rank"), ("k4-h-rank-name", "k4-h-rank"), ("k4-h-bar", "k4-h-rank"), ("k4-h-me", "k4-h-top"),
			("k4-mw-icon", "k4-mw"), ("k4-mw-bar", "k4-mw"), ("k4-lb-me-icon", "k4-lb-me"), ("k4-a-show-unlocked", "k4-a-empty"),
			("k4-d-pick-icon", "k4-d-pick"), ("k4-d-pick-type", "k4-d-pick"), ("k4-d-wait-bar", "k4-d-wait"), ("k4-d-takeback", "k4-d-wait"), ("k4-d-mine-icon", "k4-d-mine"), ("k4-d-mine-bar", "k4-d-mine"),
			("k4-season-bar", "k4-season"), ("k4-set-mode-lock", "k4-view-settings"),
			("k4-set-points-on", "k4-set-points"), ("k4-set-points-off", "k4-set-points"), ("k4-set-tag-on", "k4-set-tag"), ("k4-set-tag-off", "k4-set-tag"),
			("k4-h-sd0", "k4-view-home"), ("k4-sd0", "k4-view-missions"), ("k4-a-sc0", "k4-view-achievements"),
			("k4-lb-players", "k4-view-leaderboard"), ("k4-lb-ranks", "k4-view-leaderboard"), ("k4-dp-list", "k4-view-duels"), ("k4-pf-ss-list", "k4-view-profile"),
		});

		List<string> wrong = pairs.Where(pair => !ids[pair.Inner].Ancestors().Contains(ids[pair.Outer])).Select(pair => $"{pair.Inner} not in {pair.Outer}").ToList();
		Assert.True(wrong.Count == 0, string.Join(", ", wrong));
		// The toast icon must not be inside the ring: a toast without a medal hides only the ring.
		Assert.All(Enumerable.Range(0, 3), i => Assert.DoesNotContain(ids[$"k4-t{i}-ring"], ids[$"k4-t{i}-icon"].Ancestors()));
	}

	// The root, the toast stack and the banner cover the screen above every other HUD; they must let the mouse
	// through, or they take the hover from other plugins' mouse menus. The window itself captures the mouse.
	[Fact]
	public void Only_the_window_takes_the_mouse()
	{
		XElement root = Layout.Root!.Elements("Panel").Single();
		// CS2's resource compiler rejects an id on the root panel ("Found root panel with 'id' attribute").
		Assert.Null(root.Attribute("id"));
		Assert.Contains("k4-root", Classes(root));
		Assert.Equal("false", (string?)root.Attribute("hittest"));
		foreach (string id in new[] { "k4-toasts", "k4-promo" })
		{
			XElement part = ById[id];
			Assert.Equal("false", (string?)part.Attribute("hittest"));
			Assert.All(part.Descendants(), element => Assert.Equal("false", (string?)element.Attribute("hittest")));
		}
		Assert.Null(ById["k4-panel"].Attribute("hittest"));
		// Explanations never take the mouse from the card they explain.
		Assert.All(Layout.Descendants().Where(element => Classes(element).Contains("k4-tip")), tip => Assert.Equal("false", (string?)tip.Attribute("hittest")));
	}

	[Fact]
	public void The_panel_is_a_centered_1240_by_760_window()
	{
		string rule = Rule("k4-panel");
		Assert.Contains("width: 1240px;", rule, StringComparison.Ordinal);
		Assert.Contains("height: 760px;", rule, StringComparison.Ordinal);
		Assert.Contains("horizontal-align: center;", rule, StringComparison.Ordinal);
		Assert.Contains("vertical-align: middle;", rule, StringComparison.Ordinal);
		Assert.Contains("border-radius: 6px;", rule, StringComparison.Ordinal);
		Assert.Matches(@"\.k4-panel\.k4-closed\s*\{[^}]*visibility: collapse;", Stylesheet);
		Assert.DoesNotContain("map-", Stylesheet, StringComparison.Ordinal);
	}

	// The window is 760 high and centered at the 1080 reference height, so it ends at y 920. While it is open the
	// toast stack moves under it, and one 80px toast fits above the bottom of the screen.
	[Fact]
	public void Toasts_move_under_the_open_window()
	{
		string toast = Rule("k4-toast");
		Assert.Contains("width: 420px;", toast, StringComparison.Ordinal);
		Assert.Contains("height: 80px;", toast, StringComparison.Ordinal);
		Match open = Regex.Match(Stylesheet, @"\.k4-toasts\.k4-open\s*\{[^}]*margin: (\d+)px");
		Assert.True(open.Success, "no .k4-toasts.k4-open rule with a top margin");
		int top = int.Parse(open.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
		Assert.InRange(top, (1080 + 760) / 2, 1080 - 80);
	}

	// Only the stylesheet of the server's contract version hides the out-of-date bar, so a player with older or
	// newer K4 files sees it and everyone else never does.
	[Fact]
	public void The_out_of_date_bar_is_hidden_only_by_the_contract_class()
	{
		XElement bar = ById["k4-outdated"];
		Assert.DoesNotContain("k4-gone", Classes(bar));
		Assert.DoesNotContain(bar.Ancestors(), element => Classes(element).Contains("k4-gone"));
		Assert.Contains(bar.Descendants("Label"), label => ((string?)label.Attribute("text") ?? "").Length > 20 && !((string?)label.Attribute("text"))!.Contains("{s:"));

		List<Match> hiding = Regex.Matches(CommentPattern().Replace(Stylesheet, ""), @"([^{}]*k4-outdated[^{}]*)\{([^}]*)\}").Where(match => match.Groups[2].Value.Contains("collapse")).ToList();
		Match only = Assert.Single(hiding);
		Assert.Equal(".k4-c8 .k4-outdated", only.Groups[1].Value.Trim());
		Assert.Contains("k4-closed", Classes(ById["k4-panel"]));
	}

	// The copyright line is fixed text in the footer and never hidden.
	[Fact]
	public void The_footer_carries_the_copyright()
	{
		XElement copyright = Layout.Descendants("Label").Single(label => (string?)label.Attribute("text") == "© 2026 karola3vax");
		Assert.Contains(copyright.Ancestors(), element => Classes(element).Contains("k4-foot"));
		Assert.Null(copyright.Attribute("id"));
		Assert.DoesNotContain(copyright.AncestorsAndSelf(), element => Classes(element).Contains("k4-gone") || ((string?)element.Attribute("id") ?? "k4-panel") is not ("k4-panel" or "k4-root"));
		Assert.DoesNotContain("visibility", Rule("k4-copyright"), StringComparison.Ordinal);
	}

	[Fact]
	public void Every_class_group_in_the_contract_is_defined()
	{
		List<string> classes =
		[
			"tp-t", "tp-ct", "ic-watch", "t-gold", "t-silver", "t-bronze", "t-locked", "k4-p1", "k4-p2", "k4-p3",
			"k4-tc-green", "k4-tc-gold", "k4-tc-red", "k4-tc-blue", "k4-tc-purple", "k4-len-short", "k4-len-normal", "k4-len-long",
			"k4-n-duel", "k4-n-swap", "k4-n-showcase", "k4-n-ach", "k4-n-season", "k4-c8",
			"k4-closed", "k4-show", "k4-gone", "k4-active", "k4-done", "k4-disabled", "k4-current", "k4-pinned", "k4-locked", "k4-on", "k4-today",
			"k4-easy", "k4-medium", "k4-hard", "k4-run", "k4-out", "k4-me", "k4-ok", "k4-bad", "k4-empty", "k4-open",
			// Pictures the design and the duel types use
			"ic-icon_star", "ic-gift", "dn-penetrate", "dn-noscope", "ic-kill", "ic-stat_headshots", "ic-stat_mvps", "ic-stat_assists",
			"ic-clock", "ic-hot_trend", "ic-check", "ic-map_death", "ic-crosshair_circle", "ic-stats", "ic-trophy", "ic-leader",
			"ic-winner_ribbon", "ic-quest", "ic-home", "ic-profile", "ic-refresh_skinny", "ic-settings", "ic-warning", "ic-casual",
			"ic-kill_headshot", "eq-ak47", "eq-awp", "eq-knife", "eq-taser", "eq-ssg08",
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
		HashSet<string> builtIn = ["stratum-bold-tf"];
		List<string> missing = Layout.Descendants().SelectMany(Classes).Distinct()
			.Where(name => !builtIn.Contains(name) && !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}(?![\w-])"))
			.ToList();
		Assert.True(missing.Count == 0, $"Not styled in k4_ui.css: {string.Join(", ", missing)}");
	}

	// The 8.0 look (docs/panel-contract.md "Look"): the token greys and the accent, plus colours that mean something
	// in the game. Rank names (col-*) are left out, because server owners pick those in the config. Alpha variants
	// of an allowed colour (tag tints, shadows) are fine.
	[Fact]
	public void The_theme_follows_the_tokens()
	{
		HashSet<string> allowed =
		[
			// s0 s1 s2 s3 s4, line line-hi line-sel, tx tx2 tx3, acc (ink = s0)
			"0B0B0C", "131315", "1A1A1D", "222226", "2B2B30", "26262A", "3A3A40", "8E8E96", "EDEDED", "A8A8AD", "75757B", "F2F2F2",
			// done, lost; gold, silver, bronze; T, CT
			"6FA886", "C4675C", "C8A45C", "B4B4B4", "A97F5E", "C9A85A", "8FA6BA",
			// medal ring gradient stops
			"F0DDB0", "6E5A2E", "5A5A5A", "E6C8B0", "523B2A",
			// shadows and transparent
			"000000",
		];
		string css = Regex.Replace(CommentPattern().Replace(Stylesheet, ""), @"^\.col-.*$", "", RegexOptions.Multiline);

		List<string> stray = Regex.Matches(css, @"#([0-9A-Fa-f]{6})(?:[0-9A-Fa-f]{2})?\b")
			.Select(match => match.Groups[1].Value.ToUpperInvariant())
			.Where(color => !allowed.Contains(color))
			.Distinct()
			.ToList();
		Assert.True(stray.Count == 0, $"Colors outside the tokens: {string.Join(", ", stray)}");
		Assert.DoesNotMatch(@"rgba?\(", css);

		// Corners: window 6, cards and buttons 4, tags and bars 2; medal rings are round. 6px only on the window and the banner.
		List<string> radii = Regex.Matches(css, @"border-radius:\s*([^;]+);").Select(match => match.Groups[1].Value.Trim()).Distinct().ToList();
		Assert.True(radii.All(radius => radius is "6px" or "4px" or "2px" or "50%"), $"Corners outside 6/4/2 px: {string.Join(", ", radii)}");
		List<string> windows = Regex.Matches(css, @"(?m)^([^{}\n]+)\s*\{[^}]*border-radius: 6px;").Select(match => match.Groups[1].Value.Trim()).ToList();
		Assert.Equal([".k4-bg, .k4-shade", ".k4-panel", ".k4-promo"], windows.Order());
		Assert.Contains("border-radius: 50%;", Rule("k4-ring"), StringComparison.Ordinal);

		// Sentence case and no letter spacing.
		Assert.DoesNotContain("letter-spacing", css, StringComparison.Ordinal);

		// The flame file itself is orange, so the theme shows it in grey.
		Assert.Matches(@"\.ic-hot_trend[^{]*\{ saturation: 0; \}", css);
	}

	// White is the accent for exactly three things: the open tab's underline, the one main button, and "you".
	[Fact]
	public void White_marks_only_the_tab_the_main_button_and_you()
	{
		string css = CommentPattern().Replace(Stylesheet, "");
		List<string> selectors = Regex.Matches(css, @"([^{}]+)\{[^}]*:\s*[^;]*\bacc\b[^;]*;[^}]*\}").Select(match => match.Groups[1].Value.Trim()).ToList();
		Assert.NotEmpty(selectors);
		Assert.All(selectors, selector => Assert.Matches(@"k4-tab-line|k4-btn-main|k4-me|k4-h-me|k4-lb-you|k4-current|k4-rk-you", selector));
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

	// Controls click like CS2's own menus.
	[Theory]
	[InlineData("k4-tab:hover", "UIPanorama.sidemenu_rollover")]
	[InlineData("k4-tab:active", "UIPanorama.sidemenu_select")]
	[InlineData("k4-btn:hover", "UIPanorama.generic_button_rollover")]
	[InlineData("k4-btn:active", "UIPanorama.generic_button_press")]
	[InlineData("k4-pick:active", "UIPanorama.inventory_item_select")]
	[InlineData("k4-promo:not(.k4-gone)", "UI.ArmsRace.BecomeMatchLeader")]
	public void Controls_play_cs2_sounds(string selector, string sound)
		=> Assert.Contains($"sound: \"{sound}\";", Regex.Match(Stylesheet, $@"(?m)^\.{Regex.Escape(selector)}\s*\{{[^}}]*\}}").Value, StringComparison.Ordinal);

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
		// Every picture is one CS2 already has under panorama/images, except the rank emblems and icon copies,
		// which the addon ships. Those must exist in it, under panorama/images/custom_game: the only image folder
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
		// when it has any other panel type or attribute. Allowed (client.dll 2000927, Image seen in CS2SC and
		// Refrag): Panel {id, class, hittest}, Label {id, class, hittest, text}, Image {id, class, hittest, src},
		// Button {id, class}; hittest on a Button is rejected. An Image src is fixed and must be a game path.
		Dictionary<string, HashSet<string>> attributes = new()
		{
			["Panel"] = ["id", "class", "hittest"],
			["Label"] = ["id", "class", "hittest", "text"],
			["Image"] = ["id", "class", "hittest", "src"],
			["Button"] = ["id", "class"],
		};

		XElement root = Layout.Root!;
		List<XElement> elements = root.Descendants().Where(element => element.Ancestors("styles").Any() is false && element.Name.LocalName is not "styles").ToList();
		List<string> problems = elements
			.SelectMany(element => (attributes.ContainsKey(element.Name.LocalName) ? [] : new[] { $"<{element.Name.LocalName}>" })
				.Concat(element.Attributes().Where(attribute => !attributes.TryGetValue(element.Name.LocalName, out HashSet<string>? allowed)
						|| !allowed.Contains(attribute.Name.LocalName))
					.Select(attribute => $"{element.Name.LocalName} {attribute.Name.LocalName}=")))
			.Distinct()
			.ToList();
		Assert.True(problems.Count == 0, $"Not allowed in a CS2 custom HUD layout: {string.Join(", ", problems)}");

		List<string> sources = elements.Where(element => element.Name.LocalName == "Image").Select(image => (string?)image.Attribute("src") ?? "").ToList();
		Assert.All(sources, src => Assert.StartsWith("s2r://panorama/images/", src, StringComparison.Ordinal));
		Assert.All(elements.Where(element => element.Attribute("hittest") is { } hittest), element => Assert.Contains((string)element.Attribute("hittest")!, new[] { "true", "false" }));
	}

	[Fact]
	public void Layout_includes_its_compiled_stylesheet()
		=> Assert.Contains(Layout.Descendants("include"), include => (string?)include.Attribute("src") == "s2r://panorama/styles/custom_game/k4_ui.vcss_c");

	// About 3,500 elements load without a visible hitch (Refrag ships ~30,000), and the shared HUD entity's
	// id table grows with every id. Keep the layout well under both until the in-game load test (tech council U2, U6).
	[Fact]
	public void The_layout_stays_inside_its_budgets()
	{
		List<XElement> elements = Layout.Root!.Descendants().Where(element => element.Ancestors("styles").Any() is false && element.Name.LocalName is not ("styles" or "include")).ToList();
		int ids = elements.Count(element => element.Attribute("id") is not null);
		int classes = elements.SelectMany(Classes).Distinct().Count();
		int variables = elements.Select(element => (string?)element.Attribute("text")).OfType<string>().SelectMany(text => Regex.Matches(text, @"\{s:(\w+)\}").Select(match => match.Groups[1].Value)).Distinct().Count();
		output.WriteLine($"elements {elements.Count} (Panel {elements.Count(e => e.Name.LocalName == "Panel")}, Label {elements.Count(e => e.Name.LocalName == "Label")}, Button {elements.Count(e => e.Name.LocalName == "Button")}, Image {elements.Count(e => e.Name.LocalName == "Image")}), ids {ids}, classes {classes}, dialog variables {variables}");
		Assert.InRange(elements.Count, 1, 4000);
		Assert.InRange(ids, 1, 1300);
		Assert.InRange(classes, 1, 400);
		Assert.InRange(variables, 1, 80);
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
		Assert.All(Range("k4-pf-st{i}", 8),
			id => Assert.Contains(ById[id].Descendants("Label"), label => Classes(label).Contains("k4-tile-value") && (string?)label.Attribute("text") == "{s:value}"));
	}

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

	// wash-color tints the whole panel, its background too: a white badge with a dark icon showed as a black box in
	// game (the rank list's "you" mark, 2026-11-20). A tinted icon must sit on a panel without its own background.
	[Fact]
	public void No_rule_tints_a_panel_that_has_its_own_background()
	{
		List<string> both = [.. Regex.Matches(Stylesheet, @"([^{}]+)\{([^}]*)\}")
			.Where(rule => rule.Groups[2].Value.Contains("wash-color", StringComparison.Ordinal) && rule.Groups[2].Value.Contains("background-color", StringComparison.Ordinal))
			.Select(rule => rule.Groups[1].Value.Trim())];
		Assert.Empty(both);
	}

	[GeneratedRegex(@"s2r://(panorama/(?!styles/)[^""]+)")]
	private static partial Regex ImagePattern();
}
