"""광장 지도(Plaza_WorldMap.jpg)를 리깅 해달 그림체(진한 갈색 외곽선 · 납작한 2단 명암 · 따뜻하고 차분한 색)로 바꾼다.

지도를 새로 그리지 않고 원본의 영역(풀 · 흙길 · 물 · 돌 · 꽃)을 그대로 나눠 다시 칠하므로
해안선 · 길 · 걷기 영역 · 영토 좌표가 원본과 픽셀 단위로 같다.

사용 (저장소 루트에서):
    python Tools/MapStyle/stylize_map.py                       # 원본 → Assets/Art/Plaza/Plaza_WorldMap.jpg
    python Tools/MapStyle/stylize_map.py <입력> <출력> [--preview 미리보기.png]

원본(ArtSource/Plaza/BeforeRigStyle/Plaza_WorldMap.jpg)에서 매번 새로 만들기 때문에 여러 번 돌려도 된다.
Tools/MapExpansion/build_territory_art.py로 지도를 다시 만들면 그 결과를 원본 자리에 복사한 뒤 이 스크립트를 다시 돌린다.
"""
import argparse
import os

import cv2
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

# 해달 아틀라스(ArtSource/Otter/QuarterRig/atlas.png)에서 뽑은 기준:
#   외곽선 #210502 · 작업복 초록 #687848 · 털 #d88868 · 크림 #f8e8c8 · 모자 노랑 #f8a838
# 배경은 캐릭터보다 한 단계 밝고 선도 얇고 옅게 둬서 해달이 위에 또렷이 보이게 한다.
def hexc(s):
    return np.array([int(s[i:i + 2], 16) for i in (1, 3, 5)], np.float32)

PAL = {
    "grass": hexc("#a9c06c"), "grass_shade": hexc("#93ad5c"), "grass_tuft": hexc("#738f45"),
    "grass_light": hexc("#bccf82"),
    "sand": hexc("#f4dcaa"), "sand_shade": hexc("#e8c690"), "sand_dot": hexc("#d9b07a"),
    "water": hexc("#86cfcb"), "water_deep": hexc("#6dbcbf"), "water_light": hexc("#b4e3d9"),
    "foam": hexc("#fbf3df"),
    "rock": hexc("#c2b3a3"), "rock_shade": hexc("#9c8b7d"), "rock_light": hexc("#ddd1c3"),
    "rock_wet": hexc("#8fa79a"), "moss": hexc("#93a861"),
    "pebble": hexc("#f7eedc"), "pebble_shade": hexc("#dbc9ae"),
    "flower_white": hexc("#fff6e4"), "flower_yellow": hexc("#f5cf5c"), "flower_pink": hexc("#f2a7a0"),
    "line": hexc("#5b3d26"), "line_soft": hexc("#7b6a3c"), "line_water": hexc("#3f7f86"),
}

# 광장 동남쪽 공터의 조개 표식(원본 지도 픽셀 좌표 x0, y0, x1, y1)
SHELL_MARKS = [(3440, 2690, 3620, 2850)]


def soft(mask, sigma=0.9):
    return cv2.GaussianBlur(mask.astype(np.float32), (0, 0), sigma)


def clean(mask, k_open, k_close, min_area):
    m = mask.astype(np.uint8)
    if k_close:
        m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k_close, k_close)))
    if k_open:
        m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k_open, k_open)))
    if min_area:
        n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
        keep = st[:, cv2.CC_STAT_AREA] >= min_area
        keep[0] = False
        m = keep[lab].astype(np.uint8)
    return m.astype(bool)


def fill_holes(mask):
    m = mask.astype(np.uint8)
    cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    out = np.zeros_like(m)
    cv2.drawContours(out, cnts, -1, 1, cv2.FILLED)
    return out.astype(bool)


def rocks_only(mask, min_area, max_area, min_solidity):
    """덩어리 크기와 꽉 찬 정도(면적 / 볼록 껍질 면적)로 동글동글한 것만 남긴다."""
    m = mask.astype(np.uint8)
    cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    out = np.zeros_like(m)
    for c in cnts:
        a = cv2.contourArea(c)
        if a < min_area or a > max_area:
            continue
        hull = cv2.contourArea(cv2.convexHull(c))
        if hull > 0 and a / hull >= min_solidity:
            cv2.drawContours(out, [c], -1, 1, cv2.FILLED)
    return out.astype(bool) & mask if min_area < 10 else out.astype(bool)


def smooth_shape(mask, sigma):
    """둥근 스티커 느낌: 마스크를 흐리게 했다가 다시 자른다(잔털 · 톱니 제거)."""
    return cv2.GaussianBlur(mask.astype(np.float32), (0, 0), sigma) > 0.5


def inner_line(mask, width):
    """마스크 안쪽 가장자리에 width 픽셀 선(부드러운 끝)."""
    d = cv2.distanceTransform(mask.astype(np.uint8), cv2.DIST_L2, 5)
    a = np.clip(width + 0.5 - d, 0, 1)
    a[~mask] = 0
    return a


def lay(out, alpha, color):
    a = alpha[..., None]
    out *= 1 - a
    out += a * color


def stylize(src):
    rgb = src.astype(np.float32)
    hsv = cv2.cvtColor(src, cv2.COLOR_RGB2HSV)
    H, S, V = [hsv[..., i].astype(np.float32) for i in range(3)]
    h, w = H.shape

    # 1) 영역 나누기 (원본 색 기준). 흐린 색으로 판정해서 붓 자국 노이즈를 무시한다.
    blur = cv2.GaussianBlur(src, (0, 0), 3)
    bh = cv2.cvtColor(blur, cv2.COLOR_RGB2HSV)
    bH, bS, bV = [bh[..., i].astype(np.float32) for i in range(3)]
    water = (bH >= 78) & (bH <= 130) & (bS > 70)
    grass = (bH >= 24) & (bH < 78) & (bS > 70)
    water = clean(smooth_shape(water, 4), 5, 9, 4000)
    grass = clean(smooth_shape(grass & ~water, 5), 7, 9, 900)
    # 바다 위 바위섬: 물에 둘러싸인 작은 땅은 모래섬이 아니라 이끼 낀 바위로 칠한다.
    n, lab, st, _ = cv2.connectedComponentsWithStats((~water).astype(np.uint8), 8)
    small = st[:, cv2.CC_STAT_AREA] < 40000
    small[0] = False
    islet = small[lab] & ~water
    moss = islet & grass
    grass &= ~islet

    # 돌: 채도 낮은 회색 덩어리. 위쪽 밝은 면이 빠져 초승달처럼 잘리지 않게 구멍을 메우고,
    # 해안 거품처럼 길쭉하거나 큰 얼룩은 모양(꽉 찬 정도)으로 걸러낸다.
    gray = (S < 70) & (V > 55) & (V < 250) & ~water
    rock = rocks_only(fill_holes(clean(gray, 3, 7, 50)), 40, 25000, 0.72)
    # 물속 바위: 물보다 확실히 어둡다(거품은 밝아서 제외)
    sub = (bV < 185) & (bS < 215) & water
    sub_rock = rocks_only(fill_holes(clean(smooth_shape(sub, 2.5), 5, 7, 300)), 300, 60000, 0.7)
    rock = smooth_shape(rock | sub_rock | islet, 1.5)

    # 꽃: 풀 위의 밝은 점(흰/노랑/분홍)
    flower_white = (S < 70) & (V > 225)
    flower_yellow = (H >= 18) & (H <= 30) & (S > 150) & (V > 232)
    flower_pink = ((H < 12) | (H > 160)) & (S > 50) & (V > 180)
    near_grass = cv2.dilate(grass.astype(np.uint8), np.ones((15, 15), np.uint8)).astype(bool)
    fw = rocks_only(clean(flower_white & near_grass & ~water, 0, 3, 6), 6, 160, 0.4) & ~rock
    fy = rocks_only(clean(flower_yellow & grass, 0, 3, 10), 10, 140, 0.6)
    fp = rocks_only(clean(flower_pink & near_grass & ~water, 0, 3, 6), 6, 160, 0.4) & ~rock

    sand = ~(grass | water)

    # 2) 명암: 원본 밝기를 크게 흐린 뒤 영역별 중앙값으로 2단 분리
    L = cv2.GaussianBlur(cv2.cvtColor(src, cv2.COLOR_RGB2GRAY).astype(np.float32), (0, 0), 7)
    Ld = cv2.GaussianBlur(cv2.cvtColor(src, cv2.COLOR_RGB2GRAY).astype(np.float32), (0, 0), 1.2)

    out = np.zeros((h, w, 3), np.float32)
    out[:] = PAL["sand"]

    # 흙길: 어두운 쪽만 그늘색, 작은 진한 점(자갈 · 발자국)은 점으로
    sand_med = np.median(L[sand])
    lay(out, soft(smooth_shape(sand & (L < sand_med - 12), 6)) * 0.7, PAL["sand_shade"])
    dots = sand & (Ld < cv2.GaussianBlur(Ld, (0, 0), 6) - 34) & ~rock
    lay(out, soft(clean(dots, 0, 3, 10)) * 0.6, PAL["sand_dot"])

    # 물: 해안에서 멀수록 진하게, 원본의 밝은 물결은 연한 물결로
    gw = soft(water, 1.0)
    dist_shore = cv2.distanceTransform((water | islet).astype(np.uint8), cv2.DIST_L2, 5)
    dist_islet = cv2.distanceTransform((~islet).astype(np.uint8), cv2.DIST_L2, 5)
    deep = np.clip((dist_shore - 40) / 220, 0, 1)
    water_col = PAL["water"][None, None] * (1 - deep[..., None]) + PAL["water_deep"][None, None] * deep[..., None]
    out = out * (1 - gw[..., None]) + water_col * gw[..., None]
    ripple = water & (Ld > cv2.GaussianBlur(Ld, (0, 0), 8) + 10)
    ripple = clean(ripple, 0, 3, 25)
    lay(out, soft(ripple, 1.2) * 0.85, PAL["water_light"])
    # 해안 거품 띠 + 물가 선
    foam = water & ((dist_shore <= 9) | (dist_islet <= 4))
    lay(out, soft(foam, 1.0), PAL["foam"])
    lay(out, inner_line(water, 1.2) * 0.9, PAL["line_water"])
    foam_in = water & (((dist_shore > 9) & (dist_shore <= 11)) | ((dist_islet > 4) & (dist_islet <= 6)))
    lay(out, soft(foam_in, 0.8) * 0.5, PAL["line_water"])

    # 풀: 기본색 → 어두운 덩어리 그늘 → 밝은 쪽 하이라이트 → 원본의 진한 풀잎 → 외곽선
    # 풀밭이 살짝 도톰해 보이게 아래쪽 흙길에 얇은 그림자
    drop = np.roll(grass, 5, axis=0) & ~grass & ~water
    lay(out, soft(drop, 1.2) * 0.8, PAL["sand_shade"])
    gg = soft(grass, 1.0)
    lay(out, gg, PAL["grass"])
    g_med = np.median(L[grass])
    Lg = cv2.GaussianBlur(L, (0, 0), 10)
    lay(out, soft(smooth_shape(grass & (Lg < g_med - 10), 8), 1.5) * gg * 0.55, PAL["grass_shade"])
    lay(out, soft(smooth_shape(grass & (Lg > g_med + 14), 8), 1.5) * gg * 0.7, PAL["grass_light"])
    tuft = grass & (Ld < cv2.GaussianBlur(Ld, (0, 0), 5) - 22) & ~rock
    tuft = clean(tuft, 0, 3, 14)
    lay(out, soft(tuft, 0.9), PAL["grass_tuft"])
    lay(out, inner_line(grass, 1.6), PAL["line_soft"])

    # 돌: 납작한 2단 명암 + 위쪽 하이라이트 + 진한 외곽선 (물속 돌은 이끼색)
    # 하얀 조약돌 · 조개 표식처럼 밝은 덩어리는 크림색으로 두고 원본의 무늬 선을 살린다.
    lr = cv2.GaussianBlur(cv2.cvtColor(src, cv2.COLOR_RGB2GRAY).astype(np.float32), (0, 0), 2)
    n, lab = cv2.connectedComponents(rock.astype(np.uint8), connectivity=8)
    med = np.zeros(n, np.float32)
    if n > 1:
        sums = np.bincount(lab.ravel(), weights=lr.ravel(), minlength=n)
        cnt = np.maximum(np.bincount(lab.ravel(), minlength=n), 1)
        med = sums / cnt
    pebble = (med[lab] > 196) & rock & ~sub_rock & ~islet
    for x0, y0, x1, y1 in SHELL_MARKS:  # 조개 표식은 돌과 밝기가 비슷해서 위치로 지정
        box = np.zeros_like(rock)
        box[y0:y1, x0:x1] = True
        ids = np.unique(lab[box & rock])
        pebble |= np.isin(lab, ids[ids > 0]) & rock
    rock = rock & ~pebble
    pb = soft(pebble, 0.8)
    lay(out, pb, PAL["pebble"])
    lay(out, soft(pebble & (lr < 188), 0.8) * pb, PAL["pebble_shade"])
    lay(out, soft(pebble & (Ld < 150), 0.6) * pb, PAL["line"])
    lay(out, inner_line(pebble, 1.3), PAL["line"])
    rk = soft(rock, 0.9)
    rock_col = np.where(sub_rock[..., None], PAL["rock_wet"], PAL["rock"])
    rock_col = np.where(moss[..., None], PAL["moss"], rock_col)
    out = out * (1 - rk[..., None]) + rock_col * rk[..., None]
    r_med = np.median(lr[rock]) if rock.any() else 128
    lay(out, soft(rock & (lr < r_med - 6), 1.0) * rk, PAL["rock_shade"])
    top = rock & ~np.roll(cv2.erode(rock.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool), -4, axis=0)
    lay(out, soft(top & (lr > r_med), 1.0) * 0.7, PAL["rock_light"])
    lay(out, inner_line(rock, 1.5), PAL["line"])

    # 꽃: 납작한 색 + 얇은 외곽선
    for m, c in ((fw, PAL["flower_white"]), (fy, PAL["flower_yellow"]), (fp, PAL["flower_pink"])):
        if m.any():
            ring = cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
            lay(out, soft(ring, 0.6) * 0.75, PAL["line"])
            lay(out, soft(m, 0.6), c)

    return np.clip(out, 0, 255).astype(np.uint8)


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_SRC = os.path.join(ROOT, "ArtSource", "Plaza", "BeforeRigStyle", "Plaza_WorldMap.jpg")
DEFAULT_DST = os.path.join(ROOT, "Assets", "Art", "Plaza", "Plaza_WorldMap.jpg")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src", nargs="?", default=DEFAULT_SRC)
    ap.add_argument("dst", nargs="?", default=DEFAULT_DST)
    ap.add_argument("--preview")
    a = ap.parse_args()
    src = np.array(Image.open(a.src).convert("RGB"))
    res = stylize(src)
    if a.dst.lower().endswith((".jpg", ".jpeg")):
        # 같은 파일 이름(= 같은 GUID)이라 씬을 다시 만들 필요가 없다. 선이 번지지 않게 색 축소(subsampling) 끔.
        Image.fromarray(res).save(a.dst, quality=95, subsampling=0)
    else:
        Image.fromarray(res).save(a.dst)
    if a.preview:
        pw = 1200
        Image.fromarray(res).resize((pw, int(pw * res.shape[0] / res.shape[1])), Image.LANCZOS).save(a.preview)
    print("saved", a.dst, res.shape)


if __name__ == "__main__":
    main()
