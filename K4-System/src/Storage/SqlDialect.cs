namespace K4System.Storage;

using System.Data.Common;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using MySqlConnector;

/// <summary>Table names for one table prefix.</summary>
internal sealed record TableNames(string Prefix, string LevelRanks)
{
	public string Times => $"{Prefix}k4times";
	public string Stats => $"{Prefix}k4stats";
	public string Ranks => $"{Prefix}k4ranks";
	public string Rewards => $"{Prefix}k4rankrewards";
	public string Resets => $"{Prefix}k4rankresets";
	public string Schema => $"{Prefix}k4schema";
	public string Missions => $"{Prefix}k4missions";
	public string MissionState => $"{Prefix}k4missionstate";
	public string PointLog => $"{Prefix}k4pointlog";
	public string Achievements => $"{Prefix}k4achievements";
	public string Seasons => $"{Prefix}k4seasons";
	public string SeasonState => $"{Prefix}k4seasonstate";
	public string WeaponKills => $"{Prefix}k4weaponkills";
	public string Profiles => $"{Prefix}k4profiles";
}

/// <summary>
/// The SQL that differs between MySQL and SQLite. Both accept backtick-quoted identifiers,
/// so shared statements are written once in <see cref="SqlPlayerStore"/>.
/// </summary>
internal abstract class SqlDialect
{
	public abstract string Name { get; }

	public abstract DbConnection CreateConnection();

	/// <summary>Called once at startup, before any other statement.</summary>
	public virtual Task PrepareAsync(DbConnection connection) => Task.CompletedTask;

	public abstract string InsertIgnore { get; }

	/// <summary>The clause between the VALUES list and the update assignments of an upsert.</summary>
	public abstract string UpsertClause(string keyColumns);

	public abstract string UnixTimestampNow { get; }

	/// <summary>The larger of two values: GREATEST in MySQL, the two-argument MAX in SQLite.</summary>
	public abstract string Greatest { get; }

	/// <summary>A database-clock time @Seconds seconds ago, comparable with columns filled by CURRENT_TIMESTAMP.</summary>
	public abstract string SecondsAgo { get; }

	/// <summary>True when the exception says the column already exists (another server added it first).</summary>
	public virtual bool IsDuplicateColumn(Exception ex) => false;

	/// <summary>True for rows last seen more than @Days days ago. Never-set dates are kept.</summary>
	public abstract string OlderThanDays(string column);

	public abstract string LevelRanksOlderThanDays { get; }

	public abstract IEnumerable<string> CreateTables(TableNames tables);

	public abstract string CreateLevelRanksTable(string table);

	public abstract string CreateSchemaTable(string table);

	public abstract string AddRewardExecutedColumn(string table);

	public abstract Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column);

	public abstract Task<bool> IndexExistsAsync(DbConnection connection, string table, string index);

	public abstract string CreateIndex(string table, string index, string column);

	public abstract DateTime? ReadUtcDateTime(object? value);
}

internal sealed class MySqlDialect(string connectionString, string databaseName) : SqlDialect
{
	public override string Name => "MySQL";

	public override DbConnection CreateConnection() => new MySqlConnection(connectionString);

	public override string InsertIgnore => "INSERT IGNORE INTO";

	public override string UpsertClause(string keyColumns) => "ON DUPLICATE KEY UPDATE";

	public override string UnixTimestampNow => "UNIX_TIMESTAMP()";

	public override string Greatest => "GREATEST";

	public override string SecondsAgo => "CURRENT_TIMESTAMP - INTERVAL @Seconds SECOND";

	public override bool IsDuplicateColumn(Exception ex) => ex is MySqlConnector.MySqlException { Number: 1060 };

	public override string OlderThanDays(string column)
		=> $"`{column}` < NOW() - INTERVAL @Days DAY AND `{column}` > STR_TO_DATE('1970-01-01', '%Y-%m-%d')";

	public override string LevelRanksOlderThanDays => "`lastconnect` < UNIX_TIMESTAMP(NOW() - INTERVAL @Days DAY)";

	// These definitions are the ones earlier versions created. Existing tables are adopted as they are.
	// They also work on old servers: MySQL before 5.6.5 has no CURRENT_TIMESTAMP default for DATETIME
	// (the reward claim sets claimed_at itself), and an index on a utf8mb4 column stays within
	// 767 bytes (191 characters) for servers without large index prefixes.
	public override IEnumerable<string> CreateTables(TableNames tables)
	{
		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Times}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
			`all` INT NOT NULL DEFAULT 0,
			`ct` INT NOT NULL DEFAULT 0,
			`t` INT NOT NULL DEFAULT 0,
			`spec` INT NOT NULL DEFAULT 0,
			`dead` INT NOT NULL DEFAULT 0,
			`alive` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		string statColumns = string.Join(",\n", Players.StatColumns.Names.Select(column => $"\t\t\t`{column}` INT NOT NULL DEFAULT 0"));
		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Stats}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
{statColumns}
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Ranks}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
			`rank` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`points` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Rewards}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`reward_id` VARCHAR(64) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`rank_name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`command` TEXT COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`claimed_at` DATETIME NOT NULL,
			`executed_at` DATETIME NULL,
			PRIMARY KEY (`steam_id`, `reward_id`),
			INDEX `idx_rank_name` (`rank_name`(191))
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Resets}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`last_reset_at` DATETIME NOT NULL
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Missions}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`day` CHAR(10) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`slot` INT NOT NULL,
			`mission_id` VARCHAR(64) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`progress` INT NOT NULL DEFAULT 0,
			`completed` TINYINT NOT NULL DEFAULT 0,
			PRIMARY KEY (`steam_id`, `day`, `slot`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.MissionState}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`streak` INT NOT NULL DEFAULT 0,
			`last_completed_day` CHAR(10) COLLATE 'utf8mb4_unicode_ci' NULL,
			`reroll_day` CHAR(10) COLLATE 'utf8mb4_unicode_ci' NULL,
			`rerolls_used` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.PointLog}` (
			`id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`changed_at` DATETIME NOT NULL,
			`delta` INT NOT NULL,
			`points_after` INT NOT NULL,
			`source` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`actor` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			INDEX `idx_steam_changed` (`steam_id`, `changed_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Achievements}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`achievement_id` VARCHAR(64) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`unlocked_at` DATETIME NOT NULL,
			PRIMARY KEY (`steam_id`, `achievement_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		// One row per player and season. points and rank stay NULL while the season runs and get the final values when it ends.
		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Seasons}` (
			`season` CHAR(10) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`peak_points` INT NOT NULL DEFAULT 0,
			`points` INT NULL,
			`rank` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NULL,
			PRIMARY KEY (`season`, `steam_id`),
			INDEX `idx_season_points` (`season`, `points`),
			INDEX `idx_steam` (`steam_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.SeasonState}` (
			`id` INT PRIMARY KEY NOT NULL,
			`season` CHAR(10) COLLATE 'utf8mb4_unicode_ci' NOT NULL
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.WeaponKills}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`weapon` VARCHAR(64) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`kills` INT NOT NULL DEFAULT 0,
			PRIMARY KEY (`steam_id`, `weapon`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Profiles}` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`showcase` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL DEFAULT ''
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
	}

	public override string CreateLevelRanksTable(string table) => $@"CREATE TABLE IF NOT EXISTS `{table}` (
		`steam` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY,
		`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci',
		`value` INT NOT NULL DEFAULT 0,
		`rank` INT NOT NULL DEFAULT 0,
		`kills` INT NOT NULL DEFAULT 0,
		`deaths` INT NOT NULL DEFAULT 0,
		`shoots` INT NOT NULL DEFAULT 0,
		`hits` INT NOT NULL DEFAULT 0,
		`headshots` INT NOT NULL DEFAULT 0,
		`assists` INT NOT NULL DEFAULT 0,
		`round_win` INT NOT NULL DEFAULT 0,
		`round_lose` INT NOT NULL DEFAULT 0,
		`playtime` INT NOT NULL DEFAULT 0,
		`lastconnect` INT NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

	public override string CreateSchemaTable(string table) => $@"CREATE TABLE IF NOT EXISTS `{table}` (
		`id` INT PRIMARY KEY NOT NULL,
		`version` INT NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

	public override string AddRewardExecutedColumn(string table)
		=> $"ALTER TABLE `{table}` ADD COLUMN `executed_at` DATETIME NULL AFTER `claimed_at`;";

	public override async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column)
	{
		int count = await connection.QuerySingleAsync<int>(
			"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @DatabaseName AND TABLE_NAME = @Table AND COLUMN_NAME = @Column;",
			new { DatabaseName = databaseName, Table = table, Column = column });
		return count > 0;
	}

	public override async Task<bool> IndexExistsAsync(DbConnection connection, string table, string index)
	{
		int count = await connection.QuerySingleAsync<int>(
			"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = @DatabaseName AND TABLE_NAME = @Table AND INDEX_NAME = @Index;",
			new { DatabaseName = databaseName, Table = table, Index = index });
		return count > 0;
	}

	public override string CreateIndex(string table, string index, string column)
		=> $"CREATE INDEX `{index}` ON `{table}` (`{column}`);";

	public override DateTime? ReadUtcDateTime(object? value)
		=> value is DateTime dateTime ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc) : null;
}

internal sealed class SqliteDialect(string filePath) : SqlDialect
{
	// Without Microsoft's SQLite bundle nothing selects the native library, so K4 does it once here.
	static SqliteDialect() => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

	private readonly string connectionString = new SqliteConnectionStringBuilder
	{
		DataSource = filePath,
		Mode = SqliteOpenMode.ReadWriteCreate,
		Cache = SqliteCacheMode.Private,
		Pooling = true,
		DefaultTimeout = 30
	}.ToString();

	public override string Name => "SQLite";

	public override DbConnection CreateConnection() => new SqliteConnection(connectionString);

	public override async Task PrepareAsync(DbConnection connection)
	{
		// WAL lets the game thread's reads continue while a save is being written.
		await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
	}

	public override string InsertIgnore => "INSERT OR IGNORE INTO";

	public override string UpsertClause(string keyColumns) => $"ON CONFLICT({keyColumns}) DO UPDATE SET";

	public override string UnixTimestampNow => "CAST(strftime('%s', 'now') AS INTEGER)";

	public override string Greatest => "MAX";

	public override string SecondsAgo => "datetime('now', (-@Seconds) || ' seconds')";

	public override bool IsDuplicateColumn(Exception ex) => ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase);

	public override string OlderThanDays(string column)
		=> $"`{column}` < datetime('now', '-' || @Days || ' days') AND `{column}` > '1970-01-01'";

	public override string LevelRanksOlderThanDays => "`lastconnect` < CAST(strftime('%s', 'now', '-' || @Days || ' days') AS INTEGER)";

	public override IEnumerable<string> CreateTables(TableNames tables)
	{
		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Times}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`name` TEXT NOT NULL,
			`lastseen` TEXT NOT NULL,
			`all` INTEGER NOT NULL DEFAULT 0,
			`ct` INTEGER NOT NULL DEFAULT 0,
			`t` INTEGER NOT NULL DEFAULT 0,
			`spec` INTEGER NOT NULL DEFAULT 0,
			`dead` INTEGER NOT NULL DEFAULT 0,
			`alive` INTEGER NOT NULL DEFAULT 0
		);";

		string statColumns = string.Join(",\n", Players.StatColumns.Names.Select(column => $"\t\t\t`{column}` INTEGER NOT NULL DEFAULT 0"));
		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Stats}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`name` TEXT NOT NULL,
			`lastseen` TEXT NOT NULL,
{statColumns}
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Ranks}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`name` TEXT NOT NULL,
			`lastseen` TEXT NOT NULL,
			`rank` TEXT NOT NULL,
			`points` INTEGER NOT NULL DEFAULT 0
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Rewards}` (
			`steam_id` TEXT NOT NULL,
			`reward_id` TEXT NOT NULL,
			`rank_name` TEXT NOT NULL,
			`command` TEXT NOT NULL,
			`claimed_at` TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
			`executed_at` TEXT NULL,
			PRIMARY KEY (`steam_id`, `reward_id`)
		);";

		yield return $"CREATE INDEX IF NOT EXISTS `idx_{tables.Rewards}_rank_name` ON `{tables.Rewards}` (`rank_name`);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Resets}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`last_reset_at` TEXT NOT NULL
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Missions}` (
			`steam_id` TEXT NOT NULL,
			`day` TEXT NOT NULL,
			`slot` INTEGER NOT NULL,
			`mission_id` TEXT NOT NULL,
			`progress` INTEGER NOT NULL DEFAULT 0,
			`completed` INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (`steam_id`, `day`, `slot`)
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.MissionState}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`streak` INTEGER NOT NULL DEFAULT 0,
			`last_completed_day` TEXT NULL,
			`reroll_day` TEXT NULL,
			`rerolls_used` INTEGER NOT NULL DEFAULT 0
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.PointLog}` (
			`id` INTEGER PRIMARY KEY AUTOINCREMENT,
			`steam_id` TEXT NOT NULL,
			`changed_at` TEXT NOT NULL,
			`delta` INTEGER NOT NULL,
			`points_after` INTEGER NOT NULL,
			`source` TEXT NOT NULL,
			`actor` TEXT NOT NULL
		);";

		yield return $"CREATE INDEX IF NOT EXISTS `idx_{tables.PointLog}_steam` ON `{tables.PointLog}` (`steam_id`, `changed_at`);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Achievements}` (
			`steam_id` TEXT NOT NULL,
			`achievement_id` TEXT NOT NULL,
			`unlocked_at` TEXT NOT NULL,
			PRIMARY KEY (`steam_id`, `achievement_id`)
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Seasons}` (
			`season` TEXT NOT NULL,
			`steam_id` TEXT NOT NULL,
			`name` TEXT NOT NULL,
			`peak_points` INTEGER NOT NULL DEFAULT 0,
			`points` INTEGER NULL,
			`rank` TEXT NULL,
			PRIMARY KEY (`season`, `steam_id`)
		);";

		yield return $"CREATE INDEX IF NOT EXISTS `idx_{tables.Seasons}_points` ON `{tables.Seasons}` (`season`, `points`);";
		yield return $"CREATE INDEX IF NOT EXISTS `idx_{tables.Seasons}_steam` ON `{tables.Seasons}` (`steam_id`);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.SeasonState}` (
			`id` INTEGER PRIMARY KEY NOT NULL,
			`season` TEXT NOT NULL
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.WeaponKills}` (
			`steam_id` TEXT NOT NULL,
			`weapon` TEXT NOT NULL,
			`kills` INTEGER NOT NULL DEFAULT 0,
			PRIMARY KEY (`steam_id`, `weapon`)
		);";

		yield return $@"CREATE TABLE IF NOT EXISTS `{tables.Profiles}` (
			`steam_id` TEXT PRIMARY KEY NOT NULL,
			`showcase` TEXT NOT NULL DEFAULT ''
		);";
	}

	public override string CreateLevelRanksTable(string table) => $@"CREATE TABLE IF NOT EXISTS `{table}` (
		`steam` TEXT PRIMARY KEY,
		`name` TEXT,
		`value` INTEGER NOT NULL DEFAULT 0,
		`rank` INTEGER NOT NULL DEFAULT 0,
		`kills` INTEGER NOT NULL DEFAULT 0,
		`deaths` INTEGER NOT NULL DEFAULT 0,
		`shoots` INTEGER NOT NULL DEFAULT 0,
		`hits` INTEGER NOT NULL DEFAULT 0,
		`headshots` INTEGER NOT NULL DEFAULT 0,
		`assists` INTEGER NOT NULL DEFAULT 0,
		`round_win` INTEGER NOT NULL DEFAULT 0,
		`round_lose` INTEGER NOT NULL DEFAULT 0,
		`playtime` INTEGER NOT NULL DEFAULT 0,
		`lastconnect` INTEGER NOT NULL DEFAULT 0
	);";

	public override string CreateSchemaTable(string table) => $@"CREATE TABLE IF NOT EXISTS `{table}` (
		`id` INTEGER PRIMARY KEY NOT NULL,
		`version` INTEGER NOT NULL
	);";

	public override string AddRewardExecutedColumn(string table)
		=> $"ALTER TABLE `{table}` ADD COLUMN `executed_at` TEXT NULL;";

	public override async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column)
	{
		long count = await connection.QuerySingleAsync<long>(
			"SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column;",
			new { Table = table, Column = column });
		return count > 0;
	}

	public override async Task<bool> IndexExistsAsync(DbConnection connection, string table, string index)
	{
		long count = await connection.QuerySingleAsync<long>(
			"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND tbl_name = @Table AND name = @Index;",
			new { Table = table, Index = index });
		return count > 0;
	}

	public override string CreateIndex(string table, string index, string column)
		=> $"CREATE INDEX IF NOT EXISTS `{index}` ON `{table}` (`{column}`);";

	public override DateTime? ReadUtcDateTime(object? value)
	{
		return value switch
		{
			DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
			string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed) => parsed,
			_ => null
		};
	}
}
