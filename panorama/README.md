# K4 Panorama addon

The clickable K4 panel (`!k4`, `!rank`, `!missions`, `!top`, `!ranks`, `!stats`, `!time`, `!achievements`) and its toast pop-ups are a CS2 Panorama layout. A server plugin cannot add Panorama files, so they ship to players as a Steam Workshop addon. The server mounts that addon with MultiAddonManager, players download it when they join, and K4 fills in the text and handles the clicks.

| File | What it is |
| --- | --- |
| `k4system/panorama/layout/custom_game/k4_ui.xml` | The panel and toast layout |
| `k4system/panorama/styles/custom_game/k4_ui.css` | Colors, sizes, and the hidden or shown states |
| `k4system/panorama/images/custom_game/k4/ranks/` | Sharp copies of CS2's rank emblems (see below) |
| `tools/gen_k4_ui.py`, `tools/gen_k4_css.py` | Generate the layout and stylesheet. Edit these, then run them |
| `tools/gen_k4_ranks.py` | Writes the rank emblem copies straight from the installed game |
| `tools/check_game_images.py` | Checks every game image the addon uses against the installed CS2, and that the emblem copies match the current game. Run it before publishing |

The plugin finds the layout at `panorama/layout/custom_game/k4_ui.xml`. Keep that path.

## The panel (7.1)

The panel covers the whole screen in the style of CS2's main menu: a sidebar on the left (the player's rank and points, the seven pages, the season card, and the copyright line at the bottom) and the open page on the right, over a screenshot of the current map dimmed from left to right. The pages are Home, Profile, Missions, Achievements, Leaderboard, Ranks and Stats. Toasts stack at the right under the kill feed, newest on top, at most three at once, each with a timer bar that runs out over its length; a promotion shows as a large banner at the top centre. The panel captures the mouse while it is open; the toast stack and the banner never do.

Every id, dialog variable and class the server uses is listed in [`docs/panel-contract.md`](../docs/panel-contract.md). The layout and the server code follow that page, and `PanoramaLayoutTests` checks the layout and stylesheet against it. Change the contract first, then both sides.

Panorama scales the HUD by screen height, so heights in the stylesheet are fixed and widths flow: the sidebar is 300px wide and the page shares the rest, which looks the same at 16:9 and 16:10.

The map background is two panels: `k4-bg` always draws CS2's season background, and `k4-bg-map` on top of it gets `map-<name>` from the server. The stylesheet defines `map-<name>` for every map that has a base 1080p screenshot in CS2 (`panorama/images/map_icons/screenshots/1080p/<map>_png`); any other map draws nothing there and the season background shows through. The list lives in `MAPS` in `gen_k4_css.py`, so the generator runs without the game. After a CS2 update that adds maps, print the current list and update `MAPS`:

```bash
cd panorama/tools
python -c "import re, sys; from vpk import Vpk; print(sorted(m[1] for k in Vpk(sys.argv[1]).entries if (m := re.fullmatch(r'panorama/images/map_icons/screenshots/1080p/(.+)_png\.vtex_c', k)) and not re.search(r'_\d+$', m[1])))" "<CS2>/game/csgo/pak01_dir.vpk"
```

Leave out `random` and `lobby_mapveto`; they are not maps. `check_game_images.py` fails when an update removes a screenshot the stylesheet uses.

CS2 checks this layout before showing it: K4 uses only `Panel`, `Label`, and `Button` elements. `Panel` may have `id`, `class`, and `hittest`; `Label` also `text`; `Button` only `id` and `class`. Any other element or attribute (for example `hittest` on a `Button`) makes every player's game reject the whole panel without a visible error; the client console then says *Layout xml did not pass CustomHud validation*. The layout tests enforce this.

The panel reuses CS2's own stylesheet, icons (`panorama/images/icons/...`), and UI sounds. If a Valve update renames one of those files, that icon goes blank; the rest of the panel keeps working.

The rank emblems are the one exception. CS2 draws an SVG at the size written in the file, and the game's rank emblems are only 32×13 pixels, so they looked blurry at the panel's size. The panel cannot use the `Image` element that CS2's own menus use to draw them larger. The addon therefore carries copies of Valve's emblems with only that size raised ten times (320×130). They must stay under `panorama/images/custom_game/`: it is the only image folder the Workshop Manager packs, and images anywhere else are silently left out of the upload. Both tools read the game's own `pak01_dir.vpk` and need only Python:

```bash
python panorama/tools/check_game_images.py "<CS2>/game/csgo/pak01_dir.vpk"
python panorama/tools/gen_k4_ranks.py "<CS2>/game/csgo/pak01_dir.vpk"
```

`check_game_images.py` exits with an error and names the file when a game image the panel uses is gone or an emblem no longer matches the game; `gen_k4_ranks.py` then rewrites all 38 emblems from the current game files. (It also still accepts a folder of SVGs unpacked with Source2Viewer-CLI.)

### Server and addon versions

Players and servers update at different times, so every part a newer server fills starts hidden in the layout (`k4-gone`). A server older than the addon simply never shows those parts; a server newer than a player's addon writes to parts the player does not have, which does nothing. The contract marks each of them "starts `k4-gone`", and the layout tests check that each of them does.

The 7.1 layout renamed almost every id of the 7.0 one (the tabs became the sidebar's pages and every view was rebuilt), so a 7.0 server cannot fill a 7.1 addon, or the other way round. Update the server and the addon together.

## The official addon

The K4 panel is published on the Steam Workshop as item [`3811667212`](https://steamcommunity.com/sharedfiles/filedetails/?id=3811667212). Most servers only need that ID: skip to [Turn it on for a server](#turn-it-on-for-a-server). Publish your own copy only if you change the layout or style.

## Publish the addon (once, and after every layout change)

You need a Steam account that owns CS2.

1. **Install the tools.** In Steam, right-click Counter-Strike 2 → Properties → DLC, and tick *Counter-Strike 2 Workshop Tools*. Let it download.
2. **Create the addon.** Start CS2 with *Launch Counter-Strike 2 Workshop Tools*. In the addon manager, create a new addon named `k4system`. CS2 creates two folders:
   - `...\Counter-Strike Global Offensive\content\csgo_addons\k4system\`
   - `...\Counter-Strike Global Offensive\game\csgo_addons\k4system\`
3. **Copy the source.** Copy the `panorama` folder from `panorama/k4system/` in this repository into `content\csgo_addons\k4system\`, so the layout ends up at `content\csgo_addons\k4system\panorama\layout\custom_game\k4_ui.xml`.
4. **Compile.** Close the tools, open a command prompt in the `Counter-Strike Global Offensive` folder, and run:

   ```bat
   game\bin\win64\resourcecompiler.exe -i "content\csgo_addons\k4system\panorama\*" -r
   ```

   This writes `k4_ui.vxml_c` and `k4_ui.vcss_c` under `game\csgo_addons\k4system\panorama\`. Any error here names the line in the `.xml` or `.css` file; send it to whoever maintains K4.

   The tools fill a new addon with sample sounds, post-processing, and map files. Move everything in `game\csgo_addons\k4system\` except `addoninfo.txt` and the `panorama` folder out of the addon before uploading, or every player downloads them. The `tools_*` files and `_bakeresourcecache` are the editor's own cache and are not uploaded.
5. **Upload.** If the Workshop Manager refuses an addon without a map, open Hammer from the Workshop Tools, create an empty map, save it as `k4system.vmap` in the addon, build it once, and try again. Start the Workshop Tools again with the `k4system` addon and open the *Workshop Manager* from the tools list. Choose *New*, pick the `k4system` addon, give it a title (for example "K4-System panel"), a preview image, and visibility **Public**, then submit.
6. **Note the ID.** The addon's workshop page address ends in `?id=1234567890`. That number is the addon ID. A new item, and each new version, waits for Steam moderation; until it is approved, other players and servers cannot download it.

To update the panel later, repeat steps 3 to 5 and choose the existing item in the Workshop Manager instead of *New*. The server picks up the new version on its next map change (MultiAddonManager re-downloads it), and players get it the next time they join.

## Turn it on for a server

1. **Install MultiAddonManager** (a Metamod:Source plugin) on the server. Follow its own install guide.
2. **Add the addon.** In `game/csgo/cfg/multiaddonmanager/multiaddonmanager.cfg`, add the ID to the list:

   ```cfg
   mm_extra_addons "3811667212"
   ```

   If other addons are already listed, separate the IDs with commas. If you published your own copy, use its ID instead.
3. **Enable the panel in K4.** In `configs/plugins/K4-System/k4-system.jsonc` set:

   ```jsonc
   "panorama": { "enabled": true }
   ```

4. **Restart the server**, join, and type `!k4`.

With the panel on, the K4 commands open their tab instead of printing to chat. Promotions, demotions, finished missions, unlocked achievements, the round summary, and more show as a small pop-up (a toast) at the right side of the screen, below the kill feed, instead of a chat line; see [Toasts](#toasts). Single point messages after the round summary stay in chat. Click **✕** (top right) to close the panel. While it is open the mouse controls the panel, not your aim, so K4 also closes it when freeze time ends and after 45 seconds without a click.

## Toasts

Toasts stack under the kill feed, newest on top, at most three at once. Each player can turn them off with `!toasts` (`commands.toasts`) or the button on their profile; K4 remembers it. A player with toasts off gets the chat line instead wherever one exists (round summary, rank changes, finished missions, the daily streak).

| Event | Color and length | What it shows |
| --- | --- | --- |
| Round end, when `ranks.round-summary` is on | green (gain) or red (loss) | "Round won", "Round lost" or "Round"; the reasons such as "3× Kill · Headshot · Round won"; "+86" or "-12"; your team patch. Replaces the chat summary |
| Promotion | the large banner | the new rank and its emblem |
| Demotion | red | the new rank and its emblem |
| Hold your rank | red | a point loss left you within 10% of the points between your rank and the one below; once per rank per connection |
| Leaderboard move | blue, short | "Now #12" and "▲ 3" when your place improves, only inside the top 100, never on joining. Needs `tags.top-placement.enabled`, which keeps places up to date |
| Mission progress | blue, short | a daily or weekly mission passed 50% or 90% (not on completion), "6/10" with a bar |
| Mission complete | green | the mission, its icon and "+reward" |
| All missions done | gold | the streak day and "+bonus" |
| Achievement almost there | blue, short | 90% of the goal, "47/50" with a bar; once per achievement per connection, not for goals already at 90% when you join |
| Achievement unlocked | gold, long | the achievement in a bronze, silver or gold medal ring: under 300 points bronze, under 800 silver, 800 and more gold |
| Points event starts | gold, long | "2×" and the minutes left |
| Bounty | red to everyone when someone is wanted; gold to the player who claims it | the wanted player and the reward |
| Duel challenge | purple, to the challenged player | the challenger, the stake, and the `!duel accept` command |
| New season | gold, long | the season name |
| Season ending (last 3 days, on joining) | gold | the time left and your place, such as "#4" |

Promotions use `PanoramaHud.Promote`; everything else goes through `PanoramaHud.Toast` (see `docs/panel-contract.md`). The rules for when a toast fires live in `K4-System/src/Hud/ToastRules.cs` and are tested in `ToastRulesTests` with real cases in `K4-System.Tests/Evals/toasts.json`.

## If something does not work

| What you see | Likely cause |
| --- | --- |
| `!k4` shows nothing at all | Start CS2 with the launch option `-condebug`, join, type `!k4`, and open `game/csgo/console.log`. *Layout xml did not pass CustomHud validation* means the layout uses something CS2 does not allow (see above). *Failed to load layout* means the addon is not on your client: check `mm_extra_addons`, the ID, and that the item is Public and approved, then rejoin |
| The server console says *Addon download ... failed to start* right after a restart | MultiAddonManager tried before the server logged on to Steam. Run `changelevel <current map>` once; after that the addon stays on disk |
| You are not sure the server side works | Run `css_k4_hud` in the server console. It lists the panel entity and, for each player, whether the panel is open and the mouse is captured |
| The rank picture is missing | Set `"rank-icons": "text"` to show the rank tag instead, and report it |
| The server log says the panel could not be created | The server's CS2 build or CounterStrikeSharp is too old for `custom_hud_layout`. Update both. Commands use chat meanwhile |
| The panel appears but looks unstyled | The `.css` was not compiled. Repeat step 4 and upload again |
| Old look after an update | Players must rejoin to download the new addon version |

Turning `panorama.enabled` back to `false` returns every command to the chat messages and the center-screen mission card.
