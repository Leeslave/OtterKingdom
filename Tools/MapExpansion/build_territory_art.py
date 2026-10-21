"""영토(숲 개간) 그림과 배치 데이터를 만든다 — 구역·이사 기획: 중앙 공터에서 시작해 사방의 구역 블록으로 넓힌다.

python Tools/MapExpansion/build_territory_art.py                     # 배치만 (지도 · 나무 그림은 있는 것을 씀)
python Tools/MapExpansion/build_territory_art.py --rebuild-sprites   # 나무 · 덤불 · 점선 원 · 도끼 말풍선 그림도 다시
python Tools/MapExpansion/build_territory_art.py --rebuild-map       # 원본 지도에서 Plaza_WorldMap.jpg도 다시 (--no-upscale: Lanczos)

입력
  Assets/Art/Plaza/Plaza_WorldMap.jpg            전체 지도 (--rebuild-map이면 Tools/MapExpansion/source/plaza_world_map.png에서 만듦)
  Assets/Art/Plaza/Props/Prop_Tree.png · Prop_Bush.png · Prop_Stump.png  우리 그림체의 나무·덤불·그루터기 (--rebuild-sprites)

출력
  Assets/Art/Plaza/Territory/Shade_*.png         숲 묶음별 바닥 그늘
  Assets/Art/Plaza/territory_layout.json         걷기 영역(공터 · 단계 · 바닷가) · 숲 묶음(개간 조각 · 늘 남는 숲) · 개간 자리 ·
                                                 이웃집 자리 · 구역 블록 단계와 부지 칸
  Tools/MapExpansion/preview_forest.png          확인용 미리보기

땅: 시작 = 중앙 공터(START). 방향마다 구역 블록 하나(북동 · 남 · 북서 · 서), 블록은 3단계로 커진다.
단계 = 숲 개간 N번 → 영토 확장 미션 → 그 단계 땅이 열림 (걷기 영역 · 카메라 · 구역 블록의 부지 칸).
남동쪽 옛 시작 광장은 바닷가(낚시터를 찾으면 열림). 블록 사이 · 가장자리 숲은 늘 남는다.

좌표는 모두 기존 바닥 그림(Plaza_Ground.png, 1024x1536) 픽셀 = plaza_layout.json과 같은 기준 (왼쪽 위 원점, 아래로 +y).
지도 픽셀 = 기존 바닥 픽셀 + (MAP_OX, MAP_OY). 칸(세이브 좌표) ↔ 바닥 px: ((x + 0.8) × 40, (33.6 − y) × 40).
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



# ---------------------------------------------------------------------------
# 땅 나누기 (구역·이사 기획: 중앙 공터에서 시작해 사방의 구역 블록으로 넓힘)
#
# 칸 = 광장 꾸미기 격자의 세이브 좌표 (40px, 아래로 -y). (x, y, 가로, 세로)는 왼쪽 아래 칸 기준.
# 바닥 px = ((x + 0.8) * 40, (33.6 - y) * 40)

CELL = 40

# 시작 = 중앙 공터 (늘 열림, 게시판 → 마을회관 → 성)
START = (-42, 38, 26, 20)
# 바닷가 (옛 시작 광장, 선착장 · 낚시터 쪽): 낚시터를 찾으면 열림 (Lv.15)
SHORE = (-1, -4, 26, 37)
SHORE_DEVELOPMENT = "fishing_found"
# 바닷가 길: 공터 남동쪽 모서리에서 지도의 모래길을 따라 바닷가까지 (ㄱ자, 남쪽 블록 · 북동 블록 사이 숲).
# 바닷가와 같이 열림 → 그 숲이 사라지고 걸어서 선착장까지 감. 그루터기 없음
SHORE_PATH = [(-16, 38, 14, 5), (-4, 31, 5, 12)]

# 방향 = 구역 블록 하나. 단계 = 블록이 커지는 단계 (그 단계의 숲을 개간하고 영토 확장 미션을 끝내면 열림)
# 단계: (칸 영역, 숲 개간 수). 1단계가 공터에 가장 가깝다
DIRECTIONS = [
    {"key": "ne", "direction": "NorthEast", "block": "district_northeast",
     "tiers": [((-16, 46, 18, 16), 3), ((2, 46, 18, 16), 2), ((-16, 62, 36, 16), 2)]},
    {"key": "s", "direction": "South", "block": "district_south",
     "tiers": [((-40, 26, 18, 12), 3), ((-22, 26, 18, 12), 2), ((-40, 12, 36, 14), 2)]},
    {"key": "nw", "direction": "NorthWest", "block": "district_northwest",
     "tiers": [((-52, 58, 18, 14), 3), ((-70, 58, 18, 14), 2), ((-70, 72, 36, 14), 2)]},
    {"key": "w", "direction": "West", "block": "district_west",
     "tiers": [((-58, 34, 16, 16), 3), ((-74, 34, 16, 16), 2), ((-74, 18, 32, 16), 2)]},
]
CENTER_BLOCK = "district_center"

# 단계마다 부지 칸 수: (큰 칸 8x5 = 집 · 건물, 작은 칸 4x3 = 의자 · 벤치 · 특성 건물)
SLOT_TARGETS = [(2, 2), (2, 1), (2, 2)]
SLOT_MAX_SAND = 0.2
WILD_TILE = 1024        # 늘 남는 숲(블록 사이 · 가장자리)을 이 크기로 묶음 (그늘 그림 크기)
STUMPS_PER_PATCH = 4


def expansion_id(key, tier):
    return f"territory_{key}_{tier}"


def cell_rect_px(r):
    """칸 영역 → 바닥 px (x0, y0 = 위, x1, y1 = 아래)"""
    x, y, w, h = r
    return ((x + 0.8) * CELL, (33.6 - (y + h)) * CELL, (x + w + 0.8) * CELL, (33.6 - y) * CELL)


def in_rect(rect_px, x, y):
    return rect_px[0] <= x < rect_px[2] and rect_px[1] <= y < rect_px[3]


def rect_center(rect_px):
    return ((rect_px[0] + rect_px[2]) / 2, (rect_px[1] + rect_px[3]) / 2)


def tiers():
    """(방향, 단계 번호, 칸 영역, 개간 수, 앞 단계까지 열린 영역 px)"""
    out = []
    for d in DIRECTIONS:
        prev = cell_rect_px(START)
        for t, (rect, clearings) in enumerate(d["tiers"], start=1):
            out.append((d, t, rect, clearings, prev))
            prev = cell_rect_px(rect)
    return out


def patches(rect, clearings, prev_px):
    """단계 영역을 개간 수만큼 띠로 나눔 (앞 단계 쪽 띠가 1번)"""
    x0, y0, x1, y1 = cell_rect_px(rect)
    pcx, pcy = rect_center(prev_px)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    out = []
    if abs(cx - pcx) >= abs(cy - pcy):
        w = (x1 - x0) / clearings
        for k in range(clearings):
            out.append((x0 + k * w, y0, x0 + (k + 1) * w, y1))
        if cx < pcx:
            out.reverse()
    else:
        h = (y1 - y0) / clearings
        for k in range(clearings):
            out.append((x0, y0 + k * h, x1, y0 + (k + 1) * h))
        if cy < pcy:
            out.reverse()
    return out


def region_polygons(walk, rect_px, pad):
    """영역(+pad) 안의 걷기 다각형들 (바다에서 떨어진 땅만, 지도 가장자리는 MAP_MARGIN 안쪽)"""
    x0 = max(rect_px[0] - pad, -MAP_OX + MAP_MARGIN)
    y0 = max(rect_px[1] - pad, -MAP_OY + MAP_MARGIN)
    x1 = min(rect_px[2] + pad, RIGHT - MAP_MARGIN)
    y1 = min(rect_px[3] + pad, BOTTOM - MAP_MARGIN)
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


def px_rect_json(r):
    return {"x0": round(r[0], 1), "y0": round(r[1], 1), "x1": round(r[2], 1), "y1": round(r[3], 1)}


# ---------------------------------------------------------------------------
# 숲 배치

def group_at(x, y, patch_list):
    """그 자리의 숲 묶음 (공터 · 바닷가는 None)"""
    if in_rect(cell_rect_px(START), x, y) or in_rect(cell_rect_px(SHORE), x, y):
        return None
    for p in patch_list:
        if in_rect(p["rect"], x, y):
            return p["id"]
    i, j = int((x + MAP_OX) // WILD_TILE), int((y + MAP_OY) // WILD_TILE)
    return f"wild_{i}_{j}"


def hits_open(x, y, half_width, height):
    """나무 그림(발밑 x, y에서 위로 height, 좌우 half_width)이 공터 · 바닷가에 걸치는지"""
    for r in (cell_rect_px(START), cell_rect_px(SHORE)):
        if x + half_width > r[0] + 16 and x - half_width < r[2] - 16 and y > r[1] + 16 and y - height < r[3] - 16:
            return True
    return False


def build_forest(sea, patch_list):
    rng = random.Random(SEED)
    trees_ok = erode_land(sea, TREE_FROM_SEA)
    bush_ok = erode_land(sea, WALK_FROM_SEA + 6)
    x0, y0, x1, y1 = -MAP_OX + 10, -MAP_OY + 10, RIGHT - 10, BOTTOM - 10
    groups = {}
    for p in patch_list:
        groups[p["id"]] = {"id": p["id"], "development": p["development"], "stumpsUntil": p["expansion"],
                           "rect": p["rect"], "trees": [], "stumps": []}

    def group(gid):
        if gid not in groups:
            groups[gid] = {"id": gid, "development": "", "stumpsUntil": "", "rect": None, "trees": [], "stumps": []}
        return groups[gid]

    def tree_ok(x, y):
        return group_at(x, y, patch_list) is not None and mask_at(trees_ok, x, y) and not hits_open(x, y, 100, 240)

    for x, y in poisson(rng, x0, y0, x1, y1, TREE_SPACING, tree_ok):
        roll = rng.random()
        sprite = "Forest_Oak_0" if roll < 0.42 else "Forest_Oak_1" if roll < 0.68 else "Forest_Pine_0" if roll < 0.86 else "Forest_Pine_1"
        scale = rng.uniform(0.5, 0.68) if sprite.startswith("Forest_Oak") else rng.uniform(0.62, 0.82)
        group(group_at(x, y, patch_list))["trees"].append({"sprite": sprite, "x": round(x, 1), "y": round(y, 1), "scale": round(scale, 3),
                                                           "flip": rng.random() < 0.5, "tint": round(rng.uniform(0.86, 1.0), 3)})

    # 숲 가장자리(공터 둘레 · 바닷가 쪽) 덤불
    sx0, sy0, sx1, sy1 = cell_rect_px(START)

    def bush_ok_at(x, y):
        gid = group_at(x, y, patch_list)
        if gid is None or not mask_at(bush_ok, x, y) or hits_open(x, y, 55, 100):
            return False
        near_start = sx0 - 150 < x < sx1 + 150 and sy0 - 150 < y < sy1 + 150
        return near_start or not mask_at(trees_ok, x, y)

    for x, y in poisson(rng, x0, y0, x1, y1, BUSH_SPACING, bush_ok_at):
        if rng.random() < 0.35:
            continue
        group(group_at(x, y, patch_list))["trees"].append({"sprite": "Forest_Bush", "x": round(x, 1), "y": round(y, 1),
                                                           "scale": round(rng.uniform(0.3, 0.42), 3), "flip": rng.random() < 0.5,
                                                           "tint": round(rng.uniform(0.9, 1.0), 3)})

    # 개간한 조각에 남는 그루터기 (그 단계가 열리기 전까지)
    for g in groups.values():
        if not g["development"] or not g["stumpsUntil"]:
            continue
        cands = [(t["x"], t["y"]) for t in g["trees"] if t["sprite"] != "Forest_Bush"]
        rng.shuffle(cands)
        chosen = []
        for p in cands:
            if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 200 ** 2 for q in chosen):
                chosen.append(p)
            if len(chosen) >= STUMPS_PER_PATCH:
                break
        g["stumps"] = [{"sprite": "Forest_Stump", "x": p[0], "y": p[1], "scale": round(rng.uniform(0.26, 0.34), 3),
                        "flip": rng.random() < 0.5, "tint": 1.0} for p in chosen]
    return [g for g in groups.values() if g["trees"]]


def build_shades(sea, groups, patch_list):
    """묶음마다 숲 바닥 그늘 (땅에만, 그 묶음 자리만, 가장자리는 부드럽게)"""
    land = erode_land(sea, 20).astype(np.uint8)
    W, H = MAP_W // SHADE_SCALE, MAP_H // SHADE_SCALE
    land_s = cv2.resize(land, (W, H), interpolation=cv2.INTER_AREA).astype(np.float32)
    # 칸마다 어느 묶음인지 (그늘 해상도): 늘 남는 숲 타일 → 개간 조각 → 공터 · 바닷가는 없음
    ids = {g["id"]: n + 1 for n, g in enumerate(groups)}
    ys, xs = np.mgrid[0:H, 0:W]
    px = xs * SHADE_SCALE - MAP_OX + SHADE_SCALE / 2
    py = ys * SHADE_SCALE - MAP_OY + SHADE_SCALE / 2
    owner = np.zeros((H, W), np.int32)
    ti, tj = ((px + MAP_OX) // WILD_TILE).astype(int), ((py + MAP_OY) // WILD_TILE).astype(int)
    for gid, n in ids.items():
        if gid.startswith("wild_"):
            i, j = map(int, gid[5:].split("_"))
            owner[(ti == i) & (tj == j)] = n

    def fill(rect_px, value):
        sel = (px >= rect_px[0]) & (px < rect_px[2]) & (py >= rect_px[1]) & (py < rect_px[3])
        owner[sel] = value

    for p in patch_list:
        fill(p["rect"], ids.get(p["id"], 0))
    fill(cell_rect_px(START), 0)
    fill(cell_rect_px(SHORE), 0)
    for g in groups:
        mask = ((owner == ids[g["id"]]).astype(np.float32) * land_s)
        rows = np.where(mask.max(axis=1) > 0)[0]
        cols = np.where(mask.max(axis=0) > 0)[0]
        if len(rows) == 0:
            continue
        pad = 10
        sy0, sy1 = max(0, rows[0] - pad), min(H, rows[-1] + pad + 1)
        sx0, sx1 = max(0, cols[0] - pad), min(W, cols[-1] + pad + 1)
        a = cv2.GaussianBlur(mask, (0, 0), 4)[sy0:sy1, sx0:sx1]
        alpha = (np.clip(a, 0, 1) * 0.5 * 255).astype(np.uint8)
        rgba = np.zeros((sy1 - sy0, sx1 - sx0, 4), np.uint8)
        rgba[..., 0], rgba[..., 1], rgba[..., 2] = 30, 62, 30
        rgba[..., 3] = alpha
        name = f"Shade_{g['id']}"
        Image.fromarray(rgba, "RGBA").save(os.path.join(OUT_DIR, f"{name}.png"))
        g["shade"] = {"sprite": name, "x0": int(sx0) * SHADE_SCALE - MAP_OX, "y0": int(sy0) * SHADE_SCALE - MAP_OY,
                      "x1": int(sx1) * SHADE_SCALE - MAP_OX, "y1": int(sy1) * SHADE_SCALE - MAP_OY}


# ---------------------------------------------------------------------------
# 개간 자리 · 이웃집 자리

def nearest_ok(mask, x, y, limit=200):
    if mask_at(mask, x, y):
        return round(x), round(y)
    for r in range(8, limit, 8):
        for a in range(0, 360, 20):
            px, py = x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))
            if mask_at(mask, px, py):
                return round(px), round(py)
    return round(x), round(y)


def build_sites(walk):
    """방향 · 단계마다 점선 원 자리: 앞 단계까지 열린 땅의 가장자리에서 새 땅 쪽"""
    sites = []
    for d, t, rect, clearings, prev in tiers():
        tx0, ty0, tx1, ty1 = cell_rect_px(rect)
        tcx, tcy = (tx0 + tx1) / 2, (ty0 + ty1) / 2
        # 앞 땅 안에서 새 땅 가운데에 가장 가까운 점 → 안쪽으로 90px
        nx = min(max(tcx, prev[0] + 90), prev[2] - 90)
        ny = min(max(tcy, prev[1] + 90), prev[3] - 90)
        x, y = nearest_ok(walk, nx, ny)
        dx, dy = tcx - x, tcy - y
        n = math.hypot(dx, dy) or 1
        dx, dy = dx / n, dy / n
        # 보낸 해달: 숲 쪽으로 조금, 옆으로 ±48
        stands = [round(dx * 16 - dy * 48), round(dy * 16 + dx * 48), round(dx * 16 + dy * 48), round(dy * 16 - dx * 48)]
        sites.append({"direction": d["direction"], "tier": t, "x": x, "y": y, "stands": stands,
                      "look": [round(dx * 280), round(dy * 280)]})
    return sites


def build_homes(walk):
    # 처음 넓힌 쪽 1단계 땅의 새 이웃 집 (화분 · 빨랫줄은 집 기준으로 같은 자리)
    homes = []
    for d in DIRECTIONS:
        x0, y0, x1, y1 = cell_rect_px(d["tiers"][0][0])
        x, y = nearest_ok(walk, (x0 + x1) / 2, (y0 + y1) / 2 + 60)
        homes.append({"direction": d["direction"], "development": f"territory_home_{d['key']}", "x": x, "y": y})
    return {"list": homes, "potOffset": [-100, 28], "lineOffset": [115, 45]}


# ---------------------------------------------------------------------------
# 구역 블록 단계 · 부지 칸 (모래길을 피해 풀밭에 줄 맞춤)

def sand_cells(world):
    """칸(세이브 좌표)마다 모래 · 바다 비율 → dict[(x, y)] = 모래 + 바다 × 5"""
    small = np.asarray(world.resize((MAP_W // 4, MAP_H // 4), Image.BILINEAR)).astype(np.int32)
    r, g, b = small[..., 0], small[..., 1], small[..., 2]
    sea = (b > g) & (b > r)
    sand = (r > 190) & (b > 100) & (r > g + 10) & ~sea
    out = {}
    for x in range(-77, 26):
        for y in range(-13, 87):
            x0, y0, x1, y1 = cell_rect_px((x, y, 1, 1))
            mx0, my0 = int((x0 + MAP_OX) // 4), int((y0 + MAP_OY) // 4)
            mx1, my1 = int((x1 + MAP_OX) // 4), int((y1 + MAP_OY) // 4)
            if mx0 < 0 or my0 < 0 or mx1 > sand.shape[1] or my1 > sand.shape[0]:
                out[(x, y)] = 5.0
                continue
            out[(x, y)] = float(sand[my0:my1, mx0:mx1].mean()) + float(sea[my0:my1, mx0:mx1].mean()) * 5
    return out


def rect_sand(sand, x, y, w, h):
    total = 0.0
    for i in range(x, x + w):
        for j in range(y, y + h):
            total += sand.get((i, j), 5.0)
    return total / (w * h)


def overlaps(a, b, gap=0):
    return a[0] - gap < b[0] + b[2] and b[0] - gap < a[0] + a[2] and a[1] - gap < b[1] + b[3] and b[1] - gap < a[1] + a[3]


def place_slots(sand, rect, large, small, taken):
    """단계 영역 안: 큰 칸은 가로 9 · 세로 6 간격 격자 (칸 사이 1칸 길), 작은 칸은 남은 빈 곳"""
    rx, ry, rw, rh = rect
    cx, cy = rx + rw / 2, ry + rh / 2

    def dist(x, y):
        return math.hypot((x - cx) / rw, (y - cy) / rh)

    best, best_score = [], float("inf")
    for ox in range(9):
        for oy in range(6):
            cands = []
            for x in range(rx + ox, rx + rw - 8 + 1, 9):
                for y in range(ry + oy, ry + rh - 5 + 1, 6):
                    s = rect_sand(sand, x, y, 8, 5)
                    if s <= SLOT_MAX_SAND and not any(overlaps((x, y, 8, 5), t, 1) for t in taken):
                        cands.append((s + dist(x + 4, y + 2.5) * 0.1, (x, y, 8, 5)))
            cands.sort()
            n = min(large, len(cands))
            score = (large - n) * 10 + sum(c[0] for c in cands[:n])
            if score < best_score:
                best_score, best = score, [c[1] for c in cands[:n]]
    slots = list(best)
    cands = []
    for y in range(ry, ry + rh - 3 + 1):
        for x in range(rx, rx + rw - 4 + 1):
            s = rect_sand(sand, x, y, 4, 3)
            if s <= SLOT_MAX_SAND:
                cands.append((s + dist(x + 2, y + 1.5) * 0.1, (x, y, 4, 3)))
    cands.sort()
    for _, r in cands:
        if sum(1 for s in slots if s[2] == 4) >= small:
            break
        if not any(overlaps(r, t, 1) for t in taken + slots):
            slots.append(r)
    slots.sort(key=lambda s: (-s[1], s[0]))
    return slots


def build_districts(world):
    sand = sand_cells(world)
    taken = []
    blocks = [{"block": CENTER_BLOCK, "stages": [{"development": "", "x": START[0], "y": START[1], "w": START[2], "h": START[3]}],
               "slots": []}]
    for d in DIRECTIONS:
        block = {"block": d["block"], "direction": d["direction"], "stages": [], "slots": []}
        for t, (rect, _) in enumerate(d["tiers"], start=1):
            block["stages"].append({"development": expansion_id(d["key"], t), "x": rect[0], "y": rect[1], "w": rect[2], "h": rect[3]})
            large, small = SLOT_TARGETS[t - 1]
            for s in place_slots(sand, rect, large, small, taken):
                taken.append(s)
                block["slots"].append({"x": s[0], "y": s[1], "w": s[2], "h": s[3], "stage": t - 1})
        blocks.append(block)
    return blocks


# ---------------------------------------------------------------------------
# 미리보기

def preview(world, groups, sprites, sites, regions, districts, out, scale=0.25, show_walk=True):
    W, H = int(MAP_W * scale), int(MAP_H * scale)
    img = world.resize((W, H), Image.BILINEAR).convert("RGBA")
    P = lambda x, y: ((x + MAP_OX) * scale, (y + MAP_OY) * scale)
    for g in groups:
        sh = g.get("shade")
        if not sh:
            continue
        tile = Image.open(os.path.join(OUT_DIR, sh["sprite"] + ".png"))
        x0, y0 = P(sh["x0"], sh["y0"])
        x1, y1 = P(sh["x1"], sh["y1"])
        img.alpha_composite(tile.resize((max(1, int(x1 - x0)), max(1, int(y1 - y0)))), (int(x0), int(y0)))
    items = [t for g in groups for t in g["trees"]]
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
    colors = {"district_center": (160, 110, 255, 255), "district_northeast": (255, 99, 71, 255), "district_south": (255, 215, 0, 255),
              "district_northwest": (30, 144, 255, 255), "district_west": (50, 205, 50, 255)}
    for b in districts:
        c = colors.get(b["block"], (255, 255, 255, 255))
        for st in b["stages"]:
            r = cell_rect_px((st["x"], st["y"], st["w"], st["h"]))
            d.rectangle([*P(r[0], r[1]), *P(r[2], r[3])], outline=c, width=2)
        for s in b["slots"]:
            r = cell_rect_px((s["x"], s["y"], s["w"], s["h"]))
            d.rectangle([*P(r[0], r[1]), *P(r[2], r[3])], outline=(255, 255, 255, 255), width=1)
    if show_walk:
        for reg in regions:
            for poly in reg["polygons"]:
                pts = [P(poly["points"][k], poly["points"][k + 1]) for k in range(0, len(poly["points"]), 2)]
                d.line(pts + [pts[0]], fill=(60, 255, 90, 255) if reg["development"] else (255, 255, 255, 255), width=1)
    for s in sites:
        cx, cy = P(s["x"], s["y"])
        d.ellipse([cx - 6, cy - 4, cx + 6, cy + 4], outline=(255, 255, 255, 255), width=2)
        key = next(dd["key"] for dd in DIRECTIONS if dd["direction"] == s["direction"])
        d.text((cx + 7, cy - 6), f"{key}{s['tier']}", fill=(255, 255, 255, 255))
    img.convert("RGB").save(out)
    print("미리보기", os.path.relpath(out, ROOT))


def load_sprites_info():
    """그림을 다시 만들지 않을 때: 지난 배치의 그림 정보 + 그림 파일"""
    with open(OUT_LAYOUT, encoding="utf-8") as f:
        info = json.load(f)["sprites"]
    return info, {i["name"]: Image.open(os.path.join(OUT_DIR, f"{i['name']}.png")).convert("RGBA") for i in info}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rebuild-map", action="store_true", help="원본 지도에서 Plaza_WorldMap.jpg를 다시 만듦 (업스케일 캐시가 필요)")
    ap.add_argument("--no-upscale", action="store_true", help="--rebuild-map에서 Real-ESRGAN 대신 Lanczos")
    ap.add_argument("--rebuild-sprites", action="store_true", help="나무 · 덤불 · 점선 원 · 도끼 말풍선 그림을 다시 만듦")
    args = ap.parse_args()

    world = build_map(not args.no_upscale) if args.rebuild_map else Image.open(OUT_MAP).convert("RGB")
    sprite_info, sprites = build_sprites() if args.rebuild_sprites else load_sprites_info()
    sea = sea_mask(world)
    walk = erode_land(sea, WALK_FROM_SEA)

    # 숲 개간 조각 (단계마다 개간 수만큼)
    patch_list = []
    for d, t, rect, clearings, prev in tiers():
        for k, p in enumerate(patches(rect, clearings, prev), start=1):
            patch_list.append({"id": f"{d['key']}_{t}_{k}", "rect": p, "development": f"{expansion_id(d['key'], t)}_c{k}",
                               "expansion": expansion_id(d["key"], t)})
    for k, rect in enumerate(SHORE_PATH, start=1):
        patch_list.append({"id": f"shore_path_{k}", "rect": cell_rect_px(rect), "development": SHORE_DEVELOPMENT, "expansion": ""})

    # 걷기 영역: 공터(늘) · 단계(넓히면) · 바닷가(낚시터를 찾으면)
    regions = [{"id": "start", "development": "", "rect": px_rect_json(cell_rect_px(START)),
                "polygons": region_polygons(walk, cell_rect_px(START), 0)}]
    for d, t, rect, clearings, prev in tiers():
        regions.append({"id": f"{d['key']}_{t}", "direction": d["direction"], "tier": t, "development": expansion_id(d["key"], t),
                        "rect": px_rect_json(cell_rect_px(rect)), "polygons": region_polygons(walk, cell_rect_px(rect), CELL_OVERLAP)})
    regions.append({"id": "shore", "development": SHORE_DEVELOPMENT, "rect": px_rect_json(cell_rect_px(SHORE)),
                    "polygons": region_polygons(walk, cell_rect_px(SHORE), CELL_OVERLAP)})
    for k, rect in enumerate(SHORE_PATH, start=1):
        regions.append({"id": f"shore_path_{k}", "development": SHORE_DEVELOPMENT, "rect": px_rect_json(cell_rect_px(rect)),
                        "polygons": region_polygons(walk, cell_rect_px(rect), CELL_OVERLAP)})

    # 옛 그늘 그림은 지움 (묶음 이름이 바뀜)
    for name in os.listdir(OUT_DIR):
        if name.startswith("Shade_") and name.endswith(".png"):
            os.remove(os.path.join(OUT_DIR, name))
    groups = build_forest(sea, patch_list)
    build_shades(sea, groups, patch_list)
    for name in os.listdir(OUT_DIR):
        if name.startswith("Shade_") and name.endswith(".png.meta") and not os.path.exists(os.path.join(OUT_DIR, name[:-5])):
            os.remove(os.path.join(OUT_DIR, name))
    for g in groups:
        g.pop("rect", None)
    sites = build_sites(walk)
    homes = build_homes(walk)
    districts = build_districts(world)

    layout = {
        "comment": "build_territory_art.py가 만듦. 좌표 = 기존 바닥(Plaza_Ground.png) 픽셀, 왼쪽 위 원점·아래로 +y (districts는 꾸미기 격자 세이브 좌표 칸).",
        "groundPixelsPerUnit": 40,
        "map": {"sprite": "Plaza_WorldMap", "x0": -MAP_OX, "y0": -MAP_OY, "x1": RIGHT, "y1": BOTTOM},
        "start": px_rect_json(cell_rect_px(START)),
        "sprites": sprite_info,
        "regions": regions,
        "groups": groups,
        "sites": sites,
        "homes": homes,
        "districts": districts,
        "tiers": [{"direction": d["direction"], "key": d["key"], "tier": t, "clearings": c} for d, t, r, c, p in tiers()],
    }
    text = json.dumps(layout, ensure_ascii=False, indent=1)
    with open(OUT_LAYOUT, "w", encoding="utf-8") as f:
        f.write(text)
    n_trees = sum(len(g["trees"]) for g in groups)
    n_slots = sum(len(b["slots"]) for b in districts)
    print(f"영역 {len(regions)} · 숲 묶음 {len(groups)} · 나무/덤불 {n_trees} · 그루터기 {sum(len(g['stumps']) for g in groups)} · 부지 칸 {n_slots}")
    print("배치 ->", os.path.relpath(OUT_LAYOUT, ROOT))

    preview(world, groups, sprites, sites, regions, districts, os.path.join(HERE, "preview_forest.png"))


if __name__ == "__main__":
    main()
