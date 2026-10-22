namespace K4System.Missions;

using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

/// <summary>Reads the running map and server settings for MapAbilities. Game thread only.</summary>
internal static class MapReader
{
	public static MapFacts Read(bool freeForAllConfig, HashSet<string> seenWeapons)
	{
		int bombSites = 0, hostageSpots = 0, buyZones = 0;

		foreach (CEntityInstance entity in Utilities.GetAllEntities())
		{
			if (!entity.IsValid)
				continue;

			string name = entity.DesignerName;
			switch (name)
			{
				case "func_bomb_target":
					bombSites++;
					break;
				case "hostage_entity" or "info_hostage_spawn":
					hostageSpots++;
					break;
				case "func_buyzone":
					buyZones++;
					break;
				default:
					if (name.StartsWith("weapon_", StringComparison.Ordinal) && WeaponName(entity) is string weapon)
						seenWeapons.Add(weapon);
					break;
			}
		}

		return new MapFacts
		{
			MapName = Server.MapName ?? string.Empty,
			GameType = (int)(Number("game_type") ?? 0),
			GameMode = (int)(Number("game_mode") ?? 0),
			Respawns = Number("mp_respawn_on_death_t") is > 0 && Number("mp_respawn_on_death_ct") is > 0,
			FreeForAll = freeForAllConfig || Number("mp_teammates_are_enemies") is > 0,
			BombSites = bombSites,
			HostageSpots = hostageSpots,
			BuyZones = buyZones,
			BuyAnywhere = Number("mp_buy_anywhere") is > 0,
			BuyTime = (float)(Number("mp_buytime") ?? 0),
			BuyableGuns = (int)(Number("mp_buy_allow_guns") ?? 255),
			BuyableGrenades = Number("mp_buy_allow_grenades") is not 0,
			SeenWeapons = seenWeapons
		};
	}

	/// <summary>Adds the weapons every player carries right now.</summary>
	public static void AddCarriedWeapons(HashSet<string> seenWeapons)
	{
		foreach (CCSPlayerController controller in Utilities.GetPlayers())
		{
			if (controller is not { IsValid: true, PawnIsAlive: true } || controller.PlayerPawn.Value?.WeaponServices is not { } services)
				continue;

			foreach (var handle in services.MyWeapons)
			{
				if (handle.Value is CBasePlayerWeapon { IsValid: true } weapon && WeaponName(weapon) is string name)
					seenWeapons.Add(name);
			}
		}
	}

	private static string? WeaponName(CEntityInstance entity)
	{
		try
		{
			return WeaponCatalog.NameOf(new CBasePlayerWeapon(entity.Handle).AttributeManager.Item.ItemDefinitionIndex);
		}
		catch (Exception ex) when (ex is NativeException or NullReferenceException)
		{
			return null;
		}
	}

	/// <summary>A console variable as a number, whatever its type. Null when it does not exist.</summary>
	private static double? Number(string name)
	{
		ConVar? convar = ConVar.Find(name);
		if (convar is null)
			return null;

		return convar.Type switch
		{
			ConVarType.Bool => convar.GetPrimitiveValue<bool>() ? 1 : 0,
			ConVarType.Int16 => convar.GetPrimitiveValue<short>(),
			ConVarType.UInt16 => convar.GetPrimitiveValue<ushort>(),
			ConVarType.Int32 => convar.GetPrimitiveValue<int>(),
			ConVarType.UInt32 => convar.GetPrimitiveValue<uint>(),
			ConVarType.Int64 => convar.GetPrimitiveValue<long>(),
			ConVarType.UInt64 => convar.GetPrimitiveValue<ulong>(),
			ConVarType.Float32 => convar.GetPrimitiveValue<float>(),
			ConVarType.Float64 => convar.GetPrimitiveValue<double>(),
			ConVarType.String when double.TryParse(convar.StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) => value,
			_ => null
		};
	}
}
