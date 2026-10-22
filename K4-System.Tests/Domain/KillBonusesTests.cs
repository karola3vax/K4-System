namespace K4System.Tests.Domain;

using K4System.Config;
using K4System.Domain;

public class KillBonusesTests
{
	private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
	private static readonly PointValues Points = PointPresets.Get(PointPresets.Casual);

	[Fact]
	public void FirstKillStartsStreakWithoutBonus()
	{
		(int streak, PointBonus? bonus) = KillBonuses.NextStreak(0, Now.AddHours(-1), Now, 5, Points);

		Assert.Equal(1, streak);
		Assert.Null(bonus);
	}

	[Fact]
	public void KillInsideWindowContinuesStreak()
	{
		(int streak, PointBonus? bonus) = KillBonuses.NextStreak(1, Now.AddSeconds(-3), Now, 5, Points);

		Assert.Equal(2, streak);
		Assert.Equal(new PointBonus(Points.DoubleKill, "reason.double-kill"), bonus);
	}

	[Fact]
	public void KillAtWindowEdgeContinuesStreak()
	{
		(int streak, _) = KillBonuses.NextStreak(2, Now.AddSeconds(-5), Now, 5, Points);

		Assert.Equal(3, streak);
	}

	[Fact]
	public void KillOutsideWindowRestartsStreak()
	{
		(int streak, PointBonus? bonus) = KillBonuses.NextStreak(4, Now.AddSeconds(-6), Now, 5, Points);

		Assert.Equal(1, streak);
		Assert.Null(bonus);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void NoWindowMeansEveryKillContinues(int secondsBetweenKills)
	{
		(int streak, PointBonus? bonus) = KillBonuses.NextStreak(3, Now.AddHours(-2), Now, secondsBetweenKills, Points);

		Assert.Equal(4, streak);
		Assert.Equal(new PointBonus(Points.Domination, "reason.domination"), bonus);
	}

	[Fact]
	public void StreakTwelveIsGodlikeThenResetsToOne()
	{
		(int twelve, PointBonus? godlike) = KillBonuses.NextStreak(11, Now, Now, 0, Points);
		Assert.Equal(12, twelve);
		Assert.Equal(new PointBonus(Points.GodLike, "reason.godlike"), godlike);

		(int after, PointBonus? none) = KillBonuses.NextStreak(12, Now, Now, 0, Points);
		Assert.Equal(1, after);
		Assert.Null(none);
	}

	[Fact]
	public void EveryStreakFromTwoToTwelveHasBonus()
	{
		for (int streak = 2; streak <= 12; streak++)
			Assert.NotNull(KillBonuses.ForStreak(streak, Points));

		Assert.Null(KillBonuses.ForStreak(1, Points));
		Assert.Null(KillBonuses.ForStreak(13, Points));
	}

	[Theory]
	[InlineData("hegrenade", "reason.grenade-kill")]
	[InlineData("inferno", "reason.fire-kill")]
	[InlineData("molotov", "reason.impact-kill")]
	[InlineData("flashbang", "reason.impact-kill")]
	[InlineData("smokegrenade", "reason.impact-kill")]
	[InlineData("knife_t", "reason.knife-kill")]
	[InlineData("bayonet", "reason.knife-kill")]
	[InlineData("taser", "reason.taser-kill")]
	public void WeaponBonusesUseExpectedPhrase(string weapon, string phrase)
	{
		PointBonus? bonus = KillBonuses.ForWeapon(weapon, Points);

		Assert.NotNull(bonus);
		Assert.Equal(phrase, bonus.Value.Phrase);
	}

	[Theory]
	[InlineData("ak47")]
	[InlineData("awp")]
	public void RegularWeaponsHaveNoBonus(string weapon)
	{
		Assert.Null(KillBonuses.ForWeapon(weapon, Points));
	}
}
