"""P3 임시 아트: 공동사업 소품과 광장 동쪽 확장 바닥.

  python Tools/UIGen/p3_art.py

- Assets/Art/Settlement/Prop_SupplyBox.png      비축 상자 (나무 상자 + 목재·사과)
- Assets/Art/Settlement/Prop_CommunityTable.png 공동 식탁 (나무 식탁 + 피크닉 천·바구니를 위에 얹음)
- Assets/Art/Settlement/Prop_FlowerPot.png      환영 화분
- Assets/Art/Settlement/Prop_Clothesline.png    환영 빨랫줄
- Assets/Art/Plaza/Plaza_Ground_East.png        동쪽 확장 바닥 = 기존 바닥 오른쪽 절반을 좌우로 뒤집음 (이음매가 그대로 이어짐)

기존 소품과 같은 아이소메트릭 · 진한 갈색 테두리 스티커 느낌으로 도형을 그린 임시 그림이다 (정식 아트가 나오면 같은 이름으로 바꿔 끼움).
4배로 그린 뒤 줄여 테두리를 부드럽게 한다.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Art")
SS = 4  # 슈퍼샘플

OUTLINE = (75, 46, 34, 255)
WOOD = (226, 156, 80, 255)
WOOD_LIGHT = (241, 190, 112, 255)
WOOD_DARK = (178, 104, 52, 255)
POT = (214, 112, 74, 255)
POT_DARK = (168, 78, 50, 255)
SOIL = (110, 72, 48, 255)
LEAF = (112, 176, 66, 255)
LEAF_DARK = (66, 128, 46, 255)
SHADOW = (40, 30, 20, 70)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def scale(points):
    return [(x * SS, y * SS) for x, y in points]


def poly(draw, points, fill, width=4):
    draw.polygon(scale(points), fill=fill, outline=OUTLINE, width=width * SS)


def line(draw, points, fill, width):
    draw.line(scale(points), fill=fill, width=width * SS, joint="curve")


def ellipse(draw, box, fill, outline=True, width=4):
    x0, y0, x1, y1 = box
    draw.ellipse((x0 * SS, y0 * SS, x1 * SS, y1 * SS), fill=fill, outline=OUTLINE if outline else None,
                 width=width * SS if outline else 0)


def finish(img, name, folder="Settlement"):
    w, h = img.size
    out = img.resize((w // SS, h // SS), Image.LANCZOS)
    path = os.path.join(ART, folder, name)
    out.save(path)
    print("saved", os.path.relpath(path, ROOT), out.size)


def shadow(draw, cx, cy, rx, ry):
    draw.ellipse(((cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS), fill=SHADOW)


def iso_box(draw, cx, base_y, half_w, depth, height, top, left, right, planks=True):
    """아이소메트릭 상자: 앞 모서리가 (cx, base_y)."""
    front = (cx, base_y)
    lb = (cx - half_w, base_y - depth)
    rb = (cx + half_w, base_y - depth)
    back = (cx, base_y - 2 * depth)
    up = lambda p: (p[0], p[1] - height)
    poly(draw, [lb, front, up(front), up(lb)], left)
    poly(draw, [front, rb, up(rb), up(front)], right)
    poly(draw, [up(lb), up(front), up(rb), up(back)], top)
    if planks:
        for t in (0.33, 0.66):
            y = height * t
            line(draw, [(lb[0], lb[1] - y), (front[0], front[1] - y), (rb[0], rb[1] - y)], WOOD_DARK, 3)
    return up(lb), up(front), up(rb), up(back)


def supply_box():
    img = canvas(240, 230)
    d = ImageDraw.Draw(img)
    shadow(d, 120, 196, 104, 26)
    tl, tf, tr, tb = iso_box(d, 120, 214, 100, 52, 78, WOOD_LIGHT, WOOD, WOOD_DARK)
    # 위에 쌓인 목재 두 개와 사과
    for i, (x, y) in enumerate([(96, 92), (132, 80)]):
        poly(d, [(x - 34, y + 8), (x + 30, y - 12), (x + 36, y + 2), (x - 28, y + 22)], WOOD, 3)
        ellipse(d, (x + 26, y - 14, x + 42, y + 4), (238, 196, 120, 255), True, 3)
    for x, y in [(150, 98), (170, 108)]:
        ellipse(d, (x - 13, y - 13, x + 13, y + 13), (222, 64, 58, 255), True, 3)
        line(d, [(x, y - 12), (x + 3, y - 20)], OUTLINE, 3)
    finish(img, "Prop_SupplyBox.png")


def community_table():
    picnic = Image.open(os.path.join(ART, "Plaza", "Props", "Prop_Picnic.png")).convert("RGBA")
    w, h = 440, 330
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    cx, base = w // 2, h - 30
    shadow(d, cx, base - 40, 190, 40)
    # 다리 네 개
    for x, y in [(cx - 150, base - 70), (cx + 150, base - 70), (cx - 30, base - 8), (cx + 30, base - 8)]:
        poly(d, [(x - 9, y - 70), (x + 9, y - 70), (x + 9, y), (x - 9, y)], WOOD_DARK, 3)
    # 상판 (두께 있는 마름모)
    half_w, depth, thick, lift = 200, 92, 16, 78
    front = (cx, base - lift)
    lb = (cx - half_w, front[1] - depth)
    rb = (cx + half_w, front[1] - depth)
    back = (cx, front[1] - 2 * depth)
    poly(d, [lb, front, (front[0], front[1] + thick), (lb[0], lb[1] + thick)], WOOD, 3)
    poly(d, [front, rb, (rb[0], rb[1] + thick), (front[0], front[1] + thick)], WOOD_DARK, 3)
    poly(d, [lb, front, rb, back], WOOD_LIGHT, 4)
    img_small = img.resize((w, h), Image.LANCZOS)
    # 피크닉 천·바구니를 상판에 얹음 (천의 마름모가 상판과 같은 방향)
    cloth_w = int(half_w * 2 * 0.92)
    cloth = picnic.resize((cloth_w, int(picnic.height * cloth_w / picnic.width)), Image.LANCZOS)
    top_center_y = front[1] - depth
    img_small.alpha_composite(cloth, (cx - cloth_w // 2, int(top_center_y - cloth.height * 0.62)))
    path = os.path.join(ART, "Settlement", "Prop_CommunityTable.png")
    img_small.save(path)
    print("saved", os.path.relpath(path, ROOT), img_small.size)


def flower(draw, x, y, color, r=9):
    for dx, dy in [(-r, 0), (r, 0), (0, -r), (0, r)]:
        ellipse(draw, (x + dx - r, y + dy - r, x + dx + r, y + dy + r), color, True, 2)
    ellipse(draw, (x - 6, y - 6, x + 6, y + 6), (250, 214, 90, 255), True, 2)


def flower_pot():
    img = canvas(150, 190)
    d = ImageDraw.Draw(img)
    shadow(d, 75, 172, 52, 12)
    # 잎
    for box in [(28, 40, 78, 112), (70, 34, 122, 108), (46, 18, 104, 92)]:
        ellipse(d, box, LEAF, True, 3)
    line(d, [(75, 100), (60, 56)], LEAF_DARK, 3)
    line(d, [(75, 100), (96, 52)], LEAF_DARK, 3)
    flower(d, 52, 52, (244, 140, 172, 255))
    flower(d, 98, 46, (255, 255, 255, 255))
    flower(d, 76, 30, (244, 140, 172, 255))
    # 화분
    poly(d, [(36, 104), (114, 104), (104, 172), (46, 172)], POT, 4)
    poly(d, [(28, 92), (122, 92), (122, 112), (28, 112)], POT_DARK, 4)
    ellipse(d, (34, 86, 116, 100), SOIL, True, 3)
    finish(img, "Prop_FlowerPot.png")


def clothesline():
    img = canvas(320, 250)
    d = ImageDraw.Draw(img)
    shadow(d, 160, 226, 140, 16)
    # 기둥
    for x in (36, 284):
        poly(d, [(x - 8, 60), (x + 8, 60), (x + 8, 230), (x - 8, 230)], WOOD_DARK, 3)
        poly(d, [(x - 20, 56), (x + 20, 56), (x + 20, 68), (x - 20, 68)], WOOD, 3)
    # 줄 (살짝 처짐)
    def sag(x):
        t = (x - 36) / 248.0
        return 66 + 18 * (1 - (2 * t - 1) ** 2)

    line(d, [(36 + i * 8, sag(36 + i * 8)) for i in range(32)], (238, 232, 216, 255), 3)

    # 옷: 셔츠 · 수건 · 작은 양말
    x = 92
    y = sag(x)
    poly(d, [(x - 34, y), (x + 34, y), (x + 46, y + 22), (x + 30, y + 30), (x + 28, y + 86), (x - 28, y + 86),
             (x - 30, y + 30), (x - 46, y + 22)], (120, 180, 232, 255), 3)
    x = 168
    y = sag(x)
    poly(d, [(x - 28, y), (x + 28, y), (x + 28, y + 70), (x - 28, y + 70)], (250, 214, 110, 255), 3)
    line(d, [(x - 28, y + 54), (x + 28, y + 54)], (226, 160, 70, 255), 3)
    x = 234
    y = sag(x)
    poly(d, [(x - 12, y), (x + 12, y), (x + 12, y + 40), (x + 22, y + 52), (x + 6, y + 58), (x - 12, y + 44)],
         (244, 140, 172, 255), 3)
    for px in (58, 126, 142, 194, 222, 246):
        ellipse(d, (px - 4, sag(px) - 6, px + 4, sag(px) + 4), WOOD_DARK, False)
    finish(img, "Prop_Clothesline.png")


def ground_east():
    """기존 바닥의 오른쪽 절반을 좌우로 뒤집어 동쪽에 붙일 조각 (왼쪽 끝 = 기존 바닥 오른쪽 끝)."""
    ground = Image.open(os.path.join(ART, "Plaza", "Plaza_Ground.png")).convert("RGBA")
    w, h = ground.size
    east = ground.crop((w // 2, 0, w, h)).transpose(Image.FLIP_LEFT_RIGHT)
    path = os.path.join(ART, "Plaza", "Plaza_Ground_East.png")
    east.save(path)
    print("saved", os.path.relpath(path, ROOT), east.size)


if __name__ == "__main__":
    supply_box()
    community_table()
    flower_pot()
    clothesline()
    ground_east()
