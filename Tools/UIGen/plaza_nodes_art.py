# 광장 바위·나무의 작은 연출 그림: 튀는 돌 조각 / 나뭇잎 / 약점 반짝이 / 다시 생기기까지 작은 시계(FX_Regrow_0~7)
# 분위기 연출: 발밑 그림자(FX_Shadow) / 나비 두 장(FX_Butterfly_0~1, 색은 코드에서 입힘) / 구름 그림자(FX_CloudShadow)
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




# 다시 생기기까지 남은 정도를 보여 주는 작은 시계: 크림 원에 초록 부채꼴이 12시부터 시계 방향으로 참
REGROW_STEPS = 8


def regrow_clock():
    s = 64 * SS
    for step in range(REGROW_STEPS):
        img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        o = 5 * SS
        d.ellipse([0, 0, s - 1, s - 1], fill=COCOA)
        d.ellipse([o, o, s - 1 - o, s - 1 - o], fill=(0xFF, 0xF4, 0xE6, 255))
        fill = (step + 1) / REGROW_STEPS
        inner = o + 3 * SS
        if fill > 0:
            d.pieslice([inner, inner, s - 1 - inner, s - 1 - inner], -90, -90 + 360 * fill, fill=(0x8C, 0xC8, 0x4B, 255))
        # 바늘
        d.line([(s / 2, s / 2), (s / 2, inner + 2 * SS)], fill=COCOA, width=4 * SS)
        d.ellipse([s / 2 - 4 * SS, s / 2 - 4 * SS, s / 2 + 4 * SS, s / 2 + 4 * SS], fill=COCOA)
        down(img, (64, 64)).save(os.path.join(OUT, f"FX_Regrow_{step}.png"))


def soft_ellipse(w, h, power):
    """가운데가 진하고 가장자리로 갈수록 흐려지는 타원 (흰색, 알파만)"""
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            dx = (x + 0.5 - w / 2) / (w / 2)
            dy = (y + 0.5 - h / 2) / (h / 2)
            d = (dx * dx + dy * dy) ** 0.5
            if d < 1:
                px[x, y] = (255, 255, 255, int(255 * (1 - d) ** power))
    return img


def shadow():
    soft_ellipse(128, 64, 0.55).save(os.path.join(OUT, "FX_Shadow.png"))


def cloud_shadow():
    # 몽글몽글한 구름 모양: 타원 몇 개를 겹쳐 흐리게
    w, h = 512, 256
    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    for cx, cy, rw, rh in ((0.35, 0.55, 0.32, 0.36), (0.55, 0.42, 0.30, 0.34), (0.70, 0.58, 0.26, 0.30), (0.50, 0.65, 0.36, 0.28)):
        blob = soft_ellipse(int(w * rw * 2), int(h * rh * 2), 1.2)
        img.alpha_composite(blob, (int(w * cx - blob.width / 2), int(h * cy - blob.height / 2)))
    img.save(os.path.join(OUT, "FX_CloudShadow.png"))


def butterfly():
    # 날개를 편 것 / 접은 것 두 장. 날개는 밝은 흰색 (코드에서 분홍·노랑·하늘색을 곱함)
    s = 64 * SS
    for frame, spread in enumerate((1.0, 0.45)):
        img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        c = s / 2
        o = 4 * SS
        for side in (-1, 1):
            # 윗날개, 아랫날개
            for (wx, wy, rw, rh) in ((0.20, -0.10, 0.20, 0.20), (0.15, 0.13, 0.14, 0.14)):
                x = c + side * wx * s * spread
                y = c + wy * s
                rx = rw * s * spread
                ry = rh * s
                d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=COCOA)
                d.ellipse([x - rx + o, y - ry + o, x + rx - o, y + ry - o], fill=(255, 255, 255, 255))
                d.ellipse([x - rx * 0.3, y - ry * 0.3, x + rx * 0.3, y + ry * 0.3], fill=(255, 236, 214, 255))
        d.rounded_rectangle([c - 3 * SS, c - 0.18 * s, c + 3 * SS, c + 0.2 * s], radius=3 * SS, fill=COCOA)
        down(img, (64, 64)).save(os.path.join(OUT, f"FX_Butterfly_{frame}.png"))


def main():
    shadow()
    cloud_shadow()
    butterfly()
    regrow_clock()
    chip()
    leaf()
    sparkle()
    print("saved plaza fx art")


if __name__ == "__main__":
    main()
