namespace K4System.Tests.Storage;

using System.Data.Common;
using Dapper;
using K4System.Storage;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>A disposable database for one test, with its own table prefix.</summary>
public abstract class TestDatabase : IAsyncDisposable
{
	protected TestDatabase()
	{
		Prefix = $"t{Guid.NewGuid():N}"[..13] + "_";
	}

	public string Prefix { get; }

	internal TableNames Tables => new(Prefix, Prefix + "lvl_base");

	internal abstract SqlDialect Dialect { get; }

	internal SqlPlayerStore CreateStore(bool levelRanks = false)
		=> new(Dialect, Tables, levelRanks, NullLogger.Instance);

	public async Task<DbConnection> OpenAsync()
	{
		DbConnection connection = Dialect.CreateConnection();
		await connection.OpenAsync();
		return connection;
	}

	public async Task<T> ScalarAsync<T>(string sql, object? parameters = null)
	{
		await using DbConnection connection = await OpenAsync();
		object? value = await connection.ExecuteScalarAsync(sql, parameters);
		return (T)Convert.ChangeType(value!, typeof(T));
	}

	public async Task ExecuteAsync(string sql, object? parameters = null)
	{
		await using DbConnection connection = await OpenAsync();
		await connection.ExecuteAsync(sql, parameters);
	}

	/// <summary>Moves a player's last-seen time into the past, for purge tests.</summary>
	public abstract Task AgeLastSeenAsync(string table, string steamId, int days);

	public abstract ValueTask DisposeAsync();
}

public sealed class SqliteTestDatabase : TestDatabase
{
	private readonly string path = Path.Combine(Path.GetTempPath(), $"k4-test-{Guid.NewGuid():N}.db");
	private SqliteDialect? dialect;

	internal override SqlDialect Dialect => dialect ??= new SqliteDialect(path);

	public override Task AgeLastSeenAsync(string table, string steamId, int days)
		=> ExecuteAsync($"UPDATE `{table}` SET `lastseen` = datetime('now', '-{days} days') WHERE `steam_id` = @SteamId;", new { SteamId = steamId });

	public override ValueTask DisposeAsync()
	{
		Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
		foreach (string file in new[] { path, path + "-wal", path + "-shm" })
		{
			try
			{
				File.Delete(file);
			}
			catch (IOException)
			{
				// A leftover temp file is harmless.
			}
		}

		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// Uses the MySQL server named by the K4_TEST_MYSQL connection string. Tests using it are skipped
/// when the variable is not set.
/// </summary>
public sealed class MySqlTestDatabase : TestDatabase
{
	public static readonly string? ConnectionString = Environment.GetEnvironmentVariable("K4_TEST_MYSQL");

	private MySqlDialect? dialect;

	internal override SqlDialect Dialect
	{
		get
		{
			if (dialect is null)
			{
				MySqlConnector.MySqlConnectionStringBuilder builder = new(ConnectionString!);
				dialect = new MySqlDialect(builder.ConnectionString, builder.Database);
			}

			return dialect;
		}
	}

	public override Task AgeLastSeenAsync(string table, string steamId, int days)
		=> ExecuteAsync($"UPDATE `{table}` SET `lastseen` = NOW() - INTERVAL {days} DAY WHERE `steam_id` = @SteamId;", new { SteamId = steamId });

	public override async ValueTask DisposeAsync()
	{
		await using DbConnection connection = await OpenAsync();
		IEnumerable<string> tables = await connection.QueryAsync<string>(
			"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE @Prefix;",
			new { Prefix = Prefix + "%" });

		foreach (string table in tables)
			await connection.ExecuteAsync($"DROP TABLE IF EXISTS `{table}`;");
	}
}

public sealed class MySqlFactAttribute : FactAttribute
{
	public MySqlFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(MySqlTestDatabase.ConnectionString))
			Skip = "Set K4_TEST_MYSQL to a MySQL connection string to run MySQL tests.";
	}
}
