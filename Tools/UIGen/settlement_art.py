# 정착 진행(P0) 그림: 게시판, 나뭇가지, 공사 터, 밭 표지판, 말풍선, 해달 얼굴, 아이콘.
# 스티커 스타일 (코코아 외곽선, 파스텔). 실행: python Tools/UIGen/settlement_art.py
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

import ui_gen
from ui_gen import COCOA, CREAM, PAPER, MUSTARD, CORAL

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")
PROPS = os.path.join(ROOT, "Assets", "Art", "Plaza", "Props")
SHEETS = os.path.join(ROOT, "Assets", "Sprites", "Characters")
SS = 4

WOOD = "#C48A58"; WOOD_DARK = "#9A6440"; WOOD_LIGHT = "#DDA772"
STONE = "#D3CEC6"; STONE_DARK = "#A9A39A"
DIRT = "#D9B07A"; DIRT_DARK = "#C2955F"
LEAF = "#8DBF5A"; LEAF_DARK = "#6E9E45"
RED = "#E2584C"


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def finish(im, w, h):
    return im.resize((w, h), Image.LANCZOS)


def rrect(d, box, r, fill, ow=7):
    x0, y0, x1, y1 = [v * SS for v in box]
    d.rounded_rectangle((x0, y0, x1, y1), r * SS, fill=COCOA)
    o = ow * SS
    d.rounded_rectangle((x0 + o, y0 + o, x1 - o, y1 - o), max(1, (r - ow)) * SS, fill=fill)


def poly(d, pts, fill, ow=7):
    """외곽선 다각형: 코코아로 넓게 칠한 뒤 안쪽을 채움 (선 두께 ow)"""
    p = [(x * SS, y * SS) for x, y in pts]
    d.polygon(p, fill=COCOA)
    d.line(p + [p[0]], fill=COCOA, width=ow * 2 * SS, joint="curve")
    cx = sum(x for x, _ in p) / len(p)
    cy = sum(y for _, y in p) / len(p)
    inner = []
    for x, y in p:
        dx, dy = x - cx, y - cy
        L = math.hypot(dx, dy) or 1
        inner.append((x - dx / L * ow * SS * 0.9, y - dy / L * ow * SS * 0.9))
    d.polygon(inner, fill=fill)


def stick(d, a, b, width, fill=WOOD, ow=6):
    ax, ay = a[0] * SS, a[1] * SS
    bx, by = b[0] * SS, b[1] * SS
    d.line((ax, ay, bx, by), fill=COCOA, width=(width + ow * 2) * SS)
    for x, y in ((ax, ay), (bx, by)):
        r = (width / 2 + ow) * SS
        d.ellipse((x - r, y - r, x + r, y + r), fill=COCOA)
    d.line((ax, ay, bx, by), fill=fill, width=width * SS)
    for x, y in ((ax, ay), (bx, by)):
        r = width / 2 * SS
        d.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def leaf(d, cx, cy, size, angle):
    pts = []
    for i in range(16):
        t = i / 16 * math.tau
        x = math.cos(t) * size
        y = math.sin(t) * size * 0.5
        ca, sa = math.cos(angle), math.sin(angle)
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    poly(d, pts, LEAF, ow=4)


def tuft(d, cx, cy, s=1.0):
    for dx, ang in ((-14, -2.2), (0, -1.57), (14, -0.9)):
        leaf(d, cx + dx * s, cy - 10 * s, 13 * s, ang)


# ── 게시판 ─────────────────────────────────────────────
def board():
    W, H = 320, 330
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    # 기둥
    rrect(d, (52, 120, 86, 322), 12, WOOD_DARK)
    rrect(d, (234, 120, 268, 322), 12, WOOD_DARK)
    # 판
    rrect(d, (18, 60, 302, 252), 22, WOOD)
    for y in (112, 160, 208):
        d.line((34 * SS, y * SS, 286 * SS, y * SS), fill=WOOD_DARK, width=4 * SS)
    # 지붕 판자
    poly(d, [(6, 72), (40, 26), (280, 26), (314, 72)], "#B8693F")
    d.line((40 * SS, 48 * SS, 280 * SS, 48 * SS), fill="#9C5432", width=4 * SS)
    # 종이들
    papers = [((44, 86, 126, 176), -4, PAPER), ((140, 80, 214, 150), 3, "#FFF0C8"),
              ((150, 160, 230, 232), -2, PAPER), ((228, 92, 284, 170), 5, "#E6F2D6"),
              ((52, 184, 128, 238), 2, "#FFE3D6")]
    for (x0, y0, x1, y1), rot, color in papers:
        p = Image.new("RGBA", ((x1 - x0 + 20) * SS, (y1 - y0 + 20) * SS), (0, 0, 0, 0))
        pd = ImageDraw.Draw(p)
        rrect(pd, (10, 10, x1 - x0 + 10, y1 - y0 + 10), 6, color, ow=5)
        for i in range(3):
            yy = 30 + i * 14
            if yy < y1 - y0:
                pd.line((22 * SS, yy * SS, (x1 - x0 - 4) * SS, yy * SS), fill="#C9B79F", width=3 * SS)
        p = p.rotate(rot, resample=Image.BICUBIC, expand=False)
        im.alpha_composite(p, ((x0 - 10) * SS, (y0 - 10) * SS))
    d = ImageDraw.Draw(im)
    for x, y, c in ((85, 90, RED), (177, 84, "#5B9BD5"), (190, 164, MUSTARD), (256, 96, RED), (90, 188, "#5B9BD5")):
        r = 7 * SS
        d.ellipse((x * SS - r - 3 * SS, y * SS - r - 3 * SS, x * SS + r + 3 * SS, y * SS + r + 3 * SS), fill=COCOA)
        d.ellipse((x * SS - r, y * SS - r, x * SS + r, y * SS + r), fill=c)
    # 발밑 풀
    tuft(d, 69, 322, 1.0)
    tuft(d, 251, 322, 1.0)
    return finish(im, W, H)


# ── 나뭇가지 무더기 ─────────────────────────────────────
def branches():
    W, H = 230, 150
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    stick(d, (30, 118), (196, 82), 18, WOOD)
    stick(d, (52, 72), (200, 126), 16, WOOD_LIGHT)
    stick(d, (80, 130), (150, 52), 14, WOOD_DARK)
    stick(d, (150, 60), (172, 36), 9, WOOD_DARK)
    stick(d, (110, 104), (90, 76), 8, WOOD)
    leaf(d, 176, 30, 14, -0.6)
    leaf(d, 64, 62, 13, -2.4)
    leaf(d, 206, 120, 12, 0.4)
    return finish(im, W, H)


# ── 돌무더기 (주워서 돌을 얻는 곳) ──────────────────────
def pebbles():
    W, H = 200, 130
    im = canvas(W, H)
    d = ImageDraw.Draw(im)

    def pebble(cx, cy, rx, ry, color, ow=6):
        d.ellipse(((cx - rx - ow) * SS, (cy - ry - ow) * SS, (cx + rx + ow) * SS, (cy + ry + ow) * SS), fill=COCOA)
        d.ellipse(((cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS), fill=color)
        # 아랫면 그림자 + 윗면 반짝임
        d.chord(((cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS), 20, 160, fill=STONE_DARK)
        d.ellipse(((cx - rx * 0.45) * SS, (cy - ry * 0.65) * SS, (cx - rx * 0.05) * SS, (cy - ry * 0.3) * SS), fill="#F4F2EE")

    # 뒤 → 앞 순서로 겹쳐 그림
    pebble(100, 58, 42, 30, STONE)
    pebble(54, 84, 36, 26, "#DCD7CF")
    pebble(146, 86, 38, 27, STONE)
    pebble(98, 102, 28, 19, "#E6E2DB")
    pebble(176, 112, 14, 10, "#DCD7CF", ow=5)
    pebble(22, 112, 13, 9, STONE, ow=5)
    return finish(im, W, H)


# ── 공사 터 (주춧돌 + 말뚝) ──────────────────────────────
def foundation():
    W, H = 380, 250
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    top, right, bottom, left = (190, 30), (360, 120), (190, 210), (20, 120)
    poly(d, [top, right, bottom, left], DIRT, ow=6)
    rnd = random.Random(7)
    # 흙 결
    for _ in range(26):
        x = rnd.uniform(90, 290)
        y = rnd.uniform(80, 160)
        if abs(x - 190) / 170 + abs(y - 120) / 90 < 0.8:
            d.ellipse(((x - 4) * SS, (y - 3) * SS, (x + 4) * SS, (y + 3) * SS), fill=DIRT_DARK)
    # 테두리 돌
    def edge(a, b, n):
        for i in range(n):
            t = (i + 0.5) / n
            x = a[0] + (b[0] - a[0]) * t
            y = a[1] + (b[1] - a[1]) * t
            rrect(d, (x - 15, y - 10, x + 15, y + 10), 7, STONE if i % 2 else "#E2DED7", ow=5)
    edge(left, top, 6)
    edge(top, right, 6)
    edge(left, bottom, 6)
    edge(bottom, right, 6)
    # 말뚝 (앞쪽은 나중에 그려 위에)
    for x, y in (top, left, right, bottom):
        rrect(d, (x - 10, y - 52, x + 10, y + 6), 6, WOOD_DARK, ow=5)
    # 작은 집 팻말
    rrect(d, (40, 150, 104, 196), 8, WOOD_LIGHT, ow=5)
    poly(d, [(56, 182), (56, 168), (72, 158), (88, 168), (88, 182)], WOOD_DARK, ow=3)
    return finish(im, W, H)


# ── 밭 표지판 (잠김 / 열림) ──────────────────────────────
def farm_sign(locked):
    W, H = 170, 220
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    rrect(d, (70, 90, 100, 214), 10, WOOD_DARK)
    rrect(d, (14, 20, 156, 120), 18, WOOD_LIGHT)
    c = ui_gen.carrot(84)
    im.alpha_composite(c.resize((84 * SS, 84 * SS), Image.LANCZOS), (43 * SS, 26 * SS))
    if locked:
        d = ImageDraw.Draw(im)
        # 자물쇠
        d.arc((104 * SS, 2 * SS, 150 * SS, 52 * SS), 180, 360, fill=COCOA, width=14 * SS)
        d.arc((108 * SS, 6 * SS, 146 * SS, 48 * SS), 180, 360, fill="#B8B2A8", width=6 * SS)
        rrect(d, (96, 26, 158, 76), 10, "#E8C766", ow=6)
        d.ellipse((121 * SS, 40 * SS, 133 * SS, 52 * SS), fill=COCOA)
        d.rectangle((125 * SS, 48 * SS, 129 * SS, 62 * SS), fill=COCOA)
    tuft(ImageDraw.Draw(im), 85, 214, 1.0)
    return finish(im, W, H)


# ── 말풍선 ───────────────────────────────────────────────
def bubble(kind):
    W, H = 120, 128
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    poly(d, [(46, 96), (60, 122), (74, 96)], CREAM, ow=6)
    rrect(d, (6, 6, 114, 102), 32, CREAM, ow=7)
    d.rectangle((50 * SS, 92 * SS, 70 * SS, 100 * SS), fill=CREAM)
    if kind == "alert":
        rrect(d, (50, 20, 70, 66), 10, RED, ow=0)
        d.ellipse((50 * SS, 72 * SS, 70 * SS, 92 * SS), fill=RED)
    else:  # 망치
        h = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
        hd = ImageDraw.Draw(h)
        stick(hd, (40, 82), (72, 40), 10, WOOD, ow=5)
        rrect(hd, (52, 18, 100, 44), 8, "#8E8A86", ow=5)
        h = h.rotate(-8, resample=Image.BICUBIC, center=(60 * SS, 54 * SS))
        im.alpha_composite(h)
    return finish(im, W, H)


# ── 공사 먼지 (뭉게뭉게 크림색 구름) ─────────────────────
def dust():
    W, H = 140, 110
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for cx, cy, r in ((40, 66, 30), (70, 46, 36), (102, 64, 28), (70, 76, 30)):
        d.ellipse(((cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS), fill=(243, 230, 208, 235))
    for cx, cy, r in ((62, 40, 14), (92, 56, 10)):
        d.ellipse(((cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS), fill=(255, 250, 240, 255))
    im = im.filter(ImageFilter.GaussianBlur(SS * 1.5))
    return finish(im, W, H)


# ── 해달 말풍선 (넓은 크림 말풍선, 아래 가운데 꼬리) ────
def speech_bubble():
    W, H = 340, 150
    im = canvas(W, H)
    d = ImageDraw.Draw(im)
    poly(d, [(150, 112), (170, 146), (190, 112)], CREAM, ow=6)
    rrect(d, (6, 6, 334, 118), 40, CREAM, ow=7)
    d.rectangle((152 * SS, 104 * SS, 188 * SS, 116 * SS), fill=CREAM)
    return finish(im, W, H)


# ── 해달 얼굴 (스프라이트 시트 첫 칸에서 잘라냄) ──────────
def portrait(sheet, cell, crop_box, size=256):
    src = Image.open(os.path.join(SHEETS, sheet)).convert("RGBA")
    x0, y0, w, h = cell
    c = src.crop((x0, y0, x0 + w, y0 + h)).crop(crop_box)
    c.thumbnail((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(c, ((size - c.width) // 2, (size - c.height) // 2))
    return out


def fit_icon(path, size=160):
    src = Image.open(path).convert("RGBA")
    src = src.crop(src.getbbox())
    src.thumbnail((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(src, ((size - src.width) // 2, (size - src.height) // 2))
    return out


def clearing_icon(size=160):
    """개간: 덤불 + 돌 + 통나무를 한 아이콘에"""
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bush = fit_icon(os.path.join(PROPS, "Prop_Bush.png"), 104)
    rock = fit_icon(os.path.join(PROPS, "Prop_Rock.png"), 92)
    log = fit_icon(os.path.join(PROPS, "Prop_Log.png"), 84)
    out.alpha_composite(bush, (4, 6))
    out.alpha_composite(rock, (62, 46))
    out.alpha_composite(log, (10, 78))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    save = lambda im, name: (im.save(os.path.join(OUT, name + ".png")), print("saved", name))
    save(board(), "Prop_Board")
    save(branches(), "Prop_Branches")
    save(pebbles(), "Prop_Pebbles")
    save(foundation(), "Prop_Foundation")
    save(farm_sign(True), "Prop_FarmSign_Locked")
    save(farm_sign(False), "Prop_FarmSign")
    save(bubble("alert"), "UI_Bubble_Alert")
    save(bubble("hammer"), "UI_Bubble_Hammer")
    save(dust(), "FX_Dust")
    save(speech_bubble(), "UI_Bubble_Speech")
    save(Image.new("RGBA", (8, 8), (255, 255, 255, 255)), "Mask_Square")

    # 얼굴: 방문 해달 시트는 240x260 칸, 광부는 걷기 시트 아래(앞모습) 줄
    save(portrait("Visitors/Snack.png", (0, 0, 240, 260), (20, 10, 220, 210)), "ICON_Otter_Snack")
    save(portrait("Visitors/Painter.png", (0, 0, 240, 260), (20, 10, 220, 210)), "ICON_Otter_Painter")
    save(portrait("Visitors/Sleepy.png", (0, 0, 240, 260), (20, 10, 220, 210)), "ICON_Otter_Sleepy")
    save(portrait("MinerOtter/MinerOtter_Walk.png", (0, 272 * 2, 212, 272), (6, 20, 206, 220)), "ICON_Otter_Builder")

    save(fit_icon(os.path.join(PROPS, "Prop_Log.png")), "ICON_Item_Wood")
    save(fit_icon(os.path.join(PROPS, "Prop_House_Blue.png")), "ICON_House_Blue")
    save(fit_icon(os.path.join(PROPS, "Prop_House_Red.png")), "ICON_House_Red")
    save(clearing_icon(), "ICON_Clearing")
    save(fit_icon(os.path.join(OUT, "Prop_Board.png")), "ICON_Board")


if __name__ == "__main__":
    main()
