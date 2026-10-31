namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Config;
using K4System.Domain;

public class PointEventsTests
{
	private static readonly DateTime Saturday = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

	private static List<EventWindow> Parse(params EventSchedule[] entries) => PointEvents.Parse(entries, []);

	[Fact]
	public void Invalid_schedule_entries_are_skipped_with_a_warning_each()
	{
		List<string> warnings = [];
		List<EventWindow> windows = PointEvents.Parse(
		[
			new EventSchedule { From = "18:00", To = "22:00", Multiplier = 2 },
			new EventSchedule { From = "6pm", To = "22:00", Multiplier = 2 },
			new EventSchedule { From = "18:00", To = "22:00", Multiplier = 1 },
			new EventSchedule { From = "18:00", To = "22:00", Multiplier = 11 },
			new EventSchedule { Days = ["Saturday", "Caturday"], From = "18:00", To = "22:00", Multiplier = 2 },
			new EventSchedule { Days = ["6"], From = "18:00", To = "22:00", Multiplier = 2 },
			null
		], warnings);

		Assert.Single(windows);
		Assert.Equal(6, warnings.Count);
		Assert.Contains(warnings, warning => warning.Contains("\"Caturday\""));
	}

	[Fact]
	public void The_config_template_ships_with_an_empty_schedule()
	{
		PluginConfig config = new();
		Assert.DoesNotContain(ConfigValidator.ValidateAndNormalize(config), warning => warning.StartsWith("events", StringComparison.Ordinal));
		Assert.Empty(config.Events.Windows);
	}

	[Fact]
	public void An_admin_event_wins_over_the_schedule_and_ends_on_time()
	{
		EventState state = new(Parse(new EventSchedule { From = "00:00", To = "00:00", Multiplier = 1.5 }), TimeZoneInfo.Utc);

		state.Start(3, 60, Saturday);

		Assert.Equal(3, state.Multiplier(Saturday.AddMinutes(59)));
		Assert.Equal(1.5, state.Multiplier(Saturday.AddMinutes(60)));
	}

	[Fact]
	public void Stopping_a_scheduled_event_silences_it_until_it_would_have_ended()
	{
		EventState state = new(Parse(new EventSchedule { From = "10:00", To = "14:00", Multiplier = 2 }), TimeZoneInfo.Utc);

		Assert.True(state.Stop(Saturday));
		Assert.Equal(1, state.Multiplier(Saturday.AddHours(1)));
		// The next day's window is on again.
		Assert.Equal(2, state.Multiplier(Saturday.AddDays(1)));
		Assert.False(new EventState([], TimeZoneInfo.Utc).Stop(Saturday));
	}

	[Fact]
	public void Advance_reports_each_start_and_end_once()
	{
		EventState state = new([], TimeZoneInfo.Utc);
		Assert.Equal((null, null), state.Advance(Saturday));

		state.Start(2, 30, Saturday);
		(ActiveEvent? ended, ActiveEvent? started) = state.Advance(Saturday);
		Assert.Null(ended);
		Assert.Equal(2, started?.Multiplier);
		Assert.Equal((null, null), state.Advance(Saturday.AddMinutes(1)));

		(ended, started) = state.Advance(Saturday.AddMinutes(30));
		Assert.Equal(2, ended?.Multiplier);
		Assert.Null(started);
	}

	[Theory]
	[InlineData(2.0, "2")]
	[InlineData(1.5, "1.5")]
	[InlineData(1.25, "1.25")]
	public void Multipliers_read_like_players_write_them(double multiplier, string expected)
		=> Assert.Equal(expected, PointEvents.Format(multiplier));

	private sealed record EventCase(string Name, string TimeZone, EventSchedule[] Schedule, DateTime NowUtc, double ExpectedMultiplier, DateTime? ExpectedEndsUtc);

	/// <summary>Real schedules from Evals/points-events.json: time zones, past-midnight windows, overlaps, a clock change.</summary>
	[Fact]
	public void Eval_schedules_are_on_exactly_when_expected()
	{
		EventCase[] cases = JsonSerializer.Deserialize<EventCase[]>(File.ReadAllText(TestData.EvalPath("points-events.json")), ConfigFile.Options)!;
		Assert.NotEmpty(cases);

		foreach (EventCase test in cases)
		{
			List<string> warnings = [];
			List<EventWindow> windows = PointEvents.Parse(test.Schedule, warnings);
			Assert.True(warnings.Count == 0, $"{test.Name}: {string.Join(" ", warnings)}");

			ActiveEvent? active = PointEvents.Scheduled(windows, test.NowUtc.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById(test.TimeZone));

			Assert.True(test.ExpectedMultiplier == (active?.Multiplier ?? 1), $"{test.Name}: multiplier {active?.Multiplier ?? 1}");
			Assert.True(test.ExpectedEndsUtc?.ToUniversalTime() == active?.EndsUtc, $"{test.Name}: ends {active?.EndsUtc:o}");
		}
	}
}
