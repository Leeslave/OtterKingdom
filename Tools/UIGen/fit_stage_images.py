# 공사 단계 그림(AI로 그린 원본)을 완성 집 그림과 겹치게 맞춘다.
# - 원본: Tools/UIGen/stage_sources/
#     stage0_site.png   건설 예정지 (1단계 그림에서 나무 골조만 걷어 낸 편집본 — 같은 캔버스·같은 자리)
#     stage1_frame.png  골조       ┐ 지붕 기와가 없어 두 집이 같이 씀
#     stage2_walls.png  벽         ┘
#     stage3_<집>.png    마무리 (완성 집 그림을 고친 것 — 우물·화분이 빠져 그림 영역이 달라서 지붕을 겹쳐 맞춤)
# - 결과: Assets/Art/Settlement/Stage_<집>_<0~3>.png — 완성 집 PNG 캔버스 + 사방 PAD 여백,
#   그림 폭을 완성 집 폭에 맞추고 아래 가운데를 완성 집 아래 가운데에 맞춤 (Unity에서 같은 피벗·PPU로 겹쳐 보임)
#   0단계는 1단계와 똑같은 비율·위치로 옮겨서 돌 기초가 정확히 겹치게 함
# 실행: python Tools/UIGen/fit_stage_images.py
import colorsys
import os

from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCES = os.path.join(ROOT, "Tools", "UIGen", "stage_sources")
PROPS = os.path.join(ROOT, "Assets", "Art", "Plaza", "Props")
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")

SITE = "stage0_site.png"
FRAME = "stage1_frame.png"

# 집 → 단계 1~3 원본 파일 (0단계는 SITE를 1단계 기준으로)
STAGES = {
    "House_Blue": [FRAME, "stage2_walls.png", "stage3_blue.png"],
    "House_Red": [FRAME, "stage2_walls.png", "stage3_red.png"],
}


def placement(src, house):
    """원본의 그림 영역(bbox)을 완성 집에 맞출 때의 (자를 영역, 크기, 붙일 위치)"""
    box = src.getbbox()
    hx0, hy0, hx1, hy1 = house.getbbox()
    width = hx1 - hx0
    height = round((box[3] - box[1]) * width / (box[2] - box[0]))
    # 위로 넘치면 캔버스 안에 들어오게 줄임 (아래 가운데 기준은 그대로)
    if height > hy1:
        width = round(width * hy1 / height)
        height = hy1
    cx = (hx0 + hx1) // 2
    return box, (width, height), (cx - width // 2, hy1 - height)


# 완성 집 그림을 고친 단계 → 지붕 색 (hue 범위 0~360)으로 겹쳐 맞춤
ROOF_HUES = {"House_Blue": (160, 200), "House_Red": (0, 22)}


def roof_mask(img, hues):
    """지붕 기와 픽셀 집합 (채도·밝기가 충분하고 hue가 범위 안)"""
    lo, hi = hues
    w, h = img.size
    px = img.load()
    out = set()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 200:
                continue
            hue, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s > 0.45 and v > 0.5 and lo <= hue * 360 <= hi:
                out.add((x, y))
    return out


def bbox(points):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def roof_placement(src, house, hues):
    """지붕끼리 가장 많이 겹치는 (자를 영역, 크기, 붙일 위치). 지붕 폭으로 대강 맞춘 뒤 배율·위치를 조금씩 바꿔 봄"""
    target = roof_mask(house, hues)
    tx0, ty0, tx1, ty1 = bbox(target)
    box = src.getbbox()
    cropped = src.crop(box)
    sx0, _, sx1, _ = bbox(roof_mask(cropped.resize((cropped.width // 4, cropped.height // 4)), hues))
    base = (tx1 - tx0) / ((sx1 - sx0) * 4)

    best = None
    for k in range(-8, 9):
        scale = base * (1 + k * 0.01)
        size = (round(cropped.width * scale), round(cropped.height * scale))
        mask = roof_mask(cropped.resize(size, Image.LANCZOS), hues)
        mx0, my0, mx1, my1 = bbox(mask)
        # 지붕 영역의 왼쪽 위를 먼저 맞추고 주변을 찾음
        ox, oy = tx0 - mx0, ty0 - my0
        for dx in range(-6, 7):
            for dy in range(-6, 7):
                moved = {(x + ox + dx, y + oy + dy) for x, y in mask}
                iou = len(moved & target) / len(moved | target)
                if best is None or iou > best[0]:
                    best = (iou, size, (ox + dx, oy + dy))
    iou, size, pos = best
    print(f"  지붕 겹침 {iou:.2f}, 크기 {size}, 위치 {pos}")
    return box, size, pos


# 완성 집 캔버스 둘레에 더하는 여백 (비계·판자가 집 밖으로 나와도 잘리지 않게).
# Unity는 단계 그림이 사방으로 같은 여백을 가진다고 보고 피벗을 옮김 (SettlementSetup.ImportStageSprite)
PAD = 48


def render(src, house, place):
    box, size, pos = place
    out = Image.new("RGBA", (house.width + PAD * 2, house.height + PAD * 2), (0, 0, 0, 0))
    img = src.crop(box).resize(size, Image.LANCZOS)
    out.paste(img, (pos[0] + PAD, pos[1] + PAD), img)
    return out


def load(name):
    return Image.open(os.path.join(SOURCES, name)).convert("RGBA")


def main():
    frame = load(FRAME)
    site_path = os.path.join(SOURCES, SITE)
    site = load(SITE) if os.path.exists(site_path) else None
    if site is not None and site.size != frame.size:
        print(f"주의: {SITE} 캔버스 크기가 {FRAME}와 달라 자기 그림 영역으로 맞춥니다 (돌 기초가 조금 어긋날 수 있음)")

    for name, files in STAGES.items():
        house = Image.open(os.path.join(PROPS, f"Prop_{name}.png")).convert("RGBA")
        for i, file in enumerate(files, 1):
            src = load(file)
            place = roof_placement(src, house, ROOF_HUES[name]) if i == 3 else placement(src, house)
            render(src, house, place).save(os.path.join(OUT, f"Stage_{name}_{i}.png"))
            print("saved", f"Stage_{name}_{i}")

        if site is not None:
            # 1단계와 같은 자를 영역·크기·위치 → 돌 기초가 1단계와 정확히 겹침
            place = placement(frame, house) if site.size == frame.size else placement(site, house)
            render(site, house, place).save(os.path.join(OUT, f"Stage_{name}_0.png"))
            print("saved", f"Stage_{name}_0")


if __name__ == "__main__":
    main()
