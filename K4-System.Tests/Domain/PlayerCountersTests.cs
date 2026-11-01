namespace K4System.Tests.Domain;

using K4System.Players;
using K4System.Services;

public class PlayerCountersTests
{
	[Theory]
	[InlineData(310, 150, 310f / 150)]
	[InlineData(12, 0, 12f)]
	[InlineData(0, 0, 0f)]
	[InlineData(0, 7, 0f)]
	public void KillDeathRatioShowsKillsWhenThereAreNoDeaths(int kills, int deaths, float expected)
		=> Assert.Equal(expected, StatCounters.KillDeathRatio(kills, deaths), 4);

	[Theory]
	[InlineData(259188, 6, "2d, 23h, 59m, 48s")]
	[InlineData(259188, 2, "2d, 23h")]
	[InlineData(86700, 2, "1d")]
	[InlineData(2710, 2, "45m, 10s")]
	[InlineData(0, 2, "0s")]
	public void PlaytimeKeepsTheLargestUnits(int seconds, int units, string expected)
		=> Assert.Equal(expected, PlaytimeCounters.Format(seconds, unit => unit[..1], units));

	[Fact]
	public void RankToEveryoneIsOffUnlessTheServerTurnsItOn()
		=> Assert.False(new K4System.Config.PluginConfig().Commands.RankToEveryone);

	[Fact]
	public void TimeResetsClearCountersAndAnchors()
	{
		DateTime oldUtc = new(2026, 8, 22, 11, 0, 0, DateTimeKind.Utc);
		DateTime nowUtc = oldUtc.AddHours(1);
		PlaytimeCounters counters = new([100, 50, 0, 0, 25, 0], oldUtc);

		counters.Reset(nowUtc);

		Assert.All(counters.ToArray(), value => Assert.Equal(0, value));
		foreach (PlaytimeAnchor anchor in Enum.GetValues<PlaytimeAnchor>())
			Assert.Equal(nowUtc, counters.AnchorOf(anchor));

		counters.AddElapsed(PlaytimeField.All, PlaytimeAnchor.Connect, nowUtc.AddSeconds(5));
		Assert.Equal(5, counters[PlaytimeField.All]);
		Assert.Equal(nowUtc.AddSeconds(5), counters.AnchorOf(PlaytimeAnchor.Connect));
	}

	[Fact]
	public void HeadshotPercentageUsesKills()
	{
		Assert.Equal(50f, StatService.HeadshotPercentage(5, 10));
		Assert.Equal(0f, StatService.HeadshotPercentage(5, 0));
	}

	[Fact]
	public void PlaytimeRewardsUsePerPlayerIntervals()
	{
		DateTime joinedUtc = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

		Assert.False(RankService.IsPlaytimeRewardDue(joinedUtc.AddMinutes(9), joinedUtc, 600));
		Assert.True(RankService.IsPlaytimeRewardDue(joinedUtc.AddMinutes(10), joinedUtc, 600));
		Assert.True(RankService.IsPlaytimeRewardEligible(joinedUtc.AddMinutes(20), joinedUtc, true, true, false, 600));
		Assert.False(RankService.IsPlaytimeRewardEligible(joinedUtc.AddMinutes(20), joinedUtc, false, true, false, 600));
		Assert.False(RankService.IsPlaytimeRewardEligible(joinedUtc.AddMinutes(1), joinedUtc, true, false, false, 600));
		Assert.True(RankService.IsPlaytimeRewardEligible(joinedUtc.AddMinutes(20), joinedUtc, false, false, true, 600));
	}

	[Fact]
	public void StatColumnNamesMatchStatFieldOrder()
	{
		StatField[] fields = Enum.GetValues<StatField>();

		Assert.Equal(38, StatColumns.Names.Count);
		Assert.Equal(38, fields.Length);
		Assert.Equal(StatColumns.Names.Count, StatColumns.Names.Distinct(StringComparer.Ordinal).Count());

		for (int index = 0; index < fields.Length; index++)
		{
			Assert.Equal(index, (int)fields[index]);
			Assert.Equal(ToSnakeCase(fields[index].ToString()), StatColumns.Column(fields[index]));
		}
	}

	[Fact]
	public void PlaytimeColumnNamesMatchPlaytimeFieldOrder()
	{
		PlaytimeField[] fields = Enum.GetValues<PlaytimeField>();

		Assert.Equal(fields.Length, PlaytimeColumns.Names.Count);
		Assert.Equal(["all", "ct", "t", "spec", "alive", "dead"], PlaytimeColumns.Names);

		for (int index = 0; index < fields.Length; index++)
		{
			Assert.Equal(index, (int)fields[index]);
			Assert.Equal(fields[index].ToString().ToLowerInvariant(), PlaytimeColumns.Names[index]);
		}
	}

	[Fact]
	public void CountersRejectWrongLengths()
	{
		Assert.Throws<ArgumentException>(() => new StatCounters(new int[37]));
		Assert.Throws<ArgumentException>(() => new PlaytimeCounters(new int[5], DateTime.UtcNow));
	}

	private static string ToSnakeCase(string name)
	{
		// "FirstBlood" is the only field written as one word in its column name.
		if (name is "FirstBlood")
			return "firstblood";

		System.Text.StringBuilder builder = new();
		for (int index = 0; index < name.Length; index++)
		{
			char character = name[index];
			if (char.IsUpper(character) && index > 0)
				builder.Append('_');
			builder.Append(char.ToLowerInvariant(character));
		}

		return builder.ToString();
	}
}
