"""The farmer's head (QuarterRigV14) without its straw hat: the cast rigs' default head.

The farmer's head art has the hat drawn in, so only the face below the brim is the farmer's own:
everything above a seam row is redrawn as a plain fur dome (same outline weight and fur colour)
with round ears behind it, in the farmer's ear colours. Front, sleeping front and rear heads keep
the farmer's sprite size and pivot, so they sit on the neck like his. The cast's hats cover most
of the dome anyway (costumes.js).

    python Tools/OtterCastRig/farmer_head.py     # writes ArtSource/Otter/CastRig/farmer-head/*.png to look at
"""
import pathlib

import cv2
import numpy as np
from PIL import Image, ImageDraw


ROOT = pathlib.Path(__file__).resolve().parents[2]
FARMER = ROOT / "Assets/Resources/QuarterRigV14"
OUTLINE = (32, 4, 0)
EAR = (124, 68, 48)
EAR_INNER = (250, 228, 200)
EAR_SHADE = (92, 46, 32)
LINE = 9            # outline width, image px
SS = 4              # supersampling for the drawn shapes
BLEND = 18          # rows over which the farmer's face fades in
# Base row (where the dome meets the face's sides), dome height above it (front and rear tops level
# on the neck) and ears (centre x, centre y above the base, radius, inner).
VIEWS = {
    "front": dict(base=278, dome=160, ears=[(122, 118, 54, True), (430, 134, 48, False)]),
    "rear": dict(base=304, dome=194, ears=[(96, 150, 54, False), (436, 150, 54, False)]),
}


def eye_blobs(rgba):
    """(mask, boxes) of the two eyes: big dark blobs in the lower part of the head."""
    h = rgba.shape[0]
    rgb = rgba[:, :, :3].astype(int)
    luma = rgb[:, :, 0] * .3 + rgb[:, :, 1] * .59 + rgb[:, :, 2] * .11
    dark = ((luma < 70) & (rgba[:, :, 3] > 200)).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(dark, 8)
    eyes = [i for i in range(1, n) if stats[i, 1] > h * .35 and 1000 < stats[i, 4] < 12000 and stats[i, 3] > stats[i, 2]]
    mask = cv2.dilate(np.isin(labels, eyes).astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    return mask, [stats[i] for i in eyes]


def seam_curve(rgba, base):
    """Per column, the first row of the farmer's own face: the base row, but above an eye that
    reaches higher (the far eye sits right under the brim)."""
    seam = np.full(rgba.shape[1], base)
    for x0, y0, bw, bh, area in eye_blobs(rgba)[1]:
        seam[max(0, x0 - 8):x0 + bw + 8] = np.minimum(seam[max(0, x0 - 8):x0 + bw + 8], y0 - 4)
    return seam


def fur_colour(rgba, seam):
    """Median fur just below the seam (not outline, muzzle or cheek)."""
    band = rgba[seam + 4:seam + 24].reshape(-1, 4).astype(int)
    rgb = band[:, :3]
    fur = band[(band[:, 3] > 250) & (rgb.sum(1) > 250) & (rgb[:, 0] - rgb[:, 2] > 70) & (rgb[:, 1] < 170)]
    return tuple(int(v) for v in np.median(fur[:, :3], axis=0))


def dome_span(rgba, base):
    """Centre x and half width of the head's outline at the base row (where the dome is drawn from)."""
    xs = np.where(rgba[base, :, 3] > 128)[0]
    left, right = int(xs.min()), int(xs.max())
    return (left + right) / 2, (right - left) / 2


def bare(rgba, view, seam):
    spec = VIEWS[view]
    h, w = rgba.shape[:2]
    base = spec["base"]
    cx, rx = dome_span(rgba, base)
    fur = fur_colour(rgba, base + 30)

    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)

    def ellipse(x, y, rx_, ry_, fill):
        d.ellipse([(x - rx_) * SS, (y - ry_) * SS, (x + rx_) * SS, (y + ry_) * SS], fill=fill)

    # Ears first, the dome covers their lower half.
    for ex, up, r, inner in spec["ears"]:
        ey = base - up
        ellipse(ex, ey, r, r, OUTLINE + (255,))
        ellipse(ex, ey, r - LINE, r - LINE, EAR + (255,))
        if inner:
            ellipse(ex + r * .18, ey + r * .14, r * .5, r * .56, EAR_INNER + (255,))
        else:
            ellipse(ex + (r * .12 if ex < cx else -r * .12), ey + r * .1, r * .48, r * .54, EAR_SHADE + (255,))
    # Dome: upper half of an ellipse through the face's outline at the base row.
    dome = spec["dome"]
    ellipse(cx, base, rx, dome, OUTLINE + (255,))
    ellipse(cx, base, rx - LINE, dome - LINE, fur + (255,))
    top = np.array(big.resize((w, h), Image.LANCZOS))
    top[base + BLEND + 1:] = 0
    shell = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(shell).ellipse([(cx - rx) * SS, (base - dome) * SS, (cx + rx) * SS, (base + dome) * SS], fill=255)
    shell = np.array(shell.resize((w, h), Image.LANCZOS)).astype(np.float32) / 255
    shell[base + BLEND + 1:] = 0

    # The farmer's face below the seam curve, fading in over BLEND rows (the outline sides line up).
    face = rgba.astype(np.float32).copy()
    # The brim's shadow on the fur just under it goes: darker fur there becomes plain fur.
    rgb = face[:, :, :3]
    luma = rgb @ np.array([.3, .59, .11], np.float32)
    shade = (np.linalg.norm(rgb - np.array(fur, np.float32), axis=2) < 60) & (luma > 70)
    shade &= luma < np.dot(fur, [.3, .59, .11]) + 4
    shade[base + 36:] = False
    rgb[shade] = np.array(fur, np.float32)
    seam = cv2.GaussianBlur(seam.astype(np.float32)[None, :], (0, 0), 10)[0]
    ramp = np.clip((np.arange(h)[:, None] - seam[None, :]) / BLEND, 0, 1)
    ramp[eye_blobs(rgba)[0]] = 1   # the eyes stay whole
    # Outside the dome (the cheek fluff sticking out) nothing to fade into: the face starts at the seam.
    ramp = np.where(shell < .5, (ramp > 0).astype(np.float32), ramp)
    face[:, :, 3] *= ramp
    out = Image.fromarray(top)
    out.alpha_composite(Image.fromarray(face.astype(np.uint8)))
    return mend_outline(np.array(out), np.maximum(shell, face[:, :, 3] / 255), base, eye_blobs(rgba)[0], fur)


def mend_outline(rgba, silhouette, base, eyes, fur):
    """Where the dome meets the farmer's face the two outlines don't line up (the dome's side runs on
    into the cheek fluff, bits of the brim's outline stay): around the seam, dark lines inside the head
    become fur, and the outline is drawn again along the joined silhouette (head only: the ears keep theirs)."""
    h = rgba.shape[0]
    inside = (silhouette > .5).astype(np.uint8)
    dist = cv2.distanceTransform(inside, cv2.DIST_L2, 5)
    rows = np.arange(h)[:, None]
    zone = np.clip(np.minimum(rows - (base - 70), (base + 30) - rows) / 10, 0, 1).astype(np.float32)
    rgb = rgba[:, :, :3].astype(np.float32)
    luma = rgb @ np.array([.3, .59, .11], np.float32)
    stray = (luma < 90) & (dist > LINE + 3) & (zone > 0) & ~eyes & (rows < base + 16)   # (the nose is lower)
    stray = cv2.GaussianBlur(cv2.dilate(stray.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(np.float32), (0, 0), 1)[..., None]
    out = rgba.copy()
    out[:, :, :3] = (rgb * (1 - stray) + np.array(fur, np.float32) * stray).astype(np.uint8)
    band = cv2.GaussianBlur(np.clip(LINE + .5 - dist, 0, 1) * inside, (0, 0), .6) * zone
    out[:, :, :3] = (out[:, :, :3] * (1 - band[..., None]) + np.array(OUTLINE, np.float32) * band[..., None]).astype(np.uint8)
    return out


def heads():
    load = lambda name: np.array(Image.open(FARMER / f"{name}.png").convert("RGBA"))
    front, rear = load("front-head"), load("rear-head")
    # The sleeping head uses the awake one's seam, so both swap without a jump.
    seam = seam_curve(front, VIEWS["front"]["base"])
    return {"front-head": bare(front, "front", seam),
            "front-head-sleep": bare(load("front-head-sleep"), "front", seam),
            "rear-head": bare(rear, "rear", seam_curve(rear, VIEWS["rear"]["base"]))}


def main():
    out = ROOT / "ArtSource/Otter/CastRig/farmer-head"
    out.mkdir(parents=True, exist_ok=True)
    for name, image in heads().items():
        Image.fromarray(image).save(out / f"{name}.png")
        print(name, image.shape[1], "x", image.shape[0])


if __name__ == "__main__":
    main()
