"""두리번이 그린 왕국 지도: 우리 땅 전체(광장 맵 + 5방향 영토 + 바닷가)를 양피지 위 미니어처로.

땅 모양은 진짜 맵(Plaza_WorldMap.jpg)을 줄여 지도 색으로 칠하고, 영토 경계(territory_layout.json)를 점선으로,
영토 밖은 작은 나무 그림(게임 숲 그림)으로 채운다. 광장 · 집 · 밭 · 광산 · 낚시터는 게임 그림을 작게.

    python Tools/StoryCuts/worldmap.py out.png      # 지도만 미리 보기
"""
import json
import math
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

import scene as s
from scene import img, fit

LAYOUT = json.loads((s.ART / "Plaza/territory_layout.json").read_text(encoding="utf-8"))
TITLE_FONT = s.ROOT / "Assets/Fonts/Cafe24Ssurround-v2.0.otf"
BODY_FONT = s.ROOT / "Assets/Fonts/NanumSquareRoundOTFR.otf"

INK = (92, 58, 36)
PARCHMENT = (244, 230, 196)
SEA = (150, 199, 210)
SEA_DEEP = (120, 176, 196)
GRASS = (176, 204, 128)
SAND = (232, 210, 160)


def _ground_to_map(x, y, k, off):
    """territory_layout 바닥 좌표 → 지도 그림 좌표"""
    m = LAYOUT["map"]
    return off[0] + (x - m["x0"]) * k, off[1] + (y - m["y0"]) * k


def _land_colors(world):
    """맵 그림을 지도 색 세 가지(바다 · 모래길 · 풀)로"""
    rgb = world.convert("RGB")
    r, g, b = [ch.point(lambda v: v) for ch in rgb.split()]
    px = rgb.load()
    out = Image.new("RGB", rgb.size)
    o = out.load()
    for y in range(rgb.height):
        for x in range(rgb.width):
            rr, gg, bb = px[x, y]
            if bb > rr + 25 and bb > gg - 10:
                o[x, y] = SEA
            elif rr > 200 and gg > 175 and bb > 120:
                o[x, y] = SAND
            else:
                o[x, y] = GRASS
    # 잔무늬를 지우고 덩어리만 (지도처럼)
    return out.filter(ImageFilter.ModeFilter(9)).filter(ImageFilter.ModeFilter(7))


def _outline(mask, width, color):
    """마스크 가장자리에 잉크 선"""
    edge = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(width | 1)), mask)
    layer = Image.new("RGBA", mask.size, color + (0,))
    layer.putalpha(edge.point(lambda v: 230 if v > 60 else 0))
    return layer


def _dashed_rect(d, box, dash=10, gap=8, width=3, color=INK + (170,)):
    x0, y0, x1, y1 = box
    for (ax, ay, bx, by) in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)):
        length = math.hypot(bx - ax, by - ay)
        n = int(length // (dash + gap)) + 1
        for i in range(n):
            t0 = i * (dash + gap) / length
            t1 = min(1, (i * (dash + gap) + dash) / length)
            d.line((ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1), fill=color, width=width)


def _compass(size):
    im = Image.new("RGBA", (size, size))
    d = ImageDraw.Draw(im)
    c = size / 2
    r = size * .42
    d.ellipse((c - r * .55, c - r * .55, c + r * .55, c + r * .55), outline=INK + (255,), width=3)
    for a, long in ((0, 1), (90, 1), (180, 1), (270, 1), (45, .55), (135, .55), (225, .55), (315, .55)):
        rad = math.radians(a - 90)
        tip = (c + math.cos(rad) * r * long, c + math.sin(rad) * r * long)
        side = math.radians(a)
        w = r * .14
        left = (c + math.cos(side) * w, c + math.sin(side) * w)
        right = (c - math.cos(side) * w, c - math.sin(side) * w)
        fill = (196, 74, 58, 255) if a == 0 else (INK + (255,) if long == 1 else (170, 130, 90, 255))
        d.polygon([tip, left, (c, c), right], fill=fill, outline=INK + (255,))
    font = ImageFont.truetype(str(TITLE_FONT), int(size * .2))
    d.text((c, c - r - size * .02), "N", font=font, fill=(196, 74, 58, 255), anchor="ms")
    return im


def _label(d, xy, text, size, font=TITLE_FONT, fill=INK, stroke=PARCHMENT):
    f = ImageFont.truetype(str(font), size)
    d.text(xy, text, font=f, fill=fill + (255,), anchor="mm", stroke_width=max(2, size // 9), stroke_fill=stroke + (255,))


def miniature_map(width=1200, height=900, seed=7):
    """양피지 지도 한 장 (RGBA)"""
    rnd = random.Random(seed)
    m = LAYOUT["map"]
    gw, gh = m["x1"] - m["x0"], m["y1"] - m["y0"]
    margin = int(width * .06)
    # 바다 판: 제목 아래부터 테두리 안쪽까지
    sea_box = (margin * .75, margin + height * .06, width - margin * .75, height - margin * .75)
    k = min((sea_box[2] - sea_box[0]) * .9 / gw, (sea_box[3] - sea_box[1]) * .92 / gh)
    off = ((sea_box[0] + sea_box[2] - gw * k) / 2, (sea_box[1] + sea_box[3] - gh * k) / 2)

    # 양피지 (가장자리가 조금 짙고 얼룩진)
    paper = Image.new("RGBA", (width, height), PARCHMENT + (255,))
    stain = Image.new("L", (width, height), 0)
    sd = ImageDraw.Draw(stain)
    for _ in range(18):
        x, y, r = rnd.uniform(0, width), rnd.uniform(0, height), rnd.uniform(40, 160)
        sd.ellipse((x - r, y - r, x + r, y + r), fill=rnd.randint(10, 26))
    edge = Image.new("L", (width, height), 0)
    ImageDraw.Draw(edge).rectangle((0, 0, width, height), outline=70, width=int(width * .05))
    shade = ImageChops.add(stain.filter(ImageFilter.GaussianBlur(40)), edge.filter(ImageFilter.GaussianBlur(int(width * .04))))
    paper = Image.composite(Image.new("RGBA", (width, height), (196, 160, 110, 255)), paper, shade)

    # 바다 (물결 줄)
    sea = Image.new("RGBA", (width, height), SEA + (255,))
    wd = ImageDraw.Draw(sea)
    for _ in range(int(width * height / 4500)):
        x, y = rnd.uniform(0, width), rnd.uniform(0, height)
        wd.arc((x - 10, y - 4, x + 10, y + 6), 200, 340, fill=SEA_DEEP + (255,), width=2)
    sea_mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(sea_mask).rounded_rectangle(tuple(int(v) for v in sea_box), 24, fill=255)
    paper.paste(sea, (0, 0), sea_mask)

    # 땅 (진짜 맵을 줄여 지도 색으로) — 네모 맵을 섬 모양으로 잘라 바다에 띄움
    world = s.world_map().resize((int(gw * k), int(gh * k)), Image.LANCZOS)
    land = _land_colors(world).convert("RGBA")
    island = Image.new("L", land.size, 0)
    cx, cy = land.width / 2, land.height / 2
    phase = [rnd.uniform(0, 6.28) for _ in range(3)]
    outline = []
    for i in range(240):
        a = i / 240 * 2 * math.pi
        ca, sa = math.cos(a), math.sin(a)
        # 모서리가 둥근 네모(초타원) + 들쭉날쭉한 해안
        r = 1 / ((abs(ca) ** 2.7 + abs(sa) ** 2.7) ** (1 / 2.7))
        r *= 1.0 + .05 * math.sin(3 * a + phase[0]) + .03 * math.sin(7 * a + phase[1]) + .015 * math.sin(13 * a + phase[2])
        outline.append((cx + ca * r * cx * .93, cy + sa * r * cy * .93))
    ImageDraw.Draw(island).polygon(outline, fill=255)
    land_mask = Image.new("L", land.size, 0)
    lp, mp, ip = land.load(), land_mask.load(), island.load()
    for y in range(land.height):
        for x in range(land.width):
            if lp[x, y][:3] != SEA and ip[x, y]:
                mp[x, y] = 255
    land_mask = land_mask.filter(ImageFilter.ModeFilter(5))
    mp = land_mask.load()
    paper.paste(land, (int(off[0]), int(off[1])), land_mask)
    coast = _outline(land_mask, 5, INK)
    paper.alpha_composite(coast, (int(off[0]), int(off[1])))
    # 바다 판 테두리
    ImageDraw.Draw(paper).rounded_rectangle(tuple(int(v) for v in sea_box), 24, outline=INK + (200,), width=3)

    d = ImageDraw.Draw(paper)
    # 영토 밖 = 숲: 작은 나무를 흩뿌림 (시작 땅 · 영토 · 바닷가는 비움)
    regions = [r["rect"] for r in LAYOUT["regions"]]

    def in_region(gx, gy):
        return any(r["x0"] <= gx <= r["x1"] and r["y0"] <= gy <= r["y1"] for r in regions)

    trees = [fit(img(f"Plaza/Territory/Forest_{n}.png"), int(height * .045)) for n in ("Pine_0", "Pine_1", "Oak_0", "Oak_1")]
    step = height * .03
    gy = m["y0"] + 60
    pts = []
    while gy < m["y1"]:
        gx = m["x0"] + 60
        while gx < m["x1"]:
            jx, jy = gx + rnd.uniform(-.35, .35) * step / k, gy + rnd.uniform(-.3, .3) * step / k
            px_, py_ = _ground_to_map(jx, jy, k, off)
            lx, ly = int(px_ - off[0]), int(py_ - off[1])
            if 0 <= lx < land.width and 0 <= ly < land.height and mp[lx, ly] and not in_region(jx, jy):
                pts.append((px_, py_))
            gx += step / k
        gy += step / k * .8
    for px_, py_ in sorted(pts, key=lambda p: p[1]):
        t = rnd.choice(trees)
        paper.alpha_composite(t, (int(px_ - t.width / 2), int(py_ - t.height)))

    # 영토 경계: 점선 네모 (섬 안쪽만)
    dashes = Image.new("RGBA", (width, height))
    dd = ImageDraw.Draw(dashes)
    for r in LAYOUT["regions"]:
        if r["id"] == "start" or r["id"].startswith("shore"):
            continue
        a = _ground_to_map(r["rect"]["x0"], r["rect"]["y0"], k, off)
        b = _ground_to_map(r["rect"]["x1"], r["rect"]["y1"], k, off)
        _dashed_rect(dd, (a[0] + 3, a[1] + 3, b[0] - 3, b[1] - 3))
    on_island = Image.new("L", (width, height), 0)
    on_island.paste(island, (int(off[0]), int(off[1])))
    dashes.putalpha(ImageChops.multiply(dashes.getchannel("A"), on_island))
    paper.alpha_composite(dashes)

    # 길: 광장에서 각 방향 집으로 점선 발자국 길
    plaza = _ground_to_map((LAYOUT["start"]["x0"] + LAYOUT["start"]["x1"]) / 2, (LAYOUT["start"]["y0"] + LAYOUT["start"]["y1"]) / 2, k, off)
    for home in LAYOUT["homes"]["list"]:
        hx, hy = _ground_to_map(home["x"], home["y"], k, off)
        n = int(math.hypot(hx - plaza[0], hy - plaza[1]) / 14)
        for i in range(1, n):
            t = i / n
            x = plaza[0] + (hx - plaza[0]) * t
            y = plaza[1] + (hy - plaza[1]) * t + math.sin(t * math.pi) * 18
            if i % 2:
                d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(176, 92, 64, 230))
        house = fit(img("Plaza/Props/Prop_House_Red_OtterStyle.png" if hx < plaza[0] else "Plaza/Props/Prop_House_Blue_OtterStyle.png"), int(height * .06))
        paper.alpha_composite(house, (int(hx - house.width / 2), int(hy - house.height * .8)))

    # 이름 붙은 곳: 광장(마을 회관) · 밭 · 광산 · 낚시터
    shore = next(r for r in LAYOUT["regions"] if r["id"] == "shore")["rect"]
    marks = [
        ("Settlement/Prop_TownHall.png", plaza[0], plaza[1] + height * .03, .11, "광장"),
        ("UI/Travel/ICON_Place_Farm.png", *_ground_to_map(-1900, 330, k, off), .075, "밭"),
        ("UI/Travel/ICON_Place_Mine.png", *_ground_to_map(-2150, -1330, k, off), .08, "광산"),
        ("UI/Travel/ICON_Place_FishingSpot.png", *_ground_to_map((shore["x0"] + shore["x1"]) / 2, shore["y0"] + 700, k, off), .08, "낚시터"),
    ]
    for path, x, y, h, name in marks:
        icon = fit(img(path), int(height * h))
        paper.alpha_composite(icon, (int(x - icon.width / 2), int(y - icon.height * .85)))
        _label(d, (x, y + height * .03), name, int(height * .032))

    # 아직 못 가 본 곳: 빨간 X (두리번의 다음 목적지)
    x, y = _ground_to_map(400, -1500, k, off)
    d.line((x - 14, y - 14, x + 14, y + 14), fill=(196, 74, 58, 255), width=7)
    d.line((x - 14, y + 14, x + 14, y - 14), fill=(196, 74, 58, 255), width=7)
    _label(d, (x, y + height * .045), "?", int(height * .05), fill=(196, 74, 58))

    # 나침반 · 제목 · 테두리
    comp = _compass(int(height * .2))
    paper.alpha_composite(comp, (int(width - margin - comp.width * .9), int(height - margin - comp.height * .95)))
    _label(d, (width / 2, margin * .9), "우리 왕국 지도", int(height * .065))
    d.rounded_rectangle((margin * .35, margin * .35, width - margin * .35, height - margin * .35), 18, outline=INK + (200,), width=4)
    d.rounded_rectangle((margin * .5, margin * .5, width - margin * .5, height - margin * .5), 14, outline=INK + (120,), width=2)

    # 종이 모양 (모서리 둥글게)
    shape = Image.new("L", (width, height), 0)
    ImageDraw.Draw(shape).rounded_rectangle((0, 0, width - 1, height - 1), 22, fill=255)
    paper.putalpha(shape)
    return paper


def held_map(width, height, angle=0.0):
    """장면에 들고 있는 지도: 미니어처 지도 + 잉크 외곽선 + 그림자"""
    m = miniature_map(width, height)
    pad = 24
    im = Image.new("RGBA", (width + pad * 2, height + pad * 2))
    shadow = Image.new("RGBA", im.size)
    ImageDraw.Draw(shadow).rounded_rectangle((pad + 8, pad + 12, pad + width + 8, pad + height + 12), 22, fill=(60, 35, 15, 90))
    im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8)))
    im.alpha_composite(m, (pad, pad))
    ImageDraw.Draw(im).rounded_rectangle((pad, pad, pad + width - 1, pad + height - 1), 22, outline=s.OUTLINE, width=6)
    return im.rotate(angle, resample=Image.BICUBIC, expand=True) if angle else im


if __name__ == "__main__":
    miniature_map().save(sys.argv[1] if len(sys.argv) > 1 else "worldmap.png")
