# 공사 단계 그림(AI로 그린 원본)을 완성 집 그림과 겹치게 맞춘다.
# - 원본: Tools/UIGen/stage_sources/
#     stage0_site.png   건설 예정지 (1단계 그림에서 나무 골조만 걷어 낸 편집본 — 같은 캔버스·같은 자리)
#     stage1_frame.png  골조       ┐ 지붕 기와가 없어 두 집이 같이 씀
#     stage2_walls.png  벽         ┘
#     stage3_<집>.png    마무리 (집마다 지붕 색)
# - 결과: Assets/Art/Settlement/Stage_<집>_<0~3>.png — 완성 집 PNG와 같은 캔버스 크기,
#   그림 폭을 완성 집 폭에 맞추고 아래 가운데를 완성 집 아래 가운데에 맞춤 (Unity에서 같은 피벗·PPU로 겹쳐 보임)
#   0단계는 1단계와 똑같은 비율·위치로 옮겨서 돌 기초가 정확히 겹치게 함
# 실행: python Tools/UIGen/fit_stage_images.py
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


def render(src, house, place):
    box, size, pos = place
    out = Image.new("RGBA", house.size, (0, 0, 0, 0))
    out.alpha_composite(src.crop(box).resize(size, Image.LANCZOS), pos)
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
            render(src, house, placement(src, house)).save(os.path.join(OUT, f"Stage_{name}_{i}.png"))
            print("saved", f"Stage_{name}_{i}")

        if site is not None:
            # 1단계와 같은 자를 영역·크기·위치 → 돌 기초가 1단계와 정확히 겹침
            place = placement(frame, house) if site.size == frame.size else placement(site, house)
            render(site, house, place).save(os.path.join(OUT, f"Stage_{name}_0.png"))
            print("saved", f"Stage_{name}_0")


if __name__ == "__main__":
    main()
