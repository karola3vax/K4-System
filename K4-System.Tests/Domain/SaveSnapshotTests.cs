namespace K4System.Tests.Domain;

using K4System.Storage;

public class SaveSnapshotTests
{
	private static readonly DateTime ResetUtc = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void SaveRetryPolicyIsBounded()
	{
		Assert.Equal([250, 1000, 3000, 3000], Enumerable.Range(1, 4).Select(SaveQueue.RetryDelay));
	}

	[Fact]
	public void FailedResetMetadataSurvivesLaterSnapshots()
	{
		PlayerSnapshot reset = new(1, "Old", "None", -1, 0, null, null, ResetUtc);
		PlayerSnapshot later = new(1, "New", "Gold", 1, 100, null, null, null);

		PlayerSnapshot merged = later.MergeOnto(reset);

		Assert.Equal(ResetUtc, merged.PersonalRankResetUtc);
		Assert.Equal("New", merged.Name);
		Assert.Equal("Gold", merged.RankName);
		Assert.Equal(100, merged.Points);
	}

	[Fact]
	public void NewerResetReplacesOlderReset()
	{
		DateTime newerReset = ResetUtc.AddHours(1);
		PlayerSnapshot older = new(1, "Old", "None", -1, 0, null, null, ResetUtc);
		PlayerSnapshot newer = new(1, "New", "None", -1, 0, null, null, newerReset);

		Assert.Equal(newerReset, newer.MergeOnto(older).PersonalRankResetUtc);
	}

	[Fact]
	public void MergeWithoutPendingResetKeepsNewerSnapshot()
	{
		PlayerSnapshot older = new(1, "Old", "None", -1, 0, null, null, null);
		PlayerSnapshot newer = new(1, "New", "Gold", 1, 100, null, null, null);

		Assert.Same(newer, newer.MergeOnto(older));
		Assert.Same(newer, newer.MergeOnto(null));
	}
}
