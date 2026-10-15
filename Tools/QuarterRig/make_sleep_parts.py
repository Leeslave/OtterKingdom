"""Extra V14 rig art for dozing: a closed-eye front head and a "z" for the Zzz.

Reads the cropped head part (crop_unity_parts.py output), paints the open eyes
over with the surrounding fur (inpaint) and draws sleepy eyelid arcs in the
outline colour. Same size and pivot as front-head, so QuarterRigView can swap
the sprite in place. Rerun after export-unity.cjs (it rebuilds front-head).

    python Tools/QuarterRig/make_sleep_parts.py                                  # farmer (QuarterRigV14)
    python Tools/QuarterRig/make_sleep_parts.py --rig WaiterRigV14 --eye-luma 110 --fill shapes  # waiter
"""
import argparse
import pathlib
import re
import uuid

import cv2
import numpy as np
from PIL import Image, ImageDraw

RESOURCES = pathlib.Path(__file__).resolve().parents[2] / "Assets/Resources"
OUT = RESOURCES / "QuarterRigV14"
EYE_LUMA = 70
FILL = "nearest"
DETACH = 0   # >0: open the dark mask with a disc this wide first (glasses touching an eye)
LID_WIDTH = 10   # closed-eye line, sprite px
OPEN_EYES = 2   # 1 for a face that already has one eye shut (the 작업반장's scarred wink)
OUTLINE = (48, 16, 7)  # #301007, the exporter's outline brown
CREAM = (250, 236, 206)


def eye_components(rgba):
    """The two big dark blobs in the lower half of the face (eyes; the nose and mouth are smaller/flatter)."""
    rgb = rgba[:, :, :3].astype(float)
    luma = rgb[:, :, 0] * .3 + rgb[:, :, 1] * .59 + rgb[:, :, 2] * .11
    dark = ((luma < EYE_LUMA) & (rgba[:, :, 3] > 200)).astype(np.uint8)
    if DETACH:
        # Thin lines touching an eye (glasses) are opened away so the eye stands alone.
        dark = cv2.morphologyEx(dark, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (DETACH, DETACH)))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(dark, 8)
    h = rgba.shape[0]
    eyes = [i for i in range(1, n)
            if stats[i, 1] > h * .35 and 1000 < stats[i, 4] < 12000 and stats[i, 3] > .9 * stats[i, 2]]   # (round eyes too)
    # An ear's dark inner outline can pass too (chef); the eyes are the two biggest.
    eyes = sorted(eyes, key=lambda i: -stats[i, 4])[:2] if len(eyes) > 2 else eyes
    if len(eyes) != OPEN_EYES:
        raise SystemExit(f"expected {OPEN_EYES} open eyes in front-head, found {len(eyes)}")
    return labels, [stats[i] for i in eyes], eyes


def sleep_head():
    rgba = np.array(Image.open(OUT / "front-head.png").convert("RGBA"))
    result = close_eyes(rgba)
    dst = OUT / "front-head-sleep.png"
    Image.fromarray(result).save(dst, optimize=True)
    copy_meta(OUT / "front-head.png.meta", dst)


def smile_head():
    """front-head-smile.png: the eyes as happy upward arcs (^ ^), for smiling while it works (the 행상인)."""
    rgba = np.array(Image.open(OUT / "front-head.png").convert("RGBA"))
    result = close_eyes(rgba, smile=True)
    dst = OUT / "front-head-smile.png"
    Image.fromarray(result).save(dst, optimize=True)
    copy_meta(OUT / "front-head.png.meta", dst)


def close_eyes(rgba, smile=False):
    """The head (RGBA array) with its open eyes painted over and redrawn as closed lids."""
    labels, stats, ids = eye_components(rgba)

    mask = np.zeros(labels.shape, np.uint8)
    for i in ids:
        blob = (labels == i).astype(np.uint8)
        # Fill the highlight holes, then grow past the anti-aliased edge.
        contours, _ = cv2.findContours(blob, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        cv2.drawContours(mask, contours, -1, 1, cv2.FILLED)
    mask = cv2.dilate(mask, np.ones((7, 7), np.uint8))

    if FILL == "split":
        out = fill_split(rgba, mask)
    elif FILL == "shapes":
        out = fill_by_shapes(rgba, mask)
    else:
        out = fill_nearest(rgba, mask)

    # Closed eyes: gentle downward arcs across where each eye was, a little below its middle.
    image = Image.fromarray(out)
    big = image.resize((image.width * 4, image.height * 4), Image.LANCZOS)
    draw = ImageDraw.Draw(big)
    for x, y, w, h, _ in stats:
        if smile:
            # Happy eyes: arcs bowed upwards, a little above the eye's middle.
            cx, cy = (x + w / 2) * 4, (y + h * .62) * 4
            rx, ry = w * .8 * 4, h * .42 * 4
            draw.arc((cx - rx, cy - ry, cx + rx, cy + ry), 195, 345, fill=OUTLINE + (255,), width=int(LID_WIDTH * 1.25 * 4))
            continue
        cx, cy = (x + w / 2) * 4, (y + h * .58) * 4
        rx, ry = w * .66 * 4, h * .24 * 4
        draw.arc((cx - rx, cy - ry, cx + rx, cy + ry), 15, 165, fill=OUTLINE + (255,), width=int(LID_WIDTH * 4))
    image = big.resize(image.size, Image.LANCZOS)
    # Keep the original silhouette exactly (the arcs stay inside the face).
    result = np.array(image)
    result[:, :, 3] = rgba[:, :, 3]
    print(f"closed eyes at {[(int(s[0]), int(s[1])) for s in stats]}")
    return result


def fill_split(rgba, mask):
    """Flat fur above the muzzle's edge, flat cream below it: each painted pixel takes whichever of the two
    is nearest outside the eye, that map blurred so the muzzle's edge runs on smoothly (for an eye that sits
    on the edge, where blending colours leaves a haze)."""
    rgb = rgba[:, :, :3].astype(np.float32)
    inside = mask.astype(bool)
    opaque = rgba[:, :, 3] > 200
    luma = rgb @ np.array([.3, .59, .11], np.float32)
    cream = opaque & (luma > 200) & (rgb.max(axis=2) - rgb.min(axis=2) < 40)
    _, nearest = cv2.distanceTransformWithLabels(mask.astype(np.uint8), cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
    lut = np.zeros(nearest.max() + 1, np.float32)
    lut[nearest[~inside]] = cream[~inside]
    side = cv2.GaussianBlur(np.where(inside, lut[nearest], cream.astype(np.float32)), (0, 0), 5)
    below = np.clip((side - .5) * 4 + .5, 0, 1)[..., None]
    ring = cv2.dilate(mask.astype(np.uint8), np.ones((25, 25), np.uint8)).astype(bool) & ~inside & opaque
    pink = rgb[:, :, 0] - rgb[:, :, 1] > 90
    fur = np.median(rgb[ring & ~cream & ~pink & (luma > 70)], axis=0)
    cream_colour = np.median(rgb[ring & cream], axis=0)
    paint = below * cream_colour + (1 - below) * fur
    soft = cv2.GaussianBlur(inside.astype(np.float32), (0, 0), 1.2)[..., None]
    out = rgba.copy()
    out[:, :, :3] = np.clip(rgb * (1 - soft) + paint * soft, 0, 255).astype(np.uint8)
    return out


def fill_nearest(rgba, mask):
    """Paint the eyes over with the nearest skin outside them (fur stays fur, muzzle stays cream;
    inpainting pulled in pale streaks), then soften the fill so no seam shows."""
    out = rgba.copy()
    _, nearest = cv2.distanceTransformWithLabels(mask, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
    outside = mask == 0
    lut = np.zeros((nearest.max() + 1, 3), np.float32)
    lut[nearest[outside]] = rgba[:, :, :3][outside]
    fill = lut[nearest]
    fill = cv2.GaussianBlur(fill, (0, 0), 3)
    soft = cv2.GaussianBlur(cv2.dilate(mask, np.ones((3, 3), np.uint8)).astype(np.float32), (0, 0), 1.5)[:, :, None]
    out[:, :, :3] = (rgba[:, :, :3] * (1 - soft) + fill * soft).astype(np.uint8)
    return out


def fill_by_shapes(rgba, mask):
    """For eyes that cover the edge of the muzzle or a blush: rebuild those hidden edges.

    The skin around the eyes is grouped by colour (dark line work dropped); the most common group
    (fur) is the base, and each other group (muzzle cream, blush pink) gets an ellipse fitted to its
    visible outline. Inside the eyes each pixel takes the colour of the topmost shape it falls in,
    so the muzzle's curve carries on smoothly behind where the eye was.
    """
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    ring = (cv2.dilate(mask, np.ones((25, 25), np.uint8)) > 0) & (mask == 0) & (alpha > 250)
    samples = rgb[ring]
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, .5)
    _, groups, centres = cv2.kmeans(samples, 4, None, criteria, 5, cv2.KMEANS_PP_CENTERS)
    counts = np.bincount(groups.ravel(), minlength=4)
    keep = [g for g in np.argsort(-counts) if centres[g] @ np.array([.3, .59, .11]) > 100 and counts[g] > 50]

    fill = np.zeros_like(rgb)
    fill[:] = centres[keep[0]]
    near_eye = cv2.dilate(mask, np.ones((9, 9), np.uint8)) > 0
    # Each pixel belongs to the colour group it is closest to (a fixed tolerance let light fur pass as blush).
    distances = np.stack([np.linalg.norm(rgb - centres[g], axis=2) for g in range(len(centres))], axis=2)
    closest = np.argmin(distances, axis=2)
    for g in keep[1:]:
        region = (closest == g) & (distances[:, :, g] < 40) & (alpha > 200) & ~near_eye
        n, labels, stats, _ = cv2.connectedComponentsWithStats(region.astype(np.uint8), 8)
        touching = set(np.unique(labels[(cv2.dilate(mask, np.ones((31, 31), np.uint8)) > 0) & region])) - {0}
        shape = np.zeros(mask.shape, np.uint8)
        for i in touching:
            # A sliver cut off by the eye can't describe a shape.
            if stats[i, 4] < 600:
                continue
            contours, _ = cv2.findContours((labels == i).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
            points = np.vstack([c.reshape(-1, 2) for c in contours])
            # Only the outline away from the eye describes the shape; the cut along the eye doesn't.
            points = points[~near_eye[points[:, 1], points[:, 0]]]
            if len(points) < 5:
                continue
            ellipse = cv2.fitEllipse(points.astype(np.float32))
            # A fit much bigger than the piece it came from has run off; leave that edge to the fur.
            if np.pi * ellipse[1][0] * ellipse[1][1] / 4 > 2.5 * stats[i, 4]:
                continue
            cv2.ellipse(shape, ellipse, 1, cv2.FILLED)
        fill[shape > 0] = centres[g]

    fill = cv2.GaussianBlur(fill, (0, 0), 1.2)
    soft = cv2.GaussianBlur(cv2.dilate(mask, np.ones((3, 3), np.uint8)).astype(np.float32), (0, 0), 1.5)[:, :, None]
    out = rgba.copy()
    out[:, :, :3] = (rgb * (1 - soft) + fill * soft).astype(np.uint8)
    return out


def z_letter():
    """A chunky cream "z" with the outline brown, drawn big and scaled down for smooth edges."""
    size, k = 72, 4
    big = Image.new("RGBA", (size * k, size * k), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    m, s = 16 * k, (size - 16) * k
    stroke = [(m, m), (s, m), (m, s), (s, s)]
    draw.line(stroke, fill=OUTLINE + (255,), width=22 * k, joint="curve")
    for p in stroke:
        draw.ellipse((p[0] - 11 * k, p[1] - 11 * k, p[0] + 11 * k, p[1] + 11 * k), fill=OUTLINE + (255,))
    draw.line(stroke, fill=CREAM + (255,), width=11 * k, joint="curve")
    for p in stroke:
        draw.ellipse((p[0] - 5.5 * k, p[1] - 5.5 * k, p[0] + 5.5 * k, p[1] + 5.5 * k), fill=CREAM + (255,))
    dst = OUT / "fx-z.png"
    big.resize((size, size), Image.LANCZOS).save(dst, optimize=True)
    template = (OUT / "front-head.png.meta").read_text(encoding="utf-8")
    meta = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", "spritePivot: {x: 0.5, y: 0.5}", template)
    write_meta(meta, dst)
    print(f"{dst.name}: {size}x{size}")


def note_letter():
    """fx-note.png: a chunky cream eighth note with the outline brown, like the "z" (for the 음악가)."""
    size, k = 72, 4
    big = Image.new("RGBA", (size * k, size * k), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)

    def note(colour, grow):
        g = grow * k
        draw.ellipse((10 * k - g, 44 * k - g, 32 * k + g, 62 * k + g), fill=colour)                 # head
        draw.rectangle((27 * k - g, 12 * k - g, 33 * k + g, 54 * k + g), fill=colour)                # stem
        draw.polygon([(27 * k - g, 10 * k - g), (54 * k + g, 22 * k), (54 * k + g, 32 * k + g),       # flag
                      (33 * k - g, 22 * k + g)], fill=colour)
    note(OUTLINE + (255,), 5.5)
    note(CREAM + (255,), 0)
    dst = OUT / "fx-note.png"
    big.resize((size, size), Image.LANCZOS).save(dst, optimize=True)
    template = (OUT / "front-head.png.meta").read_text(encoding="utf-8")
    meta = re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}", "spritePivot: {x: 0.5, y: 0.5}", template)
    write_meta(meta, dst)
    print(f"{dst.name}: {size}x{size}")


def copy_meta(template_path, dst):
    write_meta(template_path.read_text(encoding="utf-8"), dst)


def write_meta(template, dst):
    """Same import settings as the template, under this file's own (kept or new) GUID."""
    meta = dst.with_name(dst.name + ".meta")
    old = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
    guid = old.group(1) if old else uuid.uuid4().hex
    text = re.sub(r"^guid: \w+", f"guid: {guid}", template.replace("\r\n", "\n"), flags=re.M)
    text = re.sub(r"spriteID: \w+", "spriteID: ", text)
    meta.write_text(text, encoding="utf-8", newline="\r\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--rig", default="QuarterRigV14", help="folder under Assets/Resources")
    parser.add_argument("--eye-luma", type=int, default=70, help="pixels darker than this count as eye")
    parser.add_argument("--detach", type=int, default=0, help="px: cut thin lines (glasses) off the eyes")
    parser.add_argument("--lid", type=float, default=10, help="closed-eye line width (match a drawn wink)")
    parser.add_argument("--fill", choices=["nearest", "shapes", "split"], default="nearest",
                        help="shapes: rebuild a muzzle/blush edge the eyes cover (waiter)")
    parser.add_argument("--note", action="store_true", help="also make fx-note.png (plays guitar when seated)")
    parser.add_argument("--smile", action="store_true", help="make front-head-smile.png (happy ^ ^ eyes) instead")
    parser.add_argument("--open-eyes", type=int, default=2, help="1 if the face already has one eye shut")
    args = parser.parse_args()
    OUT = RESOURCES / args.rig
    EYE_LUMA = args.eye_luma
    FILL = args.fill
    OPEN_EYES = args.open_eyes
    LID_WIDTH = args.lid
    DETACH = args.detach
    if args.smile:
        smile_head()
    else:
        sleep_head()
        z_letter()
        if args.note:
            note_letter()
