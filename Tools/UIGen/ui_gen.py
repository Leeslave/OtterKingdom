"""OtterKingdom inventory UI generator: sticker-style frames (cocoa outline, flat pastel, bottom lip)."""
import os, sys
from PIL import Image, ImageDraw, ImageFont

SS = 4  # supersample
COCOA = "#4B2E22"
CREAM = "#FFF4E6"; CREAM_LIP = "#EBCFA8"; CREAM_LINE = "#F0DCC0"
PAPER = "#FFFBF4"; PAPER_LIP = "#EAD6BA"
PEACH = "#F5C6AA"; PEACH_LIP = "#E0A283"
EMPTY = "#F7E6D2"; EMPTY_LIP = "#E9D0B2"
MUSTARD = "#F2C94C"; CORAL = "#E57D6B"; CORAL_LIP = "#C65F4E"
SKY = "#A9CBE3"; SAGE = "#9DB58A"; ORANGE = "#F29A4A"
FONT = "C:/Windows/Fonts/NanumGothicExtraBold.ttf"


def box(w, h, r, face, lip, ow=8, lip_h=10, inner_line=None, shine=False, ring=None, pad=None):
    """Rounded sticker box. Everything decorative stays inside the corner radius so it 9-slices cleanly.
    pad: transparent margin around the box (defaults to 8 when a ring is drawn) so state sprites share one canvas size."""
    if pad is None:
        pad = 8 if ring else 0
    W, H = (w + pad * 2) * SS, (h + pad * 2) * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = lambda v: int(round(v * SS))
    o = pad
    if ring:  # selection ring outside the outline
        d.rounded_rectangle([0, 0, W - 1, H - 1], radius=s(r + pad), fill=ring)
    d.rounded_rectangle([s(o), s(o), s(o + w) - 1, s(o + h) - 1], radius=s(r), fill=COCOA)
    d.rounded_rectangle([s(o + ow), s(o + ow), s(o + w - ow) - 1, s(o + h - ow) - 1], radius=s(r - ow), fill=lip)
    d.rounded_rectangle([s(o + ow), s(o + ow), s(o + w - ow) - 1, s(o + h - ow - lip_h) - 1], radius=s(r - ow), fill=face)
    if inner_line:
        ins = 18
        d.rounded_rectangle([s(o + ow + ins), s(o + ow + ins), s(o + w - ow - ins) - 1, s(o + h - ow - lip_h - ins) - 1],
                            radius=s(max(r - ow - ins, 4)), outline=inner_line, width=s(3))
    if shine:
        k = r / 64  # scale the highlight with the corner size
        b = o + ow  # anchor to the face so the shine never crosses the outline
        d.rounded_rectangle([s(b + 26 * k), s(b + 14 * k), s(b + 54 * k), s(b + 24 * k)], radius=s(5 * k), fill=(255, 255, 255, 220))
        d.ellipse([s(b + 12 * k), s(b + 28 * k), s(b + 22 * k), s(b + 38 * k)], fill=(255, 255, 255, 220))
    return img.resize((w + pad * 2, h + pad * 2), Image.LANCZOS)


def ring_overlay(w, h, r, color=MUSTARD, pad=8, overlap=3):
    """Selection ring only (transparent center), laid on top of a box with the same w/h/r/pad."""
    W, H = (w + pad * 2) * SS, (h + pad * 2) * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = lambda v: int(round(v * SS))
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=s(r + pad), fill=color)
    i = pad + overlap  # tuck slightly under the cocoa outline so no seam shows
    d.rounded_rectangle([s(i), s(i), W - s(i) - 1, H - s(i) - 1], radius=s(r - overlap), fill=(0, 0, 0, 0))
    return img.resize((w + pad * 2, h + pad * 2), Image.LANCZOS)


def outlined_poly(d, pts, fill, ow):
    d.line(pts + [pts[0]], fill=COCOA, width=ow * 2, joint="curve")
    for p in pts:
        d.ellipse([p[0] - ow, p[1] - ow, p[0] + ow, p[1] + ow], fill=COCOA)
    d.polygon(pts, fill=fill)


def carrot(size):
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    ow = int(size * 0.035 * SS)
    u = S / 100
    for (x0, y0, x1, y1) in [(40, 6, 54, 34), (52, 4, 66, 32), (30, 12, 46, 36)]:
        d.ellipse([x0 * u - ow, y0 * u - ow, x1 * u + ow, y1 * u + ow], fill=COCOA)
    for (x0, y0, x1, y1) in [(40, 6, 54, 34), (52, 4, 66, 32), (30, 12, 46, 36)]:
        d.ellipse([x0 * u, y0 * u, x1 * u, y1 * u], fill=SAGE)
    pts = [(28 * u, 34 * u), (40 * u, 28 * u), (58 * u, 28 * u), (72 * u, 34 * u), (54 * u, 92 * u), (46 * u, 92 * u)]
    outlined_poly(d, pts, ORANGE, ow)
    for y, x0, x1 in [(46, 38, 48), (60, 52, 60), (72, 44, 51)]:
        d.line([(x0 * u, y * u), (x1 * u, y * u)], fill=COCOA, width=int(ow * 0.8))
    d.ellipse([34 * u, 36 * u, 40 * u, 42 * u], fill=(255, 255, 255, 200))
    return img.resize((size, size), Image.LANCZOS)


def fish(size):
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    ow = int(size * 0.035 * SS); u = S / 100
    tail = [(70 * u, 50 * u), (92 * u, 32 * u), (92 * u, 68 * u)]
    outlined_poly(d, tail, "#8FB6D4", ow)
    d.ellipse([10 * u - ow, 28 * u - ow, 78 * u + ow, 72 * u + ow], fill=COCOA)
    d.ellipse([10 * u, 28 * u, 78 * u, 72 * u], fill=SKY)
    d.ellipse([24 * u, 40 * u, 32 * u, 48 * u], fill=COCOA)
    d.ellipse([38 * u, 58 * u, 50 * u, 64 * u], fill="#F4A6A0")
    d.ellipse([30 * u, 32 * u, 44 * u, 37 * u], fill=(255, 255, 255, 200))
    return img.resize((size, size), Image.LANCZOS)


def coin(size):
    S = size * SS; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    ow = int(size * 0.07 * SS)
    d.ellipse([0, 0, S - 1, S - 1], fill=COCOA)
    d.ellipse([ow, ow, S - ow, S - ow], fill=MUSTARD)
    m = S * 0.28
    d.ellipse([m, m, S - m, S - m], outline="#D9A92E", width=ow)
    return img.resize((size, size), Image.LANCZOS)


def close_button(size=96, ow=8, lip_h=10):
    """Round coral button with a baked white X (fixed size, not 9-sliced)."""
    base = box(size, size, size // 2, CORAL, CORAL_LIP, ow=ow, lip_h=lip_h)  # no shine: it would collide with the X
    S = size * SS; x_img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(x_img)
    cx, cy = S / 2, (ow + (size - ow - lip_h)) / 2 * SS  # center of the face, not the whole box
    a, w = size * 0.17 * SS, size * 0.11 * SS
    for (x0, y0), (x1, y1) in [((-a, -a), (a, a)), ((-a, a), (a, -a))]:
        d.line([(cx + x0, cy + y0), (cx + x1, cy + y1)], fill="white", width=int(w))
        for px, py in [(x0, y0), (x1, y1)]:  # round caps
            d.ellipse([cx + px - w / 2, cy + py - w / 2, cx + px + w / 2, cy + py + w / 2], fill="white")
    base.alpha_composite(x_img.resize((size, size), Image.LANCZOS))
    return base


def text(d, xy, s, size, fill=COCOA, anchor="la"):
    d.text(xy, s, font=ImageFont.truetype(FONT, size), fill=fill, anchor=anchor)


def paste(base, im, x, y):
    base.alpha_composite(im, (int(x), int(y)))


def mockup(path):
    """Portrait layout (Canvas Scaler reference 1080x1920): detail on top, tabs + grid below (thumb reach)."""
    W, H = 1080, 1920
    m = Image.new("RGBA", (W, H), "#CFE9EC")
    d = ImageDraw.Draw(m)
    for y in range(60, H, 150):  # soft water waves
        for x in range(-40, W + 40, 120):
            d.arc([x, y, x + 60, y + 30], 200, 340, fill="#BBDDE2", width=5)

    # --- top: detail panel ---
    rx, ry, rw, rh = 40, 90, 1000, 740
    paste(m, box(rw, rh, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), rx, ry)
    paste(m, close_button(96), rx + rw - 80, ry - 34)

    ix, iy = rx + 50, ry + 56
    paste(m, box(340, 340, 60, PEACH, PEACH_LIP, shine=True), ix, iy)
    paste(m, carrot(270), ix + 35, iy + 26)
    bx, bw = ix + 380, 520
    text(d, (bx + 4, iy + 6), "당근", 60)
    text(d, (bx + 6, iy + 86), "채소 · 흔함", 30, fill="#9C7C66")
    for j, (label, val) in enumerate([("보유 개수", "12"), ("가격", "50")]):
        by = iy + 146 + j * 106
        paste(m, box(bw, 92, 32, PAPER, PAPER_LIP), bx, by)
        text(d, (bx + 30, by + 41), label, 32, anchor="lm")
        if label == "가격":
            paste(m, coin(44), bx + bw - 150, by + 19)
        text(d, (bx + bw - 34, by + 41), val, 36, anchor="rm")

    dx, dy = ix, iy + 380
    paste(m, box(rw - 100, 260, 32, PAPER, PAPER_LIP), dx, dy)
    for k, line in enumerate(["해달 왕국 밭에서 갓 뽑은 아삭한 당근.",
                              "해달들이 간식으로 가장 좋아하는 채소예요.",
                              "시장에 팔면 코인을 받을 수 있어요."]):
        text(d, (dx + 40, dy + 46 + k * 54), line, 31, fill="#6B4A3A")

    # --- bottom: tabs + grid panel ---
    ty = 862
    paste(m, box(170, 96, 32, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), 72, ty - 8)
    text(d, (72 + 8 + 85, ty + 43), "채소", 34, anchor="mm")
    paste(m, box(170, 96, 32, CREAM, CREAM_LIP), 268, ty)
    text(d, (268 + 85, ty + 43), "어류", 34, fill="#9C7C66", anchor="mm")

    px, py, pw, ph = 40, 948, 1000, 916
    paste(m, box(pw, ph, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), px, py)
    cell, gap, cols = 190, 22, 4
    gx = px + (pw - (cell * cols + gap * (cols - 1))) // 2
    gy = py + 42
    for i in range(16):
        cx, cy = gx + (i % cols) * (cell + gap), gy + (i // cols) * (cell + gap)
        if i == 0:
            paste(m, box(cell, cell, 36, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), cx - 8, cy - 8)
            paste(m, carrot(136), cx + 27, cy + 16)
            text(d, (cx + cell - 20, cy + cell - 28), "x12", 28, anchor="rs")
        elif i == 1:
            paste(m, box(cell, cell, 36, PEACH, PEACH_LIP, shine=True), cx, cy)
            paste(m, fish(136), cx + 27, cy + 16)
            text(d, (cx + cell - 20, cy + cell - 28), "x3", 28, anchor="rs")
        else:
            paste(m, box(cell, cell, 36, EMPTY, EMPTY_LIP), cx, cy)
    m.convert("RGB").save(path)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    mockup(os.path.join(out, "inventory_mockup.png"))
    # name: (image, 9-slice border L, T, R, B)
    sprites = {
        "UI_Inventory_Panel": (box(192, 192, 64, CREAM, CREAM_LIP, inner_line=CREAM_LINE), (72, 72, 72, 80)),
        # tabs: 112x96 box + 8px pad, so normal/selected swap without layout shift
        "UI_Inventory_Tab_Normal": (box(112, 96, 32, CREAM, CREAM_LIP, pad=8), (48, 48, 48, 56)),
        "UI_Inventory_Tab_Selected": (box(112, 96, 32, PEACH, PEACH_LIP, shine=True, ring=MUSTARD), (48, 48, 48, 56)),
        # slots: 128x128 box + 8px pad; the ring is a separate overlay so it can pulse
        "UI_Inventory_Slot_Filled": (box(128, 128, 36, PEACH, PEACH_LIP, shine=True, pad=8), (52, 52, 52, 60)),
        "UI_Inventory_Slot_Empty": (box(128, 128, 36, EMPTY, EMPTY_LIP, pad=8), (52, 52, 52, 60)),
        "UI_Inventory_Slot_SelectRing": (ring_overlay(128, 128, 36), (52, 52, 52, 52)),
        # shared by the info boxes (owned count, price) and the description box
        "UI_Inventory_Box": (box(112, 104, 32, PAPER, PAPER_LIP), (40, 40, 40, 48)),
        "UI_Inventory_CloseButton": (close_button(96), None),
    }
    for name, (img, border) in sprites.items():
        img.save(os.path.join(out, name + ".png"))
        info = f"border L{border[0]} T{border[1]} R{border[2]} B{border[3]}" if border else "no 9-slice"
        print(f"{name}.png {img.size[0]}x{img.size[1]}  {info}")
