"""Idle images of the plaza cast cut from the reference picture, with new fur colours and cheek fur.

    python Tools/OtterIdleCast/from_reference.py

Source: ArtSource/Otter/IdleCast/reference/cast-reference.png (the user's picture, 1024x572).
For each otter (with what it sits on or works at):
  1. crop and enlarge x3 (smooth resampling, so the edits below aren't blocky);
  2. clear the background - grass, shadows, water and the bank, wherever they reach from the edge
     of the crop (the outlines keep them out of the otter and its props);
  3. recolour the fur (only the light pink-brown fur; blush, muzzle, wood and dark accents stay);
  4. cheek fur: trim the thin spikes into a rounder cheek (bigger tufts stay), redraw the outline
     where a spike was cut, and lighten the cheek fur a little.
Output: ArtSource/Otter/IdleCast/idle-<id>.png (transparent) and idle-cast-sheet.png.
"""
import pathlib

import cv2
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
SRC = ROOT / "ArtSource/Otter/IdleCast/reference/cast-reference.png"
OUT = ROOT / "ArtSource/Otter/IdleCast"
UP = 3
OUTLINE = np.array([52, 22, 14], np.float32)
FUR = np.array([230, 165, 139], np.float32)      # the reference's fur
CREAM = np.array([252, 240, 228], np.float32)

# box in the reference (x0, y0, x1, y1); cheek zones as (cx, cy, rx, ry) in reference px; fur colour.
CAST = {
    "painter": dict(box=(44, 22, 312, 282), cheeks=[(80, 116, 15, 17), (214, 106, 9, 13)], fur=(214, 150, 118)),
    "chef": dict(box=(432, 12, 668, 294), cheeks=[(496, 120, 15, 17), (606, 110, 9, 13)], fur=(232, 182, 136)),
    "explorer": dict(box=(62, 316, 298, 537), cheeks=[(99, 408, 13, 15)], fur=(200, 128, 98)),
    # The river bank runs behind these two and touches the dock / cap, so they also get an outline of
    # what to keep (the otter, its props, the rod and the fishing line), in reference px.
    "sailor": dict(box=(408, 304, 650, 570), cheeks=[(440, 396, 15, 17), (588, 384, 9, 13)], fur=(190, 150, 130),
                   keep=[(452, 306), (568, 306), (590, 350), (592, 401), (646, 397), (646, 418),
                         (598, 448), (605, 480), (616, 482), (616, 512), (560, 522), (502, 536), (500, 570), (468, 570), (466, 533),
                         (418, 509), (406, 482), (420, 458), (432, 455), (438, 400), (440, 345)],
                   # The fishing line runs through the bank's dirt, so it's redrawn rather than cut out.
                   line=[(639, 409), (637, 470), (636, 552)]),
    "sleepy": dict(box=(736, 316, 992, 542), cheeks=[(906, 392, 9, 13)], fur=(236, 190, 164),
                   keep=[(790, 326), (840, 316), (874, 326), (896, 350), (926, 378), (992, 468), (992, 530), (890, 546),
                         (760, 482), (740, 442), (746, 407), (759, 385), (772, 355)]),
}


def to_hsv(rgb):
    return cv2.cvtColor(np.clip(rgb, 0, 255).astype(np.uint8), cv2.COLOR_RGB2HSV).astype(np.float32)


def background(rgb):
    """Grass, its shadows, water, foam and the bank that connect to the crop's border."""
    hsv = to_hsv(rgb)
    h, s, v = hsv[..., 0] * 2, hsv[..., 1] / 255, hsv[..., 2] / 255
    luma = rgb @ np.array([.3, .59, .11], np.float32)
    green = (h > 70) & (h < 160) & (s > .2)
    blue = (h >= 160) & (h < 230) & (s > .15)
    pale = (s < .2) & (v > .7)                         # foam on the water
    bank = (h > 15) & (h < 45) & (s > .3) & (v > .45)  # the dirt edge of the bank
    # The bank's own outline is dark green; the drawing's outlines are dark brown, so they stay.
    dark_green = (h > 70) & (h < 170) & (s > .3)
    candidate = ((green | blue | pale | bank) & (luma > 60)) | dark_green
    n, labels = cv2.connectedComponents(candidate.astype(np.uint8), connectivity=4)
    edge = np.zeros_like(candidate)
    edge[0, :] = edge[-1, :] = edge[:, 0] = edge[:, -1] = True
    touching = set(np.unique(labels[edge & candidate])) - {0}
    # Some bank dirt sits against the props: only count it when it's mostly beside grass or water.
    return np.isin(labels, list(touching))


def keep_main(mask, share=.08):
    """Drop little islands (ripples, stray grass) not part of the picture's main pieces."""
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), 8)
    if n <= 1:
        return mask
    biggest = stats[1:, cv2.CC_STAT_AREA].max()
    keep = [i for i in range(1, n) if stats[i, cv2.CC_STAT_AREA] >= biggest * share]
    return np.isin(labels, keep)


def fur_mask(rgb):
    hsv = to_hsv(rgb)
    h, s, v = hsv[..., 0] * 2, hsv[..., 1] / 255, hsv[..., 2] / 255
    return (h > 11) & (h < 24) & (s > .27) & (s < .5) & (v > .72)


def recolour_fur(rgb, mask, target):
    ratio = rgb / FUR
    out = rgb.copy()
    out[mask] = np.clip(ratio[mask] * np.array(target, np.float32), 0, 255)
    return out


def cheeks(rgb, alpha, zones, box):
    """Trim thin cheek spikes, redraw the outline where one was cut, lighten the cheek fur."""
    x0, y0 = box[0], box[1]
    zone = np.zeros(alpha.shape, np.uint8)
    for cx, cy, rx, ry in zones:
        cv2.ellipse(zone, ((cx - x0) * UP, (cy - y0) * UP), (rx * UP, ry * UP), 0, 0, 360, 1, -1)
    zone = zone.astype(bool)
    solid = (alpha > 127).astype(np.uint8)
    k = 4 * UP + 1
    opened = cv2.morphologyEx(solid, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))).astype(bool)
    cut = zone & solid.astype(bool) & ~opened
    alpha = alpha.copy()
    alpha[cut] = 0
    # New edge inside the zone: an outline band as thick as the drawing's.
    inner = cv2.erode(opened.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3 * UP, 3 * UP))).astype(bool)
    band = zone & opened & ~inner & cv2.dilate(cut.astype(np.uint8), np.ones((3 * UP, 3 * UP), np.uint8)).astype(bool)
    rgb = rgb.copy()
    rgb[band] = OUTLINE
    # Lighter cheek fur, fading out from the zone's middle.
    soft = cv2.GaussianBlur(zone.astype(np.float32), (0, 0), 3 * UP)[..., None] * .45
    fur = fur_mask(rgb)
    blend = rgb * (1 - soft) + CREAM * soft
    rgb[fur & zone] = blend[fur & zone]
    return rgb, alpha, int(cut.sum())


def defringe(rgb, alpha):
    """Edge pixels picked up green/blue from the background: give them the outline colour."""
    edge = (alpha > 0) & ~cv2.erode((alpha > 0).astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    hsv = to_hsv(rgb)
    tinted = edge & (hsv[..., 0] * 2 > 60) & (hsv[..., 0] * 2 < 240) & (hsv[..., 1] > 50)
    rgb = rgb.copy()
    rgb[tinted] = OUTLINE
    return rgb


def make(member, spec, source):
    x0, y0, x1, y1 = spec["box"]
    crop = source.crop((x0, y0, x1, y1))
    big = np.array(crop.resize((crop.width * UP, crop.height * UP), Image.LANCZOS)).astype(np.float32)
    bg = background(big)
    solid = ~bg
    if "keep" in spec:
        area = np.zeros(solid.shape, np.uint8)
        pts = np.array([((x - x0) * UP, (y - y0) * UP) for x, y in spec["keep"]], np.int32)
        cv2.fillPoly(area, [pts], 1)
        solid &= area.astype(bool)
    # Inside a keep outline everything left is wanted, thin bits like the fishing line included.
    solid = keep_main(solid, .001 if "keep" in spec else .08)
    alpha = (solid * 255).astype(np.uint8)
    rgb = recolour_fur(big, fur_mask(big) & solid, spec["fur"])
    rgb, alpha, cut = cheeks(rgb, alpha, spec["cheeks"], spec["box"])
    rgb = defringe(rgb, alpha)
    if "line" in spec:
        layer = np.zeros(alpha.shape, np.uint8)
        pts = np.array([((x - x0) * UP, (y - y0) * UP) for x, y in spec["line"]], np.int32)
        cv2.polylines(layer, [pts], False, 255, thickness=2 * UP - 1, lineType=cv2.LINE_AA)
        drawn = layer > 0
        mix = (layer.astype(np.float32) / 255)[..., None]
        rgb[drawn] = (rgb * (1 - mix) + np.array([64, 52, 46], np.float32) * mix)[drawn]
        alpha = np.maximum(alpha, layer)
    alpha = cv2.GaussianBlur(alpha, (0, 0), .7)
    out = np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha])
    image = Image.fromarray(out)
    image = image.crop(image.getchannel("A").getbbox())
    image.save(OUT / f"idle-{member}.png", optimize=True)
    print(f"idle-{member}.png {image.width}x{image.height}, cheek pixels trimmed {cut}")
    return image


def main():
    source = Image.open(SRC).convert("RGB")
    images = [make(member, spec, source) for member, spec in CAST.items()]
    height = max(im.height for im in images)
    sheet = Image.new("RGBA", (sum(im.width for im in images) + 30 * (len(images) + 1), height + 60), (232, 237, 223, 255))
    x = 30
    for im in images:
        sheet.alpha_composite(im, (x, 30 + height - im.height))
        x += im.width + 30
    sheet.save(OUT / "idle-cast-sheet.png", optimize=True)


if __name__ == "__main__":
    main()
