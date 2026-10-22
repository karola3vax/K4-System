namespace K4System.Missions;

public sealed class MissionProgress(MissionDefinition mission, int progress = 0, bool completed = false)
{
	public MissionDefinition Mission { get; set; } = mission;

	public int Progress { get; set; } = progress;

	public bool Completed { get; set; } = completed;

	/// <summary>Not started yet, so it may still be rerolled or swapped for free.</summary>
	public bool Untouched => !Completed && Progress == 0;
}

/// <summary>A player's missions for one day and week, plus their streak. Game thread only.</summary>
public sealed class PlayerMissions
{
	public required DateOnly Day { get; set; }

	/// <summary>The three daily missions: Easy, Medium, Hard.</summary>
	public required List<MissionProgress> Slots { get; set; }

	/// <summary>The Monday the weekly mission belongs to.</summary>
	public DateOnly WeekStart { get; set; }

	/// <summary>This week's mission. Null when missions.jsonc has no Weekly missions.</summary>
	public MissionProgress? Weekly { get; set; }

	public int RerollsUsed { get; set; }

	public int Streak { get; set; }

	public DateOnly? LastCompletedDay { get; set; }

	/// <summary>All three daily missions are done. The weekly mission does not count for the streak.</summary>
	public bool AllCompleted => Slots.All(slot => slot.Completed);

	/// <summary>Daily missions first, then the weekly one.</summary>
	public IEnumerable<MissionProgress> All => Weekly is null ? Slots : Slots.Append(Weekly);

	internal MissionSnapshot Snapshot() => new(
		MissionRules.FormatDay(Day),
		Slots.Select((slot, index) => new MissionSlotSnapshot(index, slot.Mission.Id, slot.Progress, slot.Completed)).ToArray(),
		RerollsUsed,
		Streak,
		LastCompletedDay is DateOnly last ? MissionRules.FormatDay(last) : null,
		MissionRules.FormatDay(WeekStart),
		Weekly is null ? null : new MissionSlotSnapshot(MissionRules.WeeklySlot, Weekly.Mission.Id, Weekly.Progress, Weekly.Completed));
}

internal sealed record MissionSlotSnapshot(int Slot, string MissionId, int Progress, bool Completed);

internal sealed record MissionSnapshot(
	string Day,
	IReadOnlyList<MissionSlotSnapshot> Slots,
	int RerollsUsed,
	int Streak,
	string? LastCompletedDay,
	string? Week = null,
	MissionSlotSnapshot? Weekly = null);

/// <summary>What the database holds for a player: today's rows, this week's row (if any), and their streak state.</summary>
internal sealed record MissionRecord(IReadOnlyList<MissionSlotSnapshot> Slots, int RerollsUsed, int Streak, string? LastCompletedDay, MissionSlotSnapshot? Weekly = null);
