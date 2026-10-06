"""P4 임시 아트: Lv.10~15 해달의 부탁으로 짓는 특성 건물 (꾸미기 모드에서 자리를 골라 지음).

  python Tools/UIGen/p4_buildings_art.py

- Assets/Art/Settlement/Building_Shop.png      해달 상점 (장사 · 줄무늬 차양 + 동전 간판)
- Assets/Art/Settlement/Building_Workshop.png  목공소 (나무캐기 · 통나무 더미 + 톱 간판)
- Assets/Art/Settlement/Building_Quarry.png    채석 작업소 (채굴 · 돌무더기 + 곡괭이 간판)
- Assets/Art/Settlement/Building_Granary.png   농업 창고 (농사 · 둥근 지붕 + 당근 간판)

P3 소품(p3_art.py)과 같은 아이소메트릭 · 진한 갈색 테두리 스티커 느낌의 도형 그림. 정식 아트가 나오면 같은 이름으로 바꿔 끼운다.
그림 아래 가운데가 건물의 앞 모서리(피벗 0.5, 0.02)라, 꾸미기 격자 4 × 3칸에 가로를 맞춰 그린다 (BuildingVisual).
"""
import math
from PIL import ImageDraw

from p3_art import (OUTLINE, WOOD, WOOD_LIGHT, WOOD_DARK, LEAF, LEAF_DARK, canvas, poly, line, ellipse, finish, shadow, iso_box)

W, H = 400, 420
CX, BASE = 200, 408  # 앞 모서리
HALF, DEPTH = 176, 92  # 바닥 마름모 (가로 반, 깊이 반)

WALL_L = (240, 222, 186, 255)
WALL_R = (214, 190, 150, 255)
STONE_L = (178, 178, 170, 255)
STONE_R = (146, 146, 140, 255)
STONE_TOP = (204, 204, 196, 255)
WINDOW = (126, 190, 214, 255)
DOOR = (150, 92, 56, 255)
GOLD = (246, 200, 72, 255)
GOLD_DARK = (206, 150, 40, 255)
CARROT = (240, 128, 52, 255)


def house(d, wall_l, wall_r, height):
    """바닥 마름모 위의 벽 상자 (지붕 없이). 위쪽 네 꼭짓점을 돌려줌"""
    return iso_box(d, CX, BASE, HALF, DEPTH, height, wall_l, wall_l, wall_r, planks=False)


def gable_roof(d, top, color, dark, rise):
    """박공지붕: 왼쪽 앞 처마 ~ 오른쪽 뒤 처마, 용마루가 가운데 위로"""
    tl, tf, tr, tb = top
    ridge_front = ((tl[0] + tf[0]) / 2, (tl[1] + tf[1]) / 2 - rise)
    ridge_back = ((tr[0] + tb[0]) / 2, (tr[1] + tb[1]) / 2 - rise)
    over = 14
    poly(d, [(tl[0] - over, tl[1] + 6), (tf[0], tf[1] + over), ridge_front], dark)  # 앞 박공 면 (어두움)
    poly(d, [(tf[0], tf[1] + over), (tr[0] + over, tr[1] + 6), ridge_back, ridge_front], color)
    poly(d, [(tl[0] - over, tl[1] + 6), ridge_front, ridge_back, (tb[0], tb[1] - 4)], color)
    line(d, [ridge_front, ridge_back], dark, 4)


def window(d, x, y, w=34, h=30):
    poly(d, [(x, y), (x + w, y - w * 0.52), (x + w, y - w * 0.52 + h), (x, y + h)], WINDOW, 3)
    line(d, [(x + w / 2, y - w * 0.26), (x + w / 2, y - w * 0.26 + h)], OUTLINE, 2)


def door(d, x, y, w=40, h=64):
    poly(d, [(x, y), (x + w, y + w * 0.52), (x + w, y + w * 0.52 - h), (x, y - h)], DOOR, 3)
    ellipse(d, (x + w - 12, y - h / 2, x + w - 4, y - h / 2 + 8), GOLD, True, 2)


def sign(d, x, y, r, draw_icon):
    ellipse(d, (x - r, y - r, x + r, y + r), WOOD_LIGHT, True, 4)
    draw_icon(d, x, y)


def coin_icon(d, x, y):
    ellipse(d, (x - 16, y - 16, x + 16, y + 16), GOLD, True, 3)
    line(d, [(x, y - 8), (x, y + 8)], GOLD_DARK, 4)


def saw_icon(d, x, y):
    poly(d, [(x - 18, y + 6), (x + 14, y - 10), (x + 18, y - 2), (x - 14, y + 14)], (200, 206, 214, 255), 3)
    for i in range(4):
        t = i / 3
        bx, by = x - 14 + 28 * t, y + 12 - 14 * t
        line(d, [(bx, by), (bx + 3, by + 5)], OUTLINE, 2)
    poly(d, [(x + 12, y - 12), (x + 22, y - 18), (x + 26, y - 8), (x + 16, y - 2)], WOOD_DARK, 3)


def pick_icon(d, x, y):
    line(d, [(x - 12, y + 16), (x + 10, y - 14)], WOOD_DARK, 6)
    poly(d, [(x - 4, y - 18), (x + 22, y - 12), (x + 18, y - 6), (x - 2, y - 10)], (190, 196, 204, 255), 3)


def carrot_icon(d, x, y):
    poly(d, [(x - 6, y - 8), (x + 10, y - 4), (x - 6, y + 18)], CARROT, 3)
    for dx in (-6, 0, 6):
        line(d, [(x + 2, y - 8), (x + 2 + dx, y - 20)], LEAF_DARK, 4)


def awning(d, top, colors):
    """앞 오른쪽 벽 위의 줄무늬 차양"""
    tl, tf, tr, tb = top
    n = 6
    for i in range(n):
        a = (tf[0] + (tr[0] - tf[0]) * i / n, tf[1] + (tr[1] - tf[1]) * i / n)
        b = (tf[0] + (tr[0] - tf[0]) * (i + 1) / n, tf[1] + (tr[1] - tf[1]) * (i + 1) / n)
        poly(d, [(a[0], a[1] + 40), (b[0], b[1] + 40), (b[0] + 10, b[1] + 72), (a[0] + 10, a[1] + 72)], colors[i % 2], 3)


def logs(d, x, y, n=3):
    for i in range(n):
        lx, ly = x + i * 12, y - i * 22
        poly(d, [(lx - 40, ly + 10), (lx + 30, ly - 24), (lx + 38, ly - 10), (lx - 32, ly + 24)], WOOD, 3)
        ellipse(d, (lx + 26, ly - 28, lx + 44, ly - 6), WOOD_LIGHT, True, 3)


def rocks(d, x, y):
    for dx, dy, r in [(-24, 6, 20), (10, 0, 24), (-2, -22, 18), (30, -14, 14)]:
        poly(d, [(x + dx - r, y + dy), (x + dx - r * 0.4, y + dy - r), (x + dx + r * 0.6, y + dy - r * 0.8),
                 (x + dx + r, y + dy), (x + dx + r * 0.3, y + dy + r * 0.5)], STONE_L, 3)


def crates(d, x, y):
    iso_box(d, x, y, 26, 14, 26, WOOD_LIGHT, WOOD, WOOD_DARK)
    iso_box(d, x + 30, y - 8, 22, 12, 22, WOOD_LIGHT, WOOD, WOOD_DARK)


def shop():
    img = canvas(W, H)
    d = ImageDraw.Draw(img)
    shadow(d, CX, BASE - DEPTH, HALF + 10, DEPTH + 10)
    top = house(d, WALL_L, WALL_R, 150)
    gable_roof(d, top, (232, 96, 88, 255), (184, 66, 62, 255), 92)
    door(d, CX - 92, BASE - 46)
    window(d, CX - 150, BASE - 132)
    awning(d, top, [(255, 255, 255, 255), (232, 96, 88, 255)])
    crates(d, CX + 92, BASE - 18)
    sign(d, CX + 62, top[1][1] - 34, 28, coin_icon)
    finish(img, "Building_Shop.png")


def workshop():
    img = canvas(W, H)
    d = ImageDraw.Draw(img)
    shadow(d, CX, BASE - DEPTH, HALF + 10, DEPTH + 10)
    top = house(d, (226, 176, 112, 255), (196, 140, 82, 255), 140)
    for i in range(1, 6):  # 판자 무늬
        y = 140 * i / 6
        line(d, [(CX - HALF, BASE - DEPTH - y), (CX, BASE - y), (CX + HALF, BASE - DEPTH - y)], WOOD_DARK, 2)
    gable_roof(d, top, (126, 98, 76, 255), (92, 70, 54, 255), 86)
    door(d, CX - 96, BASE - 44, 52, 74)
    window(d, CX + 40, BASE - 118)
    logs(d, CX + 96, BASE - 6)
    sign(d, CX - 40, top[1][1] - 36, 28, saw_icon)
    finish(img, "Building_Workshop.png")


def quarry():
    img = canvas(W, H)
    d = ImageDraw.Draw(img)
    shadow(d, CX, BASE - DEPTH, HALF + 10, DEPTH + 10)
    top = house(d, STONE_L, STONE_R, 130)
    for i in range(1, 4):  # 돌 줄눈
        y = 130 * i / 4
        line(d, [(CX - HALF, BASE - DEPTH - y), (CX, BASE - y), (CX + HALF, BASE - DEPTH - y)], (120, 120, 114, 255), 2)
    gable_roof(d, top, (120, 132, 150, 255), (88, 98, 114, 255), 80)
    door(d, CX - 92, BASE - 46)
    window(d, CX + 50, BASE - 112)
    rocks(d, CX + 100, BASE - 14)
    sign(d, CX - 40, top[1][1] - 34, 28, pick_icon)
    finish(img, "Building_Quarry.png")


def granary():
    img = canvas(W, H)
    d = ImageDraw.Draw(img)
    shadow(d, CX, BASE - DEPTH, HALF + 10, DEPTH + 10)
    top = house(d, (236, 206, 140, 255), (208, 172, 104, 255), 150)
    gable_roof(d, top, (112, 176, 66, 255), (66, 128, 46, 255), 110)
    door(d, CX - 100, BASE - 40, 60, 80)
    window(d, CX + 50, BASE - 124)
    # 앞의 자루 두 개
    for dx in (70, 112):
        ellipse(d, (CX + dx - 22, BASE - 70, CX + dx + 22, BASE - 22), (232, 214, 170, 255), True, 3)
        line(d, [(CX + dx - 8, BASE - 66), (CX + dx + 8, BASE - 66)], WOOD_DARK, 3)
    sign(d, CX - 40, top[1][1] - 36, 28, carrot_icon)
    finish(img, "Building_Granary.png")


if __name__ == "__main__":
    shop()
    workshop()
    quarry()
    granary()
