"""Idle images of the plaza cast rebuilt in the V14 look (farmer / waiter line art) - an alternative to
from_reference.py (the chosen idle images); writes to ArtSource/Otter/IdleCast/v14/.

    python Tools/OtterIdleCast/build.py

1. make_base.py: a bare V14 head (the waiter's, hair removed, farmer's eyes), its closed-eye
   version, and each member's body and arms with the waiter's vest / shirt / bowtie / sleeves
   recoloured -> ArtSource/Otter/IdleCast/v14/base/.
2. Packs those, the waiter's legs and tail, and its rig layout with cast.js (pose, hats, props)
   into ArtSource/Otter/IdleCast/v14/idle-cast.html, a page with every member idling side by side.
3. Opens that page in headless Chrome once per member (#export=<id>) and saves the otter, trimmed,
   on a transparent background as ArtSource/Otter/IdleCast/v14/idle-<id>.png.
"""
import base64
import json
import pathlib
import re
import subprocess

from PIL import Image

import make_base

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
WAITER = ROOT / "Assets/Resources/WaiterRigV14"
BASE = ROOT / "ArtSource/Otter/IdleCast/v14/base"
OUT = ROOT / "ArtSource/Otter/IdleCast/v14"
CHROME = pathlib.Path("C:/Program Files/Google/Chrome/Application/chrome.exe")
# Export canvas: 900 x 1000, the otter's feet at (450, 930), drawn at this many screen px per rig px.
SCALE = 2.15


def pivot_of(png):
    meta = (png.parent / (png.name + ".meta")).read_text(encoding="utf-8")
    m = re.search(r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", meta)
    return [float(m.group(1)), float(m.group(2))]


def sprite(png, pivot):
    image = Image.open(png)
    data = base64.b64encode(png.read_bytes()).decode("ascii")
    return {"src": "data:image/png;base64," + data, "w": image.width, "h": image.height, "pivot": pivot}


def build_data():
    rig = json.loads((WAITER / "rig.json").read_text(encoding="utf-8"))
    head_pivot = pivot_of(WAITER / "front-head.png")
    parts = {"head": sprite(BASE / "head.png", head_pivot), "head-sleep": sprite(BASE / "head-sleep.png", head_pivot)}
    for name in ("nearLeg", "farLeg", "tail"):
        png = WAITER / f"front-{name}.png"
        parts[name] = sprite(png, pivot_of(png))
    outfits = {}
    for member in make_base.OUTFITS:
        outfits[member] = {name: sprite(BASE / f"{member}-{name}.png", pivot_of(WAITER / f"front-{name}.png"))
                           for name in ("body", "nearArm", "farArm")}
    return {"rig": {"legSolver": rig["legSolver"], "bones": rig["bones"]}, "parts": parts, "outfits": outfits}


PAGE = """<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>해달 Idle 모음</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Jua&family=IBM+Plex+Sans+KR:wght@400;500&display=swap">
<style>
:root { --bg: #e9ede3; --ink: #2c241b; --muted: #6b675b; --stage: #dfe6d5; --line: #d3d9c8; --shadow: rgba(70, 90, 50, .16); }
@media (prefers-color-scheme: dark) { :root { --bg: #191c16; --ink: #ece7dc; --muted: #a49f92; --stage: #283023; --line: #353b2e; --shadow: rgba(0, 0, 0, .35); } }
body { margin: 0; background: var(--bg); color: var(--ink); font-family: "IBM Plex Sans KR", system-ui, "Malgun Gothic", sans-serif; }
main { max-width: 1180px; margin: 0 auto; padding: 20px 16px 32px; display: grid; gap: 12px; }
h1 { font-family: "Jua", system-ui, sans-serif; font-weight: 400; font-size: 28px; margin: 0; }
p { margin: 0; color: var(--muted); }
canvas#stage { display: block; width: 100%; aspect-ratio: 5 / 1.6; background: var(--stage); border: 1px solid var(--line); border-radius: 14px; }
html.export, html.export body { background: transparent; margin: 0; overflow: hidden; }
html.export main > :not(canvas) { display: none; }
html.export main { padding: 0; max-width: none; }
html.export canvas#stage { position: fixed; left: 0; top: 0; width: 900px; height: 1000px; aspect-ratio: auto; background: transparent; border: 0; border-radius: 0; }
</style>
</head>
<body>
<main>
<h1>해달 Idle 모음</h1>
<p>농부·웨이터 해달과 같은 V14 그림체로 만든 광장 해달들의 서 있는 모습이에요. 이미지는 같은 폴더의 idle-*.png.</p>
<canvas id="stage" aria-label="화가, 요리사, 탐험가, 뱃사람, 잠꾸러기 해달이 나란히 서 있는 모습"></canvas>
</main>
<script>const DATA = __DATA__;</script>
<script>
__CAST__
</script>
</body>
</html>
"""


def build_page():
    page = PAGE.replace("__DATA__", json.dumps(build_data(), separators=(",", ":")))
    page = page.replace("__CAST__", (HERE / "cast.js").read_text(encoding="utf-8"))
    out = OUT / "idle-cast.html"
    out.write_text(page, encoding="utf-8", newline="\r\n")
    return out


def export(page_path, member, scratch):
    shot = scratch / f"{member}.png"
    url = page_path.as_uri() + f"#export={member}&scale={SCALE}"
    subprocess.run([str(CHROME), "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--default-background-color=00000000", "--window-size=900,1000",
                    "--virtual-time-budget=4000", f"--screenshot={shot}", url],
                   check=True, capture_output=True, timeout=120)
    image = Image.open(shot).convert("RGBA")
    box = image.getchannel("A").getbbox()
    if box is None:
        raise SystemExit(f"{member}: nothing was drawn")
    pad = 6
    box = (max(0, box[0] - pad), max(0, box[1] - pad), min(image.width, box[2] + pad), min(image.height, box[3] + pad))
    if box[1] == 0:
        raise SystemExit(f"{member}: cut off at the top; lower SCALE")
    trimmed = image.crop(box)
    dst = OUT / f"idle-{member}.png"
    trimmed.save(dst, optimize=True)
    feet = ((450 - box[0]) / trimmed.width, (box[3] - 930) / trimmed.height)
    print(f"{dst.relative_to(ROOT)}: {trimmed.width}x{trimmed.height}, feet pivot ({feet[0]:.3f}, {feet[1]:.3f})")
    return trimmed


def main():
    make_base.main()
    page = build_page()
    print(page.relative_to(ROOT))
    scratch = OUT / ".export"
    scratch.mkdir(exist_ok=True)
    images = [export(page, member, scratch) for member in make_base.OUTFITS]
    for leftover in scratch.iterdir():
        leftover.unlink()
    scratch.rmdir()
    # All of them on one sheet, feet on a line, for a quick look.
    height = max(im.height for im in images)
    sheet = Image.new("RGBA", (sum(im.width for im in images) + 30 * (len(images) + 1), height + 60), (232, 237, 223, 255))
    x = 30
    for im in images:
        sheet.alpha_composite(im, (x, 30 + height - im.height))
        x += im.width + 30
    sheet.save(OUT / "idle-cast-sheet.png", optimize=True)


if __name__ == "__main__":
    main()
