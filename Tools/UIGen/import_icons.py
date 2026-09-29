"""Normalize ChatGPT icon renders into game icons: trim, center, fit to 256x256 (8px margin).
The full-res original is copied to ArtSource/Icons (outside Assets, so Unity ignores it).
usage: python import_icons.py <out_dir_under_Assets> <src_file>=<ICON_Name> [<src_file>=<ICON_Name> ...]"""
import os, shutil, sys
from PIL import Image

SIZE, MARGIN = 256, 8
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def normalize(src, dst):
    im = Image.open(src).convert("RGBA")
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()  # ignore faint stray pixels
    im = im.crop(bbox)
    k = (SIZE - 2 * MARGIN) / max(im.size)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.alpha_composite(im, ((SIZE - im.width) // 2, (SIZE - im.height) // 2))
    out.save(dst)
    return im.size


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    archive = os.path.join(ROOT, "ArtSource", "Icons")
    os.makedirs(archive, exist_ok=True)
    for pair in sys.argv[2:]:
        src, name = pair.split("=")
        shutil.copy(src, os.path.join(archive, name + os.path.splitext(src)[1]))
        size = normalize(src, os.path.join(out_dir, name + ".png"))
        print(f"{name:28s} content {size[0]}x{size[1]}")
