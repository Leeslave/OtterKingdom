# Re-packs the AI-generated MinerOtter sheets into uniform grids, same idea
# as repack_fishing_otter.py: every frame gets the same character scale,
# feet on a shared baseline and the helmet centre on the cell's centre column.
#
# Differences from the fishing script:
# - The helmet is yellow, so the hat mask uses different colours.
# - Each sheet gets its OWN cell size (layout.json lists cellW/cellH/
#   baselineFromTop per sheet): the pickaxe swing is far wider than the walk,
#   and the mining clip is only shown inside the emote bubble anyway.
# - Sheets: walk (8 x 4, right / left / front / back) and mine (4 x 2,
#   re-packed into one row of 8 in reading order).
#
# Usage: python repack_miner_otter.py <source image dir> <output dir>
#   <source image dir> must hold walk.png and mine.png.
from PIL import Image
import numpy as np
from scipy import ndimage
import json, os, sys

SRC = sys.argv[1] if len(sys.argv) > 1 else 'images/'
OUT = sys.argv[2] if len(sys.argv) > 2 else 'out/'
OUT_COLS = 8

# source file, output name, source rows, source cols, neutral frames
# (row, col) used to measure the scale — standing, not swinging.
SHEETS = [
    ('walk', 'MinerOtter_Walk', 4, 8, [(0, c) for c in range(8)]),
    ('mine', 'MinerOtter_Mine', 2, 4, [(0, 0), (1, 3)]),
]
# Scale comes from hat top -> feet height only. Unlike the farmer script the
# hat width is NOT mixed in: the side-view walk frames show the brim
# foreshortened (~25% narrower relative to height than the fishing sheets),
# which made the walking otter ~9% bigger than the fishing one.
TARGET_H = 220.0     # hat top -> feet of a neutral pose, in output px
PAD = 6
SPLIT_RATIO = 1.6    # a blob this many times the median frame size is 2+ frames


def label(mask):
    return ndimage.label(mask, structure=np.ones((3, 3)))


def split_merged(a2, expected):
    # Cuts the emptiest column through any blob that is clearly two frames
    # glued together, until no such blob is left.
    for _ in range(expected):
        lab, n = label(a2)
        sizes = np.array(ndimage.sum(a2, lab, range(1, n + 1)))
        top = np.sort(sizes)[::-1][:expected]
        median = np.median(top)
        big = [i for i in range(n) if sizes[i] > SPLIT_RATIO * median]
        if not big:
            return
        objs = ndimage.find_objects(lab)
        for i in big:
            sl = objs[i]
            sub = lab[sl] == i + 1
            colsum = sub.sum(0)
            w = sub.shape[1]
            lo, hi = int(w * 0.3), int(w * 0.7)
            x = sl[1].start + lo + int(np.argmin(colsum[lo:hi]))
            a2[sl[0], x] = False


def hat_mask(crop, body):
    # Yellow helmet (~250,190,80); the pale lamp has too much blue to match.
    r, g, b = (crop[..., i].astype(int) for i in range(3))
    return (r > 200) & (g > 140) & (g < 225) & (b < 130) & (r - b > 100) & body


def extract(key, R, C):
    im = np.asarray(Image.open(os.path.join(SRC, key + '.png')).convert('RGBA')).astype(np.uint8)
    alpha = im[..., 3]
    a = alpha > 8
    H = a.shape[0]
    rowsum = a.sum(1)
    a2 = a.copy()
    for r in range(1, R):
        y = int(H * r / R)
        lo, hi = y - int(H / R * 0.25), y + int(H / R * 0.25)
        a2[lo + int(np.argmin(rowsum[lo:hi])), :] = False
    expected = R * C
    split_merged(a2, expected)

    lab, n = label(a2)
    sizes = np.array(ndimage.sum(a2, lab, range(1, n + 1)))
    objs = ndimage.find_objects(lab)
    order = list(np.argsort(-sizes))
    main = order[:expected]
    cents = {m: ndimage.center_of_mass(a2, lab, m + 1) for m in main}
    ms = sorted(main, key=lambda m: cents[m][0])
    grid = [sorted(ms[r * C:(r + 1) * C], key=lambda m: cents[m][1]) for r in range(R)]

    # Motion lines, "!" marks, water drops, bobbers on a thin line: attach
    # each leftover piece to the nearest frame.
    owned = {m: [m] for m in main}
    for i in order[expected:]:
        if sizes[i] < 40:
            continue
        cy, cx = ndimage.center_of_mass(a2, lab, i + 1)

        def d(m):
            sl = objs[m]
            dy = max(sl[0].start - cy, 0, cy - sl[0].stop)
            dx = max(sl[1].start - cx, 0, cx - sl[1].stop)
            return dx * dx + dy * dy
        owned[min(main, key=d)].append(i)

    frames = []
    for r in range(R):
        row = []
        for m in grid[r]:
            mask = np.isin(lab, [i + 1 for i in owned[m]])
            mask = ndimage.binary_dilation(mask, iterations=2) & (alpha > 0)
            # Dilation must not reach back into a neighbour that was split off.
            other = (lab > 0) & ~np.isin(lab, [i + 1 for i in owned[m]])
            mask &= ~other
            ys, xs = np.nonzero(mask)
            y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
            crop = im[y0:y1, x0:x1].copy()
            crop[..., 3] = np.where(mask[y0:y1, x0:x1], crop[..., 3], 0)
            body = lab[y0:y1, x0:x1] == m + 1
            bys = np.nonzero(body.any(1))[0]
            feet = bys.max() + 1
            hat = hat_mask(crop, body)
            top = bys.min() + int((feet - bys.min()) * 0.5)
            hat[top:] = False
            hys, hxs = np.nonzero(hat)
            lo, hi = np.percentile(hxs, [1, 99])
            hat_top = np.percentile(hys, 1)
            row.append(dict(img=crop, feet=feet, hatx=hxs.mean(),
                            hatw=hi - lo, h=feet - hat_top))
        frames.append(row)
    return frames


def main():
    os.makedirs(OUT, exist_ok=True)
    meta = dict(targetH=TARGET_H)
    for key, name, R, C, ref in SHEETS:
        fr = extract(key, R, C)
        hs = np.mean([fr[r][c]['h'] for r, c in ref])
        scale = TARGET_H / hs
        print(f'{name}: neutral h={hs:.1f} scale={scale:.3f}')
        flat = [f for row in fr for f in row]
        rows = fr if C == OUT_COLS else [flat[i:i + OUT_COLS] for i in range(0, len(flat), OUT_COLS)]

        L = Rt = U = D = 0
        out_rows = []
        for row in rows:
            out = []
            for f in row:
                h, w = f['img'].shape[:2]
                nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
                img = Image.fromarray(f['img'], 'RGBA').resize((nw, nh), Image.LANCZOS)
                ax, ay = f['hatx'] * nw / w, f['feet'] * nh / h
                L = max(L, ax); Rt = max(Rt, nw - ax); U = max(U, ay); D = max(D, nh - ay)
                out.append((img, ax, ay))
            out_rows.append(out)

        half = int(np.ceil(max(L, Rt))) + PAD
        cw = half * 2
        base = int(np.ceil(U)) + PAD
        chh = base + int(np.ceil(D)) + PAD
        cw += (-cw) % 4
        chh += (-chh) % 4
        print(f'  cell {cw}x{chh}, baseline from top {base}')

        sheet = Image.new('RGBA', (cw * OUT_COLS, chh * len(out_rows)), (0, 0, 0, 0))
        for r, row in enumerate(out_rows):
            for c, (img, ax, ay) in enumerate(row):
                sheet.alpha_composite(img, (c * cw + cw // 2 - round(ax), r * chh + base - round(ay)))
        sheet.save(os.path.join(OUT, name + '.png'), optimize=True)
        meta[name] = dict(rows=len(out_rows), cellW=cw, cellH=chh, baselineFromTop=base)
    json.dump(meta, open(os.path.join(OUT, 'layout.json'), 'w'), indent=1)


main()
