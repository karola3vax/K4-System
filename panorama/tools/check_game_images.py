"""Checks the K4 addon against an installed CS2: every game image it uses exists, and the rank emblem copies
still match the game's artwork. Run it before publishing the addon, and after CS2 updates.

usage: check_game_images.py "<CS2>/game/csgo/pak01_dir.vpk"
Exit code 0 when everything matches. A changed emblem means: run gen_k4_ranks.py with the same VPK.
"""
import os
import re
import sys

from gen_k4_ranks import GAME_FOLDER, OUT, RANKS, scaled
from vpk import Vpk

ADDON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "k4system")


def main() -> int:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    game = Vpk(sys.argv[1])

    text = ""
    for root, _, files in os.walk(ADDON):
        for name in files:
            if name.endswith((".css", ".xml")):
                with open(os.path.join(root, name), encoding="utf-8") as file:
                    text += file.read()

    problems = []
    refs = sorted(set(re.findall(r's2r://(panorama/(?!styles/)[^"\)\s]+)', text)))
    for ref in refs:
        if ref.startswith("panorama/images/custom_game/"):
            if not os.path.exists(os.path.join(ADDON, os.path.splitext(ref)[0] + ".svg")):
                problems.append(f"missing in the addon: {ref}")
        elif ref + "_c" not in game:
            problems.append(f"not in the game: {ref}")

    for kind in ("skillgroup", "wingman"):
        for number in RANKS:
            name = f"{kind}{number}"
            with open(os.path.join(OUT, f"{name}.svg"), encoding="utf-8") as file:
                ours = file.read()
            if ours != scaled(game.svg(f"{GAME_FOLDER}/{name}.vsvg_c").replace("\r\n", "\n")):
                problems.append(f"emblem differs from the game: {name}.svg")

    for problem in problems:
        print(problem)
    print(f"{len(refs)} image references, {2 * len(RANKS)} emblems: " + ("all match the game" if not problems else f"{len(problems)} problem(s)"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
