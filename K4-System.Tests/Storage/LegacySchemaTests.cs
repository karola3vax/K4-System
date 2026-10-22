namespace K4System.Tests.Storage;

using K4System.Players;
using K4System.Storage;

/// <summary>
/// An existing MySQL database from K4-System 5.0 must keep working with no manual steps.
/// The table definitions below are copied from the 5.0 release.
/// </summary>
public sealed class LegacySchemaTests
{
	private const ulong Veteran = 76561198000000042;

	private static string[] Version50Tables(string prefix) =>
	[
		$@"CREATE TABLE `{prefix}k4times` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
			`all` INT NOT NULL DEFAULT 0,
			`ct` INT NOT NULL DEFAULT 0,
			`t` INT NOT NULL DEFAULT 0,
			`spec` INT NOT NULL DEFAULT 0,
			`dead` INT NOT NULL DEFAULT 0,
			`alive` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",
		$@"CREATE TABLE `{prefix}k4stats` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
			`kills` INT NOT NULL DEFAULT 0, `firstblood` INT NOT NULL DEFAULT 0, `deaths` INT NOT NULL DEFAULT 0,
			`assists` INT NOT NULL DEFAULT 0, `shoots` INT NOT NULL DEFAULT 0, `hits_taken` INT NOT NULL DEFAULT 0,
			`hits_given` INT NOT NULL DEFAULT 0, `headshots` INT NOT NULL DEFAULT 0, `chest_hits` INT NOT NULL DEFAULT 0,
			`stomach_hits` INT NOT NULL DEFAULT 0, `left_arm_hits` INT NOT NULL DEFAULT 0, `right_arm_hits` INT NOT NULL DEFAULT 0,
			`left_leg_hits` INT NOT NULL DEFAULT 0, `right_leg_hits` INT NOT NULL DEFAULT 0, `neck_hits` INT NOT NULL DEFAULT 0,
			`unused_hits` INT NOT NULL DEFAULT 0, `gear_hits` INT NOT NULL DEFAULT 0, `special_hits` INT NOT NULL DEFAULT 0,
			`grenades` INT NOT NULL DEFAULT 0, `mvp` INT NOT NULL DEFAULT 0, `round_win` INT NOT NULL DEFAULT 0,
			`round_lose` INT NOT NULL DEFAULT 0, `game_win` INT NOT NULL DEFAULT 0, `game_lose` INT NOT NULL DEFAULT 0,
			`rounds_overall` INT NOT NULL DEFAULT 0, `rounds_ct` INT NOT NULL DEFAULT 0, `rounds_t` INT NOT NULL DEFAULT 0,
			`bomb_planted` INT NOT NULL DEFAULT 0, `bomb_defused` INT NOT NULL DEFAULT 0, `hostage_rescued` INT NOT NULL DEFAULT 0,
			`hostage_killed` INT NOT NULL DEFAULT 0, `noscope_kill` INT NOT NULL DEFAULT 0, `penetrated_kill` INT NOT NULL DEFAULT 0,
			`thrusmoke_kill` INT NOT NULL DEFAULT 0, `flashed_kill` INT NOT NULL DEFAULT 0, `dominated_kill` INT NOT NULL DEFAULT 0,
			`revenge_kill` INT NOT NULL DEFAULT 0, `assist_flash` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",
		$@"CREATE TABLE `{prefix}k4ranks` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`lastseen` DATETIME NOT NULL,
			`rank` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`points` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",
		$@"CREATE TABLE `{prefix}k4rankrewards` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`reward_id` VARCHAR(64) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`rank_name` VARCHAR(255) COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`command` TEXT COLLATE 'utf8mb4_unicode_ci' NOT NULL,
			`claimed_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
			PRIMARY KEY (`steam_id`, `reward_id`),
			INDEX `idx_rank_name` (`rank_name`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",
		$@"CREATE TABLE `{prefix}k4rankresets` (
			`steam_id` VARCHAR(32) COLLATE 'utf8mb4_unicode_ci' PRIMARY KEY NOT NULL,
			`last_reset_at` DATETIME NOT NULL
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;"
	];

	[MySqlFact]
	public async Task Version_5_0_database_is_adopted_without_losing_data()
	{
		await using MySqlTestDatabase db = new();
		string prefix = db.Prefix;
		string id = Veteran.ToString();

		foreach (string statement in Version50Tables(prefix))
			await db.ExecuteAsync(statement);

		await db.ExecuteAsync($"INSERT INTO `{prefix}k4ranks` VALUES (@Id, 'Veteran', NOW(), 'Gold Nova I', 15500);", new { Id = id });
		await db.ExecuteAsync($"INSERT INTO `{prefix}k4stats` (`steam_id`, `name`, `lastseen`, `kills`, `assist_flash`) VALUES (@Id, 'Veteran', NOW(), 321, 4);", new { Id = id });
		await db.ExecuteAsync($"INSERT INTO `{prefix}k4times` (`steam_id`, `name`, `lastseen`, `all`, `t`) VALUES (@Id, 'Veteran', NOW(), 7200, 3600);", new { Id = id });
		await db.ExecuteAsync($"INSERT INTO `{prefix}k4rankrewards` (`steam_id`, `reward_id`, `rank_name`, `command`) VALUES (@Id, 'old-reward', 'Gold Nova I', 'say old');", new { Id = id });
		await db.ExecuteAsync($"INSERT INTO `{prefix}k4rankresets` VALUES (@Id, '2026-08-01 10:00:00');", new { Id = id });

		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();

		// Claims from before reward tracking existed count as already delivered.
		Assert.Empty(await store.GetPendingRewardsAsync(Veteran));
		Assert.False(await store.TryClaimRewardAsync(Veteran, "old-reward", "Gold Nova I", "say old"));

		PlayerRecord? record = await store.LoadOrCreateAsync(Veteran, "Veteran", "None", 0);
		Assert.NotNull(record);
		Assert.Equal(15500, record.Points);
		Assert.Equal(321, record.Stats[(int)StatField.Kills]);
		Assert.Equal(4, record.Stats[(int)StatField.AssistFlash]);
		Assert.Equal(7200, record.Playtime[(int)PlaytimeField.All]);
		Assert.Equal(3600, record.Playtime[(int)PlaytimeField.T]);
		Assert.Equal(new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), record.LastPersonalRankResetUtc);
		Assert.Equal("Gold Nova I", await db.ScalarAsync<string>($"SELECT `rank` FROM `{prefix}k4ranks` WHERE `steam_id` = @Id;", new { Id = id }));

		Assert.Equal(1, await db.ScalarAsync<int>(
			"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @Table AND INDEX_NAME = @Index;",
			new { Table = $"{prefix}k4ranks", Index = $"idx_{prefix}k4ranks_points" }));
	}

	[MySqlFact]
	public async Task Version_5_1_pending_rewards_stay_pending()
	{
		await using MySqlTestDatabase db = new();
		string prefix = db.Prefix;

		foreach (string statement in Version50Tables(prefix))
			await db.ExecuteAsync(statement);

		await db.ExecuteAsync($"ALTER TABLE `{prefix}k4rankrewards` ADD COLUMN `executed_at` DATETIME NULL AFTER `claimed_at`;");
		await db.ExecuteAsync($"INSERT INTO `{prefix}k4rankrewards` (`steam_id`, `reward_id`, `rank_name`, `command`) VALUES (@Id, 'pending', 'Gold', 'say pending');", new { Id = Veteran.ToString() });

		SqlPlayerStore store = db.CreateStore();
		await store.InitializeAsync();

		StoredReward reward = Assert.Single(await store.GetPendingRewardsAsync(Veteran));
		Assert.Equal("pending", reward.RewardId);
	}
}
