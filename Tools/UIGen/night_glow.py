# 밤에 켜지는 불빛 그림: 집 창문(하늘색 유리)·가로등 유리(노란 유리)만 골라 따뜻한 빛으로 칠하고 은은한 번짐을 더함.
# 원본 소품 그림과 같은 캔버스라 같은 PPU·피벗으로 겹치면 정확히 창문 자리에 빛이 남.
# - 결과: Assets/Art/Settlement/Night_<소품>.png  (집 파랑·빨강, 가로등)
#         Assets/Art/Settlement/FX_Firefly.png    (반딧불 점)
# 실행: python Tools/UIGen/night_glow.py
import colorsys
import os

from PIL import Image, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PROPS = os.path.join(ROOT, "Assets", "Art", "Plaza", "Props")
OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")

WARM = (255, 214, 120)

# 소품 → 유리로 볼 색 (hue 범위, 최소 채도, 최대 채도, 최소 밝기, 덩어리 최소·최대 픽셀)
# 집 유리는 옅은 청록(지붕 청록보다 채도가 낮음), 가로등 유리는 노랑(나무 기둥 주황보다 hue가 높음)
GLASS = {
    "House_Blue": (132, 205, 0.08, 0.42, 0.72, 25, 3000),
    "House_Red": (132, 205, 0.08, 0.42, 0.72, 25, 3000),
    "Lamp": (36, 64, 0.35, 1.00, 0.82, 40, 20000),
}

# 이 범위(캔버스 비율 x0, y0, x1, y1, 위가 0) 안의 유리만 (가로등은 왼쪽 랜턴 쪽만, 기둥 반짝임 제외)
REGION = {
    "Lamp": (0.0, 0.0, 0.5, 1.0),
}


def compact(region):
    """창유리처럼 뭉친 덩어리인지 (지붕·기둥의 길쭉한 반짝임 줄은 빼려고): 가로세로 비율과 채운 정도"""
    xs = [p[0] for p in region]
    ys = [p[1] for p in region]
    bw = max(xs) - min(xs) + 1
    bh = max(ys) - min(ys) + 1
    ratio = max(bw, bh) / min(bw, bh)
    fill = len(region) / (bw * bh)
    return ratio <= 2.2 and fill >= 0.4


def inside(region, fx, fy):
    return region is None or (region[0] <= fx <= region[2] and region[1] <= fy <= region[3])


def glass_mask(img, rule, region):
    lo, hi, smin, smax, vmin, min_px, max_px = rule
    w, h = img.size
    src = img.load()
    hit = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a < 200:
                continue
            hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            hit[y][x] = lo <= hh * 360 <= hi and smin <= s <= smax and v >= vmin and inside(region, x / w, y / h)
    # 창문 크기 덩어리만 남김 (외톨이 점·넓은 면 제외)
    mask = Image.new("L", (w, h), 0)
    dst = mask.load()
    seen = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not hit[y][x] or seen[y][x]:
                continue
            stack = [(x, y)]
            seen[y][x] = True
            region = []
            while stack:
                cx, cy = stack.pop()
                region.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and hit[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if min_px <= len(region) <= max_px and compact(region):
                for px, py in region:
                    dst[px, py] = 255
    # 유리 안의 창살 틈을 메움
    return mask.filter(ImageFilter.MaxFilter(3))


def glow(name):
    img = Image.open(os.path.join(PROPS, f"Prop_{name}.png")).convert("RGBA")
    mask = glass_mask(img, GLASS[name], REGION.get(name))
    # 번짐: 유리 바깥으로 흐릿하게 퍼지는 빛 + 유리는 또렷하게
    halo = mask.filter(ImageFilter.GaussianBlur(radius=max(4, img.width // 40))).point(lambda a: int(a * 0.55))
    alpha = Image.eval(Image.merge("L", [mask]), lambda a: a)
    combined = Image.new("L", img.size, 0)
    combined.paste(halo)
    combined = Image.composite(alpha, combined, mask)
    out = Image.new("RGBA", img.size, WARM + (0,))
    out.putalpha(combined)
    out.save(os.path.join(OUT, f"Night_{name}.png"))
    print("saved", f"Night_{name}", "glass px", sum(1 for p in mask.get_flattened_data() if p))


def firefly():
    s = 64
    img = Image.new("L", (s, s), 0)
    px = img.load()
    for y in range(s):
        for x in range(s):
            d = (((x + 0.5 - s / 2) / (s / 2)) ** 2 + ((y + 0.5 - s / 2) / (s / 2)) ** 2) ** 0.5
            if d < 1:
                core = 1.0 if d < 0.18 else 0.0
                px[x, y] = int(255 * max(core, (1 - d) ** 2.2))
    out = Image.new("RGBA", (s, s), (255, 240, 150, 0))
    out.putalpha(img)
    out.save(os.path.join(OUT, "FX_Firefly.png"))
    print("saved FX_Firefly")


def main():
    for name in GLASS:
        glow(name)
    firefly()


if __name__ == "__main__":
    main()
