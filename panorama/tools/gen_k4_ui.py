"""Writes the K4 Panorama layout (7.1): a full-screen panel in the style of CS2's main menu, and a toast stack.

Every id, dialog variable and class the server uses is listed in docs/panel-contract.md; this script and
PanoramaLayoutTests follow it. Repeated parts are generated so their ids stay consistent.

CS2 validates server-driven HUD layouts: only Panel {id, class, hittest}, Label {id, class, hittest, text}
and Button {id, class} pass. Any other element or attribute, hittest on a Button included, makes the client
reject the whole layout. Pictures are therefore CSS background-image classes on Panels.
"""
import contextlib
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "layout", "custom_game", "k4_ui.xml")

# Nav id -> CS2 icon (panorama/images/icons/ui/<name>.vsvg)
NAV = [("home", "home"), ("profile", "profile"), ("missions", "quest"), ("achievements", "winner_ribbon"),
       ("leaderboard", "leader"), ("ranks", "trophy"), ("stats", "stats")]

HOME_MISSIONS = 4
HOME_ACHIEVEMENTS = 3
HOME_QUICK = 5
SHOWCASE = 3
PROFILE_STATS = 6
PROFILE_SEASONS = 3
MISSIONS = 3
STREAK_DAYS = 7
ACH_COLUMNS, ACH_ROWS = 5, 3
LB_ROWS = 7
RANK_COLUMNS, RANK_ROWS = 5, 4
STAT_TILES = 8
STAT_WEAPONS = 3
TOASTS = 3

REGULAR, MEDIUM, BOLD = "stratum-regular-tf", "stratum-medium-tf", "stratum-bold-tf"
GONE = "k4-gone"

lines = []
depth = 0


def tag(name, id=None, cls=None, hittest=None, text=None):
    out = name
    if id:
        out += f' id="{id}"'
    if cls:
        out += f' class="{cls}"'
    if hittest is not None:
        out += f' hittest="{hittest}"'
    if text is not None:
        out += f' text="{text}"'
    return out


def leaf(name, **attrs):
    lines.append("\t" * depth + f"<{tag(name, **attrs)} />")


@contextlib.contextmanager
def box(name="Panel", **attrs):
    global depth
    lines.append("\t" * depth + f"<{tag(name, **attrs)}>")
    depth += 1
    yield
    depth -= 1
    lines.append("\t" * depth + f"</{name}>")


def panel(cls, id=None, **attrs):
    leaf("Panel", id=id, cls=cls, **attrs)


def label(text, cls, font=MEDIUM, id=None, **attrs):
    leaf("Label", id=id, cls=f"{cls} {font}", text=text, **attrs)


def bar(fill_id, cls="", **attrs):
    with box(cls=f"k4-bar {cls}".strip(), **attrs):
        panel("k4-bar-fill w0", id=fill_id, **attrs)


def medal(ring_id, icon_id, cls, **attrs):
    """A medal: tier ring (t-gold, t-silver, t-bronze, t-locked) around a dark disc with a white icon."""
    with box(id=ring_id, cls=f"k4-ring {cls} t-locked", **attrs):
        with box(cls="k4-disc", **attrs):
            panel("k4-medal-icon", id=icon_id, **attrs)


def first(i):
    # Grid cells are spaced by a left margin; the first cell of a row has none (Panorama has no :first-child).
    return " k4-first" if i == 0 else ""


def grow():
    panel("k4-grow")


def vgrow():
    panel("k4-vgrow")


def header_space():
    # The close button and the event badge float over every view's top right corner (k4-topbar), so view
    # headers keep their own items on the left.
    panel("k4-header-space")


def pager(prev_id, next_id, cls=""):
    with box(cls=f"k4-pager {cls}".strip()):
        leaf("Button", id=prev_id, cls="k4-arrow k4-arrow-left")
        label("{s:page}", "k4-page", MEDIUM)
        leaf("Button", id=next_id, cls="k4-arrow k4-arrow-right")


with box("root"):
    with box("styles"):
        for src in ("s2r://panorama/styles/csgostyles.vcss_c", "s2r://panorama/styles/custom_game/k4_ui.vcss_c"):
            lines.append("\t" * depth + f'<include src="{src}" />')
    # The full-screen root, the toast stack and the banner let the mouse through (hittest="false"): the root
    # sits above every other HUD, and without it it took the hover from other plugins' mouse menus.
    with box(cls="k4-root", hittest="false"):

        # ------------------------------------------------------------ panel
        with box(id="k4-panel", cls="k4-panel k4-closed"):
            with box(id="k4-bg", cls="k4-bg map-default"):
                panel("k4-bg-map", id="k4-bg-map")
            panel("k4-shade")
            with box(cls="k4-frame"):

                # Sidebar
                with box(cls="k4-sidebar"):
                    with box(cls="k4-brand"):
                        label("K4", "k4-brand-name", BOLD)
                        label("SYSTEM", "k4-brand-sub", BOLD)
                    with box("Button", id="k4-me", cls="k4-me"):
                        panel("k4-me-icon sg-0", id="k4-me-icon")
                        with box(cls="k4-me-text"):
                            label("{s:name}", "k4-me-name", BOLD)
                            label("{s:meta}", "k4-me-meta", MEDIUM)
                    for nav, icon in NAV:
                        with box("Button", id=f"k4-nav-{nav}", cls="k4-nav"):
                            panel(f"k4-nav-icon ic-{icon}")
                            label("{s:label}", "k4-nav-label", MEDIUM)
                    vgrow()
                    with box(id="k4-season", cls=f"k4-season {GONE}"):
                        label("{s:title}", "k4-season-title", BOLD)
                        label("{s:left}", "k4-season-left", BOLD)
                        bar("k4-season-bar", "k4-bar-season")
                    with box(cls="k4-copyright-box"):
                        label("© 2026 karola3vax", "k4-copyright", BOLD)

                with box(cls="k4-main"):

                    # Home
                    with box(id="k4-view-home", cls="k4-view k4-view-pad"):
                        header_space()
                        with box(cls="k4-h-hero"):
                            panel("k4-h-rank-icon sg-0", id="k4-h-rank-icon")
                            with box(cls="k4-h-hero-text"):
                                label("{s:rank}", "k4-h-rank-name", BOLD, id="k4-h-rank-name")
                                with box(cls="k4-h-points-row"):
                                    label("{s:points}", "k4-h-points", BOLD)
                                    label("{s:place}", "k4-place", BOLD)
                                bar("k4-h-bar", "k4-bar-hero")
                                label("{s:next}", "k4-h-next", MEDIUM)
                        with box(cls="k4-h-cards"):
                            with box("Button", id="k4-h-missions", cls="k4-tile k4-h-card k4-first"):
                                panel("k4-cap-icon ic-quest")
                                with box(cls="k4-h-slots"):
                                    for i in range(HOME_MISSIONS):
                                        with box(id=f"k4-h-m{i}", cls=f"k4-h-m{first(i)} {GONE}"):
                                            panel("k4-h-m-icon", id=f"k4-h-m{i}-icon")
                                            label("{s:value}", "k4-h-m-value", BOLD)
                            with box("Button", id="k4-h-ach", cls="k4-tile k4-h-card"):
                                panel("k4-cap-icon ic-winner_ribbon")
                                with box(cls="k4-h-slots"):
                                    for i in range(HOME_ACHIEVEMENTS):
                                        with box(id=f"k4-h-a{i}", cls=f"k4-h-a{first(i)} {GONE}"):
                                            medal(f"k4-h-a{i}-ring", f"k4-h-a{i}-icon", "k4-ring-home")
                                            label("{s:name}", "k4-h-a-name", BOLD)
                                            bar(f"k4-h-a{i}-bar", "k4-bar-small")
                            with box("Button", id="k4-h-top", cls="k4-tile k4-h-card"):
                                panel("k4-cap-icon ic-leader")
                                with box(cls="k4-h-slots"):
                                    # Left to right: 2nd, 1st, 3rd, like a podium.
                                    for i, place in enumerate((2, 1, 3)):
                                        with box(id=f"k4-h-p{i}", cls=f"k4-h-p k4-place-{place}{first(i)} {GONE}"):
                                            panel("k4-h-p-icon sg-0", id=f"k4-h-p{i}-icon")
                                            label("{s:name}", "k4-h-p-name", BOLD)
                                            with box(cls="k4-step"):
                                                label("{s:pos}", "k4-step-pos", BOLD)
                        with box(cls="k4-h-quick"):
                            for i in range(HOME_QUICK):
                                with box("Button", id=f"k4-h-q{i}", cls=f"k4-tile k4-h-q{' k4-h-q-wide' if i == HOME_QUICK - 1 else ''}{first(i)} {GONE}"):
                                    with box(cls="k4-h-q-inner"):
                                        panel("k4-h-q-icon", id=f"k4-h-q{i}-icon")
                                        label("{s:value}", "k4-tile-value k4-h-q-value", BOLD)

                    # Profile
                    with box(id="k4-view-profile", cls="k4-view k4-view-pad"):
                        with box(cls="k4-pf-head"):
                            with box("Button", id="k4-pf-back", cls=f"k4-btn {GONE}"):
                                panel("k4-btn-icon k4-icon-back ic-expand")
                                label("{s:back}", "k4-btn-label", BOLD)
                            for toggle in ("toasts", "chat"):
                                with box("Button", id=f"k4-pf-{toggle}", cls=f"k4-btn k4-toggle {GONE}"):
                                    panel("k4-toggle-dot")
                                    label("{s:label}", "k4-btn-label", BOLD)
                        with box(cls="k4-pf-banner"):
                            with box(cls="k4-pf-emblem"):
                                panel("k4-pf-icon sg-0", id="k4-pf-icon")
                                label("{s:tag}", f"k4-pf-badge {GONE}", BOLD, id="k4-pf-badge")
                            with box(cls="k4-pf-text"):
                                label("{s:name}", "k4-pf-name", BOLD)
                                with box(cls="k4-pf-line"):
                                    label("{s:rank}", "k4-pf-rank", BOLD, id="k4-pf-rank")
                                    label("{s:points}", "k4-pf-points", BOLD)
                                    label("{s:place}", "k4-place", BOLD)
                            with box(cls="k4-pf-showcase"):
                                with box(cls="k4-pf-sc-row"):
                                    for i in range(SHOWCASE):
                                        with box(id=f"k4-pf-sc{i}", cls=f"k4-pf-sc{first(i)} {GONE}"):
                                            medal(f"k4-pf-sc{i}-ring", f"k4-pf-sc{i}-icon", "k4-ring-showcase")
                                            label("{s:name}", "k4-pf-sc-name", BOLD)
                                label("{s:hint}", f"k4-pf-sc-hint {GONE}", MEDIUM, id="k4-pf-sc-hint")
                        with box(cls="k4-pf-stats"):
                            for i in range(PROFILE_STATS):
                                with box(id=f"k4-pf-st{i}", cls=f"k4-tile k4-pf-st{first(i)}"):
                                    panel("k4-pf-st-icon", id=f"k4-pf-st{i}-icon")
                                    label("{s:value}", "k4-tile-value k4-pf-st-value", BOLD)
                                    label("{s:label}", "k4-cap k4-pf-st-label", BOLD)
                        with box(cls="k4-pf-bottom"):
                            with box(id="k4-pf-fav", cls=f"k4-tile k4-pf-fav {GONE}"):
                                panel("k4-pf-fav-icon", id="k4-pf-fav-icon")
                                with box(cls="k4-pf-fav-line"):
                                    label("{s:fav}", "k4-pf-fav-name", BOLD)
                                    label("{s:favkills}", "k4-pf-fav-kills", MEDIUM)
                            with box(cls="k4-pf-seasons"):
                                for i in range(PROFILE_SEASONS):
                                    with box(id=f"k4-pf-ss{i}", cls=f"k4-tile k4-pf-ss{first(i)} {GONE}"):
                                        label("{s:season}", "k4-cap k4-pf-ss-season", BOLD)
                                        panel("k4-pf-ss-icon sg-0", id=f"k4-pf-ss{i}-icon")
                                        label("{s:pos}", "k4-pf-ss-pos", BOLD)
                                label("{s:none}", f"k4-pf-ss-none {GONE}", MEDIUM, id="k4-pf-ss-none")

                    # Missions
                    with box(id="k4-view-missions", cls="k4-view k4-view-pad"):
                        with box(cls="k4-m-head"):
                            panel("k4-m-head-icon ic-clock")
                            label("{s:reset}", "k4-m-reset", BOLD)
                        with box(cls="k4-m-cards"):
                            for i in range(MISSIONS):
                                with box(id=f"k4-m{i}", cls=f"k4-m{first(i)} {GONE}"):
                                    with box(cls="k4-m-top"):
                                        label("{s:diff}", "k4-m-diff", BOLD)
                                        grow()
                                        label("{s:reward}", "k4-reward", BOLD)
                                    vgrow()
                                    panel("k4-m-icon", id=f"k4-m{i}-icon")
                                    label("{s:desc}", "k4-m-desc", MEDIUM)
                                    bar(f"k4-m{i}-bar", "k4-bar-mission")
                                    with box(cls="k4-m-bottom"):
                                        label("{s:progress}", "k4-m-progress", BOLD)
                                        grow()
                                        with box("Button", id=f"k4-reroll-{i}", cls=f"k4-btn {GONE}"):
                                            panel("k4-btn-icon ic-refresh_skinny")
                                            label("{s:label}", "k4-btn-label", BOLD)
                        with box(id="k4-mw", cls=f"k4-mw {GONE}"):
                            panel("k4-mw-icon", id="k4-mw-icon")
                            with box(cls="k4-mw-text"):
                                label("{s:left}", "k4-mw-left", BOLD)
                                label("{s:desc}", "k4-mw-desc", MEDIUM)
                                bar("k4-mw-bar", "k4-bar-mission")
                            with box(cls="k4-mw-side"):
                                label("{s:progress}", "k4-mw-progress", BOLD)
                                label("{s:reward}", "k4-reward k4-mw-reward", BOLD)
                        with box(cls="k4-streak"):
                            panel("k4-streak-icon ic-hot_trend")
                            label("{s:streak}", "k4-streak-count", BOLD)
                            for i in range(STREAK_DAYS):
                                with box(id=f"k4-sd{i}", cls="k4-sd"):
                                    panel("k4-sd-icon")
                            label("{s:bonus}", "k4-reward k4-streak-bonus", BOLD)

                    # Achievements
                    with box(id="k4-view-achievements", cls="k4-view k4-view-pad"):
                        with box(cls="k4-a-head"):
                            for name in ("all", "progress", "unlocked", "shown"):
                                with box("Button", id=f"k4-af-{name}", cls="k4-chip"):
                                    if name == "shown":
                                        panel("k4-chip-icon ic-icon_star")
                                    label("{s:label}", "k4-chip-label", BOLD)
                            label("{s:count}", "k4-a-count", BOLD)
                        with box(cls="k4-grid"):
                            for row in range(ACH_ROWS):
                                with box(cls=f"k4-grid-row{first(row)}"):
                                    for column in range(ACH_COLUMNS):
                                        i = row * ACH_COLUMNS + column
                                        with box("Button", id=f"k4-a{i}", cls=f"k4-a{first(column)} {GONE}"):
                                            with box(cls="k4-a-medal"):
                                                medal(f"k4-a{i}-ring", f"k4-a{i}-icon", "k4-ring-card")
                                                panel("k4-a-pin ic-icon_star")
                                            label("{s:name}", "k4-a-name", BOLD)
                                            label("{s:goal}", "k4-a-goal", MEDIUM)
                                            bar(f"k4-a{i}-bar", "k4-bar-card")
                        pager("k4-ach-prev", "k4-ach-next", "k4-pager-bottom")

                    # Leaderboard
                    with box(id="k4-view-leaderboard", cls="k4-view"):
                        with box(cls="k4-lb-stage"):
                            with box(cls="k4-lb-head"):
                                leaf("Button", id="k4-lb-newer", cls="k4-arrow k4-arrow-left")
                                with box(cls="k4-lb-season"):
                                    label("{s:season}", "k4-lb-season-name", BOLD)
                                    label("{s:status}", "k4-lb-status", BOLD)
                                leaf("Button", id="k4-lb-older", cls="k4-arrow k4-arrow-right")
                                pager("k4-lb-prev", "k4-lb-next")
                            with box(cls="k4-lb-podium"):
                                for i, place in enumerate((2, 1, 3)):
                                    with box("Button", id=f"k4-lb-p{i}", cls=f"k4-lb-p k4-place-{place}{first(i)} {GONE}"):
                                        panel("k4-lb-p-icon sg-0", id=f"k4-lb-p{i}-icon")
                                        label("{s:name}", "k4-lb-p-name", BOLD)
                                        label("{s:points}", "k4-lb-p-points", MEDIUM)
                                        with box(cls="k4-step"):
                                            label("{s:pos}", "k4-step-pos", BOLD)
                        with box(cls="k4-lb-list"):
                            for i in range(LB_ROWS):
                                with box("Button", id=f"k4-lb-{i}", cls=f"k4-lb-row{' k4-lb-alt' if i % 2 else ''} {GONE}"):
                                    label("{s:pos}", "k4-lb-pos", BOLD)
                                    panel("k4-lb-icon sg-0", id=f"k4-lb-{i}-icon")
                                    label("{s:name}", "k4-lb-name", MEDIUM)
                                    label("{s:points}", "k4-lb-points", BOLD)
                            vgrow()
                            with box(id="k4-lb-me", cls=f"k4-lb-row k4-lb-me {GONE}"):
                                label("{s:pos}", "k4-lb-pos", BOLD)
                                panel("k4-lb-icon sg-0", id="k4-lb-me-icon")
                                label("{s:name}", "k4-lb-name", BOLD)
                                label("{s:points}", "k4-lb-points", BOLD)

                    # Ranks
                    with box(id="k4-view-ranks", cls="k4-view k4-view-pad"):
                        with box(cls="k4-rk-head"):
                            with box(cls="k4-pager"):
                                leaf("Button", id="k4-rk-prev", cls=f"k4-arrow k4-arrow-left {GONE}")
                                label("{s:page}", "k4-page", MEDIUM)
                                leaf("Button", id="k4-rk-next", cls=f"k4-arrow k4-arrow-right {GONE}")
                        with box(cls="k4-grid"):
                            for row in range(RANK_ROWS):
                                with box(cls=f"k4-grid-row{first(row)}"):
                                    for column in range(RANK_COLUMNS):
                                        i = row * RANK_COLUMNS + column
                                        with box(id=f"k4-rk{i}", cls=f"k4-rk{first(column)} {GONE}"):
                                            with box(cls="k4-rk-body"):
                                                panel("k4-rk-icon sg-0", id=f"k4-rk{i}-icon")
                                                label("{s:name}", "k4-rk-name", BOLD, id=f"k4-rk{i}-name")
                                                label("{s:points}", "k4-rk-points", MEDIUM)
                                            panel(f"k4-rk-gift ic-gift {GONE}", id=f"k4-rk{i}-gift")
                                            with box(id=f"k4-rk{i}-you", cls=f"k4-rk-you {GONE}"):
                                                panel("k4-rk-you-icon ic-profile")

                    # Stats
                    with box(id="k4-view-stats", cls="k4-view k4-view-pad"):
                        header_space()
                        with box(cls="k4-st-tiles"):
                            for row in range(2):
                                with box(cls=f"k4-st-row{first(row)}"):
                                    for column in range(STAT_TILES // 2):
                                        i = row * (STAT_TILES // 2) + column
                                        with box(id=f"k4-st{i}", cls=f"k4-tile k4-st{first(column)} {GONE}"):
                                            panel("k4-st-icon", id=f"k4-st{i}-icon")
                                            with box(cls="k4-st-text"):
                                                label("{s:value}", "k4-tile-value k4-st-value", BOLD)
                                                label("{s:label}", "k4-cap", BOLD)
                        with box(cls="k4-st-bottom"):
                            with box(cls="k4-tile k4-st-weapons"):
                                with box(cls="k4-sw-list"):
                                    for i in range(STAT_WEAPONS):
                                        with box(id=f"k4-sw{i}", cls=f"k4-sw{first(i)} {GONE}"):
                                            panel("k4-sw-icon", id=f"k4-sw{i}-icon")
                                            bar(f"k4-sw{i}-bar", "k4-bar-weapon")
                                            label("{s:kills}", "k4-sw-kills", BOLD)
                            with box(id="k4-pt", cls=f"k4-tile k4-pt {GONE}"):
                                with box(cls="k4-pt-list"):
                                    with box(cls="k4-pt-head"):
                                        panel("k4-pt-head-icon ic-clock")
                                        label("{s:total}", "k4-tile-value k4-pt-total", BOLD)
                                    for team, picture in (("t", "tp-t"), ("ct", "tp-ct"), ("spec", "ic-watch")):
                                        with box(id=f"k4-pt-{team}", cls=f"k4-pt-team k4-pt-{team}"):
                                            panel(f"k4-pt-icon {picture}")
                                            with box(cls="k4-pt-text"):
                                                with box(cls="k4-pt-line"):
                                                    label("{s:name}", "k4-pt-name", BOLD)
                                                    grow()
                                                    label("{s:time}", "k4-pt-time", BOLD)
                                                bar(f"k4-pt-{team}-bar", "k4-bar-team")

                    # Close button and points event, over the top right corner of every view
                    with box(cls="k4-topbar"):
                        with box(id="k4-event", cls=f"k4-event {GONE}"):
                            panel("k4-event-icon ic-hot_trend")
                            label("{s:event}", "k4-event-text", BOLD)
                        with box("Button", id="k4-close", cls="k4-close"):
                            panel("k4-close-icon ic-cancel")

        # ------------------------------------------------------------ promotion banner
        with box(id="k4-promo", cls=f"k4-promo {GONE}", hittest="false"):
            label("{s:title}", "k4-promo-title", BOLD, hittest="false")
            panel("k4-promo-icon sg-0", id="k4-promo-icon", hittest="false")
            label("{s:rank}", "k4-promo-rank", BOLD, hittest="false")

        # ------------------------------------------------------------ toasts: under the kill feed, newest on top
        with box(id="k4-toasts", cls="k4-toasts", hittest="false"):
            for i in range(TOASTS):
                with box(id=f"k4-t{i}", cls=f"k4-toast k4-toast-{i} k4-tc-green k4-len-normal {GONE}", hittest="false"):
                    panel("k4-toast-strip", hittest="false")
                    with box(cls="k4-toast-row", hittest="false"):
                        with box(cls="k4-toast-pic", hittest="false"):
                            # The ring is behind the icon, not around it: a toast without a medal hides only the ring.
                            with box(id=f"k4-t{i}-ring", cls=f"k4-ring k4-ring-toast t-locked {GONE}", hittest="false"):
                                panel("k4-disc", hittest="false")
                            panel("k4-toast-icon", id=f"k4-t{i}-icon", hittest="false")
                        with box(cls="k4-toast-body", hittest="false"):
                            label("{s:kind}", "k4-toast-kind", BOLD, hittest="false")
                            label("{s:text}", "k4-toast-text", MEDIUM, hittest="false")
                            with box(id=f"k4-t{i}-prog", cls=f"k4-bar k4-bar-toast {GONE}", hittest="false"):
                                panel("k4-bar-fill w0", id=f"k4-t{i}-bar", hittest="false")
                        label("{s:value}", "k4-toast-value", BOLD, hittest="false")
                    panel("k4-toast-timer", id=f"k4-t{i}-timer", hittest="false")
            label("{s:more}", f"k4-t-more {GONE}", BOLD, id="k4-t-more", hittest="false")

open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
print(f"wrote {len(lines)} lines")
