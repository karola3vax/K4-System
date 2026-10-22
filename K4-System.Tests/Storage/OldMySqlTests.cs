namespace K4System.Tests.Storage;

using K4System.Storage;

/// <summary>
/// The tables K4 creates must work on older MySQL servers too (6.2.2: MySQL 5.5 refused the
/// rank rewards table, and the tables after it were never created).
/// </summary>
public sealed class OldMySqlTests
{
	private static IEnumerable<string> Statements()
		=> new MySqlDialect("Server=unused", "unused").CreateTables(new TableNames("", "lvl_base"));

	[Fact]
	public void NoDatetimeColumnDefaultsToTheCurrentTime()
	{
		// MySQL before 5.6.5: "Invalid default value" for DATETIME ... DEFAULT CURRENT_TIMESTAMP.
		foreach (string statement in Statements())
			Assert.DoesNotContain("DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", statement, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TheRankNameIndexFitsSmallIndexPrefixes()
	{
		// Without large index prefixes an index holds at most 767 bytes: 191 utf8mb4 characters.
		string rewards = Statements().Single(statement => statement.Contains("k4rankrewards", StringComparison.Ordinal));
		Assert.Contains("(`rank_name`(191))", rewards, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("CREATE TABLE IF NOT EXISTS `k4rankresets` (", "k4rankresets")]
	[InlineData("CREATE INDEX IF NOT EXISTS `idx_x` ON `t` (`c`);", "idx_x")]
	[InlineData("SELECT 1;", "?")]
	public void StatementTargetNamesTheTable(string statement, string expected)
		=> Assert.Equal(expected, SqlPlayerStore.StatementTarget(statement));
}
