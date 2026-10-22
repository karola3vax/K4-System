namespace K4System.Storage;

using K4System.Missions;

/// <summary>An immutable copy of one player's data, captured on the game thread and saved in the background.</summary>
/// <param name="RankName">Null when the rank module is disabled; the ranks row is then left alone.</param>
/// <param name="RankId">Runtime rank id, mirrored to LevelRanks. -1 for the default rank or no rank data.</param>
/// <param name="Stats">Null when the stat module is disabled.</param>
/// <param name="Playtime">Null when the playtime module is disabled.</param>
/// <param name="PersonalRankResetUtc">Set when this save must also clear reward claims and record a personal reset.</param>
/// <param name="Missions">Today's missions and streak. Null when missions are off or not loaded yet.</param>
internal sealed record PlayerSnapshot(
	ulong SteamId,
	string Name,
	string? RankName,
	int RankId,
	int Points,
	int[]? Stats,
	int[]? Playtime,
	DateTime? PersonalRankResetUtc,
	MissionSnapshot? Missions = null)
{
	/// <summary>A newer snapshot replaces an older one but must not lose a pending personal reset.</summary>
	public PlayerSnapshot MergeOnto(PlayerSnapshot? previous)
		=> previous?.PersonalRankResetUtc is not null && PersonalRankResetUtc is null
			? this with { PersonalRankResetUtc = previous.PersonalRankResetUtc }
			: this;
}

/// <summary>A player's stored data, as loaded on connect.</summary>
/// <param name="Placement">Leaderboard position, starting at 1.</param>
internal sealed record PlayerRecord(int Points, int[] Stats, int[] Playtime, DateTime? LastPersonalRankResetUtc, int Placement);

internal sealed record TopEntry(int Points, string Name, string Rank);

internal sealed record StoredReward(string RewardId, string RankName);
