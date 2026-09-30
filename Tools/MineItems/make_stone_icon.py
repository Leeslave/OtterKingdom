"""Placeholder icon for the stone item (ore_stone) until real art arrives.

Draws a faceted grey rock in the same flat, brown-outlined style as the
diamond icon (drawn at 4x and downsampled for smooth edges).

Usage: python Tools/MineItems/make_stone_icon.py [output path]
       (default: Assets/Art/Item/Ore/Stone.png)
"""
import os
import sys

from PIL import Image, ImageDraw

SS = 4
SIZE = 512
OUTLINE = (92, 46, 28, 255)  # the diamond icon's brown outline
BASE = (160, 160, 168, 255)
LIGHT = (196, 197, 204, 255)
LIGHTEST = (226, 227, 232, 255)
DARK = (122, 121, 130, 255)
DARKEST = (96, 95, 104, 255)
OUTLINE_W = 18

# Rock silhouette and facets, in 512px space.
SILHOUETTE = [(70, 330), (110, 200), (200, 130), (320, 120), (420, 185),
              (460, 300), (420, 390), (290, 420), (150, 405)]
FACETS = [
    ([(110, 200), (200, 130), (320, 120), (270, 215), (160, 240)], LIGHTEST),
    ([(320, 120), (420, 185), (390, 265), (270, 215)], LIGHT),
    ([(70, 330), (110, 200), (160, 240), (180, 330)], BASE),
    ([(160, 240), (270, 215), (390, 265), (330, 330), (180, 330)], LIGHT),
    ([(390, 265), (420, 185), (460, 300), (420, 390), (330, 330)], DARK),
    ([(70, 330), (180, 330), (330, 330), (420, 390), (290, 420), (150, 405)], DARKEST),
]
EDGES = [
    [(160, 240), (270, 215), (390, 265)],
    [(270, 215), (320, 120)],
    [(160, 240), (180, 330), (330, 330), (390, 265)],
    [(330, 330), (420, 390)],
]


def s(points):
    return [(x * SS, y * SS) for x, y in points]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "Assets/Art/Item/Ore/Stone.png"
    im = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    # Outline: the silhouette drawn fat, facets on top.
    d.polygon(s(SILHOUETTE), fill=OUTLINE)
    d.line(s(SILHOUETTE + SILHOUETTE[:1]), fill=OUTLINE, width=OUTLINE_W * 2 * SS, joint="curve")
    for pts, color in FACETS:
        d.polygon(s(pts), fill=color)
    for pts in EDGES:
        d.line(s(pts), fill=OUTLINE, width=6 * SS, joint="curve")
    # A small highlight like the diamond's.
    d.ellipse(s([(200, 160), (250, 185)]), fill=(250, 250, 252, 255))

    im = im.resize((SIZE, SIZE), Image.LANCZOS)
    im = im.crop(im.getbbox())
    os.makedirs(os.path.dirname(out), exist_ok=True)
    im.save(out)
    print("wrote", out, im.size)


if __name__ == "__main__":
    main()
