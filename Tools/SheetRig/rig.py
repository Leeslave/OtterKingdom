"""Bone rig + mesh renderer for the cut otter parts (preview / video only).

Coordinates: 1x sheet-cell pixels (x right, y down) in the specs, SCALE x in the
images. Rotation in degrees, positive = counter-clockwise on screen.
Each part hangs on one bone; the side-view tail is skinned to a 3-bone chain
and drawn as a deformed triangle grid so it bends.
"""
import math
import numpy as np
from PIL import Image, ImageOps, ImageDraw
from scipy import ndimage

import cut_parts as cp

S = cp.SCALE


def mat(a=1, b=0, c=0, d=1, tx=0, ty=0):
    return np.array([[a, b, tx], [c, d, ty], [0, 0, 1]], float)


def T(x, y):
    return mat(tx=x, ty=y)


def R(deg):
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    # y points down, so a CCW-looking rotation is [[c, s], [-s, c]]
    return mat(c, s, -s, c)


def Sc(sx, sy):
    return mat(sx, 0, 0, sy)


class Bone:
    def __init__(self, name, parent, pivot, part=None, layer=0, chain=None):
        self.name, self.parent, self.part, self.layer = name, parent, part, layer
        self.pivot = np.array(pivot, float) * S
        self.chain = chain   # (tip point, n segments) for a bendy tail


def tint(im, k):
    a = np.asarray(im).astype(float)
    a[..., :3] *= k
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def shift(im, dx, dy):
    out = Image.new('RGBA', im.size)
    out.paste(im, (int(round(dx * S)), int(round(dy * S))))
    return out


# Side legs: the source leg's hinge x (centre of its capsule top, see cut_parts SIDE),
# the hip height, the ankle (where the foot hinges under the trouser cuff) and where
# the near / far legs hang. Trouser length = SIDE_ANKLE_Y - SIDE_HIP_Y.
SIDE_LEG_X = 229.5
SIDE_HIP_Y = 186
SIDE_ANKLE_Y = 207
SIDE_HIP_NEAR, SIDE_HIP_FAR = 214, 203


def build_views(parts=None):
    """{view: (bones, images)}; images are full-cell RGBA at S x, one per part name."""
    if parts is None:
        parts = cp.cut_all()
    f, s, b = dict(parts['Front']), dict(parts['Side']), dict(parts['Back'])
    # missing / hidden parts are derived: mirrored legs, a darker copy of the near arm
    f['Leg_R'] = ImageOps.mirror(f['Leg_L'])          # canvas centre = body axis x=208
    b['Leg_L'] = ImageOps.mirror(b['Leg_R'])
    s['Arm_Far'] = shift(tint(s['Arm_Near'], 0.8), 7, -2)
    # side legs: one trouser tube + foot drawn straight under the hip; the far leg is a
    # darker copy. Both hang from (almost) the same hip point so they cross while walking.
    leg, foot = s['Leg_Near'], s['Foot_Near']
    for side, x, k in (('Near', SIDE_HIP_NEAR, 1.0), ('Far', SIDE_HIP_FAR, 0.72)):
        s[f'Leg_{side}'] = shift(tint(leg, k), x - SIDE_LEG_X, 0)
        s[f'Foot_{side}'] = shift(tint(foot, k), x - SIDE_LEG_X, 0)
    b['Tail'] = shift(b['Tail'], 18, 0)               # hang it from the middle of the back

    front = [
        Bone('Root', None, (208, 226)),
        Bone('Leg_L', 'Root', (190, 194), 'Leg_L', 0),
        Bone('Leg_R', 'Root', (226, 194), 'Leg_R', 0),
        Bone('Body', 'Root', (208, 196), 'Body', 10),
        Bone('Arm_L', 'Body', (172, 142), 'Arm_L', 20),
        Bone('Arm_R', 'Body', (245, 142), 'Arm_R', 20),
        Bone('Head', 'Body', (208, 137), 'Head', 30),
    ]
    side = [
        Bone('Root', None, (208, 226)),
        # Hip carries the body and both legs, so lowering it keeps the planted foot on the ground
        Bone('Hip', 'Root', (209, SIDE_HIP_Y)),
        Bone('Leg_Far', 'Hip', (SIDE_HIP_FAR, SIDE_HIP_Y), 'Leg_Far', 2),
        Bone('Foot_Far', 'Leg_Far', (SIDE_HIP_FAR, SIDE_ANKLE_Y), 'Foot_Far', 1),
        Bone('Leg_Near', 'Hip', (SIDE_HIP_NEAR, SIDE_HIP_Y), 'Leg_Near', 4),
        Bone('Foot_Near', 'Leg_Near', (SIDE_HIP_NEAR, SIDE_ANKLE_Y), 'Foot_Near', 3),
        Bone('Body', 'Hip', (208, 198), 'Body', 10),
        Bone('Arm_Far', 'Body', (209, 147), 'Arm_Far', 0),
        Bone('Tail', 'Body', (172, 186), 'Tail', 6, chain=((114, 170), 3)),
        Bone('Arm_Near', 'Body', (202, 149), 'Arm_Near', 20),
        Bone('Head', 'Body', (212, 139), 'Head', 30),
    ]
    back = [
        Bone('Root', None, (208, 226)),
        Bone('Leg_L', 'Root', (188, 197), 'Leg_L', 0),
        Bone('Leg_R', 'Root', (228, 197), 'Leg_R', 0),
        Bone('Body', 'Root', (208, 196), 'Body', 10),
        Bone('Tail', 'Body', (208, 178), 'Tail', 15),
        Bone('Arm_L', 'Body', (176, 142), 'Arm_L', 20),
        Bone('Arm_R', 'Body', (240, 142), 'Arm_R', 20),
        Bone('Head', 'Body', (208, 137), 'Head', 30),
    ]
    return {'Front': (front, f), 'Side': (side, s), 'Back': (back, b)}


# --- sprites -------------------------------------------------------------------------

class Sprite:
    """Trimmed premultiplied part image + its mesh (in full-cell S x coordinates)."""

    def __init__(self, im, bone):
        bb = im.getbbox()
        pad = 3
        self.x0, self.y0 = bb[0] - pad, bb[1] - pad
        crop = np.asarray(im.crop((self.x0, self.y0, bb[2] + pad, bb[3] + pad))).astype(np.float32)
        crop[..., :3] *= crop[..., 3:4] / 255
        self.img = crop
        h, w = crop.shape[:2]
        if bone.chain:
            tip = np.array(bone.chain[0], float) * S
            n = bone.chain[1]
            axis = tip - bone.pivot
            self.seg = axis / n
            nx, ny = 10, 4
        else:
            n, nx, ny = 1, 1, 1
        xs = np.linspace(self.x0, self.x0 + w, nx + 1)
        ys = np.linspace(self.y0, self.y0 + h, ny + 1)
        gx, gy = np.meshgrid(xs, ys)
        self.verts = np.stack([gx.ravel(), gy.ravel()], 1)
        tris = []
        for j in range(ny):
            for i in range(nx):
                a = j * (nx + 1) + i
                tris += [(a, a + 1, a + nx + 2), (a, a + nx + 2, a + nx + 1)]
        self.tris = np.array(tris)
        # weights along the chain: each bone owns its segment's middle, cross-fade at joints
        self.weights = np.zeros((len(self.verts), n))
        if n == 1:
            self.weights[:, 0] = 1
        else:
            d = self.seg / np.linalg.norm(self.seg)
            t = ((self.verts - bone.pivot) @ d) / np.linalg.norm(self.seg)
            f = np.clip(t - 0.5, 0, n - 1)
            a = np.minimum(np.floor(f).astype(int), n - 2)
            w = f - a
            self.weights[np.arange(len(f)), a] = 1 - w
            self.weights[np.arange(len(f)), a + 1] = w


def draw_mesh(canvas, spr, mats, ox, oy, k):
    """Draw sprite deformed by per-bone matrices onto premultiplied canvas.
    Canvas pixel = world * k + (ox, oy)."""
    v = np.hstack([spr.verts, np.ones((len(spr.verts), 1))])
    dst = np.zeros((len(v), 2))
    for i, m in enumerate(mats):
        dst += spr.weights[:, i:i + 1] * (v @ m.T)[:, :2]
    dst = dst * k + [ox, oy]
    H, W = canvas.shape[:2]
    x0, y0 = np.floor(dst.min(0)).astype(int) - 1
    x1, y1 = np.ceil(dst.max(0)).astype(int) + 2
    x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W), min(y1, H)
    if x1 <= x0 or y1 <= y0:
        return
    ids = Image.new('I', (x1 - x0, y1 - y0), 0)
    dr = ImageDraw.Draw(ids)
    for t, tri in enumerate(spr.tris):
        dr.polygon([tuple(dst[i] - [x0, y0]) for i in tri], fill=t + 1)
    ids = np.asarray(ids)
    py, px = np.nonzero(ids)
    if len(py) == 0:
        return
    tid = ids[py, px] - 1
    # affine dst -> src per triangle
    A = np.zeros((len(spr.tris), 2, 3))
    for t, tri in enumerate(spr.tris):
        Dm = np.hstack([dst[tri], np.ones((3, 1))])
        Sm = spr.verts[tri] - [spr.x0, spr.y0]
        try:
            A[t] = np.linalg.solve(Dm, Sm).T
        except np.linalg.LinAlgError:
            pass
    q = np.stack([px + x0 + 0.5, py + y0 + 0.5, np.ones_like(px)], 1)
    src = np.einsum('nij,nj->ni', A[tid], q) - 0.5
    samp = np.stack([ndimage.map_coordinates(spr.img[..., c], [src[:, 1], src[:, 0]], order=1, mode='constant')
                     for c in range(4)], 1)
    ys, xs = py + y0, px + x0
    a = samp[:, 3:4] / 255
    canvas[ys, xs] = samp + canvas[ys, xs] * (1 - a)


class Rig:
    def __init__(self, bones, images, recolor=None, alts=None):
        """alts = {bone: {key: image}}: sprites a pose can swap in with pose[bone]['alt'] = key
        (e.g. a blinking head)."""
        self.bones = {b.name: b for b in bones}
        self.order = [b.name for b in bones]
        self.sprites = {}
        self.alts = {}
        for b in bones:
            if b.part:
                im = images[b.part]
                if recolor:
                    im = recolor(b.part, im)
                self.sprites[b.name] = Sprite(im, b)
        for bone, ims in (alts or {}).items():
            self.alts[bone] = {k: Sprite(im, self.bones[bone]) for k, im in ims.items()}

    def world(self, pose):
        """pose: bone -> dict(rot, dx, dy, sx, sy) in 1x px / degrees. Returns bone -> [matrices]."""
        out = {}
        for name in self.order:
            b = self.bones[name]
            p = pose.get(name, {})
            px, py = b.pivot
            local = T(px + p.get('dx', 0) * S, py + p.get('dy', 0) * S) @ R(p.get('rot', 0)) \
                @ Sc(p.get('sx', 1), p.get('sy', 1)) @ T(-px, -py)
            m = (out[b.parent][0] if b.parent else np.eye(3)) @ local
            mats = [m]
            if b.chain:
                spr = self.sprites[name]
                n = b.chain[1]
                seg = spr.seg
                acc = m
                joint = b.pivot.copy()
                for i in range(1, n):
                    joint = joint + seg
                    r = pose.get(f'{name}_{i + 1}', {}).get('rot', 0)
                    acc = acc @ T(*joint) @ R(r) @ T(*-joint)
                    mats.append(acc)
            out[name] = mats
        return out

    def render(self, pose, size, origin, k=1.0, flip=False):
        """Render onto a transparent premultiplied float canvas. origin = canvas px of the Root pivot."""
        W, H = size
        canvas = np.zeros((H, W, 4), np.float32)
        mats = self.world(pose)
        root = self.bones['Root'].pivot
        ox, oy = origin[0] - root[0] * k, origin[1] - root[1] * k
        # a pose can move a part in front of / behind others: pose[bone]['layer']
        for name in sorted(self.sprites, key=lambda n: pose.get(n, {}).get('layer', self.bones[n].layer)):
            spr = self.alts.get(name, {}).get(pose.get(name, {}).get('alt'), self.sprites[name])
            draw_mesh(canvas, spr, mats[name], ox, oy, k)
        if flip:
            canvas = canvas[:, ::-1]
            canvas = np.ascontiguousarray(canvas)
            # keep the root where it was asked for
            shift_px = int(round(2 * origin[0] - W + 1))
            canvas = np.roll(canvas, shift_px, axis=1)
        return canvas

    def joints(self, pose):
        mats = self.world(pose)
        pts = {}
        for name in self.order:
            b = self.bones[name]
            pts[name] = (mats[name][0] @ [*b.pivot, 1])[:2]
        return pts


def to_pil(prem):
    a = prem[..., 3:4]
    rgb = np.where(a > 0, prem[..., :3] * 255 / np.maximum(a, 1e-3), 0)
    return Image.fromarray(np.dstack([rgb.clip(0, 255), a.clip(0, 255)]).astype(np.uint8))
