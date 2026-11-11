namespace K4System.Tests.Domain;

using System.Text.Json;
using K4System.Config;

/// <summary>panorama.toasts: the config forms, the stored values and the mode a player gets.</summary>
public class ToastModeTests
{
	private static ToastSettings Parse(string json)
		=> JsonSerializer.Deserialize<PanoramaSettings>($$"""{ "toasts": {{json}} }""", ConfigFile.Options)!.Toasts;

	[Fact]
	public void A_missing_setting_is_pop_ups_that_players_may_change()
	{
		PanoramaSettings settings = JsonSerializer.Deserialize<PanoramaSettings>("{}", ConfigFile.Options)!;
		Assert.Equal(ToastMode.Panel, settings.Toasts.Default);
		Assert.True(settings.Toasts.PlayersCanChange);
	}

	// 7.3.0 configs keep their meaning: false was chat for everyone, locked.
	[Theory]
	[InlineData("true", ToastMode.Panel, true)]
	[InlineData("false", ToastMode.Chat, false)]
	[InlineData("""{ "default": "both" }""", ToastMode.Both, true)]
	[InlineData("""{ "default": "Chat", "players-can-change": false }""", ToastMode.Chat, false)]
	[InlineData("""{ "players-can-change": false }""", ToastMode.Panel, false)]
	public void Config_forms(string json, ToastMode mode, bool playersCanChange)
	{
		ToastSettings settings = Parse(json);
		Assert.Equal(mode, settings.Default);
		Assert.Equal(playersCanChange, settings.PlayersCanChange);
	}

	[Theory]
	[InlineData("""{ "default": "loud" }""")]
	[InlineData("""{ "mode": "chat" }""")]
	[InlineData("\"chat\"")]
	public void A_wrong_setting_is_refused(string json)
		=> Assert.ThrowsAny<JsonException>(() => Parse(json));

	[Fact]
	public void The_default_config_file_parses_to_the_defaults()
	{
		PluginConfig config = JsonSerializer.Deserialize<PluginConfig>(ConfigFile.Template, ConfigFile.Options)!;
		Assert.Equal(ToastMode.Panel, config.Panorama.Toasts.Default);
		Assert.True(config.Panorama.Toasts.PlayersCanChange);
	}

	// Stored 0 is what 7.3 and older saved as "toasts off"; 1 is no choice (the old "on" and every new row).
	[Theory]
	[InlineData(0, ToastMode.Chat)]
	[InlineData(1, ToastMode.Both)]
	[InlineData(2, ToastMode.Both)]
	[InlineData(3, ToastMode.Panel)]
	public void A_player_keeps_their_own_choice_or_gets_the_server_default(long stored, ToastMode expected)
		=> Assert.Equal(expected, ToastModes.Effective(stored, new ToastSettings { Default = ToastMode.Both }));

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void A_locked_server_ignores_every_choice(long stored)
		=> Assert.Equal(ToastMode.Chat, ToastModes.Effective(stored, new ToastSettings { Default = ToastMode.Chat, PlayersCanChange = false }));

	[Theory]
	[InlineData(ToastMode.Panel)]
	[InlineData(ToastMode.Chat)]
	[InlineData(ToastMode.Both)]
	public void A_saved_choice_reads_back_as_the_same_mode(ToastMode mode)
		=> Assert.Equal(mode, ToastModes.Effective(ToastModes.ToStored(mode), new ToastSettings { Default = ToastMode.Chat }));

	[Fact]
	public void Toasts_steps_through_every_mode_and_back()
	{
		Assert.Equal(ToastMode.Chat, ToastModes.Next(ToastMode.Panel));
		Assert.Equal(ToastMode.Both, ToastModes.Next(ToastMode.Chat));
		Assert.Equal(ToastMode.Panel, ToastModes.Next(ToastMode.Both));
	}

	// An older K4 reads anything but 0 as "toasts on", so a downgrade keeps pop-ups for both and panel.
	[Fact]
	public void Older_versions_still_read_the_stored_values()
	{
		Assert.Equal(0, ToastModes.ToStored(ToastMode.Chat));
		Assert.NotEqual(0, ToastModes.ToStored(ToastMode.Panel));
		Assert.NotEqual(0, ToastModes.ToStored(ToastMode.Both));
	}
}
