"""광장 농장 부품 그림 (Docs/농장_설계형_기획.md) — 밭 칸 흙 3단계 · 수확 상자 · 흙길 · 돌길 · 스프링클러.

스티커 스타일(코코아 외곽선, 파스텔). 임시 그림: GPT 그림이 오면 같은 이름으로 덮어쓰기 (이미 있는 파일은 --all이 아니면 그대로).
python Tools/UIGen/farm_parts_art.py
"""
import argparse
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Farm", "Plaza")
SS = 4
COCOA = (75, 46, 34, 255)


def shade(c, k):
    if k >= 0:
        return tuple(int(c[i] + (255 - c[i]) * k) for i in range(3)) + (255,)
    return tuple(int(c[i] + (60 - c[i]) * -k) for i in range(3)) + (255,)


def rounded(d, box, r, fill, outline=None, width=0):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=width)


def sparkle(d, x, y, r, color=(255, 250, 220, 255)):
    pts = []
    for i in range(8):
        a = i / 8 * math.tau - math.pi / 2
        rr = r if i % 2 == 0 else r * 0.28
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    d.polygon(pts, fill=color)


def soil(base, accent=None, sparkles=False, seed=1, size=384):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = S * 0.04
    rounded(d, [m, m, S - m, S - m], S * 0.1, shade(base, -0.25), COCOA, int(S * 0.025))
    inner = [m + S * 0.05, m + S * 0.05, S - m - S * 0.05, S - m - S * 0.05]
    rounded(d, inner, S * 0.07, base)
    rows = 3
    top, bottom = inner[1] + S * 0.04, inner[3] - S * 0.04
    h = (bottom - top) / rows
    for r in range(rows):
        y0 = top + r * h
        ridge = [inner[0] + S * 0.04, y0 + h * 0.12, inner[2] - S * 0.04, y0 + h * 0.78]
        rounded(d, ridge, h * 0.3, shade(base, 0.18))
        d.line([(ridge[0] + h * 0.2, ridge[3] + h * 0.06), (ridge[2] - h * 0.2, ridge[3] + h * 0.06)], fill=shade(base, -0.45), width=int(S * 0.012))
        d.line([(ridge[0] + h * 0.3, ridge[1] + h * 0.14), (ridge[2] - h * 0.5, ridge[1] + h * 0.14)], fill=shade(base, 0.38), width=int(S * 0.008))
    rng = random.Random(seed)
    for _ in range(26):
        x, y = rng.uniform(inner[0] + 20, inner[2] - 20), rng.uniform(inner[1] + 20, inner[3] - 20)
        r = rng.uniform(S * 0.006, S * 0.012)
        d.ellipse([x - r, y - r, x + r, y + r], fill=shade(base, -0.35 if rng.random() < 0.6 else 0.3))
    if accent:
        for _ in range(10):
            x, y = rng.uniform(inner[0] + 40, inner[2] - 40), rng.uniform(inner[1] + 40, inner[3] - 40)
            r = S * 0.016
            d.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=accent)
    if sparkles:
        for _ in range(7):
            sparkle(d, rng.uniform(inner[0] + 60, inner[2] - 60), rng.uniform(inner[1] + 60, inner[3] - 60), S * 0.03)
    return img.resize((size, size), Image.LANCZOS)


def crate(size=192):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    wood = (206, 146, 88, 255)
    d.ellipse([S * 0.1, S * 0.82, S * 0.9, S * 0.96], fill=(60, 36, 40, 60))
    front = [S * 0.12, S * 0.36, S * 0.88, S * 0.9]
    top = [(S * 0.12, S * 0.36), (S * 0.24, S * 0.16), (S * 0.96, S * 0.16), (S * 0.88, S * 0.36)]
    d.polygon(top, fill=shade(wood, 0.2))
    inner = [(S * 0.2, S * 0.33), (S * 0.27, S * 0.21), (S * 0.9, S * 0.21), (S * 0.83, S * 0.33)]
    d.polygon(inner, fill=(92, 58, 40, 255))
    d.rectangle(front, fill=wood)
    for k in range(1, 3):
        y = front[1] + (front[3] - front[1]) * k / 3
        d.line([(front[0], y), (front[2], y)], fill=shade(wood, -0.35), width=int(S * 0.012))
    d.line([(front[0], front[1]), (front[2], front[3])], fill=shade(wood, -0.2), width=int(S * 0.04))
    side = [(S * 0.88, S * 0.36), (S * 0.96, S * 0.16), (S * 0.96, S * 0.72), (S * 0.88, S * 0.9)]
    d.polygon(side, fill=shade(wood, -0.25))
    for poly in (top, side):
        d.line(poly + [poly[0]], fill=COCOA, width=int(S * 0.02), joint="curve")
    d.rectangle(front, outline=COCOA, width=int(S * 0.02))
    return img.resize((size, size), Image.LANCZOS)


def path(stone=False, size=128, seed=3):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    base = (226, 196, 150, 255) if not stone else (196, 190, 182, 255)
    d.rectangle([0, 0, S, S], fill=base)
    rng = random.Random(seed)
    if stone:
        for gy in range(3):
            for gx in range(3):
                x0 = gx * S / 3 + (S / 6 if gy % 2 else 0) - S / 6 * (gy % 2)
                y0 = gy * S / 3
                box = [x0 + S * 0.02, y0 + S * 0.02, x0 + S / 3 - S * 0.02, y0 + S / 3 - S * 0.02]
                c = shade((176, 170, 164), rng.uniform(-0.1, 0.25))
                rounded(d, box, S * 0.06, c, shade((120, 112, 104), 0), int(S * 0.01))
    else:
        for _ in range(14):
            x, y = rng.uniform(0, S), rng.uniform(0, S)
            r = rng.uniform(S * 0.015, S * 0.04)
            d.ellipse([x - r, y - r * 0.7, x + r, y + r * 0.7], fill=shade(base, rng.choice([-0.2, 0.25])))
    img = img.resize((size, size), Image.LANCZOS)
    # 가장자리를 부드럽게 (이웃 길과 이어지게)
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([3, 3, size - 3, size - 3], radius=size * 0.12, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(3))
    img.putalpha(mask)
    return img


def sprinkler(size=192):
    S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([S * 0.25, S * 0.84, S * 0.75, S * 0.95], fill=(60, 36, 40, 60))
    # 물줄기
    for a in (-60, -30, 30, 60):
        rad = math.radians(a - 90)
        pts = []
        for i in range(12):
            t = i / 11
            x = S * 0.5 + math.cos(rad) * S * 0.42 * t
            y = S * 0.36 + math.sin(rad) * S * 0.28 * t + (S * 0.35 * t * t)
            pts.append((x, y))
        d.line(pts, fill=(120, 196, 240, 220), width=int(S * 0.025), joint="curve")
    post = [S * 0.45, S * 0.42, S * 0.55, S * 0.9]
    d.rectangle(post, fill=(186, 128, 80, 255), outline=COCOA, width=int(S * 0.018))
    head = [S * 0.34, S * 0.3, S * 0.66, S * 0.46]
    rounded(d, head, S * 0.06, (232, 188, 90, 255), COCOA, int(S * 0.02))
    d.ellipse([S * 0.44, S * 0.22, S * 0.56, S * 0.32], fill=(120, 196, 240, 255), outline=COCOA, width=int(S * 0.015))
    d.ellipse([S * 0.38, S * 0.33, S * 0.46, S * 0.37], fill=(255, 250, 220, 220))
    return img.resize((size, size), Image.LANCZOS)


def save(img, name):
    path = os.path.join(OUT, name)
    if os.path.exists(path) and not ARGS.all:
        return
    os.makedirs(OUT, exist_ok=True)
    img.save(path)
    print("그림", os.path.relpath(path, ROOT))


def main():
    save(soil((168, 116, 74)), "FARM_Soil_0.png")
    save(soil((122, 78, 48), accent=(126, 186, 96, 255), seed=2), "FARM_Soil_1.png")
    save(soil((196, 146, 72), accent=(250, 214, 100, 255), sparkles=True, seed=4), "FARM_Soil_2.png")
    save(crate(), "FARM_Crate.png")
    save(path(), "FARM_Path_Dirt.png")
    save(path(stone=True), "FARM_Path_Stone.png")
    save(sprinkler(), "FARM_Sprinkler.png")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true")
    ARGS = ap.parse_args()
    main()
