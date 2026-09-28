# Re-packs AI-generated FarmerOtter sheets into a uniform grid:
# every frame same cell size, same character scale, feet on a shared
# baseline and hat centre on the cell's centre column.
from PIL import Image
import numpy as np
from scipy import ndimage
import json, sys

SRC = 'images/'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'scratchpad/out/'
COLS = 8
# name, rows, neutral frames used for scale reference [(row,col)]
SHEETS = [
    ('5', 'FarmerOtter_Sheet',              5, [(0,c) for c in range(8)] + [(4,0),(4,7)]),
    ('1', 'FarmerOtter_IdleAction_Eat',     4, [(0,0),(0,7),(1,0),(1,7)]),
    ('2', 'FarmerOtter_IdleAction_Net',     4, [(0,0),(0,7),(1,0),(1,7)]),
    ('3', 'FarmerOtter_IdleAction_Stretch', 4, [(0,0),(0,7),(1,0),(1,7)]),
    ('4', 'FarmerOtter_IdleAction_Squat',   4, [(0,0),(0,7),(1,0),(1,7)]),
]
TARGET_H = 200.0     # neutral standing height in output px
TARGET_HAT = TARGET_H / 1.40
PAD = 6

def label(alpha):
    return ndimage.label(alpha, structure=np.ones((3,3)))

def extract(key, R):
    im = np.asarray(Image.open(SRC + key + '.png')).astype(np.uint8)
    alpha = im[...,3]
    a = alpha > 8
    # Rows can touch (sheet 5 rows 3/4), so cut along the emptiest scanline
    # between row centres before labelling.
    lab, n = label(a)
    sizes = np.array(ndimage.sum(a, lab, range(1, n+1)))
    rowsum = a.sum(1)
    H = a.shape[0]
    cuts = []
    for r in range(1, R):
        y = int(H * r / R)
        lo, hi = y - int(H/R*0.25), y + int(H/R*0.25)
        cuts.append(lo + int(np.argmin(rowsum[lo:hi])))
    a2 = a.copy()
    for y in cuts: a2[y, :] = False
    lab, n = label(a2)
    sizes = np.array(ndimage.sum(a2, lab, range(1, n+1)))
    objs = ndimage.find_objects(lab)
    order = list(np.argsort(-sizes))
    main = order[:COLS*R]
    cents = {m: ndimage.center_of_mass(a2, lab, m+1) for m in main}
    ms = sorted(main, key=lambda m: cents[m][0])
    grid = [sorted(ms[r*COLS:(r+1)*COLS], key=lambda m: cents[m][1]) for r in range(R)]
    owned = {m: [m] for m in main}
    for i in order[COLS*R:]:
        if sizes[i] < 40: continue       # speckle noise
        cy, cx = ndimage.center_of_mass(a2, lab, i+1)
        def d(m):
            sl = objs[m]
            dy = max(sl[0].start - cy, 0, cy - sl[0].stop)
            dx = max(sl[1].start - cx, 0, cx - sl[1].stop)
            return dx*dx + dy*dy
        owned[min(main, key=d)].append(i)
    frames = []
    for r in range(R):
        row = []
        for m in grid[r]:
            mask = np.isin(lab, [i+1 for i in owned[m]])
            mask = ndimage.binary_dilation(mask, iterations=2) & (alpha > 0)
            ys, xs = np.nonzero(mask)
            y0, y1, x0, x1 = ys.min(), ys.max()+1, xs.min(), xs.max()+1
            crop = im[y0:y1, x0:x1].copy()
            crop[..., 3] = np.where(mask[y0:y1, x0:x1], crop[..., 3], 0)
            # anchors: feet = bottom of main body blob, hat = x-centroid of
            # the largest yellow blob (net/crop/motion lines are not yellow)
            body = lab[y0:y1, x0:x1] == m+1
            bys = np.nonzero(body.any(1))[0]
            feet = bys.max() + 1
            rr, gg, bb = crop[...,0].astype(int), crop[...,1].astype(int), crop[...,2].astype(int)
            yel = (rr > 200) & (gg > 150) & (gg < 225) & (bb < 120) & body
            # hat = yellow pixels in the upper part of the body (brown band
            # splits crown/brim, so take them all rather than one blob)
            top = bys.min() + int((feet - bys.min()) * 0.55)
            yel[top:] = False
            hys, hxs = np.nonzero(yel)
            lo, hi = np.percentile(hxs, [1, 99])
            row.append(dict(img=crop, feet=feet, hatx=hxs.mean(),
                            hatw=hi-lo, h=feet - bys.min()))
        frames.append(row)
    return frames

def main():
    import os
    os.makedirs(OUT, exist_ok=True)
    all_frames = {}
    for key, name, R, neutral in SHEETS:
        fr = extract(key, R)
        hs = np.mean([fr[r][c]['h'] for r, c in neutral])
        ws = np.mean([fr[r][c]['hatw'] for r, c in neutral])
        scale = 0.5 * (TARGET_H / hs + TARGET_HAT / ws)
        print(f'{name}: neutral h={hs:.1f} hat={ws:.1f} scale={scale:.3f}')
        all_frames[name] = (fr, scale)
    # scale every frame, measure extents around anchor
    L = Rt = U = D = 0
    scaled = {}
    for name, (fr, s) in all_frames.items():
        rows = []
        for row in fr:
            out = []
            for f in row:
                h, w = f['img'].shape[:2]
                nw, nh = max(1, round(w*s)), max(1, round(h*s))
                img = Image.fromarray(f['img'], 'RGBA').resize((nw, nh), Image.LANCZOS)
                ax, ay = f['hatx']*s*nw/(w*s), f['feet']*nh/h
                L = max(L, ax); Rt = max(Rt, nw-ax); U = max(U, ay); D = max(D, nh-ay)
                out.append((img, ax, ay))
            rows.append(out)
        scaled[name] = rows
    half = int(np.ceil(max(L, Rt))) + PAD
    cw = half * 2
    base = int(np.ceil(U)) + PAD
    chh = base + int(np.ceil(D)) + PAD
    # round up to multiples of 4 for tidiness
    cw += (-cw) % 4; chh += (-chh) % 4
    print('cell', cw, chh, 'baseline from top', base)
    meta = dict(cellW=cw, cellH=chh, baselineFromTop=base, targetH=TARGET_H)
    for name, rows in scaled.items():
        sheet = Image.new('RGBA', (cw*COLS, chh*len(rows)), (0,0,0,0))
        for r, row in enumerate(rows):
            for c, (img, ax, ay) in enumerate(row):
                ox = c*cw + cw//2 - round(ax)
                oy = r*chh + base - round(ay)
                sheet.alpha_composite(img, (ox, oy))
        sheet.save(OUT + name + '.png', optimize=True)
        meta[name] = len(rows)
    json.dump(meta, open(OUT + 'layout.json', 'w'), indent=1)

main()
