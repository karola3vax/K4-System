namespace K4System.Tests.Domain;

using K4System.Hud;

public class PanelRulesTests
{
	// The Missions page shows the streak's current week of seven days.
	[Theory]
	[InlineData(0, false, 1, 0, 0)] // no streak: day 1 is today
	[InlineData(3, false, 1, 3, 3)] // three days done, the fourth is today
	[InlineData(3, true, 1, 3, 2)] // today was the third day, done
	[InlineData(6, false, 1, 6, 6)] // the seventh day is today
	[InlineData(7, true, 1, 7, 6)] // a full week, done today
	[InlineData(7, false, 8, 0, 0)] // the next week starts: day 8 is today
	[InlineData(9, false, 8, 2, 2)]
	[InlineData(14, true, 8, 7, 6)]
	public void The_streak_shows_its_current_week(int streak, bool doneToday, int firstDay, int lit, int todayIndex)
		=> Assert.Equal((firstDay, lit, todayIndex), PanoramaHud.StreakWindow(streak, doneToday));

	[Theory]
	[InlineData("k4-lb-p0", 0)]
	[InlineData("k4-lb-p2", 2)]
	[InlineData("k4-lb-0", 3)]
	[InlineData("k4-lb-6", 9)]
	[InlineData("k4-lb-7", null)]
	[InlineData("k4-lb-prev", null)]
	[InlineData("k4-lb-newer", null)]
	[InlineData("k4-lb-me", null)]
	public void Leaderboard_clicks_map_to_their_player(string button, int? slot)
		=> Assert.Equal(slot, PanoramaHud.LeaderboardSlot(button));

	[Theory]
	[InlineData("k4-dp0", "k4-dp", 0)]
	[InlineData("k4-dp7", "k4-dp", 7)]
	[InlineData("k4-dt5", "k4-dt", 5)]
	[InlineData("k4-ds4", "k4-ds", 4)]
	[InlineData("k4-dp-next", "k4-dp", null)]
	[InlineData("k4-dp-prev", "k4-dp", null)]
	[InlineData("k4-dp", "k4-dp", null)]
	[InlineData("k4-dpx", "k4-dp", null)]
	[InlineData("k4-dt0", "k4-dp", null)] // a type card is not a player
	[InlineData("k4-d-send", "k4-ds", null)]
	public void Duel_clicks_map_to_their_slot(string button, string prefix, int? slot)
		=> Assert.Equal(slot, PanoramaHud.DuelSlot(button, prefix));

	// The panel finds a page's ids by the tab's number, so the two lists must stay in the same order.
	[Theory]
	[InlineData(HudTab.Home, "home")]
	[InlineData(HudTab.Leaderboard, "leaderboard")]
	[InlineData(HudTab.Duels, "duels")]
	[InlineData(HudTab.Ranks, "ranks")]
	[InlineData(HudTab.Stats, "stats")]
	public void Every_tab_has_its_page(HudTab tab, string page)
		=> Assert.Equal(page, PanoramaHud.PageIds[(int)tab]);

	[Fact]
	public void There_is_one_page_per_tab()
		=> Assert.Equal(Enum.GetValues<HudTab>().Distinct().Count(), PanoramaHud.PageIds.Length);
}
