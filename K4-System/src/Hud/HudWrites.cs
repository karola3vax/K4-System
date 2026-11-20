namespace K4System.Hud;

using CounterStrikeSharp.API.Core;

/// <summary>
/// What the layout entity already holds per player slot, and the one place panel writes go out. Every write searches
/// and changes the entity's networked vectors, which hold every player's panel, so a redraw sends only the texts and
/// classes that changed. Classes are kept in groups (one bar width, one icon, shown or hidden) that start at the
/// layout's default (docs/panel-contract.md), so a part that is still as the layout built it costs no write.
/// Forget a slot (or everything) whenever the entity's state for it may differ: a new entity, a new player in the slot.
/// </summary>
/// <param name="sendVar">Writes one dialog variable to the entity; the only seam a test replaces.</param>
/// <param name="sendClass">Switches one class on the entity.</param>
internal sealed class HudWrites(Action<CCSPlayerController?, string, string, string> sendVar, Action<CCSPlayerController?, string, string, bool> sendClass)
{
	private readonly Dictionary<int, Dictionary<(string Panel, string Name), string>> vars = [];
	private readonly Dictionary<int, Dictionary<(string Panel, string Name), bool>> classes = [];

	// The class applied per panel and group; "" is none. A missing entry is the layout's default.
	private readonly Dictionary<int, Dictionary<(string Panel, string Group), string>> groups = [];

	// Who the remembered state of each slot was drawn for. The entity keeps a slot's classes when its player leaves.
	private readonly Dictionary<int, ulong> owners = [];

	public HudWrites() : this((_, _, _, _) => { }, (_, _, _, _) => { })
	{
	}

	public long Sent { get; private set; }

	public long Skipped { get; private set; }

	/// <summary>True when the text must be sent; it is then remembered as sent.</summary>
	public bool Var(int slot, string panel, string name, string value)
		=> Changed(vars, slot, (panel, name), value);

	/// <summary>True when the class must be switched; it is then remembered as switched.</summary>
	public bool Class(int slot, string panel, string name, bool on)
		=> Changed(classes, slot, (panel, name), on);

	public void SetVar(int slot, CCSPlayerController? controller, string panel, string name, string value)
	{
		if (Var(slot, panel, name, value))
			Send(() => sendVar(controller, panel, name, value), () => vars[slot].Remove((panel, name)));
	}

	public void SetClass(int slot, CCSPlayerController? controller, string panel, string name, bool on)
	{
		if (Class(slot, panel, name, on))
			Send(() => sendClass(controller, panel, name, on), () => classes[slot].Remove((panel, name)));
	}

	/// <summary>
	/// Marks the slot as drawn for this player. When the slot still holds what was drawn for someone else (a disconnect
	/// the server could not tie to a player), it is first put back to the layout's defaults, so the new player never
	/// starts with the old player's screens, rows or bar widths showing.
	/// </summary>
	public void Claim(int slot, ulong owner, CCSPlayerController? controller)
	{
		if (owners.TryGetValue(slot, out ulong previous) && previous != owner)
			Reset(slot, controller);
		owners[slot] = owner;
	}

	/// <summary>Applies one class out of a group (bar width, color, icon, hidden), sending only what changed.</summary>
	public void Swap(int slot, CCSPlayerController? controller, string panel, string group, string? wanted)
	{
		if (!groups.TryGetValue(slot, out Dictionary<(string, string), string>? mine))
			groups[slot] = mine = [];

		string? current = mine.TryGetValue((panel, group), out string? stored) ? NoneIfEmpty(stored) : DefaultClass(group);
		if (current == wanted)
			return;

		if (current is not null)
			SetClass(slot, controller, panel, current, false);
		if (wanted is not null)
			SetClass(slot, controller, panel, wanted, true);
		mine[(panel, group)] = wanted ?? "";
	}

	/// <summary>
	/// Puts every class this slot changed back to the layout's default, then forgets the slot, so the next player in
	/// it starts from the layout as built. Rows, screens, toasts and the open window all go back.
	/// </summary>
	public void Reset(int slot, CCSPlayerController? controller)
	{
		if (groups.TryGetValue(slot, out Dictionary<(string Panel, string Group), string>? mine))
		{
			foreach (((string panel, string group), string stored) in mine)
			{
				string? current = NoneIfEmpty(stored);
				string? fallback = DefaultClass(group);
				if (current == fallback)
					continue;
				if (current is not null)
					sendClass(controller, panel, current, false);
				if (fallback is not null)
					sendClass(controller, panel, fallback, true);
			}
		}

		Forget(slot);
	}

	/// <summary>The class a layout panel of this group starts with (docs/panel-contract.md), or null.</summary>
	internal static string? DefaultClass(string group) => group switch
	{
		"icon" => "sg-0",
		"bar" => "w0",
		"tier" => "t-locked",
		"toast" => "k4-tc-green",
		"length" => "k4-len-normal",
		HudPen.StartsGone => "k4-gone",
		"is:k4-closed" => "k4-closed",
		_ => null
	};

	public void Forget()
	{
		vars.Clear();
		classes.Clear();
		groups.Clear();
		owners.Clear();
	}

	public void Forget(int slot)
	{
		vars.Remove(slot);
		classes.Remove(slot);
		groups.Remove(slot);
		owners.Remove(slot);
	}

	private static string? NoneIfEmpty(string stored) => stored.Length == 0 ? null : stored;

	// A write that failed must not count as sent, or the next draw would skip it. Only that one write is unknown now:
	// forgetting the whole slot would treat classes the entity still holds as the layout's defaults.
	private static void Send(Action write, Action undo)
	{
		try
		{
			write();
		}
		catch
		{
			undo();
			throw;
		}
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

/// <summary>One player's writes to the panel: texts, state classes and class groups, each sent only when it changes.</summary>
internal readonly struct HudPen(HudWrites writes, int slot, CCSPlayerController? controller)
{
	/// <summary>The group of parts the layout starts hidden (`k4-gone`).</summary>
	public const string StartsGone = "starts-gone";

	private const string StartsShown = "starts-shown";

	public void Var(string panel, string name, string value) => writes.SetVar(slot, controller, panel, name, value);

	public void Swap(string panel, string group, string? wanted) => writes.Swap(slot, controller, panel, group, wanted);

	/// <summary>A state class such as k4-active. It starts off, except k4-closed on k4-panel.</summary>
	public void Flag(string panel, string name, bool on) => Swap(panel, "is:" + name, on ? name : null);

	/// <summary>
	/// A class sent the first time whatever its value, for parts whose start in the layout the contract leaves open.
	/// Not reset on disconnect: the next player's first draw sends it again.
	/// </summary>
	public void Set(string panel, string name, bool on) => writes.SetClass(slot, controller, panel, name, on);

	/// <summary>Shows or hides a part the layout starts hidden.</summary>
	public void Show(string panel, bool show) => Swap(panel, StartsGone, show ? null : "k4-gone");

	/// <summary>Hides or shows a part the layout starts visible.</summary>
	public void Gone(string panel, bool gone) => Swap(panel, StartsShown, gone ? "k4-gone" : null);

	/// <summary>A bar fill in 5% steps (w0 to w20).</summary>
	public void Bar(string panel, long value, long target) => Swap(panel, "bar", $"w{BarStep(value, target)}");

	internal static int BarStep(long value, long target) => target <= 0 ? 20 : (int)Math.Clamp(value * 20 / target, 0, 20);
}
