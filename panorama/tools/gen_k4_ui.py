"""Writes the K4 Panorama layout (8.0): a centered 1240x760 window with tabs along the top, and the toast stack.

Every id, dialog variable and class the server uses is listed in docs/panel-contract.md (contract 8); this
script and PanoramaLayoutTests follow it. Repeated parts are generated so their ids stay consistent.

CS2 validates server-driven HUD layouts: only Panel {id, class, hittest}, Label {id, class, hittest, text},
Image {id, class, hittest, src} and Button {id, class} pass. Any other element or attribute, hittest on a
Button included, makes the client reject the whole layout. Pictures the server picks are CSS
background-image classes on Panels; the layout uses no Image at all today.

Window, 1240x760 (gen_k4_css.py has the sizes):
  top bar 56      brand, six tabs, event tag, gear, close
  crumbs 40       Back, where you are, season clock
  duel bar        a challenge waiting for you, on every screen (k4-gone otherwise)
  body            the seven screens, 1190x590 inside the padding
  footer 32       status line, out-of-date bar, key hint, copyright
"""
import contextlib
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "layout", "custom_game", "k4_ui.xml")

# Tab id -> CS2 icon (panorama/images/icons/ui/<name>.vsvg)
TABS = [("home", "home"), ("missions", "quest"), ("achievements", "winner_ribbon"), ("leaderboard", "leader"),
        ("duels", "casual"), ("profile", "profile")]

# Pools: the contract's table. PanoramaLayoutTests checks these counts in the XML.
LEADERBOARD_ROWS = 100
RANK_ROWS = 40
ACHIEVEMENT_ROWS = 64
DUEL_PLAYER_ROWS = 64
SEASON_ROWS = 24
LIVE_DUEL_ROWS = 4

HOME_MISSIONS = 4
HOME_STATS = 5
HOME_NEXT = 3
STREAK_DAYS = 7
MISSIONS = 3
SHOWCASE = 3
DUEL_TYPES = 6
DUEL_STAKES = 5
PROFILE_STATS = 8
PROFILE_WEAPONS = 3
TOASTS = 3

TITLE = "stratum-bold-tf"   # Stratum for titles and numbers; everything else is CS2's Noto (csgostyles Label)
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


def label(text, cls, title=False, id=None, **attrs):
    leaf("Label", id=id, cls=f"{cls} {TITLE}" if title else cls, text=text, **attrs)


def pic(cls, id=None, **attrs):
    """A panel that only draws a picture (a game icon, a rank emblem): its picture is a class."""
    panel(f"k4-pic {cls}", id=id, **attrs)


def bar(fill_id, cls="", **attrs):
    """A progress bar: the fill carries the id and the width class w0 .. w20."""
    with box(cls=f"k4-bar {cls}".strip(), **attrs):
        panel("k4-bar-fill w0", id=fill_id, **attrs)


def medal(ring_id, icon_id, size, **attrs):
    """A medal: tier ring (t-gold, t-silver, t-bronze, t-locked) around a dark disc with a white icon."""
    with box(id=ring_id, cls=f"k4-ring k4-ring-{size} t-locked", **attrs):
        with box(cls="k4-disc", **attrs):
            pic("k4-medal-icon", id=icon_id, **attrs)


def tip(*texts):
    """A hover explanation laid over its card: hidden until the card is hovered, never takes the mouse.

    By default it shows {s:tip}. A card whose sentence the server already sends (a mission's text, a stat's name)
    moves that label into its tip instead, so the card shows only its picture and numbers."""
    texts = texts or ("{s:tip}",)
    with box(cls="k4-tip", hittest="false"):
        if len(texts) == 1:
            label(texts[0], "k4-tip-text", hittest="false")
            return
        with box(cls="k4-tip-body", hittest="false"):
            for text in texts:
                label(text, "k4-tip-line", hittest="false")


def first(i):
    # Cells in a row are spaced by a left margin; the first one has none (Panorama has no :first-child).
    return " k4-first" if i == 0 else ""


def grow():
    panel("k4-grow")


def vgrow():
    panel("k4-vgrow")


def button(id, text_cls="k4-btn-label", cls="k4-btn", icon=None, gone=False):
    with box("Button", id=id, cls=f"{cls}{' ' + GONE if gone else ''}"):
        if icon:
            pic(f"k4-btn-icon {icon}")
        label("{s:label}", text_cls)


def scroll(id, cls):
    """A list the player scrolls on their own PC: hittest="true" is what lets the mouse wheel reach it."""
    return box(id=id, cls=f"k4-scroll {cls}", hittest="true")


with box("root"):
    with box("styles"):
        for src in ("s2r://panorama/styles/csgostyles.vcss_c", "s2r://panorama/styles/custom_game/k4_ui.vcss_c"):
            lines.append("\t" * depth + f'<include src="{src}" />')
    # The screen-wide root, the toast stack and the banner let the mouse through (hittest="false"): the root
    # sits above every other HUD, and without it it took the hover from other plugins' mouse menus.
    with box(cls="k4-root", hittest="false"):

        # ------------------------------------------------------------ the window
        with box(id="k4-panel", cls="k4-panel k4-closed"):
          # CS2's season art in grey behind the menu, dimmed; the frame on top holds the menu, top to bottom.
          panel("k4-bg")
          panel("k4-shade")
          with box(cls="k4-frame"):

            # Top bar: brand, tabs, event, gear, close
            with box(cls="k4-top"):
                with box(cls="k4-brand"):
                    label("K4", "k4-brand-name", True)
                    label("System", "k4-brand-sub", True)
                for i, (name, icon) in enumerate(TABS):
                    with box("Button", id=f"k4-nav-{name}", cls=f"k4-tab{first(i)}"):
                        with box(cls="k4-tab-inner"):
                            pic(f"k4-tab-icon ic-{icon}")
                            label("{s:label}", "k4-tab-label")
                            if name == "duels":
                                label("{s:count}", f"k4-tab-badge {GONE}", True, id="k4-nav-duels-badge")
                        panel("k4-tab-line")
                grow()
                with box(id="k4-event", cls=f"k4-event {GONE}"):
                    pic("k4-event-icon ic-hot_trend")
                    label("{s:event}", "k4-event-text")
                with box("Button", id="k4-nav-settings", cls="k4-square"):
                    pic("k4-square-icon ic-settings")
                with box("Button", id="k4-close", cls="k4-square"):
                    pic("k4-square-icon ic-cancel")

            # Crumbs: Back, where you are, the season clock
            with box(id="k4-crumbs", cls="k4-crumbs"):
                with box("Button", id="k4-back", cls=f"k4-back {GONE}"):
                    pic("k4-back-icon ic-expand")
                    label("{s:label}", "k4-back-label")
                label("{s:crumb}", "k4-crumb")
                grow()
                with box(id="k4-season", cls=f"k4-season {GONE}"):
                    label("{s:title}", "k4-season-title")
                    label("{s:left}", "k4-season-left")
                    bar("k4-season-bar", "k4-bar-thin k4-bar-season")

            # A challenge waiting for this player, on every screen
            with box(id="k4-duel-bar", cls=f"k4-duel-bar {GONE}"):
                # One line: the rule's picture, who, the rule and stake, the time left, then accept or decline.
                with box(cls="k4-card k4-duel-bar-card"):
                    pic("k4-duel-bar-icon", id="k4-duel-bar-icon")
                    label("{s:title}", "k4-duel-bar-title")
                    label("{s:text}", "k4-duel-bar-msg")
                    grow()
                    bar("k4-duel-bar-timer", "k4-bar-thin k4-bar-clock k4-duel-bar-track")
                    label("{s:left}", "k4-duel-bar-left", True)
                    button("k4-duel-bar-accept", cls="k4-btn k4-btn-main k4-btn-icon-only", icon="ic-check")
                    button("k4-duel-bar-decline", cls="k4-btn k4-btn-ghost k4-btn-icon-only k4-gap", icon="ic-cancel")

            with box(cls="k4-body"):

                # ------------------------------------------------ Home: your rank, today, the top 3, what to do next, your numbers
                # The simple look (owner 2026-11-21): pictures and numbers in front, the sentences on hover.
                with box(id="k4-view-home", cls="k4-view k4-home"):
                    with box("Button", id="k4-h-rank", cls="k4-click k4-h-rank"):
                        pic("k4-em-hero sg-0", id="k4-h-rank-icon")
                        with box(cls="k4-h-rank-text"):
                            label("{s:rank}", "k4-h-rank-name", True, id="k4-h-rank-name")
                            with box(cls="k4-row"):
                                label("{s:points}", "k4-h-points", True)
                                label("{s:place}", "k4-h-place", True)
                            bar("k4-h-bar", "k4-h-bar-track")
                            # The way to the next rank: still filled by the server, left out of the simple look.
                            with box(cls="k4-row k4-h-next-row"):
                                label("{s:next}", "k4-h-nextrank")
                                label("{s:togo}", "k4-h-togo")
                    with box(cls="k4-h-mid"):
                        with box(id="k4-h-today", cls="k4-card k4-h-today"):
                            with box(cls="k4-row k4-head"):
                                pic("k4-cap-icon ic-quest")
                                label("{s:title}", "k4-cap-text")
                                label("{s:reset}", "k4-cap-text")
                            with box(cls="k4-h-tiles"):
                                for i in range(HOME_MISSIONS):
                                    with box("Button", id=f"k4-h-m{i}", cls=f"k4-h-m k4-has-tip{first(i)} {GONE}"):
                                        with box(cls="k4-h-m-body"):
                                            pic("k4-h-m-icon", id=f"k4-h-m{i}-icon")
                                            label("{s:progress}", "k4-h-m-progress", True)
                                            bar(f"k4-h-m{i}-bar", "k4-bar-thin k4-h-m-bar")
                                        tip("{s:desc}", "{s:reward}")
                            # The streak week lives on Missions; Home keeps the parts for the server, hidden.
                            with box(cls="k4-h-streak"):
                                for i in range(STREAK_DAYS):
                                    with box(id=f"k4-h-sd{i}", cls=f"k4-h-sd{first(i)}"):
                                        pic("k4-sd-check ic-check")
                                        label("{s:day}", "k4-h-sd-day", True)
                        with box("Button", id="k4-h-top", cls="k4-card k4-click k4-h-top"):
                            with box(cls="k4-row k4-head"):
                                pic("k4-cap-icon ic-leader")
                                label("{s:title}", "k4-cap-text")
                            # The podium, laid out 2-1-3.
                            with box(cls="k4-h-podium"):
                                for i in (1, 0, 2):
                                    with box(id=f"k4-h-p{i}", cls=f"k4-h-p k4-h-p-{i + 1} {GONE}"):
                                        pic("k4-em-m sg-0", id=f"k4-h-p{i}-icon")
                                        label("{s:name}", "k4-h-p-name")
                                        label("{s:points}", "k4-h-p-points")
                                        with box(cls="k4-h-step"):
                                            label("{s:pos}", "k4-h-p-pos", True)
                            with box(id="k4-h-me", cls=f"k4-h-me {GONE}"):
                                label("{s:pos}", "k4-h-me-pos", True)
                                grow()
                                label("{s:points}", "k4-h-me-points", True)
                        with box(id="k4-h-next", cls="k4-card k4-h-next"):
                            with box(cls="k4-row k4-head"):
                                pic("k4-cap-icon ic-winner_ribbon")
                                label("{s:title}", "k4-cap-text")
                            with box(cls="k4-h-medals"):
                                for i in range(HOME_NEXT):
                                    with box("Button", id=f"k4-h-n{i}", cls=f"k4-click k4-h-n k4-has-tip {GONE}"):
                                        with box(cls="k4-h-n-body"):
                                            with box(cls="k4-h-n-ring"):
                                                pic("k4-h-n-icon", id=f"k4-h-n{i}-icon")
                                            label("{s:title}", "k4-h-n-title")
                                        tip("{s:title}", "{s:text}")
                    with box(id="k4-h-stats", cls=f"k4-h-stats {GONE}"):
                        for i in range(HOME_STATS):
                            with box(id=f"k4-h-s{i}", cls=f"k4-h-s k4-has-tip{first(i)} {GONE}"):
                                with box(cls="k4-h-s-body"):
                                    pic("k4-h-s-icon", id=f"k4-h-s{i}-icon")
                                    label("{s:value}", "k4-h-s-value", True)
                                tip("{s:label}")

                # ------------------------------------------------ Missions
                with box(id="k4-view-missions", cls="k4-view k4-missions"):
                    with box(cls="k4-toolbar"):
                        label("{s:title}", "k4-title", True)
                        pic("k4-title-icon ic-clock")
                        label("{s:reset}", "k4-title-fact k4-m-reset")
                        grow()
                        with box(cls="k4-tag k4-tag-big k4-m-swaps"):
                            pic("k4-tag-icon ic-refresh_skinny")
                            label("{s:swaps}", "k4-tag-label", True)
                    with box(cls="k4-m-cards"):
                        for i in range(MISSIONS):
                            # Stacked, not flowed (k4-has-tip): the strip, the picture and number, the tip, and on
                            # top of the tip the swap button and reward, then the swap question.
                            with box(id=f"k4-m{i}", cls=f"k4-card k4-m k4-has-tip{first(i)} {GONE}"):
                                panel("k4-m-strip")
                                with box(cls="k4-m-body"):
                                    pic("k4-m-icon", id=f"k4-m{i}-icon")
                                    label("{s:progress}", "k4-m-progress", True)
                                    bar(f"k4-m{i}-bar", "k4-m-bar")
                                tip("{s:diff}", "{s:desc}", "{s:paid}")
                                with box(cls="k4-m-top"):
                                    button(f"k4-reroll-{i}", cls="k4-btn k4-btn-sm k4-btn-ghost k4-btn-icon-only", icon="ic-refresh_skinny", gone=True)
                                    grow()
                                    label("{s:reward}", "k4-m-reward", True)
                                with box(id=f"k4-m{i}-ask", cls=f"k4-m-ask {GONE}"):
                                    label("{s:ask}", "k4-m-ask-text")
                                    with box(cls="k4-row k4-m-ask-row"):
                                        button(f"k4-m{i}-yes", cls="k4-btn k4-btn-main")
                                        button(f"k4-m{i}-no", cls="k4-btn k4-btn-ghost k4-gap")
                    with box(cls="k4-m-bottom"):
                        with box(id="k4-mw", cls=f"k4-card k4-mw k4-has-tip {GONE}"):
                            with box(cls="k4-mw-body"):
                                pic("k4-mw-icon", id="k4-mw-icon")
                                with box(cls="k4-mw-text"):
                                    with box(cls="k4-row"):
                                        label("{s:title}", "k4-mw-title", True)
                                        label("{s:left}", "k4-mw-left")
                                    bar("k4-mw-bar", "k4-bar-thin k4-mw-track")
                                with box(cls="k4-mw-side"):
                                    label("{s:progress}", "k4-mw-progress", True)
                                    label("{s:reward}", "k4-mw-reward", True)
                            tip("{s:desc}")
                        with box(cls="k4-card k4-streak"):
                            # The streak count and its sentence stay with the server; the days say it with numbers.
                            with box(cls="k4-streak-head"):
                                label("{s:streak}", "k4-streak-count")
                                label("{s:streakhint}", "k4-streak-hint")
                            for i in range(STREAK_DAYS):
                                with box(id=f"k4-sd{i}", cls=f"k4-sd k4-has-tip{first(i)}"):
                                    with box(cls="k4-sd-body"):
                                        pic("k4-sd-check ic-check")
                                        label("{s:day}", "k4-sd-day", True)
                                        label("{s:bonus}", "k4-sd-bonus", True)
                                    tip()

                # ------------------------------------------------ Achievements
                with box(id="k4-view-achievements", cls="k4-view k4-achievements"):
                    with box(cls="k4-toolbar"):
                        for i, name in enumerate(("all", "progress", "unlocked", "shown")):
                            button(f"k4-af-{name}", "k4-chip-label", f"k4-chip{first(i)}")
                        grow()
                        # The showcase: the three medals on the player's profile, small, before the count.
                        with box(cls="k4-a-showcase"):
                            label("{s:showcase}", "k4-a-sc-cap")
                            for i in range(SHOWCASE):
                                with box("Button", id=f"k4-a-sc{i}", cls=f"k4-a-sc k4-click"):
                                    medal(f"k4-a-sc{i}-ring", f"k4-a-sc{i}-icon", 32)
                                    label("{s:name}", "k4-a-sc-name")
                                    label("{s:empty}", "k4-a-sc-empty")
                        label("{s:count}", "k4-a-count", True)
                    with scroll("k4-a-list", "k4-a-list"):
                        with box(id="k4-a-empty", cls=f"k4-a-empty {GONE}"):
                            label("{s:none}", "k4-empty-text")
                            button("k4-a-show-unlocked")
                        for i in range(ACHIEVEMENT_ROWS):
                            with box("Button", id=f"k4-a{i}", cls=f"k4-card k4-click k4-a k4-has-tip {GONE}"):
                                with box(cls="k4-a-body"):
                                    medal(f"k4-a{i}-ring", f"k4-a{i}-icon", 64)
                                    label("{s:name}", "k4-a-name")
                                    bar(f"k4-a{i}-bar", "k4-bar-thin k4-a-bar")
                                    pic("k4-a-check ic-check")
                                    # The goal and the text are in the tip; the server still fills them.
                                    label("{s:goal}", "k4-a-goal", True)
                                    label("{s:desc}", "k4-a-desc")
                                pic("k4-a-pin ic-icon_star")
                                tip()
                        label("{s:more}", f"k4-more {GONE}", id="k4-a-more")

                # ------------------------------------------------ Leaderboard: players, or the rank list
                with box(id="k4-view-leaderboard", cls="k4-view k4-leaderboard"):
                    with box(cls="k4-toolbar"):
                        button("k4-lb-tab-players", "k4-chip-label", "k4-chip k4-first")
                        button("k4-lb-tab-ranks", "k4-chip-label", "k4-chip")
                        with box(cls="k4-lb-switch"):
                            with box("Button", id="k4-lb-newer", cls="k4-arrow k4-arrow-left"):
                                pic("k4-arrow-icon ic-expand")
                            label("{s:season}", "k4-lb-season", True)
                            with box(cls="k4-tag k4-lb-status"):
                                label("{s:status}", "k4-tag-label")
                            with box("Button", id="k4-lb-older", cls="k4-arrow k4-arrow-right"):
                                pic("k4-arrow-icon ic-expand")
                        grow()
                        label("{s:count}", "k4-title-fact k4-lb-count")
                    with box(id="k4-lb-players", cls="k4-lb-players"):
                        with scroll("k4-lb-list", "k4-card k4-lb-list"):
                            for i in range(LEADERBOARD_ROWS):
                                with box(id=f"k4-lb-r{i}", cls=f"k4-lb-row {GONE}"):
                                    # The whole row opens the player's profile: its content sits inside that button.
                                    with box("Button", id=f"k4-lb-r{i}-profile", cls="k4-lb-open"):
                                        label("{s:pos}", "k4-lb-pos", True)
                                        pic("k4-em-m sg-0", id=f"k4-lb-r{i}-icon")
                                        label("{s:name}", "k4-lb-name")
                                        label("{s:rank}", "k4-lb-rank")
                                        label("{s:points}", "k4-lb-points", True)
                                        label("{s:profile}", "k4-lb-caption")
                                    # A fixed-width box, so the points line up with the pinned row when Challenge is hidden.
                                    with box(cls="k4-lb-btns"):
                                        with box("Button", id=f"k4-lb-r{i}-duel", cls=f"k4-btn k4-btn-sm k4-btn-ghost k4-btn-icon-only k4-lb-duel {GONE}"):
                                            pic("k4-btn-icon ic-casual")
                                            label("{s:duel}", "k4-btn-label")
                            label("{s:more}", f"k4-more {GONE}", id="k4-lb-more")
                        with box(id="k4-lb-me", cls=f"k4-lb-row k4-lb-me k4-me {GONE}"):
                            with box(cls="k4-lb-open"):
                                label("{s:pos}", "k4-lb-pos", True)
                                pic("k4-em-m sg-0", id="k4-lb-me-icon")
                                label("{s:name}", "k4-lb-name")
                                label("{s:rank}", "k4-lb-rank")
                                label("{s:points}", "k4-lb-points", True)
                            panel("k4-lb-btns")
                    with box(id="k4-lb-ranks", cls=f"k4-lb-ranks {GONE}"):
                        with box(id="k4-lb-you", cls="k4-lb-you k4-me"):
                            label("{s:text}", "k4-lb-you-text")
                        with scroll("k4-rk-list", "k4-card k4-rk-list"):
                            for i in range(RANK_ROWS):
                                with box(id=f"k4-rk{i}", cls=f"k4-rk k4-has-tip {GONE}"):
                                    with box(cls="k4-rk-body"):
                                        pic("k4-em-rk sg-0", id=f"k4-rk{i}-icon")
                                        label("{s:name}", "k4-rk-name", True, id=f"k4-rk{i}-name")
                                        pic(f"k4-rk-you ic-profile {GONE}", id=f"k4-rk{i}-you")
                                        pic(f"k4-rk-gift ic-gift {GONE}", id=f"k4-rk{i}-gift")
                                        label("{s:reward}", "k4-rk-reward")
                                        label("{s:points}", "k4-rk-points", True)
                                    tip()
                            label("{s:more}", f"k4-more {GONE}", id="k4-rk-more")

                # ------------------------------------------------ Duels: pick a player, a rule and a stake; the duels on the right
                with box(id="k4-view-duels", cls="k4-view k4-duels"):
                    with box(cls="k4-d-build"):
                        # The step captions stay with the server; the simple look shows the choices alone.
                        with box(cls="k4-row k4-sub"):
                            label("{s:pick}", "k4-sub-title")
                            label("{s:online}", "k4-sub-fact")
                        with scroll("k4-dp-list", "k4-dp-list"):
                            for i in range(DUEL_PLAYER_ROWS):
                                with box("Button", id=f"k4-dp{i}", cls=f"k4-card k4-click k4-pick k4-dp {GONE}"):
                                    pic("k4-em-dp sg-0", id=f"k4-dp{i}-icon")
                                    label("{s:name}", "k4-dp-name")
                                    with box(cls="k4-dp-extra"):
                                        label("{s:points}", "k4-dp-points")
                                        label("{s:state}", "k4-dp-state")
                            label("{s:more}", f"k4-more {GONE}", id="k4-dp-more")
                            label("{s:none}", f"k4-empty-text {GONE}", id="k4-dp-none")
                        with box(cls="k4-row k4-sub"):
                            label("{s:types}", "k4-sub-title")
                        with box(cls="k4-d-types"):
                            for i in range(DUEL_TYPES):
                                with box("Button", id=f"k4-dt{i}", cls=f"k4-card k4-click k4-pick k4-dt k4-has-tip{first(i)} {GONE}"):
                                    with box(cls="k4-dt-body"):
                                        pic("k4-dt-icon", id=f"k4-dt{i}-icon")
                                        label("{s:name}", "k4-dt-name")
                                    tip("{s:name}", "{s:tip}")
                        with box(cls="k4-row k4-sub"):
                            label("{s:stakes}", "k4-sub-title")
                        with box(cls="k4-d-stakes"):
                            for i in range(DUEL_STAKES):
                                with box("Button", id=f"k4-ds{i}", cls=f"k4-chip{first(i)} {GONE}"):
                                    label("{s:label}", "k4-chip-label", True)
                        with box(cls="k4-card k4-d-sum"):
                            # Who, the rule and the stake, as pictures and numbers; why it cannot be sent yet.
                            with box(id="k4-d-pick", cls=f"k4-d-pick {GONE}"):
                                pic("k4-em-s sg-0", id="k4-d-pick-icon")
                                label("{s:target}", "k4-d-pick-name")
                                pic("k4-d-pick-type", id="k4-d-pick-type")
                                label("{s:stake}", "k4-d-pick-stake", True)
                            label("{s:status}", "k4-d-status")
                            grow()
                            button("k4-d-send", cls="k4-btn k4-btn-main k4-btn-big")
                    with box(cls="k4-d-side"):
                        with box(id="k4-d-wait", cls=f"k4-card k4-d-wait {GONE}"):
                            label("{s:text}", "k4-d-wait-text")
                            bar("k4-d-wait-bar", "k4-bar-thin k4-bar-clock")
                            button("k4-d-takeback", cls="k4-btn k4-btn-sm")
                        with box(id="k4-d-mine", cls=f"k4-card k4-d-mine {GONE}"):
                            with box(cls="k4-row"):
                                pic("k4-d-mine-icon", id="k4-d-mine-icon")
                                label("{s:title}", "k4-d-mine-title")
                                grow()
                                label("{s:score}", "k4-d-mine-score", True)
                            label("{s:text}", "k4-d-mine-text")
                            bar("k4-d-mine-bar", "k4-bar-thin k4-bar-clock")
                        with box(cls="k4-card k4-d-live"):
                            with box(cls="k4-row k4-head"):
                                pic("k4-cap-icon ic-casual")
                                label("{s:live}", "k4-cap-text")
                            for i in range(LIVE_DUEL_ROWS):
                                with box(id=f"k4-dl{i}", cls=f"k4-dl k4-has-tip {GONE}"):
                                    with box(cls="k4-dl-body"):
                                        pic("k4-dl-icon", id=f"k4-dl{i}-icon")
                                        label("{s:names}", "k4-dl-names")
                                        label("{s:score}", "k4-dl-score", True)
                                    tip("{s:info}")
                            label("{s:none}", f"k4-empty-text {GONE}", id="k4-dl-none")

                # ------------------------------------------------ Profile: your own, or another player's
                with box(id="k4-view-profile", cls="k4-view k4-profile"):
                    with box(cls="k4-card k4-pf-left"):
                        with box(cls="k4-pf-head"):
                            label("{s:name}", "k4-pf-name", True)
                            label("{s:place}", "k4-pf-place", True)
                        pic("k4-em-pf sg-0", id="k4-pf-icon")
                        with box(id="k4-pf-badge", cls=f"k4-pf-badge {GONE}"):
                            label("{s:tag}", "k4-pf-badge-text", True)
                        label("{s:rank}", "k4-pf-rank", True, id="k4-pf-rank")
                        label("{s:points}", "k4-pf-points", True)
                        bar("k4-pf-bar", "k4-pf-bar-track")
                        label("{s:next}", "k4-pf-next")
                        button("k4-pf-challenge", cls="k4-btn k4-btn-main k4-pf-challenge", icon="ic-casual", gone=True)
                        vgrow()
                        with box(cls="k4-pf-showcase"):
                            for i in range(SHOWCASE):
                                with box("Button", id=f"k4-pf-sc{i}", cls=f"k4-pf-sc k4-click k4-has-tip{first(i)} {GONE}"):
                                    with box(cls="k4-pf-sc-body"):
                                        medal(f"k4-pf-sc{i}-ring", f"k4-pf-sc{i}-icon", 64)
                                        label("{s:name}", "k4-pf-sc-name")
                                        label("{s:desc}", "k4-pf-sc-desc")
                                    tip()
                        with box(id="k4-pf-sc-hint", cls=f"k4-pf-sc-hint {GONE}"):
                            label("{s:hint}", "k4-empty-text")
                    with box(cls="k4-pf-right"):
                        for row in range(2):
                            with box(cls=f"k4-pf-tiles{first(row)}"):
                                for column in range(PROFILE_STATS // 2):
                                    i = row * (PROFILE_STATS // 2) + column
                                    with box(id=f"k4-pf-st{i}", cls=f"k4-card k4-pf-st k4-has-tip{first(column)} {GONE}"):
                                        with box(cls="k4-pf-st-body"):
                                            pic("k4-pf-st-icon", id=f"k4-pf-st{i}-icon")
                                            label("{s:value}", "k4-tile-value", True)
                                            label("{s:label}", "k4-pf-st-label")
                                        tip()
                        with box(cls="k4-pf-pair"):
                            with box(cls="k4-pf-col"):
                                with box(cls="k4-card k4-pf-weapons"):
                                    with box(cls="k4-row k4-pf-card-head"):
                                        label("{s:fav}", "k4-pf-card-title")
                                        label("{s:favkills}", "k4-pf-card-fact")
                                    for i in range(PROFILE_WEAPONS):
                                        with box(id=f"k4-pf-sw{i}", cls=f"k4-pf-line {GONE}"):
                                            pic("k4-pf-sw-icon", id=f"k4-pf-sw{i}-icon")
                                            label("{s:weapon}", "k4-pf-line-name")
                                            bar(f"k4-pf-sw{i}-bar", "k4-pf-line-bar")
                                            label("{s:kills}", "k4-pf-line-value", True)
                                with box(id="k4-pf-pt", cls=f"k4-card k4-pf-pt {GONE}"):
                                    with box(cls="k4-row k4-pf-card-head"):
                                        pic("k4-pf-card-icon ic-clock")
                                        label("{s:total}", "k4-pf-card-title", True)
                                    for side, picture in (("t", "tp-t"), ("ct", "tp-ct"), ("spec", "ic-watch")):
                                        with box(id=f"k4-pf-pt-{side}", cls=f"k4-pf-line k4-side-{side}"):
                                            pic(f"k4-pf-side-icon {picture}")
                                            label("{s:name}", "k4-pf-line-name")
                                            bar(f"k4-pf-pt-{side}-bar", "k4-pf-line-bar")
                                            label("{s:time}", "k4-pf-line-value", True)
                            with scroll("k4-pf-ss-list", "k4-card k4-pf-ss-list"):
                                for i in range(SEASON_ROWS):
                                    with box(id=f"k4-pf-ss{i}", cls=f"k4-pf-ss {GONE}"):
                                        pic("k4-em-ss sg-0", id=f"k4-pf-ss{i}-icon")
                                        label("{s:season}", "k4-pf-ss-season")
                                        label("{s:pos}", "k4-pf-ss-pos", True)
                                with box(id="k4-pf-ss-none", cls=f"k4-pf-ss-none {GONE}"):
                                    label("{s:none}", "k4-empty-text")

                # ------------------------------------------------ Settings (the gear)
                with box(id="k4-view-settings", cls="k4-view k4-settings"):
                    with box(cls="k4-toolbar"):
                        label("{s:title}", "k4-title", True)
                    with box(cls="k4-card k4-set-card"):
                        with box(cls="k4-set-row k4-first"):
                            label("{s:mode}", "k4-set-name")
                            with box(cls="k4-seg"):
                                for i, mode in enumerate(("panel", "chat", "both")):
                                    button(f"k4-set-mode-{mode}", "k4-seg-label", f"k4-seg-item{first(i)}")
                            label("{s:lock}", f"k4-set-lock {GONE}", id="k4-set-mode-lock")
                        for row in ("points", "tag"):
                            with box(id=f"k4-set-{row}", cls=f"k4-set-row {GONE}"):
                                label(f"{{s:{row}}}", "k4-set-name")
                                with box(cls="k4-seg"):
                                    button(f"k4-set-{row}-on", "k4-seg-label", "k4-seg-item k4-first")
                                    button(f"k4-set-{row}-off", "k4-seg-label", "k4-seg-item")

            # Footer: the last click's result, the out-of-date bar, the key hint, the copyright
            with box(cls="k4-foot"):
                with box(id="k4-status", cls=f"k4-status {GONE}"):
                    pic("k4-status-icon k4-status-ok ic-check")
                    pic("k4-status-icon k4-status-bad ic-warning")
                    label("{s:status}", "k4-status-text")
                # Visible unless the server's contract class matches this stylesheet (.k4-c8 hides it).
                with box(id="k4-outdated", cls="k4-outdated"):
                    pic("k4-outdated-icon ic-warning")
                    label("K4 files do not match this server. Restart CS2 to update the K4 addon.", "k4-outdated-text")
                grow()
                with box(id="k4-keys", cls="k4-keys"):
                    label("{s:keys}", "k4-keys-text")
                label("© 2026 karola3vax", "k4-copyright")

        # ------------------------------------------------------------ promotion banner
        with box(id="k4-promo", cls=f"k4-promo {GONE}", hittest="false"):
            label("{s:title}", "k4-promo-title", hittest="false")
            pic("k4-promo-icon sg-0", id="k4-promo-icon", hittest="false")
            label("{s:rank}", "k4-promo-rank", True, hittest="false")

        # ------------------------------------------------------------ toasts: under the kill feed, newest on top;
        # under the window while it is open (k4-open)
        with box(id="k4-toasts", cls="k4-toasts", hittest="false"):
            for i in range(TOASTS):
                with box(id=f"k4-t{i}", cls=f"k4-toast k4-toast-{i} k4-tc-green k4-len-normal {GONE}", hittest="false"):
                    panel("k4-toast-strip", hittest="false")
                    with box(cls="k4-toast-row", hittest="false"):
                        with box(cls="k4-toast-pic", hittest="false"):
                            # The ring is behind the icon, not around it: a toast without a medal hides only the ring.
                            with box(id=f"k4-t{i}-ring", cls=f"k4-ring k4-ring-toast t-locked {GONE}", hittest="false"):
                                panel("k4-disc", hittest="false")
                            pic("k4-toast-icon", id=f"k4-t{i}-icon", hittest="false")
                        with box(cls="k4-toast-body", hittest="false"):
                            label("{s:kind}", "k4-toast-kind", hittest="false")
                            label("{s:text}", "k4-toast-text", hittest="false")
                            with box(id=f"k4-t{i}-prog", cls=f"k4-bar k4-bar-thin k4-bar-toast {GONE}", hittest="false"):
                                panel("k4-bar-fill w0", id=f"k4-t{i}-bar", hittest="false")
                        label("{s:value}", "k4-toast-value", True, hittest="false")
                    panel("k4-toast-timer", id=f"k4-t{i}-timer", hittest="false")
            label("{s:more}", f"k4-t-more {GONE}", id="k4-t-more", hittest="false")

open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
elements = sum(1 for line in lines if line.lstrip().startswith("<") and not line.lstrip().startswith(("</", "<include", "<root", "<styles")))
ids = sum(line.count(' id="') for line in lines)
print(f"wrote {len(lines)} lines: {elements} elements, {ids} ids")
