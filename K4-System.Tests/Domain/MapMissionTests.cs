namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Missions;

/// <summary>Missions that follow the map, the weekly mission, and the new mission types.</summary>
public class MapMissionTests
{
	private static readonly DateOnly Day = new(2026, 10, 14); // a Wednesday
	private static MissionPool DefaultPool => MissionFile.Parse(MissionFile.DefaultContent);
	private static Dictionary<string, MapRule> DefaultRules => new PluginConfig().Missions.MapRules;

	private static MapFacts Competitive(string map = "de_dust2") => new()
	{
		MapName = map,
		GameType = 0,
		GameMode = 1,
		BombSites = 2,
		BuyZones = 2,
		BuyTime = 20,
		BuyableGuns = 255,
		BuyableGrenades = true
	};

	/// <summary>An aim map: no bomb sites, no buy zones, everyone spawns with an AK and a Desert Eagle.</summary>
	private static MapFacts AimMap() => new()
	{
		MapName = "aim_redline",
		GameType = 0,
		GameMode = 0,
		BuyTime = 45,
		BuyableGuns = 255,
		BuyableGrenades = true,
		SeenWeapons = new HashSet<string> { "ak47", "deagle", "knife" }
	};

	private static MissionDefinition Mission(MissionType type, string? weapon = null) => new() { Id = type.ToString(), Type = type, Weapon = weapon, Target = 1 };

	// ---------------------------------------------------------------- reading the map

	[Fact]
	public void A_bomb_map_with_buying_allows_everything_but_hostages()
	{
		MapAbilities map = MapAbilities.Read(Competitive(), DefaultRules);

		foreach (MissionType type in Enum.GetValues<MissionType>().Where(type => type != MissionType.HostageRescues))
			Assert.True(map.Allows(Mission(type, type == MissionType.WeaponKills ? "awp" : null)), $"{type} should be possible on de_dust2.");

		Assert.False(map.Allows(Mission(MissionType.HostageRescues)));
	}

	[Fact]
	public void An_aim_map_gives_no_grenade_bomb_or_missing_weapon_missions()
	{
		MapAbilities map = MapAbilities.Read(AimMap(), DefaultRules);

		Assert.False(map.Allows(Mission(MissionType.GrenadeKills)));
		Assert.False(map.Allows(Mission(MissionType.FlashAssists)));
		Assert.False(map.Allows(Mission(MissionType.SmokeKills)));
		Assert.False(map.Allows(Mission(MissionType.BlindKills)));
		Assert.False(map.Allows(Mission(MissionType.BombPlants)));
		Assert.False(map.Allows(Mission(MissionType.BombDefuses)));
		Assert.False(map.Allows(Mission(MissionType.WeaponKills, "m4a1")));
		Assert.False(map.Allows(Mission(MissionType.SniperKills)));
		Assert.False(map.Allows(Mission(MissionType.NoscopeKills)));
		Assert.False(map.Allows(Mission(MissionType.ZeusKills)));

		Assert.True(map.Allows(Mission(MissionType.WeaponKills, "ak47")));
		Assert.True(map.Allows(Mission(MissionType.RifleKills)));
		Assert.True(map.Allows(Mission(MissionType.PistolKills)));
		Assert.True(map.Allows(Mission(MissionType.Headshots)));
		Assert.True(map.Allows(Mission(MissionType.KnifeKills)));
		Assert.True(map.Allows(Mission(MissionType.RoundWins)));
		Assert.True(map.Allows(Mission(MissionType.ClutchWins)));
	}

	[Fact]
	public void The_aim_rule_wins_even_when_the_server_sells_grenades()
	{
		MapFacts facts = AimMap() with { BuyZones = 2 };

		Assert.False(MapAbilities.Read(facts, DefaultRules).Grenades);
		Assert.True(MapAbilities.Read(facts, new Dictionary<string, MapRule>()).Grenades);
	}

	[Fact]
	public void Deathmatch_and_respawn_servers_have_no_rounds_or_bomb()
	{
		MapAbilities deathmatch = MapAbilities.Read(Competitive("de_mirage") with { GameType = 1, GameMode = 2 }, DefaultRules);
		MapAbilities respawns = MapAbilities.Read(Competitive("de_mirage") with { Respawns = true }, DefaultRules);

		foreach (MapAbilities map in new[] { deathmatch, respawns })
		{
			Assert.False(map.Allows(Mission(MissionType.RoundWins)));
			Assert.False(map.Allows(Mission(MissionType.ClutchWins)));
			Assert.False(map.Allows(Mission(MissionType.OpeningKills)));
			Assert.False(map.Allows(Mission(MissionType.SurviveRounds)));
			Assert.False(map.Allows(Mission(MissionType.BombPlants)));
			Assert.True(map.Allows(Mission(MissionType.Kills)));
			Assert.True(map.Allows(Mission(MissionType.LifeKills)));
			Assert.True(map.Allows(Mission(MissionType.MatchWins)));
		}
	}

	[Fact]
	public void Retakes_plants_the_bomb_itself_so_only_defusing_counts()
	{
		MapAbilities map = MapAbilities.Read(Competitive() with { GameMode = 5 }, DefaultRules);

		Assert.False(map.Allows(Mission(MissionType.BombPlants)));
		Assert.True(map.Allows(Mission(MissionType.BombDefuses)));
	}

	[Fact]
	public void Free_for_all_has_no_clutches()
	{
		MapAbilities map = MapAbilities.Read(Competitive() with { FreeForAll = true }, DefaultRules);

		Assert.False(map.Allows(Mission(MissionType.ClutchWins)));
		Assert.True(map.Allows(Mission(MissionType.RoundWins)));
	}

	[Fact]
	public void Hostage_maps_allow_rescues()
		=> Assert.True(MapAbilities.Read(Competitive("cs_office") with { BombSites = 0, HostageSpots = 4 }, DefaultRules).Allows(Mission(MissionType.HostageRescues)));

	[Fact]
	public void Only_the_gun_groups_the_server_sells_count()
	{
		// mp_buy_allow_guns 1: pistols only.
		MapAbilities map = MapAbilities.Read(Competitive() with { BuyableGuns = 1 }, DefaultRules);

		Assert.True(map.Allows(Mission(MissionType.PistolKills)));
		Assert.True(map.Allows(Mission(MissionType.WeaponKills, "deagle")));
		Assert.False(map.Allows(Mission(MissionType.RifleKills)));
		Assert.False(map.Allows(Mission(MissionType.WeaponKills, "awp")));
	}

	[Fact]
	public void Weapons_given_by_a_retakes_plugin_are_seen_without_buying()
	{
		MapFacts facts = Competitive() with { BuyTime = 0, SeenWeapons = new HashSet<string> { "awp", "m4a1_silencer", "flashbang" } };
		MapAbilities map = MapAbilities.Read(facts, DefaultRules);

		Assert.True(map.Allows(Mission(MissionType.WeaponKills, "m4a1")));
		Assert.True(map.Allows(Mission(MissionType.NoscopeKills)));
		Assert.True(map.Allows(Mission(MissionType.FlashAssists)));
		Assert.False(map.Allows(Mission(MissionType.WeaponKills, "ak47")));
	}

	[Fact]
	public void Owner_rules_override_what_K4_read_and_later_rules_win()
	{
		Dictionary<string, MapRule> rules = new()
		{
			["*"] = new MapRule { Never = ["ZeusKills"] },
			["awp_*"] = new MapRule { Grenades = false, Weapons = ["awp"] },
			["awp_lego*"] = new MapRule { Weapons = ["all"], Rounds = false }
		};

		MapAbilities lego = MapAbilities.Read(Competitive("AWP_Lego_2"), rules);
		Assert.True(lego.Allows(Mission(MissionType.WeaponKills, "ak47")));
		Assert.False(lego.Allows(Mission(MissionType.GrenadeKills)));
		Assert.False(lego.Allows(Mission(MissionType.RoundWins)));
		Assert.False(lego.Allows(Mission(MissionType.ZeusKills)));

		MapAbilities awp = MapAbilities.Read(Competitive("awp_india"), rules);
		Assert.True(awp.Allows(Mission(MissionType.SniperKills)));
		Assert.False(awp.Allows(Mission(MissionType.RifleKills)));
	}

	[Theory]
	[InlineData("aim_*", "aim_redline", true)]
	[InlineData("aim_*", "AIM_REDLINE", true)]
	[InlineData("aim_*", "de_aim", false)]
	[InlineData("de_?ust2", "de_dust2", true)]
	[InlineData("de_dust2", "de_dust2_night", false)]
	[InlineData("de_(old)", "de_(old)", true)]
	public void Map_patterns_match_whole_names(string pattern, string map, bool expected)
		=> Assert.Equal(expected, MapAbilities.MapMatches(pattern, map));

	// ---------------------------------------------------------------- assignment and swaps

	[Fact]
	public void Players_on_an_aim_map_never_get_missions_it_cannot_offer()
	{
		MissionPool pool = DefaultPool;
		MapAbilities map = MapAbilities.Read(AimMap(), DefaultRules);

		for (ulong player = 0; player < 200; player++)
		{
			IReadOnlyList<MissionDefinition> missions = MissionRules.Assign(pool, 76561198000000000 + player, Day, map);
			Assert.All(missions, mission => Assert.True(map.Allows(mission), $"{mission.Id} was given on an aim map."));
			Assert.True(map.Allows(MissionRules.AssignWeekly(pool, 76561198000000000 + player, MissionRules.WeekStart(Day), map)!));
		}
	}

	[Fact]
	public void Without_a_map_the_assignment_is_the_same_as_before()
	{
		MissionPool pool = DefaultPool;
		Assert.Equal(MissionRules.Assign(pool, 7, Day).Select(mission => mission.Id), MissionRules.Assign(pool, 7, Day, null).Select(mission => mission.Id));
	}

	[Fact]
	public void A_mission_the_map_cannot_offer_is_swapped_for_one_it_can()
	{
		MissionPool pool = DefaultPool;
		MapAbilities map = MapAbilities.Read(AimMap(), DefaultRules);
		MissionDefinition grenade = pool.ById["grenade-kills-2"];

		MissionDefinition? replacement = MissionRules.Swap(pool, 7, Day, grenade, ["kills-15", "kills-40", "grenade-kills-2"], map);

		Assert.NotNull(replacement);
		Assert.Equal(MissionDifficulty.Hard, replacement.Difficulty);
		Assert.True(map.Allows(replacement));
		Assert.Equal(replacement.Id, MissionRules.Swap(pool, 7, Day, grenade, ["kills-15", "kills-40", "grenade-kills-2"], map)!.Id);
	}

	[Fact]
	public void Nothing_to_swap_to_keeps_the_mission()
	{
		MissionPool pool = MissionFile.Parse("""
			{
				"e": { "Type": "Kills", "Difficulty": "Easy", "Target": 1 },
				"m": { "Type": "Kills", "Difficulty": "Medium", "Target": 1 },
				"h": { "Type": "GrenadeKills", "Difficulty": "Hard", "Target": 1 },
				"h2": { "Type": "BombPlants", "Difficulty": "Hard", "Target": 1 }
			}
			""");
		MapAbilities map = MapAbilities.Read(AimMap(), DefaultRules);

		Assert.Null(MissionRules.Swap(pool, 7, Day, pool.ById["h"], ["e", "m", "h"], map));
		// When no mission of a difficulty fits the map, one is still given; it can be done on another map.
		Assert.Contains(MissionRules.Assign(pool, 1, Day, map)[2].Id, new[] { "h", "h2" });
	}

	// ---------------------------------------------------------------- the weekly mission

	[Theory]
	[InlineData(2026, 10, 12, 2026, 10, 12)]
	[InlineData(2026, 10, 14, 2026, 10, 12)]
	[InlineData(2026, 10, 18, 2026, 10, 12)]
	[InlineData(2026, 10, 19, 2026, 10, 19)]
	public void The_week_starts_on_Monday(int y, int m, int d, int wy, int wm, int wd)
		=> Assert.Equal(new DateOnly(wy, wm, wd), MissionRules.WeekStart(new DateOnly(y, m, d)));

	[Fact]
	public void The_weekly_mission_resets_at_Monday_midnight()
	{
		DateTime wednesdayNoonUtc = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
		Assert.Equal(TimeSpan.FromHours(4 * 24 + 12), MissionRules.UntilWeeklyReset(wednesdayNoonUtc, TimeZoneInfo.Utc));
		Assert.Equal("4d 12h", MissionRules.FormatDuration(MissionRules.UntilWeeklyReset(wednesdayNoonUtc, TimeZoneInfo.Utc)));
		Assert.Equal("5h 07m", MissionRules.FormatDuration(new TimeSpan(5, 7, 0)));
	}

	[Fact]
	public void Old_mission_files_without_weekly_missions_still_load()
	{
		MissionPool pool = MissionFile.Parse(File.ReadAllText(TestData.EvalPath("missions-6.1-default.json")));

		Assert.Empty(pool.Of(MissionDifficulty.Weekly));
		Assert.Null(MissionRules.AssignWeekly(pool, 7, MissionRules.WeekStart(Day)));
	}

	[Fact]
	public void The_weekly_mission_is_saved_in_its_own_slot_and_restored()
	{
		MissionPool pool = DefaultPool;
		MissionService service = new(new PluginConfig(), pool, TimeZoneInfo.Utc, null!, null!, null!, null!, null!, null!, null!, null!, null!);

		PlayerMissions fresh = service.Build(7, Day, new MissionRecord([], 0, 0, null));
		Assert.NotNull(fresh.Weekly);
		Assert.Equal(new DateOnly(2026, 10, 12), fresh.WeekStart);
		Assert.Equal(MissionDifficulty.Weekly, fresh.Weekly.Mission.Difficulty);
		Assert.Equal(4, fresh.All.Count());

		PlayerMissions saved = service.Build(7, Day, new MissionRecord([], 0, 0, null,
			new MissionSlotSnapshot(MissionRules.WeeklySlot, "weekly-kills-300", 120, false)));
		Assert.Equal("weekly-kills-300", saved.Weekly!.Mission.Id);
		Assert.Equal(120, saved.Weekly.Progress);

		MissionSnapshot snapshot = saved.Snapshot();
		Assert.Equal("2026-10-12", snapshot.Week);
		Assert.Equal(MissionRules.WeeklySlot, snapshot.Weekly!.Slot);
		Assert.Equal(120, snapshot.Weekly.Progress);
		Assert.Equal([0, 1, 2], snapshot.Slots.Select(slot => slot.Slot));
	}

	[Fact]
	public void The_weekly_mission_does_not_count_for_the_daily_streak()
	{
		MissionService service = new(new PluginConfig(), DefaultPool, TimeZoneInfo.Utc, null!, null!, null!, null!, null!, null!, null!, null!, null!);
		PlayerMissions missions = service.Build(7, Day, new MissionRecord([], 0, 0, null));

		foreach (MissionProgress slot in missions.Slots)
			slot.Completed = true;

		Assert.False(missions.Weekly!.Completed);
		Assert.True(missions.AllCompleted);
	}

	[Fact]
	public void Weekly_missions_pay_the_weekly_reward()
	{
		PluginConfig config = new();
		MissionService service = new(config, DefaultPool, TimeZoneInfo.Utc, null!, null!, null!, null!, null!, null!, null!, null!, null!);

		Assert.Equal(config.Missions.Rewards.Weekly, service.RewardOf(DefaultPool.ById["weekly-kills-300"]));
		Assert.Equal(config.Missions.Rewards.Hard, service.RewardOf(DefaultPool.ById["kills-80"]));
	}

	// ---------------------------------------------------------------- the missions file

	[Fact]
	public void An_unedited_old_default_file_is_recognized_and_an_edited_one_is_not()
	{
		string old = File.ReadAllText(TestData.EvalPath("missions-6.1-default.json"));

		Assert.True(MissionFile.IsOldDefault(old));
		Assert.True(MissionFile.IsOldDefault(old.Replace("\r\n", "\n").Replace("\n", "\r\n")));
		Assert.True(MissionFile.IsOldDefault("﻿" + old + "\n"));
		Assert.False(MissionFile.IsOldDefault(old.Replace("\"Target\": 15", "\"Target\": 20")));
		Assert.False(MissionFile.IsOldDefault(MissionFile.DefaultContent));
	}

	[Fact]
	public void The_new_default_list_has_every_new_mission_type()
	{
		MissionPool pool = DefaultPool;
		IEnumerable<MissionType> missing = Enum.GetValues<MissionType>()
			.Where(type => type != MissionType.HostageRescues && pool.Missions.All(mission => mission.Type != type));

		Assert.Empty(missing);
		Assert.True(pool.Of(MissionDifficulty.Weekly).Count >= 3);
		Assert.Contains("kills-15", pool.ById.Keys);
	}

	[Fact]
	public void Distance_is_only_for_long_range_missions()
	{
		Assert.Contains("Distance", Assert.Throws<InvalidDataException>(() => MissionFile.Parse("""
			{ "e": { "Type": "Kills", "Distance": 30, "Difficulty": "Easy", "Target": 1 }, "m": { "Type": "Kills", "Difficulty": "Medium", "Target": 1 }, "h": { "Type": "Kills", "Difficulty": "Hard", "Target": 1 } }
			""")).Message);

		MissionPool pool = MissionFile.Parse("""
			{ "e": { "Type": "LongRangeKills", "Difficulty": "Easy", "Target": 1 }, "m": { "Type": "LongRangeKills", "Distance": 50, "Difficulty": "Medium", "Target": 1 }, "h": { "Type": "Kills", "Difficulty": "Hard", "Target": 1 } }
			""");
		Assert.Equal(MissionDefinition.DefaultDistance, pool.ById["e"].MinimumDistance);
		Assert.Equal(50, pool.ById["m"].MinimumDistance);
	}

	[Fact]
	public void Kills_in_one_round_or_life_count_the_best_try()
	{
		Assert.True(Mission(MissionType.RoundKills).CountsBestTry);
		Assert.True(Mission(MissionType.LifeKills).CountsBestTry);
		Assert.False(Mission(MissionType.Kills).CountsBestTry);
	}

	[Fact]
	public void Weapon_groups_follow_the_buy_menu()
	{
		Assert.Contains("usp_silencer", WeaponCatalog.GroupOf(MissionType.PistolKills)!);
		Assert.Contains("mp5sd", WeaponCatalog.GroupOf(MissionType.SmgKills)!);
		Assert.Contains("m4a1_silencer", WeaponCatalog.GroupOf(MissionType.RifleKills)!);
		Assert.DoesNotContain("awp", WeaponCatalog.GroupOf(MissionType.RifleKills)!);
		Assert.Contains("ssg08", WeaponCatalog.GroupOf(MissionType.SniperKills)!);
		Assert.Equal("usp_silencer", WeaponCatalog.NameOf(61));
		Assert.Equal("m4a1_silencer", WeaponCatalog.NameOf(60));
		Assert.Null(WeaponCatalog.NameOf(49));
	}
}
