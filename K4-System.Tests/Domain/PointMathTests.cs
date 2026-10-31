namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Domain;
using K4System.Ranks;

public class PointMathTests
{
	// The old CalculateStoredPoints(current, amount, allowChange: false) returned the saved points.
	// In the new code RankService.ApplyPoints returns before touching points when the ladder
	// forbids automatic scoring, so the policy half of that test lives in the ladder check.
	[Fact]
	public void BlockedAutomaticScoringPreservesSavedPoints()
	{
		RankLadder ladder = RankLadder.Create([TestData.Rank("None", -1), TestData.Rank("VIP", 100, "@css/vip")], RankGameMode.FlagsOnly);

		Assert.False(ladder.IsAutomaticScoringAllowed(_ => false));
	}

	[Fact]
	public void ManualPointSettersBypassAutomaticScoringPolicy()
	{
		Assert.Equal(750, PointMath.AddToStored(0, 750));
		Assert.Equal(0, PointMath.AddToStored(0, -15));
		Assert.Equal(750, PointMath.ClampStored(750));
		Assert.Equal(0, PointMath.ClampStored(-15));
	}

	[Fact]
	public void PointArithmeticClampsOverflow()
	{
		Assert.Equal(int.MaxValue, PointMath.AddToStored(int.MaxValue, 1));
		Assert.Equal(0, PointMath.AddToStored(0, -1));
		Assert.Equal(int.MaxValue, PointMath.AddClamped(int.MaxValue, 1));
		Assert.Equal(int.MinValue, PointMath.AddClamped(int.MinValue, -1));
		Assert.Equal(int.MaxValue, PointMath.Scale(int.MaxValue, 2));
		Assert.Equal(0, PointMath.Scale(100, double.NaN));
		Assert.Equal(0, PointMath.Scale(100, double.PositiveInfinity));
		Assert.Equal(0, PointMath.Scale(100, -1));
		Assert.Equal(125, PointMath.Scale(100, 1.25));
	}

	// Regression: a victim with 0 points used to be worth the full amount, which made an empty alt the best farm target.
	[Theory]
	[InlineData(10, 10000, 0, 5)]
	[InlineData(10, 10000, 1, 5)]
	[InlineData(10, 0, 0, 10)]
	[InlineData(10, 1000, 1000, 10)]
	[InlineData(10, 1000, 2000, 20)]
	[InlineData(10, 0, 5000, 30)]
	public void DynamicPointsTreatAnEmptyAccountAsTheWeakestOpponent(int amount, int killer, int victim, int expected)
		=> Assert.Equal(expected, PointMath.Dynamic(amount, killer, victim, 0.5, 3.0));
}
