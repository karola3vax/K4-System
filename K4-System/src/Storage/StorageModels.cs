namespace K4System.Storage;

using K4System.Missions;

/// <summary>An immutable copy of one player's data, captured on the game thread and saved in the background.</summary>
/// <param name="RankName">Null when the rank module is disabled; the ranks row is then left alone.</param>
/// <param name="RankId">Runtime rank id, mirrored to LevelRanks. -1 for the default rank or no rank data.</param>
/// <param name="Stats">Null when the stat module is disabled.</param>
/// <param name="Playtime">Null when the playtime module is disabled.</param>
/// <param name="PersonalRankResetUtc">Set when this save must also clear reward claims and record a personal reset.</param>
/// <param name="Missions">Today's missions and streak. Null when missions are off or not loaded yet.</param>
/// <param name="NewAchievements">Achievements unlocked since the last save; earlier ones are already stored.</param>
internal sealed record PlayerSnapshot(
	ulong SteamId,
	string Name,
	string? RankName,
	int RankId,
	int Points,
	int[]? Stats,
	int[]? Playtime,
	DateTime? PersonalRankResetUtc,
	MissionSnapshot? Missions = null,
	IReadOnlyCollection<string>? NewAchievements = null)
{
	/// <summary>A newer snapshot replaces an older one but must not lose a pending personal reset or new achievements.</summary>
	public PlayerSnapshot MergeOnto(PlayerSnapshot? previous)
	{
		if (previous is null)
			return this;

		PlayerSnapshot merged = this;
		if (previous.PersonalRankResetUtc is not null && PersonalRankResetUtc is null)
			merged = merged with { PersonalRankResetUtc = previous.PersonalRankResetUtc };
		if (previous.NewAchievements is { Count: > 0 } older)
			merged = merged with { NewAchievements = [.. (NewAchievements ?? []).Union(older, StringComparer.Ordinal)] };
		return merged;
	}
}

/// <summary>A player's stored data, as loaded on connect.</summary>
/// <param name="Placement">Leaderboard position, starting at 1.</param>
/// <param name="Achievements">Keys of the achievements the player has unlocked.</param>
internal sealed record PlayerRecord(int Points, int[] Stats, int[] Playtime, DateTime? LastPersonalRankResetUtc, int Placement, IReadOnlyCollection<string> Achievements);

internal sealed record TopEntry(int Points, string Name, string Rank);

internal sealed record StoredReward(string RewardId, string RankName);

/// <summary>A manual point change for the audit log: an admin command, a reset, or another plugin.</summary>
/// <param name="Source">set, give, remove, reset, self-reset, or api.</param>
/// <param name="Actor">Who did it, such as "Admin (76561198000000001)", "console", or a plugin.</param>
internal sealed record PointChange(ulong SteamId, int Delta, int PointsAfter, string Source, string Actor);

internal sealed record PointHistoryEntry(DateTime ChangedUtc, int Delta, int PointsAfter, string Source, string Actor);
