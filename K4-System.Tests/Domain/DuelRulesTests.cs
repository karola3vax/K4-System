namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Domain;
using K4System.Missions;

public class DuelRulesTests
{
	public sealed record DuelCase(string Name, string Rule, string? Type, string? Weapon, bool Headshot, bool Noscope, bool? ExpectedCounts,
		bool ChallengerPresent, int ChallengerKills, bool OpponentPresent, int OpponentKills, int Target, bool Over, int? ExpectedWinner);

	/// <summary>Real kill events and score lines from Evals/duels.json.</summary>
	[Fact]
	public void Eval_duel_kills_and_endings_follow_the_rules()
	{
		foreach (DuelCase test in TestData.LoadEvalList<DuelCase>("duels.json"))
		{
			if (test.Rule == "counts")
			{
				Assert.True(DuelRules.TryParse(test.Type, out DuelType type), $"{test.Name}: unknown type {test.Type}");
				Assert.True(test.ExpectedCounts == DuelRules.Counts(type, test.Weapon ?? string.Empty, test.Headshot, test.Noscope), test.Name);
			}
			else if (test.Rule == "decide")
			{
				int? winner = DuelRules.Decide(test.ChallengerPresent, test.ChallengerKills, test.OpponentPresent, test.OpponentKills, test.Target, test.Over);
				Assert.True(test.ExpectedWinner == winner, $"{test.Name}: got {winner?.ToString() ?? "running"}");
			}
			else
				Assert.Fail($"{test.Name}: unknown rule {test.Rule}");
		}
	}

	[Theory]
	[InlineData(DuelType.Any, "ak47", false, false, true)]
	[InlineData(DuelType.Any, "knife", false, false, true)]
	[InlineData(DuelType.Noscope, "awp", false, true, true)]
	[InlineData(DuelType.Noscope, "awp", true, false, false)]
	[InlineData(DuelType.Zeus, "taser", false, false, true)]
	[InlineData(DuelType.Zeus, "weapon_taser", false, false, true)]
	[InlineData(DuelType.Zeus, "deagle", true, false, false)]
	[InlineData(DuelType.Knife, "knife", false, false, true)]
	[InlineData(DuelType.Knife, "knife_karambit", false, false, true)]
	[InlineData(DuelType.Knife, "bayonet", false, false, true)]
	[InlineData(DuelType.Knife, "taser", false, false, false)]
	[InlineData(DuelType.Scout, "ssg08", false, false, true)]
	[InlineData(DuelType.Scout, "ssg08", true, true, true)]
	[InlineData(DuelType.Scout, "awp", false, true, false)]
	[InlineData(DuelType.Headshot, "deagle", true, false, true)]
	[InlineData(DuelType.Headshot, "knife", true, false, true)]
	[InlineData(DuelType.Headshot, "ak47", false, false, false)]
	public void Only_kills_that_fit_the_type_count(DuelType type, string weapon, bool headshot, bool noscope, bool expected)
		=> Assert.Equal(expected, DuelRules.Counts(type, weapon, headshot, noscope));

	[Theory]
	[InlineData(5, 3, 5, false, 1)]
	[InlineData(2, 5, 5, false, -1)]
	[InlineData(4, 4, 5, false, null)]
	[InlineData(4, 2, 5, true, 1)]
	[InlineData(1, 3, 5, true, -1)]
	[InlineData(3, 3, 5, true, 0)]
	[InlineData(0, 0, 5, true, 0)]
	[InlineData(1, 0, 1, false, 1)]
	public void First_to_the_target_wins_and_the_leader_wins_at_the_end(int challenger, int opponent, int target, bool over, int? expected)
		=> Assert.Equal(expected, DuelRules.Decide(true, challenger, true, opponent, target, over));

	[Fact]
	public void A_player_who_left_loses_when_the_duel_ends()
	{
		Assert.Equal(-1, DuelRules.Decide(false, 4, true, 1, 5, over: true));
		Assert.Equal(1, DuelRules.Decide(true, 0, false, 3, 5, over: true));
		Assert.Null(DuelRules.Decide(false, 4, true, 1, 5, over: false));
	}

	[Theory]
	[InlineData(5000, 5000, 10, 10, 500)]
	[InlineData(5000, 900, 10, 10, 90)]
	[InlineData(50, 5000, 10, 10, 0)]
	[InlineData(0, 0, 10, 10, 0)]
	public void Max_stake_is_zero_below_the_minimum(int challenger, int opponent, int percent, int min, int expected)
		=> Assert.Equal(expected, DuelRules.MaxStake(challenger, opponent, percent, min));

	[Fact]
	public void Stake_presets_above_the_cap_are_not_allowed()
	{
		DuelSettings settings = new();
		int max = DuelRules.MaxStake(3000, 6000, settings.MaxStakePercent, settings.MinStake);

		Assert.Equal(300, max);
		Assert.Equal([100, 250], settings.Stakes.Where(stake => DuelRules.StakeAllowed(stake, settings.MinStake, max)));
		Assert.True(DuelRules.StakeAllowed(max, settings.MinStake, max));
		Assert.False(DuelRules.StakeAllowed(9, settings.MinStake, max));
		Assert.False(DuelRules.StakeAllowed(10, settings.MinStake, 0));
	}

	[Fact]
	public void The_cooldown_runs_from_the_end_of_the_duel()
	{
		DateTime ended = new(2026, 11, 2, 20, 0, 0, DateTimeKind.Utc);
		int seconds = new DuelSettings().CooldownSeconds;

		Assert.Equal(TimeSpan.FromSeconds(120), ResetPolicy.CommandCooldownRemaining(ended, ended, seconds));
		Assert.Equal(1, DuelRules.WholeSeconds(ResetPolicy.CommandCooldownRemaining(ended.AddSeconds(119.5), ended, seconds)));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(ended.AddSeconds(120), ended, seconds));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(ended, null, seconds));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(ended, ended, 0));
	}

	[Theory]
	[InlineData("noscope", true, DuelType.Noscope)]
	[InlineData("HeadShot", true, DuelType.Headshot)]
	[InlineData(" zeus ", true, DuelType.Zeus)]
	[InlineData("2", false, DuelType.Any)]
	[InlineData("taser", false, DuelType.Any)]
	[InlineData(null, false, DuelType.Any)]
	public void Type_names_parse_case_insensitive_and_numbers_do_not(string? text, bool ok, DuelType expected)
	{
		Assert.Equal(ok, DuelRules.TryParse(text, out DuelType type));
		Assert.Equal(expected, type);
	}

	private static readonly string[] AllTypes = DuelRules.TypeNames;

	[Fact]
	public void Types_follow_the_weapons_the_map_offers()
	{
		Dictionary<string, MapRule> rules = new PluginConfig().Missions.MapRules;
		MapFacts competitive = new() { MapName = "de_dust2", GameType = 0, GameMode = 1, BombSites = 2, BuyZones = 2, BuyTime = 20, BuyableGuns = 255, BuyableGrenades = true };
		MapFacts aim = new() { MapName = "aim_redline", BuyTime = 45, BuyableGuns = 255, SeenWeapons = new HashSet<string> { "ak47", "deagle", "knife" } };
		MapFacts awpOnly = new() { MapName = "awp_lego", SeenWeapons = new HashSet<string> { "awp", "knife" } };
		MapFacts scoutOnly = new() { MapName = "fy_scoutknivez", SeenWeapons = new HashSet<string> { "ssg08", "knife" } };

		Assert.Equal(Enum.GetValues<DuelType>(), DuelRules.Available(AllTypes, MapAbilities.Read(competitive, rules).Weapons));
		Assert.Equal([DuelType.Any, DuelType.Knife, DuelType.Headshot], DuelRules.Available(AllTypes, MapAbilities.Read(aim, rules).Weapons));
		Assert.Equal([DuelType.Any, DuelType.Noscope, DuelType.Knife, DuelType.Headshot], DuelRules.Available(AllTypes, MapAbilities.Read(awpOnly, rules).Weapons));
		Assert.Equal([DuelType.Any, DuelType.Noscope, DuelType.Knife, DuelType.Scout, DuelType.Headshot], DuelRules.Available(AllTypes, MapAbilities.Read(scoutOnly, rules).Weapons));
		Assert.Equal([DuelType.Any, DuelType.Zeus, DuelType.Knife, DuelType.Headshot], DuelRules.Available(AllTypes, new HashSet<string> { "taser" }));
	}

	[Fact]
	public void An_unread_map_rules_nothing_out_and_the_config_switches_types_off()
	{
		Assert.Equal(Enum.GetValues<DuelType>(), DuelRules.Available(AllTypes, null));
		Assert.Equal([DuelType.Knife, DuelType.Scout], DuelRules.Available(["scout", "KNIFE", "bogus"], null));
		Assert.Equal([DuelType.Knife], DuelRules.Available(["scout", "knife"], new HashSet<string> { "awp" }));
		Assert.Empty(DuelRules.Available([], null));
	}

	[Fact]
	public void Duel_settings_are_kept_in_range()
	{
		PluginConfig config = new();
		DuelSettings duels = config.Duels;
		duels.TargetKills = 0;
		duels.Minutes = 500;
		duels.CooldownSeconds = -5;
		duels.Stakes = [500, -1, 100, 500, 0, 2000, 50, 75];
		duels.Types = ["Knife", "laser", "noscope", "knife"];

		List<string> warnings = ConfigValidator.ValidateAndNormalize(config);

		Assert.Equal(1, duels.TargetKills);
		Assert.Equal(60, duels.Minutes);
		Assert.Equal(0, duels.CooldownSeconds);
		Assert.Equal([50, 75, 100, 500], duels.Stakes);
		Assert.Equal(["knife", "noscope"], duels.Types);
		Assert.Contains(warnings, warning => warning.Contains("duels.types: 'laser'"));
		Assert.Contains(warnings, warning => warning.Contains("duels.stakes must be positive"));
		Assert.Contains(warnings, warning => warning.Contains("more than 4"));

		duels.TargetKills = 99;
		duels.Minutes = 0;
		duels.CooldownSeconds = 99999;
		ConfigValidator.ValidateAndNormalize(config);
		Assert.Equal(50, duels.TargetKills);
		Assert.Equal(1, duels.Minutes);
		Assert.Equal(3600, duels.CooldownSeconds);
	}

	[Fact]
	public void Empty_or_null_duel_lists_fall_back_safely()
	{
		PluginConfig config = new();
		config.Duels.Stakes = [-5];
		config.Duels.Types = [];

		List<string> warnings = ConfigValidator.ValidateAndNormalize(config);

		Assert.Equal([100, 250, 500, 1000], config.Duels.Stakes);
		Assert.Empty(config.Duels.Types);
		Assert.Contains(warnings, warning => warning.Contains("duels.types is empty"));

		config.Duels.Stakes = null!;
		config.Duels.Types = null!;
		ConfigValidator.ValidateAndNormalize(config);
		Assert.Equal([100, 250, 500, 1000], config.Duels.Stakes);
		Assert.Equal(AllTypes, config.Duels.Types);
	}

	[Fact]
	public void A_config_from_before_7_2_loads_with_the_new_duel_defaults()
	{
		const string old = """{ "version": 1, "duels": { "enabled": true, "min-stake": 20, "max-stake-percent": 15, "accept-seconds": 30, "block-same-ip": true } }""";
		ConfigLoadResult result = ConfigFile.Parse(old, "old", created: false);

		Assert.Null(result.Error);
		DuelSettings duels = result.Config!.Duels;
		Assert.Equal(20, duels.MinStake);
		Assert.Equal(5, duels.TargetKills);
		Assert.Equal(10, duels.Minutes);
		Assert.Equal(120, duels.CooldownSeconds);
		Assert.Equal([100, 250, 500, 1000], duels.Stakes);
		Assert.Equal(AllTypes, duels.Types);
	}
}
