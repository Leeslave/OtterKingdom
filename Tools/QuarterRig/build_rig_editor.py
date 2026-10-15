"""Build rig-editor.html: every QuarterRigView rig's parts, pivots and saved tuning packed into one page.

Rigs come from the plaza prefabs that use QuarterRigView (their resourceFolder and characterScale).

    python Tools/QuarterRig/build_rig_editor.py              # -> Tools/QuarterRig/rig-editor.html
    python Tools/QuarterRig/build_rig_editor.py --body OUT   # page body only (for publishing as an artifact)

Rerun after re-exporting parts, adding a rig prefab or changing a rig's tuning.json.
The last rig, "꾸미기", is cut from the user's reference pictures (Tools/CustomOtter/build.py: body
parts, a head per head fur, expression overlays, tint weights); the page tints its fur, swaps the
head fur and expression, and draws hats and accessories.
The page's pose code is a port of QuarterRigView.cs; change both together.
"""
import argparse
import base64
import io
import json
import pathlib
import re
import sys

from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
RESOURCES = ROOT / "Assets/Resources"
PREFABS = ROOT / "Assets/Prefabs/Plaza"
RIG_VIEW_GUID = "a29eaed5e9ac4a1badeddf527641c691"  # QuarterRigView.cs
RIG_LABELS = {"QuarterRigV14": "농부", "WaiterRigV14": "웨이터", "ChefRigV1": "요리사",
              "FisherRigV1": "뱃사람", "BaseballRigV1": "야구 해달", "ArtisanRigV1": "작업반장", "PajamaRigV1": "잠옷 해달", "ReceptionRigV1": "안내원", "MinerRigV1": "광부", "PhotographerRigV1": "사진가", "MusicianRigV1": "음악가", "MerchantRigV1": "행상인", "BardRigV1": "공연복 음악가", "DiverRigV1": "잠수부", "CleanerRigV1": "청소부", "FlowerRigV1": "꽃 카디건", "StripeRigV1": "줄무늬 니트", "HoodieRigV1": "후드티", "KnitRigV1": "케이블 니트", "OverallsRigV1": "노랑 멜빵", "PlaidRigV1": "체크 치마", "BearPajamaRigV1": "곰돌이 잠옷", "BeanieRigV1": "방울 모자", "ScholarRigV1": "안경 학자", "VarsityRigV1": "야구 점퍼"}
BENCH_PNG = ROOT / "Assets/Art/Plaza/Props/Prop_Bench.png"
BENCH_PREFAB = ROOT / "Assets/Prefabs/Plaza/Prop_Bench.prefab"
CROP_ICON = ROOT / "Assets/Art/Item/Vegetable/Carrot.png"
BALL_ICON = ROOT / "Assets/Art/Item/Toy/ICON_Toy_SoccerBall.png"
PUZZLE_ICON = ROOT / "Assets/Art/Item/Toy/ICON_Toy_Puzzle.png"
CUSTOM = "CustomOtter"


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


def icon(path, size=256, trim=True):
    # Shown at well under 256 screen px, so a small copy is plenty.
    image = Image.open(path).convert("RGBA")
    if trim:
        image = image.crop(image.getchannel("A").getbbox())
    image.thumbnail((size, size), Image.LANCZOS)
    return {"src": data_uri(image), "w": image.width, "h": image.height, "pivot": [.5, .5]}


def rig_prefabs():
    """(resourceFolder, characterScale) of each plaza prefab that uses QuarterRigView, farmer first."""
    found = {}
    for prefab in sorted(PREFABS.glob("*.prefab")):
        text = prefab.read_text(encoding="utf-8")
        if RIG_VIEW_GUID not in text:
            continue
        folder = re.search(r"resourceFolder: (\S+)", text).group(1)
        scale = float(re.search(r"characterScale: ([\d.]+)", text).group(1))
        if (RESOURCES / folder / "rig.json").exists():
            found.setdefault(folder, scale)
    return sorted(found.items(), key=lambda item: item[0] != "QuarterRigV14")


def build_rig(folder, scale):
    root = RESOURCES / folder
    rig = json.loads((root / "rig.json").read_text(encoding="utf-8"))
    tuning_path = root / "tuning.json"
    return {"label": RIG_LABELS.get(folder, folder), "scale": scale,
            "rig": {"legSolver": rig["legSolver"], "bones": rig["bones"], "rearBones": rig["rearBones"]},
            "sprites": {png.stem: sprite(png) for png in sorted(root.glob("*.png"))},
            "tuning": json.loads(tuning_path.read_text(encoding="utf-8")) if tuning_path.exists() else None}


def build_custom(scale):
    """The customisable otter (Tools/CustomOtter): its parts, rig and the page's data."""
    sys.path.insert(0, str(ROOT / "Tools/CustomOtter"))
    import build as custom
    sprites, rig, data = custom.build()
    out = {}
    for name, (image, pivot, weight) in sprites.items():
        out[name] = {"src": data_uri(Image.fromarray(image)), "w": image.shape[1], "h": image.shape[0],
                     "pivot": [float(pivot[0]), float(pivot[1])]}
        if weight is not None:
            out[name]["tint"] = data_uri(Image.fromarray(weight))
    return {"label": "꾸미기", "scale": scale, "rig": rig, "sprites": out, "tuning": None,
            "custom": json.loads(json.dumps(data, default=float))}


def build_data():
    rigs = {folder: build_rig(folder, scale) for folder, scale in rig_prefabs()}
    rigs[CUSTOM] = build_custom(rigs["QuarterRigV14"]["scale"])
    # Toys are drawn as the plaza does it: the whole icon (padding included) fitted to the cell.
    return {"rigs": rigs,
            "shared": {"crop": icon(CROP_ICON), "ball": icon(BALL_ICON, trim=False),
                       "puzzle": icon(PUZZLE_ICON, trim=False)},
            "bench": bench()}


# The published page has a size limit: its pictures go in smaller (drawn at the same size, a little softer).
BODY_IMAGE_SCALE = .7


def shrink_images(node):
    """Every embedded picture (src / tint) at BODY_IMAGE_SCALE of its pixels; sizes and pivots stay."""
    if isinstance(node, dict):
        for key, value in node.items():
            if key in ("src", "tint") and isinstance(value, str) and value.startswith("data:image/png;base64,"):
                image = Image.open(io.BytesIO(base64.b64decode(value.split(",", 1)[1])))
                size = (max(1, round(image.width * BODY_IMAGE_SCALE)), max(1, round(image.height * BODY_IMAGE_SCALE)))
                node[key] = data_uri(image.resize(size, Image.LANCZOS))
            else:
                shrink_images(value)
    elif isinstance(node, list):
        for value in node:
            shrink_images(value)
    return node


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--body", type=pathlib.Path, help="write only the page body here (no <!doctype>/<html>)")
    args = parser.parse_args()

    template = (HERE / "rig-editor.template.html").read_text(encoding="utf-8")
    data = build_data()
    if args.body:
        shrink_images(data)
    body = template.replace("/*RIG_DATA*/null", json.dumps(data, separators=(",", ":")))
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
