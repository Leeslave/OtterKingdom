"""Expression overlays for the customisable otter, taken from the user's expression sheet.

reference/expressions.png is a 3x3 grid of the basic otter (small) with nine expressions, in the
editor's order: basic, smile, excited / surprised, wink, angry / sleepy, sweat, love. Each cell is
lined up with the basic cell (same drawing, so a shift), and the basic cell with the base picture
(by the eyes), so every expression lands on the base head.

An overlay holds only what the expression changes, drawn over the head with its eyes and mouth
painted out: where the cell differs from the basic cell its pixels are used (upscaled), elsewhere the
base picture's own crisp eyes and mouth. Pixels that match the painted-out head stay transparent.
Hearts floating beside the otter (love) are kept too.
"""
import cv2
import numpy as np
from PIL import Image

import cutout

ORDER = ["basic", "smile", "excited", "surprised", "wink", "angry", "sleepy", "sweat", "love"]


def cells(sheet):
    """The nine cells, all the same size."""
    h, w = sheet.shape[:2]
    ch, cw = h // 3, w // 3
    return [sheet[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw] for r in range(3) for c in range(3)]


def shift_to(alpha, ref):
    """Translation lining `alpha` up with `ref` (silhouettes of the same drawing)."""
    (dx, dy), _ = cv2.phaseCorrelate(ref.astype(np.float32), alpha.astype(np.float32))
    return -dx, -dy


def ink(rgba):
    import build
    return cv2.GaussianBlur(((build.luma(rgba[..., :3].astype(np.float32)) < 90) & (rgba[..., 3] > 128)).astype(np.float32), (0, 0), 1)


def refine(cell, ref, dx, dy, reach=12):
    """The shift onto the basic cell, exact: the head's top outline and ears (never changed by an
    expression) matched against the basic cell's around the silhouette's guess."""
    a = (ref[..., 3] > 0).astype(np.uint8)
    x, y, w, h = cv2.boundingRect(a)
    top = ink(ref)[y:y + int(h * .32), x + int(w * .2):x + w]   # the head's top and ears (the tail is lower left)
    ox, oy = x + int(w * .2), y
    big = np.pad(ink(cell), reach + 2)
    best = None
    for sy in range(-reach, reach + 1):
        for sx in range(-reach, reach + 1):
            cx, cy = int(round(ox - dx)) + sx + reach + 2, int(round(oy - dy)) + sy + reach + 2
            patch = big[cy:cy + top.shape[0], cx:cx + top.shape[1]]
            if patch.shape != top.shape:
                continue
            score = float((patch * top).sum() / (np.sqrt((patch ** 2).sum() * (top ** 2).sum()) + 1e-6))
            if best is None or score > best[0]:
                best = (score, sx, sy)
    return dx - best[1], dy - best[2]


def nose_near(rgba, at):
    """Centre of the solid, wide dark blob nearest `at` (the nose: the same in every expression)."""
    import build
    rgb = rgba[..., :3].astype(np.float32)
    dark = ((build.luma(rgb) < 70) & (rgba[..., 3] > 200)).astype(np.uint8)
    n, _, stats, cent = cv2.connectedComponentsWithStats(dark, 8)
    ok = [cent[i] for i in range(1, n) if 30 < stats[i, 4] < 600 and stats[i, 4] / (stats[i, 2] * stats[i, 3]) > .5
          and stats[i, 2] >= stats[i, 3] and np.hypot(*(cent[i] - at)) < 16]
    # (none when the mouth touches the nose: then the silhouette's shift stands)
    return min(ok, key=lambda c: np.hypot(*(c - at))) if ok else None


def accents(rgb, otter):
    """Coloured extras: red-pink shapes outside the otter (hearts) and blue anywhere (a sweat drop)."""
    r, g, b = (rgb[..., i].astype(np.float32) for i in range(3))
    outside = ~cv2.dilate(otter.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    red = (r > 190) & (g < 150) & (b < 150) & outside
    blue = (b > r + 40) & (b > 150)
    return cv2.GaussianBlur((red | blue).astype(np.float32), (0, 0), .7)


BLUSH = (252, 159, 147)


def mixes(rgb, a, b, d):
    """Pixels within d of the colour line from a to b (anti-aliased edges between the two)."""
    a, b = np.array(a, np.float32), np.array(b, np.float32)
    t = np.clip(((rgb - a) @ (b - a)) / float((b - a) @ (b - a)), 0, 1)[..., None]
    return np.linalg.norm(rgb - (a + t * (b - a)), axis=2) < d


PAD = 420   # room on the right for the hearts (they float beside the head)


def overlays(base, blank_head, head_mask, features, base_eyes):
    """{expression: RGBA overlay in the base picture's frame, PAD wider on the right} (none for basic)."""
    import build
    sheet = np.array(Image.open(build.REF / "expressions.png").convert("RGB"))
    raw = cells(sheet)
    cut = [cutout.cutout(c) for c in raw]
    ref_alpha = cut[0][..., 3] > 0
    cell_face = build.features(cut[0], scale=.27)
    cell_eyes = cell_face["eyes"]
    m, _ = cv2.estimateAffinePartial2D(np.float32(cell_eyes), np.float32(base_eyes))
    h, w = base.shape[:2][0], base.shape[1] + PAD
    pad = lambda a: np.pad(a, [(0, 0), (0, PAD)] + [(0, 0)] * (a.ndim - 2))
    warp = lambda img, mat: cv2.warpAffine(img.astype(np.float32), mat, (w, h), flags=cv2.INTER_CUBIC, borderValue=0)

    def to_base(i):
        dx, dy = shift_to(cut[i][..., 3] > 0, ref_alpha)
        dx, dy = refine(cut[i], cut[0], dx, dy)
        mat = m @ np.array([[1, 0, dx], [0, 1, dy], [0, 0, 1]], np.float64)
        return warp(raw[i], mat), warp(accents(raw[i], cut[i][..., 3] > 0), mat)

    basic, _ = to_base(0)
    blank = pad(blank_head)[..., :3].astype(np.float32)
    original = pad(base)[..., :3].astype(np.float32)
    head_mask = pad(head_mask)
    # The face: eyes, brows, nose, mouth - the head's lower middle.
    (ex0, ey0), (ex1, ey1) = base_eyes
    span = ex1 - ex0
    face = np.zeros((h, w), np.uint8)
    cv2.ellipse(face, (int((ex0 + ex1) / 2), int((ey0 + ey1) / 2 + span * .12)), (int(span * 1.05), int(span * .62)), -8, 0, 360, 1, -1)
    face = face.astype(bool) & head_mask & ~build.ring(head_mask, 28)   # not the head's outline
    own = pad(cv2.dilate((features["eye_mask"] | features["mouth_mask"]).astype(np.uint8), np.ones((7, 7), np.uint8))).astype(bool)
    # Ghosts to drop: the small picture's muzzle and cheek edges never sit exactly on the base's, so
    # fur, cream and (on the cheeks) blush coloured pixels are the head underneath, not the expression.
    # Edge pixels are mixes of two of those, so a pixel close to the line between two tones is a ghost too.
    near = lambda a, tone, d: np.linalg.norm(a - np.array(tone, np.float32), axis=2) < d
    blush_zone = cv2.dilate(near(original, BLUSH, 40).astype(np.uint8), np.ones((25, 25), np.uint8)).astype(bool)
    # The nose comes from the small picture with the mouth (it is drawn a little differently per face).
    nose = pad(cv2.dilate(features["nose_mask"].astype(np.uint8), np.ones((41, 41), np.uint8))).astype(bool) & face
    diff_to = lambda a, b: np.linalg.norm(a - b, axis=2)
    out = {}
    for i, name in enumerate(ORDER):
        if name == "basic":
            continue
        colour, extra = to_base(i)
        changed = cv2.dilate(((diff_to(colour, basic) > 45) & face).astype(np.uint8), np.ones((15, 15), np.uint8)).astype(bool) & face
        changed |= nose
        src = np.where(changed[..., None], colour, original)
        alpha = np.clip((diff_to(src, blank) - 22) / 45, 0, 1)
        alpha[~(changed | (own & face))] = 0
        ghost = changed & (mixes(src, build.FUR, build.CREAM, 26) | near(src, build.FUR, 38) |
                           (blush_zone & (mixes(src, build.CREAM, BLUSH, 26) | mixes(src, build.FUR, BLUSH, 26))))
        alpha[ghost] = 0
        alpha = np.maximum(alpha, extra)
        rgba = np.zeros((h, w, 4), np.uint8)
        rgba[..., :3] = np.clip(np.where(extra[..., None] >= alpha[..., None] - 1e-3, colour, src), 0, 255).astype(np.uint8)
        rgba[..., 3] = (alpha * 255).astype(np.uint8)
        out[name] = rgba
    return out
