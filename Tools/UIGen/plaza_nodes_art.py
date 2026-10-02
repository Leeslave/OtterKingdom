# 광장 바위·나무의 작은 연출 그림: 튀는 돌 조각 / 나뭇잎 / 약점 반짝이
# (바위·사과나무·사과는 GPT 그림 → fit_plaza_nodes.py)
# 실행: python Tools/UIGen/plaza_nodes_art.py
import math
import os

from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")

COCOA = (0x4B, 0x2E, 0x22, 255)
SS = 4  # 크게 그린 뒤 줄여서 테두리를 부드럽게


def down(img, size):
    return img.resize(size, Image.LANCZOS)










def chip():
    s = 64 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = [(0.2, 0.35), (0.55, 0.15), (0.85, 0.4), (0.7, 0.85), (0.3, 0.8)]
    d.polygon([(x * s, y * s) for x, y in pts], fill=COCOA)
    inner = [(0.27, 0.38), (0.55, 0.23), (0.77, 0.42), (0.65, 0.76), (0.34, 0.72)]
    d.polygon([(x * s, y * s) for x, y in inner], fill=(0xC9, 0xC5, 0xBE, 255))
    down(img, (64, 64)).save(os.path.join(OUT, "FX_StoneChip.png"))


def leaf():
    s = 64 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0.1 * s, 0.3 * s, 0.9 * s, 0.7 * s], fill=COCOA)
    d.ellipse([0.16 * s, 0.36 * s, 0.84 * s, 0.64 * s], fill=(0x8C, 0xC8, 0x4B, 255))
    d.line([(0.2 * s, 0.5 * s), (0.8 * s, 0.5 * s)], fill=(0x5E, 0x9A, 0x32, 255), width=3 * SS)
    img = img.rotate(30, resample=Image.BICUBIC)
    down(img, (64, 64)).save(os.path.join(OUT, "FX_Leaf.png"))


def sparkle():
    s = 96 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = s / 2

    def star(r_out, r_in, fill):
        pts = []
        for i in range(8):
            r = r_out if i % 2 == 0 else r_in
            a = -math.pi / 2 + i * math.pi / 4
            pts.append((c + math.cos(a) * r, c + math.sin(a) * r))
        d.polygon(pts, fill=fill)

    star(0.48 * s, 0.14 * s, COCOA)
    star(0.40 * s, 0.10 * s, (0xFF, 0xD8, 0x4A, 255))
    star(0.20 * s, 0.05 * s, (255, 255, 240, 255))
    down(img, (96, 96)).save(os.path.join(OUT, "FX_Sparkle.png"))




def main():
    chip()
    leaf()
    sparkle()
    print("saved plaza fx art")


if __name__ == "__main__":
    main()
