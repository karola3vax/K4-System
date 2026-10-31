namespace K4System.Achievements;

using System.Text.Json.Serialization;
using K4System.Players;

/// <summary>What an achievement counts. Every type reads a counter K4 already stores, so progress is never lost.</summary>
public enum AchievementType
{
	Kills,
	Headshots,
	Assists,
	Mvps,
	RoundWins,
	MatchWins,
	OpeningKills,
	BombPlants,
	BombDefuses,
	HostageRescues,
	NoscopeKills,
	WallbangKills,
	SmokeKills,
	BlindKills,
	RevengeKills,
	PlaytimeHours,
	MissionStreak
}

/// <summary>One entry of achievements.jsonc.</summary>
public sealed class AchievementDefinition
{
	/// <summary>The key in achievements.jsonc. Stored in the database, so it must not change.</summary>
	[JsonIgnore]
	public string Id { get; set; } = string.Empty;

	public AchievementType Type { get; set; }

	public int Goal { get; set; }

	/// <summary>Points given once when it unlocks.</summary>
	public int Points { get; set; }

	/// <summary>A server command run once when it unlocks, with the same placeholders as rank rewards.</summary>
	public string? Command { get; set; }

	/// <summary>The shown name. Without it, the language files' "achievement.name.&lt;key&gt;" is used.</summary>
	public string? Name { get; set; }
}

/// <summary>The counters achievements read. Null for a module that is off, so its achievements never unlock.</summary>
internal readonly record struct AchievementCounters(int[]? Stats, int? PlaytimeSeconds, int? MissionStreak);

/// <summary>Achievement rules that need no game objects.</summary>
internal static class AchievementRules
{
	public static StatField? StatOf(AchievementType type) => type switch
	{
		AchievementType.Kills => StatField.Kills,
		AchievementType.Headshots => StatField.Headshots,
		AchievementType.Assists => StatField.Assists,
		AchievementType.Mvps => StatField.Mvp,
		AchievementType.RoundWins => StatField.RoundWin,
		AchievementType.MatchWins => StatField.GameWin,
		AchievementType.OpeningKills => StatField.FirstBlood,
		AchievementType.BombPlants => StatField.BombPlanted,
		AchievementType.BombDefuses => StatField.BombDefused,
		AchievementType.HostageRescues => StatField.HostageRescued,
		AchievementType.NoscopeKills => StatField.NoscopeKill,
		AchievementType.WallbangKills => StatField.PenetratedKill,
		AchievementType.SmokeKills => StatField.ThrusmokeKill,
		AchievementType.BlindKills => StatField.FlashedKill,
		AchievementType.RevengeKills => StatField.RevengeKill,
		_ => null
	};

	/// <summary>The language key that describes the goal, such as "Get {0} kills". Most reuse the mission texts.</summary>
	public static string GoalKey(AchievementType type) => type switch
	{
		AchievementType.Kills => "mission.kills",
		AchievementType.Headshots => "mission.headshots",
		AchievementType.Assists => "mission.assists",
		AchievementType.Mvps => "mission.mvps",
		AchievementType.RoundWins => "mission.round-wins",
		AchievementType.MatchWins => "mission.match-wins",
		AchievementType.OpeningKills => "mission.opening-kills",
		AchievementType.BombPlants => "mission.bomb-plants",
		AchievementType.BombDefuses => "mission.bomb-defuses",
		AchievementType.HostageRescues => "mission.hostage-rescues",
		AchievementType.NoscopeKills => "mission.noscope-kills",
		AchievementType.WallbangKills => "mission.wallbang-kills",
		AchievementType.SmokeKills => "mission.smoke-kills",
		AchievementType.BlindKills => "mission.blind-kills",
		AchievementType.RevengeKills => "mission.revenge-kills",
		AchievementType.PlaytimeHours => "achievement.goal.playtime-hours",
		_ => "achievement.goal.mission-streak"
	};

	/// <summary>How far the player is, or null when the module the achievement reads is off.</summary>
	public static int? Progress(AchievementDefinition achievement, AchievementCounters counters) => achievement.Type switch
	{
		AchievementType.PlaytimeHours => counters.PlaytimeSeconds / 3600,
		AchievementType.MissionStreak => counters.MissionStreak,
		AchievementType type => counters.Stats is int[] stats && StatOf(type) is StatField field ? stats[(int)field] : null
	};

	/// <summary>Achievements whose goal is reached but that are not unlocked yet, in file order.</summary>
	public static List<AchievementDefinition> NewlyMet(IEnumerable<AchievementDefinition> all, IReadOnlySet<string> unlocked, AchievementCounters counters)
		=> all.Where(achievement => !unlocked.Contains(achievement.Id) && Progress(achievement, counters) >= achievement.Goal).ToList();

	/// <summary>Unfinished achievements the player can work on, closest to done first.</summary>
	public static List<(AchievementDefinition Achievement, int Progress)> Closest(IEnumerable<AchievementDefinition> all, IReadOnlySet<string> unlocked, AchievementCounters counters, int count)
		=> all
			.Where(achievement => !unlocked.Contains(achievement.Id))
			.Select(achievement => (Achievement: achievement, Progress: Progress(achievement, counters)))
			.Where(item => item.Progress is not null)
			.Select(item => (item.Achievement, Progress: Math.Min(item.Progress!.Value, item.Achievement.Goal)))
			.OrderByDescending(item => (double)item.Progress / item.Achievement.Goal)
			.ThenBy(item => item.Achievement.Goal)
			.Take(count)
			.ToList();
}

/// <summary>A player's unlocked achievements. Game thread only.</summary>
public sealed class PlayerAchievements(IEnumerable<string> stored)
{
	private readonly HashSet<string> unlocked = new(stored, StringComparer.Ordinal);
	private readonly HashSet<string> unsaved = new(StringComparer.Ordinal);

	public IReadOnlySet<string> Unlocked => unlocked;

	public void Unlock(string id)
	{
		if (unlocked.Add(id))
			unsaved.Add(id);
	}

	/// <summary>Unlocks since the last save, handed to that save. The save queue keeps them until they are written.</summary>
	internal string[] TakeUnsaved()
	{
		string[] taken = [.. unsaved];
		unsaved.Clear();
		return taken;
	}
}
