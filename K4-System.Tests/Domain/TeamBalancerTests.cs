namespace K4System.Tests.Domain;

using K4System.Domain;

public class TeamBalancerTests
{
	private static BalanceMember<string> T(string name, int points) => new(name, true, points);

	private static BalanceMember<string> Ct(string name, int points) => new(name, false, points);

	[Fact]
	public void SizeBalancingMovesLowestPointPlayers()
	{
		BalanceMember<string>[] members =
		[
			T("t300", 300), T("t100", 100), T("t500", 500), T("t200", 200), T("t400", 400),
			Ct("ct1000", 1000)
		];

		(List<BalanceMove<string>> moves, bool limitReached) = TeamBalancer.Balance(members, int.MaxValue);

		Assert.False(limitReached);
		Assert.Equal(
			[new BalanceMove<string>("t100", false), new BalanceMove<string>("t200", false)],
			moves);
	}

	[Fact]
	public void SizeBalancingAllowsOnePlayerDifference()
	{
		BalanceMember<string>[] members = [T("a", 10), T("b", 20), T("c", 30), Ct("d", 40), Ct("e", 50)];

		(List<BalanceMove<string>> moves, _) = TeamBalancer.Balance(members, int.MaxValue);

		Assert.Empty(moves);
	}

	[Fact]
	public void SwapsReduceThePointGap()
	{
		// T = 550, CT = 300, gap 250. Swapping t300 with ct100 leaves T = 350, CT = 500, gap 150.
		BalanceMember<string>[] members = [T("t300", 300), T("t250", 250), Ct("ct200", 200), Ct("ct100", 100)];

		(List<BalanceMove<string>> moves, bool limitReached) = TeamBalancer.Balance(members, 0);

		Assert.False(limitReached);
		Assert.Equal(
			[new BalanceMove<string>("t300", false), new BalanceMove<string>("ct100", true)],
			moves);

		long terrorists = 0;
		long counterTerrorists = 0;
		foreach (BalanceMember<string> member in members)
		{
			bool isTerrorist = moves.Any(move => move.Player == member.Player) ? moves.Single(move => move.Player == member.Player).ToTerrorist : member.IsTerrorist;
			if (isTerrorist)
				terrorists += member.Points;
			else
				counterTerrorists += member.Points;
		}

		Assert.True(Math.Abs(terrorists - counterTerrorists) < 250);
	}

	[Fact]
	public void StopsWithinMaxDifference()
	{
		BalanceMember<string>[] members = [T("t300", 300), T("t250", 250), Ct("ct200", 200), Ct("ct100", 100)];

		(List<BalanceMove<string>> moves, bool limitReached) = TeamBalancer.Balance(members, 250);

		Assert.Empty(moves);
		Assert.False(limitReached);
	}

	[Fact]
	public void DoesNotSwapWhenSwapWouldNotReduceGap()
	{
		// Gap 300; best swap moves 400 points each way and would widen the gap to 500.
		BalanceMember<string>[] members = [T("t500", 500), T("t100", 100), Ct("ct200", 200), Ct("ct100", 100)];

		(List<BalanceMove<string>> moves, _) = TeamBalancer.Balance(members, 0);

		Assert.Empty(moves);
	}
}
