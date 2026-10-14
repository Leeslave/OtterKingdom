"""The rig editor's customisable otter ("꾸미기"), cut from the user's reference pictures.

    python Tools/CustomOtter/build.py      # previews in ArtSource/Otter/CustomOtter/parts

ArtSource/Otter/CustomOtter/reference/:
  base-basic.png      the bare otter (기본형): every body part comes from here
  mane-2..5.png       the same otter with other head fur: only their heads are used
  expressions.png     3x3 sheet of the basic otter's expressions (expressions.py)

Parts are cut along the drawing's own lines (chin, arm, leg and tail outlines; coordinates below are
the base picture's pixels). Where a part hides another (the body under the head and arms, the tail and
leg tops under the body) the hidden side is filled with the surrounding fur so nothing gaps open when
parts move. The back view has no picture: it is the same silhouettes with the face, belly and paw
details painted over with fur (and the ears' insides with their brown).

build() returns the rig for build_rig_editor.py: QuarterRigView-style bones (rig px, 2 sprite px per
rig px) and sprites with joint pivots, one head (plain and with eyes/mouth painted out) per head fur.
"""
import pathlib
import sys

import cv2
import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
REF = ROOT / "ArtSource/Otter/CustomOtter/reference"
PREVIEW = ROOT / "ArtSource/Otter/CustomOtter/parts"
sys.path.insert(0, str(HERE))
import cutout  # noqa: E402
import expressions  # noqa: E402

SPRITE = .6                 # sprite px per reference px (the reference otter is ~1090 px tall -> ~330 rig px)
RIG = SPRITE / 2            # rig px per reference px
GROUND, ROOT_X = 1216, 607  # the base picture's ground line and the point between the feet
LINE = 15                   # outline width, reference px

FUR = np.array((198, 140, 111), np.float32)
CREAM = np.array((250, 241, 228), np.float32)
PAW = np.array((133, 81, 60), np.float32)
OUTLINE = np.array((13, 1, 0), np.float32)
# For the fur tint: fur and paw tones (tinted) against everything else (kept).
FUR_TONES = [(200, 143, 114), (193, 134, 105), (133, 81, 60), (165, 110, 85), (110, 70, 52)]
KEPT_TONES = [(13, 1, 0), (49, 27, 19), (250, 241, 228), (252, 159, 147), (227, 197, 180), (240, 225, 210)]
FARMER = ROOT / "Assets/Resources/QuarterRigV14"

# Head fur pictures: id, label, file.
MANES = [("basic", "기본형", "base-basic"), ("messy", "헝클어진", "mane-2"), ("round", "둥근 풍성", "mane-3"),
         ("spiky", "뾰족 갈기", "mane-4"), ("swept", "옆 넘김", "mane-5")]

# Where the head meets the body (x, y), left to right: the chin's outline strokes at the sides and,
# between them, the muzzle's lower edge (the drawing has no line there). The head is everything above.
CHIN = [(0, 712), (340, 714), (360, 722), (400, 728), (450, 733), (510, 737), (560, 731), (600, 731),
        (650, 727), (700, 720), (760, 711), (800, 716), (820, 708), (850, 690), (900, 650), (1300, 650)]
# Outer edge of the body's left outline (y, x): the tail and the near arm's back are left of it.
BODY_LEFT = [(700, 330), (840, 328), (880, 330), (920, 340), (960, 355), (1000, 362), (1040, 372),
             (1080, 392), (1110, 400), (1130, 405), (1300, 405)]
BODY_BOTTOM = 1112          # the body stops here; the legs (behind it) start higher, at LEG_TOP
LEG_TOP, LEG_SPLIT = 1050, 607
NEAR_ARM = dict(x=(300, 506), y=(740, 1000))       # left of the arm's inner line, down to the paw
FAR_ARM = dict(x=(818, 1000), y=(770, 965), outside=872)   # right of its inner line; paw past the body's edge
JOINTS = {"hips": (607, 1080), "body": (615, 1040), "head": (625, 728), "nearArm": (440, 790),
          "farArm": (845, 800), "tail": (405, 1045)}
SOLES = {"nearLeg": (500, 1214), "farLeg": (712, 1212)}


def interp(points, at):
    xs, ys = zip(*points)
    return np.interp(at, xs, ys)


def load(name):
    return cutout.cutout(np.array(Image.open(REF / f"{name}.png").convert("RGB")))


def luma(rgb):
    return rgb[..., 0] * .3 + rgb[..., 1] * .59 + rgb[..., 2] * .11


def grid(shape):
    return np.mgrid[0:shape[0], 0:shape[1]]


def masked(rgba, mask):
    out = rgba.copy()
    out[..., 3] = (out[..., 3] * mask.astype(np.float32)).astype(np.uint8)
    return out


def paint(rgba, where, colour, soft=1.2):
    """Paint `where` with a flat colour (soft edged)."""
    w = cv2.GaussianBlur(where.astype(np.float32), (0, 0), soft)[..., None]
    out = rgba.copy()
    out[..., :3] = np.clip(rgba[..., :3] * (1 - w) + colour * w, 0, 255).astype(np.uint8)
    return out


def fill_fur(rgba, region, colour=FUR):
    """Shapes drawn inside `region` (lines, paw) become plain fur; the region's outline stays."""
    return paint(rgba, region & (rgba[..., 3] > 0), colour)


def ring(mask, width):
    """The mask's pixels within `width` px of its edge."""
    m = mask.astype(np.uint8)
    return mask & ~cv2.erode(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * width + 1, 2 * width + 1))).astype(bool)


# ---- body parts (base picture) ----
def body_parts(base):
    h, w = base.shape[:2]
    ys, xs = grid((h, w))
    otter = base[..., 3] > 0
    above_chin = ys <= interp(CHIN, xs)
    left_of_body = xs < interp(BODY_LEFT, ys)
    tail = otter & left_of_body & (ys > 880)
    parts = {}

    # Body: below the chin, above BODY_BOTTOM, not the tail or the far paw sticking out past its edge.
    far_out = (xs > FAR_ARM["outside"]) & (ys > FAR_ARM["y"][0]) & (ys < FAR_ARM["y"][1])
    body = otter & ~above_chin & (ys < BODY_BOTTOM) & ~tail & ~far_out
    image = masked(base, body)
    # The near arm drawn on it (inner line, paw) goes: fur, keeping the body's own outline at the left.
    arm_area = (xs >= interp(BODY_LEFT, ys) + LINE + 2) & (xs < NEAR_ARM["x"][1] + 6) & \
               (ys > NEAR_ARM["y"][0]) & (ys < NEAR_ARM["y"][1] + 4)
    darkish = luma(base[..., :3].astype(np.float32)) < 150
    image = fill_fur(image, arm_area & darkish)
    # ... and the far arm's inner line.
    image = fill_fur(image, (xs > FAR_ARM["x"][0] - 4) & (xs < 848) & (ys > FAR_ARM["y"][0]) & (ys < 935) & darkish)
    # Under the head: the torso goes on up (fur, its side outlines continued) so a nodding head opens no gap.
    cap = np.zeros((h, w), np.uint8)
    cv2.ellipse(cap, (612, 745), (262, 95), 0, 180, 360, 1, -1)
    cap = cap.astype(bool) & above_chin
    capped = image.copy()
    capped[cap] = np.concatenate([FUR, [255]]).astype(np.uint8)
    edge = np.zeros((h, w), np.uint8)
    cv2.ellipse(edge, (612, 745), (262, 95), 0, 180, 360, 1, LINE)
    capped[edge.astype(bool) & cap] = np.concatenate([OUTLINE, [255]]).astype(np.uint8)
    parts["body"] = capped

    # Arms: copies of their area (drawn over the body, which no longer has them).
    near = otter & (xs >= NEAR_ARM["x"][0]) & (xs < NEAR_ARM["x"][1]) & (ys >= NEAR_ARM["y"][0]) & \
           (ys < NEAR_ARM["y"][1]) & ~(left_of_body & (ys > 940))
    parts["nearArm"] = masked(base, near)
    far = otter & (xs >= FAR_ARM["x"][0]) & (ys >= FAR_ARM["y"][0]) & (ys < FAR_ARM["y"][1])
    parts["farArm"] = masked(base, far)

    # Legs: from LEG_TOP (hidden under the body) to the feet, split between them.
    legs = otter & (ys >= LEG_TOP) & ~tail
    parts["nearLeg"] = masked(base, legs & (xs < LEG_SPLIT))
    parts["farLeg"] = masked(base, legs & (xs >= LEG_SPLIT))

    # Tail: left of the body, running on under it as fur.
    tail_img = masked(base, tail)
    under = (xs >= interp(BODY_LEFT, ys)) & (xs < interp(BODY_LEFT, ys) + 45) & (ys > 985) & (ys < 1100)
    tail_img[under] = np.concatenate([FUR, [255]]).astype(np.uint8)
    parts["tail"] = tail_img
    return parts


def not_outline(rgba):
    """Opaque pixels that are not part of the silhouette's outline (dark pixels near the edge)."""
    dark = luma(rgba[..., :3].astype(np.float32)) < 90
    return (rgba[..., 3] > 200) & ~(ring(rgba[..., 3] > 128, LINE + 6) & dark)


def rear_limb(front):
    """An arm or leg seen from behind: the toe lines inside the paw go (filled from around them)."""
    dark = (luma(front[..., :3].astype(np.float32)) < 90) & not_outline(front)
    mask = cv2.dilate(dark.astype(np.uint8), np.ones((5, 5), np.uint8))
    out = front.copy()
    out[..., :3] = cv2.inpaint(np.ascontiguousarray(front[..., :3]), mask, 6, cv2.INPAINT_TELEA)
    out[..., 3] = front[..., 3]
    return out


def rear_body(front):
    """The back: belly and anything else drawn inside the outline becomes fur."""
    return fill_fur(front, not_outline(front))


# ---- heads ----
def features(picture, scale=1.0):
    """Eye boxes (near, far), nose and mouth of an otter picture or head: the eyes are solid upright
    ovals, the nose a solid wide one, the mouth the thin line(s) under the nose."""
    rgb = picture[..., :3].astype(np.float32)
    dark = ((luma(rgb) < 70) & (picture[..., 3] > 200)).astype(np.uint8)
    n, labels, stats, cent = cv2.connectedComponentsWithStats(dark, 8)
    solid = lambda i: stats[i, 4] / (stats[i, 2] * stats[i, 3])
    eyes = [i for i in range(1, n) if stats[i, 4] > 1500 * scale ** 2 and solid(i) > .65 and 1.15 < stats[i, 3] / stats[i, 2] < 2.2]
    eyes = sorted(sorted(eyes, key=lambda i: -stats[i, 4])[:2], key=lambda i: stats[i, 0])
    assert len(eyes) == 2, "eyes not found"
    left, right = stats[eyes[0], 0], stats[eyes[1], 0] + stats[eyes[1], 2]
    noses = [i for i in range(1, n) if stats[i, 4] > 600 * scale ** 2 and solid(i) > .6 and stats[i, 2] / stats[i, 3] > 1.3
             and left < cent[i][0] < right and cent[i][1] > min(cent[e][1] for e in eyes)]
    nose = max(noses, key=lambda i: stats[i, 4])
    mouth = [i for i in range(1, n) if i not in eyes and i != nose and stats[i, 4] > 200 * scale ** 2 and solid(i) < .5
             and stats[nose, 1] + stats[nose, 3] - 5 < stats[i, 1] < stats[nose, 1] + 4 * stats[nose, 3]
             and abs(cent[i][0] - cent[nose][0]) < 3 * stats[nose, 2]]
    return {"eyes": [cent[i] for i in eyes], "eye_boxes": [stats[i] for i in eyes], "nose": cent[nose],
            "eye_mask": np.isin(labels, eyes), "mouth_mask": np.isin(labels, mouth), "nose_mask": labels == nose}


def align(image, src_pts, dst_pts, shape):
    """Similarity transform taking src points onto dst points, applied to an RGBA image."""
    m, _ = cv2.estimateAffinePartial2D(np.float32(src_pts), np.float32(dst_pts))
    return cv2.warpAffine(image, m, (shape[1], shape[0]), flags=cv2.INTER_CUBIC, borderValue=(0, 0, 0, 0))


def cut_head(picture):
    h, w = picture.shape[:2]
    ys, xs = grid((h, w))
    return masked(picture, ys <= interp(CHIN, xs))


def blank(head, f):
    """Eyes, nose and mouth painted out: an expression is drawn on top instead."""
    rgb = head[..., :3].astype(np.float32)
    opaque = head[..., 3] > 200
    cream = (np.linalg.norm(rgb - CREAM, axis=2) < 40) & opaque
    # The mouth and nose go (an expression brings both, so they always match).
    out = paint(head, cv2.dilate((f["mouth_mask"] | f["nose_mask"]).astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool), CREAM)
    # Each eye: an oval just bigger than it, cream where the muzzle outside it is nearest, fur elsewhere.
    for x, y, bw, bh, _ in f["eye_boxes"]:
        one = np.zeros(cream.shape, np.uint8)
        cv2.ellipse(one, (int(x + bw / 2), int(y + bh / 2)), (int(bw / 2 + 5), int(bh / 2 + 5)), 0, 0, 360, 1, -1)
        inside = one.astype(bool)
        _, nearest = cv2.distanceTransformWithLabels(one, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
        lut = np.zeros(nearest.max() + 1, np.float32)
        lut[nearest[~inside]] = cream[~inside]
        side = cv2.GaussianBlur(np.where(inside, lut[nearest], cream.astype(np.float32)), (0, 0), 4)
        below = np.clip((side - .5) * 4 + .5, 0, 1)[..., None]
        soft = cv2.GaussianBlur(inside.astype(np.float32), (0, 0), 1.5)[..., None]
        colour = below * CREAM + (1 - below) * FUR
        out[..., :3] = np.clip(out[..., :3] * (1 - soft) + colour * soft, 0, 255).astype(np.uint8)
    return out


def rear_head(head):
    """The back of the head: the silhouette and ears; inside, fur (the ears' insides turn brown)."""
    rgb = head[..., :3].astype(np.float32)
    opaque = head[..., 3] > 128
    inner = not_outline(head)
    brown = (np.linalg.norm(rgb - PAW, axis=2) < 45) & (head[..., 3] > 200)
    n, labels, stats, _ = cv2.connectedComponentsWithStats(cv2.dilate(brown.astype(np.uint8), np.ones((9, 9), np.uint8)), 8)
    ears = np.zeros(opaque.shape, bool)
    # The ears: the two biggest brown patches in the top half (shading in the fur can be brownish too).
    top = [i for i in range(1, n) if stats[i, 1] + stats[i, 3] / 2 < head.shape[0] * .5]
    for i in sorted(top, key=lambda i: -stats[i, 4])[:2]:
        contours, _ = cv2.findContours((labels == i).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        filled = np.zeros(opaque.shape, np.uint8)
        cv2.drawContours(filled, contours, -1, 1, -1)
        ears |= filled.astype(bool)
    out = fill_fur(head, inner & ~ears)
    ear_inner = ears & inner & (np.linalg.norm(rgb - CREAM, axis=2) < 50)
    return paint(out, ear_inner, PAW)


def heads(base):
    """{mane id: (front head, blank front head, rear head, features)} all in the base picture's frame."""
    target = features(cut_head(base))
    out = {}
    for mane, _, name in MANES:
        picture = base if name == "base-basic" else load(name)
        if picture is not base:
            src = features(picture)["eyes"]
            picture = align(picture, src, target["eyes"], base.shape)
        head = cut_head(picture)
        f = features(head)
        out[mane] = (head, blank(head, f), rear_head(head), f)
    return out


# ---- export ----
def crop(rgba, joint):
    """Trimmed sprite at SPRITE scale and its pivot (Unity's: from the bottom left, 0..1)."""
    a = rgba[..., 3]
    x0, y0, w, h = cv2.boundingRect((a > 0).astype(np.uint8))
    x0, y0 = x0 - 4, y0 - 4
    w, h = w + 8, h + 8
    piece = np.zeros((h, w, 4), np.uint8)
    sx0, sy0 = max(x0, 0), max(y0, 0)
    sx1, sy1 = min(x0 + w, rgba.shape[1]), min(y0 + h, rgba.shape[0])
    piece[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = rgba[sy0:sy1, sx0:sx1]
    # Premultiplied resize keeps edges clean.
    pm = piece.astype(np.float32)
    pm[..., :3] *= pm[..., 3:] / 255
    size = (max(1, round(w * SPRITE)), max(1, round(h * SPRITE)))
    pm = cv2.resize(pm, size, interpolation=cv2.INTER_AREA)
    alpha = np.clip(pm[..., 3:], 1e-3, 255)
    pm[..., :3] = np.where(pm[..., 3:] > 0, pm[..., :3] * 255 / alpha, 0)
    out = np.clip(pm, 0, 255).astype(np.uint8)
    pivot = ((joint[0] - x0) / w, 1 - (joint[1] - y0) / h)
    return out, (round(pivot[0], 5), round(pivot[1], 5))


def rig_px(p):
    return [round((p[0] - ROOT_X) * RIG, 2), round((p[1] - GROUND) * RIG, 2)]


def rel(child, parent):
    a, b = rig_px(child), rig_px(parent)
    return [round(a[0] - b[0], 2), round(a[1] - b[1], 2)]


def tint_weight(rgba):
    """0..1 per pixel (as 0..255): how much it is fur or paw, soft between the nearest tones."""
    rgb = rgba[..., :3].astype(np.float32)
    dist = lambda tones: np.min(np.stack([np.linalg.norm(rgb - np.array(t, np.float32), axis=2) for t in tones]), axis=0)
    d_fur, d_kept = dist(FUR_TONES), dist(KEPT_TONES)
    w = np.clip((d_kept / np.maximum(d_fur + d_kept, 1e-3) - .4) / .2, 0, 1)
    w = w * w * (3 - 2 * w)
    w[rgba[..., 3] == 0] = 0
    return (w * 255).astype(np.uint8)


def farmer_sprite(name):
    """The farmer's backpack (the 가방 accessory): his sprite and pivot as they are."""
    import re
    png = FARMER / f"{name}.png"
    m = re.search(r"spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", (FARMER / f"{name}.png.meta").read_text(encoding="utf-8"))
    return np.array(Image.open(png).convert("RGBA")), (float(m.group(1)), float(m.group(2)))


def rig_json():
    J, S = JOINTS, SOLES
    bone = lambda name, parent, pos, layer: {"name": name, "parent": parent, "pos": pos, "part": name, "layer": layer}
    front = [
        {"name": "hips", "parent": None, "pos": rig_px(J["hips"])},
        bone("body", "hips", rel(J["body"], J["hips"]), 4),
        bone("tail", "hips", rel(J["tail"], J["hips"]), 0),
        bone("farLeg", "hips", rel(S["farLeg"], J["hips"]), 2),
        bone("nearLeg", "hips", rel(S["nearLeg"], J["hips"]), 3),
        bone("farArm", "body", rel(J["farArm"], J["body"]), 5),
        bone("nearArm", "body", rel(J["nearArm"], J["body"]), 6),
        bone("head", "body", rel(J["head"], J["body"]), 8),
        # The farmer's backpack, on the back at the shoulders (behind the body; in front of it seen from behind).
        {"name": "backpack", "parent": "body", "pos": [-68, -58], "part": "backpack", "layer": 1},
    ]
    # The back: same layout, the tail in front of the body.
    rear = [dict(b) for b in front]
    for b in rear:
        if b["name"] == "backpack":
            b["pos"] = [-6, -50]
    for b in rear:
        b["layer"] = {"tail": 7, "farLeg": 0, "nearLeg": 1, "farArm": 2, "body": 3, "nearArm": 5, "head": 8, "backpack": 6}.get(b["name"], b.get("layer"))
    legs = {"type": "rigid straight leg, sole-anchored", "stanceFraction": .6, "cycleSeconds": .84, "stepHalfWidth": 10,
            "lift": 11, "nearX": rig_px(S["nearLeg"])[0], "farX": rig_px(S["farLeg"])[0], "swingAngle": .055}
    return {"legSolver": legs, "bones": front, "rearBones": rear}


def head_anchor(head):
    """Crown (top of the head, not the ears) and half width at the eyes, in the head's rig px."""
    a = head[..., 3] > 128
    f = features(head)
    eye_y = int((f["eyes"][0][1] + f["eyes"][1][1]) / 2)
    xs = np.where(a[eye_y])[0]
    cx = (xs.min() + xs.max()) / 2
    col = np.where(a[:, int(cx)])[0]
    joint = JOINTS["head"]
    to = lambda x, y: [round((x - joint[0]) * RIG, 2), round((y - joint[1]) * RIG, 2)]
    return {"crown": to(cx, col.min() + LINE), "half": round((xs.max() - xs.min()) / 2 * RIG, 2), "base": to(cx, eye_y)[1]}


def build():
    """(sprites {name: (RGBA, pivot, tint weight or None)}, rig json, extra data for the page)."""
    base = load("base-basic")
    parts = body_parts(base)
    sprites = {}
    for name, image in parts.items():
        joint = SOLES[name] if name in SOLES else JOINTS[name]
        sprites[f"front-{name}"] = crop(image, joint)
        back = rear_body(image) if name == "body" else rear_limb(image) if name != "tail" else image
        sprites[f"rear-{name}"] = crop(back, joint)
    all_heads = heads(base)
    for mane, (head, empty, back, f) in all_heads.items():
        sprites[f"front-head-{mane}"] = crop(head, JOINTS["head"])
        sprites[f"front-head-{mane}-blank"] = crop(empty, JOINTS["head"])
        sprites[f"rear-head-{mane}"] = crop(back, JOINTS["head"])
    # Tint weights for everything so far (fur and paws); faces and the backpack are not tinted.
    sprites = {name: (image, pivot, tint_weight(image)) for name, (image, pivot) in sprites.items()}
    head, empty, _, f = all_heads["basic"]
    for name, overlay in expressions.overlays(base, empty, head[..., 3] > 0, f, f["eyes"]).items():
        sprites[f"face-{name}"] = (*crop(overlay, JOINTS["head"]), None)
    for view in ("front", "rear"):
        sprites[f"{view}-backpack"] = (*farmer_sprite(f"{view}-backpack"), None)
    anchor = head_anchor(head)
    data = {"manes": [[m, label] for m, label, _ in MANES], "front": anchor, "rear": anchor,
            "fur": [int(v) for v in FUR], "furLum": round(float(luma(FUR)), 2)}
    return sprites, rig_json(), data


def main():
    sprites, rig, data = build()
    PREVIEW.mkdir(parents=True, exist_ok=True)
    for name, (image, pivot, _) in sprites.items():
        Image.fromarray(image).save(PREVIEW / f"{name}.png")
    print(len(sprites), "sprites;", rig["legSolver"]["nearX"], rig["legSolver"]["farX"])
    print(data)


if __name__ == "__main__":
    main()
