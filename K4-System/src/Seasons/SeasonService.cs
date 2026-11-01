namespace K4System.Seasons;

using CounterStrikeSharp.API.Core.Translations;
using K4System.Config;
using K4System.Hud;
using K4System.Players;
using K4System.Services;
using K4System.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Monthly seasons. The database holds the current season; when its month is over, one server saves everyone, then
/// ends it in one transaction (final points kept in k4seasons, all points back to the start). Every server notices
/// the new season on its next check and resets its online players in memory. Points captured in an older season are
/// never written over the reset (see <see cref="SqlPlayerStore.SaveAsync"/>). Game thread only, except the database work.
/// </summary>
internal sealed class SeasonService(
	PluginConfig config,
	PlayerRegistry players,
	RankService ranks,
	SqlPlayerStore store,
	Func<Task<bool>> saveAll,
	TimeZoneInfo zone,
	GameThread gameThread,
	BackgroundWork background,
	Chat chat,
	HudBridge hud,
	ILogger logger)
{
	/// <summary>Seconds between checks; a season ends at most this late.</summary>
	public const float CheckSeconds = 60f;

	private bool busy;

	/// <summary>The season this server plays in; null until the database answered.</summary>
	public string? Current { get; private set; }

	public TimeZoneInfo Zone => zone;

	/// <summary>Reads (or writes, on the very first start) the stored season. Starting never ends a season.</summary>
	public void Start(Task storageReady)
	{
		background.Run(async () =>
		{
			await storageReady;
			string season = await store.EnsureSeasonAsync(SeasonClock.MonthlyId(DateTime.UtcNow, zone));
			gameThread.Post(() => Adopt(season, announce: false));
		}, "Reading the current season");
	}

	/// <summary>The repeating check: ends a finished season, or picks up one another server started.</summary>
	public void Tick()
	{
		if (Current is not string current || busy)
			return;

		DateTime now = DateTime.UtcNow;
		if (SeasonClock.IsOver(current, now, zone))
		{
			End(current, SeasonClock.MonthlyId(now, zone), "the month ended");
			return;
		}

		busy = true;
		background.Run(async () =>
		{
			try
			{
				string? stored = await store.GetSeasonAsync();
				if (stored is not null && stored != current)
					gameThread.Post(() => Adopt(stored, announce: true));
			}
			finally
			{
				gameThread.Post(() => busy = false);
			}
		}, "Checking the season");
	}

	/// <summary>Ends the season now, for css_k4_season end. Returns false when one already started today.</summary>
	public bool EndNow(string actor)
	{
		if (Current is not string current || busy)
			return false;

		string next = SeasonClock.TodayId(DateTime.UtcNow, zone);
		if (next == current)
			return false;

		End(current, next, $"{actor} ended it");
		return true;
	}

	private void End(string from, string to, string why)
	{
		busy = true;
		Task<bool> saved = saveAll();

		background.Run(async () =>
		{
			try
			{
				// Everyone's latest points first, so the stored final results are complete.
				if (!await saved)
					logger.LogWarning("Some players could not be saved before season {Season} ended; their final points may be a few minutes old.", from);

				bool ended = await store.EndSeasonAsync(from, to, config.Ranks.StartPoints, ranks.Ladder.None.Name);
				if (ended)
					logger.LogInformation("Season {Season} ended ({Reason}). Season {Next} started and all points are back to {Start}.", from, why, to, config.Ranks.StartPoints);

				string season = await store.GetSeasonAsync() ?? to;
				gameThread.Post(() => Adopt(season, announce: true));
			}
			finally
			{
				gameThread.Post(() => busy = false);
			}
		}, $"Ending season {from}");
	}

	private void Adopt(string season, bool announce)
	{
		bool changed = Current is not null && Current != season;
		Current = season;

		foreach (K4Player player in players.All.ToArray())
			Join(player);

		if (!changed || !announce)
			return;

		foreach (K4Player player in players.ValidHumans)
		{
			string name = SeasonClock.Name(season, player.Controller.GetLanguage());
			chat.Tell(player.Controller, "season.started", name, config.Ranks.StartPoints);
			hud.Hud?.ShowToast(player, "hud.toast.season", chat.For(player.Controller, "hud.toast.season-text", name), ToastKind.Gold, "ic-trophy");
		}

		ranks.RefreshPlacements();
		hud.Hud?.SeasonChanged();
	}

	/// <summary>
	/// Puts a loaded player into the current season. A player whose points were loaded in an earlier season (they
	/// joined while it ended) starts the new one from the start points, like everyone else.
	/// </summary>
	public void Join(K4Player player)
	{
		if (Current is not string current || !player.IsHuman || player.Rank is null || player.Season == current)
			return;

		bool reset = player.Season is not null;
		player.Season = current;
		if (!reset)
			return;

		player.Rank.Points = config.Ranks.StartPoints;
		player.Rank.RoundPoints = 0;
		player.Rank.RoundReasons.Clear();
		if (player.IsValid)
			ranks.RefreshRank(player, announce: false, allowRewards: false);
	}

	/// <summary>Reminds a joining player in the season's last days.</summary>
	public void Greet(K4Player player)
	{
		if (Current is string current && SeasonClock.EndsSoon(current, DateTime.UtcNow, zone) && player.IsValid)
			chat.Tell(player.Controller, "season.ending", SeasonClock.Name(current, player.Controller.GetLanguage()), TimeLeft(player.Controller, current));
	}

	/// <summary>"2d, 4h" until the season ends, with the reader's unit names.</summary>
	public string TimeLeft(CounterStrikeSharp.API.Core.CCSPlayerController? reader, string season)
		=> PlaytimeCounters.Format((int)Math.Min(SeasonClock.Left(season, DateTime.UtcNow, zone).TotalSeconds, int.MaxValue),
			unit => chat.For(reader, $"unit.{unit}"), 2);
}
