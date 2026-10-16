"""A plain V14 otter head for the idle cast, in the farmer's and waiter's own line art.

The waiter's head (Resources/WaiterRigV14/front-head.png) without its hair strands, with the
farmer's round eyes (Resources/QuarterRigV14/front-head.png) in place of its half-lidded ones.
Also a closed-eye version for the sleepy otter. Output: ArtSource/Otter/IdleCast/v14/base/.
"""
import pathlib
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[2]
WAITER = ROOT / "Assets/Resources/WaiterRigV14"
FARMER = ROOT / "Assets/Resources/QuarterRigV14"
OUT = ROOT / "ArtSource/Otter/IdleCast/v14/base"
sys.path.insert(0, str(ROOT / "Tools/QuarterRig"))
import make_sleep_parts  # noqa: E402

# Farmer eyes come out a little bigger on the waiter's wider face, and sit a touch lower than
# the waiter's lidded eyes did.
EYE_SCALE = 1.3
EYE_DROP = 10


def dark_components(rgba, luma_below):
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb @ np.array([.3, .59, .11], np.float32)
    dark = ((luma < luma_below) & (rgba[:, :, 3] > 200)).astype(np.uint8)
    return cv2.connectedComponentsWithStats(dark, 8)


def filled(labels, ids, grow):
    mask = np.zeros(labels.shape, np.uint8)
    for i in ids:
        contours, _ = cv2.findContours((labels == i).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        cv2.drawContours(mask, contours, -1, 1, cv2.FILLED)
    return cv2.dilate(mask, np.ones((grow, grow), np.uint8))


def farmer_eyes():
    """Each farmer eye as an RGBA patch (dark iris + highlight, soft edge) and its size."""
    head = np.array(Image.open(FARMER / "front-head.png").convert("RGBA"))
    n, labels, stats, _ = dark_components(head, 70)
    h = head.shape[0]
    eyes = [i for i in range(1, n) if stats[i, 1] > h * .35 and 1000 < stats[i, 4] < 5000 and stats[i, 3] > stats[i, 2]]
    patches = []
    for i in sorted(eyes, key=lambda i: stats[i, 0]):
        x, y, w, hh, _ = stats[i]
        mask = filled(labels, [i], 3).astype(np.float32)
        mask = cv2.GaussianBlur(mask, (0, 0), .8)
        pad = 4
        crop = head[y - pad:y + hh + pad, x - pad:x + w + pad].copy()
        crop[:, :, 3] = (np.clip(mask[y - pad:y + hh + pad, x - pad:x + w + pad], 0, 1) * 255).astype(np.uint8)
        patches.append(Image.fromarray(crop))
    return patches


def bare_head():
    head = np.array(Image.open(WAITER / "front-head.png").convert("RGBA"))
    h = head.shape[0]
    n, labels, stats, _ = dark_components(head, 110)
    dist = cv2.distanceTransform((head[:, :, 3] > 128).astype(np.uint8), cv2.DIST_L2, 5)
    # Hair: the big line drawing floating inside the head (not the outline, ears, eyes, nose or mouth).
    hair = [i for i in range(1, n) if stats[i, 4] > 3000 and stats[i, 1] < h * .3 and dist[labels == i].max() > 150]
    eyes = [i for i in range(1, n) if stats[i, 1] > h * .35 and 1000 < stats[i, 4] < 12000 and stats[i, 3] > stats[i, 2]]
    assert len(hair) == 1 and len(eyes) == 2, (hair, eyes)

    out = make_sleep_parts.fill_nearest(head, filled(labels, hair, 7))
    out = make_sleep_parts.fill_by_shapes(out, filled(labels, eyes, 7))

    image = Image.fromarray(out)
    for patch, i in zip(farmer_eyes(), sorted(eyes, key=lambda i: stats[i, 0])):
        x, y, w, hh, _ = stats[i]
        patch = patch.resize((round(patch.width * EYE_SCALE), round(patch.height * EYE_SCALE)), Image.LANCZOS)
        cx, cy = x + w / 2, y + hh / 2 + EYE_DROP
        image.alpha_composite(patch, (round(cx - patch.width / 2), round(cy - patch.height / 2)))
    result = np.array(image)
    result[:, :, 3] = head[:, :, 3]
    return result


# ---- outfits: the waiter's vest / shirt / bowtie / sleeves recoloured, keeping their shading ----
# Colour groups of the waiter's body and arms (k-means over their pixels).
OUTLINE = (50, 10, 0)
VEST = (80, 43, 31)
FUR = (213, 159, 133)
WHITE = (251, 243, 234)
BOW = (236, 137, 62)
PAW = (113, 64, 47)
# Above this row of front-body.png the white is the shirt collar; below it, the belly.
SHIRT_ROWS = 140

OUTFITS = {
    #            vest (smock/jacket), shirt collar, bowtie / scarf, sleeves, vest pattern
    "painter": dict(vest=(243, 230, 207), shirt=(251, 247, 240), bow=(214, 83, 74), sleeve=(243, 230, 207), pattern="paint"),
    "chef": dict(vest=(252, 250, 245), shirt=(252, 250, 245), bow=(214, 83, 74), sleeve=(252, 250, 245), pattern="buttons"),
    "explorer": dict(vest=(196, 168, 108), shirt=(250, 246, 236), bow=(111, 143, 78), sleeve=(250, 246, 236), pattern="pockets"),
    "sailor": dict(vest=(250, 248, 242), shirt=(250, 248, 242), bow=(214, 83, 74), sleeve=(250, 248, 242), pattern="stripes"),
    "sleepy": dict(vest=(150, 186, 232), shirt=(150, 186, 232), bow=(150, 186, 232), sleeve=(150, 186, 232), pattern="dots"),
}
NAVY = (47, 95, 167)


def luma(c):
    return c[..., 0] * .3 + c[..., 1] * .59 + c[..., 2] * .11 if isinstance(c, np.ndarray) else c[0] * .3 + c[1] * .59 + c[2] * .11


def nearest_group(rgb, groups):
    d = np.stack([np.linalg.norm(rgb - np.array(g, np.float32), axis=2) for g in groups], axis=2)
    return np.argmin(d, axis=2)


def tint(rgb, where, source, target):
    """Recolour pixels of one group to the target colour, keeping their shading relative to the source."""
    ratio = (luma(rgb) / max(luma(source), 1))[..., None]
    rgb[where] = np.clip(np.array(target, np.float32) * ratio, 0, 255)[where]


def pattern_layer(kind, size):
    """Pattern colours (RGBA) to lay over the vest area, in body.png pixels."""
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    w, h = size
    if kind == "stripes":
        for y in range(18, h, 26):
            draw.rectangle((0, y, w, y + 11), fill=NAVY + (255,))
    elif kind == "dots":
        for row, y in enumerate(range(30, h, 34)):
            for x in range(18 + (row % 2) * 17, w, 34):
                draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(248, 250, 255, 255))
    elif kind == "paint":
        for x, y, c in [(70, 110, (228, 107, 92)), (208, 70, (79, 138, 214)), (226, 128, (242, 193, 75)), (96, 60, (124, 181, 90)), (180, 140, (228, 107, 92))]:
            draw.ellipse((x - 9, y - 7, x + 9, y + 7), fill=c + (255,))
    elif kind == "buttons":
        for x in (178, 214):
            for y in (86, 116, 146):
                draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill=OUTLINE + (255,))
                draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(236, 228, 210, 255))
    elif kind == "pockets":
        for x in (60, 196):
            draw.rounded_rectangle((x, 104, x + 44, 140), radius=8, fill=OUTLINE + (255,))
            draw.rounded_rectangle((x + 4, 108, x + 40, 136), radius=6, fill=(186, 158, 98, 255))
            draw.line((x + 4, 118, x + 40, 118), fill=OUTLINE + (255,), width=4)
    return np.array(layer)


def dress_body(outfit):
    body = np.array(Image.open(WAITER / "front-body.png").convert("RGBA"))
    rgb = body[:, :, :3].astype(np.float32)
    alpha = body[:, :, 3]
    group = nearest_group(rgb, [OUTLINE, VEST, FUR, WHITE, BOW])
    rows = np.arange(body.shape[0])[:, None]
    vest = (group == 1) & (alpha > 0)
    tint(rgb, vest, VEST, outfit["vest"])
    tint(rgb, (group == 3) & (rows < SHIRT_ROWS), WHITE, outfit["shirt"])
    tint(rgb, group == 4, BOW, outfit["bow"])
    # Pattern only on the vest itself (shrunk a little so it stays inside the lines), shaded like it.
    inside = cv2.erode(vest.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    layer = pattern_layer(outfit["pattern"], (body.shape[1], body.shape[0]))
    paint = inside & (layer[:, :, 3] > 0)
    shade = (luma(body[:, :, :3].astype(np.float32)) / luma(VEST))[..., None]
    rgb[paint] = np.clip(layer[:, :, :3].astype(np.float32) * np.clip(shade, .6, 1.15), 0, 255)[paint]
    out = body.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def dress_arm(name, outfit):
    arm = np.array(Image.open(WAITER / f"front-{name}.png").convert("RGBA"))
    rgb = arm[:, :, :3].astype(np.float32)
    group = nearest_group(rgb, [OUTLINE, WHITE, FUR, PAW])
    tint(rgb, group == 1, WHITE, outfit["sleeve"])
    if outfit["pattern"] == "stripes":
        sleeve = (group == 1) & (arm[:, :, 3] > 0)
        rows = np.arange(arm.shape[0])[:, None] + np.zeros((1, arm.shape[1]), int)
        band = sleeve & (((rows - 4) % 26) < 11)
        rgb[band] = np.array(NAVY, np.float32)
    out = arm.copy()
    out[:, :, :3] = rgb.astype(np.uint8)
    return out


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    head = bare_head()
    Image.fromarray(head).save(OUT / "head.png", optimize=True)
    make_sleep_parts.EYE_LUMA = 70
    Image.fromarray(make_sleep_parts.close_eyes(head)).save(OUT / "head-sleep.png", optimize=True)
    for member, outfit in OUTFITS.items():
        Image.fromarray(dress_body(outfit)).save(OUT / f"{member}-body.png", optimize=True)
        for arm in ("nearArm", "farArm"):
            Image.fromarray(dress_arm(arm, outfit)).save(OUT / f"{member}-{arm}.png", optimize=True)
    print(f"base head and {len(OUTFITS)} outfits written")


if __name__ == "__main__":
    main()
