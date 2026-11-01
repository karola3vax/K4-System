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
/// <param name="Season">The season the points belong to. Points from an earlier season are not written over a newer
/// season's reset. Null when seasons are off.</param>
/// <param name="WeaponKills">Kill totals of the weapons that changed since the last save.</param>
/// <param name="Showcase">The achievements shown on the profile, comma separated; null when unchanged.</param>
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
	IReadOnlyCollection<string>? NewAchievements = null,
	string? Season = null,
	IReadOnlyDictionary<string, int>? WeaponKills = null,
	string? Showcase = null)
{
	/// <summary>A newer snapshot replaces an older one but must not lose a pending personal reset, new achievements,
	/// changed weapon totals, or a changed showcase.</summary>
	public PlayerSnapshot MergeOnto(PlayerSnapshot? previous)
	{
		if (previous is null)
			return this;

		PlayerSnapshot merged = this;
		if (previous.PersonalRankResetUtc is not null && PersonalRankResetUtc is null)
			merged = merged with { PersonalRankResetUtc = previous.PersonalRankResetUtc };
		if (previous.NewAchievements is { Count: > 0 } older)
			merged = merged with { NewAchievements = [.. (NewAchievements ?? []).Union(older, StringComparer.Ordinal)] };
		if (previous.WeaponKills is { Count: > 0 } olderKills)
		{
			Dictionary<string, int> kills = new(olderKills, StringComparer.Ordinal);
			foreach ((string weapon, int count) in WeaponKills ?? new Dictionary<string, int>())
				kills[weapon] = count;
			merged = merged with { WeaponKills = kills };
		}
		if (Showcase is null && previous.Showcase is not null)
			merged = merged with { Showcase = previous.Showcase };
		return merged;
	}
}

/// <summary>A player's stored data, as loaded on connect.</summary>
/// <param name="Placement">Leaderboard position, starting at 1.</param>
/// <param name="Achievements">Keys of the achievements the player has unlocked.</param>
/// <param name="Season">The season stored in the database when the player was loaded; null when seasons are off.</param>
/// <param name="WeaponKills">Kills per weapon, by kill-event name such as "ak47".</param>
/// <param name="Showcase">The achievements the player shows on their profile, in order.</param>
internal sealed record PlayerRecord(
	int Points,
	int[] Stats,
	int[] Playtime,
	DateTime? LastPersonalRankResetUtc,
	int Placement,
	IReadOnlyCollection<string> Achievements,
	string? Season = null,
	IReadOnlyDictionary<string, int>? WeaponKills = null,
	IReadOnlyList<string>? Showcase = null);

/// <param name="SteamId">0 when unknown; the panel then cannot open the player's profile.</param>
internal sealed record TopEntry(int Points, string Name, string Rank, ulong SteamId = 0);

/// <summary>A player's result in a finished season.</summary>
/// <param name="Season">The season id, the date it started.</param>
/// <param name="Rank">The rank name the player ended the season with.</param>
/// <param name="PeakPoints">The most points the player had during the season.</param>
/// <param name="Placement">The final position among the season's players, starting at 1.</param>
internal sealed record SeasonResult(string Season, int Points, string Rank, int PeakPoints, int Placement, int Players);

/// <summary>Another player's stored profile, read without creating rows for them.</summary>
internal sealed record ProfileRecord(ulong SteamId, string Name, string Rank, PlayerRecord Record, int Total, IReadOnlyList<SeasonResult> Seasons);

internal sealed record StoredReward(string RewardId, string RankName);

/// <summary>A manual point change for the audit log: an admin command, a reset, or another plugin.</summary>
/// <param name="Source">set, give, remove, reset, self-reset, or api.</param>
/// <param name="Actor">Who did it, such as "Admin (76561198000000001)", "console", or a plugin.</param>
internal sealed record PointChange(ulong SteamId, int Delta, int PointsAfter, string Source, string Actor);

internal sealed record PointHistoryEntry(DateTime ChangedUtc, int Delta, int PointsAfter, string Source, string Actor);
