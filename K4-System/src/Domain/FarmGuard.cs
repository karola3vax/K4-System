namespace K4System.Domain;

/// <summary>Anti-farming rules for kill points that need no game objects.</summary>
internal static class FarmGuard
{
	/// <summary>
	/// The share of the normal points a kill is worth: 0 between players on the same connection, the reduced share once
	/// the killer already got <paramref name="fullKills"/> kills on this victim inside the window, otherwise all of it.
	/// </summary>
	/// <param name="earlierKills">Kills by the same killer on the same victim inside the window, before this one.</param>
	/// <param name="fullKills">Kills on one victim that count fully. 0 turns the repeat-kill rule off.</param>
	public static double KillShare(bool sameAddress, int earlierKills, int fullKills, int reducedPercent)
	{
		if (sameAddress)
			return 0;

		if (fullKills <= 0 || earlierKills < fullKills)
			return 1;

		return Math.Clamp(reducedPercent, 0, 100) / 100.0;
	}
}

/// <summary>When one killer last killed each victim, for the repeat-kill rule. Only touched on the game thread.</summary>
internal sealed class KillHistory
{
	private readonly Dictionary<ulong, Queue<DateTime>> kills = [];

	/// <summary>Records a kill on the victim and returns how many earlier kills on them are inside the window.</summary>
	public int Record(ulong victim, DateTime nowUtc, TimeSpan window)
	{
		if (!kills.TryGetValue(victim, out Queue<DateTime>? times))
			kills[victim] = times = new Queue<DateTime>();

		while (times.Count > 0 && nowUtc - times.Peek() >= window)
			times.Dequeue();

		int earlier = times.Count;
		times.Enqueue(nowUtc);
		return earlier;
	}

	/// <summary>Forgets victims with no kill inside the window, so the history cannot grow over a long session.</summary>
	public void Prune(DateTime nowUtc, TimeSpan window)
	{
		foreach ((ulong victim, Queue<DateTime> times) in kills.ToArray())
		{
			while (times.Count > 0 && nowUtc - times.Peek() >= window)
				times.Dequeue();
			if (times.Count == 0)
				kills.Remove(victim);
		}
	}

	public int VictimCount => kills.Count;
}
