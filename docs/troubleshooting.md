# Common problems

[Back to the README](../README.md)

| Problem | Check this |
| --- | --- |
| Plugin does not load | CounterStrikeSharp, .NET 10, and the release folder structure |
| Database errors | `storage.mysql` host, credentials, database, port, and SSL mode; or, for SQLite, write access to the `sqlite-file` location |
| Points do not change | `modules.ranks`, `ranks.mode`, permissions, warmup, minimum players, bots, and point values |
| Missions missing | `modules.ranks` must be on. The log says "Daily missions are off because..." when `missions.jsonc` has an error |
| Panel shows nothing | See [panorama/README.md](../panorama/README.md#if-something-does-not-work) |
| Admin command denied | `@k4system/admin` or server-console execution |
| Rank or tag missing | Player load, permission gates, scoreboard settings, and `FollowCS2ServerGuidelines` |
| Country tag missing | `GeoLite2-Country.mmdb` in `plugins/K4-System/` |
| K4 says it is not running | The log names the setting to fix in `k4-system.jsonc`, or asks you to move 5.x settings into the new file |
