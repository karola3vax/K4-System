# Configuration

[Back to the README](../README.md)

## The config file

All settings live in one commented file, `configs/plugins/K4-System/k4-system.jsonc`. Every setting has a comment that explains it. After an edit, `css_k4_reload` checks the files and applies them without a restart (see [Commands](commands.md#reloading-after-an-edit)). Delete the file to get a fresh default copy. Unknown or misspelled settings stop K4 with a message that names them, so typos never fail silently.

| Section | What it controls |
| --- | --- |
| `storage` | SQLite or MySQL, table prefix, purge of inactive players, optional LevelRanks table |
| `modules` | Turn ranks, stats, playtime, missions, [achievements](achievements.md), and the admin list on or off |
| `tracking` | Minimum players, warmup, bots, and free-for-all. Applies to points and stats alike |
| `ranks` | Rank mode, start points, VIP multiplier, round summary, kill streaks, reset cooldown, dynamic death points, team balance, playtime reward |
| `points` | A point preset plus your own overrides |
| `anti-farm` | No points between players on the same connection, and less for killing the same player over and over |
| `events` | Scheduled points events, such as 2× points on weekend evenings |
| `missions` | Time zone, rewards per difficulty, streak bonus, rerolls per day, bots, and map rules |
| `bounty` | The wanted kill streak, the bounty reward, and the survivor bonus |
| `duels` | Duel stakes, how long a challenge waits, and the same-connection block |
| `panorama` | The clickable panel and its rank icons (competitive, wingman, or text) |
| `scoreboard` | What the CS2 rank area shows, custom icons, points in the score column |
| `tags` | Rank tags, top-placement tag, country tag, admin tags and `!admins` colors |
| `messages` | Join hint, connect and disconnect announcements |
| `commands` | Command names and the cooldown of `!rank`, `!top`, and `!resetmydata` |

### Storage: SQLite or MySQL

```jsonc
"storage": {
  "type": "sqlite",               // or "mysql"
  "sqlite-file": "k4system.db",   // relative paths start in plugins/K4-System/
  "mysql": { "host": "localhost", "port": 3306, "database": "cs2", "username": "root", "password": "", "ssl-mode": "None" },
  "table-prefix": ""
}
```

| Choose | When |
| --- | --- |
| `sqlite` | You run one server and want no database setup. Everything is stored in one file beside the plugin |
| `mysql` | You run several servers on one database, use a web panel, or need the LevelRanks table for an external tool |

Several servers on one database share players, but a player should be on one of them at a time. Each server saves the player's totals when they leave and reads them when they join; a player who jumps to another server while the first one cannot reach the database yet may start there with the totals from before, and one side's progress is overwritten.

Both use the same tables, so commands, top lists, rewards, and resets behave the same. K4 does not copy data between the two. If you replace the whole plugin folder when updating, keep `k4system.db`.

By default K4 deletes players who have not joined for 30 days (`storage.purge-after-days`). Set it to `0` to keep everyone.

### Point presets

Choose a preset, then change single values in `overrides`:

```jsonc
"points": {
  "preset": "competitive",
  "overrides": { "kill": 12, "bomb-drop": 0 }
}
```

| Event | casual | competitive | deathmatch |
| --- | ---: | ---: | ---: |
| `kill` / `death` | 20 / -15 | 10 / -10 | 5 / -3 |
| `headshot` | 5 | 3 | 2 |
| `assist` / `flash-assist` | 10 / 15 | 4 / 3 | 1 / 1 |
| `team-kill` / `team-assist` / `team-flash-assist` | -40 / -10 / -15 | -30 / -5 / -5 | -10 / 0 / 0 |
| `suicide` | -20 | -15 | -5 |
| `noscope` / `through-smoke` / `blind-kill` / `wallbang` | 60 / 40 / 60 / 5 | 10 / 5 / 5 / 2 | 3 / 2 / 2 / 1 |
| `long-distance-kill` (from `long-distance-meters`) | 60 (30 m) | 5 (40 m) | 2 (40 m) |
| `grenade-kill` / `fire-kill` / `impact-kill` | 60 / 50 / 60 | 10 / 10 / 20 | 3 / 3 / 5 |
| `knife-kill` / `taser-kill` | 60 / 60 | 15 / 15 | 5 / 5 |
| Streaks `double-kill` … `godlike` | 10, 15, 25, 30, 40, 50, 55, 65, 70, 80, 90 | 2, 4, 6, 8, 10, then 12 | 1, 2, 3, 4, 5, then 6 |
| `round-win` / `round-loss` / `mvp` | 5 / 0 / 25 | 10 / -5 / 10 | 0 / 0 / 0 |
| `bomb-plant` / `bomb-defuse` / `bomb-defuse-team` / `bomb-explode` | 30 / 40 / 10 / 25 | 10 / 15 / 5 / 5 | 0 |
| `bomb-pickup` / `bomb-drop` | 5 / -10 | 0 / 0 | 0 |
| `hostage-rescue` / `hostage-rescue-all` / `hostage-hurt` / `hostage-kill` | 30 / 25 / -10 / -40 | 10 / 10 / -5 / -20 | 0 |
| `playtime` (every `ranks.playtime-reward.every-minutes`) | 15 | 0 | 0 |

`casual` is the default and uses the same values as K4-System 5.x. `competitive` rewards winning rounds and clean play over flashy kills. `deathmatch` counts only kills, in small steps, for servers without rounds or objectives.

Bots are never stored as persistent ranked players. Bot settings only decide whether eligible human interactions with bots count.

## Anti-farming

On by default, and normal play never notices it. It only stops points being farmed with a second account or a friend.

- **Same connection.** With `same-ip`, a kill between two players on the same connection gives the killer nothing and costs the victim nothing, so a second account cannot feed the first. Kill bonuses and the kill streak do not count either.
- **The same victim again and again.** The first `repeat-kills` kills on one player within `window-minutes` count fully. Each further kill on that player gives `reduced-percent` of the points, bonuses included, and the death costs the victim the same share. The point message says so, such as "(same player again: 25%)". Once the window has passed without kills on that player, kills count fully again. `"repeat-kills": 0` turns this rule off.

Team kills, suicides, assists, objectives and missions are not affected.

```jsonc
"anti-farm": {
  "same-ip": true,
  "repeat-kills": 10,
  "window-minutes": 10,
  "reduced-percent": 25
}
```

## Points events

A points event multiplies the points players earn from kills, rounds, objectives and playtime for a while. It is announced to everyone when it starts and ends, and players who join during one are told. With the panel, everyone also gets a pop-up. Mission rewards, bounties and duels are not multiplied, and point losses stay as they are. The VIP multiplier applies on top.

Schedule events in `events.schedule`. Each entry has `days` (Monday to Sunday; leave it empty for every day), `from` and `to` as 24-hour `"HH:mm"` times in `missions.timezone`, and a `multiplier` above 1 and up to 10. A `to` before `from` runs past midnight, so a Friday `"22:00"` to `"02:00"` event ends on Saturday at 2:00. When events overlap, the highest multiplier counts. The schedule is empty by default, so nothing changes until you add one.

```jsonc
"events": {
  "schedule": [
    { "days": ["Saturday", "Sunday"], "from": "18:00", "to": "22:00", "multiplier": 2 },
    { "days": ["Friday"], "from": "22:00", "to": "02:00", "multiplier": 1.5 }
  ]
}
```

Admins can start one at any time with `css_k4_event <multiplier> <minutes>`, such as `css_k4_event 2 60`, and end it with `css_k4_event stop`. Stopping a scheduled event silences it until it would have ended. `css_k4_event` alone shows the event that is on. Events an admin started end when K4 reloads; scheduled ones carry on.

## Bounties and duels

Both are on by default and need the ranks module. They are built to be rare, so they stay special.

**Bounty.** A player who gets `kill-streak` kills on real players without dying becomes wanted. Everyone sees it in chat, and as a pop-up when the panel is on. The enemy who kills the wanted player gets `reward` points; the wanted player loses nothing extra. A suicide, a team kill, a bot kill, or leaving the server only ends the bounty. A wanted player who is still wanted when the match ends, or when the map changes, gets `survivor-reward`. Only one player is wanted at a time, and each player at most once per map. Bot kills do not count toward the streak.

**Duel.** `!duel <player> <points>` challenges a player, who has `accept-seconds` to type `!duel accept` or `!duel decline`. On accept, both stakes are taken and held. Whoever gets more kills from then until the match ends wins both stakes; a tie returns them. A player who is not on the server when the match ends loses, so a losing duel cannot be dodged by leaving (reconnecting within the map is fine). On servers that change maps without a match end, the duel is decided when the map changes, without a chat message. Each player can duel once per map. The stake is between `min-stake` and `max-stake-percent` of the poorer player's points. With `block-same-ip`, two players on the same connection cannot duel, so points cannot be moved between accounts. Duels are off during warmup and below `tracking.minimum-players`.

```jsonc
"bounty": {
  "enabled": true,
  "kill-streak": 7,
  "reward": 150,
  "survivor-reward": 50
},
"duels": {
  "enabled": true,
  "min-stake": 10,
  "max-stake-percent": 10,
  "accept-seconds": 30,
  "block-same-ip": true
}
```

## Rank game modes

Set `mode` inside `ranks`:

```jsonc
"ranks": {
  "mode": "Normal"
}
```

| Mode | Active ranks | Automatic scoring |
| --- | --- | --- |
| `Normal` | Regular and permission-gated ranks | No additional game-mode restriction |
| `FlagsOnly` | The starting rank plus ranks that have `RequiredPermissions` | Waits until the player has one of those permissions |
| `NoFlags` | Default rank plus ranks without requirements | No additional game-mode restriction |

In `FlagsOnly`, automatic gains and losses wait until the player has a matching permission, group, or command override. Saved points stay as they are while scoring is blocked. Admin commands and other plugins can still change points.

Flags are rechecked after player data loads and on the next round, score attempt, spawn, team change, manual point update, or `!rank` command. The highest rank the player may hold wins. If a player skips ranks, only the reward of the rank they land on is paid, and rewards are not paid when a rank changes only because data loaded or a permission changed.

Changing modes does not rewrite `ranks.jsonc`, stored points, or database tables. Ranks switched off by the mode are left out of the scoreboard icons, so there are no gaps. Restart K4-System after changing the mode.

## Permission-gated ranks

`RequiredPermissions` checks existing CounterStrikeSharp permissions, groups, or enabled command overrides. K4-System does not create or store those requirements. The separate `Permissions` property grants K4-owned permissions while the player holds that rank.

```jsonc
"Global Elite": {
  "Name": "Global Elite",
  "Tag": "[GE]",
  "Point": 100000,
  "Color": "lightred",
  "RequiredPermissions": ["@css/global"]
}
```

If a player has enough points but lacks the requirement, K4-System keeps them at the highest lower rank they may hold. Multiple `RequiredPermissions` entries are alternatives, so any one match is enough.

Regular and gated ranks may share a threshold to create parallel progression paths. Put the unrestricted rank first and the gated variant after it:

```jsonc
"Gold": {
  "Name": "Gold",
  "Point": 5000,
  "Color": "gold"
},
"VIP Gold": {
  "Name": "VIP Gold",
  "Point": 5000,
  "Color": "lightred",
  "RequiredPermissions": ["@css/vip"]
}
```

The player receives the last eligible rank at that threshold. `!rank` uses the same permission-aware progression to show the actual current and next ranks.

Rank-granted permissions must use the `@k4system/` domain so K4 can remove only its own grants:

```jsonc
"Permissions": [
  {
    "DisplayName": "VIP commands",
    "PermissionName": "@k4system/rank/vip"
  }
]
```

Existing permissions from `admins.json`, groups, or other plugins are never claimed as K4-owned and are not removed during rank changes.

## Command rewards

Run one or more server commands when a player first reaches a rank:

```jsonc
"Silver III": {
  "Name": "Silver III",
  "Tag": "[SIII]",
  "Point": 3000,
  "Color": "silver",
  "RewardDescription": "{green}500 store credits\n{gold}VIP access",
  "Rewards": [
    {
      "Type": "command",
      "Value": "css_store_givecredits {steamid} 500"
    }
  ]
}
```

- Only `command` rewards are supported.
- Commands run in the server console.
- Pending and completed commands are stored in `k4rankrewards`, preventing the same SteamID from normally receiving the same configured reward twice.
- Supported placeholders: `{steamid}`, `{userid}`, `{name}`, `{rank}`, `{rankid}`, and `{points}`.
- Rewards are not backfilled on initial load or for skipped intermediate ranks.

Interrupted pending rewards resume when the player reconnects. A server crash after the external command runs but before completion is saved can repeat that command, so reward commands should be safe to run twice.

`RewardDescription` appears for ranks that contain at least one valid command reward. Use JSON's `\n` escape for a new chat line. `/n` remains ordinary text. CounterStrikeSharp color tags such as `{green}`, `{gold}`, and `{red}` are case-insensitive; unknown tags remain visible.

## Scoreboard ranks

K4-System can display points or rank icons directly in CS2's matchmaking rank area. No companion plugin is needed.

Set `rank-display` inside `scoreboard`:

```jsonc
"rank-display": "Premier"
```

| Mode | Scoreboard display |
| --- | --- |
| `Disabled` | Do not write matchmaking rank fields |
| `Premier` | Show the player's live K4 points as a Premier-style rating |
| `Competitive` | Map active K4 ranks to Silver I through Global Elite |
| `Wingman` | Map active K4 ranks to Wingman icons |
| `Custom` | Map `None` and active K4 ranks to a configured custom skillgroup icon range |

Premier ratings use a minimum display value of `1` so a new zero-point player remains visible. Competitive and Wingman values are clamped to the 18 built-in icons, where the default `None` remains unranked. Custom mode reserves the base icon for `None` and starts active ranks at the next icon.

For custom icons, configure the first skillgroup ID, the number of available icons, and any K4-rank offset:

```jsonc
"rank-display": "Custom",
"custom-icons": { "first-id": 19000051, "count": 20, "offset": 0 }
```

Custom SVG files go under `panorama/images/icons/skillgroups/` and use names such as `skillgroup19000051.svg`. With the example above, `skillgroup19000051.svg` is `None`, `skillgroup19000052.svg` is the first active K4 rank, and `skillgroup19000070.svg` is the nineteenth active rank. The default 18-rank ladder only needs a count of `19`; use `20` when `ranks.jsonc` contains an additional active rank.

> [!CAUTION]
> CounterStrikeSharp blocks matchmaking-rank fields while `FollowCS2ServerGuidelines` is `true`. K4-System will leave scoreboard ranks disabled and log the exact setting to review in `addons/counterstrikesharp/configs/core.json`. CounterStrikeSharp documents that disabling this protection is at the server owner's own risk. K4-System never changes it automatically.

## Languages, tags, and GeoIP

K4-System ships 36 languages:

| | | | |
| --- | --- | --- | --- |
| `ar` Arabic | `az` Azerbaijani | `bg` Bulgarian | `cs` Czech |
| `da` Danish | `de` German | `el` Greek | `en` English |
| `es` Spanish | `et` Estonian | `fi` Finnish | `fr` French |
| `hu` Hungarian | `id` Indonesian | `it` Italian | `ja` Japanese |
| `kk` Kazakh | `ko` Korean | `lt` Lithuanian | `lv` Latvian |
| `nl` Dutch | `no` Norwegian | `pl` Polish | `pt-BR` Portuguese (Brazil) |
| `pt-PT` Portuguese (Portugal) | `ro` Romanian | `ru` Russian | `sk` Slovak |
| `sr` Serbian (Latin) | `sv` Swedish | `th` Thai | `tr` Turkish |
| `uk` Ukrainian | `vi` Vietnamese | `zh-CN` Chinese (Simplified) | `zh-TW` Chinese (Traditional) |

The server language is `ServerLanguage` in CounterStrikeSharp's `configs/core.json`. Each player can pick their own with `!lang <code>`, for example `!lang pl`; every K4 message, including point messages and announcements, then uses that language. A missing message falls back to English. Translations other than English were written by AI and not reviewed by native speakers; corrections are welcome.

### Your own wording

Don't edit `plugins/K4-System/lang/`: every update replaces it. Put the lines you want to change in `configs/plugins/K4-System/lang/<language>.json` instead, which updates never touch. The file only needs those lines; every other line keeps the shipped text, so new lines from an update still show. For example, `configs/plugins/K4-System/lang/en.json`:

```json
{
  "prefix": "{gold}[MyServer]"
}
```

Copy the line names from the shipped `lang/en.json`. Colors and placeholders such as `{0}` work the same. Your line only replaces that language: a player using `!lang tr` still sees the shipped Turkish line, unless you also add `tr.json`. A line that has no translation in their language uses your line for the server language. K4 reads the folder when it loads, creates it with a short `README.txt` on first start, and names any file it can't read in the server console.

Rank tags come from `ranks.jsonc`, admin tags come from `tags.admins`, and optional country tags require `GeoLite2-Country.mmdb` beside `K4-System.dll`. K4 leaves another plugin's clan tag alone when every K4 tag source is disabled, and only clears the last tag that K4 itself applied. Players can use `!toggletag` to hide or show the admin permission tag; it does not hide rank, placement, or country tags.
