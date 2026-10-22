namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Config;
using K4System.Domain;

public class ResetPolicyTests
{
	private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void PersonalResetTargetIsNormalized()
	{
		Assert.True(ResetPolicy.TryNormalizeTarget(" Rank ", out string target));
		Assert.Equal(ResetPolicy.Rank, target);
	}

	[Fact]
	public void AdminResetTargetDefaultsToAll()
	{
		string[] args = ["css_resetdata", "@all"];

		Assert.True(ResetPolicy.TryGetAdminTarget(args.Length, index => args[index], out string target));
		Assert.Equal(ResetPolicy.All, target);
	}

	[Fact]
	public void AdminResetTargetReadsArgumentTwo()
	{
		string[] args = ["css_resetdata", "PlayerName", "stat"];

		Assert.True(ResetPolicy.TryGetAdminTarget(args.Length, index => args[index], out string target));
		Assert.Equal(ResetPolicy.Stat, target);
	}

	[Fact]
	public void InvalidResetTargetIsRejected()
	{
		string[] args = ["css_resetdata", "PlayerName", "invalid"];

		Assert.False(ResetPolicy.TryGetAdminTarget(args.Length, index => args[index], out _));
	}

	[Fact]
	public void ResetAllSelectsEverySection()
	{
		Assert.True(ResetPolicy.Includes(ResetPolicy.All, ResetPolicy.Time));
		Assert.True(ResetPolicy.Includes(ResetPolicy.All, ResetPolicy.Rank));
		Assert.True(ResetPolicy.Includes(ResetPolicy.All, ResetPolicy.Stat));
		Assert.False(ResetPolicy.Includes(ResetPolicy.Rank, ResetPolicy.Time));
	}

	[Fact]
	public void PersonalRankResetsClearRewardClaims()
	{
		Assert.True(ResetPolicy.ClearsRewardClaims(ResetPolicy.Rank));
		Assert.True(ResetPolicy.ClearsRewardClaims(ResetPolicy.All));
		Assert.False(ResetPolicy.ClearsRewardClaims(ResetPolicy.Stat));
		Assert.False(ResetPolicy.ClearsRewardClaims(ResetPolicy.Time));
	}

	[Fact]
	public void PersonalRankResetCooldownPolicy()
	{
		Assert.Equal(TimeSpan.Zero, ResetPolicy.RankResetCooldownRemaining(Now, null, 24));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.RankResetCooldownRemaining(Now, Now, 0));
		Assert.Equal(TimeSpan.FromHours(23), ResetPolicy.RankResetCooldownRemaining(Now, Now.AddHours(-1), 24));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.RankResetCooldownRemaining(Now, Now.AddHours(-24), 24));
		Assert.Equal(TimeSpan.FromHours(24), ResetPolicy.RankResetCooldownRemaining(Now, Now.AddMinutes(1), 24));
		Assert.Equal("1d 01:01:01", ResetPolicy.FormatRemaining(TimeSpan.FromSeconds(90061)));
		Assert.Equal("00:01:00", ResetPolicy.FormatRemaining(TimeSpan.FromSeconds(59.1)));

		using JsonDocument configJson = JsonDocument.Parse(JsonSerializer.Serialize(new PluginConfig()));
		Assert.Equal(24, configJson.RootElement
			.GetProperty("ranks")
			.GetProperty("reset-cooldown-hours")
			.GetInt32());
	}

	[Fact]
	public void AllPersonalResetsHaveShortCooldown()
	{
		DateTime now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(now, null, 3));
		Assert.Equal(TimeSpan.FromSeconds(2), ResetPolicy.CommandCooldownRemaining(now, now.AddSeconds(-1), 3));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(now, now.AddSeconds(-3), 3));
		Assert.Equal(TimeSpan.Zero, ResetPolicy.CommandCooldownRemaining(now, now, 0));
	}

	public static TheoryData<string> EvalCaseNames()
	{
		TheoryData<string> names = [];
		foreach (PersonalRankResetEvalCase evalCase in TestData.LoadEvalList<PersonalRankResetEvalCase>("personal-rank-resets.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EvalCaseNames))]
	public void PersonalRankResetEvalScenario(string name)
	{
		PersonalRankResetEvalCase evalCase = TestData.LoadEvalList<PersonalRankResetEvalCase>("personal-rank-resets.json").Single(item => item.Name == name);

		Assert.True(ResetPolicy.TryNormalizeTarget(evalCase.Target, out string target));
		bool clearsRewardClaims = ResetPolicy.ClearsRewardClaims(target);
		DateTime? lastResetUtc = evalCase.ElapsedHours is null ? null : Now.AddHours(-evalCase.ElapsedHours.Value);
		TimeSpan remaining = clearsRewardClaims
			? ResetPolicy.RankResetCooldownRemaining(Now, lastResetUtc, evalCase.CooldownHours)
			: TimeSpan.Zero;

		Assert.Equal(evalCase.ExpectedClearRewardClaims, clearsRewardClaims);
		Assert.Equal(evalCase.ExpectedAllowed, remaining == TimeSpan.Zero);
		Assert.Equal(evalCase.ExpectedRemainingSeconds, (int)Math.Round(remaining.TotalSeconds));
	}

	public sealed class PersonalRankResetEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public string Target { get; set; } = string.Empty;
		public int CooldownHours { get; set; }
		public double? ElapsedHours { get; set; }
		public bool ExpectedClearRewardClaims { get; set; }
		public bool ExpectedAllowed { get; set; }
		public int ExpectedRemainingSeconds { get; set; }
	}
}
