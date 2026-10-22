namespace K4System.Ranks;

using K4System.Config;

/// <summary>
/// The active rank ladder for one session. Ranks are ordered by points; ranks that share a
/// threshold keep their file order, so the last eligible one at a threshold wins.
/// </summary>
internal sealed class RankLadder
{
	private RankLadder(IReadOnlyList<RankDefinition> ranks, RankDefinition none, RankGameMode mode)
	{
		Ranks = ranks;
		None = none;
		Mode = mode;
	}

	public IReadOnlyList<RankDefinition> Ranks { get; }

	public RankDefinition None { get; }

	public RankGameMode Mode { get; }

	public int ActiveRankCount => Ranks.Count(rank => !rank.IsDefault);

	/// <summary>Filters by game mode, orders by points, assigns runtime ids, and resolves colors.</summary>
	public static RankLadder Create(IEnumerable<RankDefinition> definitions, RankGameMode mode, Func<string, string>? colorResolver = null)
	{
		List<RankDefinition> ranks = definitions
			.Where(rank => ShouldInclude(rank, mode))
			.OrderBy(rank => rank.Point)
			.ToList();

		int id = 0;
		foreach (RankDefinition rank in ranks)
		{
			rank.Id = rank.IsDefault ? -1 : id++;
			rank.ColorName = rank.Color.Trim().ToLowerInvariant();
			if (colorResolver is not null)
				rank.Color = colorResolver(rank.Color);
		}

		RankDefinition none = ranks.Single(rank => rank.IsDefault);
		return new RankLadder(ranks, none, mode);
	}

	internal static bool ShouldInclude(RankDefinition rank, RankGameMode mode)
	{
		if (rank.IsDefault)
			return true;

		return mode switch
		{
			RankGameMode.Normal => true,
			RankGameMode.FlagsOnly => rank.HasRequirements,
			RankGameMode.NoFlags => !rank.HasRequirements,
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown rank game mode.")
		};
	}

	public static bool MeetsRequirements(RankDefinition rank, Func<string, bool> hasPermission)
	{
		if (rank.RequiredPermissions is null || rank.RequiredPermissions.Count == 0)
			return true;

		bool hasRequirement = false;

		foreach (string requirement in rank.RequiredPermissions)
		{
			if (string.IsNullOrWhiteSpace(requirement))
				continue;

			hasRequirement = true;

			if (hasPermission(requirement.Trim()))
				return true;
		}

		return !hasRequirement;
	}

	/// <summary>The highest rank the player may hold with these points and permissions.</summary>
	public RankDefinition Resolve(int points, Func<string, bool> hasPermission)
		=> Resolve(Ranks, None, points, hasPermission);

	private static RankDefinition Resolve(IEnumerable<RankDefinition> ordered, RankDefinition none, int points, Func<string, bool> hasPermission)
	{
		RankDefinition? result = null;

		foreach (RankDefinition rank in ordered)
		{
			if (points >= rank.Point && MeetsRequirements(rank, hasPermission))
				result = rank;
		}

		return result ?? none;
	}

	/// <summary>The highest rank by points alone. Used where no player is available.</summary>
	public RankDefinition ResolveIgnoringPermissions(int points)
		=> Ranks.LastOrDefault(rank => points >= rank.Point) ?? None;

	/// <summary>The distinct ranks this player can actually move through, lowest first.</summary>
	public IReadOnlyList<RankDefinition> Progression(Func<string, bool> hasPermission)
	{
		IEnumerable<int> thresholds = Ranks
			.Select(rank => rank.Point)
			.Append(None.Point)
			.Distinct()
			.OrderBy(point => point);
		List<RankDefinition> progression = [];

		foreach (int threshold in thresholds)
		{
			RankDefinition effective = Resolve(threshold, hasPermission);

			if (progression.Count > 0 && ReferenceEquals(effective, progression[^1]))
				continue;

			progression.Add(effective);
		}

		return progression;
	}

	public static RankDefinition? Next(IReadOnlyList<RankDefinition> progression, int points)
		=> progression.FirstOrDefault(rank => rank.Point > points);

	public static int Position(IReadOnlyList<RankDefinition> progression, RankDefinition current)
	{
		for (int index = 0; index < progression.Count; index++)
		{
			RankDefinition rank = progression[index];
			if (ReferenceEquals(rank, current) || (!string.IsNullOrWhiteSpace(current.Key) && rank.Key == current.Key))
				return index + 1;
		}

		return 1;
	}

	/// <summary>In FlagsOnly mode, automatic scoring waits until the player matches an active flagged rank.</summary>
	public bool IsAutomaticScoringAllowed(Func<string, bool> hasPermission)
	{
		return Mode switch
		{
			RankGameMode.Normal or RankGameMode.NoFlags => true,
			RankGameMode.FlagsOnly => Ranks.Any(rank => !rank.IsDefault && rank.HasRequirements && MeetsRequirements(rank, hasPermission)),
			_ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown rank game mode.")
		};
	}

	public RankDefinition? FindByName(string name)
		=> Ranks.FirstOrDefault(rank => rank.Name == name);
}
