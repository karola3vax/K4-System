"""Writes sharp copies of the CS2 icons the K4 panel uses.

CS2 draws an SVG background at the size written in the file and then stretches the picture. The game's
icons are about 32x32, so on the full-screen panel (mission cards, medals, stat tiles) they looked blurry.
Like the rank emblems (gen_k4_ranks.py), the addon carries copies whose only change is a larger size in
the root <svg> tag. The artwork is Valve's own.

usage: gen_k4_icons.py "<CS2>/game/csgo/pak01_dir.vpk"

Which icons: every s2r://panorama/images/custom_game/k4/icons/<group>/<name>.vsvg the stylesheet references,
so run gen_k4_css.py first. check_game_images.py says when a copy no longer matches the game.
"""
import os
import re
import sys

from vpk import Vpk

# The longer side of every copy, in px: sharp at the largest size the panel shows an icon.
SIZE = 256
GAME_FOLDERS = {"ui": "panorama/images/icons/ui", "equipment": "panorama/images/icons/equipment", "deathnotice": "panorama/images/hud/deathnotice"}
ADDON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system")
CSS = os.path.join(ADDON, "panorama", "styles", "custom_game", "k4_ui.css")
OUT = os.path.join(ADDON, "panorama", "images", "custom_game", "k4", "icons")
REF = re.compile(r"s2r://panorama/images/custom_game/k4/icons/([a-z]+)/([a-z0-9_]+)\.vsvg")


def referenced() -> list[tuple[str, str]]:
    with open(CSS, encoding="utf-8") as file:
        return sorted(set(REF.findall(file.read())))


def source(game: Vpk, group: str, name: str) -> str:
    """The icon's SVG text inside the game's compiled .vsvg_c."""
    compiled = game.read(f"{GAME_FOLDERS[group]}/{name}.vsvg_c")
    start, stop = compiled.find(b"<svg"), compiled.rfind(b"</svg>")
    if start < 0 or stop < 0:
        sys.exit(f"{group}/{name}: no SVG text inside")
    return compiled[start:stop + 6].decode("utf-8").replace("\r\n", "\n")


def scaled(svg: str) -> str:
    """The same SVG with its width and height raised so the longer side is SIZE px. The viewBox keeps the drawing."""
    root = re.search(r"<svg\b[^>]*>", svg)
    if root is None:
        sys.exit("no <svg> tag")
    tag = root.group(0)
    width = re.search(r'\swidth="([\d.]+)(px)?"', tag)
    height = re.search(r'\sheight="([\d.]+)(px)?"', tag)
    if width and height:
        w, h = float(width.group(1)), float(height.group(1))
    else:
        box = re.search(r'viewBox="[-\d.]+[ ,]+[-\d.]+[ ,]+([\d.]+)[ ,]+([\d.]+)"', tag)
        if box is None:
            sys.exit("the <svg> tag has no size and no viewBox")
        w, h = float(box.group(1)), float(box.group(2))
    factor = SIZE / max(w, h)
    new = re.sub(r'\s(width|height)="[\d.]+(px)?"', "", tag)
    if "viewBox" not in new:
        new = new.replace("<svg", f'<svg viewBox="0 0 {w:g} {h:g}"', 1)
    new = new.replace("<svg", f'<svg width="{w * factor:g}px" height="{h * factor:g}px"', 1)
    return svg[:root.start()] + new + svg[root.end():]


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    game = Vpk(sys.argv[1])
    icons = referenced()
    for group, name in icons:
        folder = os.path.join(OUT, group)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as target:
            target.write(scaled(source(game, group, name)))
    print(f"{len(icons)} icons -> {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
