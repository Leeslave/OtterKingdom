# Cuts the AI-generated plaza prop sheet (transparent PNG, 3 cols x 4 rows
# of loose objects) into one PNG per prop, and records each prop's ground
# pivot — the point where it meets the floor — into
# Assets/Art/Plaza/Props/plaza_props_pivots.json for PlazaSceneSetup.cs.
#
# Pivot x = centre of the opaque pixels in the bottom few rows (so the lamp
# pivots on its post, not the middle of its bounding box); pivot y = the
# lowest solid row. Both normalised to the cropped image, as Unity expects.
#
# Usage (from repo root): python Tools/PlazaProps/slice_plaza_props.py
from PIL import Image
import numpy as np
from scipy import ndimage
import json, os

SRC = 'Tools/PlazaProps/plaza_props_sheet.png'
OUT = 'Assets/Art/Plaza/Props'
# Sheet layout, row-major.
NAMES = [
    ['Tree', 'House_Red', 'House_Blue'],
    ['Bench', 'Fence_Rising', 'Fence_Falling'],  # Rising = '/' , Falling = '\'
    ['Lamp', 'Bush', 'Rock'],
    ['Picnic', 'Log', 'Stump'],
]
ALPHA_CORE = 30    # separates props from each other
ALPHA_MIN = 8      # below this is generator noise (invisible, but bloats the crop)
ALPHA_SOLID = 128  # counts as the object's real edge for the pivot
MIN_PROP_AREA = 40 * 40
PAD = 2
BASE_BAND = 0.06   # bottom fraction of the object used for pivot x

img = Image.open(SRC).convert('RGBA')
rgba = np.asarray(img).copy()
rgba[rgba[..., 3] < ALPHA_MIN] = 0
alpha = rgba[..., 3]
H, W = alpha.shape

# Label on clearly-opaque pixels (the two houses touch through faint
# alpha), then hand every remaining non-transparent pixel — soft edges,
# loose petals — to the nearest prop.
core = alpha > ALPHA_CORE
lab, n = ndimage.label(core, structure=np.ones((3, 3)))
sizes = ndimage.sum(core, lab, range(1, n + 1))
keep = np.zeros(n + 1, dtype=np.int32)
big = []
for i in range(n):
    if sizes[i] >= MIN_PROP_AREA:
        big.append(i + 1)
        keep[i + 1] = i + 1
lab = keep[lab]
_, (iy, ix) = ndimage.distance_transform_edt(lab == 0, return_indices=True)
lab = np.where(alpha > 0, lab[iy, ix], 0)

rows = len(NAMES)
cols = len(NAMES[0])
if len(big) != rows * cols:
    raise SystemExit(f'expected {rows * cols} props, found {len(big)} — tweak ALPHA_CORE/MIN_PROP_AREA')

os.makedirs(OUT, exist_ok=True)
meta = []
for b in big:
    own_full = (lab == b) & (rgba[..., 3] > 0)
    ys, xs = np.nonzero(own_full)
    y0, y1 = max(ys.min() - PAD, 0), min(ys.max() + 1 + PAD, H)
    x0, x1 = max(xs.min() - PAD, 0), min(xs.max() + 1 + PAD, W)
    cy, cx = (ys.min() + ys.max()) / 2, (xs.min() + xs.max()) / 2
    name = NAMES[int(cy / H * rows)][int(cx / W * cols)]
    crop = rgba[y0:y1, x0:x1].copy()
    crop[~own_full[y0:y1, x0:x1]] = 0  # drop any neighbour pixels inside the box

    solid = crop[..., 3] >= ALPHA_SOLID
    ys, xs = np.nonzero(solid)
    bottom = ys.max()
    top = ys.min()
    band = ys >= bottom - max(2, int((bottom - top) * BASE_BAND))
    px = xs[band].mean()
    h, w = crop.shape[:2]
    Image.fromarray(crop).save(f'{OUT}/Prop_{name}.png')
    meta.append({
        'name': name, 'width': w, 'height': h,
        'pivotX': round(float((px + 0.5) / w), 4),
        'pivotY': round(float((h - 1 - bottom) / h), 4),
    })
    print(f"{name:14s} {w}x{h} pivot ({meta[-1]['pivotX']}, {meta[-1]['pivotY']})")

# Wrapped in an object and kept flat so Unity's JsonUtility can read it.
meta.sort(key=lambda m: m['name'])
with open(f'{OUT}/plaza_props_pivots.json', 'w') as f:
    json.dump({'props': meta}, f, indent=2)
