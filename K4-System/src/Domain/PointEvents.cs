namespace K4System.Domain;

using System.Globalization;

/// <summary>A scheduled points event: on these weekdays (none means every day), from one local time to another.</summary>
/// <remarks>A window whose end is before its start runs past midnight and belongs to the day it starts.</remarks>
internal sealed record EventWindow(IReadOnlySet<DayOfWeek> Days, TimeOnly From, TimeOnly To, double Multiplier);

/// <summary>A points event that is on now, and when it ends.</summary>
internal readonly record struct ActiveEvent(double Multiplier, DateTime EndsUtc, bool Manual);

/// <summary>Points event rules that need no game objects.</summary>
internal static class PointEvents
{
	public const double MaxMultiplier = 10;
	public const int MaxMinutes = 1440;

	/// <summary>The schedule entries from the config as windows. Invalid entries are skipped with a warning.</summary>
	public static List<EventWindow> Parse(IEnumerable<Config.EventSchedule?> schedule, List<string> warnings)
	{
		List<EventWindow> windows = [];
		int number = 0;

		foreach (Config.EventSchedule? entry in schedule)
		{
			number++;
			string where = $"events.schedule entry {number}";

			if (entry is null)
			{
				warnings.Add($"{where} is empty and was skipped.");
				continue;
			}

			if (!TimeOnly.TryParseExact(entry.From?.Trim(), "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly from)
				|| !TimeOnly.TryParseExact(entry.To?.Trim(), "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly to))
			{
				warnings.Add($"{where}: \"from\" and \"to\" must be 24-hour times such as \"18:00\". It was skipped.");
				continue;
			}

			if (!IsValidMultiplier(entry.Multiplier))
			{
				warnings.Add($"{where}: \"multiplier\" must be above 1 and at most {MaxMultiplier}, such as 1.5 or 2. It was skipped.");
				continue;
			}

			HashSet<DayOfWeek> days = [];
			string[] unknown = [];
			foreach (string day in entry.Days ?? [])
			{
				if (Enum.TryParse(day?.Trim(), ignoreCase: true, out DayOfWeek parsed) && Enum.IsDefined(parsed) && !int.TryParse(day, out _))
					days.Add(parsed);
				else
					unknown = [.. unknown, day ?? "null"];
			}

			if (unknown.Length > 0)
			{
				warnings.Add($"{where}: {string.Join(", ", unknown.Select(day => $"\"{day}\""))} is not a weekday. Use Monday to Sunday. It was skipped.");
				continue;
			}

			windows.Add(new EventWindow(days, from, to, entry.Multiplier));
		}

		return windows;
	}

	public static bool IsValidMultiplier(double multiplier)
		=> double.IsFinite(multiplier) && multiplier > 1 && multiplier <= MaxMultiplier;

	/// <summary>The scheduled event on at this moment: the highest multiplier, then the one that runs longest.</summary>
	public static ActiveEvent? Scheduled(IReadOnlyList<EventWindow> windows, DateTime utcNow, TimeZoneInfo zone)
	{
		if (windows.Count == 0)
			return null;

		DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
		ActiveEvent? best = null;

		foreach (EventWindow window in windows)
		{
			// Yesterday's window may still run past midnight.
			foreach (DateTime day in new[] { local.Date.AddDays(-1), local.Date })
			{
				if (window.Days.Count > 0 && !window.Days.Contains(day.DayOfWeek))
					continue;

				DateTime start = day + window.From.ToTimeSpan();
				DateTime end = window.To > window.From ? day + window.To.ToTimeSpan() : day.AddDays(1) + window.To.ToTimeSpan();
				if (local < start || local >= end)
					continue;

				ActiveEvent found = new(window.Multiplier, ToUtc(end, zone), Manual: false);
				if (best is not ActiveEvent current || found.Multiplier > current.Multiplier
					|| (found.Multiplier == current.Multiplier && found.EndsUtc > current.EndsUtc))
					best = found;
			}
		}

		return best;
	}

	// A local time skipped by a clock change does not exist; the event ends an hour later instead.
	private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
	{
		DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
		if (zone.IsInvalidTime(unspecified))
			unspecified = unspecified.AddHours(1);
		return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
	}

	/// <summary>Whole minutes left, rounded up, for messages.</summary>
	public static int MinutesLeft(ActiveEvent active, DateTime utcNow)
		=> (int)Math.Max(1, Math.Ceiling((active.EndsUtc - utcNow).TotalMinutes));

	/// <summary>"2", "1.5": the multiplier as players read it.</summary>
	public static string Format(double multiplier) => multiplier.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// Which points event is on: one an admin started, else the schedule. An admin who stops a scheduled event
/// silences it until it would have ended. Game thread only; times are passed in so it can be tested.
/// </summary>
internal sealed class EventState(IReadOnlyList<EventWindow> windows, TimeZoneInfo zone)
{
	private ActiveEvent? manual;
	private DateTime suppressedUntilUtc = DateTime.MinValue;
	private ActiveEvent? announced;

	public ActiveEvent? Current(DateTime utcNow)
	{
		if (manual is ActiveEvent started)
		{
			if (utcNow < started.EndsUtc)
				return started;
			manual = null;
		}

		ActiveEvent? scheduled = PointEvents.Scheduled(windows, utcNow, zone);
		return scheduled is ActiveEvent found && utcNow >= suppressedUntilUtc ? found : null;
	}

	public double Multiplier(DateTime utcNow) => Current(utcNow)?.Multiplier ?? 1;

	public void Start(double multiplier, int minutes, DateTime utcNow)
		=> manual = new ActiveEvent(multiplier, utcNow.AddMinutes(minutes), Manual: true);

	/// <summary>Ends the event that is on. False when none is.</summary>
	public bool Stop(DateTime utcNow)
	{
		ActiveEvent? current = Current(utcNow);
		if (current is null)
			return false;

		manual = null;
		if (PointEvents.Scheduled(windows, utcNow, zone) is ActiveEvent scheduled)
			suppressedUntilUtc = scheduled.EndsUtc;
		return true;
	}

	/// <summary>
	/// What changed since the last call: the event that ended (to announce its end) and the one that started.
	/// One event replacing another reports both.
	/// </summary>
	public (ActiveEvent? Ended, ActiveEvent? Started) Advance(DateTime utcNow)
	{
		ActiveEvent? now = Current(utcNow);
		if (now == announced)
			return (null, null);

		ActiveEvent? before = announced;
		announced = now;
		return (before, now);
	}
}
