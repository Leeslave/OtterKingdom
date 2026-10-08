"""농부 해달 그림을 바탕으로 털 · 모자 · 멜빵바지 색을 바꾸고 소품을 얹어 해달 여러 마리를 만든다 (임시 그림).

농부 해달의 시트 5장(Assets/Sprites/Characters/FarmerOtter, 8열 × 416×240 칸)과 얼굴(ICON_Otter_Farmer)을
색 영역으로 나눠 (털 · 모자 · 모자 띠 · 멜빵바지 · 단추, 테두리 · 흰 털 · 볼 · 입은 그대로) 명암을 지킨 채 색을 바꾸고,
칸마다 모자 띠 · 목 위치를 찾아 소품(꽃 · 리본 · 새싹 · 깃털 · 별 · 목도리 · 방울 · 나비넥타이 · 체리)을 그린다.
같은 그림·같은 뼈대 칸이라 기존 농부 해달 애니메이션(Idle · 걷기 · 수확 · 하품 등)을 그대로 쓴다.

    python Tools/OtterCast/farmer_cast.py            # 전부 만들기 → Assets/Sprites/Characters/Cast · Assets/Art/Otter
    python Tools/OtterCast/farmer_cast.py --lineup   # 줄 세운 그림만 (ArtSource/Otter/FarmerCast/lineup.png)
그 뒤 Unity에서 Tools/Settlement/Apply Otter Cast (시트 자르기 · 애니메이션 · 광장 프리팹 · 해달에 연결)
"""
import json, math, os, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SRC = os.path.join(ROOT, 'Assets', 'Sprites', 'Characters', 'FarmerOtter')
ICON = os.path.join(ROOT, 'Assets', 'Art', 'Otter', 'ICON_Otter_Farmer.png')
OUT = os.path.join(ROOT, 'ArtSource', 'Otter', 'FarmerCast')
CAST = os.path.join(HERE, 'cast.json')

CELL_W, CELL_H = 416, 240
# 시트 → 줄마다 보는 방향 (side_r · side_l · front · back)
SHEETS = {
    'FarmerOtter_Sheet': ['side_r', 'side_l', 'front', 'back', 'side_r'],
    'FarmerOtter_IdleAction_Eat': ['side_r', 'side_l', 'front', 'back'],
    'FarmerOtter_IdleAction_Net': ['side_r', 'side_l', 'front', 'back'],
    'FarmerOtter_IdleAction_Squat': ['side_r', 'side_l', 'front', 'back'],
    'FarmerOtter_IdleAction_Stretch': ['side_r', 'side_l', 'front', 'back'],
}

# 바탕 색 (농부 해달): 털 · 모자 · 멜빵바지 · 모자 띠 (HSV, H는 도)
REF = {
    'fur': (15.0, 0.40, 0.82),
    'hat': (39.0, 0.66, 0.99),
    'overall': (96.0, 0.32, 0.45),
    'band': (14.0, 0.64, 0.42),
}
OUTLINE = (58, 28, 18)

KEEP, FUR, HAT, OVERALL, BAND, BUTTON = 0, 1, 2, 3, 4, 5


def hsv_of(rgb):
    r, g, b = [c / 255 for c in rgb]
    arr = np.array([[[r, g, b]]], np.float32)
    h, s, v = cv2.cvtColor(arr, cv2.COLOR_RGB2HSV)[0, 0]
    return float(h), float(s), float(v)


def classify(rgba):
    """칸(또는 얼굴) 하나를 색 영역으로 나눔"""
    rgb = rgba[..., :3].astype(np.float32) / 255
    a = rgba[..., 3]
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    solid = a > 8
    dark = v < 0.22
    white = (s < 0.16) & (v > 0.55)
    # 볼(분홍, 털보다 붉고 밝음: H 4~8 · V 0.94~) · 입 · 혀(진한 빨강)는 그대로. 손발 그늘(H 8~10 · S 0.55 · V 0.6)은 털
    pinkish = (h <= 8.5) | (h >= 340)
    blush = pinkish & (v >= 0.94) & (s > 0.2) & (s < 0.55)
    mouth = pinkish & (s >= 0.6) & (v >= 0.55)
    red = blush | mouth
    hat = (h >= 22) & (h <= 62) & (s >= 0.42) & (v >= 0.35)
    overall = (h > 62) & (h <= 160) & (s > 0.10) & (v >= 0.2)
    fur = ((h <= 30) | (h >= 330)) & (s >= 0.12)
    label = np.zeros(a.shape, np.uint8)
    keep = ~solid | dark | white | red
    label[fur & ~keep] = FUR
    label[hat & ~keep] = HAT
    label[overall & ~keep] = OVERALL

    # 단추: 멜빵바지 높이에 있는 작은 모자색 덩어리 (귀에 잘린 모자 조각은 모자 그대로) / 모자는 가장 큰 덩어리
    hat_mask = label == HAT
    comps, n = ndimage.label(hat_mask)
    if n:
        sizes = ndimage.sum(hat_mask, comps, range(1, n + 1))
        big = 1 + int(np.argmax(sizes))
        oy = np.nonzero(label == OVERALL)[0]
        overall_top = np.percentile(oy, 2) if len(oy) > 50 else a.shape[0]
        centers = ndimage.center_of_mass(hat_mask, comps, range(1, n + 1))
        for i, (size, (cy, _)) in enumerate(zip(sizes, centers), start=1):
            if i != big and size < 400 and cy > overall_top - 4:
                label[comps == i] = BUTTON
        hat_mask = (label == HAT)
    # 모자 띠: 위아래 가까이에 모자가 있는 털색 (모자 안쪽 줄)
    if hat_mask.any():
        reach = 16
        csum = np.vstack([np.zeros((1, hat_mask.shape[1]), np.int32), np.cumsum(hat_mask, axis=0, dtype=np.int32)])
        ys = np.arange(hat_mask.shape[0])
        lo = np.clip(ys - reach, 0, None)
        hi = np.clip(ys + reach + 1, None, hat_mask.shape[0])
        above = (csum[ys] - csum[lo]) > 0          # 위 reach칸 안에 모자
        below = (csum[hi] - csum[ys + 1]) > 0      # 아래 reach칸 안에 모자
        band = (label == FUR) & above & below
        label[band] = BAND
    return label, hsv


def recolor(rgba, label, hsv, look):
    out = rgba.copy()
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    for cls, key in ((FUR, 'fur'), (HAT, 'hat'), (OVERALL, 'overall'), (BAND, 'band'), (BUTTON, 'button')):
        mask = label == cls
        if not mask.any() or key not in look:
            continue
        ref = REF['hat'] if key == 'button' else REF[key]
        th, ts, tv = hsv_of(look[key])
        nh = (th + (h[mask] - ref[0]) * 0.5) % 360
        ns = np.clip(s[mask] * (ts / ref[1]), 0, 1)
        # 어두운 색으로는 곱해서, 밝은 색으로는 더해서 (밝게 곱하면 밝은 쪽이 하얗게 날아감)
        nv = v[mask] * (tv / ref[2]) if tv <= ref[2] else v[mask] + (tv - ref[2])
        nv = np.clip(nv, 0, 1)
        px = np.stack([nh, ns, nv], -1).astype(np.float32)[None]
        rgb = cv2.cvtColor(px, cv2.COLOR_HSV2RGB)[0]
        out[..., :3][mask] = np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


# ---------------- 소품 ----------------

def shade(rgb, k):
    return tuple(int(max(0, min(255, c * k))) for c in rgb)


def measure(label):
    """앞모습 한 칸에서 잰 몸 치수: 모자 폭 · 모자 꼭대기 → 목 거리 · 목 반폭 (옆 · 뒤 모습 목 위치에 씀)"""
    ys, xs = np.nonzero((label == HAT) | (label == BAND))
    oy, ox = np.nonzero(label == OVERALL)
    top = int(np.percentile(oy, 2))
    row = ox[np.abs(oy - top - 6) < 4]
    return {'hat_w': float(xs.max() - xs.min()), 'neck_drop': float(top - ys.min()), 'neck_half': float(row.max() - row.min()) / 2}


def anchors(label, view, geom):
    """모자 띠 위치 · 목 위치 (없으면 None). geom = 같은 크기 앞모습에서 잰 치수"""
    ys, xs = np.nonzero((label == HAT) | (label == BAND))
    if len(xs) < 50:
        return None
    hx0, hx1, hy0, hy1 = xs.min(), xs.max(), ys.min(), ys.max()
    by, bx = np.nonzero(label == BAND)
    band_y = int(np.median(by)) if len(by) > 20 else int(hy0 + (hy1 - hy0) * 0.6)
    oy, ox = np.nonzero(label == OVERALL)
    neck = None
    if len(oy) > 50:
        top = int(np.percentile(oy, 2))
        if view == 'front':
            row = ox[np.abs(oy - top - 6) < 4]
            cx = (row.min() + row.max()) / 2 if len(row) else ox.mean()
        else:
            # 옆 · 뒤는 바지 꼭대기가 가슴 · 등 가운데라 모자 꼭대기에서 앞모습만큼 내려온 곳 (웅크리면 바지 꼭대기)
            top = int(min(top, hy0 + geom['neck_drop']))
            cx = (ox.min() + ox.max()) / 2
        half = geom['neck_half'] * (0.8 if view in ('side_r', 'side_l') else 1.0)
        neck = (int(cx - half), int(cx + half), top)
    hat_w = hx1 - hx0
    if view == 'front':
        pin = (hx0 + hat_w * 0.74, band_y)
    elif view == 'back':
        pin = (hx0 + hat_w * 0.30, band_y)
    elif view == 'side_r':
        pin = (hx0 + hat_w * 0.60, band_y)  # 뒤쪽은 귀가 띠를 가림
    else:
        pin = (hx1 - hat_w * 0.60, band_y)
    return {'hat': (hx0, hy0, hx1, hy1), 'pin': pin, 'top': ((hx0 + hx1) / 2, hy0), 'neck': neck, 'view': view,
            'scale': geom['hat_w'] / 150}


def outline_poly(d, pts, fill, w):
    d.polygon(pts, fill=fill, outline=OUTLINE, width=w)


def flower(d, cx, cy, r, color, center=(255, 214, 92)):
    w = max(2, int(r * 0.22))
    for i in range(5):
        a = -math.pi / 2 + i * 2 * math.pi / 5
        px, py = cx + math.cos(a) * r * 0.62, cy + math.sin(a) * r * 0.62
        d.ellipse([px - r * 0.5, py - r * 0.5, px + r * 0.5, py + r * 0.5], fill=color, outline=OUTLINE, width=w)
    d.ellipse([cx - r * 0.32, cy - r * 0.32, cx + r * 0.32, cy + r * 0.32], fill=center, outline=OUTLINE, width=w)


def bow(d, cx, cy, r, color):
    w = max(2, int(r * 0.2))
    outline_poly(d, [(cx, cy), (cx - r, cy - r * 0.7), (cx - r * 1.05, cy + r * 0.7)], color, w)
    outline_poly(d, [(cx, cy), (cx + r, cy - r * 0.7), (cx + r * 1.05, cy + r * 0.7)], color, w)
    d.ellipse([cx - r * 0.32, cy - r * 0.32, cx + r * 0.32, cy + r * 0.32], fill=shade(color, 0.85), outline=OUTLINE, width=w)


def leaf(d, x0, y0, x1, y1, color, w):
    """줄기 끝(x0, y0)에서 (x1, y1)로 뻗은 통통한 잎"""
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    nx, ny = -(y1 - y0) * 0.38, (x1 - x0) * 0.38
    pts = []
    for t in np.linspace(0, 1, 9):
        bx, by = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        k = math.sin(math.pi * t)
        pts.append((bx + nx * k, by + ny * k))
    for t in np.linspace(1, 0, 9)[1:-1]:
        bx, by = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        k = math.sin(math.pi * t)
        pts.append((bx - nx * k, by - ny * k))
    outline_poly(d, pts, color, w)


def sprout(d, cx, cy, r, color):
    w = max(2, int(r * 0.11))
    top = cy - r * 0.75
    d.line([(cx, cy), (cx, top)], fill=OUTLINE, width=w * 3)
    d.line([(cx, cy), (cx, top)], fill=shade(color, 0.85), width=w)
    leaf(d, cx, top, cx - r * 0.95, top - r * 0.5, color, w)
    leaf(d, cx, top, cx + r * 0.95, top - r * 0.62, shade(color, 1.08), w)


def feather(d, cx, cy, r, color, lean):
    w = max(2, int(r * 0.18))
    tip = (cx + lean * r * 0.5, cy - r * 1.5)
    pts = [(cx, cy), (cx - r * 0.38 + lean * r * 0.1, cy - r * 0.8), tip, (cx + r * 0.38 + lean * r * 0.3, cy - r * 0.7)]
    outline_poly(d, pts, color, w)
    d.line([(cx, cy), tip], fill=shade(color, 0.7), width=max(1, w - 1))


def star(d, cx, cy, r, color):
    w = max(2, int(r * 0.2))
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    outline_poly(d, pts, color, w)


def cherries(d, cx, cy, r, color):
    w = max(2, int(r * 0.2))
    d.line([(cx - r * 0.45, cy + r * 0.15), (cx + r * 0.1, cy - r * 0.9)], fill=(90, 120, 50), width=w)
    d.line([(cx + r * 0.5, cy + r * 0.05), (cx + r * 0.1, cy - r * 0.9)], fill=(90, 120, 50), width=w)
    for ox, oy in ((-0.45, 0.35), (0.5, 0.25)):
        x, y = cx + ox * r, cy + oy * r
        d.ellipse([x - r * 0.4, y - r * 0.4, x + r * 0.4, y + r * 0.4], fill=color, outline=OUTLINE, width=w)
        d.ellipse([x - r * 0.2, y - r * 0.25, x - r * 0.04, y - r * 0.09], fill=(255, 235, 235))


def neckwear(d, a, kind, color, color2=None):
    if a['neck'] is None:
        return
    x0, x1, top = a['neck']
    view = a['view']
    k = a['scale']
    w = max(2, int(3 * k))
    cx = (x0 + x1) / 2
    half = (x1 - x0) / 2 + 4 * k
    y = top - 2 * k
    if kind in ('scarf', 'collar'):
        th = (11 if kind == 'scarf' else 7) * k
        d.rounded_rectangle([cx - half, y - th, cx + half, y + th * 0.5], radius=th * 0.6, fill=color, outline=OUTLINE, width=w)
        if kind == 'scarf' and color2:
            for i in (-1, 1):
                sx = cx + i * half * 0.45
                d.line([(sx, y - th + w), (sx, y + th * 0.5 - w)], fill=color2, width=max(2, int(3 * k)))
        if kind == 'scarf':
            if view == 'front':
                tail = [(cx - 2 * k, y), (cx + 9 * k, y), (cx + 12 * k, y + 22 * k), (cx + 1 * k, y + 20 * k)]
                outline_poly(d, tail, shade(color, 0.92), w)
            elif view in ('side_r', 'side_l'):
                sgn = -1 if view == 'side_r' else 1
                bx = cx + sgn * half * 0.8
                tail = [(bx, y - 4 * k), (bx + sgn * 14 * k, y + 8 * k), (bx + sgn * 8 * k, y + 16 * k), (bx - sgn * 2 * k, y + 4 * k)]
                outline_poly(d, tail, shade(color, 0.92), w)
        if kind == 'collar' and view != 'back':
            bx = cx if view == 'front' else cx + (half * 0.55 if view == 'side_r' else -half * 0.55)
            br = 7 * k
            d.ellipse([bx - br, y - br * 0.2, bx + br, y + br * 1.8], fill=(255, 210, 80), outline=OUTLINE, width=w)
            d.line([(bx - br * 0.7, y + br * 0.9), (bx + br * 0.7, y + br * 0.9)], fill=OUTLINE, width=max(1, w - 1))
    elif kind == 'bowtie' and view != 'back':
        bx = cx if view == 'front' else cx + (half * 0.6 if view == 'side_r' else -half * 0.6)
        r = (12 if view == 'front' else 8) * k
        bow(d, bx, y, r, color)
    elif kind == 'kerchief':
        th = 6 * k
        d.rounded_rectangle([cx - half, y - th, cx + half, y + th * 0.4], radius=th * 0.5, fill=color, outline=OUTLINE, width=w)
        if view == 'front':
            outline_poly(d, [(cx - half * 0.7, y), (cx + half * 0.7, y), (cx, y + 26 * k)], color, w)
            if color2:
                d.ellipse([cx - 3 * k, y + 6 * k, cx + 3 * k, y + 12 * k], fill=color2)
                d.ellipse([cx - 9 * k, y + 1 * k, cx - 4 * k, y + 6 * k], fill=color2)
                d.ellipse([cx + 4 * k, y + 1 * k, cx + 9 * k, y + 6 * k], fill=color2)
        elif view == 'back':
            outline_poly(d, [(cx - half * 0.6, y), (cx + half * 0.6, y), (cx, y + 20 * k)], color, w)


def hatwear(d, a, kind, color, center=None):
    k = a['scale']
    px, py = a['pin']
    lean = -1 if a['view'] == 'side_r' else 1
    if kind == 'flower':
        flower(d, px, py, 15 * k, color, center or (255, 214, 92))
    elif kind == 'flowers':
        flower(d, px, py, 13 * k, color)
        flower(d, px - lean * 18 * k, py + 2 * k, 10 * k, (255, 255, 255))
    elif kind == 'bow':
        bow(d, px, py, 14 * k, color)
    elif kind == 'feather':
        feather(d, px, py + 2 * k, min(18 * k, (py - 2) / 1.6), color, lean)
    elif kind == 'star':
        star(d, px, py, 13 * k, color)
    elif kind == 'cherry':
        cherries(d, px, py, 14 * k, color)
    elif kind == 'sprout':
        tx, ty = a['top']
        base = ty + 8 * k
        r = min(20 * k, (base - 2) / 1.45)  # 칸 위로 잘리지 않게 (서 있는 칸은 모자 꼭대기가 칸 위쪽에 붙어 있음)
        sprout(d, tx, base, r, color)


def decorate(rgba, label, look, view, geom):
    a = anchors(label, view, geom)
    if a is None:
        return rgba
    scale = 2  # 두 배로 그려 줄여서 계단 줄이기
    layer = Image.new('RGBA', (rgba.shape[1] * scale, rgba.shape[0] * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    big = dict(a)
    big['hat'] = tuple(v * scale for v in a['hat'])
    big['pin'] = (a['pin'][0] * scale, a['pin'][1] * scale)
    big['top'] = (a['top'][0] * scale, a['top'][1] * scale)
    big['neck'] = None if a['neck'] is None else tuple(v * scale for v in a['neck'])
    big['scale'] = a['scale'] * scale
    # 목 소품은 앞 · 뒤에서만 (옆모습은 팔 · 몸이 목을 가려 붙일 자리가 없음). 모자 소품은 어느 쪽에서나
    for item in look.get('neck', []) if view in ('front', 'back') else []:
        neckwear(d, big, item['kind'], tuple(item['color']), tuple(item['color2']) if item.get('color2') else None)
    for item in look.get('hatwear', []):
        hatwear(d, big, item['kind'], tuple(item['color']), tuple(item['center']) if item.get('center') else None)
    layer = layer.resize((rgba.shape[1], rgba.shape[0]), Image.LANCZOS)
    base = Image.fromarray(rgba)
    base.alpha_composite(layer)
    return np.array(base)


_SHEET_GEOM = None


def sheet_geom():
    """시트 칸은 모두 같은 크기라 앞모습 한 칸(본 시트 3줄 1칸)에서 한 번 잼"""
    global _SHEET_GEOM
    if _SHEET_GEOM is None:
        sheet = np.array(Image.open(os.path.join(SRC, 'FarmerOtter_Sheet.png')).convert('RGBA'))
        _SHEET_GEOM = measure(classify(sheet[2 * CELL_H:3 * CELL_H, 0:CELL_W])[0])
    return _SHEET_GEOM


def make_cell(cell, look, view, geom=None):
    label, hsv = classify(cell)
    out = recolor(cell, label, hsv, look)
    return decorate(out, label, look, view, geom or sheet_geom())


def make_icon(look):
    icon = np.array(Image.open(ICON).convert('RGBA'))
    # 얼굴은 256칸이라 소품 크기를 칸 크기에 맞춤 (모자 폭으로 저절로)
    return Image.fromarray(make_cell(icon, look, 'front', measure(classify(icon)[0])))


def load_cast():
    return json.load(open(CAST, encoding='utf-8'))


def lineup(cast, out_path):
    """얼굴 + 앞 · 옆 모습을 5 × 4로"""
    sheet = np.array(Image.open(os.path.join(SRC, 'FarmerOtter_Sheet.png')).convert('RGBA'))
    front = sheet[2 * CELL_H:3 * CELL_H, 0:CELL_W]
    side = sheet[0:CELL_H, 0:CELL_W]
    tile_w, tile_h = 300, 300
    cols = 5
    rows = math.ceil(len(cast) / cols)
    img = Image.new('RGBA', (cols * tile_w, rows * tile_h), (250, 244, 232, 255))
    d = ImageDraw.Draw(img)
    for i, look in enumerate(cast):
        x, y = (i % cols) * tile_w, (i // cols) * tile_h
        ic = make_icon(look).resize((150, 150), Image.LANCZOS)
        img.alpha_composite(ic, (x + 4, y + 8))
        f = Image.fromarray(make_cell(front, look, 'front')).crop((108, 10, 308, 240)).resize((130, 150), Image.LANCZOS)
        img.alpha_composite(f, (x + 158, y + 6))
        s = Image.fromarray(make_cell(side, look, 'side_r')).crop((60, 10, 300, 240)).resize((150, 144), Image.LANCZOS)
        img.alpha_composite(s, (x + 70, y + 150))
        d.text((x + 8, y + 276), f"{i + 1}. {look['id']}", fill=(90, 60, 40))
    img.convert('RGB').save(out_path)
    print(out_path, img.size)


# 게임용 묶음 시트: 광장에서 쓰는 줄만 0.75배로 6열에 (2048 안에 들어감 → 모바일 · WebGL에서도 줄어들지 않음)
# 순서 · 칸 크기는 Assets/Editor/Settlement/OtterCastSetup.cs와 같아야 함
PACK_SCALE = 0.75
PACK_COLS = 6
PACK_W, PACK_H = int(CELL_W * PACK_SCALE), int(CELL_H * PACK_SCALE)  # 312 × 180
PACK_CLIPS = [  # (클립, 원본 시트, 줄)
    ('Walk', 'FarmerOtter_Sheet', 0), ('Walk_Down', 'FarmerOtter_Sheet', 2), ('Walk_Up', 'FarmerOtter_Sheet', 3),
    ('Harvest', 'FarmerOtter_Sheet', 4), ('Eat', 'FarmerOtter_IdleAction_Eat', 0), ('Net', 'FarmerOtter_IdleAction_Net', 0),
    ('Squat', 'FarmerOtter_IdleAction_Squat', 0), ('Stretch', 'FarmerOtter_IdleAction_Stretch', 0),
]
FRAMES = 8


def make_pack(look):
    count = len(PACK_CLIPS) * FRAMES
    rows = math.ceil(count / PACK_COLS)
    out = Image.new('RGBA', (PACK_COLS * PACK_W, rows * PACK_H), (0, 0, 0, 0))
    sheets = {}
    for i, (clip, sheet_name, row) in enumerate(PACK_CLIPS):
        if sheet_name not in sheets:
            sheets[sheet_name] = np.array(Image.open(os.path.join(SRC, sheet_name + '.png')).convert('RGBA'))
        sheet = sheets[sheet_name]
        view = SHEETS[sheet_name][row]
        for c in range(FRAMES):
            cell = sheet[row * CELL_H:(row + 1) * CELL_H, c * CELL_W:(c + 1) * CELL_W]
            img = Image.fromarray(make_cell(cell, look, view)).resize((PACK_W, PACK_H), Image.LANCZOS)
            k = i * FRAMES + c
            out.alpha_composite(img, ((k % PACK_COLS) * PACK_W, (k // PACK_COLS) * PACK_H))
    return out


def main():
    """python farmer_cast.py [--root <프로젝트>] [--lineup] [id ...]
    묶음 시트 → <root>/Assets/Sprites/Characters/Cast/Cast_<id>.png, 얼굴 → <root>/Assets/Art/Otter/ICON_Otter_Cast_<id>.png"""
    args = sys.argv[1:]
    root = ROOT
    if '--root' in args:
        root = args[args.index('--root') + 1]
        args = [a for a in args if a not in ('--root', root)]
    cast = load_cast()
    os.makedirs(OUT, exist_ok=True)
    if '--lineup' in args:
        lineup(cast, os.path.join(OUT, 'lineup.png'))
        return
    only = [a for a in args if not a.startswith('--')]
    sheet_dir = os.path.join(root, 'Assets', 'Sprites', 'Characters', 'Cast')
    icon_dir = os.path.join(root, 'Assets', 'Art', 'Otter')
    os.makedirs(sheet_dir, exist_ok=True)
    for look in cast:
        if only and look['id'] not in only:
            continue
        make_pack(look).save(os.path.join(sheet_dir, f"Cast_{look['id']}.png"), optimize=True)
        make_icon(look).save(os.path.join(icon_dir, f"ICON_Otter_Cast_{look['id']}.png"), optimize=True)
        print('made', look['id'])
    lineup(cast, os.path.join(OUT, 'lineup.png'))


if __name__ == '__main__':
    main()
