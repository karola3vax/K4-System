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

		foreach (AchievementDefinition achievement in AchievementRules.NewlyMet(achievements, mine.Unlocked, CountersOf(player)))
		{
			mine.Unlock(achievement.Id);
			Reward(player, achievement, leaving);
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
			panel.ShowToast(player, "hud.toast.achievement", NameFor(achievement, player.Controller), ToastKind.Gold, "ic-trophy");
	}

	public string NameFor(AchievementDefinition achievement, CCSPlayerController? reader)
		=> achievement.Name ?? chat.For(reader, "achievement.name." + achievement.Id);

	public string DescriptionFor(AchievementDefinition achievement, CCSPlayerController? reader)
		=> chat.For(reader, AchievementRules.GoalKey(achievement.Type), achievement.Goal);
}
