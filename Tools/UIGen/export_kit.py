"""Export the common UI kit (buttons, ribbons, chips, tags, toggles, sliders, badges...) as individual sprites.
usage: python export_kit.py <assets_out_dir> <preview_png>
Prints each sprite's 9-slice border (L, T, R, B) and writes kit_borders.json next to this script."""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont
from ui_gen import (SS, COCOA, CREAM, CREAM_LIP, PAPER, PAPER_LIP, PEACH, PEACH_LIP, EMPTY, EMPTY_LIP, MUSTARD,
                    CORAL, CORAL_LIP, SAGE, FONT, box)
from hud_mockup import SAGE_LIP, LAV, LAV_LIP, MUSTARD_LIP, RED, RARITY, glyph_btn


def sliced(r, face, lip, ow=7, lip_h=9, shine=False, ring=None, pad=0, outline=True):
    """Square 9-slice source big enough that the corners, shine and bottom lip all sit inside the borders."""
    side = 2 * r + lip_h + 16
    img = box(side, side, r, face, lip, ow=ow if outline else 0, lip_h=lip_h, shine=shine, ring=ring, pad=pad)
    t = pad + r + 2
    return img, (t, t, t, pad + r + lip_h + 2)


def fixed(size, face, lip, ow=7, lip_h=9, shine=False, ring=None):
    return box(size, size, size // 2, face, lip, ow=ow, lip_h=lip_h, shine=shine, ring=ring)


def with_glyph(img, kind, color="white", lip_h=9, pad=0):
    """Draw a +/- bar glyph centered on the button face (face is lip_h shorter than the box)."""
    W, H = img.size
    S = SS; g = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0)); d = ImageDraw.Draw(g)
    cx, cy = W * S / 2, (H - lip_h) * S / 2
    a, w = W * 0.2 * S, W * 0.11 * S
    d.rounded_rectangle([cx - a, cy - w / 2, cx + a, cy + w / 2], radius=w / 2, fill=color)
    if kind == "plus":
        d.rounded_rectangle([cx - w / 2, cy - a, cx + w / 2, cy + a], radius=w / 2, fill=color)
    img.alpha_composite(g.resize((W, H), Image.LANCZOS))
    return img


def glyph_button(size, face, lip, kind):
    c = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    glyph_btn(c, 0, 0, size, face, lip, kind)
    return c


def tile(ok):
    size = 108; S = SS
    g = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0)); d = ImageDraw.Draw(g)
    fill = (157, 181, 138, 150) if ok else (229, 72, 77, 120)
    d.rounded_rectangle([4 * S, 4 * S, (size - 4) * S, (size - 4) * S], radius=16 * S, fill=fill,
                        outline=(255, 255, 255, 230), width=5 * S)
    return g.resize((size, size), Image.LANCZOS)


def build():
    k = {}  # name: (image, border or None)
    # --- 9-slice: buttons (colour = meaning; price buttons follow the payment currency) ---
    k["UI_Button_Coin"] = sliced(44, MUSTARD, MUSTARD_LIP)
    k["UI_Button_Shell"] = sliced(44, LAV, LAV_LIP)
    k["UI_Button_Primary"] = sliced(44, SAGE, SAGE_LIP, shine=True)
    k["UI_Button_Secondary"] = sliced(44, CREAM, CREAM_LIP)
    k["UI_Button_Paper"] = sliced(40, PAPER, PAPER_LIP)  # currency pill, sort, dropdown, list buttons, toast
    # --- 9-slice: title ribbons ---
    k["UI_Ribbon_Peach"] = sliced(52, PEACH, PEACH_LIP, ow=8, lip_h=10, shine=True)
    k["UI_Ribbon_Lav"] = sliced(52, LAV, LAV_LIP, ow=8, lip_h=10, shine=True)
    # --- 9-slice: filter chips, tags ---
    k["UI_Chip_Selected"] = sliced(32, SAGE, SAGE_LIP, ow=6, lip_h=6)
    k["UI_Chip_Normal"] = sliced(32, PAPER, PAPER_LIP, ow=6, lip_h=6)
    for key, label in [("Common", "흔함"), ("Rare", "희귀"), ("Epic", "특별")]:
        k[f"UI_Tag_Rarity_{key}"] = sliced(22, *RARITY[label], ow=4, lip_h=4)
    k["UI_Tag_Highlight"] = sliced(22, CORAL, CORAL_LIP, ow=4, lip_h=4)  # 1회 한정, +10% 보너스
    k["UI_Tag_Category"] = sliced(22, CREAM, CREAM_LIP, ow=4, lip_h=4)
    # --- 9-slice: settings controls ---
    k["UI_Toggle_On"] = sliced(34, SAGE, SAGE_LIP, ow=6, lip_h=6)
    k["UI_Toggle_Off"] = sliced(34, EMPTY, EMPTY_LIP, ow=6, lip_h=6)
    k["UI_Slider_Track"] = sliced(20, EMPTY, EMPTY_LIP, ow=5, lip_h=4)
    k["UI_Slider_Fill"] = sliced(20, PEACH, PEACH_LIP, ow=5, lip_h=4)
    # --- 9-slice: misc boxes ---
    k["UI_Box_Inset"] = sliced(36, EMPTY, EMPTY_LIP, lip_h=0, outline=False)  # icon well inside shop cards
    locked = box(128, 128, 36, "#E3D3C2", "#CDB9A4", pad=8)  # same canvas as the other slot sprites
    k["UI_Inventory_Slot_Locked"] = (locked, (52, 52, 52, 60))
    # --- fixed size (Image Type: Simple) ---
    k["UI_RoundButton_Cream"] = (fixed(96, CREAM, CREAM_LIP), None)
    k["UI_RoundButton_Peach"] = (fixed(96, PEACH, PEACH_LIP, shine=True), None)
    k["UI_RoundButton_Lav"] = (fixed(96, LAV, LAV_LIP, shine=True), None)
    k["UI_RoundButton_Featured"] = (fixed(176, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), None)  # dock center
    k["UI_Button_Confirm"] = (glyph_button(88, SAGE, SAGE_LIP, "check"), None)
    k["UI_Button_Rotate"] = (glyph_button(88, LAV, LAV_LIP, "rotate"), None)
    k["UI_Button_Plus"] = (with_glyph(fixed(96, SAGE, SAGE_LIP), "plus"), None)  # no shine: it hits the glyph
    k["UI_Button_Minus"] = (with_glyph(fixed(96, CREAM, CREAM_LIP), "minus", COCOA), None)
    k["UI_Button_PlusSmall"] = (with_glyph(fixed(54, SAGE, SAGE_LIP, ow=6, lip_h=6), "plus", lip_h=6), None)
    k["UI_Badge"] = (fixed(46, RED, "#B8363A", ow=4, lip_h=4), None)
    k["UI_Knob"] = (fixed(64, PAPER, PAPER_LIP, ow=6, lip_h=6), None)
    k["UI_Tile_OK"] = (tile(True), None)
    k["UI_Tile_Blocked"] = (tile(False), None)
    return k


def preview(kit, path):
    F = ImageFont.truetype(FONT, 18)
    cols, cw, chh = 6, 260, 230
    rows = (len(kit) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * chh), "#CFE9EC"); d = ImageDraw.Draw(sheet)
    for i, (name, (img, border)) in enumerate(kit.items()):
        x, y = (i % cols) * cw, (i // cols) * chh
        im = img if max(img.size) <= 180 else img.resize((180, int(180 * img.height / img.width)), Image.LANCZOS)
        sheet.alpha_composite(im, (x + (cw - im.width) // 2, y + 10 + (170 - im.height) // 2))
        d.text((x + cw // 2, y + 196), name.replace("UI_", ""), font=F, fill=COCOA, anchor="mm")
        d.text((x + cw // 2, y + 216), f"{img.width}x{img.height}" + (f"  {border}" if border else "  fixed"),
               font=F, fill="#9C7C66", anchor="mm")
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    out, prev = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    kit = build()
    borders = {}
    for name, (img, border) in kit.items():
        img.save(os.path.join(out, name + ".png"))
        borders[name] = border
        print(f"{name:28s} {img.width:>3}x{img.height:<3} " + (f"border L{border[0]} T{border[1]} R{border[2]} B{border[3]}" if border else "fixed"))
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "kit_borders.json"), "w", encoding="utf-8") as f:
        json.dump(borders, f, indent=1)
    preview(kit, prev)
