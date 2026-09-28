"""Main-screen HUD + popup mockups (inventory, fairy shop, purchase, decorate mode, settings) over a gameplay screenshot.
usage: python hud_mockup.py <gameplay_screenshot.png> <out_dir> <fairy.png>"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
from ui_gen import (SS, COCOA, CREAM, CREAM_LIP, CREAM_LINE, PAPER, PAPER_LIP, PEACH, PEACH_LIP, EMPTY, EMPTY_LIP,
                    MUSTARD, CORAL, CORAL_LIP, SAGE, FONT, box, coin, close_button, text, paste, mockup)

W, H = 1080, 1920
SAGE_LIP = "#7E9A6C"; LAV = "#C9B6F0"; LAV_LIP = "#A48FD0"; PINK = "#F7B8C8"; PINK_LIP = "#DE8FA5"
MUSTARD_LIP = "#D9A92E"; MUTED = "#9C7C66"; RED = "#E5484D"
EMOJI = "C:/Windows/Fonts/seguiemj.ttf"
FAIRY_POS = (170, 440)  # where the fairy NPC stands in the world (center of its sprite)

# Fairy shop catalog (prices are placeholders): (emoji, name, currency, price, category, seedling?, rarity)
# Toys: the currency follows the rarity (common -> coin, rare+ -> shell).
RARITY = {"흔함": ("#CDBBAA", "#A8927E"), "희귀": ("#9CC8EC", "#6FA3D0"), "특별": ("#C9B6F0", "#A48FD0")}
CATALOG = [("\U0001F954", "감자 모종", "coin", "100", "씨앗", True, "흔함"),
           ("\U0001F360", "고구마 모종", "coin", "150", "씨앗", True, "흔함"),
           ("\U0001F353", "딸기 모종", "coin", "300", "씨앗", True, "희귀"),
           ("\U0001FAB1", "지렁이 미끼 x10", "coin", "50", "낚시", False, "흔함"),
           ("\u26BD", "축구공", "coin", "800", "장난감", False, "흔함"),
           ("\U0001F9E9", "퍼즐", "shell", "25", "장난감", False, "희귀")]


# ---------- small drawing helpers ----------
def emoji(ch, size):
    """Placeholder icon from Segoe UI Emoji, trimmed and fitted into size x size."""
    f = ImageFont.truetype(EMOJI, 160)
    c = Image.new("RGBA", (260, 260), (0, 0, 0, 0))
    ImageDraw.Draw(c).text((20, 20), ch, font=f, embedded_color=True)
    c = c.crop(c.getbbox())
    k = size / max(c.size)
    c = c.resize((max(1, int(c.width * k)), max(1, int(c.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(c, ((size - c.width) // 2, (size - c.height) // 2))
    return out


FAIRY_SRC = None  # set from argv: the fairy NPC illustration (white background)


def fairy_sprite(size):
    """Fairy art with its white background flood-filled away from the corners, fitted into size x size."""
    img = Image.open(FAIRY_SRC).convert("RGBA")
    for corner in [(0, 0), (img.width - 1, 0), (0, img.height - 1), (img.width - 1, img.height - 1)]:
        ImageDraw.floodfill(img, corner, (0, 0, 0, 0), thresh=30)  # low thresh keeps the pale blue wings
    img = img.crop(img.getbbox())
    k = size / max(img.size)
    img = img.resize((int(img.width * k), int(img.height * k)), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(img, ((size - img.width) // 2, (size - img.height) // 2))
    return out


def item_icon(ch, size, seedling=False):
    """Crop emoji; seedlings get a small sprout badge so they read as '모종', not the harvested crop."""
    ic = emoji(ch, size)
    if seedling:
        ic.alpha_composite(emoji("\U0001F331", int(size * 0.45)), (int(size * 0.55), int(size * 0.55)))
    return ic


def shell(size):
    """Gem-like premium shell placeholder: alternating pink/lavender facets, cocoa outline, sparkle."""
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    u = S / 100; ow = 5 * u
    cx, cy, r = 50 * u, 82 * u, 44 * u
    d.pieslice([cx - r - ow, cy - r - ow, cx + r + ow, cy + r + ow], 196, 344, fill=COCOA)
    angles = [200, 229, 256, 284, 311, 340]
    for i in range(len(angles) - 1):
        d.pieslice([cx - r, cy - r, cx + r, cy + r], angles[i], angles[i + 1], fill=PINK if i % 2 == 0 else LAV)
    for a in angles[1:-1]:
        d.pieslice([cx - r, cy - r, cx + r, cy + r], a - 0.6, a + 0.6, fill=COCOA)
    d.rounded_rectangle([36 * u - ow, 72 * u - ow, 64 * u + ow, 92 * u + ow], radius=8 * u, fill=COCOA)
    d.rounded_rectangle([36 * u, 72 * u, 64 * u, 92 * u], radius=6 * u, fill="#A8E6E2")
    d.ellipse([44 * u, 76 * u, 56 * u, 88 * u], fill="white")  # pearl
    d.polygon([(26 * u, 50 * u), (29 * u, 57 * u), (36 * u, 60 * u), (29 * u, 63 * u), (26 * u, 70 * u),
               (23 * u, 63 * u), (16 * u, 60 * u), (23 * u, 57 * u)], fill="white")  # sparkle
    return img.resize((size, size), Image.LANCZOS)


def currency_icon(kind, size):
    return coin(size) if kind == "coin" else shell(int(size * 1.1))


def otext(d, xy, s, size, fill="white", stroke=6, anchor="mm"):
    """Sticker text: white fill + cocoa outline (the TMP Title_Outline preset)."""
    d.text(xy, s, font=ImageFont.truetype(FONT, size), fill=fill, stroke_width=stroke, stroke_fill=COCOA, anchor=anchor)


def round_btn(m, x, y, size, face, lip, icon, shine=True):
    paste(m, box(size, size, size // 2, face, lip, shine=shine), x, y)
    ic = int(size * 0.62)
    paste(m, icon if icon.size[0] == ic else icon.resize((ic, ic), Image.LANCZOS),
          x + (size - ic) // 2, y + (size - 10 - ic) // 2)


def glyph_btn(m, x, y, size, face, lip, kind):
    """Round button with a drawn white glyph: 'check' or 'rotate'."""
    paste(m, box(size, size, size // 2, face, lip), x, y)
    S = size * SS; g = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(g)
    u = S / 100; w = int(11 * u); cy = 45 * u
    if kind == "check":
        pts = [(30 * u, cy), (45 * u, cy + 14 * u), (70 * u, cy - 14 * u)]
        d.line(pts, fill="white", width=w, joint="curve")
        for p in (pts[0], pts[-1]):
            d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill="white")
    else:
        d.arc([28 * u, cy - 22 * u, 72 * u, cy + 22 * u], 300, 220, fill="white", width=w)
        d.polygon([(20 * u, cy - 12 * u), (42 * u, cy - 12 * u), (31 * u, cy + 6 * u)], fill="white")
    paste(m, g.resize((size, size), Image.LANCZOS), x, y)


def badge(m, d, x, y, label):
    paste(m, box(46, 46, 23, RED, "#B8363A", ow=4, lip_h=4), x, y)
    text(d, (x + 23, y + 21), label, 24, fill="white", anchor="mm")


def pill(m, d, x, y, w, icon, value, plus=True):
    """Currency display: icon overlapping the left end, value, optional + (goes to the shop)."""
    paste(m, box(w, 76, 38, PAPER, PAPER_LIP, ow=7, lip_h=8), x, y)
    paste(m, icon, x - 22, y - 8)
    text(d, (x + w - (74 if plus else 26), y + 36), value, 32, anchor="rm")
    if plus:
        paste(m, box(54, 54, 27, SAGE, SAGE_LIP, ow=6, lip_h=6), x + w - 62, y + 10)
        otext(d, (x + w - 35, y + 34), "+", 34, stroke=4)


def price_btn(m, d, x, y, w, h, kind, price):
    """Price button colour = payment method: sage=cash, lavender=shell, mustard=coin."""
    face, lip = {"coin": (MUSTARD, MUSTARD_LIP), "shell": (LAV, LAV_LIP), "cash": (SAGE, SAGE_LIP)}[kind]
    paste(m, box(w, h, h // 2, face, lip, ow=7, lip_h=9), x, y)
    if kind == "cash":
        otext(d, (x + w // 2, y + h // 2 - 4), price, 36, stroke=5)
        return
    ic = currency_icon(kind, 54)
    tw = d.textlength(price, font=ImageFont.truetype(FONT, 36))
    sx = x + (w - (ic.width + 12 + tw)) / 2  # center icon + price as one group
    paste(m, ic, sx, y + (h - 9 - ic.height) // 2)
    otext(d, (sx + ic.width + 12, y + h // 2 - 4), price, 36, stroke=5, anchor="lm")


def dim(img, alpha=150):
    out = img.copy()
    out.alpha_composite(Image.new("RGBA", img.size, (40, 25, 18, alpha)))
    return out


def game_bg(path):
    g = Image.open(path).convert("RGBA")
    g = g.crop((2, 3, g.width - 2, g.height - 2))  # drop the screenshot's dark edge
    k = max(W / g.width, H / g.height)
    g = g.resize((int(g.width * k + 0.5), int(g.height * k + 0.5)), Image.LANCZOS)
    x0, y0 = (g.width - W) // 2, (g.height - H) // 2
    return g.crop((x0, y0, x0 + W, y0 + H))


def tabs(m, d, x, y, labels, selected=0, w=170, gap=26):
    for i, label in enumerate(labels):
        tx = x + i * (w + gap)
        if i == selected:
            paste(m, box(w, 96, 32, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), tx - 8, y - 8)
            text(d, (tx + w // 2, y + 43), label, 34, anchor="mm")
        else:
            paste(m, box(w, 96, 32, CREAM, CREAM_LIP), tx, y)
            text(d, (tx + w // 2, y + 43), label, 34, fill=MUTED, anchor="mm")


# ---------- screens ----------
def top_bar(m, d):
    paste(m, box(290, 112, 40, PAPER, PAPER_LIP), 60, 80)
    round_btn(m, 22, 70, 128, PEACH, PEACH_LIP, emoji("\U0001F9A6", 80))
    text(d, (160, 96), "해달왕", 30)
    paste(m, box(64, 36, 18, MUSTARD, MUSTARD_LIP, ow=4, lip_h=4), 262, 94)
    text(d, (294, 111), "Lv.5", 20, anchor="mm")
    d.rounded_rectangle([160, 144, 326, 164], radius=10, fill=COCOA)
    d.rounded_rectangle([164, 148, 164 + int(158 * 0.62), 160], radius=6, fill=SAGE)
    pill(m, d, 392, 92, 256, coin(76), "12,480")
    pill(m, d, 690, 92, 244, shell(84), "350")


def fairy_hint(m, d):
    """Tap the fairy in the world -> fairy shop. A bobbing bubble tells the player it is interactive."""
    fx, fy = FAIRY_POS
    paste(m, fairy_sprite(170), fx - 85, fy - 85)
    paste(m, box(150, 84, 42, LAV, LAV_LIP, shine=True), fx - 75, fy - 190)
    d.polygon([(fx - 16, fy - 116), (fx + 16, fy - 116), (fx, fy - 92)], fill=COCOA)
    d.polygon([(fx - 9, fy - 118), (fx + 9, fy - 118), (fx, fy - 103)], fill=LAV_LIP)
    paste(m, emoji("\U0001F6CD", 46), fx - 62, fy - 180)
    otext(d, (fx + 24, fy - 154), "상점", 28, stroke=5)


def hud(bg):
    m = bg.copy(); d = ImageDraw.Draw(m)
    top_bar(m, d)
    round_btn(m, 962, 84, 92, CREAM, CREAM_LIP, emoji("\u2699", 60), shine=False)  # settings
    fairy_hint(m, d)

    # side shortcuts: left = daily (below the fairy), right = mail / event
    for x, y, ch, label, b in [(30, 620, "\U0001F4C5", "출석", "!"),
                               (958, 250, "\u2709", "우편", "2"),
                               (958, 420, "\U0001F381", "이벤트", None)]:
        round_btn(m, x, y, 92, CREAM, CREAM_LIP, emoji(ch, 57))
        otext(d, (x + 46, y + 112), label, 26, stroke=5)
        if b:
            badge(m, d, x + 60, y - 10, b)

    # bottom dock: thumb zone. center = decorate mode (raised); the shop lives on the fairy in the world
    paste(m, box(1020, 196, 60, CREAM, CREAM_LIP, inner_line=CREAM_LINE), 30, 1694)
    slots = [("\U0001F4D6", "도감", None), ("\U0001F4DC", "퀘스트", "3"), (None, "꾸미기", None),
             ("\U0001F392", "가방", "N"), ("\U0001F3A3", "낚시", None)]
    for i, (ch, label, b) in enumerate(slots):
        cx = 30 + 102 + i * 204
        if ch is None:
            paste(m, box(176, 176, 88, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), cx - 96, 1580)
            paste(m, emoji("\U0001F3E0", 108), cx - 54, 1606)
            otext(d, (cx, 1834), label, 30, stroke=6)
            continue
        paste(m, emoji(ch, 92), cx - 46, 1728)
        text(d, (cx, 1846), label, 28, anchor="mm")
        if b:
            badge(m, d, cx + 30, 1716, b)
    return m


def chip(m, d, x, y, label, selected, w=None):
    w = w or int(d.textlength(label, font=ImageFont.truetype(FONT, 28))) + 56
    if selected:
        paste(m, box(w, 64, 32, SAGE, SAGE_LIP, ow=6, lip_h=6), x, y)
        otext(d, (x + w // 2, y + 28), label, 28, stroke=4)
    else:
        paste(m, box(w, 64, 32, PAPER, PAPER_LIP, ow=6, lip_h=6), x, y)
        text(d, (x + w // 2, y + 28), label, 28, fill=MUTED, anchor="mm")
    return w


# Bag: two-level categories. Group tab -> (sub-filter chips, items). Toys are shown too (Pocket Camp style):
# the bag and the decorate storage are two views of the same items, and toys don't use bag capacity.
BAG_GROUPS = ["전체", "농사", "낚시", "꾸미기"]
BAG = {
    "농사": (["전체", "작물", "모종"],
           [("\U0001F955", "12", False, "흔함"), ("\U0001F954", "5", False, "흔함"), ("\U0001F353", "3", False, "희귀"),
            ("\U0001F954", "3", True, "흔함"), ("\U0001F360", "1", True, "흔함"), ("\U0001F353", "2", True, "희귀")],
           dict(icon=("\U0001F955", False), name="당근", sub="작물 · 흔함", rows=[("보유 개수", "12", None), ("판매가", "50", "coin")],
                desc=["해달 왕국 밭에서 갓 뽑은 아삭한 당근.", "해달들이 간식으로 가장 좋아해요.", "시장에 팔면 코인을 받을 수 있어요."],
                button=None), True),
    "꾸미기": (["전체", "장난감"],
            [("⚽", "1", False, "흔함"), ("\U0001F9E9", "0", False, "희귀")],
            dict(icon=("⚽", False), name="축구공", sub="장난감 · 흔함", rows=[("보유 (전체)", "2개", None), ("배치됨", "1개", None)],
                 desc=["광장에 놓으면 해달들이 모여서", "공놀이를 해요."],
                 button="배치하러 가기"), False),
}


def bag_detail(m, d, info):
    rx, ry, rw, rh = 40, 90, 1000, 740
    paste(m, box(rw, rh, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), rx, ry)
    paste(m, close_button(96), rx + rw - 80, ry - 34)
    ix, iy = rx + 50, ry + 56
    paste(m, box(340, 340, 60, PEACH, PEACH_LIP, shine=True), ix, iy)
    paste(m, item_icon(info["icon"][0], 240, info["icon"][1]), ix + 50, iy + 40)
    bx, bw = ix + 380, 520
    text(d, (bx + 4, iy + 6), info["name"], 60)
    text(d, (bx + 6, iy + 86), info["sub"], 30, fill=MUTED)
    for j, (label, val, cur) in enumerate(info["rows"]):
        by = iy + 146 + j * 106
        paste(m, box(bw, 92, 32, PAPER, PAPER_LIP), bx, by)
        text(d, (bx + 30, by + 41), label, 32, anchor="lm")
        if cur:
            paste(m, currency_icon(cur, 44), bx + bw - 150, by + 19)
        text(d, (bx + bw - 34, by + 41), val, 36, anchor="rm")
    dx, dy = ix, iy + 380
    paste(m, box(900, 260, 32, PAPER, PAPER_LIP), dx, dy)
    for k, line in enumerate(info["desc"]):
        text(d, (dx + 40, dy + 46 + k * 54), line, 31, fill="#6B4A3A")
    if info["button"]:  # toys: jump to decorate mode with this item picked up
        paste(m, box(340, 88, 44, SAGE, SAGE_LIP, ow=7, lip_h=9, shine=True), dx + 900 - 370, dy + 260 - 118)
        otext(d, (dx + 900 - 200, dy + 260 - 78), info["button"], 32, stroke=5)


def inventory(bg, group="농사"):
    chips, items, info, capped = BAG[group]
    m = dim(bg, 170); d = ImageDraw.Draw(m)
    bag_detail(m, d, info)
    tabs(m, d, 72, 862, BAG_GROUPS, selected=BAG_GROUPS.index(group))
    px, py, pw, ph = 40, 948, 1000, 916
    paste(m, box(pw, ph, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), px, py)

    x = px + 44
    for i, label in enumerate(chips):
        x += chip(m, d, x, py + 40, label, i == 0) + 16
    paste(m, box(190, 64, 32, PAPER, PAPER_LIP, ow=6, lip_h=6), px + pw - 234, py + 40)
    text(d, (px + pw - 139, py + 68), "등급순 ▼", 26, fill=MUTED, anchor="mm")

    # grid on its own layer, clipped to the panel so the 4th row reads as "scroll for more"
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
    cell, gap, gx, gy = 190, 22, px + 87, py + 140
    for i in range(16):
        cx, cy = gx + (i % 4) * (cell + gap), gy + (i // 4) * (cell + gap)
        if i < len(items):
            ch, cnt, seedling, rarity = items[i]
            sel, has = i == 0, cnt != "0"
            paste(layer, box(cell, cell, 36, PEACH if has else EMPTY, PEACH_LIP if has else EMPTY_LIP, shine=has,
                             ring=MUSTARD if sel else None), cx - (8 if sel else 0), cy - (8 if sel else 0))
            ic = item_icon(ch, 124, seedling)
            if not has:  # every copy is placed in the world
                ic.putalpha(ic.getchannel("A").point(lambda a: int(a * 0.4)))
            paste(layer, ic, cx + 33, cy + 22)
            ld.ellipse([cx + cell - 42, cy + 18, cx + cell - 18, cy + 42], fill=RARITY[rarity][0], outline=COCOA, width=4)
            text(ld, (cx + cell - 20, cy + cell - 28), f"x{cnt}" if has else "배치됨", 28 if has else 24,
                 fill=COCOA if has else MUTED, anchor="rs")
        elif not capped or i < 10:
            paste(layer, box(cell, cell, 36, EMPTY, EMPTY_LIP), cx, cy)
        else:  # locked slots (expand with 조개); toys have no capacity limit
            paste(layer, box(cell, cell, 36, "#E3D3C2", "#CDB9A4"), cx, cy)
            lock = emoji("\U0001F512", 70); lock.putalpha(lock.getchannel("A").point(lambda a: int(a * 0.7)))
            paste(layer, lock, cx + 60, cy + 52)
    clip = Image.new("L", (W, H), 0)
    ImageDraw.Draw(clip).rectangle([px + 20, py + 120, px + pw - 20, py + ph - 40], fill=255)
    layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (W, H), 0), clip))
    m.alpha_composite(layer)
    return m


def shop_frame(m, d, bubble):
    paste(m, box(300, 100, 50, LAV, LAV_LIP, shine=True), 30, 80)
    otext(d, (180, 126), "요정상점", 40)
    pill(m, d, 384, 92, 250, coin(76), "12,480", plus=False)
    pill(m, d, 672, 92, 250, shell(84), "350", plus=False)
    paste(m, close_button(96), 954, 80)
    paste(m, fairy_sprite(240), 46, 216)
    paste(m, box(700, 170, 44, PAPER, PAPER_LIP), 320, 250)
    for k, line in enumerate(bubble):
        text(d, (360, 292 + k * 50), line, 34)


def fairy_shop(bg):
    m = dim(bg, 170); d = ImageDraw.Draw(m)
    shop_frame(m, d, ["어서 와! 모종이랑 미끼,", "광장 장난감도 있어~"])
    tabs(m, d, 66, 488, ["전체", "씨앗", "낚시", "장난감"])
    paste(m, box(1020, 1300, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), 30, 574)

    cw, ch_, gap = 450, 380, 24
    for i, (ch, name, cur, price, cat, seedling, rarity) in enumerate(CATALOG):
        x, y = 70 + (i % 2) * (cw + 40), 624 + (i // 2) * (ch_ + gap)
        paste(m, box(cw, ch_, 44, PAPER, PAPER_LIP), x, y)
        paste(m, box(370, 170, 36, EMPTY, EMPTY_LIP, ow=0, lip_h=0), x + 40, y + 30)
        ic = item_icon(ch, 130, seedling)
        paste(m, ic, x + cw // 2 - 65, y + 50)
        paste(m, box(110, 44, 22, CREAM, CREAM_LIP, ow=4, lip_h=4), x + 26, y + 16)  # category chip
        text(d, (x + 81, y + 36), cat, 22, fill=MUTED, anchor="mm")
        rf, rl = RARITY[rarity]  # rarity chip
        paste(m, box(96, 44, 22, rf, rl, ow=4, lip_h=4), x + cw - 122, y + 16)
        otext(d, (x + cw - 74, y + 36), rarity, 22, stroke=3)
        text(d, (x + cw // 2, y + 240), name, 34, anchor="mm")
        price_btn(m, d, x + 40, y + 276, 370, 80, cur, price)
    return m


def purchase_popup(bg):
    """Quantity picker shown after tapping a card (seeds/bait stack; toys are usually 1)."""
    m = dim(fairy_shop(bg), 170); d = ImageDraw.Draw(m)
    px, py, pw, ph = 110, 520, 860, 900
    paste(m, box(pw, ph, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), px, py)
    paste(m, box(380, 110, 55, PEACH, PEACH_LIP, shine=True), px + pw // 2 - 190, py - 50)
    otext(d, (px + pw // 2, py + 2), "구매하기", 44)
    paste(m, close_button(96), px + pw - 76, py - 34)

    paste(m, box(220, 220, 48, PEACH, PEACH_LIP, shine=True), px + 70, py + 110)
    paste(m, item_icon("\U0001F353", 150, True), px + 105, py + 138)
    text(d, (px + 330, py + 150), "딸기 모종", 48)
    text(d, (px + 332, py + 222), "밭에 심으면 딸기가 자라요.", 28, fill="#6B4A3A")
    text(d, (px + 332, py + 264), "보유: 2개", 28, fill=MUTED)

    # quantity stepper
    y = py + 400
    paste(m, box(96, 96, 48, CREAM, CREAM_LIP), px + 170, y)
    d.rounded_rectangle([px + 198, y + 38, px + 238, y + 50], radius=6, fill=COCOA)  # minus (font has no U+2212)
    paste(m, box(300, 96, 40, PAPER, PAPER_LIP), px + 280, y)
    text(d, (px + 430, y + 44), "5", 48, anchor="mm")
    paste(m, box(96, 96, 48, SAGE, SAGE_LIP, shine=True), px + 594, y)
    otext(d, (px + 642, y + 42), "+", 48, stroke=5)
    text(d, (px + pw // 2, y + 150), "합계", 30, fill=MUTED, anchor="mm")
    paste(m, coin(56), px + pw // 2 - 110, y + 184)
    text(d, (px + pw // 2 + 30, y + 212), "1,500", 48, anchor="mm")

    by = py + ph - 170
    paste(m, box(320, 100, 50, CREAM, CREAM_LIP), px + 90, by)
    text(d, (px + 250, by + 46), "취소", 38, fill=MUTED, anchor="mm")
    price_btn(m, d, px + 450, by, 320, 100, "coin", "1,500")
    return m


def decorate_mode(bg):
    """Place plaza toys: grid overlay, ghost preview with footprint, action buttons, storage drawer."""
    m = bg.copy()
    # tile grid over the play area
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0)); g = ImageDraw.Draw(grid)
    t, top, bottom = 108, 230, 1380
    for x in range(0, W + 1, t):
        g.line([(x, top), (x, bottom)], fill=(255, 255, 255, 90), width=3)
    for y in range(top, bottom + 1, t):
        g.line([(0, y), (W, y)], fill=(255, 255, 255, 90), width=3)
    # footprint: green = can place, red = blocked (on the log)
    ok = (5 * t, top + 5 * t)
    g.rounded_rectangle([ok[0] + 4, ok[1] + 4, ok[0] + t - 4, ok[1] + t - 4], radius=16,
                        fill=(157, 181, 138, 150), outline=(255, 255, 255, 230), width=5)
    bad = (5 * t, top + 7 * t)
    g.rounded_rectangle([bad[0] + 4, bad[1] + 4, bad[0] + 2 * t - 4, bad[1] + t - 4], radius=16,
                        fill=(229, 72, 77, 120), outline=(255, 255, 255, 200), width=5)
    m.alpha_composite(grid)
    d = ImageDraw.Draw(m)

    # ghost (semi-transparent) soccer ball on the green tile + floating action buttons
    ghost = emoji("\u26BD", 88); ghost.putalpha(ghost.getchannel("A").point(lambda a: int(a * 0.85)))
    paste(m, ghost, ok[0] + 10, ok[1] + 6)
    ax, ay = ok[0] + t // 2, ok[1] - 120
    glyph_btn(m, ax - 150, ay, 88, LAV, LAV_LIP, "rotate")
    glyph_btn(m, ax - 44, ay, 88, SAGE, SAGE_LIP, "check")
    paste(m, close_button(88), ax + 62, ay)
    paste(m, emoji("\U0001F9E9", 70), bad[0] + 70, bad[1] + 18)  # an already placed puzzle, shown on a blocked spot

    # top bar: mode title + done
    paste(m, box(380, 100, 50, PEACH, PEACH_LIP, shine=True), 30, 80)
    otext(d, (220, 126), "꾸미기 모드", 40)
    paste(m, box(220, 100, 50, SAGE, SAGE_LIP, shine=True), 830, 80)
    otext(d, (940, 126), "완료", 40)

    # hint toast
    paste(m, box(700, 76, 38, PAPER, PAPER_LIP, ow=6, lip_h=7), 190, 1300)
    text(d, (540, 1336), "초록색 칸에 놓을 수 있어요", 30, anchor="mm")

    # storage drawer
    paste(m, box(1020, 500, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), 30, 1400)
    text(d, (86, 1466), "보관함", 38, anchor="lm")
    tabs(m, d, 300, 1428, ["전체", "장난감"], w=150, gap=20)
    cell, gap = 190, 22
    items = [("\u26BD", "축구공", "1", True), ("\U0001F9E9", "퍼즐", "0", False)]
    for i in range(4):
        cx, cy = 86 + i * (cell + gap), 1560
        if i < len(items):
            ch, name, cnt, sel = items[i]
            has = cnt != "0"
            paste(m, box(cell, cell, 36, PEACH if has else EMPTY, PEACH_LIP if has else EMPTY_LIP,
                         shine=has, ring=MUSTARD if sel else None), cx - (8 if sel else 0), cy - (8 if sel else 0))
            ic = emoji(ch, 110)
            if not has:
                ic.putalpha(ic.getchannel("A").point(lambda a: int(a * 0.4)))
            paste(m, ic, cx + 40, cy + 26)
            text(d, (cx + cell - 20, cy + cell - 28), f"x{cnt}", 28, anchor="rs")
            text(d, (cx + cell // 2, cy + cell + 34), name if has else "배치됨", 26,
                 fill=COCOA if has else MUTED, anchor="mm")
        elif i == len(items):  # shortcut to the fairy shop's toy tab
            paste(m, box(cell, cell, 36, EMPTY, EMPTY_LIP), cx, cy)
            otext(d, (cx + cell // 2, cy + 80), "+", 72, fill=MUTED, stroke=0)
            text(d, (cx + cell // 2, cy + cell + 34), "상점", 26, fill=MUTED, anchor="mm")
        else:
            paste(m, box(cell, cell, 36, EMPTY, EMPTY_LIP), cx, cy)
    return m


def slider(m, x, y, w, pct):
    paste(m, box(w, 40, 20, EMPTY, EMPTY_LIP, ow=5, lip_h=4), x, y)
    fw = max(40, int(w * pct))
    paste(m, box(fw, 40, 20, PEACH, PEACH_LIP, ow=5, lip_h=4), x, y)
    paste(m, box(64, 64, 32, PAPER, PAPER_LIP, ow=6, lip_h=6), x + fw - 40, y - 12)


def toggle(m, x, y, on):
    paste(m, box(140, 68, 34, SAGE if on else EMPTY, SAGE_LIP if on else EMPTY_LIP, ow=6, lip_h=6), x, y)
    paste(m, box(56, 56, 28, PAPER, PAPER_LIP, ow=5, lip_h=5), x + (78 if on else 6), y + 4)


def settings(bg):
    m = dim(hud(bg), 170); d = ImageDraw.Draw(m)
    px, py, pw, ph = 70, 380, 940, 1150
    paste(m, box(pw, ph, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), px, py)
    paste(m, box(380, 110, 55, PEACH, PEACH_LIP, shine=True), px + pw // 2 - 190, py - 50)
    otext(d, (px + pw // 2, py + 2), "설정", 48)
    paste(m, close_button(96), px + pw - 76, py - 34)

    lx, cx, y = px + 70, px + 400, py + 130
    for label, kind, val in [("배경음", "slider", 0.7), ("효과음", "slider", 0.45),
                             ("진동", "toggle", True), ("푸시 알림", "toggle", False), ("언어", "select", "한국어")]:
        text(d, (lx, y + 20), label, 38, anchor="lm")
        if kind == "slider":
            slider(m, cx, y, 420, val)
        elif kind == "toggle":
            toggle(m, cx + 280, y - 14, val)
        else:
            paste(m, box(420, 76, 30, PAPER, PAPER_LIP, ow=6, lip_h=7), cx, y - 18)
            text(d, (cx + 30, y + 18), val, 34, anchor="lm")
            text(d, (cx + 390, y + 18), "▼", 26, fill=MUTED, anchor="rm")
        y += 130
    d.line([(lx, y), (px + pw - 70, y)], fill=CREAM_LINE, width=4)
    y += 50
    for i, label in enumerate(["계정 연동", "쿠폰 입력", "고객센터", "이용약관"]):
        bx, by = lx + (i % 2) * 410, y + (i // 2) * 120
        paste(m, box(390, 96, 40, PAPER, PAPER_LIP, ow=7, lip_h=8), bx, by)
        text(d, (bx + 195, by + 44), label, 34, anchor="mm")
    text(d, (px + pw // 2, py + ph - 70), "버전 0.1.0  ·  해달 왕국", 28, fill=MUTED, anchor="mm")
    return m


if __name__ == "__main__":
    src, out, FAIRY_SRC = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out, exist_ok=True)
    bg = game_bg(src)
    screens = [("01_HUD", "메인 화면", hud(bg)), ("02_Inventory", "가방 (농사)", inventory(bg, "농사")),
               ("02b_InventoryDecor", "가방 (꾸미기)", inventory(bg, "꾸미기")),
               ("03_FairyShop", "요정상점", fairy_shop(bg)), ("04_Purchase", "구매 팝업", purchase_popup(bg)),
               ("05_Decorate", "꾸미기 모드", decorate_mode(bg)), ("06_Settings", "설정", settings(bg))]
    cols = 4
    sheet = Image.new("RGBA", (cols * 540 + (cols + 1) * 40, 2 * (960 + 110) + 20), "#FFF4E6")
    sd = ImageDraw.Draw(sheet)
    for i, (name, label, img) in enumerate(screens):
        img.convert("RGB").save(os.path.join(out, f"Screen_{name}.png"))
        x, y = 40 + (i % cols) * 580, 90 + (i // cols) * 1070
        sheet.alpha_composite(img.resize((540, 960), Image.LANCZOS), (x, y))
        sd.text((x + 270, y - 42), f"{i + 1}. {label}", font=ImageFont.truetype(FONT, 36), fill=COCOA, anchor="mm")
    sheet.convert("RGB").save(os.path.join(out, "Screens_Overview.png"))
    print("done")
