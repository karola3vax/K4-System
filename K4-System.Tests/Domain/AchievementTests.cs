namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Achievements;
using K4System.Players;
using K4System.Storage;

public class AchievementTests
{
	private static IReadOnlyList<AchievementDefinition> Defaults => AchievementFile.Parse(AchievementFile.DefaultContent);

	private static int[] Stats(params (StatField Field, int Value)[] values)
	{
		int[] stats = new int[StatColumns.Names.Count];
		foreach ((StatField field, int value) in values)
			stats[(int)field] = value;
		return stats;
	}

	[Fact]
	public void The_shipped_list_parses_and_every_name_and_goal_text_exists_in_english()
	{
		using JsonDocument english = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lang", "en.json")));
		HashSet<string> keys = english.RootElement.EnumerateObject().Select(line => line.Name).ToHashSet();

		Assert.True(Defaults.Count >= 20);
		foreach (AchievementDefinition achievement in Defaults)
		{
			Assert.Contains("achievement.name." + achievement.Id, keys);
			Assert.Contains(AchievementRules.GoalKey(achievement.Type), keys);
		}

		// Every type has a goal text, not only the shipped ones.
		foreach (AchievementType type in Enum.GetValues<AchievementType>())
			Assert.Contains(AchievementRules.GoalKey(type), keys);
	}

	[Theory]
	[InlineData("""{ "x": { "Type": "Kills", "Goal": 0, "Points": 1, "Name": "X" } }""", "Goal")]
	[InlineData("""{ "x": { "Type": "Kills", "Goal": 5, "Points": -1, "Name": "X" } }""", "negative")]
	[InlineData("""{ "x": { "Type": "Kilss", "Goal": 5, "Points": 1, "Name": "X" } }""", "invalid")]
	[InlineData("""{ "x": { "Type": "Kills", "Goal": 5, "Points": 1, "Typo": 1, "Name": "X" } }""", "invalid")]
	[InlineData("""{ "my-own": { "Type": "Kills", "Goal": 5, "Points": 1 } }""", "Name")]
	[InlineData("""{ "x": { "Type": "Kills", "Goal": 5, "Name": "X" }, "x": { "Type": "Kills", "Goal": 6, "Name": "Y" } }""", "twice")]
	[InlineData("""[]""", "object")]
	public void Mistakes_in_the_file_are_named(string content, string mentioned)
	{
		InvalidDataException error = Assert.Throws<InvalidDataException>(() => AchievementFile.Parse(content));
		Assert.Contains(mentioned, error.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void A_shipped_key_without_a_name_uses_the_language_files()
	{
		AchievementDefinition achievement = Assert.Single(AchievementFile.Parse("""{ "kills-100": { "Type": "Kills", "Goal": 50, "Points": 10 } }"""));
		Assert.Null(achievement.Name);
		Assert.Equal(50, achievement.Goal);
	}

	[Fact]
	public void Closest_lists_unfinished_achievements_nearest_to_done_first()
	{
		AchievementCounters counters = new(Stats((StatField.Kills, 900), (StatField.Headshots, 10)), 0, 0);

		List<(AchievementDefinition Achievement, int Progress)> closest = AchievementRules.Closest(Defaults, new HashSet<string> { "kills-100" }, counters, 2);

		Assert.Equal("kills-1000", closest[0].Achievement.Id);
		Assert.Equal(900, closest[0].Progress);
		Assert.Equal(2, closest.Count);
	}

	[Fact]
	public void Unlocks_are_handed_to_one_save_only()
	{
		PlayerAchievements mine = new(["kills-100"]);
		mine.Unlock("kills-100");
		mine.Unlock("kills-1000");

		Assert.Equal(["kills-1000"], mine.TakeUnsaved());
		Assert.Empty(mine.TakeUnsaved());
		Assert.Contains("kills-1000", mine.Unlocked);
	}

	// A failed save is retried with the next snapshot; its unlocks must ride along, or they are never stored.
	[Fact]
	public void A_newer_snapshot_keeps_the_unlocks_of_a_failed_one()
	{
		PlayerSnapshot failed = new(1, "A", "Gold", 1, 100, null, null, null, NewAchievements: ["kills-100"]);
		PlayerSnapshot newer = new(1, "A", "Gold", 1, 200, null, null, null, NewAchievements: ["kills-1000"]);

		PlayerSnapshot merged = newer.MergeOnto(failed);

		Assert.Equal(["kills-1000", "kills-100"], merged.NewAchievements);
		Assert.Equal(200, merged.Points);
	}

	private sealed record AchievementCase(string Name, Dictionary<string, int>? Stats, int? PlaytimeHours, int? MissionStreak, string[] Unlocked, string[] ExpectedNew);

	/// <summary>Player careers from Evals/achievements.json against the shipped list.</summary>
	[Fact]
	public void Eval_careers_unlock_exactly_the_expected_achievements()
	{
		List<AchievementCase> cases = TestData.LoadEvalList<AchievementCase>("achievements.json");

		foreach (AchievementCase test in cases)
		{
			int[]? stats = test.Stats is null ? null : Stats([.. test.Stats.Select(pair => (Enum.Parse<StatField>(pair.Key), pair.Value))]);
			AchievementCounters counters = new(stats, test.PlaytimeHours * 3600, test.MissionStreak);

			string[] unlocked = [.. AchievementRules.NewlyMet(Defaults, test.Unlocked.ToHashSet(), counters).Select(achievement => achievement.Id)];

			Assert.True(test.ExpectedNew.SequenceEqual(unlocked), $"{test.Name}: got [{string.Join(", ", unlocked)}]");
		}
	}
}
