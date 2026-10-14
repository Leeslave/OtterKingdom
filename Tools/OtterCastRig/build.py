"""Cast rigs: four of the idle cast's otters as QuarterRigView rigs, like the farmer and the waiter.

    python Tools/OtterCastRig/build.py

1. parts.py re-dresses the waiter's V14 parts per otter (fur colour, bare head, clothes).
2. For each otter, a page draws every part (front, rear, sleeping head) with its hat, outfit or
   held prop (costumes.js) in a grid, joint at each cell's centre; headless Chrome saves it as
   ArtSource/Otter/CastRig/<id>/atlas.png.
3. The cells are trimmed into Assets/Resources/<Name>RigV14/<view>-<part>.png with Sprite import
   settings whose pivot is the joint (PPU 200), next to the waiter's rig.json and fx-z.png.
4. Assets/Prefabs/Plaza/PlazaOtter_<Name>Rigged.prefab: the waiter's prefab pointed at that folder.
"""
import base64
import json
import pathlib
import re
import shutil
import subprocess
import uuid

from PIL import Image

import parts

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
WAITER = ROOT / "Assets/Resources/WaiterRigV14"
FARMER = ROOT / "Assets/Resources/QuarterRigV14"
WAITER_PREFAB = ROOT / "Assets/Prefabs/Plaza/PlazaOtter_WaiterRigged.prefab"
ART = ROOT / "ArtSource/Otter/CastRig"
CHROME = pathlib.Path("C:/Program Files/Google/Chrome/Application/chrome.exe")
CELL, COLS = 1200, 5
# The sailor and the chef are Codex's FisherRigV1 / ChefRigV1 (Tools/FisherRig, Tools/ChefRig), built from
# their own image_gen part sheets.
NAMES = {"painter": "Painter", "explorer": "Explorer", "sleepy": "Sleepy", "clown": "Clown"}
CELLS = [("front", p) for p in parts.PARTS] + [("front", "head-sleep")] + [("rear", p) for p in parts.PARTS]

PAGE = """<!doctype html><html><head><meta charset="utf-8"><title>atlas</title>
<style>html,body{margin:0;background:transparent;overflow:hidden}canvas{display:block}</style></head>
<body><canvas id="atlas"></canvas><script>const DATA = __DATA__;</script><script>__COSTUMES__</script></body></html>"""


def pivot_of(meta_path):
    m = re.search(r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", meta_path.read_text(encoding="utf-8"))
    return [float(m.group(1)), float(m.group(2))]


def waiter_meta(view, part):
    """Import settings and joint of each part: the farmer's for the head (his face), else the waiter's."""
    if part in ("head", "head-sleep"):
        return FARMER / f"{view}-{part}.png.meta"
    return WAITER / f"{view}-{part}.png.meta"


def atlas(member):
    base = ART / member / "base"
    cells = []
    for view, part in CELLS:
        png = base / f"{view}-{part}.png"
        image = Image.open(png)
        cells.append({"view": view, "part": part, "w": image.width, "h": image.height,
                      "pivot": pivot_of(waiter_meta(view, part)),
                      "src": "data:image/png;base64," + base64.b64encode(png.read_bytes()).decode("ascii")})
    page = PAGE.replace("__DATA__", json.dumps({"cells": cells})).replace("__COSTUMES__", (HERE / "costumes.js").read_text(encoding="utf-8"))
    page_path = ART / member / "atlas-page.html"
    page_path.write_text(page, encoding="utf-8")
    rows = -(-len(cells) // COLS)
    shot = ART / member / "atlas.png"
    subprocess.run([str(CHROME), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                    "--default-background-color=00000000", f"--window-size={COLS * CELL},{rows * CELL}",
                    "--virtual-time-budget=6000", f"--screenshot={shot}", page_path.as_uri() + f"?member={member}"],
                   check=True, capture_output=True, timeout=180)
    page_path.unlink()
    return Image.open(shot).convert("RGBA")


def write_meta(template_path, dst, pivot):
    meta = dst.with_name(dst.name + ".meta")
    old = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
    text = template_path.read_text(encoding="utf-8").replace("\r\n", "\n")
    text = re.sub(r"^guid: \w+", f"guid: {old.group(1) if old else uuid.uuid4().hex}", text, flags=re.M)
    text = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", f"spritePivot: {{x: {pivot[0]:.6g}, y: {pivot[1]:.6g}}}", text)
    text = re.sub(r"spriteID: \w+", "spriteID: ", text)
    meta.write_text(text, encoding="utf-8", newline="\r\n")


def export(member, sheet):
    folder = f"{NAMES[member]}RigV14"
    out = ROOT / "Assets/Resources" / folder
    out.mkdir(parents=True, exist_ok=True)
    for i, (view, part) in enumerate(CELLS):
        x0, y0 = (i % COLS) * CELL, (i // COLS) * CELL
        cell = sheet.crop((x0, y0, x0 + CELL, y0 + CELL))
        box = cell.getchannel("A").getbbox()
        pad = 2
        box = (max(0, box[0] - pad), max(0, box[1] - pad), min(CELL, box[2] + pad), min(CELL, box[3] + pad))
        if box[0] == 0 or box[1] == 0 or box[2] == CELL or box[3] == CELL:
            raise SystemExit(f"{member} {view}-{part}: touches its cell edge; make CELL bigger")
        sprite = cell.crop(box)
        w, h = sprite.size
        pivot = ((CELL / 2 - box[0]) / w, (box[3] - CELL / 2) / h)
        dst = out / f"{view}-{part}.png"
        sprite.save(dst, optimize=True)
        write_meta(waiter_meta(view, part), dst, pivot)
    shutil.copyfile(WAITER / "rig.json", out / "rig.json")
    if not (out / "rig.json.meta").exists():
        (out / "rig.json.meta").write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8", newline="\r\n")
    shutil.copyfile(WAITER / "fx-z.png", out / "fx-z.png")
    write_meta(WAITER / "fx-z.png.meta", out / "fx-z.png", (.5, .5))
    folder_meta = out.with_name(folder + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8", newline="\r\n")
    return folder


def prefab(member, folder):
    name = f"PlazaOtter_{NAMES[member]}Rigged"
    text = WAITER_PREFAB.read_text(encoding="utf-8").replace("\r\n", "\n")
    text = text.replace("resourceFolder: WaiterRigV14", f"resourceFolder: {folder}")
    text = text.replace("m_Name: PlazaOtter_WaiterRigged", f"m_Name: {name}")
    dst = WAITER_PREFAB.with_name(name + ".prefab")
    dst.write_text(text, encoding="utf-8", newline="\r\n")
    meta = dst.with_name(dst.name + ".meta")
    if not meta.exists():
        source = WAITER_PREFAB.with_name(WAITER_PREFAB.name + ".meta").read_text(encoding="utf-8")
        meta.write_text(re.sub(r"^guid: \w+", f"guid: {uuid.uuid4().hex}", source, flags=re.M), encoding="utf-8", newline="")
    return dst


def main():
    parts.main()
    for member in NAMES:
        sheet = atlas(member)
        folder = export(member, sheet)
        path = prefab(member, folder)
        print(f"{member}: Assets/Resources/{folder}, {path.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
