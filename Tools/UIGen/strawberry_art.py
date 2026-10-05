"""딸기 임시 그림 (정식 그림이 오면 같은 이름으로 바꿔 끼우면 됨).

밭 작물 칸 그림은 기존 작물 그림을 빌려 만든다 (같은 손그림 질감·크기·발밑 기준):
- 씨앗: 당근 씨앗 그림의 주황 씨를 노르스름하게
- 새싹: 오이 새싹 그림 그대로
- 다 자람: 흰 꽃이 핀 감자 덤불 위에 빨간 딸기를 그려 넣음 (땅속 감자는 딸기로 가림)
가방 아이콘과 도감 실루엣은 새로 그린다 (다른 채소 아이콘처럼 굵은 외곽선).

    python Tools/UIGen/strawberry_art.py [프로젝트 경로]
"""
import colorsys
import math
import os

from PIL import Image, ImageDraw, ImageFilter

import sys

# 다른 작업 폴더(worktree)에 만들 때: python strawberry_art.py <프로젝트 경로>
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CROP = os.path.join(ROOT, "Assets", "Art", "Farm", "Crop")
ICON = os.path.join(ROOT, "Assets", "Art", "Item", "Vegetable", "Strawberry.png")
SILHOUETTE = os.path.join(ROOT, "Assets", "Art", "UI", "Collection", "Silhouettes", "SIL_Strawberry.png")

RED = (222, 46, 52)
RED_DARK = (170, 24, 36)
RED_LIGHT = (255, 120, 110)
OUTLINE = (96, 28, 22)
SEED = (250, 214, 96)
LEAF = (86, 160, 58)
LEAF_DARK = (36, 92, 34)
SILHOUETTE_COLOR = (91, 58, 44)

SUPER = 4  # 4배로 그리고 줄여서 가장자리를 부드럽게


def body_points(cx, cy, w, h, steps=120):
    """딸기 몸통 외곽: 둥근 어깨에서 가장 넓고 아래로 갈수록 좁아져 둥근 끝 (cy = 몸통 맨 위)"""
    profile = [(u ** 0.42) * ((1 - u) ** 0.62) for u in (i / steps for i in range(steps + 1))]
    scale = (w / 2) / max(profile)
    right = [(cx + profile[i] * scale, cy + h * i / steps) for i in range(steps + 1)]
    left = [(2 * cx - x, y) for x, y in reversed(right)]
    return right + left[1:-1]


def strawberry(size, outline_ratio=0.045):
    """size × size 딸기 한 알 (투명 배경, 꼭지 포함)"""
    s = size * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    w, h = s * 0.80, s * 0.78
    cx, cy = s / 2, s * 0.17
    outline = max(2, int(s * outline_ratio))
    shape = body_points(cx, cy, w, h)

    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).polygon(shape, fill=255)
    # 몸통: 왼쪽 위가 밝고 오른쪽 아래가 어두운 그라데이션
    shade = Image.new("RGBA", (s, s))
    px = shade.load()
    for y in range(s):
        for x in range(0, s, 2):
            k = min(1.0, max(0.0, ((x - cx) / w + (y - cy) / h) * 0.9 + 0.15))
            c = tuple(int(RED[i] + (RED_DARK[i] - RED[i]) * k) for i in range(3)) + (255,)
            px[x, y] = c
            if x + 1 < s:
                px[x + 1, y] = c
    img.paste(shade, (0, 0), mask)

    draw = ImageDraw.Draw(img)
    # 씨: 엇갈린 줄 (꼭지 아래부터)
    for r in range(6):
        v = 0.24 + r * 0.12
        y = cy + h * v
        inside = [x for x in range(int(cx - w / 2), int(cx + w / 2)) if mask.getpixel((x, int(y)))]
        if not inside:
            continue
        half = (inside[-1] - inside[0]) / 2 * 0.8
        count = max(1, int(round(half * 2 / (w * 0.16))))
        for k in range(count):
            x = cx - half + (k + (0.75 if r % 2 else 0.5)) * (half * 2 / (count + (0.5 if r % 2 else 0)))
            if not mask.getpixel((int(x), int(y))):
                continue
            rx, ry = s * 0.017, s * 0.027
            draw.ellipse((x - rx, y - ry, x + rx, y + ry), fill=SEED)
    # 빛: 왼쪽 어깨 반짝임
    hl = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(hl).ellipse((cx - w * 0.36, cy + h * 0.16, cx - w * 0.16, cy + h * 0.40), fill=RED_LIGHT + (170,))
    hl = hl.filter(ImageFilter.GaussianBlur(s * 0.02))
    img.alpha_composite(Image.composite(hl, Image.new("RGBA", (s, s)), mask))
    draw = ImageDraw.Draw(img)
    draw.line(shape + [shape[0]], fill=OUTLINE, width=outline, joint="curve")

    # 꼭지: 어깨 위로 퍼진 잎 7장 (양옆·아래로 처짐) + 위로 짧은 줄기
    top = (cx, cy + h * 0.07)
    leaf_w = max(2, outline * 2 // 3)
    for deg in (-15, 15, 50, 90, 130, 165, 195):
        ang = math.radians(deg)
        length = w * (0.34 if deg in (-15, 195) else 0.27)
        tip = (top[0] + math.cos(ang) * length, top[1] + math.sin(ang) * length * 0.55)
        side = ang + math.pi / 2
        half = w * 0.06
        base_a = (top[0] + math.cos(side) * half, top[1] + math.sin(side) * half * 0.55)
        base_b = (top[0] - math.cos(side) * half, top[1] - math.sin(side) * half * 0.55)
        draw.polygon([base_a, tip, base_b], fill=LEAF)
        draw.line([base_a, tip, base_b], fill=LEAF_DARK, width=leaf_w, joint="curve")
    draw.ellipse((top[0] - w * 0.08, top[1] - h * 0.045, top[0] + w * 0.08, top[1] + h * 0.045), fill=LEAF, outline=LEAF_DARK, width=leaf_w)
    draw.line([top, (top[0] + w * 0.05, top[1] - h * 0.16)], fill=LEAF_DARK, width=int(outline * 1.4))

    return img.resize((size, size), Image.LANCZOS)


def shift_seeds(src, dst):
    """밝은 주황 씨만 노르스름하게 (흙의 갈색은 어두워서 그대로)"""
    img = Image.open(src).convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if 0.02 < h < 0.13 and s > 0.45 and v > 0.7:
                nr, ng, nb = colorsys.hsv_to_rgb(0.13, s * 0.55, min(1.0, v * 1.05))
                px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    img.save(dst)


# 다 자란 덤불(512 폭) 위 딸기 자리: (가로 비율, 세로 비율, 크기) — 아래쪽 땅속 감자를 가리고 잎 사이에 몇 알
GROWN_BERRIES = [
    [(0.30, 0.80, 92), (0.50, 0.86, 104), (0.70, 0.80, 92), (0.22, 0.58, 70), (0.76, 0.56, 72), (0.47, 0.64, 66)],
    [(0.32, 0.82, 96), (0.55, 0.86, 100), (0.74, 0.78, 86), (0.26, 0.60, 70), (0.66, 0.60, 68)],
    [(0.28, 0.80, 90), (0.48, 0.87, 104), (0.70, 0.82, 94), (0.38, 0.62, 70), (0.78, 0.60, 70), (0.20, 0.66, 64)],
]


def grown(variant):
    img = Image.open(os.path.join(CROP, f"crop_potato_grown_{variant}.png")).convert("RGBA")
    for fx, fy, size in GROWN_BERRIES[variant]:
        berry = strawberry(size, outline_ratio=0.06)
        x = int(img.width * fx - size / 2)
        y = int(img.height * fy - size * 0.35)
        img.alpha_composite(berry, (x, y))
    img.save(os.path.join(CROP, f"crop_strawberry_grown_{variant}.png"))


def main():
    for v in range(3):
        shift_seeds(os.path.join(CROP, f"crop_carrot_seed_{v}.png"), os.path.join(CROP, f"crop_strawberry_seed_{v}.png"))
        Image.open(os.path.join(CROP, f"crop_cucumber_sprout_{v}.png")).save(os.path.join(CROP, f"crop_strawberry_sprout_{v}.png"))
        grown(v)

    icon = strawberry(512, outline_ratio=0.05)
    icon.save(ICON)

    sil = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    alpha = icon.resize((240, 240), Image.LANCZOS).split()[3]
    solid = Image.new("RGBA", (240, 240), SILHOUETTE_COLOR + (255,))
    sil.paste(solid, (8, 8), alpha)
    sil.save(SILHOUETTE)
    print("딸기 그림:", CROP, ICON, SILHOUETTE)


if __name__ == "__main__":
    main()
