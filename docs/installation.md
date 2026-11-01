# Installation and updates

[Back to the README](../README.md)

## Quick install

### Requirements

| Component | Requirement |
| --- | --- |
| Game server | Counter-Strike 2 dedicated server |
| Plugin framework | [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp), built against and requiring API `1.0.376` or newer |
| Runtime | .NET `10` |
| Storage | MySQL-compatible database, or the built-in SQLite option |
| Optional GeoIP | `GeoLite2-Country.mmdb` for country tags, from [P3TERX/GeoLite.mmdb](https://github.com/P3TERX/GeoLite.mmdb/releases) |
| Optional panel | [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager) and the K4 Workshop addon `3811667212` |

### Install in six steps

1. Download the [current K4-System package](https://github.com/karola3vax/K4-System/releases).
2. Extract the ZIP into `game/csgo/addons/counterstrikesharp/` and keep its folder structure intact.
3. Start the server once. K4 creates `k4-system.jsonc`, `ranks.jsonc`, `missions.jsonc`, and `achievements.jsonc`, and runs on SQLite right away.
4. To use MySQL instead, set `storage.type` to `mysql` and fill in `storage.mysql`.
5. Pick a point preset and review modules, tags, scoreboard display, commands, and the rank ladder.
6. Restart K4-System and confirm that it loads without errors.

| File | Location |
| --- | --- |
| Main configuration | `addons/counterstrikesharp/configs/plugins/K4-System/k4-system.jsonc` |
| Rank ladder | `addons/counterstrikesharp/plugins/K4-System/ranks.jsonc` |
| Mission list | `addons/counterstrikesharp/plugins/K4-System/missions.jsonc` |
| Achievement list | `addons/counterstrikesharp/plugins/K4-System/achievements.jsonc` |
| Language files (replaced by updates) | `addons/counterstrikesharp/plugins/K4-System/lang/` |
| Your own wording (kept by updates) | `addons/counterstrikesharp/configs/plugins/K4-System/lang/` |
| Optional GeoIP database | `addons/counterstrikesharp/plugins/K4-System/GeoLite2-Country.mmdb` |

<details>
<summary><strong>Expected release package layout</strong></summary>

```text
addons/counterstrikesharp/
├── plugins/
│   └── K4-System/
│       ├── K4-System.dll
│       ├── Dapper.dll
│       ├── MaxMind.Db.dll
│       ├── MaxMind.GeoIP2.dll
│       ├── K4-System.deps.json
│       ├── Microsoft.Data.Sqlite.dll
│       ├── MySqlConnector.dll
│       ├── Microsoft.Extensions.*.dll
│       ├── SQLitePCLRaw.*.dll
│       ├── runtimes/
│       │   ├── linux-x64/native/libe_sqlite3.so
│       │   └── win-x64/native/e_sqlite3.dll
│       └── lang/
│           ├── en.json
│           ├── tr.json
│           └── ...
└── shared/
    └── K4-SharedApi/
        └── K4-SharedApi.dll
```

</details>

## Updating

1. Download the [newest release](https://github.com/karola3vax/K4-System/releases).
2. Extract it into `game/csgo/addons/counterstrikesharp/` over the old files. Do not delete `plugins/K4-System/` first: your `ranks.jsonc`, `missions.jsonc`, `achievements.jsonc`, `k4system.db`, and GeoIP file are there. The package also updates `shared/K4-SharedApi/` and the `lang` folder; your own wording in `configs/plugins/K4-System/lang/` stays as it is.
3. Remove K4-System-MMRanks and FakeRanks RevealAll if they were installed. FakeRanks is a Metamod plugin, so this needs a full server restart.
4. Restart the server.

### Updating to 7.0

7.0 adds monthly [seasons](seasons.md), on by default: on the 1st of each month everyone's points go back to the start. Updating itself resets nothing. The month you update in is the first season and keeps everyone's current points; the first reset is on the 1st of the next month. To keep points growing forever as before, set `modules.seasons` to `false` before that date. On a shared database, use the same setting on every server.

The new tables `k4seasons`, `k4seasonstate`, `k4weaponkills` and `k4profiles` are created on the next start. Existing tables and config files need no changes. For the Profile view and the season switch on the Top tab, players need the updated Workshop addon; servers and players on the old addon keep working without them.

### Upgrading from 5.x

<details>
<summary><strong>Only for servers still on K4-System 5.x</strong></summary>

Since 6.0, K4 uses a new config file and new language keys. Your database and `ranks.jsonc` keep working unchanged.

1. Install the current version and start the server once. K4 writes `configs/plugins/K4-System/k4-system.jsonc` and, because it finds your old `K4-System.json`, **does not start tracking yet**. The server log names both files.
2. Copy your settings into the new file. For MySQL set `storage.type` to `mysql` and fill in `storage.mysql`; use the same `table-prefix` as before.
3. Point values now come from a preset. `casual` equals the old defaults; put any values you had changed into `points.overrides`.
4. If you translated or edited language files, redo them from the new `lang/en.json`; the old keys are not read.
5. Restart. You can delete the old `K4-System.json` afterwards.

On its first start after 5.x, K4 adds a points index to the ranks table and a small `k4schema` table that records database upgrades. No data is changed.

| 5.x setting | 6.0 setting |
| --- | --- |
| `database-settings.*` | `storage.*` (`sslmode` is now `storage.mysql.ssl-mode`) |
| `general-settings.module_*`, `util-settings.admin-list-enable` | `modules.*` |
| `lvl-ranks-table`, `lvlranks-table-name` | `storage.levelranks.enabled`, `.table` |
| `table-purge-days` | `storage.purge-after-days` |
| `minimum-players`, `warmup-points` / `warmup-stats`, `points-for-bots` / `stats-for-bots`, `ffa-mode` | `tracking.*` (one value for points and stats) |
| `game-mode` | `ranks.mode` |
| `round-end-points` | `ranks.round-summary` |
| `playername-in-kill-messages` | `ranks.names-in-kill-messages` |
| `seconds-between-kills` | `ranks.killstreak-window-seconds` |
| `personal-rank-reset-cooldown-hours` | `ranks.reset-cooldown-hours` |
| `rank-based-team-balance*` | `ranks.team-balance.*` |
| `playtime-minutes`, `playtime-reward-afk` | `ranks.playtime-reward.every-minutes`, `.include-afk` |
| `point-settings.*` | `points.preset` + `points.overrides` |
| `scoreboard-rank-mode`, `scoreboard-custom-rank-*`, `scoreboard-score-sync` | `scoreboard.rank-display`, `.custom-icons.*`, `.score-shows-points` |
| `scoreboard-clantags`, `display-toplist-placement*`, `country-tag-before-rank`, `admin-settings` | `tags.*` |
| `spawn-message`, `connect-message-enable`, `disconnect-message-enable` | `messages.*` |
| `*-commands`, `expensive-command-cooldown` | `commands.*` |
| `load-messages` | removed |

</details>
