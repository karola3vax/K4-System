namespace K4System.Achievements;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using K4System.Hud;
using K4System.Players;
using K4System.Ranks;
using K4System.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unlocks achievements when a player's stored counters reach their goal: at round end, match end and when the
/// player leaves. Rewards are paid on unlock and saved with the unlock. Game thread only.
/// </summary>
internal sealed class AchievementService(
	IReadOnlyList<AchievementDefinition> achievements,
	PlayerRegistry players,
	RankService ranks,
	Chat chat,
	HudBridge hud,
	ILogger logger)
{
	public IReadOnlyList<AchievementDefinition> All => achievements;

	public static AchievementCounters CountersOf(K4Player player) => new(
		player.Stats?.ToArray(),
		player.Playtime?[PlaytimeField.All],
		player.Missions?.Streak);

	/// <summary>
	/// Gives a freshly loaded player their stored unlocks. Goals they already reached unlock quietly, without points or
	/// commands: rewards are for reaching a goal while playing, not for history from before an achievement existed.
	/// </summary>
	public void Restore(K4Player player, IReadOnlyCollection<string> stored, string command)
	{
		PlayerAchievements mine = player.Achievements = new PlayerAchievements(stored);
		List<AchievementDefinition> met = AchievementRules.NewlyMet(achievements, mine.Unlocked, CountersOf(player));
		foreach (AchievementDefinition achievement in met)
			mine.Unlock(achievement.Id);

		// Goals already at 90% when joining do not toast "almost there"; only reaching 90% while playing does.
		AchievementCounters counters = CountersOf(player);
		foreach (AchievementDefinition achievement in achievements)
		{
			if (AchievementRules.Progress(achievement, counters) is int progress && ToastRules.AlmostThere(progress, achievement.Goal))
				player.ToastsShown.Add(AlmostKey(achievement));
		}

		if (met.Count > 0 && player.IsValid)
			chat.Tell(player.Controller, "achievement.retro", met.Count, command);
	}

	public void CheckAll()
	{
		foreach (K4Player player in players.ValidHumans.ToArray())
			Check(player);
	}

	/// <summary>Unlocks and rewards every achievement whose goal the player reached.</summary>
	/// <param name="leaving">The player is disconnecting: points are credited without touching their entities, and nobody is told.</param>
	public void Check(K4Player player, bool leaving = false)
	{
		if (player.Achievements is not PlayerAchievements mine || !player.IsHuman)
			return;

		AchievementCounters counters = CountersOf(player);
		foreach (AchievementDefinition achievement in AchievementRules.NewlyMet(achievements, mine.Unlocked, counters))
		{
			mine.Unlock(achievement.Id);
			Reward(player, achievement, leaving);
		}

		if (!leaving && hud.Hud is PanoramaHud panel && player.IsValid)
			ShowAlmostThere(panel, player, mine, counters);
	}

	private static string AlmostKey(AchievementDefinition achievement) => "ach90:" + achievement.Id;

	/// <summary>A short blue toast, once per achievement per connection, when progress first reaches 90% of the goal.</summary>
	private void ShowAlmostThere(PanoramaHud panel, K4Player player, PlayerAchievements mine, AchievementCounters counters)
	{
		foreach (AchievementDefinition achievement in achievements)
		{
			if (mine.Unlocked.Contains(achievement.Id) || AchievementRules.Progress(achievement, counters) is not int progress
				|| !ToastRules.AlmostThere(progress, achievement.Goal) || !player.ToastsShown.Add(AlmostKey(achievement)))
				continue;

			panel.Toast(player, new Toast(chat.For(player.Controller, "toast.achievement-close"), NameFor(achievement, player.Controller),
				$"{progress}/{achievement.Goal}", PanoramaHud.AchievementIcon(achievement.Type), ToastColor.Blue, ToastLength.Short,
				ToastRules.Percent(progress, achievement.Goal)));
		}
	}

	private void Reward(K4Player player, AchievementDefinition achievement, bool leaving)
	{
		if (achievement.Points > 0)
			ranks.CreditPoints(player, achievement.Points, refresh: !leaving && player.IsValid);

		if (achievement.Command is string template)
		{
			RankState? state = player.Rank;
			string command = RankRewards.ReplacePlaceholders(template, new RewardContext(
				player.SteamId.ToString(),
				player.HasController ? player.Controller.UserId?.ToString() ?? string.Empty : string.Empty,
				player.Name,
				state?.Rank.Name ?? string.Empty,
				state?.Rank.Id.ToString() ?? "0",
				state?.Points.ToString() ?? "0"));

			try
			{
				Server.ExecuteCommand(command);
			}
			catch (Exception ex)
			{
				logger.LogError("Achievement '{Achievement}' command '{Command}' failed: {ErrorMessage}", achievement.Id, command, ex.Message);
			}
		}

		if (leaving)
			return;

		foreach (CCSPlayerController reader in Utilities.GetPlayers())
		{
			if (!reader.IsValid || reader.IsBot || reader.IsHLTV)
				continue;

			if (reader == player.Controller)
				chat.Tell(reader, "achievement.unlocked-self", NameFor(achievement, reader), DescriptionFor(achievement, reader), achievement.Points);
			else
				chat.Tell(reader, "achievement.unlocked", player.Name, NameFor(achievement, reader));
		}

		if (hud.Hud is PanoramaHud panel && player.IsValid)
			panel.Toast(player, new Toast(chat.For(player.Controller, "hud.toast.achievement"), NameFor(achievement, player.Controller),
				achievement.Points > 0 ? $"+{achievement.Points}" : "", PanoramaHud.AchievementIcon(achievement.Type), ToastColor.Gold, ToastLength.Long,
				Tier: AchievementRules.Tier(achievement)));
	}

	public string NameFor(AchievementDefinition achievement, CCSPlayerController? reader)
		=> achievement.Name ?? chat.For(reader, "achievement.name." + achievement.Id);

	public string DescriptionFor(AchievementDefinition achievement, CCSPlayerController? reader)
		=> chat.For(reader, AchievementRules.GoalKey(achievement.Type), achievement.Goal);
}
