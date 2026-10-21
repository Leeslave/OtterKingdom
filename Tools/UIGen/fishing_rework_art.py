"""낚시 개편(테스트 씬) 임시 그림: 물고기 13종 · 바다 · 선착장 · 바늘 · 찌 · 강화 아이콘.

스티커 스타일(코코아 #4B2E22 외곽선, 파스텔, 빛은 왼쪽 위). 4배로 그려 줄여서 매끈하게.
GPT 그림이 생기면 같은 파일 이름으로 덮어쓰면 된다 (Docs/낚시_개편_기획.md). 이미 있는 그림은 그대로 두고
없는 것만 그림 (--all이면 전부 다시).

python Tools/UIGen/fishing_rework_art.py [--all]  → Unity Tools/Fishing Rework/Build Test Scene
"""
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "FishingRework")
ALL = "--all" in sys.argv

SS = 4
COCOA = (75, 46, 34, 255)


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def shade(color, k):
    base = color[:3]
    out = lerp(base, (255, 255, 255), k) if k >= 0 else lerp(base, (60, 36, 40), -k)
    return out + (255,)


def save(img, *parts):
    path = os.path.join(OUT, *parts)
    if os.path.exists(path) and not ALL:
        print("그대로", os.path.relpath(path, ROOT))
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("그림", os.path.relpath(path, ROOT))


def layer(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def sticker(img, outline=7, shadow=False):
    """그린 모양 둘레에 코코아 외곽선 (알파를 넓혀 밑에 깖)"""
    alpha = img.split()[3].point(lambda a: 255 if a > 20 else 0)
    # 넓히기: 흐리게 한 뒤 낮은 문턱 (큰 MaxFilter보다 훨씬 빠름)
    grown = alpha.filter(ImageFilter.GaussianBlur(outline * SS * 0.55)).point(lambda a: 255 if a > 6 else 0)
    grown = grown.filter(ImageFilter.GaussianBlur(SS * 0.8))
    base = Image.new("RGBA", img.size, COCOA)
    base.putalpha(grown)
    if shadow:
        sh = Image.new("RGBA", img.size, (60, 36, 40, 60))
        sh.putalpha(grown.point(lambda a: a * 60 // 255))
        base = Image.alpha_composite(Image.new("RGBA", img.size, (0, 0, 0, 0)), base)
    return Image.alpha_composite(base, img)


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def clip(pattern, mask_img):
    """pattern을 mask_img의 알파 안으로만"""
    a = ImageChops.multiply(pattern.split()[3], mask_img.split()[3])
    out = pattern.copy()
    out.putalpha(a)
    return out


def sparkle(d, x, y, r, color=(255, 255, 255, 255)):
    pts = []
    for i in range(8):
        a = i / 8 * math.tau - math.pi / 2
        rr = r if i % 2 == 0 else r * 0.28
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    d.polygon(pts, fill=color)


# ---------------------------------------------------------------------------
# 물고기 (옆모습, 오른쪽을 봄). 캔버스 384x256

FW, FH = 384, 256


def body_points(cx, cy, length, height, nose=0.55, tail=0.25, n=48):
    """머리(오른쪽)가 둥글고 꼬리 쪽이 가는 물방울 몸통"""
    pts = []
    for i in range(n):
        t = i / n * math.tau
        x = math.cos(t)
        y = math.sin(t)
        # 오른쪽(x>0)은 둥글게, 왼쪽은 꼬리로 갈수록 얇게
        k = 1.0 if x > 0 else (1 - tail) + tail * (1 + x) if False else 1.0
        thin = 1.0 if x >= 0 else 1 - (1 - nose) * (-x) ** 1.6
        pts.append((cx + x * length / 2, cy + y * height / 2 * thin))
    return pts


def draw_fish(body, belly, fin, length=0.62, height=0.42, tail_kind="fork", pattern=None, eye_y=-0.12,
              dorsal=True, extra=None, glow=None, sparkles=0, seed=1):
    S = SS
    W, H = FW * S, FH * S
    img = layer(FW, FH)
    d = ImageDraw.Draw(img)
    cx, cy = W * 0.54, H * 0.52
    L, Hh = W * length, H * height
    left = cx - L / 2

    # 꼬리
    tx = left + L * 0.06
    if tail_kind == "fork":
        tail = [(tx, cy), (tx - L * 0.30, cy - Hh * 0.62), (tx - L * 0.20, cy), (tx - L * 0.30, cy + Hh * 0.62)]
    elif tail_kind == "fan":
        tail = [(tx, cy)] + [(tx - L * 0.34 * math.cos(a), cy + Hh * 0.75 * math.sin(a)) for a in
                             [i / 12 * math.pi - math.pi / 2 for i in range(13)]]
    elif tail_kind == "veil":
        tail = [(tx, cy - Hh * 0.1), (tx - L * 0.42, cy - Hh * 0.9), (tx - L * 0.30, cy - Hh * 0.1),
                (tx - L * 0.48, cy + Hh * 0.2), (tx - L * 0.30, cy + Hh * 0.35), (tx - L * 0.40, cy + Hh * 0.95),
                (tx, cy + Hh * 0.12)]
    else:  # round
        tail = [(tx, cy)] + [(tx - L * 0.22 * math.cos(a), cy + Hh * 0.5 * math.sin(a)) for a in
                             [i / 10 * math.pi - math.pi / 2 for i in range(11)]]
    d.polygon(tail, fill=fin)

    # 등지느러미 · 배지느러미
    if dorsal:
        d.polygon([(cx - L * 0.22, cy - Hh * 0.38), (cx - L * 0.05, cy - Hh * 0.82), (cx + L * 0.16, cy - Hh * 0.42)],
                  fill=fin)
    d.polygon([(cx - L * 0.08, cy + Hh * 0.36), (cx - L * 0.2, cy + Hh * 0.72), (cx + L * 0.06, cy + Hh * 0.42)], fill=fin)

    # 몸통
    pts = body_points(cx, cy, L, Hh)
    body_layer = layer(FW, FH)
    bd = ImageDraw.Draw(body_layer)
    bd.polygon(pts, fill=body)
    # 배 (아래쪽 밝게)
    detail = layer(FW, FH)
    dd = ImageDraw.Draw(detail)
    dd.ellipse([cx - L * 0.55, cy + Hh * 0.02, cx + L * 0.5, cy + Hh * 0.9], fill=belly)
    if pattern:
        pattern(dd, cx, cy, L, Hh)
    # 위쪽 그늘 · 빛
    dd.ellipse([cx - L * 0.1, cy - Hh * 0.46, cx + L * 0.36, cy - Hh * 0.22], fill=(255, 255, 255, 120))
    body_layer = Image.alpha_composite(body_layer, clip(detail, body_layer))
    img = Image.alpha_composite(img, body_layer)
    d = ImageDraw.Draw(img)

    # 아가미 줄
    gx = cx + L * 0.18
    d.arc([gx - Hh * 0.35, cy - Hh * 0.32, gx + Hh * 0.05, cy + Hh * 0.32], 300, 60, fill=(75, 46, 34, 150), width=S * 3)
    # 눈
    ex, ey, er = cx + L * 0.33, cy + Hh * eye_y, Hh * 0.13
    d.ellipse([ex - er, ey - er, ex + er, ey + er], fill=(255, 255, 255, 255))
    d.ellipse([ex - er * 0.62, ey - er * 0.55, ex + er * 0.62, ey + er * 0.7], fill=COCOA)
    d.ellipse([ex - er * 0.15, ey - er * 0.45, ex + er * 0.25, ey - er * 0.05], fill=(255, 255, 255, 255))
    # 볼 · 입
    d.ellipse([ex - er * 1.6, ey + er * 1.0, ex - er * 0.2, ey + er * 1.9], fill=(255, 150, 150, 110))
    d.arc([cx + L * 0.42, cy + Hh * 0.02, cx + L * 0.5, cy + Hh * 0.14], 90, 200, fill=COCOA, width=S * 3)

    if extra:
        extra(d, img, cx, cy, L, Hh)
    img = sticker(img, 6)
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    for _ in range(sparkles):
        sparkle(d, rng.uniform(W * 0.15, W * 0.9), rng.uniform(H * 0.1, H * 0.3), S * rng.uniform(7, 11))
    if glow:
        g = Image.new("RGBA", img.size, (0, 0, 0, 0))
        gd = ImageDraw.Draw(g)
        gd.ellipse([W * 0.08, H * 0.05, W * 0.95, H * 0.95], fill=glow)
        g = g.filter(ImageFilter.GaussianBlur(S * 18))
        img = Image.alpha_composite(g, img)
    return finish(img, FW, FH)


def stripes(color, n=3, width=0.06):
    def p(d, cx, cy, L, Hh):
        for i in range(n):
            x = cx - L * 0.2 + i * L * 0.16
            d.polygon([(x, cy - Hh), (x + L * width, cy - Hh), (x + L * width - L * 0.04, cy + Hh), (x - L * 0.04, cy + Hh)],
                      fill=color)
    return p


def waves(color):
    def p(d, cx, cy, L, Hh):
        for i in range(5):
            x = cx - L * 0.35 + i * L * 0.13
            d.arc([x, cy - Hh * 0.55, x + L * 0.12, cy - Hh * 0.05], 200, 340, fill=color, width=SS * 6)
    return p


def spots(color, count=7, seed=3, size=0.06):
    def p(d, cx, cy, L, Hh):
        rng = random.Random(seed)
        for _ in range(count):
            x = rng.uniform(cx - L * 0.35, cx + L * 0.25)
            y = rng.uniform(cy - Hh * 0.4, cy + Hh * 0.1)
            r = L * size * rng.uniform(0.7, 1.2)
            d.ellipse([x - r, y - r, x + r, y + r], fill=color)
    return p


def koi_patches(d, cx, cy, L, Hh):
    for (x, y, r, c) in [(-0.2, -0.2, 0.16, (240, 90, 60, 255)), (0.12, -0.25, 0.12, (255, 205, 80, 255)),
                         (-0.02, 0.05, 0.1, (240, 90, 60, 255))]:
        d.ellipse([cx + L * (x - r), cy + Hh * y - L * r * 0.8, cx + L * (x + r), cy + Hh * y + L * r * 0.8], fill=c)


def special_canvas():
    img = layer(FW, FH)
    return img, ImageDraw.Draw(img), FW * SS, FH * SS


def eye(d, x, y, r):
    d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 255))
    d.ellipse([x - r * 0.6, y - r * 0.5, x + r * 0.6, y + r * 0.7], fill=COCOA)
    d.ellipse([x - r * 0.12, y - r * 0.42, x + r * 0.25, y - r * 0.05], fill=(255, 255, 255, 255))


def crab(red=(240, 110, 80, 255), light=(255, 170, 130, 255)):
    img, d, W, H = special_canvas()
    cx, cy = W * 0.5, H * 0.6
    for s in (-1, 1):
        for i in range(3):
            x0 = cx + s * W * (0.1 + i * 0.06)
            d.line([(x0, cy + H * 0.08), (x0 + s * W * 0.1, cy + H * 0.22), (x0 + s * W * 0.13, cy + H * 0.33)],
                   fill=red, width=SS * 9, joint="curve")
        # 집게
        ax = cx + s * W * 0.25
        d.line([(cx + s * W * 0.12, cy - H * 0.02), (ax, cy - H * 0.18)], fill=red, width=SS * 12)
        d.ellipse([ax - W * 0.08, cy - H * 0.38, ax + W * 0.08, cy - H * 0.12], fill=red)
        d.polygon([(ax + s * W * 0.0, cy - H * 0.28), (ax + s * W * 0.1, cy - H * 0.42), (ax + s * W * 0.03, cy - H * 0.2)],
                  fill=(0, 0, 0, 0))
        # 눈자루
        d.line([(cx + s * W * 0.05, cy - H * 0.12), (cx + s * W * 0.06, cy - H * 0.3)], fill=red, width=SS * 7)
        eye(d, cx + s * W * 0.06, cy - H * 0.32, H * 0.06)
    d.ellipse([cx - W * 0.2, cy - H * 0.2, cx + W * 0.2, cy + H * 0.16], fill=red)
    d.ellipse([cx - W * 0.12, cy - H * 0.16, cx + W * 0.04, cy - H * 0.06], fill=(255, 255, 255, 110))
    d.arc([cx - W * 0.05, cy - H * 0.04, cx + W * 0.05, cy + H * 0.06], 20, 160, fill=COCOA, width=SS * 4)
    d.ellipse([cx - W * 0.15, cy - H * 0.02, cx - W * 0.09, cy + H * 0.04], fill=light)
    d.ellipse([cx + W * 0.09, cy - H * 0.02, cx + W * 0.15, cy + H * 0.04], fill=light)
    return finish(sticker(img, 6), FW, FH)


def seahorse():
    img, d, W, H = special_canvas()
    col, light = (255, 200, 90, 255), (255, 235, 160, 255)
    # 몸: 원을 이어 S자
    path = [(0.52, 0.28, 0.1), (0.5, 0.4, 0.11), (0.47, 0.52, 0.11), (0.47, 0.63, 0.1), (0.5, 0.72, 0.08),
            (0.55, 0.79, 0.065), (0.58, 0.85, 0.05), (0.55, 0.9, 0.04), (0.5, 0.9, 0.035), (0.48, 0.86, 0.03)]
    for (x, y, r) in path:
        d.ellipse([W * x - H * r, H * y - H * r, W * x + H * r, H * y + H * r], fill=col)
    # 주둥이 · 머리
    d.ellipse([W * 0.47, H * 0.13, W * 0.62, H * 0.33], fill=col)
    d.polygon([(W * 0.58, H * 0.2), (W * 0.72, H * 0.22), (W * 0.72, H * 0.28), (W * 0.58, H * 0.29)], fill=col)
    d.polygon([(W * 0.42, H * 0.42), (W * 0.33, H * 0.36), (W * 0.36, H * 0.55)], fill=(255, 160, 120, 255))
    for i in range(5):
        y = H * (0.35 + i * 0.07)
        d.arc([W * 0.42, y - H * 0.03, W * 0.58, y + H * 0.03], 0, 180, fill=(220, 150, 60, 200), width=SS * 3)
    d.ellipse([W * 0.5, H * 0.32, W * 0.56, H * 0.52], fill=light)
    eye(d, W * 0.56, H * 0.21, H * 0.04)
    return finish(sticker(img, 6), FW, FH)


def squid():
    img, d, W, H = special_canvas()
    col, light = (250, 220, 230, 255), (255, 240, 245, 255)
    cy = H * 0.5
    # 다리 (왼쪽)
    for i in range(6):
        y = cy - H * 0.14 + i * H * 0.056
        d.line([(W * 0.42, y), (W * 0.28, y + (i - 2.5) * H * 0.03), (W * 0.12, y + (i - 2.5) * H * 0.06)],
               fill=col, width=SS * 9, joint="curve")
    # 몸통 (오른쪽이 뾰족)
    d.polygon([(W * 0.36, cy - H * 0.18), (W * 0.74, cy - H * 0.16), (W * 0.92, cy), (W * 0.74, cy + H * 0.16),
               (W * 0.36, cy + H * 0.18)], fill=col)
    d.polygon([(W * 0.8, cy - H * 0.12), (W * 0.93, cy - H * 0.3), (W * 0.92, cy)], fill=col)
    d.polygon([(W * 0.8, cy + H * 0.12), (W * 0.93, cy + H * 0.3), (W * 0.92, cy)], fill=col)
    d.ellipse([W * 0.32, cy - H * 0.2, W * 0.46, cy + H * 0.2], fill=col)
    for (x, y) in [(0.55, -0.06), (0.63, 0.05), (0.7, -0.04), (0.5, 0.07)]:
        d.ellipse([W * x - SS * 8, cy + H * y - SS * 8, W * x + SS * 8, cy + H * y + SS * 8], fill=(240, 150, 170, 255))
    d.ellipse([W * 0.5, cy - H * 0.15, W * 0.78, cy - H * 0.07], fill=light)
    eye(d, W * 0.4, cy - H * 0.04, H * 0.065)
    return finish(sticker(img, 6), FW, FH)


def octopus():
    img, d, W, H = special_canvas()
    col, light = (235, 120, 150, 255), (255, 190, 205, 255)
    cx, cy = W * 0.5, H * 0.42
    for i in range(5):
        x = cx - W * 0.2 + i * W * 0.1
        pts = [(x, cy + H * 0.12)]
        for k in range(1, 7):
            pts.append((x + math.sin(k * 0.9 + i) * W * 0.03 + (i - 2) * W * 0.02 * k / 6, cy + H * 0.12 + k * H * 0.07))
        d.line(pts, fill=col, width=SS * 13, joint="curve")
    d.ellipse([cx - W * 0.2, cy - H * 0.32, cx + W * 0.2, cy + H * 0.22], fill=col)
    d.ellipse([cx - W * 0.1, cy - H * 0.26, cx + W * 0.05, cy - H * 0.12], fill=light)
    eye(d, cx - W * 0.06, cy + H * 0.02, H * 0.06)
    eye(d, cx + W * 0.07, cy + H * 0.02, H * 0.06)
    d.arc([cx - W * 0.03, cy + H * 0.06, cx + W * 0.04, cy + H * 0.13], 20, 160, fill=COCOA, width=SS * 4)
    return finish(sticker(img, 6), FW, FH)


def ray():
    img, d, W, H = special_canvas()
    col, belly = (120, 150, 190, 255), (200, 215, 235, 255)
    cx, cy = W * 0.56, H * 0.5
    d.line([(cx - W * 0.1, cy), (W * 0.06, cy + H * 0.05)], fill=col, width=SS * 7)
    d.polygon([(cx + W * 0.3, cy), (cx, cy - H * 0.4), (cx - W * 0.18, cy), (cx, cy + H * 0.4)], fill=col)
    d.ellipse([cx - W * 0.18, cy - H * 0.2, cx + W * 0.26, cy + H * 0.2], fill=col)
    d.ellipse([cx - W * 0.05, cy - H * 0.05, cx + W * 0.2, cy + H * 0.18], fill=belly)
    for (x, y) in [(-0.05, -0.15), (0.05, -0.22), (0.12, -0.1), (-0.1, 0.0)]:
        d.ellipse([cx + W * x - SS * 7, cy + H * y - SS * 7, cx + W * x + SS * 7, cy + H * y + SS * 7], fill=(90, 115, 160, 255))
    eye(d, cx + W * 0.17, cy - H * 0.08, H * 0.05)
    return finish(sticker(img, 6), FW, FH)


def puffer():
    img, d, W, H = special_canvas()
    col, belly = (250, 215, 110, 255), (255, 245, 210, 255)
    cx, cy, r = W * 0.52, H * 0.52, H * 0.34
    for i in range(18):
        a = i / 18 * math.tau
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        d.polygon([(x - math.sin(a) * SS * 10, y + math.cos(a) * SS * 10), (cx + math.cos(a) * r * 1.22, cy + math.sin(a) * r * 1.22),
                   (x + math.sin(a) * SS * 10, y - math.cos(a) * SS * 10)], fill=(230, 180, 80, 255))
    d.polygon([(cx - r * 0.9, cy), (cx - r * 1.45, cy - r * 0.45), (cx - r * 1.35, cy), (cx - r * 1.45, cy + r * 0.45)],
              fill=(240, 170, 90, 255))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
    d.ellipse([cx - r * 0.75, cy + r * 0.05, cx + r * 0.8, cy + r * 0.92], fill=belly)
    for (x, y) in [(-0.4, -0.45), (-0.1, -0.6), (0.2, -0.5), (-0.55, -0.1)]:
        d.ellipse([cx + r * x - SS * 9, cy + r * y - SS * 9, cx + r * x + SS * 9, cy + r * y + SS * 9], fill=(180, 140, 80, 255))
    d.ellipse([cx - r * 0.2, cy - r * 0.75, cx + r * 0.3, cy - r * 0.45], fill=(255, 255, 255, 140))
    eye(d, cx + r * 0.5, cy - r * 0.15, r * 0.2)
    d.ellipse([cx + r * 0.78, cy + r * 0.05, cx + r * 0.98, cy + r * 0.25], fill=(230, 120, 110, 255))
    return finish(sticker(img, 6), FW, FH)


def angler_extra(d, img, cx, cy, L, Hh):
    # 초롱 (빛나는 미끼)
    d.line([(cx + L * 0.1, cy - Hh * 0.45), (cx + L * 0.25, cy - Hh * 0.9), (cx + L * 0.45, cy - Hh * 0.85)],
           fill=(90, 70, 110, 255), width=SS * 5, joint="curve")
    d.ellipse([cx + L * 0.42, cy - Hh * 0.98, cx + L * 0.54, cy - Hh * 0.72], fill=(255, 245, 160, 255))
    # 이빨 입
    d.polygon([(cx + L * 0.5, cy + Hh * 0.0), (cx + L * 0.3, cy + Hh * 0.2), (cx + L * 0.5, cy + Hh * 0.28)], fill=COCOA)
    for i in range(3):
        x = cx + L * (0.34 + i * 0.05)
        d.polygon([(x, cy + Hh * 0.1), (x + L * 0.02, cy + Hh * 0.19), (x + L * 0.04, cy + Hh * 0.1)], fill=(255, 255, 255, 255))


def jellyfish(col, glow=None):
    img, d, W, H = special_canvas()
    cx, cy = W * 0.5, H * 0.38
    for i in range(5):
        x = cx - W * 0.12 + i * W * 0.06
        pts = [(x + math.sin(k * 0.8 + i) * W * 0.02, cy + H * 0.1 + k * H * 0.08) for k in range(7)]
        d.line(pts, fill=col[:3] + (200,), width=SS * 6, joint="curve")
    d.chord([cx - W * 0.17, cy - H * 0.28, cx + W * 0.17, cy + H * 0.26], 180, 360, fill=col)
    d.ellipse([cx - W * 0.09, cy - H * 0.2, cx + W * 0.0, cy - H * 0.08], fill=(255, 255, 255, 150))
    eye(d, cx - W * 0.05, cy - H * 0.04, H * 0.035)
    eye(d, cx + W * 0.05, cy - H * 0.04, H * 0.035)
    img = sticker(img, 5)
    if glow:
        g = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(g).ellipse([W * 0.2, H * 0.05, W * 0.8, H * 0.9], fill=glow)
        img = Image.alpha_composite(g.filter(ImageFilter.GaussianBlur(SS * 16)), img)
    return finish(img, FW, FH)


def shrimp(col, light):
    img, d, W, H = special_canvas()
    cy = H * 0.55
    for i in range(6):
        x = W * (0.3 + i * 0.07)
        r = H * (0.13 - i * 0.008)
        d.ellipse([x - r, cy - r - math.sin(i * 0.5) * H * 0.05, x + r, cy + r - math.sin(i * 0.5) * H * 0.05], fill=col)
    d.polygon([(W * 0.24, cy), (W * 0.12, cy - H * 0.14), (W * 0.14, cy + H * 0.12)], fill=col)
    for i in range(3):
        d.line([(W * 0.68, cy - H * 0.1), (W * (0.9 + i * 0.02), cy - H * (0.32 - i * 0.08))], fill=col, width=SS * 3)
    d.ellipse([W * 0.38, cy - H * 0.14, W * 0.6, cy - H * 0.04], fill=light)
    eye(d, W * 0.67, cy - H * 0.08, H * 0.04)
    return finish(sticker(img, 5), FW, FH)


def sword_extra(d, img, cx, cy, L, Hh):
    d.polygon([(cx + L * 0.46, cy - Hh * 0.06), (cx + L * 0.9, cy - Hh * 0.02), (cx + L * 0.46, cy + Hh * 0.05)],
              fill=(120, 140, 170, 255))


def fish_icons():
    plain = dict()
    items = {
        "anchovy": draw_fish((170, 205, 225, 255), (235, 245, 250, 255), (140, 180, 210, 255), 0.6, 0.28,
                             pattern=stripes((120, 160, 200, 140), 1, 0.4), seed=1),
        "mackerel": draw_fish((90, 150, 190, 255), (230, 240, 245, 255), (80, 130, 170, 255), 0.62, 0.36,
                              pattern=waves((40, 80, 120, 230)), seed=2),
        "goldfish": draw_fish((255, 150, 60, 255), (255, 210, 140, 255), (255, 120, 70, 255), 0.5, 0.46, tail_kind="veil",
                              sparkles=3, glow=(255, 220, 120, 90), seed=3),
        "crab": crab(),
        "puffer": puffer(),
        "seahorse": seahorse(),
        "seabream": draw_fish((245, 130, 140, 255), (255, 215, 210, 255), (230, 110, 120, 255), 0.6, 0.5,
                              pattern=spots((120, 170, 230, 220), 6, 4, 0.025), seed=4),
        "squid": squid(),
        "octopus": octopus(),
        "tuna": draw_fish((60, 90, 140, 255), (225, 230, 240, 255), (240, 200, 80, 255), 0.66, 0.4,
                          pattern=stripes((90, 130, 180, 120), 1, 0.3), seed=5),
        "ray": ray(),
        "angler": draw_fish((90, 80, 120, 255), (140, 130, 165, 255), (70, 60, 100, 255), 0.56, 0.52, tail_kind="round",
                            extra=angler_extra, glow=(255, 240, 150, 60), seed=6),
        "sardine": draw_fish((140, 175, 215, 255), (235, 245, 252, 255), (110, 150, 200, 255), 0.6, 0.32,
                             pattern=spots((60, 90, 140, 220), 5, 11, 0.02), seed=11),
        "shrimp": shrimp((255, 150, 130, 255), (255, 210, 195, 255)),
        "clownfish": draw_fish((255, 140, 60, 255), (255, 190, 120, 255), (255, 120, 50, 255), 0.52, 0.42, tail_kind="round",
                               pattern=stripes((255, 255, 255, 255), 3, 0.07), seed=12),
        "filefish": draw_fish((180, 170, 120, 255), (225, 220, 180, 255), (150, 140, 100, 255), 0.55, 0.5,
                              pattern=spots((120, 110, 80, 200), 8, 13, 0.025), seed=13),
        "jelly": jellyfish((230, 200, 255, 230)),
        "flatfish": draw_fish((175, 145, 110, 255), (215, 190, 155, 255), (150, 120, 90, 255), 0.66, 0.3, eye_y=-0.3,
                              pattern=spots((120, 95, 70, 220), 9, 14, 0.03), seed=14),
        "eel": draw_fish((120, 110, 90, 255), (190, 180, 150, 255), (100, 90, 70, 255), 0.8, 0.16, tail_kind="round", seed=15),
        "yellowtail": draw_fish((120, 170, 200, 255), (235, 240, 245, 255), (240, 210, 70, 255), 0.66, 0.36,
                                pattern=stripes((245, 215, 80, 255), 1, 0.5), seed=16),
        "lobster": crab((200, 80, 90, 255), (240, 150, 140, 255)),
        "sunfish": draw_fish((170, 180, 195, 255), (225, 230, 238, 255), (150, 160, 180, 255), 0.5, 0.72, tail_kind="round", seed=17),
        "swordfish": draw_fish((80, 110, 160, 255), (220, 228, 240, 255), (70, 95, 140, 255), 0.6, 0.32, extra=sword_extra, seed=18),
        "deepjelly": jellyfish((150, 120, 230, 230), glow=(190, 160, 255, 110)),
        "oarfish": draw_fish((220, 225, 235, 255), (245, 248, 252, 255), (240, 90, 90, 255), 0.84, 0.14, sparkles=3,
                             glow=(230, 230, 255, 70), seed=19),
        "koi": draw_fish((255, 248, 235, 255), (255, 255, 255, 255), (255, 170, 110, 255), 0.58, 0.4, tail_kind="veil",
                         pattern=koi_patches, sparkles=5, glow=(255, 230, 140, 120), seed=7),
    }
    for name, img in items.items():
        save(img, "Fish", f"FISH_{name}.png")


# ---------------------------------------------------------------------------
# 바다 · 하늘 · 선착장 · 소품

def gradient(w, h, stops):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        for i in range(len(stops) - 1):
            if stops[i][0] <= t <= stops[i + 1][0]:
                k = (t - stops[i][0]) / max(1e-6, stops[i + 1][0] - stops[i][0])
                c = lerp(stops[i][1], stops[i + 1][1], k)
                break
        for x in range(w):
            px[x, y] = c
    return img


def world():
    save(gradient(16, 512, [(0, (110, 170, 230, 255)), (0.65, (175, 215, 240, 255)), (1, (250, 235, 215, 255))]),
         "World", "SKY.png")
    # 물: 위(0m) → 아래(64m)
    save(gradient(16, 512, [(0, (110, 205, 220, 255)), (0.08, (75, 175, 205, 255)), (0.3, (45, 120, 175, 255)),
                            (0.65, (30, 70, 135, 255)), (1, (20, 35, 80, 255))]), "World", "WATER.png")

    # 물결 띠 (가로로 이어짐)
    w, h = 256, 48
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    pts = [(x, H * 0.45 + math.sin(x / W * math.tau * 2) * H * 0.12) for x in range(0, W + 1, SS * 2)]
    d.polygon(pts + [(W, H), (0, H)], fill=(160, 225, 235, 200))
    d.line(pts, fill=(255, 255, 255, 230), width=SS * 4)
    save(finish(img, w, h), "World", "WAVE.png")

    # 빛줄기
    w, h = 64, 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        a = int(110 * (1 - y / h) ** 1.4)
        for x in range(w):
            e = 1 - abs(x - w / 2) / (w / 2)
            px[x, y] = (255, 255, 230, int(a * e ** 1.5))
    save(img, "World", "RAY.png")

    # 먼 섬 (가로 띠)
    w, h = 1024, 160
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    rng = random.Random(9)
    for (x0, x1, top, col) in [(0.05, 0.42, 0.25, (150, 185, 170, 255)), (0.5, 0.95, 0.4, (165, 195, 185, 255)),
                               (0.3, 0.62, 0.55, (180, 205, 195, 255))]:
        pts = [(W * x0, H)]
        n = 20
        for i in range(n + 1):
            t = i / n
            pts.append((W * (x0 + (x1 - x0) * t), H * (top + (1 - top) * (1 - math.sin(t * math.pi)) ** 1.5) + rng.uniform(-6, 6) * SS))
        pts.append((W * x1, H))
        d.polygon(pts, fill=col)
    save(finish(img, w, h), "World", "ISLAND.png")

    # 구름
    w, h = 256, 128
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    for (x, y, r) in [(0.3, 0.6, 0.22), (0.5, 0.45, 0.3), (0.7, 0.6, 0.22), (0.5, 0.7, 0.2)]:
        d.ellipse([W * x - H * r * 1.3, H * y - H * r, W * x + H * r * 1.3, H * y + H * r], fill=(255, 255, 255, 235))
    save(finish(img, w, h), "World", "CLOUD.png")

    # 선착장 (옆모습). 1536x768 = 12m x 6m, 오른쪽 위 = 데크 끝
    w, h = 1536, 768
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    deck_top, deck_h = H * 0.0 + SS * 4, H * 0.06
    wood, dark, light = (205, 150, 100, 255), (160, 105, 70, 255), (230, 185, 135, 255)
    for i in range(6):
        x = W * (0.97 - i * 0.17)
        d.rectangle([x - W * 0.016, deck_top, x + W * 0.016, H * 0.98], fill=dark)
        d.rectangle([x - W * 0.016, deck_top, x - W * 0.006, H * 0.98], fill=(185, 125, 85, 255))
        d.rectangle([x - W * 0.018, H * 0.16, x + W * 0.018, H * 0.2], fill=(120, 150, 120, 255))  # 이끼 (물높이)
    d.rectangle([0, deck_top, W * 0.995, deck_top + deck_h], fill=wood)
    d.rectangle([0, deck_top, W * 0.995, deck_top + deck_h * 0.3], fill=light)
    for i in range(24):
        x = W * (i / 24)
        d.line([(x, deck_top), (x, deck_top + deck_h)], fill=dark, width=SS * 3)
    d.line([(W * 0.06, deck_top + deck_h * 1.8), (W * 0.97, deck_top + deck_h * 1.8)], fill=dark, width=SS * 10)
    img = sticker(img, 5)
    save(finish(img, w, h), "World", "PIER.png")

    # 부표
    w, h = 128, 192
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    d.line([(W * 0.5, H * 0.1), (W * 0.5, H * 0.35)], fill=(120, 110, 100, 255), width=SS * 6)
    d.polygon([(W * 0.5, H * 0.05), (W * 0.78, H * 0.13), (W * 0.5, H * 0.21)], fill=(255, 220, 90, 255))
    d.ellipse([W * 0.15, H * 0.3, W * 0.85, H * 0.78], fill=(245, 95, 85, 255))
    d.rectangle([W * 0.15, H * 0.5, W * 0.85, H * 0.6], fill=(255, 255, 255, 255))
    d.ellipse([W * 0.28, H * 0.36, W * 0.48, H * 0.46], fill=(255, 255, 255, 150))
    save(finish(sticker(img, 5), w, h), "World", "BUOY.png")

    # 해초 2 · 산호 2 · 바위 2
    for i, col in enumerate([(110, 190, 120, 255), (80, 160, 140, 255)]):
        w, h = 128, 384
        img = layer(w, h)
        d = ImageDraw.Draw(img)
        W, H = w * SS, h * SS
        for k in range(3):
            x0 = W * (0.3 + k * 0.2)
            pts = [(x0 + math.sin(t * 5 + k + i) * W * 0.12, H - t * H * (0.75 + 0.2 * ((k + i) % 2))) for t in
                   [j / 20 for j in range(21)]]
            d.line(pts, fill=col, width=SS * 14, joint="curve")
        save(finish(sticker(img, 4), w, h), "World", f"SEAWEED_{i}.png")
    for i, col in enumerate([(255, 140, 150, 255), (255, 185, 110, 255)]):
        w, h = 256, 256
        img = layer(w, h)
        d = ImageDraw.Draw(img)
        W, H = w * SS, h * SS
        rng = random.Random(20 + i)

        def branch(x, y, a, length, width, depth):
            x2, y2 = x + math.cos(a) * length, y - math.sin(a) * length
            d.line([(x, y), (x2, y2)], fill=col, width=int(width), joint="curve")
            d.ellipse([x2 - width / 2, y2 - width / 2, x2 + width / 2, y2 + width / 2], fill=col)
            if depth > 0:
                for s in (-1, 1):
                    branch(x2, y2, a + s * rng.uniform(0.3, 0.6), length * 0.7, width * 0.75, depth - 1)

        branch(W * 0.5, H, math.pi / 2, H * 0.32, SS * 22, 3)
        save(finish(sticker(img, 4), w, h), "World", f"CORAL_{i}.png")
    for i, col in enumerate([(150, 140, 135, 255), (125, 120, 130, 255)]):
        w, h = 256, 160
        img = layer(w, h)
        d = ImageDraw.Draw(img)
        W, H = w * SS, h * SS
        d.ellipse([W * 0.08, H * 0.25, W * 0.92, H * 1.4], fill=col)
        d.ellipse([W * 0.2, H * 0.35, W * 0.5, H * 0.55], fill=shade(col, 0.35))
        save(finish(sticker(img, 4), w, h), "World", f"ROCK_{i}.png")

    # 바늘 · 찌(봉돌)
    w, h = 64, 112
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    d.line([(W * 0.5, H * 0.04), (W * 0.5, H * 0.7)], fill=(200, 205, 215, 255), width=SS * 6)
    d.arc([W * 0.15, H * 0.5, W * 0.85, H * 0.95], 0, 180, fill=(200, 205, 215, 255), width=SS * 6)
    d.polygon([(W * 0.15, H * 0.72), (W * 0.06, H * 0.6), (W * 0.26, H * 0.66)], fill=(200, 205, 215, 255))
    save(finish(sticker(img, 3), w, h), "World", "HOOK.png")
    w, h = 96, 96
    img = layer(w, h)
    d = ImageDraw.Draw(img)
    W, H = w * SS, h * SS
    d.ellipse([W * 0.2, H * 0.12, W * 0.8, H * 0.88], fill=(245, 95, 85, 255))
    d.rectangle([W * 0.2, H * 0.48, W * 0.8, H * 0.88], fill=(255, 255, 255, 0))
    d.chord([W * 0.2, H * 0.12, W * 0.8, H * 0.88], 0, 180, fill=(250, 250, 245, 255))
    d.ellipse([W * 0.32, H * 0.2, W * 0.48, H * 0.36], fill=(255, 255, 255, 160))
    save(finish(sticker(img, 4), w, h), "World", "SINKER.png")


# ---------------------------------------------------------------------------
# 강화 · 화면 아이콘 (256)

def icons():
    def icon(draw):
        img = layer(256, 256)
        d = ImageDraw.Draw(img)
        draw(d, 256 * SS)
        return finish(sticker(img, 7), 256, 256)

    def line_spool(d, S):
        d.rounded_rectangle([S * 0.2, S * 0.3, S * 0.8, S * 0.7], radius=S * 0.08, fill=(130, 200, 230, 255))
        for i in range(6):
            y = S * (0.34 + i * 0.06)
            d.line([(S * 0.22, y), (S * 0.78, y + S * 0.02)], fill=(255, 255, 255, 200), width=SS * 5)
        d.rectangle([S * 0.14, S * 0.22, S * 0.24, S * 0.78], fill=(205, 150, 100, 255))
        d.rectangle([S * 0.76, S * 0.22, S * 0.86, S * 0.78], fill=(205, 150, 100, 255))
        d.line([(S * 0.8, S * 0.5), (S * 0.95, S * 0.85)], fill=(255, 255, 255, 255), width=SS * 4)

    def sinker(d, S):
        d.line([(S * 0.5, S * 0.08), (S * 0.5, S * 0.3)], fill=(220, 220, 220, 255), width=SS * 5)
        d.ellipse([S * 0.25, S * 0.25, S * 0.75, S * 0.85], fill=(150, 160, 180, 255))
        d.ellipse([S * 0.33, S * 0.33, S * 0.5, S * 0.5], fill=(255, 255, 255, 150))
        d.line([(S * 0.5, S * 0.85), (S * 0.5, S * 0.95)], fill=(220, 220, 220, 255), width=SS * 5)

    def reel(d, S):
        d.ellipse([S * 0.18, S * 0.18, S * 0.72, S * 0.72], fill=(240, 200, 90, 255))
        d.ellipse([S * 0.3, S * 0.3, S * 0.6, S * 0.6], fill=(205, 150, 70, 255))
        d.ellipse([S * 0.4, S * 0.4, S * 0.5, S * 0.5], fill=(255, 240, 200, 255))
        d.line([(S * 0.45, S * 0.45), (S * 0.85, S * 0.75)], fill=(160, 120, 80, 255), width=SS * 10)
        d.ellipse([S * 0.78, S * 0.68, S * 0.92, S * 0.82], fill=(255, 120, 100, 255))

    def book(d, S):
        d.rounded_rectangle([S * 0.16, S * 0.14, S * 0.84, S * 0.86], radius=S * 0.06, fill=(110, 185, 200, 255))
        d.rectangle([S * 0.16, S * 0.14, S * 0.26, S * 0.86], fill=(80, 150, 170, 255))
        d.ellipse([S * 0.36, S * 0.38, S * 0.72, S * 0.6], fill=(255, 220, 120, 255))
        d.polygon([(S * 0.38, S * 0.49), (S * 0.3, S * 0.4), (S * 0.3, S * 0.58)], fill=(255, 220, 120, 255))
        d.ellipse([S * 0.62, S * 0.44, S * 0.67, S * 0.49], fill=COCOA)

    def upgrade(d, S):
        d.polygon([(S * 0.5, S * 0.12), (S * 0.86, S * 0.5), (S * 0.64, S * 0.5), (S * 0.64, S * 0.86),
                   (S * 0.36, S * 0.86), (S * 0.36, S * 0.5), (S * 0.14, S * 0.5)], fill=(130, 210, 130, 255))
        d.polygon([(S * 0.5, S * 0.2), (S * 0.7, S * 0.42), (S * 0.5, S * 0.4)], fill=(255, 255, 255, 140))

    def any_fish(d, S):
        for k, (x, y, c) in enumerate([(0.38, 0.38, (120, 180, 220, 255)), (0.62, 0.62, (255, 160, 90, 255))]):
            d.ellipse([S * (x - 0.2), S * (y - 0.12), S * (x + 0.2), S * (y + 0.12)], fill=c)
            d.polygon([(S * (x - 0.17), S * y), (S * (x - 0.3), S * (y - 0.1)), (S * (x - 0.3), S * (y + 0.1))], fill=c)
            d.ellipse([S * (x + 0.08), S * (y - 0.04), S * (x + 0.12), S * y], fill=COCOA)

    def big(d, S):
        d.ellipse([S * 0.12, S * 0.3, S * 0.78, S * 0.7], fill=(80, 120, 170, 255))
        d.polygon([(S * 0.16, S * 0.5), (S * 0.02, S * 0.3), (S * 0.02, S * 0.7)], fill=(80, 120, 170, 255))
        d.ellipse([S * 0.6, S * 0.42, S * 0.66, S * 0.48], fill=(255, 255, 255, 255))
        d.rounded_rectangle([S * 0.56, S * 0.62, S * 0.96, S * 0.9], radius=S * 0.05, fill=(255, 230, 140, 255))
        d.text((S * 0.62, S * 0.64), "kg", fill=COCOA)

    def bag(d, S):
        d.rounded_rectangle([S * 0.16, S * 0.3, S * 0.84, S * 0.88], radius=S * 0.14, fill=(205, 150, 100, 255))
        d.arc([S * 0.32, S * 0.1, S * 0.68, S * 0.46], 180, 360, fill=(160, 105, 70, 255), width=SS * 12)
        d.rounded_rectangle([S * 0.16, S * 0.3, S * 0.84, S * 0.46], radius=S * 0.1, fill=(230, 185, 135, 255))
        d.ellipse([S * 0.44, S * 0.4, S * 0.56, S * 0.52], fill=(255, 220, 120, 255))
        d.ellipse([S * 0.3, S * 0.58, S * 0.62, S * 0.76], fill=(120, 180, 220, 255))
        d.polygon([(S * 0.6, S * 0.67), (S * 0.72, S * 0.58), (S * 0.72, S * 0.76)], fill=(120, 180, 220, 255))
        d.ellipse([S * 0.35, S * 0.63, S * 0.39, S * 0.67], fill=COCOA)

    for name, fn in [("ICON_Bag", bag), ("ICON_Line", line_spool), ("ICON_Sinker", sinker), ("ICON_Reel", reel), ("ICON_Book", book),
                     ("ICON_Upgrade", upgrade), ("ICON_AnyFish", any_fish), ("ICON_Big", big)]:
        save(icon(fn), "Ui", f"{name}.png")


if __name__ == "__main__":
    fish_icons()
    world()
    icons()
