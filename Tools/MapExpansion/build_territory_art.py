"""영토 확장(서쪽·북쪽 숲 개간) 그림과 배치 데이터를 만든다.

python Tools/MapExpansion/build_territory_art.py            # 전부 (업스케일은 캐시가 있으면 다시 안 함)
python Tools/MapExpansion/build_territory_art.py --no-upscale  # Real-ESRGAN 없이 Lanczos로 (빠름, 흐림)

입력
  Tools/MapExpansion/source/plaza_world_map.png  사용자가 준 전체 지도 (오른쪽 아래 = 지금 광장)
  Assets/Art/Plaza/Props/Prop_Tree.png · Prop_Bush.png · Prop_Stump.png  우리 그림체의 나무·덤불·그루터기

출력
  Assets/Art/Plaza/Plaza_WorldMap.jpg        전체 지도 (기존 바닥과 같은 PPU 40, 1px = 기존 바닥 1px. PNG는 22MB라 JPG)
  Assets/Art/Plaza/Territory/*.png           숲 나무(참나무 2 · 전나무 2) · 덤불 · 그루터기 · 그늘 · 점선 원 · 도끼 말풍선
  Assets/Art/Plaza/territory_layout.json     칸 경계 · 칸별 걷기 다각형 · 숲 묶음(나무·덤불·그루터기·그늘) · 개간 자리 · 이웃집 자리
  Tools/MapExpansion/preview_*.png           확인용 미리보기

좌표는 모두 기존 바닥 그림(Plaza_Ground.png, 1024x1536) 픽셀 = plaza_layout.json과 같은 기준 (왼쪽 위 원점, 아래로 +y).
서쪽·북쪽 땅은 음수 좌표다. 지도 픽셀 = 기존 바닥 픽셀 + (MAP_OX, MAP_OY).
"""
import argparse
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

try:
    import cv2
except ImportError:  # pip install opencv-python-headless
    sys.exit("opencv-python-headless가 필요합니다: python -m pip install opencv-python-headless")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "source", "plaza_world_map.png")
CACHE = os.path.join(HERE, "cache")
PROPS = os.path.join(ROOT, "Assets", "Art", "Plaza", "Props")
OUT_MAP = os.path.join(ROOT, "Assets", "Art", "Plaza", "Plaza_WorldMap.jpg")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Plaza", "Territory")
OUT_LAYOUT = os.path.join(ROOT, "Assets", "Art", "Plaza", "territory_layout.json")

# 새 지도에서 기존 바닥이 놓인 자리 (랜드마크 8곳 최소제곱: 조개 무늬·해안 바위·길 갈림목). 새 지도 px = U0 + 기존 px * S
S = 0.3146
U0 = 952.97
V0 = 667.72

# 지도 그림 = 기존 바닥 px 공간. 기존 바닥 (0,0)이 지도의 (MAP_OX, MAP_OY)
MAP_OX = 3029
MAP_OY = 2122
MAP_W = 4072  # 오른쪽 끝 = 기존 x 1043 (원본이 x 957에서 끝나 오른쪽 28px를 거울처럼 이어 붙임)
MAP_H = 3984  # 아래 끝 = 기존 y 1862 (바다)
RIGHT = MAP_W - MAP_OX
BOTTOM = MAP_H - MAP_OY

# 칸 경계: 열 0 = 지금 광장 (x 0~1043), 열 1~3 = 서쪽 1~3단계 / 행 0 = 지금 광장 (y 0~1862), 행 1~2 = 북쪽 1~2단계
COL_X = [0, -1010, -2020, -MAP_OX]
ROW_Y = [0, -1061, -MAP_OY]
START_WALK_TOP = 30   # 지금 걷기 영역의 위·왼쪽 끝 (plaza_layout.json walkable)

# 배치 수치 (기존 px)
WALK_FROM_SEA = 32    # 바다에서 이만큼 떨어져야 걸을 수 있음
TREE_FROM_SEA = 76
CELL_OVERLAP = 44     # 칸 걷기 다각형이 옆 칸으로 겹치는 폭 (합쳐서 이어지게)
MAP_MARGIN = 30
TREE_SPACING = 116
BUSH_SPACING = 74
SHADE_SCALE = 5       # 그늘 그림 1px = 기존 5px
SEED = 20261004

# 우리 나무 그림 팔레트 (Prop_Tree.png에서 뽑음)
OUTLINE = (52, 30, 14)
G_DEEP = (14, 92, 72)
G_DARK = (28, 106, 66)
G_BASE = (44, 124, 58)
G_MID = (84, 152, 44)
G_LIGHT = (128, 182, 42)
G_BRIGHT = (176, 206, 42)
G_SHINE = (222, 226, 64)
TRUNK = (196, 124, 70)
TRUNK_DARK = (140, 78, 52)


# ---------------------------------------------------------------------------
# 지도

def upscale_source(use_esrgan):
    os.makedirs(CACHE, exist_ok=True)
    cached = os.path.join(CACHE, "plaza_world_map_x4.png")
    src = Image.open(SOURCE).convert("RGB")
    if use_esrgan and os.path.exists(cached) and os.path.getmtime(cached) >= os.path.getmtime(SOURCE):
        return Image.open(cached).convert("RGB"), 4
    if use_esrgan:
        try:
            from realesrgan_ncnn_py import Realesrgan
            print("Real-ESRGAN (x4plus-anime) 업스케일 중...")
            up = Realesrgan(gpuid=0, model=3).process_pil(src)
            up.save(cached)
            return up, 4
        except Exception as e:  # GPU/패키지가 없으면 Lanczos
            print(f"Real-ESRGAN을 쓸 수 없어 Lanczos로 키웁니다 ({e}). python -m pip install realesrgan-ncnn-py")
    return src.resize((src.width * 4, src.height * 4), Image.LANCZOS), 4


def build_map(use_esrgan):
    up, k = upscale_source(use_esrgan)
    need_w = math.ceil((U0 + RIGHT * S) * k) + 8
    if up.width < need_w:
        # 오른쪽 끝을 거울처럼 이어 붙임 (원본이 지금 광장 오른쪽 끝보다 조금 짧음)
        pad = need_w - up.width
        strip = up.crop((up.width - pad, 0, up.width, up.height)).transpose(Image.FLIP_LEFT_RIGHT)
        padded = Image.new("RGB", (need_w, up.height))
        padded.paste(up, (0, 0))
        padded.paste(strip, (up.width, 0))
        up = padded
    a = k * S
    c = k * (U0 - MAP_OX * S)
    f = k * (V0 - MAP_OY * S)
    world = up.transform((MAP_W, MAP_H), Image.AFFINE, (a, 0, c, 0, a, f), Image.BICUBIC)
    world.save(OUT_MAP, quality=92)
    print(f"지도 {MAP_W}x{MAP_H} -> {os.path.relpath(OUT_MAP, ROOT)} ({os.path.getsize(OUT_MAP) / 1e6:.1f}MB)")
    return world


def sea_mask(world):
    """지도 1/4 해상도의 바다 마스크 (True = 바다). 물속 바위·물거품까지 바다로"""
    small = world.resize((MAP_W // 4, MAP_H // 4), Image.BILINEAR)
    hsv = np.asarray(small.convert("HSV")).astype(np.int32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    sea = ((h > 110) & (h < 150) & (s > 60) & (v > 90)).astype(np.uint8)
    sea = cv2.morphologyEx(sea, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (25, 25)))
    # 아래쪽 바다와 이어진 것만
    n, labels = cv2.connectedComponents(sea)
    keep = set(labels[-1, :].tolist()) - {0}
    sea = np.isin(labels, list(keep)).astype(np.uint8)
    sea = cv2.dilate(sea, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    return sea.astype(bool)


def erode_land(sea, px):
    """바다에서 px(기존 px) 이상 떨어진 땅 (1/4 해상도)"""
    land = (~sea).astype(np.uint8)
    r = max(1, int(round(px / 4)))
    return cv2.erode(land, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))).astype(bool)


def mask_at(mask, x, y):
    mx = int((x + MAP_OX) // 4)
    my = int((y + MAP_OY) // 4)
    if mx < 0 or my < 0 or mx >= mask.shape[1] or my >= mask.shape[0]:
        return False
    return bool(mask[my, mx])


# ---------------------------------------------------------------------------
# 칸

def col_range(i):
    return (COL_X[0], RIGHT) if i == 0 else (COL_X[i], COL_X[i - 1])


def row_range(j):
    return (ROW_Y[0], BOTTOM) if j == 0 else (ROW_Y[j], ROW_Y[j - 1])


def cell_of(x, y):
    i = 0
    if x < 0:
        i = next(c for c in range(1, len(COL_X)) if x >= COL_X[c]) if x >= COL_X[-1] else len(COL_X) - 1
    j = 0
    if y < 0:
        j = next(r for r in range(1, len(ROW_Y)) if y >= ROW_Y[r]) if y >= ROW_Y[-1] else len(ROW_Y) - 1
    return i, j


def group_of(x, y):
    """숲 묶음 ID: 서쪽 칸 W{단계}_{조각}, 북쪽 칸 N{단계}_{조각} (조각 1 = 광장 쪽), 모서리 C{열}_{행}"""
    i, j = cell_of(x, y)
    if i == 0 and j == 0:
        return None
    if j == 0:
        x0, x1 = col_range(i)
        k = 1 + min(2, int((x1 - x) / ((x1 - x0) / 3)))
        return f"W{i}_{k}"
    if i == 0:
        y0, y1 = row_range(j)
        k = 1 + min(2, int((y1 - y) / ((y1 - y0) / 3)))
        return f"N{j}_{k}"
    return f"C{i}_{j}"


def group_rect(gid):
    if gid[0] == "W":
        i, k = int(gid[1]), int(gid[3])
        x0, x1 = col_range(i)
        w = (x1 - x0) / 3
        return (x1 - k * w, 0, x1 - (k - 1) * w, BOTTOM)
    if gid[0] == "N":
        j, k = int(gid[1]), int(gid[3])
        y0, y1 = row_range(j)
        h = (y1 - y0) / 3
        return (0, y1 - k * h, RIGHT, y1 - (k - 1) * h)
    i, j = int(gid[1]), int(gid[3])
    x0, x1 = col_range(i)
    y0, y1 = row_range(j)
    return (x0, y0, x1, y1)


def all_groups():
    ids = []
    for i in range(1, len(COL_X)):
        ids += [f"W{i}_{k}" for k in (1, 2, 3)]
    for j in range(1, len(ROW_Y)):
        ids += [f"N{j}_{k}" for k in (1, 2, 3)]
    for i in range(1, len(COL_X)):
        for j in range(1, len(ROW_Y)):
            ids.append(f"C{i}_{j}")
    return ids


def cell_polygons(walk, i, j):
    """그 칸의 걷기 다각형들 (옆 칸으로 CELL_OVERLAP만큼 겹침, 지도 가장자리는 MAP_MARGIN 안쪽)"""
    x0, x1 = col_range(i)
    y0, y1 = row_range(j)
    x0 = max(x0 - CELL_OVERLAP, -MAP_OX + MAP_MARGIN)
    y0 = max(y0 - CELL_OVERLAP, -MAP_OY + MAP_MARGIN)
    x1 = min(x1 + CELL_OVERLAP, RIGHT - MAP_MARGIN)
    y1 = min(y1 + CELL_OVERLAP, BOTTOM - MAP_MARGIN)
    # 지금 광장과 이어지는 쪽은 지금 걷기 영역 안쪽까지만 (광장 소품 자리로 새 땅이 파고들지 않게)
    if i == 0:
        x0, x1 = max(x0, START_WALK_TOP), min(x1, 994)
    if j == 0:
        y0 = max(y0, START_WALK_TOP)
    m = np.zeros_like(walk, dtype=np.uint8)
    mx0, my0 = int((x0 + MAP_OX) // 4), int((y0 + MAP_OY) // 4)
    mx1, my1 = int((x1 + MAP_OX) // 4), int((y1 + MAP_OY) // 4)
    m[my0:my1, mx0:mx1] = walk[my0:my1, mx0:mx1]
    contours, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    polys = []
    for c in contours:
        if cv2.contourArea(c) < 400:
            continue
        approx = cv2.approxPolyDP(c, 1.5, True).reshape(-1, 2)
        flat = []
        for px, py in approx:
            flat += [int(px * 4 - MAP_OX), int(py * 4 - MAP_OY)]
        polys.append({"points": flat})
    return polys


# ---------------------------------------------------------------------------
# 숲 그림

def load_prop(name):
    return Image.open(os.path.join(PROPS, f"Prop_{name}.png")).convert("RGBA")


def recolor_greens(img, hue_shift, sat, val):
    """초록 잎만 색을 옮김 (외곽선·줄기·꽃은 그대로)"""
    rgba = np.asarray(img).copy()
    hsv = np.asarray(img.convert("RGB").convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    leaf = (h > 30) & (h < 120) & (s > 80) & (v > 60)
    h2 = np.where(leaf, (h + hue_shift) % 256, h)
    s2 = np.where(leaf, np.clip(s * sat, 0, 255), s)
    v2 = np.where(leaf, np.clip(v * val, 0, 255), v)
    rgb = Image.fromarray(np.stack([h2, s2, v2], -1).astype(np.uint8), "HSV").convert("RGB")
    rgba[..., :3] = np.asarray(rgb)
    return Image.fromarray(rgba, "RGBA")


def draw_pine(deep):
    """전나무: 겹친 세 층의 잎(아래가 물결 모양) + 짧은 줄기. 참나무와 같은 굵은 갈색 외곽선 · 셀 음영"""
    k = 3  # 크게 그려 줄임 (부드러운 외곽선)
    W, H = 280 * k, 440 * k
    cx = W // 2
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ol = 7 * k
    pal = [G_DEEP, G_DARK, G_BASE, G_MID, G_LIGHT, G_BRIGHT] if not deep else \
        [(10, 70, 64), G_DEEP, G_DARK, G_BASE, G_MID, G_LIGHT]

    # 줄기
    tw, ty0, ty1 = 26 * k, 330 * k, 424 * k
    d.rounded_rectangle([cx - tw - ol, ty0 - ol, cx + tw + ol, ty1 + ol], radius=10 * k, fill=OUTLINE)
    d.rounded_rectangle([cx - tw, ty0, cx + tw, ty1], radius=8 * k, fill=TRUNK)
    d.rectangle([cx + tw // 3, ty0, cx + tw - 2 * k, ty1 - 6 * k], fill=TRUNK_DARK)
    # 뿌리 쪽 풀 (참나무 아래 풀잎처럼)
    for gx, gs in ((-60, 1.0), (52, 0.85)):
        gxc = cx + gx * k
        for dx, r in ((-14, 15), (0, 19), (14, 15)):
            rr = int(r * gs * k)
            d.ellipse([gxc + dx * k - rr - ol // 2, ty1 - rr * 2 - ol // 2, gxc + dx * k + rr + ol // 2, ty1 + ol // 2], fill=OUTLINE)
        for dx, r in ((-14, 15), (0, 19), (14, 15)):
            rr = int(r * gs * k)
            d.ellipse([gxc + dx * k - rr, ty1 - rr * 2, gxc + dx * k + rr, ty1], fill=G_MID)

    tiers = [  # (꼭대기 y, 아래 y, 반폭)
        (150, 352, 130),
        (78, 262, 104),
        (8, 168, 74),
    ]
    for top, bottom, half in tiers:
        top, bottom, half = top * k, bottom * k, half * k
        bumps = 5 if half > 90 * k else 4
        r = half / bumps
        pts = [(cx, top)]
        pts.append((cx + half, bottom - r * 0.6))
        for b in range(bumps):
            x = cx + half - (2 * b + 1) * r
            pts.append((x, bottom))
        pts.append((cx - half, bottom - r * 0.6))

        def shape(dd, grow, fill, dx=0, dy=0):
            p = [(x + dx, y + dy) for x, y in pts]
            # 꼭대기는 둥글게, 아래는 물결
            dd.polygon(p, fill=fill)
            for b in range(bumps):
                x = cx + half - (2 * b + 1) * r + dx
                dd.ellipse([x - r - grow, bottom - r * 1.2 - grow + dy, x + r + grow, bottom + r * 0.55 + grow + dy], fill=fill)
            dd.ellipse([cx - 16 * k - grow + dx, top - 6 * k - grow + dy, cx + 16 * k + grow + dx, top + 26 * k + grow + dy], fill=fill)
            if grow:
                dd.line(p + [p[0]], fill=fill, width=int(grow * 2))

        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ld = ImageDraw.Draw(layer)
        shape(ld, ol, OUTLINE)
        mask = Image.new("L", (W, H), 0)
        shape(ImageDraw.Draw(mask), 0, 255)
        fill = Image.new("RGBA", (W, H), pal[1] + (255,))
        fd = ImageDraw.Draw(fill)
        # 밝은 쪽(오른쪽 위)으로 갈수록 밝게: 마스크를 옮겨 겹친 곳만 칠함
        for idx, (dx, dy) in enumerate(((-10, -14), (-22, -30), (-34, -48), (-44, -64))):
            m2 = Image.new("L", (W, H), 0)
            shape(ImageDraw.Draw(m2), 0, 255, dx * k * 0.6, dy * k * 0.6)
            sub = Image.new("RGBA", (W, H), pal[min(len(pal) - 1, idx + 2)] + (255,))
            fill.paste(sub, (0, 0), m2)
        # 잎 뭉치 하이라이트 (참나무의 둥근 잎 덩어리처럼)
        rnd = random.Random(top + half)
        for _ in range(3 if half > 90 * k else 2):
            hx = cx + rnd.uniform(-0.25, 0.45) * half
            hy = top + (bottom - top) * rnd.uniform(0.35, 0.65)
            hr = rnd.uniform(14, 20) * k
            fd.ellipse([hx - hr, hy - hr * 0.8, hx + hr, hy + hr * 0.8], fill=pal[-1] + (255,))
            fd.ellipse([hx - hr * 0.45, hy - hr * 0.5, hx + hr * 0.25, hy], fill=G_SHINE + (255,))
        # 아래 물결 그늘
        shade = Image.new("L", (W, H), 0)
        shape(ImageDraw.Draw(shade), 0, 255)
        lift = Image.new("L", (W, H), 0)
        shape(ImageDraw.Draw(lift), 0, 255, 0, -14 * k)
        shade = Image.fromarray(np.where(np.asarray(lift) > 0, 0, np.asarray(shade)).astype(np.uint8))
        fill.paste(Image.new("RGBA", (W, H), pal[0] + (255,)), (0, 0), shade)
        layer.paste(fill, (0, 0), mask)
        img = Image.alpha_composite(img, layer)
        d = ImageDraw.Draw(img)

    out = img.resize((W // k, H // k), Image.LANCZOS)
    return out.crop(out.getbbox())


def dotted_ring():
    k = 4
    size = 320 * k
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2
    R = 140 * k
    n = 22
    for i in range(n):
        a0 = 2 * math.pi * i / n
        a1 = a0 + 2 * math.pi / n * 0.55
        for width, col in ((22 * k, (74, 46, 34, 150)), (12 * k, (255, 246, 228, 255))):
            pts = [(c + R * math.cos(a0 + (a1 - a0) * t / 12), c + R * math.sin(a0 + (a1 - a0) * t / 12)) for t in range(13)]
            d.line(pts, fill=col, width=width, joint="curve")
            for p in (pts[0], pts[-1]):
                d.ellipse([p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2], fill=col)
    # 안쪽 은은한 빛
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([c - R * 0.9, c - R * 0.9, c + R * 0.9, c + R * 0.9], fill=(255, 244, 200, 60))
    glow = glow.filter(ImageFilter.GaussianBlur(30 * k))
    img = Image.alpha_composite(glow, img)
    return img.resize((size // k, size // k), Image.LANCZOS)


def axe_bubble():
    """도끼 말풍선 (UI_Bubble_Hammer와 같은 틀·색)"""
    k = 4
    W, H = 120 * k, 128 * k
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    line = (75, 46, 34, 255)
    paper = (255, 244, 230, 255)
    d.rounded_rectangle([10 * k, 8 * k, 110 * k, 98 * k], radius=24 * k, fill=line)
    d.polygon([(46 * k, 94 * k), (74 * k, 94 * k), (60 * k, 124 * k)], fill=line)
    d.rounded_rectangle([16 * k, 14 * k, 104 * k, 92 * k], radius=19 * k, fill=paper)
    d.polygon([(51 * k, 90 * k), (69 * k, 90 * k), (60 * k, 112 * k)], fill=paper)
    # 자루 (왼쪽 아래 → 오른쪽 위)
    hx0, hy0, hx1, hy1 = 34 * k, 82 * k, 78 * k, 26 * k
    d.line([(hx0, hy0), (hx1, hy1)], fill=line, width=17 * k)
    d.line([(hx0, hy0), (hx1, hy1)], fill=(196, 138, 88, 255), width=9 * k)
    d.line([(hx0 + 2 * k, hy0 - 4 * k), (hx1 - 2 * k, hy1 + 2 * k)], fill=(225, 170, 112, 255), width=3 * k)
    # 날
    blade = [(70 * k, 22 * k), (92 * k, 30 * k), (96 * k, 50 * k), (84 * k, 58 * k), (72 * k, 44 * k)]
    d.polygon(blade, fill=line)
    inner = [(73 * k, 27 * k), (88 * k, 33 * k), (91 * k, 48 * k), (84 * k, 52 * k), (75 * k, 42 * k)]
    d.polygon(inner, fill=(142, 138, 134, 255))
    d.line([(88 * k, 34 * k), (90 * k, 47 * k)], fill=(255, 255, 242, 255), width=3 * k)
    return img.resize((W // k, H // k), Image.LANCZOS)


def build_sprites():
    os.makedirs(OUT_DIR, exist_ok=True)
    pivots = json.load(open(os.path.join(PROPS, "plaza_props_pivots.json"), encoding="utf-8"))
    piv = {p["name"]: (p["pivotX"], p["pivotY"]) for p in pivots["props"]}
    tree = load_prop("Tree")
    sprites = {
        "Forest_Oak_0": (recolor_greens(tree, -3, 1.0, 0.97), piv["Tree"]),
        "Forest_Oak_1": (recolor_greens(tree, 9, 0.92, 0.82), piv["Tree"]),
        "Forest_Pine_0": (draw_pine(False), None),
        "Forest_Pine_1": (draw_pine(True), None),
        "Forest_Bush": (recolor_greens(load_prop("Bush"), 4, 0.95, 0.9), piv["Bush"]),
        "Forest_Stump": (load_prop("Stump"), piv["Stump"]),
        "Territory_Ring": (dotted_ring(), (0.5, 0.5)),
        "UI_Bubble_Axe": (axe_bubble(), (0.5, 0.0)),
    }
    info = []
    for name, (img, pivot) in sprites.items():
        if pivot is None:  # 전나무: 줄기 아래 가운데
            pivot = (0.5, 0.012)
        img.save(os.path.join(OUT_DIR, f"{name}.png"))
        info.append({"name": name, "width": img.width, "height": img.height, "pivotX": round(pivot[0], 4), "pivotY": round(pivot[1], 4)})
    return info, {k: v[0] for k, v in sprites.items()}


# ---------------------------------------------------------------------------
# 숲 배치

def poisson(rng, x0, y0, x1, y1, r, ok, tries=24):
    """Bridson 포아송 원판 표본 (서로 r 이상 떨어진 점들)"""
    cell = r / math.sqrt(2)
    gw, gh = int((x1 - x0) / cell) + 1, int((y1 - y0) / cell) + 1
    grid = [[-1] * gw for _ in range(gh)]
    pts, active = [], []

    def add(p):
        pts.append(p)
        active.append(p)
        grid[int((p[1] - y0) / cell)][int((p[0] - x0) / cell)] = len(pts) - 1

    for _ in range(4000):
        p = (rng.uniform(x0, x1), rng.uniform(y0, y1))
        if ok(*p):
            add(p)
            break
    while active:
        idx = rng.randrange(len(active))
        base = active[idx]
        found = False
        for _ in range(tries):
            a = rng.uniform(0, 2 * math.pi)
            dist = rng.uniform(r, 2 * r)
            p = (base[0] + dist * math.cos(a), base[1] + dist * math.sin(a))
            if not (x0 <= p[0] < x1 and y0 <= p[1] < y1) or not ok(*p):
                continue
            gx, gy = int((p[0] - x0) / cell), int((p[1] - y0) / cell)
            near = False
            for yy in range(max(0, gy - 2), min(gh, gy + 3)):
                for xx in range(max(0, gx - 2), min(gw, gx + 3)):
                    q = grid[yy][xx]
                    if q >= 0 and (pts[q][0] - p[0]) ** 2 + (pts[q][1] - p[1]) ** 2 < r * r:
                        near = True
                        break
                if near:
                    break
            if not near:
                add(p)
                found = True
                break
        if not found:
            active.pop(idx)
    return pts


def locked(x, y):
    return x < -12 or y < -12


def build_forest(sea):
    rng = random.Random(SEED)
    trees_ok = erode_land(sea, TREE_FROM_SEA)
    bush_ok = erode_land(sea, WALK_FROM_SEA + 6)
    x0, y0, x1, y1 = -MAP_OX + 10, -MAP_OY + 10, RIGHT - 10, BOTTOM - 10
    groups = {gid: {"id": gid, "trees": [], "stumps": []} for gid in all_groups()}

    def tree_ok(x, y):
        return locked(x, y) and mask_at(trees_ok, x, y)

    for x, y in poisson(rng, x0, y0, x1, y1, TREE_SPACING, tree_ok):
        gid = group_of(x, y)
        if gid is None:
            continue
        roll = rng.random()
        sprite = "Forest_Oak_0" if roll < 0.42 else "Forest_Oak_1" if roll < 0.68 else "Forest_Pine_0" if roll < 0.86 else "Forest_Pine_1"
        scale = rng.uniform(0.5, 0.68) if sprite.startswith("Forest_Oak") else rng.uniform(0.62, 0.82)
        groups[gid]["trees"].append({"sprite": sprite, "x": round(x, 1), "y": round(y, 1), "scale": round(scale, 3),
                                     "flip": rng.random() < 0.5, "tint": round(rng.uniform(0.86, 1.0), 3)})

    # 숲 가장자리(바닷가 쪽 · 광장 쪽) 덤불
    def bush_ok_at(x, y):
        if not locked(x, y) or not mask_at(bush_ok, x, y):
            return False
        near_plaza = (-150 < x < 0 and y > -12) or (-150 < y < 0 and x > -12)
        return near_plaza or not mask_at(trees_ok, x, y)

    for x, y in poisson(rng, x0, y0, x1, y1, BUSH_SPACING, bush_ok_at):
        gid = group_of(x, y)
        if gid is None or rng.random() < 0.35:
            continue
        groups[gid]["trees"].append({"sprite": "Forest_Bush", "x": round(x, 1), "y": round(y, 1),
                                     "scale": round(rng.uniform(0.3, 0.42), 3), "flip": rng.random() < 0.5,
                                     "tint": round(rng.uniform(0.9, 1.0), 3)})

    # 개간한 조각에 남는 그루터기
    for gid, g in groups.items():
        if gid[0] == "C":
            continue
        gx0, gy0, gx1, gy1 = group_rect(gid)
        cands = [(t["x"], t["y"]) for t in g["trees"] if t["sprite"] != "Forest_Bush"]
        rng.shuffle(cands)
        chosen = []
        for p in cands:
            if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 230 ** 2 for q in chosen):
                chosen.append(p)
            if len(chosen) >= 7:
                break
        g["stumps"] = [{"sprite": "Forest_Stump", "x": p[0], "y": p[1], "scale": round(rng.uniform(0.26, 0.34), 3),
                        "flip": rng.random() < 0.5, "tint": 1.0} for p in chosen]
    return groups


def build_shades(sea, groups):
    """묶음마다 숲 바닥 그늘 (땅에만, 가장자리는 부드럽게)"""
    land = erode_land(sea, 20).astype(np.float32)
    big = cv2.resize(land, (MAP_W // SHADE_SCALE, MAP_H // SHADE_SCALE), interpolation=cv2.INTER_AREA)
    big = cv2.GaussianBlur(big, (0, 0), 3)
    fade = 46 / SHADE_SCALE
    for gid, g in groups.items():
        rx0, ry0, rx1, ry1 = group_rect(gid)
        pad = 24
        rx0, ry0 = max(rx0 - pad, -MAP_OX), max(ry0 - pad, -MAP_OY)
        rx1, ry1 = min(rx1 + pad, RIGHT), min(ry1 + pad, BOTTOM)
        sx0, sy0 = int((rx0 + MAP_OX) / SHADE_SCALE), int((ry0 + MAP_OY) / SHADE_SCALE)
        sx1, sy1 = int(math.ceil((rx1 + MAP_OX) / SHADE_SCALE)), int(math.ceil((ry1 + MAP_OY) / SHADE_SCALE))
        a = big[sy0:sy1, sx0:sx1].copy()
        h, w = a.shape
        yy, xx = np.mgrid[0:h, 0:w]
        # 지도 가장자리가 아닌 변만 흐리게
        edge = np.ones_like(a)
        if rx0 > -MAP_OX:
            edge *= np.clip(xx / fade, 0, 1)
        if rx1 < RIGHT:
            edge *= np.clip((w - 1 - xx) / fade, 0, 1)
        if ry0 > -MAP_OY:
            edge *= np.clip(yy / fade, 0, 1)
        if ry1 < BOTTOM:
            edge *= np.clip((h - 1 - yy) / fade, 0, 1)
        alpha = (a * edge * 0.5 * 255).astype(np.uint8)
        rgba = np.zeros((h, w, 4), np.uint8)
        rgba[..., 0], rgba[..., 1], rgba[..., 2] = 30, 62, 30
        rgba[..., 3] = alpha
        name = f"Shade_{gid}"
        Image.fromarray(rgba, "RGBA").save(os.path.join(OUT_DIR, f"{name}.png"))
        g["shade"] = {"sprite": name, "x0": sx0 * SHADE_SCALE - MAP_OX, "y0": sy0 * SHADE_SCALE - MAP_OY,
                      "x1": sx1 * SHADE_SCALE - MAP_OX, "y1": sy1 * SHADE_SCALE - MAP_OY}


# ---------------------------------------------------------------------------
# 개간 자리 · 이웃집 자리

def nearest_ok(mask, x, y, limit=160):
    if mask_at(mask, x, y):
        return x, y
    for r in range(8, limit, 8):
        for a in range(0, 360, 20):
            px, py = x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))
            if mask_at(mask, px, py):
                return round(px), round(py)
    return x, y


def build_sites(walk):
    sites = []
    # 서쪽: 광장 왼쪽 길(y 745) 끝. 단계마다 그때의 서쪽 끝 / 북쪽: 광장 위쪽 길(x 760) 끝 (x 300은 큰 나무에 가림)
    for tier in range(1, len(COL_X)):
        x, y = nearest_ok(walk, COL_X[tier - 1] + 96, 745)
        sites.append({"direction": "West", "tier": tier, "x": x, "y": y,
                      "stands": [-18, -46, -18, 44], "look": [-280, 0]})
    for tier in range(1, len(ROW_Y)):
        x, y = nearest_ok(walk, 760, ROW_Y[tier - 1] + 96)
        sites.append({"direction": "North", "tier": tier, "x": x, "y": y,
                      "stands": [-52, -16, 52, -16], "look": [0, -280]})
    return sites


def build_homes(walk):
    # 첫 확장 땅의 새 이웃 집 (화분·빨랫줄은 집 기준으로 같은 자리)
    west = nearest_ok(walk, -520, 560)
    north = nearest_ok(walk, 560, -560)
    return {"west": list(west), "north": list(north), "potOffset": [-100, 28], "lineOffset": [115, 45]}


# ---------------------------------------------------------------------------
# 미리보기

def preview(world, groups, sprites, sites, cells, out, scale=0.25, show_walk=True):
    W, H = int(MAP_W * scale), int(MAP_H * scale)
    img = world.resize((W, H), Image.BILINEAR).convert("RGBA")
    P = lambda x, y: ((x + MAP_OX) * scale, (y + MAP_OY) * scale)
    for g in groups.values():
        sh = g["shade"]
        tile = Image.open(os.path.join(OUT_DIR, sh["sprite"] + ".png"))
        x0, y0 = P(sh["x0"], sh["y0"])
        x1, y1 = P(sh["x1"], sh["y1"])
        img.alpha_composite(tile.resize((max(1, int(x1 - x0)), max(1, int(y1 - y0)))), (int(x0), int(y0)))
    items = [t for g in groups.values() for t in g["trees"]]
    items.sort(key=lambda t: t["y"])
    cache = {}
    for t in items:
        key = (t["sprite"], t["scale"], t["flip"])
        if key not in cache:
            spr = sprites[t["sprite"]]
            w, h = max(1, int(spr.width * t["scale"] * scale)), max(1, int(spr.height * t["scale"] * scale))
            s = spr.resize((w, h), Image.LANCZOS)
            cache[key] = s.transpose(Image.FLIP_LEFT_RIGHT) if t["flip"] else s
        s = cache[key]
        px, py = P(t["x"], t["y"])
        img.alpha_composite(s, (int(px - s.width / 2), int(py - s.height)))
    d = ImageDraw.Draw(img)
    x0, y0 = P(0, 0)
    x1, y1 = P(1024, 1536)
    d.rectangle([x0, y0, x1, y1], outline=(255, 60, 60, 255), width=3)
    if show_walk:
        for c in cells:
            for poly in c["polygons"]:
                pts = [P(poly["points"][k], poly["points"][k + 1]) for k in range(0, len(poly["points"]), 2)]
                d.line(pts + [pts[0]], fill=(60, 255, 90, 255), width=2)
    for s in sites:
        cx, cy = P(s["x"], s["y"])
        d.ellipse([cx - 8, cy - 6, cx + 8, cy + 6], outline=(255, 255, 255, 255), width=3)
    img.convert("RGB").save(out)
    print("미리보기", os.path.relpath(out, ROOT))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-upscale", action="store_true")
    args = ap.parse_args()

    world = build_map(not args.no_upscale)
    sprite_info, sprites = build_sprites()
    sea = sea_mask(world)
    walk = erode_land(sea, WALK_FROM_SEA)

    cells = []
    for i in range(len(COL_X)):
        for j in range(len(ROW_Y)):
            if i == 0 and j == 0:
                continue
            cells.append({"col": i, "row": j, "polygons": cell_polygons(walk, i, j)})
    groups = build_forest(sea)
    build_shades(sea, groups)
    sites = build_sites(walk)
    homes = build_homes(walk)

    layout = {
        "comment": "build_territory_art.py가 만듦. 좌표 = 기존 바닥(Plaza_Ground.png) 픽셀, 왼쪽 위 원점·아래로 +y. 서쪽·북쪽은 음수.",
        "groundPixelsPerUnit": 40,
        "map": {"sprite": "Plaza_WorldMap", "x0": -MAP_OX, "y0": -MAP_OY, "x1": RIGHT, "y1": BOTTOM},
        "start": {"x0": 0, "y0": 0, "x1": RIGHT, "y1": 1536},
        "columns": COL_X,
        "rows": ROW_Y,
        "sprites": sprite_info,
        "cells": cells,
        "groups": list(groups.values()),
        "sites": sites,
        "homes": homes,
    }
    with open(OUT_LAYOUT, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, indent=1)
    n_trees = sum(len(g["trees"]) for g in groups.values())
    print(f"칸 {len(cells)} · 숲 묶음 {len(groups)} · 나무/덤불 {n_trees} · 그루터기 {sum(len(g['stumps']) for g in groups.values())}")
    print("배치 ->", os.path.relpath(OUT_LAYOUT, ROOT))

    preview(world, groups, sprites, sites, cells, os.path.join(HERE, "preview_forest.png"))


if __name__ == "__main__":
    main()
