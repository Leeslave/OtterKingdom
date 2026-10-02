"""Repacks Assets/Art/Otter/Rig/FarmerOtter_RigParts.png for the bone rig (OtterRigSetup).

Source: ArtSource/Otter/FarmerOtter_PartsSheet_v2.png. Its bottom row has the walk
parts with the hat already merged into each head. The v2 sheet has no tail parts,
so the tails come from the v1 sheet (FarmerOtter_PartsSheet.png).

- Front head and right head touch at their brim tips, so they are cut apart along a
  stepped line through the touching point.
- The v2 bodies have faint translucent "ghost" arms at their sides. Only the solid
  shape plus a 1px anti-aliased rim is kept.
- Scales are fitted against the sheet's own walk frames. Bodies are also slimmed
  horizontally.

Prints the RectInt table for OtterRigSetup.PartRects. Re-paste it whenever this runs.
Usage: python repack_otter_rig_parts.py
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC_V2 = os.path.join(ROOT, 'ArtSource', 'Otter', 'FarmerOtter_PartsSheet_v2.png')
SRC_V1 = os.path.join(ROOT, 'ArtSource', 'Otter', 'FarmerOtter_PartsSheet.png')
DST = os.path.join(ROOT, 'Assets', 'Art', 'Otter', 'Rig', 'FarmerOtter_RigParts.png')

# v2 part order (left to right): 0 head F, 1 head R, 2 head L, 3 head B,
# 4 body F, 5 body R, 6 body L, 7 body B, 8/9 front arms L/R, 10 short side arm,
# 11/12 long side arms, 13/14 spread arms, 15/16 front legs, 17/18 back legs, 19 side leg.
# v1 tails: 31 right, 32 left, 33 back.
# name, sheet, part index, mirror, scale
PARTS = [
    ('Head_Front', 'v2', 0, 0, 1), ('Head_Right', 'v2', 1, 0, 1),
    ('Head_Left', 'v2', 2, 0, 1), ('Head_Back', 'v2', 3, 0, 1),
    ('Body_Front', 'v2', 4, 0, 1), ('Body_Right', 'v2', 5, 0, 1),
    ('Body_Left', 'v2', 6, 0, 1), ('Body_Back', 'v2', 7, 0, 1),
    ('Arm_Front_L', 'v2', 8, 0, 1), ('Arm_Front_R', 'v2', 9, 0, 1),
    ('Arm_SideNear_R', 'v2', 10, 0, 1), ('Arm_SideNear_L', 'v2', 10, 1, 1),
    ('Arm_SideFar_R', 'v2', 11, 0, 1), ('Arm_SideFar_L', 'v2', 12, 0, 1),
    ('Leg_Front_L', 'v2', 15, 0, .8), ('Leg_Front_R', 'v2', 16, 0, .8),
    ('Leg_Back_L', 'v2', 17, 0, .8), ('Leg_Back_R', 'v2', 18, 0, .8),
    ('Leg_Side', 'v2', 19, 0, .8),
    ('Tail_Right', 'v1', 31, 0, .7), ('Tail_Left', 'v1', 32, 0, .7), ('Tail_Back', 'v1', 33, 0, .7),
]
BODY_SLIM = 0.85
PAD = 4
WIDTH = 512


def _blobs(al, keep_rows):
    al = al.copy()
    mask = np.zeros_like(al, dtype=bool)
    mask[keep_rows] = True
    al[~mask] = 0
    al[:, :130] = 0  # row labels
    lab, _ = ndimage.label(al > 20)
    comps = []
    for i, s in enumerate(ndimage.find_objects(lab)):
        if (lab[s] == i + 1).sum() > 300:
            comps.append((s[0].start, s[0].stop, s[1].start, s[1].stop, i + 1))
    return lab, comps


def v2_components(a):
    lab, comps = _blobs(a[:, :, 3], slice(745, None))
    comps.sort(key=lambda r: r[2])
    # comps[0] = front + right head; brim tips touch at (269, 825). Above that point the
    # front brim reaches x=269; below it the right brim starts at x=268.
    y0, y1, x0, x1, l = comps[0]
    new_l = lab.max() + 1
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    lab[(lab == l) & (xx >= np.where(yy < 826, 270, 267))] = new_l
    split = []
    for ll in (l, new_l):
        sl, sn = ndimage.label(lab == ll)
        if sn > 1:  # drop slivers of the other head left by the cut
            sizes = ndimage.sum(sl > 0, sl, range(1, sn + 1))
            lab[(sl > 0) & (sl != 1 + int(np.argmax(sizes)))] = 0
        ys, xs = np.nonzero(lab[y0:y1, x0:x1] == ll)
        split.append((y0 + ys.min(), y0 + ys.max() + 1, x0 + xs.min(), x0 + xs.max() + 1, ll))
    return lab, split + comps[1:]


def v1_components(a):
    al = a[:, :, 3].copy()
    al[766:792, :] = 0   # first label row
    al[858:, :] = 0      # second label row
    lab, comps = _blobs(al, slice(670, None))
    comps = [c for c in comps if (lab[c[0]:c[1], c[2]:c[3]] == c[4]).sum() > 800]
    comps.sort(key=lambda r: (r[0] > 780, r[2]))
    return lab, comps


def cut(a, lab, comp):
    y0, y1, x0, x1, l = comp
    sub = a[y0:y1, x0:x1].copy()
    region = lab[y0:y1, x0:x1]
    keep = ndimage.binary_dilation(region == l, iterations=1) & (sub[:, :, 3] > 0) & ((region == 0) | (region == l))
    sub[~keep] = 0
    # Keep only the solid shape (removes translucent ghost arms) plus a 1px rim.
    solid = sub[:, :, 3] > 200
    sl, sn = ndimage.label(solid)
    if sn:
        sizes = ndimage.sum(solid, sl, range(1, sn + 1))
        core = ndimage.binary_fill_holes(sl == 1 + int(np.argmax(sizes)))
        sub[~ndimage.binary_dilation(core, iterations=1)] = 0
    return Image.fromarray(sub)


def main():
    sheets = {}
    for key, path, finder in (('v2', SRC_V2, v2_components), ('v1', SRC_V1, v1_components)):
        a = np.array(Image.open(path).convert('RGBA'))
        sheets[key] = (a, *finder(a))

    imgs = []
    for name, sheet, idx, mirror, scale in PARTS:
        a, lab, comps = sheets[sheet]
        p = cut(a, lab, comps[idx])
        if mirror:
            p = p.transpose(Image.FLIP_LEFT_RIGHT)
        sx = scale * (BODY_SLIM if name.startswith('Body_') else 1)
        if sx != 1 or scale != 1:
            p = p.resize((round(p.width * sx), round(p.height * scale)), Image.LANCZOS)
        imgs.append((name, p.crop(p.getbbox())))

    x = y = PAD
    row_h = 0
    placed = []
    for name, p in imgs:
        if x + p.width + PAD > WIDTH:
            x, y, row_h = PAD, y + row_h + PAD, 0
        placed.append((name, x, y, p))
        x += p.width + PAD
        row_h = max(row_h, p.height)
    height = (y + row_h + PAD + 3) // 4 * 4

    tex = Image.new('RGBA', (WIDTH, height), (0, 0, 0, 0))
    for _, x, y, p in placed:
        tex.paste(p, (x, y))
    tex.save(DST)
    print(f'{DST} ({WIDTH}x{height})')
    for name, x, y, p in placed:
        print(f'        {{ "{name}", new RectInt({x}, {height - y - p.height}, {p.width}, {p.height}) }},')


if __name__ == '__main__':
    main()
