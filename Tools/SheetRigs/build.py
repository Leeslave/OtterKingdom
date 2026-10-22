"""Rigs for the otters the user hands over as finished part sheets (image_gen, transparent, 1536x1024).

    python Tools/SheetRigs/build.py                # every otter in OTTERS
    python Tools/SheetRigs/build.py Flower Knit    # just these

A sheet is two rows (front, rear) of five parts - head, body, arm, leg, tail - like Codex's pajama sheet
(ArtSource/Otter/PajamaRig/V1/atlas.png). This does what Codex's per-otter build.cjs does, for any such
sheet: each part is found as its own island (small bits beside it - a flower, a paw - go with the nearest
part), trimmed, and stretched into the pajama otter's sprite of that part (PajamaRigV1: the same bones,
sizes, pivots and import settings), the rear far arm mirrored. A tilted arm is first turned upright
(shoulder up, paw down), since the rig's arm hangs straight from the shoulder.

Writes Assets/Resources/<Name>RigV1 (+ rig.json, metas with new guids), the plaza prefab
PlazaOtter_<Name>Rigged (the pajama otter's, pointed at that folder), the closed-eye head for dozing
(make_sleep_parts.py; skipped when the face has no open eyes to close) and the sheet itself under
ArtSource/Otter/<Name>Rig/V1/atlas.png.
"""
import json
import pathlib
import re
import shutil
import subprocess
import sys
import uuid

import cv2
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
TEMPLATE = ROOT / "Assets/Resources/PajamaRigV1"
TEMPLATE_PREFAB = ROOT / "Assets/Prefabs/Plaza/PlazaOtter_PajamaRigged.prefab"
TYPES = ["head", "body", "arm", "leg", "tail"]
SPRITES = {"head": ["head"], "body": ["body"], "arm": ["nearArm", "farArm"], "leg": ["nearLeg", "farLeg"], "tail": ["tail"]}
# Name: (label in the rig editor, sleep-head options for make_sleep_parts.py or None when the face has no open eyes)
OTTERS = {
    "Flower": ("꽃 카디건", []),
    "Stripe": ("줄무늬 니트", None),
    "Hoodie": ("후드티", None),
    "Knit": ("케이블 니트", []),
    "Plaid": ("체크 치마", ["--open-eyes", "1"]),
    "BearPajama": ("곰돌이 잠옷", None),
    "Hero": ("주인공", []),
}


def parts(sheet):
    """{(row, type): RGBA} - big islands sorted left to right per row; small islands join the nearest."""
    alpha = sheet[..., 3]
    n, labels, stats, cent = cv2.connectedComponentsWithStats((alpha >= 128).astype(np.uint8), 8)
    big = [i for i in range(1, n) if stats[i, 4] > 3000]
    small = [i for i in range(1, n) if 30 < stats[i, 4] <= 3000]
    h = sheet.shape[0]
    owner = {i: i for i in big}
    for i in small:
        owner[i] = min(big, key=lambda j: np.hypot(*(cent[i] - cent[j])))
    out = {}
    for row in (0, 1):
        mine = sorted([i for i in big if (cent[i][1] >= h / 2) == bool(row)], key=lambda i: cent[i][0])
        assert len(mine) == 5, f"row {row}: {len(mine)} parts"
        for kind, i in zip(TYPES, mine):
            keep = np.isin(labels, [k for k, v in owner.items() if v == i])
            keep = cv2.dilate(keep.astype(np.uint8), np.ones((7, 7), np.uint8)).astype(bool)
            piece = sheet.copy()
            piece[..., 3] = np.where(keep & (piece[..., 3] >= 8), piece[..., 3], 0)
            x, y, w, hh = cv2.boundingRect((piece[..., 3] > 0).astype(np.uint8))
            out[(row, kind)] = piece[y:y + hh, x:x + w]
    return out


def upright(arm):
    """The arm turned so its long axis is vertical, the end that was lower (the paw) at the bottom."""
    ys, xs = np.nonzero(arm[..., 3] > 128)
    pts = np.stack([xs, ys], 1).astype(np.float32)
    mean = pts.mean(0)
    _, _, vt = np.linalg.svd(pts - mean)
    axis = vt[0] if vt[0][1] > 0 else -vt[0]              # pointing down (towards the paw)
    angle = np.degrees(np.arctan2(axis[0], axis[1]))     # from straight down, clockwise on screen
    pad = max(arm.shape)
    big = cv2.copyMakeBorder(arm, pad, pad, pad, pad, cv2.BORDER_CONSTANT, value=(0, 0, 0, 0))
    m = cv2.getRotationMatrix2D((big.shape[1] / 2, big.shape[0] / 2), -angle, 1.0)
    turned = cv2.warpAffine(big, m, (big.shape[1], big.shape[0]), flags=cv2.INTER_CUBIC)
    x, y, w, h = cv2.boundingRect((turned[..., 3] > 20).astype(np.uint8))
    return turned[y:y + h, x:x + w]


def fill(image, size):
    """Premultiplied stretch into size (no dark fringes at the edges)."""
    pm = image.astype(np.float32)
    pm[..., :3] *= pm[..., 3:] / 255
    pm = cv2.resize(pm, size, interpolation=cv2.INTER_AREA)
    alpha = np.clip(pm[..., 3:], 1e-3, 255)
    pm[..., :3] = np.where(pm[..., 3:] > 0, pm[..., :3] * 255 / alpha, 0)
    return np.clip(pm, 0, 255).astype(np.uint8)


def head_sprite(image, template_size, template_pivot):
    """The head scaled evenly to the template head's width and its joint put where the template's is above the
    chin: the chin is the lowest row still as wide as half the head (a braid or a paw hanging lower doesn't
    count). Returns the image and its pivot (Unity's, from the bottom left)."""
    a = image[..., 3] > 128
    widths = a.sum(axis=1)
    rows = np.nonzero(widths > .5 * widths.max())[0]
    top, chin = np.nonzero(a.any(axis=1))[0][0], rows.max()
    k = template_size[0] / image.shape[1]
    scaled = fill(image, (template_size[0], max(1, round(image.shape[0] * k))))
    mass = (chin - top) * k
    joint_y = chin * k - template_pivot[1] * mass          # template: the joint this far above the chin
    return scaled, (template_pivot[0], 1 - joint_y / scaled.shape[0])


def write_pivot(meta, pivot):
    text = meta.read_text(encoding="utf-8")
    text = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", f"spritePivot: {{x: {pivot[0]:.6g}, y: {pivot[1]:.6g}}}", text)
    meta.write_text(text, encoding="utf-8", newline="")


def new_guid(meta_text):
    return re.sub(r"^guid: \w+", f"guid: {uuid.uuid4().hex}", meta_text, flags=re.M)


def build(name, sheet_path):
    folder = f"{name}RigV1"
    out = ROOT / "Assets/Resources" / folder
    art = ROOT / f"ArtSource/Otter/{name}Rig/V1"
    art.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    if pathlib.Path(sheet_path).resolve() != (art / "atlas.png").resolve():
        Image.open(sheet_path).convert("RGBA").save(art / "atlas.png")
    sheet = np.array(Image.open(art / "atlas.png").convert("RGBA"))
    (art / "parts").mkdir(exist_ok=True)
    for (row, kind), image in parts(sheet).items():
        view = "rear" if row else "front"
        if kind == "arm":
            image = upright(image)
        Image.fromarray(image).save(art / "parts" / f"{view}-{kind}.png")
        for part in SPRITES[kind]:
            src = TEMPLATE / f"{view}-{part}.png"
            pivot = None
            if kind == "head":
                m = re.search(r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", src.with_name(src.name + ".meta").read_text(encoding="utf-8"))
                piece, pivot = head_sprite(image, Image.open(src).size, (float(m.group(1)), float(m.group(2))))
            else:
                piece = fill(image, Image.open(src).size)
            if view == "rear" and part == "farArm":
                piece = piece[:, ::-1]
            dst = out / f"{view}-{part}.png"
            Image.fromarray(piece).save(dst, optimize=True)
            meta = dst.with_name(dst.name + ".meta")
            if not meta.exists():
                meta.write_text(new_guid(src.with_name(src.name + ".meta").read_text(encoding="utf-8")), encoding="utf-8", newline="")
            if pivot is not None:
                write_pivot(meta, pivot)
    for extra in ("rig.json",):
        shutil.copyfile(TEMPLATE / extra, out / extra)
        meta = out / (extra + ".meta")
        if not meta.exists():
            meta.write_text(new_guid((TEMPLATE / (extra + ".meta")).read_text(encoding="utf-8")), encoding="utf-8", newline="")
    folder_meta = out.with_name(folder + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text(new_guid(TEMPLATE.with_name(TEMPLATE.name + ".meta").read_text(encoding="utf-8")), encoding="utf-8", newline="")
    prefab = TEMPLATE_PREFAB.with_name(f"PlazaOtter_{name}Rigged.prefab")
    text = TEMPLATE_PREFAB.read_text(encoding="utf-8")
    text = text.replace("resourceFolder: PajamaRigV1", f"resourceFolder: {folder}").replace(
        "m_Name: PlazaOtter_PajamaRigged", f"m_Name: PlazaOtter_{name}Rigged")
    prefab.write_text(text, encoding="utf-8", newline="")
    pmeta = prefab.with_name(prefab.name + ".meta")
    if not pmeta.exists():
        pmeta.write_text(new_guid(TEMPLATE_PREFAB.with_name(TEMPLATE_PREFAB.name + ".meta").read_text(encoding="utf-8")), encoding="utf-8", newline="")
    sleep = OTTERS[name][1]
    if sleep is not None:
        r = subprocess.run([sys.executable, str(ROOT / "Tools/QuarterRig/make_sleep_parts.py"), "--rig", folder, "--fill", "nearest", *sleep],
                           capture_output=True, text=True)
        print(f"  sleep head: {(r.stdout + r.stderr).strip().splitlines()[0] if (r.stdout + r.stderr).strip() else 'ok'}")
    print(f"{name}: Assets/Resources/{folder}, {prefab.relative_to(ROOT)}")


def main():
    names = sys.argv[1:] or list(OTTERS)
    for name in names:
        build(name, ROOT / f"ArtSource/Otter/{name}Rig/V1/atlas.png")


if __name__ == "__main__":
    main()
