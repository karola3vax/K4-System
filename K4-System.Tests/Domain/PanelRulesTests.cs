namespace K4System.Tests.Domain;

using System.Text.RegularExpressions;
using K4System.Hud;

public partial class PanelRulesTests
{
	// The Missions screen shows the streak's current week of seven days.
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

	// One parser maps every pooled click id back to the row the server filled.
	[Theory]
	[InlineData("k4-lb-r0-profile", "k4-lb-r", "-profile", 100, 0)]
	[InlineData("k4-lb-r99-profile", "k4-lb-r", "-profile", 100, 99)]
	[InlineData("k4-lb-r100-profile", "k4-lb-r", "-profile", 100, null)] // past the pool
	[InlineData("k4-lb-r12-duel", "k4-lb-r", "-duel", 100, 12)]
	[InlineData("k4-lb-r12-duel", "k4-lb-r", "-profile", 100, null)]
	[InlineData("k4-lb-r07-profile", "k4-lb-r", "-profile", 100, null)] // a leading zero is not a row the server made
	[InlineData("k4-lb-r-profile", "k4-lb-r", "-profile", 100, null)]
	[InlineData("k4-lb-rx-profile", "k4-lb-r", "-profile", 100, null)]
	[InlineData("k4-a63", "k4-a", "", 64, 63)]
	[InlineData("k4-a64", "k4-a", "", 64, null)]
	[InlineData("k4-a-sc0", "k4-a", "", 64, null)] // a showcase slot is not an achievement card
	[InlineData("k4-a-sc2", "k4-a-sc", "", 3, 2)]
	[InlineData("k4-af-all", "k4-a", "", 64, null)]
	[InlineData("k4-dp63", "k4-dp", "", 64, 63)]
	[InlineData("k4-dp-more", "k4-dp", "", 64, null)]
	[InlineData("k4-dt5", "k4-dt", "", 6, 5)]
	[InlineData("k4-dt0", "k4-dp", "", 64, null)] // a type card is not a player
	[InlineData("k4-ds4", "k4-ds", "", 5, 4)]
	[InlineData("k4-m2-yes", "k4-m", "-yes", 3, 2)]
	[InlineData("k4-m2-no", "k4-m", "-yes", 3, null)]
	[InlineData("k4-mw", "k4-m", "-yes", 3, null)]
	[InlineData("k4-reroll-1", "k4-reroll-", "", 3, 1)]
	[InlineData("k4-h-m3", "k4-h-m", "", 4, 3)]
	[InlineData("k4-h-me", "k4-h-m", "", 4, null)]
	[InlineData("k4-h-n2", "k4-h-n", "", 3, 2)]
	public void Pooled_clicks_map_to_their_row(string id, string prefix, string suffix, int pool, int? row)
		=> Assert.Equal(row, PanoramaHud.Row(id, prefix, suffix, pool));

	// Every row of every pool parses back to itself, so no row of the layout is dead to clicks.
	[Theory]
	[InlineData("k4-lb-r", "-profile", PanoramaHud.LeaderboardRows)]
	[InlineData("k4-lb-r", "-duel", PanoramaHud.LeaderboardRows)]
	[InlineData("k4-a", "", PanoramaHud.AchievementRows)]
	[InlineData("k4-dp", "", PanoramaHud.DuelPlayerRows)]
	public void Every_row_of_a_pool_round_trips(string prefix, string suffix, int pool)
	{
		for (int i = 0; i < pool; i++)
			Assert.Equal(i, PanoramaHud.Row($"{prefix}{i}{suffix}", prefix, suffix, pool));
	}

	[Fact]
	public void The_pools_are_the_contract_sizes()
	{
		Assert.Equal(100, PanoramaHud.LeaderboardRows);
		Assert.Equal(40, PanoramaHud.RankRows);
		Assert.Equal(64, PanoramaHud.AchievementRows);
		Assert.Equal(64, PanoramaHud.DuelPlayerRows);
		Assert.Equal(24, PanoramaHud.SeasonRows);
		Assert.Equal(4, PanoramaHud.LiveDuelRows);
		Assert.Equal(8, PanoramaHud.ContractVersion);
		Assert.Equal(120f, PanoramaHud.IdleCloseSeconds);
	}

	// The panel finds a screen's ids by the tab's number, so the two lists must stay in the same order.
	[Theory]
	[InlineData(HudTab.Home, "home")]
	[InlineData(HudTab.Missions, "missions")]
	[InlineData(HudTab.Leaderboard, "leaderboard")]
	[InlineData(HudTab.Duels, "duels")]
	[InlineData(HudTab.Profile, "profile")]
	[InlineData(HudTab.Settings, "settings")]
	public void Every_screen_has_its_ids(HudTab tab, string page)
		=> Assert.Equal(page, PanoramaHud.PageIds[(int)tab]);

	[Fact]
	public void Ranks_opens_the_leaderboard_and_every_other_name_is_a_screen()
	{
		Assert.Equal(HudTab.Leaderboard, PanoramaHud.Screen(HudTab.Ranks));
		foreach (HudTab tab in Enum.GetValues<HudTab>().Where(tab => tab != HudTab.Ranks))
			Assert.Equal(tab, PanoramaHud.Screen(tab));
		Assert.Equal(Enum.GetValues<HudTab>().Length - 1, PanoramaHud.PageIds.Length);
	}

	// Home offers at most three "do this next" cards, only for things that are true now, in the owner's order.
	[Theory]
	[InlineData(false, false, false, false, false, "")]
	[InlineData(true, false, false, false, true, "Duel,Season")]
	[InlineData(true, true, true, true, true, "Duel,Swap,Showcase")]
	[InlineData(false, false, true, true, true, "Showcase,Achievement,Season")]
	[InlineData(false, true, false, false, false, "Swap")]
	public void Next_cards_are_real_and_at_most_three(bool duel, bool swap, bool showcase, bool achievement, bool season, string expected)
		=> Assert.Equal(expected, string.Join(",", PanoramaHud.PickNext(duel, swap, showcase, achievement, season)));

	[Fact]
	public void Every_next_card_has_its_contract_class()
	{
		string contract = Contract();
		foreach (NextKind kind in Enum.GetValues<NextKind>())
			Assert.Contains($"`{PanoramaHud.NextClass(kind)}`", contract, StringComparison.Ordinal);
	}

	// The simple look shows the place short, as "#12": the leaderboard count says out of how many.
	[Theory]
	[InlineData(12, "#12")]
	[InlineData(1234, "#1,234")]
	[InlineData(0, "")]
	[InlineData(-1, "")]
	public void The_place_reads_hash_number(int placement, string expected)
		=> Assert.Equal(expected, PanoramaHud.Place(placement));

	// 0 players on the leaderboard, 150 achievements against a pool of 64.
	[Theory]
	[InlineData(0, 100, 0)]
	[InlineData(100, 100, 0)]
	[InlineData(340, 100, 240)]
	[InlineData(150, 64, 86)]
	public void Lists_longer_than_their_pool_say_how_many_are_not_shown(int count, int pool, int more)
		=> Assert.Equal(more, PanoramaHud.NotShown(count, pool));

	[Fact]
	public void Back_returns_where_the_player_came_from_newest_first()
	{
		BackHistory<string> history = new();
		Assert.False(history.TryPop(out _));

		// Leaderboard, then someone's profile, then their Duels: Back goes profile, then leaderboard.
		history.Push("leaderboard");
		history.Push("profile zabullet");
		Assert.True(history.TryPop(out string? first));
		Assert.Equal("profile zabullet", first);
		Assert.True(history.TryPop(out string? second));
		Assert.Equal("leaderboard", second);
		Assert.Equal(0, history.Count);
	}

	[Fact]
	public void Back_history_keeps_the_newest_views_only()
	{
		BackHistory<int> history = new();
		for (int i = 0; i < BackHistory<int>.Max + 5; i++)
			history.Push(i);

		Assert.Equal(BackHistory<int>.Max, history.Count);
		Assert.True(history.TryPop(out int newest));
		Assert.Equal(BackHistory<int>.Max + 4, newest);

		history.Clear();
		Assert.Equal(0, history.Count);
	}

	/// <summary>
	/// Every panel id and class the server writes is in docs/panel-contract.md. Numbers and interpolations count as
	/// row indexes (the pool tests cover the sizes), so "k4-lb-r{i}-duel" matches the contract's `k4-lb-r{i}-duel`
	/// and "k4-a0" matches `k4-a63`. Prefixes handed to the row parser or StartsWith must start a contract id.
	/// </summary>
	[Fact]
	public void Every_id_the_server_writes_is_in_the_contract()
	{
		string[] contract = [.. ContractToken().Matches(Contract()).Select(match => Normalize(match.Groups[1].Value)).Distinct()];
		string source = File.ReadAllText(Path.Combine(TestData.RepositoryRoot(), "K4-System", "src", "Hud", "PanoramaHud.cs"));

		HashSet<string> prefixes = [.. PrefixUse().Matches(source).Select(match => match.Groups[1].Value)];
		List<string> missing = [];
		foreach (Match literal in Literal().Matches(source))
		{
			string text = literal.Groups[1].Value;
			Regex wanted = new("^" + Regex.Escape(Normalize(text)).Replace("@", "[a-z0-9#-]+", StringComparison.Ordinal) + (prefixes.Contains(text) ? "" : "$"));
			// A row prefix passed on to a helper ("k4-sd" for k4-sd0 ... k4-sd6) counts when the contract has its rows.
			if (!contract.Any(wanted.IsMatch) && !contract.Contains(Normalize(text) + "#"))
				missing.Add(text);
		}

		foreach (Match row in RowCall().Matches(source))
		{
			string id = Normalize(row.Groups[1].Value + "0" + row.Groups[2].Value);
			if (!contract.Contains(id))
				missing.Add(row.Value);
		}

		Assert.True(missing.Count == 0, $"Not in docs/panel-contract.md: {string.Join(", ", missing.Distinct())}");
	}

	private static string Contract() => File.ReadAllText(Path.Combine(TestData.RepositoryRoot(), "docs", "panel-contract.md"));

	// Digits and {i} become #; an interpolation hole in the code becomes @ (any id part).
	private static string Normalize(string id)
		=> Digits().Replace(Hole().Replace(id.Replace("{i}", "#", StringComparison.Ordinal), "@"), "#");

	[GeneratedRegex(@"`(k4-[a-z0-9{}\-]+)`")]
	private static partial Regex ContractToken();

	[GeneratedRegex(@"\$?""(k4-[^""]*)""")]
	private static partial Regex Literal();

	[GeneratedRegex(@"(?:StartsWith\(|Row\(id, )""(k4-[^""]*)""")]
	private static partial Regex PrefixUse();

	[GeneratedRegex(@"Row\(id, ""(k4-[^""]*)"", ""([^""]*)""")]
	private static partial Regex RowCall();

	[GeneratedRegex(@"\{[^}]*\}")]
	private static partial Regex Hole();

	[GeneratedRegex(@"[0-9]+")]
	private static partial Regex Digits();
}
