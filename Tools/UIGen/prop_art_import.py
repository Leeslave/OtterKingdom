# 광장 소품 임시 그림(p3_art.py 등)을 ChatGPT 그림으로 바꿔 넣기.
# 새 그림을 원래 그림의 캔버스 크기 · 그림이 차지하던 자리(투명 여백을 뺀 상자)에 맞춰 넣으므로
# 광장에서의 크기 · 발밑 기준점(피벗)이 그대로다 → Unity에서 따로 고칠 것이 없다.
#   python Tools/UIGen/prop_art_import.py SupplyBox=비축상자.png
#   (여러 개: CommunityTable=식탁.png FlowerPot=화분.png Clothesline=빨랫줄.png)
# 배경이 투명하지 않으면 가장자리부터 밝은 배경을 지움. 원본은 ArtSource/Props에 보관.
import os
import shutil
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from toy_world_art import has_transparency, remove_background, trim  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARCHIVE = os.path.join(ROOT, "ArtSource", "Props")
# 이름 → 그림 파일 (Assets 기준)
PROPS = {
    "SupplyBox": "Assets/Art/Settlement/Prop_SupplyBox.png",
    "CommunityTable": "Assets/Art/Settlement/Prop_CommunityTable.png",
    "FlowerPot": "Assets/Art/Settlement/Prop_FlowerPot.png",
    "Clothesline": "Assets/Art/Settlement/Prop_Clothesline.png",
}


def replace(name, src):
    dst = os.path.join(ROOT, PROPS[name])
    old = Image.open(dst).convert("RGBA")
    box = old.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()  # 원래 그림이 차지하던 자리

    art = Image.open(src).convert("RGBA")
    if not has_transparency(art):
        art = remove_background(art)
    art = trim(art)
    bw, bh = box[2] - box[0], box[3] - box[1]
    scale = min(bw / art.width, bh / art.height)
    art = art.resize((max(1, round(art.width * scale)), max(1, round(art.height * scale))), Image.LANCZOS)

    # 가로 가운데 · 아래(발밑)를 원래 자리에 맞춤
    out = Image.new("RGBA", old.size, (0, 0, 0, 0))
    x = box[0] + (bw - art.width) // 2
    y = box[3] - art.height
    out.alpha_composite(art, (x, y))

    os.makedirs(ARCHIVE, exist_ok=True)
    shutil.copy(src, os.path.join(ARCHIVE, name + os.path.splitext(src)[1].lower()))
    out.save(dst)
    print(f"replaced {name}: {art.size} in {old.size}")


def main(args):
    if not args:
        print("사용법: python Tools/UIGen/prop_art_import.py SupplyBox=그림.png  (가능: " + ", ".join(PROPS) + ")")
        return 1
    for pair in args:
        name, src = pair.split("=", 1)
        if name not in PROPS:
            print(f"모르는 소품: {name} (가능: {', '.join(PROPS)})")
            return 1
        replace(name, src)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
