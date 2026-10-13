"""Build rig-editor.html: the V14 rig parts, pivots and any saved tuning packed into one page.

    python Tools/QuarterRig/build_rig_editor.py              # -> Tools/QuarterRig/rig-editor.html
    python Tools/QuarterRig/build_rig_editor.py --body OUT   # page body only (for publishing as an artifact)

Rerun after re-exporting the parts or changing Resources/QuarterRigV14/tuning.json.
The page's pose code is a port of QuarterRigView.cs; change both together.
"""
import argparse
import base64
import io
import json
import pathlib
import re

from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
RIG = ROOT / "Assets/Resources/QuarterRigV14"
BENCH_PNG = ROOT / "Assets/Art/Plaza/Props/Prop_Bench.png"
BENCH_PREFAB = ROOT / "Assets/Prefabs/Plaza/Prop_Bench.prefab"
CROP_ICON = ROOT / "Assets/Art/Item/Vegetable/Carrot.png"


def data_uri(image):
    buffer = io.BytesIO()
    image.save(buffer, "PNG", optimize=True)
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


def meta_value(meta_path, pattern):
    match = re.search(pattern, meta_path.read_text(encoding="utf-8"))
    if not match:
        raise SystemExit(f"{meta_path.name}: no match for {pattern}")
    return match


def sprite(png):
    """Image, pixel size and normalised pivot (from the bottom-left, as Unity stores it)."""
    pivot = meta_value(png.with_name(png.name + ".meta"), r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}")
    image = Image.open(png).convert("RGBA")
    return {"src": data_uri(image), "w": image.width, "h": image.height,
            "pivot": [float(pivot.group(1)), float(pivot.group(2))]}


def bench():
    meta = BENCH_PNG.with_name(BENCH_PNG.name + ".meta")
    pivot = meta_value(meta, r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}")
    ppu = float(meta_value(meta, r"spritePixelsToUnits: ([\d.]+)").group(1))
    # The first seat (a child transform listed in PlazaSpotView._seats).
    prefab = BENCH_PREFAB.read_text(encoding="utf-8")
    seat_id = re.search(r"_seats:\s*\n\s*- \{fileID: (\d+)\}", prefab).group(1)
    block = re.search(rf"--- !u!4 &{seat_id}\n(.*?)\n---", prefab, re.S).group(1)
    seat = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)", block)
    image = Image.open(BENCH_PNG).convert("RGBA")
    return {"src": data_uri(image), "w": image.width, "h": image.height, "ppu": ppu,
            "pivot": [float(pivot.group(1)), float(pivot.group(2))],
            "seat": [float(seat.group(1)), float(seat.group(2))]}


def crop_icon():
    # Held at ~95 rig px (190 image px at 2x), so a 256 px copy is plenty.
    image = Image.open(CROP_ICON).convert("RGBA")
    box = image.getchannel("A").getbbox()
    image = image.crop(box)
    image.thumbnail((256, 256), Image.LANCZOS)
    return {"src": data_uri(image), "w": image.width, "h": image.height, "pivot": [.5, .5]}


def build_data():
    rig = json.loads((RIG / "rig.json").read_text(encoding="utf-8"))
    sprites = {}
    for png in sorted(RIG.glob("*.png")):
        sprites[png.stem] = sprite(png)
    sprites["crop"] = crop_icon()
    tuning_path = RIG / "tuning.json"
    tuning = json.loads(tuning_path.read_text(encoding="utf-8")) if tuning_path.exists() else None
    return {"rig": {"legSolver": rig["legSolver"], "bones": rig["bones"], "rearBones": rig["rearBones"]},
            "sprites": sprites, "bench": bench(), "tuning": tuning}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--body", type=pathlib.Path, help="write only the page body here (no <!doctype>/<html>)")
    args = parser.parse_args()

    template = (HERE / "rig-editor.template.html").read_text(encoding="utf-8")
    body = template.replace("/*RIG_DATA*/null", json.dumps(build_data(), separators=(",", ":")))
    if args.body:
        # The artifact viewer can't hand out files, so the published page copies the JSON instead.
        body = body.replace("const CAN_SAVE_FILES = true;", "const CAN_SAVE_FILES = false;")
        args.body.write_text(body, encoding="utf-8")
        print(f"{args.body}: {len(body) // 1024} KB")
        return
    page = ('<!doctype html>\n<html lang="ko">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            '<style>html{color-scheme:light}body{margin:0}img{max-width:100%}[hidden]{display:none!important}</style>\n'
            '</head>\n<body>\n' + body + '\n</body>\n</html>\n')
    out = HERE / "rig-editor.html"
    out.write_text(page, encoding="utf-8", newline="\r\n")
    print(f"{out.relative_to(ROOT)}: {len(page) // 1024} KB")


if __name__ == "__main__":
    main()
