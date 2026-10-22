namespace K4System.Domain;

using K4System.Config;

internal readonly record struct PointBonus(int Points, string Phrase);

/// <summary>Weapon and kill-streak bonuses. Phrases are language keys.</summary>
internal static class KillBonuses
{
	public static PointBonus? ForWeapon(string weapon, PointValues points)
	{
		string name = weapon.ToLowerInvariant();

		// Order matters: "hegrenade" and "inferno" must win over the generic grenade check.
		if (name.Contains("hegrenade"))
			return new PointBonus(points.GrenadeKill, "reason.grenade-kill");
		if (name.Contains("inferno"))
			return new PointBonus(points.InfernoKill, "reason.fire-kill");
		if (name.Contains("grenade") || name.Contains("molotov") || name.Contains("flashbang") || name.Contains("bumpmine"))
			return new PointBonus(points.ImpactKill, "reason.impact-kill");
		if (name.Contains("knife") || name.Contains("bayonet"))
			return new PointBonus(points.KnifeKill, "reason.knife-kill");
		if (name == "taser")
			return new PointBonus(points.TaserKill, "reason.taser-kill");

		return null;
	}

	public static PointBonus? ForStreak(int streak, PointValues points) => streak switch
	{
		2 => new PointBonus(points.DoubleKill, "reason.double-kill"),
		3 => new PointBonus(points.TripleKill, "reason.triple-kill"),
		4 => new PointBonus(points.Domination, "reason.domination"),
		5 => new PointBonus(points.Rampage, "reason.rampage"),
		6 => new PointBonus(points.MegaKill, "reason.mega-kill"),
		7 => new PointBonus(points.Ownage, "reason.ownage"),
		8 => new PointBonus(points.UltraKill, "reason.ultra-kill"),
		9 => new PointBonus(points.KillingSpree, "reason.killing-spree"),
		10 => new PointBonus(points.MonsterKill, "reason.monster-kill"),
		11 => new PointBonus(points.Unstoppable, "reason.unstoppable"),
		12 => new PointBonus(points.GodLike, "reason.godlike"),
		_ => null
	};

	/// <summary>
	/// Advances a kill streak. A kill inside the time window continues the streak; after
	/// the last named streak (12) the counter starts again at 1.
	/// </summary>
	public static (int Streak, PointBonus? Bonus) NextStreak(int currentStreak, DateTime lastKillUtc, DateTime nowUtc, int secondsBetweenKills, PointValues points)
	{
		bool inWindow = secondsBetweenKills <= 0 || nowUtc - lastKillUtc <= TimeSpan.FromSeconds(secondsBetweenKills);

		if (currentStreak <= 0 || !inWindow)
			return (1, null);

		int streak = currentStreak + 1;
		PointBonus? bonus = ForStreak(streak, points);
		return bonus is null ? (1, null) : (streak, bonus);
	}
}
