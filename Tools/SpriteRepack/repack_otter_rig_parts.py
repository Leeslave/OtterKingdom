"""Repacks Assets/Art/Otter/Rig/FarmerOtter_RigParts.png for the bone rig (OtterRigSetup).

Source: ArtSource/Otter/FarmerOtter_RigPartsSheet.png has 3 rows (front, right side,
back). Each row is head (with hat), body, arm L, arm R, leg L, leg R, tail.
The left view is the right view mirrored. The mirrored copies are baked into the
texture so every view uses plain, unflipped sprites (SpriteSkin + flipX don't mix well).

The parts are kept at source resolution. Arms and legs are drawn large next to the
body, so they are scaled down to keep the chibi proportions. The side body is drawn
taller than the front one, so it is shortened to keep the chin / hip line the same
in every view (and to leave more leg showing below it).

Prints the RectInt table for OtterRigSetup.PartRects. Re-paste it whenever this runs.
Usage: python repack_otter_rig_parts.py
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'Otter', 'FarmerOtter_RigPartsSheet.png')
DST = os.path.join(ROOT, 'Assets', 'Art', 'Otter', 'Rig', 'FarmerOtter_RigParts.png')

ROW_FRONT, ROW_SIDE, ROW_BACK = 0, 1, 2
HEAD, BODY, ARM_L, ARM_R, LEG_L, LEG_R, TAIL = range(7)
ARM_SCALE = 0.82
LEG_SCALE = 0.7
SIDE_BODY_SCALE = (1, 0.9)

# name, row, column, mirror, scale (uniform, or (x, y))
PARTS = [
    ('Head_Front', ROW_FRONT, HEAD, 0, 1), ('Head_Right', ROW_SIDE, HEAD, 0, 1),
    ('Head_Left', ROW_SIDE, HEAD, 1, 1), ('Head_Back', ROW_BACK, HEAD, 0, 1),
    ('Body_Front', ROW_FRONT, BODY, 0, 1), ('Body_Right', ROW_SIDE, BODY, 0, SIDE_BODY_SCALE),
    ('Body_Left', ROW_SIDE, BODY, 1, SIDE_BODY_SCALE), ('Body_Back', ROW_BACK, BODY, 0, 1),
    ('Arm_Front_L', ROW_FRONT, ARM_L, 0, ARM_SCALE), ('Arm_Front_R', ROW_FRONT, ARM_R, 0, ARM_SCALE),
    ('Arm_Back_L', ROW_BACK, ARM_L, 0, ARM_SCALE), ('Arm_Back_R', ROW_BACK, ARM_R, 0, ARM_SCALE),
    ('Arm_SideNear_R', ROW_SIDE, ARM_L, 0, ARM_SCALE), ('Arm_SideFar_R', ROW_SIDE, ARM_R, 0, ARM_SCALE),
    ('Arm_SideNear_L', ROW_SIDE, ARM_L, 1, ARM_SCALE), ('Arm_SideFar_L', ROW_SIDE, ARM_R, 1, ARM_SCALE),
    ('Leg_Front_L', ROW_FRONT, LEG_L, 0, LEG_SCALE), ('Leg_Front_R', ROW_FRONT, LEG_R, 0, LEG_SCALE),
    ('Leg_Back_L', ROW_BACK, LEG_L, 0, LEG_SCALE), ('Leg_Back_R', ROW_BACK, LEG_R, 0, LEG_SCALE),
    ('Leg_SideNear_R', ROW_SIDE, LEG_L, 0, LEG_SCALE), ('Leg_SideFar_R', ROW_SIDE, LEG_R, 0, LEG_SCALE),
    ('Leg_SideNear_L', ROW_SIDE, LEG_L, 1, LEG_SCALE), ('Leg_SideFar_L', ROW_SIDE, LEG_R, 1, LEG_SCALE),
    # Front: only a small peek behind the body. Back: hangs down to the ground.
    ('Tail_Front', ROW_FRONT, TAIL, 0, 0.6), ('Tail_Right', ROW_SIDE, TAIL, 0, 0.8),
    ('Tail_Left', ROW_SIDE, TAIL, 1, 0.8), ('Tail_Back', ROW_BACK, TAIL, 0, 0.7),
]
PAD = 4
WIDTH = 1024
ROW_HEIGHT = 290  # source rows are ~290px apart


def components(a):
    """Returns the label image and a [row][column] grid of blob boxes."""
    lab, _ = ndimage.label(a[:, :, 3] > 20)
    blobs = []
    for i, s in enumerate(ndimage.find_objects(lab)):
        if (lab[s] == i + 1).sum() > 1500:  # skips stray specks on the sheet
            blobs.append((s[0].start, s[0].stop, s[1].start, s[1].stop, i + 1))
    grid = [[], [], []]
    for b in blobs:
        grid[(b[0] + b[1]) // 2 // ROW_HEIGHT].append(b)
    for row in grid:
        row.sort(key=lambda b: b[2])
        assert len(row) == 7, f'expected 7 parts per row, got {len(row)}'
    return lab, grid


def cut(a, lab, blob):
    y0, y1, x0, x1, l = blob
    sub = a[y0:y1, x0:x1].copy()
    region = lab[y0:y1, x0:x1]
    keep = ndimage.binary_dilation(region == l, iterations=1) & (sub[:, :, 3] > 0) & ((region == 0) | (region == l))
    sub[~keep] = 0
    return Image.fromarray(sub)


def main():
    a = np.array(Image.open(SRC).convert('RGBA'))
    lab, grid = components(a)

    imgs = []
    for name, row, col, mirror, scale in PARTS:
        p = cut(a, lab, grid[row][col])
        if mirror:
            p = p.transpose(Image.FLIP_LEFT_RIGHT)
        sx, sy = scale if isinstance(scale, tuple) else (scale, scale)
        if (sx, sy) != (1, 1):
            p = p.resize((round(p.width * sx), round(p.height * sy)), Image.LANCZOS)
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
