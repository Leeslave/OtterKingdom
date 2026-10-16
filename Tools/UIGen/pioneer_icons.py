# 개척 기획 부탁 아이콘: 의자(광장 벤치 그림), 광산 길 열기(이동 화면의 광산 아이콘)
# 정사각 캔버스 가운데에 맞춰 Assets/Art/Settlement/ICON_Chair.png, ICON_MinePath.png로 저장
# 실행: python Tools/UIGen/pioneer_icons.py
import os

from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")
SIZE = 256
MARGIN = 12

SOURCES = {
    "ICON_Chair": os.path.join(ROOT, "Assets", "Art", "Plaza", "Props", "Prop_Bench.png"),
    "ICON_MinePath": os.path.join(ROOT, "Assets", "Art", "UI", "Travel", "ICON_Place_Mine.png"),
}


def square(path):
    img = Image.open(path).convert("RGBA")
    img = img.crop(img.getbbox())
    inner = SIZE - MARGIN * 2
    scale = inner / max(img.size)
    img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((SIZE - img.width) // 2, (SIZE - img.height) // 2))
    return canvas


def main():
    for name, path in SOURCES.items():
        square(path).save(os.path.join(OUT, f"{name}.png"))
        print("saved", name)


if __name__ == "__main__":
    main()
