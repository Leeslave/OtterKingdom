"""도감 (collection) mockups, following Docs/도감퀘스트UI_오프라인연동_클로드전달용.md (teammate reference):
detail on top, 채소/어류/해달 tabs, 4-column grid with green checks, state legend at the bottom.
usage: python collection_mockup.py <gameplay_screenshot.png> <out_dir> <otter_cut_dir>"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
from ui_gen import (COCOA, CREAM, CREAM_LIP, CREAM_LINE, PAPER, PAPER_LIP, PEACH, PEACH_LIP, EMPTY, EMPTY_LIP,
                    MUSTARD, CORAL, CORAL_LIP, SAGE, FONT, box, close_button, text, paste)
from hud_mockup import W, H, SAGE_LIP, MUTED, emoji, otext, glyph_btn, dim, game_bg, ART_DIR

MUSTARD_LIP = "#D9A92E"
BADGE_FACE, BADGE_LIP, BADGE_TEXT = "#D4ECC6", "#A9CC98", "#3F6B38"
SIL = (91, 58, 44)  # doc: silhouettes are a solid dark brown
ROOT = os.path.join(ART_DIR, "..", "..")
OTTER_CUT = None  # set from argv: otters cut from the user's otter sheet


def fit(im, size):
    im = im.crop(im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox())
    k = size / max(im.size)
    im = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    return out


def item(rel):
    return lambda s: fit(Image.open(os.path.join(ART_DIR, "Item", rel)).convert("RGBA"), s)


def sheet_frame(rel, cols, rows, col, row):
    def make(s):
        sh = Image.open(os.path.join(ROOT, "Assets", "Sprites", "Characters", rel)).convert("RGBA")
        fw, fh = sh.width // cols, sh.height // rows
        return fit(sh.crop((col * fw, row * fh, (col + 1) * fw, (row + 1) * fh)), s)
    return make


def cut(i):
    return lambda s: fit(Image.open(os.path.join(OTTER_CUT, f"otter_{i}.png")).convert("RGBA"), s)


def emo(ch):
    return lambda s: emoji(ch, s)


def silhouette(icon):
    s = Image.new("RGBA", icon.size, SIL + (255,))
    s.putalpha(icon.getchannel("A").point(lambda v: int(v * 0.92)))
    return s


def faded(icon, k=0.45):
    icon = icon.copy()
    icon.putalpha(icon.getchannel("A").point(lambda v: int(v * k)))
    return icon


FARMER = sheet_frame("FarmerOtter/FarmerOtter_Sheet.png", 8, 5, 0, 2)
FISHER = sheet_frame("FishingOtter/FishingOtter_CatchFish.png", 8, 1, 6, 0)

# (make, state, is_new)   state: found | missing | visited
TABS = {
    "채소": dict(
        count="수집 4 / 12", legend=[("missing", "미획득"), ("found", "획득 완료")], badge="획득 완료",
        detail=(item("Vegetable/Carrot.png"), "당근", "밭에서 자라는 아삭한 채소.",
                ["달콤하고 아삭해서 해달들이 좋아해요.", "획득 장소: 밭"]),
        cells=[(item("Vegetable/Carrot.png"), "found", False), (item("Vegetable/ICON_Cucumber.png"), "found", False),
               (item("Vegetable/Potato.png"), "found", False), (item("Vegetable/ICON_SweetPotato.png"), "found", True)]
              + [(emo(c), "missing", False) for c in ["\U0001F353", "\U0001F345", "\U0001F33D", "\U0001F383",
                                                      "\U0001F9C5", "\U0001F966", "\U0001F9C4", "\U0001F346"]]),
    "어류": dict(
        count="수집 1 / 12", legend=[("missing", "미획득"), ("found", "획득 완료")], badge="획득 완료",
        detail=(item("Fish/Mackerel.png"), "고등어", "푸른 줄무늬를 가진 바다 물고기.",
                ["은빛 배를 반짝이며 헤엄쳐요.", "획득 장소: 낚시터"]),
        cells=[(item("Fish/Mackerel.png"), "found", False)]
              + [(emo(c), "missing", False) for c in ["\U0001F41F", "\U0001F420", "\U0001F421", "\U0001F988", "\U0001F991",
                                                      "\U0001F419", "\U0001F980", "\U0001F990", "\U0001F99E", "\U0001FABC",
                                                      "\U0001F42C"]]),
    "해달": dict(
        count="등록 4 / 12  ·  방문 흔적 3", legend=[("missing", "미발견"), ("visited", "방문 흔적"), ("found", "등록 완료")],
        badge="등록 완료",
        detail=(FARMER, "농부 해달", "밭을 돌보는 부지런한 친구",
                ["작은 새싹을 보면 그냥 지나치지 못해요.", "좋아하는 것: 당근"]),
        cells=[(FARMER, "found", False), (FISHER, "found", False), (cut(5), "found", False), (cut(1), "found", True),
               (cut(6), "visited", False), (cut(7), "visited", False), (cut(8), "visited", False)]
              + [(FARMER, "missing", False)] * 5),
}


def check_badge(m, x, y, size=52):
    glyph_btn(m, x, y, size, SAGE, SAGE_LIP, "check")


def legend_icon(m, state, x, y, tab):
    if state == "found":
        check_badge(m, x, y, 56)
    elif state == "visited":
        paste(m, emoji("\U0001F43E", 52), x + 2, y + 2)
    else:
        sample = FARMER(60) if tab == "해달" else TABS[tab]["cells"][-1][0](60)
        paste(m, silhouette(sample), x, y)


def collection(bg, tab):
    spec = TABS[tab]
    m = dim(bg, 170); d = ImageDraw.Draw(m)

    # --- top: selected entry detail ---
    paste(m, box(1000, 560, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), 40, 100)
    paste(m, box(380, 110, 55, PAPER, PAPER_LIP), 350, 34)  # sign-board style title (doc: 나무 간판)
    for lx in (398, 632):
        paste(m, emoji("\U0001F33F", 46), lx, 60)
    text(d, (540, 86), "도감", 56, anchor="mm")
    paste(m, close_button(96), 954, 66)

    make, name, intro, desc = spec["detail"]
    paste(m, box(380, 390, 48, PEACH, PEACH_LIP, shine=True), 90, 180)
    paste(m, make(300), 130, 215)
    text(d, (515, 196), name, 60)
    paste(m, box(250, 64, 32, BADGE_FACE, BADGE_LIP, ow=5, lip_h=5), 515, 290)
    g = Image.new("RGBA", (40, 40), (0, 0, 0, 0))
    ImageDraw.Draw(g).line([(8, 20), (17, 29), (32, 11)], fill=BADGE_TEXT, width=7, joint="curve")
    paste(m, g, 540, 300)
    text(d, (668, 320), spec["badge"], 28, fill=BADGE_TEXT, anchor="mm")
    text(d, (517, 400), intro, 30, fill=MUTED, anchor="lm")
    paste(m, box(525, 170, 32, PAPER, PAPER_LIP), 492, 450)
    for k, line in enumerate(desc):
        text(d, (518, 500 + k * 62), line, 26, fill="#6B4A3A", anchor="lm")

    # --- tabs (doc: selected tab is gold) ---
    for i, label in enumerate(["채소", "어류", "해달"]):
        x = 90 + i * 310
        if label == tab:
            paste(m, box(280, 96, 40, MUSTARD, MUSTARD_LIP, shine=True), x, 684)
            text(d, (x + 140, 728), label, 36, anchor="mm")
        else:
            paste(m, box(280, 96, 40, CREAM, CREAM_LIP), x, 692)
            text(d, (x + 140, 736), label, 36, fill="#6B4A3A", anchor="mm")

    # --- grid ---
    paste(m, box(1000, 1100, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), 40, 776)
    text(d, (92, 846), spec["count"], 32, fill="#6B4A3A", anchor="lm")
    cw, chh, gap, x0, y0 = 212, 228, 20, 86, 894
    for i, (make, state, is_new) in enumerate(spec["cells"]):
        x, y = x0 + (i % 4) * (cw + gap), y0 + (i // 4) * (chh + gap)
        selected = i == 0
        if state == "found":
            paste(m, box(cw, chh, 36, PEACH, PEACH_LIP, shine=True, ring=MUSTARD if selected else None),
                  x - (8 if selected else 0), y - (8 if selected else 0))
            paste(m, make(172), x + 20, y + 22)
            check_badge(m, x + cw - 62, y + 8)
            if is_new:  # 상세기획서 9.2: mark newly found entries until viewed
                paste(m, box(84, 42, 21, CORAL, CORAL_LIP, ow=4, lip_h=4), x + 10, y + chh - 54)
                otext(d, (x + 52, y + chh - 35), "NEW", 20, stroke=3)
        elif state == "visited":  # otters that came by while offline
            paste(m, box(cw, chh, 36, EMPTY, EMPTY_LIP), x, y)
            paste(m, faded(make(160)), x + 26, y + 14)
            paste(m, box(180, 50, 25, PAPER, PAPER_LIP, ow=4, lip_h=4), x + 16, y + chh - 68)
            paste(m, emoji("\U0001F43E", 30), x + 28, y + chh - 60)
            text(d, (x + 124, y + chh - 45), "방문 흔적", 22, fill="#6B4A3A", anchor="mm")
        else:
            paste(m, box(cw, chh, 36, EMPTY, EMPTY_LIP), x, y)
            paste(m, silhouette(make(150)), x + 31, y + 18)
            text(d, (x + cw // 2, y + chh - 38), "???", 28, fill="#6B4A3A", anchor="mm")

    # --- legend ---
    paste(m, box(920, 140, 44, PAPER, PAPER_LIP), 80, 1668)
    items = spec["legend"]
    slot_w = 920 // len(items)
    for i, (state, label) in enumerate(items):
        cx = 80 + slot_w * i + slot_w // 2
        tw = d.textlength(label, font=ImageFont.truetype(FONT, 30))
        start = int(cx - (60 + 16 + tw) / 2)
        legend_icon(m, state, start, 1702, tab)
        text(d, (start + 76, 1732), label, 30, fill="#6B4A3A", anchor="lm")
    return m


if __name__ == "__main__":
    src, out, OTTER_CUT = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out, exist_ok=True)
    bg = game_bg(src)
    shots = [(f"Screen_Collection_{k}", k, collection(bg, k)) for k in ["채소", "어류", "해달"]]
    sheet = Image.new("RGBA", (3 * 540 + 4 * 40, 960 + 110), "#FFF4E6"); sd = ImageDraw.Draw(sheet)
    for i, (name, label, img) in enumerate(shots):
        img.convert("RGB").save(os.path.join(out, name + ".png"))
        x = 40 + i * 580
        sheet.alpha_composite(img.resize((540, 960), Image.LANCZOS), (x, 90))
        sd.text((x + 270, 48), f"도감 · {label} 탭", font=ImageFont.truetype(FONT, 36), fill=COCOA, anchor="mm")
    sheet.convert("RGB").save(os.path.join(out, "Collection_Overview.png"))
    print("done")
