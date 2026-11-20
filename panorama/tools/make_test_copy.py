"""Builds a test-only copy of the K4 panel and installs it as loose files in a local CS2, so a new layout can be tried
in game before anything is published to the Workshop.

The copy has its own names everywhere (layout k4_next.xml, stylesheet k4_next.css, pictures under
images/custom_game/k4next/), so it never shadows the Workshop addon and the Workshop addon never shadows it.
A server test build must then use LayoutPath "panorama/layout/custom_game/k4_next.xml" (never committed).

    py panorama/tools/make_test_copy.py "<CS2 folder>"            # build, compile, install
    py panorama/tools/make_test_copy.py "<CS2 folder>" --dry-run  # only print what it would do
    py panorama/tools/make_test_copy.py "<CS2 folder>" --remove   # delete everything it installed

<CS2 folder> is the one that holds game/ and content/ (".../Counter-Strike Global Offensive").
"""
import argparse
import os
import shutil
import subprocess
import sys

NAME = "k4next"
LAYOUT, STYLE = "k4_next.xml", "k4_next.css"
HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "..", "k4system", "panorama")


def rename(text: str) -> str:
    """Points the copy at its own stylesheet and pictures."""
    return (text.replace("custom_game/k4_ui.vcss_c", "custom_game/k4_next.vcss_c")
                .replace("custom_game/k4/", f"custom_game/{NAME}/"))


def plan(cs2: str):
    """(content file, compiled file, loose target) for every file of the copy."""
    content = os.path.join(cs2, "content", "csgo_addons", NAME, "panorama")
    compiled = os.path.join(cs2, "game", "csgo_addons", NAME, "panorama")
    loose = os.path.join(cs2, "game", "csgo", "panorama")
    files = [(os.path.join("layout", "custom_game", "k4_ui.xml"), os.path.join("layout", "custom_game", LAYOUT)),
             (os.path.join("styles", "custom_game", "k4_ui.css"), os.path.join("styles", "custom_game", STYLE))]
    images = os.path.join(SOURCE, "images", "custom_game", "k4")
    for root, _, names in os.walk(images):
        for name in names:
            relative = os.path.relpath(os.path.join(root, name), images)
            files.append((os.path.join("images", "custom_game", "k4", relative), os.path.join("images", "custom_game", NAME, relative)))
    for source, target in files:
        stem, extension = os.path.splitext(target)
        # resourcecompiler writes name.vxml_c, name.vcss_c, name.vsvg_c; png and webp become name_png.vtex_c.
        out = stem + (f"_{extension[1:]}.vtex_c" if extension in (".png", ".webp") else f".v{extension[1:]}_c")
        yield os.path.join(SOURCE, source), os.path.join(content, target), os.path.join(compiled, out), os.path.join(loose, out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("cs2")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--remove", action="store_true")
    args = parser.parse_args()

    compiler = os.path.join(args.cs2, "game", "bin", "win64", "resourcecompiler.exe")
    if not os.path.isfile(compiler):
        print(f"No resourcecompiler at {compiler}", file=sys.stderr)
        return 1
    steps = list(plan(args.cs2))

    if args.remove:
        for _, _, _, loose in steps:
            if os.path.exists(loose):
                print(f"delete {loose}")
                if not args.dry_run:
                    os.remove(loose)
        for folder in (os.path.join(args.cs2, "content", "csgo_addons", NAME), os.path.join(args.cs2, "game", "csgo_addons", NAME)):
            if os.path.isdir(folder):
                print(f"delete {folder}")
                if not args.dry_run:
                    shutil.rmtree(folder)
        return 0

    for source, content, _, _ in steps:
        print(f"write {content}")
        if args.dry_run:
            continue
        os.makedirs(os.path.dirname(content), exist_ok=True)
        if content.endswith((".xml", ".css")):
            with open(source, encoding="utf-8") as reader, open(content, "w", encoding="utf-8", newline="\n") as writer:
                writer.write(rename(reader.read()))
        else:
            shutil.copyfile(source, content)

    missing = []
    for _, content, compiled, loose in steps:
        print(f"compile {content}")
        if args.dry_run:
            continue
        result = subprocess.run([compiler, "-i", content], capture_output=True, text=True)
        if result.returncode != 0 or not os.path.isfile(compiled):
            missing.append(content)
            print(result.stdout[-2000:], result.stderr[-2000:], sep="\n", file=sys.stderr)
            continue
        os.makedirs(os.path.dirname(loose), exist_ok=True)
        shutil.copyfile(compiled, loose)
        print(f"  -> {loose}")

    if missing:
        print(f"{len(missing)} files did not compile", file=sys.stderr)
        return 1
    print(f"{'Would install' if args.dry_run else 'Installed'} {len(steps)} files. Server test build: LayoutPath = \"panorama/layout/custom_game/{LAYOUT}\". Remove with --remove.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
