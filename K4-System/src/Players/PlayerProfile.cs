namespace K4System.Players;

/// <summary>
/// What a player's profile adds to their stats: kills per weapon and the achievements they show off. Game thread only;
/// saves receive the changes through <see cref="TakeUnsavedKills"/> and <see cref="TakeUnsavedShowcase"/>.
/// </summary>
public sealed class PlayerProfile
{
	/// <summary>Achievements a profile can show at once.</summary>
	public const int ShowcaseSize = 3;

	private readonly Dictionary<string, int> weaponKills;
	private readonly HashSet<string> unsavedWeapons = new(StringComparer.Ordinal);
	private readonly List<string> showcase;
	private bool showcaseUnsaved;

	public PlayerProfile(IReadOnlyDictionary<string, int>? weaponKills, IReadOnlyList<string>? showcase)
	{
		this.weaponKills = new Dictionary<string, int>(weaponKills ?? new Dictionary<string, int>(), StringComparer.Ordinal);
		this.showcase = [.. (showcase ?? []).Distinct(StringComparer.Ordinal).Take(ShowcaseSize)];
	}

	public IReadOnlyDictionary<string, int> WeaponKills => weaponKills;

	public IReadOnlyList<string> Showcase => showcase;

	public void AddKill(string weapon)
	{
		if (weapon.Length == 0)
			return;

		weaponKills[weapon] = weaponKills.GetValueOrDefault(weapon) + 1;
		unsavedWeapons.Add(weapon);
	}

	/// <summary>Every weapon back to zero, for a stat reset. The zeros are saved like any other change.</summary>
	public void ResetKills()
	{
		foreach (string weapon in weaponKills.Keys.ToList())
		{
			weaponKills[weapon] = 0;
			unsavedWeapons.Add(weapon);
		}
	}

	/// <summary>The weapon with the most kills; ties go to the name first in alphabetical order, so it never flickers.</summary>
	public static (string Weapon, int Kills)? Favorite(IReadOnlyDictionary<string, int> kills)
		=> kills.Where(pair => pair.Value > 0)
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key, StringComparer.Ordinal)
			.Select(pair => ((string, int)?)(pair.Key, pair.Value))
			.FirstOrDefault();

	public enum ShowcaseChange
	{
		Added,
		Removed,
		Full
	}

	/// <summary>Adds the achievement to the showcase, or removes it when it is already shown.</summary>
	public ShowcaseChange Toggle(string achievement)
	{
		ShowcaseChange change;
		if (showcase.Remove(achievement))
			change = ShowcaseChange.Removed;
		else if (showcase.Count >= ShowcaseSize)
			return ShowcaseChange.Full;
		else
		{
			showcase.Add(achievement);
			change = ShowcaseChange.Added;
		}

		showcaseUnsaved = true;
		return change;
	}

	internal Dictionary<string, int>? TakeUnsavedKills()
	{
		if (unsavedWeapons.Count == 0)
			return null;

		Dictionary<string, int> changed = unsavedWeapons.ToDictionary(weapon => weapon, weapon => weaponKills[weapon], StringComparer.Ordinal);
		unsavedWeapons.Clear();
		return changed;
	}

	internal string? TakeUnsavedShowcase()
	{
		if (!showcaseUnsaved)
			return null;

		showcaseUnsaved = false;
		return string.Join(',', showcase);
	}
}
