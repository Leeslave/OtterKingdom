"""Base parts for the cast rigs: the waiter's V14 body parts (front and rear), re-dressed per otter.

Every otter gets the farmer's face - his head without the straw hat (farmer_head.py) - and the
waiter's body line art, both in its own fur colour (the same as its idle image), and its clothes
from recolouring the waiter's vest / shirt / bowtie / sleeves - or, for the otters that wear none,
a plain furred body. Hats, aprons and held props are drawn on
top later (costumes.js), since they reach past the parts.
Output: ArtSource/Otter/CastRig/<id>/base/{front,rear}-<part>.png (+ front-head-sleep.png).
"""
import pathlib
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
WAITER = ROOT / "Assets/Resources/WaiterRigV14"
OUT = ROOT / "ArtSource/Otter/CastRig"
sys.path.insert(0, str(ROOT / "Tools/OtterIdleCast"))
sys.path.insert(0, str(ROOT / "Tools/QuarterRig"))
import make_base  # noqa: E402

import farmer_head  # noqa: E402

PARTS = ["head", "body", "nearArm", "farArm", "nearLeg", "farLeg", "tail"]
OUTLINE = (50, 10, 0)
FUR = (214, 159, 133)
VEST = (80, 43, 31)
WHITE = (251, 243, 234)
BOW = (236, 137, 62)
PAW = (113, 64, 47)
EAR = (121, 70, 52)
NAVY = (47, 95, 167)
RED = (219, 78, 72)
YELLOW = (236, 204, 92)
BLUE = (86, 140, 187)

# Fur colours are the idle images'; clothes: None = bare fur, else colours for the waiter's pieces.
CAST = {
    "painter": dict(fur=(214, 150, 118), clothes=None, sleepy=False),
    "explorer": dict(fur=(200, 128, 98), sleepy=False,
                     clothes=dict(vest=(108, 140, 76), shirt=(250, 246, 236), bow=(250, 246, 236), sleeve=(250, 246, 236),
                                  legs=(96, 128, 68))),
    "sleepy": dict(fur=(236, 190, 164), clothes=None, sleepy=True),
    # suit: the whole body is costume (fur below the vest too); vstripes: (colours, band) down the front.
    "clown": dict(fur=(196, 134, 98), sleepy=False,
                  clothes=dict(vest=RED, shirt=YELLOW, bow=RED, sleeve=RED, legs=RED, suit=True,
                               vstripes=((BLUE, YELLOW), (.50, .80)), sleeve_stripes=YELLOW)),
}


def groups(rgb, colours):
    d = np.stack([np.linalg.norm(rgb - np.array(c, np.float32), axis=2) for c in colours], axis=2)
    return np.argmin(d, axis=2)


def tint(rgb, where, source, target):
    ratio = (make_base.luma(rgb) / max(make_base.luma(source), 1))[..., None]
    rgb[where] = np.clip(np.array(target, np.float32) * ratio, 0, 255)[where]


def plain(rgba, colours, keep, keep_below=0.0, belly=False):
    """Clothes gone: the part's own outer outline, filled with fur, keeping only the colour groups in
    `keep` (the paw) from the original, and only below `keep_below` of the height (so the tones along a
    sleeve hem that look like paw don't stay). Lines drawn inside the part (vest edges, sleeve hems, the
    bowtie) go with the clothes. `belly` paints a soft cream belly like the farmer's."""
    alpha = rgba[:, :, 3]
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, colours)
    dist = cv2.distanceTransform((alpha > 128).astype(np.uint8), cv2.DIST_L2, 5)
    rim = (g == 0) & (dist <= 11)
    kept = rim.copy()
    low = np.arange(rgba.shape[0])[:, None] >= keep_below * rgba.shape[0]
    for k in keep:
        kept |= (g == k) & low
    out_rgb = np.empty_like(rgb)
    out_rgb[:] = np.array(FUR, np.float32)
    if belly:
        h, w = alpha.shape
        shape = np.zeros((h, w), np.float32)
        cv2.ellipse(shape, (w // 2, int(h * .8)), (int(w * .26), int(h * .2)), 0, 0, 360, 1, -1)
        shape = cv2.GaussianBlur(shape, (0, 0), 2)[..., None]
        out_rgb = out_rgb * (1 - shape) + np.array(WHITE, np.float32) * shape
    # Soften where kept art meets the new fur so no hard seam shows.
    soft = cv2.GaussianBlur(kept.astype(np.float32), (0, 0), .8)[..., None]
    out_rgb = out_rgb * (1 - soft) + rgb * soft
    out_rgb[kept] = rgb[kept]
    out = rgba.copy()
    out[:, :, :3] = np.clip(out_rgb, 0, 255).astype(np.uint8)
    return out


def inner_lines(rgba, group_is_outline, depth):
    """Dark lines drawn inside a part (vest edges, sleeve hems), at least `depth` px from its edge."""
    dist = cv2.distanceTransform((rgba[:, :, 3] > 128).astype(np.uint8), cv2.DIST_L2, 5)
    return group_is_outline & (dist > depth)


def bare(rgba, colours, to_fur, depth):
    """Clothes painted out to plain fur: chosen colour groups and the lines inside them become fur."""
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, colours)
    clothes = np.isin(g, to_fur) & (rgba[:, :, 3] > 0)
    lines = inner_lines(rgba, g == 0, depth)
    paint = clothes | lines
    out = make_base.make_sleep_parts.fill_nearest(rgba, cv2.dilate(lines.astype(np.uint8), np.ones((3, 3), np.uint8)))
    out_rgb = out[:, :, :3].astype(np.float32)
    out_rgb[clothes] = np.array(FUR, np.float32)
    out[:, :, :3] = out_rgb.astype(np.uint8)
    return out


# The farmer's head colours (farmer_head.py draws the dome and ears in these too).
HEAD_COLOURS = [farmer_head.OUTLINE, (216, 131, 95), (255, 234, 211), (254, 146, 110), farmer_head.EAR,
                farmer_head.EAR_INNER, farmer_head.EAR_SHADE]


def recolour_head(rgba, fur):
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, HEAD_COLOURS)
    tint(rgb, (g == 1) & (rgba[:, :, 3] > 0), HEAD_COLOURS[1], fur)
    out = rgba.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def recolour_fur(rgba, fur):
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, [OUTLINE, FUR, PAW, WHITE, VEST, BOW])
    tint(rgb, (g == 1) & (rgba[:, :, 3] > 0), FUR, fur)
    out = rgba.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def dressed(rgba, clothes, rows_for_shirt=None):
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, [OUTLINE, VEST, FUR, WHITE, BOW])
    vest = (g == 1) & (rgba[:, :, 3] > 0)
    tint(rgb, vest, VEST, clothes["vest"])
    white = g == 3
    if rows_for_shirt is not None:
        white &= np.arange(rgba.shape[0])[:, None] < rows_for_shirt
    tint(rgb, white, WHITE, clothes["shirt"])
    tint(rgb, g == 4, BOW, clothes["bow"])
    if clothes.get("suit"):
        # A one-piece suit: the fur and the cream belly below the vest are cloth as well.
        tint(rgb, ((g == 2) | ((g == 3) & ~white)) & (rgba[:, :, 3] > 0), FUR, clothes["vest"])
        vest |= ((g == 2) | (g == 3)) & (rgba[:, :, 3] > 0) & ~white
    if clothes.get("vstripes"):
        colours, (x0, x1) = clothes["vstripes"]
        inside = cv2.erode(vest.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
        cols = np.zeros(rgba.shape[:2], int) + np.arange(rgba.shape[1])[None, :]
        w = rgba.shape[1]
        band = (cols >= x0 * w) & (cols < x1 * w)
        stripe = (cols - int(x0 * w)) // 18
        for k, c in enumerate(colours):
            rgb[inside & band & (stripe % len(colours) == k)] = np.array(c, np.float32)
    if clothes.get("stripes"):
        inside = cv2.erode(vest.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
        rows = np.arange(rgba.shape[0])[:, None] + np.zeros((1, rgba.shape[1]), int)
        rgb[inside & (((rows - 18) % 26) < 11)] = np.array(NAVY, np.float32)
    out = rgba.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def arm(rgba, clothes):
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, [OUTLINE, WHITE, FUR, PAW])
    sleeve = (g == 1) & (rgba[:, :, 3] > 0)
    if clothes is None:
        return plain(rgba, [OUTLINE, WHITE, FUR, PAW], [3], keep_below=.6)
    tint(rgb, sleeve, WHITE, clothes["sleeve"])
    if clothes.get("stripes") or clothes.get("sleeve_stripes"):
        rows = np.arange(rgba.shape[0])[:, None] + np.zeros((1, rgba.shape[1]), int)
        rgb[sleeve & (((rows - 4) % 26) < 11)] = np.array(clothes.get("sleeve_stripes", NAVY), np.float32)
    out = rgba.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def leg(rgba, clothes):
    if clothes is None or "legs" not in clothes:
        return rgba
    rgb = rgba[:, :, :3].astype(np.float32)
    g = groups(rgb, [OUTLINE, FUR, PAW])
    # Trousers down to just above the paw.
    tint(rgb, g == 1, FUR, clothes["legs"])
    out = rgba.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def load(view, part):
    return np.array(Image.open(WAITER / f"{view}-{part}.png").convert("RGBA"))


def build(member, spec):
    out = OUT / member / "base"
    out.mkdir(parents=True, exist_ok=True)
    clothes = spec["clothes"]
    heads = {name: recolour_head(image, spec["fur"]) for name, image in farmer_head.heads().items()}
    parts = {}
    for view in ("front", "rear"):
        for part in PARTS:
            if part == "head":
                parts[f"{view}-head"] = heads["front-head-sleep" if view == "front" and spec["sleepy"] else f"{view}-head"]
                continue
            elif part == "body":
                image = load(view, part)
                if clothes is None:
                    image = plain(image, [OUTLINE, VEST, FUR, WHITE, BOW], [], belly=view == "front")
                else:
                    image = dressed(image, clothes, 140 if view == "front" else None)
            elif part.endswith("Arm"):
                image = arm(load(view, part), clothes)
            elif part.endswith("Leg"):
                image = leg(load(view, part), clothes)
            else:
                image = load(view, part)
            parts[f"{view}-{part}"] = recolour_fur(image, spec["fur"])
    parts["front-head-sleep"] = heads["front-head-sleep"]
    for name, image in parts.items():
        Image.fromarray(image).save(out / f"{name}.png", optimize=True)
    return parts


def main():
    for member, spec in CAST.items():
        build(member, spec)
        print(f"{member}: base parts")


if __name__ == "__main__":
    main()
