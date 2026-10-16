# 장난감 배치용 그림(광장에 놓였을 때) 넣기. 아이콘(Assets/Art/Item/Toy/ICON_Toy_*.png)과 따로 쓴다.
# - 아이콘: 정면 · 가방/뽑기 카드용
# - 배치용: 광장 소품(Prop_Bench 등)처럼 위에서 비스듬히 내려다본 모습, 바닥에 놓인 느낌
# ChatGPT로 만든 그림을 넣을 때:
#   python Tools/UIGen/toy_world_art.py --import BeachBall=비치볼.png Drum=북.png ...
#   python Tools/UIGen/toy_world_art.py --dir 받은폴더   (파일 이름에 장난감 이름이 들어 있으면 알아서 맞춤: beachball.png, WORLD_Toy_Drum.png 등)
#   → 배경이 투명하지 않으면 가장자리부터 흰/밝은 배경을 지우고, 투명 여백을 잘라 Assets/Art/Decor/World/WORLD_Toy_<이름>.png 로 넣음
#     (원본은 ArtSource/ToyWorld에 보관). 그다음 Unity 메뉴 Tools/Decor/Apply Toy World Art 로 장난감 데이터에 연결
# 장난감 이름: 아래 TOYS
import os
import shutil
import sys

from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Decor", "World")
ARCHIVE = os.path.join(ROOT, "ArtSource", "ToyWorld")
MAX_SIDE = 512
PAD = 0.04  # 잘라낸 뒤 둘레 여백 (긴 변 대비)

TOYS = ["BeachBall", "Bubbles", "ButterflyMobile", "Carousel", "CherryPinwheel", "Drum", "HotAirBalloon", "Kite",
        "PaperBoat", "PetalKite", "Puzzle", "RubberDuck", "SandBucket", "SoccerBall", "Yoyo"]


def has_transparency(im):
    lo, _ = im.getchannel("A").getextrema()
    return lo < 250


def remove_background(im, tolerance=28):
    """투명하지 않은 그림: 네 모서리에서 이어진 밝은 배경을 투명하게 (외곽선 안쪽은 닿지 않음)"""
    im = im.copy()
    w, h = im.size
    marker = (255, 0, 255, 0)
    for corner in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        r, g, b, a = im.getpixel(corner)
        if a == 0 or min(r, g, b) < 200:
            continue
        ImageDraw.floodfill(im, corner, marker, thresh=tolerance)
    px = im.load()
    for y in range(h):
        for x in range(w):
            if px[x, y] == marker:
                px[x, y] = (0, 0, 0, 0)
    return im


def trim(im):
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    return im.crop(bbox) if bbox else im


def place(src, toy):
    im = Image.open(src).convert("RGBA")
    if not has_transparency(im):
        im = remove_background(im)
    im = trim(im)
    pad = int(max(im.size) * PAD)
    canvas = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    canvas.alpha_composite(im, (pad, pad))
    scale = min(1.0, MAX_SIDE / max(canvas.size))
    if scale < 1.0:
        canvas = canvas.resize((round(canvas.width * scale), round(canvas.height * scale)), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(ARCHIVE, exist_ok=True)
    shutil.copy(src, os.path.join(ARCHIVE, toy + os.path.splitext(src)[1].lower()))
    canvas.save(os.path.join(OUT, f"WORLD_Toy_{toy}.png"))
    print(f"imported {toy} {canvas.size}")


def match_toy(filename):
    key = os.path.splitext(os.path.basename(filename))[0].lower().replace("_", "").replace("-", "").replace(" ", "")
    # 긴 이름부터 (PetalKite가 Kite로 잘못 맞지 않게)
    for toy in sorted(TOYS, key=len, reverse=True):
        if toy.lower() in key:
            return toy
    return None


def main(args):
    if len(args) >= 2 and args[0] == "--dir":
        for name in sorted(os.listdir(args[1])):
            if not name.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                continue
            toy = match_toy(name)
            if toy is None:
                print(f"skip {name} (장난감 이름을 못 찾음)")
                continue
            place(os.path.join(args[1], name), toy)
        return 0
    if len(args) >= 2 and args[0] == "--import":
        for pair in args[1:]:
            toy, src = pair.split("=", 1)
            if toy not in TOYS:
                print(f"모르는 장난감: {toy} (가능: {', '.join(TOYS)})")
                return 1
            place(src, toy)
        return 0
    print(__doc__ or "사용법은 파일 맨 위 주석을 보세요")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
