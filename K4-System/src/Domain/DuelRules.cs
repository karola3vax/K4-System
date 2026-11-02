namespace K4System.Domain;

using K4System.Missions;

/// <summary>Duel rules that need no game objects: which kills count, when a duel is over, and which types a map offers.</summary>
internal static class DuelRules
{
	/// <summary>Every type in panel order, as written in k4-system.jsonc and in "!duel &lt;player&gt; &lt;points&gt; [type]".</summary>
	public static readonly string[] TypeNames = [.. Enum.GetValues<DuelType>().Select(Name)];

	/// <summary>"noscope" for DuelType.Noscope: the config, command and language-key spelling.</summary>
	public static string Name(DuelType type) => type.ToString().ToLowerInvariant();

	/// <summary>A type name, case-insensitive. Numbers are not names, so "2" is not Zeus.</summary>
	public static bool TryParse(string? text, out DuelType type)
	{
		int index = Array.FindIndex(TypeNames, name => name.Equals(text?.Trim(), StringComparison.OrdinalIgnoreCase));
		type = index < 0 ? DuelType.Any : (DuelType)index;
		return index >= 0;
	}

	/// <summary>Whether a kill between the two duel players counts for this type. <paramref name="weapon"/> is the death event's weapon.</summary>
	public static bool Counts(DuelType type, string weapon, bool headshot, bool noscope)
	{
		string name = WeaponCatalog.Normalize(weapon);
		return type switch
		{
			DuelType.Any => true,
			DuelType.Noscope => noscope,
			DuelType.Zeus => name == WeaponCatalog.Zeus,
			DuelType.Knife => WeaponCatalog.IsKnife(name),
			DuelType.Scout => name == "ssg08",
			DuelType.Headshot => headshot,
			_ => false
		};
	}

	/// <summary>
	/// Who has won: 1 the challenger, -1 the opponent, 0 a tie (both stakes back), null still running.
	/// Reaching the target wins at once. When the duel is <paramref name="over"/> (time limit, match or map end)
	/// the leader wins, and a player who is not on the server loses.
	/// </summary>
	public static int? Decide(bool challengerPresent, int challengerKills, bool opponentPresent, int opponentKills, int target, bool over)
	{
		if (challengerKills >= target)
			return 1;
		if (opponentKills >= target)
			return -1;
		return over ? Showdowns.Winner(challengerPresent, challengerKills, opponentPresent, opponentKills) : null;
	}

	/// <summary>
	/// The enabled types the map can offer, in panel order. Noscope needs a sniper rifle, Scout the SSG 08, Zeus the
	/// taser; null weapons means the map was not read yet, so nothing is ruled out (as missions do).
	/// </summary>
	public static IReadOnlyList<DuelType> Available(IEnumerable<string> enabled, IReadOnlySet<string>? mapWeapons)
	{
		HashSet<DuelType> on = [];
		foreach (string name in enabled)
		{
			if (TryParse(name, out DuelType type))
				on.Add(type);
		}

		return [.. Enum.GetValues<DuelType>().Where(type => on.Contains(type) && MapOffers(type, mapWeapons))];
	}

	private static bool MapOffers(DuelType type, IReadOnlySet<string>? weapons) => weapons is null || type switch
	{
		DuelType.Noscope => WeaponCatalog.Snipers.Any(weapons.Contains),
		DuelType.Scout => weapons.Contains("ssg08"),
		DuelType.Zeus => weapons.Contains(WeaponCatalog.Zeus),
		_ => true
	};

	/// <summary>The largest stake two players can put up, or 0 when it would be below the minimum stake.</summary>
	public static int MaxStake(int challengerPoints, int opponentPoints, int percent, int minStake)
	{
		int max = Showdowns.MaxStake(challengerPoints, opponentPoints, percent);
		return max < minStake ? 0 : max;
	}

	/// <summary>A stake is allowed between the minimum and the cap; a cap of 0 allows nothing.</summary>
	public static bool StakeAllowed(int stake, int minStake, int maxStake) => maxStake > 0 && stake >= minStake && stake <= maxStake;

	/// <summary>Whole seconds for the panel and chat: 0.2 seconds left still shows 1.</summary>
	public static int WholeSeconds(TimeSpan left) => left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalSeconds);
}
