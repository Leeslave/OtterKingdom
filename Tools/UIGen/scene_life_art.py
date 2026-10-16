# 장소를 살아 보이게 하는 연출 그림 (흰색·밝은 색, 색과 투명도는 코드에서 입힘). Assets/Art/Settlement
# - FX_GlowSoft  등불 둘레 빛무리
# - FX_Ripple    물결 고리 (부두 기둥, 물고기가 뛴 자리)
# - FX_Droplet   물방울
# - FX_Smoke     굴뚝 연기 뭉치
# - FX_Petal_0/1 날리는 꽃잎 (분홍, 크림)
# - FX_Sunbeams  비스듬한 햇살 (화면 전체)
# - FX_Vignette  화면 가장자리를 살짝 어둡게 (가운데 투명)
# 실행: python Tools/UIGen/scene_life_art.py
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))


def radial(size, power, inner=0.0):
    """가운데 1 → 가장자리 0 (inner 안쪽은 1)"""
    w, h = size
    a = Image.new("L", size, 0)
    px = a.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot((x + 0.5 - w / 2) / (w / 2), (y + 0.5 - h / 2) / (h / 2))
            if d < 1:
                k = 1.0 if d <= inner else (1 - (d - inner) / (1 - inner))
                px[x, y] = int(255 * k ** power)
    return a


def with_alpha(alpha, color=(255, 255, 255)):
    img = Image.new("RGBA", alpha.size, color + (0,))
    img.putalpha(alpha)
    return img


def glow():
    save(with_alpha(radial((128, 128), 2.0)), "FX_GlowSoft")


def ripple():
    w, h = 256, 112
    a = Image.new("L", (w, h), 0)
    px = a.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot((x + 0.5 - w / 2) / (w / 2), (y + 0.5 - h / 2) / (h / 2))
            px[x, y] = int(255 * math.exp(-((d - 0.86) / 0.06) ** 2))
    save(with_alpha(a), "FX_Ripple")


def droplet():
    save(with_alpha(radial((32, 32), 1.2, 0.35)), "FX_Droplet")


def smoke():
    n = 160
    a = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(a)
    rnd = random.Random(7)
    for _ in range(7):
        r = rnd.uniform(0.18, 0.28) * n
        cx = n / 2 + rnd.uniform(-0.16, 0.16) * n
        cy = n / 2 + rnd.uniform(-0.12, 0.14) * n
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    a = a.filter(ImageFilter.GaussianBlur(9))
    a = ImageChops.multiply(a, radial((n, n), 0.6, 0.55))
    # 아래쪽은 살짝 회색 (뭉게 그림자)
    img = Image.new("RGBA", (n, n))
    px = img.load()
    for y in range(n):
        shade = int(255 - 26 * (y / n))
        for x in range(n):
            px[x, y] = (shade, shade, min(255, shade + 4), 0)
    img.putalpha(a)
    save(img, "FX_Smoke")


def petal(color, rim, name):
    """벚꽃 꽃잎: 위는 넓고 가운데가 살짝 갈라지고, 아래(꽃받침 쪽)로 갈수록 좁아짐"""
    n = 64
    big = 4 * n
    a = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(a)
    pts = []
    for i in range(96):
        t = i / 96 * 2 * math.pi
        y = math.cos(t)          # 1 = 위, -1 = 아래
        x = math.sin(t)
        width = 0.30 * (0.45 + 0.55 * (1 + y) / 2)
        pts.append((big / 2 + x * width * big, big / 2 - y * 0.40 * big))
    d.polygon(pts, fill=255)
    d.polygon([(big / 2 - 0.07 * big, big * 0.08), (big / 2 + 0.07 * big, big * 0.08), (big / 2, big * 0.2)], fill=0)
    a = a.filter(ImageFilter.GaussianBlur(3)).resize((n, n), Image.LANCZOS)
    edge = a.filter(ImageFilter.MinFilter(5))
    rim_layer = Image.new("RGBA", (n, n), rim + (0,))
    rim_layer.putalpha(a)
    body = Image.new("RGBA", (n, n), color + (0,))
    body.putalpha(edge)
    save(Image.alpha_composite(rim_layer, body), name)


def sunbeams():
    n = 512
    a = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(a)
    rnd = random.Random(3)
    # 왼쪽 위에서 오른쪽 아래로 비스듬한 빛줄기 몇 개 (굵기·밝기 제각각)
    angle = math.radians(28)
    dx, dy = math.sin(angle), math.cos(angle)
    for i in range(6):
        x0 = rnd.uniform(-0.15, 0.85) * n
        width = rnd.uniform(0.05, 0.13) * n
        level = int(rnd.uniform(110, 230))
        poly = [(x0, -10), (x0 + width, -10), (x0 + width + dx * n * 1.5, dy * n * 1.5), (x0 + dx * n * 1.5, dy * n * 1.5)]
        layer = Image.new("L", (n, n), 0)
        ImageDraw.Draw(layer).polygon(poly, fill=level)
        a = ImageChops.lighter(a, layer)
    a = a.filter(ImageFilter.GaussianBlur(18))
    # 위에서 아래로 갈수록 흐려짐
    fade = Image.new("L", (n, n))
    fp = fade.load()
    for y in range(n):
        v = int(255 * max(0.0, 1 - y / n) ** 1.3)
        for x in range(n):
            fp[x, y] = v
    a = ImageChops.multiply(a, fade)
    save(with_alpha(a, (255, 246, 214)), "FX_Sunbeams")


def vignette():
    n = 256
    a = Image.new("L", (n, n), 0)
    px = a.load()
    for y in range(n):
        for x in range(n):
            dxx = (x + 0.5 - n / 2) / (n / 2)
            dyy = (y + 0.5 - n / 2) / (n / 2)
            # 둥근 사각형 거리 (모서리가 가장 어둡게)
            d = (abs(dxx) ** 2.6 + abs(dyy) ** 2.6) ** (1 / 2.6)
            k = max(0.0, min(1.0, (d - 0.62) / 0.5))
            px[x, y] = int(255 * k ** 1.6)
    save(with_alpha(a, (0, 0, 0)), "FX_Vignette")


def main():
    os.makedirs(OUT, exist_ok=True)
    glow()
    ripple()
    droplet()
    smoke()
    petal((255, 190, 205), (214, 120, 150), "FX_Petal_0")
    petal((255, 246, 228), (210, 180, 150), "FX_Petal_1")
    sunbeams()
    vignette()
    print("saved scene life art")


if __name__ == "__main__":
    main()
