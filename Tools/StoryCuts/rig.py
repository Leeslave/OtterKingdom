"""Draw a QuarterRigView otter as one still picture, straight from its Resources/<Rig> parts and rig.json.

The pose math follows QuarterRigView.cs (rig px, y down, angles in radians clockwise on screen):
joints chain hips -> body -> arms/head, tail on the hips, legs stand on the ground (feet = origin).
Only still poses are needed for story cuts: standing, arm angles, sitting, sleep/smile head, tool arms.
"""
import json
import math
import pathlib
import re

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets/Resources"
RIG_PPU = 100.0          # QuarterRigView.RigPixelsPerUnit
FAR_LEG_REST_Y = 4.0
SIT_HIPS_DROP = 30.0
SIT_NEAR_HIP, SIT_FAR_HIP = (-31.4, 23.9), (17.2, 26.3)   # Unity y up
SIT_NEAR_ANGLE, SIT_FAR_ANGLE = 75.5, 80.5
SIT_LEG_LENGTH, SIT_LEG_FORESHORTEN = 72.0, .95

_cache = {}


def _meta(path):
    text = pathlib.Path(str(path) + ".meta").read_text(encoding="utf-8")
    ppu = float(re.search(r"spritePixelsToUnits: ([\d.]+)", text).group(1))
    align = int(re.search(r"\n  alignment: (\d+)", text).group(1))
    m = re.search(r"spritePivot: \{x: ([\d.\-e]+), y: ([\d.\-e]+)\}", text)
    pivot = (float(m.group(1)), float(m.group(2))) if align == 9 else (.5, .5)
    return ppu, pivot


def part(rig, name):
    """(image, pivot in image px (y down), rig px per image px) or None."""
    key = (rig, name)
    if key not in _cache:
        path = RESOURCES / rig / f"{name}.png"
        if not path.exists():
            _cache[key] = None
        else:
            image = Image.open(path).convert("RGBA")
            ppu, (px, py) = _meta(path)
            _cache[key] = (image, (px * image.width, (1 - py) * image.height), RIG_PPU / ppu)
    return _cache[key]


def load_rig(rig):
    data = json.loads((RESOURCES / rig / "rig.json").read_text(encoding="utf-8"))
    tuning = {}
    tpath = RESOURCES / rig / "tuning.json"
    if tpath.exists():
        for t in json.loads(tpath.read_text(encoding="utf-8")).get("front", []):
            tuning[t["part"]] = t
    return data, tuning


def _rot(x, y, a):
    c, s = math.cos(a), math.sin(a)
    return x * c - y * s, x * s + y * c


def paste(canvas, image, pivot, at, angle=0.0, scale=1.0, sx=1.0, sy=1.0):
    """Draw image with its pivot (image px) at canvas point `at`, turned clockwise by angle, scaled."""
    kx, ky = scale * sx, scale * sy
    c, s = math.cos(angle), math.sin(angle)
    dx, dy = at
    # out = at + R * diag(kx, ky) * (in - pivot)  ->  in = pivot + diag(1/kx, 1/ky) * R^-1 * (out - at)
    a, b = c / kx, s / kx
    d, e = -s / ky, c / ky
    coeffs = (a, b, pivot[0] - a * dx - b * dy, d, e, pivot[1] - d * dx - e * dy)
    layer = image.transform(canvas.size, Image.AFFINE, coeffs, resample=Image.BICUBIC)
    canvas.alpha_composite(layer)


def render(rig, scale=2.0, pose=None):
    """Return (RGBA picture, feet point in it). pose keys (all optional):
    head: 'awake' | 'sleep' | 'smile';  sit: 0..1;  droop: 0..1 (sleepy nod);
    nearArm / farArm / head_angle / body / tail: extra clockwise radians;  hips: extra drop (rig px);
    nearTool / farTool: tool-arm tag (front-nearArm-<tag>.png);  armOverHead: draw near arm above the head;
    held: [dict(img, arm='nearArm', height=rig px, angle=deg, dx, dy, behind=False)];  flip: mirror (face left)."""
    pose = pose or {}
    data, tuning = load_rig(rig)
    legs = data["legSolver"]
    sit = pose.get("sit", 0.0)
    droop = pose.get("droop", 0.0)
    W, H = int(520 * scale), int(560 * scale)
    origin = (W / 2, H - 80 * scale)
    canvas = Image.new("RGBA", (W, H))

    joints = {}   # name -> (x, y, angle) in rig px
    draws = []    # (layer, order, fn)
    bones = data["bones"]
    for order, b in enumerate(bones):
        name = b["name"]
        t = tuning.get(name, {})
        if name in ("nearLeg", "farLeg"):
            near = name == "nearLeg"
            stand = ((legs["nearX"] if near else legs["farX"]), 0.0 if near else FAR_LEG_REST_Y)
            deg = SIT_NEAR_ANGLE if near else SIT_FAR_ANGLE
            hip = SIT_NEAR_HIP if near else SIT_FAR_HIP
            foot = (hip[0] + math.sin(math.radians(deg)) * SIT_LEG_LENGTH * SIT_LEG_FORESHORTEN,
                    hip[1] - math.cos(math.radians(deg)) * SIT_LEG_LENGTH * SIT_LEG_FORESHORTEN)
            ux = stand[0] + (foot[0] - stand[0]) * sit + t.get("x", 0)
            uy = stand[1] + (foot[1] - stand[1]) * sit - t.get("y", 0)
            rot_ccw = deg * sit - t.get("rotation", 0)
            joints[name] = (ux, -uy, -math.radians(rot_ccw))
            leg_sy = 1 + (SIT_LEG_FORESHORTEN - 1) * sit
        else:
            x, y = b["pos"]
            angle = 0.0
            if name == "hips":
                y += 8 + SIT_HIPS_DROP * sit + pose.get("hips", 0)
            elif name == "head":
                y += 10 * droop
                angle += .28 * droop + pose.get("head_angle", 0)
            elif name == "nearArm":
                angle += -.4 * sit + pose.get("nearArm", 0)
            elif name == "farArm":
                angle += .18 * sit + pose.get("farArm", 0)
            elif name == "tail":
                angle += .22 * sit + pose.get("tail", 0)
            elif name == "body":
                angle += .035 * droop + pose.get("body", 0)
            x += t.get("x", 0)
            y += t.get("y", 0)
            angle += math.radians(t.get("rotation", 0))
            if b.get("parent"):
                px, py, pa = joints[b["parent"]]
                ox, oy = _rot(x, y, pa)
                joints[name] = (px + ox, py + oy, pa + angle)
            else:
                joints[name] = (x, y, angle)
            leg_sy = 1.0
        if name == "hips":
            continue
        sprite_name = f"front-{name}"
        if name == "head" and pose.get("head", "awake") != "awake":
            alt = f"front-head-{pose['head']}"
            if part(rig, alt):
                sprite_name = alt
        tool = pose.get(name.replace("Arm", "Tool")) if name.endswith("Arm") else None
        if tool and part(rig, f"front-{name}-{tool}"):
            sprite_name = f"front-{name}-{tool}"
        p = part(rig, sprite_name)
        if p is None:
            continue
        layer = b.get("layer", 0)
        if name == "nearArm" and pose.get("armOverHead"):
            layer = next(bb["layer"] for bb in bones if bb["name"] == "head") + 1
        jx, jy, ja = joints[name]
        sx, sy = t.get("scaleX", 1), t.get("scaleY", 1) * leg_sy
        draws.append((layer, order, p, (jx, jy, ja), sx, sy))

    # Held things ride on an arm's hand: QuarterRigView.handReach = pivot-to-bottom * .82
    for h in pose.get("held", []):
        arm = h.get("arm", "nearArm")
        ap = part(rig, f"front-{arm}")
        jx, jy, ja = joints[arm]
        reach = (ap[0].height - ap[1][1]) * ap[2] * .82
        hx, hy = _rot(h.get("dx", 0), reach + h.get("dy", 0), ja)
        if "at" in h:   # fixed spot from the feet instead of the hand (e.g. carried over the head)
            jx, jy, ja, hx, hy = h["at"][0], h["at"][1], 0.0, 0.0, 0.0
        img = h["img"]
        k = h["height"] / img.height
        layer = h.get("layer", 20 if not h.get("behind") else -1)
        draws.append((layer, 100, (img, (img.width * h.get("px", .5), img.height * h.get("py", .5)), k),
                      (jx + hx, jy + hy, math.radians(h.get("angle", 0)) + (ja if h.get("follow") else 0)), 1, 1))

    for layer, order, p, (jx, jy, ja), sx, sy in sorted(draws, key=lambda d: (d[0], d[1])):
        image, pivot, k = p
        at = (origin[0] + jx * scale, origin[1] + jy * scale)
        paste(canvas, image, pivot, at, ja, k * scale, sx, sy)

    feet = origin
    if pose.get("flip"):
        canvas = canvas.transpose(Image.FLIP_LEFT_RIGHT)
        feet = (W - origin[0], origin[1])
    box = canvas.getbbox()
    if box:
        canvas = canvas.crop(box)
        feet = (feet[0] - box[0], feet[1] - box[1])
    return canvas, feet
