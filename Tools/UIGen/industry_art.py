"""번영의 시대 광장 소품 (스티커 스타일: 코코아 외곽선 · 따뜻한 나무색). 정식 그림을 받으면 바꿈.

python Tools/UIGen/industry_art.py → Assets/Art/Industry/
  Prop_Sawhorse          목공소 제재 작업대 (I01)
  Prop_StoneBench        채석 작업소 돌 재단대 (I02)
  Pile_Plank_0..2        시설에 쌓인 판재 (적음 · 보통 · 많음)
  Pile_StoneBlock_0..2   시설에 쌓인 석재 블록
  Cart_Empty / Cart_Plank / Cart_StoneBlock   수레 (오른쪽으로 감, 손잡이는 왼쪽)
  Prop_WarehouseCrates   왕국 창고 자리 (상자 더미 + 표지)
  Prop_KingdomSign       왕국 표지판 (C01 꾸미기)
  Prop_Kiln              건설소 제련 가마 (I03)
  Pile_Ingot_0..2 · Cart_Ingot   금속 주괴 더미 · 수레
  Prop_Fountain · Prop_Flowerbed 공공 시설 꾸미기 (C04)
  TownHouse_Red · TownHouse_Blue  마을 주택 미리보기 (개량 주택 + 현관 등불 · 화분, C05)
  Pile_Precision_0..2 · Cart_Precision  정밀 부품 상자 더미 · 수레 (I06)
  Cart_Pennant           개량 수레(L06) 깃발 (수레 뒤 손잡이 쪽 위)
  Prop_RoyalBanner       왕립 산업단지(I08) 시설 깃발
  Prop_CapitalFlag       수도 광장(C07) 깃대
  Academy                왕립 학술원(C06) = 기록관 그림의 지붕을 남색으로 + 별 문장
  Palace                 왕궁(C08) = 성 그림의 파란 지붕 · 깃발을 금빛으로
"""
import math
import os

from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Industry')
SETTLEMENT = os.path.join(ROOT, 'Assets', 'Art', 'Settlement')

COCOA = (75, 46, 34, 255)
LINE = 14
S = 2  # 2배로 그리고 줄임

WOOD_LIGHT = (240, 192, 128, 255)
WOOD = (219, 156, 92, 255)
WOOD_DARK = (176, 112, 64, 255)
GRAIN = (190, 128, 78, 255)
STONE_TOP = (226, 226, 218, 255)
STONE_FRONT = (186, 186, 178, 255)
STONE_SIDE = (156, 156, 150, 255)
LEAF = (132, 196, 96, 255)
SHADOW = (60, 40, 30, 60)


def canvas(w, h):
    im = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def s(*v):
    return tuple(int(round(x * S)) for x in v)


def poly(d, pts, fill, width=LINE):
    pts = [s(*p) for p in pts]
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=COCOA, width=width, joint='curve')


def rrect(d, box, r, fill, width=LINE):
    d.rounded_rectangle(s(*box), radius=r * S, fill=fill, outline=COCOA, width=width)


def ellipse(d, box, fill, width=LINE):
    d.ellipse(s(*box), fill=fill, outline=COCOA, width=width)


def shadow(d, cx, cy, rx, ry):
    d.ellipse(s(cx - rx, cy - ry, cx + rx, cy + ry), fill=SHADOW)


def finish(im, name, scale=0.5):
    box = im.getbbox()
    pad = 6 * S
    im = im.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(im.width, box[2] + pad), min(im.height, box[3] + pad)))
    im = im.resize((max(1, int(im.width * scale)), max(1, int(im.height * scale))), Image.LANCZOS)
    path = os.path.join(OUT, name + '.png')
    im.save(path)
    print(path, im.size)


# ---------------------------------------------------------------- 나무 · 돌 조각

def plank(d, x, y, w, h, color=WOOD_LIGHT):
    """옆에서 본 판재 한 장 (앞면 + 끝 단면)"""
    rrect(d, (x, y, x + w, y + h), 8, color)
    mid = y + h / 2
    d.line(s(x + 26, mid - 4, x + w * 0.55, mid - 4), fill=GRAIN, width=5 * S)
    d.line(s(x + w * 0.4, mid + 6, x + w - 34, mid + 6), fill=GRAIN, width=5 * S)
    d.rounded_rectangle(s(x + w - 24, y + 8, x + w - 10, y + h - 8), radius=4 * S, fill=(252, 222, 170, 255))


def block(d, x, y, size):
    """석재 블록 (윗면 · 앞면 · 옆면)"""
    k = size * 0.42
    top = [(x, y + k * 0.5), (x + size * 0.55, y), (x + size * 1.0, y + k * 0.35), (x + size * 0.45, y + k * 0.85)]
    front = [(x, y + k * 0.5), (x + size * 0.45, y + k * 0.85), (x + size * 0.45, y + k * 0.85 + size * 0.62), (x, y + k * 0.5 + size * 0.62)]
    side = [(x + size * 0.45, y + k * 0.85), (x + size, y + k * 0.35), (x + size, y + k * 0.35 + size * 0.62),
            (x + size * 0.45, y + k * 0.85 + size * 0.62)]
    for face, color in ((top, STONE_TOP), (front, STONE_FRONT), (side, STONE_SIDE)):
        d.polygon([s(*p) for p in face], fill=color)
    for face in (top, front, side):
        pts = [s(*p) for p in face]
        d.line(pts + [pts[0]], fill=COCOA, width=10 * S // 2, joint='curve')
    d.line(s(x + size * 0.12, y + k * 0.5 + size * 0.3, x + size * 0.3, y + k * 0.62 + size * 0.3), fill=(150, 150, 144, 255), width=4 * S)


# ---------------------------------------------------------------- 소품

def sawhorse():
    im, d = canvas(420, 340)
    shadow(d, 210, 300, 170, 26)
    # 뒤 다리 (A자)
    for x0 in (90, 300):
        poly(d, [(x0 - 34, 300), (x0 - 14, 300), (x0 + 22, 150), (x0 + 4, 150)], WOOD_DARK)
    # 통나무
    rrect(d, (40, 112, 380, 176), 30, WOOD)
    d.line(s(80, 136, 250, 136), fill=GRAIN, width=6 * S)
    d.line(s(150, 156, 330, 156), fill=GRAIN, width=6 * S)
    ellipse(d, (346, 110, 396, 178), (250, 214, 160, 255))
    d.ellipse(s(362, 132, 380, 156), outline=GRAIN, width=4 * S)
    # 앞 다리
    for x0 in (110, 320):
        poly(d, [(x0 + 30, 302), (x0 + 50, 302), (x0 + 4, 160), (x0 - 14, 160)], WOOD)
    # 톱 (손잡이 + 날)
    poly(d, [(196, 40), (284, 112), (262, 120), (176, 54)], (214, 222, 228, 255), 10)
    for i in range(5):
        t = i / 5
        x, y = 182 + 90 * t, 52 + 66 * t
        d.line(s(x, y, x - 6, y + 9), fill=COCOA, width=3 * S)
    rrect(d, (150, 22, 206, 64), 12, (220, 92, 72, 255), 10)
    # 새싹
    ellipse(d, (32, 270, 72, 304), LEAF, 8)
    ellipse(d, (58, 262, 96, 298), (150, 210, 110, 255), 8)
    finish(im, 'Prop_Sawhorse')


def stone_bench():
    im, d = canvas(420, 330)
    shadow(d, 210, 292, 170, 26)
    # 낮은 돌 받침 (넓은 상판 + 다리 두 개)
    poly(d, [(70, 200), (110, 290), (150, 290), (130, 200)], STONE_SIDE)
    poly(d, [(290, 200), (300, 290), (340, 290), (350, 200)], STONE_SIDE)
    poly(d, [(40, 168), (190, 120), (390, 150), (250, 204)], STONE_TOP)
    poly(d, [(40, 168), (250, 204), (250, 228), (40, 192)], STONE_FRONT)
    poly(d, [(250, 204), (390, 150), (390, 172), (250, 228)], STONE_SIDE)
    # 다듬는 중인 블록
    block(d, 170, 70, 110)
    # 정 · 망치
    poly(d, [(300, 70), (318, 64), (346, 136), (330, 142)], (200, 206, 212, 255), 9)
    rrect(d, (80, 96, 150, 126), 10, (150, 150, 150, 255), 9)
    poly(d, [(112, 124), (124, 124), (138, 170), (126, 172)], WOOD, 9)
    finish(im, 'Prop_StoneBench')


def plank_pile(level):
    counts = [2, 3, 5][level]
    im, d = canvas(380, 300)
    shadow(d, 190, 270, 160, 22)
    rows = [(60, 210), (80, 170), (52, 130), (90, 92), (66, 54)]
    for i in range(counts):
        x, y = rows[i]
        plank(d, x, y + 10, 250, 46, [WOOD_LIGHT, WOOD, (245, 205, 145, 255)][i % 3])
    finish(im, f'Pile_Plank_{level}')


def stone_pile(level):
    spots = [[(150, 150)], [(80, 170), (220, 170), (150, 90)], [(40, 180), (160, 180), (280, 180), (100, 106), (220, 106), (160, 34)]][level]
    im, d = canvas(420, 320)
    shadow(d, 210, 292, 180, 22)
    for x, y in spots:
        block(d, x, y, 120)
    finish(im, f'Pile_StoneBlock_{level}')


def cart(load):
    im, d = canvas(520, 380)
    shadow(d, 280, 340, 200, 24)
    # 손잡이 (왼쪽 위로)
    d.line(s(150, 200, 30, 136), fill=COCOA, width=24 * S)
    d.line(s(150, 200, 30, 136), fill=WOOD_DARK, width=12 * S)
    rrect(d, (14, 120, 46, 152), 10, WOOD, 9)
    # 짐
    if load == 'Plank':
        for i, (x, y) in enumerate([(150, 132), (176, 96), (142, 60)]):
            plank(d, x, y, 290, 44, [WOOD_LIGHT, WOOD, (245, 205, 145, 255)][i])
    elif load == 'StoneBlock':
        for x, y in [(170, 90), (290, 90), (230, 22)]:
            block(d, x, y, 112)
    elif load == 'Ingot':
        for x, y in [(160, 128), (300, 128), (230, 84)]:
            ingot_bar(d, x, y, 140)
    elif load == 'Precision':
        for x, y in [(150, 80), (300, 80)]:
            part_crate(d, x, y, 130)
    # 짐칸
    rrect(d, (120, 170, 470, 270), 18, WOOD)
    for x in (210, 300, 390):
        d.line(s(x, 182, x, 258), fill=WOOD_DARK, width=6 * S)
    d.line(s(136, 200, 456, 200), fill=GRAIN, width=5 * S)
    # 바퀴 (앞뒤)
    for cx in (200, 400):
        ellipse(d, (cx - 58, 236, cx + 58, 352), WOOD_DARK)
        ellipse(d, (cx - 40, 254, cx + 40, 334), WOOD_LIGHT, 8)
        for a in range(0, 180, 45):
            r = math.radians(a)
            d.line(s(cx - 38 * math.cos(r), 294 - 38 * math.sin(r), cx + 38 * math.cos(r), 294 + 38 * math.sin(r)), fill=COCOA, width=6 * S)
        ellipse(d, (cx - 14, 280, cx + 14, 308), (120, 80, 50, 255), 6)
    finish(im, f'Cart_{load}')


def warehouse_crates():
    box = Image.open(os.path.join(SETTLEMENT, 'Prop_SupplyBox.png')).convert('RGBA')
    w, h = 560, 480
    im = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    shadow(d, 280, 430, 250, 34)
    # 상자 세 개 (뒤 → 앞)
    for (x, y, k) in [(150, 40, 0.95), (40, 170, 1.0), (260, 170, 1.0)]:
        b = box.resize((int(box.width * k * S), int(box.height * k * S)), Image.LANCZOS)
        im.alpha_composite(b, (x * S, y * S))
    # 표지 (기둥 + 판, 상자 그림)
    d.line(s(500, 420, 500, 230), fill=COCOA, width=26 * S)
    d.line(s(500, 420, 500, 230), fill=WOOD_DARK, width=14 * S)
    rrect(d, (420, 150, 556, 250), 18, (236, 196, 140, 255))
    rrect(d, (462, 182, 514, 226), 6, WOOD, 8)
    d.line(s(462, 196, 514, 196), fill=COCOA, width=4 * S)
    d.line(s(488, 182, 488, 196), fill=COCOA, width=4 * S)
    finish(im, 'Prop_WarehouseCrates')


def kingdom_sign():
    im, d = canvas(300, 420)
    shadow(d, 150, 392, 90, 18)
    # 기둥
    d.line(s(150, 400, 150, 170), fill=COCOA, width=30 * S)
    d.line(s(150, 400, 150, 170), fill=WOOD_DARK, width=16 * S)
    # 판 (판재로 만든 두 장)
    rrect(d, (30, 40, 270, 196), 24, (240, 200, 146, 255))
    d.line(s(44, 118, 86, 118), fill=COCOA, width=6 * S)
    d.line(s(214, 118, 256, 118), fill=COCOA, width=6 * S)
    for x, y in ((70, 72), (200, 150)):
        d.ellipse(s(x - 6, y - 6, x + 6, y + 6), fill=COCOA)
    # 조개 문장 (왕국)
    cx, cy = 150, 118
    pts = []
    for i in range(0, 181, 10):
        r = math.radians(180 + i)
        rad = 58 + 6 * math.sin(math.radians(i * 5) * 1.0)
        pts.append((cx + rad * math.cos(r), cy + 10 + rad * 0.95 * math.sin(r)))
    pts.append((cx + 16, cy + 46))
    pts.append((cx - 16, cy + 46))
    d.polygon([s(*p) for p in pts], fill=(247, 196, 168, 255))
    q = [s(*p) for p in pts]
    d.line(q + [q[0]], fill=COCOA, width=8 * S, joint='curve')
    for i in range(-2, 3):
        a = math.radians(270 + i * 26)
        d.line(s(cx, cy + 40, cx + 50 * math.cos(a), cy + 14 + 50 * math.sin(a)), fill=(214, 140, 120, 255), width=5 * S)
    # 풀
    ellipse(d, (108, 378, 146, 408), LEAF, 8)
    ellipse(d, (150, 374, 190, 406), (150, 210, 110, 255), 8)
    finish(im, 'Prop_KingdomSign')


INGOT_TOP = (246, 196, 128, 255)
INGOT_FRONT = (214, 150, 92, 255)
FIRE = (255, 160, 70, 255)
FIRE_CORE = (255, 228, 140, 255)
WATER = (150, 214, 236, 255)
WATER_DEEP = (112, 186, 220, 255)


def ingot_bar(d, x, y, w):
    """옆에서 본 금속 주괴 하나 (윗면 · 앞면)"""
    h = w * 0.36
    top = [(x + w * 0.14, y), (x + w * 0.86, y), (x + w, y + h * 0.42), (x, y + h * 0.42)]
    front = [(x, y + h * 0.42), (x + w, y + h * 0.42), (x + w * 0.92, y + h), (x + w * 0.08, y + h)]
    d.polygon([s(*q) for q in front], fill=INGOT_FRONT)
    d.polygon([s(*q) for q in top], fill=INGOT_TOP)
    for face in (top, front):
        pts = [s(*q) for q in face]
        d.line(pts + [pts[0]], fill=COCOA, width=10, joint='curve')
    d.line(s(x + w * 0.22, y + h * 0.62, x + w * 0.5, y + h * 0.62), fill=(250, 226, 180, 255), width=4 * S)


def kiln():
    im, d = canvas(420, 420)
    shadow(d, 210, 392, 180, 24)
    # 굴뚝
    rrect(d, (250, 30, 320, 170), 14, STONE_FRONT)
    rrect(d, (240, 18, 330, 50), 12, STONE_TOP)
    # 가마 몸통 (돌 반원)
    d.pieslice(s(50, 110, 370, 430), 180, 360, fill=STONE_TOP, outline=COCOA, width=LINE)
    rrect(d, (50, 260, 370, 380), 20, STONE_FRONT)
    for y in (290, 330):
        d.line(s(64, y, 356, y), fill=STONE_SIDE, width=5 * S)
    for x, y in ((110, 290), (230, 290), (170, 330), (290, 330)):
        d.line(s(x, y, x, y + 40), fill=STONE_SIDE, width=5 * S)
    # 불구멍
    d.pieslice(s(140, 190, 280, 330), 180, 360, fill=(90, 50, 40, 255), outline=COCOA, width=LINE)
    d.rectangle(s(140, 260, 280, 300), fill=(90, 50, 40, 255))
    d.line(s(140, 260, 140, 300), fill=COCOA, width=LINE)
    d.line(s(280, 260, 280, 300), fill=COCOA, width=LINE)
    ellipse(d, (160, 228, 260, 300), FIRE, 0)
    ellipse(d, (186, 250, 236, 298), FIRE_CORE, 0)
    # 모루 (앞 오른쪽)
    poly(d, [(300, 330), (400, 330), (390, 352), (310, 352)], (120, 120, 128, 255), 10)
    poly(d, [(330, 352), (370, 352), (380, 388), (320, 388)], (96, 96, 104, 255), 10)
    finish(im, 'Prop_Kiln')


def ingot_pile(level):
    spots = [[(120, 120)], [(60, 150), (200, 150), (130, 96)], [(30, 170), (170, 170), (310, 170), (100, 116), (240, 116), (170, 62)]][level]
    im, d = canvas(480, 280)
    shadow(d, 240, 252, 200, 22)
    for x, y in spots:
        ingot_bar(d, x, y, 150)
    finish(im, f'Pile_Ingot_{level}')


def small_gear(d, cx, cy, r, fill):
    pts = []
    for i in range(16):
        a = math.pi * i / 8
        rr = r if i % 2 == 0 else r * 0.76
        for k in (-0.22, 0.22):
            b = a + k * math.pi / 8
            pts.append(s(cx + rr * math.cos(b), cy + rr * math.sin(b)))
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=COCOA, width=9, joint='curve')
    d.ellipse(s(cx - r * 0.3, cy - r * 0.3, cx + r * 0.3, cy + r * 0.3), fill=(120, 120, 128, 255), outline=COCOA, width=8)


def part_crate(d, x, y, w):
    """정밀 부품 상자: 나무 상자 + 위에 톱니 둘"""
    h = w * 0.62
    small_gear(d, x + w * 0.34, y + h * 0.18, w * 0.2, (206, 214, 224, 255))
    small_gear(d, x + w * 0.68, y + h * 0.26, w * 0.15, (240, 196, 128, 255))
    rrect(d, (x, y + h * 0.3, x + w, y + h * 1.3), 12, WOOD, 10)
    d.line(s(x + 8, y + h * 0.8, x + w - 8, y + h * 0.8), fill=WOOD_DARK, width=5 * S)
    rrect(d, (x + w * 0.36, y + h * 0.62, x + w * 0.64, y + h * 0.98), 6, (236, 228, 214, 255), 7)


def precision_pile(level):
    spots = [[(120, 150)], [(40, 150), (200, 150)], [(0, 150), (160, 150), (320, 150), (80, 40), (240, 40)]][level]
    im, d = canvas(500, 330)
    shadow(d, 250, 300, 220, 22)
    for x, y in spots:
        part_crate(d, x + 20, y, 150)
    finish(im, f'Pile_Precision_{level}')


def cart_pennant():
    im, d = canvas(160, 220)
    d.line(s(40, 210, 40, 20), fill=COCOA, width=16 * S // 2 * 2)
    d.line(s(40, 210, 40, 20), fill=WOOD_DARK, width=8 * S // 2 * 2)
    poly(d, [(44, 26), (150, 52), (44, 90)], (230, 92, 86, 255), 10)
    poly(d, [(44, 52), (100, 60), (44, 74)], (255, 214, 110, 255), 6)
    ellipse(d, (28, 6, 52, 30), (255, 214, 110, 255), 8)
    finish(im, 'Cart_Pennant')


def banner_pole(name, cloth, emblem):
    im, d = canvas(220, 520)
    shadow(d, 70, 500, 50, 12)
    d.line(s(70, 505, 70, 40), fill=COCOA, width=22)
    d.line(s(70, 505, 70, 40), fill=WOOD_DARK, width=10)
    ellipse(d, (52, 18, 88, 54), (255, 214, 110, 255), 8)
    # 늘어진 깃발 (아래 제비꼬리)
    poly(d, [(76, 70), (200, 70), (200, 300), (138, 262), (76, 300)], cloth, 12)
    d.line(s(76, 92, 200, 92), fill=(255, 214, 110, 255), width=6 * S)
    emblem(d, 138, 170)
    rrect(d, (52, 486, 88, 506), 6, STONE_FRONT, 8)
    finish(im, name)


def crown(d, cx, cy, k=1.0, color=(255, 214, 110, 255)):
    pts = [(-40, 24), (-40, -16), (-20, 4), (0, -30), (20, 4), (40, -16), (40, 24)]
    poly(d, [(cx + x * k, cy + y * k) for x, y in pts], color, 9)
    for x, y in ((-40, -16), (0, -30), (40, -16)):
        ellipse(d, (cx + x * k - 7, cy + y * k - 7, cx + x * k + 7, cy + y * k + 7), (247, 160, 170, 255), 5)


def shell(d, cx, cy):
    pts = []
    for i in range(0, 181, 15):
        r = math.radians(180 + i)
        pts.append((cx + 36 * math.cos(r), cy + 6 + 34 * math.sin(r)))
    pts += [(cx + 10, cy + 30), (cx - 10, cy + 30)]
    poly(d, pts, (247, 196, 168, 255), 8)
    for a in (-50, -25, 0, 25, 50):
        r = math.radians(270 + a)
        d.line(s(cx, cy + 26, cx + 30 * math.cos(r), cy + 8 + 30 * math.sin(r)), fill=(214, 140, 120, 255), width=4 * S)


def recolor(path, out, hue_from, hue_to, target_hue, sat_mul=1.0, val_mul=1.0, overlay=None):
    """그림에서 색상(hue) 범위의 칸만 다른 색으로 (모양 · 외곽선은 그대로)"""
    import numpy as np
    im = Image.open(path).convert('RGBA')
    rgb = np.asarray(im.convert('RGB')).astype(np.float32) / 255
    hsv = np.asarray(im.convert('RGB').convert('HSV')).astype(np.float32)
    h, sat, val = hsv[..., 0] * 360 / 255, hsv[..., 1] / 255, hsv[..., 2] / 255
    mask = (h >= hue_from) & (h <= hue_to) & (sat > 0.25) & (val > 0.25)
    h2 = np.where(mask, target_hue, h)
    s2 = np.where(mask, np.clip(sat * sat_mul, 0, 1), sat)
    v2 = np.where(mask, np.clip(val * val_mul, 0, 1), val)
    hsv2 = np.stack([h2 * 255 / 360, s2 * 255, v2 * 255], -1).astype(np.uint8)
    rgb2 = Image.fromarray(hsv2, 'HSV').convert('RGB')
    result = Image.merge('RGBA', (*rgb2.split(), im.split()[3]))
    if overlay:
        overlay(result)
    result.save(os.path.join(OUT, out + '.png'))
    print(out, result.size, int(mask.sum()))


def academy():
    def emblem(im):
        d = ImageDraw.Draw(im)
        w, h = im.size
        cx, cy, r = w * 0.5, h * 0.2, w * 0.07
        # 별 문장 (지붕 위 둥근 판)
        d.ellipse((cx - r * 1.4, cy - r * 1.4, cx + r * 1.4, cy + r * 1.4), fill=(255, 246, 226, 255), outline=COCOA, width=max(4, int(w * 0.012)))
        pts = []
        for i in range(10):
            a = math.radians(-90 + i * 36)
            rr = r if i % 2 == 0 else r * 0.45
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        d.polygon(pts, fill=(255, 214, 110, 255), outline=COCOA)
    recolor(os.path.join(SETTLEMENT, 'Building_Archive.png'), 'Academy', 300, 360, 236, 0.9, 0.9, emblem)


def palace():
    recolor(os.path.join(SETTLEMENT, 'Prop_Castle.png'), 'Palace', 190, 250, 44, 1.05, 1.1)


def fountain():
    im, d = canvas(560, 460)
    shadow(d, 280, 420, 240, 34)
    # 아래 둥근 돌 수반 (앞면 · 물 · 테두리)
    d.ellipse(s(40, 250, 520, 410), fill=STONE_FRONT, outline=COCOA, width=LINE)
    d.rectangle(s(40, 300, 520, 330), fill=STONE_FRONT)
    d.line(s(40, 300, 40, 330), fill=COCOA, width=LINE)
    d.line(s(520, 300, 520, 330), fill=COCOA, width=LINE)
    ellipse(d, (40, 220, 520, 360), STONE_TOP)
    ellipse(d, (76, 240, 484, 342), WATER)
    for x0 in (130, 300):
        d.arc(s(x0, 276, x0 + 110, 310), 200, 340, fill=(230, 248, 255, 255), width=5 * S)
    # 가운데 기둥 · 작은 수반
    rrect(d, (250, 150, 310, 300), 16, STONE_FRONT)
    ellipse(d, (180, 120, 380, 186), STONE_TOP)
    ellipse(d, (204, 132, 356, 172), WATER_DEEP)
    # 물줄기 (위로 솟아 양쪽으로)
    for side in (-1, 1):
        d.arc(s(280 - 10 + min(0, side * 120), 40, 280 + 10 + max(0, side * 120), 200), 200 if side < 0 else 270, 270 if side < 0 else 340,
              fill=WATER, width=10 * S)
    d.line(s(280, 150, 280, 50), fill=WATER, width=12 * S)
    ellipse(d, (262, 30, 298, 66), (230, 248, 255, 255), 6)
    # 물방울
    for x, y in ((150, 210), (410, 214), (196, 196), (366, 198)):
        ellipse(d, (x - 10, y - 10, x + 10, y + 10), WATER, 6)
    finish(im, 'Prop_Fountain')


def flowerbed():
    im, d = canvas(560, 300)
    shadow(d, 280, 262, 250, 26)
    # 돌 테두리 화단
    rrect(d, (30, 150, 530, 250), 40, STONE_FRONT)
    rrect(d, (30, 110, 530, 200), 40, STONE_TOP)
    rrect(d, (58, 124, 502, 186), 30, (150, 104, 70, 255), 8)
    # 꽃 (잎 + 꽃잎)
    colors = [(247, 160, 170, 255), (255, 214, 110, 255), (196, 160, 236, 255), (255, 255, 255, 255)]
    for i, x in enumerate(range(90, 500, 52)):
        y = 120 - (i % 2) * 24
        d.line(s(x, y + 50, x, y + 10), fill=(110, 170, 80, 255), width=6 * S)
        ellipse(d, (x - 30, y + 26, x - 4, y + 46), LEAF, 6)
        ellipse(d, (x + 4, y + 22, x + 30, y + 42), LEAF, 6)
        c = colors[i % len(colors)]
        for a in range(0, 360, 72):
            r = math.radians(a)
            ellipse(d, (x + 16 * math.cos(r) - 13, y + 16 * math.sin(r) - 13, x + 16 * math.cos(r) + 13, y + 16 * math.sin(r) + 13), c, 6)
        ellipse(d, (x - 9, y - 9, x + 9, y + 9), (255, 220, 120, 255), 5)
    finish(im, 'Prop_Flowerbed')


def town_house_previews():
    """개량 주택 + 현관 등불(왼쪽) · 꽃 화분(오른쪽) — 창 미리보기용"""
    lamp = Image.open(os.path.join(SETTLEMENT, 'ICON_Lamp.png')).convert('RGBA')
    pot = Image.open(os.path.join(SETTLEMENT, 'Prop_FlowerPot.png')).convert('RGBA')
    for color in ('Red', 'Blue'):
        house = Image.open(os.path.join(ROOT, 'Assets', 'Art', 'Buildings', 'Detail4', f'House{color}.png')).convert('RGBA')
        house = house.crop(house.getbbox())
        w, h = house.size
        out = Image.new('RGBA', (w + 120, h + 40), (0, 0, 0, 0))
        out.alpha_composite(house, (60, 0))
        l = lamp.crop(lamp.getbbox())
        l = l.resize((int(w * 0.22), int(w * 0.22 * l.height / l.width)), Image.LANCZOS)
        out.alpha_composite(l, (8, h + 40 - l.height - 6))
        f = pot.crop(pot.getbbox())
        f = f.resize((int(w * 0.24), int(w * 0.24 * f.height / f.width)), Image.LANCZOS)
        out.alpha_composite(f, (w + 120 - f.width - 4, h + 40 - f.height - 2))
        out = out.crop(out.getbbox())
        path = os.path.join(OUT, f'TownHouse_{color}.png')
        out.thumbnail((512, 512), Image.LANCZOS)
        out.save(path)
        print(path, out.size)


def main():
    os.makedirs(OUT, exist_ok=True)
    kiln()
    for level in range(3):
        ingot_pile(level)
        precision_pile(level)
    cart('Ingot')
    cart('Precision')
    cart_pennant()
    banner_pole('Prop_RoyalBanner', (124, 92, 196, 255), lambda d, x, y: crown(d, x, y, 1.0))
    banner_pole('Prop_CapitalFlag', (92, 140, 214, 255), shell)
    academy()
    palace()
    fountain()
    flowerbed()
    town_house_previews()
    sawhorse()
    stone_bench()
    for level in range(3):
        plank_pile(level)
        stone_pile(level)
    for load in ('Empty', 'Plank', 'StoneBlock'):
        cart(load)
    warehouse_crates()
    kingdom_sign()


if __name__ == '__main__':
    main()
