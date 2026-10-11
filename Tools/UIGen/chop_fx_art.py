# 나무 베기 · 바위 캐기 손맛 그림 (스티커 풍: 코코아 #4B2E22 외곽선, 파스텔). 4배로 그려 줄여서 매끈하게.
# - Assets/Art/Settlement/FX_Axe.png       도끼 (세로, 손잡이 끝이 아래 가운데, 날은 위 왼쪽)
# - Assets/Art/Settlement/FX_Pickaxe.png   곡괭이 (세로, 머리가 위에서 좌우로)
# - Assets/Art/Settlement/FX_WoodChip.png  나무 조각
# 먼지구름은 원래 있던 Assets/Art/Settlement/FX_Dust.png (건설 연출과 같은 것)을 쓴다
# 더 좋은 그림(ChatGPT)으로 바꿀 때: python Tools/UIGen/chop_fx_art.py --import axe=도끼.png pickaxe=곡괭이.png chip=조각.png
#   → 투명 여백을 자르고, 도구는 손잡이 끝이 아래 가운데에 오게 맞춰 같은 이름으로 넣음 (원본은 ArtSource/Chop에 보관)
# 실행(코드로 그린 임시 그림): python Tools/UIGen/chop_fx_art.py
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")
COCOA = (75, 46, 34, 255)
K = 4  # 그릴 때 배율


def canvas(size):
    return Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))


def finish(img, size, name):
    img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name))


def outlined_polygon(d, points, fill, width):
    d.polygon(points, fill=COCOA)
    # 안쪽으로 줄인 다각형 대신: 외곽선 두께만큼 선을 그리고 그 안을 채움
    d.line(points + [points[0]], fill=COCOA, width=width, joint="curve")
    d.polygon(points, fill=fill)
    d.line(points + [points[0]], fill=COCOA, width=width, joint="curve")


def handle(d, size, top, bottom, w):
    cx = size * K / 2
    x0, x1 = cx - w / 2, cx + w / 2
    d.rounded_rectangle((x0 - 8 * K, top - 8 * K, x1 + 8 * K, bottom + 8 * K), radius=(w / 2 + 8 * K), fill=COCOA)
    d.rounded_rectangle((x0, top, x1, bottom), radius=w / 2, fill=(196, 132, 80, 255))
    # 나뭇결 하이라이트
    d.rounded_rectangle((x0 + w * 0.18, top + 10 * K, x0 + w * 0.42, bottom - 10 * K), radius=w * 0.12, fill=(226, 170, 112, 255))


def axe():
    size = 256
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = K
    handle(d, size, 52 * s, 238 * s, 22 * s)
    cx = size * s / 2
    # 날: 손잡이 위쪽 왼편으로 넓게 퍼지는 부채꼴
    blade = [(cx + 6 * s, 38 * s), (cx - 30 * s, 30 * s), (cx - 78 * s, 22 * s), (cx - 92 * s, 60 * s),
             (cx - 90 * s, 104 * s), (cx - 74 * s, 132 * s), (cx - 30 * s, 116 * s), (cx + 6 * s, 104 * s)]
    outlined_polygon(d, blade, (204, 210, 220, 255), 9 * s)
    # 날 끝 반짝이는 띠
    edge = [(cx - 78 * s, 28 * s), (cx - 88 * s, 60 * s), (cx - 86 * s, 100 * s), (cx - 72 * s, 124 * s),
            (cx - 62 * s, 120 * s), (cx - 74 * s, 98 * s), (cx - 76 * s, 60 * s), (cx - 68 * s, 32 * s)]
    d.polygon(edge, fill=(244, 247, 252, 255))
    # 머리 뒤쪽 뭉툭한 부분 + 묶음
    outlined_polygon(d, [(cx - 6 * s, 30 * s), (cx + 26 * s, 34 * s), (cx + 26 * s, 108 * s), (cx - 6 * s, 112 * s)],
                     (150, 158, 172, 255), 9 * s)
    d.rounded_rectangle((cx - 16 * s, 118 * s, cx + 16 * s, 132 * s), radius=5 * s, fill=COCOA)
    d.rounded_rectangle((cx - 12 * s, 121 * s, cx + 12 * s, 129 * s), radius=3 * s, fill=(232, 96, 92, 255))
    finish(img, size, "FX_Axe.png")


def pickaxe():
    size = 256
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = K
    handle(d, size, 50 * s, 238 * s, 22 * s)
    cx = size * s / 2
    # 머리: 위로 볼록한 초승달 (양 끝이 뾰족)
    head = [(cx - 112 * s, 92 * s), (cx - 84 * s, 54 * s), (cx - 40 * s, 30 * s), (cx, 24 * s), (cx + 40 * s, 30 * s),
            (cx + 84 * s, 54 * s), (cx + 112 * s, 92 * s), (cx + 74 * s, 74 * s), (cx + 36 * s, 62 * s),
            (cx, 58 * s), (cx - 36 * s, 62 * s), (cx - 74 * s, 74 * s)]
    outlined_polygon(d, head, (196, 202, 214, 255), 9 * s)
    d.polygon([(cx - 70 * s, 58 * s), (cx - 36 * s, 40 * s), (cx, 34 * s), (cx + 20 * s, 36 * s), (cx - 4 * s, 44 * s),
               (cx - 38 * s, 50 * s)], fill=(240, 244, 250, 255))
    d.rounded_rectangle((cx - 18 * s, 50 * s, cx + 18 * s, 70 * s), radius=6 * s, fill=COCOA)
    d.rounded_rectangle((cx - 13 * s, 54 * s, cx + 13 * s, 66 * s), radius=4 * s, fill=(110, 116, 130, 255))
    finish(img, size, "FX_Pickaxe.png")


def wood_chip():
    size = 64
    img = canvas(size)
    d = ImageDraw.Draw(img)
    s = K
    pts = [(10 * s, 30 * s), (30 * s, 10 * s), (54 * s, 22 * s), (48 * s, 46 * s), (22 * s, 52 * s)]
    outlined_polygon(d, pts, (232, 184, 120, 255), 5 * s)
    d.polygon([(30 * s, 16 * s), (48 * s, 25 * s), (44 * s, 40 * s), (28 * s, 30 * s)], fill=(246, 214, 160, 255))
    finish(img, size, "FX_WoodChip.png")


def dust():
    size = 128
    img = canvas(size)
    a = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(a)
    s = K
    for cx, cy, r in ((44, 74, 30), (78, 70, 34), (62, 48, 30), (96, 86, 20), (28, 90, 18)):
        d.ellipse(((cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s), fill=255)
    a = a.filter(ImageFilter.GaussianBlur(3 * s))
    base = Image.new("RGBA", img.size, (255, 255, 255, 0))
    base.putalpha(a.point(lambda v: int(v * 0.95)))
    # 아래쪽을 살짝 어둡게 (색을 곱해도 입체감이 남게)
    shade = Image.new("L", img.size, 0)
    ImageDraw.Draw(shade).rectangle((0, 70 * s, size * s, size * s), fill=60)
    shade = shade.filter(ImageFilter.GaussianBlur(10 * s))
    rgb = Image.new("RGBA", img.size, (255, 255, 255, 255))
    dark = Image.new("RGBA", img.size, (205, 196, 184, 255))
    rgb = Image.composite(dark, rgb, shade)
    rgb.putalpha(base.getchannel("A"))
    finish(rgb, size, "FX_Dust.png")


def trim(im):
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    return im.crop(bbox)


def import_tool(src, name, size=512):
    """도구: 손잡이 끝(맨 아래 줄들의 가운데)이 그림 가로 가운데에 오게 좌우를 채우고 정사각형으로"""
    im = trim(Image.open(src).convert("RGBA"))
    w, h = im.size
    alpha = im.getchannel("A").load()
    xs = [x for y in range(int(h * 0.9), h) for x in range(w) if alpha[x, y] > 128]
    grip = (min(xs) + max(xs)) / 2 if xs else w / 2
    half = max(grip, w - grip)
    canvas_w = int(half * 2)
    side = max(canvas_w, h)
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    out.alpha_composite(im, (int(side / 2 - grip), side - h))
    out.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name))


def import_icon(src, name, size=128):
    im = trim(Image.open(src).convert("RGBA"))
    side = max(im.size)
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    out.alpha_composite(im, ((side - im.width) // 2, (side - im.height) // 2))
    out.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name))


def import_art(pairs):
    import shutil
    archive = os.path.join(ROOT, "ArtSource", "Chop")
    os.makedirs(archive, exist_ok=True)
    for pair in pairs:
        key, src = pair.split("=", 1)
        shutil.copy(src, os.path.join(archive, key + os.path.splitext(src)[1]))
        if key == "axe":
            import_tool(src, "FX_Axe.png")
        elif key == "pickaxe":
            import_tool(src, "FX_Pickaxe.png")
        elif key == "chip":
            import_icon(src, "FX_WoodChip.png")
        print("imported", key)


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1 and sys.argv[1] == "--import":
        import_art(sys.argv[2:])
        sys.exit(0)
    axe()
    pickaxe()
    wood_chip()
    print("chop fx ok")
