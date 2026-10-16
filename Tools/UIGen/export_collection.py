"""Export the 도감 (collection) resources that the mockup needs beyond the common kit.
- UI parts        -> Assets/Art/UI/Collection/
- silhouettes     -> Assets/Art/UI/Collection/Silhouettes/   (solid dark brown, doc: "실루엣: 짙은 갈색 단색")
- otter portraits -> Assets/Art/Otter/                        (cut from the in-game sprite sheets)
usage: python export_collection.py <preview_png>
Prints 9-slice borders (L, T, R, B) and merges them into kit_borders.json."""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont
from ui_gen import SS, COCOA, PAPER, PAPER_LIP, MUSTARD, SAGE, FONT, box
from hud_mockup import ART_DIR
from export_kit import sliced

ROOT = os.path.normpath(os.path.join(ART_DIR, "..", ".."))
UI_OUT = os.path.join(ART_DIR, "UI", "Collection")
SIL_OUT = os.path.join(UI_OUT, "Silhouettes")
OTTER_OUT = os.path.join(ART_DIR, "Otter")
MUSTARD_LIP = "#D9A92E"
BADGE_FACE, BADGE_LIP, BADGE_TEXT = "#D4ECC6", "#A9CC98", "#3F6B38"
SIL = (91, 58, 44)
ICON, MARGIN = 256, 8


def fit(im, size=ICON, margin=MARGIN):
    im = im.crop(im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox())
    k = (size - 2 * margin) / max(im.size)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    return out


def silhouette(im):
    s = Image.new("RGBA", im.size, SIL + (255,))
    s.putalpha(im.getchannel("A"))
    return s


def frame(rel, cols, rows, col, row):
    sh = Image.open(os.path.join(ROOT, "Assets", "Sprites", "Characters", rel)).convert("RGBA")
    fw, fh = sh.width // cols, sh.height // rows
    return sh.crop((col * fw, row * fh, (col + 1) * fw, (row + 1) * fh))


def leaf(size=64):
    """Small green leaf ornament for the title board (sticker style)."""
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    u = S / 100; ow = 6 * u
    d.line([(4 * u, 50 * u), (22 * u, 50 * u)], fill=COCOA, width=int(8 * u))  # stem
    d.ellipse([18 * u - ow, 27 * u - ow, 94 * u + ow, 73 * u + ow], fill=COCOA)
    d.ellipse([18 * u, 27 * u, 94 * u, 73 * u], fill="#9DB58A")
    d.line([(24 * u, 50 * u), (88 * u, 50 * u)], fill="#6E8F5C", width=int(5 * u))
    img = img.rotate(35, resample=Image.BICUBIC)
    return img.resize((size, size), Image.LANCZOS)


def paw(size=64, color="#E8A05E"):
    """Paw print for the otter '방문 흔적' state."""
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    u = S / 100; ow = 5 * u
    shapes = [(28, 48, 72, 88), (12, 30, 30, 52), (30, 12, 48, 36), (52, 12, 70, 36), (70, 30, 88, 52)]
    for x0, y0, x1, y1 in shapes:
        d.ellipse([x0 * u - ow, y0 * u - ow, x1 * u + ow, y1 * u + ow], fill=COCOA)
    for x0, y0, x1, y1 in shapes:
        d.ellipse([x0 * u, y0 * u, x1 * u, y1 * u], fill=color)
    return img.resize((size, size), Image.LANCZOS)


def small_check(size=48, color=BADGE_TEXT):
    """Check mark drawn inside the '획득 완료 / 등록 완료' status badge (no background)."""
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    u = S / 100; w = int(16 * u)
    pts = [(18 * u, 52 * u), (42 * u, 74 * u), (82 * u, 28 * u)]
    d.line(pts, fill=color, width=w, joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=color)
    return img.resize((size, size), Image.LANCZOS)


def build():
    ui = {
        # same canvas/borders as UI_Inventory_Tab_* so tabs can swap sprites without layout shift
        "UI_Tab_Gold": (box(112, 96, 32, MUSTARD, MUSTARD_LIP, shine=True, pad=8), (48, 48, 48, 56)),
        "UI_TitleBoard": sliced(52, PAPER, PAPER_LIP, ow=8, lip_h=10),
        "UI_Badge_Status": sliced(32, BADGE_FACE, BADGE_LIP, ow=5, lip_h=5),
        "UI_Deco_Leaf": (leaf(), None),
        "UI_Icon_CheckSmall": (small_check(), None),
        "UI_Icon_Paw": (paw(), None),
    }
    farmer = frame("FarmerOtter/FarmerOtter_Sheet.png", 8, 5, 0, 2)       # front idle
    fisher = frame("FishingOtter/FishingOtter_CatchFish.png", 8, 1, 6, 0)  # holding a fish
    otters = {"ICON_Otter_Farmer": fit(farmer), "ICON_Otter_Fisher": fit(fisher)}
    sil_src = {"SIL_Carrot": "Item/Vegetable/Carrot.png", "SIL_Potato": "Item/Vegetable/Potato.png",
               "SIL_Cucumber": "Item/Vegetable/ICON_Cucumber.png", "SIL_SweetPotato": "Item/Vegetable/ICON_SweetPotato.png",
               "SIL_Mackerel": "Item/Fish/Mackerel.png"}
    sils = {k: silhouette(fit(Image.open(os.path.join(ART_DIR, v)).convert("RGBA"))) for k, v in sil_src.items()}
    sils["SIL_Otter"] = silhouette(otters["ICON_Otter_Farmer"])  # generic '미발견' otter shape
    return ui, otters, sils


def preview(groups, path):
    F = ImageFont.truetype(FONT, 18)
    items = [(n, im, b) for g in groups for n, (im, b) in g.items()]
    cols, cw, ch = 7, 200, 210
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * ch), "#FFF4E6"); d = ImageDraw.Draw(sheet)
    for i, (name, im, border) in enumerate(items):
        x, y = (i % cols) * cw, (i // cols) * ch
        t = im.copy(); t.thumbnail((150, 150), Image.LANCZOS)
        sheet.alpha_composite(t, (x + (cw - t.width) // 2, y + 12 + (150 - t.height) // 2))
        d.text((x + cw // 2, y + 176), name, font=F, fill=COCOA, anchor="mm")
        d.text((x + cw // 2, y + 196), f"{im.width}x{im.height}" + (f" {border}" if border else ""), font=F, fill="#9C7C66", anchor="mm")
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    ui, otters, sils = build()
    for folder, group in [(UI_OUT, ui), (OTTER_OUT, {k: (v, None) for k, v in otters.items()}),
                          (SIL_OUT, {k: (v, None) for k, v in sils.items()})]:
        os.makedirs(folder, exist_ok=True)
        for name, (img, border) in (group.items() if folder == UI_OUT else group.items()):
            img.save(os.path.join(folder, name + ".png"))
            print(f"{os.path.relpath(os.path.join(folder, name + '.png'), ROOT):58s} {img.width}x{img.height} " +
                  (f"border L{border[0]} T{border[1]} R{border[2]} B{border[3]}" if border else ""))
    borders_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "kit_borders.json")
    borders = json.load(open(borders_path, encoding="utf-8")) if os.path.exists(borders_path) else {}
    borders.update({k: b for k, (_, b) in ui.items()})
    json.dump(borders, open(borders_path, "w", encoding="utf-8"), indent=1)
    preview([ui, {k: (v, None) for k, v in otters.items()}, {k: (v, None) for k, v in sils.items()}], sys.argv[1])
