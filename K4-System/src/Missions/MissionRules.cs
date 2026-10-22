namespace K4System.Missions;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Pure rules for missions: days and weeks, assignment, swaps, rerolls, streaks, and progress text.</summary>
internal static class MissionRules
{
	public const string DayFormat = "yyyy-MM-dd";

	/// <summary>The database slot of the weekly mission. Its row is stored under the Monday its week starts on.</summary>
	public const int WeeklySlot = 3;

	public static readonly MissionDifficulty[] DailyDifficulties = [MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard];

	/// <summary>
	/// Accepts an IANA or Windows timezone name, "UTC", or a fixed offset such as "+03:00".
	/// Unknown names fall back to UTC with a warning, because some Linux hosts ship no timezone data.
	/// </summary>
	public static (TimeZoneInfo Zone, string? Warning) ResolveTimeZone(string? name)
	{
		string value = name?.Trim() ?? string.Empty;

		if (value.Length == 0 || value.Equals("UTC", StringComparison.OrdinalIgnoreCase))
			return (TimeZoneInfo.Utc, null);

		if ((value[0] is '+' or '-') && TimeSpan.TryParseExact(value[1..], ["hh\\:mm", "h\\:mm", "hh", "h"], CultureInfo.InvariantCulture, out TimeSpan offset))
		{
			TimeSpan signed = value[0] == '-' ? -offset : offset;
			if (offset > TimeSpan.FromHours(14) || signed.Ticks % TimeSpan.TicksPerMinute != 0)
				return (TimeZoneInfo.Utc, $"missions.timezone '{value}' is outside -14:00 to +14:00. Missions reset at midnight UTC.");
			return (TimeZoneInfo.CreateCustomTimeZone($"UTC{value}", signed, $"UTC{value}", $"UTC{value}"), null);
		}

		try
		{
			return (TimeZoneInfo.FindSystemTimeZoneById(value), null);
		}
		catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			return (TimeZoneInfo.Utc, $"missions.timezone '{value}' is not known on this server. Missions reset at midnight UTC; use an offset such as \"+03:00\" instead.");
		}
	}

	public static DateOnly Today(DateTime utcNow, TimeZoneInfo zone)
		=> DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));

	/// <summary>The Monday the week of this day starts on.</summary>
	public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

	public static TimeSpan UntilReset(DateTime utcNow, TimeZoneInfo zone) => UntilMidnight(utcNow, zone, days: 1);

	public static TimeSpan UntilWeeklyReset(DateTime utcNow, TimeZoneInfo zone)
	{
		DateOnly today = Today(utcNow, zone);
		return UntilMidnight(utcNow, zone, days: WeekStart(today).AddDays(7).DayNumber - today.DayNumber);
	}

	private static TimeSpan UntilMidnight(DateTime utcNow, TimeZoneInfo zone, int days)
	{
		DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
		DateTime nextMidnight = local.Date.AddDays(days);

		// Some zones jump from 23:59 to 01:00 on their daylight-saving day; the day starts at the first valid minute.
		for (int minute = 0; minute < 180 && zone.IsInvalidTime(nextMidnight); minute++)
			nextMidnight = nextMidnight.AddMinutes(1);

		DateTime nextMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextMidnight, DateTimeKind.Unspecified), zone);
		TimeSpan remaining = nextMidnightUtc - DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
	}

	public static string FormatDay(DateOnly day) => day.ToString(DayFormat, CultureInfo.InvariantCulture);

	public static DateOnly? ParseDay(string? value)
		=> DateOnly.TryParseExact(value, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day) ? day : null;

	/// <summary>
	/// One Easy, one Medium, and one Hard mission, the same for a player all day, different between players and days.
	/// When the map is known, only missions that can be done on it are picked.
	/// </summary>
	public static IReadOnlyList<MissionDefinition> Assign(MissionPool pool, ulong steamId, DateOnly day, MapAbilities? map = null)
	{
		List<MissionDefinition> missions = [];

		foreach (MissionDifficulty difficulty in DailyDifficulties)
		{
			IReadOnlyList<MissionDefinition> choices = Doable(pool.Of(difficulty), map);
			missions.Add(choices[Pick(choices.Count, steamId, day, difficulty.ToString())]);
		}

		return missions;
	}

	/// <summary>The week's mission, or null when missions.jsonc has no Weekly missions.</summary>
	public static MissionDefinition? AssignWeekly(MissionPool pool, ulong steamId, DateOnly weekStart, MapAbilities? map = null)
	{
		IReadOnlyList<MissionDefinition> choices = Doable(pool.Of(MissionDifficulty.Weekly), map);
		return choices.Count == 0 ? null : choices[Pick(choices.Count, steamId, weekStart, nameof(MissionDifficulty.Weekly))];
	}

	/// <summary>The missions the map allows, or all of them when the map is unknown or allows none.</summary>
	private static IReadOnlyList<MissionDefinition> Doable(IReadOnlyList<MissionDefinition> choices, MapAbilities? map)
	{
		if (map is null)
			return choices;

		List<MissionDefinition> doable = choices.Where(map.Allows).ToList();
		return doable.Count == 0 ? choices : doable;
	}

	/// <summary>A different mission of the same difficulty, or null when there is no other choice.</summary>
	public static MissionDefinition? Reroll(MissionPool pool, ulong steamId, DateOnly day, MissionDefinition current, IEnumerable<string> todaysIds, MapAbilities? map = null)
		=> Replacement(pool, steamId, day, current, todaysIds, map, "reroll:");

	/// <summary>
	/// A mission of the same difficulty that the map allows, for one it does not. Null when nothing fits, then the
	/// player keeps the old one and can finish it on another map.
	/// </summary>
	public static MissionDefinition? Swap(MissionPool pool, ulong steamId, DateOnly day, MissionDefinition current, IEnumerable<string> todaysIds, MapAbilities map)
	{
		HashSet<string> taken = new(todaysIds, StringComparer.Ordinal);
		List<MissionDefinition> choices = pool.Of(current.Difficulty).Where(mission => !taken.Contains(mission.Id) && map.Allows(mission)).ToList();
		return choices.Count == 0 ? null : choices[Pick(choices.Count, steamId, day, "swap:" + current.Id)];
	}

	private static MissionDefinition? Replacement(MissionPool pool, ulong steamId, DateOnly day, MissionDefinition current, IEnumerable<string> todaysIds, MapAbilities? map, string salt)
	{
		HashSet<string> taken = new(todaysIds, StringComparer.Ordinal);
		IReadOnlyList<MissionDefinition> choices = Doable(pool.Of(current.Difficulty).Where(mission => !taken.Contains(mission.Id)).ToList(), map);
		return choices.Count == 0 ? null : choices[Pick(choices.Count, steamId, day, salt + current.Id)];
	}

	private static int Pick(int count, ulong steamId, DateOnly day, string salt)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{steamId}|{FormatDay(day)}|{salt}"));
		return (int)(BitConverter.ToUInt32(hash, 0) % (uint)count);
	}

	/// <summary>The streak shown today: it is broken once a whole day passed without finishing all missions.</summary>
	public static int CurrentStreak(int streak, DateOnly? lastCompletedDay, DateOnly today)
		=> lastCompletedDay is DateOnly last && (last == today || last == today.AddDays(-1)) ? streak : 0;

	/// <summary>The streak after finishing all of today's missions.</summary>
	public static int NextStreak(int streak, DateOnly? lastCompletedDay, DateOnly today)
	{
		if (lastCompletedDay == today)
			return streak;
		return lastCompletedDay == today.AddDays(-1) ? streak + 1 : 1;
	}

	/// <summary>Streak bonus in points: percentPerDay for every streak day, up to maxDays.</summary>
	public static int StreakBonus(int streak, int dayRewardTotal, int percentPerDay, int maxDays)
	{
		long percent = (long)Math.Clamp(streak, 0, Math.Max(0, maxDays)) * Math.Max(0, percentPerDay);
		return (int)Math.Clamp(dayRewardTotal * percent / 100, 0, int.MaxValue);
	}

	public static string ProgressBar(int progress, int target, int segments = 10)
	{
		int filled = target <= 0 ? segments : (int)Math.Clamp((long)progress * segments / target, 0, segments);
		return new string('▰', filled) + new string('▱', segments - filled);
	}

	/// <summary>"5h 07m", or "3d 04h" from one day up.</summary>
	public static string FormatDuration(TimeSpan remaining)
	{
		long minutes = Math.Max(0, (long)Math.Ceiling(remaining.TotalMinutes));
		return minutes >= 24 * 60 ? $"{minutes / (24 * 60)}d {minutes / 60 % 24:00}h" : $"{minutes / 60}h {minutes % 60:00}m";
	}
}
