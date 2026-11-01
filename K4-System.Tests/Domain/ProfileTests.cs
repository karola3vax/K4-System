namespace K4System.Tests.Domain;

using K4System.Hud;
using K4System.Missions;
using K4System.Players;
using K4System.Services;
using K4System.Storage;

public class ProfileTests
{
	[Fact]
	public void The_showcase_holds_three_and_a_second_click_takes_one_off()
	{
		PlayerProfile profile = new(null, null);

		Assert.Equal(PlayerProfile.ShowcaseChange.Added, profile.Toggle("a"));
		Assert.Equal(PlayerProfile.ShowcaseChange.Added, profile.Toggle("b"));
		Assert.Equal(PlayerProfile.ShowcaseChange.Added, profile.Toggle("c"));
		Assert.Equal(PlayerProfile.ShowcaseChange.Full, profile.Toggle("d"));
		Assert.Equal(PlayerProfile.ShowcaseChange.Removed, profile.Toggle("b"));
		Assert.Equal(PlayerProfile.ShowcaseChange.Added, profile.Toggle("d"));

		Assert.Equal(["a", "c", "d"], profile.Showcase);
		Assert.Equal("a,c,d", profile.TakeUnsavedShowcase());
		Assert.Null(profile.TakeUnsavedShowcase());
	}

	[Fact]
	public void A_stored_showcase_loads_without_duplicates_or_extras()
		=> Assert.Equal(["a", "b", "c"], new PlayerProfile(null, ["a", "a", "b", "c", "d"]).Showcase);

	[Fact]
	public void Weapon_kills_save_only_what_changed()
	{
		PlayerProfile profile = new(new Dictionary<string, int> { ["ak47"] = 10, ["awp"] = 3 }, null);
		Assert.Null(profile.TakeUnsavedKills());

		profile.AddKill("awp");
		profile.AddKill("awp");
		profile.AddKill("");

		Assert.Equal(new Dictionary<string, int> { ["awp"] = 5 }, profile.TakeUnsavedKills());
		Assert.Null(profile.TakeUnsavedKills());
	}

	[Fact]
	public void A_stat_reset_saves_every_weapon_as_zero()
	{
		PlayerProfile profile = new(new Dictionary<string, int> { ["ak47"] = 10, ["awp"] = 3 }, null);
		profile.ResetKills();

		Assert.Equal(new Dictionary<string, int> { ["ak47"] = 0, ["awp"] = 0 }, profile.TakeUnsavedKills());
		Assert.Null(PlayerProfile.Favorite(profile.WeaponKills));
	}

	[Fact]
	public void The_favorite_weapon_has_the_most_kills_and_ties_never_flicker()
	{
		Assert.Equal(("ak47", 12), PlayerProfile.Favorite(new Dictionary<string, int> { ["awp"] = 3, ["ak47"] = 12, ["knife"] = 1 }));
		Assert.Equal(("ak47", 5), PlayerProfile.Favorite(new Dictionary<string, int> { ["m4a1"] = 5, ["ak47"] = 5 }));
		Assert.Null(PlayerProfile.Favorite(new Dictionary<string, int>()));
	}

	[Theory]
	[InlineData("weapon_knife_karambit", "knife")]
	[InlineData("bayonet", "knife")]
	[InlineData("knife_t", "knife")]
	[InlineData("AK47", "ak47")]
	[InlineData(null, "")]
	public void Every_knife_counts_as_one_weapon(string? weapon, string expected)
		=> Assert.Equal(expected, StatService.ProfileWeapon(weapon));

	[Theory]
	[InlineData("m4a1", "M4A4")]
	[InlineData("m4a1_silencer", "M4A1-S")]
	[InlineData("inferno", "Molotov")]
	[InlineData("something_new", "something_new")]
	public void Weapons_show_their_buy_menu_names(string weapon, string expected)
		=> Assert.Equal(expected, WeaponCatalog.DisplayName(weapon));

	[Fact]
	public void A_newer_snapshot_keeps_older_unsaved_weapon_kills_and_showcase()
	{
		PlayerSnapshot older = new(1, "A", "R", 0, 10, null, null, null, WeaponKills: new Dictionary<string, int> { ["ak47"] = 4, ["awp"] = 1 }, Showcase: "x");
		PlayerSnapshot newer = new(1, "A", "R", 0, 12, null, null, null, WeaponKills: new Dictionary<string, int> { ["ak47"] = 5 }, Season: "2026-11-01");

		PlayerSnapshot merged = newer.MergeOnto(older);

		Assert.Equal(new Dictionary<string, int> { ["ak47"] = 5, ["awp"] = 1 }, merged.WeaponKills);
		Assert.Equal("x", merged.Showcase);
		Assert.Equal("2026-11-01", merged.Season);
		Assert.Equal(12, merged.Points);
	}

	[Fact]
	public void Profile_layout_sizes_match_the_server()
	{
		Assert.Equal(3, PlayerProfile.ShowcaseSize);
		Assert.Equal(6, PanoramaHud.ProfileTiles);
		Assert.Equal(3, PanoramaHud.ProfileSeasons);
	}
}
