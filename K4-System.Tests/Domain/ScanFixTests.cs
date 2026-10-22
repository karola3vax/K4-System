namespace K4System.Tests.Domain;

using K4System.Domain;
using K4System.Missions;
using K4System.Services;
using Microsoft.Extensions.Localization;

/// <summary>Regression tests for the October 2026 code scan.</summary>
public class ScanFixTests
{
	/// <summary>Formats like CounterStrikeSharp's localizer: the overload with arguments always runs string.Format.</summary>
	private sealed class FormattingLocalizer(Dictionary<string, string> texts) : IStringLocalizer
	{
		public LocalizedString this[string name] => new(name, texts[name]);

		public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(texts[name], arguments));

		public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => texts.Select(pair => new LocalizedString(pair.Key, pair.Value));
	}

	[Fact]
	public void Texts_with_named_tokens_are_returned_unformatted_when_there_are_no_arguments()
	{
		Chat chat = new(new FormattingLocalizer(new()
		{
			["announce.disconnect"] = "{name} left ({rank}, {points} points)",
			["points.gain"] = "{0} points (+{1})",
			["prefix"] = "[K4]"
		}));

		Assert.Equal("{name} left ({rank}, {points} points)", chat["announce.disconnect", []]);
		Assert.Equal("{name} left ({rank}, {points} points)", chat["announce.disconnect"]);
		Assert.Equal("10 points (+2)", chat["points.gain", 10, 2]);
		Assert.Equal(" [K4] {name} left ({rank}, {points} points)", chat.Prefixed("announce.disconnect"));
	}

	[Theory]
	[InlineData("+15:00")]
	[InlineData("-23")]
	public void Timezone_offsets_beyond_14_hours_fall_back_to_utc(string offset)
	{
		(TimeZoneInfo zone, string? warning) = MissionRules.ResolveTimeZone(offset);
		Assert.Equal(TimeZoneInfo.Utc, zone);
		Assert.NotNull(warning);
	}

	[Fact]
	public void Timezone_offsets_up_to_14_hours_still_work()
	{
		(TimeZoneInfo zone, string? warning) = MissionRules.ResolveTimeZone("+14:00");
		Assert.Equal(TimeSpan.FromHours(14), zone.BaseUtcOffset);
		Assert.Null(warning);
	}

	[Fact]
	public void Weapon_missions_with_unknown_weapons_are_reported()
	{
		MissionPool pool = MissionFile.Parse("""
			{
			  "a": { "Type": "WeaponKills", "Weapon": "m4a4", "Difficulty": "Easy", "Target": 5 },
			  "b": { "Type": "WeaponKills", "Weapon": "m4a1", "Difficulty": "Easy", "Target": 5 },
			  "c": { "Type": "WeaponKills", "Weapon": "weapon_usp_silencer", "Difficulty": "Medium", "Target": 5 },
			  "d": { "Type": "Kills", "Difficulty": "Hard", "Target": 5 },
			  "e": { "Type": "WeaponKills", "Weapon": "knife_karambit", "Difficulty": "Hard", "Target": 5 },
			  "f": { "Type": "WeaponKills", "Weapon": "c4", "Difficulty": "Hard", "Target": 1 }
			}
			""");

		string warning = Assert.Single(MissionFile.WeaponWarnings(pool));
		Assert.Contains("'a'", warning);
		Assert.Empty(MissionFile.WeaponWarnings(MissionFile.Parse(MissionFile.DefaultContent)));
	}

	[Theory]
	[InlineData("lightred", true)]
	[InlineData("Gold", true)]
	[InlineData("default", true)]
	[InlineData("lightgreen", false)]
	[InlineData("gray", false)]
	public void Chat_color_names_are_checked(string name, bool known)
		=> Assert.Equal(known, ChatColorText.IsKnown(name));
}
