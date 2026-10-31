namespace K4System.Domain;

/// <summary>Bounty and duel rules that need no game objects.</summary>
internal static class Showdowns
{
	/// <summary>The largest duel stake: a share of the poorer player's points.</summary>
	public static int MaxStake(int challengerPoints, int targetPoints, int percent)
		=> (int)(Math.Max(0L, Math.Min(challengerPoints, targetPoints)) * Math.Clamp(percent, 0, 100) / 100);

	/// <summary>Who takes the pot: 1 the challenger, -1 the opponent, 0 a tie or nobody left.</summary>
	/// <remarks>A player who is not on the server at the end loses, even with more kills.</remarks>
	public static int Winner(bool challengerPresent, int challengerKills, bool opponentPresent, int opponentKills)
	{
		if (challengerPresent != opponentPresent)
			return challengerPresent ? 1 : -1;

		return challengerPresent ? Math.Sign(challengerKills.CompareTo(opponentKills)) : 0;
	}

	/// <summary>
	/// What each player of an unfinished duel gets when K4 unloads: their own stake back, or both stakes for the
	/// player still here when the other has left (a leaver loses, as at the match end).
	/// </summary>
	public static (int Challenger, int Opponent) UnloadRefund(bool challengerPresent, bool opponentPresent, int stake)
	{
		int pot = PointMath.AddClamped(stake, stake);
		return (challengerPresent, opponentPresent) switch
		{
			(true, true) => (stake, stake),
			(true, false) => (pot, 0),
			(false, true) => (0, pot),
			_ => (0, 0)
		};
	}

	/// <summary>Two players connect from the same address. Unknown addresses never match.</summary>
	public static bool SameAddress(string? left, string? right)
	{
		string host = Host(left);
		return host.Length > 0 && host == Host(right);
	}

	// "1.2.3.4:27005" -> "1.2.3.4"; bots and loopback players have no usable address.
	private static string Host(string? address)
	{
		if (string.IsNullOrWhiteSpace(address))
			return string.Empty;

		int colon = address.LastIndexOf(':');
		string host = (colon > 0 ? address[..colon] : address).Trim();
		return host is "loopback" or "0.0.0.0" or "127.0.0.1" ? string.Empty : host;
	}
}
