# Missions

[Back to the README](../README.md)

Every day each player gets three missions: one easy, one medium, and one hard. Every week they also get one big weekly mission. Missions differ between players and stay the same all day (or all week), also after reconnecting or a map change. `!missions` shows them on a center-screen card and in chat with progress bars:

```text
1. Easy             Get 15 kills              ▰▰▰▰▰▱▱▱▱▱ 8/15 · +150
2. Medium           Earn 3 MVPs               ▱▱▱▱▱▱▱▱▱▱ 0/3  · +300
3. Hard             Get 10 kills with the AWP ▰▰▰▱▱▱▱▱▱▱ 3/10 · +600
4. Weekly (4d 12h)  Get 300 kills             ▰▰▱▱▱▱▱▱▱▱ 71/300 · +2500
Streak: 4 · new missions in 5h 07m
```

## Missions follow the map

K4 looks at every map and only gives missions that can be done there. It reads the bomb sites and hostages, the game mode (deathmatch and arms race have no rounds), respawns, free-for-all, what the buy menu sells (`mp_buy_allow_guns`, `mp_buy_allow_grenades`), and the weapons players actually carry or use. So an aim map gives no grenade or bomb missions, an AWP map gives no rifle missions, and deathmatch gives no round or clutch missions.

| Situation | What happens |
| --- | --- |
| New missions | Picked from the missions that fit the current map |
| An untouched mission that does not fit this map | Swapped for free for one of the same difficulty that fits, with a chat message. It does not use the daily reroll |
| A started mission that does not fit this map | Kept and marked "not on this map", so the player can finish it on another server or map |

When K4 reads a map wrong, correct it in `missions.map-rules` in `k4-system.jsonc`. Patterns use `*` for any text, later lines win, and every value is optional:

```jsonc
"map-rules": {
  "aim_*": { "grenades": false },
  "awp_*": { "grenades": false, "weapons": ["awp"] },
  "de_*": { "never": ["ZeusKills"] }
}
```

`rounds`, `bomb`, `hostages`, `grenades`: `true` or `false`. `weapons`: the only weapons on the map, or `["all"]`. `never`: mission types never given there.

| Rule | Default |
| --- | --- |
| Rewards | `missions.rewards`: easy 150, medium 300, hard 600, weekly 2500 points |
| Streak | `missions.streak-bonus-percent` and `missions.streak-max-days`. Finish all three daily missions on consecutive days: +10% of the day's rewards per streak day, up to +70% on day 7. Missing a day starts over. The weekly mission does not count for the streak |
| Reroll | `!missions reroll 2` swaps daily mission 2 for another of the same difficulty if it has no progress yet, `missions.rerolls-per-day` times a day (default 1). The weekly mission is not rerolled |
| Reset | Midnight in `missions.timezone` (`Europe/Istanbul`); the weekly mission on Monday at midnight. If the server has no timezone data, use an offset such as `"+03:00"` |
| Counting | `tracking.minimum-players` and warmup apply, as for stats. Team kills never count |
| Fair play | Kills and assists on bots do not count (`missions.count-bots`). AFK minutes do not count for playtime missions. Round wins only count for players who played the whole round on that team |

Mission rewards are paid even in `FlagsOnly` rank mode and without the VIP multiplier. Missions need the rank module, because they reward points.

The list of possible missions is `plugins/K4-System/missions.jsonc`:

```jsonc
"awp-kills-10": { "Type": "WeaponKills", "Weapon": "awp", "Difficulty": "Hard", "Target": 10 },
"mvps-3": { "Type": "Mvps", "Difficulty": "Medium", "Target": 3, "Reward": 400 }
```

| Type | Counts |
| --- | --- |
| `Kills`, `Headshots`, `KnifeKills`, `GrenadeKills`, `NoscopeKills`, `ZeusKills` | Kills of enemies (grenade includes fire) |
| `WeaponKills` | Kills with a weapon whose name contains `Weapon`, such as `ak47`, `m4a1` (both M4s), `awp`, `deagle` |
| `PistolKills`, `SmgKills`, `ShotgunKills`, `RifleKills`, `SniperKills` | Kills with any weapon of that buy-menu group |
| `WallbangKills`, `SmokeKills`, `JumpKills`, `BlindKills`, `RevengeKills` | Kills through a wall, through smoke, while in the air, while flashed, or on the player who last killed you |
| `LongRangeKills` | Kills from at least `Distance` meters away (30 if left out) |
| `OpeningKills` | The first kill of a round |
| `RoundKills`, `LifeKills` | That many kills in one round, or in one life. Progress shows the best try |
| `ClutchWins` | Rounds won after being left the last one alive on the team |
| `SurviveRounds` | Rounds the player is still alive at the end of |
| `Damage` | Health damage dealt to enemies |
| `Assists`, `FlashAssists` | Assists on enemy kills |
| `Mvps`, `RoundWins` | Round MVPs and rounds won |
| `BombPlants`, `BombDefuses`, `HostageRescues` | Objective actions; only given on maps that have them |
| `PlaytimeMinutes` | Minutes played on a team |
| `MatchesPlayed`, `MatchWins` | Finished matches the player was in for at least half of; in free-for-all the top fragger wins |

`Difficulty` is `Easy`, `Medium`, `Hard`, or `Weekly`. Each daily difficulty needs at least one mission, and a few more give rerolls and map swaps something to choose from. Without any `Weekly` mission there is no weekly mission. Keep the keys of missions players may have today, because the key is stored with their progress.

A `missions.jsonc` that was never edited is updated to the new default list automatically. An edited one is left alone; copy the new missions you want from the default list in this repository (`K4-System/src/Missions/default-missions.jsonc`).
