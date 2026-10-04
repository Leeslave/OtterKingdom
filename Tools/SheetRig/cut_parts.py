"""Cuts rig parts out of the frame-animated farmer otter sheet.

Source: Assets/Sprites/Characters/FarmerOtter/FarmerOtter_Sheet.png (8x5 cells of
416x240; rows = walk right / walk left / walk down / walk up / harvest).
One frame per view (front / side / back) is split into parts:
  1. interior regions (enclosed by the dark outline) are assigned to parts by seed
     points, with polygons overriding where one region spans several parts,
  2. outline pixels go to the top-most part whose interior is within OUTLINE_R,
  3. areas hidden under other parts are painted in: nearest colour from chosen
     regions + a new outline, mirrored from the symmetric side, or extruded
     (limb tops / tail root continue under the parent as a straight tube).
Work happens at SCALE x the sheet resolution so painted outlines stay smooth.
Coordinates in the specs below are 1x cell pixels (x right, y down).
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SHEET = os.path.join(ROOT, 'Assets/Sprites/Characters/FarmerOtter/FarmerOtter_Sheet.png')
CELL_W, CELL_H = 416, 240
SCALE = 2
INTERIOR_LUM = 70
OUTLINE_R = 3.5 * SCALE      # outline pixels this close to a part's interior can belong to it
OUTLINE_W = 3.0 * SCALE      # width of painted outlines
OUTLINE_RGB = (52, 30, 26)
SS = 4                       # supersampling for painted shapes
INK_W = 3.2 * SCALE          # outline width redrawn around every part's silhouette
ROUND_SIGMA = 6.0            # silhouette rounding (S x px): corners become arcs, small bumps / notches melt
SHARPEN = dict(radius=1.5, percent=60, threshold=1)   # the sheet is upscaled and soft; crisp up inner lines


def near(pts, src=None, outline=True, smooth=True, holes_only=False, src_poly=None):
    """Paint the polygon with the nearest colour of the source regions (seed points) /
    source polygon (default: this part's own interior) and outline the new edge."""
    return dict(op='near', pts=pts, src=src, outline=outline, smooth=smooth, holes_only=holes_only,
                src_poly=src_poly)


def erase(pts):
    """Clear the polygon except this part's clean interior (outline/AA rings of a removed neighbour)."""
    return dict(op='erase', pts=pts, smooth=False)


def clip(pts):
    """Keep only what lies inside the polygon (drops neighbours' scraps)."""
    return dict(op='clip', pts=pts, smooth=False)


def mirror(pts, axis):
    return dict(op='mirror', pts=pts, axis=axis, smooth=False)


def extrude(at, length, direction='up'):
    """Repeat the row/column at `at` outward by `length` (1x px)."""
    return dict(op='extrude', at=at, length=length, dir=direction)


def capsule(cx, half, cy, bottom=240, margin=8):
    """Clip a limb to a tube with a round top centred on its hinge (cx, cy), so the
    hidden end stays inside the parent whatever the rotation. margin widens the
    tube below the hinge."""
    import math
    arc = [(cx + half * math.cos(a), cy - half * math.sin(a)) for a in np.linspace(math.pi, 0, 24)]
    return clip(arc + [(cx + half + margin, cy), (cx + half + margin, bottom),
                       (cx - half - margin, bottom), (cx - half - margin, cy)])


# --- view specs -------------------------------------------------------------------

SHIRT_F, BIB_F = (208, 143), (208, 175)
FRONT = dict(
    frame=(2, 6),
    layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Arm_L': 20, 'Arm_R': 20, 'Head': 30},
    seeds={
        'Head': [(205, 40), (150, 40), (160, 52), (270, 40), (262, 50), (205, 85), (205, 66), (140, 90), (285, 90)],
        'Arm_L': [(160, 160)],
        'Arm_R': [(258, 160)],
        'Body': [SHIRT_F, BIB_F, (189, 152), (231, 152)],
        'Leg_L': [(190, 220)],
        'Leg_R': [(225, 212)],
    },
    # (part, polygon, only regions containing these seeds or None)
    # edges left without an outline: the body's bottom cut sits on the legs seamlessly
    open={'Body': [[(150, 190), (266, 190), (266, 204), (150, 204)]]},
    cuts=[
        ('Leg_L', [(150, 194), (210, 194), (210, 240), (150, 240)], [BIB_F]),
        ('Leg_R', [(210, 194), (270, 194), (270, 240), (210, 240)], [BIB_F]),
    ],
    paint={
        'Body': [
            near([(176, 118), (240, 118), (247, 130), (250, 142), (166, 142), (169, 130)], src=[SHIRT_F]),
            near([(184, 136), (171, 140), (165, 148), (163, 160), (164, 172), (167, 184), (180, 184), (183, 150)],
                 src=[BIB_F]),
            near([(232, 136), (245, 140), (251, 148), (253, 160), (252, 172), (249, 184), (236, 184), (233, 150)],
                 src=[BIB_F]),
            near([(184, 134), (172, 138), (166, 146), (184, 148)], src=[SHIRT_F]),
            near([(232, 134), (244, 138), (250, 146), (232, 148)], src=[SHIRT_F]),
        ],
        'Leg_L': [extrude(198, 18)],
        'Head': [clip([(100, 0), (320, 0), (320, 110), (275, 112), (266, 122), (258, 131), (244, 137), (226, 140.5),
                       (208, 141), (190, 140.5), (174, 137), (160, 131), (152, 122), (144, 112), (100, 110)])],
        'Arm_L': [near([(156, 148), (158, 140), (166, 134), (176, 133), (182, 137), (181, 146)])],
        'Arm_R': [near([(237, 146), (236, 137), (242, 133), (252, 134), (260, 140), (262, 148)])],
    },
)

SHIRT_S, BIB_S = (190, 147), (230, 180)
# bottom curve of the side body (left to right); the legs swing out from under it
SIDE_BUM = [(168, 176), (168.5, 184), (171, 191), (178, 196.5), (190, 200), (208, 201.5), (226, 200), (238, 196.5),
            (245, 191), (249.5, 184), (251, 176)]
SIDE = dict(
    frame=(0, 7),
    layers={'Leg_Far': 2, 'Foot_Near': 3, 'Leg_Near': 4, 'Tail': 6, 'Body': 10, 'Arm_Near': 20, 'Head': 30},
    seeds={
        'Head': [(225, 40), (160, 60), (155, 77), (200, 85), (200, 110), (150, 105), (240, 70)],
        'Arm_Near': [(205, 160), (208, 186)],
        'Body': [BIB_S, (182, 150), SHIRT_S, (240, 150), (228, 152), (239, 158), (183, 158)],
        'Tail': [(140, 180)],
        'Leg_Far': [(180, 215)],
        # near leg = trouser tube (the overalls region is shared with the body, so it comes
        # from the Leg_Near cut below, no seed) + a separate foot
        'Foot_Near': [(232, 219)],
    },
    cuts=[
        # the sleeve outline has a gap, so arm and overalls are one region
        ('Arm_Near', [(184, 146), (193, 143), (210, 144), (221, 148), (223, 158), (219, 164), (216, 170),
                      (215, 182), (209, 190), (199, 190), (192, 184), (187, 170), (184, 160)], [(205, 160)]),
        ('Leg_Far', [(150, 198), (207, 198), (207, 240), (150, 240)], [BIB_S]),
        ('Leg_Near', [(207, 198), (270, 198), (270, 240), (207, 240)], [BIB_S]),
    ],
    paint={
        'Body': [
            near([(178, 126), (245, 126), (252, 138), (250, 150), (172, 150), (172, 138)], src=[SHIRT_S, BIB_S]),
            near([(182, 144), (224, 144), (226, 160), (220, 192), (190, 194), (183, 172)], src=[BIB_S]),
            near([(182, 141), (204, 140), (208, 148), (196, 152), (183, 152)], src=[(182, 150), (183, 158)]),
            # round bum: the legs swing (and cross) under it, so the bottom needs its own outline
            clip([(100, 0), (320, 0), (320, 176)] + SIDE_BUM[::-1] + [(100, 176)]),
            near([(167, 172), (251, 172)] + SIDE_BUM[::-1], src=[BIB_S], smooth=False),
        ],
        'Tail': [near([(158, 164), (170, 161), (182, 166), (186, 180), (182, 194), (170, 199), (158, 199)])],
        'Arm_Near': [clip([(183, 146), (192, 139), (211, 140), (224, 145), (227, 158), (222, 167), (219, 172),
                           (219, 184), (211, 194), (197, 194), (188, 187), (183, 172), (183, 160)])],
        'Head': [clip([(100, 0), (320, 0), (320, 110), (272, 115), (262, 128), (250, 135), (236, 139.5), (220, 141),
                       (200, 140.5), (182, 138.5), (166, 134), (156, 128), (146, 118), (100, 110)])],
        # only the near leg is used (both legs swing from one hip and cross); the far leg is its copy
        'Leg_Near': [extrude(201, 34), capsule(229.5, 17.5, 186, margin=0.5)],
        # the foot's top hides under the trouser cuff; round it off so it can rock on the ankle
        'Foot_Near': [near([(210, 221), (210.5, 212), (215, 206), (222, 203.5), (232, 203), (241, 204.5), (247, 208), (249.5, 214)],
                           src_poly=[(214, 217), (248, 217), (248, 225), (214, 225)])],
    },
)

SHIRT_B, BIB_B = (190, 147), (230, 185)
BACK = dict(
    frame=(3, 0),
    layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Tail': 15, 'Arm_L': 20, 'Arm_R': 20, 'Head': 30},
    seeds={
        'Head': [(205, 70), (150, 60), (265, 60), (205, 100), (205, 125)],
        'Arm_L': [(160, 150)],
        'Arm_R': [(255, 150)],
        'Body': [SHIRT_B, (225, 145), (205, 152), (192, 160), (220, 160), BIB_B, (195, 165), (222, 165)],
        'Tail': [(185, 198)],
        'Leg_R': [(228, 220)],
    },
    open={'Body': [[(150, 192), (266, 192), (266, 206), (150, 206)]]},
    cuts=[
        # sleeves and shirt share regions; the crease lines split them
        ('Body', [(181, 128), (240, 128), (232, 136), (240, 162), (244, 192), (171, 192), (174, 162), (181, 136)],
         [(160, 150), (255, 150)]),
        ('Leg_R', [(207, 196), (270, 196), (270, 240), (207, 240)], [BIB_B]),
    ],
    paint={
        'Body': [
            near([(178, 124), (236, 124), (244, 136), (240, 146), (174, 146), (170, 136)], src=[SHIRT_B, (225, 145)]),
            erase([(146, 170), (222, 170), (222, 200), (146, 200)]),
            mirror([(150, 168), (209, 168), (209, 200), (150, 200)], 208),
            near([(188, 172), (228, 172), (228, 197), (188, 197)], src=[BIB_B], outline=False, smooth=False,
                 holes_only=True),
            near([(176, 144), (168, 150), (165, 162), (166, 174), (174, 178), (178, 150)], src=[BIB_B]),
            near([(238, 144), (246, 150), (249, 162), (248, 174), (240, 178), (236, 150)], src=[BIB_B]),
            near([(178, 140), (170, 142), (166, 150), (180, 152)], src=[SHIRT_B]),
            near([(236, 140), (244, 142), (248, 150), (234, 152)], src=[(225, 145)]),
        ],
        'Head': [clip([(100, 0), (320, 0), (320, 112), (270, 114), (262, 121), (254, 128), (244, 133), (226, 138),
                       (208, 139.5), (190, 138), (172, 133), (162, 128), (154, 121), (146, 114), (100, 112)])],
        'Leg_R': [
            near([(216, 192), (208, 192), (208, 209), (216, 209)], smooth=False),
            extrude(199, 18),
        ],
    },
)

VIEWS = {'Front': FRONT, 'Side': SIDE, 'Back': BACK}


# --- helpers ------------------------------------------------------------------------

def load_cell(r, c):
    im = Image.open(SHEET).crop((c * CELL_W, r * CELL_H, (c + 1) * CELL_W, (r + 1) * CELL_H))
    im = im.convert('RGBa').resize((CELL_W * SCALE, CELL_H * SCALE), Image.LANCZOS).convert('RGBA')
    return np.asarray(im).astype(np.float32)


def chaikin(pts, it=3):
    pts = np.asarray(pts, float)
    for _ in range(it):
        nxt = np.roll(pts, -1, 0)
        q, r = 0.75 * pts + 0.25 * nxt, 0.25 * pts + 0.75 * nxt
        pts = np.stack([q, r], 1).reshape(-1, 2)
    return pts


def coverage(pts, shape, smooth=True):
    """Anti-aliased coverage (0..1) of a polygon given in 1x coordinates."""
    h, w = shape
    big = Image.new('L', (w * SS, h * SS), 0)
    k = SCALE * SS
    p = chaikin(pts) if smooth else np.asarray(pts, float)
    ImageDraw.Draw(big).polygon([(x * k, y * k) for x, y in p], fill=255)
    return np.asarray(big.resize((w, h), Image.BOX)).astype(np.float32) / 255


def segment(rgba):
    lum = rgba[..., :3].mean(2)
    interior = (rgba[..., 3] > 160) & (lum > INTERIOR_LUM)
    lab, n = ndimage.label(interior)
    return interior, lab, n


def region_at(lab, pt):
    x, y = int(round(pt[0] * SCALE)), int(round(pt[1] * SCALE))
    if lab[y, x]:
        return lab[y, x]
    # seed landed on a line: nearest labelled pixel
    win = lab[max(0, y - 6):y + 7, max(0, x - 6):x + 7]
    ys, xs = np.nonzero(win)
    if len(ys) == 0:
        raise ValueError(f'seed {pt} hits no region')
    i = np.argmin((ys - 6) ** 2 + (xs - 6) ** 2)
    return win[ys[i], xs[i]]


def assign(spec, rgba):
    interior, lab, n = segment(rgba)
    names = list(spec['layers'])
    pid = {p: i + 1 for i, p in enumerate(names)}
    region_part = np.zeros(n + 1, int)
    for part, pts in spec['seeds'].items():
        for pt in pts:
            region_part[region_at(lab, pt)] = pid[part]
    part_map = region_part[lab] * interior
    for part, poly, only in spec.get('cuts', []):
        m = (coverage(poly, lab.shape, smooth=False) > 0.5) & interior
        if only:
            m &= np.isin(lab, [region_at(lab, s) for s in only])
        part_map[m] = pid[part]

    layer = np.array([spec['layers'][p] for p in names], float)
    dists = np.stack([ndimage.distance_transform_edt(part_map != pid[p]) for p in names])
    # unassigned interior specks: sitting between two parts they are gaps in the
    # outline (treat as outline), otherwise details like the nose (nearest part)
    known = part_map > 0
    close = (dists <= OUTLINE_R + 2).sum(0)
    gap = interior & ~known & (close >= 2)
    interior = interior & ~gap
    _, (iy, ix) = ndimage.distance_transform_edt(~known, return_indices=True)
    part_map = np.where(interior & ~known, part_map[iy, ix], part_map)

    # outline pixels -> top-most part within OUTLINE_R, else the nearest part
    opaque = rgba[..., 3] > 2
    line = opaque & ~interior
    dists = np.stack([ndimage.distance_transform_edt(part_map != pid[p]) for p in names])
    score = np.where(dists <= OUTLINE_R, layer[:, None, None] * 1000 - dists, -1e9 - dists)
    best = np.argmax(score, 0) + 1
    part_map = np.where(line, best, part_map)
    part_map[~opaque] = 0
    return part_map, pid, interior, lab, gap


def unpremul(img):
    a = img[..., 3:4] / 255
    return img[..., :3] / np.maximum(a, 1e-3)


def paint_part(img, own, interior, lab, ops):
    """img: premultiplied float RGBA of this part only (0 elsewhere)."""
    h, w = own.shape
    img = img.copy()
    for op in ops:
        alpha = img[..., 3] / 255
        if op['op'] == 'extrude':
            at, n = int(round(op['at'] * SCALE)), int(round(op['length'] * SCALE))
            if op['dir'] in ('up', 'down'):
                step = -1 if op['dir'] == 'up' else 1
                for i in range(1, n + 1):
                    img[at + step * i] = img[at]
            else:
                step = -1 if op['dir'] == 'left' else 1
                for i in range(1, n + 1):
                    img[:, at + step * i] = img[:, at]
            continue
        cov = coverage(op['pts'], (h, w), op['smooth'])
        if op['op'] == 'clip':
            img *= cov[..., None]
            continue
        core = ndimage.binary_erosion(interior, iterations=2)
        if op['op'] == 'erase':
            img[(cov > 0.5) & ~(own & core)] = 0
            continue
        if op['op'] == 'mirror':
            ax = op['axis'] * SCALE
            xs = np.clip((2 * ax - np.arange(w)).round().astype(int), 0, w - 1)
            src = img[:, xs]
            take = (cov > 0) & (alpha < 0.5)
            img[take] = src[take] * cov[take, None]
            continue
        # nearest colour from the source regions (default: this part's interior), smoothed
        src = interior & own
        if op['src']:
            src &= np.isin(lab, [region_at(lab, s) for s in op['src']])
        if op['src_poly']:
            src &= coverage(op['src_poly'], (h, w), smooth=False) > 0.5
        src = ndimage.binary_erosion(src, iterations=2)
        _, (iy, ix) = ndimage.distance_transform_edt(~src, return_indices=True)
        col = unpremul(img)
        fill = col[iy, ix]
        blur = np.stack([ndimage.gaussian_filter(fill[..., k], 2.0) for k in range(3)], -1)
        target = (cov > 0) & ~(own & core & (alpha > 0.98))
        if op['holes_only']:
            target &= alpha < 0.5
        new_alpha = np.maximum(alpha, cov)
        if op['outline']:
            dist_in = ndimage.distance_transform_edt(new_alpha > 0.5)
            t = np.clip(OUTLINE_W + 0.5 - dist_in, 0, 1)[..., None]  # 1 inside the band, AA inner edge
            blur = blur * (1 - t) + np.array(OUTLINE_RGB, float) * t
        a_new = np.where(target, new_alpha, alpha)
        out = np.where(target[..., None], blur, col)
        img[..., :3] = out * a_new[..., None]
        img[..., 3] = a_new * 255
    return img


def sharpen(rgba):
    im = Image.fromarray(rgba.astype(np.uint8))
    rgb = im.convert('RGB').filter(ImageFilter.UnsharpMask(**SHARPEN))
    return np.dstack([np.asarray(rgb), rgba[..., 3]]).astype(np.float32)


def cut_view(spec):
    rgba = load_cell(*spec['frame'])
    part_map, pid, interior, lab, gap = assign(spec, rgba)   # segment the unsharpened pixels
    rgba = sharpen(rgba)
    opaque = rgba[..., 3] > 2
    # the sheet was matted on white: semi-transparent rim pixels are whitish halos,
    # and outline gaps show the colour behind. Both become outline colour so nothing
    # glows when parts overlap differently.
    rim = (opaque & ~interior & (rgba[..., 3] < 250)) | gap
    rgba[rim, :3] = OUTLINE_RGB
    prem = rgba.copy()
    prem[..., :3] *= rgba[..., 3:4] / 255
    parts = {}
    for part, i in pid.items():
        own = part_map == i
        cl, n = ndimage.label(own)
        if n > 1:
            sizes = ndimage.sum(own, cl, range(1, n + 1))
            own = np.isin(cl, 1 + np.nonzero(sizes >= max(60, sizes.max() * 0.02))[0])
        img = paint_part(prem * own[..., None], own, interior, lab, spec.get('paint', {}).get(part, []))
        open_cov = np.zeros(own.shape, np.float32)
        for poly in spec.get('open', {}).get(part, []):
            open_cov = np.maximum(open_cov, coverage(poly, own.shape, smooth=False))
        parts[part] = ink(img, open_cov)
    return parts, part_map


def ink(img, open_cov):
    """Line finish: round the silhouette off and redraw a uniform anti-aliased outline
    all around it (cut edges included), except inside open_cov."""
    a = img[..., 3] / 255
    ys, xs = np.nonzero(a > 0.01)
    if len(ys) == 0:
        return img
    h, w = a.shape
    pad = 12 + int(ROUND_SIGMA * 3)
    y0, y1 = max(ys.min() - pad, 0), min(ys.max() + pad + 1, h)
    x0, x1 = max(xs.min() - pad, 0), min(xs.max() + pad + 1, w)
    ac, oc = a[y0:y1, x0:x1].copy(), open_cov[y0:y1, x0:x1]
    col = unpremul(img)[y0:y1, x0:x1]
    # pinholes left between neighbouring parts count as inside
    solid = ac > 0.5
    holes = ndimage.binary_fill_holes(solid) & ~solid
    ac[holes] = 1
    solid |= holes
    # every pixel takes the nearest solid colour, so whatever the rounding adds is filled sensibly
    _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
    col = np.where(solid[..., None], col, col[iy, ix])
    k = 4
    up = ndimage.zoom(ac, k, order=1, grid_mode=True, mode='nearest')
    # Gaussian blur + threshold = curvature smoothing: corners turn into arcs, bumps and
    # notches smaller than ~ROUND_SIGMA melt away, long straight / gently curved edges stay
    rounded = ndimage.gaussian_filter(up, ROUND_SIGMA * k) > 0.5
    if oc.any():   # keep open edges as they were
        keep = ndimage.zoom(oc, k, order=1, grid_mode=True, mode='nearest') > 0.5
        rounded = np.where(keep, up > 0.5, rounded)
    band = rounded & (ndimage.distance_transform_edt(rounded) <= INK_W * k)
    pool = lambda m: m.reshape(m.shape[0] // k, k, m.shape[1] // k, k).mean((1, 3))
    cov, f = pool(rounded), pool(band)
    f = f / np.maximum(cov, 1e-3) * (1 - oc)
    alpha = cov * (1 - oc) + ac * oc
    col = col * (1 - f[..., None]) + np.array(OUTLINE_RGB, np.float32) * f[..., None]
    out = np.zeros_like(img)
    out[y0:y1, x0:x1, :3] = col * alpha[..., None]
    out[y0:y1, x0:x1, 3] = alpha * 255
    return out


def to_image(prem):
    a = prem[..., 3:4]
    rgb = np.where(a > 0, prem[..., :3] * 255 / np.maximum(a, 1e-3), 0)
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(a, 0, 255)]).astype(np.uint8))


def cut_all():
    """{view: {part: RGBA image at SCALE x cell size}} for every view."""
    return {v: {p: to_image(img) for p, img in cut_view(spec)[0].items()} for v, spec in VIEWS.items()}


if __name__ == '__main__':
    # debug: full-cell part images (same canvas as the source frame) into ./parts_debug
    out = 'parts_debug'
    os.makedirs(out, exist_ok=True)
    for vname, parts in cut_all().items():
        for p, im in parts.items():
            if im.getbbox():
                im.save(os.path.join(out, f'{vname}_{p}.png'))
        print(vname, 'done')
