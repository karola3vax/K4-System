namespace K4System.Domain;

/// <summary>Reset targets and cooldown rules for !resetmydata and css_resetdata.</summary>
internal static class ResetPolicy
{
	public const string All = "all";
	public const string Time = "time";
	public const string Stat = "stat";
	public const string Rank = "rank";

	public static bool TryNormalizeTarget(string? value, out string target)
	{
		target = (value ?? string.Empty).Trim().ToLowerInvariant();
		return target is All or Time or Stat or Rank;
	}

	/// <summary>The admin command resets everything when no section is given.</summary>
	public static bool TryGetAdminTarget(int argCount, Func<int, string> getArg, out string target)
	{
		if (argCount <= 2)
		{
			target = All;
			return true;
		}

		return TryNormalizeTarget(getArg(2), out target);
	}

	public static bool Includes(string target, string section)
		=> target == All || target == section;

	/// <summary>Personal rank resets clear reward claims so rewards can be earned again.</summary>
	public static bool ClearsRewardClaims(string target)
		=> Includes(target, Rank);

	public static TimeSpan RankResetCooldownRemaining(DateTime nowUtc, DateTime? lastResetUtc, int cooldownHours)
	{
		if (lastResetUtc is null || cooldownHours <= 0)
			return TimeSpan.Zero;

		long cooldownSeconds = Math.Min((long)cooldownHours * 3600L, TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond);
		TimeSpan cooldown = TimeSpan.FromSeconds(cooldownSeconds);
		TimeSpan elapsed = nowUtc - lastResetUtc.Value;

		if (elapsed <= TimeSpan.Zero)
			return cooldown;

		return elapsed >= cooldown ? TimeSpan.Zero : cooldown - elapsed;
	}

	public static TimeSpan CommandCooldownRemaining(DateTime nowUtc, DateTime? lastUseUtc, int cooldownSeconds)
	{
		if (lastUseUtc is null || cooldownSeconds <= 0)
			return TimeSpan.Zero;

		TimeSpan remaining = TimeSpan.FromSeconds(cooldownSeconds) - (nowUtc - lastUseUtc.Value);
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
	}

	public static string FormatRemaining(TimeSpan remaining)
	{
		long totalSeconds = Math.Max(1L, (long)Math.Ceiling(remaining.TotalSeconds));
		long days = totalSeconds / 86400L;
		long hours = totalSeconds % 86400L / 3600L;
		long minutes = totalSeconds % 3600L / 60L;
		long seconds = totalSeconds % 60L;

		return days > 0
			? $"{days}d {hours:00}:{minutes:00}:{seconds:00}"
			: $"{hours:00}:{minutes:00}:{seconds:00}";
	}
}
