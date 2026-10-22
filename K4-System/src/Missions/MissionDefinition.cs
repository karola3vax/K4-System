namespace K4System.Missions;

using System.Text.Json.Serialization;

public enum MissionDifficulty
{
	Easy,
	Medium,
	Hard,
	Weekly
}

/// <summary>What a mission counts. The JSON names are used in missions.jsonc.</summary>
public enum MissionType
{
	Kills,
	Headshots,
	KnifeKills,
	GrenadeKills,
	NoscopeKills,
	WeaponKills,
	Assists,
	FlashAssists,
	Mvps,
	RoundWins,
	BombPlants,
	BombDefuses,
	HostageRescues,
	PlaytimeMinutes,
	MatchesPlayed,
	MatchWins,
	WallbangKills,
	SmokeKills,
	JumpKills,
	BlindKills,
	LongRangeKills,
	ZeusKills,
	OpeningKills,
	ClutchWins,
	RoundKills,
	SurviveRounds,
	PistolKills,
	SmgKills,
	ShotgunKills,
	RifleKills,
	SniperKills,
	Damage,
	LifeKills,
	RevengeKills
}

/// <summary>One mission from missions.jsonc. The JSON key is its id, which is stored in the database.</summary>
public sealed class MissionDefinition
{
	public const int DefaultDistance = 30;

	[JsonIgnore]
	public string Id { get; set; } = string.Empty;

	public MissionType Type { get; set; }

	public MissionDifficulty Difficulty { get; set; }

	public int Target { get; set; }

	/// <summary>WeaponKills only: part of the weapon name, e.g. "ak47", "awp", "m4a1".</summary>
	public string? Weapon { get; set; }

	/// <summary>LongRangeKills only: the shortest distance that counts, in meters.</summary>
	public int? Distance { get; set; }

	/// <summary>Optional: replaces the reward set for this difficulty in k4-system.jsonc.</summary>
	public int? Reward { get; set; }

	/// <summary>
	/// RoundKills and LifeKills ask for a number of kills at once, so progress is the best try of the day
	/// instead of a running total.
	/// </summary>
	[JsonIgnore]
	public bool CountsBestTry => Type is MissionType.RoundKills or MissionType.LifeKills;

	[JsonIgnore]
	public int MinimumDistance => Distance ?? DefaultDistance;

	/// <summary>Language key that describes this mission type.</summary>
	[JsonIgnore]
	public string DescriptionKey => Type switch
	{
		MissionType.Kills => "mission.kills",
		MissionType.Headshots => "mission.headshots",
		MissionType.KnifeKills => "mission.knife-kills",
		MissionType.GrenadeKills => "mission.grenade-kills",
		MissionType.NoscopeKills => "mission.noscope-kills",
		MissionType.WeaponKills => "mission.weapon-kills",
		MissionType.Assists => "mission.assists",
		MissionType.FlashAssists => "mission.flash-assists",
		MissionType.Mvps => "mission.mvps",
		MissionType.RoundWins => "mission.round-wins",
		MissionType.BombPlants => "mission.bomb-plants",
		MissionType.BombDefuses => "mission.bomb-defuses",
		MissionType.HostageRescues => "mission.hostage-rescues",
		MissionType.PlaytimeMinutes => "mission.playtime",
		MissionType.MatchesPlayed => "mission.matches-played",
		MissionType.MatchWins => "mission.match-wins",
		MissionType.WallbangKills => "mission.wallbang-kills",
		MissionType.SmokeKills => "mission.smoke-kills",
		MissionType.JumpKills => "mission.jump-kills",
		MissionType.BlindKills => "mission.blind-kills",
		MissionType.LongRangeKills => "mission.long-range-kills",
		MissionType.ZeusKills => "mission.zeus-kills",
		MissionType.OpeningKills => "mission.opening-kills",
		MissionType.ClutchWins => "mission.clutch-wins",
		MissionType.RoundKills => "mission.round-kills",
		MissionType.SurviveRounds => "mission.survive-rounds",
		MissionType.PistolKills => "mission.pistol-kills",
		MissionType.SmgKills => "mission.smg-kills",
		MissionType.ShotgunKills => "mission.shotgun-kills",
		MissionType.RifleKills => "mission.rifle-kills",
		MissionType.SniperKills => "mission.sniper-kills",
		MissionType.Damage => "mission.damage",
		MissionType.LifeKills => "mission.life-kills",
		MissionType.RevengeKills => "mission.revenge-kills",
		_ => "mission.kills"
	};
}
