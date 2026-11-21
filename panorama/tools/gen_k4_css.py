"""Writes the K4 Panorama stylesheet (8.0): a centered 1240x760 graphite window with tabs along the top, and the toasts.

The look (docs/panel-contract.md, "Look"): one solid grey ramp, white only for the open tab's underline, the one
main button of a card and the player's own "you" mark. Other colour only where the game gives it meaning: rank
emblems and rank names, gold/silver/bronze, green for done, red for lost, T and CT. Sentence case, no letter
spacing. Stratum (stratum-bold-tf) for titles and numbers, CS2's Noto (csgostyles' Label font) for text.
Corners: window 6px, cards and buttons 4px, tags and bars 2px; medal rings are round.
PanoramaLayoutTests.The_theme_follows_the_tokens fails on any colour or corner outside TOKENS, MEANING and TIERS.

The classes the server swaps are generated at the bottom: game icons, rank emblems, team patches, medal tiers,
bar widths, rank colours, toast colours (with their CS2 sounds) and lengths, and the contract class k4-c8.
Every image is one CS2 ships, referenced the way Valve's stylesheets do (s2r://panorama/images/...), except the
rank emblems and the icons: the addon carries sharp copies of CS2's own (panorama/images/custom_game/k4/ranks
and .../icons, written by gen_k4_ranks.py and gen_k4_icons.py), because the game draws them at 32x13 and 32x32
and they looked blurry at the panel's sizes. Sounds are CS2's own UI sound events.

Panorama scales the HUD by screen height (1080 is 1:1), so the window is a fixed 1240x760 at every aspect
ratio, centered: it runs from y 160 to y 920. Inside its border: top bar 56, crumbs 40, footer 32, and the
body with 20px 24px padding, so a screen gets 1190x590 (522 high while the duel bar shows). Lists scroll on
the player's PC (overflow: squish scroll); everything else is sized to fit without scrolling.
"""
import os
import re

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "styles", "custom_game", "k4_ui.css")

# The contract version this stylesheet belongs to: the server sets k4-c<N> on k4-panel and only the matching
# stylesheet hides the out-of-date bar. Bump together with PanoramaHud.ContractVersion and docs/panel-contract.md.
CONTRACT = 8

# The grey ramp and the accent (council 2, owner-approved 2026-11-20). All solid.
TOKENS = {
    "s0": "#0B0B0C", "s1": "#131315", "s2": "#1A1A1D", "s3": "#222226", "s4": "#2B2B30",
    "line": "#26262A", "line-hi": "#3A3A40", "line-sel": "#8E8E96",
    "tx": "#EDEDED", "tx2": "#A8A8AD", "tx3": "#75757B",
    "acc": "#F2F2F2", "ink": "#0B0B0C",
}

# Colours that mean something in the game.
MEANING = {
    "ok": "#6FA886", "bad": "#C4675C",
    "gold": "#C8A45C", "silver": "#B4B4B4", "bronze": "#A97F5E",
    "side-t": "#C9A85A", "side-ct": "#8FA6BA",
}

# Server owners pick these in the config; they only ever colour a rank name.
COLORS = {
    "default": "#FFFFFF", "white": "#FFFFFF", "darkred": "#BB8181", "green": "#83B983",
    "lightyellow": "#D2CC97", "lightblue": "#ABC7E3", "olive": "#A9B686", "lime": "#A8DB92",
    "red": "#D27A7A", "lightpurple": "#CAB3E6", "purple": "#A779C3", "grey": "#B0B0B0",
    "yellow": "#CEBB84", "gold": "#C3AD7A", "silver": "#C4C8CC", "blue": "#8099CA",
    "darkblue": "#808DBC", "bluegrey": "#A9B2BE", "magenta": "#BF7DB3", "lightred": "#DC9A95",
    "orange": "#D1A075",
}

# panorama/images/icons/ui/<name>.vsvg, used as ic-<name> (stats, missions, toasts, the panel's own icons)
UI_ICONS = [
    "kill", "kill_headshot", "map_death", "stats", "stat_headshots", "stat_assists", "stat_mvps", "trophy",
    "crosshair_circle", "clock", "bullet", "hostage_rescued", "play", "winner_ribbon", "check", "hot_trend",
    "cold_trend", "trade_up", "mission_star", "bomb_c4", "xp_rank", "quest", "leader", "gift", "refresh_skinny",
    "crosshair", "first_person", "multipleenemieshere", "icon_star_multi", "shield", "health",
    "home", "profile", "watch", "icon_star", "cancel", "expand", "locked", "timer", "star", "casual",
    "settings", "warning",
]

# panorama/images/hud/deathnotice/<name>.vsvg (the kill feed's kill markers), used as dn-<name>
DEATHNOTICE_ICONS = ["penetrate", "smoke_kill", "inairkill", "blind_kill", "revenge", "domination", "noscope"]

# panorama/images/icons/equipment/<name>.vsvg (killfeed icons), used as eq-<name> for weapons
EQUIPMENT_ICONS = [
    "knife", "hegrenade", "flashbang_assist", "planted_c4", "defuser", "taser", "molotov", "incgrenade",
    "ak47", "aug", "awp", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar", "glock",
    "hkp2000", "m249", "m4a1", "m4a1_silencer", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev", "nova", "p2000",
    "p250", "p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer", "xm1014",
]

# Toast colour -> strip and kind colour, and the CS2 sound the toast plays when it appears. The names stay from
# 7.x (contract); blue and purple are the two information greys.
TOAST_COLORS = {
    "green": ("ok", "UI.Mission.Complete"),
    "gold": ("gold", "UIPanorama.EOM.CardReveal"),
    "red": ("bad", "UI.DeathMatchBonusAlertEnd"),
    "blue": ("tx2", "UIPanorama.popup_newweapon"),
    "purple": ("tx3", "UI.DeathMatchBonusAlertStart"),
}

# Toast length -> how long the timer bar takes to run out
TOAST_LENGTHS = {"short": "2.5s", "normal": "3.5s", "long": "5s"}

# Medal ring: light, middle and dark stop of the diagonal gradient
TIERS = {
    "gold": ("#F0DDB0", "#C8A45C", "#6E5A2E"),
    "silver": ("#F2F2F2", "#B4B4B4", "#5A5A5A"),
    "bronze": ("#E6C8B0", "#A97F5E", "#523B2A"),
}

# Cells spaced by a left (or top) margin; k4-first cancels it (gen_k4_ui.py first()).
GAPS_LEFT = {
    "k4-tab": 0, "k4-h-sd": 6, "k4-h-m": 10, "k4-h-s": 16, "k4-m": 14, "k4-sd": 8, "k4-dt": 10, "k4-pf-sc": 8,
    "k4-pf-st": 12, "k4-seg-item": 1, "k4-chip": 10,
}
GAPS_TOP = {"k4-pf-tiles": 12}

# Next-card kinds: the edge mark's colour. Achievements and the showcase are about medals (gold).
NEXT_KINDS = {"duel": "tx", "swap": "tx3", "showcase": "gold", "ach": "gold", "season": "tx2"}

defines = "".join(f"@define {name}: {value};\n" for name, value in {**TOKENS, **MEANING}.items())

# No text-transform anywhere: the client would uppercase in its own locale (a Turkish client wrote "MİSSİONS"),
# and 8.0 is sentence case. No letter-spacing either.
css = defines + r"""@define fast: 0.08s;

Label
{
	color: tx;
	font-weight: normal;
}

.k4-root
{
	width: 100%;
	height: 100%;
	z-index: 2000000000;
}

.k4-gone
{
	visibility: collapse;
}

.k4-grow
{
	width: fill-parent-flow( 1.0 );
}

.k4-vgrow
{
	height: fill-parent-flow( 1.0 );
}

.k4-row
{
	width: 100%;
	flow-children: right;
}

.k4-pic
{
	background-size: contain;
	background-repeat: no-repeat;
	background-position: 50% 50%;
}

.k4-disabled
{
	opacity: 0.4;
}

/* Locked: its pictures in the darkest text grey (achievements also show a grey t-locked ring) */
.k4-locked .k4-pic
{
	wash-color: tx3;
}

/* ---------------------------------------------------------------- the window */

.k4-panel
{
	width: 1240px;
	height: 760px;
	horizontal-align: center;
	vertical-align: middle;
	background-color: s0;
	border: 1px solid line;
	border-radius: 6px;
	box-shadow: #000000B8 0px 24px 72px 0px;
	opacity: 1;
	transform: scale3d( 1, 1, 1 );
	transition-property: opacity, transform;
	transition-duration: 0.12s;
	transition-timing-function: ease-out;
}

.k4-panel.k4-closed
{
	opacity: 0;
	transform: scale3d( 0.98, 0.98, 1 );
	visibility: collapse;
}

/* Behind the menu (owner 2026-11-21, from 7.x): CS2's season art in grey, dimmed as in 7.x: 97% on the left, 92% at 45%, 76% on the
   right. The frame on top holds the menu itself, top to bottom. */
.k4-bg, .k4-shade, .k4-frame
{
	width: 100%;
	height: 100%;
}

.k4-bg, .k4-shade
{
	border-radius: 6px;
}

.k4-bg
{
	background-image: url("s2r://panorama/images/backgrounds/season_stats_background_png.vtex");
	background-size: cover;
	background-position: 50% 50%;
	saturation: 0;
}

.k4-shade
{
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #0B0B0CF7 ), color-stop( 0.45, #0B0B0CEB ), to( #0B0B0CC2 ) );
}

.k4-frame
{
	flow-children: down;
}

/* Top bar: brand, tabs, event, gear, close */
.k4-top
{
	width: 100%;
	height: 56px;
	flow-children: right;
	padding: 0px 12px 0px 20px;
	border-bottom: 1px solid line;
}

.k4-brand
{
	flow-children: right;
	vertical-align: center;
	margin-right: 20px;
}

.k4-brand-name
{
	font-size: 24px;
	color: tx;
	vertical-align: center;
}

.k4-brand-sub
{
	font-size: 15px;
	color: tx3;
	margin-left: 6px;
	vertical-align: center;
}

.k4-tab
{
	height: 100%;
	padding: 0px 14px;
	transition-property: background-color;
	transition-duration: fast;
}

.k4-tab-inner
{
	flow-children: right;
	vertical-align: center;
}

.k4-tab-icon
{
	width: 16px;
	height: 16px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-tab-label
{
	margin-left: 8px;
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
}

.k4-tab-badge
{
	margin-left: 6px;
	vertical-align: center;
	min-width: 18px;
	height: 18px;
	padding: 0px 5px;
	border-radius: 2px;
	background-color: s4;
	font-size: 13px;
	text-align: center;
	color: tx;
}

/* The open tab: a 3px white underline (one of the three places white is allowed) */
.k4-tab-line
{
	width: 100%;
	height: 3px;
	vertical-align: bottom;
	border-radius: 2px;
	background-color: acc;
	visibility: collapse;
}

.k4-tab:hover
{
	background-color: s2;
	sound: "UIPanorama.sidemenu_rollover";
}

.k4-tab:active
{
	sound: "UIPanorama.sidemenu_select";
}

.k4-tab:hover .k4-tab-label, .k4-tab.k4-active .k4-tab-label
{
	color: tx;
}

.k4-tab:hover .k4-tab-icon, .k4-tab.k4-active .k4-tab-icon
{
	wash-color: tx;
}

.k4-tab.k4-active .k4-tab-line
{
	visibility: visible;
}

.k4-event
{
	height: 28px;
	padding: 0px 10px;
	margin-right: 12px;
	vertical-align: center;
	flow-children: right;
	border-radius: 2px;
	background-color: #C8A45C26;
}

.k4-event-icon
{
	width: 14px;
	height: 14px;
	vertical-align: center;
	wash-color: gold;
}

.k4-event-text
{
	margin-left: 6px;
	vertical-align: center;
	font-size: 13px;
	font-weight: bold;
	color: gold;
	white-space: nowrap;
}

/* The gear and the close button */
.k4-square
{
	width: 36px;
	height: 36px;
	margin-left: 8px;
	vertical-align: center;
	border-radius: 4px;
	border: 1px solid line;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-square-icon
{
	width: 18px;
	height: 18px;
	horizontal-align: center;
	vertical-align: center;
	wash-color: tx2;
}

.k4-square:hover
{
	background-color: s2;
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-square:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-square.k4-active
{
	background-color: s3;
	border: 1px solid line-sel;
}

.k4-square:hover .k4-square-icon, .k4-square.k4-active .k4-square-icon
{
	wash-color: tx;
}

/* Crumbs: Back, where you are, the season clock */
.k4-crumbs
{
	width: 100%;
	height: 40px;
	flow-children: right;
	padding: 0px 24px;
	border-bottom: 1px solid line;
}

.k4-back
{
	height: 28px;
	padding: 0px 10px 0px 6px;
	margin-right: 12px;
	vertical-align: center;
	flow-children: right;
	border-radius: 4px;
	border: 1px solid line;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-back:hover
{
	background-color: s2;
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-back:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-back-icon
{
	width: 14px;
	height: 14px;
	vertical-align: center;
	transform: rotateZ( 90deg );
	wash-color: tx2;
}

.k4-back-label
{
	margin-left: 4px;
	vertical-align: center;
	font-size: 13px;
	font-weight: bold;
	color: tx;
	white-space: nowrap;
}

.k4-crumb
{
	vertical-align: center;
	font-size: 14px;
	color: tx2;
	white-space: nowrap;
	text-overflow: ellipsis;
	max-width: 640px;
}

.k4-season
{
	flow-children: right;
	vertical-align: center;
}

.k4-season-title
{
	vertical-align: center;
	font-size: 13px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
}

.k4-season-left
{
	margin-left: 8px;
	vertical-align: center;
	font-size: 13px;
	color: tx;
	white-space: nowrap;
}

/* The duel bar: a challenge for this player, under the crumbs on every screen */
.k4-duel-bar
{
	width: 100%;
	padding: 12px 24px 0px 24px;
}

.k4-duel-bar-card
{
	width: 100%;
	height: 56px;
	flow-children: right;
	padding: 0px 12px 0px 14px;
	border: 1px solid line-hi;
}

.k4-duel-bar-icon
{
	width: 26px;
	height: 26px;
	vertical-align: center;
	wash-color: tx;
}

.k4-duel-bar-title
{
	margin-left: 14px;
	vertical-align: center;
	font-size: 16px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 240px;
}

.k4-duel-bar-msg
{
	margin-left: 14px;
	vertical-align: center;
	font-size: 15px;
	color: tx3;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 420px;
}

.k4-bar.k4-duel-bar-track
{
	width: 140px;
	vertical-align: center;
}

.k4-duel-bar-left
{
	margin: 0px 16px 0px 12px;
	vertical-align: center;
	font-size: 15px;
	white-space: nowrap;
}

/* The screens */
.k4-body
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	padding: 20px 24px;
}

.k4-view
{
	width: 100%;
	height: 100%;
	visibility: collapse;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.1s;
}

.k4-view.k4-show
{
	visibility: visible;
	opacity: 1;
}

/* Footer: status line, out-of-date bar, key hint, copyright */
.k4-foot
{
	width: 100%;
	height: 32px;
	flow-children: right;
	padding: 0px 24px;
	border-top: 1px solid line;
}

.k4-status
{
	flow-children: right;
	vertical-align: center;
	margin-right: 16px;
}

.k4-status-icon
{
	width: 14px;
	height: 14px;
	vertical-align: center;
	margin-right: 6px;
	visibility: collapse;
}

.k4-status-text
{
	vertical-align: center;
	font-size: 13px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 560px;
}

.k4-status.k4-ok .k4-status-ok, .k4-status.k4-bad .k4-status-bad
{
	visibility: visible;
}

.k4-status.k4-ok .k4-status-text { color: ok; }
.k4-status.k4-ok .k4-status-icon { wash-color: ok; }
.k4-status.k4-bad .k4-status-text { color: bad; }
.k4-status.k4-bad .k4-status-icon { wash-color: bad; }

/* Visible by default: only the stylesheet of the server's contract version hides it (generated below). */
.k4-outdated
{
	height: 22px;
	padding: 0px 8px;
	vertical-align: center;
	flow-children: right;
	border-radius: 2px;
	background-color: #C4675C26;
}

.k4-outdated-icon
{
	width: 14px;
	height: 14px;
	vertical-align: center;
	wash-color: bad;
}

.k4-outdated-text
{
	margin-left: 6px;
	vertical-align: center;
	font-size: 13px;
	font-weight: bold;
	color: bad;
	white-space: nowrap;
}

.k4-keys
{
	vertical-align: center;
}

.k4-keys-text
{
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

.k4-copyright
{
	margin-left: 16px;
	vertical-align: center;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

/* ---------------------------------------------------------------- shared parts */

.k4-card
{
	border-radius: 4px;
	background-color: s1;
	border: 1px solid line;
	transition-property: background-color, border;
	transition-duration: fast;
}

/* A card that does something when clicked */
.k4-click:hover
{
	background-color: s2;
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-click:active
{
	sound: "UIPanorama.generic_button_press";
}

/* A choice in a group (duel player, duel type): lighter grey and a grey outline, never white */
.k4-pick.k4-active, .k4-pick.k4-active:hover
{
	background-color: s3;
	border: 1px solid line-sel;
}

.k4-pick:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-head
{
	width: 100%;
	height: 22px;
}

/* A card's caption is a small picture; its words stay with the server but are not shown. */
.k4-cap-icon
{
	width: 16px;
	height: 16px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-cap-text
{
	visibility: collapse;
}

.k4-toolbar
{
	width: 100%;
	height: 40px;
	flow-children: right;
}

.k4-title
{
	vertical-align: center;
	font-size: 22px;
	color: tx;
	white-space: nowrap;
}

.k4-title-fact
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 14px;
	color: tx2;
	white-space: nowrap;
}

.k4-sub
{
	height: 24px;
	margin-bottom: 8px;
}

.k4-sub-title
{
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
}

.k4-sub-fact
{
	margin-left: 10px;
	vertical-align: center;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

.k4-rule
{
	width: 100%;
	height: 1px;
	margin: 18px 0px;
	background-color: line;
}

.k4-empty-text
{
	width: 100%;
	font-size: 14px;
	color: tx3;
}

.k4-more
{
	width: 100%;
	padding: 10px 16px;
	font-size: 13px;
	color: tx3;
}

/* Tags: 2px corners, colour on its 15% tint */
.k4-tag
{
	height: 22px;
	padding: 0px 8px;
	vertical-align: center;
	flow-children: right;
	border-radius: 2px;
	background-color: s3;
}

.k4-tag-big
{
	height: 28px;
	padding: 0px 10px;
}

.k4-tag-icon
{
	width: 12px;
	height: 12px;
	margin-right: 5px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-tag-label
{
	vertical-align: center;
	font-size: 12px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
}

.k4-tag-big .k4-tag-label
{
	font-size: 13px;
}

/* Buttons: 36px, 4px corners. Secondary = outline, main = white (one per card), ghost = quiet outline. */
.k4-btn
{
	height: 36px;
	padding: 0px 16px;
	vertical-align: center;
	flow-children: right;
	border-radius: 4px;
	border: 1px solid line-hi;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-btn-label
{
	vertical-align: center;
	font-size: 14px;
	font-weight: bold;
	color: tx;
	white-space: nowrap;
}

.k4-btn-icon
{
	width: 14px;
	height: 14px;
	margin-right: 8px;
	vertical-align: center;
	wash-color: tx;
}

.k4-btn:hover
{
	background-color: s2;
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-btn:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-btn-main, .k4-btn-main:hover
{
	background-color: acc;
	border: 1px solid acc;
}

.k4-btn-main .k4-btn-label { color: ink; }
.k4-btn-main .k4-btn-icon { wash-color: ink; }

.k4-btn-ghost
{
	border: 1px solid line;
}

.k4-btn-ghost .k4-btn-label { color: tx2; }
.k4-btn-ghost .k4-btn-icon { wash-color: tx2; }
.k4-btn-ghost:hover .k4-btn-label { color: tx; }

.k4-btn-sm
{
	height: 28px;
	padding: 0px 12px;
}

.k4-btn-sm .k4-btn-label
{
	font-size: 13px;
}

.k4-btn-big
{
	height: 40px;
	padding: 0px 26px;
}

.k4-btn-big .k4-btn-label
{
	font-size: 15px;
}

.k4-gap
{
	margin-left: 8px;
}

/* A button that is only its picture (accept, decline, swap, challenge): the caption stays for the server, hidden. */
.k4-btn-icon-only
{
	width: 36px;
	padding: 0px;
}

.k4-btn-sm.k4-btn-icon-only
{
	width: 30px;
	height: 30px;
}

.k4-btn-icon-only .k4-btn-label
{
	visibility: collapse;
}

.k4-btn-icon-only .k4-btn-icon
{
	margin-right: 0px;
	horizontal-align: center;
}

/* Chips: a choice on its own (filters, the Players / Ranks switch, stakes); the picked one is lighter with a grey outline */
.k4-chip
{
	height: 32px;
	padding: 0px 12px;
	vertical-align: center;
	border-radius: 4px;
	background-color: s1;
	border: 1px solid line-hi;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-chip-label
{
	vertical-align: center;
	font-size: 14px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
}

.k4-chip:hover
{
	background-color: s2;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-chip:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-chip.k4-active, .k4-chip.k4-active:hover
{
	background-color: s3;
	border: 1px solid line-sel;
}

.k4-chip:hover .k4-chip-label, .k4-chip.k4-active .k4-chip-label
{
	color: tx;
}

/* Switch: one box, items split by 1px lines; the picked item is lighter grey with a grey outline */
.k4-seg
{
	height: 36px;
	vertical-align: center;
	flow-children: right;
	border-radius: 4px;
	border: 1px solid line;
	background-color: line;
}

.k4-seg-item
{
	height: 100%;
	padding: 0px 14px;
	background-color: s1;
	border: 1px solid #00000000;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-seg-label
{
	vertical-align: center;
	font-size: 14px;
	font-weight: bold;
	color: tx2;
	white-space: nowrap;
}

.k4-seg-item:hover
{
	background-color: s2;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-seg-item:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-seg-item.k4-active, .k4-seg-item.k4-active:hover
{
	background-color: s3;
	border: 1px solid line-sel;
}

.k4-seg-item:hover .k4-seg-label, .k4-seg-item.k4-active .k4-seg-label
{
	color: tx;
}

/* Arrows (season switch) */
.k4-arrow
{
	width: 32px;
	height: 32px;
	vertical-align: center;
	border-radius: 4px;
	border: 1px solid line;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-arrow-icon
{
	width: 14px;
	height: 14px;
	horizontal-align: center;
	vertical-align: center;
	wash-color: tx2;
}

.k4-arrow-left .k4-arrow-icon { transform: rotateZ( 90deg ); }
.k4-arrow-right .k4-arrow-icon { transform: rotateZ( -90deg ); }

.k4-arrow:hover
{
	background-color: s2;
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-arrow:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-arrow.k4-disabled
{
	opacity: 0.3;
}

/* Bars: 6px (4px thin), track s4, 2px corners. Running = near white (text tone, not the accent). */
.k4-bar
{
	width: 100%;
	height: 6px;
	border-radius: 2px;
	background-color: s4;
}

.k4-bar-thin
{
	height: 4px;
}

.k4-bar-fill
{
	height: 100%;
	border-radius: 2px;
	background-color: tx;
	transition-property: width;
	transition-duration: 0.4s;
	transition-timing-function: ease-out;
}

.k4-bar-clock .k4-bar-fill
{
	background-color: tx3;
}

.k4-done .k4-bar-fill
{
	background-color: ok;
}

.k4-bar-season
{
	width: 120px;
	margin-left: 12px;
	vertical-align: center;
}

/* Rank emblems: the game's 2.46 : 1 */
.k4-em-hero { width: 320px; height: 130px; vertical-align: center; }
.k4-em-pf { width: 216px; height: 88px; horizontal-align: center; margin-top: 14px; }
.k4-em-dp { width: 70px; height: 28px; horizontal-align: center; }
.k4-em-ss { width: 54px; height: 22px; vertical-align: center; }
.k4-em-l { width: 128px; height: 52px; vertical-align: center; }
.k4-em-rk { width: 96px; height: 39px; vertical-align: center; margin-right: 16px; }
.k4-em-m { width: 72px; height: 29px; vertical-align: center; }
.k4-em-s { width: 52px; height: 21px; vertical-align: center; }

/* Medal: tier ring, dark disc, white icon */
.k4-ring
{
	border-radius: 50%;
	vertical-align: center;
}

.k4-ring-32 { width: 32px; height: 32px; padding: 3px; }
.k4-ring-64 { width: 64px; height: 64px; padding: 5px; }
.k4-ring-toast { width: 56px; height: 56px; padding: 4px; horizontal-align: center; }

.k4-disc
{
	width: 100%;
	height: 100%;
	border-radius: 50%;
	background-color: gradient( radial, 50% 35%, 0% 0%, 70% 70%, from( s3 ), to( s0 ) );
}

.k4-medal-icon
{
	width: 48%;
	height: 48%;
	horizontal-align: center;
	vertical-align: center;
}

.t-locked
{
	background-color: s4;
}

.t-locked .k4-medal-icon
{
	wash-color: tx3;
	opacity: 0.6;
}

/* Hover explanations: a card with k4-has-tip lays its k4-tip over itself while hovered */
.k4-has-tip
{
	flow-children: none;
}

.k4-tip
{
	width: 100%;
	height: 100%;
	border-radius: 4px;
	background-color: #0B0B0CF2;
	border: 1px solid line-hi;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.12s;
}

.k4-has-tip:hover .k4-tip
{
	opacity: 1;
}

.k4-tip-body
{
	width: 90%;
	horizontal-align: center;
	vertical-align: center;
	flow-children: down;
}

.k4-tip-line
{
	width: 100%;
	text-align: center;
	font-size: 13px;
	color: tx;
}

.k4-tip-text
{
	width: 90%;
	horizontal-align: center;
	vertical-align: center;
	text-align: center;
	font-size: 13px;
	color: tx;
}

/* Scroll boxes: the player scrolls with the mouse wheel on their own PC; the bar is in the grey ramp */
.k4-scroll
{
	overflow: squish scroll;
}

.k4-scroll VerticalScrollBar
{
	width: 6px;
	height: 100%;
	margin: 0px 1px 0px 0px;
	horizontal-align: right;
	vertical-align: top;
	layout-position: fixed;
	opacity: 1;
	background-color: #00000000;
	-s2-mix-blend-mode: normal;
}

.k4-scroll VerticalScrollBar .ScrollThumb
{
	width: 6px;
	min-height: 32px;
	border-radius: 2px;
	background-color: s4;
	transition-property: background-color;
	transition-duration: 0.1s;
}

.k4-scroll VerticalScrollBar:hover .ScrollThumb, .k4-scroll VerticalScrollBar:active .ScrollThumb
{
	background-color: tx3;
}

/* Gold, silver, bronze place marks (leaderboard rows 1-3, Home's top 3) and the white "you" mark */
.k4-p1 .k4-lb-pos { color: gold; }
.k4-p2 .k4-lb-pos { color: silver; }
.k4-p3 .k4-lb-pos { color: bronze; }
.k4-lb-row.k4-p1 { border-left: 3px solid gold; }
.k4-lb-row.k4-p2 { border-left: 3px solid silver; }
.k4-lb-row.k4-p3 { border-left: 3px solid bronze; }

/* ---------------------------------------------------------------- Home */

/* The simple look (owner 2026-11-21): the rank as a big hero; today's missions as tiles, the top 3 podium and
   "next to do" as medals; the player's big numbers. Pictures and numbers in front, the sentences on hover. */
.k4-home
{
	flow-children: down;
}

.k4-h-rank
{
	width: 100%;
	height: 150px;
	padding: 0px 8px;
	flow-children: right;
}

.k4-h-rank-text
{
	width: fill-parent-flow( 1.0 );
	margin-left: 28px;
	vertical-align: center;
	flow-children: down;
}

.k4-h-rank-name
{
	width: 100%;
	font-size: 54px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-h-points
{
	font-size: 22px;
	white-space: nowrap;
}

.k4-h-place
{
	margin-left: 12px;
	vertical-align: bottom;
	font-size: 18px;
	color: gold;
	white-space: nowrap;
}

.k4-bar.k4-h-bar-track
{
	width: 520px;
	margin-top: 14px;
}

.k4-h-next-row
{
	visibility: collapse;
}

/* Labels the server still fills but the simple look leaves out (their boxes are collapsed) */
.k4-h-nextrank, .k4-h-togo, .k4-streak-count, .k4-streak-hint, .k4-dp-points, .k4-dp-state
{
	white-space: nowrap;
}

.k4-h-mid
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 16px;
	flow-children: right;
}

.k4-h-today
{
	width: 440px;
	height: 100%;
	padding: 14px 16px 16px 16px;
	flow-children: down;
}

.k4-h-tiles
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 10px;
	flow-children: right;
}

/* A mission tile: picture, progress, bar; green when done. Its text and reward show on hover. */
.k4-h-m
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	border-radius: 4px;
	background-color: s2;
	border: 1px solid line;
	transition-property: background-color, border;
	transition-duration: fast;
}

.k4-h-m:hover
{
	border: 1px solid line-hi;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-h-m:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-h-m-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-h-m-icon
{
	width: 40px;
	height: 40px;
	horizontal-align: center;
	wash-color: tx;
}

.k4-h-m-progress
{
	max-width: 100%;
	margin-top: 14px;
	horizontal-align: center;
	font-size: 18px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-bar.k4-h-m-bar
{
	width: 60%;
	margin-top: 10px;
	horizontal-align: center;
}

.k4-h-m.k4-done, .k4-m.k4-done, .k4-mw.k4-done
{
	background-color: #6FA8861A;
	border: 1px solid #6FA88659;
}

.k4-done .k4-h-m-progress, .k4-done .k4-m-progress, .k4-done .k4-mw-progress { color: ok; }

/* Home's streak days: the parts stay for the server; the week is shown on Missions. */
.k4-h-streak
{
	visibility: collapse;
}

.k4-h-sd
{
	flow-children: right;
}

.k4-h-sd-day
{
	font-size: 14px;
}

.k4-sd-check
{
	width: 16px;
	height: 16px;
	horizontal-align: center;
	wash-color: ok;
	visibility: collapse;
}

.k4-on .k4-sd-check
{
	visibility: visible;
}

.k4-h-top
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	margin-left: 16px;
	padding: 14px 16px;
	flow-children: down;
}

.k4-h-podium
{
	width: fit-children;
	height: fill-parent-flow( 1.0 );
	horizontal-align: center;
	flow-children: right;
}

.k4-h-p
{
	width: 104px;
	height: fit-children;
	margin-left: 10px;
	vertical-align: bottom;
	flow-children: down;
}

.k4-h-p.k4-h-p-2
{
	margin-left: 0px;
}

.k4-h-p .k4-em-m
{
	horizontal-align: center;
}

.k4-h-p-name
{
	width: 100%;
	margin-top: 4px;
	margin-bottom: 6px;
	horizontal-align: center;
	text-align: center;
	font-size: 14px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-h-p-points
{
	visibility: collapse;
}

.k4-h-step
{
	width: 100%;
	border-radius: 2px;
}

.k4-h-p-1 .k4-h-step { height: 86px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #C8A45C ), to( #6E5A2E ) ); }
.k4-h-p-2 .k4-h-step { height: 62px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #B4B4B4 ), to( #5A5A5A ) ); }
.k4-h-p-3 .k4-h-step { height: 46px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #A97F5E ), to( #523B2A ) ); }

.k4-h-p-pos
{
	horizontal-align: center;
	margin-top: 6px;
	font-size: 22px;
	color: s0;
	white-space: nowrap;
}

.k4-h-me
{
	width: 100%;
	height: 34px;
	margin-top: 10px;
	padding: 0px 12px 0px 10px;
	flow-children: right;
	border-radius: 4px;
	background-color: s3;
	border-left: 3px solid acc;
}

.k4-h-me-pos, .k4-h-me-points
{
	vertical-align: center;
	font-size: 15px;
	white-space: nowrap;
}

/* "Next to do": medal rings with a title; the ring's colour says what kind of thing it is (generated below). */
.k4-h-next
{
	width: 332px;
	height: 100%;
	margin-left: 16px;
	padding: 14px 16px;
	flow-children: down;
}

.k4-h-medals
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
}

.k4-h-n
{
	width: fill-parent-flow( 1.0 );
	height: 140px;
	vertical-align: center;
	border-radius: 4px;
	border: 1px solid #00000000;
}

.k4-h-n-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-h-n-ring
{
	width: 76px;
	height: 76px;
	horizontal-align: center;
	border-radius: 50%;
	background-color: s2;
	border: 3px solid tx3;
}

.k4-h-n-icon
{
	width: 34px;
	height: 34px;
	horizontal-align: center;
	vertical-align: center;
	wash-color: tx;
}

.k4-h-n-title
{
	width: 100%;
	margin-top: 10px;
	padding: 0px 4px;
	text-align: center;
	font-size: 13px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

/* The big numbers: a picture and a value; the name shows on hover. */
.k4-h-stats
{
	width: 100%;
	height: 78px;
	margin-top: 16px;
	flow-children: right;
}

.k4-h-s
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	border-radius: 4px;
	background-color: s1;
	border: 1px solid line;
}

.k4-h-s-body
{
	height: 100%;
	horizontal-align: center;
	flow-children: right;
}

.k4-h-s-icon
{
	width: 30px;
	height: 30px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-h-s-value
{
	margin-left: 16px;
	vertical-align: center;
	font-size: 32px;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 150px;
}

"""

for kind, color in NEXT_KINDS.items():
    css += f".k4-h-n.k4-n-{kind} .k4-h-n-ring {{ border: 3px solid {color}; }}\n"

css += r"""
/* ---------------------------------------------------------------- Missions */

.k4-missions
{
	flow-children: down;
}

.k4-title-icon
{
	width: 14px;
	height: 14px;
	margin-left: 16px;
	vertical-align: center;
	wash-color: tx3;
}

.k4-title-fact.k4-m-reset
{
	margin-left: 6px;
	font-size: 13px;
	color: tx3;
}

/* Swaps left: a picture and a number */
.k4-tag.k4-m-swaps
{
	height: 32px;
	border-radius: 4px;
	background-color: s1;
	border: 1px solid line-hi;
}

.k4-m-swaps .k4-tag-icon
{
	width: 14px;
	height: 14px;
	wash-color: tx;
}

.k4-m-swaps .k4-tag-label
{
	font-size: 15px;
	color: tx;
}

.k4-m-cards
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 14px;
	flow-children: right;
}

/* A mission card: the difficulty strip on top (easy green, medium gold, hard red), the reward top right, a big
   picture, the progress and its bar; green when done. Difficulty and text show on hover. */
.k4-m
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-m-strip
{
	width: 100%;
	height: 3px;
	border-radius: 2px;
	background-color: tx3;
}

.k4-easy .k4-m-strip { background-color: ok; }
.k4-medium .k4-m-strip { background-color: gold; }
.k4-hard .k4-m-strip { background-color: bad; }

.k4-m-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-m-icon
{
	width: 84px;
	height: 84px;
	horizontal-align: center;
	wash-color: tx;
}

.k4-m-progress
{
	max-width: 90%;
	margin-top: 16px;
	horizontal-align: center;
	font-size: 30px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-bar.k4-m-bar
{
	width: 72%;
	margin-top: 12px;
	horizontal-align: center;
}

.k4-m-top
{
	width: 100%;
	height: 44px;
	flow-children: right;
	padding: 10px 14px 0px 10px;
}

.k4-m-reward
{
	vertical-align: center;
	font-size: 18px;
	color: gold;
	white-space: nowrap;
}

/* The swap question, over the bottom of the card */
.k4-m-ask
{
	width: 100%;
	vertical-align: bottom;
	flow-children: down;
	padding: 14px;
	border-radius: 4px;
	background-color: s1;
	border-top: 1px solid line-hi;
}

.k4-m-ask-text
{
	width: 100%;
	text-align: center;
	font-size: 14px;
	font-weight: bold;
}

.k4-row.k4-m-ask-row
{
	width: fit-children;
	margin-top: 10px;
	horizontal-align: center;
}

.k4-m-bottom
{
	width: 100%;
	height: 88px;
	margin-top: 16px;
	flow-children: right;
}

/* The weekly mission in one line: picture, "Weekly" and the time left, bar, progress and reward */
.k4-mw
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-mw-body
{
	width: 100%;
	height: 100%;
	flow-children: right;
	padding: 0px 20px;
}

.k4-mw-icon
{
	width: 40px;
	height: 40px;
	vertical-align: center;
	wash-color: tx;
}

.k4-mw-text
{
	width: fill-parent-flow( 1.0 );
	margin: 0px 24px;
	vertical-align: center;
	flow-children: down;
}

.k4-mw-title
{
	font-size: 17px;
	white-space: nowrap;
}

.k4-mw-left
{
	margin-left: 8px;
	vertical-align: bottom;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

.k4-bar.k4-mw-track
{
	margin-top: 10px;
}

.k4-mw-side
{
	width: 120px;
	vertical-align: center;
	flow-children: down;
}

.k4-mw-progress
{
	horizontal-align: right;
	font-size: 26px;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 120px;
}

.k4-mw-reward
{
	horizontal-align: right;
	font-size: 16px;
	color: gold;
	white-space: nowrap;
}

/* The streak week: the day's number and its bonus, never a sentence, so it fits in every language. Done days are
   green with a check; today has a dashed edge ("where you are", not a pick). */
.k4-streak
{
	width: fit-children;
	height: 100%;
	margin-left: 16px;
	padding: 0px 16px;
	flow-children: right;
}

.k4-streak-head
{
	visibility: collapse;
}

.k4-sd
{
	width: 58px;
	height: 58px;
	vertical-align: center;
	border-radius: 4px;
	background-color: s1;
	border: 1px solid line;
}

.k4-sd-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-sd-day
{
	horizontal-align: center;
	font-size: 17px;
}

.k4-sd-bonus
{
	max-width: 54px;
	margin-top: 2px;
	horizontal-align: center;
	font-size: 11px;
	color: tx3;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-sd.k4-on
{
	background-color: #6FA8861A;
	border: 1px solid #6FA88659;
}

.k4-sd.k4-on .k4-sd-day
{
	visibility: collapse;
}

.k4-sd.k4-on .k4-sd-bonus
{
	color: ok;
}

.k4-sd.k4-today
{
	border: 1px dashed tx2;
}

/* ---------------------------------------------------------------- Achievements */

.k4-achievements
{
	flow-children: down;
}

/* The three medals on the player's profile, small, next to the count. A click takes one off. */
.k4-a-showcase
{
	height: 100%;
	margin-right: 20px;
	flow-children: right;
}

.k4-a-sc-cap
{
	margin-right: 6px;
	vertical-align: center;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

.k4-a-sc
{
	width: 40px;
	height: 40px;
	margin-left: 4px;
	vertical-align: center;
	border-radius: 4px;
	border: 1px solid #00000000;
}

.k4-a-sc .k4-ring
{
	horizontal-align: center;
}

.k4-a-sc-name, .k4-a-sc-empty
{
	visibility: collapse;
}

.k4-a-sc.k4-empty .k4-ring { opacity: 0.5; }

.k4-a-count
{
	vertical-align: center;
	font-size: 17px;
	white-space: nowrap;
}

.k4-a-list
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 14px;
	flow-children: right-wrap;
}

.k4-a-empty
{
	width: 100%;
	flow-children: down;
	padding: 40px 0px;
}

.k4-a-empty .k4-empty-text
{
	text-align: center;
	margin-bottom: 14px;
}

.k4-a-empty .k4-btn
{
	horizontal-align: center;
}

/* A medal tile: ring, name, bar; unlocked ones get the metal ring and a check. The text shows on hover. */
.k4-a
{
	width: 186px;
	height: 132px;
	margin: 0px 12px 12px 0px;
}

.k4-a-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-a-body .k4-ring
{
	horizontal-align: center;
}

.k4-a-name
{
	width: 100%;
	margin-top: 10px;
	padding: 0px 8px;
	text-align: center;
	font-size: 14px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-bar.k4-a-bar
{
	width: 64%;
	margin-top: 8px;
	horizontal-align: center;
}

.k4-a-check
{
	width: 14px;
	height: 14px;
	margin-top: 6px;
	horizontal-align: center;
	wash-color: ok;
	visibility: collapse;
}

.k4-a-goal, .k4-a-desc
{
	visibility: collapse;
}

.k4-a.k4-done
{
	border: 1px solid line-hi;
}

.k4-a.k4-done .k4-a-bar { visibility: collapse; }
.k4-a.k4-done .k4-a-check { visibility: visible; }

.k4-a-pin
{
	width: 14px;
	height: 14px;
	horizontal-align: right;
	margin: 10px 10px 0px 0px;
	wash-color: tx;
	visibility: collapse;
}

.k4-a.k4-pinned, .k4-a.k4-pinned:hover
{
	border: 1px solid line-sel;
}

.k4-a.k4-pinned .k4-a-pin
{
	visibility: visible;
}

/* ---------------------------------------------------------------- Leaderboard */

.k4-leaderboard
{
	flow-children: down;
}

.k4-lb-switch
{
	margin-left: 20px;
	vertical-align: center;
	flow-children: right;
}

/* The rank list has no seasons and no player count: the season switch and the count belong to the players list. */
.k4-lb-ranklist .k4-lb-switch, .k4-lb-ranklist .k4-lb-count
{
	visibility: collapse;
}

.k4-lb-season
{
	margin: 0px 12px;
	vertical-align: center;
	font-size: 20px;
	white-space: nowrap;
}

.k4-lb-status
{
	margin-right: 12px;
}

.k4-lb-players, .k4-lb-ranks
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 14px;
	flow-children: down;
}

.k4-lb-list, .k4-rk-list
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: down;
}

/* A row: place, emblem, name, points, and a small challenge button. The rest of the row opens the profile. */
.k4-lb-row
{
	width: 100%;
	height: 42px;
	flow-children: right;
	border-left: 3px solid #00000000;
	border-bottom: 1px solid line;
	transition-property: background-color;
	transition-duration: fast;
}

.k4-lb-row:hover
{
	background-color: s2;
}

.k4-lb-open
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: right;
	padding-left: 16px;
}

.k4-lb-open:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-lb-pos
{
	width: 40px;
	vertical-align: center;
	font-size: 17px;
	color: tx3;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-lb-name
{
	width: fill-parent-flow( 1.0 );
	margin-left: 16px;
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-lb-rank, .k4-lb-caption
{
	visibility: collapse;
}

.k4-lb-points
{
	width: 120px;
	vertical-align: center;
	text-align: right;
	font-size: 17px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-lb-btns
{
	width: 62px;
	height: 100%;
}

.k4-lb-duel
{
	horizontal-align: center;
	vertical-align: center;
}

/* The player's own row: in the list, and pinned under it */
.k4-lb-row.k4-me, .k4-lb-row.k4-me:hover
{
	background-color: s3;
	border-left: 3px solid acc;
}

.k4-lb-row.k4-me .k4-lb-pos
{
	color: tx;
}

.k4-lb-me
{
	height: 44px;
	margin-top: 16px;
	border-radius: 4px;
	border-bottom: 0px solid #00000000;
}

.k4-lb-you
{
	width: 100%;
	height: 48px;
	margin-bottom: 8px;
	flow-children: right;
	padding: 0px 16px;
	border-radius: 4px;
	background-color: s3;
	border-left: 3px solid acc;
}

.k4-lb-you-text
{
	width: 100%;
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-rk
{
	width: 100%;
	height: 56px;
	border-left: 3px solid #00000000;
	border-bottom: 1px solid line;
	transition-property: background-color;
	transition-duration: fast;
}

.k4-rk:hover
{
	background-color: s2;
}

.k4-rk.k4-current, .k4-rk.k4-current:hover
{
	background-color: s3;
	border-left: 3px solid acc;
}

.k4-rk-body
{
	width: 100%;
	height: 100%;
	flow-children: right;
	padding: 0px 16px;
}

.k4-rk-name
{
	width: 300px;
	vertical-align: center;
	font-size: 18px;
	white-space: nowrap;
	text-overflow: shrink;
}

/* The white "you" mark. wash-color tints the whole panel, its background included, so a white badge with a dark
   icon on it came out as a black box: the icon alone is tinted white instead. */
.k4-rk-you
{
	width: 18px;
	height: 18px;
	margin-left: 10px;
	vertical-align: center;
	wash-color: acc;
}

.k4-rk-gift
{
	width: 16px;
	height: 16px;
	margin-left: 12px;
	vertical-align: center;
	wash-color: gold;
}

.k4-rk-reward
{
	width: fill-parent-flow( 1.0 );
	margin-left: 8px;
	vertical-align: center;
	font-size: 14px;
	color: tx2;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-rk-points
{
	width: 140px;
	vertical-align: center;
	text-align: right;
	font-size: 16px;
	color: tx2;
	white-space: nowrap;
	text-overflow: shrink;
}

/* ---------------------------------------------------------------- Duels */

.k4-duels
{
	flow-children: right;
}

.k4-d-build
{
	width: 846px;
	height: 100%;
	flow-children: down;
}

/* The step captions stay with the server; the choices speak for themselves. */
.k4-duels .k4-sub
{
	visibility: collapse;
}

/* Three rows of players show; more scroll. The rules, stakes and challenge sit right under them. */
.k4-dp-list
{
	width: 100%;
	height: 246px;
	flow-children: right-wrap;
	margin-bottom: 4px;
}

/* A player: emblem and name. One who cannot be challenged now is dimmed (k4-disabled). */
.k4-dp
{
	width: 158px;
	height: 72px;
	margin: 0px 10px 10px 0px;
	flow-children: down;
	padding-top: 11px;
}

.k4-dp-name
{
	width: 100%;
	margin-top: 6px;
	padding: 0px 8px;
	text-align: center;
	font-size: 13px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-dp-extra
{
	visibility: collapse;
}

/* A rule: its picture; the name and what counts show on hover. */
.k4-d-types
{
	width: 100%;
	height: 70px;
	flow-children: right;
	margin-bottom: 14px;
}

.k4-dt
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-dt-body
{
	width: 100%;
	height: 100%;
}

.k4-dt-icon
{
	width: 64px;
	height: 30px;
	horizontal-align: center;
	vertical-align: center;
	wash-color: tx2;
}

.k4-dt-name
{
	visibility: collapse;
}

.k4-dt.k4-active .k4-dt-icon { wash-color: tx; }

.k4-d-stakes
{
	width: 100%;
	flow-children: right;
}

/* The challenge in one line: emblem, name, the rule's picture, the stake; why it cannot be sent yet; Challenge. */
.k4-d-sum
{
	width: 100%;
	height: 64px;
	margin-top: 14px;
	flow-children: right;
	padding: 0px 12px 0px 16px;
}

.k4-d-pick
{
	height: 100%;
	flow-children: right;
}

.k4-d-pick-name
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 16px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 220px;
}

.k4-d-pick-type
{
	width: 22px;
	height: 22px;
	margin-left: 14px;
	vertical-align: center;
	wash-color: tx;
}

.k4-d-pick-stake
{
	margin-left: 10px;
	vertical-align: center;
	font-size: 18px;
	color: gold;
	white-space: nowrap;
}

.k4-d-status
{
	margin-left: 14px;
	vertical-align: center;
	font-size: 13px;
	color: tx2;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 420px;
}

.k4-d-side
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	margin-left: 16px;
	flow-children: down;
}

.k4-d-wait, .k4-d-mine
{
	width: 100%;
	margin-bottom: 12px;
	padding: 16px;
	flow-children: down;
}

.k4-d-wait-text
{
	width: 100%;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-wait .k4-bar
{
	margin: 12px 0px;
}

.k4-d-mine-icon
{
	width: 28px;
	height: 28px;
	vertical-align: center;
}

.k4-d-mine-title
{
	margin-left: 10px;
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 180px;
}

.k4-d-mine-score
{
	vertical-align: center;
	font-size: 30px;
	white-space: nowrap;
}

.k4-d-mine-text
{
	width: 100%;
	margin: 6px 0px 10px 0px;
	font-size: 13px;
	color: tx2;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-live
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	padding: 14px 16px;
	flow-children: down;
}

/* A running duel: the rule's picture, who, the score; the rule, stake and time left on hover. */
.k4-dl
{
	width: 100%;
	height: 52px;
	border-bottom: 1px solid line;
}

.k4-dl-body
{
	width: 100%;
	height: 100%;
	flow-children: right;
}

.k4-dl-icon
{
	width: 24px;
	height: 24px;
	vertical-align: center;
	wash-color: tx;
}

.k4-dl-names
{
	width: fill-parent-flow( 1.0 );
	margin-left: 12px;
	vertical-align: center;
	font-size: 14px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-dl-score
{
	width: 64px;
	vertical-align: center;
	text-align: right;
	font-size: 20px;
	white-space: nowrap;
}

/* ---------------------------------------------------------------- Profile */

.k4-profile
{
	flow-children: right;
}

/* Left: name and place, the big emblem, rank, points, bar, and the showcase medals with their names */
.k4-pf-left
{
	width: 340px;
	height: 100%;
	padding: 20px;
	flow-children: down;
}

.k4-pf-head
{
	horizontal-align: center;
	flow-children: right;
}

.k4-pf-name
{
	max-width: 220px;
	font-size: 20px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-place
{
	margin-left: 8px;
	font-size: 20px;
	color: gold;
	white-space: nowrap;
}

.k4-pf-badge
{
	height: 40px;
	margin-top: 16px;
	padding: 0px 14px;
	horizontal-align: center;
	border-radius: 2px;
	background-color: s3;
}

.k4-pf-badge-text
{
	vertical-align: center;
	font-size: 22px;
	white-space: nowrap;
}

.k4-pf-rank
{
	width: 100%;
	margin-top: 12px;
	text-align: center;
	font-size: 30px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-points
{
	margin-top: 6px;
	horizontal-align: center;
	font-size: 16px;
	white-space: nowrap;
}

.k4-bar.k4-pf-bar-track
{
	margin-top: 12px;
}

.k4-pf-next
{
	visibility: collapse;
}

.k4-pf-challenge
{
	margin-top: 16px;
	horizontal-align: center;
}

.k4-pf-showcase
{
	horizontal-align: center;
	flow-children: right;
}

.k4-pf-sc
{
	width: 92px;
	height: 104px;
	border-radius: 4px;
	border: 1px solid #00000000;
}

.k4-pf-sc-body
{
	width: 100%;
	vertical-align: center;
	flow-children: down;
}

.k4-pf-sc-body .k4-ring
{
	horizontal-align: center;
}

.k4-pf-sc-name
{
	width: 100%;
	margin-top: 8px;
	padding: 0px 4px;
	text-align: center;
	font-size: 13px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-sc-desc
{
	visibility: collapse;
}

.k4-pf-sc-hint
{
	width: 100%;
}

.k4-pf-sc-hint .k4-empty-text
{
	text-align: center;
	font-size: 13px;
}

/* Right: eight numbers (picture and value; the name on hover), the weapons and time per side, past seasons */
.k4-pf-right
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	margin-left: 16px;
	flow-children: down;
}

.k4-pf-tiles
{
	width: 100%;
	height: 72px;
	flow-children: right;
}

.k4-pf-st
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-pf-st-body
{
	height: 100%;
	horizontal-align: center;
	flow-children: right;
}

.k4-pf-st-icon
{
	width: 26px;
	height: 26px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-pf-st-label
{
	visibility: collapse;
}

/* One line, shrunk to fit: a long value (big playtime, a long translation) wrapped and was cut off. */
.k4-tile-value
{
	max-width: 150px;
	margin-left: 14px;
	vertical-align: center;
	font-size: 30px;
	color: tx;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-pair
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 16px;
	flow-children: right;
}

.k4-pf-col
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
}

.k4-pf-weapons
{
	width: 100%;
	padding: 8px 18px;
	flow-children: down;
}

.k4-pf-weapons .k4-pf-card-head, .k4-pf-weapons .k4-pf-line-name
{
	visibility: collapse;
}

.k4-pf-pt
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	margin-top: 16px;
	padding: 12px 18px;
	flow-children: down;
}

.k4-pf-card-head
{
	height: 24px;
}

.k4-pf-card-icon
{
	width: 14px;
	height: 14px;
	margin-right: 8px;
	vertical-align: center;
	wash-color: tx2;
}

.k4-pf-card-title
{
	vertical-align: center;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 220px;
}

.k4-pf-card-fact
{
	vertical-align: center;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
}

.k4-pf-line
{
	width: 100%;
	height: 44px;
	flow-children: right;
}

.k4-pf-sw-icon
{
	width: 86px;
	height: 26px;
	vertical-align: center;
	wash-color: tx;
}

.k4-pf-side-icon
{
	width: 22px;
	height: 22px;
	vertical-align: center;
}

.k4-pf-line-name
{
	width: 110px;
	margin-left: 12px;
	vertical-align: center;
	font-size: 14px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-line-bar
{
	width: fill-parent-flow( 1.0 );
	margin: 0px 14px;
	vertical-align: center;
}

.k4-pf-line-value
{
	width: 70px;
	vertical-align: center;
	text-align: right;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-side-t .k4-pf-line-name { color: side-t; }
.k4-side-t .k4-bar-fill { background-color: side-t; }
.k4-side-ct .k4-pf-line-name { color: side-ct; }
.k4-side-ct .k4-bar-fill { background-color: side-ct; }
.k4-side-spec .k4-pf-line-name { color: tx2; }
.k4-side-spec .k4-bar-fill { background-color: tx3; }
.k4-side-spec .k4-pf-side-icon { wash-color: tx2; }

/* Past seasons: emblem, season, place */
.k4-pf-ss-list
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	margin-left: 16px;
	flow-children: down;
	padding: 8px 0px;
}

.k4-pf-ss
{
	width: 100%;
	height: 40px;
	flow-children: right;
	padding: 0px 16px;
	border-bottom: 1px solid line;
}

.k4-pf-ss-season
{
	width: fill-parent-flow( 1.0 );
	margin-left: 14px;
	vertical-align: center;
	font-size: 14px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-ss-pos
{
	width: 90px;
	vertical-align: center;
	text-align: right;
	font-size: 16px;
	white-space: nowrap;
}

.k4-pf-ss-none
{
	width: 100%;
	padding: 12px 16px;
}

/* ---------------------------------------------------------------- Settings */

.k4-settings
{
	flow-children: down;
}

.k4-set-card
{
	width: 760px;
	margin-top: 12px;
	padding: 0px 20px;
	flow-children: down;
}

.k4-set-row
{
	width: 100%;
	height: 64px;
	flow-children: right;
	border-top: 1px solid line;
}

.k4-set-row.k4-first
{
	border-top: 0px solid #00000000;
}

.k4-set-name
{
	width: 300px;
	vertical-align: center;
	font-size: 15px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-set-lock
{
	margin-left: 14px;
	vertical-align: center;
	font-size: 13px;
	color: tx3;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 260px;
}

/* ---------------------------------------------------------------- toasts: under the kill feed, newest on top */

@keyframes 'k4-toast-in'
{
	0%
	{
		opacity: 0;
		transform: translateX( 40px );
	}

	100%
	{
		opacity: 1;
		transform: translateX( 0px );
	}
}

@keyframes 'k4-promo-in'
{
	0%
	{
		opacity: 0;
		transform: scale3d( 0.85, 0.85, 1 );
	}

	100%
	{
		opacity: 1;
		transform: scale3d( 1, 1, 1 );
	}
}

.k4-toasts
{
	width: 420px;
	flow-children: down;
	horizontal-align: right;
	vertical-align: top;
	margin: 240px 24px 0px 0px;
}

/* While the window is open the toasts sit under it: it ends at y 920, so one 80px toast fits above y 1080.
   The server shows one at a time then and queues the rest. */
.k4-toasts.k4-open
{
	horizontal-align: center;
	margin: 932px 0px 0px 0px;
}

.k4-toast
{
	width: 420px;
	height: 80px;
	margin-bottom: 8px;
	border-radius: 4px;
	background-color: s0;
	border: 1px solid line;
	box-shadow: #00000080 0px 8px 24px 0px;
	transition-property: opacity, transform;
	transition-duration: 0.2s;
	transition-timing-function: ease-in;
}

.k4-toast:not(.k4-gone)
{
	animation-name: k4-toast-in;
	animation-duration: 0.2s;
	animation-timing-function: ease-out;
	animation-iteration-count: 1;
}

/* Leaving: the server adds k4-out, waits for this slide-out, then hides the toast. */
.k4-toast.k4-out
{
	opacity: 0;
	transform: translateX( 40px );
}

.k4-toast-0 { border: 1px solid line-hi; }
.k4-toast-1 { opacity: 0.85; }
.k4-toast-2 { opacity: 0.7; }

.k4-toast-strip
{
	width: 3px;
	height: 56px;
	vertical-align: center;
	border-radius: 2px;
	background-color: ok;
}

.k4-toast-row
{
	width: 100%;
	height: 100%;
	flow-children: right;
	padding: 0px 18px 0px 14px;
}

.k4-toast-pic
{
	width: 56px;
	height: 56px;
	vertical-align: center;
}

/* 40px wide fits inside the medal ring; square icons stay 32px tall, a weapon gets the extra width. */
.k4-toast-icon
{
	width: 40px;
	height: 32px;
	horizontal-align: center;
	vertical-align: center;
}

/* No medal ring: the picture gets the whole 56px, so wide ones (weapons, rank emblems) are not drawn tiny. */
.k4-wide .k4-toast-icon
{
	width: 56px;
	height: 40px;
}

.k4-toast-body
{
	width: fill-parent-flow( 1.0 );
	margin-left: 14px;
	vertical-align: center;
	flow-children: down;
}

.k4-toast-kind
{
	width: 100%;
	font-size: 13px;
	font-weight: bold;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-toast-text
{
	width: 100%;
	margin-top: 2px;
	font-size: 16px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-bar-toast
{
	margin-top: 6px;
}

.k4-toast-value
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 22px;
	white-space: nowrap;
}

/* Full width while the toast waits, empty at the end of its length; without k4-run it snaps back at once. */
.k4-toast-timer
{
	width: 100%;
	height: 2px;
	vertical-align: bottom;
	background-color: tx3;
	transition-property: width;
	transition-duration: 0s;
	transition-timing-function: linear;
}

.k4-toast-timer.k4-run
{
	width: 0%;
}

.k4-t-more
{
	horizontal-align: right;
	padding: 4px 12px;
	border-radius: 2px;
	background-color: s0;
	font-size: 13px;
	color: tx2;
}

/* ---------------------------------------------------------------- promotion banner */

.k4-promo
{
	flow-children: down;
	horizontal-align: center;
	vertical-align: top;
	margin-top: 120px;
	padding: 22px 56px 26px 56px;
	border-radius: 6px;
	background-color: s0;
	border: 1px solid line-hi;
	box-shadow: #000000B8 0px 24px 72px 0px;
	transition-property: opacity, transform;
	transition-duration: 0.3s;
	transition-timing-function: ease-in;
}

.k4-promo:not(.k4-gone)
{
	animation-name: k4-promo-in;
	animation-duration: 0.35s;
	animation-timing-function: ease-out;
	animation-iteration-count: 1;
	sound: "UI.ArmsRace.BecomeMatchLeader";
}

.k4-promo.k4-out
{
	opacity: 0;
	transform: scale3d( 0.9, 0.9, 1 );
}

.k4-promo-title
{
	horizontal-align: center;
	font-size: 18px;
	font-weight: bold;
	color: tx2;
}

.k4-promo-icon
{
	width: 256px;
	height: 104px;
	horizontal-align: center;
	margin-top: 10px;
}

.k4-promo-rank
{
	horizontal-align: center;
	margin-top: 8px;
	font-size: 40px;
}

/* ---------------------------------------------------------------- generated */
"""

css += f"\n/* Contract {CONTRACT}: the server sets k4-c{CONTRACT} on k4-panel; only then is the out-of-date bar hidden */\n"
css += f".k4-c{CONTRACT} .k4-outdated {{ visibility: collapse; }}\n"

css += "\n/* Grid gaps */\n"
for name, gap in GAPS_LEFT.items():
    css += f".{name} {{ margin-left: {gap}px; }}\n"
for name, gap in GAPS_TOP.items():
    css += f".{name} {{ margin-top: {gap}px; }}\n"
css += ",\n".join(f".{name}.k4-first" for name in GAPS_LEFT) + "\n{\n\tmargin-left: 0px;\n}\n"
css += ",\n".join(f".{name}.k4-first" for name in GAPS_TOP) + "\n{\n\tmargin-top: 0px;\n}\n"

css += "\n/* Medal tiers */\n"
for tier, (light, middle, dark) in TIERS.items():
    css += (f".t-{tier} {{ background-color: gradient( linear, 0% 0%, 100% 100%, from( {light} ), "
            f"color-stop( 0.45, {middle} ), to( {dark} ) ); }}\n")

css += "\n/* Toast colors and their sounds */\n"
for name, (color, sound) in TOAST_COLORS.items():
    css += f".k4-tc-{name} .k4-toast-strip {{ background-color: {color}; }}\n"
    css += f".k4-tc-{name} .k4-toast-kind {{ color: {color}; }}\n"
    css += f".k4-tc-{name} .k4-bar-toast .k4-bar-fill {{ background-color: {color}; }}\n"
    css += f'.k4-toast.k4-tc-{name}:not(.k4-gone) {{ sound: "{sound}"; }}\n'

css += "\n/* Toast lengths: the timer bar's run-out time */\n"
for name, duration in TOAST_LENGTHS.items():
    css += f".k4-len-{name} .k4-toast-timer.k4-run {{ transition-duration: {duration}; }}\n"

css += "\n/* Game icons */\n"
for name in UI_ICONS:
    css += f'.ic-{name} {{ background-image: url("s2r://panorama/images/icons/ui/{name}.vsvg"); }}\n'
# Valve drew these few in orange or blue; the theme shows them in grey.
css += ".ic-hot_trend, .ic-cold_trend, .ic-stat_assists, .ic-stat_headshots, .ic-stat_mvps { saturation: 0; }\n"
for name in EQUIPMENT_ICONS:
    css += f'.eq-{name} {{ background-image: url("s2r://panorama/images/icons/equipment/{name}.vsvg"); }}\n'
for name in DEATHNOTICE_ICONS:
    css += f'.dn-{name} {{ background-image: url("s2r://panorama/images/hud/deathnotice/{name}.vsvg"); }}\n'

css += "\n/* Team patches (CS2's win panel) */\n"
css += '.tp-t { background-image: url("s2r://panorama/images/hud/winpanel/t_patch_png.vtex"); }\n'
css += '.tp-ct { background-image: url("s2r://panorama/images/hud/winpanel/ct_patch_png.vtex"); }\n'

css += "\n/* CS2 rank icons, numbered like the scoreboard; sharp copies shipped in the addon (gen_k4_ranks.py) */\n"
for number in range(19):
    css += f'.sg-{number} {{ background-image: url("s2r://panorama/images/custom_game/k4/ranks/skillgroup{number}.vsvg"); }}\n'
    css += f'.wg-{number} {{ background-image: url("s2r://panorama/images/custom_game/k4/ranks/wingman{number}.vsvg"); }}\n'

css += "\n/* Progress bar widths */\n"
for step in range(21):
    css += f".k4-bar-fill.w{step} {{ width: {step * 5}%; }}\n"

css += "\n/* Rank colors */\n"
for name, color in COLORS.items():
    css += f".col-{name} {{ color: {color}; }}\n"

# CS2 draws an SVG at the size written in the file, then stretches it: its icons are about 32px, so on the
# full-screen panel they looked blurry. Every icon points at the addon's sharp copy instead
# (panorama/images/custom_game/k4/icons, written by gen_k4_icons.py from this stylesheet's references).
ICON_FOLDERS = {"icons/ui": "ui", "icons/equipment": "equipment", "hud/deathnotice": "deathnotice"}
css = re.sub(r"s2r://panorama/images/(icons/ui|icons/equipment|hud/deathnotice)/([a-z0-9_]+)\.vsvg",
             lambda match: f"s2r://panorama/images/custom_game/k4/icons/{ICON_FOLDERS[match.group(1)]}/{match.group(2)}.vsvg", css)

# Self-check: every @define name used as a colour is defined, and no rule uses a letter-spacing or uppercase.
assert "letter-spacing" not in css and "text-transform" not in css
assert css.count("{") == css.count("}"), "unbalanced braces"

open(OUT, "w", encoding="utf-8", newline="\n").write(css)
print(f"ok: contract {CONTRACT}, {css.count(chr(10))} lines")
