# Achievements

[Back to the README](../README.md)

Achievements are long-term goals, such as 1000 kills or a 7-day mission streak. Each one unlocks once, for good, and gives its points (and an optional server command) once. Everyone in chat sees who unlocked what; with the panel, the player also gets a pop-up.

Progress comes from what K4 already stores: stats, playtime and the mission streak. Nothing new is counted, so progress starts with a player's first game, and a player who already has 5000 kills is not asked to start over.

Players type `!achievements` (or `!ach`) to see how many they have and the five closest to done.

## Turning them on or off

`modules.achievements` in `k4-system.jsonc` is `true` by default and needs the ranks module, because achievements give points. The command names are in `commands.achievements`.

## The list

The list is in `plugins/K4-System/achievements.jsonc`, written with 21 achievements on first start. Each entry:

```jsonc
"kills-1000": { "Type": "Kills", "Goal": 1000, "Points": 500 },
"vip-week": { "Type": "PlaytimeHours", "Goal": 50, "Points": 0, "Name": "Server Regular", "Command": "css_addvip {steamid} 7" }
```

| Field | Meaning |
| --- | --- |
| key | Stored in the database. Do not rename a key players may have unlocked |
| `Type` | What counts: `Kills`, `Headshots`, `Assists`, `Mvps`, `RoundWins`, `MatchWins`, `OpeningKills`, `BombPlants`, `BombDefuses`, `HostageRescues`, `NoscopeKills`, `WallbangKills`, `SmokeKills`, `BlindKills`, `RevengeKills`, `PlaytimeHours`, `MissionStreak` |
| `Goal` | The number to reach, 1 or more |
| `Points` | Points given once on unlock, 0 or more |
| `Command` | Optional server command run once on unlock. Placeholders: `{steamid}`, `{userid}`, `{name}`, `{rank}`, `{rankid}`, `{points}` |
| `Name` | Optional shown name. The shipped achievements have names in all 36 languages; your own need a `Name` |

The goal is described in each player's language from the type, such as "Get 1000 kills". Stat types need the stats module, `PlaytimeHours` the playtime module, and `MissionStreak` the missions module; without the module, those achievements never unlock.

After editing the file, run `css_k4_reload`. It checks the file first and names any mistake.

## Rules worth knowing

- **Already reached counts, without the reward.** A goal a player had already reached when the achievement was added, or when this version first loaded them, unlocks quietly without its points or command, and the player is told how many they already earned. Rewards are for reaching a goal while playing. This keeps one update from handing out thousands of points, or running every reward command at once, for history from before achievements existed.
- **Checked at round end, match end and when a player leaves**, so a goal reached in the last round still pays.
- **Saved with the points.** An unlock is written in the same database transaction as the points it gave, so the two cannot be stored apart.
- **Permanent.** `!resetmydata` and admin resets do not remove achievements. Inactive players purged after `storage.purge-after-days` lose theirs with the rest of their data.
- **Not part of point events.** A points event does not multiply achievement points.
