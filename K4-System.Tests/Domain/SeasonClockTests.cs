namespace K4System.Tests.Domain;

using System.Globalization;
using K4System.Missions;
using K4System.Seasons;

public class SeasonClockTests
{
	private sealed record SeasonCase(string Name, string TimeZone, string Season, DateTime NowUtc, bool ExpectedOver, DateTime ExpectedEndsUtc, bool ExpectedEndsSoon, string ExpectedNext);

	// Eval: every case in Evals/seasons.json. Season ends drive a full points reset, so a wrong end is a wrong reset.
	[Fact]
	public void Season_dates_follow_the_eval_cases()
	{
		List<SeasonCase> cases = TestData.LoadEvalList<SeasonCase>("seasons.json");
		Assert.True(cases.Count >= 10);

		foreach (SeasonCase item in cases)
		{
			TimeZoneInfo zone = MissionRules.ResolveTimeZone(item.TimeZone).Item1;
			DateTime now = DateTime.SpecifyKind(item.NowUtc.ToUniversalTime(), DateTimeKind.Utc);

			Assert.True(item.ExpectedOver == SeasonClock.IsOver(item.Season, now, zone), $"{item.Name}: over");
			Assert.True(item.ExpectedEndsUtc.ToUniversalTime() == SeasonClock.EndsUtc(item.Season, zone), $"{item.Name}: ends {SeasonClock.EndsUtc(item.Season, zone):O}");
			Assert.True(item.ExpectedEndsSoon == SeasonClock.EndsSoon(item.Season, now, zone), $"{item.Name}: ends soon");
			Assert.True(item.ExpectedNext == SeasonClock.MonthlyId(now, zone), $"{item.Name}: next {SeasonClock.MonthlyId(now, zone)}");
		}
	}

	[Fact]
	public void An_early_end_starts_a_season_named_after_today()
	{
		TimeZoneInfo zone = MissionRules.ResolveTimeZone("Europe/Istanbul").Item1;
		// 22:30 UTC on 14 November is already the 15th in Istanbul.
		Assert.Equal("2026-11-15", SeasonClock.TodayId(new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc), zone));
	}

	[Theory]
	[InlineData("2026-11-01", "en", "November 2026")]
	[InlineData("2026-11-15", "en", "15 Nov 2026")]
	[InlineData("2026-11-01", "tr", "Kasım 2026")]
	[InlineData("2026-11-01", "de", "November 2026")]
	[InlineData("not-a-date", "en", "not-a-date")]
	public void Season_names_read_in_the_players_language(string season, string language, string expected)
		=> Assert.Equal(expected, SeasonClock.Name(season, CultureInfo.GetCultureInfo(language)));

	[Fact]
	public void Time_left_never_goes_below_zero()
		=> Assert.Equal(TimeSpan.Zero, SeasonClock.Left("2026-01-01", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc));
}
