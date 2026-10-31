namespace K4System.Config;

using System.Text.RegularExpressions;
using K4System.Domain;
using MySqlConnector;

/// <summary>
/// Checks a parsed configuration. Mistakes that would make K4 behave unpredictably throw with a
/// message that names the setting. Out-of-range numbers are corrected for this session and reported.
/// </summary>
internal static partial class ConfigValidator
{
	public const string MySql = "mysql";
	public const string Sqlite = "sqlite";

	[GeneratedRegex("^[A-Za-z0-9_]*$")]
	private static partial Regex TablePrefixPattern();

	[GeneratedRegex("^[A-Za-z0-9_]+$")]
	private static partial Regex TableNamePattern();

	public static bool IsSqlite(StorageSettings storage)
		=> string.Equals(storage.Type?.Trim(), Sqlite, StringComparison.OrdinalIgnoreCase);

	/// <summary>Validates, resolves the point values, and returns warnings to log.</summary>
	public static List<string> ValidateAndNormalize(PluginConfig config)
	{
		ValidateSections(config);
		ValidateStorage(config.Storage);
		ValidateRanks(config.Ranks);
		ValidateScoreboard(config.Scoreboard);
		config.PointValues = ResolvePoints(config.Points);
		return Normalize(config);
	}

	internal static void ValidateSections(PluginConfig config)
	{
		if (config.Storage?.MySql is null || config.Storage.LevelRanks is null || config.Modules is null || config.Tracking is null ||
			config.Ranks?.DynamicDeathPoints is null || config.Ranks.TeamBalance is null || config.Ranks.PlaytimeReward is null ||
			config.Points?.Overrides is null || config.AntiFarm is null || config.Missions?.Rewards is null || config.Bounty is null || config.Duels is null || config.Panorama is null || config.Scoreboard?.CustomIcons is null ||
			config.Tags?.TopPlacement is null || config.Tags.Admins is null || config.Messages is null || config.Commands is null)
			throw new InvalidDataException("A section of k4-system.jsonc is missing or null. Delete the file to get a fresh one.");

		CommandSettings commands = config.Commands;
		List<string>?[] aliasLists = [commands.Main, commands.Rank, commands.Top, commands.Ranks, commands.Stats, commands.Playtime, commands.Reset, commands.Admins, commands.Missions, commands.Duel];
		if (aliasLists.Any(list => list is null || list.Any(string.IsNullOrWhiteSpace)))
			throw new InvalidDataException("commands: every alias list must exist and contain no blank names.");

		if (config.Tags.Admins.Any(admin => admin is null || string.IsNullOrWhiteSpace(admin.Permission)))
			throw new InvalidDataException("tags.admins: every entry needs a permission.");

		if (config.Tags.TopPlacement.Format is null)
			throw new InvalidDataException("tags.top-placement.format cannot be null.");
	}

	internal static void ValidateStorage(StorageSettings storage)
	{
		string type = storage.Type?.Trim() ?? string.Empty;
		bool isSqlite = string.Equals(type, Sqlite, StringComparison.OrdinalIgnoreCase);

		if (!isSqlite && !string.Equals(type, MySql, StringComparison.OrdinalIgnoreCase))
			throw new InvalidDataException($"storage.type '{storage.Type}' is invalid. Use sqlite or mysql.");

		if (isSqlite && string.IsNullOrWhiteSpace(storage.SqliteFile))
			throw new InvalidDataException("storage.sqlite-file cannot be blank.");

		if (!isSqlite && (string.IsNullOrWhiteSpace(storage.MySql.Host) || string.IsNullOrWhiteSpace(storage.MySql.Username) || string.IsNullOrWhiteSpace(storage.MySql.Database)))
			throw new InvalidDataException("storage.mysql: host, username, and database are required.");

		if (storage.TablePrefix is null || !TablePrefixPattern().IsMatch(storage.TablePrefix))
			throw new InvalidDataException("storage.table-prefix may only contain letters, numbers, and underscores.");

		if (storage.LevelRanks.Enabled && (storage.LevelRanks.Table is null || !TableNamePattern().IsMatch(storage.LevelRanks.Table)))
			throw new InvalidDataException("storage.levelranks.table may only contain letters, numbers, and underscores.");
	}

	internal static void ValidateRanks(RankSettings ranks)
	{
		if (!Enum.IsDefined(ranks.Mode))
			throw new InvalidDataException($"ranks.mode '{ranks.Mode}' is invalid. Use Normal, FlagsOnly, or NoFlags.");
	}

	internal static void ValidateScoreboard(ScoreboardSettings scoreboard)
	{
		if (!Enum.IsDefined(scoreboard.RankDisplay))
			throw new InvalidDataException($"scoreboard.rank-display '{scoreboard.RankDisplay}' is invalid. Use Disabled, Premier, Competitive, Wingman, or Custom.");

		if (scoreboard.RankDisplay != ScoreboardRankMode.Custom)
			return;

		CustomIconSettings icons = scoreboard.CustomIcons;
		if (icons.FirstId <= 0 || icons.Count <= 0 || icons.Offset < 0)
			throw new InvalidDataException("scoreboard.custom-icons needs a positive first-id and count, and an offset of 0 or more.");

		if ((long)icons.FirstId + icons.Count - 1 > int.MaxValue)
			throw new InvalidDataException("scoreboard.custom-icons: first-id plus count is too large.");
	}

	internal static PointValues ResolvePoints(PointsSettings points)
	{
		if (string.IsNullOrWhiteSpace(points.Preset) || !PointPresets.Exists(points.Preset))
			throw new InvalidDataException($"points.preset '{points.Preset}' is invalid. Use {string.Join(", ", PointPresets.Names)}.");

		string[] unknown = points.Overrides.Keys.Where(name => !PointValues.Names.Contains(name)).ToArray();
		if (unknown.Length > 0)
			throw new InvalidDataException($"points.overrides has unknown names: {string.Join(", ", unknown)}. See the list in the config file comments.");

		return PointPresets.Get(points.Preset).With(points.Overrides);
	}

	/// <summary>Command names that cannot be typed, or that sit in two lists and so run two commands at once.</summary>
	internal static IEnumerable<string> CommandNameWarnings(CommandSettings commands)
	{
		(string List, List<string> Names)[] lists =
		[
			("main", commands.Main), ("rank", commands.Rank), ("top", commands.Top), ("ranks", commands.Ranks), ("stats", commands.Stats),
			("playtime", commands.Playtime), ("reset", commands.Reset), ("admins", commands.Admins), ("missions", commands.Missions),
			("duel", commands.Duel)
		];

		Dictionary<string, string> owner = new(StringComparer.OrdinalIgnoreCase);
		foreach ((string list, List<string> names) in lists)
		{
			foreach (string name in names)
			{
				if (name.Any(c => char.IsWhiteSpace(c) || c == ','))
					yield return $"commands.{list}: \"{name}\" is one name with a comma or space in it. Put each name in its own quotes, such as [\"k4\", \"lvl\"].";
				else if (owner.TryGetValue(name, out string? first) && first != list)
					yield return $"commands: \"{name}\" is in both commands.{first} and commands.{list}, so !{name} runs both commands. Keep it in one list.";
				else if (first == list)
					yield return $"commands.{list}: \"{name}\" is listed twice; it is used once.";
				else
					owner.TryAdd(name, list);
			}
		}
	}

	private static List<string> Normalize(PluginConfig config)
	{
		List<string> warnings = [.. CommandNameWarnings(config.Commands)];
		RankSettings ranks = config.Ranks;

		// A name twice in one list would register the command twice, so one !rank would answer twice.
		CommandSettings commands = config.Commands;
		foreach (List<string> names in new[] { commands.Main, commands.Rank, commands.Top, commands.Ranks, commands.Stats, commands.Playtime, commands.Reset, commands.Admins, commands.Missions, commands.Duel })
		{
			HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
			names.RemoveAll(name => !seen.Add(name));
		}
		MySqlSettings mySql = config.Storage.MySql;

		if (!IsSqlite(config.Storage))
		{
			if (mySql.Port is < 1 or > 65535)
			{
				warnings.Add($"storage.mysql.port {mySql.Port} is invalid. Using 3306.");
				mySql.Port = 3306;
			}

			if (!Enum.TryParse<MySqlSslMode>(mySql.SslMode, true, out _))
			{
				warnings.Add($"storage.mysql.ssl-mode '{mySql.SslMode}' is invalid. Using None.");
				mySql.SslMode = nameof(MySqlSslMode.None);
			}
		}

		if (config.Commands.CooldownSeconds < 0)
		{
			warnings.Add("commands.cooldown-seconds cannot be negative. Using 0.");
			config.Commands.CooldownSeconds = 0;
		}

		config.Storage.PurgeAfterDays = Math.Max(0, config.Storage.PurgeAfterDays);
		config.Tracking.MinimumPlayers = Math.Max(0, config.Tracking.MinimumPlayers);
		ranks.StartPoints = Math.Max(0, ranks.StartPoints);
		ranks.ResetCooldownHours = Math.Max(0, ranks.ResetCooldownHours);
		ranks.KillstreakWindowSeconds = Math.Max(0, ranks.KillstreakWindowSeconds);
		ranks.TeamBalance.MaxDifference = Math.Max(0, ranks.TeamBalance.MaxDifference);
		config.Tags.TopPlacement.Max = Math.Max(0, config.Tags.TopPlacement.Max);

		foreach (AdminTag admin in config.Tags.Admins.Where(admin => admin.ListColor is not null && !ChatColorText.IsKnown(admin.ListColor)))
		{
			warnings.Add($"tags.admins: '{admin.ListColor}' for {admin.Permission} is not a chat color. Using the default color.");
			admin.ListColor = "default";
		}

		if (!double.IsFinite(ranks.VipMultiplier) || ranks.VipMultiplier < 0)
		{
			warnings.Add("ranks.vip-multiplier must be 0 or more. Using 1.0.");
			ranks.VipMultiplier = 1.0;
		}

		DynamicDeathPointSettings dynamic = ranks.DynamicDeathPoints;
		if (!double.IsFinite(dynamic.MinMultiplier) || dynamic.MinMultiplier < 0)
			dynamic.MinMultiplier = 0;
		if (!double.IsFinite(dynamic.MaxMultiplier) || dynamic.MaxMultiplier < 0)
			dynamic.MaxMultiplier = 0;
		if (dynamic.MinMultiplier > dynamic.MaxMultiplier)
		{
			warnings.Add("ranks.dynamic-death-points: min-multiplier was above max-multiplier, so they were swapped.");
			(dynamic.MinMultiplier, dynamic.MaxMultiplier) = (dynamic.MaxMultiplier, dynamic.MinMultiplier);
		}

		MissionSettings missions = config.Missions;
		if (missions.Rewards.Easy < 0 || missions.Rewards.Medium < 0 || missions.Rewards.Hard < 0 || missions.Rewards.Weekly < 0)
		{
			warnings.Add("missions.rewards cannot be negative. Negative values were set to 0.");
			missions.Rewards.Easy = Math.Max(0, missions.Rewards.Easy);
			missions.Rewards.Medium = Math.Max(0, missions.Rewards.Medium);
			missions.Rewards.Hard = Math.Max(0, missions.Rewards.Hard);
			missions.Rewards.Weekly = Math.Max(0, missions.Rewards.Weekly);
		}

		missions.MapRules ??= [];
		foreach ((string pattern, MapRule? rule) in missions.MapRules.ToArray())
		{
			if (rule is null || string.IsNullOrWhiteSpace(pattern))
			{
				warnings.Add($"missions.map-rules: '{pattern}' is empty and was skipped.");
				missions.MapRules.Remove(pattern);
				continue;
			}

			foreach (string name in rule.Never ?? [])
			{
				if (!Enum.TryParse(name, ignoreCase: true, out K4System.Missions.MissionType _))
					warnings.Add($"missions.map-rules '{pattern}': '{name}' in \"never\" is not a mission type, such as BombPlants or GrenadeKills.");
			}
		}

		string icons = config.Panorama.RankIcons?.Trim().ToLowerInvariant() ?? "";
		if (icons is not ("competitive" or "wingman" or "text"))
		{
			warnings.Add($"panorama.rank-icons '{config.Panorama.RankIcons}' is invalid. Using competitive.");
			config.Panorama.RankIcons = "competitive";
		}

		missions.StreakBonusPercent = Math.Max(0, missions.StreakBonusPercent);
		missions.StreakMaxDays = Math.Max(0, missions.StreakMaxDays);
		missions.RerollsPerDay = Math.Max(0, missions.RerollsPerDay);

		if (config.Modules.Missions && !config.Modules.Ranks)
		{
			warnings.Add("modules.missions needs modules.ranks, because missions reward points. Missions are off.");
			config.Modules.Missions = false;
		}

		if (!float.IsFinite(ranks.PlaytimeReward.EveryMinutes) || ranks.PlaytimeReward.EveryMinutes < 0)
			ranks.PlaytimeReward.EveryMinutes = 0;
		// Far beyond any real interval, and safe for TimeSpan; a huge value used to throw on every timer tick.
		ranks.PlaytimeReward.EveryMinutes = Math.Min(ranks.PlaytimeReward.EveryMinutes, 10080);

		BountySettings bounty = config.Bounty;
		bounty.KillStreak = Math.Max(1, bounty.KillStreak);
		bounty.Reward = Math.Max(0, bounty.Reward);
		bounty.SurvivorReward = Math.Max(0, bounty.SurvivorReward);

		AntiFarmSettings farm = config.AntiFarm;
		farm.RepeatKills = Math.Max(0, farm.RepeatKills);
		farm.WindowMinutes = Math.Clamp(farm.WindowMinutes, 1, 1440);
		farm.ReducedPercent = Math.Clamp(farm.ReducedPercent, 0, 100);

		DuelSettings duels = config.Duels;
		duels.MinStake = Math.Max(1, duels.MinStake);
		duels.MaxStakePercent = Math.Clamp(duels.MaxStakePercent, 0, 100);
		duels.AcceptSeconds = Math.Clamp(duels.AcceptSeconds, 5, 300);

		return warnings;
	}
}
