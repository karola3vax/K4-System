namespace K4System.Tests.Domain;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using K4System.Hud;

/// <summary>
/// The Panorama files ship in the workshop addon and cannot be tested in-game here, so these tests
/// make sure every panel id and style class the server uses exists in the layout and stylesheet.
/// </summary>
public partial class PanoramaLayoutTests
{
	private static string AddonRoot => Path.Combine(TestData.RepositoryRoot(), "panorama", "k4system");

	private static XDocument Layout => XDocument.Load(Path.Combine(AddonRoot, "panorama", "layout", "custom_game", "k4_ui.xml"));

	private static string Stylesheet => File.ReadAllText(Path.Combine(AddonRoot, "panorama", "styles", "custom_game", "k4_ui.css"));

	private static HashSet<string> Ids => Layout.Descendants().Select(element => (string?)element.Attribute("id")).OfType<string>().ToHashSet();

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
		Assert.Equal(ids.Count, ids.Distinct().Count());
	}

	[Fact]
	public void Every_panel_the_server_writes_to_exists()
	{
		List<string> expected =
		[
			"k4-panel", "k4-toast", "k4-close", "k4-subtitle",
			"k4-view-rank", "k4-view-missions", "k4-view-top", "k4-view-ranks", "k4-view-stats",
			"k4-rank-badge", "k4-rank-icon", "k4-rank-name", "k4-rank-bar", "k4-rank-reward", "k4-toast-icon",
			"k4-top-me", "k4-top-me-icon", "k4-top-me-pos", "k4-top-prev", "k4-top-next", "k4-rk-prev", "k4-rk-next",
			"k4-showdown", "k4-sd-wanted", "k4-sd-duel", "k4-sd-challenge", "k4-sd-hint", "k4-duel-accept", "k4-duel-decline",
			"k4-event", "k4-view-achievements", "k4-ach-prev", "k4-ach-next", "k4-ach-page"
		];
		expected.AddRange(new[] { "rank", "missions", "top", "ranks", "stats", "achievements" }.Select(tab => $"k4-tab-{tab}"));
		for (int i = 0; i < PanoramaHud.AchievementCards; i++)
			expected.AddRange([$"k4-ach-{i}", $"k4-ach-{i}-icon", $"k4-ach-{i}-bar"]);

		for (int i = 0; i < PanoramaHud.MissionCards; i++)
			expected.AddRange([$"k4-m{i}", $"k4-m{i}-diff", $"k4-m{i}-icon", $"k4-m{i}-bar", $"k4-reroll-{i}"]);
		for (int i = 0; i < 10; i++)
			expected.AddRange([$"k4-top-{i}", $"k4-top-{i}-icon", $"k4-top-{i}-pos", $"k4-rk-{i}", $"k4-rk-{i}-icon", $"k4-rk-{i}-name"]);
		for (int i = 0; i < 9; i++)
			expected.AddRange([$"k4-st-{i}", $"k4-st-{i}-icon"]);

		HashSet<string> ids = Ids;
		List<string> missing = expected.Where(id => !ids.Contains(id)).ToList();
		Assert.True(missing.Count == 0, $"Missing in k4_ui.xml: {string.Join(", ", missing)}");
	}

	[Fact]
	public void Every_clickable_id_is_a_button()
	{
		List<string> clickable = ["k4-close", "k4-top-prev", "k4-top-next", "k4-rk-prev", "k4-rk-next", "k4-duel-accept", "k4-duel-decline", "k4-ach-prev", "k4-ach-next"];
		clickable.AddRange(Enumerable.Range(0, 3).Select(i => $"k4-reroll-{i}"));
		clickable.AddRange(new[] { "rank", "missions", "top", "ranks", "stats", "achievements" }.Select(tab => $"k4-tab-{tab}"));
		Dictionary<string, string> elements = Layout.Descendants()
			.Where(element => element.Attribute("id") is not null)
			.ToDictionary(element => (string)element.Attribute("id")!, element => element.Name.LocalName);

		Assert.All(clickable, id => Assert.Equal("Button", elements[id]));
	}

	[Theory]
	[InlineData("k4-rank-icon", "icon")]
	[InlineData("k4-top-0-icon", "icon")]
	[InlineData("k4-rank-bar", "bar")]
	[InlineData("k4-m0-bar", "bar")]
	[InlineData("k4-toast", "toast")]
	[InlineData("k4-toast-icon", "toast-icon")]
	public void Server_defaults_match_the_layouts_starting_classes(string panel, string group)
	{
		// The server assumes these classes are already set, and removes them the first time it swaps.
		string? classes = Layout.Descendants().Where(element => (string?)element.Attribute("id") == panel)
			.Select(element => (string?)element.Attribute("class")).Single();
		Assert.Contains(PanoramaHud.DefaultClass(group)!, (classes ?? "").Split(' '));
	}

	// Players update the addon before or after their server: a 6.4 server never touches what 6.5 added,
	// so the new tab, view content and banner must start hidden, or a 6.4 server would show them empty.
	[Theory]
	[InlineData("k4-tab-achievements")]
	[InlineData("k4-event")]
	[InlineData("k4-showdown")]
	public void Parts_newer_servers_fill_start_hidden(string panel)
	{
		string? classes = Layout.Descendants().Where(element => (string?)element.Attribute("id") == panel)
			.Select(element => (string?)element.Attribute("class")).Single();
		Assert.Contains("k4-gone", (classes ?? "").Split(' '));
	}

	[Fact]
	public void The_server_tab_list_matches_the_layout_tabs_in_order()
	{
		List<string> layoutTabs = Layout.Descendants()
			.Select(element => (string?)element.Attribute("id"))
			.OfType<string>()
			.Where(id => id.StartsWith("k4-tab-", StringComparison.Ordinal))
			.Select(id => id["k4-tab-".Length..])
			.ToList();
		Assert.Equal(Enum.GetNames<HudTab>().Select(name => name.ToLowerInvariant()), layoutTabs);
	}

	[Theory]
	[InlineData("ak47", "eq-ak47")]
	[InlineData("weapon_AWP", "eq-awp")]
	[InlineData("m4a1_silencer", "eq-m4a1_silencer")]
	[InlineData("m4a4", "ic-bullet")]
	[InlineData(null, "ic-bullet")]
	public void Weapon_missions_use_the_killfeed_icon_when_there_is_one(string? weapon, string expected)
		=> Assert.Equal(expected, PanoramaHud.WeaponIcon(weapon));

	[Fact]
	public void Every_style_class_the_server_toggles_is_defined()
	{
		string css = Stylesheet;
		List<string> classes =
		[
			"k4-closed", "k4-show", "k4-gone", "k4-active", "k4-done", "k4-easy", "k4-medium", "k4-hard", "k4-weekly", "k4-blocked",
			"k4-current", "k4-disabled", "k4-toast-green", "k4-toast-gold", "k4-toast-red", "k4-place-1", "k4-place-2", "k4-place-3"
		];
		classes.AddRange(PanoramaHud.IconClasses());
		classes.AddRange(Enumerable.Range(0, 21).Select(step => $"w{step}"));
		classes.AddRange(Enumerable.Range(0, 19).SelectMany(number => new[] { $"sg-{number}", $"wg-{number}" }));
		classes.AddRange(new[] { "default", "white", "darkred", "green", "lightyellow", "lightblue", "olive", "lime", "red", "lightpurple",
			"purple", "grey", "yellow", "gold", "silver", "blue", "darkblue", "bluegrey", "magenta", "lightred", "orange" }.Select(color => $"col-{color}"));

		List<string> missing = classes.Where(name => !Regex.IsMatch(css, $@"\.{Regex.Escape(name)}\b")).ToList();
		Assert.True(missing.Count == 0, $"Missing in k4_ui.css: {string.Join(", ", missing)}");
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

	[GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
	private static partial Regex CommentPattern();

	[GeneratedRegex(@"s2r://(panorama/(?!styles/)[^""]+)")]
	private static partial Regex ImagePattern();
}
