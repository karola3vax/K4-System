"""Writes sharp copies of CS2's rank emblems for the K4 addon.

CS2 draws an SVG background at the size written in the file and then stretches the picture.
The game's rank emblems (panorama/images/icons/skillgroups/skillgroupN.vsvg and wingmanN.vsvg)
are 32x13, so the panel's larger rank icon looked blurry. A server-driven HUD cannot use the
Image element that CS2's own menus use to draw them larger, so the addon carries copies whose
only change is the size in the root <svg> tag (SCALE times larger). The artwork is Valve's own.

usage: gen_k4_ranks.py "<CS2>/game/csgo/pak01_dir.vpk"
   or: gen_k4_ranks.py <folder with skillgroup0.svg .. skillgroup18.svg and wingman0.svg .. wingman18.svg>

With the VPK, the emblems are read straight from the installed game, so the copies always match the current
CS2 artwork. Run it again after a CS2 update that changes the rank emblems (check_game_images.py says so).
"""
import os
import re
import sys

from vpk import Vpk

SCALE = 10
RANKS = range(19)
GAME_FOLDER = "panorama/images/icons/skillgroups"
# Under panorama/images/custom_game: the only image folder CS2's Workshop Manager packs (gameinfo.gi
# AddonConfig/VpkDirectories); anything elsewhere is silently left out of the upload.
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system", "panorama", "images", "custom_game", "k4", "ranks")


def find(folder: str, name: str) -> str:
    for root, _, files in os.walk(folder):
        if name in files:
            return os.path.join(root, name)
    sys.exit(f"{name} not found under {folder}")


def scaled(svg: str) -> str:
    root = re.search(r"<svg\b[^>]*>", svg)
    if root is None:
        sys.exit("no <svg> tag")
    tag = root.group(0)
    for attribute in ("width", "height"):
        match = re.search(rf'\s{attribute}="([\d.]+)(px)?"', tag)
        if match is None:
            sys.exit(f"the <svg> tag has no {attribute}")
        value = float(match.group(1)) * SCALE
        tag = tag.replace(match.group(0), f' {attribute}="{value:g}px"', 1)
    return svg[:root.start()] + tag + svg[root.end():]


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    source = sys.argv[1]
    game = Vpk(source) if source.lower().endswith(".vpk") else None
    os.makedirs(OUT, exist_ok=True)
    for kind in ("skillgroup", "wingman"):
        for number in RANKS:
            name = f"{kind}{number}"
            if game is not None:
                text = game.svg(f"{GAME_FOLDER}/{name}.vsvg_c")
            else:
                with open(find(source, f"{name}.svg"), encoding="utf-8") as file:
                    text = file.read()
            with open(os.path.join(OUT, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as target:
                target.write(scaled(text.replace("\r\n", "\n")))
    print(f"{2 * len(RANKS)} rank emblems -> {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
