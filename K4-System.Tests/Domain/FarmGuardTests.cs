namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Domain;

public class FarmGuardTests
{
	[Theory]
	[InlineData(true, 0, 10, 25, 0.0)]
	[InlineData(false, 0, 10, 25, 1.0)]
	[InlineData(false, 9, 10, 25, 1.0)]
	[InlineData(false, 10, 10, 25, 0.25)]
	[InlineData(false, 50, 10, 25, 0.25)]
	[InlineData(false, 50, 0, 25, 1.0)]
	[InlineData(false, 10, 10, 250, 1.0)]
	[InlineData(false, 10, 10, -5, 0.0)]
	public void KillShareFollowsTheRules(bool sameAddress, int earlier, int fullKills, int reducedPercent, double expected)
		=> Assert.Equal(expected, FarmGuard.KillShare(sameAddress, earlier, fullKills, reducedPercent));

	[Fact]
	public void HistoryCountsOnlyKillsInsideTheWindow()
	{
		KillHistory history = new();
		DateTime start = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
		TimeSpan window = TimeSpan.FromMinutes(10);

		Assert.Equal(0, history.Record(1, start, window));
		Assert.Equal(1, history.Record(1, start.AddMinutes(1), window));
		Assert.Equal(0, history.Record(2, start.AddMinutes(2), window));
		// The first kill is exactly ten minutes old and no longer counts.
		Assert.Equal(1, history.Record(1, start.AddMinutes(10), window));
	}

	[Fact]
	public void PruneDropsVictimsWithoutRecentKills()
	{
		KillHistory history = new();
		DateTime start = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
		TimeSpan window = TimeSpan.FromMinutes(10);
		history.Record(1, start, window);
		history.Record(2, start.AddMinutes(8), window);

		history.Prune(start.AddMinutes(12), window);

		Assert.Equal(1, history.VictimCount);
	}

	private sealed record FarmCase(string Name, bool SameAddress, int FullKills, int WindowMinutes, int ReducedPercent, int[] KillMinutes, int KillPoints, int[] ExpectedPoints);

	/// <summary>Whole sessions from Evals/anti-farm.json: one killer, one victim, kills at the given minutes.</summary>
	[Fact]
	public void Eval_sessions_pay_the_expected_points()
	{
		FarmCase[] cases = JsonSerializer.Deserialize<FarmCase[]>(File.ReadAllText(TestData.EvalPath("anti-farm.json")))!;
		Assert.NotEmpty(cases);

		foreach (FarmCase test in cases)
		{
			KillHistory history = new();
			DateTime start = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
			int[] paid = test.KillMinutes
				.Select(minute => history.Record(7, start.AddMinutes(minute), TimeSpan.FromMinutes(test.WindowMinutes)))
				.Select(earlier => PointMath.Scale(test.KillPoints, FarmGuard.KillShare(test.SameAddress, earlier, test.FullKills, test.ReducedPercent)))
				.ToArray();

			Assert.True(test.ExpectedPoints.SequenceEqual(paid), $"{test.Name}: expected [{string.Join(", ", test.ExpectedPoints)}], got [{string.Join(", ", paid)}]");
		}
	}
}
