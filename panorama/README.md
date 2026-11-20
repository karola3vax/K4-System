# K4 Panorama addon

The clickable K4 panel (`!k4`, `!rank`, `!missions`, `!top`, `!ranks`, `!stats`, `!time`, `!achievements`) and its toast pop-ups are a CS2 Panorama layout. A server plugin cannot add Panorama files, so they ship to players as a Steam Workshop addon. The server mounts that addon with MultiAddonManager, players download it when they join, and K4 fills in the text and handles the clicks.

| File | What it is |
| --- | --- |
| `k4system/panorama/layout/custom_game/k4_ui.xml` | The panel and toast layout |
| `k4system/panorama/styles/custom_game/k4_ui.css` | Colors, sizes, and the hidden or shown states |
| `k4system/panorama/images/custom_game/k4/ranks/`, `.../k4/icons/` | Sharp copies of CS2's rank emblems and icons (see below) |
| `tools/gen_k4_ui.py`, `tools/gen_k4_css.py` | Generate the layout and stylesheet. Edit these, then run them |
| `tools/gen_k4_ranks.py`, `tools/gen_k4_icons.py` | Write the emblem and icon copies straight from the installed game |
| `tools/check_game_images.py` | Checks every game image the addon uses against the installed CS2, and that the emblem and icon copies match the current game. Run it before publishing |

The plugin finds the layout at `panorama/layout/custom_game/k4_ui.xml`. Keep that path.

## The panel (8.0)

The panel is a solid graphite window, 1240x760, centered over the game. The game stays visible around it; there is no map picture and no dim. From top to bottom:

| Part | Height | What is in it |
| --- | --- | --- |
| Top bar | 56 | "K4 System", the tabs Home, Missions, Achievements, Leaderboard, Duels (with a count of waiting challenges) and Profile, the points event tag, the gear (settings) and close |
| Crumbs | 40 | Back (when you came from another screen, for example a player's profile from the leaderboard), where you are, and the season clock |
| Duel bar | 68 | only while a challenge waits for you: who, what, the time left, Accept and Decline. It shows on every screen |
| Screen | 590 | the open screen (522 while the duel bar shows) |
| Footer | 32 | the result of your last click (green or red), the out-of-date bar, the key hint ("Tab closes") and "© 2026 karola3vax", always visible |

The screens: **Home** (your rank with your place, Top 3 and you, today's missions with their text and the 7-day streak, and up to three "do this next" cards), **Missions** (three daily cards, the weekly one and the streak; swapping a mission asks Yes or No inside the card), **Achievements** (your three showcase slots, the filters, and a scroll grid of every achievement), **Leaderboard** (a Players or Rank list switch: the top 100 in a scroll list with Profile and Challenge on each row and your own row pinned under the list, or every rank with its reward and a pinned "you" card), **Duels** (pick an online player, a duel type and a stake, then Send; your waiting challenge with Take back, your running duel and the others on the right), **Profile** (your own or another player's: rank, showcase, stats, top weapons, time per side and past seasons) and **Settings** (pop-ups, chat or both; point messages; rank tag). Stats live in Profile and the rank list in Leaderboard.

Every id, dialog variable and class the server uses is listed in [`docs/panel-contract.md`](../docs/panel-contract.md). The layout and the server code follow that page, and `PanoramaLayoutTests` checks the layout and stylesheet against it: every id and its element, the pool sizes, the scroll boxes, the out-of-date bar, the theme and the size budgets. Change the contract first, then both sides.

Panorama scales the HUD by screen height (1080 is 1:1), so the window is a fixed 1240x760 at every aspect ratio and ends at y 920. Every screen is sized to fit its 1190x590 without clipped text; long lists scroll instead.

### Scroll lists

The leaderboard (100 rows), the rank list (40), the achievements (64), the online players on Duels (64) and the past seasons on Profile (24) scroll with the mouse wheel on the player's own PC. Each list is one `k4-*-list` box with `hittest="true"` (without it the wheel does not reach the box) and the class `k4-scroll` (`overflow: squish scroll` and a 6px scrollbar in the window's greys). The rows are built in the layout and start `k4-gone`; the server fills the ones it needs, and the hidden rows shorten the scroll. The server never sees the scroll position, so anything the player must reach without scrolling (their own leaderboard row, the "you" card on the rank list) sits outside the box, and a list longer than its rows ends with a "12 more not shown" line.

### The look

One grey ramp (`s0` window, `s1` card, `s2` hover, `s3` picked, `s4` bar track; three line greys; three text greys), all solid. White is the accent for exactly three things: the open tab's underline, the one main button of a card (Send challenge, Accept), and your own "you" mark. A picked choice is a lighter grey with a grey outline, never white. Other colour appears only where the game gives it meaning: rank emblems and the rank name colours a server owner picks, gold, silver and bronze for medals and leaderboard places 1 to 3, green for done (a green check tag and a green bar, never a green border), red for lost, and the T and CT colours for time per side. Sentence case and no letter spacing; the game's Stratum font for titles and numbers and its Noto for text. Corners: window 6px, cards and buttons 4px, tags and bars 2px; medal rings are round. The tokens are at the top of `gen_k4_css.py`, and `The_theme_follows_the_tokens` in `PanoramaLayoutTests` fails on any other colour or corner.

Controls make CS2's own menu sounds: tabs the side menu sounds, buttons the generic button sounds, choices the inventory select sound. Each toast colour plays its CS2 sound when the toast appears.

### What CS2 allows

CS2 checks this layout before showing it. Allowed are `Panel` (`id`, `class`, `hittest`), `Label` (also `text`), `Image` (`id`, `class`, `hittest`, `src`) and `Button` (only `id` and `class`). An `Image` `src` is fixed in the layout and must start with `s2r://panorama/images/` (a game image, or the addon's own under `panorama/images/custom_game/`); the server cannot change it, so every picture the server picks (rank emblems, icons, medal tiers) is a CSS class instead. Any other element or attribute (for example `hittest` on a `Button`) makes every player's game reject the whole panel without a visible error; the client console then says *Layout xml did not pass CustomHud validation*. The full-screen root, the toast stack and the promotion banner are `hittest="false"`, so they never take the mouse from the game or from other plugins' menus. No scripts, game fonts only. The layout tests enforce all of this.

The panel reuses CS2's own stylesheet and UI sounds. If a Valve update renames a sound or a game image, that one part goes quiet or blank; the rest of the panel keeps working.

The rank emblems and the icons are copies. CS2 draws an SVG at the size written in the file, and the game's emblems are only 32x13 pixels and its icons about 32x32, so they looked blurry at the panel's size. The addon carries copies of Valve's emblems and icons with only that size raised (emblems 320x130, icons 256 on the longer side). They must stay under `panorama/images/custom_game/`: it is the only image folder the Workshop Manager packs, and images anywhere else are silently left out of the upload. The tools read the game's own `pak01_dir.vpk` and need only Python:

```bash
python panorama/tools/check_game_images.py "<CS2>/game/csgo/pak01_dir.vpk"
python panorama/tools/gen_k4_ranks.py "<CS2>/game/csgo/pak01_dir.vpk"
python panorama/tools/gen_k4_icons.py "<CS2>/game/csgo/pak01_dir.vpk"
```

`check_game_images.py` exits with an error and names the file when a game image the panel uses is gone or a copy no longer matches the game; `gen_k4_ranks.py` then rewrites all 38 emblems, and `gen_k4_icons.py` every icon the stylesheet references (run `gen_k4_css.py` first when you add an icon).

### Toasts while the window is open

Toasts sit under the kill feed at the right, 420x80 each, newest on top, at most three. While the window is open they would cover it, so the stack moves under the window (the server sets `k4-open` on `k4-toasts`): the window ends at y 920 and one toast fits below it. Only one shows then; the rest wait and show one after another.

### Server and addon versions

Players and servers update at different times. Every part a newer server fills starts hidden in the layout (`k4-gone`), so a server older than the addon never shows it, and a server newer than a player's addon writes to parts the player does not have, which does nothing.

8.0 adds an out-of-date bar. The server sets the class `k4-c8` on the window (contract version 8). The footer's `k4-outdated` bar ("K4 files do not match this server") is visible by default, and only the 8.0 stylesheet hides it, under `.k4-c8`. A player whose addon does not match the server's contract sees that one line; everyone else never sees it. A contract change that renames or removes ids bumps the number on both sides.

**8.0 is a one-time break.** It renamed or removed most 7.x ids (the sidebar, the Stats and Ranks pages and the map background are gone). A 7.x server cannot fill an 8.0 addon, and an 8.0 server cannot fill a 7.x addon. Publish the Workshop addon and update the server on the same day. A 7.x server with the 8.0 addon opens an empty window that shows the out-of-date bar, because `k4-panel` and its default `k4-closed` keep their names.

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
   mm_client_extra_addons "3811667212"
   ```

   `mm_client_extra_addons` sends the addon to players only; the server itself does not need the panel files (`mm_extra_addons` would also mount it on the server). If other addons are already listed, separate the IDs with commas. If you published your own copy, use its ID instead.
3. **Enable the panel in K4.** In `configs/plugins/K4-System/k4-system.jsonc` set:

   ```jsonc
   "panorama": { "enabled": true }
   ```

4. **Restart the server**, join, and type `!k4`.

With the panel on, the K4 commands open their tab instead of printing to chat. Promotions, demotions, finished missions, unlocked achievements, the round summary, and more show as a small pop-up (a toast) at the right side of the screen, below the kill feed, instead of a chat line; see [Toasts](#toasts). Single point messages after the round summary stay in chat. Click **✕** (top right) or press Tab to close the panel. While it is open the mouse controls the panel, not your aim, so K4 also closes it when freeze time ends and after two minutes without a click (the server cannot see scrolling, so it waits longer than 7.x did).

## Toasts

Toasts stack under the kill feed, newest on top, at most three at once. Each player can turn them off with `!toasts` (`commands.toasts`) or on the settings screen (the gear: Pop-ups, Chat or Both); K4 remembers it. A player with toasts off gets the chat line instead wherever one exists (round summary, rank changes, finished missions, the daily streak).

To keep the panel but use chat for everyone, set `"toasts": false` under `panorama` in `k4-system.jsonc`. Every player then gets the chat line wherever one exists, as with toasts turned off; progress pop-ups without a chat line (almost-done missions and achievements, leaderboard climbs, the rank-at-risk warning) are not shown. The toasts button is hidden, and `!toasts` stays off. Each player's own choice is kept for when toasts are turned on again.

The colors below are the names the server sends. Green, red and gold are drawn muted, blue as light grey, and purple as grey.

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
| `!k4` shows nothing at all | Start CS2 with the launch option `-condebug`, join, type `!k4`, and open `game/csgo/console.log`. *Layout xml did not pass CustomHud validation* means the layout uses something CS2 does not allow (see above). *Failed to load layout* means the addon is not on your client: check `mm_client_extra_addons`, the ID, and that the item is Public and approved, then rejoin |
| The server console says *Addon download ... failed to start* right after a restart | MultiAddonManager tried before the server logged on to Steam. Run `changelevel <current map>` once; after that the addon stays on disk |
| You are not sure the server side works | Run `css_k4_hud` in the server console. It lists the panel entity and, for each player, whether the panel is open and the mouse is captured |
| The rank picture is missing | Set `"rank-icons": "text"` to show the rank tag instead, and report it |
| The server log says the panel could not be created | The server's CS2 build or CounterStrikeSharp is too old for `custom_hud_layout`. Update both. Commands use chat meanwhile |
| The panel appears but looks unstyled | The `.css` was not compiled. Repeat step 4 and upload again |
| Old look after an update | Players must rejoin to download the new addon version |

Turning `panorama.enabled` back to `false` returns every command to the chat messages and the center-screen mission card.
