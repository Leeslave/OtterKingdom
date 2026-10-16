"""Building props for the builder otters' rigs (Codex's rigs).

    python Tools/MechanicRig/build.py                        # 야구 해달: BaseballRigV1, the hammer
    python Tools/MechanicRig/build.py --rig ArtisanRigV1     # 작업반장 해달: the sledgehammer (오함마)

The otter's parts are Codex's rig (Assets/Resources/BaseballRigV1, PlazaOtter_BaseballRigged). This adds
what QuarterRigView's building loop (Loop.Build) needs to that folder, all game art used as it is:
the hammer from the builder's hammer bubble (Art/Settlement/UI_Bubble_Hammer.png), turned upright
(head up, pivot where the paw grips), the wood item icon trimmed as the log (pivot on its top, where
the blows land), and the wood chip effect for the chips. It also makes the closed-eye head for dozing
(make_sleep_parts.py) and turns on workWhenIdle in the plaza prefab, so it builds while standing about.
Rerun after rebuilding the rig (node Tools/BaseballRig/build.cjs rewrites the folder's parts).
"""
import argparse
import pathlib
import re
import subprocess
import sys
import uuid

import cv2
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
TEMPLATE_META = ROOT / "Assets/Resources/WaiterRigV14/front-head.png.meta"
HAMMER_BUBBLE = ROOT / "Assets/Art/Settlement/UI_Bubble_Hammer.png"
LOG = ROOT / "Assets/Art/Settlement/ICON_Item_Wood.png"
CHIP = ROOT / "Assets/Art/Settlement/FX_WoodChip.png"
PREFABS = {"BaseballRigV1": ROOT / "Assets/Prefabs/Plaza/PlazaOtter_BaseballRigged.prefab",
           "ArtisanRigV1": ROOT / "Assets/Prefabs/Plaza/PlazaOtter_ArtisanRigged.prefab"}
# Rigs that build with the sledgehammer instead (QuarterRigView: sledge.png -> the heavy two-handed swing).
SLEDGE_RIGS = {"ArtisanRigV1"}
ONE_EYED = {"ArtisanRigV1"}
HAMMER_PX = 140      # hammer height in sprite px (70 rig px: a forearm and a half)
HAMMER_GRIP = .3     # where the paw grips the handle, as a fraction of the hammer from the handle's end
# The sledgehammer (작업반장): this much bigger than the hammer, its handle this much longer again, this tall
# in sprite px, gripped this far up from the handle's end.
SLEDGE_HEAD, SLEDGE_HANDLE, SLEDGE_PX, SLEDGE_GRIP = 1.6, 1.7, 320, .12


def median_colour(rgba, where):
    return np.median(rgba[..., :3][where & (rgba[..., 3] > 250)].astype(np.float32), axis=0)


def hammer():
    """The hammer from the builder's bubble, upright (head at the top), and its pivot (the grip)."""
    a = np.array(Image.open(HAMMER_BUBBLE).convert("RGBA"))
    rgb = a[..., :3].astype(np.float32)
    # Inside the bubble: not its cream, not its frame.
    inner = cv2.erode((a[..., 3] > 0).astype(np.uint8), np.ones((15, 15), np.uint8)).astype(bool)
    tool = inner & ~(np.linalg.norm(rgb - median_colour(a, inner), axis=2) < 30)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(tool.astype(np.uint8), 8)
    tool = labels == 1 + int(np.argmax(stats[1:, 4]))
    piece = a.copy()
    piece[..., 3] = (piece[..., 3] * cv2.dilate(tool.astype(np.uint8), np.ones((3, 3), np.uint8))).astype(np.uint8)
    x, y, w, h = cv2.boundingRect(tool.astype(np.uint8))
    piece = piece[y:y + h, x:x + w]
    # The handle runs from bottom left to the head at top right: turn it upright.
    ys, xs = np.nonzero(piece[..., 3] > 128)
    pts = np.stack([xs, ys], 1).astype(np.float32)
    _, _, vt = np.linalg.svd(pts - pts.mean(0))
    axis = vt[0] if vt[0][1] < 0 else -vt[0]
    angle = np.degrees(np.arctan2(axis[0], -axis[1]))
    big = cv2.copyMakeBorder(piece, 40, 40, 40, 40, cv2.BORDER_CONSTANT, value=(0, 0, 0, 0))
    m = cv2.getRotationMatrix2D((big.shape[1] / 2, big.shape[0] / 2), angle, 1.0)
    turned = cv2.warpAffine(big, m, (big.shape[1], big.shape[0]), flags=cv2.INTER_CUBIC)
    x, y, w, h = cv2.boundingRect((turned[..., 3] > 20).astype(np.uint8))
    turned = turned[y:y + h, x:x + w]
    # To size, premultiplied so the edges stay clean.
    k = HAMMER_PX / turned.shape[0]
    pm = turned.astype(np.float32)
    pm[..., :3] *= pm[..., 3:] / 255
    pm = cv2.resize(pm, (round(turned.shape[1] * k), HAMMER_PX), interpolation=cv2.INTER_CUBIC)
    pm[..., 3] = np.clip(pm[..., 3], 0, 255)
    alpha = np.clip(pm[..., 3:], 1e-3, 255)
    pm[..., :3] = np.where(pm[..., 3:] > 0, pm[..., :3] * 255 / alpha, 0)
    out = np.pad(np.clip(pm, 0, 255).astype(np.uint8), ((4, 4), (4, 4), (0, 0)))
    return out, (.5, (4 + HAMMER_PX * HAMMER_GRIP) / out.shape[0])


def resize_rgba(img, size):
    """Premultiplied resize, so edges stay clean."""
    pm = img.astype(np.float32)
    pm[..., :3] *= pm[..., 3:] / 255
    pm = cv2.resize(pm, size, interpolation=cv2.INTER_CUBIC)
    pm[..., 3] = np.clip(pm[..., 3], 0, 255)
    alpha = np.clip(pm[..., 3:], 1e-3, 255)
    pm[..., :3] = np.where(pm[..., 3:] > 0, pm[..., :3] * 255 / alpha, 0)
    return np.clip(pm, 0, 255).astype(np.uint8)


def sledge():
    """A sledgehammer (오함마) from the same hammer, upright: cut across the handle just below the head;
    the top (head and the start of the handle) scaled up by SLEDGE_HEAD, the rest of the handle by the same
    across but SLEDGE_HANDLE times longer - the hammer's own pixels, so the cut lines up. Pivot where the
    near paw grips, low on the handle."""
    small, _ = hammer()
    small = small.copy()
    # (the bubble's pale halo round the tool would stretch into streaks)
    pale = small[..., :3].astype(np.int32).mean(axis=2) > 200
    small[..., 3] = np.where(pale & (small[..., 3] < 250), 0, small[..., 3])
    rgb = small[..., :3].astype(np.int32)
    orange = (rgb[..., 0] > 170) & (rgb[..., 1] > 100) & (rgb[..., 1] < 170) & (rgb[..., 2] < 120) & (small[..., 3] > 200)
    # The handle's top: the first row where it is a plain bar (orange between its outlines), well below the head.
    rows = np.nonzero(orange.any(axis=1))[0]
    cut = int(rows.min() + .25 * (rows.max() - rows.min()))
    # ... and its rounded end is kept as it is (stretched it would come to a point).
    end = int(rows.max() - 8)
    top, bar, cap = small[:cut], small[cut:end], small[end:]
    S, L = SLEDGE_HEAD, SLEDGE_HEAD * SLEDGE_HANDLE
    w = round(small.shape[1] * S)
    pieces = [resize_rgba(top, (w, round(top.shape[0] * S))), resize_rgba(bar, (w, round(bar.shape[0] * L))),
              resize_rgba(cap, (w, round(cap.shape[0] * S)))]
    out = np.concatenate(pieces, axis=0)
    x, y, bw, bh = cv2.boundingRect((out[..., 3] > 0).astype(np.uint8))
    out = out[y:y + bh, x:x + bw]
    k = SLEDGE_PX / out.shape[0]
    out = np.pad(resize_rgba(out, (round(out.shape[1] * k), SLEDGE_PX)), ((4, 4), (4, 4), (0, 0)))
    xs = np.nonzero(orange[cut:].any(axis=0))[0]
    grip_x = ((xs.min() + xs.max()) / 2 * S - x) * k + 4
    return out, (grip_x / out.shape[1], (4 + SLEDGE_PX * SLEDGE_GRIP) / out.shape[0])


def write_meta(dst, pivot):
    meta = dst.with_name(dst.name + ".meta")
    old = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
    text = TEMPLATE_META.read_text(encoding="utf-8").replace("\r\n", "\n")
    text = re.sub(r"^guid: \w+", f"guid: {old.group(1) if old else uuid.uuid4().hex}", text, flags=re.M)
    text = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", f"spritePivot: {{x: {pivot[0]:.6g}, y: {pivot[1]:.6g}}}", text)
    text = re.sub(r"spriteID: \w+", "spriteID: ", text)
    meta.write_text(text, encoding="utf-8", newline="\r\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rig", default="BaseballRigV1", help="folder under Assets/Resources")
    out = ROOT / "Assets/Resources" / parser.parse_args().rig
    log = np.array(Image.open(LOG).convert("RGBA"))
    x, y, w, h = cv2.boundingRect((log[..., 3] > 0).astype(np.uint8))
    tool = ("sledge", sledge()) if out.name in SLEDGE_RIGS else ("hammer", hammer())
    props = {tool[0]: tool[1], "log": (log[y:y + h, x:x + w], (.5, .8)),
             "chip": (np.array(Image.open(CHIP).convert("RGBA")), (.5, .5))}
    for name, (image, pivot) in props.items():
        dst = out / f"{name}.png"
        Image.fromarray(image).save(dst, optimize=True)
        write_meta(dst, pivot)
    # The closed-eye head (and the "z") for dozing on a bench.
    # The 작업반장's scarred eye is shut already: close the other one, as thick as that wink.
    one_eye = ["--open-eyes", "1", "--lid", "13"] if out.name in ONE_EYED else []
    subprocess.run([sys.executable, str(ROOT / "Tools/QuarterRig/make_sleep_parts.py"), "--rig", out.name, "--fill", "split" if one_eye else "shapes", *one_eye], check=True)
    prefab = PREFABS.get(out.name)
    if prefab is not None:
        text = prefab.read_text(encoding="utf-8")
        if "workWhenIdle" not in text:
            nl = "\r\n" if "\r\n" in text else "\n"
            line = f"  walkAnimReferenceSpeed: 1.5{nl}"
            text = text.replace(line, line + f"  workWhenIdle: 1{nl}", 1)
            prefab.write_text(text, encoding="utf-8", newline="")
    print(f"{out.relative_to(ROOT)}: {tool[0]}, log, chip, sleeping head")


if __name__ == "__main__":
    main()
