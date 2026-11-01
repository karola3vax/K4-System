namespace K4System.Seasons;

using System.Globalization;

/// <summary>
/// Season dates. A season's id is the local date it started ("2026-11-01"), and it runs until the 1st of the
/// next month at midnight in the mission time zone. A season ended early by an admin starts a new one that day
/// ("2026-11-15"), which still ends on the 1st of the next month.
/// </summary>
internal static class SeasonClock
{
	public const string IdFormat = "yyyy-MM-dd";

	/// <summary>The days before the end in which players are reminded that the season ends soon.</summary>
	public const int FinalDays = 3;

	/// <summary>The season a fresh start or a monthly change begins: the 1st of the current local month.</summary>
	public static string MonthlyId(DateTime utc, TimeZoneInfo zone)
	{
		DateTime local = TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), zone);
		return new DateTime(local.Year, local.Month, 1).ToString(IdFormat, CultureInfo.InvariantCulture);
	}

	/// <summary>The season an early end begins: today's local date.</summary>
	public static string TodayId(DateTime utc, TimeZoneInfo zone)
		=> TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), zone).Date.ToString(IdFormat, CultureInfo.InvariantCulture);

	public static bool TryParse(string? id, out DateTime start)
		=> DateTime.TryParseExact(id, IdFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out start);

	/// <summary>When the season ends: midnight on the 1st of the month after it started, local time.</summary>
	public static DateTime EndsUtc(string id, TimeZoneInfo zone)
	{
		if (!TryParse(id, out DateTime start))
			throw new FormatException($"'{id}' is not a season id");

		DateTime end = new DateTime(start.Year, start.Month, 1).AddMonths(1);
		// Midnight can fall in a daylight-saving gap in a few zones; the first valid minute after it is used then.
		while (zone.IsInvalidTime(end))
			end = end.AddMinutes(1);
		return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(end, DateTimeKind.Unspecified), zone);
	}

	public static bool IsOver(string id, DateTime utc, TimeZoneInfo zone) => Utc(utc) >= EndsUtc(id, zone);

	public static TimeSpan Left(string id, DateTime utc, TimeZoneInfo zone)
	{
		TimeSpan left = EndsUtc(id, zone) - Utc(utc);
		return left < TimeSpan.Zero ? TimeSpan.Zero : left;
	}

	/// <summary>True in the last <see cref="FinalDays"/> days of the season.</summary>
	public static bool EndsSoon(string id, DateTime utc, TimeZoneInfo zone)
		=> !IsOver(id, utc, zone) && Left(id, utc, zone) <= TimeSpan.FromDays(FinalDays);

	/// <summary>"November 2026" for a season that began on the 1st, "15 Nov 2026" for one an admin started mid-month.</summary>
	public static string Name(string id, CultureInfo language)
	{
		if (!TryParse(id, out DateTime start))
			return id;

		string name = start.Day == 1 ? start.ToString("MMMM yyyy", language) : start.ToString("d MMM yyyy", language);
		return name.Length > 0 ? char.ToUpper(name[0], language) + name[1..] : name;
	}

	private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
