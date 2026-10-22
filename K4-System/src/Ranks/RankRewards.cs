namespace K4System.Ranks;

using System.Security.Cryptography;
using System.Text;
using CounterStrikeSharp.API.Core.Translations;

internal readonly record struct RewardCommand(string RewardId, string RankName, string CommandTemplate, string Command);

internal readonly record struct RewardContext(string SteamId, string UserId, string Name, string Rank, string RankId, string Points);

/// <summary>Rules for one-time rank reward commands. Reward ids are stored in the database and must stay stable.</summary>
internal static class RankRewards
{
	/// <summary>Rewards of the new rank, only when it is a promotion. Skipped intermediate ranks get nothing.</summary>
	public static IReadOnlyList<RewardCommand> ForPromotion(RankDefinition? oldRank, RankDefinition newRank, RewardContext context)
	{
		if (oldRank is null || newRank.Point <= oldRank.Point || newRank.Rewards is null)
			return [];

		List<RewardCommand> commands = [];
		string rankKey = RewardKey(newRank);

		foreach (RankReward reward in newRank.Rewards)
		{
			if (!reward.IsValidCommand)
				continue;

			commands.Add(new RewardCommand(CreateId(rankKey, reward.Value), newRank.Name, reward.Value, ReplacePlaceholders(reward.Value, context)));
		}

		return commands;
	}

	/// <summary>Rebuilds a stored pending reward from the current configuration, or null when it no longer exists.</summary>
	public static RewardCommand? Rebuild(RankLadder ladder, string rankName, string rewardId, RewardContextFactory contextFor)
	{
		RankDefinition? rank = ladder.FindByName(rankName);
		RankReward? reward = rank?.Rewards?.FirstOrDefault(candidate => candidate.IsValidCommand && CreateId(RewardKey(rank), candidate.Value) == rewardId);

		if (rank is null || reward is null)
			return null;

		return new RewardCommand(rewardId, rank.Name, reward.Value, ReplacePlaceholders(reward.Value, contextFor(rank)));
	}

	internal delegate RewardContext RewardContextFactory(RankDefinition rank);

	public static IReadOnlyList<string> DescriptionLines(RankDefinition rank)
	{
		if (string.IsNullOrWhiteSpace(rank.RewardDescription) || rank.Rewards is null || !rank.Rewards.Any(reward => reward.IsValidCommand))
			return [];

		return rank.RewardDescription
			.Split(["\r\n", "\n", "\r"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.ReplaceColorTags())
			.ToArray();
	}

	public static string ReplacePlaceholders(string command, RewardContext context)
	{
		return command
			.Replace("{steamid}", context.SteamId, StringComparison.Ordinal)
			.Replace("{userid}", context.UserId, StringComparison.Ordinal)
			.Replace("{name}", Sanitize(context.Name), StringComparison.Ordinal)
			.Replace("{rank}", Sanitize(context.Rank), StringComparison.Ordinal)
			.Replace("{rankid}", context.RankId, StringComparison.Ordinal)
			.Replace("{points}", context.Points, StringComparison.Ordinal);
	}

	public static string CreateId(string rankKey, string command)
	{
		byte[] bytes = Encoding.UTF8.GetBytes($"{rankKey}\n{command}");
		return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
	}

	private static string RewardKey(RankDefinition rank)
		=> string.IsNullOrWhiteSpace(rank.Key) ? rank.Name : rank.Key;

	// Player-controlled text must not be able to start a new server command.
	private static string Sanitize(string value)
	{
		return value
			.Replace("\r", " ", StringComparison.Ordinal)
			.Replace("\n", " ", StringComparison.Ordinal)
			.Replace(";", " ", StringComparison.Ordinal)
			.Replace("\"", "'", StringComparison.Ordinal);
	}
}
