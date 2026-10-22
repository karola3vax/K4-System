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
| Toggles | `!toggletag`, `!togglepointmsg` (fixed names, ranks module only) |
| Missions | `!missions`, `!mission`, `!daily`; `!missions reroll <1-3>` (daily missions only) |

Set `commands.rank-to-everyone` to `true` to make `!rank` work like LVL Ranks: everyone in chat then sees the player's leaderboard place, points, kills, deaths and KDR (each reader in their own language). Without the stats module the line shows the place and points only. `!rank` then only posts that one line, which the player sees too, and does not open the panel (`!k4` still does), and the usual `commands.cooldown-seconds` limits how often the line can be posted. The default is `false`.

Personal `rank` and `all` resets clear that player's rank-reward claim history, so the same rewards can be earned again. Rewards already delivered by server commands are not removed. The persistent `ranks.reset-cooldown-hours` setting defaults to `24`; `stat` and `time` resets do not use this long cooldown or change reward claims. Every personal reset also uses the short `commands.cooldown-seconds` to stop chat and database spam.

## Admin commands

Players need `@k4system/admin`. The server console may run these commands directly.

```text
css_givepoints <target> <amount>
css_removepoints <target> <amount>
css_setpoints <target> <amount>
css_resetdata <target> [all|time|stat|rank]
css_k4_hud        (server console only) shows whether the panel works for each player
```

Without a section, `css_resetdata` resets everything. Admin resets keep the reward history, so rewards are not paid twice.

CounterStrikeSharp targets include player names, `#userid`, `@all`, `@t`, `@ct`, and `@alive`.

```text
css_givepoints @all 50
css_setpoints "#12" 5000
css_resetdata "PlayerName" rank
```

| Permission | Purpose |
| --- | --- |
| `@k4system/admin` | Use K4-System admin commands |
| `@k4system/vip/points-multiplier` | Receive the configured multiplier on positive gains |
