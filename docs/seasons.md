# Seasons

[Back to the README](../README.md)

A season is one month. On the 1st of each month at midnight (in `missions.timezone`), everyone's points go back to `ranks.start-points` and the race starts again. Before that, each player's final points, rank and position are kept, so nothing is lost: players see their past seasons on their [profile](profiles.md), and the Top tab can switch to any finished season's final standings.

Seasons are on by default from 7.0 (`modules.seasons`). They need the ranks module.

## The first season after updating

Updating never resets anyone. The month you install 7.0 in is the first season, and it starts with the points players already have. The first reset happens on the 1st of the next month. Players who still have points from before seasons but do not play that month are kept in that first season's results too, so their old points can always be looked up (and restored, see below).

## What happens when a season ends

| Resets | Stays |
| --- | --- |
| Points, back to `ranks.start-points` | Stats, playtime, achievements and their rewards |
| Rank, back to the starting rank | Missions, the mission streak |
| Rank reward claims, so rewards can be earned again | Weapon kills and the achievements shown on profiles |

In the last 3 days of a season, players are told when it ends as they join. When it ends, everyone online gets a chat line and, with the panel, a pop-up. `!season` shows the current season and how long is left.

Rank rewards can be earned again in every season, so a reward's server command runs again when a player reaches that rank again. Use commands that are safe to run twice, such as giving a VIP flag that already exists.

## Commands

```text
!season              the current season and when it ends (everyone)
css_k4_season        the same, for admins and the server console
css_k4_season end    ends the season now and starts a new one today (@k4system/admin)
```

A season ended early with `css_k4_season end` still ends on the 1st of the next month, so the monthly rhythm stays. Only one season can start per day.

## Several servers on one database

The season is stored in the database, so every server sharing it plays the same season. When it ends, the first server to notice saves its players and ends it; the others pick up the new season within a minute and reset their players. Points earned on the other servers in the last minutes before the end may be missing from the final results, because those servers had not saved them yet.

Use the same `modules.seasons` setting on every server that shares a database. A server with seasons off saves its players' points as before 7.0, so a player who was online there during the reset keeps their old points.

## Turning seasons off

Set `modules.seasons` to `false`. Points then keep growing as before 7.0. Finished seasons stay in the database. Turning seasons on again later ends the stored season at once if its month is over.

## Undoing a reset

Every season's final points are in the `k4seasons` table (with your table prefix). To put a season's final points back, for example after a mistaken `css_k4_season end`, stop every server using the database, then run:

```sql
UPDATE `k4ranks` SET
  `points` = (SELECT s.`points` FROM `k4seasons` s WHERE s.`season` = '2026-11-01' AND s.`steam_id` = `k4ranks`.`steam_id`),
  `rank` = (SELECT s.`rank` FROM `k4seasons` s WHERE s.`season` = '2026-11-01' AND s.`steam_id` = `k4ranks`.`steam_id`)
WHERE `steam_id` IN (SELECT `steam_id` FROM `k4seasons` WHERE `season` = '2026-11-01');

-- Make it the running season again, so it is not also listed as finished.
UPDATE `k4seasons` SET `points` = NULL, `rank` = NULL WHERE `season` = '2026-11-01';
UPDATE `k4seasonstate` SET `season` = '2026-11-01' WHERE `id` = 1;
```

Replace `2026-11-01` with the season you want back (its start date; `!season` and the Top tab show the month). Points earned after the reset are lost by this. Rank reward claims are not restored; rewards are earned again as players reach their ranks.

## Tables

| Table | Holds |
| --- | --- |
| `k4seasonstate` | The current season (one row) |
| `k4seasons` | One row per player and season: best points, and once the season ends, the final points and rank |

Season results are kept when inactive players are purged, so past leaderboards stay complete.
