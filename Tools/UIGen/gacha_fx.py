# 보물 조개 뽑기 연출용 그림 (흰색 바탕 — 색은 코드에서 입힘). 빛살·빛 번짐·별빛은 축하 연출 그림(Resources/UI/Celebrate)을 같이 씀.
# - Assets/Art/Gacha/FX_CrackCore_{1,2,3}.png  조개 위 금 (단계마다 길어짐, 조개 그림과 같은 640 캔버스)
# - Assets/Art/Gacha/FX_CrackGlow_{1,2,3}.png  금 사이로 새는 빛 (등급 색을 입힘)
# - Assets/Art/Gacha/Shell_{Common,Rare,Epic,Pickup}_{L,R}.png  큰 금을 따라 자른 조개 반쪽 (쩍 갈라질 때)
# - Assets/Art/Gacha/FX_Ring.png               조개가 갈라질 때 퍼지는 빛 고리
# - Assets/Art/Gacha/FX_WaterBody.png          해달 앞 물결 (바다 색을 입혀 해달 아랫몸을 덮음)
# - Assets/Art/Gacha/FX_WaterFoam.png          물결 위 거품 줄
# 실행: python Tools/UIGen/gacha_fx.py  (조개 그림 Shell_Common.png가 먼저 있어야 함 — import_gacha_art.py)
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Gacha")


def white_with_alpha(alpha):
    img = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
    img.putalpha(alpha)
    return img


def shell_mask():
    """조개 그림 안쪽 (외곽선 두께만큼 줄임) — 금이 조개 밖으로 나가지 않게"""
    shell = Image.open(os.path.join(OUT, "Shell_Common.png")).convert("RGBA")
    mask = shell.getchannel("A").point(lambda a: 255 if a > 128 else 0)
    return mask.filter(ImageFilter.MinFilter(41)), shell.size


def zigzag(start, end, steps, wobble, rng):
    (x0, y0), (x1, y1) = start, end
    points = [start]
    for i in range(1, steps):
        t = i / steps
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        # 진행 방향에 수직으로 흔듦 (번갈아 좌우)
        dx, dy = x1 - x0, y1 - y0
        length = math.hypot(dx, dy) or 1
        nx, ny = -dy / length, dx / length
        side = (1 if i % 2 else -1) * wobble * (0.6 + 0.8 * rng.random())
        points.append((x + nx * side, y + ny * side))
    points.append(end)
    return points


def crack_paths(size, stage):
    """가운데를 위에서 아래로 가르는 큰 금 + 단계마다 늘어나는 잔금 (조개는 이 선을 따라 좌우로 갈라짐)"""
    rng = random.Random(7)
    w, h = size
    cx = w / 2
    top, bottom = h * 0.12, h * 0.9
    reach = {1: 0.38, 2: 0.7, 3: 1.0}[stage]
    main_end = (cx + 4, top + (bottom - top) * reach)
    paths = [zigzag((cx - 6, top), main_end, 3 + stage * 3, 22, rng)]
    branches = [
        (0.22, -1, 0.16, 0.1),
        (0.34, 1, 0.2, 0.12),
        (0.52, -1, 0.24, 0.16),
        (0.62, 1, 0.22, 0.2),
        (0.8, -1, 0.18, 0.12),
        (0.88, 1, 0.16, 0.1),
    ]
    count = {1: 0, 2: 3, 3: 6}[stage]
    main = paths[0]
    for at, side, dx, dy in branches[:count]:
        if at > reach:
            continue
        p = main[min(len(main) - 1, max(1, round(at * (len(main) - 1))))]
        end = (p[0] + side * w * dx, p[1] + h * dy * 0.6 - h * 0.04)
        paths.append(zigzag(p, end, 3, 10, rng))
    return paths


COCOA = (75, 46, 34)


def crack_lines(size, stage, extra):
    a = Image.new("L", size, 0)
    d = ImageDraw.Draw(a)
    for i, path in enumerate(crack_paths(size, stage)):
        width = (9 if i == 0 else 6) + extra
        d.line(path, fill=255, width=width, joint="curve")
        for x, y in (path[0], path[-1]):
            d.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=255)
    return a


def draw_cracks(size, stage, inside):
    """금: 코코아 테두리 안에 하얀 빛 줄 (조개 위에서도 금이 또렷하게). 빛: 하얀 번짐 (등급 색을 입힘)"""
    light = ImageChops.multiply(crack_lines(size, stage, 0), inside)
    edge = ImageChops.multiply(crack_lines(size, stage, 7), inside)
    core = Image.new("RGBA", size, COCOA + (0,))
    core.putalpha(edge)
    core.alpha_composite(white_with_alpha(light))
    glow = light.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(16))
    glow = glow.point(lambda v: min(255, int(v * 2.4)))
    glow = ImageChops.multiply(glow, inside.filter(ImageFilter.GaussianBlur(6)))
    return core, white_with_alpha(glow)


def ring(size=512):
    a = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(a)
    r = size * 0.4
    c = size / 2
    d.ellipse((c - r, c - r, c + r, c + r), outline=255, width=int(size * 0.05))
    return white_with_alpha(a.filter(ImageFilter.GaussianBlur(size * 0.02)))


def wave_y(x, width, base, amp):
    t = x / width * math.pi * 2
    return base + amp * (math.sin(t * 2) * 0.6 + math.sin(t * 5 + 1.3) * 0.25 + math.sin(t * 9 + 0.4) * 0.15)


def water(width=1024, height=260):
    """위쪽이 물결 모양인 띠. 위는 진하고 아래로 갈수록 투명 (바다 배경과 이어짐)"""
    base, amp = 34, 14
    body = Image.new("L", (width, height), 0)
    px = body.load()
    for x in range(width):
        top = wave_y(x, width, base, amp)
        for y in range(height):
            if y < top:
                continue
            fade = 1 - max(0.0, (y - top) / (height - top)) ** 1.6
            edge = min(1.0, (y - top) / 3.0)  # 위 가장자리 부드럽게
            px[x, y] = int(255 * 0.96 * fade * edge)
    foam = Image.new("L", (width, 64), 0)
    d = ImageDraw.Draw(foam)
    points = [(x, wave_y(x, width, 30, amp)) for x in range(0, width + 1, 4)]
    d.line(points, fill=255, width=7, joint="curve")
    # 물결 마루에만 짧은 하이라이트
    for i in range(6):
        x0 = width * (0.08 + i * 0.16)
        seg = [(x, wave_y(x, width, 30, amp) + 9) for x in range(int(x0), int(x0 + width * 0.06), 4)]
        d.line(seg, fill=180, width=4)
    foam = foam.filter(ImageFilter.GaussianBlur(1.2))
    return white_with_alpha(body), white_with_alpha(foam)


def shell_halves(size):
    """조개를 큰 금(3단계 가운데 금)을 따라 좌우로 자른 반쪽 그림 (Shell_*_L / _R, 캔버스 그대로). 잘린 면에 코코아 테두리"""
    w, h = size
    path = crack_paths(size, 3)[0]
    line = [(path[0][0], 0)] + path + [(path[-1][0], h)]
    for name in ("Common", "Rare", "Epic", "Pickup"):
        shell = Image.open(os.path.join(OUT, f"Shell_{name}.png")).convert("RGBA")
        for side, poly in (("L", [(0, 0)] + line + [(0, h)]), ("R", [(w, 0)] + line + [(w, h)])):
            region = Image.new("L", (w, h), 0)
            ImageDraw.Draw(region).polygon(poly, fill=255)
            half = shell.copy()
            half.putalpha(ImageChops.multiply(shell.getchannel("A"), region))
            edge = Image.new("L", (w, h), 0)
            ImageDraw.Draw(edge).line(line, fill=255, width=10, joint="curve")
            stroke = Image.new("RGBA", (w, h), COCOA + (0,))
            stroke.putalpha(ImageChops.multiply(edge, half.getchannel("A")))
            half.alpha_composite(stroke)
            half.save(os.path.join(OUT, f"Shell_{name}_{side}.png"))


def main():
    inside, size = shell_mask()
    for stage in (1, 2, 3):
        core, glow = draw_cracks(size, stage, inside)
        core.save(os.path.join(OUT, f"FX_CrackCore_{stage}.png"))
        glow.save(os.path.join(OUT, f"FX_CrackGlow_{stage}.png"))
    shell_halves(size)
    ring().save(os.path.join(OUT, "FX_Ring.png"))
    body, foam = water()
    body.save(os.path.join(OUT, "FX_WaterBody.png"))
    foam.save(os.path.join(OUT, "FX_WaterFoam.png"))
    print("gacha fx ok")


if __name__ == "__main__":
    main()
