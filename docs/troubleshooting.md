# Common problems

[Back to the README](../README.md)

| Problem | Check this |
| --- | --- |
| Plugin does not load | CounterStrikeSharp, .NET 10, and the release folder structure |
| Database errors | `storage.mysql` host, credentials, database, port, and SSL mode; or, for SQLite, write access to the `sqlite-file` location |
| Points do not change | `modules.ranks`, `ranks.mode`, permissions, warmup, minimum players, bots, and point values |
| Missions missing | `modules.ranks` must be on. The log says "Daily missions are off because..." when `missions.jsonc` has an error |
| Achievements missing | `modules.ranks` and `modules.achievements` must be on. The log says "Achievements are off because..." when `achievements.jsonc` has an error; `css_k4_reload` names the mistake |
| A kill gave no points | Anti-farming: the two players are on the same connection, or the killer already got many kills on that player in the last minutes. See `anti-farm` in [Configuration](configuration.md#anti-farming) |
| A config change did nothing | Run `css_k4_reload` (it checks the files and names any mistake) or restart the server |
| Panel shows nothing | See [panorama/README.md](../panorama/README.md#if-something-does-not-work) |
| Admin command denied | `@k4system/admin` or server-console execution |
| Rank or tag missing | Player load, permission gates, scoreboard settings, and `FollowCS2ServerGuidelines` |
| Country tag missing | `GeoLite2-Country.mmdb` in `plugins/K4-System/` |
| K4 says it is not running | The log names the setting to fix in `k4-system.jsonc`, or asks you to move 5.x settings into the new file |
