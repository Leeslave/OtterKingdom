"""Hairstyles, manes and small role items drawn onto the front-rig heads / bodies, in the
drawings' style: soft vertical gradient + paper grain, ink outline as thick as the
drawings' own (LINE orig px), thin ink strands.

Back styles are drawn behind the head (they show around the face / under the hat brim),
front styles on top of hatless heads only, and never below the brow line, so the
drawings' eyes and brows stay untouched.

Coordinates in GEOM / BODY and in the style functions are pixels of the ORIGINAL
drawing; `s` converts to the working canvas the arrays live on.
"""
import math
import numpy as np
from scipy import ndimage

import cut_parts as cp
import front_rig as fr

LINE = 11.0      # outline width, orig px (the drawings' lines are ~12.5)
STRAND = 5.0     # inner strand lines, orig px

# head geometry of the four drawings (orig px)
GEOM = {
    'Chief': dict(cx=623, face_l=293, face_r=954, face_y=540, chin=706, top=300, brow=470,
                  ear_l=(392, 335), ear_r=(855, 335), pin=(560, 250)),
    'Pajama': dict(cx=620, face_l=287, face_r=961, face_y=540, chin=712, top=345, brow=470,
                   ear_l=(345, 380), ear_r=(915, 380), pin=(705, 215)),
    'Miner': dict(cx=619, face_l=254, face_r=984, face_y=580, chin=708, top=450, brow=450,
                  ear_l=(255, 245), ear_r=(985, 245), pin=(470, 265)),
    'Fisher': dict(cx=630, face_l=314, face_r=986, face_y=620, chin=750, top=540, brow=535,
                   ear_l=(340, 330), ear_r=(920, 330), pin=(470, 415)),
}
# body anchors (orig px): neck joint, chest, waist
BODY = {
    'Chief': dict(cx=622, neck=702, chest=815, waist=975, half=135),
    'Pajama': dict(cx=612, neck=712, chest=820, waist=965, half=150),
    'Miner': dict(cx=620, neck=706, chest=830, waist=985, half=150),
    'Fisher': dict(cx=630, neck=748, chest=860, waist=1000, half=150),
}


# --- primitives (working px inside; inputs in orig px) ---------------------------------------------

class Pen:
    """Draws on one straight-alpha array whose pixels are `s` x the original drawing."""

    def __init__(self, a, s, ink, seed=0):
        self.a, self.s = a, s
        self.ink = np.asarray(ink, np.float32)
        self.rng = np.random.default_rng(seed)

    # coverage helpers
    def poly(self, pts, smooth=True):
        with _px():
            return cp.coverage([(x * self.s, y * self.s) for x, y in pts], self.a.shape[:2], smooth)

    def ell(self, cx, cy, rx, ry, rot=0.0):
        return self.poly(fr.ellipse(cx, cy, rx, ry, rot, n=48), smooth=False)

    def line(self, pts, width):
        with _px():
            return fr.stroke(self.a.shape[:2], [(x * self.s, y * self.s) for x, y in pts], width * self.s)

    # painting
    def fill(self, cov, color, light=1.12, dark=0.8, line=LINE, grain=True):
        """Gradient-filled shape with an ink outline inside its edge."""
        sh = self.a.shape[:2]
        ys, xs = np.nonzero(cov > 0.05)
        if len(ys) == 0:
            return
        y0, y1 = ys.min(), ys.max()
        t = np.clip((np.arange(sh[0], dtype=np.float32)[:, None] - y0) / max(y1 - y0, 1), 0, 1)[..., None]
        col = np.asarray(color, np.float32)
        g = col * light * (1 - t) + col * dark * t
        g = np.broadcast_to(g, sh + (3,)).copy()
        if grain:
            n = ndimage.gaussian_filter(self.rng.standard_normal(sh).astype(np.float32), 1.2)
            g *= (1 + 0.05 * n / max(n.std(), 1e-6))[..., None]
        if line:
            f = _band(cov, line * self.s)[..., None]
            g = g * (1 - f) + self.ink * f
        self.over(cov, np.clip(g, 0, 255))

    def stroke(self, pts, width, color=None, alpha=1.0, clip=None):
        c = self.line(pts, width)
        if clip is not None:
            c = c * clip
        self.over(c, self.ink if color is None else color, alpha)

    def over(self, cov, color, alpha=1.0):
        a = self.a
        c = np.clip(cov * alpha, 0, 1)
        col = np.broadcast_to(np.asarray(color, np.float32), a.shape[:2] + (3,))
        da = a[..., 3] / 255
        oa = c + da * (1 - c)
        rgb = (col * c[..., None] + a[..., :3] * (da * (1 - c))[..., None]) / np.maximum(oa, 1e-6)[..., None]
        a[..., :3] = np.where(oa[..., None] > 0, rgb, a[..., :3])
        a[..., 3] = oa * 255


class _px:
    def __enter__(self):
        self.saved = cp.SCALE
        cp.SCALE = 1

    def __exit__(self, *exc):
        cp.SCALE = self.saved


def _band(cov, width):
    k = 4
    up = ndimage.zoom(cov, k, order=1, grid_mode=True, mode='nearest') > 0.5
    b = up & (ndimage.distance_transform_edt(up) <= width * k)
    pool = lambda m: m.reshape(m.shape[0] // k, k, m.shape[1] // k, k).mean((1, 3))
    return np.clip(pool(b) / np.maximum(pool(up), 1e-3), 0, 1)


def under(top, bottom):
    """`top` composited over `bottom` (straight alpha arrays)."""
    ta, ba = top[..., 3:4] / 255, bottom[..., 3:4] / 255
    oa = ta + ba * (1 - ta)
    rgb = (top[..., :3] * ta + bottom[..., :3] * ba * (1 - ta)) / np.maximum(oa, 1e-6)
    out = np.zeros_like(top)
    out[..., :3] = np.where(oa > 0, rgb, 0)
    out[..., 3:4] = oa * 255
    return out


def union(*covs):
    out = covs[0]
    for c in covs[1:]:
        out = np.maximum(out, c)
    return out


def mirror(pts, cx):
    return [(2 * cx - x, y) for x, y in pts]


def darker(c, k=0.75):
    return tuple(v * k for v in c)


# --- locks: the building block of every hairstyle ------------------------------------------------------

def lock(p0, p1, w, bend=0.0, n=18, root=0.15):
    """A tapered strand bundle from p0 (root, width w) to a pointed tip at p1, curved by
    `bend` (orig px, positive = to the right of the p0->p1 direction). Returns
    (outline polygon, centre line)."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    nrm = np.array((-d[1], d[0])) / max(np.linalg.norm(d), 1e-6)
    ctrl = (p0 + p1) / 2 + nrm * bend
    left, right, mid = [], [], []
    for t in np.linspace(0, 1, n):
        pt = (1 - t) ** 2 * p0 + 2 * (1 - t) * t * ctrl + t ** 2 * p1
        tan = 2 * (1 - t) * (ctrl - p0) + 2 * t * (p1 - ctrl)
        tn = np.array((-tan[1], tan[0])) / max(np.linalg.norm(tan), 1e-6)
        wt = w * (min(1.0, t / root) * 0.35 + 0.65 if t < root else (1 - t) ** 0.85)
        left.append(tuple(pt + tn * wt / 2))
        right.append(tuple(pt - tn * wt / 2))
        mid.append(tuple(pt))
    return left + right[::-1], mid


def locks(pen, specs):
    """Union coverage of several locks + their centre lines."""
    covs, mids = [], []
    for p0, p1, w, bend in specs:
        poly, mid = lock(p0, p1, w, bend)
        covs.append(pen.poly(poly, smooth=False))
        mids.append(mid)
    return union(*covs), mids


def strands(pen, mids, cov, frac=(0.2, 0.75), every=1):
    for i, m in enumerate(mids):
        if i % every:
            continue
        a, b = int(len(m) * frac[0]), int(len(m) * frac[1])
        pen.stroke(m[a:b], STRAND, clip=cov)


def shine(pen, cov, pts, color):
    """Soft highlight arc across the top of a hair mass."""
    light = tuple(min(255, c * 1.25 + 30) for c in color)
    pen.stroke(pts, 16, color=light, alpha=0.55, clip=ndimage.binary_erosion(cov > 0.5, iterations=4) * cov)


# --- back styles (behind the head) ---------------------------------------------------------------------

def back(pen, style, g, color, tie=None):
    L, R, cx, top, fy, chin = g['face_l'], g['face_r'], g['cx'], g['top'], g['face_y'], g['chin']
    side = lambda specs: specs + [(mirror([p0], cx)[0], mirror([p1], cx)[0], w, -b) for p0, p1, w, b in specs]
    if style == 'long':
        mass = pen.poly([(L + 40, top - 10), (R - 40, top - 10), (R + 34, fy), (R + 40, chin + 40),
                         (L - 40, chin + 40), (L - 34, fy)])
        cov, mids = locks(pen, side([((L - 10, top + 10), (L - 62, chin + 110), 116, 30),
                                     ((L + 10, fy - 30), (L - 22, chin + 140), 104, 18),
                                     ((L + 40, fy + 20), (L + 22, chin + 118), 92, 10)]))
        cov = union(cov, mass)
        pen.fill(cov, color)
        strands(pen, mids, cov)
    elif style == 'bob':
        mass = pen.poly([(L + 10, top - 10), (R - 10, top - 10), (R + 40, fy), (R + 30, chin - 10),
                         (L - 30, chin - 10), (L - 40, fy)])
        cov, mids = locks(pen, side([((L - 20, top + 20), (L + 6, chin + 40), 92, 34),
                                     ((L + 10, fy), (L + 44, chin + 30), 76, 22),
                                     ((L - 30, fy - 60), (L - 26, chin + 10), 70, 20)]))
        cov = union(cov, mass)
        pen.fill(cov, color)
        strands(pen, mids, cov)
    elif style == 'pigtails':
        for sgn in (-1, 1):
            tx = L + 8 if sgn < 0 else R - 8
            t = (tx, fy - 40)
            specs = [(t, (tx + sgn * 122, chin + 30), 118, -sgn * 40), (t, (tx + sgn * 76, chin + 74), 100, -sgn * 20),
                     (t, (tx + sgn * 146, fy + 46), 88, -sgn * 46)]
            cov, mids = locks(pen, specs)
            pen.fill(cov, color)
            strands(pen, mids, cov)
            if tie:
                pen.fill(pen.ell(tx + sgn * 10, fy - 38, 24, 24), tie, line=LINE * 0.8, grain=False)
    elif style == 'ponytail':
        t = (R - 30, top + 30)
        specs = [(t, (R + 132, chin + 10), 132, -56), (t, (R + 84, chin + 70), 112, -36),
                 (t, (R + 156, fy + 10), 96, -64), (t, (R + 40, chin + 40), 90, -20)]
        cov, mids = locks(pen, specs)
        pen.fill(cov, color)
        strands(pen, mids, cov)
        if tie:
            pen.fill(pen.ell(t[0] + 14, t[1] + 6, 26, 26), tie, line=LINE * 0.8, grain=False)
    elif style == 'braids':
        for sgn in (-1, 1):
            x0 = L + 5 if sgn < 0 else R - 5
            for i in range(6):
                x = x0 + sgn * (18 + 6 * i)
                y = fy + 10 + 46 * i
                pen.fill(pen.ell(x, y, 30 - 2 * i, 28 - 1.5 * i, 20 * sgn), color)
            if tie:
                pen.fill(pen.ell(x0 + sgn * 54, fy + 300, 17, 15), tie, line=LINE * 0.8)
                poly, _ = lock((x0 + sgn * 56, fy + 312), (x0 + sgn * 58, fy + 372), 46, 0)
                pen.fill(pen.poly(poly, smooth=False), color)
    elif style == 'mane':
        rx, ry = (R - L) / 2 + 66, chin - fy + 92
        specs = []
        for ang in np.linspace(math.radians(-25), math.radians(205), 13):
            tip = (cx + rx * math.cos(ang), fy - 10 + ry * math.sin(ang))
            root = (cx + rx * 0.62 * math.cos(ang), fy - 10 + ry * 0.62 * math.sin(ang))
            if tip[1] < top - 30:
                continue
            specs.append((root, tip, 112, 18))
        cov, mids = locks(pen, specs)
        cov = union(cov, pen.ell(cx, fy - 10, rx * 0.68, ry * 0.68))
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.45, 0.85))


# --- front styles (the hatless chief head; kept above its brows at y ~485) ------------------------------

CROWN = (623, 258, 182, 92)      # ellipse over the skull, between / over the inner ears


def front(pen, style, g, color):
    cx = g['cx']
    crown = pen.ell(*CROWN)
    if style == 'bangs':
        specs = [((x, 240), (x + (x - cx) * 0.06, 446 if i % 2 else 430), 78, 0)
                 for i, x in enumerate((470, 521, 572, 623, 674, 725, 776))]
        specs += [((462, 270), (424, 470), 66, -12), ((784, 270), (822, 470), 66, 12)]
        cov, mids = locks(pen, specs)
        cov = union(cov, crown)
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.35, 0.85), every=2)
        shine(pen, cov, [(520, 250), (623, 222), (726, 250)], color)
    elif style == 'side_swoop':
        specs = [((x, 225), (x - 120, 418 + (x - 500) * 0.12), 96, 46) for x in (520, 580, 640, 700, 760)]
        specs += [((790, 250), (818, 462), 70, 14), ((470, 270), (420, 448), 72, -10)]
        cov, mids = locks(pen, specs)
        cov = union(cov, crown)
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.3, 0.85))
        shine(pen, cov, [(560, 225), (660, 210), (740, 240)], color)
    elif style == 'puff':
        blobs = [(470, 300, 54), (500, 236, 62), (560, 196, 66), (623, 180, 70), (686, 196, 66), (746, 236, 62),
                 (776, 300, 54), (580, 262, 60), (666, 262, 60), (450, 360, 42), (796, 360, 42), (623, 290, 56)]
        cov = union(*[pen.ell(x, y, r, r * 0.95) for x, y, r in blobs])
        pen.fill(cov, color)
        for x, y, r in blobs[:9]:
            pen.stroke([(x - r * 0.4, y + r * 0.15), (x - r * 0.1, y - r * 0.3), (x + r * 0.3, y - r * 0.1),
                        (x + r * 0.1, y + r * 0.2)], STRAND * 0.9, clip=cov)
    elif style in ('bun', 'twin_buns'):
        # hair combed up from the hairline into the bun(s): locks run hairline -> bun
        buns = [(cx, 140, 66)] if style == 'bun' else [(492, 176, 56), (754, 176, 56)]
        specs = []
        for x in (452, 500, 548, 596, 650, 698, 746, 794):
            bx, by, _ = min(buns, key=lambda b: abs(b[0] - x))
            hy = 330 if 470 < x < 776 else 400
            specs.append(((x, hy), (bx + (x - bx) * 0.15, by + 30), 84, (x - bx) * 0.12))
        cov, mids = locks(pen, specs)
        cov = union(cov, pen.ell(cx, 262, 176, 84))
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.1, 0.65))
        shine(pen, cov, [(540, 250), (623, 222), (706, 250)], color)
        for x, y, r in buns:
            b = pen.ell(x, y, r, r * 0.92)
            pen.fill(b, color)
            pen.stroke([(x - r * 0.6, y + r * 0.1), (x - r * 0.2, y - r * 0.45), (x + r * 0.45, y - r * 0.3),
                        (x + r * 0.35, y + r * 0.3), (x - r * 0.15, y + r * 0.35)], STRAND, clip=b)
    elif style == 'spiky':
        specs = [((cx + 70 * math.cos(math.radians(a)), 300), (cx + 205 * math.cos(math.radians(a)),
                                                               300 + 215 * math.sin(math.radians(a))), 96, 0)
                 for a in (-165, -140, -116, -90, -64, -40, -15)]
        cov, mids = locks(pen, specs)
        cov = union(cov, pen.ell(cx, 272, 170, 78))
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.4, 0.8))
    elif style == 'mohawk':
        specs = [((cx + dx, 300), (cx + dx * 2.2, 70 + abs(dx) * 1.2), 70, dx * 0.4) for dx in (-40, -20, 0, 20, 40)]
        cov, mids = locks(pen, specs)
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.3, 0.8))
    elif style == 'pompadour':
        specs = [((x, 340), (x + 60, 110 + abs(x - cx) * 0.3), 120, -70) for x in (480, 545, 610, 675, 740)]
        specs += [((462, 290), (430, 440), 64, -10), ((784, 290), (816, 440), 64, 10)]
        cov, mids = locks(pen, specs)
        cov = union(cov, crown)
        pen.fill(cov, color)
        strands(pen, mids, cov, (0.25, 0.8))
        shine(pen, cov, [(520, 210), (620, 160), (720, 190)], color)
    elif style == 'cowlick':
        poly, _ = lock((cx - 6, 230), (cx + 40, 100), 40, -60)
        pen.fill(pen.poly(poly, smooth=False), color, line=LINE * 0.85)


# --- role items -------------------------------------------------------------------------------------------

def item(pen, name, g, colors):
    if name == 'pencil':     # behind the right ear
        ex, ey = g['ear_r']
        cov = pen.poly([(ex - 70, ey + 70), (ex + 55, ey - 40), (ex + 72, ey - 22), (ex - 52, ey + 88)], smooth=False)
        pen.fill(cov, (246, 196, 72), line=LINE * 0.7, grain=False)
        pen.fill(pen.poly([(ex - 70, ey + 70), (ex - 52, ey + 88), (ex - 90, ey + 100)], smooth=False),
                 (240, 214, 170), line=LINE * 0.7, grain=False)
        pen.fill(pen.ell(ex + 64, ey - 31, 15, 14), (240, 140, 150), line=LINE * 0.7, grain=False)
    elif name in ('bee', 'butterfly', 'star', 'flower', 'bow'):
        x, y = g['pin'] if name != 'flower' else g['ear_r']
        if name == 'bee':
            for sgn in (-1, 1):
                pen.fill(pen.ell(x + sgn * 16, y - 26, 18, 12, -30 * sgn), (235, 245, 255), line=LINE * 0.6,
                         grain=False)
            body = pen.ell(x, y, 30, 22)
            pen.fill(body, (250, 206, 70), line=LINE * 0.7, grain=False)
            for dx in (-6, 10):
                pen.stroke([(x + dx, y - 20), (x + dx + 2, y + 20)], 7, clip=body)
        elif name == 'butterfly':
            wing = colors.get('item', (150, 190, 240))
            for sgn in (-1, 1):
                pen.fill(pen.ell(x + sgn * 30, y - 16, 30, 24, 25 * sgn), wing, line=LINE * 0.6)
                pen.fill(pen.ell(x + sgn * 24, y + 20, 20, 16, -20 * sgn), wing, line=LINE * 0.6)
            pen.fill(pen.ell(x, y, 8, 30), (90, 60, 50), line=LINE * 0.5, grain=False)
        elif name == 'star':
            pts = []
            for k in range(10):
                r = 38 if k % 2 == 0 else 16
                ang = math.radians(-90 + k * 36)
                pts.append((x + r * math.cos(ang), y + r * math.sin(ang)))
            pen.fill(pen.poly(pts, smooth=False), (252, 214, 90), line=LINE * 0.7, grain=False)
        elif name == 'flower':
            petal = colors.get('item', (250, 168, 190))
            for k in range(5):
                ang = math.radians(k * 72 - 90)
                pen.fill(pen.ell(x + 24 * math.cos(ang), y + 24 * math.sin(ang), 20, 15, math.degrees(ang)), petal,
                         line=LINE * 0.6)
            pen.fill(pen.ell(x, y, 13, 13), (252, 216, 92), line=LINE * 0.6, grain=False)
        elif name == 'bow':
            c = colors.get('item', (238, 110, 130))
            for sgn in (-1, 1):
                pen.fill(pen.poly([(x, y - 4), (x + sgn * 44, y - 26), (x + sgn * 50, y), (x + sgn * 44, y + 26),
                                   (x, y + 4)]), c, line=LINE * 0.7)
            pen.fill(pen.ell(x, y, 14, 16), c, line=LINE * 0.7)
    elif name == 'beret':
        c = colors.get('item', (196, 70, 70))
        pen.fill(pen.ell(g['cx'] + 30, 230, 205, 78, -8), c)
        pen.fill(pen.ell(g['cx'] + 60, 160, 16, 20), c, line=LINE * 0.7)
    elif name == 'chef_hat':
        white = (250, 250, 246)
        puff = union(pen.ell(g['cx'] - 85, 150, 80, 72), pen.ell(g['cx'], 110, 92, 82), pen.ell(g['cx'] + 85, 150, 80, 72),
                     pen.poly([(g['cx'] - 150, 160), (g['cx'] + 150, 160), (g['cx'] + 140, 250), (g['cx'] - 140, 250)]))
        pen.fill(puff, white, light=1.0, dark=0.9)
        pen.fill(pen.poly([(g['cx'] - 160, 228), (g['cx'] + 160, 228), (g['cx'] + 165, 285), (g['cx'] - 165, 285)]),
                 white, light=1.0, dark=0.88)
    elif name == 'headband':
        c = colors.get('item', (220, 80, 70))
        band = pen.poly([(462, 300), (520, 262), (623, 246), (726, 262), (786, 300), (780, 336), (623, 300),
                         (468, 336)])
        pen.fill(band, c, line=LINE * 0.8)
    elif name == 'freckles':
        for cx_, sgn in ((g['face_l'] + 130, 1), (g['face_r'] - 130, -1)):
            for dx, dy in ((-14, 0), (0, 12), (14, -2)):
                pen.over(pen.ell(cx_ + dx, g['face_y'] + 75 + dy, 4.5, 4.2), (176, 104, 82), 0.9)
    elif name == 'soot':
        for cx_, cy_ in ((g['face_l'] + 110, g['face_y'] + 95), (g['face_r'] - 150, g['face_y'] + 110)):
            pen.over(ndimage.gaussian_filter(pen.ell(cx_, cy_, 26, 9, -12), 2.5) * (pen.a[..., 3] > 200),
                     (100, 84, 80), 0.4)
    elif name == 'bandage':
        x, y = g['face_r'] - 140, g['face_y'] + 60
        pen.fill(pen.poly([(x - 34, y - 20), (x + 26, y - 40), (x + 34, y - 16), (x - 26, y + 4)]), (246, 226, 196),
                 line=LINE * 0.6, grain=False)


def body_item(pen, name, b, colors):
    cx, neck, chest, waist, half = b['cx'], b['neck'], b['chest'], b['waist'], b['half']
    if name == 'camera':
        pen.stroke([(cx - 95, neck + 20), (cx - 40, chest + 10)], 9, color=(70, 60, 60))
        pen.stroke([(cx + 95, neck + 20), (cx + 40, chest + 10)], 9, color=(70, 60, 60))
        pen.fill(pen.poly([(cx - 62, chest - 10), (cx + 62, chest - 10), (cx + 62, chest + 62), (cx - 62, chest + 62)]),
                 (80, 80, 88), line=LINE * 0.7, grain=False)
        pen.fill(pen.ell(cx, chest + 26, 27, 27), (60, 70, 90), line=LINE * 0.6, grain=False)
        pen.fill(pen.ell(cx, chest + 26, 13, 13), (140, 170, 210), line=0, grain=False)
        pen.over(pen.ell(cx - 5, chest + 20, 5, 5), (255, 255, 255))
        pen.fill(pen.poly([(cx + 30, chest - 24), (cx + 54, chest - 24), (cx + 54, chest - 10), (cx + 30, chest - 10)],
                          smooth=False), (200, 70, 60), line=LINE * 0.5, grain=False)
    elif name == 'apron':
        white = colors.get('apron', (250, 248, 240))
        apron = [(cx - 70, chest - 40), (cx + 70, chest - 40), (cx + 78, chest + 40), (cx + half - 10, chest + 60),
                 (cx + half - 4, waist), (cx - half + 4, waist), (cx - half + 10, chest + 60), (cx - 78, chest + 40)]
        pen.fill(pen.poly(apron), white, light=1.0, dark=0.9)
        pen.fill(pen.poly([(cx - 40, chest + 70), (cx + 40, chest + 70), (cx + 40, chest + 120), (cx - 40, chest + 120)]),
                 white, light=0.98, dark=0.92, line=LINE * 0.6)
    elif name == 'bowtie':
        c = colors.get('item', (196, 60, 70))
        y = neck + 38
        for sgn in (-1, 1):
            pen.fill(pen.poly([(cx, y - 4), (cx + sgn * 46, y - 24), (cx + sgn * 50, y), (cx + sgn * 46, y + 24),
                               (cx, y + 4)]), c, line=LINE * 0.7)
        pen.fill(pen.ell(cx, y, 13, 15), c, line=LINE * 0.7)
