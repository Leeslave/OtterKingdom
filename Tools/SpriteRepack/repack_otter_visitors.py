# Re-packs the AI-generated plaza visitor sheets (Snack / Sleepy / Painter)
# into uniform grids, same idea as repack_farmer_otter.py: every frame gets
# the same cell, feet on a shared baseline and the body centre on the cell's
# centre column, so Unity can slice on the exact grid with a centre pivot and
# flipX mirrors the otter in place.
#
# Source sheets are 1024x1536, 4 cols x 6 rows, one clean alpha blob per
# frame, NOT on an exact grid (frames drift a few px per row/column):
#   row 0 Idle, 1 Walk_Down, 2 Walk_Up, 3 Walk (side, facing right),
#   4 action A, 5 action B   (Snack Eat/Happy, Sleepy Yawn/Sleep,
#                             Painter Paint/Wave)
# Output keeps that layout: 4 cols x 6 rows of cellW x cellH per character.
# All three characters share one cell size so the Unity setup has a single
# grid and one feet offset.
#
# No hat mask like the farmer script: Snack has no hat and Sleepy's cap tail
# swings to either side. The anchor is the median x of the torso band instead
# (below the head, above the tail), which stays put across a clip.
#
# Usage: python repack_otter_visitors.py <source image dir> <output dir>
#   <source image dir> must hold Snack.png, Sleepy.png and Painter.png.
from PIL import Image
import numpy as np
from scipy import ndimage
import json, os, sys

SRC = sys.argv[1] if len(sys.argv) > 1 else 'images/'
OUT = sys.argv[2] if len(sys.argv) > 2 else 'out/'
NAMES = ['Snack', 'Sleepy', 'Painter']
ROWS, COLS = 6, 4
PAD = 6
# Torso band as a fraction of the frame's top -> feet height.
TORSO_BAND = (0.45, 0.65)


def extract(name):
    im = np.asarray(Image.open(os.path.join(SRC, name + '.png')).convert('RGBA'))
    alpha = im[..., 3]
    lab, n = ndimage.label(alpha > 8, structure=np.ones((3, 3)))
    sizes = np.array(ndimage.sum(alpha > 8, lab, range(1, n + 1)))
    main = list(np.argsort(-sizes)[:ROWS * COLS])
    if sizes[main[-1]] < 0.5 * np.median(sizes[main]):
        raise SystemExit(f'{name}: expected {ROWS * COLS} frames, the smallest blob is too small')
    cents = {m: ndimage.center_of_mass(alpha > 8, lab, m + 1) for m in main}
    ms = sorted(main, key=lambda m: cents[m][0])
    grid = [sorted(ms[r * COLS:(r + 1) * COLS], key=lambda m: cents[m][1]) for r in range(ROWS)]

    frames = []
    for row in grid:
        out = []
        for m in row:
            # Grow by 2px to keep the soft edge, but never into a neighbour.
            own = lab == m + 1
            mask = ndimage.binary_dilation(own, iterations=2) & (alpha > 0) & ((lab == 0) | own)
            ys, xs = np.nonzero(mask)
            y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            crop = im[y0:y1, x0:x1].copy()
            crop[..., 3] = np.where(mask[y0:y1, x0:x1], crop[..., 3], 0)
            body = own[y0:y1, x0:x1]
            bys = np.nonzero(body.any(1))[0]
            top, feet = bys.min(), bys.max() + 1
            h = feet - top
            band = body[top + int(h * TORSO_BAND[0]):top + int(h * TORSO_BAND[1])]
            ax = float(np.median(np.nonzero(band)[1]))
            out.append(dict(img=crop, ax=ax, ay=feet, h=h))
        frames.append(out)
    return frames


def main():
    os.makedirs(OUT, exist_ok=True)
    all_frames = {n: extract(n) for n in NAMES}

    L = R = U = D = 0
    for fr in all_frames.values():
        for row in fr:
            for f in row:
                w, h = f['img'].shape[1], f['img'].shape[0]
                L = max(L, f['ax']); R = max(R, w - f['ax'])
                U = max(U, f['ay']); D = max(D, h - f['ay'])
    half = int(np.ceil(max(L, R))) + PAD
    cw = half * 2
    base = int(np.ceil(U)) + PAD
    ch = base + int(np.ceil(D)) + PAD
    cw += (-cw) % 4
    ch += (-ch) % 4
    print(f'cell {cw}x{ch}, baseline from top {base}')

    meta = dict(rows=ROWS, cols=COLS, cellW=cw, cellH=ch, baselineFromTop=base)
    for name, fr in all_frames.items():
        sheet = Image.new('RGBA', (cw * COLS, ch * ROWS), (0, 0, 0, 0))
        for r, row in enumerate(fr):
            for c, f in enumerate(row):
                img = Image.fromarray(f['img'], 'RGBA')
                sheet.alpha_composite(img, (c * cw + cw // 2 - round(f['ax']), r * ch + base - round(f['ay'])))
        sheet.save(os.path.join(OUT, name + '.png'), optimize=True)
        idle_h = np.mean([f['h'] for f in fr[0]])
        meta[name] = dict(idleHeight=round(float(idle_h), 1))
        print(f'{name}: idle height {idle_h:.1f}px')
    json.dump(meta, open(os.path.join(OUT, 'layout.json'), 'w'), indent=1)


main()
