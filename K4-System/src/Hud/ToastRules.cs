namespace K4System.Hud;

/// <summary>When the 7.1 toasts fire. Pure rules, so they are tested without the game.</summary>
internal static class ToastRules
{
	/// <summary>Mission progress toasts at these percents of the target; completion has its own toast.</summary>
	public static readonly int[] MissionMilestones = [50, 90];

	/// <summary>An achievement toasts "almost there" at this percent of its goal.</summary>
	public const int AlmostTherePercent = 90;

	/// <summary>The hold-your-rank warning fires within this percent of the points between a rank and the one below.</summary>
	public const int HoldRankPercent = 10;

	/// <summary>Leaderboard moves toast only for placements up to this one.</summary>
	public const int LeaderboardLimit = 100;

	/// <summary>True when <paramref name="progress"/> is at least <paramref name="percent"/>% of <paramref name="target"/>.</summary>
	public static bool Reached(long progress, long target, int percent) => target > 0 && progress * 100 >= target * percent;

	/// <summary>
	/// The highest milestone (50 or 90) that this progress step crossed, or null for none. A step that completes the
	/// mission returns null: the completion toast covers it. Mission progress only grows within a day (a reroll only
	/// replaces untouched missions), so each milestone of a mission crosses at most once per day without any flags.
	/// </summary>
	public static int? MissionMilestone(int before, int after, int target)
	{
		if (target <= 0 || after <= before || after >= target)
			return null;

		for (int i = MissionMilestones.Length - 1; i >= 0; i--)
		{
			int percent = MissionMilestones[i];
			if (Reached(after, target, percent) && !Reached(before, target, percent))
				return percent;
		}
		return null;
	}

	/// <summary>An unfinished goal at 90% or more, such as 47 of 50 kills.</summary>
	public static bool AlmostThere(int progress, int goal) => progress < goal && Reached(progress, goal, AlmostTherePercent);

	/// <summary>
	/// True when a point loss leaves the player still in their rank but within 10% of the points between it and the
	/// rank below. Ranks at 0 points or less have nothing to fall to and never warn; a rank below with -1 points (the
	/// default rank) counts as 0.
	/// </summary>
	public static bool HoldRank(int before, int after, int rankPoint, int belowPoint)
	{
		long gap = rankPoint - (long)Math.Max(belowPoint, 0);
		long margin = after - (long)rankPoint;
		return after < before && rankPoint > 0 && gap > 0 && margin >= 0 && margin * 100 <= gap * HoldRankPercent;
	}

	/// <summary>Places gained, or null when the move does not toast: no earlier placement (the first load), no gain, or outside the top 100.</summary>
	public static int? LeaderboardGain(int before, int after)
		=> before > 0 && after > 0 && after < before && after <= LeaderboardLimit ? before - after : null;

	/// <summary>"3× Kill · Headshot · Round won": each reason once, with its count when above one, at most <paramref name="max"/>.</summary>
	public static string RoundSummary(IEnumerable<(string Label, int Count)> reasons, int max = 4)
		=> string.Join(" · ", reasons.Where(reason => reason.Count > 0).Take(max).Select(reason => reason.Count > 1 ? $"{reason.Count}× {reason.Label}" : reason.Label));

	/// <summary>"+86" or "-12".</summary>
	public static string Signed(long points) => points > 0 ? $"+{points}" : points.ToString(System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>0 to 100 for a progress bar.</summary>
	public static int Percent(long progress, long target) => target <= 0 ? 0 : (int)Math.Clamp(progress * 100 / target, 0, 100);
}
