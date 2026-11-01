namespace K4System.Config;

using System.Text.Json.Serialization;

// K4-System 6.0 configuration, read from configs/plugins/K4-System/k4-system.jsonc.
// The commented template in Config/default-config.jsonc must match these defaults; a test checks it.

public sealed class PluginConfig
{
	public const int CurrentVersion = 1;

	[JsonPropertyName("version")]
	public int Version { get; set; } = CurrentVersion;

	[JsonPropertyName("storage")]
	public StorageSettings Storage { get; set; } = new();

	[JsonPropertyName("modules")]
	public ModuleSettings Modules { get; set; } = new();

	[JsonPropertyName("tracking")]
	public TrackingSettings Tracking { get; set; } = new();

	[JsonPropertyName("ranks")]
	public RankSettings Ranks { get; set; } = new();

	[JsonPropertyName("points")]
	public PointsSettings Points { get; set; } = new();

	[JsonPropertyName("anti-farm")]
	public AntiFarmSettings AntiFarm { get; set; } = new();

	[JsonPropertyName("events")]
	public EventSettings Events { get; set; } = new();

	[JsonPropertyName("missions")]
	public MissionSettings Missions { get; set; } = new();

	[JsonPropertyName("bounty")]
	public BountySettings Bounty { get; set; } = new();

	[JsonPropertyName("duels")]
	public DuelSettings Duels { get; set; } = new();

	[JsonPropertyName("scoreboard")]
	public ScoreboardSettings Scoreboard { get; set; } = new();

	[JsonPropertyName("panorama")]
	public PanoramaSettings Panorama { get; set; } = new();

	[JsonPropertyName("tags")]
	public TagSettings Tags { get; set; } = new();

	[JsonPropertyName("messages")]
	public MessageSettings Messages { get; set; } = new();

	[JsonPropertyName("commands")]
	public CommandSettings Commands { get; set; } = new();

	/// <summary>Point values after applying the preset and overrides. Set during validation.</summary>
	[JsonIgnore]
	public PointValues PointValues { get; set; } = PointPresets.Get(PointPresets.Casual);
}

public sealed class StorageSettings
{
	/// <summary>"sqlite" or "mysql".</summary>
	[JsonPropertyName("type")]
	public string Type { get; set; } = "sqlite";

	[JsonPropertyName("sqlite-file")]
	public string SqliteFile { get; set; } = "k4system.db";

	[JsonPropertyName("mysql")]
	public MySqlSettings MySql { get; set; } = new();

	[JsonPropertyName("table-prefix")]
	public string TablePrefix { get; set; } = "";

	[JsonPropertyName("purge-after-days")]
	public int PurgeAfterDays { get; set; } = 30;

	[JsonPropertyName("levelranks")]
	public LevelRanksSettings LevelRanks { get; set; } = new();
}

public sealed class MySqlSettings
{
	[JsonPropertyName("host")]
	public string Host { get; set; } = "localhost";

	[JsonPropertyName("port")]
	public int Port { get; set; } = 3306;

	[JsonPropertyName("database")]
	public string Database { get; set; } = "cs2";

	[JsonPropertyName("username")]
	public string Username { get; set; } = "root";

	[JsonPropertyName("password")]
	public string Password { get; set; } = "";

	[JsonPropertyName("ssl-mode")]
	public string SslMode { get; set; } = "None";
}

public sealed class LevelRanksSettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	[JsonPropertyName("table")]
	public string Table { get; set; } = "lvl_base";
}

public sealed class ModuleSettings
{
	[JsonPropertyName("ranks")]
	public bool Ranks { get; set; } = true;

	[JsonPropertyName("stats")]
	public bool Stats { get; set; } = true;

	[JsonPropertyName("playtime")]
	public bool Playtime { get; set; } = true;

	[JsonPropertyName("admin-list")]
	public bool AdminList { get; set; } = true;

	[JsonPropertyName("missions")]
	public bool Missions { get; set; } = true;

	[JsonPropertyName("achievements")]
	public bool Achievements { get; set; } = true;

	[JsonPropertyName("seasons")]
	public bool Seasons { get; set; } = true;

	[JsonPropertyName("profiles")]
	public bool Profiles { get; set; } = true;
}

/// <summary>When points and stats count at all.</summary>
public sealed class TrackingSettings
{
	[JsonPropertyName("minimum-players")]
	public int MinimumPlayers { get; set; } = 1;

	[JsonPropertyName("count-warmup")]
	public bool CountWarmup { get; set; }

	[JsonPropertyName("count-bots")]
	public bool CountBots { get; set; } = true;

	[JsonPropertyName("free-for-all")]
	public bool FreeForAll { get; set; }
}

public sealed class RankGameModeJsonConverter : JsonStringEnumConverter<RankGameMode>
{
	public RankGameModeJsonConverter() : base(null, false) { }
}

[JsonConverter(typeof(RankGameModeJsonConverter))]
public enum RankGameMode
{
	Normal,
	FlagsOnly,
	NoFlags
}

public sealed class RankSettings
{
	[JsonPropertyName("mode")]
	public RankGameMode Mode { get; set; } = RankGameMode.Normal;

	[JsonPropertyName("start-points")]
	public int StartPoints { get; set; }

	[JsonPropertyName("vip-multiplier")]
	public double VipMultiplier { get; set; } = 1.25;

	[JsonPropertyName("round-summary")]
	public bool RoundSummary { get; set; }

	[JsonPropertyName("names-in-kill-messages")]
	public bool NamesInKillMessages { get; set; }

	[JsonPropertyName("killstreak-window-seconds")]
	public int KillstreakWindowSeconds { get; set; }

	[JsonPropertyName("reset-killstreak-on-round-end")]
	public bool ResetKillstreakOnRoundEnd { get; set; } = true;

	[JsonPropertyName("reset-cooldown-hours")]
	public int ResetCooldownHours { get; set; } = 24;

	[JsonPropertyName("dynamic-death-points")]
	public DynamicDeathPointSettings DynamicDeathPoints { get; set; } = new();

	[JsonPropertyName("team-balance")]
	public TeamBalanceSettings TeamBalance { get; set; } = new();

	[JsonPropertyName("playtime-reward")]
	public PlaytimeRewardSettings PlaytimeReward { get; set; } = new();
}

public sealed class DynamicDeathPointSettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	[JsonPropertyName("min-multiplier")]
	public double MinMultiplier { get; set; } = 0.5;

	[JsonPropertyName("max-multiplier")]
	public double MaxMultiplier { get; set; } = 3.0;
}

public sealed class TeamBalanceSettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	[JsonPropertyName("max-difference")]
	public int MaxDifference { get; set; } = 200;
}

public sealed class PlaytimeRewardSettings
{
	[JsonPropertyName("every-minutes")]
	public float EveryMinutes { get; set; } = 10;

	[JsonPropertyName("include-afk")]
	public bool IncludeAfk { get; set; }
}

public sealed class PointsSettings
{
	[JsonPropertyName("preset")]
	public string Preset { get; set; } = PointPresets.Casual;

	/// <summary>Point values that replace the preset's, by name.</summary>
	[JsonPropertyName("overrides")]
	public Dictionary<string, int> Overrides { get; set; } = [];
}

/// <summary>Keeps kill points fair when the same two players keep meeting, or play from one connection.</summary>
public sealed class AntiFarmSettings
{
	/// <summary>Kills between players on the same connection give and take no points.</summary>
	[JsonPropertyName("same-ip")]
	public bool SameIp { get; set; } = true;

	/// <summary>Kills on the same player within the window that count fully. 0 turns the rule off.</summary>
	[JsonPropertyName("repeat-kills")]
	public int RepeatKills { get; set; } = 10;

	[JsonPropertyName("window-minutes")]
	public int WindowMinutes { get; set; } = 10;

	/// <summary>The share of the points each further kill on that player is worth, and the death costs.</summary>
	[JsonPropertyName("reduced-percent")]
	public int ReducedPercent { get; set; } = 25;
}

/// <summary>More points for playing at set times. Admins can also start one with css_k4_event.</summary>
public sealed class EventSettings
{
	[JsonPropertyName("schedule")]
	public List<EventSchedule?> Schedule { get; set; } = [];

	/// <summary>The valid schedule entries. Set during validation.</summary>
	[JsonIgnore]
	internal List<Domain.EventWindow> Windows { get; set; } = [];
}

public sealed class EventSchedule
{
	/// <summary>Monday to Sunday. Empty means every day.</summary>
	[JsonPropertyName("days")]
	public List<string>? Days { get; set; } = [];

	/// <summary>24-hour local time in missions.timezone, such as "18:00".</summary>
	[JsonPropertyName("from")]
	public string? From { get; set; } = "18:00";

	/// <summary>Before "from" means the event runs past midnight.</summary>
	[JsonPropertyName("to")]
	public string? To { get; set; } = "22:00";

	[JsonPropertyName("multiplier")]
	public double Multiplier { get; set; } = 2;
}

public sealed class MissionSettings
{
	/// <summary>When missions reset at midnight: an IANA name such as "Europe/Istanbul", "UTC", or an offset such as "+03:00".</summary>
	[JsonPropertyName("timezone")]
	public string Timezone { get; set; } = "Europe/Istanbul";

	[JsonPropertyName("rewards")]
	public MissionRewardSettings Rewards { get; set; } = new();

	[JsonPropertyName("streak-bonus-percent")]
	public int StreakBonusPercent { get; set; } = 10;

	[JsonPropertyName("streak-max-days")]
	public int StreakMaxDays { get; set; } = 7;

	[JsonPropertyName("rerolls-per-day")]
	public int RerollsPerDay { get; set; } = 1;

	/// <summary>Kills, deaths, and assists involving bots count for missions. Off by default so missions cannot be farmed on bots.</summary>
	[JsonPropertyName("count-bots")]
	public bool CountBots { get; set; }

	/// <summary>
	/// Corrections for maps K4 reads wrong, by map name pattern (* matches any text). Later rules win.
	/// K4 already checks every map for bomb sites, hostages, rounds, buying, and the weapons players carry.
	/// </summary>
	[JsonPropertyName("map-rules")]
	public Dictionary<string, MapRule> MapRules { get; set; } = new()
	{
		["aim_*"] = new MapRule { Grenades = false },
		["awp_*"] = new MapRule { Grenades = false, Weapons = ["awp"] }
	};
}

/// <summary>What a map allows. Every value is optional; a missing one is detected by K4.</summary>
public sealed class MapRule
{
	[JsonPropertyName("rounds")]
	public bool? Rounds { get; set; }

	[JsonPropertyName("bomb")]
	public bool? Bomb { get; set; }

	[JsonPropertyName("hostages")]
	public bool? Hostages { get; set; }

	[JsonPropertyName("grenades")]
	public bool? Grenades { get; set; }

	/// <summary>The only weapons on the map, such as ["awp"], or ["all"].</summary>
	[JsonPropertyName("weapons")]
	public List<string>? Weapons { get; set; }

	/// <summary>Mission types never given on the map, such as ["BombPlants"].</summary>
	[JsonPropertyName("never")]
	public List<string>? Never { get; set; }
}

public sealed class MissionRewardSettings
{
	[JsonPropertyName("easy")]
	public int Easy { get; set; } = 150;

	[JsonPropertyName("medium")]
	public int Medium { get; set; } = 300;

	[JsonPropertyName("hard")]
	public int Hard { get; set; } = 600;

	[JsonPropertyName("weekly")]
	public int Weekly { get; set; } = 2500;
}

/// <summary>"Wanted": a long kill streak puts a bounty on the player.</summary>
public sealed class BountySettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	/// <summary>Kills in one life, against real players, that make a player wanted.</summary>
	[JsonPropertyName("kill-streak")]
	public int KillStreak { get; set; } = 7;

	[JsonPropertyName("reward")]
	public int Reward { get; set; } = 150;

	/// <summary>Paid to a wanted player who is still wanted when the match ends.</summary>
	[JsonPropertyName("survivor-reward")]
	public int SurvivorReward { get; set; } = 50;
}

/// <summary>Point wagers between two players: most kills until the match ends wins.</summary>
public sealed class DuelSettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	[JsonPropertyName("min-stake")]
	public int MinStake { get; set; } = 10;

	/// <summary>The stake may be at most this share of the poorer player's points.</summary>
	[JsonPropertyName("max-stake-percent")]
	public int MaxStakePercent { get; set; } = 10;

	[JsonPropertyName("accept-seconds")]
	public int AcceptSeconds { get; set; } = 30;

	[JsonPropertyName("block-same-ip")]
	public bool BlockSameIp { get; set; } = true;
}

public sealed class ScoreboardRankModeJsonConverter : JsonStringEnumConverter<ScoreboardRankMode>
{
	public ScoreboardRankModeJsonConverter() : base(null, false) { }
}

[JsonConverter(typeof(ScoreboardRankModeJsonConverter))]
public enum ScoreboardRankMode
{
	Disabled,
	Premier,
	Competitive,
	Wingman,
	Custom
}

public sealed class PanoramaSettings
{
	/// <summary>Show the K4 panel and toasts. Needs the K4 workshop addon on players' clients (MultiAddonManager).</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; }

	/// <summary>"competitive" or "wingman" shows CS2's rank icons; "text" shows the rank tag instead.</summary>
	[JsonPropertyName("rank-icons")]
	public string RankIcons { get; set; } = "competitive";
}

public sealed class ScoreboardSettings
{
	[JsonPropertyName("rank-display")]
	public ScoreboardRankMode RankDisplay { get; set; } = ScoreboardRankMode.Premier;

	[JsonPropertyName("custom-icons")]
	public CustomIconSettings CustomIcons { get; set; } = new();

	[JsonPropertyName("score-shows-points")]
	public bool ScoreShowsPoints { get; set; }
}

public sealed class CustomIconSettings
{
	[JsonPropertyName("first-id")]
	public int FirstId { get; set; }

	[JsonPropertyName("count")]
	public int Count { get; set; }

	[JsonPropertyName("offset")]
	public int Offset { get; set; }
}

public sealed class TagSettings
{
	[JsonPropertyName("rank-tags")]
	public bool RankTags { get; set; }

	[JsonPropertyName("top-placement")]
	public TopPlacementSettings TopPlacement { get; set; } = new();

	[JsonPropertyName("country")]
	public bool Country { get; set; }

	[JsonPropertyName("admins")]
	public List<AdminTag> Admins { get; set; } =
	[
		new AdminTag { Permission = "@css/root", Tag = "[Owner]", ListColor = "red" },
		new AdminTag { Permission = "@css/generic", Tag = "[Admin]", ListColor = "lightblue" }
	];
}

public sealed class TopPlacementSettings
{
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	[JsonPropertyName("max")]
	public int Max { get; set; } = 100;

	[JsonPropertyName("format")]
	public string Format { get; set; } = "TOP #{placement}";
}

public sealed class AdminTag
{
	[JsonPropertyName("permission")]
	public string Permission { get; set; } = string.Empty;

	[JsonPropertyName("tag")]
	public string? Tag { get; set; }

	[JsonPropertyName("list-color")]
	public string? ListColor { get; set; }
}

public sealed class MessageSettings
{
	[JsonPropertyName("join-hint")]
	public bool JoinHint { get; set; } = true;

	[JsonPropertyName("connect")]
	public bool Connect { get; set; }

	[JsonPropertyName("disconnect")]
	public bool Disconnect { get; set; }
}

public sealed class CommandSettings
{
	[JsonPropertyName("cooldown-seconds")]
	public int CooldownSeconds { get; set; } = 3;

	/// <summary>!rank also posts the player's place, points, kills, deaths and KDR to everyone (LVL Ranks style).</summary>
	[JsonPropertyName("rank-to-everyone")]
	public bool RankToEveryone { get; set; }

	/// <summary>Opens the panel (or the help list without it), such as !k4.</summary>
	[JsonPropertyName("main")]
	public List<string> Main { get; set; } = ["k4"];

	[JsonPropertyName("rank")]
	public List<string> Rank { get; set; } = ["rank", "myrank"];

	[JsonPropertyName("top")]
	public List<string> Top { get; set; } = ["top", "ranktop", "toplist"];

	[JsonPropertyName("ranks")]
	public List<string> Ranks { get; set; } = ["ranks", "ranklist"];

	[JsonPropertyName("stats")]
	public List<string> Stats { get; set; } = ["stats", "mystats", "stat", "mystat", "statistic", "statistics"];

	[JsonPropertyName("playtime")]
	public List<string> Playtime { get; set; } = ["time", "mytime", "playtime"];

	[JsonPropertyName("reset")]
	public List<string> Reset { get; set; } = ["resetmydata"];

	[JsonPropertyName("admins")]
	public List<string> Admins { get; set; } = ["admins", "listadmins"];

	[JsonPropertyName("missions")]
	public List<string> Missions { get; set; } = ["missions", "mission", "daily"];

	[JsonPropertyName("duel")]
	public List<string> Duel { get; set; } = ["duel"];

	[JsonPropertyName("achievements")]
	public List<string> Achievements { get; set; } = ["achievements", "achievement", "ach"];

	[JsonPropertyName("profile")]
	public List<string> Profile { get; set; } = ["profile"];

	[JsonPropertyName("season")]
	public List<string> Season { get; set; } = ["season"];

	/// <summary>Turns the player's own panel pop-ups on or off, such as !toasts. Remembered per player.</summary>
	[JsonPropertyName("toasts")]
	public List<string> Toasts { get; set; } = ["toasts"];
}
