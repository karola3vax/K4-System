namespace K4System.Tests.Storage;

using K4System.Ranks;
using K4System.Storage;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Regression tests for issues found in the 6.0 independent review.</summary>
public sealed class ReviewFixTests
{
	[Fact]
	public void Rank_points_written_as_decimals_still_load()
	{
		List<RankDefinition> ranks = RankFile.Parse("""
			{
				"None": { "Name": "None", "Point": -1.0, "Color": "default" },
				"Silver": { "Name": "Silver", "Point": 500.0, "Color": "silver" }
			}
			""");

		Assert.Equal([-1, 500], ranks.Select(rank => rank.Point));
		Assert.Throws<InvalidDataException>(() => RankFile.Parse("""{ "None": { "Name": "None", "Point": 1.5, "Color": "default" } }"""));
	}

	[Fact]
	public void Index_names_stay_within_the_MySQL_limit()
	{
		Assert.Equal("idx_k4ranks_points", SqlPlayerStore.IndexName("k4ranks", "points"));

		string longTable = new string('p', 60) + "k4ranks";
		string name = SqlPlayerStore.IndexName(longTable, "points");
		Assert.True(name.Length <= 64);
		Assert.Equal(name, SqlPlayerStore.IndexName(longTable, "points"));
	}

	[Fact]
	public async Task Unloading_after_a_failed_save_releases_every_waiter()
	{
		SaveQueue queue = new(_ => throw new InvalidOperationException("database down"), NullLogger.Instance);

		Task<bool> first = queue.Enqueue(new PlayerSnapshot(1, "A", "Rank", 0, 1, null, null, null));
		Assert.False(await first);

		Task<bool> second = queue.Enqueue(new PlayerSnapshot(2, "B", "Rank", 0, 1, null, null, null));
		Task<bool> flush = queue.FlushAsync(1);

		Assert.True(queue.Complete(TimeSpan.FromSeconds(20)));
		Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(1)));
		Assert.False(await flush.WaitAsync(TimeSpan.FromSeconds(1)));
	}
}
