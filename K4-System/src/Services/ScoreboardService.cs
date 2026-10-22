namespace K4System.Services;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using K4System.Config;
using K4System.Domain;
using K4System.Players;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

/// <summary>Writes K4 points or ranks into CS2's matchmaking rank fields and reveals them every few seconds.</summary>
internal sealed class ScoreboardService(BasePlugin plugin, PluginConfig config, PlayerRegistry players, GameThread gameThread, ILogger logger)
{
	private Listeners.OnTick? tickListener;
	private Timer? revealTimer;
	private bool updatesAllowed;

	public bool IsEnabled => tickListener is not null;

	public void Start(bool hotReload)
	{
		updatesAllowed = hotReload;
		ScoreboardRankMode mode = config.Scoreboard.RankDisplay;

		if (!ScoreboardRanks.ShouldEnable(mode, CoreConfig.FollowCS2ServerGuidelines))
		{
			if (mode != ScoreboardRankMode.Disabled)
			{
				logger.LogWarning(
					"Scoreboard ranks are disabled because CounterStrikeSharp follows CS2 server guidelines. To use them, review FollowCS2ServerGuidelines in addons/counterstrikesharp/configs/core.json and restart K4-System.");
			}
			return;
		}

		tickListener = UpdateRanks;
		plugin.RegisterListener(tickListener);
		revealTimer = plugin.AddTimer(ScoreboardRanks.RevealIntervalSeconds, Reveal, TimerFlags.REPEAT);

		if (ConVar.Find("mp_halftime")?.GetPrimitiveValue<bool>() == false)
			logger.LogWarning("Halftime is disabled, which may cause scoreboard rank rendering problems.");

		logger.LogInformation("Built-in scoreboard ranks enabled in {ScoreboardRankMode} mode.", mode);
	}

	public void Stop()
	{
		if (tickListener is not null)
		{
			plugin.RemoveListener(tickListener);
			tickListener = null;
		}

		revealTimer?.Kill();
		revealTimer = null;
		updatesAllowed = false;
	}

	/// <summary>Updates start with the first round and stop when the match ends.</summary>
	public void AllowUpdates(bool allowed) => updatesAllowed = allowed;

	private void UpdateRanks()
	{
		if (!updatesAllowed || !gameThread.IsActive || CoreConfig.FollowCS2ServerGuidelines)
			return;

		ScoreboardSettings settings = config.Scoreboard;

		foreach (K4Player player in players.All)
		{
			if (player.Rank is null || !player.IsHuman || !player.IsValid)
				continue;

			ScoreboardRankValue value = ScoreboardRanks.GetValue(
				settings.RankDisplay,
				player.Rank.Rank.Id,
				player.Rank.Points,
				settings.CustomIcons.FirstId,
				settings.CustomIcons.Count,
				settings.CustomIcons.Offset);

			CCSPlayerController controller = player.Controller;
			if (controller.CompetitiveWins == 10 && controller.CompetitiveRankType == value.RankType && controller.CompetitiveRanking == value.Ranking)
				continue;

			controller.CompetitiveWins = 10;
			controller.CompetitiveRankType = value.RankType;
			controller.CompetitiveRanking = value.Ranking;
			// Flag every changed field; in Premier mode only the ranking number may change.
			Utilities.SetStateChanged(controller, "CCSPlayerController", "m_iCompetitiveWins");
			Utilities.SetStateChanged(controller, "CCSPlayerController", "m_iCompetitiveRankType");
			Utilities.SetStateChanged(controller, "CCSPlayerController", "m_iCompetitiveRanking");
		}
	}

	private void Reveal()
	{
		if (!gameThread.IsActive || CoreConfig.FollowCS2ServerGuidelines)
			return;

		try
		{
			using UserMessage message = UserMessage.FromPartialName(ScoreboardRanks.RevealMessageName);
			message.Recipients.AddAllPlayers();
			message.Send();
		}
		catch (Exception ex)
		{
			logger.LogError("Unable to reveal scoreboard ranks; the reveal timer was stopped: {ErrorMessage}", ex.Message);
			revealTimer?.Kill();
			revealTimer = null;
		}
	}
}
