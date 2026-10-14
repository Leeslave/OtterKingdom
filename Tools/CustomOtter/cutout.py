"""Cut the otter out of one of the user's reference pictures (ArtSource/Otter/CustomOtter/reference).

The pictures are flat mint backgrounds (some framed like a card, with a soft ground shadow) around an
otter with a thick dark outline. Background = everything reachable from the border without crossing
the outline, plus any other mint pixels (gaps enclosed between parts); the outline's anti-aliased
rim keeps a matching partial alpha so edges stay smooth.
"""
import cv2
import numpy as np

OUTLINE = np.array((30, 10, 6), np.float32)


def luma(rgb):
    return rgb[..., 0] * .3 + rgb[..., 1] * .59 + rgb[..., 2] * .11


def mint(rgb):
    """Background-ish: the mint page, the card's frame and the grey-green ground shadow (the otter has no green)."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (g > r + 6) & (g > b - 4) & (luma(rgb) > 120)


def cutout(rgb):
    """RGBA of the otter (the picture's pixels, background cleared)."""
    rgb = rgb.astype(np.float32)
    h, w = rgb.shape[:2]
    dark = luma(rgb) < 110
    open_ = (~dark).astype(np.uint8)
    # Flood the open area from the border.
    n, labels = cv2.connectedComponents(open_, 4)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    background = np.isin(labels, list(border)) | (mint(rgb) & ~dark)
    # Small leftover islands inside the background (dust, frame specks) go too.
    n, labels, stats, _ = cv2.connectedComponentsWithStats((~background).astype(np.uint8), 8)
    keep = 1 + int(np.argmax(stats[1:, 4]))
    otter = labels == keep
    # Partial alpha on the rim: how far the pixel is from the page towards the outline.
    page = np.median(rgb[background & (cv2.dilate(otter.astype(np.uint8), np.ones((9, 9), np.uint8)) == 0)], axis=0)
    rim = cv2.dilate(otter.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool) & ~otter
    alpha = otter.astype(np.float32)
    t = np.clip((luma(page) - luma(rgb)) / max(luma(page) - luma(OUTLINE), 1), 0, 1)
    alpha[rim] = t[rim]
    out = np.zeros((h, w, 4), np.uint8)
    colour = rgb.copy()
    colour[rim] = OUTLINE   # the rim is outline mixed with page: keep the outline's colour at partial alpha
    out[..., :3] = np.clip(colour, 0, 255).astype(np.uint8)
    out[..., 3] = (alpha * 255).astype(np.uint8)
    return out
