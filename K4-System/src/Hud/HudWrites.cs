namespace K4System.Hud;

/// <summary>
/// What the layout entity already holds per player slot. Every panel write searches and changes the entity's
/// networked vectors, which hold every player's panel, so a redraw sends only the texts and classes that changed.
/// Forget a slot (or everything) whenever the entity's state for it may differ: a new entity, a new player in the slot.
/// </summary>
internal sealed class HudWrites
{
	private readonly Dictionary<int, Dictionary<(string Panel, string Name), string>> vars = [];
	private readonly Dictionary<int, Dictionary<(string Panel, string Name), bool>> classes = [];

	public long Sent { get; private set; }

	public long Skipped { get; private set; }

	/// <summary>True when the text must be sent; it is then remembered as sent.</summary>
	public bool Var(int slot, string panel, string name, string value)
		=> Changed(vars, slot, (panel, name), value);

	/// <summary>True when the class must be switched; it is then remembered as switched.</summary>
	public bool Class(int slot, string panel, string name, bool on)
		=> Changed(classes, slot, (panel, name), on);

	public void Forget()
	{
		vars.Clear();
		classes.Clear();
	}

	public void Forget(int slot)
	{
		vars.Remove(slot);
		classes.Remove(slot);
	}

	private bool Changed<T>(Dictionary<int, Dictionary<(string, string), T>> all, int slot, (string, string) key, T value)
	{
		if (!all.TryGetValue(slot, out Dictionary<(string, string), T>? mine))
			all[slot] = mine = [];

		if (mine.TryGetValue(key, out T? current) && EqualityComparer<T>.Default.Equals(current, value))
		{
			Skipped++;
			return false;
		}

		mine[key] = value;
		Sent++;
		return true;
	}
}
