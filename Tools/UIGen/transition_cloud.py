# 장소 이동 때 화면을 덮었다 걷히는 뭉게구름 (스티커 스타일: 코코아 외곽선, 흰 몸통, 아래쪽 하늘빛 그늘)
# - 결과: Assets/Resources/UI/TransitionCloud.png (ScreenFader가 Resources에서 읽음)
# 실행: python Tools/UIGen/transition_cloud.py
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI")

SS = 2
W, H = 1024 * SS, 520 * SS
COCOA = (0x4B, 0x2E, 0x22, 255)
WHITE = (255, 253, 248, 255)
SHADE = (214, 230, 245, 255)

# 뭉게구름 = 원 여러 개 (가운데 x, 가운데 y, 반지름) — 캔버스 비율
PUFFS = [
    (0.16, 0.66, 0.15), (0.30, 0.50, 0.21), (0.48, 0.38, 0.26), (0.66, 0.46, 0.22),
    (0.82, 0.60, 0.16), (0.40, 0.70, 0.20), (0.62, 0.70, 0.20), (0.24, 0.74, 0.14), (0.78, 0.74, 0.14),
]


def circles(draw, grow, fill, dy=0.0):
    for cx, cy, r in PUFFS:
        rr = r * H + grow
        x, y = cx * W, cy * H + dy * H
        draw.ellipse([x - rr, y - rr, x + rr, y + rr], fill=fill)


def main():
    os.makedirs(OUT, exist_ok=True)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    circles(d, 9 * SS, COCOA)          # 외곽선
    circles(d, 0, SHADE)               # 아래 그늘
    body = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    circles(ImageDraw.Draw(body), -6 * SS, WHITE, dy=-0.035)  # 흰 몸통 (살짝 위로 → 아래에 그늘이 남음)
    # 몸통은 그늘 안쪽만
    mask = Image.new("L", (W, H), 0)
    circles(ImageDraw.Draw(mask), 0, 255)
    body.putalpha(Image.composite(body.split()[3], Image.new("L", (W, H), 0), mask))
    img.alpha_composite(body)
    img = img.resize((W // SS, H // SS), Image.LANCZOS)
    img.save(os.path.join(OUT, "TransitionCloud.png"))
    print("saved TransitionCloud", img.size)


if __name__ == "__main__":
    main()
