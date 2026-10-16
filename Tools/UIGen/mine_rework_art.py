"""광산 개편(테스트 씬) 그림 — 광석 · 화석 · 곡괭이 아이콘, 바위 광맥, 효과 그림.

스티커 스타일(코코아 #4B2E22 외곽선, 파스텔, 왼쪽 위 빛). 4배로 그려 줄여서 매끈하게.
임시 그림이므로 GPT 그림이 오면 같은 파일 이름으로 덮어쓰면 된다 (Docs/광산_개편_기획.md 7장).

python Tools/UIGen/mine_rework_art.py          (이미 있는 아이콘은 그대로, 광맥 · 효과만 다시)
광맥을 GPT 그림으로: Assets/Art/MineRework/Veins/src/<copper|iron|silver|crystal|gold|pearl|ruby|diamond|rainbow>.png
(투명 배경 광석 덩어리 스티커 하나)를 넣고 이 스크립트 → Unity Tools/Mine Rework/Build Test Scene
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "MineRework")
# 광산 바위 (GPT 그림이 있으면 그것, 없으면 광장 바위)
CUSTOM_ROCK = os.path.join(ROOT, "Assets", "Art", "MineRework", "Rocks", "ROCK_0.png")
ROCK = CUSTOM_ROCK if os.path.exists(CUSTOM_ROCK) else os.path.join(ROOT, "Assets", "Art", "Settlement", "Prop_Rock_0.png")

SS = 4
COCOA = (75, 46, 34, 255)
LIGHT = (-0.6, -0.8)  # 빛이 오는 쪽 (왼쪽 위)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def shade(color, k):
    """k > 0 밝게(흰색 쪽), k < 0 어둡게(코코아 쪽)"""
    base = color[:3]
    if k >= 0:
        out = lerp(base, (255, 255, 255), k)
    else:
        out = lerp(base, (60, 36, 40), -k)
    return out + (255,)


def canvas(size):
    return Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))


def finish(img, size):
    return img.resize((size, size), Image.LANCZOS)


def save(img, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("그림", os.path.relpath(path, ROOT))


def poly_outline(d, pts, width, color=COCOA):
    d.line(pts + [pts[0]], fill=color, width=width, joint="curve")
    r = width / 2
    for x, y in pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)


# ---------------------------------------------------------------------------
# 깎은 덩어리 (광석 · 보석). 바깥 다각형 + 안쪽 윗면, 면마다 빛 방향으로 밝기

def faceted(d, cx, cy, radius, color, sides=9, seed=1, jitter=0.16, squash=0.86, inner=0.52, outline=None,
            regular=False, rotate=0.0, top_lift=0.1):
    rng = random.Random(seed)
    outer = []
    for i in range(sides):
        a = rotate + i / sides * math.tau
        r = radius * (1 if regular else rng.uniform(1 - jitter, 1 + jitter * 0.5))
        outer.append((cx + math.cos(a) * r, cy + math.sin(a) * r * squash))
    ix, iy = cx - radius * 0.1, cy - radius * top_lift
    inn = [(ix + (x - cx) * inner, iy + (y - cy) * inner) for x, y in outer]
    # 그림자 (바닥)
    d.ellipse([cx - radius * 0.9, cy + radius * squash * 0.72, cx + radius * 0.9, cy + radius * squash * 1.08],
              fill=(60, 36, 40, 50))
    for i in range(sides):
        j = (i + 1) % sides
        mx = (outer[i][0] + outer[j][0]) / 2 - cx
        my = (outer[i][1] + outer[j][1]) / 2 - cy
        n = math.hypot(mx, my) or 1
        lit = (mx / n) * LIGHT[0] + (my / n) * LIGHT[1]
        d.polygon([outer[i], outer[j], inn[j], inn[i]], fill=shade(color, 0.35 * lit if lit > 0 else 0.42 * lit))
    d.polygon(inn, fill=shade(color, 0.32))
    w = outline or max(6, int(radius * 0.11))
    line = (75, 46, 34, 120)
    for i in range(sides):
        d.line([outer[i], inn[i]], fill=line, width=max(2, w // 3))
    poly_outline(d, inn, max(2, w // 3), line)
    poly_outline(d, outer, w)
    return outer, inn


def highlight(d, x, y, w, h, alpha=210):
    d.ellipse([x, y, x + w, y + h], fill=(255, 255, 255, alpha))


def sparkle(d, x, y, r, color=(255, 255, 255, 255)):
    pts = []
    for i in range(8):
        a = i / 8 * math.tau - math.pi / 2
        rr = r if i % 2 == 0 else r * 0.28
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    d.polygon(pts, fill=color)


# ---------------------------------------------------------------------------
# 광석 아이콘

def ore_nugget(color, seed, deco=None, size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    outer, inn = faceted(d, S * 0.5, S * 0.54, S * 0.36, color, sides=9, seed=seed)
    xs = [p[0] for p in inn]
    ys = [p[1] for p in inn]
    tx, ty, tw, th = min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys)
    if deco:
        deco(d, tx, ty, tw, th, color)
    highlight(d, tx + tw * 0.12, ty + th * 0.1, tw * 0.28, th * 0.16)
    sparkle(d, S * 0.76, S * 0.24, S * 0.07)
    return finish(img, size)


def deco_shell(d, x, y, w, h, color):
    # 조개 무늬: 아래에서 퍼지는 줄
    bx, by = x + w * 0.5, y + h * 0.95
    for k in range(-2, 3):
        a = math.radians(-90 + k * 22)
        d.line([(bx, by), (bx + math.cos(a) * w * 0.48, by + math.sin(a) * h * 0.85)], fill=shade(color, -0.35), width=7)


def deco_wave(d, x, y, w, h, color):
    for row in range(2):
        pts = []
        yy = y + h * (0.42 + row * 0.3)
        for i in range(21):
            t = i / 20
            pts.append((x + w * (0.08 + t * 0.84), yy + math.sin(t * math.tau * 1.5) * h * 0.07))
        d.line(pts, fill=shade(color, -0.4), width=8, joint="curve")


def deco_moon(d, x, y, w, h, color):
    cx, cy, r = x + w * 0.55, y + h * 0.52, min(w, h) * 0.3
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 250, 220, 255))
    d.ellipse([cx - r * 0.55, cy - r * 1.05, cx + r * 1.35, cy + r * 0.85], fill=shade(color, 0.32))


def deco_sun(d, x, y, w, h, color):
    cx, cy, r = x + w * 0.52, y + h * 0.52, min(w, h) * 0.2
    for i in range(8):
        a = i / 8 * math.tau
        d.line([(cx + math.cos(a) * r * 1.4, cy + math.sin(a) * r * 1.4), (cx + math.cos(a) * r * 2.1, cy + math.sin(a) * r * 2.1)],
               fill=(255, 245, 190, 255), width=9)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 248, 200, 255))


def crystal_prism(d, cx, by, w, h, color, tilt=0.0):
    """끝이 뾰족한 육각 기둥 하나 (아래 by가 바닥)"""
    t = math.tan(tilt)
    top = (cx + t * h, by - h)
    l_shoulder = (cx - w / 2 + t * h * 0.72, by - h * 0.72)
    r_shoulder = (cx + w / 2 + t * h * 0.72, by - h * 0.72)
    mid_shoulder = (cx + w * 0.08 + t * h * 0.78, by - h * 0.78)
    bl, br, bm = (cx - w / 2, by), (cx + w / 2, by), (cx + w * 0.08, by + w * 0.06)
    d.polygon([l_shoulder, mid_shoulder, bm, bl], fill=shade(color, 0.28))
    d.polygon([mid_shoulder, r_shoulder, br, bm], fill=shade(color, -0.18))
    d.polygon([top, mid_shoulder, l_shoulder], fill=shade(color, 0.55))
    d.polygon([top, r_shoulder, mid_shoulder], fill=shade(color, 0.1))
    pts = [top, r_shoulder, br, bm, bl, l_shoulder]
    line = (75, 46, 34, 110)
    d.line([top, mid_shoulder, bm], fill=line, width=4)
    d.line([l_shoulder, mid_shoulder, r_shoulder], fill=line, width=4)
    poly_outline(d, pts, 9)
    highlight(d, l_shoulder[0] + w * 0.1, l_shoulder[1] + h * 0.05, w * 0.14, h * 0.32, 170)


def ore_crystal(color, size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.16, S * 0.74, S * 0.84, S * 0.9], fill=(60, 36, 40, 50))
    faceted(d, S * 0.5, S * 0.76, S * 0.3, (176, 160, 168, 255), sides=8, seed=4, squash=0.42, inner=0.6, top_lift=0.02)
    crystal_prism(d, S * 0.33, S * 0.78, S * 0.17, S * 0.42, color, tilt=-0.38)
    crystal_prism(d, S * 0.68, S * 0.8, S * 0.16, S * 0.36, color, tilt=0.42)
    crystal_prism(d, S * 0.5, S * 0.8, S * 0.22, S * 0.6, color, tilt=0.02)
    sparkle(d, S * 0.74, S * 0.2, S * 0.07)
    sparkle(d, S * 0.24, S * 0.36, S * 0.04)
    return finish(img, size)


def ore_gem(color, sides=8, size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    outer, inn = faceted(d, S * 0.5, S * 0.52, S * 0.36, color, sides=sides, regular=True, rotate=math.pi / sides,
                         squash=0.9, inner=0.56, top_lift=0.04)
    xs = [p[0] for p in inn]
    ys = [p[1] for p in inn]
    highlight(d, min(xs) + (max(xs) - min(xs)) * 0.14, min(ys) + (max(ys) - min(ys)) * 0.12,
              (max(xs) - min(xs)) * 0.3, (max(ys) - min(ys)) * 0.18)
    sparkle(d, S * 0.75, S * 0.22, S * 0.08)
    sparkle(d, S * 0.3, S * 0.74, S * 0.04)
    return finish(img, size)


def scallop(d, cx, cy, r, colors, ribs=7, rainbow=False):
    """부채꼴 조개 (아래 꼭지 cx, cy+r*0.55)"""
    hinge = (cx, cy + r * 0.62)
    top = []
    n = ribs * 6
    for i in range(n + 1):
        t = i / n
        a = math.radians(200 + t * 140)
        bump = 1 + 0.05 * math.cos(t * ribs * math.tau)
        top.append((cx + math.cos(a) * r * bump, cy + math.sin(a) * r * bump * 0.95 + r * 0.1))
    for k in range(ribs):
        a0 = top[k * 6]
        a1 = top[(k + 1) * 6]
        seg = [hinge] + top[k * 6:(k + 1) * 6 + 1]
        if rainbow:
            hue = k / ribs
            col = hsv(hue, 0.42, 1.0)
        else:
            col = shade(colors, 0.25 if k % 2 == 0 else 0.0)
        d.polygon(seg, fill=col)
    for k in range(ribs + 1):
        d.line([hinge, top[min(k * 6, n)]], fill=(75, 46, 34, 110), width=5)
    # 꼭지 귀
    ear = [(hinge[0] - r * 0.28, hinge[1] - r * 0.02), (hinge[0] + r * 0.28, hinge[1] - r * 0.02),
           (hinge[0] + r * 0.2, hinge[1] + r * 0.2), (hinge[0] - r * 0.2, hinge[1] + r * 0.2)]
    d.polygon(ear, fill=shade(colors, -0.1) if not rainbow else (230, 210, 240, 255))
    poly_outline(d, ear, 9)
    poly_outline(d, [hinge] + top, 10)


def hsv(h, s, v):
    i = int(h * 6) % 6
    f = h * 6 - int(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    r, g, b = [(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)][i]
    return (int(r * 255), int(g * 255), int(b * 255), 255)


def ore_pearl(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.14, S * 0.76, S * 0.86, S * 0.92], fill=(60, 36, 40, 50))
    scallop(d, S * 0.5, S * 0.56, S * 0.36, (200, 178, 232, 255))
    # 진주
    r = S * 0.17
    cx, cy = S * 0.5, S * 0.5
    for k in range(12, 0, -1):
        t = k / 12
        col = lerp((236, 228, 250), (255, 255, 255), 1 - t)
        rr = r * t
        ox, oy = -r * 0.25 * (1 - t), -r * 0.3 * (1 - t)
        d.ellipse([cx - rr + ox, cy - rr + oy, cx + rr + ox, cy + rr + oy], fill=col + (255,))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=COCOA, width=9)
    highlight(d, cx - r * 0.6, cy - r * 0.62, r * 0.55, r * 0.4, 230)
    sparkle(d, S * 0.76, S * 0.24, S * 0.07)
    return finish(img, size)


def ore_rainbow(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.14, S * 0.78, S * 0.86, S * 0.93], fill=(60, 36, 40, 50))
    scallop(d, S * 0.5, S * 0.5, S * 0.4, (255, 255, 255, 255), ribs=7, rainbow=True)
    highlight(d, S * 0.26, S * 0.26, S * 0.16, S * 0.08, 200)
    for x, y, r in [(0.78, 0.2, 0.08), (0.2, 0.3, 0.05), (0.84, 0.56, 0.04)]:
        sparkle(d, S * x, S * y, S * r)
    return finish(img, size)


# ---------------------------------------------------------------------------
# 화석 · 유물

def stone_disc(d, S, seed, color=(214, 200, 182, 255)):
    return faceted(d, S * 0.5, S * 0.55, S * 0.37, color, sides=11, seed=seed, jitter=0.08, squash=0.8, inner=0.66, top_lift=0.06)


def relic_shell_fossil(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    stone_disc(d, S, 31)
    cx, cy = S * 0.48, S * 0.5
    pts = []
    for i in range(160):
        t = i / 159
        a = t * math.tau * 2.4
        r = S * (0.02 + 0.17 * t)
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r * 0.82))
    d.line(pts, fill=(140, 112, 92, 255), width=16, joint="curve")
    d.line(pts, fill=(176, 150, 128, 255), width=6, joint="curve")
    return finish(img, size)


def relic_paw_fossil(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    stone_disc(d, S, 37, (200, 190, 176, 255))
    pad = (138, 114, 98, 255)
    cx, cy = S * 0.5, S * 0.56
    d.ellipse([cx - S * 0.11, cy - S * 0.06, cx + S * 0.11, cy + S * 0.1], fill=pad)
    for dx, dy in [(-0.13, -0.11), (-0.045, -0.17), (0.045, -0.17), (0.13, -0.11)]:
        x, y = cx + S * dx, cy + S * dy
        d.ellipse([x - S * 0.035, y - S * 0.045, x + S * 0.035, y + S * 0.045], fill=pad)
    return finish(img, size)


def pickaxe(head, size=256, handle=(196, 132, 84, 255), rope=False, faceted_head=False, worn=False):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.2, S * 0.82, S * 0.8, S * 0.94], fill=(60, 36, 40, 45))
    # 자루 (왼쪽 아래 → 오른쪽 위)
    a = math.radians(-50)
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    x0, y0 = S * 0.24, S * 0.84
    L, W = S * 0.66, S * 0.075
    hp = [(x0 + px * W, y0 + py * W), (x0 + ux * L + px * W, y0 + uy * L + py * W),
          (x0 + ux * L - px * W, y0 + uy * L - py * W), (x0 - px * W, y0 - py * W)]
    d.polygon(hp, fill=handle)
    d.polygon([hp[0], hp[1], ((hp[0][0] + hp[3][0]) / 2 + ux * L, (hp[0][1] + hp[3][1]) / 2 + uy * L),
               ((hp[0][0] + hp[3][0]) / 2, (hp[0][1] + hp[3][1]) / 2)], fill=shade(handle, -0.18))
    poly_outline(d, hp, 10)
    # 손잡이 감개
    for k in range(3):
        t = 0.08 + k * 0.07
        cxk, cyk = x0 + ux * L * t, y0 + uy * L * t
        d.line([(cxk + px * W * 1.15, cyk + py * W * 1.15), (cxk - px * W * 1.15, cyk - py * W * 1.15)], fill=(120, 76, 52, 255), width=14)
    # 머리: 자루 끝에서 직각으로 휜 초승달
    hx, hy = x0 + ux * L * 0.86, y0 + uy * L * 0.86
    span = S * 0.36
    pts_top, pts_bot = [], []
    for i in range(25):
        t = i / 24 * 2 - 1
        bend = (1 - t * t) * S * 0.07
        thick = S * (0.055 * (1 - abs(t)) + 0.012)
        bx = hx + px * span * t - ux * bend
        by = hy + py * span * t - uy * bend
        pts_top.append((bx + ux * thick, by + uy * thick))
        pts_bot.append((bx - ux * thick, by - uy * thick))
    head_poly = pts_top + pts_bot[::-1]
    d.polygon(head_poly, fill=head)
    d.polygon(pts_top + [(hx + ux * S * 0.01, hy + uy * S * 0.01)], fill=shade(head, 0.3))
    if faceted_head:
        for i in range(2, 24, 4):
            d.line([pts_top[i], pts_bot[i]], fill=(75, 46, 34, 110), width=5)
    if worn:
        d.line([pts_bot[6], ((pts_top[8][0] + pts_bot[8][0]) / 2, (pts_top[8][1] + pts_bot[8][1]) / 2)], fill=(75, 46, 34, 160), width=6)
        d.line([pts_top[17], ((pts_top[15][0] + pts_bot[15][0]) / 2, (pts_top[15][1] + pts_bot[15][1]) / 2)], fill=(75, 46, 34, 160), width=6)
    poly_outline(d, head_poly, 10)
    # 머리 묶음
    if rope:
        for k in (-1, 1):
            d.line([(hx + px * W * 1.6 * k + ux * W * 1.4, hy + py * W * 1.6 * k + uy * W * 1.4),
                    (hx + px * W * 1.6 * k - ux * W * 1.4, hy + py * W * 1.6 * k - uy * W * 1.4)], fill=(210, 176, 120, 255), width=16)
    else:
        cap = [(hx + px * W * 1.3 + ux * W * 1.5, hy + py * W * 1.3 + uy * W * 1.5),
               (hx - px * W * 1.3 + ux * W * 1.5, hy - py * W * 1.3 + uy * W * 1.5),
               (hx - px * W * 1.3 - ux * W * 1.3, hy - py * W * 1.3 - uy * W * 1.3),
               (hx + px * W * 1.3 - ux * W * 1.3, hy + py * W * 1.3 - uy * W * 1.3)]
        d.polygon(cap, fill=shade(head, -0.25))
        poly_outline(d, cap, 8)
    highlight(d, pts_top[5][0] - S * 0.02, pts_top[5][1] - S * 0.01, S * 0.07, S * 0.035, 200)
    if faceted_head:
        sparkle(d, S * 0.8, S * 0.18, S * 0.07)
    return finish(img, size)


def relic_crown(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.16, S * 0.74, S * 0.84, S * 0.9], fill=(60, 36, 40, 50))
    gold = (246, 196, 72, 255)
    base_y, top_y = S * 0.74, S * 0.3
    pts = [(S * 0.16, base_y), (S * 0.16, S * 0.46), (S * 0.27, S * 0.56), (S * 0.36, top_y), (S * 0.46, S * 0.52),
           (S * 0.56, S * 0.28), (S * 0.62, S * 0.44), (S * 0.7, S * 0.4), (S * 0.66, S * 0.5), (S * 0.74, S * 0.55),
           (S * 0.68, S * 0.62), (S * 0.76, base_y)]
    d.polygon(pts, fill=gold)
    d.polygon([(S * 0.16, S * 0.62), (S * 0.74, S * 0.62), (S * 0.76, base_y), (S * 0.16, base_y)], fill=shade(gold, -0.15))
    poly_outline(d, pts, 10)
    for x, col in [(0.28, (230, 72, 92, 255)), (0.46, (90, 190, 220, 255)), (0.63, (120, 200, 140, 255))]:
        r = S * 0.045
        d.ellipse([S * x - r, S * 0.68 - r, S * x + r, S * 0.68 + r], fill=col, outline=COCOA, width=6)
    sparkle(d, S * 0.36, S * 0.26, S * 0.05)
    highlight(d, S * 0.2, S * 0.48, S * 0.06, S * 0.1, 190)
    return finish(img, size)


def relic_key(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.16, S * 0.8, S * 0.84, S * 0.93], fill=(60, 36, 40, 45))
    gold = (240, 192, 80, 255)
    teal = (94, 196, 196, 255)
    # 자루
    shaft = [(S * 0.34, S * 0.46), (S * 0.86, S * 0.46), (S * 0.86, S * 0.55), (S * 0.34, S * 0.55)]
    d.polygon(shaft, fill=gold)
    poly_outline(d, shaft, 10)
    bit = [(S * 0.7, S * 0.55), (S * 0.8, S * 0.55), (S * 0.8, S * 0.68), (S * 0.75, S * 0.64), (S * 0.7, S * 0.68)]
    d.polygon(bit, fill=gold)
    poly_outline(d, bit, 10)
    # 고리 (조개 모양)
    scallop(d, S * 0.26, S * 0.46, S * 0.2, teal, ribs=5)
    d.ellipse([S * 0.21, S * 0.38, S * 0.31, S * 0.48], fill=(255, 250, 230, 255), outline=COCOA, width=8)
    sparkle(d, S * 0.84, S * 0.32, S * 0.06)
    return finish(img, size)


# ---------------------------------------------------------------------------
# 바위 광맥 (Prop_Rock_0 위에 겹침, 같은 469x300)

VEIN_SPOTS = [(205, 66, 40), (296, 100, 34), (126, 140, 33), (356, 154, 30), (238, 176, 28)]
# 위 자리는 광장 바위(469x300, 보이는 곳 x 9~460 · y 7~268) 기준 → 다른 바위면 보이는 상자에 맞춰 옮김
_BASE_BOX = (9, 7, 460, 268)


def vein_spots():
    if ROCK != CUSTOM_ROCK:
        return VEIN_SPOTS
    box = Image.open(ROCK).convert("RGBA").split()[3].point(lambda a: 255 if a > 40 else 0).getbbox()
    bx0, by0, bx1, by1 = _BASE_BOX
    k = (box[2] - box[0]) / (bx1 - bx0)
    out = []
    for x, y, r in VEIN_SPOTS:
        fx, fy = (x - bx0) / (bx1 - bx0), (y - by0) / (by1 - by0)
        out.append((box[0] + fx * (box[2] - box[0]), box[1] + fy * (box[3] - box[1]), r * k))
    return out


def vein(color, kind="nugget", seed=7):
    rock = Image.open(ROCK).convert("RGBA")
    W, H = rock.size
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for n, (x, y, r) in enumerate(vein_spots()):
        cx, cy, rr = x * SS, y * SS, r * SS
        if kind == "crystal":
            crystal_prism(d, cx - rr * 0.35, cy + rr * 0.55, rr * 0.7, rr * 1.4, color, tilt=-0.35)
            crystal_prism(d, cx + rr * 0.15, cy + rr * 0.6, rr * 0.95, rr * 2.0, color, tilt=(n - 2) * 0.12)
        elif kind == "pearl":
            d.ellipse([cx - rr * 0.8, cy - rr * 0.8, cx + rr * 0.8, cy + rr * 0.8], fill=(250, 246, 255, 255), outline=COCOA, width=7)
            highlight(d, cx - rr * 0.5, cy - rr * 0.55, rr * 0.5, rr * 0.35, 230)
        elif kind == "rainbow":
            faceted(d, cx, cy, rr, hsv(n / len(VEIN_SPOTS), 0.45, 1.0), sides=7, seed=seed + n, outline=7, inner=0.5)
        else:
            faceted(d, cx, cy, rr, color, sides=7, seed=seed + n, outline=7, inner=0.5)
    img = img.resize((W, H), Image.LANCZOS)
    # 바위 밖으로 나간 부분은 지움 (바위 모양 안쪽만)
    alpha = rock.split()[3].point(lambda a: 255 if a > 40 else 0).filter(ImageFilter.MaxFilter(9))
    r, g, b, a = img.split()
    a = Image.composite(a, Image.new("L", a.size, 0), alpha)
    return Image.merge("RGBA", (r, g, b, a))


# GPT로 뽑은 광석 덩어리 스티커(Veins/src/<이름>.png)를 바위 위 다섯 자리에 크기 · 각도를 달리해 박음
VEIN_SRC = os.path.join(OUT, "Veins", "src")
VEIN_TILT = [-14, 10, -6, 16, -20]


def vein_from_source(src_path):
    rock = Image.open(ROCK).convert("RGBA")
    W, H = rock.size
    src = Image.open(src_path).convert("RGBA")
    box = src.getbbox()
    if box:
        src = src.crop(box)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for n, (x, y, r) in enumerate(vein_spots()):
        size = int(r * 2.3)
        k = size / max(src.size)
        piece = src.resize((max(1, int(src.width * k)), max(1, int(src.height * k))), Image.LANCZOS)
        piece = piece.rotate(VEIN_TILT[n % len(VEIN_TILT)], resample=Image.BICUBIC, expand=True)
        img.alpha_composite(piece, (int(x - piece.width / 2), int(y - piece.height * 0.6)))
    alpha = rock.split()[3].point(lambda a: 255 if a > 40 else 0).filter(ImageFilter.MaxFilter(9))
    r, g, b, a = img.split()
    a = Image.composite(a, Image.new("L", a.size, 0), alpha)
    return Image.merge("RGBA", (r, g, b, a))


def soft_glow(size=256, color=(255, 255, 255), power=1.8):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - c, y - c) / c)
            a = (1 - t) ** power
            px[x, y] = color + (int(255 * a),)
    return img


def spark(size=128):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    sparkle(d, S / 2, S / 2, S * 0.48)
    return finish(img, size).filter(ImageFilter.GaussianBlur(0.6))


def ring(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.08, S * 0.08, S * 0.92, S * 0.92], outline=(255, 255, 255, 255), width=int(S * 0.05))
    return finish(img, size).filter(ImageFilter.GaussianBlur(1.5))


def vignette(size=512):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            t = math.hypot((x - c) / c, (y - c) / c)
            a = max(0.0, min(1.0, (t - 0.55) / 0.75))
            px[x, y] = (0, 0, 0, int(255 * a * a))
    return img


def workbench(size=256):
    img = canvas(size)
    d = ImageDraw.Draw(img)
    S = size * SS
    d.ellipse([S * 0.1, S * 0.8, S * 0.9, S * 0.94], fill=(60, 36, 40, 50))
    iron = (120, 128, 150, 255)
    anvil = [(S * 0.12, S * 0.42), (S * 0.88, S * 0.42), (S * 0.8, S * 0.52), (S * 0.64, S * 0.56), (S * 0.66, S * 0.72),
             (S * 0.78, S * 0.82), (S * 0.24, S * 0.82), (S * 0.36, S * 0.72), (S * 0.38, S * 0.56), (S * 0.2, S * 0.52)]
    d.polygon(anvil, fill=iron)
    d.polygon([(S * 0.12, S * 0.42), (S * 0.88, S * 0.42), (S * 0.84, S * 0.47), (S * 0.16, S * 0.47)], fill=shade(iron, 0.35))
    poly_outline(d, anvil, 11)
    small = pickaxe((236, 140, 80, 255), size=size)
    img.alpha_composite(small.resize((S // 2, S // 2), Image.LANCZOS).rotate(18, resample=Image.BICUBIC), (int(S * 0.42), int(S * 0.02)))
    sparkle(d, S * 0.22, S * 0.28, S * 0.07, (255, 236, 150, 255))
    sparkle(d, S * 0.32, S * 0.18, S * 0.04, (255, 236, 150, 255))
    return finish(img, size)


def save_new(img, *parts):
    """이미 있는 그림(GPT로 바꾼 것)은 덮어쓰지 않음 (--all이면 덮어씀)"""
    if not ARGS.all and os.path.exists(os.path.join(OUT, *parts)):
        return
    save(img, *parts)


def main():
    ores = {
        "copper": ore_nugget((232, 140, 82, 255), 11, deco_shell),
        "iron": ore_nugget((128, 148, 176, 255), 12, deco_wave),
        "silver": ore_nugget((204, 214, 232, 255), 13, deco_moon),
        "crystal": ore_crystal((255, 146, 178, 255)),
        "gold": ore_nugget((248, 194, 58, 255), 14, deco_sun),
        "pearl": ore_pearl(),
        "ruby": ore_gem((232, 72, 96, 255)),
        "rainbow": ore_rainbow(),
    }
    for key, img in ores.items():
        save_new(img, "Ores", f"ORE_{key}.png")

    relics = {
        "shell_fossil": relic_shell_fossil(),
        "paw_fossil": relic_paw_fossil(),
        "old_pickaxe": pickaxe((150, 104, 74, 255), worn=True, handle=(160, 118, 86, 255)),
        "crown_piece": relic_crown(),
        "palace_key": relic_key(),
    }
    for key, img in relics.items():
        save_new(img, "Relics", f"RELIC_{key}.png")

    picks = [
        ("0_pebble", pickaxe((178, 172, 166, 255), rope=True)),
        ("1_copper", pickaxe((236, 140, 80, 255))),
        ("2_iron", pickaxe((128, 148, 176, 255))),
        ("3_silver", pickaxe((214, 222, 238, 255))),
        ("4_gold", pickaxe((248, 194, 58, 255))),
        ("5_diamond", pickaxe((126, 214, 240, 255), faceted_head=True)),
    ]
    for key, img in picks:
        save_new(img, "Pickaxes", f"PICK_{key}.png")

    veins = {
        "copper": vein((232, 140, 82, 255)),
        "iron": vein((128, 148, 176, 255)),
        "silver": vein((214, 222, 238, 255)),
        "crystal": vein((255, 146, 178, 255), "crystal"),
        "gold": vein((248, 194, 58, 255)),
        "pearl": vein((250, 246, 255, 255), "pearl"),
        "ruby": vein((232, 72, 96, 255), "crystal"),
        "diamond": vein((126, 214, 240, 255), "crystal"),
        "rainbow": vein((255, 255, 255, 255), "rainbow"),
    }
    for key, img in veins.items():
        src = os.path.join(VEIN_SRC, f"{key}.png")
        save(vein_from_source(src) if os.path.exists(src) else img, "Veins", f"VEIN_{key}.png")

    save(soft_glow(), "Fx", "FX_Glow.png")
    save(spark(), "Fx", "FX_Spark.png")
    save(ring(), "Fx", "FX_Ring.png")
    save(soft_glow(64, power=1.2), "Fx", "FX_Mote.png")
    save(vignette(), "Fx", "FX_Vignette.png")
    save_new(workbench(), "Ui", "ICON_Workbench.png")


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true", help="아이콘도 임시 그림으로 다시 그림 (GPT 그림을 덮어씀!)")
    ARGS = ap.parse_args()
    main()
