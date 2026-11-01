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
}
