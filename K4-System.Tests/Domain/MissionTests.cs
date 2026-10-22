namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Missions;

public class MissionTests
{
	private static readonly DateOnly Day = new(2026, 10, 1);
	private static MissionPool DefaultPool => MissionFile.Parse(MissionFile.DefaultContent);

	// ---------------------------------------------------------------- missions.jsonc

	[Fact]
	public void Default_mission_file_loads_with_every_difficulty()
	{
		MissionPool pool = DefaultPool;

		foreach (MissionDifficulty difficulty in Enum.GetValues<MissionDifficulty>())
			Assert.True(pool.Of(difficulty).Count >= 3, $"{difficulty} needs several missions so rerolls have a choice.");

		Assert.All(pool.Missions, mission => Assert.True(mission.Id.Length <= 64));
		Assert.Contains(pool.Missions, mission => mission.Type == MissionType.WeaponKills && mission.Weapon == "awp");
	}

	[Fact]
	public void Mission_file_rejects_mistakes_with_clear_messages()
	{
		Assert.Contains("Target", Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""
			{ "a": { "Type": "Kills", "Difficulty": "Easy", "Target": 0 } }
			""")).Message);

		Assert.Contains("Weapon", Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""
			{ "a": { "Type": "WeaponKills", "Difficulty": "Easy", "Target": 5 } }
			""")).Message);

		Assert.Contains("Medium", Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""
			{ "a": { "Type": "Kills", "Difficulty": "Easy", "Target": 5 }, "b": { "Type": "Kills", "Difficulty": "Hard", "Target": 5 } }
			""")).Message);

		Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""{ "a": { "Type": "Flying", "Difficulty": "Easy", "Target": 5 } }"""));
		Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""{ "a": { "Type": "Kills", "Difficulty": "Easy", "Target": 5, "Typo": 1 } }"""));
	}

	[Fact]
	public void Weapon_names_are_normalized()
	{
		MissionPool pool = MissionFile.Parse("""
			{
				"e": { "Type": "WeaponKills", "Weapon": " Weapon_AK47 ", "Difficulty": "Easy", "Target": 1 },
				"m": { "Type": "Kills", "Difficulty": "Medium", "Target": 1 },
				"h": { "Type": "Kills", "Difficulty": "Hard", "Target": 1 },
			}
			""");

		Assert.Equal("ak47", pool.ById["e"].Weapon);
		Assert.Equal("AK-47", MissionService.WeaponName("ak47"));
		Assert.Equal("NOVA", MissionService.WeaponName("nova"));
	}

	// ---------------------------------------------------------------- assignment

	[Fact]
	public void Each_player_gets_one_mission_per_difficulty_that_stays_all_day()
	{
		MissionPool pool = DefaultPool;
		IReadOnlyList<MissionDefinition> first = MissionRules.Assign(pool, 76561198000000001, Day);
		IReadOnlyList<MissionDefinition> again = MissionRules.Assign(pool, 76561198000000001, Day);

		Assert.Equal([MissionDifficulty.Easy, MissionDifficulty.Medium, MissionDifficulty.Hard], first.Select(mission => mission.Difficulty));
		Assert.Equal(first.Select(mission => mission.Id), again.Select(mission => mission.Id));
	}

	[Fact]
	public void Assignments_vary_between_players_and_days()
	{
		MissionPool pool = DefaultPool;
		HashSet<string> sets = [];

		for (ulong player = 0; player < 40; player++)
			sets.Add(string.Join(",", MissionRules.Assign(pool, 76561198000000000 + player, Day).Select(mission => mission.Id)));
		for (int day = 0; day < 40; day++)
			sets.Add(string.Join(",", MissionRules.Assign(pool, 76561198000000001, Day.AddDays(day)).Select(mission => mission.Id)));

		Assert.True(sets.Count > 30, $"Only {sets.Count} distinct mission sets in 80 draws.");
	}

	[Fact]
	public void Reroll_picks_another_mission_of_the_same_difficulty()
	{
		MissionPool pool = DefaultPool;
		IReadOnlyList<MissionDefinition> today = MissionRules.Assign(pool, 76561198000000001, Day);

		MissionDefinition? replacement = MissionRules.Reroll(pool, 76561198000000001, Day, today[2], today.Select(mission => mission.Id));

		Assert.NotNull(replacement);
		Assert.Equal(MissionDifficulty.Hard, replacement.Difficulty);
		Assert.DoesNotContain(replacement.Id, today.Select(mission => mission.Id));
	}

	[Fact]
	public void Reroll_has_no_choice_when_a_difficulty_has_one_mission()
	{
		MissionPool pool = MissionFile.Parse("""
			{ "e": { "Type": "Kills", "Difficulty": "Easy", "Target": 1 }, "m": { "Type": "Kills", "Difficulty": "Medium", "Target": 1 }, "h": { "Type": "Kills", "Difficulty": "Hard", "Target": 1 } }
			""");

		Assert.Null(MissionRules.Reroll(pool, 1, Day, pool.ById["e"], ["e", "m", "h"]));
	}

	[Fact]
	public void Saved_progress_and_rerolled_missions_are_restored()
	{
		MissionPool pool = DefaultPool;
		MissionService service = new(new PluginConfig(), pool, TimeZoneInfo.Utc, null!, null!, null!, null!, null!, null!, null!, null!, null!);
		string otherHard = pool.Of(MissionDifficulty.Hard).Select(mission => mission.Id)
			.First(id => id != MissionRules.Assign(pool, 7, Day)[2].Id);

		PlayerMissions missions = service.Build(7, Day, new MissionRecord(
			[new MissionSlotSnapshot(0, MissionRules.Assign(pool, 7, Day)[0].Id, 4, false), new MissionSlotSnapshot(2, otherHard, 999, true), new MissionSlotSnapshot(1, "removed-mission", 3, false)],
			RerollsUsed: 1, Streak: 4, LastCompletedDay: "2026-09-30"));

		Assert.Equal(4, missions.Slots[0].Progress);
		Assert.Equal(0, missions.Slots[1].Progress);
		Assert.False(missions.Slots[1].Completed);
		Assert.Equal(otherHard, missions.Slots[2].Mission.Id);
		Assert.True(missions.Slots[2].Completed);
		Assert.Equal(missions.Slots[2].Mission.Target, missions.Slots[2].Progress);
		Assert.Equal(1, missions.RerollsUsed);
		Assert.Equal(new DateOnly(2026, 9, 30), missions.LastCompletedDay);

		MissionSnapshot snapshot = missions.Snapshot();
		Assert.Equal("2026-10-01", snapshot.Day);
		Assert.Equal([0, 1, 2], snapshot.Slots.Select(slot => slot.Slot));
	}

	[Fact]
	public void A_paid_mission_removed_from_the_file_stays_done()
	{
		MissionPool pool = DefaultPool;
		MissionService service = new(new PluginConfig(), pool, TimeZoneInfo.Utc, null!, null!, null!, null!, null!, null!, null!, null!, null!);

		PlayerMissions missions = service.Build(7, Day, new MissionRecord([new MissionSlotSnapshot(0, "deleted-easy-mission", 15, true)], 0, 0, null));

		Assert.True(missions.Slots[0].Completed);
		Assert.Equal(missions.Slots[0].Mission.Target, missions.Slots[0].Progress);
	}

	// ---------------------------------------------------------------- streaks and rewards

	[Fact]
	public void Streak_grows_on_consecutive_days_and_resets_after_a_gap()
	{
		Assert.Equal(1, MissionRules.NextStreak(0, null, Day));
		Assert.Equal(5, MissionRules.NextStreak(4, Day.AddDays(-1), Day));
		Assert.Equal(1, MissionRules.NextStreak(4, Day.AddDays(-2), Day));
		Assert.Equal(4, MissionRules.NextStreak(4, Day, Day));

		Assert.Equal(4, MissionRules.CurrentStreak(4, Day.AddDays(-1), Day));
		Assert.Equal(4, MissionRules.CurrentStreak(4, Day, Day));
		Assert.Equal(0, MissionRules.CurrentStreak(4, Day.AddDays(-2), Day));
		Assert.Equal(0, MissionRules.CurrentStreak(4, null, Day));
	}

	[Theory]
	[InlineData(1, 105)]
	[InlineData(3, 315)]
	[InlineData(7, 735)]
	[InlineData(30, 735)]
	public void Streak_bonus_is_ten_percent_per_day_up_to_seven(int streak, int expected)
		=> Assert.Equal(expected, MissionRules.StreakBonus(streak, 1050, 10, 7));

	[Fact]
	public void Default_rewards_are_worth_about_an_evening_bonus()
	{
		MissionSettings settings = new PluginConfig().Missions;
		int day = settings.Rewards.Easy + settings.Rewards.Medium + settings.Rewards.Hard;

		Assert.Equal(1050, day);
		Assert.Equal(735, MissionRules.StreakBonus(7, day, settings.StreakBonusPercent, settings.StreakMaxDays));
		Assert.Equal(1, settings.RerollsPerDay);
	}

	// ---------------------------------------------------------------- time and text

	[Fact]
	public void Days_change_at_midnight_in_the_configured_timezone()
	{
		(TimeZoneInfo istanbul, string? warning) = MissionRules.ResolveTimeZone("+03:00");
		Assert.Null(warning);

		DateTime beforeMidnight = new(2026, 10, 1, 20, 59, 0, DateTimeKind.Utc);
		DateTime afterMidnight = new(2026, 10, 1, 21, 1, 0, DateTimeKind.Utc);

		Assert.Equal(new DateOnly(2026, 10, 1), MissionRules.Today(beforeMidnight, istanbul));
		Assert.Equal(new DateOnly(2026, 10, 2), MissionRules.Today(afterMidnight, istanbul));
		Assert.Equal(TimeSpan.FromMinutes(1), MissionRules.UntilReset(beforeMidnight, istanbul));
		Assert.Equal("0h 01m", MissionRules.FormatDuration(TimeSpan.FromSeconds(20)));
	}

	[Fact]
	public void Reset_time_skips_a_midnight_that_does_not_exist()
	{
		// Clocks jump from 23:59 to 01:00 on the first Sunday of September.
		TimeZoneInfo.TransitionTime start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 9, 1, DayOfWeek.Sunday);
		TimeZoneInfo.TransitionTime end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 0, 0, 0), 4, 1, DayOfWeek.Sunday);
		TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
		TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Test/MidnightJump", TimeSpan.FromHours(-4), "Test", "Test", "Test DST", [rule]);

		// Saturday 5 September 2026, 23:30 local (03:30 UTC Sunday). The next valid local time is 01:00.
		DateTime utc = new(2026, 9, 6, 3, 30, 0, DateTimeKind.Utc);

		TimeSpan remaining = MissionRules.UntilReset(utc, zone);
		Assert.Equal(TimeSpan.FromMinutes(30), remaining);
	}

	[Fact]
	public void Unknown_timezones_fall_back_to_utc_with_a_warning()
	{
		(TimeZoneInfo zone, string? warning) = MissionRules.ResolveTimeZone("Mars/Olympus");
		Assert.Equal(TimeZoneInfo.Utc, zone);
		Assert.NotNull(warning);

		Assert.Null(MissionRules.ResolveTimeZone("UTC").Warning);
		Assert.Null(MissionRules.ResolveTimeZone("-05:30").Warning);
	}

	[Fact]
	public void Progress_bars_fill_in_tenths()
	{
		Assert.Equal("▱▱▱▱▱▱▱▱▱▱", MissionRules.ProgressBar(0, 15));
		Assert.Equal("▰▰▰▰▰▱▱▱▱▱", MissionRules.ProgressBar(8, 15));
		Assert.Equal("▰▰▰▰▰▰▰▰▰▰", MissionRules.ProgressBar(15, 15));
		Assert.Equal("▰▰▰▰▰▰▰▰▰▰", MissionRules.ProgressBar(99, 15));
	}

	[Fact]
	public void Day_text_round_trips()
	{
		Assert.Equal("2026-10-01", MissionRules.FormatDay(Day));
		Assert.Equal(Day, MissionRules.ParseDay("2026-10-01"));
		Assert.Null(MissionRules.ParseDay("01.10.2026"));
		Assert.Null(MissionRules.ParseDay(null));
	}

	[Fact]
	public void Missions_need_the_rank_module()
	{
		PluginConfig config = new();
		config.Modules.Ranks = false;

		List<string> warnings = ConfigValidator.ValidateAndNormalize(config);

		Assert.False(config.Modules.Missions);
		Assert.Contains(warnings, warning => warning.Contains("modules.missions"));
	}
}
