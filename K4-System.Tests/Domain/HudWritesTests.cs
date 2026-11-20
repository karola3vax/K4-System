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

/// <summary>
/// The write budget (k4-menu-council-tech): the real frame, leaderboard and toast painters driven through a fake
/// writer. What feeds those painters from the game (players, services, the database) is not covered here.
/// </summary>
public class HudWriteBudgetTests
{
	private int writes;

	private HudWrites Counting() => new((_, _, _, _) => writes++, (_, _, _, _) => writes++);

	private int Count(Action paint)
	{
		writes = 0;
		paint();
		return writes;
	}

	private static PanoramaHud.FrameView Frame(HudTab tab, bool back = false, (string, bool)? status = null) => new(
		tab,
		[true, true, true, true, true, true],
		["Home", "Missions", "Achievements", "Leaderboard", "Duels", "Profile"],
		0,
		null,
		tab.ToString(),
		back ? "Back" : null,
		new PanoramaHud.SeasonClockView("Season · November 2026", "11d left", 10, 30),
		null,
		status,
		"Tab closes");

	[Fact]
	public void A_tab_switch_with_cached_data_and_an_unchanged_redraw()
	{
		HudWrites sent = Counting();
		HudPen pen = new(sent, 1, null);
		int open = Count(() => PanoramaHud.PaintFrame(pen, Frame(HudTab.Home)));

		// The old tab and screen off, the new ones on, and the crumb text. The screen was filled before, so its
		// unchanged rows cost nothing.
		Assert.Equal(5, Count(() => PanoramaHud.PaintFrame(pen, Frame(HudTab.Leaderboard))));
		Assert.Equal(0, Count(() => PanoramaHud.PaintFrame(pen, Frame(HudTab.Leaderboard))));

		// Below a tab (another player's profile) Back also shows, with its label the first time: 4 + 1 + 2 = 7, one
		// over the council's 6. A click result in the footer adds its show, text and ok class (3) the first time.
		Assert.Equal(7, Count(() => PanoramaHud.PaintFrame(pen, Frame(HudTab.Profile, back: true))));
		Assert.Equal(3, Count(() => PanoramaHud.PaintFrame(pen, Frame(HudTab.Profile, back: true, status: ("Challenge sent to ayaro", true)))));
		Assert.InRange(open, 1, 40);
	}

	private static List<PanoramaHud.LeaderboardRow> Rows(int count, int me = 12) => [.. Enumerable.Range(1, count).Select(i =>
		new PanoramaHud.LeaderboardRow(i.ToString(), $"player{i}", "Gold Nova", (5000 - i).ToString(), $"sg-{1 + i % 18}", i, i == me, i % 10 == 0))];

	[Fact]
	public void The_first_leaderboard_fill_and_its_redraw()
	{
		HudWrites sent = Counting();
		HudPen pen = new(sent, 1, null);

		// Per row: shown, 4 texts, 2 for the emblem (the layout's sg-0 off, the rank's on) = 7, the floor for 100 rows;
		// plus 3 medals, the own row mark, 10 Challenge buttons and the more line (2): 716, over the council's 700 by
		// what the contract adds on top of the 7-per-row floor. The screen header adds about 10 more.
		Assert.Equal(716, Count(() => PanoramaHud.PaintLeaderboard(pen, Rows(100), profiles: true, "240 more not shown")));
		Assert.Equal(0, Count(() => PanoramaHud.PaintLeaderboard(pen, Rows(100), profiles: true, "240 more not shown")));

		// Two players swap places: only what changed in their two rows is sent.
		List<PanoramaHud.LeaderboardRow> moved = Rows(100);
		(moved[3], moved[4]) = (moved[4] with { Pos = "4" }, moved[3] with { Pos = "5" });
		Assert.InRange(Count(() => PanoramaHud.PaintLeaderboard(pen, moved, profiles: true, "240 more not shown")), 1, 14);
	}

	[Fact]
	public void An_empty_leaderboard_costs_nothing_and_a_shorter_one_hides_its_old_rows()
	{
		HudWrites sent = Counting();
		HudPen pen = new(sent, 1, null);
		Assert.Equal(0, Count(() => PanoramaHud.PaintLeaderboard(pen, [], profiles: true, "")));

		PanoramaHud.PaintLeaderboard(pen, Rows(20), profiles: true, "");
		// 18 rows hide, and the Challenge buttons of rows 10 and 20 with them.
		Assert.Equal(18 + 2, Count(() => PanoramaHud.PaintLeaderboard(pen, Rows(2), profiles: true, "")));
	}

	[Fact]
	public void One_toast_is_at_most_sixteen_writes()
	{
		ToastEntry plain = new(new Toast("Mission done", "Get 10 kills", "+80", "ic-kill", ToastColor.Green));
		ToastEntry big = new(new Toast("Achievement unlocked", "Headhunter", "+500", "ic-kill_headshot", ToastColor.Gold, ToastLength.Long, 90, "gold"));

		HudPen pen = new(Counting(), 1, null);
		Assert.InRange(Count(() => PanoramaHud.PaintToasts(pen, [plain], [plain], 0, windowOpen: false)), 1, 16);

		// The most a toast can carry: gold, long, a medal ring and a progress bar. 15 here plus the timer's k4-run on
		// the next frame is 16. Opening the window (k4-open) and the "+2" line for waiting toasts are the stack's,
		// not the toast's: 3 more, once.
		HudPen fresh = new(Counting(), 2, null);
		Assert.Equal(15, Count(() => PanoramaHud.PaintToasts(fresh, [big], [big], 0, windowOpen: false)));
		Assert.Equal(3, Count(() => PanoramaHud.PaintToasts(fresh, [big], [], 2, windowOpen: true)));
	}

	[Fact]
	public void Parts_still_as_the_layout_built_them_cost_no_write()
	{
		HudPen pen = new(Counting(), 1, null);
		Assert.Equal(0, Count(() =>
		{
			pen.Show("k4-lb-r5", false);     // starts k4-gone
			pen.Gone("k4-nav-home", false);  // starts shown
			pen.Flag("k4-a3", "k4-done", false);
			pen.Flag("k4-panel", "k4-closed", true);
			pen.Bar("k4-h-bar", 0, 10);      // w0
			pen.Swap("k4-h-rank-icon", "icon", "sg-0");
		}));
		Assert.Equal(1, Count(() => pen.Show("k4-lb-r5", true)));
		Assert.Equal(2, Count(() => pen.Swap("k4-h-rank-icon", "icon", "sg-7")));
	}

	[Fact]
	public void A_disconnect_puts_the_slot_back_to_the_layout_defaults()
	{
		List<(string Panel, string Name, bool On)> classes = [];
		HudWrites sent = new((_, _, _, _) => { }, (_, panel, name, on) => classes.Add((panel, name, on)));
		HudPen pen = new(sent, 1, null);
		pen.Flag("k4-panel", "k4-closed", false);
		pen.Show("k4-lb-r5", true);
		pen.Flag("k4-view-duels", "k4-show", true);
		pen.Swap("k4-h-rank-icon", "icon", "sg-7");
		pen.Show("k4-lb-r6", true);
		pen.Show("k4-lb-r6", false); // back at its default already: nothing to undo
		classes.Clear();

		sent.Reset(1, null);

		Assert.Equal(
			[("k4-panel", "k4-closed", true), ("k4-lb-r5", "k4-gone", true), ("k4-view-duels", "k4-show", false), ("k4-h-rank-icon", "sg-7", false), ("k4-h-rank-icon", "sg-0", true)],
			classes);

		// The next player in the slot starts from the defaults, so showing the row is sent again.
		classes.Clear();
		pen.Show("k4-lb-r5", true);
		Assert.Single(classes);
	}

	[Fact]
	public void A_failed_write_is_sent_again()
	{
		bool fail = true;
		int tries = 0;
		HudWrites sent = new((_, _, _, _) =>
		{
			tries++;
			if (fail)
				throw new InvalidOperationException();
		}, (_, _, _, _) => { });
		Assert.Throws<InvalidOperationException>(() => sent.SetVar(1, null, "k4-crumbs", "crumb", "Home"));
		fail = false;
		sent.SetVar(1, null, "k4-crumbs", "crumb", "Home");
		Assert.Equal(2, tries);
	}

	[Fact]
	public void A_failed_write_keeps_what_the_slot_already_shows()
	{
		bool fail = false;
		List<(string Panel, string Name, bool On)> classes = [];
		HudWrites sent = new((_, _, _, _) => { }, (_, panel, name, on) =>
		{
			if (fail)
				throw new InvalidOperationException();
			classes.Add((panel, name, on));
		});
		HudPen pen = new(sent, 1, null);
		pen.Flag("k4-view-leaderboard", "k4-show", true);
		fail = true;
		Assert.Throws<InvalidOperationException>(() => pen.Bar("k4-h-bar", 5, 10));
		fail = false;
		classes.Clear();

		// Leaving the leaderboard must still hide it: the failure did not make the slot look like the layout's defaults.
		pen.Flag("k4-view-leaderboard", "k4-show", false);
		pen.Bar("k4-h-bar", 5, 10);

		Assert.Equal([("k4-view-leaderboard", "k4-show", false), ("k4-h-bar", "w0", false), ("k4-h-bar", "w10", true)], classes);
	}

	[Fact]
	public void A_new_player_in_a_slot_starts_from_the_layout_defaults()
	{
		List<(string Panel, string Name, bool On)> classes = [];
		HudWrites sent = new((_, _, _, _) => { }, (_, panel, name, on) => classes.Add((panel, name, on)));
		HudPen pen = new(sent, 1, null);
		sent.Claim(1, 111, null);
		pen.Flag("k4-view-leaderboard", "k4-show", true);
		pen.Bar("k4-h-bar", 12, 20);
		classes.Clear();

		// The same player again: nothing to undo.
		sent.Claim(1, 111, null);
		Assert.Empty(classes);

		// Another player in the slot (the server never learned who left): the old screen and bar are taken back first.
		sent.Claim(1, 222, null);
		Assert.Equal([("k4-view-leaderboard", "k4-show", false), ("k4-h-bar", "w12", false), ("k4-h-bar", "w0", true)], classes);
	}
}
