namespace K4System.Domain;

/// <summary>Overflow-safe point arithmetic. Stored points never go below zero.</summary>
internal static class PointMath
{
	public static int AddToStored(int current, int amount)
		=> (int)Math.Clamp((long)current + amount, 0, int.MaxValue);

	public static int ClampStored(int points)
		=> Math.Max(0, points);

	public static int AddClamped(int value, int amount)
		=> (int)Math.Clamp((long)value + amount, int.MinValue, int.MaxValue);

	public static int Scale(int amount, double multiplier)
	{
		if (!double.IsFinite(multiplier) || multiplier < 0)
			return 0;

		return (int)Math.Clamp(Math.Round(amount * multiplier), int.MinValue, int.MaxValue);
	}

	/// <summary>
	/// Scales a kill or death by the victim-to-killer point ratio, within the configured bounds.
	/// No points counts as 1, so an empty account is the weakest opponent, not a full-value one.
	/// </summary>
	public static int Dynamic(int amount, int fromPoints, int toPoints, double minMultiplier, double maxMultiplier)
	{
		double ratio = Math.Clamp((double)Math.Max(1, toPoints) / Math.Max(1, fromPoints), minMultiplier, maxMultiplier);
		return Scale(amount, ratio);
	}
}
