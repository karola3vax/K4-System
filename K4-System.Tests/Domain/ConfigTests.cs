namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Config;

public class ConfigTests
{
	[Fact]
	public void Command_name_typos_are_reported()
	{
		CommandSettings commands = new() { Main = ["k4, lvl"], Rank = ["ranks", "myrank"] };

		List<string> warnings = ConfigValidator.CommandNameWarnings(commands).ToList();

		Assert.Equal(2, warnings.Count);
		Assert.Contains(warnings, warning => warning.Contains("\"k4, lvl\" is one name"));
		Assert.Contains(warnings, warning => warning.Contains("\"ranks\" is in both commands.rank and commands.ranks"));
		Assert.Empty(ConfigValidator.CommandNameWarnings(new CommandSettings()));
	}

	private static PluginConfig ParseTemplate()
	{
		ConfigLoadResult result = ConfigFile.Parse(ConfigFile.Template, "template", created: true);
		Assert.Null(result.Error);
		return result.Config!;
	}

	[Fact]
	public void Commented_template_matches_the_code_defaults()
	{
		PluginConfig fromTemplate = ParseTemplate();
		PluginConfig defaults = new();

		Assert.Equal(JsonSerializer.Serialize(defaults), JsonSerializer.Serialize(fromTemplate));
		Assert.Empty(ConfigValidator.ValidateAndNormalize(fromTemplate));
	}

	[Fact]
	public void Template_documents_every_point_name_and_preset()
	{
		string template = ConfigFile.Template;

		foreach (string name in PointValues.Names)
			Assert.Contains(name, template);
		foreach (string preset in PointPresets.Names)
			Assert.Contains(preset, template);
	}

	[Fact]
	public void Defaults_are_sqlite_casual_premier()
	{
		PluginConfig config = new();
		ConfigValidator.ValidateAndNormalize(config);

		Assert.True(ConfigValidator.IsSqlite(config.Storage));
		Assert.Equal(PointPresets.Casual, config.Points.Preset);
		Assert.Equal(ScoreboardRankMode.Premier, config.Scoreboard.RankDisplay);
		Assert.Equal(RankGameMode.Normal, config.Ranks.Mode);
		Assert.Equal(24, config.Ranks.ResetCooldownHours);
		Assert.Equal(1, config.Tracking.MinimumPlayers);
		Assert.True(config.Tracking.CountBots);
		Assert.Equal(10f, config.Ranks.PlaytimeReward.EveryMinutes);
	}

	[Fact]
	public void Unknown_settings_are_rejected_so_typos_are_noticed()
	{
		ConfigLoadResult result = ConfigFile.Parse("""{ "version": 1, "ranks": { "start-point": 5 } }""", "test", created: false);

		Assert.Null(result.Config);
		Assert.Contains("start-point", result.Error);
	}

	[Fact]
	public void Missing_settings_use_defaults_and_comments_are_allowed()
	{
		ConfigLoadResult result = ConfigFile.Parse("""
			// comment
			{ "version": 1, "storage": { "type": "mysql", }, }
			""", "test", created: false);

		Assert.NotNull(result.Config);
		Assert.Equal("mysql", result.Config.Storage.Type);
		Assert.Equal(3306, result.Config.Storage.MySql.Port);
		Assert.True(result.Config.Modules.Ranks);
	}

	[Fact]
	public void Other_file_versions_are_refused()
	{
		ConfigLoadResult result = ConfigFile.Parse("""{ "version": 2 }""", "test", created: false);

		Assert.Null(result.Config);
		Assert.Contains("version 2", result.Error);
	}

	[Fact]
	public void Enums_are_read_as_names_only()
	{
		Assert.NotNull(ConfigFile.Parse("""{ "ranks": { "mode": "FlagsOnly" }, "scoreboard": { "rank-display": "Wingman" } }""", "t", false).Config);
		Assert.Null(ConfigFile.Parse("""{ "ranks": { "mode": "Invalid" } }""", "t", false).Config);
		Assert.Null(ConfigFile.Parse("""{ "ranks": { "mode": 1 } }""", "t", false).Config);
		Assert.Null(ConfigFile.Parse("""{ "scoreboard": { "rank-display": "DangerZone" } }""", "t", false).Config);

		string json = JsonSerializer.Serialize(new PluginConfig());
		Assert.Contains("\"Normal\"", json);
		Assert.Contains("\"Premier\"", json);
	}

	[Fact]
	public void First_start_writes_the_template_and_loads_it()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"k4-config-{Guid.NewGuid():N}");
		try
		{
			ConfigLoadResult result = ConfigFile.Load(directory);

			Assert.True(result.Created);
			Assert.NotNull(result.Config);
			Assert.Equal(ConfigFile.Template, File.ReadAllText(Path.Combine(directory, ConfigFile.FileName)));

			ConfigLoadResult second = ConfigFile.Load(directory);
			Assert.False(second.Created);
			Assert.NotNull(second.Config);
		}
		finally
		{
			Directory.Delete(directory, true);
		}
	}

	[Fact]
	public void An_old_5x_config_stops_K4_until_the_new_file_is_filled_in()
	{
		string directory = Path.Combine(Path.GetTempPath(), $"k4-config-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		try
		{
			File.WriteAllText(Path.Combine(directory, ConfigFile.LegacyFileName), "{}");

			ConfigLoadResult result = ConfigFile.Load(directory);
			Assert.Null(result.Config);
			Assert.Contains(ConfigFile.LegacyFileName, result.Error);
			Assert.True(File.Exists(Path.Combine(directory, ConfigFile.FileName)));

			// Once the new file exists, it is used and the old one is ignored.
			Assert.NotNull(ConfigFile.Load(directory).Config);
		}
		finally
		{
			Directory.Delete(directory, true);
		}
	}

	[Fact]
	public void Null_sections_and_blank_aliases_are_rejected()
	{
		ConfigValidator.ValidateSections(new PluginConfig());

		PluginConfig nullSection = new() { Ranks = null! };
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateSections(nullSection));

		PluginConfig blankAlias = new();
		blankAlias.Commands.Rank.Add(" ");
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateSections(blankAlias));

		PluginConfig blankAdmin = new();
		blankAdmin.Tags.Admins.Add(new AdminTag { Permission = "" });
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateSections(blankAdmin));
	}

	[Theory]
	[InlineData("sqlite")]
	[InlineData("SQLite")]
	[InlineData("mysql")]
	[InlineData(" MySQL ")]
	public void Storage_type_accepts_sqlite_and_mysql(string type)
		=> ConfigValidator.ValidateStorage(new StorageSettings { Type = type });

	[Theory]
	[InlineData("postgres")]
	[InlineData("")]
	public void Storage_type_rejects_anything_else(string type)
		=> Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateStorage(new StorageSettings { Type = type }));

	[Fact]
	public void Storage_requires_the_settings_of_the_chosen_type()
	{
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateStorage(new StorageSettings { Type = "sqlite", SqliteFile = " " }));
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateStorage(new StorageSettings { Type = "mysql", MySql = new MySqlSettings { Host = "" } }));

		// MySQL fields do not matter for SQLite.
		ConfigValidator.ValidateStorage(new StorageSettings { Type = "sqlite", MySql = new MySqlSettings { Host = "" } });
	}

	[Theory]
	[InlineData("k4.")]
	[InlineData("k4-")]
	[InlineData("k4 ")]
	public void Table_prefix_must_be_a_safe_identifier(string prefix)
		=> Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateStorage(new StorageSettings { TablePrefix = prefix }));

	[Fact]
	public void LevelRanks_table_name_is_checked_only_when_enabled()
	{
		ConfigValidator.ValidateStorage(new StorageSettings { LevelRanks = new LevelRanksSettings { Table = "bad name" } });
		Assert.Throws<InvalidDataException>(() =>
			ConfigValidator.ValidateStorage(new StorageSettings { LevelRanks = new LevelRanksSettings { Enabled = true, Table = "bad name" } }));
	}

	[Fact]
	public void Custom_scoreboard_icons_need_a_valid_range()
	{
		ScoreboardSettings custom(int first, int count, int offset)
			=> new() { RankDisplay = ScoreboardRankMode.Custom, CustomIcons = new CustomIconSettings { FirstId = first, Count = count, Offset = offset } };

		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateScoreboard(custom(0, 0, 0)));
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateScoreboard(custom(19000051, 18, -1)));
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateScoreboard(custom(int.MaxValue, 2, 0)));
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateScoreboard(new ScoreboardSettings { RankDisplay = (ScoreboardRankMode)99 }));
		ConfigValidator.ValidateScoreboard(custom(19000051, 18, 0));
	}

	[Fact]
	public void Unsafe_numbers_are_corrected_with_warnings()
	{
		PluginConfig config = new();
		config.Storage.Type = "mysql";
		config.Storage.MySql.Port = 70000;
		config.Storage.MySql.SslMode = "sometimes";
		config.Ranks.VipMultiplier = double.NaN;
		config.Ranks.DynamicDeathPoints.MinMultiplier = 4;
		config.Ranks.DynamicDeathPoints.MaxMultiplier = 2;
		config.Commands.CooldownSeconds = -1;
		config.Tracking.MinimumPlayers = -3;

		List<string> warnings = ConfigValidator.ValidateAndNormalize(config);

		Assert.Equal(5, warnings.Count);
		Assert.Equal(3306, config.Storage.MySql.Port);
		Assert.Equal("None", config.Storage.MySql.SslMode);
		Assert.Equal(1.0, config.Ranks.VipMultiplier);
		Assert.Equal((2.0, 4.0), (config.Ranks.DynamicDeathPoints.MinMultiplier, config.Ranks.DynamicDeathPoints.MaxMultiplier));
		Assert.Equal(0, config.Commands.CooldownSeconds);
		Assert.Equal(0, config.Tracking.MinimumPlayers);
	}

	// ---------------------------------------------------------------- points

	[Fact]
	public void Every_preset_defines_every_point_name()
	{
		foreach (string preset in PointPresets.Names)
		{
			PointValues values = PointPresets.Get(preset);
			Assert.Equal(PointValues.Names.Count, values.All.Count);
		}

		Assert.Equal(PointValues.Names.Count, PointValues.Names.Distinct().Count());
	}

	[Fact]
	public void Overrides_replace_single_preset_values()
	{
		PluginConfig config = new();
		config.Points.Preset = "Competitive";
		config.Points.Overrides["kill"] = 99;

		ConfigValidator.ValidateAndNormalize(config);

		Assert.Equal(99, config.PointValues.Kill);
		Assert.Equal(PointPresets.Get(PointPresets.Competitive).Death, config.PointValues.Death);
	}

	[Fact]
	public void Unknown_presets_and_point_names_are_rejected()
	{
		PluginConfig badPreset = new();
		badPreset.Points.Preset = "hardcore";
		Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateAndNormalize(badPreset));

		PluginConfig badName = new();
		badName.Points.Overrides["kills"] = 5;
		InvalidDataException error = Assert.Throws<InvalidDataException>(() => ConfigValidator.ValidateAndNormalize(badName));
		Assert.Contains("kills", error.Message);
	}

	[Fact]
	public void Casual_preset_keeps_the_5x_default_economy()
	{
		PointValues points = PointPresets.Get(PointPresets.Casual);

		(string Name, int Expected)[] expected =
		[
			("death", -15), ("kill", 20), ("headshot", 5), ("wallbang", 5), ("noscope", 60), ("through-smoke", 40),
			("blind-kill", 60), ("team-kill", -40), ("suicide", -20), ("assist", 10), ("team-assist", -10),
			("flash-assist", 15), ("team-flash-assist", -15), ("round-win", 5), ("round-loss", 0), ("mvp", 25),
			("bomb-drop", -10), ("bomb-pickup", 5), ("bomb-defuse", 40), ("bomb-defuse-team", 10), ("bomb-plant", 30),
			("bomb-explode", 25), ("hostage-hurt", -10), ("hostage-kill", -40), ("hostage-rescue", 30), ("hostage-rescue-all", 25),
			("long-distance-kill", 60), ("long-distance-meters", 30), ("double-kill", 10), ("triple-kill", 15), ("domination", 25),
			("rampage", 30), ("mega-kill", 40), ("ownage", 50), ("ultra-kill", 55), ("killing-spree", 65), ("monster-kill", 70),
			("unstoppable", 80), ("godlike", 90), ("grenade-kill", 60), ("fire-kill", 50), ("impact-kill", 60), ("taser-kill", 60),
			("knife-kill", 60), ("playtime", 15)
		];

		foreach ((string name, int value) in expected)
			Assert.True(points[name] == value, $"{name}: expected {value}, got {points[name]}.");
	}

	[Theory]
	[InlineData(PointPresets.Casual)]
	[InlineData(PointPresets.Competitive)]
	[InlineData(PointPresets.Deathmatch)]
	public void Every_preset_rewards_an_even_player(string preset)
	{
		PointValues points = PointPresets.Get(preset);

		// One kill for every death, a few assists and headshots, half the rounds won.
		int perMatch = 15 * points.Kill + 15 * points.Death + 5 * points.Headshot + 4 * points.Assist + 8 * points.RoundWin + 8 * points.RoundLose;

		Assert.True(perMatch > 0, $"{preset} gives {perMatch} points for an even match.");
		Assert.True(points.TeamKill < 0 && points.Suicide < 0, $"{preset} must punish team kills and suicides.");
	}

	public static TheoryData<string> EconomyCaseNames()
	{
		TheoryData<string> names = [];
		foreach (DefaultEconomyEvalCase evalCase in TestData.LoadEvalList<DefaultEconomyEvalCase>("default-economy.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EconomyCaseNames))]
	public void Casual_economy_balance_eval(string name)
	{
		DefaultEconomyEvalCase evalCase = TestData.LoadEvalList<DefaultEconomyEvalCase>("default-economy.json").Single(item => item.Name == name);
		PointValues points = PointPresets.Get(PointPresets.Casual);
		float playtimeMinutes = new PluginConfig().Ranks.PlaytimeReward.EveryMinutes;

		double playtimeTicks = evalCase.ActiveMinutes / playtimeMinutes;
		double matchPoints =
			evalCase.Kills * points.Kill +
			evalCase.Deaths * points.Death +
			evalCase.Headshots * points.Headshot +
			evalCase.Assists * points.Assist +
			evalCase.RoundWins * points.RoundWin +
			evalCase.Mvps * points.MVP +
			playtimeTicks * points.PlaytimePoints +
			evalCase.BombPlants * points.BombPlant +
			evalCase.BombDefuses * points.BombDefused +
			evalCase.BombExplodes * points.BombExploded +
			evalCase.RareBonuses * points.NoScope +
			evalCase.DoubleKills * points.DoubleKill +
			evalCase.TripleKills * points.TripleKill;

		double pointsPerHour = matchPoints * (60.0 / evalCase.MatchMinutes);
		double hoursToTop = evalCase.GlobalElitePoints / pointsPerHour;
		double playtimeOnlyHours = evalCase.GlobalElitePoints / ((60.0 / playtimeMinutes) * points.PlaytimePoints);

		Assert.InRange(hoursToTop, evalCase.TargetMinHours, evalCase.TargetMaxHours);
		Assert.InRange(playtimeOnlyHours, evalCase.PlaytimeOnlyMinHours, evalCase.PlaytimeOnlyMaxHours);
	}

	public sealed class DefaultEconomyEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public int GlobalElitePoints { get; set; }
		public double MatchMinutes { get; set; }
		public double ActiveMinutes { get; set; }
		public double Kills { get; set; }
		public double Deaths { get; set; }
		public double Headshots { get; set; }
		public double Assists { get; set; }
		public double RoundWins { get; set; }
		public double Mvps { get; set; }
		public double BombPlants { get; set; }
		public double BombDefuses { get; set; }
		public double BombExplodes { get; set; }
		public double RareBonuses { get; set; }
		public double DoubleKills { get; set; }
		public double TripleKills { get; set; }
		public double TargetMinHours { get; set; }
		public double TargetMaxHours { get; set; }
		public double PlaytimeOnlyMinHours { get; set; }
		public double PlaytimeOnlyMaxHours { get; set; }
	}
}
