"""광장 돌길 (번영의 시대 마을 정비: 광장 한 구역 흙길 → 돌길 C01, 큰길 C03).

광장 지도(Plaza_Aligned_8144)의 흙색 칸에만 돌을 깐 겹 그림을 만든다.
- 돌: 지도 좌표에 고정된 지터 격자 보로노이 → 어느 그림에서 그려도 같은 자리 · 같은 색 (그림끼리 겹쳐도 티 안 남)
- 풀 가장자리는 흙 테두리를 남김, 끝으로 갈수록 돌이 듬성듬성
- 그림마다 지도 비율 자리(u, v, 폭, 높이)를 출력 → IndustrySetup이 그대로 씀

python Tools/UIGen/plaza_paving.py → Assets/Art/Industry/Paving_PlazaCenter.png · Paving_Road_*.png · ICON_Paving*.png
"""
import json
import math
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
MAP = os.path.join(ROOT, 'Assets', 'Art', 'Plaza', 'AlignedOtterMap', 'Plaza_Aligned_8144.png')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Industry')

SPACING = 66     # 돌 간격 (픽셀, 8144 기준 ≈ 0.8 월드)
GAP = 5          # 돌 사이 틈 (흙이 보임)
LINE = 5         # 돌 외곽선
RIM = 26         # 풀 가장자리에 남기는 흙 테두리
SEED = 7

STONES = [(238, 230, 216), (228, 219, 203), (219, 210, 195), (233, 224, 207), (224, 214, 196)]
OUTLINE = (158, 134, 116)

# 가운데 흙 마당 (C01): 4072 지도의 (1880, 1540) · 반지름 약 500의 두 배
CENTER = (3760, 3080)
INNER = 900      # 여기까지는 돌이 빽빽
OUTER = 1250     # 여기서 돌이 끝남

# 큰길 (C03): 가운데 마당과 이어진 흙길을 길 따라 이만큼까지 (8144 기준), 끝 FADE 비율에서 듬성듬성
ROAD_REACH = 2600
ROAD_FADE = 0.25
ROAD_PIECE = 4096   # 그림 한 장 최대 크기 (넘으면 나눔)
ROAD_RIM = 10       # 좁은 길에도 두세 줄 깔리게 테두리를 좁게
GEO_SCALE = 8       # 길 따라 거리는 1/8 지도에서 셈


def dirt_mask(rgb):
    r, g, b = (rgb[..., i].astype(np.int16) for i in range(3))
    return ((r > 225) & (g > 150) & (g < 228) & (b > 80) & (b < 185) & (r - b > 55)).astype(np.uint8)


def cell_hash(gj, gi, salt):
    """격자 칸 → 0~1 (지도 좌표에 고정)"""
    with np.errstate(over='ignore'):
        h = (np.asarray(gj, np.int64) * 73856093) ^ (np.asarray(gi, np.int64) * 19349663) ^ np.int64(salt * 83492791 + SEED * 1640531)
        h = ((h ^ (h >> 13)) * 1274126177) & 0x7FFFFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFFFF) / float(0xFFFFFF)


def cell_point(gj, gi):
    """칸의 돌 가운데 (지도 픽셀)"""
    jx = (cell_hash(gj, gi, 1) - 0.5) * 0.76 * SPACING
    jy = (cell_hash(gj, gi, 2) - 0.5) * 0.76 * SPACING
    px = gi * SPACING + SPACING / 2 + jx + (gj % 2) * SPACING * 0.5
    py = gj * SPACING + SPACING / 2 + jy
    return px, py


def geodesic(full):
    """가운데 마당(INNER 안의 흙)에서 흙길을 따라 간 거리 (1/8 지도, 닿지 않으면 -1)"""
    small = np.asarray(full.resize((full.width // GEO_SCALE, full.height // GEO_SCALE), Image.BILINEAR))
    mask = dirt_mask(small)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    h, w = mask.shape
    yy, xx = np.mgrid[0:h, 0:w]
    seed = (np.hypot(xx * GEO_SCALE - CENTER[0], yy * GEO_SCALE - CENTER[1]) < INNER) & (mask > 0)
    dist = np.full((h, w), -1, np.int32)
    dist[seed] = 0
    front = seed.astype(np.uint8)
    reached = front.copy()
    kernel = np.ones((3, 3), np.uint8)
    for step in range(1, ROAD_REACH // GEO_SCALE + 1):
        grown = cv2.dilate(reached, kernel) & mask & (1 - reached)
        if not grown.any():
            break
        dist[grown > 0] = step * GEO_SCALE
        reached |= grown
    return dist


def segment_distance(x, y, poly):
    """꺾은선까지 거리와 길을 따라 간 비율 (0 = 시작, 1 = 끝)"""
    best, best_t = 1e18, 0.0
    lengths = [math.dist(poly[i], poly[i + 1]) for i in range(len(poly) - 1)]
    total = sum(lengths)
    walked = 0.0
    for (ax, ay), (bx, by), length in zip(poly, poly[1:], lengths):
        dx, dy = bx - ax, by - ay
        t = min(1.0, max(0.0, ((x - ax) * dx + (y - ay) * dy) / max(1e-9, dx * dx + dy * dy)))
        d = math.hypot(ax + t * dx - x, ay + t * dy - y)
        if d < best:
            best, best_t = d, (walked + t * length) / total
        walked += length
    return best, best_t


def render(full, box, keep_chance, rim=RIM):
    """box = (x0, y0, x1, y1) 지도 픽셀. keep_chance(x, y) → 이 칸에 돌을 둘 확률. rim = 풀 가장자리에 남기는 흙 테두리"""
    x0, y0, x1, y1 = box
    crop = np.asarray(full.crop(box))
    h, w = crop.shape[:2]
    mask = dirt_mask(crop)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    edge = cv2.distanceTransform(mask, cv2.DIST_L2, 5)

    j0, j1 = y0 // SPACING - 2, y1 // SPACING + 3
    i0, i1 = x0 // SPACING - 3, x1 // SPACING + 3
    gj, gi = np.mgrid[j0:j1, i0:i1]
    px, py = cell_point(gj, gi)
    keep = np.zeros(gj.shape, bool)
    for a in range(gj.shape[0]):
        for b in range(gj.shape[1]):
            x, y = px[a, b], py[a, b]
            lx, ly = int(x - x0), int(y - y0)
            if not (0 <= lx < w and 0 <= ly < h) or edge[ly, lx] < rim + SPACING * 0.3:
                continue
            keep[a, b] = cell_hash(np.int64(gj[a, b]), np.int64(gi[a, b]), 3) < keep_chance(x, y)
    color = (cell_hash(gj, gi, 4) * len(STONES)).astype(int) % len(STONES)

    yy, xx = np.mgrid[0:h, 0:w]
    gx, gy = xx + x0, yy + y0
    cj = gy // SPACING
    ci = (gx - (cj % 2) * SPACING // 2) // SPACING
    best = np.full((h, w), 1e9, np.float32)
    second = np.full((h, w), 1e9, np.float32)
    owner = np.zeros((h, w, 2), np.int32)
    for dj in (-1, 0, 1):
        for di in (-2, -1, 0, 1, 2):
            j = cj + dj
            i = ci + di
            qx, qy = cell_point(j, i)
            d = np.hypot(gx - qx, gy - qy).astype(np.float32)
            closer = d < best
            second = np.where(closer, best, np.minimum(second, d))
            owner[..., 0] = np.where(closer, j - j0, owner[..., 0])
            owner[..., 1] = np.where(closer, i - i0, owner[..., 1])
            best = np.where(closer, d, best)
    border = (second - best) * 0.5
    oj = np.clip(owner[..., 0], 0, keep.shape[0] - 1)
    oi = np.clip(owner[..., 1], 0, keep.shape[1] - 1)

    stone = keep[oj, oi] & (border > GAP) & (edge > rim * 0.6)
    round_kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (19, 19))
    stone = cv2.morphologyEx(stone.astype(np.uint8), cv2.MORPH_OPEN, round_kernel).astype(bool)
    inner = cv2.erode(stone.astype(np.uint8), cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * LINE + 1, 2 * LINE + 1))).astype(bool)
    line = stone & ~inner
    ox = (gx - px[oj, oi]) / SPACING
    oy = (gy - py[oj, oi]) / SPACING
    light = np.clip(1.0 - 0.18 * (ox + oy) + 0.05 * np.clip(border / (SPACING * 0.3), 0, 1), 0.82, 1.08)
    base = np.array(STONES, np.float32)[color[oj, oi]]
    fill = np.clip(base * light[..., None], 0, 255).astype(np.uint8)

    out = np.zeros((h, w, 4), np.uint8)
    out[stone, :3] = fill[stone]
    out[stone, 3] = 255
    out[line, :3] = OUTLINE
    out[..., 3] = cv2.GaussianBlur(out[..., 3], (3, 3), 0)
    return out, crop


def placement(full, box):
    W, H = full.size
    x0, y0, x1, y1 = box
    return {'u': (x0 + x1) / 2 / W, 'v': (y0 + y1) / 2 / H, 'width': (x1 - x0) / W, 'height': (y1 - y0) / H}


def icon(out, crop, name):
    h, w = out.shape[:2]
    c = min(h, w) // 2
    r = min(260, c)
    cx, cy = w // 2, h // 2
    ground = Image.fromarray(crop[cy - r:cy + r, cx - r:cx + r]).convert('RGBA')
    ground.alpha_composite(Image.fromarray(out[cy - r:cy + r, cx - r:cx + r], 'RGBA'))
    size = ground.size[0]
    mask = Image.new('L', ground.size, 0)
    ImageDraw.Draw(mask).ellipse((10, 10, size - 10, size - 10), fill=255)
    result = Image.new('RGBA', ground.size, (0, 0, 0, 0))
    result.paste(ground, (0, 0), mask)
    ImageDraw.Draw(result).ellipse((10, 10, size - 10, size - 10), outline=(75, 46, 34, 255), width=16)
    result.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, name + '.png'))


def center_chance(x, y):
    d = math.hypot(x - CENTER[0], y - CENTER[1])
    if d > OUTER:
        return 0.0
    return 1.0 if d < INNER else (1 - (d - INNER) / (OUTER - INNER)) ** 1.6


def road_chance(dist):
    h, w = dist.shape

    def chance(x, y):
        gx, gy = int(x // GEO_SCALE), int(y // GEO_SCALE)
        if not (0 <= gx < w and 0 <= gy < h) or dist[gy, gx] < 0:
            return 0.0
        t = dist[gy, gx] / ROAD_REACH
        return 1.0 if t < 1 - ROAD_FADE else max(0.0, (1 - t) / ROAD_FADE) ** 1.4
    return chance


def main():
    full = Image.open(MAP).convert('RGB')
    os.makedirs(OUT, exist_ok=True)
    result = {}

    # 가운데 마당
    box = (CENTER[0] - OUTER - SPACING, CENTER[1] - OUTER - SPACING, CENTER[0] + OUTER + SPACING, CENTER[1] + OUTER + SPACING)
    out, crop = render(full, box, center_chance)
    Image.fromarray(out, 'RGBA').save(os.path.join(OUT, 'Paving_PlazaCenter.png'))
    icon(out, crop, 'ICON_Paving')
    result['PlazaCenter'] = placement(full, box)

    # 큰길: 닿는 곳의 범위를 4096 넘지 않는 조각으로 나눔
    dist = geodesic(full)
    ys, xs = np.nonzero(dist >= 0)
    pad = SPACING * 2
    x0, x1 = int(xs.min() * GEO_SCALE - pad), int((xs.max() + 1) * GEO_SCALE + pad)
    y0, y1 = int(ys.min() * GEO_SCALE - pad), int((ys.max() + 1) * GEO_SCALE + pad)
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(full.width, x1), min(full.height, y1)
    cols = math.ceil((x1 - x0) / ROAD_PIECE)
    rows = math.ceil((y1 - y0) / ROAD_PIECE)
    chance = road_chance(dist)
    for r in range(rows):
        for c in range(cols):
            bx0 = x0 + (x1 - x0) * c // cols
            bx1 = x0 + (x1 - x0) * (c + 1) // cols
            by0 = y0 + (y1 - y0) * r // rows
            by1 = y0 + (y1 - y0) * (r + 1) // rows
            box = (bx0, by0, bx1, by1)
            out, crop = render(full, box, chance, ROAD_RIM)
            if out[..., 3].max() == 0:
                continue
            # 빈 곳은 잘라 냄 (텍스처를 작게)
            ys_, xs_ = np.nonzero(out[..., 3])
            l, t, rr, b = xs_.min() - 4, ys_.min() - 4, xs_.max() + 5, ys_.max() + 5
            l, t = max(0, l), max(0, t)
            out = out[t:b, l:rr]
            box = (bx0 + l, by0 + t, bx0 + l + out.shape[1], by0 + t + out.shape[0])
            name = f'Road_{r}_{c}'
            Image.fromarray(out, 'RGBA').save(os.path.join(OUT, f'Paving_{name}.png'))
            result[name] = placement(full, box)
    # 아이콘: 동쪽 길 한 부분
    box = (CENTER[0] + OUTER - 200, CENTER[1] - 520, CENTER[0] + OUTER + 320, CENTER[1])
    out, crop = render(full, box, chance, ROAD_RIM)
    icon(out, crop, 'ICON_PavingRoad')

    # 그림 자리 (IndustrySetup이 읽음)
    with open(os.path.join(os.path.dirname(__file__), 'paving_placement.json'), 'w', encoding='utf-8') as f:
        json.dump(result, f, indent=1)
    print(json.dumps(result, indent=1))


if __name__ == '__main__':
    main()
