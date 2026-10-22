namespace K4System.Domain;

internal readonly record struct BalanceMember<T>(T Player, bool IsTerrorist, int Points);

internal readonly record struct BalanceMove<T>(T Player, bool ToTerrorist);

/// <summary>
/// Rank-based team balance. First evens the team sizes by moving the lowest-point players,
/// then swaps the strongest and weakest players while that reduces the point gap.
/// </summary>
internal static class TeamBalancer
{
	public const int MaxSwaps = 10;

	public static (List<BalanceMove<T>> Moves, bool SwapLimitReached) Balance<T>(IEnumerable<BalanceMember<T>> members, int maxDifference)
		where T : notnull
	{
		List<BalanceMember<T>> terrorists = members.Where(member => member.IsTerrorist).ToList();
		List<BalanceMember<T>> counterTerrorists = members.Where(member => !member.IsTerrorist).ToList();
		Dictionary<T, bool> destinations = [];
		List<T> order = [];

		void Move(BalanceMember<T> member, bool toTerrorist)
		{
			if (!destinations.ContainsKey(member.Player))
				order.Add(member.Player);
			destinations[member.Player] = toTerrorist;
		}

		while (Math.Abs(terrorists.Count - counterTerrorists.Count) > 1)
		{
			bool terroristsLarger = terrorists.Count > counterTerrorists.Count;
			List<BalanceMember<T>> larger = terroristsLarger ? terrorists : counterTerrorists;
			List<BalanceMember<T>> smaller = terroristsLarger ? counterTerrorists : terrorists;
			BalanceMember<T> member = larger.OrderBy(candidate => candidate.Points).First();

			larger.Remove(member);
			smaller.Add(member);
			Move(member, !terroristsLarger);
		}

		int remainingSwaps = MaxSwaps;
		while (terrorists.Count > 0 && counterTerrorists.Count > 0 && remainingSwaps-- > 0)
		{
			long terroristPoints = terrorists.Sum(member => (long)member.Points);
			long counterTerroristPoints = counterTerrorists.Sum(member => (long)member.Points);
			long difference = Math.Abs(terroristPoints - counterTerroristPoints);

			if (difference <= maxDifference)
				break;

			bool terroristsStronger = terroristPoints > counterTerroristPoints;
			List<BalanceMember<T>> stronger = terroristsStronger ? terrorists : counterTerrorists;
			List<BalanceMember<T>> weaker = terroristsStronger ? counterTerrorists : terrorists;
			BalanceMember<T> strongest = stronger.OrderByDescending(member => member.Points).First();
			BalanceMember<T> weakest = weaker.OrderBy(member => member.Points).First();
			long delta = (long)strongest.Points - weakest.Points;

			if (delta <= 0 || Math.Abs(difference - (2 * delta)) >= difference)
				break;

			stronger.Remove(strongest);
			weaker.Remove(weakest);
			stronger.Add(weakest);
			weaker.Add(strongest);

			Move(strongest, !terroristsStronger);
			Move(weakest, terroristsStronger);
		}

		List<BalanceMove<T>> moves = order.Select(player => new BalanceMove<T>(player, destinations[player])).ToList();
		return (moves, remainingSwaps < 0);
	}
}
