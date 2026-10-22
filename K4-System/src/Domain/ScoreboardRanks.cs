namespace K4System.Domain;

using K4System.Config;

internal readonly record struct ScoreboardRankValue(sbyte RankType, int Ranking);

/// <summary>Maps K4 points and ranks to CS2's matchmaking rank fields.</summary>
internal static class ScoreboardRanks
{
	public const float RevealIntervalSeconds = 5.0f;
	public const string RevealMessageName = "CCSUsrMsg_ServerRankRevealAll";

	public static bool ShouldEnable(ScoreboardRankMode mode, bool followsServerGuidelines)
		=> mode != ScoreboardRankMode.Disabled && !followsServerGuidelines;

	/// <param name="internalRankId">Runtime rank id: -1 for the default rank, then 0, 1, 2...</param>
	public static ScoreboardRankValue GetValue(ScoreboardRankMode mode, int internalRankId, int points, int customBase, int customCount, int customOffset)
	{
		int rankId = internalRankId + 1;

		return mode switch
		{
			ScoreboardRankMode.Premier => new ScoreboardRankValue(11, Math.Max(1, points)),
			ScoreboardRankMode.Competitive => new ScoreboardRankValue(12, Math.Clamp(rankId, 0, 18)),
			ScoreboardRankMode.Wingman => new ScoreboardRankValue(7, Math.Clamp(rankId, 0, 18)),
			ScoreboardRankMode.Custom when customBase > 0 && customCount > 0 =>
				new ScoreboardRankValue(12, customBase + Math.Clamp(rankId - customOffset, 0, customCount - 1)),
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The scoreboard rank mode cannot produce a rank value.")
		};
	}
}
