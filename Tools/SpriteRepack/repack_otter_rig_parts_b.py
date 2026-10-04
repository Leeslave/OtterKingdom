"""Repacks Assets/Art/Otter/RigB/FarmerOtter_RigPartsB.png for the arm-swap rig
(OtterRigSetup, config B).

Source: ArtSource/Otter/FarmerOtter_RigPartsSheetB.png, front and back views only.
Rows: [head, body, leg L, leg R, tail], then 6 arm poses, then the same two rows
for the back view. The 6 arm poses are 3 for the screen-right arm and 3 for the
screen-left arm (in that order - the paws are drawn for those sides), each with the
hand pointing left, hanging, pointing right. The walk swaps between them.

The sheet has an opaque dark background with glows, so parts are cut out as the
light regions enclosed by their black outline, grown back over the outline. A
blurred 1px rim keeps the edge anti-aliased.

Arm pivots sit at the shoulder (the far end of the white sleeve), so swapping
poses keeps the shoulder fixed on its bone.

Prints the RectInt table (PartRects) and the arm pivot table (ArmPivots) for
OtterRigSetup's config B. Re-paste both whenever this runs.
Usage: python repack_otter_rig_parts_b.py
"""
import os
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'Otter', 'FarmerOtter_RigPartsSheetB.png')
DST = os.path.join(ROOT, 'Assets', 'Art', 'Otter', 'RigB', 'FarmerOtter_RigPartsB.png')

OUTLINE_LUM = 28   # outline pixels are ~2-5, the background ~40-100
OUTLINE_PX = 7     # how far to grow the enclosed regions back over the outline
ROW_HEIGHT = 256
ARM_SCALE = 0.75
LEG_SCALE = 0.7

# name, row, column, scale
_ARM_POSES = ['R_In', 'R_Mid', 'R_Out', 'L_Out', 'L_Mid', 'L_In']
PARTS = []
for view, row in (('Front', 0), ('Back', 2)):
    PARTS += [
        (f'Head_{view}', row, 0, 1), (f'Body_{view}', row, 1, 1),
        (f'Leg_{view}_L', row, 2, LEG_SCALE), (f'Leg_{view}_R', row, 3, LEG_SCALE),
        (f'Tail_{view}', row, 4, 0.6 if view == 'Front' else 0.7),
    ]
    PARTS += [(f'Arm_{view}_{pose}', row + 1, col, ARM_SCALE) for col, pose in enumerate(_ARM_POSES)]
PAD = 4
WIDTH = 1024


def segment(rgb):
    lum = rgb.mean(2)
    dark = lum < OUTLINE_LUM
    lab, _ = ndimage.label(~dark)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])))
    inner = np.zeros_like(dark)
    for i, s in enumerate(ndimage.find_objects(lab)):
        if i + 1 in edge or (lab[s] == i + 1).sum() < 200:
            continue
        inner[s] |= lab[s] == i + 1
    mask = ndimage.binary_fill_holes(ndimage.binary_dilation(inner, iterations=OUTLINE_PX) & (inner | dark))
    ml, _ = ndimage.label(mask)
    grid = {}
    for i, s in enumerate(ndimage.find_objects(ml)):
        if (ml[s] == i + 1).sum() > 3000:
            row = (s[0].start + s[0].stop) // 2 // ROW_HEIGHT
            grid.setdefault(row, []).append((s[0].start, s[0].stop, s[1].start, s[1].stop, i + 1))
    for row in grid.values():
        row.sort(key=lambda b: b[2])
    assert [len(grid[r]) for r in range(4)] == [5, 6, 5, 6], {r: len(v) for r, v in grid.items()}
    return ml, grid


def cut(rgb, ml, blob):
    y0, y1, x0, x1, l = blob
    y0, x0 = max(0, y0 - 3), max(0, x0 - 3)
    y1, x1 = y1 + 3, x1 + 3
    m = (ml[y0:y1, x0:x1] == l).astype(float)
    soft = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))) / 255
    alpha = np.maximum(m, np.clip(soft * 1.6 - 0.3, 0, 1) * (1 - m))
    sub = rgb[y0:y1, x0:x1].copy()
    sub[m == 0] = (12, 8, 8)  # rim pixels take the outline colour, not the glow behind it
    p = Image.fromarray(np.dstack([sub, alpha * 255]).astype(np.uint8))
    return p.crop(p.getbbox())


def shoulder_pivot(p):
    """Normalized pivot (Unity, y up) at the shoulder end of an arm sprite."""
    a = np.asarray(p).astype(int)
    op = a[:, :, 3] > 128
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    white = op & (r > 200) & (g > 185) & (b > 160)
    brown = op & (r < 170) & (g < 110) & ~white
    wc, bc = np.argwhere(white).mean(0), np.argwhere(brown).mean(0)
    d = (wc - bc) / np.linalg.norm(wc - bc)
    far = np.percentile((np.argwhere(op) - wc) @ d, 99)
    sy, sx = wc + d * far * 0.55
    return sx / p.width, 1 - sy / p.height


def main():
    rgb = np.asarray(Image.open(SRC).convert('RGB')).astype(int)
    ml, grid = segment(rgb)

    imgs = []
    for name, row, col, scale in PARTS:
        p = cut(rgb, ml, grid[row][col])
        if scale != 1:
            p = p.resize((round(p.width * scale), round(p.height * scale)), Image.LANCZOS)
        imgs.append((name, p))

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
    print('// PartRects')
    for name, x, y, p in placed:
        print(f'        {{ "{name}", new RectInt({x}, {height - y - p.height}, {p.width}, {p.height}) }},')
    print('// ArmPivots')
    for name, _, _, p in placed:
        if name.startswith('Arm_'):
            px, py = shoulder_pivot(p)
            print(f'        {{ "{name}", new Vector2({px:.3f}f, {py:.3f}f) }},')


if __name__ == '__main__':
    main()
