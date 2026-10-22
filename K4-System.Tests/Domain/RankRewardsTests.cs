namespace K4System.Tests.Domain;

using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using K4System.Config;
using K4System.Ranks;

public class RankRewardsTests
{
	private const string CreditsCommand = "css_store_givecredits {steamid} 500";

	private static RewardContext Context() => new("76561198000000000", "12", "Player", "Gold", "4", "500");

	private static RankDefinition RankWithCommand(string name, int points, string command)
	{
		RankDefinition rank = TestData.Rank(name, points);
		rank.Rewards = [new RankReward { Type = "command", Value = command }];
		return rank;
	}

	[Fact]
	public void CommandRewardTriggersOnPromotion()
	{
		RankDefinition oldRank = TestData.Rank("Silver", 100);
		RankDefinition newRank = RankWithCommand("Gold", 500, CreditsCommand);

		IReadOnlyList<RewardCommand> commands = RankRewards.ForPromotion(oldRank, newRank, Context());

		RewardCommand command = Assert.Single(commands);
		Assert.Equal(CreditsCommand, command.CommandTemplate);
		Assert.Equal("css_store_givecredits 76561198000000000 500", command.Command);
		Assert.Equal(64, command.RewardId.Length);
	}

	[Fact]
	public void CommandRewardDoesNotTriggerOnDemotion()
	{
		RankDefinition oldRank = TestData.Rank("Gold", 500);
		RankDefinition newRank = RankWithCommand("Silver", 100, CreditsCommand);

		Assert.Empty(RankRewards.ForPromotion(oldRank, newRank, Context()));
	}

	[Fact]
	public void CommandRewardDoesNotTriggerOnInitialLoad()
	{
		RankDefinition newRank = RankWithCommand("Gold", 500, CreditsCommand);

		Assert.Empty(RankRewards.ForPromotion(null, newRank, Context()));
	}

	[Fact]
	public void RankJumpsExecuteFinalRankRewardsOnly()
	{
		RankDefinition none = TestData.Rank("None", -1);
		RankDefinition lower = TestData.Rank("VIP Bronze", 100, "@css/vip");
		lower.Rewards = [new RankReward { Type = "command", Value = "reward lower" }];
		RankDefinition highest = TestData.Rank("VIP Global", 500, "@css/vip");
		highest.Rewards = [new RankReward { Type = "command", Value = "reward final" }];
		RankLadder ladder = RankLadder.Create([none, lower, highest], RankGameMode.Normal);

		RankDefinition resolved = ladder.Resolve(600, _ => true);
		IReadOnlyList<RewardCommand> commands = RankRewards.ForPromotion(none, resolved, Context());

		RewardCommand command = Assert.Single(commands);
		Assert.Equal("reward final", command.Command);
	}

	[Fact]
	public void DuplicateRewardClaimPreventsRepeatedCommand()
	{
		RewardCommand rewardCommand = new(
			RankRewards.CreateId("Gold", CreditsCommand),
			"Gold",
			CreditsCommand,
			"css_store_givecredits 76561198000000000 500");
		HashSet<string> claimed = [];

		Assert.True(claimed.Add(rewardCommand.RewardId));
		Assert.False(claimed.Add(rewardCommand.RewardId));
		Assert.Equal(rewardCommand.RewardId, RankRewards.CreateId("Gold", CreditsCommand));
	}

	[Fact]
	public void RewardIdIsStableForRankKeyAndCommand()
	{
		RankDefinition oldRank = TestData.Rank("Silver", 100);
		RankDefinition newRank = RankWithCommand("Gold", 500, CreditsCommand);

		RewardCommand command = Assert.Single(RankRewards.ForPromotion(oldRank, newRank, Context()));

		Assert.Equal(RankRewards.CreateId("Gold", CreditsCommand), command.RewardId);
		Assert.NotEqual(RankRewards.CreateId("Silver", CreditsCommand), command.RewardId);
	}

	[Fact]
	public void RewardPlaceholdersAreReplaced()
	{
		RewardContext context = new("76561198000000000", "12", "Bad\";Name\n", "Gold", "4", "500");

		string command = RankRewards.ReplacePlaceholders("reward {steamid} {userid} {name} {rank} {rankid} {points}", context);

		Assert.Equal("reward 76561198000000000 12 Bad' Name  Gold 4 500", command);
	}

	[Fact]
	public void RewardDescriptionLinesAreFormatted()
	{
		RankDefinition rank = TestData.Rank("Gold", 500);
		rank.RewardDescription = "  {green}500 credits  \r\n\r\n {gold}VIP {red}access \r Unknown {future}tag ";

		Assert.Empty(RankRewards.DescriptionLines(rank));

		rank.Rewards = [new RankReward { Type = "item", Value = "crate" }];
		Assert.Empty(RankRewards.DescriptionLines(rank));

		rank.Rewards = [new RankReward { Type = "command", Value = " " }];
		Assert.Empty(RankRewards.DescriptionLines(rank));

		rank.Rewards = [new RankReward { Type = "COMMAND", Value = CreditsCommand }];
		IReadOnlyList<string> lines = RankRewards.DescriptionLines(rank);
		Assert.Equal(3, lines.Count);
		Assert.Contains(ChatColors.Green.ToString(), lines[0]);
		Assert.DoesNotContain("{green}", lines[0], StringComparison.OrdinalIgnoreCase);
		Assert.Contains(ChatColors.Gold.ToString(), lines[1]);
		Assert.Contains(ChatColors.Red.ToString(), lines[1]);
		Assert.Equal("Unknown {future}tag", lines[2]);

		rank.RewardDescription = " ";
		Assert.Empty(RankRewards.DescriptionLines(rank));
	}

	public static TheoryData<string> EvalCaseNames()
	{
		TheoryData<string> names = [];
		foreach (RewardDescriptionEvalCase evalCase in TestData.LoadEvalList<RewardDescriptionEvalCase>("reward-descriptions.json"))
			names.Add(evalCase.Name);
		return names;
	}

	[Theory]
	[MemberData(nameof(EvalCaseNames))]
	public void RewardDescriptionEvalScenario(string name)
	{
		RewardDescriptionEvalCase evalCase = TestData.LoadEvalList<RewardDescriptionEvalCase>("reward-descriptions.json").Single(item => item.Name == name);
		RankDefinition rank = TestData.Rank("Reward", 100);
		rank.RewardDescription = evalCase.RewardDescription;
		rank.Rewards = [new RankReward { Type = evalCase.RewardType, Value = evalCase.RewardValue }];

		Assert.Equal(evalCase.ExpectedLines.Select(line => line.ReplaceColorTags()), RankRewards.DescriptionLines(rank));
	}

	public sealed class RewardDescriptionEvalCase
	{
		public string Name { get; set; } = string.Empty;
		public string RewardDescription { get; set; } = string.Empty;
		public string RewardType { get; set; } = "command";
		public string RewardValue { get; set; } = "reward";
		public List<string> ExpectedLines { get; set; } = [];
	}
}
