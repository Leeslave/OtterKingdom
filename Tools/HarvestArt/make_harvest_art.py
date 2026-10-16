"""새 채소 10종 · 물고기 9종 임시 그림: 기존 채소 · 고등어 그림에서 채소(물고기) 부분만 색을 바꾸고 모양을 늘이고 줄임.

채소: 바탕 채소의 아이콘 · 모종 아이콘 · 밭 그림 9장(씨 · 새싹 · 다 자람 × 칸 3)에서 채소 색 영역만 새 색으로 (잎 · 흙 · 테두리는 그대로)
  → Assets/Art/Item/Vegetable/ICON_Crop_<Id>.png · ICON_Seedling_<Id>.png, Assets/Art/Farm/Crop/crop_<id>_<stage>_<n>.png
물고기: 고등어 아이콘의 등 · 배 색을 바꾸고 몸을 늘이고 줄임 → Assets/Art/Item/Fish/ICON_Fish_<Id>.png
표 · 수치는 Assets/Editor/Farm/HarvestExpansionSetup.cs와 같은 ID.

    python Tools/HarvestArt/make_harvest_art.py [--root <프로젝트>] [--preview]
"""
import os, sys
import numpy as np
import cv2
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
VEG = 'Assets/Art/Item/Vegetable'
FIELD = 'Assets/Art/Farm/Crop'
FISH = 'Assets/Art/Item/Fish'

# 바탕 채소: 아이콘 · 모종 아이콘 · 밭 그림 이름
BASES = {
    'carrot': ('Carrot', 'ICON_Seeding_Carrot', 'carrot'),
    'potato': ('Potato', 'ICON_Seedling_Potato', 'potato'),
    'cucumber': ('ICON_Cucumber', 'ICON_Seeding_Cucumber', 'cucumber'),
    'strawberry': ('Strawberry', 'ICON_Seedling_Strawberry', 'strawberry'),
}

# (Id, 밭 이름(소문자), 바탕, 채소 색, 씨앗 점 색(딸기만, 없으면 그대로))
CROPS = [
    ('Radish', 'radish', 'carrot', (244, 240, 230), None),
    ('Taro', 'taro', 'potato', (150, 118, 96), None),
    ('Zucchini', 'zucchini', 'cucumber', (150, 196, 80), None),
    ('Raspberry', 'raspberry', 'strawberry', (226, 60, 110), (250, 180, 200)),
    ('Beet', 'beet', 'carrot', (168, 32, 88), None),
    ('PurplePotato', 'purple_potato', 'potato', (150, 96, 170), None),
    ('WhiteStrawberry', 'white_strawberry', 'strawberry', (252, 240, 232), (230, 60, 70)),
    ('Blueberry', 'blueberry', 'strawberry', (70, 96, 196), (200, 220, 255)),
    ('GoldenCarrot', 'golden_carrot', 'carrot', (246, 196, 40), None),
    ('GoldenPotato', 'golden_potato', 'potato', (244, 200, 60), None),
]

# (Id, 등 색, 배 색, 가로 배율, 세로 배율)
FISHES = [
    ('Sardine', (64, 96, 150), (226, 232, 238), 1.0, 0.78),
    ('HorseMackerel', (124, 142, 112), (236, 226, 176), 1.0, 0.95),
    ('Saury', (44, 64, 118), (220, 228, 236), 1.0, 0.62),
    ('Rockfish', (104, 92, 84), (186, 176, 166), 0.95, 1.22),
    ('SeaBream', (236, 112, 124), (252, 216, 216), 0.95, 1.32),
    ('Salmon', (112, 132, 154), (246, 164, 134), 1.0, 1.0),
    ('Flounder', (196, 164, 116), (236, 222, 196), 1.0, 1.55),
    ('Tuna', (34, 54, 104), (226, 232, 240), 1.0, 1.18),
    ('GoldenMackerel', (244, 192, 52), (255, 236, 160), 1.0, 1.0),
]


def hsv(rgba):
    return cv2.cvtColor(rgba[..., :3].astype(np.float32) / 255, cv2.COLOR_RGB2HSV)


def hsv_of(rgb):
    h, s, v = cv2.cvtColor(np.array([[rgb]], np.float32) / 255, cv2.COLOR_RGB2HSV)[0, 0]
    return float(h), float(s), float(v)


def veggie_mask(rgba, base):
    h, s, v = [c for c in np.moveaxis(hsv(rgba), -1, 0)]
    solid = rgba[..., 3] > 8
    if base == 'carrot':
        m = (h >= 12) & (h <= 45) & (s >= 0.5) & (v >= 0.62)
    elif base == 'potato':
        m = (h >= 22) & (h <= 52) & (s > 0.18) & (s < 0.62) & (v >= 0.62)
    elif base == 'cucumber':
        m = (h >= 50) & (h <= 160) & (s >= 0.12) & (v >= 0.15)
    else:  # strawberry: 빨간 열매 (테두리 V ≤ 0.4 · 모종 흙 V ≈ 0.55는 빼고, 열매 V 0.65~)
        m = ((h <= 15) | (h >= 335)) & (s >= 0.45) & (v >= 0.62)
    return m & solid


def seed_mask(rgba, berry):
    """딸기 씨앗: 빨간 열매 안의 노란 점"""
    h, s, v = [c for c in np.moveaxis(hsv(rgba), -1, 0)]
    yellow = (h >= 36) & (h <= 66) & (s >= 0.3) & (v >= 0.55) & (rgba[..., 3] > 8)
    inside = ndimage.binary_fill_holes(ndimage.binary_closing(berry, iterations=2))  # 씨앗은 열매 안의 구멍
    return yellow & inside


def recolor(rgba, mask, target):
    """마스크 안을 명암을 지킨 채 target 색으로 (어둡게는 곱해서, 밝게는 더해서)"""
    out = rgba.copy()
    if not mask.any():
        return out
    hv = hsv(rgba)
    h, s, v = hv[..., 0][mask], hv[..., 1][mask], hv[..., 2][mask]
    rh, rs, rv = float(np.median(h)), max(float(np.median(s)), 0.05), max(float(np.median(v)), 0.05)
    th, ts, tv = hsv_of(target)
    dh = (h - rh + 180) % 360 - 180
    nh = (th + dh * 0.5) % 360
    ns = np.clip(s * (ts / rs), 0, 1)
    nv = v * (tv / rv) if tv <= rv else v + (tv - rv)
    nv = np.clip(nv, 0, 1)
    rgb = cv2.cvtColor(np.stack([nh, ns, nv], -1).astype(np.float32)[None], cv2.COLOR_HSV2RGB)[0]
    out[..., :3][mask] = np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


def make_crop_image(path, base, color, seed_color, max_size=None):
    img = Image.open(path).convert('RGBA')
    if max_size and max(img.size) > max_size:
        img.thumbnail((max_size, max_size), Image.LANCZOS)
    rgba = np.array(img)
    mask = veggie_mask(rgba, base)
    out = recolor(rgba, mask, color)
    if base == 'strawberry' and seed_color is not None:
        out = recolor(out, seed_mask(rgba, mask), seed_color)
    return Image.fromarray(out)


def fish_masks(rgba):
    h, s, v = [c for c in np.moveaxis(hsv(rgba), -1, 0)]
    solid = rgba[..., 3] > 8
    dark = v < 0.36
    back = solid & ~dark & (((h >= 150) & (h <= 230) & (s >= 0.06)) | ((s < 0.12) & (v < 0.66)))
    belly = solid & ~dark & ~back & (s < 0.2) & (v >= 0.6)
    return back, belly


def make_fish(path, back_color, belly_color, sx, sy):
    rgba = np.array(Image.open(path).convert('RGBA'))
    back, belly = fish_masks(rgba)
    out = recolor(rgba, back, back_color)
    out = recolor(out, belly, belly_color)
    img = Image.fromarray(out)
    w, h = img.size
    bbox = img.getbbox()
    body = img.crop(bbox)
    nw, nh = int(body.width * sx), int(body.height * sy)
    fit = min(1.0, (w * 0.96) / nw, (h * 0.96) / nh)
    body = body.resize((max(1, int(nw * fit)), max(1, int(nh * fit))), Image.LANCZOS)
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    canvas.alpha_composite(body, ((w - body.width) // 2, (h - body.height) // 2))
    if w > 512:
        canvas.thumbnail((512, 512), Image.LANCZOS)
    return canvas


def main():
    args = sys.argv[1:]
    root = ROOT
    if '--root' in args:
        root = args[args.index('--root') + 1]
    src = lambda rel: os.path.join(ROOT, rel)
    dst = lambda rel: os.path.join(root, rel)
    previews = []
    for cid, field, base, color, seed_color in CROPS:
        icon, seedling, base_field = BASES[base]
        crop_icon = make_crop_image(src(f'{VEG}/{icon}.png'), base, color, seed_color, 512)
        crop_icon.save(dst(f'{VEG}/ICON_Crop_{cid}.png'), optimize=True)
        seed_icon = make_crop_image(src(f'{VEG}/{seedling}.png'), base, color, seed_color, 256)
        seed_icon.save(dst(f'{VEG}/ICON_Seedling_{cid}.png'), optimize=True)
        tiles = [crop_icon, seed_icon]
        for stage in ('seed', 'sprout', 'grown'):
            for n in range(3):
                img = make_crop_image(src(f'{FIELD}/crop_{base_field}_{stage}_{n}.png'), base, color, seed_color)
                img.save(dst(f'{FIELD}/crop_{field}_{stage}_{n}.png'), optimize=True)
                if n == 1:
                    tiles.append(img)
        previews.append(tiles)
        print('crop', cid)
    fish_tiles = []
    for fid, back, belly, sx, sy in FISHES:
        img = make_fish(src(f'{FISH}/Mackerel.png'), back, belly, sx, sy)
        img.save(dst(f'{FISH}/ICON_Fish_{fid}.png'), optimize=True)
        fish_tiles.append(img)
        print('fish', fid)
    if '--preview' in args:
        T = 120
        sheet = Image.new('RGBA', (T * 5, T * (len(previews) + 2)), (238, 228, 204, 255))
        for r, tiles in enumerate(previews):
            for c, t in enumerate(tiles):
                t = t.copy()
                t.thumbnail((T, T))
                sheet.alpha_composite(t, (c * T, r * T))
        for i, t in enumerate(fish_tiles):
            t = t.copy()
            t.thumbnail((T, T))
            sheet.alpha_composite(t, ((i % 5) * T, (len(previews) + i // 5) * T))
        out = os.path.join(root, 'ArtSource', 'Harvest_preview.png')
        os.makedirs(os.path.dirname(out), exist_ok=True)
        sheet.save(out)
        print(out)


if __name__ == '__main__':
    main()
