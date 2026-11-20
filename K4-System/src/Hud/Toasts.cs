namespace K4System.Hud;

/// <summary>The colored strip and sound of a toast: green for done, gold for rewards, red for warnings, blue for progress, purple for duels.</summary>
public enum ToastColor
{
	Green,
	Gold,
	Red,
	Blue,
	Purple
}

/// <summary>How long a toast stays: short for small progress, long for big moments such as an achievement.</summary>
public enum ToastLength
{
	Short,
	Normal,
	Long
}

/// <summary>
/// One pop-up under the kill feed. <paramref name="Kind"/>, <paramref name="Text"/> and <paramref name="Value"/> are
/// already in the reader's language. <paramref name="Visual"/> is one picture class from docs/panel-contract.md
/// (an icon, a weapon, a rank emblem or a team patch).
/// </summary>
/// <param name="Value">The reward or number on the right, such as "+80" or "6/10"; empty for none.</param>
/// <param name="BarPercent">0 to 100 for a progress bar under the text; null for none.</param>
/// <param name="Tier">"gold", "silver" or "bronze" draws the picture inside a medal ring; null for none.</param>
public sealed record Toast(
	string Kind,
	string Text,
	string Value,
	string Visual,
	ToastColor Color,
	ToastLength Length = ToastLength.Normal,
	int? BarPercent = null,
	string? Tier = null)
{
	public const int Slots = 3;

	public static float Seconds(ToastLength length) => length switch
	{
		ToastLength.Short => 2.5f,
		ToastLength.Long => 5f,
		_ => 3.5f
	};
}

/// <summary>A toast on screen or waiting for a free slot. Game thread only.</summary>
internal sealed class ToastEntry(Toast toast)
{
	public Toast Toast { get; } = toast;

	/// <summary>On screen with its timer running; false while it waits.</summary>
	public bool Showing { get; set; }

	/// <summary>Sliding out: still drawn in its slot until the slide-out ends.</summary>
	public bool Leaving { get; set; }

	/// <summary>Counts each start, so the timer of an earlier showing does nothing after the toast was put back to wait.</summary>
	public int Run { get; set; }
}
