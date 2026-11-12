"""왕국 화면 개편 시안 (테스터 피드백: 레벨업 조건 · 마을 관리 화면 분리 · 구역 용도).
실제 게임 UI 부품(RuntimeUIStyle이 쓰는 9-slice 그림)과 게임 폰트로 1080x1920에 그린다. 숫자 · 이름은 예시.
usage: python kingdom_mockup.py <out_dir>
  00_hud_level        상단 바: 레벨이 막혔을 때 (지금 / 제안)
  01_tab_progress     왕국 화면 [발전] 탭 — 레벨업 조건 · 다음 목표 · 영토 · 섬 지도 · 완료한 발전
  02_tab_residents    [주민] 탭 — 주민 현황 · 지금 하는 일 · 주민 목록
  03_tab_districts    [구역] 탭 — 질서도 · 구역 지도 · 구역 효과
  04_build_districts  건설(꾸미기) 모드 [구역 보기] — 구역 색 · 놓을 때 효과 미리보기 · 빈 땅 바로 정하기
  05_change_confirm   용도 바꾸기 확인 — 바뀌는 것을 숫자로
  06_tab_work         [일터] 탭 — 지금의 일터와 설비를 작은 칩 4개로"""
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Art")
UI = os.path.join(ART, "UI")
DOCS_IMG = os.path.join(ROOT, "Docs", "Images")
TITLE_FONT = os.path.join(ROOT, "Assets", "Fonts", "Cafe24Ssurround-v2.0.otf")
BODY_FONT = os.path.join(ROOT, "Assets", "Fonts", "NanumSquareRoundOTFR.otf")
SCRATCH_SHOTS = None  # 06의 일터 화면 캡처 (argv[2], 없으면 06 생략)

W, H = 1080, 1920
COCOA = (0x4B, 0x2E, 0x22, 255)
MUTED = (0x9A, 0x7F, 0x6E, 255)
GREEN = (0x6E, 0x9A, 0x5A, 255)
RED = (0xD9, 0x5B, 0x4B, 255)
WHITE = (255, 255, 255, 255)
# 구역 색 (구역_배치안 그림과 같은 계열)
ZONE = {"생산": (0x5B, 0x9B, 0xD5), "주거": (0xF0, 0x8A, 0x4B), "상업": (0xF2, 0xC9, 0x2E),
        "농업": (0x6C, 0xC0, 0x4A), "중앙": (0xA8, 0x8B, 0xC9), "빈 땅": (0xBF, 0xB2, 0xA4)}

# 내용 칸 (RuntimeUIWindow: 패널 L/R 40, 위 90 + 리본 절반, 아래 56 · 탭 줄 · 뷰포트 안쪽 30)
PANEL = (40, 142, 1040, 1864)
VIEW = (70, 340, 1010, 1820)
CX0, CX1 = 70, 1010
CW = CX1 - CX0


# ------------------------------------------------------------------ 기본
def F(size, title=False):
    return ImageFont.truetype(TITLE_FONT if title else BODY_FONT, size)


_cache = {}


def sprite(rel):
    if rel not in _cache:
        _cache[rel] = Image.open(os.path.join(UI, rel + ".png")).convert("RGBA")
    return _cache[rel]


def border_of(rel):
    meta = open(os.path.join(UI, rel + ".png.meta"), encoding="utf-8").read()
    m = re.search(r"spriteBorder: \{x: (\d+), y: (\d+), z: (\d+), w: (\d+)\}", meta)
    l, b, r, t = (int(v) for v in m.groups())
    return l, t, r, b


def tint(im, color):
    if color is None:
        return im
    r, g, b = (c / 255 for c in color[:3])
    ch = im.split()
    return Image.merge("RGBA", (ch[0].point(lambda v: int(v * r)), ch[1].point(lambda v: int(v * g)),
                                ch[2].point(lambda v: int(v * b)), ch[3]))


def nine(rel, w, h, color=None, border=None):
    """Unity Sliced 이미지와 같게 (테두리는 원래 픽셀 크기, 가운데만 늘림)"""
    src = sprite(rel)
    l, t, r, b = border if border else border_of(rel)
    if l + r == 0:
        return tint(src.resize((w, h), Image.LANCZOS), color)
    k = min(1.0, w / (l + r), h / (t + b))
    sw, sh = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    L, T, R, B = int(l * k), int(t * k), int(r * k), int(b * k)
    xs = [(0, l, 0, L), (l, sw - r, L, w - R), (sw - r, sw, w - R, w)]
    ys = [(0, t, 0, T), (t, sh - b, T, h - B), (sh - b, sh, h - B, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS)
            out.alpha_composite(piece, (dx0, dy0))
    return tint(out, color)


def put(base, im, x, y):
    base.alpha_composite(im, (int(x), int(y)))


def load(path, size):
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getchannel("A").getbbox())
    k = size / max(im.size)
    return im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)


def art(rel, size):
    return load(os.path.join(ART, rel), size)


def put_c(base, im, cx, cy):
    put(base, im, cx - im.width // 2, cy - im.height // 2)


def T(base, xy, s, size, title=False, fill=COCOA, anchor="la", spacing=10):
    d = ImageDraw.Draw(base)
    d.multiline_text(xy, s, font=F(size, title), fill=fill, anchor=anchor, spacing=spacing)


def tw(s, size, title=False):
    return int(F(size, title).getlength(s))


# ------------------------------------------------------------------ 부품 (RuntimeUICards와 같은 이름)
BUTTON = {"primary": "Common/UI_Button_Primary", "secondary": "Common/UI_Button_Secondary",
          "feature": "Common/UI_Button_Coin", "option": "Common/UI_Button_Paper"}
TAG = {"normal": "Common/UI_Tag_Category", "warn": "Common/UI_Tag_Highlight", "on": "Collection/UI_Badge_Status",
       "gold": "Collection/UI_Tab_Gold"}


def card(base, x, y, w, h, ring=False, dimmed=False):
    put(base, nine("Inventory/UI_Inventory_Box", w, h, (235, 228, 220) if dimmed else None), x, y)
    if ring:
        d = ImageDraw.Draw(base)
        d.rounded_rectangle([x - 6, y - 6, x + w + 6, y + h + 6], radius=46, outline=(0xF2, 0xC9, 0x4C, 255), width=7)


def inset(base, x, y, w, h):
    put(base, nine("Common/UI_Box_Inset", w, h), x, y)


def button(base, x, y, w, h, label, kind="primary", size=32):
    put(base, nine(BUTTON[kind], w, h), x, y)
    color = WHITE if kind == "primary" else COCOA
    d = ImageDraw.Draw(base)
    d.text((x + w // 2, y + h // 2 - 4), label, font=F(size, True), fill=color, anchor="mm",
           stroke_width=3 if kind == "primary" else 0, stroke_fill=(0x6E, 0x8A, 0x5E, 255))


def tag(base, x, y, label, kind="normal", size=24, anchor="l"):
    w = tw(label, size, True) + 40
    h = size + 26
    if anchor == "r":
        x -= w
    put(base, nine(TAG[kind], w, h), x, y)
    fill = WHITE if kind == "warn" else COCOA
    T(base, (x + w // 2, y + h // 2 - 3), label, size, True, fill, "mm")
    return w


def bar(base, x, y, w, frac, h=26, color=None):
    put(base, nine("Common/UI_Slider_Track", w, h), x, y)
    if frac > 0:
        fw = max(h, int(w * min(1.0, frac)))
        put(base, nine("Common/UI_Bar_Fill", fw, h, color), x, y)


def switch(base, x, y, on):
    w, h = 96, 54
    put(base, nine("Common/UI_Slider_Track", w, h), x, y)
    if on:
        put(base, nine("Common/UI_Bar_Fill", w, h), x, y)
    knob = sprite("Common/UI_Knob").resize((h - 6, h - 6), Image.LANCZOS)
    put(base, knob, x + (w - h + 3 if on else 3), y + 3)


def tile(base, x, y, size, icon=None, filled=False, locked=False):
    put(base, nine("Inventory/UI_Inventory_Slot_Filled" if filled else "Inventory/UI_Inventory_Slot_Empty", size, size), x, y)
    if icon is not None:
        ic = icon.copy()
        k = size * 0.72 / max(ic.size)
        ic = ic.resize((max(1, int(ic.width * k)), max(1, int(ic.height * k))), Image.LANCZOS)
        if locked:
            ic.putalpha(ic.getchannel("A").point(lambda v: int(v * 0.45)))
        put_c(base, ic, x + size // 2, y + size // 2 - 4)
    if locked:
        lock = load(os.path.join(UI, "Inventory", "LockIcon.png"), int(size * 0.42))
        put(base, lock, x + size - lock.width + 6, y + size - lock.height + 4)


def check(base, cx, cy, done, r=24):
    d = ImageDraw.Draw(base)
    if done:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=GREEN, outline=COCOA, width=4)
        d.line([(cx - 11, cy + 1), (cx - 3, cy + 9), (cx + 12, cy - 9)], fill=WHITE, width=7, joint="curve")
    else:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 251, 244, 255), outline=COCOA, width=4)


def dot(base, cx, cy, r=13, color=RED):
    ImageDraw.Draw(base).ellipse([cx - r, cy - r, cx + r, cy + r], fill=color, outline=WHITE, width=4)


def swatch(base, x, y, size, name, dashed=False):
    c = ZONE[name]
    d = ImageDraw.Draw(base)
    d.rounded_rectangle([x, y, x + size, y + size], radius=size // 4, fill=c + (255,), outline=COCOA, width=4)
    if dashed:
        d.rounded_rectangle([x, y, x + size, y + size], radius=size // 4, fill=(250, 245, 238, 255), outline=MUTED, width=4)
        T(base, (x + size // 2, y + size // 2 - 2), "?", int(size * 0.55), True, MUTED, "mm")


def section(base, y, label, right=None):
    T(base, (CX0 + 8, y), label, 32, True)
    if right:
        T(base, (CX1 - 8, y + 4), right, 26, False, MUTED, "ra")


def arrow_right(base, x, y, color=COCOA):
    d = ImageDraw.Draw(base)
    d.polygon([(x, y - 14), (x + 22, y), (x, y + 14)], fill=color)
    d.line([(x - 26, y), (x + 2, y)], fill=color, width=8)


# ------------------------------------------------------------------ 배경 · 창
def backdrop():
    """게임 캡처(광장 + 상단 바 + 하단 바)를 흐리게 (창 뒤 배경)"""
    shot = Image.open(os.path.join(DOCS_IMG, "구역이사_3단계_구역선택.png")).convert("RGBA").resize((W, H))
    return shot


def window(title, icon, tabs, selected, dots=()):
    img = backdrop()
    img.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 128)))  # RuntimeUIKit.DimColor (검정 50%)
    x0, y0, x1, y1 = PANEL
    put(img, nine("Inventory/UI_Inventory_Panel", x1 - x0, y1 - y0, (252, 245, 230), (72, 72, 72, 80)), x0, y0)
    # 리본 (아이콘 76 + 제목)
    tw_ = tw(title, 48, True)
    rw = tw_ + (76 + 22 if icon else 0) + 120
    rx = (W - rw) // 2
    put(img, nine("Common/UI_Ribbon_Peach", rw, 104, (245, 199, 173)), rx, y0 - 52)
    if icon:
        put_c(img, art(icon, 76), rx + 60 + 38, y0)
    T(img, (rx + 60 + (76 + 22 if icon else 0), y0 - 4), title, 48, True, anchor="lm")
    # 닫기
    put(img, sprite("Inventory/UI_Inventory_CloseButton"), x1 + 20 - 96, y0 - 34)
    # 탭 4개 (간격 18)
    top = y0 + 80
    n = len(tabs)
    gap = 18
    tw_each = (x1 - x0 - 72 - gap * (n - 1)) / n
    for i, name in enumerate(tabs):
        tx = int(x0 + 36 + i * (tw_each + gap))
        sel = i == selected
        put(img, nine("Inventory/UI_Inventory_Tab_Selected" if sel else "Inventory/UI_Inventory_Tab_Normal", int(tw_each), 100), tx, top)
        T(img, (tx + int(tw_each) // 2, top + 46), name, 36, True, COCOA, "mm")
        if name in dots:
            dot(img, tx + int(tw_each) - 14, top + 12)
    return img


def content_layer():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def finish(img, layer, fade=True):
    """내용을 뷰포트로 잘라 붙이고, 아래로 더 있으면 끝을 흐리게"""
    x0, y0, x1, y1 = VIEW
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rectangle([x0 - 10, y0, x1 + 10, y1], fill=255)
    if fade:
        md = ImageDraw.Draw(mask)
        for i in range(70):
            md.line([(x0 - 10, y1 - i), (x1 + 10, y1 - i)], fill=int(255 * (i / 70) ** 0.8))
    clipped = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    clipped.paste(layer, (0, 0), Image.composite(layer.getchannel("A"), Image.new("L", (W, H), 0), mask))
    img.alpha_composite(clipped)
    return img


def note_strip(img, lines, y=None):
    """시안 설명 띠 (게임 화면이 아님) — 화면 맨 아래 하단 바 자리"""
    h = 40 + 44 * len(lines)
    y = H - h if y is None else y
    strip = Image.new("RGBA", (W, h), (40, 30, 26, 225))
    img.alpha_composite(strip, (0, y))
    for i, s in enumerate(lines):
        T(img, (36, y + 22 + i * 44), s, 28, False, (255, 240, 220, 255))


TABS = ["발전", "주민", "구역", "일터"]
OTTERS = [("뚝딱이", "Sunny"), ("새싹이", "Farmer"), ("첨벙이", "Fisher"), ("깡깡이", "Miner"), ("데굴이", "Mint"),
          ("퐁당이", "Ocean"), ("짤랑이", "Honey"), ("살랑이", "Blossom"), ("쿵쿵이", "Forest"), ("콩콩이", "Cocoa"),
          ("또박이", "Sailor"), ("끄적이", "Ink"), ("몽실", "Explorer")]


def otter(cast, size):
    file = "ICON_Otter_Farmer.png" if cast == "Farmer" else f"ICON_Otter_Cast_{cast}.png"
    return art(os.path.join("Otter", file), size)


# ------------------------------------------------------------------ 00 상단 바
def hud_level():
    shot = Image.open(os.path.join(DOCS_IMG, "구역이사_3단계_구역선택.png")).convert("RGBA").resize((W, H))
    strip = shot.crop((0, 0, W, 200))
    img = shot.filter(ImageFilter.GaussianBlur(10))
    img.alpha_composite(Image.new("RGBA", (W, H), (40, 28, 22, 170)))

    def label(y, s, sub):
        T(img, (40, y), s, 40, True, WHITE)
        T(img, (40, y + 54), sub, 28, False, (240, 225, 210, 255))

    label(70, "지금", "경험치가 다 차도 막대만 꽉 찬 채 멈춰요. 왜 안 오르는지 안 보여요.")
    put(img, strip, 0, 170)
    label(470, "제안", "막히면 Lv 칩에 자물쇠 · 막대 끝에 \"부탁 필요\". 누르면 왕국 화면 [발전] 탭.")
    b = strip.copy()
    # 경험치 막대 가득 + 자물쇠
    put(b, nine("Common/UI_Bar_Fill", 168, 22), 161, 143)
    lock = load(os.path.join(UI, "Inventory", "LockIcon.png"), 50)
    put(b, lock, 316, 76)
    put(img, b, 0, 570)
    # 말풍선 (위 꼬리 → Lv 칩)
    bx, by, bw, bh = 110, 800, 700, 170
    d = ImageDraw.Draw(img)
    put(img, nine("Common/UI_Button_Paper", bw, bh), bx, by)
    d.polygon([(278, by + 8), (318, by - 34), (346, by + 8)], fill=(255, 251, 244, 255), outline=None)
    d.line([(278, by + 6), (318, by - 34), (346, by + 6)], fill=COCOA, width=7, joint="curve")
    T(img, (bx + 36, by + 30), "경험치가 가득 찼어요!", 36, True)
    T(img, (bx + 36, by + 88), "큰 부탁 \"마을 회의소 마련하기\"를 끝내면 Lv.7", 28)
    tag(img, bx + bw - 30, by + 26, "가 보기 ›", "on", 26, anchor="r")
    # 칩도 같은 화면
    label(1060, "들어가는 곳", "프로필(레벨) · \"작은 마을\" 칩 · 광장의 마을회관 건물 → 모두 같은 왕국 화면")
    chip_y = 1200
    for i, (s, icon) in enumerate([("프로필 · Lv", "Otter/ICON_Otter_Farmer.png"), ("작은 마을 칩", "Settlement/ICON_House_Red.png"),
                                   ("마을회관 건물", "Settlement/ICON_TownHall.png")]):
        x = 60 + i * 330
        card(img, x, chip_y, 300, 230)
        put_c(img, art(icon, 110), x + 150, chip_y + 90)
        T(img, (x + 150, chip_y + 180), s, 28, True, anchor="mm")
    arrow_y = chip_y + 300
    ImageDraw.Draw(img).polygon([(W // 2 - 30, arrow_y - 6), (W // 2 + 30, arrow_y - 6), (W // 2, arrow_y + 30)], fill=WHITE)
    ImageDraw.Draw(img).line([(W // 2, arrow_y - 50), (W // 2, arrow_y)], fill=WHITE, width=12)
    card(img, 240, arrow_y + 60, 600, 120)
    T(img, (540, arrow_y + 116), "왕국 화면 [발전] 탭", 40, True, anchor="mm")
    return img


# ------------------------------------------------------------------ 01 발전
def tab_progress():
    img = window("우리 왕국", "Settlement/ICON_TownHall.png", TABS, 0, dots={"구역"})
    L = content_layer()
    y = 360
    # 레벨업 조건 카드 (막혔을 때만 맨 위, 금테)
    card(L, CX0, y, CW, 380, ring=True)
    put(L, nine("Collection/UI_Tab_Gold", 132, 132), CX0 + 28, y + 28)
    T(L, (CX0 + 94, y + 80), "Lv.7", 38, True, anchor="mm")
    T(L, (CX0 + 190, y + 34), "Lv.7 레벨업 조건", 38, True)
    T(L, (CX0 + 190, y + 88), "지금 Lv.6 · 두 가지를 다 채우면 바로 올라요", 26, False, MUTED)
    tag(L, CX1 - 26, y + 30, "1개 남음", "warn", 26, anchor="r")
    inset(L, CX0 + 24, y + 182, CW - 48, 176)
    r1 = y + 226
    check(L, CX0 + 76, r1, True)
    T(L, (CX0 + 116, r1), "경험치", 30, True, anchor="lm")
    bar(L, CX0 + 250, r1 - 13, 450, 1.0)
    T(L, (CX1 - 48, r1), "1,600 / 1,600", 26, False, COCOA, "rm")
    r2 = y + 310
    check(L, CX0 + 76, r2, False)
    T(L, (CX0 + 116, r2), "큰 부탁", 30, True, anchor="lm")
    T(L, (CX0 + 250, r2), "마을 회의소 마련하기 · 3/5단계", 28, False, COCOA, "lm")
    button(L, CX1 - 216, r2 - 34, 168, 68, "가 보기", "primary", 30)
    y += 380 + 30
    T(L, (CX0 + 8, y), "경험치는 넘쳐도 모아 둬요 — 부탁을 끝내는 순간 남은 경험치까지 반영", 24, False, MUTED)
    y += 56

    section(L, y, "다음 목표")
    y += 52
    card(L, CX0, y, CW, 196)
    tile(L, CX0 + 26, y + 30, 132, art("Settlement/ICON_BoardUpgrade.png", 120), filled=True)
    T(L, (CX0 + 184, y + 30), "마을 회의소 마련하기", 34, True)
    T(L, (CX0 + 184, y + 80), "다음: 회의소 탁자 놓기", 26, False, MUTED)
    bar(L, CX0 + 184, y + 130, 330, 0.6)
    T(L, (CX0 + 530, y + 143), "3 / 5", 26, False, COCOA, "lm")
    button(L, CX1 - 210, y + 62, 168, 72, "가 보기", "primary", 30)
    y += 196 + 28

    section(L, y, "땅 넓히기")
    y += 52
    for icon, title, sub, btn, kind in [("Settlement/ICON_Clearing.png", "숲 개간", "개간 기회 1번 · 공터 가장자리 점선 원을 눌러요", "가 보기", "secondary"),
                                         ("UI/Nav/ICON_Nav_Travel.png", "섬 지도", "정찰대를 보내 새 땅을 찾아요", "열기", "secondary")]:
        card(L, CX0, y, CW, 150)
        tile(L, CX0 + 24, y + 22, 104, art(icon, 96))
        T(L, (CX0 + 152, y + 30), title, 32, True)
        T(L, (CX0 + 152, y + 80), sub, 25, False, MUTED)
        button(L, CX1 - 196, y + 38, 156, 68, btn, kind, 28)
        y += 150 + 18
    y += 14

    section(L, y, "완료한 발전", "13 / 30")
    y += 56
    done = [("작은 집", "Settlement/ICON_House_Red.png"), ("의자", "Settlement/ICON_Chair.png"),
            ("광산 길", "Settlement/ICON_MinePath.png"), ("게시판", "Settlement/ICON_Board.png"),
            ("새 이웃의 집", "Settlement/ICON_House_Blue.png"), ("마을회관", "Settlement/ICON_TownHall.png"),
            ("공용 공간", "Settlement/ICON_CommonSpace.png"), ("길드 접수", "Settlement/ICON_GuildOffice.png"),
            ("가로등", "Settlement/ICON_Lamp.png"), ("다음 발전", None)]
    cols, cell, gap = 5, 160, 25
    for i, (name, icon) in enumerate(done):
        cx = CX0 + 12 + (i % cols) * (cell + gap)
        cy = y + (i // cols) * (cell + 64)
        tile(L, cx, cy, cell, art(icon, 140) if icon else None, filled=icon is not None, locked=icon is None)
        T(L, (cx + cell // 2, cy + cell + 26), name, 24, False, COCOA if icon else MUTED, "mm")
    return finish(img, L)


# ------------------------------------------------------------------ 02 주민
def tab_residents():
    img = window("우리 왕국", "Settlement/ICON_TownHall.png", TABS, 1, dots={"구역"})
    L = content_layer()
    y = 360
    card(L, CX0, y, CW, 210)
    stats = [("정착 주민", "17명", "Settlement/ICON_Labor_Settled.png"), ("보낼 수 있음", "5명", "Settlement/ICON_Labor_Available.png"),
             ("일하는 중", "12명", "Settlement/ICON_Labor_Working.png")]
    sw = (CW - 48 - 2 * 18) // 3
    for i, (label, value, icon) in enumerate(stats):
        x = CX0 + 24 + i * (sw + 18)
        inset(L, x, y + 24, sw, 162)
        put_c(L, art(icon, 64), x + 52, y + 105)
        T(L, (x + 96, y + 62), label, 25, False, MUTED)
        T(L, (x + 96, y + 104), value, 46, True)
    y += 210 + 30

    section(L, y, "지금 하는 일", "3건")
    y += 52
    card(L, CX0, y, CW, 480)
    jobs = [("Settlement/ICON_Otter_Builder.png", None, "가로등 공사", "건설 해달 · 짓는 중", 0.7, "1분 20초"),
            (None, "Miner", "광산 주변 정리", "깡깡이 · 데굴이", 0.35, "18초"),
            ("UI/Nav/ICON_Nav_Travel.png", None, "섬 정찰 · 남쪽 바닷가", "첨벙이 외 2명", 0.15, "12분 40초")]
    for i, (icon, cast, title, sub, frac, left) in enumerate(jobs):
        ry = y + 26 + i * 148
        tile(L, CX0 + 26, ry, 124, art(icon, 110) if icon else otter(cast, 110), filled=True)
        T(L, (CX0 + 176, ry + 10), title, 32, True)
        T(L, (CX0 + 176, ry + 56), sub, 25, False, MUTED)
        bar(L, CX0 + 176, ry + 94, 520, frac)
        T(L, (CX1 - 34, ry + 107), left, 28, True, COCOA, "rm")
    y += 480 + 30

    section(L, y, "주민 17명")
    tx = CX1 - 6
    for label, kind in [("일하는 중 12", "normal"), ("쉬는 중 5", "normal"), ("전체", "gold")]:
        tx -= tag(L, tx, y - 6, label, kind, 24, anchor="r") + 12
    y += 60
    status = ["일하는 중", "쉬는 중", "정찰", "일하는 중", "일하는 중", "쉬는 중", "일하는 중", "일하는 중", "쉬는 중", "일하는 중"]
    cols, cell, gap = 5, 160, 25
    for i, (name, cast) in enumerate(OTTERS[:10]):
        cx = CX0 + 12 + (i % cols) * (cell + gap)
        cy = y + (i // cols) * (cell + 104)
        tile(L, cx, cy, cell, otter(cast, 140), filled=status[i] != "쉬는 중")
        T(L, (cx + cell // 2, cy + cell + 24), name, 26, True, anchor="mm")
        kind = {"일하는 중": "on", "쉬는 중": "normal", "정찰": "gold"}[status[i]]
        w = tw(status[i], 20, True) + 40
        tag(L, cx + (cell - w) // 2, cy + cell + 46, status[i], kind, 20)
    return finish(img, L)


# ------------------------------------------------------------------ 구역 지도 (구역_배치안 그림에서 글자를 지운 것)
def zone_map():
    src = Image.open(os.path.join(DOCS_IMG, "구역_배치안.png")).convert("RGBA")
    # (지울 칸, 같은 크기로 덮을 칸) — 글자 상자 위/아래 같은 구역 바닥으로 덮음
    for box_, take in [((176, 286, 296, 340), (176, 340, 296, 394)), ((341, 284, 569, 340), (341, 342, 569, 398)),
                       ((708, 36, 862, 86), (708, 186, 862, 236)), ((114, 684, 256, 738), (114, 738, 256, 792)),
                       ((468, 746, 622, 800), (468, 690, 622, 744)), ((754, 529, 959, 556), (754, 560, 959, 587))]:
        src.paste(src.crop(take), box_[:2])
    return src


def zone_chip(base, cx, cy, name, line, empty=False):
    s1, s2 = 30, 22
    w = max(tw(name, s1, True), tw(line, s2)) + 90
    h = 92
    cx = min(max(cx, w // 2 + 16), W - w // 2 - 16)
    x, y = cx - w // 2, cy - h // 2
    put(base, nine("Common/UI_Button_Paper", w, h), x, y)
    swatch(base, x + 16, y + 22, 40, name if name in ZONE else "빈 땅", dashed=empty)
    T(base, (x + 70, y + 18), name, s1, True)
    T(base, (x + 70, y + 54), line, s2, False, MUTED if empty else GREEN)
    return w


# ------------------------------------------------------------------ 03 구역
EFFECTS = [("생산", "북서쪽 숲 · 건물 3채", "생산 속도 +15%", 15, 20),
           ("주거", "북동쪽 언덕 · 건물 2채", "손님 해달 방문 +10%", 10, 20),
           ("상업", "남쪽 바닷길 · 건물 1채", "판매가 +5%", 5, 10),
           ("농업", "서쪽 들판", "밭 자라는 시간 -10%", 10, 10)]


def tab_districts(as_background=False):
    img = window("우리 왕국", "Settlement/ICON_TownHall.png", TABS, 2, dots={"구역"})
    L = content_layer()
    y = 360
    card(L, CX0, y, CW, 250)
    T(L, (CX0 + 30, y + 30), "왕국 질서도", 34, True)
    T(L, (CX1 - 30, y + 26), "72 / 100", 40, True, anchor="ra")
    bx, bw = CX0 + 30, CW - 60
    bar(L, bx, y + 102, bw, 0.72, 30)
    d = ImageDraw.Draw(L)
    for v, label, done in [(10, "이사 지시서", True), (80, "성", False)]:
        mx = bx + bw * v // 100
        d.line([(mx, y + 92), (mx, y + 142)], fill=COCOA, width=5)
        tag(L, mx - (tw(label, 22, True) + 40) // 2, y + 148, label + (" 열림" if done else " 80"), "on" if done else "normal", 22)
    T(L, (CX0 + 30, y + 206), "맞는 구역에 건물이 들어설수록 올라요 · 성까지 8", 25, False, MUTED)
    y += 250 + 30

    section(L, y, "구역 지도")
    tag(L, CX1 - 6, y - 6, "빈 땅 1곳", "warn", 24, anchor="r")
    y += 56
    card(L, CX0, y, CW, 560)
    m = zone_map().resize((530, 518), Image.LANCZOS)
    mm = Image.new("L", m.size, 0)
    ImageDraw.Draw(mm).rounded_rectangle([0, 0, m.width - 1, m.height - 1], radius=28, fill=255)
    L.paste(m, (CX0 + 22, y + 21), mm)
    ImageDraw.Draw(L).rounded_rectangle([CX0 + 22, y + 21, CX0 + 22 + m.width, y + 21 + m.height], radius=28, outline=COCOA, width=5)
    lx = CX0 + 580
    legend = [("생산", "북서쪽 숲"), ("주거", "북동쪽 언덕"), ("상업", "남쪽 바닷길"), ("농업", "서쪽 들판"),
              ("중앙", "중앙 공터"), ("빈 땅", "동쪽 오솔길")]
    for i, (name, block) in enumerate(legend):
        ly = y + 34 + i * 66
        swatch(L, lx, ly, 44, name, dashed=name == "빈 땅")
        T(L, (lx + 62, ly + 2), name + ("" if name == "빈 땅" else " 지역"), 27, True, MUTED if name == "빈 땅" else COCOA)
        T(L, (lx + 62, ly + 36), block, 21, False, MUTED)
    button(L, lx - 6, y + 448, CX1 - 24 - lx + 6, 84, "구역 정하기", "primary", 32)
    y += 560 + 30

    section(L, y, "구역 효과", "건물이 맞는 구역에 있을 때만")
    y += 52
    card(L, CX0, y, CW, 4 * 118 + 30)
    for i, (name, where, effect, cur, mx_) in enumerate(EFFECTS):
        ry = y + 22 + i * 118
        swatch(L, CX0 + 26, ry + 16, 72, name)
        T(L, (CX0 + 120, ry + 10), name + " 지역", 30, True)
        T(L, (CX0 + 120, ry + 54), where, 23, False, MUTED)
        T(L, (CX1 - 30, ry + 12), effect, 30, True, GREEN, "ra")
        bar(L, CX1 - 330, ry + 62, 300, cur / mx_, 20)
        T(L, (CX1 - 344, ry + 72), f"최대 {mx_}%", 21, False, MUTED, "rm")
        if i < 3:
            ImageDraw.Draw(L).line([(CX0 + 26, ry + 108), (CX1 - 26, ry + 108)], fill=(0xEA, 0xD6, 0xBA, 255), width=3)
    y += 4 * 118 + 30 + 30
    section(L, y, "이사 지시서 #2")
    y += 52
    card(L, CX0, y, CW, 150)
    T(L, (CX0 + 30, y + 30), "건물 2채를 주거 지역으로 (0 / 2)", 30, True)
    T(L, (CX0 + 30, y + 80), "보상 골드 300 · 경험치 120", 25, False, MUTED)
    button(L, CX1 - 210, y + 40, 170, 70, "이사하기", "secondary", 30)
    return finish(img, L)


# ------------------------------------------------------------------ 04 건설 모드 · 구역 보기
def build_districts():
    src = zone_map()
    crop = src.crop((330, 0, 890, 996))
    img = crop.resize((W, int(996 * W / 560)), Image.LANCZOS).crop((0, 0, W, H))
    k = W / 560

    def P(x, y):
        return (x - 330) * k, y * k

    # 위 버튼 (꾸미기 모드 · 완료) + 구역 보기 스위치
    button(img, 30, 60, 330, 96, "꾸미기 모드", "secondary", 36)
    button(img, 840, 60, 210, 96, "완료", "primary", 38)
    put(img, nine("Common/UI_Button_Paper", 330, 84), 30, 176)
    T(img, (58, 216), "구역 보기", 30, True, anchor="lm")
    switch(img, 240, 191, True)
    # 구역 이름표 (색 + 지금 효과)
    zone_chip(img, *P(455, 300), "중앙", "게시판 · 마을회관 · 성")
    zone_chip(img, 880, 760, "주거", "손님 해달 방문 +10%")
    zone_chip(img, *P(545, 690), "상업", "판매가 +5%")
    zone_chip(img, 150, 440, "생산", "생산 속도 +15%")
    # 놓는 중인 집: 초록 바닥 + 미리보기 말풍선
    fx, fy = P(690, 112)
    fw, fh = 300, 200
    foot = Image.new("RGBA", (fw, fh), (120, 200, 110, 120))
    ImageDraw.Draw(foot).rectangle([0, 0, fw - 1, fh - 1], outline=(70, 150, 60, 255), width=6)
    put(img, foot, fx, fy + 110)
    house = load(os.path.join(ART, "Buildings", "Detail4", "HouseRed.png"), 300)
    house.putalpha(house.getchannel("A").point(lambda v: int(v * 0.85)))
    put(img, house, fx, fy + 310 - house.height)
    by = fy + 330
    bw = 470
    bx = fx + fw // 2 - bw // 2 - 60
    put(img, nine("Common/UI_Button_Paper", bw, 132), bx, by)
    d = ImageDraw.Draw(img)
    d.polygon([(bx + bw // 2 + 30, by + 6), (bx + bw // 2 + 60, by - 26), (bx + bw // 2 + 90, by + 6)], fill=(255, 251, 244, 255))
    check(img, bx + 46, by + 48, True, 20)
    T(img, (bx + 80, by + 26), "주거 지역 안", 30, True)
    T(img, (bx + 80, by + 72), "손님 해달 방문 +10% → +15%", 24, False, GREEN)
    # 확인 · 취소
    for i, kind in enumerate(["primary", "option"]):
        bx_, by_ = fx - 112, fy + 140 + i * 104
        put(img, nine(BUTTON[kind], 92, 92), bx_, by_)
        cx_, cy_ = bx_ + 46, by_ + 42
        if kind == "primary":
            d.line([(cx_ - 18, cy_ + 2), (cx_ - 5, cy_ + 15), (cx_ + 19, cy_ - 13)], fill=WHITE, width=10, joint="curve")
        else:
            d.line([(cx_ - 15, cy_ - 15), (cx_ + 15, cy_ + 15)], fill=COCOA, width=9)
            d.line([(cx_ - 15, cy_ + 15), (cx_ + 15, cy_ - 15)], fill=COCOA, width=9)
    # 빈 땅 (동쪽 오솔길): 누르면 바로 용도 고르기
    ex, ey = 812, 1180
    zone_chip(img, ex + 110, ey, "빈 땅", "동쪽 오솔길 · 눌러서 정하기", empty=True)
    tx_, ty_ = ex + 60, ey + 92
    for r_, a_ in [(34, 255), (20, 255)]:
        d.ellipse([tx_ - r_, ty_ - r_, tx_ + r_, ty_ + r_], outline=(255, 255, 255, a_), width=6)
    # 아래 시트: 빈 땅을 눌렀을 때
    sy = 1430
    card(img, 30, sy, 1020, 330)
    T(img, (64, sy + 30), "동쪽 오솔길 · 아직 용도가 없어요", 34, True)
    T(img, (64, sy + 82), "골라 보면 그 구역 효과를 미리 보여 줘요", 25, False, MUTED)
    opts = [("주거", "방문 +5%/채"), ("상업", "판매가 +5%/채"), ("생산", "생산 +5%/채"), ("농업", "자라기 -10%")]
    ow = (1020 - 60 - 3 * 16) // 4
    for i, (name, fx_) in enumerate(opts):
        ox = 60 + i * (ow + 16)
        inset(img, ox, sy + 136, ow, 160)
        swatch(img, ox + ow // 2 - 26, sy + 152, 52, name)
        T(img, (ox + ow // 2, sy + 232), name + " 지역", 26, True, anchor="mm")
        T(img, (ox + ow // 2, sy + 268), fx_, 21, False, GREEN, "mm")
    return img


# ------------------------------------------------------------------ 05 용도 바꾸기 확인
def change_confirm():
    img = tab_districts()
    img.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 128)))
    px, py, pw, ph = 70, 470, 940, 1030
    put(img, nine("Inventory/UI_Inventory_Panel", pw, ph, (252, 245, 230), (72, 72, 72, 80)), px, py)
    title = "용도 바꾸기"
    rw = tw(title, 46, True) + 140
    put(img, nine("Common/UI_Ribbon_Peach", rw, 100, (245, 199, 173)), (W - rw) // 2, py - 50)
    T(img, (W // 2, py - 4), title, 46, True, anchor="mm")
    put(img, sprite("Inventory/UI_Inventory_CloseButton"), px + pw + 20 - 96, py - 34)
    y = py + 86
    T(img, (W // 2, y), "남쪽 바닷길", 40, True, anchor="mm")
    y += 76
    # 상업 → 생산
    for cx, name in [(W // 2 - 190, "상업"), (W // 2 + 190, "생산")]:
        w = tw(name + " 지역", 32, True) + 100
        put(img, nine("Common/UI_Button_Paper", w, 84), cx - w // 2, y - 42)
        swatch(img, cx - w // 2 + 18, y - 20, 40, name)
        T(img, (cx - w // 2 + 72, y - 3), name + " 지역", 32, True, anchor="lm")
    arrow_right(img, W // 2 + 8, y)
    y += 74
    inset(img, px + 40, y, pw - 80, 520)
    T(img, (px + 70, y + 26), "바뀌는 것", 30, True)
    rows = [("warn", "맞지 않게 되는 건물 2채", "요정 상점 · 길드 접수소 → 이사 지시서로 옮길 수 있어요", None),
            ("down", "판매가 보너스", "상업 지역이 없어져요", "+5% → 0%"),
            ("up", "생산 속도 보너스", "생산 지역 건물 4채 (최대 20%)", "+15% → +20%"),
            ("down", "왕국 질서도", "맞지 않는 건물만큼 내려가요", "72 → 61")]
    for i, (kind, title_, sub, value) in enumerate(rows):
        ry = y + 92 + i * 104
        if kind == "warn":
            dot(img, px + 86, ry + 22, 16, RED)
            T(img, (px + 86, ry + 21), "!", 22, True, WHITE, "mm")
        else:
            T(img, (px + 86, ry + 22), "▲" if kind == "up" else "▼", 30, True, GREEN if kind == "up" else RED, "mm")
        T(img, (px + 120, ry), title_, 29, True, RED if kind == "warn" else COCOA)
        T(img, (px + 120, ry + 42), sub, 22, False, MUTED)
        if value:
            T(img, (px + pw - 70, ry + 18), value, 32, False, GREEN if kind == "up" else RED, "ra")
    y += 520 + 30
    T(img, (W // 2, y + 10), "건물은 그대로 남아요. 맞지 않는 건물은 보너스 · 질서도에서 빠져요.", 24, False, MUTED, "mm")
    y += 60
    button(img, px + 60, y, 380, 100, "그대로 두기", "secondary", 34)
    button(img, px + pw - 60 - 380, y, 380, 100, "바꾸기", "feature", 34)
    return img


# ------------------------------------------------------------------ 06 일터
def tab_work(shot_path):
    img = window("우리 왕국", "Settlement/ICON_TownHall.png", TABS, 3, dots={"구역"})
    L = content_layer()
    y = 356
    x = CX0 + 6
    for i, name in enumerate(["생산", "가공", "창고", "설비"]):
        x += tag(L, x, y, name, "gold" if i == 1 else "normal", 30) + 14
    T(L, (CX1 - 6, y + 20), "지금의 \"일터와 설비\" 그대로", 24, False, MUTED, "ra")
    shot = Image.open(shot_path).convert("RGBA")
    body = shot.crop((26, 168, 514, 168 + (VIEW[3] - 450) // 2)).resize((976, VIEW[3] - 450), Image.LANCZOS)
    put(L, body, 52, 450)
    return finish(img, L)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    shots = {"00_hud_level": hud_level, "01_tab_progress": tab_progress, "02_tab_residents": tab_residents,
             "03_tab_districts": tab_districts, "04_build_districts": build_districts, "05_change_confirm": change_confirm}
    for name, fn in shots.items():
        fn().convert("RGB").save(os.path.join(out, name + ".png"))
        print("saved", name)
    if len(sys.argv) > 2 and os.path.exists(sys.argv[2]):
        tab_work(sys.argv[2]).convert("RGB").save(os.path.join(out, "06_tab_work.png"))
        print("saved 06_tab_work")


if __name__ == "__main__":
    main()
