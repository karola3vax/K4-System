namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Domain;

public class ScoreboardRanksTests
{
	public static TheoryData<string> EvalCaseNames()
	{
		TheoryData<string> names = [];
		foreach (ScoreboardRankEvalCase evalCase in TestData.LoadEvalList<ScoreboardRankEvalCase>("scoreboard-ranks.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EvalCaseNames))]
	public void ScoreboardRankEvalScenario(string name)
	{
		ScoreboardRankEvalCase evalCase = TestData.LoadEvalList<ScoreboardRankEvalCase>("scoreboard-ranks.json").Single(item => item.Name == name);

		Assert.Equal(evalCase.ExpectedEnabled, ScoreboardRanks.ShouldEnable(evalCase.Mode, evalCase.FollowsServerGuidelines));

		if (evalCase.ExpectedRankType is null || evalCase.ExpectedRanking is null)
			return;

		ScoreboardRankValue value = ScoreboardRanks.GetValue(
			evalCase.Mode,
			evalCase.InternalRankId,
			evalCase.Points,
			evalCase.CustomRankBase,
			evalCase.CustomRankCount,
			evalCase.CustomRankOffset);

		Assert.Equal((sbyte)evalCase.ExpectedRankType.Value, value.RankType);
		Assert.Equal(evalCase.ExpectedRanking.Value, value.Ranking);
	}

	[Fact]
	public void ScoreboardRevealPolicyIsFixed()
	{
		Assert.Equal(5.0f, ScoreboardRanks.RevealIntervalSeconds);
		Assert.Equal("CCSUsrMsg_ServerRankRevealAll", ScoreboardRanks.RevealMessageName);
		Assert.False(ScoreboardRanks.ShouldEnable(ScoreboardRankMode.Disabled, false));
		Assert.False(ScoreboardRanks.ShouldEnable(ScoreboardRankMode.Premier, true));
		Assert.True(ScoreboardRanks.ShouldEnable(ScoreboardRankMode.Premier, false));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScoreboardRanks.GetValue(ScoreboardRankMode.Disabled, 0, 100, 0, 0, 0));
	}

	public sealed class ScoreboardRankEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public ScoreboardRankMode Mode { get; set; }
		public bool FollowsServerGuidelines { get; set; }
		public bool ExpectedEnabled { get; set; }
		public int InternalRankId { get; set; }
		public int Points { get; set; }
		public int CustomRankBase { get; set; }
		public int CustomRankCount { get; set; }
		public int CustomRankOffset { get; set; }
		public int? ExpectedRankType { get; set; }
		public int? ExpectedRanking { get; set; }
	}
}
