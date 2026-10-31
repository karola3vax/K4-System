namespace K4System.Missions;

using System.Text.RegularExpressions;
using K4System.Config;

/// <summary>What K4 read from the running map and server. Gathered on the game thread by MapReader.</summary>
public sealed record MapFacts
{
	public string MapName { get; init; } = string.Empty;

	/// <summary>game_type and game_mode, numbered as in CS2's gamemodes.txt.</summary>
	public int GameType { get; init; }

	public int GameMode { get; init; }

	/// <summary>mp_respawn_on_death_t and _ct are both on: deathmatch-style respawns.</summary>
	public bool Respawns { get; init; }

	/// <summary>mp_teammates_are_enemies, or tracking.free-for-all in k4-system.jsonc.</summary>
	public bool FreeForAll { get; init; }

	public int BombSites { get; init; }

	public int HostageSpots { get; init; }

	public int BuyZones { get; init; }

	/// <summary>mp_buy_anywhere is on for at least one team.</summary>
	public bool BuyAnywhere { get; init; }

	public float BuyTime { get; init; }

	/// <summary>mp_buy_allow_guns: pistols (1), SMGs (2), rifles (4), shotguns (8), sniper rifles (16), heavy MGs (32).</summary>
	public int BuyableGuns { get; init; }

	/// <summary>mp_buy_allow_grenades.</summary>
	public bool BuyableGrenades { get; init; }

	/// <summary>Weapons seen on this map: lying on the ground, carried by players, or used for a kill.</summary>
	public IReadOnlySet<string> SeenWeapons { get; init; } = new HashSet<string>();
}

/// <summary>
/// What players can do on the current map. A mission that needs something the map does not have, such as
/// a grenade mission on an aim map, is never given there.
/// </summary>
public sealed record MapAbilities(bool Rounds, bool Teams, bool Bomb, bool Hostages, bool Grenades, IReadOnlySet<string> Weapons, IReadOnlySet<MissionType> Never)
{
	// gamemodes.txt: game_type 0 is classic (mode 5 is Retakes); game_type 1 holds arms race (mode 0) and deathmatch (mode 2).
	private const int ClassicType = 0;
	private const int RetakesMode = 5;
	private const int GunGameType = 1;
	private const int ArmsRaceMode = 0;
	private const int DeathmatchMode = 2;

	public bool Allows(MissionDefinition mission)
	{
		if (Never.Contains(mission.Type))
			return false;

		return mission.Type switch
		{
			MissionType.GrenadeKills or MissionType.FlashAssists or MissionType.SmokeKills or MissionType.BlindKills => Grenades,
			MissionType.NoscopeKills => HasAny(WeaponCatalog.Snipers),
			MissionType.WeaponKills => mission.Weapon is string part && AllowsWeapon(part),
			MissionType.ZeusKills => Weapons.Contains(WeaponCatalog.Zeus),
			MissionType.PistolKills or MissionType.SmgKills or MissionType.ShotgunKills or MissionType.RifleKills or MissionType.SniperKills
				=> HasAny(WeaponCatalog.GroupOf(mission.Type)!),
			MissionType.RoundWins or MissionType.Mvps or MissionType.OpeningKills or MissionType.RoundKills or MissionType.SurviveRounds => Rounds,
			MissionType.ClutchWins => Rounds && Teams,
			MissionType.BombPlants or MissionType.BombDefuses => Bomb,
			MissionType.HostageRescues => Hostages,
			_ => true
		};
	}

	private bool HasAny(string[] group) => group.Any(Weapons.Contains);

	// Everyone carries a knife, and grenades follow the grenade rule: Weapons never lists either, so a knife or
	// grenade WeaponKills mission was swapped away on every map.
	private bool AllowsWeapon(string part)
	{
		if (WeaponCatalog.IsKnife(part))
			return true;
		if (part is "inferno" or "planted_c4" || WeaponCatalog.Grenades.Any(grenade => grenade.Contains(part, StringComparison.Ordinal)))
			return part == "planted_c4" ? Bomb : Grenades;
		return Weapons.Any(weapon => weapon.Contains(part, StringComparison.Ordinal));
	}

	/// <summary>Works out the abilities from what the server reports, then applies the owner's map rules in file order.</summary>
	public static MapAbilities Read(MapFacts facts, IReadOnlyDictionary<string, MapRule>? rules)
	{
		MapRule rule = Merge(facts.MapName, rules);

		bool gunGame = facts.GameType == GunGameType && facts.GameMode is ArmsRaceMode or DeathmatchMode;
		bool rounds = rule.Rounds ?? (!gunGame && !facts.Respawns);
		bool bomb = rule.Bomb ?? (rounds && facts.BombSites > 0);
		bool hostages = rule.Hostages ?? (rounds && facts.HostageSpots > 0);
		bool buying = (facts.BuyZones > 0 || facts.BuyAnywhere) && facts.BuyTime > 0;

		HashSet<string> weapons;
		if (rule.Weapons is { } listed)
		{
			weapons = listed.Any(name => name.Equals("all", StringComparison.OrdinalIgnoreCase))
				? WeaponCatalog.BuyGroups.SelectMany(group => group).Append(WeaponCatalog.Zeus).ToHashSet(StringComparer.Ordinal)
				: listed.Select(WeaponCatalog.Normalize).Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);
		}
		else
		{
			weapons = new HashSet<string>(facts.SeenWeapons.Where(name => !WeaponCatalog.Grenades.Contains(name)), StringComparer.Ordinal);

			// Arms race hands out every gun class over a match.
			if (facts.GameType == GunGameType && facts.GameMode == ArmsRaceMode)
				weapons.UnionWith(WeaponCatalog.BuyGroups.SelectMany(group => group));

			if (buying)
			{
				for (int bit = 0; bit < WeaponCatalog.BuyGroups.Length; bit++)
				{
					if ((facts.BuyableGuns & (1 << bit)) != 0)
						weapons.UnionWith(WeaponCatalog.BuyGroups[bit]);
				}

				weapons.Add(WeaponCatalog.Zeus);
			}
		}

		bool grenades = rule.Grenades ?? ((buying && facts.BuyableGrenades) || facts.SeenWeapons.Any(WeaponCatalog.Grenades.Contains));

		HashSet<MissionType> never = [];
		// Valve's Retakes mode plants the bomb by itself.
		if (facts.GameType == ClassicType && facts.GameMode == RetakesMode)
			never.Add(MissionType.BombPlants);
		foreach (string name in rule.Never ?? [])
		{
			if (Enum.TryParse(name, ignoreCase: true, out MissionType type))
				never.Add(type);
		}

		return new MapAbilities(rounds, !facts.FreeForAll, bomb, hostages, grenades, weapons, never);
	}

	/// <summary>Every rule whose pattern matches the map, later rules overriding earlier ones value by value.</summary>
	internal static MapRule Merge(string mapName, IReadOnlyDictionary<string, MapRule>? rules)
	{
		MapRule merged = new();
		foreach ((string pattern, MapRule rule) in rules ?? new Dictionary<string, MapRule>())
		{
			if (rule is null || !MapMatches(pattern, mapName))
				continue;

			merged.Rounds = rule.Rounds ?? merged.Rounds;
			merged.Bomb = rule.Bomb ?? merged.Bomb;
			merged.Hostages = rule.Hostages ?? merged.Hostages;
			merged.Grenades = rule.Grenades ?? merged.Grenades;
			merged.Weapons = rule.Weapons ?? merged.Weapons;
			if (rule.Never is not null)
				merged.Never = [.. merged.Never ?? [], .. rule.Never];
		}

		return merged;
	}

	/// <summary>Map name patterns: * matches any text, ? one character. Case does not matter.</summary>
	internal static bool MapMatches(string pattern, string mapName)
	{
		string regex = "^" + Regex.Escape(pattern.Trim()).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";
		return Regex.IsMatch(mapName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
	}
}
