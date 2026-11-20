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

	private static ToastEntry Entry(string kind) => new(new Toast(kind, "", "", "ic-check", ToastColor.Green));

	// The stack is newest first, as PanoramaHud keeps it.
	private static void Add(List<ToastEntry> stack, ToastEntry entry) => stack.Insert(0, entry);

	[Fact]
	public void With_the_window_closed_up_to_three_show_newest_first()
	{
		List<ToastEntry> stack = [];
		ToastEntry a = Entry("a"), b = Entry("b"), c = Entry("c"), d = Entry("d");
		foreach (ToastEntry entry in new[] { a, b, c, d })
			Add(stack, entry);

		(List<ToastEntry> visible, List<ToastEntry> started) = ToastRules.Arrange(stack, ToastRules.ClosedSlots);
		Assert.Equal([d, c, b], visible);
		Assert.Equal([d, c, b], started);
		Assert.False(a.Showing);
	}

	[Fact]
	public void With_the_window_open_one_shows_and_the_rest_wait_their_turn()
	{
		List<ToastEntry> stack = [];
		ToastEntry first = Entry("first");
		Add(stack, first);
		Assert.Equal([first], ToastRules.Arrange(stack, ToastRules.OpenSlots).Visible);

		// A newer toast waits: the one on screen keeps its slot until its time is up.
		ToastEntry second = Entry("second"), third = Entry("third");
		Add(stack, second);
		Add(stack, third);
		(List<ToastEntry> visible, List<ToastEntry> started) = ToastRules.Arrange(stack, ToastRules.OpenSlots);
		Assert.Equal([first], visible);
		Assert.Empty(started);

		// It leaves: the newest waiting one is next.
		stack.Remove(first);
		Assert.Equal([third], ToastRules.Arrange(stack, ToastRules.OpenSlots).Started);
		stack.Remove(third);
		Assert.Equal([second], ToastRules.Arrange(stack, ToastRules.OpenSlots).Started);
	}

	[Fact]
	public void Opening_the_window_drops_the_extra_toasts_already_seen_and_never_replays_them()
	{
		List<ToastEntry> stack = [];
		ToastEntry a = Entry("a"), b = Entry("b"), c = Entry("c");
		foreach (ToastEntry entry in new[] { a, b, c })
			Add(stack, entry);
		ToastRules.Arrange(stack, ToastRules.ClosedSlots);
		int runOfA = a.Run;
		b.Leaving = true;

		(List<ToastEntry> visible, List<ToastEntry> started) = ToastRules.Arrange(stack, ToastRules.OpenSlots);
		Assert.Equal([c], visible);
		Assert.Empty(started);
		Assert.Equal([c], stack);
		Assert.False(a.Showing);
		Assert.NotEqual(runOfA, a.Run); // its running timer no longer matches

		// Once c leaves, nothing that was already on screen comes back.
		stack.Remove(c);
		Assert.Empty(ToastRules.Arrange(stack, ToastRules.OpenSlots).Started);
	}

	// The window closes while toasts wait: they show again, up to three.
	[Fact]
	public void Closing_the_window_shows_the_waiting_toasts()
	{
		List<ToastEntry> stack = [];
		ToastEntry a = Entry("a"), b = Entry("b"), c = Entry("c");
		foreach (ToastEntry entry in new[] { a, b, c })
			Add(stack, entry);
		ToastRules.Arrange(stack, ToastRules.OpenSlots);
		Assert.Single(stack, entry => entry.Showing);

		(List<ToastEntry> visible, List<ToastEntry> started) = ToastRules.Arrange(stack, ToastRules.ClosedSlots);
		Assert.Equal(3, visible.Count);
		Assert.Equal(2, started.Count);
	}

	[Fact]
	public void Old_waiting_toasts_are_dropped_past_the_limit()
	{
		List<ToastEntry> stack = [];
		ToastEntry oldest = Entry("oldest");
		Add(stack, oldest);
		for (int i = 0; i < ToastRules.MaxWaiting + 3; i++)
			Add(stack, Entry($"t{i}"));

		ToastRules.Arrange(stack, ToastRules.OpenSlots);
		Assert.Equal(ToastRules.MaxWaiting, stack.Count(entry => !entry.Showing));
		Assert.Contains(stack, entry => entry.Showing);
		Assert.DoesNotContain(oldest, stack.Where(entry => !entry.Showing));
	}
}
