namespace K4System.Players;

/// <summary>Every statistic K4 stores. The order matches <see cref="StatColumns.Names"/>.</summary>
public enum StatField
{
	Kills,
	FirstBlood,
	Deaths,
	Assists,
	Shoots,
	HitsTaken,
	HitsGiven,
	Headshots,
	ChestHits,
	StomachHits,
	LeftArmHits,
	RightArmHits,
	LeftLegHits,
	RightLegHits,
	NeckHits,
	UnusedHits,
	GearHits,
	SpecialHits,
	Grenades,
	Mvp,
	RoundWin,
	RoundLose,
	GameWin,
	GameLose,
	RoundsOverall,
	RoundsCt,
	RoundsT,
	BombPlanted,
	BombDefused,
	HostageRescued,
	HostageKilled,
	NoscopeKill,
	PenetratedKill,
	ThrusmokeKill,
	FlashedKill,
	DominatedKill,
	RevengeKill,
	AssistFlash
}

/// <summary>The single list of k4stats column names. The database contract depends on these names.</summary>
public static class StatColumns
{
	public static readonly IReadOnlyList<string> Names =
	[
		"kills", "firstblood", "deaths", "assists", "shoots", "hits_taken", "hits_given", "headshots",
		"chest_hits", "stomach_hits", "left_arm_hits", "right_arm_hits", "left_leg_hits", "right_leg_hits",
		"neck_hits", "unused_hits", "gear_hits", "special_hits", "grenades", "mvp", "round_win", "round_lose",
		"game_win", "game_lose", "rounds_overall", "rounds_ct", "rounds_t", "bomb_planted", "bomb_defused",
		"hostage_rescued", "hostage_killed", "noscope_kill", "penetrated_kill", "thrusmoke_kill", "flashed_kill",
		"dominated_kill", "revenge_kill", "assist_flash"
	];

	public static string Column(StatField field) => Names[(int)field];
}

public sealed class StatCounters
{
	private readonly int[] values;

	public StatCounters() : this(new int[StatColumns.Names.Count]) { }

	public StatCounters(int[] values)
	{
		if (values.Length != StatColumns.Names.Count)
			throw new ArgumentException($"Expected {StatColumns.Names.Count} stat values.", nameof(values));

		this.values = values;
	}

	public int this[StatField field] => values[(int)field];

	public void Increment(StatField field)
		=> values[(int)field] = values[(int)field] == int.MaxValue ? int.MaxValue : values[(int)field] + 1;

	public void Reset() => Array.Clear(values);

	public int[] ToArray() => (int[])values.Clone();

	/// <summary>Kills per death; with no deaths yet, the kills themselves (as LVL Ranks shows it).</summary>
	public static float KillDeathRatio(int kills, int deaths) => deaths > 0 ? (float)kills / deaths : kills;
}
