# K4 Panorama addon

The clickable K4 panel (`!k4`, `!rank`, `!missions`, `!top`, `!ranks`, `!stats`) and its toast pop-ups are a CS2 Panorama layout. A server plugin cannot add Panorama files, so they ship to players as a Steam Workshop addon. The server mounts that addon with MultiAddonManager, players download it when they join, and K4 fills in the text and handles the clicks.

| File | What it is |
| --- | --- |
| `k4system/panorama/layout/custom_game/k4_ui.xml` | The panel and toast layout |
| `k4system/panorama/styles/custom_game/k4_ui.css` | Colors, sizes, and the hidden or shown states |
| `k4system/panorama/images/custom_game/k4/ranks/` | Sharp copies of CS2's rank emblems (see below) |
| `tools/gen_k4_ui.py`, `tools/gen_k4_css.py` | Generate the layout and stylesheet. Edit these, then run them |
| `tools/gen_k4_ranks.py` | Writes the rank emblem copies from the game's own files |

The plugin finds the layout at `panorama/layout/custom_game/k4_ui.xml`. Keep that path.

CS2 checks this layout before showing it: it may only contain `Panel`, `Label`, and `Button` elements with `id`, `class`, and `text` attributes. Anything else (an `Image`, `hittest`, `src`) makes every player's game reject the whole panel without a visible error; the client console then says *Layout xml did not pass CustomHud validation*. The layout tests enforce this.

The panel reuses CS2's own stylesheet, icons (`panorama/images/icons/...`), and UI sounds. If a Valve update renames one of those files, that icon goes blank; the rest of the panel keeps working.

The rank emblems are the one exception. CS2 draws an SVG at the size written in the file, and the game's rank emblems are only 32×13 pixels, so they looked blurry at the panel's size. The panel cannot use the `Image` element that CS2's own menus use to draw them larger. The addon therefore carries copies of Valve's emblems with only that size raised ten times (320×130). They must stay under `panorama/images/custom_game/`: it is the only image folder the Workshop Manager packs, and images anywhere else are silently left out of the upload. To refresh them after a CS2 update, unpack `panorama/images/icons/skillgroups/skillgroup0..18.vsvg_c` and `wingman0..18.vsvg_c` with Source2Viewer-CLI and run `tools/gen_k4_ranks.py <folder>`.

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

With the panel on, the K4 commands open their tab instead of printing to chat. Promotions, demotions, and finished missions show as a small pop-up at the right side of the screen, below the kill feed, instead of a chat line. Point messages stay in chat. Click **✕** (top right) to close the panel; the scoreboard key (**Tab**) should close it too, but check that on your server. While it is open the mouse controls the panel, not your aim, so K4 also closes it when freeze time ends and after 45 seconds without a click.

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
