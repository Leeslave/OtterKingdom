"""광장 소품 · 영토 숲 그림(투명 PNG)의 색을 새 지도 · 리깅 해달 색감에 맞춘다.

모양과 선은 그대로 두고 색만 바꾼다.
- 초록: 형광 기운을 빼고 지도 풀색(노란기 도는 세이지) 쪽으로
- 전체 채도 살짝 낮춤
- 아주 어두운 선: 해달 외곽선과 같은 진한 갈색으로

사용 (저장소 루트에서):
    python Tools/MapStyle/grade_props.py                        # 원본 숲 · 소품 → Assets/Art/Plaza/Territory, Props
    python Tools/MapStyle/grade_props.py <입력.png|폴더> <출력폴더>

원본은 ArtSource/Plaza/BeforeRigStyle/{Territory,Props}에 있고 매번 거기서 새로 만든다(여러 번 돌려도 색이 겹치지 않음).
"""
import argparse
import os

import cv2
import numpy as np
from PIL import Image

LINE = np.array([0x33, 0x1a, 0x10], np.float32)  # 해달 외곽선(#210502)보다 한 톤 밝게 — 배경용
GREEN_TARGET = 92.0   # 도(°). 지도 풀색(#a9c06c ≈ 76°)보다 살짝 푸르게 — 나무가 바닥과 구분되게
GREEN_PULL = 0.5      # 초록 색상을 목표 쪽으로 당기는 정도
GREEN_SAT = 0.70
ALL_SAT = 0.92


def grade(rgba):
    rgb = rgba[..., :3]
    a = rgba[..., 3:]
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV_FULL).astype(np.float32)
    h = hsv[..., 0] * 360 / 256
    s = hsv[..., 1]
    v = hsv[..., 2]

    # 연두~초록(45°~155°) 가중치: 가운데일수록 1 (형광 연두 하이라이트까지 포함)
    wg = np.clip(1.4 - np.abs(h - 100) / 40, 0, 1)
    h = h + (GREEN_TARGET - h) * GREEN_PULL * wg
    s = s * (1 - (1 - GREEN_SAT) * wg) * ALL_SAT
    v = v * (1 - 0.06 * wg)

    hsv[..., 0] = np.mod(h, 360) * 256 / 360
    hsv[..., 1] = np.clip(s, 0, 255)
    hsv[..., 2] = np.clip(v, 0, 255)
    out = cv2.cvtColor(hsv.astype(np.uint8), cv2.COLOR_HSV2RGB_FULL).astype(np.float32)

    # 거의 검은 선 → 진한 갈색 (밝기에 따라 부드럽게)
    lum = rgb.astype(np.float32).mean(2)
    wl = np.clip((70 - lum) / 40, 0, 1)[..., None]
    out = out * (1 - wl) + LINE * wl
    return np.dstack([np.clip(out, 0, 255).astype(np.uint8), a])


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BEFORE = os.path.join(ROOT, "ArtSource", "Plaza", "BeforeRigStyle")
DEFAULT_JOBS = [(os.path.join(BEFORE, "Territory"), os.path.join(ROOT, "Assets", "Art", "Plaza", "Territory")),
                (os.path.join(BEFORE, "Props"), os.path.join(ROOT, "Assets", "Art", "Plaza", "Props"))]


def run(src, dst_dir):
    files = [src] if os.path.isfile(src) else [
        os.path.join(src, f) for f in sorted(os.listdir(src)) if f.lower().endswith(".png")]
    os.makedirs(dst_dir, exist_ok=True)
    for f in files:
        im = np.array(Image.open(f).convert("RGBA"))
        Image.fromarray(grade(im)).save(os.path.join(dst_dir, os.path.basename(f)))
        print("graded", os.path.relpath(os.path.join(dst_dir, os.path.basename(f)), ROOT))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src", nargs="?")
    ap.add_argument("dst_dir", nargs="?")
    a = ap.parse_args()
    if a.src and a.dst_dir:
        run(a.src, a.dst_dir)
    else:
        for src, dst in DEFAULT_JOBS:
            run(src, dst)


if __name__ == "__main__":
    main()
