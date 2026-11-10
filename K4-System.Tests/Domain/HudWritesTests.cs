namespace K4System.Tests.Domain;

using K4System.Hud;

/// <summary>The panel sends only what changed; a skipped write that was needed would leave a stale panel.</summary>
public class HudWritesTests
{
	[Fact]
	public void Only_changed_texts_and_classes_are_sent()
	{
		HudWrites sent = new();

		Assert.True(sent.Var(1, "k4-me", "name", "Alice"));
		Assert.False(sent.Var(1, "k4-me", "name", "Alice"));
		Assert.True(sent.Var(1, "k4-me", "name", "Bob"));

		Assert.True(sent.Class(1, "k4-panel", "k4-closed", false));
		Assert.False(sent.Class(1, "k4-panel", "k4-closed", false));
		Assert.True(sent.Class(1, "k4-panel", "k4-closed", true));

		Assert.Equal(4, sent.Sent);
		Assert.Equal(2, sent.Skipped);
	}

	[Fact]
	public void Slots_panels_and_names_are_separate()
	{
		HudWrites sent = new();
		sent.Var(1, "k4-me", "name", "Alice");

		Assert.True(sent.Var(2, "k4-me", "name", "Alice"));
		Assert.True(sent.Var(1, "k4-other", "name", "Alice"));
		Assert.True(sent.Var(1, "k4-me", "meta", "Alice"));
		// A text and a class with the same panel and name are different things.
		Assert.True(sent.Class(1, "k4-me", "name", true));
	}

	[Fact]
	public void Forgetting_sends_everything_again()
	{
		HudWrites sent = new();
		sent.Var(1, "k4-me", "name", "Alice");
		sent.Var(2, "k4-me", "name", "Bob");
		sent.Class(1, "k4-panel", "k4-closed", true);

		// A new player in slot 1.
		sent.Forget(1);
		Assert.True(sent.Var(1, "k4-me", "name", "Alice"));
		Assert.True(sent.Class(1, "k4-panel", "k4-closed", true));
		Assert.False(sent.Var(2, "k4-me", "name", "Bob"));

		// A new layout entity.
		sent.Forget();
		Assert.True(sent.Var(2, "k4-me", "name", "Bob"));
	}

	// The toast timer bar restarts by switching its class off and on; both writes must go out.
	[Fact]
	public void Switching_a_class_off_and_on_sends_both()
	{
		HudWrites sent = new();
		sent.Class(1, "k4-t0-timer", "k4-run", true);

		Assert.True(sent.Class(1, "k4-t0-timer", "k4-run", false));
		Assert.True(sent.Class(1, "k4-t0-timer", "k4-run", true));
	}
}
