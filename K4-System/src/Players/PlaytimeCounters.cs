namespace K4System.Players;

using System.Text;

/// <summary>Playtime columns in k4times. The order matches <see cref="PlaytimeColumns.Names"/>.</summary>
public enum PlaytimeField
{
	All,
	Ct,
	T,
	Spec,
	Alive,
	Dead
}

public static class PlaytimeColumns
{
	public static readonly IReadOnlyList<string> Names = ["all", "ct", "t", "spec", "alive", "dead"];
}

/// <summary>What a running counter is measured from.</summary>
public enum PlaytimeAnchor
{
	Connect,
	Team,
	Death
}

/// <summary>Stored playtime in seconds, plus the UTC moments the running counters started.</summary>
public sealed class PlaytimeCounters
{
	private readonly int[] seconds;
	private readonly DateTime[] anchors = new DateTime[3];

	public PlaytimeCounters(int[] seconds, DateTime nowUtc)
	{
		if (seconds.Length != PlaytimeColumns.Names.Count)
			throw new ArgumentException($"Expected {PlaytimeColumns.Names.Count} playtime values.", nameof(seconds));

		this.seconds = seconds;
		RestartAnchors(nowUtc);
	}

	public int this[PlaytimeField field] => seconds[(int)field];

	public DateTime AnchorOf(PlaytimeAnchor anchor) => anchors[(int)anchor];

	/// <summary>Adds the time since the anchor to the field, then moves the anchor to now.</summary>
	public void AddElapsed(PlaytimeField field, PlaytimeAnchor anchor, DateTime nowUtc)
	{
		long elapsed = Math.Max(0L, (long)(nowUtc - anchors[(int)anchor]).TotalSeconds);
		seconds[(int)field] = (int)Math.Clamp((long)seconds[(int)field] + elapsed, 0, int.MaxValue);
		anchors[(int)anchor] = nowUtc;
	}

	public void SetAnchor(PlaytimeAnchor anchor, DateTime nowUtc) => anchors[(int)anchor] = nowUtc;

	/// <summary>
	/// True from a death (or joining a team) until the next spawn. CS2 respawns survivors at every round start,
	/// so a spawn alone does not mean the time before it was spent dead.
	/// </summary>
	public bool WaitingToSpawn { get; set; }

	public void RestartAnchors(DateTime nowUtc)
	{
		anchors[(int)PlaytimeAnchor.Connect] = nowUtc;
		anchors[(int)PlaytimeAnchor.Team] = nowUtc;
		anchors[(int)PlaytimeAnchor.Death] = nowUtc;
	}

	public void Reset(DateTime nowUtc)
	{
		Array.Clear(seconds);
		RestartAnchors(nowUtc);
	}

	public int[] ToArray() => (int[])seconds.Clone();

	public static PlaytimeField FieldForTeam(int team) => team switch
	{
		2 => PlaytimeField.T,
		3 => PlaytimeField.Ct,
		_ => PlaytimeField.Spec
	};

	/// <summary>Formats seconds as "1d, 2h, 5m" using the localized unit suffixes. <paramref name="units"/> limits it to that
	/// many units from the largest one, so 2 gives "2d, 23h" for a panel tile.</summary>
	public static string Format(int totalSeconds, Func<string, string> unitSuffix, int units = 6)
	{
		string[] names = ["year", "month", "day", "hour", "minute", "second"];
		int[] divisors = [31536000, 2592000, 86400, 3600, 60, 1];
		StringBuilder builder = new();
		int remaining = Math.Max(0, totalSeconds);
		int last = names.Length - 1;

		for (int i = 0; i <= last; i++)
		{
			int value = remaining / divisors[i];
			remaining %= divisors[i];

			if (value <= 0)
				continue;

			if (builder.Length == 0)
				last = Math.Min(last, i + units - 1);

			if (builder.Length > 0)
				builder.Append(", ");
			builder.Append(value).Append(unitSuffix(names[i]));
		}

		return builder.Length > 0 ? builder.ToString() : "0" + unitSuffix("second");
	}
}
