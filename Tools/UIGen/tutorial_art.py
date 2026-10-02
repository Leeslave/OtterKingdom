# 튜토리얼 강조 테두리 (9-slice 링): 머스터드 링 + 코코아 외곽선. 실행: python Tools/UIGen/tutorial_art.py
import os
from PIL import Image, ImageDraw
from ui_gen import COCOA, MUSTARD

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "UI", "Tutorial")
SS = 4


def ring(size=160, radius=44, width=14, outline=4):
    im = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = lambda v: v * SS
    # 바깥 코코아 → 머스터드 → 안쪽 코코아 → 투명
    d.rounded_rectangle((0, 0, s(size) - 1, s(size) - 1), s(radius), fill=COCOA)
    o = outline
    d.rounded_rectangle((s(o), s(o), s(size - o) - 1, s(size - o) - 1), s(radius - o), fill=MUSTARD)
    i = outline + width
    d.rounded_rectangle((s(i), s(i), s(size - i) - 1, s(size - i) - 1), s(radius - i), fill=COCOA)
    j = i + outline
    d.rounded_rectangle((s(j), s(j), s(size - j) - 1, s(size - j) - 1), s(max(2, radius - j)), fill=(0, 0, 0, 0))
    return im.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    ring().save(os.path.join(OUT, "UI_Tutorial_Highlight.png"))
    print("saved UI_Tutorial_Highlight")
