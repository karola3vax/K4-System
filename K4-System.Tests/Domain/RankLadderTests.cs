namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Ranks;

public class RankLadderTests
{
	private static bool NoPermission(string permission) => false;

	[Fact]
	public void UnrestrictedRankResolutionIsUnchanged()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition gold = TestData.Rank("Gold", 500);
		RankLadder ladder = RankLadder.Create([none, silver, gold], RankGameMode.Normal);

		Assert.Same(gold, ladder.Resolve(600, NoPermission));
	}

	[Fact]
	public void RestrictedRankIsSkippedWithoutPermission()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition global = TestData.Rank("Global", 500, "@css/test");
		RankLadder ladder = RankLadder.Create([none, silver, global], RankGameMode.Normal);

		Assert.Same(silver, ladder.Resolve(600, NoPermission));
	}

	[Fact]
	public void RestrictedRankIsAssignedWithMatchingPermission()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition global = TestData.Rank("Global", 500, "@css/test");
		RankLadder ladder = RankLadder.Create([none, silver, global], RankGameMode.Normal);

		Assert.Same(global, ladder.Resolve(600, permission => permission == "@css/test"));
	}

	[Fact]
	public void RankGatesUseAnyMatchingPermission()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition global = TestData.Rank("Global", 500, "@missing", "#vip");
		RankLadder ladder = RankLadder.Create([none, global], RankGameMode.Normal);

		Assert.Same(global, ladder.Resolve(600, permission => permission == "#vip"));
	}

	[Fact]
	public void RankGatesFallBackToHighestAllowedLowerRank()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition eagle = TestData.Rank("Eagle", 500, "@css/eagle");
		RankDefinition global = TestData.Rank("Global", 900, "@css/global");
		RankLadder ladder = RankLadder.Create([none, silver, eagle, global], RankGameMode.Normal);

		Assert.Same(silver, ladder.Resolve(1000, NoPermission));
	}

	[Fact]
	public void HighestMatchingFlagSkipsLowerFlaggedRanks()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition bronze = TestData.Rank("VIP Bronze", 100, "@css/bronze");
		RankDefinition global = TestData.Rank("VIP Global", 500, "@css/global");
		RankLadder ladder = RankLadder.Create([none, bronze, global], RankGameMode.Normal);

		Assert.Same(global, ladder.Resolve(600, permission => permission == "@css/global"));
	}

	[Fact]
	public void UnrestrictedEffectiveProgressionIsUnchanged()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition gold = TestData.Rank("Gold", 500);
		RankLadder ladder = RankLadder.Create([none, silver, gold], RankGameMode.Normal);

		IReadOnlyList<RankDefinition> progression = ladder.Progression(NoPermission);

		Assert.Equal(3, progression.Count);
		Assert.Same(none, progression[0]);
		Assert.Same(silver, progression[1]);
		Assert.Same(gold, progression[2]);
		Assert.Equal(2, RankLadder.Position(progression, silver));
		Assert.Same(gold, RankLadder.Next(progression, 150));
	}

	[Fact]
	public void ParallelProgressionPredictsRegularRankWithoutPermission()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition levelOne = TestData.Rank("Level 1", 100);
		RankDefinition vipLevelOne = TestData.Rank("VIP Level 1", 100, "@css/vip");
		RankDefinition levelTwo = TestData.Rank("Level 2", 200);
		RankDefinition vipLevelTwo = TestData.Rank("VIP Level 2", 200, "@css/vip");
		RankLadder ladder = RankLadder.Create([none, levelOne, vipLevelOne, levelTwo, vipLevelTwo], RankGameMode.Normal);

		IReadOnlyList<RankDefinition> progression = ladder.Progression(NoPermission);
		RankDefinition? next = RankLadder.Next(progression, 150);

		Assert.Equal(3, progression.Count);
		Assert.Same(levelOne, progression[1]);
		Assert.Same(levelTwo, next);
		Assert.Same(next, ladder.Resolve(next!.Point, NoPermission));
	}

	[Fact]
	public void ParallelProgressionPredictsGatedRankWithPermission()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition levelOne = TestData.Rank("Level 1", 100);
		RankDefinition vipLevelOne = TestData.Rank("VIP Level 1", 100, "@css/vip", "#supporter");
		RankDefinition levelTwo = TestData.Rank("Level 2", 200);
		RankDefinition vipLevelTwo = TestData.Rank("VIP Level 2", 200, "@css/vip", "#supporter");
		RankLadder ladder = RankLadder.Create([none, levelOne, vipLevelOne, levelTwo, vipLevelTwo], RankGameMode.Normal);
		static bool HasPermission(string permission) => permission == "#supporter";

		IReadOnlyList<RankDefinition> progression = ladder.Progression(HasPermission);
		RankDefinition? next = RankLadder.Next(progression, 150);

		Assert.Equal(3, progression.Count);
		Assert.Same(vipLevelOne, progression[1]);
		Assert.Same(vipLevelTwo, next);
		Assert.Same(next, ladder.Resolve(next!.Point, HasPermission));
	}

	[Fact]
	public void EffectiveProgressionSkipsInaccessibleThresholds()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition silver = TestData.Rank("Silver", 100);
		RankDefinition vipGold = TestData.Rank("VIP Gold", 200, "@css/vip");
		RankDefinition global = TestData.Rank("Global", 300);
		RankLadder ladder = RankLadder.Create([none, silver, vipGold, global], RankGameMode.Normal);

		IReadOnlyList<RankDefinition> progression = ladder.Progression(NoPermission);

		Assert.Equal(3, progression.Count);
		Assert.Same(global, RankLadder.Next(progression, 150));
	}

	[Fact]
	public void EffectiveProgressionHasNoNextRankAtMaximum()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition global = TestData.Rank("Global", 500);
		RankLadder ladder = RankLadder.Create([none, global], RankGameMode.Normal);

		Assert.Null(RankLadder.Next(ladder.Progression(NoPermission), 500));
	}

	[Fact]
	public void GameModesFilterRegularAndFlaggedRanks()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition regular = TestData.Rank("Regular", 100);
		RankDefinition whitespace = TestData.Rank("Whitespace", 200, " ", "\t");
		RankDefinition flagged = TestData.Rank("Flagged", 300, "@css/vip");
		RankDefinition[] all = [none, regular, whitespace, flagged];

		Assert.False(none.HasRequirements);
		Assert.False(whitespace.HasRequirements);
		Assert.True(flagged.HasRequirements);

		foreach (RankDefinition rank in all)
			Assert.True(RankLadder.ShouldInclude(rank, RankGameMode.Normal));

		Assert.True(RankLadder.ShouldInclude(none, RankGameMode.FlagsOnly));
		Assert.False(RankLadder.ShouldInclude(regular, RankGameMode.FlagsOnly));
		Assert.False(RankLadder.ShouldInclude(whitespace, RankGameMode.FlagsOnly));
		Assert.True(RankLadder.ShouldInclude(flagged, RankGameMode.FlagsOnly));

		Assert.True(RankLadder.ShouldInclude(none, RankGameMode.NoFlags));
		Assert.True(RankLadder.ShouldInclude(regular, RankGameMode.NoFlags));
		Assert.True(RankLadder.ShouldInclude(whitespace, RankGameMode.NoFlags));
		Assert.False(RankLadder.ShouldInclude(flagged, RankGameMode.NoFlags));

		RankLadder flagsOnly = RankLadder.Create(all, RankGameMode.FlagsOnly);
		Assert.Equal(["None", "Flagged"], flagsOnly.Ranks.Select(rank => rank.Key));
		Assert.Equal([-1, 0], flagsOnly.Ranks.Select(rank => rank.Id));

		RankLadder noFlags = RankLadder.Create(all, RankGameMode.NoFlags);
		Assert.Equal(["None", "Regular", "Whitespace"], noFlags.Ranks.Select(rank => rank.Key));
		Assert.Equal([-1, 0, 1], noFlags.Ranks.Select(rank => rank.Id));
	}

	[Fact]
	public void FlagsOnlyScoringRequiresMatchingFlag()
	{
		RankDefinition[] ranks() => [TestData.Rank("None", -1), TestData.Rank("VIP", 100, "@css/vip"), TestData.Rank("Admin", 200, "#admin")];

		Assert.True(RankLadder.Create(ranks(), RankGameMode.Normal).IsAutomaticScoringAllowed(NoPermission));
		Assert.True(RankLadder.Create(ranks(), RankGameMode.NoFlags).IsAutomaticScoringAllowed(NoPermission));
		Assert.False(RankLadder.Create(ranks(), RankGameMode.FlagsOnly).IsAutomaticScoringAllowed(NoPermission));
		Assert.True(RankLadder.Create(ranks(), RankGameMode.FlagsOnly).IsAutomaticScoringAllowed(permission => permission == "#admin"));
	}

	[Fact]
	public void RemovingLastFlagStopsAutomaticScoring()
	{
		RankLadder ladder = RankLadder.Create([TestData.Rank("None", -1), TestData.Rank("VIP", 100, "@css/vip")], RankGameMode.FlagsOnly);
		HashSet<string> permissions = ["@css/vip"];

		Assert.True(ladder.IsAutomaticScoringAllowed(permissions.Contains));
		permissions.Clear();
		Assert.False(ladder.IsAutomaticScoringAllowed(permissions.Contains));
	}

	public static TheoryData<string> EvalCaseNames()
	{
		TheoryData<string> names = [];
		foreach (ProgressionEvalCase evalCase in TestData.LoadEvalList<ProgressionEvalCase>("rank-progression.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EvalCaseNames))]
	public void RankProgressionEvalScenario(string name)
	{
		ProgressionEvalCase evalCase = TestData.LoadEvalList<ProgressionEvalCase>("rank-progression.json").Single(item => item.Name == name);
		RankLadder ladder = RankLadder.Create(BuildRanks(evalCase), evalCase.GameMode);
		HashSet<string> permissions = new(evalCase.GrantedPermissions, StringComparer.Ordinal);
		bool HasPermission(string permission) => permissions.Contains(permission);

		RankDefinition current = ladder.Resolve(evalCase.Points, HasPermission);
		IReadOnlyList<RankDefinition> progression = ladder.Progression(HasPermission);
		RankDefinition? next = RankLadder.Next(progression, evalCase.Points);

		Assert.Equal(evalCase.ExpectedCurrent, current.Key);
		Assert.Equal(evalCase.ExpectedNext, next?.Key);
		Assert.Equal(evalCase.ExpectedScoringAllowed, ladder.IsAutomaticScoringAllowed(HasPermission));

		if (next is not null)
			Assert.Equal(next.Key, ladder.Resolve(next.Point, HasPermission).Key);
	}

	private static List<RankDefinition> BuildRanks(ProgressionEvalCase evalCase)
	{
		if (evalCase.GeneratedParallelLevels > 0)
		{
			List<RankDefinition> generated = [TestData.Rank("None", -1)];
			for (int level = 1; level <= evalCase.GeneratedParallelLevels; level++)
			{
				int points = level * evalCase.PointStep;
				generated.Add(TestData.Rank($"Level {level}", points));
				generated.Add(TestData.Rank($"VIP Level {level}", points, evalCase.GatedPermission));
			}

			return generated;
		}

		return evalCase.Ranks
			.Select(rank => TestData.Rank(rank.Key, rank.Point, rank.RequiredPermissions.ToArray()))
			.ToList();
	}

	public sealed class ProgressionEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public int Points { get; set; }
		public List<string> GrantedPermissions { get; set; } = [];
		public List<ProgressionEvalRank> Ranks { get; set; } = [];
		public int GeneratedParallelLevels { get; set; }
		public int PointStep { get; set; } = 100;
		public string GatedPermission { get; set; } = "@css/vip";
		public string ExpectedCurrent { get; set; } = string.Empty;
		public string? ExpectedNext { get; set; }
		public RankGameMode GameMode { get; set; } = RankGameMode.Normal;
		public bool ExpectedScoringAllowed { get; set; } = true;
	}

	public sealed class ProgressionEvalRank
	{
		public string Key { get; set; } = string.Empty;
		public int Point { get; set; }
		public List<string> RequiredPermissions { get; set; } = [];
	}
}
