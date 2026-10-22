"""The 주인공's arms holding the other otters' tools, for its special actions.

    python Tools/HeroRig/tool_arms.py

The 광부, 사진가, 행상인, 청소부 and 음악가 have their tool drawn into an arm, gripped by that otter's paw (front
view). For each, that arm's sleeve (its colour regions, the outline and soft edge around them) is taken away, the
주인공's own arm is laid in its place - shoulder on that arm's shoulder (the sprite pivot), paw under that arm's paw,
foreshortened like it - and the gripping paw is kept as drawn, only recoloured to the 주인공's paw colour (its
shading kept). So the tool stays whole under the paw. Same canvas and pivot as the original arm, so the action's
keys (made for that arm) pose it the same way. Saved as Assets/Resources/HeroRigV1/front-<arm>-<tag>.png;
QuarterRigView swaps it in while that action plays.
"""
import pathlib
import re
import uuid

import cv2
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
RES = ROOT / "Assets/Resources"
HERO = RES / "HeroRigV1"
INK = 85             # luma below this is outline (or a dark tool part)
ARM_WIDTH = .9       # the 주인공's sleeve this wide (its length goes to wherever the paw is)
# tag: (rig, arm, a point on each colour region of the gripping paw, a point on each region of the sleeve - x, y px,
#       how far off the sleeve's colours its darker folds may be)
CASES = {
    "mine": ("MinerRigV1", "nearArm", [(182, 307)], [(108, 331)], 26),
    "photo": ("PhotographerRigV1", "nearArm", [(106, 115)], [(87, 30), (46, 90), (50, 150)], 30),
    "ring": ("MerchantRigV1", "farArm", [(73, 120), (116, 112)], [(24, 71), (42, 100)], 70),
    "sweep": ("CleanerRigV1", "farArm", [(118, 173)], [(40, 198), (80, 203)], 18),
    "play": ("BardRigV1", "nearArm", [(119, 143)], [(100, 42), (55, 89), (72, 119)], 40),
}
# Tool and paw moved this much further out along the arm (px): the 주인공's big head would hide the bell.
REACH = {"ring": 85}


def pivot(png):
    m = re.search(r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", png.with_name(png.name + ".meta").read_text(encoding="utf-8"))
    return float(m.group(1)), float(m.group(2))


def luma(rgb):
    return rgb @ np.array([.299, .587, .114])


def regions(im):
    alpha = im[..., 3] > 128
    ink = alpha & (luma(im[..., :3].astype(float)) < INK)
    fill = alpha & ~ink
    return cv2.connectedComponents(fill.astype(np.uint8), connectivity=4)[1], ink


def part(im, lab, seeds, reach=9, tolerance=26):
    """Colour regions at the seeds, plus same-coloured bits close by (split off by fold lines)."""
    ids = [lab[y, x] for x, y in seeds]
    assert 0 not in ids, "a seed is on an outline"
    mask = np.isin(lab, ids)
    rgb = im[..., :3].astype(float)
    near = cv2.dilate(mask.astype(np.uint8), np.ones((reach, reach), np.uint8)) > 0
    # Never the outline itself (its near-black brown), however dark the folds get.
    ink = (im[..., 3] > 128) & (luma(rgb) < INK)
    outline = np.median(rgb[ink], axis=0) if ink.any() else np.zeros(3)
    not_outline = np.linalg.norm(rgb - outline, axis=2) > 45
    for i in ids:
        mask |= near & (np.linalg.norm(rgb - rgb[lab == i].mean(0), axis=2) < tolerance) & (im[..., 3] > 0) & not_outline
    return mask


def hero_arm(name):
    """The 주인공's arm sprite, its shoulder (pivot) and paw centre, and its paw's mean colour."""
    png = HERO / f"front-{name}.png"
    im = np.array(Image.open(png).convert("RGBA"))
    px, py = pivot(png)
    shoulder = np.array([px * im.shape[1], (1 - py) * im.shape[0]])
    lab, _ = regions(im)
    ys, xs = np.nonzero(im[..., 3] > 128)
    low = ys > ys.min() + .75 * (ys.max() - ys.min())
    paw = np.array([xs[low].mean(), ys[low].mean()])
    # The paw's colour: the biggest colour region in the bottom quarter.
    ids, counts = np.unique(lab[ys[low], xs[low]], return_counts=True)
    counts[ids == 0] = 0
    colour = im[..., :3][lab == ids[np.argmax(counts)]].astype(float).mean(0)
    return im, shoulder, paw, colour


def build(tag):
    rig, arm, paw_seeds, sleeve_seeds, folds = CASES[tag]
    src = RES / rig / f"front-{arm}.png"
    im = np.array(Image.open(src).convert("RGBA"))
    h, w = im.shape[:2]
    px, py = pivot(src)
    shoulder = np.array([px * w, (1 - py) * h])
    lab, ink = regions(im)
    paw = part(im, lab, paw_seeds)
    sleeve = part(im, lab, sleeve_seeds, reach=31, tolerance=folds) & ~paw
    # Each outline pixel (and soft edge) belongs to the nearest of sleeve, paw and tool.
    tool_fill = (im[..., 3] > 128) & ~ink & ~sleeve & ~paw
    def dist(mask):
        return cv2.distanceTransform((~mask).astype(np.uint8), cv2.DIST_L2, 3)
    ds, dp, dt = dist(sleeve), dist(paw), dist(tool_fill)
    edge = ink | ((im[..., 3] > 0) & (im[..., 3] <= 160))
    paw_outline = edge & (dp < 12) & (dp <= ds) & (dp <= dt)
    drop = sleeve | (edge & (ds < 16) & (ds < dp) & (ds < dt))
    # The gripping paw (with its outline), recoloured: the 주인공's paw colour, this paw's shading.
    mine, hs, hp, colour = hero_arm(arm)
    rgb = im[..., :3].astype(float)
    shade = luma(rgb) / max(luma(rgb[paw].mean(0)), 1.0)
    grip = im.copy()
    grip[..., :3][paw] = np.clip(colour * shade[paw][:, None], 0, 255).astype(np.uint8)
    grip[~(paw | paw_outline)] = 0
    tool = im.copy()
    tool[drop | paw | paw_outline] = 0
    # The 주인공's arm, shoulder on the shoulder and paw under the paw: turned, as wide as ARM_WIDTH,
    # as long as it takes (foreshortened when the paw comes towards us).
    ys, xs = np.nonzero(paw)
    target = np.array([xs.mean(), ys.mean()])
    reach = REACH.get(tag, 0)
    if reach:
        # A bigger canvas, tool and paw slid out along the arm.
        pad = reach + 4
        out_dir = (target - shoulder) / np.linalg.norm(target - shoulder) * reach
        shift = np.float32([[1, 0, pad + out_dir[0]], [0, 1, pad + out_dir[1]]])
        h, w = h + 2 * pad, w + 2 * pad
        tool = cv2.warpAffine(tool, shift, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
        grip = cv2.warpAffine(grip, shift, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
        shoulder = shoulder + pad
        target = target + pad + out_dir
    v0, v1 = hp - hs, target - shoulder
    stretch = np.linalg.norm(v1) / np.linalg.norm(v0)
    ang = np.arctan2(v1[1], v1[0]) - np.arctan2(v0[1], v0[0])
    u0 = v0 / np.linalg.norm(v0)
    n0 = np.array([-u0[1], u0[0]])
    # Scale ARM_WIDTH across the arm and `stretch` along it, about the shoulder, then turn.
    scale = np.outer(u0, u0) * stretch + np.outer(n0, n0) * ARM_WIDTH
    rot = np.array([[np.cos(ang), -np.sin(ang)], [np.sin(ang), np.cos(ang)]])
    lin = rot @ scale
    m = np.hstack([lin, (shoulder - lin @ hs)[:, None]])
    placed = cv2.warpAffine(mine, m, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
    out = Image.alpha_composite(Image.alpha_composite(Image.fromarray(tool), Image.fromarray(placed)), Image.fromarray(grip))
    out = np.array(out)
    # Crumbs left over from the cut.
    n, cl, st, _ = cv2.connectedComponentsWithStats((out[..., 3] > 0).astype(np.uint8), 8)
    for i in range(1, n):
        if st[i, 4] < 120:
            out[cl == i] = 0
    dst = HERO / f"front-{arm}-{tag}.png"
    Image.fromarray(out).save(dst)
    # The other arm's import settings (a guid of its own, kept once made); the pivot on the shoulder.
    meta_path = dst.with_name(dst.name + ".meta")
    meta = src.with_name(src.name + ".meta").read_text(encoding="utf-8")
    guid = re.search(r"^guid: (\w+)", meta_path.read_text(encoding="utf-8"), re.M).group(1) if meta_path.exists() else uuid.uuid4().hex
    meta = re.sub(r"^guid: \w+", f"guid: {guid}", meta, flags=re.M)
    meta = re.sub(r"spritePivot: \{x: [-\d.e]+, y: [-\d.e]+\}",
                  f"spritePivot: {{x: {shoulder[0] / w:.6f}, y: {1 - shoulder[1] / h:.6f}}}", meta)
    meta_path.write_text(meta, encoding="utf-8")
    print(f"{dst.name}: length x{stretch:.2f}, turn {np.degrees(ang):.0f} deg")


if __name__ == "__main__":
    for tag in CASES:
        build(tag)
