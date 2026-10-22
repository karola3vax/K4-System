namespace K4System.Config;

/// <summary>How many points each event gives or takes. Names are the keys used in "points.overrides".</summary>
public sealed class PointValues
{
	private readonly Dictionary<string, int> values;

	private PointValues(Dictionary<string, int> values) => this.values = values;

	/// <summary>Every point name, in the order used by the docs and the config template.</summary>
	public static readonly IReadOnlyList<string> Names =
	[
		"kill", "death", "headshot", "assist", "flash-assist", "team-kill", "team-assist", "team-flash-assist", "suicide",
		"noscope", "through-smoke", "blind-kill", "wallbang", "long-distance-kill", "long-distance-meters",
		"grenade-kill", "fire-kill", "impact-kill", "knife-kill", "taser-kill",
		"double-kill", "triple-kill", "domination", "rampage", "mega-kill", "ownage", "ultra-kill", "killing-spree", "monster-kill", "unstoppable", "godlike",
		"round-win", "round-loss", "mvp",
		"bomb-plant", "bomb-defuse", "bomb-defuse-team", "bomb-explode", "bomb-pickup", "bomb-drop",
		"hostage-rescue", "hostage-rescue-all", "hostage-hurt", "hostage-kill",
		"playtime"
	];

	public static PointValues From(IReadOnlyDictionary<string, int> source)
	{
		Dictionary<string, int> copy = new(StringComparer.Ordinal);
		foreach (string name in Names)
			copy[name] = source.TryGetValue(name, out int value) ? value : 0;
		return new PointValues(copy);
	}

	public int this[string name] => values[name];

	public IReadOnlyDictionary<string, int> All => values;

	public PointValues With(IReadOnlyDictionary<string, int> overrides)
	{
		Dictionary<string, int> copy = new(values, StringComparer.Ordinal);
		foreach ((string name, int value) in overrides)
			copy[name] = value;
		return new PointValues(copy);
	}

	public int Kill => values["kill"];
	public int Death => values["death"];
	public int Headshot => values["headshot"];
	public int Assist => values["assist"];
	public int AssistFlash => values["flash-assist"];
	public int TeamKill => values["team-kill"];
	public int TeamAssist => values["team-assist"];
	public int TeamAssistFlash => values["team-flash-assist"];
	public int Suicide => values["suicide"];
	public int NoScope => values["noscope"];
	public int Thrusmoke => values["through-smoke"];
	public int BlindKill => values["blind-kill"];
	public int Penetrated => values["wallbang"];
	public int LongDistanceKill => values["long-distance-kill"];
	public int LongDistance => values["long-distance-meters"];
	public int GrenadeKill => values["grenade-kill"];
	public int InfernoKill => values["fire-kill"];
	public int ImpactKill => values["impact-kill"];
	public int KnifeKill => values["knife-kill"];
	public int TaserKill => values["taser-kill"];
	public int DoubleKill => values["double-kill"];
	public int TripleKill => values["triple-kill"];
	public int Domination => values["domination"];
	public int Rampage => values["rampage"];
	public int MegaKill => values["mega-kill"];
	public int Ownage => values["ownage"];
	public int UltraKill => values["ultra-kill"];
	public int KillingSpree => values["killing-spree"];
	public int MonsterKill => values["monster-kill"];
	public int Unstoppable => values["unstoppable"];
	public int GodLike => values["godlike"];
	public int RoundWin => values["round-win"];
	public int RoundLose => values["round-loss"];
	public int MVP => values["mvp"];
	public int BombPlant => values["bomb-plant"];
	public int BombDefused => values["bomb-defuse"];
	public int BombDefusedOthers => values["bomb-defuse-team"];
	public int BombExploded => values["bomb-explode"];
	public int BombPickup => values["bomb-pickup"];
	public int BombDrop => values["bomb-drop"];
	public int HostageRescue => values["hostage-rescue"];
	public int HostageRescueAll => values["hostage-rescue-all"];
	public int HostageHurt => values["hostage-hurt"];
	public int HostageKill => values["hostage-kill"];
	public int PlaytimePoints => values["playtime"];
}

/// <summary>Ready-made point economies. Pick one with "points.preset", then change single values with overrides.</summary>
public static class PointPresets
{
	public const string Casual = "casual";
	public const string Competitive = "competitive";
	public const string Deathmatch = "deathmatch";

	public static IReadOnlyList<string> Names { get; } = [Casual, Competitive, Deathmatch];

	public static bool Exists(string name) => Names.Contains(name.Trim().ToLowerInvariant());

	public static PointValues Get(string name) => PointValues.From(Table(name.Trim().ToLowerInvariant()));

	// Casual is the 5.x default economy: generous bonuses and steady progress for public servers.
	// Competitive rewards winning and clean play, with small flashy-kill bonuses and no playtime points.
	// Deathmatch has no rounds or objectives, so only kills and short streaks count, in small steps.
	private static Dictionary<string, int> Table(string name) => name switch
	{
		Casual => new()
		{
			["kill"] = 20, ["death"] = -15, ["headshot"] = 5, ["assist"] = 10, ["flash-assist"] = 15,
			["team-kill"] = -40, ["team-assist"] = -10, ["team-flash-assist"] = -15, ["suicide"] = -20,
			["noscope"] = 60, ["through-smoke"] = 40, ["blind-kill"] = 60, ["wallbang"] = 5,
			["long-distance-kill"] = 60, ["long-distance-meters"] = 30,
			["grenade-kill"] = 60, ["fire-kill"] = 50, ["impact-kill"] = 60, ["knife-kill"] = 60, ["taser-kill"] = 60,
			["double-kill"] = 10, ["triple-kill"] = 15, ["domination"] = 25, ["rampage"] = 30, ["mega-kill"] = 40,
			["ownage"] = 50, ["ultra-kill"] = 55, ["killing-spree"] = 65, ["monster-kill"] = 70, ["unstoppable"] = 80, ["godlike"] = 90,
			["round-win"] = 5, ["round-loss"] = 0, ["mvp"] = 25,
			["bomb-plant"] = 30, ["bomb-defuse"] = 40, ["bomb-defuse-team"] = 10, ["bomb-explode"] = 25, ["bomb-pickup"] = 5, ["bomb-drop"] = -10,
			["hostage-rescue"] = 30, ["hostage-rescue-all"] = 25, ["hostage-hurt"] = -10, ["hostage-kill"] = -40,
			["playtime"] = 15
		},
		Competitive => new()
		{
			["kill"] = 10, ["death"] = -10, ["headshot"] = 3, ["assist"] = 4, ["flash-assist"] = 3,
			["team-kill"] = -30, ["team-assist"] = -5, ["team-flash-assist"] = -5, ["suicide"] = -15,
			["noscope"] = 10, ["through-smoke"] = 5, ["blind-kill"] = 5, ["wallbang"] = 2,
			["long-distance-kill"] = 5, ["long-distance-meters"] = 40,
			["grenade-kill"] = 10, ["fire-kill"] = 10, ["impact-kill"] = 20, ["knife-kill"] = 15, ["taser-kill"] = 15,
			["double-kill"] = 2, ["triple-kill"] = 4, ["domination"] = 6, ["rampage"] = 8, ["mega-kill"] = 10,
			["ownage"] = 12, ["ultra-kill"] = 12, ["killing-spree"] = 12, ["monster-kill"] = 12, ["unstoppable"] = 12, ["godlike"] = 12,
			["round-win"] = 10, ["round-loss"] = -5, ["mvp"] = 10,
			["bomb-plant"] = 10, ["bomb-defuse"] = 15, ["bomb-defuse-team"] = 5, ["bomb-explode"] = 5, ["bomb-pickup"] = 0, ["bomb-drop"] = 0,
			["hostage-rescue"] = 10, ["hostage-rescue-all"] = 10, ["hostage-hurt"] = -5, ["hostage-kill"] = -20,
			["playtime"] = 0
		},
		Deathmatch => new()
		{
			["kill"] = 5, ["death"] = -3, ["headshot"] = 2, ["assist"] = 1, ["flash-assist"] = 1,
			["team-kill"] = -10, ["team-assist"] = 0, ["team-flash-assist"] = 0, ["suicide"] = -5,
			["noscope"] = 3, ["through-smoke"] = 2, ["blind-kill"] = 2, ["wallbang"] = 1,
			["long-distance-kill"] = 2, ["long-distance-meters"] = 40,
			["grenade-kill"] = 3, ["fire-kill"] = 3, ["impact-kill"] = 5, ["knife-kill"] = 5, ["taser-kill"] = 5,
			["double-kill"] = 1, ["triple-kill"] = 2, ["domination"] = 3, ["rampage"] = 4, ["mega-kill"] = 5,
			["ownage"] = 6, ["ultra-kill"] = 6, ["killing-spree"] = 6, ["monster-kill"] = 6, ["unstoppable"] = 6, ["godlike"] = 6,
			["round-win"] = 0, ["round-loss"] = 0, ["mvp"] = 0,
			["bomb-plant"] = 0, ["bomb-defuse"] = 0, ["bomb-defuse-team"] = 0, ["bomb-explode"] = 0, ["bomb-pickup"] = 0, ["bomb-drop"] = 0,
			["hostage-rescue"] = 0, ["hostage-rescue-all"] = 0, ["hostage-hurt"] = 0, ["hostage-kill"] = 0,
			["playtime"] = 0
		},
		_ => throw new InvalidDataException($"Unknown point preset '{name}'. Use {string.Join(", ", Names)}.")
	};
}
