"""Writes the K4 Panorama stylesheet.

Colors, sizes and sounds follow CS2's own panels (scoreboard.css, mainmenu.css navbar, hudmissionpanel.css,
csgostyles.css). Every image is one CS2 already ships, referenced the way Valve's stylesheets do
(s2r://panorama/images/....vsvg). The one exception is the rank emblems: the addon carries sharp copies
of CS2's own (panorama/images/custom_game/k4/ranks, written by gen_k4_ranks.py), because the game's are 32x13.
Generated parts: icon classes, rank icons, progress bar widths and rank colors.
"""
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "styles", "custom_game", "k4_ui.css")

COLORS = {
    "default": "#FFFFFF", "white": "#FFFFFF", "darkred": "#C83232", "green": "#4CCB4C",
    "lightyellow": "#EFE47A", "lightblue": "#8FC7FF", "olive": "#A2C24A", "lime": "#9AFF6E",
    "red": "#FF4D4D", "lightpurple": "#C99BFF", "purple": "#B054E8", "grey": "#B0B0B0",
    "yellow": "#F2CC60", "gold": "#E6B84C", "silver": "#C0C8D0", "blue": "#5B8DEF",
    "darkblue": "#3A5BD0", "bluegrey": "#9FB0C8", "magenta": "#E05CC8", "lightred": "#FF7B72",
    "orange": "#FF9D47",
}

# panorama/images/icons/ui/<name>.vsvg, used as ic-<name> by the server (stats, missions, toasts)
UI_ICONS = [
    "kill", "kill_headshot", "map_death", "stats", "stat_headshots", "stat_assists", "stat_mvps", "trophy",
    "crosshair_circle", "clock", "bullet", "hostage_rescued", "play", "winner_ribbon", "check", "hot_trend",
    "cold_trend", "trade_up", "mission_star", "bomb_c4", "xp_rank", "quest", "leader", "gift", "refresh_skinny",
    "crosshair", "first_person", "multipleenemieshere", "icon_star_multi", "shield", "health",
]

# panorama/images/hud/deathnotice/<name>.vsvg (the kill feed's kill markers), used as dn-<name> for style-kill missions
DEATHNOTICE_ICONS = ["penetrate", "smoke_kill", "inairkill", "blind_kill", "revenge", "domination"]

# panorama/images/icons/equipment/<name>.vsvg (killfeed icons), used as eq-<name> for weapon missions
EQUIPMENT_ICONS = [
    "knife", "hegrenade", "flashbang_assist", "planted_c4", "defuser", "taser", "molotov", "incgrenade",
    "ak47", "aug", "awp", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar", "glock",
    "hkp2000", "m249", "m4a1", "m4a1_silencer", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev", "nova", "p2000",
    "p250", "p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer", "xm1014",
]

# No text-transform: uppercase anywhere. The client uppercases with its own locale (a Turkish client writes
# "MİSSİONS"), so K4 sends labels already capitalized in each reader's language.
css = r"""@define k4-text: #FFFFFF;
@define k4-base: #CCCCCC;
@define k4-muted: #8F98A1;
@define k4-accent: #82D8FF;
@define k4-gold: #E4AE39;
@define k4-green: #4CFF6A;
@define k4-red: #DB4437;
@define k4-cell: #ffffff08;
@define k4-cell-dark: #ffffff05;
@define k4-cell-light: #ffffff10;
@define k4-line: #ffffff12;
@define k4-dur: 0.15s;

.k4-root
{
	width: 100%;
	height: 100%;
	z-index: 2000000000;
}

/* ---------------------------------------------------------------- panel: the CS2 scoreboard frame */

.k4-panel
{
	width: 640px;
	horizontal-align: center;
	vertical-align: middle;
	opacity: 1;
	transform: scale3d(1, 1, 1);
	transition-property: opacity, transform;
	transition-duration: k4-dur;
	transition-timing-function: ease-out;
}

.k4-panel.k4-closed
{
	opacity: 0;
	transform: scale3d(0.97, 0.97, 1);
	visibility: collapse;
}

.k4-bg
{
	width: 100%;
	height: 100%;
	background-color: rgba(0, 0, 0, 0.88);
	background-image: url("s2r://panorama/images/backgrounds/bluedots_large_png.vtex");
	background-size: 360px 360px;
	background-img-opacity: 0.015;
	border-radius: 5px;
	box-shadow: #00000080 0px 0px 16px 0px;
}

.k4-content
{
	width: 100%;
	flow-children: down;
	padding: 0px 0px 18px 0px;
}

.k4-header
{
	width: 100%;
	height: 48px;
	flow-children: right;
	padding: 0px 10px 0px 18px;
}

/* Static icons are Panels with a game image as background (CS2's custom HUD allows no Image elements). */
.k4-tab-icon, .k4-meta-icon, .k4-btn-icon
{
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
}

.k4-subtitle
{
	white-space: nowrap;
	color: k4-text;
	font-size: 17px;
	letter-spacing: 0.3px;
	vertical-align: center;
	width: fill-parent-flow(1.0);
	text-overflow: ellipsis;
}

/* The CloseButton class comes from csgostyles; this only places it. */
.k4-close
{
	vertical-align: center;
	sound: "UIPanorama.generic_button_rollover";
}

.k4-close:active
{
	sound: "UIPanorama.generic_button_press";
}

/* ---------------------------------------------------------------- tabs: the CS2 main-menu navbar */

.k4-tabs
{
	width: 100%;
	height: 44px;
	flow-children: right;
	padding: 0px 12px;
	background-color: rgba(0, 0, 0, 0.25);
	border-bottom: 1px solid k4-line;
}

.k4-tab
{
	height: 32px;
	margin: 0px 3px;
	padding: 0px 8px;
	flow-children: right;
	vertical-align: center;
	border-radius: 3px;
	transition-property: background-color;
	transition-duration: k4-dur;
}

.k4-tab-icon
{
	width: 16px;
	height: 16px;
	margin-right: 6px;
	vertical-align: center;
	wash-color: k4-muted;
}

.k4-tab:hover .k4-tab-icon
{
	wash-color: k4-base;
}

.k4-tab Label
{
	color: k4-text;
	font-size: 15px;
	vertical-align: center;
}

.k4-tab:hover
{
	background-color: rgba(255, 255, 255, 0.04);
	sound: "UIPanorama.loadout_sector_scroll";
}

.k4-tab.k4-active
{
	background-color: #82d7ff1a;
	border-bottom: 2px solid k4-accent;
}

.k4-tab.k4-active Label
{
	color: k4-accent;
	sound: "UIPanorama.submenu_select";
}

.k4-tab.k4-active .k4-tab-icon
{
	wash-color: k4-accent;
}

/* ---------------------------------------------------------------- shared */

.k4-view
{
	width: 100%;
	flow-children: down;
	padding: 16px 18px 0px 18px;
	visibility: collapse;
}

.k4-view.k4-show
{
	visibility: visible;
}

.k4-gone
{
	visibility: collapse;
}

.k4-split
{
	width: 100%;
	flow-children: right;
	margin-bottom: 8px;
}

.k4-split-last
{
	margin-bottom: 0px;
}

.k4-muted
{
	color: k4-muted;
	font-size: 15px;
	vertical-align: center;
}

.k4-fill
{
	width: fill-parent-flow(1.0);
}

.k4-right
{
	horizontal-align: right;
	text-align: right;
}

.k4-accent
{
	color: k4-accent;
	font-size: 15px;
	vertical-align: center;
}

.k4-meta
{
	flow-children: right;
	margin-top: 4px;
}

.k4-meta-icon
{
	width: 16px;
	height: 16px;
	margin-right: 7px;
	vertical-align: center;
	wash-color: k4-muted;
}

.k4-section
{
	width: 100%;
	flow-children: down;
	padding: 12px 14px;
	border-radius: 4px;
	border: 1px solid k4-line;
	background-color: k4-cell;
}

/* CS2's own progress bar colors (csgostyles .ProgressBarLeft) */
.k4-bar
{
	width: 100%;
	height: 6px;
	border-radius: 4px;
	background-color: #ffffff14;
}

.k4-bar-large
{
	height: 10px;
	margin: 2px 0px 0px 0px;
}

.k4-bar-short
{
	width: fill-parent-flow(1.0);
	vertical-align: center;
	margin-right: 12px;
}

.k4-bar-fill
{
	height: 100%;
	border-radius: 4px;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #134f87 ), to( #1ab8e0 ) );
}

/* ---------------------------------------------------------------- rank */

.k4-hero
{
	width: 100%;
	flow-children: right;
	margin-bottom: 14px;
}

.k4-hero-emblem
{
	width: 168px;
	height: 72px;
	border-radius: 4px;
	border: 1px solid k4-line;
	background-color: gradient( linear, 0% 0%, 0% 100%, from( #ffffff0d ), to( #ffffff04 ) );
}

.k4-rank-icon
{
	width: 144px;
	height: 58px;
	horizontal-align: center;
	vertical-align: center;
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
}

.k4-badge
{
	horizontal-align: center;
	vertical-align: center;
	font-size: 22px;
	text-align: center;
	color: k4-text;
}

.k4-hero-text
{
	flow-children: down;
	margin-left: 16px;
	vertical-align: center;
}

.k4-eyebrow
{
	color: k4-muted;
	font-size: 13px;
	letter-spacing: 1px;
}

.k4-rank-name
{
	font-size: 28px;
	color: k4-text;
}

.k4-next
{
	color: k4-base;
	font-size: 15px;
	vertical-align: center;
}

.k4-reward
{
	margin-top: 10px;
}

.k4-reward .k4-meta-icon
{
	wash-color: k4-accent;
}

/* The author line at the bottom right of the panel */
.k4-copyright
{
	horizontal-align: right;
	margin: 6px 18px 0px 0px;
	font-size: 12px;
	color: k4-muted;
}

/* ---------------------------------------------------------------- missions: CS2 mission tiles */

.k4-card
{
	width: 100%;
	flow-children: right;
	margin-bottom: 8px;
	border-radius: 4px;
	border: 1px solid k4-line;
	background-color: k4-cell;
	transition-property: wash-color;
	transition-duration: 0.5s;
}

.k4-card-stripe
{
	width: 3px;
	height: 100%;
	background-color: k4-muted;
	border-radius: 4px 0px 0px 4px;
}

.k4-card.k4-easy .k4-card-stripe { background-color: k4-green; }
.k4-card.k4-medium .k4-card-stripe { background-color: k4-gold; }
.k4-card.k4-hard .k4-card-stripe { background-color: k4-red; }
.k4-card.k4-weekly .k4-card-stripe { background-color: #B054E8; }
.k4-card.k4-ach .k4-card-stripe { background-color: k4-gold; }

.k4-mission-icon
{
	width: 56px;
	height: 32px;
	margin: 0px 6px 0px 12px;
	vertical-align: center;
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
	wash-color: k4-base;
}

.k4-card-body
{
	width: fill-parent-flow(1.0);
	flow-children: down;
	padding: 10px 12px 10px 8px;
}

.k4-diff
{
	font-size: 12px;
	padding: 2px 7px;
	margin-right: 10px;
	border-radius: 3px;
	vertical-align: center;
	color: k4-muted;
	background-color: k4-cell-light;
}

.k4-card.k4-easy .k4-diff { color: k4-green; background-color: #4cff6a1f; }
.k4-card.k4-medium .k4-diff { color: k4-gold; background-color: #e4ae391f; }
.k4-card.k4-hard .k4-diff { color: #FF6B5E; background-color: #db443726; }
.k4-card.k4-weekly .k4-diff { color: #C99BFF; background-color: #b054e826; }

/* A started mission the current map cannot offer: it waits for another map */
.k4-card.k4-blocked
{
	opacity: 0.55;
}

.k4-mission-desc
{
	white-space: nowrap;
	color: k4-text;
	font-size: 16px;
	vertical-align: center;
	width: fill-parent-flow(1.0);
	text-overflow: ellipsis;
}

.k4-progress
{
	color: k4-base;
	font-size: 14px;
	vertical-align: center;
	min-width: 150px; /* "1,227/10,000 · +2,500" fits, so every bar is the same length */
	text-align: right;
}

/* A finished mission: CS2's mission-complete green and check mark, and its sound */
.k4-card.k4-done
{
	background-color: #4cff6a12;
	border: 1px solid #4cff6a40;
}

.k4-card.k4-done .k4-mission-icon
{
	background-image: url("s2r://panorama/images/icons/ui/check.vsvg");
	wash-color: k4-green;
}

.k4-card.k4-done .k4-bar-fill
{
	background-color: k4-green;
}

.k4-card.k4-done .k4-progress
{
	color: k4-green;
}

.k4-small-btn
{
	height: 26px;
	padding: 0px 10px;
	margin-left: 10px;
	flow-children: right;
	vertical-align: center;
	border-radius: 3px;
	background-color: #ffffff0f;
	transition-property: background-color;
	transition-duration: k4-dur;
}

.k4-btn-icon
{
	width: 14px;
	height: 14px;
	margin-right: 6px;
	vertical-align: center;
	wash-color: k4-base;
}

.k4-small-btn Label
{
	color: k4-base;
	font-size: 13px;
	vertical-align: center;
}

.k4-small-btn:hover
{
	background-color: #ffffff24;
	sound: "UIPanorama.generic_button_rollover";
}

/* The main choice of a pair, such as Accept next to Decline */
.k4-btn-primary
{
	background-color: #82d8ff26;
}

.k4-btn-primary Label
{
	color: k4-accent;
}

.k4-btn-primary:hover
{
	background-color: #82d8ff40;
}

.k4-small-btn:active
{
	sound: "UIPanorama.generic_button_press";
}

.k4-footer
{
	flow-children: right;
	margin-top: 6px;
}

.k4-footer .k4-meta-icon
{
	wash-color: none;
}

/* ---------------------------------------------------------------- top and ranks: CS2 scoreboard rows */

.k4-table
{
	width: 100%;
	flow-children: down;
}

.k4-row
{
	width: 100%;
	height: 34px;
	flow-children: right;
	margin-bottom: 2px;
	border-radius: 4px;
	border: 1px solid k4-line;
	background-color: k4-cell;
}

.k4-row-alt
{
	background-color: k4-cell-dark;
}

.k4-row Label
{
	vertical-align: center;
}

.k4-row-me
{
	margin-top: 6px;
	background-color: rgba(0, 166, 255, 0.10);
	border-left: 3px solid k4-accent;
}

.k4-row.k4-current
{
	background-color: rgba(0, 166, 255, 0.10);
}

.k4-pos-cell
{
	width: 52px;
	height: 100%;
	border-radius: 4px 0px 0px 4px;
	background-color: k4-cell-dark;
	background-size: 44px 22px;
	background-repeat: no-repeat;
	background-position: center;
}

.k4-pos
{
	color: k4-base;
	font-size: 15px;
	horizontal-align: center;
	text-align: center;
}

/* Top three: CS2's laurels around the position */
.k4-pos-cell.k4-place-1 { background-image: url("s2r://panorama/images/icons/ui/laurel_gold.vsvg"); }
.k4-pos-cell.k4-place-2 { background-image: url("s2r://panorama/images/icons/ui/laurel_silver.vsvg"); }
.k4-pos-cell.k4-place-3 { background-image: url("s2r://panorama/images/icons/ui/laurel_bronze.vsvg"); }
.k4-pos-cell.k4-place-1 .k4-pos { color: #F4D27A; }
.k4-pos-cell.k4-place-2 .k4-pos { color: #D6DCE4; }
.k4-pos-cell.k4-place-3 .k4-pos { color: #D9A27A; }

.k4-row-icon
{
	width: 60px;
	height: 24px;
	margin: 0px 10px;
	vertical-align: center;
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
}

.k4-name
{
	white-space: nowrap;
	color: k4-text;
	font-size: 16px;
	width: fill-parent-flow(1.0);
	text-overflow: ellipsis;
}

.k4-row-info
{
	color: k4-muted;
	font-size: 14px;
	margin: 0px 12px;
	text-align: right;
}

.k4-pager
{
	width: 100%;
	flow-children: right;
	margin-top: 12px;
}

.k4-page
{
	width: fill-parent-flow(1.0);
	text-align: center;
}

/* Valve's pagination arrow (csgostyles .PaginationArrow) */
.k4-arrow
{
	width: 28px;
	height: 28px;
	border-radius: 3px;
	background-color: #ffffff0f;
	background-image: url("s2r://panorama/images/icons/ui/expand.vsvg");
	background-repeat: no-repeat;
	background-size: 18px 18px;
	background-position: 50% 50%;
	wash-color: k4-base;
	transition-property: background-color, opacity;
	transition-duration: k4-dur;
}

.k4-arrow-left { transform: rotateZ( 90deg ); }
.k4-arrow-right { transform: rotateZ( -90deg ); }

.k4-arrow:hover
{
	background-color: #ffffff24;
	wash-color: k4-text;
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

/* ---------------------------------------------------------------- stats */

.k4-tiles
{
	width: 100%;
	flow-children: right-wrap;
}

.k4-tile
{
	width: 190px;
	height: 64px;
	flow-children: right;
	margin: 0px 8px 8px 0px;
	border-radius: 4px;
	border: 1px solid k4-line;
	background-color: k4-cell;
}

.k4-tile-icon
{
	width: 30px;
	height: 30px;
	margin: 0px 10px 0px 14px;
	vertical-align: center;
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
	wash-color: k4-accent;
}

.k4-tile-text
{
	flow-children: down;
	vertical-align: center;
}

.k4-tile-label
{
	color: k4-muted;
	font-size: 12px;
	letter-spacing: 1px;
}

.k4-tile-value
{
	color: k4-text;
	font-size: 26px;
}

/* ---------------------------------------------------------------- toast: the CS2 HUD mission panel */

.k4-toast
{
	/* Right side, vertically centred: the kill feed owns the top right corner, the weapon list the
	   bottom right. */
	width: 420px;
	height: fit-children;
	horizontal-align: right;
	vertical-align: center;
	margin: 0px;
	opacity: 1;
	transform: translateX(0px);
	transition-property: opacity, transform;
	transition-duration: 0.2s;
	transition-timing-function: ease-in-out;
}

.k4-toast.k4-gone
{
	visibility: visible;
	opacity: 0;
	transform: translateX(200px);
}

.k4-toast-bg
{
	width: 100%;
	height: 100%;
	background-color: gradient( linear, 100% 0%, 0% 0%, from( #000000ED ), color-stop( 0.8, #00000020 ), to( #00000000 ) );
}

.k4-toast-line
{
	width: 100%;
	height: 2px;
	background-color: gradient( linear, 0% 0%, 100% 0%, from( #00000000 ), to( k4-green ) );
}

.k4-toast-line-top { vertical-align: top; }
.k4-toast-line-bottom { vertical-align: bottom; }

.k4-toast-content
{
	width: 100%;
	flow-children: right;
	padding: 14px 24px 14px 60px;
}

.k4-toast-icon
{
	width: 64px;
	height: 40px;
	vertical-align: center;
	background-size: contain;
	background-repeat: no-repeat;
	background-position: center;
}

.k4-toast-body
{
	flow-children: down;
	margin-left: 14px;
	width: fill-parent-flow(1.0);
	vertical-align: center;
}

.k4-toast-title
{
	color: k4-green;
	font-size: 15px;
	letter-spacing: 1px;
}

.k4-toast-text
{
	color: k4-text;
	font-size: 18px;
	margin-top: 2px;
}

.k4-toast.k4-toast-gold .k4-toast-title { color: k4-gold; }
.k4-toast.k4-toast-red .k4-toast-title { color: #FF6B5E; }
.k4-toast.k4-toast-gold .k4-toast-line { background-color: gradient( linear, 0% 0%, 100% 0%, from( #00000000 ), to( k4-gold ) ); }
.k4-toast.k4-toast-red .k4-toast-line { background-color: gradient( linear, 0% 0%, 100% 0%, from( #00000000 ), to( k4-red ) ); }

/* Achievements (6.5): the name in gold, like CS2's mission titles */
.k4-ach-name { color: k4-gold; font-size: 14px; margin-right: 8px; vertical-align: center; }

/* Points event banner under the header (6.5) */
.k4-event
{
	width: 100%;
	margin: 0px 0px 6px 0px;
	padding: 5px 18px;
	color: k4-gold;
	font-size: 14px;
	background-color: #e4ae391f;
	border-left: 3px solid k4-gold;
}

/* Bounty and duels on the Rank tab */
.k4-showdown { margin-top: 8px; }
.k4-wanted { color: #EB4B4B; font-size: 15px; vertical-align: center; border-left: 3px solid #EB4B4B; padding-left: 8px; }

/* CS2's own sounds when a toast appears */
.k4-toast.k4-toast-green:not(.k4-gone) { sound: "UI.Mission.Complete"; }
.k4-toast.k4-toast-gold:not(.k4-gone) { sound: "UIPanorama.EOM.CardReveal"; }

/* ---------------------------------------------------------------- generated */
"""

css += "\n/* Game icons */\n"
for name in UI_ICONS:
    css += f'.ic-{name} {{ background-image: url("s2r://panorama/images/icons/ui/{name}.vsvg"); }}\n'
for name in EQUIPMENT_ICONS:
    css += f'.eq-{name} {{ background-image: url("s2r://panorama/images/icons/equipment/{name}.vsvg"); }}\n'
for name in DEATHNOTICE_ICONS:
    css += f'.dn-{name} {{ background-image: url("s2r://panorama/images/hud/deathnotice/{name}.vsvg"); }}\n'

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

open(OUT, 'w', encoding='utf-8', newline='\n').write(css)
print("ok")
