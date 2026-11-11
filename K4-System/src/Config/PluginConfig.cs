namespace K4System.Config;

using System.Text.Json;
using System.Text.Json.Serialization;
using K4System.Domain;

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

/// <summary>Point wagers between two players: first to a number of kills of one type wins, or the leader at the time limit.</summary>
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

	/// <summary>Counted kills that win a duel at once.</summary>
	[JsonPropertyName("target-kills")]
	public int TargetKills { get; set; } = 5;

	/// <summary>Time limit: the leader wins then, a tie returns both stakes.</summary>
	[JsonPropertyName("minutes")]
	public int Minutes { get; set; } = 10;

	/// <summary>Wait after a duel ends before either player can start or accept another.</summary>
	[JsonPropertyName("cooldown-seconds")]
	public int CooldownSeconds { get; set; } = 120;

	/// <summary>Stake buttons in the panel, next to "max". At most four.</summary>
	[JsonPropertyName("stakes")]
	public List<int> Stakes { get; set; } = [100, 250, 500, 1000];

	/// <summary>Duel types players can pick: any, noscope, zeus, knife, scout, headshot.</summary>
	[JsonPropertyName("types")]
	public List<string> Types { get; set; } = [.. DuelRules.TypeNames];
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

	/// <summary>How players get K4's messages while the panel is on, and whether they may choose it themselves.</summary>
	[JsonPropertyName("toasts")]
	public ToastSettings Toasts { get; set; } = new();
}

public sealed class ToastModeJsonConverter : JsonStringEnumConverter<ToastMode>
{
	public ToastModeJsonConverter() : base(null, false) { }
}

/// <summary>Panel: a pop-up (chat only where no pop-up exists). Chat: the chat line. Both: the pop-up and the chat line.</summary>
[JsonConverter(typeof(ToastModeJsonConverter))]
public enum ToastMode
{
	Panel,
	Chat,
	Both
}

/// <summary>
/// A player's mode as stored in the settings table's `toasts` column. Older K4 versions read 0 as "toasts off" and
/// anything else as "on", so the extra values still mean something sensible after a downgrade.
/// </summary>
public static class ToastModes
{
	public const int StoredChat = 0;    // also 7.3 and older "toasts off"
	public const int StoredDefault = 1; // no choice made: the older "on", and the column default for new rows
	public const int StoredBoth = 2;
	public const int StoredPanel = 3;

	public static int ToStored(ToastMode mode) => mode switch
	{
		ToastMode.Chat => StoredChat,
		ToastMode.Both => StoredBoth,
		_ => StoredPanel
	};

	/// <summary>The mode a player gets: their own stored choice, or the server default when they have none or may not choose.</summary>
	public static ToastMode Effective(long stored, ToastSettings settings) => !settings.PlayersCanChange ? settings.Default : stored switch
	{
		StoredChat => ToastMode.Chat,
		StoredBoth => ToastMode.Both,
		StoredPanel => ToastMode.Panel,
		_ => settings.Default
	};

	/// <summary>!toasts and the profile button step Panel, Chat, Both, then back to Panel.</summary>
	public static ToastMode Next(ToastMode mode) => mode switch
	{
		ToastMode.Panel => ToastMode.Chat,
		ToastMode.Chat => ToastMode.Both,
		_ => ToastMode.Panel
	};
}

[JsonConverter(typeof(ToastSettingsJsonConverter))]
public sealed class ToastSettings
{
	/// <summary>The mode every player starts with.</summary>
	[JsonPropertyName("default")]
	public ToastMode Default { get; set; } = ToastMode.Panel;

	/// <summary>False keeps everyone on the default: the profile button is hidden and !toasts changes nothing.</summary>
	[JsonPropertyName("players-can-change")]
	public bool PlayersCanChange { get; set; } = true;
}

/// <summary>Reads the toasts block, or the 7.3.0 form "toasts": true / false (false was chat for everyone, locked).</summary>
public sealed class ToastSettingsJsonConverter : JsonConverter<ToastSettings>
{
	public override ToastSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		switch (reader.TokenType)
		{
			case JsonTokenType.True:
				return new ToastSettings();
			case JsonTokenType.False:
				return new ToastSettings { Default = ToastMode.Chat, PlayersCanChange = false };
			case JsonTokenType.StartObject:
				ToastSettings settings = new();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					string name = reader.GetString() ?? string.Empty;
					reader.Read();
					if (name.Equals("default", StringComparison.OrdinalIgnoreCase))
						settings.Default = JsonSerializer.Deserialize<ToastMode>(ref reader, options);
					else if (name.Equals("players-can-change", StringComparison.OrdinalIgnoreCase))
						settings.PlayersCanChange = reader.GetBoolean();
					else
						throw new JsonException($"panorama.toasts has no setting '{name}'. Use \"default\" and \"players-can-change\".");
				}
				return settings;
			default:
				throw new JsonException("panorama.toasts must be true, false, or { \"default\": ..., \"players-can-change\": ... }.");
		}
	}

	public override void Write(Utf8JsonWriter writer, ToastSettings value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WritePropertyName("default");
		JsonSerializer.Serialize(writer, value.Default, options);
		writer.WriteBoolean("players-can-change", value.PlayersCanChange);
		writer.WriteEndObject();
	}
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
