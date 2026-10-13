"""Extra V14 rig art for dozing: a closed-eye front head and a "z" for the Zzz.

Reads the cropped head part (crop_unity_parts.py output), paints the open eyes
over with the surrounding fur (inpaint) and draws sleepy eyelid arcs in the
outline colour. Same size and pivot as front-head, so QuarterRigView can swap
the sprite in place. Rerun after export-unity.cjs (it rebuilds front-head).
"""
import pathlib
import re
import uuid

import cv2
import numpy as np
from PIL import Image, ImageDraw

OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets/Resources/QuarterRigV14"
OUTLINE = (48, 16, 7)  # #301007, the exporter's outline brown
CREAM = (250, 236, 206)


def eye_components(rgba):
    """The two big dark blobs in the lower half of the face (eyes; the nose and mouth are smaller/flatter)."""
    rgb = rgba[:, :, :3].astype(float)
    luma = rgb[:, :, 0] * .3 + rgb[:, :, 1] * .59 + rgb[:, :, 2] * .11
    dark = ((luma < 70) & (rgba[:, :, 3] > 200)).astype(np.uint8)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(dark, 8)
    h = rgba.shape[0]
    eyes = [i for i in range(1, n)
            if stats[i, 1] > h * .5 and 1000 < stats[i, 4] < 5000 and stats[i, 3] > stats[i, 2]]
    if len(eyes) != 2:
        raise SystemExit(f"expected 2 eyes in front-head, found {len(eyes)}")
    return labels, [stats[i] for i in eyes], eyes


def sleep_head():
    src = OUT / "front-head.png"
    rgba = np.array(Image.open(src).convert("RGBA"))
    labels, stats, ids = eye_components(rgba)

    mask = np.zeros(labels.shape, np.uint8)
    for i in ids:
        blob = (labels == i).astype(np.uint8)
        # Fill the highlight holes, then grow past the anti-aliased edge.
        contours, _ = cv2.findContours(blob, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        cv2.drawContours(mask, contours, -1, 1, cv2.FILLED)
    mask = cv2.dilate(mask, np.ones((7, 7), np.uint8))

    # Paint the eyes over with the nearest skin outside them (fur stays fur, muzzle stays cream;
    # inpainting pulled in pale streaks), then soften the fill so no seam shows.
    out = rgba.copy()
    _, nearest = cv2.distanceTransformWithLabels(mask, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
    outside = mask == 0
    lut = np.zeros((nearest.max() + 1, 3), np.float32)
    lut[nearest[outside]] = rgba[:, :, :3][outside]
    fill = lut[nearest]
    fill = cv2.GaussianBlur(fill, (0, 0), 3)
    soft = cv2.GaussianBlur(cv2.dilate(mask, np.ones((3, 3), np.uint8)).astype(np.float32), (0, 0), 1.5)[:, :, None]
    out[:, :, :3] = (rgba[:, :, :3] * (1 - soft) + fill * soft).astype(np.uint8)

    # Closed eyes: gentle downward arcs across where each eye was, a little below its middle.
    image = Image.fromarray(out)
    big = image.resize((image.width * 4, image.height * 4), Image.LANCZOS)
    draw = ImageDraw.Draw(big)
    for x, y, w, h, _ in stats:
        cx, cy = (x + w / 2) * 4, (y + h * .58) * 4
        rx, ry = w * .66 * 4, h * .24 * 4
        draw.arc((cx - rx, cy - ry, cx + rx, cy + ry), 15, 165, fill=OUTLINE + (255,), width=int(10 * 4))
    image = big.resize(image.size, Image.LANCZOS)
    # Keep the original silhouette exactly (the arcs stay inside the face).
    result = np.array(image)
    result[:, :, 3] = rgba[:, :, 3]
    dst = OUT / "front-head-sleep.png"
    Image.fromarray(result).save(dst, optimize=True)
    copy_meta(OUT / "front-head.png.meta", dst)
    print(f"{dst.name}: eyes at {[(int(s[0]), int(s[1])) for s in stats]}")


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
    sleep_head()
    z_letter()
