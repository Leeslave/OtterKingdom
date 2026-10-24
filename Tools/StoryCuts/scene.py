"""Story-cut building blocks: game art (map, buildings, props, FX) and rigged otters laid out on a 1536x1024 cut.

Everything drawn here comes from the game's own art; the few things the game has no picture for
(paper, notebook, easel) are plain shapes in the rig outline colour (#320a00) so they sit in the same style.
"""
import math
import pathlib
import random

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

import rig

ROOT = rig.ROOT
ART = ROOT / "Assets/Art"
RES = ROOT / "Assets/Resources"
W, H = 1536, 1024
OUTLINE = (50, 10, 0, 255)
PAPER = (255, 247, 228, 255)

_images = {}


def img(path):
    """Game art by path under Assets/Art (or Assets/Resources when it starts with 'Resources/')."""
    if path not in _images:
        base = ROOT / "Assets" if path.startswith("Resources/") else ART
        _images[path] = Image.open(base / path).convert("RGBA")
    return _images[path]


def fit(image, height=None, width=None):
    k = height / image.height if height else width / image.width
    return image.resize((max(1, round(image.width * k)), max(1, round(image.height * k))), Image.LANCZOS)


class Cut:
    def __init__(self, background):
        self.canvas = background.convert("RGBA").resize((W, H), Image.LANCZOS) if background.size != (W, H) else background.convert("RGBA")

    # --- placing -------------------------------------------------------------------------------
    def put(self, image, x, y, anchor=(.5, 1.0), alpha=1.0, angle=0.0):
        """Paste with the anchor (fraction of the image; default bottom-centre = where it stands) at x, y."""
        if angle:
            image = image.rotate(angle, resample=Image.BICUBIC, expand=True)
        if alpha < 1:
            image = image.copy()
            image.putalpha(image.getchannel("A").point(lambda a: int(a * alpha)))
        self.canvas.alpha_composite(image, (round(x - image.width * anchor[0]), round(y - image.height * anchor[1])))
        return self

    def shadow(self, x, y, width, alpha=.55):
        a = img("Settlement/FX_Shadow.png").getchannel("A").resize((round(width), round(width * .32)), Image.LANCZOS)
        s = Image.new("RGBA", a.size, (70, 45, 20, 0))
        s.putalpha(a)
        return self.put(s, x, y, (.5, .5), alpha * .6)

    def prop(self, path, x, y, height=None, width=None, shadow=0.0, flip=False, **kw):
        image = fit(img(path), height, width)
        if flip:
            image = image.transpose(Image.FLIP_LEFT_RIGHT)
        if shadow:
            self.shadow(x, y - image.height * .02, image.width * shadow)
        return self.put(image, x, y, **kw)

    def otter(self, rig_name, x, y, height=380, shadow=True, **pose):
        """A rigged otter standing (feet) at x, y; height = standing otter height in px (~330 rig px)."""
        scale = height / 330
        picture, feet = rig.render(rig_name, max(scale, .3), pose)
        if shadow:
            self.shadow(x + (8 if not pose.get("flip") else -8) * scale, y - 4 * scale, 150 * scale, .5)
        self.canvas.alpha_composite(picture, (round(x - feet[0]), round(y - feet[1])))
        return self

    # --- effects -------------------------------------------------------------------------------
    def fx(self, path, points, size, alpha=1.0, angle_range=0, seed=1):
        r = random.Random(seed)
        base = fit(img(path), size)
        for p in points:
            k = p[2] if len(p) > 2 else 1.0
            im = base if k == 1 else fit(base, size * k)
            self.put(im, p[0], p[1], (.5, .5), alpha, r.uniform(-angle_range, angle_range))
        return self

    def scatter(self, path, box, count, size, seed=3, alpha=1.0, angle_range=180, size_jitter=.35):
        r = random.Random(seed)
        pts = [(r.uniform(box[0], box[2]), r.uniform(box[1], box[3]), r.uniform(1 - size_jitter, 1 + size_jitter)) for _ in range(count)]
        return self.fx(path, pts, size, alpha, angle_range, seed)

    def zzz(self, x, y, size=70):
        z = img("Resources/WalkerRigV1/fx-z.png")
        for i, k in enumerate((.7, 1.0, 1.3)):
            self.put(fit(z, size * k), x + i * size * .55, y - i * size * .75, (.5, .5), .95 - i * .1)
        return self

    def bubble(self, x, y, width, icons=(), text_lines=None):
        """Speech bubble (UI_Bubble_Speech) with item icons inside; tail points at bottom-centre (x, y)."""
        b = fit(img("Settlement/UI_Bubble_Speech.png"), width=width)
        left, top = x - b.width / 2, y - b.height
        self.put(b, x, y)
        inner_h = b.height * .55
        if icons:
            gap = width * .06
            icon_imgs = [fit(img(i), inner_h) if isinstance(i, str) else fit(i, inner_h) for i in icons]
            total = sum(i.width for i in icon_imgs) + gap * (len(icon_imgs) - 1)
            cx = x - total / 2
            for i in icon_imgs:
                self.put(i, cx, top + b.height * .41, (0, .5))
                cx += i.width + gap
        return self

    def glow(self, x, y, size, color=(255, 236, 170), alpha=.8):
        g = fit(img("Settlement/FX_GlowSoft.png"), size)
        tinted = Image.new("RGBA", g.size, color + (0,))
        tinted.putalpha(g.getchannel("A").point(lambda a: int(a * alpha)))
        return self.put(tinted, x, y, (.5, .5))

    # --- grading -------------------------------------------------------------------------------
    def grade(self, color, strength, mode="multiply"):
        layer = Image.new("RGBA", (W, H), color + (255,))
        rgb = self.canvas.convert("RGB")
        mixed = ImageChops.multiply(rgb, layer.convert("RGB")) if mode == "multiply" else ImageChops.screen(rgb, layer.convert("RGB"))
        self.canvas = Image.blend(rgb, mixed, strength).convert("RGBA")
        return self

    def night(self):
        """Evening blue: colours kept (multiply by a light blue), then lights are added on top."""
        rgb = self.canvas.convert("RGB")
        blue = ImageChops.multiply(rgb, Image.new("RGB", (W, H), (118, 128, 205)))
        self.canvas = ImageEnhance.Color(blue).enhance(1.15).convert("RGBA")
        return self

    def vignette(self, strength=.45, color=(60, 30, 10)):
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).ellipse((-W * .15, -H * .2, W * 1.15, H * 1.2), fill=255)
        mask = mask.filter(ImageFilter.GaussianBlur(160))
        dark = Image.new("RGBA", (W, H), color + (255,))
        inv = mask.point(lambda v: int((255 - v) * strength))
        self.canvas = Image.composite(dark, self.canvas, inv)
        return self

    def light(self, x, y, radius, color=(255, 214, 140), strength=.35):
        """Warm pool of light (screen blend), e.g. a lamp at night."""
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).ellipse((x - radius, y - radius * .8, x + radius, y + radius * .8), fill=int(255 * strength))
        mask = mask.filter(ImageFilter.GaussianBlur(radius * .45))
        lit = ImageChops.screen(self.canvas.convert("RGB"), Image.new("RGB", (W, H), color))
        self.canvas = Image.composite(lit.convert("RGBA"), self.canvas, mask)
        return self

    def blur_back(self, horizon, radius=4):
        """Soften everything above `horizon` px (depth of field for close-ups)."""
        back = self.canvas.filter(ImageFilter.GaussianBlur(radius))
        mask = Image.new("L", (W, H), 0)
        ImageDraw.Draw(mask).rectangle((0, 0, W, horizon), fill=255)
        mask = mask.filter(ImageFilter.GaussianBlur(60))
        self.canvas = Image.composite(back, self.canvas, mask)
        return self

    def save(self, path):
        self.canvas.convert("RGB").save(path, optimize=True)


# --- backgrounds ---------------------------------------------------------------------------------
_map = None


def world_map():
    global _map
    if _map is None:
        _map = Image.open(ART / "Plaza/Plaza_WorldMap.jpg").convert("RGB")
    return _map


def map_bg(cx, cy, zoom=1.0):
    """1536x1024 view of the plaza world map (rig-style recolour) centred on map px (cx, cy)."""
    m = world_map()
    w, h = W / zoom, H / zoom
    box = (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2)
    return m.crop(tuple(round(v) for v in box)).resize((W, H), Image.LANCZOS)


def portrait_bg(path, top=.0, zoom=1.0):
    """Landscape crop of a portrait game background (mine, sea): full width, starting `top` (0..1) down."""
    src = img(path).convert("RGB")
    w = src.width / zoom
    h = w * H / W
    y0 = (src.height - h) * top
    x0 = (src.width - w) / 2
    return src.crop((round(x0), round(y0), round(x0 + w), round(y0 + h))).resize((W, H), Image.LANCZOS)


# --- drawn bits (no game picture for these) -----------------------------------------------------
def paper(width, height, inner=None, lines=0, color=PAPER, pin=None, radius=14):
    """A sheet of paper in the rig outline style, optionally showing a picture or scribbled lines."""
    pad = 10
    im = Image.new("RGBA", (width + pad * 2, height + pad * 2))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((pad, pad, pad + width, pad + height), radius, fill=color, outline=OUTLINE, width=7)
    if inner is not None:
        m = 26
        pic = inner.resize((width - m * 2, height - m * 2), Image.LANCZOS).convert("RGBA")
        im.alpha_composite(pic, (pad + m, pad + m))
        d.rectangle((pad + m, pad + m, pad + width - m, pad + height - m), outline=(120, 80, 50, 255), width=4)
    for i in range(lines):
        y = pad + 34 + i * (height - 60) / max(1, lines - 1) if lines > 1 else pad + height / 2
        x1 = pad + width - 30 - (i % 3) * 22
        d.line((pad + 30, y, x1, y), fill=(150, 110, 80, 255), width=6)
    if pin:
        d.ellipse((pad + width / 2 - 14, pad - 4, pad + width / 2 + 14, pad + 24), fill=pin, outline=OUTLINE, width=4)
    return im


def easel(width, height, picture):
    """Wooden easel holding a painted canvas (the picture), drawn with the rig outline."""
    im = Image.new("RGBA", (width, height))
    d = ImageDraw.Draw(im)
    wood, dark = (196, 132, 74, 255), OUTLINE
    leg = max(14, width // 16)
    for x0, x1 in ((width * .22, width * .08), (width * .78, width * .92)):
        d.line((x0, height * .12, x1, height - 6), fill=dark, width=leg + 10)
        d.line((x0, height * .12, x1, height - 6), fill=wood, width=leg)
    d.line((width / 2, height * .05, width / 2, height * .95), fill=dark, width=leg + 8)
    d.line((width / 2, height * .05, width / 2, height * .95), fill=wood, width=leg - 4)
    cw, ch = int(width * .86), int(height * .58)
    canvas = paper(cw, ch, inner=picture, color=(255, 252, 244, 255), radius=6)
    im.alpha_composite(canvas, (int((width - canvas.width) / 2), int(height * .04)))
    shelf_y = int(height * .04 + canvas.height - 4)
    d.rounded_rectangle((width * .1, shelf_y, width * .9, shelf_y + leg + 4), 6, fill=wood, outline=dark, width=6)
    return im


def notebook(width, height, lines=5):
    im = Image.new("RGBA", (width, height))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((6, 6, width - 6, height - 6), 12, fill=(120, 150, 110, 255), outline=OUTLINE, width=7)
    d.rounded_rectangle((width * .5 - 6, 14, width - 16, height - 16), 6, fill=PAPER, outline=OUTLINE, width=5)
    d.rounded_rectangle((16, 14, width * .5 + 6, height - 16), 6, fill=PAPER, outline=OUTLINE, width=5)
    for i in range(lines):
        y = 40 + i * (height - 80) / max(1, lines - 1)
        d.line((34, y, width * .5 - 14, y), fill=(150, 110, 80, 255), width=5)
        d.line((width * .5 + 18, y, width - 34 - (i % 2) * 24, y), fill=(150, 110, 80, 255), width=5)
    return im
