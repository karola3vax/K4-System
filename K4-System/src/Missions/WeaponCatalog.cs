namespace K4System.Missions;

/// <summary>Weapon names as CS2 writes them in kill events (without "weapon_"), and the groups missions use.</summary>
internal static class WeaponCatalog
{
	// Groups in the order of mp_buy_allow_guns: pistols (1), SMGs (2), rifles (4), shotguns (8), sniper rifles (16), heavy MGs (32).
	public static readonly string[] Pistols = ["glock", "hkp2000", "usp_silencer", "p250", "fiveseven", "tec9", "cz75a", "deagle", "elite", "revolver"];
	public static readonly string[] Smgs = ["mac10", "mp9", "mp7", "mp5sd", "ump45", "p90", "bizon"];
	public static readonly string[] Rifles = ["ak47", "m4a1", "m4a1_silencer", "famas", "galilar", "aug", "sg556"];
	public static readonly string[] Shotguns = ["nova", "xm1014", "sawedoff", "mag7"];
	public static readonly string[] Snipers = ["awp", "ssg08", "scar20", "g3sg1"];
	public static readonly string[] Heavy = ["m249", "negev"];

	public static readonly string[][] BuyGroups = [Pistols, Smgs, Rifles, Shotguns, Snipers, Heavy];

	public static readonly string[] Grenades = ["hegrenade", "flashbang", "smokegrenade", "molotov", "incgrenade", "decoy"];

	public const string Zeus = "taser";

	// Item definition indexes from CS2's scripts/items/items_game.txt (read 2026-10-14). Held weapons are named
	// by this index, because it tells variants such as the USP-S and the P2000 apart.
	private static readonly Dictionary<int, string> ByItemIndex = new()
	{
		[1] = "deagle", [2] = "elite", [3] = "fiveseven", [4] = "glock", [7] = "ak47", [8] = "aug", [9] = "awp", [10] = "famas",
		[11] = "g3sg1", [13] = "galilar", [14] = "m249", [16] = "m4a1", [17] = "mac10", [19] = "p90", [23] = "mp5sd", [24] = "ump45",
		[25] = "xm1014", [26] = "bizon", [27] = "mag7", [28] = "negev", [29] = "sawedoff", [30] = "tec9", [31] = "taser", [32] = "hkp2000",
		[33] = "mp7", [34] = "mp9", [35] = "nova", [36] = "p250", [38] = "scar20", [39] = "sg556", [40] = "ssg08", [43] = "flashbang",
		[44] = "hegrenade", [45] = "smokegrenade", [46] = "molotov", [47] = "decoy", [48] = "incgrenade", [60] = "m4a1_silencer",
		[61] = "usp_silencer", [63] = "cz75a", [64] = "revolver"
	};

	public static string? NameOf(int itemIndex) => ByItemIndex.GetValueOrDefault(itemIndex);

	// The names CS2's buy menu shows, for the profile's favorite weapon.
	private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.Ordinal)
	{
		["deagle"] = "Desert Eagle", ["elite"] = "Dual Berettas", ["fiveseven"] = "Five-SeveN", ["glock"] = "Glock-18", ["ak47"] = "AK-47",
		["aug"] = "AUG", ["awp"] = "AWP", ["famas"] = "FAMAS", ["g3sg1"] = "G3SG1", ["galilar"] = "Galil AR", ["m249"] = "M249",
		["m4a1"] = "M4A4", ["m4a1_silencer"] = "M4A1-S", ["mac10"] = "MAC-10", ["p90"] = "P90", ["mp5sd"] = "MP5-SD", ["ump45"] = "UMP-45",
		["xm1014"] = "XM1014", ["bizon"] = "PP-Bizon", ["mag7"] = "MAG-7", ["negev"] = "Negev", ["sawedoff"] = "Sawed-Off",
		["tec9"] = "Tec-9", ["taser"] = "Zeus x27", ["hkp2000"] = "P2000", ["mp7"] = "MP7", ["mp9"] = "MP9", ["nova"] = "Nova",
		["p250"] = "P250", ["scar20"] = "SCAR-20", ["sg556"] = "SG 553", ["ssg08"] = "SSG 08", ["usp_silencer"] = "USP-S",
		["cz75a"] = "CZ75-Auto", ["revolver"] = "R8 Revolver", ["knife"] = "Knife", ["hegrenade"] = "HE Grenade",
		["inferno"] = "Molotov", ["molotov"] = "Molotov", ["incgrenade"] = "Incendiary", ["flashbang"] = "Flashbang",
		["smokegrenade"] = "Smoke", ["decoy"] = "Decoy", ["planted_c4"] = "C4"
	};

	/// <summary>"ak47" as "AK-47"; an unknown name is shown as it is.</summary>
	public static string DisplayName(string name) => DisplayNames.GetValueOrDefault(name, name);

	/// <summary>"weapon_ak47" or "AK47" as the kill-event name "ak47".</summary>
	public static string Normalize(string? weapon)
		=> (weapon ?? string.Empty).Trim().ToLowerInvariant().Replace("weapon_", "", StringComparison.Ordinal);

	public static bool IsKnife(string name) => name.Contains("knife", StringComparison.Ordinal) || name.Contains("bayonet", StringComparison.Ordinal);

	/// <summary>Kill-event names of grenade kills: the HE grenade, and fire from a molotov or incendiary.</summary>
	public static bool IsGrenadeKill(string name) => name is "hegrenade" or "inferno";

	/// <summary>The weapon group a group mission counts, or null for other mission types.</summary>
	public static string[]? GroupOf(MissionType type) => type switch
	{
		MissionType.PistolKills => Pistols,
		MissionType.SmgKills => Smgs,
		MissionType.ShotgunKills => Shotguns,
		MissionType.RifleKills => Rifles,
		MissionType.SniperKills => Snipers,
		_ => null
	};
}
