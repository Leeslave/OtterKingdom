"""Character variants on the farmer otter's rig: chief / miner / fisher.

Every variant starts from the farmer parts (cut_parts.cut_all) and keeps the same
canvas, bones and pivots, so one rig and one set of clips drive all of them. Per
character only the paint changes:
  - hat:    straw / band / brim recoloured (shading kept), straw tuft trimmed off,
            role details drawn on (head lamp, bobber, leaf sprig),
  - outfit: overalls / shoes recoloured, shirt stripes,
  - face:   the farmer's half-lidded eyes are painted out and new eyes drawn
            (round sparkly, small dots, smiling arcs), plus brows / glasses /
            moustache / freckles / soot.
Colours follow the existing MinerOtter / FishingOtter sprites (yellow hard hat +
blue overalls, olive bucket hat + striped shirt); the chief is new.

Coordinates are 1x sheet-cell pixels (x right, y down) like the cut_parts specs.
Images are straight-alpha float RGBA arrays at cut_parts.SCALE x.
"""
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

import cut_parts as cp

S = cp.SCALE
INK = np.array(cp.OUTLINE_RGB, np.float32)
LINE_W = 2.4          # outline width of drawn features (1x px)
SS = 4                # supersampling for drawn shapes


# --- colour helpers -----------------------------------------------------------------

def rgb_to_hsv(rgb):
    """rgb 0..255 (..., 3) -> h 0..360, s 0..1, v 0..1"""
    r, g, b = [rgb[..., i] / 255 for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rr = m & (mx == r)
    gg = m & (mx == g) & ~rr
    bb = m & ~rr & ~gg
    h[rr] = (60 * ((g - b)[rr] / d[rr])) % 360
    h[gg] = 60 * ((b - r)[gg] / d[gg]) + 120
    h[bb] = 60 * ((r - g)[bb] / d[bb]) + 240
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hue_in(h, lo, hi):
    return (h >= lo) & (h <= hi) if lo <= hi else (h >= lo) | (h <= hi)


def color_mask(a, hue=None, s=(0, 1), v=(0, 1)):
    h, sat, val = rgb_to_hsv(a[..., :3])
    m = (a[..., 3] > 128) & (sat >= s[0]) & (sat <= s[1]) & (val >= v[0]) & (val <= v[1])
    if hue:
        m &= hue_in(h, *hue)
    return m


def recolor(a, mask, target, v_ref=None, smooth=0.0):
    """Paint masked pixels with `target` (rgb), keeping their relative shading.
    v_ref = brightness that maps to exactly `target` (default: median of the mask).
    smooth > 0 blurs the shading first (removes straw texture for glossy materials)."""
    mask = mask.astype(np.float32)
    if mask.max() == 0:
        return a
    v = a[..., :3].max(-1)
    if smooth:
        w = ndimage.gaussian_filter(mask, smooth * S)
        v = np.where(mask > 0, ndimage.gaussian_filter(v * mask, smooth * S) / np.maximum(w, 1e-3), v)
    if v_ref is None:
        v_ref = np.median(v[mask > 0.5])
    new = np.clip(np.array(target, np.float32) * (v / v_ref)[..., None], 0, 255)
    out = a.copy()
    out[..., :3] = a[..., :3] * (1 - mask[..., None]) + new * mask[..., None]
    return out


# --- geometry -------------------------------------------------------------------------

def ellipse(cx, cy, rx, ry, rot=0.0, n=56):
    r = math.radians(rot)
    c, s = math.cos(r), math.sin(r)
    pts = []
    for t in np.linspace(0, 2 * math.pi, n, endpoint=False):
        x, y = rx * math.cos(t), ry * math.sin(t)
        pts.append((cx + x * c - y * s, cy + x * s + y * c))
    return pts


def cov_of(a, pts, smooth=False):
    return cp.coverage(pts, a.shape[:2], smooth=smooth)


def stroke_cov(a, pts, width, closed=False, smooth=True):
    """Anti-aliased coverage of a thick round-capped polyline."""
    p = np.asarray(pts, float)
    if smooth and len(p) > 2:
        p = chaikin_open(p) if not closed else cp.chaikin(p)
    h, w = a.shape[:2]
    k = S * SS
    big = Image.new('L', (w * SS, h * SS), 0)
    d = ImageDraw.Draw(big)
    q = [(x * k, y * k) for x, y in p]
    if closed:
        q.append(q[0])
    r = width * k / 2
    d.line(q, fill=255, width=max(1, int(round(width * k))), joint='curve')
    if not closed:   # round caps
        for x, y in (q[0], q[-1]):
            d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return np.asarray(big.resize((w, h), Image.BOX)).astype(np.float32) / 255


def chaikin_open(p, it=3):
    for _ in range(it):
        q = 0.75 * p[:-1] + 0.25 * p[1:]
        r = 0.25 * p[:-1] + 0.75 * p[1:]
        mid = np.stack([q, r], 1).reshape(-1, 2)
        p = np.vstack([p[:1], mid, p[-1:]])
    return p


# --- painting -------------------------------------------------------------------------

def over(a, cov, color, alpha=1.0):
    """Composite a flat or per-pixel colour over a (straight alpha)."""
    c = np.clip(cov * alpha, 0, 1)
    col = np.broadcast_to(np.asarray(color, np.float32), a.shape[:2] + (3,))
    da = a[..., 3] / 255
    oa = c + da * (1 - c)
    rgb = (col * c[..., None] + a[..., :3] * (da * (1 - c))[..., None]) / np.maximum(oa, 1e-6)[..., None]
    out = a.copy()
    out[..., :3] = np.where(oa[..., None] > 0, rgb, a[..., :3])
    out[..., 3] = oa * 255
    return out


def band_of(cov, width):
    """Fraction of each pixel lying in the outline band just inside the shape edge."""
    k = 4
    up = ndimage.zoom(cov, k, order=1, grid_mode=True, mode='nearest') > 0.5
    band = up & (ndimage.distance_transform_edt(up) <= width * S * k)
    pool = lambda m: m.reshape(m.shape[0] // k, k, m.shape[1] // k, k).mean((1, 3))
    return np.clip(pool(band) / np.maximum(pool(up), 1e-3), 0, 1)


def shape(a, cov, fill, line=LINE_W, line_rgb=INK, alpha=1.0):
    """Filled shape with an outline drawn inside its edge (line=0: no outline)."""
    fill = np.broadcast_to(np.asarray(fill, np.float32), a.shape[:2] + (3,))
    if line:
        f = band_of(cov, line)[..., None]
        fill = fill * (1 - f) + np.asarray(line_rgb, np.float32) * f
    return over(a, cov, fill, alpha)


def vgrad(a, top, bottom, y0, y1):
    """Vertical colour gradient between rows y0..y1 (1x)."""
    ys = np.arange(a.shape[0], dtype=np.float32)[:, None] / S
    t = np.clip((ys - y0) / max(y1 - y0, 1e-3), 0, 1)[..., None]
    g = np.asarray(top, np.float32) * (1 - t) + np.asarray(bottom, np.float32) * t
    return np.broadcast_to(g, a.shape[:2] + (3,))


def radial(a, cx, cy, r, inner, outer):
    ys, xs = np.mgrid[:a.shape[0], :a.shape[1]].astype(np.float32) / S
    t = np.clip(np.hypot(xs - cx, ys - cy) / r, 0, 1)[..., None]
    return np.asarray(inner, np.float32) * (1 - t) + np.asarray(outer, np.float32) * t


def inpaint(a, cov, sigma=2.0):
    """Paint the region under `cov` with the colours around it (outline pixels excluded)."""
    lum = a[..., :3].mean(-1)
    known = (a[..., 3] > 240) & (cov < 0.02) & (lum > 95)
    _, (iy, ix) = ndimage.distance_transform_edt(~known, return_indices=True)
    fill = a[..., :3][iy, ix]
    fill = np.stack([ndimage.gaussian_filter(fill[..., i], sigma * S) for i in range(3)], -1)
    out = a.copy()
    c = np.clip(cov, 0, 1)[..., None]
    out[..., :3] = a[..., :3] * (1 - c) + fill * c
    return out


def clip_out(a, pts):
    """Erase everything inside the polygon."""
    out = a.copy()
    out[..., 3] *= 1 - cov_of(a, pts)
    return out


def reink(a, sigma=1.5):
    """Re-run the outline pass after the silhouette changed."""
    prem = a.copy()
    prem[..., :3] *= prem[..., 3:4] / 255
    old = cp.ROUND_SIGMA
    cp.ROUND_SIGMA = sigma
    try:
        prem = cp.ink(prem, np.zeros(a.shape[:2], np.float32))
    finally:
        cp.ROUND_SIGMA = old
    out = prem.copy()
    al = out[..., 3:4] / 255
    out[..., :3] = np.where(al > 0, out[..., :3] / np.maximum(al, 1e-3), 0)
    return out


# --- regions of the source frames -----------------------------------------------------

_LABELS = {}


def region(view, seeds, grow=2.0):
    """Mask of the source frame's outlined regions under the seed points, grown over
    the inner edge of their outline (so recolours reach the anti-aliased rim)."""
    if view not in _LABELS:
        _LABELS[view] = cp.segment(cp.load_cell(*cp.VIEWS[view]['frame']))[1]
    lab = _LABELS[view]
    m = np.isin(lab, [cp.region_at(lab, s) for s in seeds])
    return ndimage.binary_dilation(m, iterations=int(grow * S))


# Hat regions of the farmer's head per view: crown (+ band, same region), brim,
# straw tuft to trim (polygon above the dome) and the band strip.
HAT = {
    'Front': dict(crown=[(210, 44)], brim=[(209, 64), (150, 82), (268, 82)],
                  tuft=[(185, 0), (235, 0), (235, 27.5), (226, 25.2), (209, 24.4), (192, 25.6), (185, 27.5)],
                  band=[(166, 43), (252, 43), (254, 64), (209, 67), (164, 64)]),
    'Side': dict(crown=[(221, 46)], brim=[(205, 81)],
                 tuft=[(196, 0), (242, 0), (242, 31.5), (231, 28.5), (218, 27.6), (206, 29), (196, 33)],
                 band=[(190, 50), (270, 40), (276, 58), (195, 70)]),
    'Back': dict(crown=[(207, 68)], brim=[(208, 100)],
                 tuft=[(186, 0), (232, 0), (232, 41.8), (226, 40.4), (218, 39.6), (209, 39.4), (200, 39.6), (192, 40.4), (186, 41.8)],
                 band=[(150, 76), (268, 76), (268, 102), (150, 102)]),
}
STRAW = dict(hue=(26, 75), s=(0.22, 1), v=(0.42, 1))
BAND = dict(hue=(340, 32), s=(0.15, 1), v=(0.3, 0.64))


def hat_masks(view, a):
    spec = HAT[view]
    crown = region(view, spec['crown'])
    brim = region(view, spec['brim'])
    band_poly = cov_of(a, spec['band']) > 0.5
    straw = color_mask(a, **STRAW)
    band = (crown | brim) & band_poly & color_mask(a, **BAND) & ~straw
    return crown & straw, brim & straw, band


# Brightness of each farmer material that maps to a variant's target colour. Taken
# once from the front view so every view (and part) of a variant gets the same tone.
VREF = {}


def init_refs(parts):
    if VREF:
        return
    head = parts['Front']['Head']
    v = head[..., :3].max(-1)
    crown, brim, band = hat_masks('Front', head)
    VREF.update(crown=np.median(v[crown]), brim=np.median(v[brim]), band=np.median(v[band]))
    leg = parts['Front']['Leg_L']
    VREF['shoe'] = np.median(leg[..., :3].max(-1)[color_mask(leg, **SHOE)])
    body = parts['Front']['Body']
    VREF['overall'] = np.median(body[..., :3].max(-1)[color_mask(body, **OVERALL)])


def restyle_hat(a, view, crown_rgb, brim_rgb, band_rgb, smooth=0.0, trim=True):
    crown, brim, band = hat_masks(view, a)
    a = recolor(a, crown, crown_rgb, VREF['crown'], smooth=smooth)
    a = recolor(a, brim, brim_rgb, VREF['brim'], smooth=smooth)
    # Flat ribbon. The farmer's band is ragged (straw grain, dark streaks), so it is
    # repainted as one strip per column, from just under the crown down to the brim,
    # with a clean outline above the brim; both edges are smoothed along x. Columns
    # where an ear hides the crown take the strip's top from their neighbours.
    a = paint_band(a, view, crown, brim, band, band_rgb)
    if trim:
        a = reink(clip_out(a, HAT[view]['tuft']))
    return a


BAND_LINE = 2.6   # outline between ribbon and brim (1x px)


def paint_band(a, view, crown, brim, band, band_rgb):
    h, w = band.shape
    spec = HAT[view]
    hat = region(view, spec['crown']) | region(view, spec['brim'])
    top, low, rim = (np.full(w, np.nan) for _ in range(3))
    for x in np.nonzero(band.any(0))[0]:
        ys = np.nonzero(band[:, x])[0]
        b = np.nonzero(brim[ys[-1]:, x])[0]
        if len(b) == 0:
            continue
        rim[x] = ys[-1] + b[0]                 # first brim pixel under the band
        c = np.nonzero(crown[:ys[0] + 2, x])[0]
        if len(c):
            top[x] = c[-1] + 1
    ok = ~np.isnan(rim)
    known = ok & ~np.isnan(top)
    if known.sum() < 4:
        return a
    xs = np.nonzero(ok)[0]
    top[xs] = np.interp(xs, np.nonzero(known)[0], top[known])
    smooth = lambda y: ndimage.gaussian_filter1d(ndimage.median_filter(y, size=int(3 * S), mode='nearest'), S)
    top[xs], rim[xs] = smooth(top[xs]), smooth(rim[xs])
    low[xs] = rim[xs] - BAND_LINE * S
    yy = np.arange(h, dtype=np.float32)[:, None] + 0.5
    with np.errstate(invalid='ignore'):
        ribbon = np.clip(yy - top[None], 0, 1) * np.clip(low[None] - yy, 0, 1)
        line = np.clip(yy - low[None], 0, 1) * np.clip(rim[None] + 0.5 - yy, 0, 1)
    ends = ndimage.gaussian_filter(ok.astype(np.float32), 0.6 * S)[None] * hat
    ribbon, line = np.nan_to_num(ribbon) * ends, np.nan_to_num(line) * ends
    v = a[..., :3].max(-1)
    wgt = ndimage.gaussian_filter(band.astype(np.float32), 1.5 * S)
    vs = ndimage.gaussian_filter(v * band, 1.5 * S) / np.maximum(wgt, 1e-3)
    vs = np.where(wgt > 0.05, vs, VREF['band'])
    new = np.clip(np.array(band_rgb, np.float32) * (vs / VREF['band'])[..., None], 0, 255)
    a = a.copy()
    a[..., :3] = a[..., :3] * (1 - ribbon[..., None]) + new * ribbon[..., None]
    a[..., :3] = a[..., :3] * (1 - line[..., None]) + INK * line[..., None]
    return a


# --- faces ------------------------------------------------------------------------------

# Farmer features to paint out (1x): eyes (with their lids) and the w-mouth.
def rbox(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


EYES = {'Front': [rbox(164.5, 84, 197.5, 118), rbox(219.5, 84, 252.5, 118)],
        'Side': [rbox(236, 88.5, 267, 120)]}
MOUTH = {'Front': ellipse(210, 116.5, 15, 5.5)}
# Where new eyes go.
EYE_AT = {'Front': [(181, 102), (236, 102)], 'Side': [(252, 106)]}


def paint_out(a, view, mouth=False):
    cov = np.zeros(a.shape[:2], np.float32)
    for e in EYES.get(view, []):
        cov = np.maximum(cov, cov_of(a, e, smooth=True))
    if mouth and view in MOUTH:
        cov = np.maximum(cov, cov_of(a, MOUTH[view]))
    cov = ndimage.gaussian_filter(cov, 0.6 * S)
    return inpaint(a, cov)


def round_eye(a, cx, cy, rx, ry, big_glint=0.4):
    """Glossy round eye with two highlights."""
    cov = cov_of(a, ellipse(cx, cy, rx, ry))
    a = shape(a, cov, vgrad(a, (40, 22, 22), (105, 60, 48), cy - ry, cy + ry), line=1.2, line_rgb=(35, 18, 18))
    a = over(a, cov_of(a, ellipse(cx + rx * 0.32, cy - ry * 0.36, rx * big_glint, ry * big_glint * 0.92)), (255, 255, 255))
    a = over(a, cov_of(a, ellipse(cx - rx * 0.38, cy + ry * 0.38, rx * 0.17, ry * 0.15)), (255, 255, 255), 0.9)
    return a


def smile_eye(a, cx, cy, w, h, width=3.0):
    """Closed happy eye: an upside-down U."""
    pts = [(cx - w / 2, cy + h / 2), (cx - w * 0.3, cy - h * 0.35), (cx, cy - h / 2),
           (cx + w * 0.3, cy - h * 0.35), (cx + w / 2, cy + h / 2)]
    return over(a, stroke_cov(a, pts, width), (45, 25, 25))


def fluffy(a, centres, fill, shade, line=2.0):
    """Cloud shape (union of ellipses) for brows / moustache: white with soft grey shading."""
    cov = np.zeros(a.shape[:2], np.float32)
    for cx, cy, rx, ry, rot in centres:
        cov = np.maximum(cov, cov_of(a, ellipse(cx, cy, rx, ry, rot)))
    ys = [c[1] for c in centres]
    return shape(a, cov, vgrad(a, fill, shade, min(ys) - 3, max(ys) + 4), line=line)


def soft_spot(a, cx, cy, rx, ry, color, alpha, rot=0.0):
    cov = ndimage.gaussian_filter(cov_of(a, ellipse(cx, cy, rx, ry, rot)), 1.2 * S)
    return over(a, cov * (a[..., 3] > 200), color, alpha)


# --- props / details --------------------------------------------------------------------

def head_lamp_front(a, cx, cy):
    a = shape(a, cov_of(a, ellipse(cx, cy, 9.5, 9)), vgrad(a, (170, 175, 185), (95, 100, 112), cy - 9, cy + 9))
    a = shape(a, cov_of(a, ellipse(cx, cy + 0.3, 6.2, 5.9)), radial(a, cx - 1.5, cy - 1.5, 7, (255, 255, 235), (250, 215, 110)),
              line=1.4)
    return over(a, cov_of(a, ellipse(cx - 2.2, cy - 2.2, 1.8, 1.5)), (255, 255, 255))


def head_lamp_side(a, cx, cy, rot=-14):
    a = shape(a, cov_of(a, ellipse(cx, cy, 8.5, 6.5, rot)), vgrad(a, (170, 175, 185), (95, 100, 112), cy - 6, cy + 6))
    r = math.radians(rot)
    fx, fy = cx + 7.2 * math.cos(r), cy + 7.2 * math.sin(r)
    return shape(a, cov_of(a, ellipse(fx, fy, 2.6, 6.0, rot)), (255, 240, 170), line=1.4)


def battery(a, cx, cy):
    pts = [(cx - 8, cy - 4.5), (cx + 8, cy - 4.5), (cx + 8, cy + 4.5), (cx - 8, cy + 4.5)]
    return shape(a, cov_of(a, pts, smooth=True), vgrad(a, (115, 118, 128), (70, 72, 82), cy - 5, cy + 5), line=1.8)


def bobber(a, cx, cy, r=4.6):
    a = over(a, stroke_cov(a, [(cx, cy - r - 3.2), (cx, cy - r + 1)], 1.6), INK)
    cov = cov_of(a, ellipse(cx, cy, r, r * 1.05))
    top = (np.arange(a.shape[0])[:, None] / S < cy).astype(np.float32)
    fill = np.where(top[..., None] > 0, np.array((225, 70, 60), np.float32), np.array((250, 250, 245), np.float32))
    a = shape(a, cov, fill, line=1.5)
    a = over(a, stroke_cov(a, [(cx - r + 0.8, cy), (cx + r - 0.8, cy)], 1.1), INK)
    return over(a, cov_of(a, ellipse(cx - r * 0.38, cy - r * 0.45, r * 0.25, r * 0.2)), (255, 255, 255), 0.85)


def leaf(a, cx, cy, length, rot, color=(115, 170, 80)):
    r = math.radians(rot)
    a = shape(a, cov_of(a, ellipse(cx, cy, length / 2, length * 0.2, rot)),
              vgrad(a, tuple(min(255, c + 30) for c in color), tuple(int(c * 0.75) for c in color), cy - 5, cy + 5),
              line=1.6)
    dx, dy = math.cos(r) * length * 0.42, math.sin(r) * length * 0.42
    return over(a, stroke_cov(a, [(cx - dx, cy - dy), (cx + dx, cy + dy)], 0.9), (70, 110, 50))


def bow_tie(a, cx, cy, color):
    dark = tuple(int(c * 0.7) for c in color)
    for sgn in (-1, 1):
        pts = [(cx, cy - 1.5), (cx + sgn * 11, cy - 6.5), (cx + sgn * 12.5, cy), (cx + sgn * 11, cy + 6.5), (cx, cy + 1.5)]
        a = shape(a, cov_of(a, pts, smooth=True), vgrad(a, color, dark, cy - 6, cy + 6), line=1.8)
    return shape(a, cov_of(a, ellipse(cx, cy, 3.6, 4.2)), color, line=1.6)


def stripes(a, mask, color, period=6.5, width=2.6):
    """Horizontal stripes over the masked (white shirt) pixels, keeping their shading."""
    ys = np.arange(a.shape[0], dtype=np.float32)[:, None] / S
    d = np.abs(ys % period - width / 2)                  # distance from the stripe centre (1x)
    on = np.clip((width / 2 - d) * S + 0.5, 0, 1)
    on = np.broadcast_to(on, mask.shape) * mask
    v = a[..., :3].max(-1)
    new = np.clip(np.asarray(color, np.float32) * (v / 240.0)[..., None], 0, 255)
    out = a.copy()
    out[..., :3] = a[..., :3] * (1 - on[..., None]) + new * on[..., None]
    return out


# --- outfits ------------------------------------------------------------------------------

OVERALL = dict(hue=(70, 175), s=(0.1, 1), v=(0.15, 1))
SHOE = dict(hue=(330, 38), s=(0.2, 1), v=(0.3, 0.75))     # v > 0.3 leaves the outline alone
SHIRT = dict(s=(0, 0.16), v=(0.72, 1))
# pale yellow-green highlight on the trouser cuffs
CUFF = dict(hue=(40, 75), s=(0.2, 0.45), v=(0.3, 1))
CUFF_PALE = dict(hue=(40, 130), s=(0.05, 0.45), v=(0.5, 1))
def outfit(parts, overall_rgb, shoe_rgb=None, shirt_stripes=None):
    """Recolour overalls (+ shoes) on every body / leg / foot part of every view."""
    for view, pv in parts.items():
        for name, a in pv.items():
            # both masks from the farmer colours: a reddish overall would read as shoe afterwards
            overall_m, shoe_m = color_mask(a, **OVERALL) | color_mask(a, **CUFF), color_mask(a, **SHOE)
            if name.startswith('Leg'):   # the cuff's pale highlight is nearly white; legs carry no shirt
                overall_m |= color_mask(a, **CUFF_PALE)
            if name.startswith(('Body', 'Leg')):
                a = recolor(a, overall_m, overall_rgb, VREF['overall'])
            if shoe_rgb and name.startswith(('Leg', 'Foot')):
                a = recolor(a, shoe_m, shoe_rgb, VREF['shoe'])
            if shirt_stripes and name.startswith(('Body', 'Arm')):
                m = color_mask(a, **SHIRT) & (a[..., 3] > 200)
                m = ndimage.binary_erosion(m, iterations=1).astype(np.float32)
                a = stripes(a, ndimage.gaussian_filter(m, 0.5 * S), shirt_stripes)
            pv[name] = a


# --- characters -----------------------------------------------------------------------------

def miner(parts):
    """Yellow hard hat with a head lamp, denim overalls, big sparkly eyes, a soot smudge."""
    outfit(parts, overall_rgb=(72, 102, 152), shoe_rgb=(118, 74, 56))
    hat = dict(crown_rgb=(252, 196, 46), brim_rgb=(246, 186, 40), band_rgb=(64, 64, 72), smooth=2.5)
    f = parts['Front']
    a = restyle_hat(f['Head'], 'Front', **hat)
    a = gloss(a, [ellipse(194, 38, 7, 3.6, -28), ellipse(184, 45, 2, 1.6)])
    a = head_lamp_front(a, 209.5, 53.5)
    a = paint_out(a, 'Front')
    for cx, cy in EYE_AT['Front']:
        a = round_eye(a, cx, cy + 1, 7.6, 9.0)
    a = soft_spot(a, 172, 125.5, 6, 1.7, (105, 88, 84), 0.42, -14)
    a = soft_spot(a, 177, 128.5, 3.6, 1.3, (105, 88, 84), 0.36, -14)
    f['Head'] = a
    s = parts['Side']
    a = restyle_hat(s['Head'], 'Side', **hat)
    a = gloss(a, [ellipse(213, 35, 7, 3.4, -18)])
    a = head_lamp_side(a, 262, 47)
    a = paint_out(a, 'Side')
    a = round_eye(a, *EYE_AT['Side'][0], 6.8, 8.6)
    a = soft_spot(a, 238, 127, 5.5, 1.6, (105, 88, 84), 0.42, -14)
    a = soft_spot(a, 243, 130, 3.4, 1.2, (105, 88, 84), 0.36, -14)
    s['Head'] = a
    b = parts['Back']
    a = restyle_hat(b['Head'], 'Back', **hat)
    a = gloss(a, [ellipse(190, 50, 8, 3.6, -30)])
    b['Head'] = battery(a, 209, 89)


def fisher(parts):
    """Olive bucket hat with a bobber, striped shirt, khaki waders, dot eyes, open grin, freckles."""
    outfit(parts, overall_rgb=(188, 160, 112), shoe_rgb=(66, 112, 104), shirt_stripes=(88, 128, 190))
    hat = dict(crown_rgb=(148, 150, 96), brim_rgb=(136, 140, 88), band_rgb=(196, 168, 118), smooth=0.8)
    f = parts['Front']
    a = restyle_hat(f['Head'], 'Front', **hat)
    a = bobber(a, 240, 54)
    a = paint_out(a, 'Front', mouth=True)
    for cx, cy in EYE_AT['Front']:
        a = round_eye(a, cx, cy + 2, 5.6, 6.6, big_glint=0.45)
    a = grin(a, 210, 113.5)
    for cx, cy in ((165, 118), (170.5, 121), (165, 124), (252, 118), (246.5, 121), (252, 124)):
        a = over(a, cov_of(a, ellipse(cx, cy, 1.25, 1.15)), (165, 100, 78), 0.9)
    f['Head'] = a
    s = parts['Side']
    a = restyle_hat(s['Head'], 'Side', **hat)
    a = bobber(a, 232, 53)
    a = paint_out(a, 'Side')
    a = round_eye(a, EYE_AT['Side'][0][0], EYE_AT['Side'][0][1] + 1.5, 5.2, 6.4, big_glint=0.45)
    for cx, cy in ((229, 117), (234.5, 120.5), (228.5, 123.5)):
        a = over(a, cov_of(a, ellipse(cx, cy, 1.25, 1.15)), (165, 100, 78), 0.9)
    s['Head'] = a
    b = parts['Back']
    a = restyle_hat(b['Head'], 'Back', **hat)
    b['Head'] = bobber(a, 176, 88)


def chief(parts):
    """Dark felt hat with a gold band and a leaf, burgundy overalls + bow tie, round
    glasses, bushy white brows and moustache, smiling closed eyes."""
    outfit(parts, overall_rgb=(140, 58, 72), shoe_rgb=(62, 56, 60))
    hat = dict(crown_rgb=(92, 70, 62), brim_rgb=(84, 63, 56), band_rgb=(232, 186, 82), smooth=2.0)
    brow_w, brow_s = (250, 250, 246), (205, 205, 200)
    f = parts['Front']
    a = restyle_hat(f['Head'], 'Front', **hat)
    a = gloss(a, [ellipse(195, 39, 6, 2.6, -25)], alpha=0.25)
    a = leaf(a, 247, 44, 15, -55)
    a = leaf(a, 252, 49, 12, -20, (95, 150, 70))
    a = paint_out(a, 'Front', mouth=True)
    for cx, cy in EYE_AT['Front']:
        a = smile_eye(a, cx, cy + 2, 13, 6.5)
    a = glasses_front(a)
    a = fluffy(a, [(172.5, 90.5, 6, 4.4, -22), (180, 87.5, 6.6, 5, -8), (188.5, 87, 6, 4.6, 6), (194.5, 88.5, 4, 3.4, 15)],
               brow_w, brow_s)
    a = fluffy(a, [(244.5, 90.5, 6, 4.4, 22), (237, 87.5, 6.6, 5, 8), (228.5, 87, 6, 4.6, -6), (222.5, 88.5, 4, 3.4, -15)],
               brow_w, brow_s)
    a = moustache_front(a, brow_w, brow_s)
    f['Head'] = a
    f['Body'] = bow_tie(f['Body'], 208.5, 143.5, (236, 190, 86))
    s = parts['Side']
    a = restyle_hat(s['Head'], 'Side', **hat)
    a = leaf(a, 203, 47, 15, -125)
    a = leaf(a, 198, 51, 12, -160, (95, 150, 70))
    a = paint_out(a, 'Side')
    a = smile_eye(a, EYE_AT['Side'][0][0], EYE_AT['Side'][0][1] + 2, 12, 6)
    a = glasses_side(a)
    a = fluffy(a, [(242.5, 92.5, 5.5, 4, -18), (249.5, 89.5, 6.4, 4.8, -4), (257.5, 90, 5.6, 4.4, 10), (263, 92, 3.6, 3, 20)],
               brow_w, brow_s)
    a = moustache_side(a, brow_w, brow_s)
    s['Head'] = a
    b = parts['Back']
    a = restyle_hat(b['Head'], 'Back', **hat)
    b['Head'] = leaf(leaf(a, 170, 82, 15, -125), 165, 87, 12, -160, (95, 150, 70))


# --- character-specific drawing helpers -----------------------------------------------------------

def gloss(a, shapes, alpha=0.6):
    for pts in shapes:
        a = over(a, ndimage.gaussian_filter(cov_of(a, pts), 0.4 * S), (255, 255, 240), alpha)
    return a


def grin(a, cx, cy):
    """Open happy mouth with a tongue."""
    mouth = [(cx - 7.5, cy), (cx + 7.5, cy), (cx + 6, cy + 4.5), (cx + 2.5, cy + 7.2), (cx, cy + 7.6),
             (cx - 2.5, cy + 7.2), (cx - 6, cy + 4.5)]
    m = cov_of(a, mouth, smooth=True)
    a = shape(a, m, (110, 40, 45), line=1.6)
    tongue = cov_of(a, ellipse(cx, cy + 6.2, 4.4, 2.8)) * ndimage.binary_erosion(m > 0.5, iterations=int(1.4 * S))
    return over(a, tongue, (240, 125, 130))


GLASS_RIM = (180, 130, 60)


def lens(a, cx, cy, r):
    a = over(a, cov_of(a, ellipse(cx, cy, r, r)), (255, 255, 255), 0.18)
    a = over(a, stroke_cov(a, [(cx - r * 0.45, cy - r * 0.05), (cx - r * 0.05, cy - r * 0.48)], 1.3), (255, 255, 255), 0.75)
    return over(a, stroke_cov(a, ellipse(cx, cy, r, r, n=48), 2.4, closed=True, smooth=False), GLASS_RIM)


def glasses_front(a):
    (lx, ly), (rx, ry) = EYE_AT['Front']
    a = over(a, stroke_cov(a, [(lx - 10.5, ly + 1), (lx - 22, ly - 3)], 2.0), GLASS_RIM)
    a = over(a, stroke_cov(a, [(rx + 10.5, ry + 1), (rx + 22, ry - 3)], 2.0), GLASS_RIM)
    a = over(a, stroke_cov(a, [(lx + 10.2, ly + 1.5), ((lx + rx) / 2, ly - 1.2), (rx - 10.2, ry + 1.5)], 2.0), GLASS_RIM)
    a = lens(a, lx, ly + 2, 10.8)
    return lens(a, rx, ry + 2, 10.8)


def glasses_side(a):
    cx, cy = EYE_AT['Side'][0]
    a = over(a, stroke_cov(a, [(cx - 9.5, cy - 1), (cx - 30, cy - 6), (cx - 38, cy - 9)], 2.0), GLASS_RIM)
    a = over(a, stroke_cov(a, [(cx + 9.5, cy + 0.5), (cx + 13.5, cy - 1)], 2.0), GLASS_RIM)
    a = over(a, cov_of(a, ellipse(cx + 0.5, cy + 2, 7.2, 10.5)), (255, 255, 255), 0.18)
    return over(a, stroke_cov(a, ellipse(cx + 0.5, cy + 2, 7.2, 10.5, n=48), 2.4, closed=True, smooth=False), GLASS_RIM)


def moustache_front(a, fill, shade):
    lobes = [(202, 114.5, 8, 5.2, 12), (193.5, 117.5, 5.6, 3.8, 30), (187.5, 118.5, 3.4, 2.6, 40),
             (218, 114.5, 8, 5.2, -12), (226.5, 117.5, 5.6, 3.8, -30), (232.5, 118.5, 3.4, 2.6, -40),
             (210, 113, 5, 3.6, 0)]
    return fluffy(a, lobes, fill, shade)


def moustache_side(a, fill, shade):
    lobes = [(269.5, 113.5, 7.4, 4.8, 8), (262, 116, 5.6, 3.8, -12), (256, 117.5, 3.6, 2.6, -25), (276, 114, 4.2, 3.2, 20)]
    return fluffy(a, lobes, fill, shade)


CHARACTERS = {'Chief': chief, 'Miner': miner, 'Fisher': fisher}


def to_arrays(parts):
    """{view: {part: PIL}} -> {view: {part: straight-alpha float array}}"""
    return {v: {p: np.asarray(im).astype(np.float32) for p, im in pv.items()} for v, pv in parts.items()}


def to_images(parts):
    return {v: {p: Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)) for p, a in pv.items()}
            for v, pv in parts.items()}


def build(name, farmer_parts):
    """Variant parts ({view: {part: PIL}}) from the farmer parts."""
    parts = to_arrays(farmer_parts)
    init_refs(parts)
    CHARACTERS[name](parts)
    return to_images(parts)
