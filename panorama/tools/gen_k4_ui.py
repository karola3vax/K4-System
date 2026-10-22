"""Writes the K4 Panorama layout. Repeated rows are generated so ids stay consistent with the server code.

The look follows CS2's own in-game panels (scoreboard frame, main-menu tabs, HUD mission panel) and uses
images that already ship with the game; the addon adds only sharp copies of CS2's rank emblems.
"""
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "layout", "custom_game", "k4_ui.xml")

TOP_ROWS = 10
RANK_ROWS = 10
STAT_TILES = 9
MISSIONS = 4  # three daily missions and the weekly one

# Tab id -> CS2 icon (panorama/images/icons/ui/<name>.vsvg)
TABS = [("rank", "xp_rank"), ("missions", "quest"), ("top", "leader"), ("ranks", "trophy"), ("stats", "stats")]


def icon(name, css):
    """A static game icon, drawn by the stylesheet (ic-<name>).

    CS2 validates server-driven HUD layouts: only Panel {id, class, hittest}, Label {id, class, hittest,
    text} and Button {id, class} pass (the layout does not use Image). Any other element or attribute,
    hittest on a Button included, makes the client reject the whole layout.
    """
    return f'<Panel class="{css} ic-{name}" />'


def bar(panel_id, extra=""):
    return f'<Panel class="k4-bar{extra}"><Panel id="{panel_id}" class="k4-bar-fill w0" /></Panel>'


lines = []


def w(depth, text):
    lines.append("\t" * depth + text)


w(0, '<root>')
w(1, '<styles>')
w(2, '<include src="s2r://panorama/styles/csgostyles.vcss_c" />')
w(2, '<include src="s2r://panorama/styles/custom_game/k4_ui.vcss_c" />')
w(1, '</styles>')
# The full-screen root and the toast let the mouse through (hittest="false"). The root sits above
# every other HUD (z-index), so without it it took the hover from other plugins' mouse menus: their
# :hover styles and hover sounds never fired while K4 ran (clicks still worked).
w(1, '<Panel class="k4-root" hittest="false">')

# ---------------------------------------------------------------- toast (CS2 HUD mission panel style)
w(2, '<Panel id="k4-toast" class="k4-toast k4-toast-green k4-gone" hittest="false">')
w(3, '<Panel class="k4-toast-bg" hittest="false" />')
w(3, '<Panel class="k4-toast-line k4-toast-line-top" hittest="false" />')
w(3, '<Panel class="k4-toast-line k4-toast-line-bottom" hittest="false" />')
w(3, '<Panel class="k4-toast-content" hittest="false">')
w(4, '<Panel id="k4-toast-icon" class="k4-toast-icon ic-check" hittest="false" />')
w(4, '<Panel class="k4-toast-body" hittest="false">')
w(5, '<Label class="k4-toast-title stratum-bold-tf" text="{s:title}" hittest="false" />')
w(5, '<Label class="k4-toast-text stratum-regular-tf" text="{s:text}" hittest="false" />')
w(4, '</Panel>')
w(3, '</Panel>')
w(2, '</Panel>')

# ---------------------------------------------------------------- main panel (CS2 scoreboard frame)
w(2, '<Panel id="k4-panel" class="k4-panel k4-closed">')
w(3, '<Panel class="k4-bg" />')
w(3, '<Panel class="k4-content">')

# Header
w(4, '<Panel class="k4-header">')
w(5, icon("xp_rank", "k4-logo"))
w(5, '<Label class="k4-brand stratum-bold-tf" text="K4" />')
w(5, '<Label id="k4-subtitle" class="k4-subtitle stratum-regular-tf" text="{s:subtitle}" />')
w(5, '<Button id="k4-close" class="CloseButton k4-close" />')
w(4, '</Panel>')

# Tabs (CS2 main-menu navbar)
w(4, '<Panel class="k4-tabs">')
for tab, name in TABS:
    w(5, f'<Button id="k4-tab-{tab}" class="k4-tab">')
    w(6, icon(name, "k4-tab-icon"))
    w(6, '<Label class="stratum-medium-tf" text="{s:label}" />')
    w(5, '</Button>')
w(4, '</Panel>')

# Rank view
w(4, '<Panel id="k4-view-rank" class="k4-view">')
w(5, '<Panel class="k4-hero">')
w(6, '<Panel class="k4-hero-emblem">')
w(7, '<Panel id="k4-rank-icon" class="k4-rank-icon sg-0" />')
w(7, '<Label id="k4-rank-badge" class="k4-badge k4-gone stratum-bold-tf" text="{s:tag}" />')
w(6, '</Panel>')
w(6, '<Panel class="k4-hero-text">')
w(7, '<Label class="k4-eyebrow stratum-medium-tf" text="{s:caption}" />')
w(7, '<Label id="k4-rank-name" class="k4-rank-name stratum-bold-tf" text="{s:rank}" />')
w(7, '<Panel class="k4-meta">')
w(8, icon("leader", "k4-meta-icon"))
w(8, '<Label class="k4-muted stratum-regular-tf" text="{s:summary}" />')
w(7, '</Panel>')
w(6, '</Panel>')
w(5, '</Panel>')
w(5, '<Panel class="k4-section">')
w(6, '<Panel class="k4-split">')
w(7, '<Label class="k4-next k4-fill stratum-medium-tf" text="{s:next}" />')
w(7, '<Label class="k4-muted k4-right stratum-regular-tf" text="{s:togo}" />')
w(6, '</Panel>')
w(6, bar("k4-rank-bar", " k4-bar-large"))
w(6, '<Panel id="k4-rank-reward" class="k4-meta k4-reward">')
w(7, icon("gift", "k4-meta-icon"))
w(7, '<Label class="k4-accent stratum-regular-tf" text="{s:reward}" />')
w(6, '</Panel>')
w(5, '</Panel>')
w(4, '</Panel>')

# Missions view (CS2 mission tiles)
w(4, '<Panel id="k4-view-missions" class="k4-view">')
for i in range(MISSIONS):
    # The weekly card starts hidden: K4 6.2.x never fills it, and 6.3+ shows it when there is a weekly mission.
    w(5, f'<Panel id="k4-m{i}" class="k4-card{" k4-gone" if i >= 3 else ""}">')
    w(6, '<Panel class="k4-card-stripe" />')
    w(6, f'<Panel id="k4-m{i}-icon" class="k4-mission-icon" />')
    w(6, '<Panel class="k4-card-body">')
    w(7, '<Panel class="k4-split">')
    w(8, f'<Label id="k4-m{i}-diff" class="k4-diff stratum-bold-tf" text="{{s:diff}}" />')
    w(8, '<Label class="k4-mission-desc stratum-medium-tf" text="{s:desc}" />')
    w(7, '</Panel>')
    w(7, '<Panel class="k4-split k4-split-last">')
    w(8, bar(f"k4-m{i}-bar", " k4-bar-short"))
    w(8, '<Label class="k4-progress stratum-medium-tf" text="{s:progress}" />')
    w(8, f'<Button id="k4-reroll-{i}" class="k4-small-btn k4-gone">')
    w(9, icon("refresh_skinny", "k4-btn-icon"))
    w(9, '<Label class="stratum-medium-tf" text="{s:reroll}" />')
    w(8, '</Button>')
    w(7, '</Panel>')
    w(6, '</Panel>')
    w(5, '</Panel>')
w(5, '<Panel class="k4-footer">')
w(6, icon("hot_trend", "k4-meta-icon"))
w(6, '<Label class="k4-muted stratum-regular-tf" text="{s:footer}" />')
w(5, '</Panel>')
w(4, '</Panel>')


def row(row_id, depth, name_id=None, position=True, css="k4-row"):
    w(depth, f'<Panel id="{row_id}" class="{css}">')
    if position:
        w(depth + 1, f'<Panel id="{row_id}-pos" class="k4-pos-cell">')
        w(depth + 2, '<Label class="k4-pos stratum-bold-tf" text="{s:pos}" />')
        w(depth + 1, '</Panel>')
    w(depth + 1, f'<Panel id="{row_id}-icon" class="k4-row-icon sg-0" />')
    name_attr = f' id="{name_id}"' if name_id else ""
    w(depth + 1, f'<Label{name_attr} class="k4-name stratum-medium-tf" text="{{s:name}}" />')
    w(depth + 1, '<Label class="k4-row-info stratum-regular-tf" text="{s:info}" />')
    w(depth, '</Panel>')


def pager(prefix, page_id):
    w(5, '<Panel class="k4-pager">')
    w(6, f'<Button id="{prefix}-prev" class="k4-arrow k4-arrow-left" />')
    w(6, f'<Label id="{page_id}" class="k4-muted k4-page stratum-medium-tf" text="{{s:page}}" />')
    w(6, f'<Button id="{prefix}-next" class="k4-arrow k4-arrow-right" />')
    w(5, '</Panel>')


# Top view (CS2 scoreboard rows)
w(4, '<Panel id="k4-view-top" class="k4-view">')
w(5, '<Panel class="k4-table">')
for i in range(TOP_ROWS):
    row(f"k4-top-{i}", 6)
w(5, '</Panel>')
row("k4-top-me", 5, css="k4-row k4-row-me")
pager("k4-top", "k4-top-page")
w(4, '</Panel>')

# Ranks view
w(4, '<Panel id="k4-view-ranks" class="k4-view">')
w(5, '<Panel class="k4-table">')
for i in range(RANK_ROWS):
    row(f"k4-rk-{i}", 6, name_id=f"k4-rk-{i}-name", position=False)
w(5, '</Panel>')
pager("k4-rk", "k4-rk-page")
w(4, '</Panel>')

# Stats view
w(4, '<Panel id="k4-view-stats" class="k4-view">')
w(5, '<Panel class="k4-tiles">')
for i in range(STAT_TILES):
    w(6, f'<Panel id="k4-st-{i}" class="k4-tile">')
    w(7, f'<Panel id="k4-st-{i}-icon" class="k4-tile-icon" />')
    w(7, '<Panel class="k4-tile-text">')
    w(8, '<Label class="k4-tile-label stratum-medium-tf" text="{s:label}" />')
    w(8, '<Label class="k4-tile-value stratum-bold-tf" text="{s:value}" />')
    w(7, '</Panel>')
    w(6, '</Panel>')
w(5, '</Panel>')
w(4, '</Panel>')

w(4, '<Label class="k4-copyright stratum-regular-tf" hittest="false" text="© 2026 karola3vax" />')
w(3, '</Panel>')
w(2, '</Panel>')
w(1, '</Panel>')
w(0, '</root>')

open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
print(f"wrote {len(lines)} lines")
