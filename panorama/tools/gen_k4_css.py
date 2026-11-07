"""Writes the K4 Panorama stylesheet (7.1): a centered 1240x760 window in the style of CS2's main menu, and the toast stack.

The classes the server swaps (docs/panel-contract.md) are generated at the bottom: game icons, rank emblems,
team patches, map backgrounds, medal tiers, bar widths, rank colors, toast colors and lengths.
Every image is one CS2 already ships, referenced the way Valve's stylesheets do (s2r://panorama/images/...).
The exceptions are the rank emblems and the icons: the addon carries sharp copies of CS2's own
(panorama/images/custom_game/k4/ranks and .../icons, written by gen_k4_ranks.py and gen_k4_icons.py),
because the game draws them at 32x13 and 32x32 and they looked blurry at the panel's sizes.
Sounds are CS2's own UI sound events, the ones Valve's Panorama files use.

Panorama scales the whole HUD by screen height (1080 is 1:1), so the window is a fixed 1240x760 at every
aspect ratio, centered over the game with no dim around it. Inside its 1px padding the sidebar is 220px and
the pages get 1018x758; every page is sized to fit that without scrolling (page padding 24px 28px, so
962x710 of content). The toasts and the promotion banner stay anchored to the screen, outside the window.
"""
import os
import re

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "styles", "custom_game", "k4_ui.css")

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

# Every map with a base 1080p screenshot in CS2 (panorama/images/map_icons/screenshots/1080p/<map>_png.vtex).
# Made from pak01_dir.vpk (2026-11-01) with vpk.py: entries matching that path, minus the numbered variants
# (<map>_1, cs_italy_0, ...) and the non-maps "random" and "lobby_mapveto". Kept here so the generator runs
# without the game; check_game_images.py fails if a CS2 update removes one.
MAPS = [
    "ar_baggage", "ar_pool_day", "ar_shoots", "ar_shoots_night", "cs_italy", "cs_office", "cs_shelter",
    "de_ancient", "de_ancient_night", "de_anubis", "de_boulder", "de_cache", "de_debris", "de_dust2",
    "de_eldorado", "de_fachwerk", "de_inferno", "de_mirage", "de_nuke", "de_overpass", "de_poseidon",
    "de_train", "de_vertigo", "rush_001",
]

# Toast color -> left strip and kind text color, and the CS2 sound the toast plays when it appears.
TOAST_COLORS = {
    "green": ("#6FA886", "UI.Mission.Complete"),
    "gold": ("#C8A45C", "UIPanorama.EOM.CardReveal"),
    "red": ("#C4675C", "UI.DeathMatchBonusAlertEnd"),
    "blue": ("#D4D4D4", "UIPanorama.popup_newweapon"),
    "purple": ("#8C8C8C", "UI.DeathMatchBonusAlertStart"),
}

# Toast length -> how long the timer bar takes to run out
TOAST_LENGTHS = {"short": "2.5s", "normal": "3.5s", "long": "5s"}

# Medal ring: light, middle and dark stop of the diagonal gradient
TIERS = {
    "gold": ("#F0DDB0", "#C8A45C", "#6E5A2E"),
    "silver": ("#F2F2F2", "#B4B4B4", "#5A5A5A"),
    "bronze": ("#E6C8B0", "#A97F5E", "#523B2A"),
}

# Panels that only draw a picture
PICTURES = [
    "k4-nav-icon", "k4-me-icon", "k4-h-rank-icon", "k4-cap-icon", "k4-h-m-icon", "k4-medal-icon", "k4-h-p-icon",
    "k4-h-q-icon", "k4-btn-icon", "k4-pf-icon", "k4-pf-st-icon", "k4-pf-fav-icon", "k4-pf-ss-icon",
    "k4-m-head-icon", "k4-m-icon", "k4-mw-icon", "k4-streak-icon", "k4-sd-icon", "k4-chip-icon", "k4-a-pin",
    "k4-lb-p-icon", "k4-lb-icon", "k4-rk-icon", "k4-rk-gift", "k4-rk-you-icon", "k4-st-icon", "k4-sw-icon",
    "k4-pt-head-icon", "k4-pt-icon", "k4-event-icon", "k4-close-icon", "k4-promo-icon", "k4-toast-icon",
    "k4-bg", "k4-bg-map", "k4-dp-icon", "k4-dt-icon", "k4-d-send-icon", "k4-d-in-icon", "k4-d-mine-icon", "k4-dl-icon",
]

# Grid cells spaced by a left margin, and rows spaced by a top margin; k4-first cancels it (see gen_k4_ui.py).
GAPS_LEFT = {
    "k4-h-card": 16, "k4-h-m": 10, "k4-h-a": 10, "k4-h-p": 10, "k4-h-q": 14, "k4-pf-sc": 12, "k4-pf-st": 12,
    "k4-pf-ss": 12, "k4-m": 16, "k4-a": 10, "k4-rk": 10, "k4-st": 12, "k4-lb-p": 20, "k4-dp": 10, "k4-dt": 10,
    "k4-ds": 10, "k4-d-btn": 10,
}
GAPS_TOP = {"k4-grid-row": 10, "k4-st-row": 12, "k4-sw": 28, "k4-d-step": 14, "k4-d-row": 10, "k4-dl": 6}

# No text-transform: uppercase anywhere. The client uppercases with its own locale (a Turkish client writes
# "MİSSİONS"), so K4 sends labels already capitalized in each reader's language.
css = r"""@define k4-text: #FFFFFF;
@define k4-soft: #D4D4D4;
@define k4-muted: #B0B0B0;
@define k4-dim: #8C8C8C;
@define k4-accent: #FFFFFF;
@define k4-gold: #C8A45C;
@define k4-gold-light: #DCC48A;
@define k4-green: #6FA886;
@define k4-green-light: #8FC4A4;
@define k4-red: #C4675C;
@define k4-tile: #0F0F0F9E;
@define k4-tile-line: #FFFFFF17;
@define k4-dur: 0.15s;

Label
{
	color: k4-text;
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

/* Spacers that push what follows to the end of a row (k4-grow) or a column (k4-vgrow) */
.k4-grow
{
	width: fill-parent-flow( 1.0 );
}

.k4-vgrow
{
	height: fill-parent-flow( 1.0 );
}

/* ---------------------------------------------------------------- frame */

.k4-panel
{
	width: 1240px;
	height: 760px;
	opacity: 1;
	transition-property: opacity;
	transition-duration: 0.2s;
	transition-timing-function: ease-out;
	horizontal-align: center;
	vertical-align: middle;
	padding: 1px;
	border: 1px solid #FFFFFF2E;
	background-color: #0A0A0A;
	box-shadow: #000000B3 0px 16px 48px 0px;
}

.k4-panel.k4-closed
{
	opacity: 0;
	visibility: collapse;
}

/* k4-bg always draws the fallback; k4-bg-map draws the current map over it, or nothing for an unknown map. */
.k4-bg, .k4-bg-map
{
	width: 100%;
	height: 100%;
}

.k4-bg
{
	background-color: #0A0A0A;
	saturation: 0;
}

/* The map, in grey (k4-bg), dims from left to right */
.k4-shade
{
	width: 100%;
	height: 100%;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #080808F7 ), color-stop( 0.45, #080808EB ), to( #080808C2 ) );
}

.k4-frame
{
	width: 100%;
	height: 100%;
	flow-children: right;
	overflow: clip;
}

.k4-main
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-view
{
	width: 100%;
	height: 100%;
	flow-children: down;
	visibility: collapse;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.2s;
}

.k4-view.k4-show
{
	visibility: visible;
	opacity: 1;
}

.k4-view-pad
{
	padding: 24px 28px 24px 28px;
}

.k4-header-space
{
	width: 100%;
	height: 36px;
	margin-bottom: 16px;
}

.k4-tile
{
	background-color: k4-tile;
	border: 1px solid k4-tile-line;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-cap
{
	font-size: 12px;
	letter-spacing: 2px;
	color: k4-muted;
}

/* One line, shrunk to fit: a long value (big playtime, a long translation) wrapped and was cut off. */
.k4-tile-value
{
	color: k4-text;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-place
{
	font-size: 20px;
	color: k4-text;
	margin-left: 14px;
}

.k4-reward
{
	font-size: 20px;
	color: k4-text;
	vertical-align: center;
}

/* ---------------------------------------------------------------- sidebar */

.k4-sidebar
{
	width: 220px;
	height: 100%;
	flow-children: down;
	background-color: #060606D9;
	border-right: 1px solid #FFFFFF12;
}

.k4-brand
{
	flow-children: right;
	margin: 22px 18px 16px 18px;
}

.k4-brand-name
{
	font-size: 30px;
	letter-spacing: 1px;
}

.k4-brand-sub
{
	font-size: 13px;
	letter-spacing: 3px;
	color: k4-muted;
	vertical-align: bottom;
	margin: 0px 0px 6px 8px;
}

.k4-me
{
	width: 196px;
	horizontal-align: center;
	flow-children: right;
	margin-bottom: 16px;
	padding: 10px;
	background-color: #FFFFFF0D;
	border: 1px solid #FFFFFF14;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-me:hover
{
	background-color: #FFFFFF17;
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.sidemenu_rollover";
}

.k4-me:active
{
	sound: "UIPanorama.sidemenu_select";
}

.k4-me-icon
{
	width: 52px;
	height: 21px;
	vertical-align: center;
}

.k4-me-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 10px;
	vertical-align: center;
}

.k4-me-name
{
	width: 100%;
	font-size: 16px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-me-meta
{
	width: 100%;
	font-size: 13px;
	color: k4-muted;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-nav
{
	width: 100%;
	height: 44px;
	flow-children: right;
	padding-left: 15px;
	border-left: 3px solid #FFFFFF00;
	transition-property: background-color;
	transition-duration: k4-dur;
}

.k4-nav-icon
{
	width: 18px;
	height: 18px;
	vertical-align: center;
	opacity: 0.7;
	transition-property: opacity;
	transition-duration: k4-dur;
}

.k4-nav-label
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 15px;
	letter-spacing: 1px;
	color: k4-muted;
	transition-property: color;
	transition-duration: k4-dur;
	width: fill-parent-flow( 1.0 );
	margin-right: 8px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-nav:hover
{
	background-color: #FFFFFF0D;
	sound: "UIPanorama.sidemenu_rollover";
}

.k4-nav:hover .k4-nav-label
{
	color: k4-text;
}

.k4-nav:active
{
	sound: "UIPanorama.sidemenu_select";
}

.k4-nav.k4-active
{
	background-color: #FFFFFF14;
	border-left: 3px solid k4-accent;
}

.k4-nav.k4-active .k4-nav-label
{
	color: k4-text;
}

.k4-nav.k4-active .k4-nav-icon
{
	opacity: 1;
}

.k4-season
{
	width: 196px;
	horizontal-align: center;
	flow-children: down;
	margin-bottom: 12px;
	padding: 12px 14px;
	background-color: #FFFFFF0A;
	border: 1px solid #FFFFFF1F;
}

.k4-season-title
{
	width: 100%;
	font-size: 12px;
	letter-spacing: 1px;
	color: k4-soft;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-season-left
{
	width: 100%;
	font-size: 20px;
	margin-top: 2px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-bar-season
{
	height: 4px;
	margin-top: 8px;
}

.k4-bar-season .k4-bar-fill
{
	background-color: k4-text;
}

.k4-copyright-box
{
	width: 100%;
	padding: 12px 10px 16px 10px;
	border-top: 1px solid #FFFFFF12;
}

.k4-copyright
{
	horizontal-align: center;
	text-align: center;
	font-size: 18px;
	letter-spacing: 0px;
	color: #FFFFFF;
}

/* ---------------------------------------------------------------- top right: points event and close */

.k4-topbar
{
	height: 36px;
	flow-children: right;
	horizontal-align: right;
	vertical-align: top;
	margin: 24px 28px 0px 0px;
}

.k4-event
{
	height: 36px;
	flow-children: right;
	padding: 0px 14px;
	margin-right: 12px;
	background-color: #FFFFFF14;
	border: 1px solid #FFFFFF4D;
}

.k4-event-icon
{
	width: 18px;
	height: 18px;
	vertical-align: center;
	wash-color: k4-text;
}

.k4-event-text
{
	margin-left: 8px;
	vertical-align: center;
	font-size: 16px;
	letter-spacing: 1px;
	color: k4-text;
}

.k4-close
{
	width: 36px;
	height: 36px;
	border: 1px solid #FFFFFF33;
	background-color: #0000004D;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-close-icon
{
	width: 16px;
	height: 16px;
	horizontal-align: center;
	vertical-align: center;
}

.k4-close:hover
{
	background-color: #FFFFFF1F;
	border: 1px solid #FFFFFF66;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-close:active
{
	sound: "UIPanorama.generic_button_press";
}

/* ---------------------------------------------------------------- shared controls */

.k4-bar
{
	width: 100%;
	height: 6px;
	background-color: #FFFFFF24;
}

.k4-bar-fill
{
	height: 100%;
	background-color: k4-accent;
	transition-property: width;
	transition-duration: 0.4s;
	transition-timing-function: ease-out;
}

.k4-btn
{
	height: 36px;
	flow-children: right;
	padding: 0px 14px;
	margin-right: 10px;
	border: 1px solid #FFFFFF4D;
	background-color: #00000066;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-btn:hover
{
	background-color: #FFFFFF1F;
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-btn:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-btn-icon
{
	width: 16px;
	height: 16px;
	margin-right: 8px;
	vertical-align: center;
}

.k4-icon-back
{
	transform: rotateZ( 90deg );
}

.k4-btn-label
{
	vertical-align: center;
	font-size: 14px;
	letter-spacing: 1px;
}

.k4-toggle-dot
{
	width: 10px;
	height: 10px;
	margin-right: 8px;
	vertical-align: center;
	background-color: #FFFFFF40;
}

.k4-toggle.k4-on
{
	border: 1px solid #FFFFFF99;
}

.k4-toggle.k4-on .k4-toggle-dot
{
	background-color: k4-text;
}

.k4-arrow
{
	width: 32px;
	height: 32px;
	vertical-align: center;
	border: 1px solid #FFFFFF40;
	background-color: #00000059;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-repeat: no-repeat;
	background-size: 16px 16px;
	background-position: 50% 50%;
	transition-property: background-color, border, opacity;
	transition-duration: k4-dur;
}

.k4-arrow-left { transform: rotateZ( 90deg ); }
.k4-arrow-right { transform: rotateZ( -90deg ); }

.k4-arrow:hover
{
	background-color: #FFFFFF1F;
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-arrow:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-arrow.k4-disabled
{
	opacity: 0.25;
}

.k4-pager
{
	height: 36px;
	flow-children: right;
}

.k4-pager-bottom
{
	horizontal-align: center;
	margin-top: 10px;
}

.k4-page
{
	min-width: 60px;
	margin: 0px 12px;
	vertical-align: center;
	text-align: center;
	font-size: 14px;
	color: k4-muted;
}

/* ---------------------------------------------------------------- medal: tier ring, dark disc, white icon */

.k4-ring
{
	border-radius: 50%;
	horizontal-align: center;
}

.k4-disc
{
	width: 100%;
	height: 100%;
	border-radius: 50%;
	background-color: gradient( radial, 50% 35%, 0% 0%, 70% 70%, from( #242424 ), to( #0C0C0C ) );
}

.k4-medal-icon
{
	width: 46%;
	height: 46%;
	horizontal-align: center;
	vertical-align: center;
}

.t-locked
{
	background-color: #2A2A2A;
}

.t-locked .k4-medal-icon
{
	opacity: 0.35;
}

.k4-ring-home { width: 68px; height: 68px; padding: 5px; }
.k4-ring-showcase { width: 92px; height: 92px; padding: 6px; }
.k4-ring-card { width: 70px; height: 70px; padding: 5px; }
.k4-ring-toast { width: 72px; height: 72px; padding: 5px; vertical-align: center; }

/* ---------------------------------------------------------------- home */

.k4-h-hero
{
	width: 100%;
	height: 180px;
	flow-children: right;
	margin-bottom: 16px;
}

.k4-h-rank-icon
{
	width: 320px;
	height: 130px;
	vertical-align: center;
}

.k4-h-hero-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 28px;
	vertical-align: center;
}

.k4-h-rank-name
{
	width: 100%;
	font-size: 60px;
	color: k4-text;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-h-points-row
{
	flow-children: right;
	margin-top: 6px;
}

.k4-h-points
{
	font-size: 22px;
}

.k4-bar-hero
{
	width: 420px;
	max-width: 100%;
	height: 8px;
	margin-top: 12px;
}

.k4-bar-hero .k4-bar-fill
{
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #9A9A9A ), to( #FFFFFF ) );
}

.k4-h-next
{
	margin-top: 8px;
	font-size: 15px;
	color: k4-muted;
}

.k4-h-cards
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
}

.k4-h-card
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	padding: 16px;
}

.k4-h-card:hover, .k4-h-q:hover
{
	background-color: #1C1C1CB3;
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.mainmenu_rollover";
}

.k4-h-card:active, .k4-h-q:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-cap-icon
{
	width: 20px;
	height: 20px;
	margin-bottom: 10px;
	opacity: 0.6;
}

.k4-h-slots
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
}

.k4-h-m
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	background-color: #FFFFFF0D;
	border: 1px solid #FFFFFF1F;
}

.k4-h-m-icon
{
	width: 40px;
	height: 40px;
	horizontal-align: center;
	vertical-align: center;
	margin-bottom: 30px;
}

.k4-h-m-value
{
	horizontal-align: center;
	vertical-align: center;
	margin-top: 40px;
	text-align: center;
	font-size: 18px;
	white-space: nowrap;
	text-overflow: shrink;
	max-width: 100%;
}

.k4-h-m.k4-done
{
	background-color: #6FA88629;
	border: 1px solid #6FA88699;
}

.k4-h-m.k4-done .k4-h-m-value
{
	color: k4-green-light;
}

.k4-h-a
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: center;
}

.k4-h-a-name
{
	width: 100%;
	margin-top: 8px;
	text-align: center;
	font-size: 14px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-bar-small
{
	width: 72px;
	max-width: 100%;
	horizontal-align: center;
	margin-top: 8px;
}

.k4-h-p
{
	width: fill-parent-flow( 1.0 );
	height: fit-children;
	flow-children: down;
	vertical-align: bottom;
}

.k4-h-p-icon
{
	width: 80px;
	height: 33px;
	horizontal-align: center;
}

.k4-h-p-name
{
	width: 100%;
	margin: 6px 0px;
	text-align: center;
	font-size: 14px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-step
{
	width: 100%;
}

.k4-step-pos
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 30px;
}

.k4-place-1 .k4-step { background-color: gradient( linear, 0% 0%, 0% 100%, from( #C8A45C ), to( #6E5A2E ) ); }
.k4-place-2 .k4-step { background-color: gradient( linear, 0% 0%, 0% 100%, from( #9A9A9A ), to( #4F4F4F ) ); }
.k4-place-3 .k4-step { background-color: gradient( linear, 0% 0%, 0% 100%, from( #A97F5E ), to( #5E4431 ) ); }
.k4-h-p.k4-place-1 .k4-step { height: 110px; }
.k4-h-p.k4-place-2 .k4-step { height: 78px; }
.k4-h-p.k4-place-3 .k4-step { height: 54px; }

.k4-h-quick
{
	width: 100%;
	height: 72px;
	flow-children: right;
	margin-top: 16px;
}

.k4-h-q
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-h-q-inner
{
	max-width: 100%;
	flow-children: right;
	horizontal-align: center;
	vertical-align: center;
	padding: 0px 10px;
}

.k4-h-q-icon
{
	width: 32px;
	height: 32px;
	vertical-align: center;
}

.k4-h-q-wide .k4-h-q-icon
{
	width: 80px;
}

.k4-h-q-value
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 28px;
}

/* ---------------------------------------------------------------- profile */

.k4-pf-head
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-pf-banner
{
	width: 100%;
	height: 190px;
	flow-children: right;
}

.k4-pf-emblem
{
	width: 200px;
	height: 81px;
	vertical-align: center;
}

.k4-pf-icon
{
	width: 100%;
	height: 100%;
}

.k4-pf-badge
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 36px;
}

.k4-pf-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 20px;
	vertical-align: center;
}

.k4-pf-name
{
	width: 100%;
	font-size: 48px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-line
{
	flow-children: right;
	margin-top: 4px;
}

.k4-pf-rank
{
	font-size: 20px;
	color: k4-text;
}

.k4-pf-points
{
	margin-left: 14px;
	font-size: 20px;
}

.k4-pf-showcase
{
	flow-children: down;
	margin-left: 16px;
	vertical-align: center;
}

.k4-pf-sc-row
{
	flow-children: right;
	horizontal-align: right;
}

.k4-pf-sc
{
	width: 124px;
	flow-children: down;
}

.k4-pf-sc-name
{
	width: 100%;
	margin-top: 6px;
	text-align: center;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-pf-sc-hint
{
	width: 396px;
	horizontal-align: right;
	text-align: right;
	font-size: 13px;
	color: k4-dim;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-stats
{
	width: 100%;
	height: 116px;
	flow-children: right;
	margin-top: 14px;
}

.k4-pf-st
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	padding: 12px 8px;
}

.k4-pf-st-icon
{
	width: 28px;
	height: 28px;
	horizontal-align: center;
	opacity: 0.9;
}

.k4-pf-st-value
{
	max-width: 100%;
	horizontal-align: center;
	margin-top: 6px;
	font-size: 28px;
}

.k4-pf-st-label
{
	max-width: 100%;
	horizontal-align: center;
	margin-top: 4px;
	font-size: 12px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pf-bottom
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
	margin-top: 14px;
}

.k4-pf-fav
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	margin-right: 14px;
}

.k4-pf-fav-icon
{
	width: 90%;
	height: 80px;
	horizontal-align: center;
	vertical-align: center;
	margin-bottom: 44px;
}

.k4-pf-fav-line
{
	max-width: 100%;
	flow-children: right;
	horizontal-align: center;
	vertical-align: center;
	margin-top: 80px;
}

.k4-pf-fav-name
{
	font-size: 24px;
}

.k4-pf-fav-kills
{
	margin-left: 8px;
	vertical-align: center;
	font-size: 17px;
	color: k4-muted;
}

.k4-pf-seasons
{
	width: fill-parent-flow( 2.0 );
	height: 100%;
	flow-children: right;
}

.k4-pf-ss
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
}

.k4-pf-ss-season
{
	horizontal-align: center;
	vertical-align: center;
	margin-bottom: 120px;
}

.k4-pf-ss-icon
{
	width: 150px;
	max-width: 90%;
	height: 61px;
	horizontal-align: center;
	vertical-align: center;
}

.k4-pf-ss-pos
{
	horizontal-align: center;
	vertical-align: center;
	margin-top: 120px;
	font-size: 30px;
}

.k4-pf-ss-none
{
	vertical-align: center;
	font-size: 16px;
	color: k4-dim;
}

/* ---------------------------------------------------------------- missions */

.k4-m-head
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-m-head-icon
{
	width: 22px;
	height: 22px;
	vertical-align: center;
}

.k4-m-reset
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 22px;
}

.k4-m-cards
{
	width: 100%;
	height: 400px;
	flow-children: right;
	margin-top: 16px;
}

.k4-m
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	padding: 16px 20px 20px 20px;
	border: 2px solid #FFFFFF26;
	background-color: #0F0F0FB3;
}

.k4-m.k4-done { border: 2px solid #6FA886CC; }

.k4-m-top
{
	width: 100%;
	flow-children: right;
}

.k4-m-diff
{
	padding: 4px 10px;
	vertical-align: center;
	font-size: 13px;
	letter-spacing: 1px;
	color: #0A0A0A;
	background-color: k4-muted;
}

.k4-easy .k4-m-diff { background-color: #8C8C8C; }
.k4-medium .k4-m-diff { background-color: #BDBDBD; }
.k4-hard .k4-m-diff { background-color: #FFFFFF; }

.k4-m-icon
{
	width: 100%;
	height: 84px;
	margin-bottom: 12px;
}

.k4-m-desc
{
	width: 100%;
	font-size: 20px;
}

.k4-bar-mission
{
	height: 8px;
	margin-top: 12px;
}

.k4-m.k4-done .k4-bar-fill { background-color: k4-green-light; }

.k4-m-bottom
{
	width: 100%;
	height: 36px;
	flow-children: right;
	margin-top: 12px;
}

.k4-m-progress
{
	vertical-align: center;
	font-size: 20px;
}

.k4-done .k4-m-progress, .k4-done .k4-mw-progress
{
	color: k4-green-light;
}

.k4-m-bottom .k4-btn
{
	margin-right: 0px;
}

.k4-mw
{
	width: 100%;
	flow-children: right;
	margin-top: 16px;
	padding: 16px 24px;
	border: 2px solid #FFFFFF4D;
	background-color: #0F0F0FB3;
}

.k4-mw.k4-done
{
	border: 2px solid #6FA886CC;
}

.k4-mw-icon
{
	width: 64px;
	height: 64px;
	vertical-align: center;
}

.k4-mw-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 20px;
	vertical-align: center;
}

.k4-mw-left
{
	font-size: 13px;
	letter-spacing: 2px;
	color: k4-soft;
}

.k4-mw-desc
{
	width: 100%;
	margin-top: 6px;
	font-size: 24px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-mw.k4-done .k4-bar-fill { background-color: k4-green-light; }

.k4-mw-side
{
	flow-children: down;
	margin-left: 20px;
	vertical-align: center;
}

.k4-mw-progress
{
	horizontal-align: right;
	font-size: 30px;
}

.k4-mw-reward
{
	horizontal-align: right;
	font-size: 20px;
}

.k4-streak
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
	margin-top: 8px;
}

.k4-streak-icon
{
	width: 32px;
	height: 32px;
	vertical-align: center;
}

.k4-streak-count
{
	width: 90px;
	margin-left: 12px;
	vertical-align: center;
	font-size: 28px;
	color: k4-text;
}

.k4-sd
{
	width: fill-parent-flow( 1.0 );
	height: 72px;
	margin-left: 10px;
	vertical-align: center;
	background-color: #FFFFFF0D;
	border: 2px solid #FFFFFF4D;
}

.k4-sd-icon
{
	width: 22px;
	height: 22px;
	horizontal-align: center;
	vertical-align: center;
	background-image: url("s2r://panorama/images/icons/ui/hot_trend.vsvg");
	saturation: 0;
	opacity: 0.5;
}

.k4-sd.k4-today
{
	border: 2px solid k4-accent;
}

.k4-sd.k4-today .k4-sd-icon
{
	opacity: 1;
}

.k4-sd.k4-on
{
	background-color: #FFFFFF26;
	border: 2px solid #FFFFFF59;
}

.k4-sd.k4-on .k4-sd-icon
{
	background-image: url("s2r://panorama/images/icons/ui/check.vsvg");
	wash-color: k4-text;
	opacity: 1;
}

.k4-streak-bonus
{
	width: 80px;
	margin-left: 12px;
	text-align: right;
	font-size: 22px;
}

/* ---------------------------------------------------------------- achievements */

.k4-a-head
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-chip
{
	height: 32px;
	flow-children: right;
	padding: 0px 14px;
	margin-right: 10px;
	vertical-align: center;
	border: 1px solid #FFFFFF33;
	background-color: #00000040;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-chip-icon
{
	width: 14px;
	height: 14px;
	margin-right: 6px;
	vertical-align: center;
	wash-color: k4-soft;
}

.k4-chip-label
{
	vertical-align: center;
	font-size: 13px;
	letter-spacing: 1px;
	color: k4-soft;
}

.k4-chip:hover
{
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-chip:active
{
	sound: "UIPanorama.submenu_select";
}

.k4-chip:hover .k4-chip-label, .k4-chip.k4-active .k4-chip-label
{
	color: k4-text;
}

.k4-chip.k4-active
{
	background-color: #FFFFFF1F;
	border: 1px solid k4-accent;
}

.k4-a-count
{
	margin-left: 16px;
	vertical-align: center;
	font-size: 24px;
}

.k4-grid
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-top: 14px;
}

.k4-grid-row
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
}

.k4-a
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	padding: 10px 8px 10px 8px;
	background-color: #0F0F0F8C;
	border: 1px solid #FFFFFF14;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-a:hover
{
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.inventory_item_rollover";
}

.k4-a:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-a.k4-pinned
{
	background-color: #FFFFFF14;
	border: 1px solid #FFFFFF80;
}

.k4-a-medal
{
	width: 110px;
	height: 82px;
	horizontal-align: center;
}

.k4-a-pin
{
	width: 24px;
	height: 24px;
	horizontal-align: right;
	vertical-align: top;
	wash-color: k4-text;
	visibility: collapse;
}

.k4-a.k4-pinned .k4-a-pin
{
	visibility: visible;
}

.k4-a-name
{
	width: 100%;
	margin-top: 6px;
	text-align: center;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-a-goal
{
	width: 100%;
	margin-top: 2px;
	text-align: center;
	font-size: 13px;
	color: k4-soft;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-bar-card
{
	width: 120px;
	max-width: 100%;
	horizontal-align: center;
	margin-top: 6px;
}

/* A full bar is a finished achievement: gold, like its ring */
.k4-bar-card .k4-bar-fill.w20 { background-color: k4-gold; }

/* ---------------------------------------------------------------- leaderboard */

.k4-lb-stage
{
	width: 100%;
	height: 380px;
	flow-children: down;
	padding: 24px 28px 0px 28px;
	background-color: gradient( linear, 0% 0%, 0% 100%, from( #08080800 ), to( #0A0A0AF2 ) );
}

.k4-lb-head
{
	width: 100%;
	height: 44px;
	flow-children: right;
}

.k4-lb-head .k4-arrow
{
	width: 40px;
	height: 40px;
}

.k4-lb-head .k4-pager
{
	margin-left: 24px;
	height: 44px;
}

.k4-lb-season
{
	width: 260px;
	flow-children: down;
	vertical-align: center;
}

.k4-lb-season-name
{
	width: 100%;
	text-align: center;
	font-size: 20px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-lb-status
{
	width: 100%;
	text-align: center;
	font-size: 12px;
	letter-spacing: 2px;
	color: k4-muted;
}

.k4-lb-podium
{
	width: fit-children;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
	horizontal-align: center;
}

.k4-lb-p
{
	width: 230px;
	flow-children: down;
	vertical-align: bottom;
}

.k4-lb-p-icon
{
	width: 130px;
	height: 53px;
	horizontal-align: center;
}

.k4-lb-p.k4-place-1 .k4-lb-p-icon
{
	width: 170px;
	height: 69px;
}

.k4-lb-p-name
{
	width: 100%;
	margin-top: 6px;
	text-align: center;
	font-size: 20px;
	white-space: nowrap;
	text-overflow: ellipsis;
	transition-property: color;
	transition-duration: k4-dur;
	color: k4-soft;
}

.k4-lb-p-points
{
	horizontal-align: center;
	font-size: 15px;
	color: k4-soft;
}

.k4-lb-p .k4-step
{
	margin-top: 6px;
}

.k4-lb-p .k4-step-pos
{
	vertical-align: top;
	margin-top: 4px;
	font-size: 40px;
	color: #0000008C;
}

.k4-lb-p.k4-place-1 .k4-step { height: 120px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #DCC48A ), to( #8F7A45 ) ); }
.k4-lb-p.k4-place-2 .k4-step { height: 84px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #D4D4D4 ), to( #7A7A7A ) ); }
.k4-lb-p.k4-place-3 .k4-step { height: 60px; background-color: gradient( linear, 0% 0%, 0% 100%, from( #C29A7C ), to( #6E5240 ) ); }

.k4-lb-p:hover
{
	sound: "UIPanorama.generic_button_rollover";
}

.k4-lb-p:hover .k4-lb-p-name
{
	color: k4-text;
}

.k4-lb-list
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: down;
	padding: 10px 28px 24px 28px;
}

.k4-lb-row
{
	width: 100%;
	height: 38px;
	flow-children: right;
	margin-bottom: 3px;
	padding: 0px 18px;
	transition-property: background-color;
	transition-duration: k4-dur;
}

.k4-lb-alt
{
	background-color: #FFFFFF08;
}

Button.k4-lb-row:hover
{
	background-color: #FFFFFF14;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-lb-pos
{
	width: 48px;
	vertical-align: center;
	font-size: 18px;
	color: k4-muted;
}

.k4-lb-icon
{
	width: 80px;
	height: 32px;
	margin-left: 16px;
	vertical-align: center;
}

.k4-lb-name
{
	width: fill-parent-flow( 1.0 );
	margin-left: 16px;
	vertical-align: center;
	font-size: 17px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-lb-points
{
	margin-left: 16px;
	vertical-align: center;
	font-size: 17px;
}

.k4-lb-me
{
	height: 42px;
	margin: 0px;
	border: 2px solid k4-accent;
	background-color: #FFFFFF1F;
}

.k4-lb-me .k4-lb-pos
{
	color: k4-accent;
}

/* ---------------------------------------------------------------- ranks */

.k4-rk-head
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-rk
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	background-color: #0F0F0F8C;
	border: 2px solid #FFFFFF14;
}

.k4-rk.k4-current
{
	background-color: #FFFFFF1A;
	border: 2px solid k4-accent;
}

.k4-rk-body
{
	width: 100%;
	flow-children: down;
	vertical-align: center;
}

.k4-rk-icon
{
	width: 130px;
	max-width: 86%;
	height: 53px;
	horizontal-align: center;
}

.k4-rk-name
{
	width: 92%;
	horizontal-align: center;
	margin-top: 4px;
	text-align: center;
	font-size: 15px;
	color: k4-text;
}

.k4-rk-points
{
	horizontal-align: center;
	margin-top: 2px;
	font-size: 13px;
	color: k4-muted;
}

.k4-rk-gift
{
	width: 20px;
	height: 20px;
	horizontal-align: right;
	vertical-align: top;
	margin: 8px 8px 0px 0px;
	wash-color: k4-soft;
}

.k4-rk-you
{
	padding: 2px 8px;
	margin: 8px 0px 0px 8px;
	background-color: k4-accent;
}

.k4-rk-you-icon
{
	width: 14px;
	height: 14px;
	wash-color: #0A0A0A;
}

/* ---------------------------------------------------------------- stats */

.k4-st-tiles
{
	width: 100%;
	flow-children: down;
}

.k4-st-row
{
	width: 100%;
	height: 80px;
	flow-children: right;
}

.k4-st
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: right;
	padding: 0px 16px;
}

.k4-st-icon
{
	width: 40px;
	height: 40px;
	vertical-align: center;
}

.k4-st-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 14px;
	vertical-align: center;
}

.k4-st-value
{
	width: 100%;
	font-size: 34px;
}

.k4-st-bottom
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: right;
	margin-top: 14px;
}

.k4-st-weapons
{
	width: fill-parent-flow( 3.0 );
	height: 100%;
	padding: 20px 24px;
}

.k4-sw-list
{
	width: 100%;
	flow-children: down;
	vertical-align: center;
}

.k4-sw
{
	width: 100%;
	height: 64px;
	flow-children: right;
}

.k4-sw-icon
{
	width: 170px;
	height: 54px;
	vertical-align: center;
}

.k4-bar-weapon
{
	width: fill-parent-flow( 1.0 );
	height: 10px;
	margin: 0px 16px;
	vertical-align: center;
	background-color: #FFFFFF1A;
}

.k4-bar-weapon .k4-bar-fill
{
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #7A7A7A ), to( #E0E0E0 ) );
}

.k4-sw-kills
{
	width: 70px;
	vertical-align: center;
	text-align: right;
	font-size: 24px;
}

.k4-pt
{
	width: fill-parent-flow( 2.0 );
	height: 100%;
	margin-left: 14px;
	padding: 20px 22px;
}

.k4-pt-list
{
	width: 100%;
	flow-children: down;
	vertical-align: center;
}

.k4-pt-head
{
	width: 100%;
	flow-children: right;
}

.k4-pt-head-icon
{
	width: 32px;
	height: 32px;
	vertical-align: center;
}

.k4-pt-total
{
	width: fill-parent-flow( 1.0 );
	margin-left: 12px;
	vertical-align: center;
	font-size: 34px;
}

.k4-pt-team
{
	width: 100%;
	flow-children: right;
	margin-top: 16px;
}

.k4-pt-icon
{
	width: 56px;
	height: 56px;
	vertical-align: center;
}

.k4-pt-spec .k4-pt-icon
{
	width: 32px;
	height: 32px;
	margin: 0px 12px;
}

.k4-pt-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 14px;
	vertical-align: center;
}

.k4-pt-line
{
	width: 100%;
	flow-children: right;
}

.k4-pt-name
{
	vertical-align: bottom;
	font-size: 15px;
	letter-spacing: 1px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-pt-time
{
	vertical-align: bottom;
	font-size: 22px;
	white-space: nowrap;
}

.k4-bar-team
{
	height: 8px;
	margin-top: 6px;
	background-color: #FFFFFF1A;
}

.k4-pt-t .k4-pt-name { color: k4-gold; }
.k4-pt-t .k4-bar-fill { background-color: k4-gold; }
.k4-pt-ct .k4-pt-name { color: #8FA6BA; }
.k4-pt-ct .k4-bar-fill { background-color: #8FA6BA; }
.k4-pt-spec .k4-pt-name { color: k4-dim; }
.k4-pt-spec .k4-bar-fill { background-color: k4-dim; }

/* ---------------------------------------------------------------- duels (7.2) */

/* Two columns in the 962x710 page: the challenge builder (566px) and the incoming, own and running duels (372px).
   Fixed heights, left: step 36 + 6 + players 194, 14 + 36 + 6 + types 178, 14 + 36 + 6 + stakes 36, 14 + foot 56 = 632.
   Right: header space 52, incoming 172, own 12 + 98, caption 18 + 17, running 8 + 218 = 595. */
.k4-d-cols
{
	width: 100%;
	height: 100%;
	flow-children: right;
}

.k4-d-build
{
	width: 566px;
	height: 100%;
	flow-children: down;
}

.k4-d-side
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	flow-children: down;
	margin-left: 24px;
}

.k4-d-step
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-d-cap
{
	vertical-align: center;
	font-size: 13px;
	color: k4-soft;
}

.k4-d-grid
{
	width: 100%;
	flow-children: down;
	margin-top: 6px;
}

.k4-d-row
{
	width: 100%;
	flow-children: right;
}

/* Players: fixed widths, so a short last page keeps its cards the same size (4 x 134 + 3 x 10 = 566). */
.k4-dp
{
	width: 134px;
	height: 92px;
	flow-children: down;
	padding: 8px;
	background-color: k4-tile;
	border: 2px solid #FFFFFF14;
	transition-property: background-color, border, opacity;
	transition-duration: k4-dur;
}

.k4-dp:hover
{
	border: 2px solid #FFFFFF80;
	sound: "UIPanorama.inventory_item_rollover";
}

.k4-dp:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-dp.k4-active
{
	background-color: #FFFFFF1A;
	border: 2px solid k4-accent;
}

.k4-dp.k4-disabled
{
	opacity: 0.35;
}

.k4-dp-icon
{
	width: 76px;
	height: 31px;
	margin-top: 2px;
	horizontal-align: center;
}

.k4-dp-name
{
	width: 100%;
	margin-top: 4px;
	text-align: center;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-dp-points
{
	horizontal-align: center;
	font-size: 12px;
	color: k4-muted;
}

/* Duel types: 3 x 182 + 2 x 10 = 566; the explanation lies over the whole card. */
.k4-dt
{
	width: 182px;
	height: 84px;
	background-color: k4-tile;
	border: 2px solid #FFFFFF14;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-dt:hover
{
	border: 2px solid #FFFFFF80;
	sound: "UIPanorama.inventory_item_rollover";
}

.k4-dt:active
{
	sound: "UIPanorama.inventory_item_select";
}

.k4-dt.k4-active
{
	background-color: #FFFFFF1A;
	border: 2px solid k4-accent;
}

.k4-dt-body
{
	width: 100%;
	flow-children: down;
	vertical-align: center;
}

.k4-dt-icon
{
	width: 96px;
	height: 40px;
	horizontal-align: center;
}

.k4-dt-name
{
	width: 100%;
	margin-top: 6px;
	text-align: center;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-dt.k4-active .k4-dt-name
{
	color: k4-text;
}

/* Stakes */
.k4-d-stakes
{
	width: 100%;
	height: 36px;
	flow-children: right;
	margin-top: 6px;
}

.k4-ds
{
	min-width: 80px;
	height: 36px;
	padding: 0px 18px;
	border: 1px solid #FFFFFF33;
	background-color: #00000040;
	transition-property: background-color, border, opacity;
	transition-duration: k4-dur;
}

.k4-ds-label
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 16px;
	color: k4-soft;
}

.k4-ds:hover
{
	border: 1px solid #FFFFFF80;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-ds:active
{
	sound: "UIPanorama.submenu_select";
}

.k4-ds.k4-active
{
	background-color: #FFFFFF1F;
	border: 1px solid k4-accent;
}

.k4-ds.k4-active .k4-ds-label
{
	color: k4-text;
}

.k4-ds.k4-disabled
{
	opacity: 0.3;
}

/* What the challenge will be, why it cannot be sent yet, and Send */
.k4-d-foot
{
	width: 100%;
	height: 56px;
	flow-children: right;
	margin-top: 14px;
}

.k4-d-foot-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-right: 16px;
	vertical-align: center;
}

.k4-d-summary
{
	width: 100%;
	font-size: 16px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-status
{
	width: 100%;
	margin-top: 2px;
	font-size: 13px;
	color: k4-red;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-send
{
	width: 220px;
	height: 56px;
	border: 1px solid #FFFFFF;
	background-color: #FFFFFF;
	transition-property: background-color, border, opacity;
	transition-duration: k4-dur;
}

.k4-d-send:hover
{
	background-color: #D4D4D4;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-d-send:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-d-send.k4-disabled, .k4-d-send.k4-disabled:hover
{
	opacity: 0.4;
	background-color: #FFFFFF14;
	border: 1px solid #FFFFFF26;
}

.k4-d-send.k4-disabled .k4-d-send-label
{
	color: k4-dim;
}

.k4-d-send.k4-disabled .k4-d-send-icon
{
	wash-color: k4-dim;
}

.k4-d-send-inner
{
	flow-children: right;
	horizontal-align: center;
	vertical-align: center;
}

.k4-d-send-icon
{
	width: 24px;
	height: 24px;
	margin-right: 10px;
	vertical-align: center;
	wash-color: #0A0A0A;
}

.k4-d-send-label
{
	vertical-align: center;
	font-size: 20px;
	letter-spacing: 1px;
	color: #0A0A0A;
}

/* Incoming challenge: the most prominent card on the page */
.k4-d-in
{
	width: 100%;
	flow-children: down;
	padding: 16px;
	border: 2px solid k4-accent;
	background-color: #141414E6;
}

.k4-d-in-top, .k4-d-mine-top
{
	width: 100%;
	flow-children: right;
}

.k4-d-in-top
{
	height: 66px;
}

.k4-d-in-icon
{
	width: 72px;
	height: 56px;
	vertical-align: center;
}

.k4-d-in-text, .k4-d-mine-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 14px;
	vertical-align: center;
}

.k4-d-in-title, .k4-d-mine-title
{
	width: 100%;
	font-size: 13px;
	letter-spacing: 2px;
	color: k4-text;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-in-msg
{
	width: 100%;
	max-height: 46px;
	margin-top: 2px;
	font-size: 17px;
	text-overflow: ellipsis;
}

.k4-bar-duel
{
	height: 6px;
	margin-top: 12px;
}


.k4-d-in-btns
{
	width: 100%;
	height: 40px;
	flow-children: right;
	margin-top: 12px;
}

.k4-d-btn
{
	width: fill-parent-flow( 1.0 );
	height: 100%;
	transition-property: background-color, border;
	transition-duration: k4-dur;
}

.k4-d-btn-label
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 16px;
	letter-spacing: 1px;
}

.k4-d-accept
{
	background-color: #FFFFFF;
	border: 1px solid #FFFFFF;
}

.k4-d-accept .k4-d-btn-label
{
	color: #0A0A0A;
}

.k4-d-accept:hover
{
	background-color: #D4D4D4;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-d-decline
{
	background-color: #00000066;
	border: 1px solid #FFFFFF4D;
}

.k4-d-decline:hover
{
	background-color: #C4675C40;
	border: 1px solid k4-red;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-d-btn:active
{
	sound: "UIPanorama.generic_button_press";
}

/* The player's own running duel */
.k4-d-mine
{
	width: 100%;
	flow-children: down;
	margin-top: 12px;
	padding: 14px;
	border: 1px solid #FFFFFF33;
	background-color: k4-tile;
}

.k4-d-mine-top
{
	height: 52px;
}

.k4-d-mine-icon
{
	width: 64px;
	height: 44px;
	vertical-align: center;
}

/* Keeps the line off the score; it shrinks rather than cuts, because the time left is at its end. */
.k4-d-mine-text
{
	width: fill-parent-flow( 1.0 );
	margin-left: 12px;
}

.k4-d-mine-msg
{
	width: 100%;
	margin-top: 2px;
	font-size: 15px;
	color: k4-soft;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-d-mine-score
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 40px;
	white-space: nowrap;
}

.k4-d-mine .k4-bar-duel
{
	margin-top: 10px;
}

/* Every running duel */
.k4-d-live-cap
{
	margin-top: 18px;
	font-size: 13px;
	color: k4-soft;
}

.k4-d-live
{
	width: 100%;
	flow-children: down;
	margin-top: 8px;
}

.k4-dl
{
	width: 100%;
	height: 50px;
	flow-children: right;
	padding: 0px 12px;
	background-color: #FFFFFF0A;
	border: 1px solid #FFFFFF12;
}

.k4-dl-icon
{
	width: 56px;
	height: 28px;
	vertical-align: center;
}

.k4-dl-text
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 12px;
	vertical-align: center;
}

.k4-dl-names
{
	width: 100%;
	font-size: 15px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-dl-info
{
	width: 100%;
	font-size: 12px;
	color: k4-muted;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-dl-score
{
	margin-left: 10px;
	vertical-align: center;
	font-size: 20px;
	color: k4-text;
	white-space: nowrap;
}

.k4-dl-none
{
	margin-top: 4px;
	font-size: 14px;
	color: k4-dim;
}

/* ---------------------------------------------------------------- toasts: under the kill feed, newest on top */

@keyframes 'k4-toast-in'
{
	0%
	{
		opacity: 0;
		transform: translateX( 120px );
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
	width: 480px;
	flow-children: down;
	horizontal-align: right;
	vertical-align: top;
	margin: 240px 24px 0px 0px;
}

.k4-toast
{
	width: 480px;
	height: 100px;
	margin-bottom: 10px;
	border: 1px solid #FFFFFF1A;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #0A0A0AF0 ), to( #0A0A0ACC ) );
}

.k4-toast:not(.k4-gone)
{
	animation-name: k4-toast-in;
	animation-duration: 0.25s;
	animation-timing-function: ease-out;
	animation-iteration-count: 1;
}

/* Leaving: the server adds k4-out, waits for this slide-out, then hides the toast. */
.k4-toast
{
	transition-property: opacity, transform;
	transition-duration: 0.3s;
	transition-timing-function: ease-in;
}

.k4-toast.k4-out
{
	opacity: 0;
	transform: translateX( 120px );
}

.k4-promo
{
	transition-property: opacity, transform;
	transition-duration: 0.3s;
	transition-timing-function: ease-in;
}

.k4-promo.k4-out
{
	opacity: 0;
	transform: scale3d( 0.9, 0.9, 1 );
}

.k4-toast-0 { border: 1px solid #FFFFFF40; }
.k4-toast-1 { opacity: 0.85; }
.k4-toast-2 { opacity: 0.6; }

.k4-toast-strip
{
	width: 5px;
	height: 100%;
	background-color: k4-green;
}

.k4-toast-row
{
	width: 100%;
	height: 100%;
	flow-children: right;
	padding: 0px 20px 0px 24px;
}

.k4-toast-pic
{
	width: 100px;
	height: 72px;
	vertical-align: center;
}

.k4-toast-icon
{
	width: 92px;
	height: 44px;
	horizontal-align: center;
	vertical-align: center;
}

.k4-toast-body
{
	width: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-left: 16px;
	vertical-align: center;
}

.k4-toast-kind
{
	width: 100%;
	font-size: 14px;
	letter-spacing: 3px;
	white-space: nowrap;
	text-overflow: shrink;
}

.k4-toast-text
{
	width: 100%;
	margin-top: 4px;
	font-size: 21px;
	white-space: nowrap;
	text-overflow: ellipsis;
}

.k4-bar-toast
{
	height: 5px;
	margin-top: 6px;
}

.k4-toast-value
{
	margin-left: 12px;
	vertical-align: center;
	font-size: 26px;
	color: k4-text;
	white-space: nowrap;
}

/* Full width while the toast waits, empty at the end of its length; without k4-run it snaps back at once. */
.k4-toast-timer
{
	width: 100%;
	height: 3px;
	vertical-align: bottom;
	background-color: #FFFFFF59;
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
	background-color: #00000099;
	font-size: 15px;
	letter-spacing: 2px;
	color: k4-muted;
}

.k4-promo
{
	flow-children: down;
	horizontal-align: center;
	vertical-align: top;
	margin-top: 120px;
	padding: 22px 70px 26px 70px;
	border: 1px solid #FFFFFF59;
	background-color: gradient( radial, 50% 50%, 0% 0%, 70% 70%, from( #2A2A2AD9 ), to( #0A0A0AE6 ) );
}

.k4-promo:not(.k4-gone)
{
	animation-name: k4-promo-in;
	animation-duration: 0.35s;
	animation-timing-function: ease-out;
	animation-iteration-count: 1;
	sound: "UI.ArmsRace.BecomeMatchLeader";
}

.k4-promo-title
{
	horizontal-align: center;
	font-size: 18px;
	letter-spacing: 6px;
	color: k4-text;
}

.k4-promo-icon
{
	width: 300px;
	height: 122px;
	horizontal-align: center;
	margin-top: 6px;
}

.k4-promo-rank
{
	horizontal-align: center;
	margin-top: 6px;
	font-size: 40px;
}

/* ---------------------------------------------------------------- generated */

/* ---------------------------------------------------------------- explanations (7.1.1) */

/* Everything shown as a picture explains itself: hovering a card lays a short explanation over it. */
.k4-tip
{
	width: 100%;
	height: 100%;
	background-color: #050505F2;
	border: 1px solid #FFFFFF40;
	opacity: 0;
	transition-property: opacity;
	transition-duration: 0.12s;
}

.k4-has-tip:hover .k4-tip
{
	opacity: 1;
}

.k4-tip-text
{
	width: 90%;
	horizontal-align: center;
	vertical-align: center;
	text-align: center;
	font-size: 13px;
	color: #FFFFFF;
}

/* Cards that stack their content keep it in a body, so the explanation can lie over it instead of below. */
.k4-a
{
	flow-children: none;
}

.k4-a-body
{
	width: 100%;
	height: 100%;
	flow-children: down;
}

.k4-a-desc
{
	width: 100%;
	margin: 2px 6px 4px 6px;
	text-align: center;
	font-size: 12px;
	color: #B0B0B0;
	max-height: 30px;
	text-overflow: ellipsis;
}

.k4-h-a
{
	flow-children: none;
}

.k4-h-a-body
{
	width: 100%;
	flow-children: down;
}

.k4-pf-sc
{
	width: 124px;
	flow-children: none;
}

.k4-pf-sc-body
{
	width: 100%;
	flow-children: down;
}

.k4-pf-sc-desc
{
	width: 100%;
	margin-top: 2px;
	text-align: center;
	font-size: 12px;
	color: #B0B0B0;
}

/* The mission streak: what it is, and what each day of it pays. */
.k4-streak-wrap
{
	width: 100%;
	height: fill-parent-flow( 1.0 );
	flow-children: down;
	margin-top: 16px;
}

.k4-streak-head
{
	width: 100%;
	height: 36px;
	flow-children: right;
}

.k4-streak-head .k4-streak-count
{
	width: 60px;
}

.k4-streak-hint
{
	margin-left: 8px;
	vertical-align: center;
	font-size: 15px;
	color: #D4D4D4;
}

.k4-streak
{
	height: 72px;
	margin-top: 8px;
}

.k4-sd
{
	height: 72px;
	margin-left: 10px;
	flow-children: none;
}

.k4-sd.k4-first
{
	margin-left: 0px;
}

.k4-sd-body
{
	width: 100%;
	height: 100%;
	flow-children: down;
}

.k4-sd-body .k4-sd-icon
{
	vertical-align: top;
	margin-top: 10px;
}

.k4-sd-day
{
	horizontal-align: center;
	margin-top: 2px;
	font-size: 13px;
	letter-spacing: 1px;
	color: #FFFFFF;
}

.k4-sd-bonus
{
	horizontal-align: center;
	font-size: 13px;
	color: k4-soft;
}

.k4-sw-name
{
	width: 110px;
	margin-left: 12px;
	vertical-align: center;
	font-size: 17px;
	white-space: nowrap;
	text-overflow: shrink;
}

"""

css += "\n/* Pictures: a game image as background (CS2's custom HUD allows no Image elements) */\n"
css += ",\n".join(f".{name}" for name in PICTURES)
css += "\n{\n\tbackground-size: contain;\n\tbackground-repeat: no-repeat;\n\tbackground-position: 50% 50%;\n}\n"
# The backgrounds fill the screen instead; this repeats after the shared rule so it wins.
css += ".k4-bg, .k4-bg-map { background-size: cover; }\n"

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

css += "\n/* Map backgrounds */\n"
css += '.map-default { background-image: url("s2r://panorama/images/backgrounds/season_stats_background_png.vtex"); }\n'
for name in MAPS:
    css += f'.map-{name} {{ background-image: url("s2r://panorama/images/map_icons/screenshots/1080p/{name}_png.vtex"); }}\n'

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

open(OUT, "w", encoding="utf-8", newline="\n").write(css)
print(f"ok: {len(MAPS)} maps")
