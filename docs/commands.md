# Commands and permissions

[Back to the README](../README.md)

Command names are set in `commands` in `k4-system.jsonc`. These are the defaults:

| Purpose | Commands |
| --- | --- |
| Help and panel | `!k4` (`commands.main`, for example `["k4", "lvl"]`) |
| Rank | `!rank`, `!myrank` |
| Top list | `!top`, `!ranktop`, `!toplist` (add a number up to 25; default 5) |
| Rank list | `!ranks`, `!ranklist` |
| Stats | `!stats`, `!mystats`, `!stat`, `!mystat`, `!statistic`, `!statistics` |
| Playtime | `!time`, `!mytime`, `!playtime` |
| Admin list | `!admins`, `!listadmins` |
| Personal reset | `!resetmydata <all\|time\|stat\|rank>` |
| Toggles | `!toggletag`, `!togglepointmsg` (fixed names, ranks module only; `!togglepointmsg` is remembered per player since 7.1) |
| Panel pop-ups | `!toasts` (`commands.toasts`, panel only): turns your own toasts on or off; remembered per player |
| Missions | `!missions`, `!mission`, `!daily`; `!missions reroll <1-3>` (daily missions only) |
| Duels | `!duel <player> <points>`, `!duel accept`, `!duel decline` (`decline` also takes back your own challenge) |
| Achievements | `!achievements`, `!achievement`, `!ach` |
| Profile | `!profile`, `!profile <name>`, `!profile <steamid64>` ([profiles](profiles.md)) |
| Season | `!season` ([seasons](seasons.md)) |

Set `commands.rank-to-everyone` to `true` to make `!rank` work like LVL Ranks: everyone in chat then sees the player's leaderboard place, points, kills, deaths and KDR (each reader in their own language). Without the stats module the line shows the place and points only. `!rank` then only posts that one line, which the player sees too, and does not open the panel (`!k4` still does), and the usual `commands.cooldown-seconds` limits how often the line can be posted. The default is `false`.

Personal `rank` and `all` resets clear that player's rank-reward claim history, so the same rewards can be earned again. Rewards already delivered by server commands are not removed. The persistent `ranks.reset-cooldown-hours` setting defaults to `24`; `stat` and `time` resets do not use this long cooldown or change reward claims. Every personal reset also uses the short `commands.cooldown-seconds` to stop chat and database spam.

## Admin commands

Players need `@k4system/admin`. The server console may run these commands directly.

```text
css_givepoints <target> <amount>
css_removepoints <target> <amount>
css_setpoints <target> <amount>
css_resetdata <target> [all|time|stat|rank]
css_k4_history <target|steamid64> [count]   shows who changed a player's points (default 10, up to 25)
css_k4_event <multiplier> <minutes> | stop   starts or ends a points event, such as css_k4_event 2 60
css_k4_season [end]   shows the season, or ends it now and starts a new one today (see seasons.md)
css_k4_reload     checks k4-system.jsonc, ranks.jsonc and missions.jsonc, then reloads K4-System without a restart
css_k4_hud        (server console only) shows whether the panel works for each player
```

Without a section, `css_resetdata` resets everything. Admin resets keep the reward history, so rewards are not paid twice.

CounterStrikeSharp targets include player names, `#userid`, `@all`, `@t`, `@ct`, and `@alive`. A plain name that matches several players is refused for these commands, so `css_resetdata jo` cannot hit every "jo" on the server; use `#userid`, a fuller name, or a group.

```text
css_givepoints @all 50
css_setpoints "#12" 5000
css_resetdata "PlayerName" rank
```

### Reloading after an edit

`css_k4_reload` reads `k4-system.jsonc`, `ranks.jsonc` and `missions.jsonc` exactly as a start would. When any of them has a problem, nothing is reloaded: K4 keeps running with the old settings and the reply names the file and the problem. When all are fine, K4-System reloads at once. Players keep their data (everything is saved first), the panel closes, and running duels give both stakes back, because a reload ends them. Your own wording in `configs/plugins/K4-System/lang/` is read again too.

### Point history

Every manual point change is logged: `css_setpoints`, `css_givepoints`, `css_removepoints`, `css_resetdata`, a player's own `!resetmydata`, and points set by another plugin through the shared API. Each entry has the time (UTC), the change, the points afterwards, and who did it (the admin's name and SteamID64, `console`, or `shared API`). `css_k4_history` shows the newest entries; a SteamID64 also works for players who are offline. Points from playing (kills, rounds, missions) are not logged. Entries are kept for 90 days, and removed with the player when inactive players are purged.

```text
css_k4_history "#12"
css_k4_history 76561198000000001 25
```

| Permission | Purpose |
| --- | --- |
| `@k4system/admin` | Use K4-System admin commands |
| `@k4system/vip/points-multiplier` | Receive the configured multiplier on positive gains |
