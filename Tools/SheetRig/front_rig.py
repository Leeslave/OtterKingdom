"""Front-view rigs for otters drawn as a single front illustration
(ArtSource/Otter/FrontRig/Source/<Prefix>_Front.png): pajama, miner, fisher, chief.

Same pipeline as cut_parts (the farmer sheet), adapted to big, crisp drawings:
  1. the drawing is scaled so ear top -> soles is TARGET_H px (= the farmer's 2x parts),
  2. pixels are assigned to parts by zone polygons (pixelwise, later zones win; small
     outlined regions go whole to their majority zone), seeds (whole outlined regions)
     and cuts (polygon x listed regions), outline pixels go to the top-most part
     whose interior is within reach,
  3. hidden areas are painted in (cut_parts.paint_part ops), the silhouette re-inked
     with the drawing's own outline colour; 'open' polygons mark cut edges that sit
     on top of the same colour (sleeve over shirt) and stay outline-less, feathered,
  4. a closed-eye copy of the head is painted for blinking.
Spec coordinates are pixels of the ORIGINAL drawing (x right, y down).

The rig has one view (front). Bones: Root (feet centre) -> Leg_L, Leg_R, Body ->
Head (-> Pompom), Arm_L (-> Bobber), Arm_R. Props held in a paw are part of that
arm's sprite, so they follow the arm.
"""
import math
import os
from contextlib import contextmanager
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

import cut_parts as cp
import rig

SRC_DIR = os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'FrontRig', 'Source')
TARGET_H = 395.0       # ear top -> soles, working px (the farmer's 2x parts: 60 -> 452)
ROUND_SIGMA = 2.0      # these drawings are crisp: tidy the cut edges, keep the fur tufts
LINE_PX = 12.5         # outline width of the source drawings (original px)
FEATHER = 1.3          # px, alpha ramp on open (outline-less) cut edges
SS = 4


# --- spec helpers (original px) ---------------------------------------------------------

def near(pts, src=None, outline=True, smooth=True, holes_only=False, src_poly=None):
    return cp.near(pts, src, outline, smooth, holes_only, src_poly)


def ellipse(cx, cy, rx, ry, rot=0.0, n=40):
    r = math.radians(rot)
    c, s = math.cos(r), math.sin(r)
    return [(cx + rx * math.cos(t) * c - ry * math.sin(t) * s, cy + rx * math.cos(t) * s + ry * math.sin(t) * c)
            for t in np.linspace(0, 2 * math.pi, n, endpoint=False)]


def mirror_x(pts, axis):
    return [(2 * axis - x, y) for x, y in pts][::-1]


def disc_clip(cx, cy, r, n=48):
    return cp.clip(ellipse(cx, cy, r, r, n=n))


EVERYTHING = [(-50, -50), (5000, -50), (5000, 5000), (-50, 5000)]


# --- characters ------------------------------------------------------------------------------

def _pajama():
    ax = 623.5   # the drawing's symmetry axis
    # sleeve: shoulder -> along the collar line -> down the sleeve seam -> under the paw
    arm_l = [(300, 720), (380, 700), (440, 694), (448, 720), (452, 748), (472, 768), (486, 778), (474, 806),
             (462, 840), (452, 870), (446, 900), (441, 935), (425, 965), (300, 965)]
    collar_l = [(438, 692), (464, 692), (466, 734), (480, 750), (508, 754), (508, 772), (476, 772), (452, 754),
                (444, 724)]
    open_l = [(428, 688), (452, 688), (458, 744), (478, 764), (496, 772), (490, 790), (468, 782), (446, 758),
              (436, 724)]
    under_l = [(434, 706), (530, 706), (530, 968), (420, 968), (424, 900), (438, 840), (450, 790), (444, 740)]
    return dict(
        prefix='PajamaOtter', name='잠옷 해달',
        ear_top=300, root_x=600,
        layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Arm_L': 20, 'Arm_R': 20, 'Head': 30, 'Pompom': 32},
        zones=[
            ('Body', EVERYTHING),
            ('Head', [(-50, -50), (1300, -50), (1300, 640), (910, 640), (895, 652), (870, 668), (830, 688),
                      (770, 704), (700, 714), (620, 720), (540, 718), (470, 711), (400, 698), (355, 682),
                      (325, 665), (305, 645), (-50, 640)]),
            ('Arm_L', arm_l),
            ('Arm_R', mirror_x(arm_l, ax)),
            ('Leg_L', [(380, 1008), (602, 1008), (602, 1300), (380, 1300)]),
            ('Leg_R', [(602, 1008), (860, 1008), (860, 1300), (602, 1300)]),
        ],
        seeds={'Pompom': [(1046, 468)]},
        # outline pixels forced to a part: the collar edge stays with the body when the arms swing
        lines=[('Body', collar_l), ('Body', mirror_x(collar_l, ax))],
        paint={
            # cap tip under the pompom, rounded off so the pompom can swing
            'Head': [near(ellipse(1004, 404, 24, 19, 30), src=[(722, 265)])],
            'Body': [near(under_l, src=[(488, 855)]), near(mirror_x(under_l, ax), src=[(728, 848)])],
            'Leg_L': [cp.extrude(1011, 50)],
            'Leg_R': [cp.extrude(1011, 50)],
        },
        open={
            'Body': [[(400, 998), (840, 998), (840, 1018), (400, 1018)]],
            'Arm_L': [open_l],
            'Arm_R': [mirror_x(open_l, ax)],
        },
        blink=dict(erase=[[(410, 450), (560, 450), (560, 615), (410, 615)], [(676, 450), (826, 450), (826, 615), (676, 615)]],
                   lids=[[(452, 546), (472, 560), (491, 565), (510, 560), (530, 546)],
                         [(706, 546), (726, 560), (745, 565), (764, 560), (784, 546)]],
                   brows=[[(424, 508), (470, 492), (520, 474), (544, 468)],
                          [(812, 508), (766, 492), (716, 474), (692, 468)]],
                   width=10, brow_width=12),
        bones=[
            ('Root', None, (600, None), None),
            ('Leg_L', 'Root', (515, 1012), 'Leg_L'),
            ('Leg_R', 'Root', (690, 1012), 'Leg_R'),
            ('Body', 'Root', (602, 1000), 'Body'),
            ('Arm_L', 'Body', (452, 768), 'Arm_L'),
            ('Arm_R', 'Body', (795, 768), 'Arm_R'),
            ('Head', 'Body', (614, 712), 'Head'),
            ('Pompom', 'Head', (1012, 420), 'Pompom'),
        ],
    )


def _miner():
    return dict(
        prefix='MinerOtter', name='광부 해달',
        ear_top=172, root_x=615,
        layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Arm_L': 20, 'Arm_R': 20, 'Head': 30},
        zones=[
            ('Body', EVERYTHING),
            ('Arm_L', [(-50, 600), (300, 640), (340, 660), (400, 682), (478, 697), (474, 760), (462, 820),
                       (445, 850), (420, 875), (408, 900), (404, 960), (412, 1010), (-50, 1010)]),
            ('Arm_R', [(1300, 600), (950, 625), (900, 650), (840, 672), (764, 692), (772, 760), (785, 820),
                       (822, 846), (838, 900), (845, 950), (848, 1000), (1300, 1000)]),
            ('Head', [(-50, -50), (1300, -50), (1300, 600), (980, 600), (950, 625), (900, 650), (840, 672),
                      (760, 692), (680, 704), (620, 708), (560, 706), (480, 697), (400, 680), (340, 660),
                      (300, 640), (270, 610), (-50, 600)]),
            # below the seat's curved line (the overalls' hip curve stays on the body)
            ('Leg_L', [(380, 1012), (612, 1012), (612, 1300), (380, 1300)]),
            ('Leg_R', [(612, 1012), (880, 1012), (880, 1300), (612, 1300)]),
        ],
        # the overalls' edge next to the paws / pickaxe handle stays with the body
        lines=[('Body', [(398, 872), (420, 862), (426, 1004), (404, 1004)]),
               ('Body', [(812, 846), (838, 846), (848, 1000), (822, 1000)])],
        seeds={'Body': [(812, 912)]},
        paint={
            'Body': [near([(392, 688), (492, 688), (492, 990), (402, 990), (398, 900), (414, 860), (440, 800),
                           (452, 740)], src=[(624, 732)]),
                     near([(842, 688), (752, 688), (752, 990), (832, 990), (826, 900), (806, 850), (790, 800),
                           (778, 740)], src=[(624, 732)])],
            'Leg_L': [cp.extrude(1016, 60)],
            'Leg_R': [cp.extrude(1016, 60)],
        },
        open={'Body': [[(380, 1004), (880, 1004), (880, 1022), (380, 1022)]]},
        blink=dict(erase=[[(405, 440), (545, 440), (545, 575), (405, 575)], [(695, 440), (835, 440), (835, 575), (695, 575)]],
                   lids=[[(442, 520), (462, 528), (481, 531), (500, 528), (520, 520)],
                         [(718, 520), (738, 528), (757, 531), (776, 528), (796, 520)]],
                   brows=[[(424, 458), (472, 461), (528, 467)], [(814, 458), (766, 461), (710, 467)]],
                   width=10, brow_width=15),
        bones=[
            ('Root', None, (615, None), None),
            ('Leg_L', 'Root', (510, 1014), 'Leg_L'),
            ('Leg_R', 'Root', (720, 1014), 'Leg_R'),
            ('Body', 'Root', (615, 990), 'Body'),
            ('Arm_L', 'Body', (452, 752), 'Arm_L'),
            ('Arm_R', 'Body', (782, 752), 'Arm_R'),
            ('Head', 'Body', (620, 706), 'Head'),
        ],
    )


def _fisher():
    ax = 628.5
    # upper rod (above the paw), widened on the left for the line that runs along it
    rod = [(140, 150), (200, 140), (232, 300), (279, 560), (330, 700), (352, 790), (318, 798), (226, 560),
           (160, 330)]
    bobber = [(165, 458), (228, 458), (240, 560), (252, 600), (262, 612), (270, 640), (268, 692), (252, 728),
              (165, 728)]
    return dict(
        prefix='FishingOtter', name='낚시 해달',
        ear_top=290, root_x=632,
        layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Arm_L': 20, 'Arm_R': 20, 'Head': 30, 'Rod': 35,
                'Bobber': 36},
        zones=[
            ('Body', EVERYTHING),
            ('Arm_L', [(-50, 620), (260, 620), (290, 700), (340, 735), (420, 752), (436, 760), (430, 800),
                       (420, 840), (410, 880), (404, 940), (400, 1000), (398, 1070), (-50, 1070)]),
            ('Arm_R', [(1300, 620), (940, 620), (900, 700), (830, 735), (808, 733), (825, 800), (837, 867),
                       (846, 933), (852, 995), (1300, 995)]),
            ('Head', [(-50, -50), (1300, -50), (1300, 640), (975, 640), (950, 680), (915, 712), (860, 735),
                      (780, 748), (640, 754), (500, 750), (400, 738), (330, 715), (295, 690), (268, 650),
                      (-50, 640)]),
            ('Rod', rod),
            ('Bobber', bobber),
            ('Leg_L', [(420, 1032), (632, 1032), (632, 1300), (420, 1300)]),
            ('Leg_R', [(632, 1032), (860, 1032), (860, 1300), (632, 1300)]),
        ],
        lines=[('Body', [(838, 870), (858, 870), (866, 998), (846, 998)])],
        paint={
            # hat brim hidden behind the rod (the cheek is right of the rod, nothing to fill there)
            'Head': [near([(244, 498), (290, 468), (304, 594), (264, 598), (240, 566)], src=[(663, 393)])],
            'Body': [near([(396, 748), (470, 748), (470, 990), (392, 990), (398, 900), (408, 840), (424, 790)],
                          src=[(639, 808)]),
                     near([(800, 732), (850, 732), (862, 995), (800, 995)], src=[(639, 808)])],
            'Leg_L': [cp.extrude(1035, 50)],
            'Leg_R': [cp.extrude(1035, 50)],
        },
        open={'Body': [[(420, 1024), (860, 1024), (860, 1042), (420, 1042)]]},
        blink=dict(erase=[[(440, 528), (560, 528), (560, 665), (440, 665)], [(697, 528), (817, 528), (817, 665), (697, 665)]],
                   lids=[[(470, 608), (485, 615), (498, 617), (511, 615), (526, 608)],
                         [(731, 608), (746, 615), (759, 617), (772, 615), (787, 608)]],
                   brows=[[(460, 552), (536, 552)], [(721, 552), (797, 552)]],
                   width=10, brow_width=17),
        bones=[
            ('Root', None, (632, None), None),
            ('Leg_L', 'Root', (530, 1036), 'Leg_L'),
            ('Leg_R', 'Root', (735, 1036), 'Leg_R'),
            ('Body', 'Root', (632, 1030), 'Body'),
            ('Arm_L', 'Body', (414, 790), 'Arm_L'),
            ('Rod', 'Arm_L', (330, 840), 'Rod'),
            ('Bobber', 'Rod', (218, 458), 'Bobber'),
            ('Arm_R', 'Body', (830, 780), 'Arm_R'),
            ('Head', 'Body', (630, 748), 'Head'),
        ],
    )


def _chief():
    return dict(
        prefix='ChiefOtter', name='촌장 해달',
        ear_top=262, root_x=620,
        layers={'Leg_L': 0, 'Leg_R': 0, 'Body': 10, 'Arm_L': 20, 'Arm_R': 20, 'Scarf': 25, 'Head': 30},
        zones=[
            ('Body', EVERYTHING),
            ('Arm_L', [(-50, 620), (330, 640), (380, 690), (430, 712), (468, 722), (468, 760), (466, 831),
                       (452, 881), (445, 931), (445, 995), (-50, 995)]),
            ('Arm_R', [(1300, 620), (900, 640), (860, 690), (800, 712), (775, 725), (777, 760), (791, 831),
                       (802, 881), (809, 931), (813, 995), (1300, 995)]),
            ('Head', [(-50, -50), (1300, -50), (1300, 600), (940, 600), (905, 640), (860, 670), (800, 690),
                      (720, 702), (620, 706), (520, 703), (440, 692), (380, 675), (340, 650), (310, 610),
                      (-50, 600)]),
            ('Leg_L', [(430, 1000), (616, 1000), (616, 1300), (430, 1300)]),
            ('Leg_R', [(616, 1000), (830, 1000), (830, 1300), (616, 1300)]),
        ],
        # neckerchief knot + tails (whole outlined regions)
        seeds={'Scarf': [(743, 716), (807, 717), (774, 758)]},
        paint={
            # neckerchief band under the knot, shirt / vest under the arms, sleeve under the tails
            'Body': [near([(706, 698), (792, 700), (786, 738), (706, 748)], src=[(651, 741)]),
                     near([(440, 740), (476, 740), (476, 990), (436, 990), (440, 900)], src=[(529, 869)]),
                     near([(776, 740), (800, 740), (816, 990), (776, 990)], src=[(705, 887)])],
            'Arm_R': [near([(786, 716), (846, 712), (884, 742), (884, 792), (802, 802)], src=[(826, 829)])],
            'Leg_L': [cp.extrude(1003, 50)],
            'Leg_R': [cp.extrude(1003, 50)],
        },
        open={'Body': [[(430, 992), (830, 992), (830, 1010), (430, 1010)]]},
        blink=dict(erase=[[(430, 478), (552, 478), (552, 600), (430, 600)], [(696, 478), (818, 478), (818, 600), (696, 600)]],
                   lids=[[(460, 552), (475, 560), (490, 562), (505, 560), (520, 552)],
                         [(728, 552), (743, 560), (758, 562), (773, 560), (788, 552)]],
                   brows=[[(448, 496), (534, 496)], [(714, 496), (800, 496)]],
                   width=10, brow_width=16),
        bones=[
            ('Root', None, (620, None), None),
            ('Leg_L', 'Root', (525, 1004), 'Leg_L'),
            ('Leg_R', 'Root', (712, 1004), 'Leg_R'),
            ('Body', 'Root', (620, 995), 'Body'),
            ('Scarf', 'Body', (745, 722), 'Scarf'),
            ('Arm_L', 'Body', (448, 785), 'Arm_L'),
            ('Arm_R', 'Body', (792, 785), 'Arm_R'),
            ('Head', 'Body', (622, 702), 'Head'),
        ],
    )


CHARS = {'Pajama': _pajama(), 'Miner': _miner(), 'Fisher': _fisher(), 'Chief': _chief()}


# --- pipeline --------------------------------------------------------------------------------

@contextmanager
def working(spec, s):
    saved = cp.SCALE, cp.OUTLINE_RGB, cp.ROUND_SIGMA, cp.INK_W
    cp.SCALE, cp.OUTLINE_RGB, cp.ROUND_SIGMA, cp.INK_W = s, spec['ink'], ROUND_SIGMA, LINE_PX * s
    try:
        yield
    finally:
        cp.SCALE, cp.OUTLINE_RGB, cp.ROUND_SIGMA, cp.INK_W = saved


def load(spec):
    im = Image.open(os.path.join(SRC_DIR, spec['prefix'] + '_Front.png')).convert('RGBA')
    a = np.asarray(im).astype(np.float32)
    a[a[..., 3] < 8] = 0
    opaque = a[..., 3] > 128
    spec.setdefault('feet', int(np.nonzero(opaque.any(1))[0].max()))
    # the drawing's own outline colour (darkest common colour)
    lum = a[..., :3].mean(2)
    spec.setdefault('ink', tuple(int(v) for v in np.median(a[..., :3][(a[..., 3] > 250) & (lum < 45)], 0)))
    s = TARGET_H / (spec['feet'] - spec['ear_top'])
    w = Image.fromarray(a.astype(np.uint8)).convert('RGBa')
    w = w.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS).convert('RGBA')
    return np.asarray(w).astype(np.float32), s


def zone_map(spec, shape, pid):
    z = np.zeros(shape, np.int32)
    for part, poly in spec['zones']:
        z[cp.coverage(poly, shape, smooth=False) > 0.5] = pid[part]
    return z


def assign(spec, rgba):
    interior, lab, n = cp.segment(rgba)
    names = list(spec['layers'])
    pid = {p: i + 1 for i, p in enumerate(names)}
    zones = zone_map(spec, lab.shape, pid)

    # pixelwise zones; small outlined regions (stars, buttons) go whole to their majority
    part_map = np.where(interior, zones, 0)
    counts = np.zeros((n + 1, len(names) + 1), np.int64)
    np.add.at(counts, (lab[interior], zones[interior]), 1)
    size = counts.sum(1)
    small = (size > 0) & (size < (60 * cp.SCALE) ** 2)
    majority = counts.argmax(1)
    m = interior & small[lab]
    part_map[m] = majority[lab[m]]
    for part, pts in spec.get('seeds', {}).items():
        for pt in pts:
            part_map[lab == cp.region_at(lab, pt)] = pid[part]
    for part, poly, only in spec.get('cuts', []):
        mm = (cp.coverage(poly, lab.shape, smooth=False) > 0.5) & interior
        if only:
            mm &= np.isin(lab, [cp.region_at(lab, s) for s in only])
        part_map[mm] = pid[part]

    # outline pixels: top-most part with interior within reach, else the zone they lie in
    opaque = rgba[..., 3] > 2
    line = opaque & ~interior
    layer = np.array([spec['layers'][p] for p in names], float)
    dists = np.stack([ndimage.distance_transform_edt(part_map != pid[p]) for p in names])
    score = np.where(dists <= cp.OUTLINE_R, layer[:, None, None] * 1000 - dists, -1e9)
    best = np.argmax(score, 0) + 1
    reach = score.max(0) > -1e9
    part_map = np.where(line & reach, best, part_map)
    part_map = np.where(line & ~reach, zones, part_map)
    for part, poly in spec.get('lines', []):
        part_map[line & (cp.coverage(poly, lab.shape, smooth=False) > 0.5)] = pid[part]
    part_map[~opaque] = 0
    return part_map, pid, interior, lab


def feather(prem, open_cov):
    """Soften the hard alpha step of open cut edges."""
    if not open_cov.any():
        return prem
    a = prem[..., 3] / 255
    soft = np.minimum(a, ndimage.gaussian_filter(a, FEATHER) * 1.0)
    na = a * (1 - open_cov) + soft * open_cov
    out = prem.copy()
    k = np.where(a > 1e-3, na / np.maximum(a, 1e-3), 0)
    out *= k[..., None]
    return out


def unpremul(prem):
    a = prem[..., 3:4] / 255
    out = prem.copy()
    out[..., :3] = np.where(a > 0, prem[..., :3] / np.maximum(a, 1e-3), 0)
    return out


def stroke(shape, pts, width):
    """Anti-aliased coverage of a smooth round-capped polyline (spec coordinates)."""
    p = np.asarray(pts, float)
    for _ in range(3):
        q = 0.75 * p[:-1] + 0.25 * p[1:]
        r = 0.25 * p[:-1] + 0.75 * p[1:]
        p = np.vstack([p[:1], np.stack([q, r], 1).reshape(-1, 2), p[-1:]])
    h, w = shape
    k = cp.SCALE * SS
    big = Image.new('L', (w * SS, h * SS), 0)
    d = ImageDraw.Draw(big)
    q = [(x * k, y * k) for x, y in p]
    rw = width * k
    d.line(q, fill=255, width=max(1, int(round(rw))), joint='curve')
    for x, y in (q[0], q[-1]):
        d.ellipse([x - rw / 2, y - rw / 2, x + rw / 2, y + rw / 2], fill=255)
    return np.asarray(big.resize((w, h), Image.BOX)).astype(np.float32) / 255


def blink_head(head, spec):
    """Closed eyes: the eyes (+ brows) are the dark blobs inside the blink search boxes;
    they are painted out with the surrounding fur and new lid arcs / brows drawn."""
    b = spec['blink']
    a = unpremul(head)
    lum = a[..., :3].mean(-1)
    box = np.zeros(lum.shape, np.float32)
    for poly in b['erase']:
        box = np.maximum(box, cp.coverage(poly, box.shape, smooth=False))
    # eyes / brows are ink-dark (lum < 45), fur is lum >= 90 (measured on all four drawings)
    dark = (box > 0.5) & (a[..., 3] > 128) & (lum < 70)
    lab, n = ndimage.label(dark)
    if n:
        sizes = ndimage.sum(dark, lab, range(1, n + 1))
        dark = np.isin(lab, 1 + np.nonzero(sizes > 20)[0])
    grow = ndimage.binary_dilation(dark, iterations=3)          # + the anti-aliased rim
    cov = ndimage.gaussian_filter(grow.astype(np.float32), 0.8)
    known = (a[..., 3] > 240) & ~ndimage.binary_dilation(grow, iterations=2) & (lum > 85)
    _, (iy, ix) = ndimage.distance_transform_edt(~known, return_indices=True)
    fill = a[..., :3][iy, ix]
    fill = np.stack([ndimage.gaussian_filter(fill[..., i], 3.0) for i in range(3)], -1)
    a[..., :3] = a[..., :3] * (1 - cov[..., None]) + fill * cov[..., None]
    for pts, wd in [(p, b['width']) for p in b['lids']] + [(p, b.get('brow_width', 10)) for p in b.get('brows', [])]:
        c = stroke(cov.shape, pts, wd)[..., None]
        a[..., :3] = a[..., :3] * (1 - c) + np.array(spec['ink'], np.float32) * c
    out = a.copy()
    out[..., :3] *= out[..., 3:4] / 255
    return out


def cut(name):
    """{part: premultiplied float RGBA (working px)} incl. 'Head_Blink', and the scale."""
    spec = CHARS[name]
    rgba, s = load(spec)
    with working(spec, s):
        part_map, pid, interior, lab = assign(spec, rgba)
        opaque = rgba[..., 3] > 2
        rgba = rgba.copy()
        rgba[opaque & ~interior & (rgba[..., 3] < 250), :3] = spec['ink']   # no light halo on the rim
        prem = rgba.copy()
        prem[..., :3] *= rgba[..., 3:4] / 255
        parts = {}
        for part, i in pid.items():
            own = part_map == i
            cl, n = ndimage.label(own)
            if n > 1:
                sizes = ndimage.sum(own, cl, range(1, n + 1))
                own = np.isin(cl, 1 + np.nonzero(sizes >= max(40, sizes.max() * 0.03))[0])
            img = cp.paint_part(prem * own[..., None], own, interior, lab, spec.get('paint', {}).get(part, []))
            oc = np.zeros(own.shape, np.float32)
            for poly in spec.get('open', {}).get(part, []):
                oc = np.maximum(oc, cp.coverage(poly, own.shape, smooth=False))
            parts[part] = feather(cp.ink(img, oc), oc)
        if 'blink' in spec:
            parts['Head_Blink'] = blink_head(parts['Head'], spec)
    return parts, s


def to_images(parts):
    return {k: cp.to_image(v) for k, v in parts.items()}


def build(name, parts=None):
    """(bones, images, alts) for rig.Rig; pivots converted to rig units (working px / rig.S)."""
    spec = CHARS[name]
    if parts is None:
        parts, s = cut(name)
        ims = to_images(parts)
    else:
        ims, s = parts
    k = s / rig.S
    bones = []
    for i, (bname, parent, (x, y), part) in enumerate(spec['bones']):
        if y is None:
            if 'feet' not in spec:
                load(spec)
            y = spec['feet']
        layer = spec['layers'].get(part, 0) if part else 0
        bones.append(rig.Bone(bname, parent, (x * k, y * k), part, layer))
    alts = {'Head': {'blink': ims['Head_Blink']}} if 'Head_Blink' in ims else {}
    return bones, ims, alts


def make_rig(name, parts=None):
    bones, ims, alts = build(name, parts)
    return rig.Rig(bones, ims, alts=alts)
