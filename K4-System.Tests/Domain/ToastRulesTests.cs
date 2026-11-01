namespace K4System.Tests.Domain;

using K4System.Achievements;
using K4System.Hud;

public class ToastRulesTests
{
	public sealed record ToastCase(string Name, string Rule, int Before, int After, int Target, int RankPoint, int BelowPoint, int? ExpectedPercent, bool? ExpectedWarn);

	/// <summary>Real mission and rank numbers from Evals/toasts.json.</summary>
	[Fact]
	public void Eval_milestones_and_rank_warnings_fire_exactly_when_expected()
	{
		foreach (ToastCase test in TestData.LoadEvalList<ToastCase>("toasts.json"))
		{
			if (test.Rule == "milestone")
				Assert.True(test.ExpectedPercent == ToastRules.MissionMilestone(test.Before, test.After, test.Target), test.Name);
			else if (test.Rule == "hold-rank")
				Assert.True(test.ExpectedWarn == ToastRules.HoldRank(test.Before, test.After, test.RankPoint, test.BelowPoint), test.Name);
			else
				Assert.Fail($"{test.Name}: unknown rule {test.Rule}");
		}
	}

	[Theory]
	[InlineData(0, 10, null)]
	[InlineData(4, 5, 50)]
	[InlineData(8, 9, 90)]
	[InlineData(0, 9, 90)]
	[InlineData(9, 10, null)]
	[InlineData(5, 4, null)]
	public void A_mission_milestone_toasts_once_when_crossed(int before, int after, int? expected)
		=> Assert.Equal(expected, ToastRules.MissionMilestone(before, after, 10));

	[Fact]
	public void Each_milestone_of_a_mission_fires_once_while_it_counts_up()
	{
		List<int> fired = [];
		for (int progress = 1; progress < 20; progress++)
		{
			if (ToastRules.MissionMilestone(progress - 1, progress, 20) is int percent)
				fired.Add(percent);
		}
		Assert.Equal([50, 90], fired);
	}

	[Theory]
	[InlineData(44, 50, false)]
	[InlineData(45, 50, true)]
	[InlineData(49, 50, true)]
	[InlineData(50, 50, false)]
	[InlineData(0, 0, false)]
	public void Almost_there_is_90_percent_and_unfinished(int progress, int goal, bool expected)
		=> Assert.Equal(expected, ToastRules.AlmostThere(progress, goal));

	[Theory]
	[InlineData(1000, 1020, 1015, 800, true)]
	[InlineData(1000, 1100, 1050, 800, false)]
	[InlineData(1000, 1000, 1010, 800, false)]
	[InlineData(0, 10, 2, -1, false)]
	[InlineData(1000, 1010, 1005, 1000, false)]
	public void Hold_your_rank_warns_close_to_the_rank_below(int rankPoint, int before, int after, int belowPoint, bool expected)
		=> Assert.Equal(expected, ToastRules.HoldRank(before, after, rankPoint, belowPoint));

	[Theory]
	[InlineData(12, 9, 3)]
	[InlineData(150, 100, 50)]
	[InlineData(150, 101, null)]
	[InlineData(9, 9, null)]
	[InlineData(9, 12, null)]
	[InlineData(0, 5, null)]
	public void A_leaderboard_gain_toasts_only_inside_the_top_100(int before, int after, int? expected)
		=> Assert.Equal(expected, ToastRules.LeaderboardGain(before, after));

	[Fact]
	public void The_round_summary_lists_reasons_with_counts()
	{
		Assert.Equal("3× Kill · Headshot · Round won", ToastRules.RoundSummary([("Kill", 3), ("Headshot", 1), ("Round won", 1)]));
		Assert.Equal("Death", ToastRules.RoundSummary([("Death", 1), ("Unused", 0)]));
		Assert.Equal("A · B", ToastRules.RoundSummary([("A", 1), ("B", 1), ("C", 1)], max: 2));
		Assert.Equal("", ToastRules.RoundSummary([]));
	}

	[Theory]
	[InlineData(86, "+86")]
	[InlineData(-12, "-12")]
	[InlineData(0, "0")]
	public void Values_carry_their_sign(int points, string expected) => Assert.Equal(expected, ToastRules.Signed(points));

	[Theory]
	[InlineData(6, 10, 60)]
	[InlineData(47, 50, 94)]
	[InlineData(12, 10, 100)]
	[InlineData(1, 0, 0)]
	public void Bars_are_whole_percents(int progress, int target, int expected) => Assert.Equal(expected, ToastRules.Percent(progress, target));

	[Theory]
	[InlineData(0, "bronze")]
	[InlineData(299, "bronze")]
	[InlineData(300, "silver")]
	[InlineData(799, "silver")]
	[InlineData(800, "gold")]
	[InlineData(2500, "gold")]
	public void The_achievement_tier_comes_from_its_points(int points, string expected)
		=> Assert.Equal(expected, AchievementRules.Tier(new AchievementDefinition { Points = points }));

	[Fact]
	public void The_shipped_achievements_use_all_three_tiers()
		=> Assert.Equal(["bronze", "gold", "silver"], AchievementFile.Parse(AchievementFile.DefaultContent).Select(AchievementRules.Tier).Distinct().Order());
}
