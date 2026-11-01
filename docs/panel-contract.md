# Panel contract (7.1)

The K4 panel is two halves that ship apart: the layout and stylesheet in the Workshop addon (`panorama/`), and the server code that fills them (`K4-System/src/Hud/PanoramaHud.cs`). This page is the contract between them: every id, dialog variable and class the server uses. Change both sides together, and the layout tests (`PanoramaLayoutTests`) check the ids and classes listed here.

The design is the canvas "K4 Panel Redesign" (full screen, CS2 main-menu style, visual first).

## Rules the layout must follow

- Only `Panel`, `Label` and `Button`. `Panel` may have `id`, `class`, `hittest`; `Label` also `text`; `Button` only `id` and `class`. Anything else makes CS2 reject the whole layout.
- Every picture is a CSS `background-image` class. The server picks pictures by swapping classes, never by URL.
- A dialog variable set on a panel reaches every label inside it, so `{s:name}` below means "a Label with `text="{s:name}"` somewhere inside that panel".
- Parts a newer server fills start with `k4-gone`, so an older server never shows them empty.
- Bar widths are the classes `w0` to `w20` (5% steps) on the bar's fill panel.

## Shared class groups

| Group | Classes | Default in the layout |
| --- | --- | --- |
| Rank emblem | `sg-0` … `sg-18` (competitive), `wg-0` … `wg-18` (wingman) | `sg-0` |
| Game icon | `ic-<name>` (icons/ui), `eq-<weapon>` (icons/equipment), `dn-<name>` (hud/deathnotice) | none |
| Team patch | `tp-t`, `tp-ct`, and `ic-watch` for spectators | none |
| Medal tier | `t-gold`, `t-silver`, `t-bronze`, `t-locked` on a ring panel | `t-locked` |
| Rank color | `col-<color>` (the 21 chat color names) | none |
| Map background | `map-<mapname>` for every map with a screenshot in CS2 (`map_icons/screenshots/1080p/<map>_png`), else `map-default` | `map-default` |
| Toast color | `k4-tc-green`, `k4-tc-gold`, `k4-tc-red`, `k4-tc-blue`, `k4-tc-purple` | `k4-tc-green` |
| Toast length | `k4-len-short` (2.5 s), `k4-len-normal` (3.5 s), `k4-len-long` (5 s): the timer bar's shrink time | `k4-len-normal` |
| State | `k4-closed`, `k4-show`, `k4-gone`, `k4-active`, `k4-done`, `k4-disabled`, `k4-current`, `k4-pinned`, `k4-locked`, `k4-on`, `k4-today`, `k4-easy`, `k4-medium`, `k4-hard`, `k4-run` | |

## Frame

| Id | Kind | Variables and classes |
| --- | --- | --- |
| `k4-panel` | Panel, full screen | `k4-closed` while closed |
| `k4-bg` | Panel, full screen, class `map-default` always | the fallback background |
| `k4-bg-map` | Panel inside `k4-bg`, covering it | map background group; a `map-<name>` the stylesheet does not define draws nothing, so the fallback shows through |
| `k4-me` | Button (opens the profile) | `{s:name}`, `{s:meta}`; inside `k4-me-icon` (rank emblem group) |
| `k4-nav-home`, `k4-nav-profile`, `k4-nav-missions`, `k4-nav-achievements`, `k4-nav-leaderboard`, `k4-nav-ranks`, `k4-nav-stats` | Button | `{s:label}`; `k4-active` on the open page; `k4-gone` when the module is off |
| `k4-season` | Panel, starts `k4-gone` | `{s:title}`, `{s:left}`; bar `k4-season-bar` |
| `k4-event` | Panel, starts `k4-gone` | `{s:event}` |
| `k4-close` | Button | |
| `k4-view-home`, `k4-view-profile`, `k4-view-missions`, `k4-view-achievements`, `k4-view-leaderboard`, `k4-view-ranks`, `k4-view-stats` | Panel | `k4-show` on the open page |

The copyright line "© 2026 karola3vax" is fixed text in the layout.

## Home

| Id | Variables and classes |
| --- | --- |
| `k4-h-rank-icon` | rank emblem group (large) |
| `k4-h-rank-name` | `{s:rank}`, rank color group |
| `k4-view-home` | `{s:points}`, `{s:place}`, `{s:next}` |
| `k4-h-bar` | bar |
| `k4-h-missions` (Button, opens Missions) | slots `k4-h-m0` … `k4-h-m3` (start `k4-gone`): `{s:value}`, icon `k4-h-m{i}-icon`, `k4-done` |
| `k4-h-ach` (Button, opens Achievements) | slots `k4-h-a0` … `k4-h-a2` (start `k4-gone`): `{s:name}`, ring `k4-h-a{i}-ring` (tier), icon `k4-h-a{i}-icon`, bar `k4-h-a{i}-bar` |
| `k4-h-top` (Button, opens Leaderboard) | slots `k4-h-p0` (2nd), `k4-h-p1` (1st), `k4-h-p2` (3rd), start `k4-gone`: `{s:name}`, `{s:pos}`, icon `k4-h-p{i}-icon` |
| `k4-h-q0` … `k4-h-q4` (Buttons, open Stats) | `{s:value}`, icon `k4-h-q{i}-icon`; start `k4-gone` |

## Profile

| Id | Variables and classes |
| --- | --- |
| `k4-view-profile` | `{s:name}`, `{s:points}`, `{s:place}`, `{s:hint}`, `{s:fav}`, `{s:favkills}`, `{s:none}` |
| `k4-pf-icon` / `k4-pf-badge` | rank emblem group / text badge `{s:tag}` (one of the two is `k4-gone`) |
| `k4-pf-rank` | `{s:rank}`, rank color group |
| `k4-pf-sc0` … `k4-pf-sc2` (start `k4-gone`) | `{s:name}`, ring `k4-pf-sc{i}-ring` (tier), icon `k4-pf-sc{i}-icon` |
| `k4-pf-sc-hint` (starts `k4-gone`) | the `{s:hint}` label |
| `k4-pf-st0` … `k4-pf-st5` | `{s:value}`, `{s:label}`, icon `k4-pf-st{i}-icon` |
| `k4-pf-fav` (starts `k4-gone`) | icon `k4-pf-fav-icon` (weapon) |
| `k4-pf-ss0` … `k4-pf-ss2` (start `k4-gone`) | `{s:season}`, `{s:pos}`, icon `k4-pf-ss{i}-icon` |
| `k4-pf-ss-none` (starts `k4-gone`) | the `{s:none}` label |
| `k4-pf-back` (Button, starts `k4-gone`) | `{s:back}` |
| `k4-pf-toasts`, `k4-pf-chat` (Buttons, start `k4-gone`, own profile only) | `{s:label}`, `k4-on` while on |

## Missions

| Id | Variables and classes |
| --- | --- |
| `k4-view-missions` | `{s:reset}`, `{s:streak}`, `{s:bonus}` |
| `k4-m0` … `k4-m2` (start `k4-gone`) | `{s:diff}`, `{s:reward}`, `{s:desc}`, `{s:progress}`; icon `k4-m{i}-icon`; bar `k4-m{i}-bar`; difficulty `k4-easy`/`k4-medium`/`k4-hard`; `k4-done` |
| `k4-reroll-0` … `k4-reroll-2` (Buttons, start `k4-gone`) | `{s:label}` |
| `k4-mw` (starts `k4-gone`) | `{s:left}`, `{s:desc}`, `{s:progress}`, `{s:reward}`; icon `k4-mw-icon`; bar `k4-mw-bar`; `k4-done` |
| `k4-sd0` … `k4-sd6` | `k4-on` for a finished day, `k4-today` for today |

## Achievements

| Id | Variables and classes |
| --- | --- |
| `k4-view-achievements` | `{s:count}`, `{s:page}` |
| `k4-af-all`, `k4-af-progress`, `k4-af-unlocked`, `k4-af-shown` (Buttons) | `{s:label}`, `k4-active` |
| `k4-a0` … `k4-a14` (Buttons, start `k4-gone`) | `{s:name}`, `{s:goal}`; ring `k4-a{i}-ring` (tier, `t-locked` while locked); icon `k4-a{i}-icon`; bar `k4-a{i}-bar`; `k4-pinned` shows the star |
| `k4-ach-prev`, `k4-ach-next` (Buttons) | `k4-disabled` |

## Leaderboard

| Id | Variables and classes |
| --- | --- |
| `k4-view-leaderboard` | `{s:season}`, `{s:status}`, `{s:page}` |
| `k4-lb-newer`, `k4-lb-older`, `k4-lb-prev`, `k4-lb-next` (Buttons) | `k4-disabled` |
| `k4-lb-p0` (2nd), `k4-lb-p1` (1st), `k4-lb-p2` (3rd) (Buttons, start `k4-gone`) | `{s:name}`, `{s:points}`, `{s:pos}`; icon `k4-lb-p{i}-icon` |
| `k4-lb-0` … `k4-lb-6` (Buttons, start `k4-gone`) | `{s:pos}`, `{s:name}`, `{s:points}`; icon `k4-lb-{i}-icon` |
| `k4-lb-me` (starts `k4-gone`) | `{s:pos}`, `{s:name}`, `{s:points}`; icon `k4-lb-me-icon` |

## Ranks

| Id | Variables and classes |
| --- | --- |
| `k4-view-ranks` | `{s:page}` |
| `k4-rk0` … `k4-rk19` (start `k4-gone`) | `{s:name}` (label `k4-rk{i}-name`, rank color group), `{s:points}`; icon `k4-rk{i}-icon`; `k4-current`; `k4-rk{i}-gift` and `k4-rk{i}-you` start `k4-gone` |
| `k4-rk-prev`, `k4-rk-next` (Buttons, start `k4-gone`) | shown only with more than 20 ranks |

## Stats

| Id | Variables and classes |
| --- | --- |
| `k4-view-stats` | `{s:total}` |
| `k4-st0` … `k4-st7` (start `k4-gone`) | `{s:value}`, `{s:label}`; icon `k4-st{i}-icon` |
| `k4-sw0` … `k4-sw2` (start `k4-gone`) | `{s:kills}`; icon `k4-sw{i}-icon` (weapon); bar `k4-sw{i}-bar` |
| `k4-pt` (starts `k4-gone`) | the playtime card |
| `k4-pt-t`, `k4-pt-ct`, `k4-pt-spec` | `{s:name}`, `{s:time}`; bar `k4-pt-t-bar`, `k4-pt-ct-bar`, `k4-pt-spec-bar` |

## Toasts

Toasts sit under the kill feed at the right, newest on top, at most three at once.

| Id | Variables and classes |
| --- | --- |
| `k4-toasts` | Panel, `hittest="false"`, holds the stack and `k4-t-more` |
| `k4-t0` … `k4-t2` (start `k4-gone`) | `{s:kind}`, `{s:text}`, `{s:value}`; toast color group; toast length group; icon `k4-t{i}-icon` (any picture group: icon, emblem, team patch); medal ring `k4-t{i}-ring` (tier, `k4-gone` when the toast has no medal); progress `k4-t{i}-prog` (`k4-gone` without progress) with bar `k4-t{i}-bar`; timer `k4-t{i}-timer`, which shrinks from full to empty over the toast length while it has `k4-run` |
| `k4-t-more` (starts `k4-gone`) | `{s:more}` |
| `k4-promo` (starts `k4-gone`) | the promotion banner: `{s:title}`, `{s:rank}`; icon `k4-promo-icon` (rank emblem group) |

A toast carries CS2's own sound through its color class, like 7.0 (`UI.Mission.Complete` for green, `UIPanorama.EOM.CardReveal` for gold).

## Server API for toasts

`K4-System/src/Hud/Toasts.cs` defines what other services send; `PanoramaHud.Toast` shows it and respects the player's toast setting.

```csharp
hud.Hud?.Toast(player, new Toast(kind, text, value, visual, ToastColor.Gold, ToastLength.Long, BarPercent: null, Tier: "gold"));
hud.Hud?.Promote(player, rank);   // the large promotion banner
```

`kind`, `text` and `value` are already in the player's language. `visual` is one picture class from the groups above.
