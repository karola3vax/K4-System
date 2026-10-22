namespace K4System;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using K4System.Api;
using K4System.Commands;
using K4System.Config;
using K4System.Domain;
using K4System.Hud;
using K4System.Missions;
using K4System.Players;
using K4System.Ranks;
using K4System.Services;
using K4System.Storage;
using Microsoft.Extensions.Logging;
using MySqlConnector;

[MinimumApiVersion(376)]
public sealed class Plugin : BasePlugin
{
	public override string ModuleName => "K4-System";

	public override string ModuleDescription => "Enhances the server with playtime tracking, statistics, commands, and player ranks.";

	public override string ModuleAuthor => "K4ryuu; maintained by karola3vax";

	public override string ModuleVersion => "6.3.3 " +
#if RELEASE
		"(release)";
#else
		"(debug)";
#endif

	// How long unloading may wait for the final save before giving up.
	private static readonly TimeSpan UnloadSaveTimeout = TimeSpan.FromSeconds(15);

	// A hot reload loads a fresh copy of this assembly, so the unfinished save worker is handed over
	// through the process-wide AppDomain data instead of a static field.
	private const string UnfinishedSavesKey = "K4System.UnfinishedSaves";
	private static readonly TimeSpan UnfinishedSavesWait = TimeSpan.FromMinutes(2);

	public PluginConfig Config { get; private set; } = new();

	private volatile bool active;
	private PlayerRegistry? players;
	private PlayerService? playerService;
	private RankService? ranks;
	private PlaytimeService? playtime;
	private ScoreboardService? scoreboard;
	private SaveQueue? saves;
	private BackgroundWork? background;
	private CountryLookup? countries;
	private PanoramaHud? hud;
	private SharedPlayerApi.Source? apiSource;

	/// <summary>Reads and checks k4-system.jsonc. Logs the reason and returns false when K4 must not start.</summary>
	private bool TryLoadConfig()
	{
		ConfigLoadResult result = ConfigFile.Load(ConfigFile.DirectoryFor(ModuleDirectory));

		if (result.Created)
			Logger.LogInformation("Created the default config file: {Path}", result.Path);

		if (result.Config is null)
		{
			Logger.LogError("{Error}", result.Error);
			return false;
		}

		try
		{
			foreach (string warning in ConfigValidator.ValidateAndNormalize(result.Config))
				Logger.LogWarning("{Warning}", warning);
		}
		catch (InvalidDataException ex)
		{
			Logger.LogError("{Path}: {Error} K4-System is not running until this is fixed.", result.Path, ex.Message);
			return false;
		}

		Config = result.Config;
		return true;
	}

	/// <summary>The shipped language files plus the server's own lines from configs/plugins/K4-System/lang.</summary>
	private Microsoft.Extensions.Localization.IStringLocalizer LoadLanguage()
	{
		string directory = Path.Combine(ConfigFile.DirectoryFor(ModuleDirectory), "lang");
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> lines = new Dictionary<string, IReadOnlyDictionary<string, string>>();
		try
		{
			(lines, IReadOnlyList<CustomLanguage.FileCheck> files) = CustomLanguage.Load(directory);
			foreach (CustomLanguage.FileCheck file in files.Where(file => file.Problem is not null))
				Logger.LogError("{File} in {Path} can't be read, so that language uses the normal text: {Problem}", file.Name, directory, file.Problem);
			if (lines.Count > 0)
				Logger.LogInformation("Your own wording from {Path}: {Files}", directory,
					string.Join(", ", files.Where(file => file.Problem is null).Select(file => file.Name)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.LogWarning("Could not read {Path} for your own wording: {Error}", directory, ex.Message);
		}

		return new CustomLanguage(Localizer, Path.Combine(ModuleDirectory, "lang"), lines);
	}

	public override void Load(bool hotReload)
	{
		if (!TryLoadConfig())
			return;

		active = true;

		Chat chat = new(LoadLanguage());
		GameThread gameThread = new(() => active);
		GameState game = new(Logger);

		players = new PlayerRegistry();
		background = new BackgroundWork(Logger);
		countries = new CountryLookup(ModuleDirectory, Logger);

		SqlDialect dialect = CreateDialect();
		TableNames tables = new(Config.Storage.TablePrefix, Config.Storage.LevelRanks.Table);
		SqlPlayerStore store = new(dialect, tables, Config.Storage.LevelRanks.Enabled, Logger);
		saves = new SaveQueue(store.SaveAsync, Logger);
		Task storageReady = StartStorage(store);

		HudBridge hudBridge = new();
		RewardService? rewards = null;
		if (Config.Modules.Ranks)
		{
			RankLadder ladder = LoadRankLadder();
			ClanTagService clanTags = new(Config, countries, gameThread);
			rewards = new RewardService(ladder, players, store, gameThread, background, Logger);
			ranks = new RankService(Config, ladder, players, game, rewards, clanTags, store, gameThread, background, chat, hudBridge, Logger);
			scoreboard = new ScoreboardService(this, Config, players, gameThread, Logger);
		}

		playtime = new PlaytimeService(chat);
		StatService? stats = Config.Modules.Stats ? new StatService(Config, players, game) : null;
		MissionService? missions = ranks is not null && Config.Modules.Missions && LoadMissionPool() is MissionPool pool
			? new MissionService(Config, pool, ResolveMissionTimeZone(), players, ranks, game, store, gameThread, background, chat, hudBridge, (seconds, action) => AddTimer(seconds, action))
			: null;

		playerService = new PlayerService(this, Config, players, store, saves, storageReady, ranks, rewards, missions, playtime, game, gameThread, background, countries, chat, Logger);

		RankService? rankService = ranks;
		int gameThreadId = Environment.CurrentManagedThreadId;
		apiSource = new SharedPlayerApi.Source(players, () => active, () => Environment.CurrentManagedThreadId == gameThreadId,
			(player, points) => rankService?.SetPoints(player, points));
		SharedPlayerApi.Register(apiSource, Logger);

		if (Config.Panorama.Enabled)
		{
			hud = new PanoramaHud(this, Config, players, ranks, missions, playtime, store, gameThread, background, chat, Logger);
			hudBridge.Hud = hud;
			RegisterHudListeners(hud, players);
			hud.Warmup();
		}

		new GameEvents(this, Config, players, playerService, ranks, stats, Config.Modules.Playtime ? playtime : null, scoreboard, missions, store, game, gameThread, background, chat, Logger).Register();
		new K4Commands(this, Config, players, playerService, ranks, rewards, missions, playtime, store, gameThread, background, chat, new CommandGuard(chat), hud).Register();

		scoreboard?.Start(hotReload);

		Logger.LogInformation("K4-System {Version} loaded with {Database} storage.", ModuleVersion, store.DialectName);

		if (hotReload)
		{
			playerService.QueueConnectedPlayersRefresh(0.1f);
			AddTimer(2.0f, () => gameThread.PostWorldUpdate(game.TryUpdate));
		}
	}

	private void RegisterHudListeners(PanoramaHud panel, PlayerRegistry registry)
	{
		RegisterListener<Listeners.OnCustomHudClicked>(panel.OnClick);
		RegisterListener<Listeners.OnMapStart>(_ => panel.OnMapStart());
		RegisterListener<Listeners.OnPlayerButtonsChanged>((controller, pressed, _) =>
		{
			if (registry.Get(controller) is K4Player player)
				panel.OnButtonsPressed(player, pressed);
		});
		RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
		{
			if (@event.Userid is { } controller)
				panel.OnDisconnect(controller);
			return HookResult.Continue;
		}, HookMode.Pre);

		// The panel captures the mouse, so nobody should start a live round unable to aim.
		RegisterEventHandler<EventRoundFreezeEnd>((_, _) =>
		{
			panel.CloseAll();
			return HookResult.Continue;
		});
	}

	private SqlDialect CreateDialect()
	{
		StorageSettings storage = Config.Storage;

		if (ConfigValidator.IsSqlite(storage))
		{
			string path = Path.IsPathRooted(storage.SqliteFile) ? storage.SqliteFile : Path.Combine(ModuleDirectory, storage.SqliteFile);
			return new SqliteDialect(path);
		}

		MySqlSettings settings = storage.MySql;

		MySqlConnectionStringBuilder builder = new()
		{
			Server = settings.Host,
			UserID = settings.Username,
			Password = settings.Password,
			Database = settings.Database,
			Port = (uint)settings.Port,
			SslMode = Enum.Parse<MySqlSslMode>(settings.SslMode, true)
		};

		return new MySqlDialect(builder.ConnectionString, settings.Database);
	}

	// Creates and migrates tables in the background, then purges inactive players.
	private Task StartStorage(SqlPlayerStore store)
	{
		int purgeDays = Config.Storage.PurgeAfterDays;

		Task? unfinished = AppDomain.CurrentDomain.GetData(UnfinishedSavesKey) as Task;
		AppDomain.CurrentDomain.SetData(UnfinishedSavesKey, null);

		Task ready = Task.Run(async () =>
		{
			// The previous instance may still be writing newer data; reading players before it finishes would load stale values.
			if (unfinished is not null && !unfinished.IsCompleted)
			{
				Logger.LogWarning("Waiting for the previous K4-System instance to finish saving player data.");
				if (await Task.WhenAny(unfinished, Task.Delay(UnfinishedSavesWait)) != unfinished)
					Logger.LogError("The previous K4-System instance is still saving after {Minutes} minutes; continuing anyway.", UnfinishedSavesWait.TotalMinutes);
			}

			try
			{
				await store.InitializeAsync();
			}
			catch (Exception ex)
			{
				Logger.LogError("K4-System database startup failed: {ErrorMessage}", ex.Message);
				throw;
			}
		});

		background!.Run(async () =>
		{
			await ready.ContinueWith(_ => { }, TaskScheduler.Default);
			if (ready.IsCompletedSuccessfully)
				await store.PurgeAsync(purgeDays);
		}, "Purging inactive players");

		return ready;
	}

	/// <summary>Reads missions.jsonc, writing the default list on first start. Null turns missions off for this session.</summary>
	private MissionPool? LoadMissionPool()
	{
		string path = Path.Join(ModuleDirectory, MissionFile.FileName);

		try
		{
			if (!File.Exists(path))
			{
				File.WriteAllText(path, MissionFile.DefaultContent);
				Logger.LogInformation("Default missions file created.");
			}
			else if (MissionFile.IsOldDefault(File.ReadAllText(path)))
			{
				// Never edited, so the owner gets the new missions without doing anything.
				File.WriteAllText(path, MissionFile.DefaultContent);
				Logger.LogInformation("{File} was never edited, so it was updated to the new default list with more missions and a weekly mission.", MissionFile.FileName);
			}

			MissionPool pool = MissionFile.Parse(File.ReadAllText(path));
			int weekly = pool.Of(MissionDifficulty.Weekly).Count;
			Logger.LogInformation("Missions loaded: {Count} daily, {Weekly} weekly.", pool.Missions.Count - weekly, weekly);
			if (weekly == 0)
				Logger.LogInformation("{File} has no Weekly missions, so there is no weekly mission. github.com/karola3vax/K4-System/blob/main/docs/missions.md shows how to add some.", MissionFile.FileName);
			foreach (string warning in MissionFile.WeaponWarnings(pool))
				Logger.LogWarning("{Warning}", warning);
			return pool;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
		{
			Logger.LogError("Daily missions are off because {File} could not be loaded: {Error}", MissionFile.FileName, ex.Message);
			return null;
		}
	}

	private TimeZoneInfo ResolveMissionTimeZone()
	{
		(TimeZoneInfo zone, string? warning) = MissionRules.ResolveTimeZone(Config.Missions.Timezone);
		if (warning is not null)
			Logger.LogWarning("{Warning}", warning);
		return zone;
	}

	private RankLadder LoadRankLadder()
	{
		string path = Path.Join(ModuleDirectory, RankFile.FileName);
		string content;

		try
		{
			if (!File.Exists(path))
			{
				File.WriteAllText(path, RankFile.DefaultContent);
				Logger.LogInformation("Default ranks file created.");
			}

			content = File.ReadAllText(path);
		}
		catch (Exception ex)
		{
			Logger.LogError("Unable to read or create ranks.jsonc. Default ranks will be used in memory for this session: {ErrorMessage}", ex.Message);
			content = RankFile.DefaultContent;
		}

		List<RankDefinition> definitions;
		try
		{
			definitions = RankFile.Parse(content);
		}
		catch (Exception ex)
		{
			Logger.LogError("Unable to load ranks.jsonc. The existing file was left unchanged and default ranks will be used for this session: {ErrorMessage}", ex.Message);
			definitions = RankFile.Parse(RankFile.DefaultContent);
		}

		// An unknown color name would be printed in chat as a word in front of the rank name.
		foreach (RankDefinition rank in definitions.Where(rank => !ChatColorText.IsKnown(rank.Color)))
		{
			Logger.LogWarning("Rank '{RankName}' uses the unknown color '{Color}'; using the default color. Valid names are listed in CounterStrikeSharp's ChatColors, for example lightred, gold, silver.", rank.Name, rank.Color);
			rank.Color = "default";
		}

		RankLadder ladder = RankLadder.Create(definitions, Config.Ranks.Mode, ChatColorText.Apply);
		Logger.LogInformation("Rank game mode {GameMode} loaded with {ActiveRankCount} active non-default ranks.", ladder.Mode, ladder.ActiveRankCount);

		if (ladder.ActiveRankCount == 0)
			Logger.LogWarning("Rank game mode {GameMode} has no active non-default ranks.", ladder.Mode);

		return ladder;
	}

	public override void Unload(bool hotReload)
	{
		active = false;

		try
		{
			if (players is not null && playerService is not null)
			{
				playerService.SettleAllPlaytime();

				Task<bool> saved = playerService.SaveAll();
				if (saves is not null && !saves.Complete(UnloadSaveTimeout))
				{
					AppDomain.CurrentDomain.SetData(UnfinishedSavesKey, saves.Worker);
					Logger.LogError("K4-System could not finish saving player data within {Seconds} seconds while unloading. Saving continues in the background; a reloaded K4-System waits for it.", UnloadSaveTimeout.TotalSeconds);
				}
				else if (saved.IsCompleted && !saved.Result)
					Logger.LogError("Some player data could not be saved while unloading.");

				background?.WaitAll(TimeSpan.FromSeconds(5));

				if (ranks is not null)
				{
					foreach (K4Player player in players.All.ToArray())
					{
						if (player.IsHuman)
							ranks.ReleasePlayer(player);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("An error occurred while saving player data during unload: {ErrorMessage}", ex.Message);
		}

		hud?.Shutdown();
		scoreboard?.Stop();

		if (apiSource is not null)
			SharedPlayerApi.Release(apiSource);

		countries?.Dispose();
		players?.Clear();
	}
}
