# 광장 바위·사과나무·사과 그림(GPT 원본)을 게임용으로 자르고 맞춘다.
# - 원본: Tools/UIGen/plaza_sources/
#     rock_sheet.png        바위 4칸 (멀쩡함 / 금 감 / 많이 금 감 / 자갈) — 같은 폭 4칸, 칸마다 같은 자리
#     apple_tree_sheet.png  사과나무 2칸 (사과 달림 / 흔든 뒤 잎이 성김) — 같은 폭 2칸
#     apple.png             사과 아이콘
# - 결과: Assets/Art/Settlement/
#     Prop_Rock_0~2.png, Prop_Rock_Rubble.png   네 장이 같은 캔버스·같은 자리 (멀쩡한 바위 바닥이 캔버스 아래 10% → 피벗 0.1)
#     Prop_AppleTree_0~1.png                   두 장이 같은 캔버스, 밑동을 같은 x에 맞춤
#     ICON_Item_Apple.png                      128px 정사각
# 실행: python Tools/UIGen/fit_plaza_nodes.py
import os

from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCES = os.path.join(ROOT, "Tools", "UIGen", "plaza_sources")
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")

ALPHA_FLOOR = 24     # 이보다 옅은 픽셀은 잡티로 보고 지움
PIVOT_Y = 0.1        # SettlementSetup.PropArt의 피벗과 같게
MARGIN = 6
ROCK_HEIGHT = 300
TREE_HEIGHT = 520


def load(name):
    img = Image.open(os.path.join(SOURCES, name)).convert("RGBA")
    alpha = img.split()[3].point(lambda a: 0 if a < ALPHA_FLOOR else a)
    img.putalpha(alpha)
    return img


def cells(sheet, count):
    w = sheet.width // count
    return [sheet.crop((i * w, 0, (i + 1) * w, sheet.height)) for i in range(count)]


def union(boxes):
    return (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))


def frame(images, box, height, ground):
    """같은 영역(box)으로 잘라, 땅(ground, 원본 y)이 캔버스 아래 PIVOT_Y 지점에 오게 하고 height로 줄임.
    땅 아래로 조금 삐져나온 조각(깨진 돌 부스러기)은 아래 여백 안에 들어감"""
    x0, y0, x1 = box[0] - MARGIN, box[1] - MARGIN, box[2] + MARGIN
    total_h = round((ground - y0) / (1 - PIVOT_Y))
    if y0 + total_h < box[3]:
        print(f"  주의: 땅 아래로 {box[3] - y0 - total_h}px 잘림")
    out = []
    for img in images:
        canvas = img.crop((x0, y0, x1, y0 + total_h))
        scale = height / total_h
        out.append(canvas.resize((round(canvas.width * scale), height), Image.LANCZOS))
    return out


def trunk_x(img):
    """아래쪽 줄의 갈색 픽셀 평균 x (밑동 가운데)"""
    px = img.load()
    _, _, _, bottom = img.getbbox()
    xs = []
    for y in range(int(bottom * 0.85), bottom, 2):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a > 200 and r > 150 and 80 < g < 170 and b < 110:
                xs.append(x)
    return sum(xs) / len(xs)


def rocks():
    parts = cells(load("rock_sheet.png"), 4)
    box = union([p.getbbox() for p in parts])
    # 땅 = 멀쩡한 바위의 바닥
    names = ["Prop_Rock_0", "Prop_Rock_1", "Prop_Rock_2", "Prop_Rock_Rubble"]
    for name, img in zip(names, frame(parts, box, ROCK_HEIGHT, parts[0].getbbox()[3])):
        img.save(os.path.join(OUT, f"{name}.png"))
        print("saved", name, img.size)


def trees():
    parts = cells(load("apple_tree_sheet.png"), 2)
    # 두 번째 칸을 밑동이 첫 칸과 같은 x가 되게 옮김
    shift = round(trunk_x(parts[0]) - trunk_x(parts[1]))
    moved = Image.new("RGBA", parts[1].size, (0, 0, 0, 0))
    moved.alpha_composite(parts[1], (max(shift, 0), 0), (max(-shift, 0), 0))
    parts[1] = moved
    box = union([p.getbbox() for p in parts])
    for i, img in enumerate(frame(parts, box, TREE_HEIGHT, box[3])):
        img.save(os.path.join(OUT, f"Prop_AppleTree_{i}.png"))
        print("saved", f"Prop_AppleTree_{i}", img.size, "shift", shift)


def apple():
    img = load("apple.png")
    img = img.crop(img.getbbox())
    side = max(img.size) + MARGIN * 4
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((side - img.width) // 2, (side - img.height) // 2))
    canvas.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, "ICON_Item_Apple.png"))
    print("saved ICON_Item_Apple")


def main():
    rocks()
    trees()
    apple()


if __name__ == "__main__":
    main()
