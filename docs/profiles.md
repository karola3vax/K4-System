# Profiles

[Back to the README](../README.md)

Every player has a profile: their rank, key stats, playtime, favorite weapon, up to three achievements they chose to show, and their last three [seasons](seasons.md).

Profiles are on by default from 7.0 (`modules.profiles`).

## Opening a profile

With the [panel](../panorama/README.md):

- Click your own name at the top of the panel.
- Click any name in the Top tab, also on a past season's list. Players who are offline work too.
- Type `!profile` for your own, or `!profile <name>` for a player on the server. A SteamID64 (`!profile 76561198000000001`) also finds players who are offline.

Without the panel, `!profile` prints the same information in chat.

## What it shows

| Part | Where it comes from |
| --- | --- |
| Rank, points, position | The rank module |
| Kills, K/D, headshots | The stats module |
| Playtime | The playtime module |
| Favorite weapon | The weapon with the most kills. Counted from 7.0 on, so it appears after a player's first kills on 7.0. Every knife counts as "Knife" |
| Achievements shown | Up to three unlocked achievements the player picked |
| Achievements | How many of the server's achievements the player unlocked |
| Past seasons | The last three finished seasons: final rank, points and position, and the best rank reached |

A part whose module is off is left out.

## Choosing the achievements to show

In the Achievements tab, click an unlocked achievement to show it on your profile; it gets a gold star. Click it again to take it off. Up to three can be shown at once. Achievements that are not unlocked cannot be shown.

## Resets

`!resetmydata stat` (and an admin's `css_resetdata <target> stat`) also resets the weapon kills. The achievements shown stay.

## Tables

| Table | Holds |
| --- | --- |
| `k4weaponkills` | Kills per weapon and player |
| `k4profiles` | The achievements each player shows |

Both are removed with the player when inactive players are purged.
