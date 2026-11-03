"""번영의 시대 광장 소품 (스티커 스타일: 코코아 외곽선 · 따뜻한 나무색). 정식 그림을 받으면 바꿈.

python Tools/UIGen/industry_art.py → Assets/Art/Industry/
  Prop_Sawhorse          목공소 제재 작업대 (I01)
  Prop_StoneBench        채석 작업소 돌 재단대 (I02)
  Pile_Plank_0..2        시설에 쌓인 판재 (적음 · 보통 · 많음)
  Pile_StoneBlock_0..2   시설에 쌓인 석재 블록
  Cart_Empty / Cart_Plank / Cart_StoneBlock   수레 (오른쪽으로 감, 손잡이는 왼쪽)
  Prop_WarehouseCrates   왕국 창고 자리 (상자 더미 + 표지)
  Prop_KingdomSign       왕국 표지판 (C01 꾸미기)
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


def main():
    os.makedirs(OUT, exist_ok=True)
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
