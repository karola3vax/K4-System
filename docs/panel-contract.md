# Panel contract (8.0)

The K4 panel is two halves that ship apart: the layout and stylesheet in the Workshop addon (`panorama/`), and the server code that fills them (`K4-System/src/Hud/PanoramaHud.cs`). This page is the contract between them: every id, dialog variable and class the server uses. Change both sides together. `PanoramaLayoutTests` checks the layout and stylesheet against this page.

The 8.0 menu is a centered 1240x760 window with tabs along the top: Home, Missions, Achievements, Leaderboard, Duels and Profile, plus a gear for settings. Stats live in Profile and the rank list lives in Leaderboard. Long lists scroll on the player's own PC. The design comes from three councils and the owner's picks (2026-11-20).

## Contract version

`PanoramaHud.ContractVersion` is `8`. The server sets the class `k4-c8` on `k4-panel` every time it draws the frame. The layout's `k4-outdated` bar is visible by default and the stylesheet hides it only under `.k4-c8`. So a player whose addon is older or newer than the server sees one line saying their K4 files are out of date, and everyone else never sees it.

- Adding a part inside a version keeps the `k4-gone` rule: the part starts `k4-gone` and an older server never shows it.
- A change that renames or removes ids bumps the number, in `ContractVersion`, in the stylesheet and on this page.
- `k4-panel`, its default class `k4-closed`, and `k4-outdated` keep their names in every version, so any server can at least open the window and the bar explains the rest.

8.0 renamed and removed most 7.x ids (the sidebar, Stats and Ranks pages are gone). A 7.x server cannot fill an 8.0 addon and an 8.0 server cannot fill a 7.x addon: update the server and the Workshop addon on the same day.

## Rules the layout must follow

- Only `Panel`, `Label`, `Image` and `Button`. `Panel` may have `id`, `class`, `hittest`; `Label` also `text`; `Image` `id`, `class`, `hittest`, `src`; `Button` only `id` and `class`. Anything else makes CS2 reject the whole layout. An `Image` `src` is fixed in the addon and must start with `s2r://panorama/images/`.
- Pictures the server picks are CSS `background-image` classes. The server picks pictures by swapping classes, never by URL.
- A dialog variable set on a panel reaches every label inside it, so `{s:name}` below means "a Label with `text="{s:name}"` somewhere inside that panel". Labels shared by every row of a list (button captions) are set once on the list panel.
- Parts a newer server fills start with `k4-gone`, so an older server never shows them empty. Every pooled row starts `k4-gone`.
- Bar widths are the classes `w0` to `w20` (5% steps) on the bar's fill panel.
- The root panel (class `k4-root`), the toast stack and the promotion banner are `hittest="false"`, so they never take the mouse from the game or other plugins. Every scroll box (`k4-*-list`) is `hittest="true"` with the class `k4-scroll`, which is what lets the mouse wheel scroll it.
- The server never sees scrolling or hover. Anything the player must reach without scrolling (their own row) sits outside the scroll box.

## Pools

Fixed numbers of pre-built rows. The C# constant and the layout must match (`PanoramaLayoutTests` checks both).

| Pool | Rows | C# constant |
| --- | --- | --- |
| Leaderboard players `k4-lb-r{i}` | 100 | `PanoramaHud.LeaderboardRows` |
| Rank list `k4-rk{i}` | 40 | `PanoramaHud.RankRows` |
| Achievements `k4-a{i}` | 64 | `PanoramaHud.AchievementRows` |
| Duel players `k4-dp{i}` | 64 | `PanoramaHud.DuelPlayerRows` |
| Past seasons `k4-pf-ss{i}` | 24 | `PanoramaHud.SeasonRows` |
| Running duels `k4-dl{i}` | 4 | `PanoramaHud.LiveDuelRows` |

A list longer than its pool ends with its `-more` label (`{s:more}`, for example "12 more not shown").

## Shared class groups

| Group | Classes | Default in the layout |
| --- | --- | --- |
| Rank emblem | `sg-0` … `sg-18` (competitive), `wg-0` … `wg-18` (wingman) | `sg-0` |
| Game icon | `ic-<name>` (icons/ui), `eq-<weapon>` (icons/equipment), `dn-<name>` (hud/deathnotice) | none |
| Team patch | `tp-t`, `tp-ct`, and `ic-watch` for spectators | none |
| Medal tier | `t-gold`, `t-silver`, `t-bronze`, `t-locked` on a ring panel | `t-locked` |
| Place medal | `k4-p1`, `k4-p2`, `k4-p3` on a leaderboard row (gold, silver, bronze number and edge mark) | none |
| Rank color | `col-<color>` (the 21 chat color names) | none |
| Toast color | `k4-tc-green`, `k4-tc-gold`, `k4-tc-red`, `k4-tc-blue`, `k4-tc-purple` | `k4-tc-green` |
| Toast length | `k4-len-short` (2.5 s), `k4-len-normal` (3.5 s), `k4-len-long` (5 s): the timer bar's shrink time | `k4-len-normal` |
| Next card kind | `k4-n-duel`, `k4-n-swap`, `k4-n-showcase`, `k4-n-ach`, `k4-n-season` | none |
| State | `k4-closed`, `k4-show`, `k4-gone`, `k4-active`, `k4-done`, `k4-disabled`, `k4-current`, `k4-pinned`, `k4-locked`, `k4-on`, `k4-today`, `k4-easy`, `k4-medium`, `k4-hard`, `k4-run`, `k4-out`, `k4-me`, `k4-ok`, `k4-bad`, `k4-empty`, `k4-open`, `k4-lb-ranklist`, `k4-wide` | |

## Frame

| Id | Kind | Variables and classes |
| --- | --- | --- |
| class `k4-root` | the root Panel, full screen, `hittest="false"`, no id (CS2's resource compiler rejects an id on the root panel) | holds the window, the toasts and the promotion banner |
| `k4-panel` | Panel, the centered 1240x760 window | `k4-closed` while closed; `k4-c8` from the server |
| `k4-outdated` | Panel | fixed English text in the layout; hidden by the stylesheet under `.k4-c8` |
| `k4-nav-home`, `k4-nav-missions`, `k4-nav-achievements`, `k4-nav-leaderboard`, `k4-nav-duels`, `k4-nav-profile` | Button, a top tab | `{s:label}`; `k4-active` on the open tab; `k4-gone` when the module is off |
| `k4-nav-duels-badge` | Label inside `k4-nav-duels`, starts `k4-gone` | `{s:count}`: challenges waiting for this player |
| `k4-event` | Panel, starts `k4-gone` | `{s:event}` (for example "2x points, 14 min") |
| `k4-nav-settings` | Button, the gear | `k4-active` on the settings screen |
| `k4-close` | Button | |
| `k4-crumbs` | Panel, the row under the tabs | `{s:crumb}` (where you are, for example "Leaderboard / zabullet") |
| `k4-back` | Button inside `k4-crumbs`, starts `k4-gone` | `{s:label}`; goes back to the screen you came from |
| `k4-season` | Panel inside `k4-crumbs`, starts `k4-gone` | `{s:title}`, `{s:left}`; bar `k4-season-bar` |
| `k4-duel-bar` | Panel under `k4-crumbs`, starts `k4-gone` | a challenge waiting for this player, on every screen: `{s:title}`, `{s:text}`, `{s:left}`; icon `k4-duel-bar-icon` (picture group); bar `k4-duel-bar-timer` (time left to answer) |
| `k4-duel-bar-accept`, `k4-duel-bar-decline` | Button inside `k4-duel-bar` | `{s:label}` |
| `k4-view-home`, `k4-view-missions`, `k4-view-achievements`, `k4-view-leaderboard`, `k4-view-duels`, `k4-view-profile`, `k4-view-settings` | Panel | `k4-show` on the open screen |
| `k4-status` | Panel in the footer, starts `k4-gone` | `{s:status}`: the result of the player's last click; `k4-ok` or `k4-bad`. While the window is closed the same message goes to chat |
| `k4-keys` | Panel in the footer | `{s:keys}` (for example "Tab closes") |

The copyright line "© 2026 karola3vax" is fixed text in the footer, always visible.

## Home

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-h-rank` | Button, opens Leaderboard on the rank list | `{s:rank}` (label `k4-h-rank-name`, rank color group), `{s:points}`, `{s:place}` (for example "#12 of 340"), `{s:next}` (next rank, empty at the top), `{s:togo}`; icon `k4-h-rank-icon` (rank emblem group); bar `k4-h-bar` |
| `k4-h-top` | Button, opens Leaderboard | `{s:title}`; rows `k4-h-p0` … `k4-h-p4`, the top 5 (start `k4-gone`): `{s:pos}`, `{s:name}`, `{s:points}`; icon `k4-h-p{i}-icon`; row `k4-h-me` (starts `k4-gone`): `{s:pos}`, `{s:points}` |
| `k4-h-today` | Panel | `{s:title}`, `{s:reset}` |
| `k4-h-m0` … `k4-h-m3` | Button, opens Missions, start `k4-gone` | `k4-h-m3` is the weekly mission. `{s:desc}`, `{s:progress}`, `{s:reward}`; icon `k4-h-m{i}-icon`; bar `k4-h-m{i}-bar`; `k4-done` |
| `k4-h-sd0` … `k4-h-sd6` | Panel | `{s:day}`; `k4-on` for a finished day, `k4-today` for today |
| `k4-h-next` | Panel | `{s:title}` |
| `k4-h-n0` … `k4-h-n2` | Button, start `k4-gone` | a "do this next" card, only real items: `{s:title}`, `{s:text}`; icon `k4-h-n{i}-icon` (picture group); next card kind group. The server keeps what each card opens |

## Missions

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-missions` | Panel | `{s:title}`, `{s:reset}`, `{s:swaps}` (for example "1 swap left today"), `{s:streak}`, `{s:streakhint}` (shown as one line: "0 days in a row · ...") |
| `k4-m0` … `k4-m2` | Panel, start `k4-gone` | `{s:diff}`, `{s:reward}`, `{s:desc}`, `{s:progress}`, `{s:paid}`; icon `k4-m{i}-icon`; bar `k4-m{i}-bar`; difficulty `k4-easy` / `k4-medium` / `k4-hard`; `k4-done` |
| `k4-reroll-0` … `k4-reroll-2` | Button, start `k4-gone` | `{s:label}`; asks first (below) |
| `k4-m0-ask` … `k4-m2-ask` | Panel, start `k4-gone` | the swap question inside the card: `{s:ask}` |
| `k4-m{i}-yes`, `k4-m{i}-no` | Button inside `k4-m{i}-ask` | `{s:label}` |
| `k4-mw` | Panel, starts `k4-gone` | `{s:left}`, `{s:desc}`, `{s:progress}`, `{s:reward}`; icon `k4-mw-icon`; bar `k4-mw-bar`; `k4-done` |
| `k4-sd0` … `k4-sd6` | Panel | `k4-on` for a finished day, `k4-today` for today; `{s:day}`, `{s:bonus}`, `{s:tip}` |

## Achievements

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-achievements` | Panel | `{s:count}`, `{s:showcase}` (the strip caption, for example "On your profile 2/3") |
| `k4-a-sc0` … `k4-a-sc2` | Button, the showcase slots | `{s:name}`, `{s:empty}`; ring `k4-a-sc{i}-ring` (tier); icon `k4-a-sc{i}-icon`; `k4-empty` for a free slot. A click on a filled slot takes it off the profile |
| `k4-af-all`, `k4-af-progress`, `k4-af-unlocked`, `k4-af-shown` | Button | `{s:label}`, `k4-active` |
| `k4-a-list` | Panel, scroll box | |
| `k4-a0` … `k4-a63` | Button, start `k4-gone` | `{s:name}`, `{s:goal}`, `{s:desc}`, `{s:tip}`; ring `k4-a{i}-ring` (tier, `t-locked` while locked); icon `k4-a{i}-icon`; bar `k4-a{i}-bar`; `k4-done` when unlocked; `k4-pinned` when on the profile. A click on an unlocked card pins or unpins it |
| `k4-a-more` | Label, starts `k4-gone` | `{s:more}` |
| `k4-a-empty` | Panel, starts `k4-gone` | `{s:none}`; button `k4-a-show-unlocked` (`{s:label}`) switches the filter to Unlocked |

## Leaderboard

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-leaderboard` | Panel | `{s:season}`, `{s:status}` (Live or Final), `{s:count}` (for example "of 340 players"); `k4-lb-ranklist` while the rank list shows (hides the season switch and the count) |
| `k4-lb-tab-players`, `k4-lb-tab-ranks` | Button, the Players / Rank list switch | `{s:label}`, `k4-active` |
| `k4-lb-newer`, `k4-lb-older` | Button | `k4-disabled`; the season switch (players only) |
| `k4-lb-players` | Panel | `k4-gone` while the rank list shows |
| `k4-lb-list` | Panel, scroll box | `{s:profile}`, `{s:duel}`: the row button captions |
| `k4-lb-r0` … `k4-lb-r99` | Panel, start `k4-gone` | `{s:pos}`, `{s:name}`, `{s:rank}`, `{s:points}`; icon `k4-lb-r{i}-icon` (rank emblem group); place medal group; `k4-me` on the player's own row |
| `k4-lb-r{i}-profile` | Button inside the row | opens that player's profile |
| `k4-lb-r{i}-duel` | Button inside the row, starts `k4-gone` | opens Duels with that player picked; shown only for a player who is online now and can be challenged (live season, Duels on) |
| `k4-lb-more` | Label, starts `k4-gone` | `{s:more}` |
| `k4-lb-me` | Panel outside the scroll box, starts `k4-gone` | the player's own row, always visible: `{s:pos}`, `{s:name}`, `{s:rank}`, `{s:points}`; icon `k4-lb-me-icon` |
| `k4-lb-ranks` | Panel | `k4-gone` while the players show |
| `k4-lb-you` | Panel outside the scroll box | `{s:text}` (for example "You: Gold Nova III. Next: Gold Nova Master in 640") |
| `k4-rk-list` | Panel, scroll box | |
| `k4-rk0` … `k4-rk39` | Panel, start `k4-gone` | `{s:name}` (label `k4-rk{i}-name`, rank color group), `{s:points}`, `{s:reward}`, `{s:tip}`; icon `k4-rk{i}-icon`; `k4-current`; `k4-rk{i}-gift` and `k4-rk{i}-you` start `k4-gone` |
| `k4-rk-more` | Label, starts `k4-gone` | `{s:more}` |

## Duels

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-duels` | Panel | `{s:pick}`, `{s:types}`, `{s:stakes}` (captions), `{s:online}`, `{s:summary}` (what the challenge will be), `{s:status}` (why sending is not possible now, empty otherwise), `{s:live}` |
| `k4-dp-list` | Panel, scroll box | |
| `k4-dp0` … `k4-dp63` | Button, start `k4-gone` | an online player: `{s:name}`, `{s:points}`, `{s:state}` (for example "In a duel"); icon `k4-dp{i}-icon` (rank emblem group); `k4-active` when picked; `k4-disabled` when they cannot be challenged now |
| `k4-dp-more` | Label, starts `k4-gone` | `{s:more}` |
| `k4-dp-none` | Label, starts `k4-gone` | `{s:none}` |
| `k4-dt0` … `k4-dt5` | Button, start `k4-gone` | a duel type: `{s:name}`, `{s:tip}` (`k4-has-tip` card); icon `k4-dt{i}-icon` (picture group); `k4-active` when picked |
| `k4-ds0` … `k4-ds4` | Button, start `k4-gone` | a stake: `{s:label}`; `k4-active` when picked; `k4-disabled` over the allowed maximum |
| `k4-d-send` | Button | `{s:label}`; `k4-disabled` until a player, type and stake are picked and the challenge is allowed |
| `k4-d-wait` | Panel, starts `k4-gone` | the player's own challenge waiting for an answer: `{s:text}`; bar `k4-d-wait-bar` (time left) |
| `k4-d-takeback` | Button inside `k4-d-wait` | `{s:label}` |
| `k4-d-mine` | Panel, starts `k4-gone` | the player's own running duel: `{s:title}`, `{s:score}`, `{s:text}`; icon `k4-d-mine-icon`; bar `k4-d-mine-bar` (time left) |
| `k4-dl0` … `k4-dl3` | Panel, start `k4-gone` | other running duels: `{s:names}`, `{s:score}`, `{s:info}`; icon `k4-dl{i}-icon` |
| `k4-dl-none` | Label, starts `k4-gone` | `{s:none}` |

An incoming challenge is answered on `k4-duel-bar` (frame), on every screen.

Duel type pictures: Any `ic-kill`, No-scope `dn-noscope`, Zeus `eq-taser`, Knife `eq-knife`, Scout `eq-ssg08`, Headshot `ic-kill_headshot`.

## Profile

The player's own profile, or another player's (opened from the leaderboard; `k4-back` returns).

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-profile` | Panel | `{s:name}`, `{s:points}`, `{s:place}`, `{s:next}`, `{s:fav}`, `{s:favkills}`, `{s:none}`, `{s:hint}`, `{s:total}` (playtime total) |
| `k4-pf-icon` / `k4-pf-badge` | Panel | rank emblem group / text badge `{s:tag}` (one of the two is `k4-gone`) |
| `k4-pf-rank` | Label | `{s:rank}`, rank color group |
| `k4-pf-bar` | Panel | bar to the next rank |
| `k4-pf-challenge` | Button, starts `k4-gone` | `{s:label}`; on another player's profile when they can be challenged now |
| `k4-pf-sc0` … `k4-pf-sc2` | Button, start `k4-gone` | `{s:name}`, `{s:desc}`, `{s:tip}`; ring `k4-pf-sc{i}-ring` (tier); icon `k4-pf-sc{i}-icon`. A click on your own opens Achievements |
| `k4-pf-sc-hint` | Panel, starts `k4-gone` | the `{s:hint}` label |
| `k4-pf-st0` … `k4-pf-st7` | Panel, start `k4-gone` | `{s:value}`, `{s:label}`, `{s:tip}`; icon `k4-pf-st{i}-icon` |
| `k4-pf-sw0` … `k4-pf-sw2` | Panel, start `k4-gone` | top weapons: `{s:weapon}`, `{s:kills}`; icon `k4-pf-sw{i}-icon` (weapon); bar `k4-pf-sw{i}-bar` |
| `k4-pf-pt` | Panel, starts `k4-gone` | time per side |
| `k4-pf-pt-t`, `k4-pf-pt-ct`, `k4-pf-pt-spec` | Panel | `{s:name}`, `{s:time}`; bar `k4-pf-pt-t-bar`, `k4-pf-pt-ct-bar`, `k4-pf-pt-spec-bar` |
| `k4-pf-ss-list` | Panel, scroll box | |
| `k4-pf-ss0` … `k4-pf-ss23` | Panel, start `k4-gone` | `{s:season}`, `{s:pos}`; icon `k4-pf-ss{i}-icon` |
| `k4-pf-ss-none` | Panel, starts `k4-gone` | the `{s:none}` label |

## Settings

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-view-settings` | Panel | `{s:title}`, `{s:mode}`, `{s:points}`, `{s:tag}` (row captions) |
| `k4-set-mode-panel`, `k4-set-mode-chat`, `k4-set-mode-both` | Button | `{s:label}`; `k4-active` on the player's mode; `k4-disabled` when the server locks the mode |
| `k4-set-mode-lock` | Label, starts `k4-gone` | `{s:lock}` (for example "Set by the server") |
| `k4-set-points` | Panel, starts `k4-gone` | the point messages row (ranks module on) |
| `k4-set-points-on`, `k4-set-points-off` | Button inside `k4-set-points` | `{s:label}`, `k4-active` |
| `k4-set-tag` | Panel, starts `k4-gone` | the rank tag row (ranks module on) |
| `k4-set-tag-on`, `k4-set-tag-off` | Button inside `k4-set-tag` | `{s:label}`, `k4-active` |

## Toasts

Toasts sit under the kill feed at the right, newest on top, at most three at once. While the window is open they move under it (`k4-open` on `k4-toasts`) and only one shows at a time; the rest wait and show one after another.

| Id | Variables and classes |
| --- | --- |
| `k4-toasts` | Panel, `hittest="false"`, holds the stack and `k4-t-more`; `k4-open` while the window is open |
| `k4-t0` … `k4-t2` (start `k4-gone`) | `{s:kind}`, `{s:text}`, `{s:value}`; toast color group; toast length group; icon `k4-t{i}-icon` (any picture group: icon, emblem, team patch); medal ring `k4-t{i}-ring` (tier, `k4-gone` when the toast has no medal); `k4-wide` on the toast when it has no medal, so its picture gets the full width; progress `k4-t{i}-prog` (`k4-gone` without progress) with bar `k4-t{i}-bar`; timer `k4-t{i}-timer`, which shrinks from full to empty over the toast length while it has `k4-run` |
| `k4-t-more` (starts `k4-gone`) | `{s:more}` |
| `k4-promo` (starts `k4-gone`, `hittest="false"`) | the promotion banner: `{s:title}`, `{s:rank}`; icon `k4-promo-icon` (rank emblem group) |

A toast carries CS2's own sound through its color class (`UI.Mission.Complete` for green, `UIPanorama.EOM.CardReveal` for gold).

## Server API for toasts

`K4-System/src/Hud/Toasts.cs` defines what other services send; `PanoramaHud.Toast` shows it and respects the player's pop-up mode.

```csharp
hud.Hud?.Toast(player, new Toast(kind, text, value, visual, ToastColor.Gold, ToastLength.Long, BarPercent: null, Tier: "gold"));
hud.Hud?.Promote(player, rank);   // the large promotion banner
```

`kind`, `text` and `value` are already in the player's language. `visual` is one picture class from the groups above.

## Explanations

A card that shows a picture or number without words explains itself on hover. A card with the class `k4-has-tip` holds a `k4-tip` panel (`hittest="false"`) with a `{s:tip}` label, laid over the card and shown while the card is hovered. Cards with tips: `k4-a{i}`, `k4-rk{i}`, `k4-sd{i}`, `k4-pf-sc{i}`, `k4-pf-st{i}`, `k4-dt{i}`.

## Commands

| Command | Opens |
| --- | --- |
| `!k4`, `!rank` | Home |
| `!missions` | Missions |
| `!achievements` | Achievements |
| `!top` | Leaderboard, players |
| `!ranks` | Leaderboard, rank list |
| `!duels` | Duels |
| `!stats`, `!time` | Profile |

## Look (8.0)

Opaque graphite window (no map picture), one grey ramp, white as the only accent (the open tab's underline, the one main button of a card, the player's own "you" mark). Other color only where the game gives it meaning: rank emblems and rank names, gold, silver and bronze medals, green for done, red for lost. Sentence case, no letter spacing. Stratum for titles and numbers, Noto for text. Corners: window 6 px, cards 4 px, tags 2 px. The tokens are at the top of `gen_k4_css.py`; `The_theme_follows_the_tokens` in `PanoramaLayoutTests` fails on any other color.
