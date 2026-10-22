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

	/// <summary>Formats seconds as "1d, 2h, 5m" using the localized unit suffixes.</summary>
	public static string Format(int totalSeconds, Func<string, string> unitSuffix)
	{
		string[] units = ["year", "month", "day", "hour", "minute", "second"];
		int[] divisors = [31536000, 2592000, 86400, 3600, 60, 1];
		StringBuilder builder = new();
		int remaining = Math.Max(0, totalSeconds);

		for (int i = 0; i < units.Length; i++)
		{
			int value = remaining / divisors[i];
			remaining %= divisors[i];

			if (value <= 0)
				continue;

			if (builder.Length > 0)
				builder.Append(", ");
			builder.Append(value).Append(unitSuffix(units[i]));
		}

		return builder.Length > 0 ? builder.ToString() : "0" + unitSuffix("second");
	}
}
