"""광산 미로(지하 1층 테스트 씬) 그림 — 막힌 벽 덧그림, 보물 상자, 내려가는 계단, 지도 칸.

갱도 화면 4장(Assets/Art/MineMaze/Views/VIEW_F · FL · FR · FLR)은 사용자가 그린 그림이고, 이 스크립트는 건드리지 않는다.
- VIEW_WALL: 앞이 막힌 칸. 갱도 그림의 첫 나무 틀 안쪽을 같은 그림의 바위 결로 메운 덧그림 (같은 크기, 투명 배경)
- CHEST_closed / CHEST_open: 보물 상자 (스티커 스타일 임시 그림)
- STAIRS_down: 아래층으로 내려가는 사다리 구멍 (임시 그림)

python Tools/UIGen/mine_maze_art.py → Unity Tools/Mine Maze/Build Test Scene
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "MineMaze")
VIEWS = os.path.join(OUT, "Views")

SS = 4
COCOA = (75, 46, 34, 255)

# 갱도 그림(1701×925)의 첫 나무 틀 안쪽 (기둥 안쪽 · 들보 아래 · 틀 바닥)
FRAME_LEFT, FRAME_RIGHT, FRAME_TOP, FRAME_FLOOR = 646, 994, 246, 545


def save(img, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("그림", os.path.relpath(path, ROOT))


def wall_overlay():
    """첫 틀 안쪽을 바위로 막은 덧그림. 바위 결은 같은 그림 오른쪽 벽에서 떠 옴"""
    base = Image.open(os.path.join(VIEWS, "VIEW_F.png")).convert("RGB")
    w, h = base.size
    # 오른쪽 벽 바위 결 (빛이 고르게 든 곳) → 틀 안쪽 크기로
    patch = base.crop((1150, 230, 1560, 640)).transpose(Image.FLIP_LEFT_RIGHT)
    box_w, box_h = FRAME_RIGHT - FRAME_LEFT + 40, FRAME_FLOOR - FRAME_TOP + 30
    patch = patch.resize((box_w, box_h), Image.LANCZOS)
    # 가운데로 갈수록 조금 밝게 (막힌 벽이 앞에 있다는 느낌), 위 · 양옆은 어둡게
    shade = Image.new("L", (box_w, box_h), 0)
    sd = ImageDraw.Draw(shade)
    for i in range(40):
        k = i / 39
        sd.ellipse([box_w * (0.5 - 0.6 * (1 - k)), box_h * (0.55 - 0.7 * (1 - k)),
                    box_w * (0.5 + 0.6 * (1 - k)), box_h * (0.55 + 0.7 * (1 - k))], fill=int(255 * k))
    shade = shade.filter(ImageFilter.GaussianBlur(30))
    dark = Image.new("RGB", (box_w, box_h), (10, 18, 24))
    lit = ImageChops.multiply(patch, Image.new("RGB", (box_w, box_h), (205, 222, 230)))
    wall = Image.composite(lit, dark, shade.point(lambda v: 90 + v * 165 // 255))

    # 막힌 벽 모양: 틀 안쪽을 다 덮고, 아래는 울퉁불퉁 (돌무더기)
    mask = Image.new("L", (w * SS, h * SS), 0)
    md = ImageDraw.Draw(mask)
    rng = random.Random(7)
    pts = [((FRAME_LEFT - 6) * SS, (FRAME_TOP - 8) * SS), ((FRAME_RIGHT + 6) * SS, (FRAME_TOP - 8) * SS)]
    bottom = []
    steps = 14
    for i in range(steps + 1):
        x = FRAME_RIGHT + 6 - (FRAME_RIGHT - FRAME_LEFT + 12) * i / steps
        y = FRAME_FLOOR - 18 + rng.uniform(-14, 10) - 26 * math.sin(math.pi * i / steps)
        bottom.append((x * SS, y * SS))
    md.polygon(pts + bottom, fill=255)
    mask = mask.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(2.5))

    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    layer.paste(wall, (FRAME_LEFT - 20, FRAME_TOP - 12))
    layer.putalpha(mask)

    # 벽 아래 돌무더기 몇 개 (그림의 바위 색)
    rubble = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rubble)
    for _ in range(16):
        cx = rng.uniform(FRAME_LEFT + 10, FRAME_RIGHT - 10)
        cy = FRAME_FLOOR - 26 + rng.uniform(-10, 18)
        r = rng.uniform(10, 26)
        sides = rng.randint(5, 7)
        poly = []
        for s in range(sides):
            a = 2 * math.pi * s / sides + rng.uniform(-0.3, 0.3)
            rr = r * rng.uniform(0.7, 1.1)
            poly.append(((cx + math.cos(a) * rr) * SS, (cy + math.sin(a) * rr * 0.7) * SS))
        tone = rng.randint(34, 58)
        rd.polygon(poly, fill=(tone - 18, tone + 8, tone + 18, 255))
        # 왼쪽 위 빛
        hl = [(x - 3 * SS, y - 3 * SS) for x, y in poly[: max(2, sides // 2)]]
        if len(hl) >= 2:
            rd.line(hl, fill=(110, 140, 150, 90), width=3 * SS)
    rubble = rubble.resize((w, h), Image.LANCZOS)
    layer = Image.alpha_composite(layer, rubble)

    # 막힌 벽 둘레 그늘 (나무 틀 안쪽으로 스며드는 어둠)
    edge = Image.new("L", (w, h), 0)
    ed = ImageDraw.Draw(edge)
    ed.rectangle([FRAME_LEFT - 4, FRAME_TOP - 6, FRAME_RIGHT + 4, FRAME_FLOOR - 10], outline=200, width=26)
    edge = ImageChops.multiply(edge.filter(ImageFilter.GaussianBlur(16)), mask)
    shadow = Image.new("RGBA", (w, h), (4, 8, 12, 0))
    shadow.putalpha(edge)
    layer = Image.alpha_composite(layer, shadow)
    save(layer, "Views", "VIEW_WALL.png")


def poly_outline(d, pts, width, color=COCOA):
    d.line(pts + [pts[0]], fill=color, width=width, joint="curve")
    r = width / 2
    for x, y in pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)


def chest(opened):
    """보물 상자 (나무 + 금테, 코코아 외곽선). 발밑 = 그림 아래 가운데"""
    size = 512
    img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = SS
    ow = 14 * s
    wood, wood_dark, wood_light = (176, 108, 62, 255), (128, 74, 44, 255), (214, 150, 96, 255)
    gold, gold_light = (242, 184, 58, 255), (255, 226, 140, 255)
    left, right, top, bottom = 70 * s, 442 * s, 250 * s, 470 * s
    # 그림자
    d.ellipse([left - 10 * s, bottom - 26 * s, right + 10 * s, bottom + 22 * s], fill=(10, 16, 22, 110))
    # 몸통
    body = [(left, top), (right, top), (right, bottom), (left, bottom)]
    d.polygon(body, fill=wood)
    for i in range(1, 4):
        y = top + (bottom - top) * i / 4
        d.line([(left + 8 * s, y), (right - 8 * s, y)], fill=wood_dark, width=5 * s)
    d.rectangle([left, top, right, top + 22 * s], fill=wood_light)
    # 금테 · 자물쇠
    for x in (left + 40 * s, right - 40 * s):
        d.rectangle([x - 18 * s, top, x + 18 * s, bottom], fill=gold)
        d.line([(x - 10 * s, top + 8 * s), (x - 10 * s, bottom - 8 * s)], fill=gold_light, width=5 * s)
    poly_outline(d, body, ow)
    if opened:
        # 열린 뚜껑 (뒤로 젖혀짐) + 안쪽 어둠 + 빛
        inside = [(left + 10 * s, top - 4 * s), (right - 10 * s, top - 4 * s), (right - 30 * s, top + 34 * s), (left + 30 * s, top + 34 * s)]
        d.polygon(inside, fill=(48, 28, 22, 255))
        lid = [(left - 4 * s, top - 6 * s), (right + 4 * s, top - 6 * s), (right - 20 * s, 80 * s), (left + 20 * s, 80 * s)]
        d.polygon(lid, fill=wood_dark)
        d.line([(left + 40 * s, 92 * s), (right - 40 * s, 92 * s)], fill=wood, width=10 * s)
        poly_outline(d, lid, ow)
        glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        gd.ellipse([left + 60 * s, top - 90 * s, right - 60 * s, top + 40 * s], fill=(255, 230, 150, 120))
        img = Image.alpha_composite(glow.filter(ImageFilter.GaussianBlur(26 * s)), img)
    else:
        lid = [(left - 6 * s, top + 4 * s), (right + 6 * s, top + 4 * s), (right - 6 * s, 150 * s), (left + 6 * s, 150 * s)]
        d.rounded_rectangle([left - 6 * s, 150 * s, right + 6 * s, top + 10 * s], radius=10 * s, fill=wood)
        d.pieslice([left - 6 * s, 90 * s, right + 6 * s, 210 * s], 180, 360, fill=wood_light)
        for x in (left + 40 * s, right - 40 * s):
            d.rectangle([x - 18 * s, 120 * s, x + 18 * s, top + 10 * s], fill=gold)
        d.line([(left - 6 * s, 150 * s), (left - 6 * s, top + 10 * s), (right + 6 * s, top + 10 * s), (right + 6 * s, 150 * s)], fill=COCOA, width=ow)
        d.arc([left - 6 * s, 90 * s, right + 6 * s, 210 * s], 180, 360, fill=COCOA, width=ow)
        # 자물쇠
        cx = (left + right) / 2
        d.rounded_rectangle([cx - 34 * s, top - 30 * s, cx + 34 * s, top + 44 * s], radius=10 * s, fill=gold, outline=COCOA, width=10 * s)
        d.ellipse([cx - 10 * s, top - 4 * s, cx + 10 * s, top + 16 * s], fill=COCOA)
        d.line([(cx - 20 * s, top - 22 * s), (cx - 8 * s, top - 22 * s)], fill=gold_light, width=6 * s)
    img = img.resize((size, size), Image.LANCZOS)
    save(img, "Objects", "CHEST_open.png" if opened else "CHEST_closed.png")


def stairs():
    """바닥에 뚫린 구멍 + 아래로 내려가는 나무 사다리 (발밑 = 그림 아래 가운데)"""
    w, h = 640, 400
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = SS
    # 구멍 (가장자리 바위 → 안쪽 어둠)
    d.ellipse([40 * s, 150 * s, 600 * s, 390 * s], fill=(70, 92, 102, 255))
    d.ellipse([80 * s, 175 * s, 560 * s, 370 * s], fill=(14, 22, 28, 255))
    d.ellipse([140 * s, 205 * s, 500 * s, 350 * s], fill=(4, 6, 9, 255))
    # 사다리 (구멍에서 위로 솟음)
    rail = (150, 102, 64, 255)
    for x in (250, 390):
        d.line([(x * s, 330 * s), (x * s - 20 * s, 30 * s)], fill=COCOA, width=26 * s)
        d.line([(x * s, 330 * s), (x * s - 20 * s, 30 * s)], fill=rail, width=14 * s)
    for i in range(6):
        y = 60 + i * 50
        x0 = 250 - 20 * (330 - y) / 300
        x1 = 390 - 20 * (330 - y) / 300
        d.line([(x0 * s, y * s), (x1 * s, y * s)], fill=COCOA, width=20 * s)
        d.line([(x0 * s, y * s), (x1 * s, y * s)], fill=(196, 140, 88, 255), width=9 * s)
    img = img.resize((w, h), Image.LANCZOS)
    save(img, "Objects", "STAIRS_down.png")


if __name__ == "__main__":
    wall_overlay()
    chest(False)
    chest(True)
    stairs()
