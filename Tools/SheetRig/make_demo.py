"""Renders the rig demo video (1280x720, 30 fps, H.264) from the cut parts.

Usage: python make_demo.py [out.mp4]          (default ArtSource/Otter/SheetRig/otter_rig_demo.mp4)
       python make_demo.py x <section>        (6 check frames of one section as PNGs)
Needs: pip install imageio-ffmpeg (bundled ffmpeg).
"""
import math
import os
import subprocess
import sys
import colorsys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import imageio_ffmpeg

import rig
import anim
import cut_parts as cp

W, H, FPS = 1280, 720, 30
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'SheetRig', 'otter_rig_demo.mp4')
ONLY = sys.argv[2] if len(sys.argv) > 2 else None   # render one section to png frames for checking

FONT = 'C:/Windows/Fonts/malgun.ttf'
FONT_B = 'C:/Windows/Fonts/malgunbd.ttf'
f_title = ImageFont.truetype(FONT_B, 44)
f_sub = ImageFont.truetype(FONT, 24)
f_lab = ImageFont.truetype(FONT_B, 22)
f_small = ImageFont.truetype(FONT, 17)

BG = (246, 241, 228)
INK = (70, 45, 35)
ACCENT = (196, 120, 40)

views = rig.build_views()
RIGS = {v: rig.Rig(*views[v]) for v in views}
ROOT = {v: RIGS[v].bones['Root'].pivot for v in RIGS}

KOR = {'Head': '머리', 'Body': '몸통', 'Arm_L': '팔', 'Arm_R': '팔', 'Leg_L': '다리', 'Leg_R': '다리',
       'Tail': '꼬리', 'Arm_Near': '앞팔', 'Arm_Far': '뒷팔', 'Leg_Near': '앞다리', 'Leg_Far': '뒷다리',
       'Foot_Near': '앞발', 'Foot_Far': '뒷발'}
VIEW_KOR = {'Front': '정면 (아래)', 'Side': '측면 (오른쪽)', 'Left': '측면 (왼쪽 = 반전)', 'Back': '뒷모습 (위)'}


def ease(x):
    x = min(1, max(0, x))
    return x * x * (3 - 2 * x)


def blit(frame, prem, alpha=1.0):
    """Composite a premultiplied float canvas onto an RGB float frame."""
    a = prem[..., 3:4] / 255 * alpha
    frame[:] = prem[..., :3] * alpha + frame * (1 - a)


def text(d, xy, s, font, fill=INK, anchor='la'):
    d.text(xy, s, font=font, fill=fill, anchor=anchor)


def header(img, title, sub=None):
    d = ImageDraw.Draw(img)
    text(d, (48, 34), title, f_title)
    if sub:
        text(d, (50, 92), sub, f_sub, fill=(120, 95, 80))


def shadow(frame, cx, cy, rx, ry, a=0.16):
    yy, xx = np.ogrid[:H, :W]
    m = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    s = np.clip(1.3 - m, 0, 1) * a
    frame *= (1 - s[..., None] * 0.6)


def render_char(view, pose, origin, k, rg=None):
    rg = rg or RIGS['Side' if view == 'Left' else view]
    return rg.render(pose, (W, H), origin, k=k, flip=(view == 'Left'))


def pose_for(view, kind, t):
    v = 'Side' if view == 'Left' else view
    return anim.walk(v, t) if kind == 'walk' else anim.idle(v, t)


def to_frame(img):
    return np.asarray(img.convert('RGB')).astype(np.float32)


def from_frame(fr):
    return Image.fromarray(fr.clip(0, 255).astype(np.uint8))


# --- sections ------------------------------------------------------------------------

POS3 = {'Front': 250, 'Side': 640, 'Back': 1030}


def sec_intro(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '농부 해달 리깅 시연 (3차)', '외곽선을 둥글게 다시 따고, 측면 걷기를 발 구르기(뒤꿈치→발끝) 보행으로 개선')
    d = ImageDraw.Draw(img)
    for i, (v, spec) in enumerate(cp.VIEWS.items()):
        a = ease((t - 0.3 - 0.25 * i) / 0.6)
        cell = Image.fromarray(cp.load_cell(*spec['frame']).astype(np.uint8))
        cell = cell.crop((200, 0, 632, 480)).resize((324, 360), Image.LANCZOS)
        x = POS3[v] - 162
        y = 190 + int((1 - a) * 30)
        if a > 0:
            cell.putalpha(cell.getchannel('A').point(lambda p: int(p * a)))
            img.paste(cell, (x, y), cell)
            text(d, (POS3[v], 575), VIEW_KOR[v].split(' ')[0], f_lab, anchor='ma')
    text(d, (W // 2, 640), '원본: FarmerOtter_Sheet.png  (정면 7번째 · 측면 8번째 · 뒷모습 1번째 프레임)', f_small,
         fill=(140, 115, 100), anchor='ma')
    return to_frame(img)


EXPLODE_DIR = {
    'Head': (0, -1), 'Body': (0, 0), 'Tail': (-1, 0.15),
    'Arm_L': (-1, 0), 'Arm_R': (1, 0), 'Leg_L': (-0.5, 1), 'Leg_R': (0.5, 1),
    'Arm_Near': (0.8, 0.3), 'Arm_Far': (1, -0.4), 'Leg_Near': (0.6, 0.7), 'Leg_Far': (-0.6, 0.7),
    'Foot_Near': (0.1, 0.5), 'Foot_Far': (-0.1, 0.5),
}


def explode_pose(view, amt):
    pose = {}
    for name, (dx, dy) in EXPLODE_DIR.items():
        if name in RIGS[view].bones:
            pose[name] = dict(dx=dx * amt, dy=dy * amt)
    if view == 'Back':
        pose['Tail'] = dict(dx=0, dy=amt * 0.9)
    # children of the body are pushed relative to it; undo the body's own push for legs (root children)
    return pose


def sec_parts(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '1. 파츠 분리', '가려진 부분은 새로 칠해서 채우고, 실루엣을 둥글게 다듬어 외곽선을 같은 굵기로 다시 땀')
    fr = to_frame(img)
    # exploded -> hold -> assemble
    amt = 34 * (1 - ease((t - 3.2) / 1.6)) * ease(t / 0.8)
    k = 0.62
    labels = []
    for v in ('Front', 'Side', 'Back'):
        origin = (POS3[v], 560)
        pose = explode_pose(v, amt)
        blit(fr, render_char(v, pose, origin, k))
        if amt > 20:
            js = RIGS[v].joints(pose)
            root = ROOT[v]
            for name, b in RIGS[v].bones.items():
                if not b.part:
                    continue
                spr = RIGS[v].sprites[name]
                m = RIGS[v].world(pose)[name][0]
                cx = spr.x0 + spr.img.shape[1] / 2
                cy = spr.y0 + spr.img.shape[0] / 2
                wx, wy = (m @ [cx, cy, 1])[:2]
                labels.append(((wx - root[0]) * k + origin[0], (wy - root[1]) * k + origin[1], KOR[name], (amt - 20) / 14))
    img = from_frame(fr)
    d = ImageDraw.Draw(img, 'RGBA')
    for x, y, s, a in labels:
        a = int(255 * min(1, a))
        bb = d.textbbox((x, y), s, font=f_small, anchor='mm')
        d.rounded_rectangle([bb[0] - 6, bb[1] - 3, bb[2] + 6, bb[3] + 3], 6, fill=(255, 255, 255, int(a * 0.85)))
        d.text((x, y), s, font=f_small, fill=(*INK, a), anchor='mm')
    for v in ('Front', 'Side', 'Back'):
        text(d, (POS3[v], 610), VIEW_KOR[v].split(' ')[0], f_lab, anchor='ma')
    return to_frame(img)


def draw_bones(img, view, pose, origin, k, alpha):
    if alpha <= 0:
        return
    rg = RIGS[view]
    d = ImageDraw.Draw(img, 'RGBA')
    mats = rg.world(pose)
    root = ROOT[view]
    conv = lambda p: ((p[0] - root[0]) * k + origin[0], (p[1] - root[1]) * k + origin[1])
    pts = {}
    for name, b in rg.bones.items():
        pts[name] = conv((mats[name][0] @ [*b.pivot, 1])[:2])
    a = int(255 * alpha)
    # bone = line from the joint toward the part's far end
    for name, b in rg.bones.items():
        if not b.parent:
            continue
        if not b.part:
            d.line([pts[b.parent], pts[name]], fill=(255, 255, 255, a // 2), width=2)
            continue
        chain = [pts[name]]
        if b.chain:
            spr = rg.sprites[name]
            j = b.pivot.copy()
            for i in range(b.chain[1]):
                j = j + spr.seg
                chain.append(conv((mats[name][min(i, len(mats[name]) - 1)] @ [*j, 1])[:2]))
        else:
            spr = rg.sprites[name]
            cx, cy = spr.x0 + spr.img.shape[1] / 2, spr.y0 + spr.img.shape[0] / 2
            far = np.array([cx, cy]) + (np.array([cx, cy]) - b.pivot) * 0.6
            chain.append(conv((mats[name][0] @ [*far, 1])[:2]))
        d.line([pts[b.parent], pts[name]], fill=(255, 255, 255, a // 2), width=2)
        d.line(chain, fill=(40, 120, 255, a), width=5)
        for p in chain[1:]:
            d.ellipse([p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4], fill=(40, 120, 255, a))
    for name, p in pts.items():
        r = 7 if name != 'Root' else 9
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=(255, 210, 60, a), outline=(60, 40, 30, a), width=2)


def sec_idle(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '2. 본 구조 + 대기(Idle)', '피벗(노란 점) = 관절.  몸통 → 머리 · 팔 · 꼬리(3마디).  측면은 골반 → 다리 → 발(발목 관절)')
    fr = to_frame(img)
    k = 0.62
    bone_a = 1 - ease((t - 4.0) / 0.8)
    for v in ('Front', 'Side', 'Back'):
        origin = (POS3[v], 560)
        pose = anim.idle(v, t)
        shadow(fr, origin[0], origin[1] + 6, 75, 12)
        blit(fr, render_char(v, pose, origin, k), alpha=1 - 0.35 * bone_a)
    img = from_frame(fr)
    for v in ('Front', 'Side', 'Back'):
        draw_bones(img, v, anim.idle(v, t), (POS3[v], 560), k, bone_a)
        text(ImageDraw.Draw(img), (POS3[v], 610), VIEW_KOR[v].split(' ')[0], f_lab, anchor='ma')
    return to_frame(img)


POS4 = {'Front': 175, 'Side': 485, 'Left': 795, 'Back': 1105}


def ground(img, y, x0, x1, offset):
    """Ground line with dashes scrolled by `offset` px (shows the planted foot staying put)."""
    d = ImageDraw.Draw(img)
    d.line([(x0, y), (x1, y)], fill=(190, 170, 140), width=3)
    step = 36
    for i in range(-1, int((x1 - x0) / step) + 2):
        x = x0 + i * step + offset % step
        if x0 <= x <= x1 - 14:
            d.line([(x, y + 9), (x + 14, y + 9)], fill=(205, 188, 160), width=3)


def side_px_per_sec(k):
    """Canvas speed at which a side-walking otter drawn at scale k doesn't skid."""
    return anim.SIDE_SPEED * cp.SCALE * k


def sec_walk4(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '3. 걷기(Walk) — 4방향', '측면은 양발이 교차 · 왼쪽은 오른쪽 리그 좌우 반전 · 바닥 점선 = 제자리 걷기 기준')
    k = 0.6
    for v in POS4:
        off = {'Side': -1, 'Left': 1}.get(v, 0) * side_px_per_sec(k) * t
        ground(img, 568, POS4[v] - 130, POS4[v] + 130, off)
    fr = to_frame(img)
    for v in POS4:
        origin = (POS4[v], 565)
        shadow(fr, origin[0], origin[1] + 6, 72, 11)
        blit(fr, render_char(v, pose_for(v, 'walk', t), origin, k))
    img = from_frame(fr)
    d = ImageDraw.Draw(img)
    for v in POS4:
        text(d, (POS4[v], 610), VIEW_KOR[v], f_small if v == 'Left' else f_lab, anchor='ma')
    return to_frame(img)


def sec_side(t, dur):
    """Side walk across the screen at the no-skid speed, then slow motion in place."""
    img = Image.new('RGB', (W, H), BG)
    k = 0.85
    gy = 600
    speed = side_px_per_sec(k)
    cross = 5.6
    if t < cross:
        header(img, '4. 측면 걷기 — 발 구르기', '뒤꿈치로 착지 → 발바닥 디딤 → 발끝으로 차고 뒤로 들림 → 앞으로 뻗음 · 디딘 발은 땅에 고정')
        x = -160 + speed * t
        ground(img, gy + 3, 0, W, 0)
        wt = t
    else:
        header(img, '4. 측면 걷기 — 슬로 모션 (0.35배)', '바지는 짧게 몸속으로 접히고(무릎 굽힘 느낌) 발이 따로 까딱임 · 뒷다리는 어둡게')
        x = W // 2
        wt = (t - cross) * 0.35
        ground(img, gy + 3, 0, W, -speed * wt)
    fr = to_frame(img)
    shadow(fr, x, gy + 5, 95, 14, 0.2)
    blit(fr, render_char('Side', anim.walk('Side', wt), (x, gy), k))
    # quick dip to the background colour between the two halves
    fade = min(1.0, abs(t - cross) / 0.25)
    return fr * fade + np.array(BG, np.float32) * (1 - fade)


# free roam: waypoints (x, y) in px, pauses in seconds
ROAM_K = 0.5
ROAM_SPEED_V = 115.0      # front / back stepping has no ground contact to match


def roam_speed(a, b):
    return side_px_per_sec(ROAM_K) if _dir_view(a, b) in ('Side', 'Left') else ROAM_SPEED_V
ROUTE = [((330, 355), 0.8), ((950, 355), 0.9), ((950, 615), 0.6), ((330, 615), 0.9), ((330, 355), 0.8)]


def make_field():
    rng = np.random.default_rng(7)
    img = Image.new('RGB', (W, H), (150, 190, 110))
    d = ImageDraw.Draw(img)
    for _ in range(420):
        x, y = rng.integers(0, W), rng.integers(110, H)
        c = tuple(int(v) for v in (np.array([120, 165, 85]) + rng.integers(-12, 12, 3)))
        d.ellipse([x, y, x + rng.integers(6, 16), y + rng.integers(3, 7)], fill=c)
    # tilled plot in the middle of the walking loop
    d.rounded_rectangle([430, 390, 850, 550], 18, fill=(150, 105, 70))
    for i in range(5):
        y = 410 + i * 30
        d.rounded_rectangle([450, y, 830, y + 14], 7, fill=(125, 85, 55))
        for j in range(8):
            x = 470 + j * 47
            d.ellipse([x, y - 10, x + 14, y + 4], fill=(95, 160, 70))
            d.ellipse([x + 8, y - 13, x + 22, y + 1], fill=(110, 175, 80))
    d.rectangle([0, 0, W, 110], fill=BG)
    return img.filter(ImageFilter.GaussianBlur(0.6))


FIELD = None


def roam_state(t):
    """(x, y, view, moving, walk_time) along ROUTE."""
    tt = 0.0
    walk_t = 0.0
    for i in range(len(ROUTE)):
        (x0, y0), pause = ROUTE[i]
        if t < tt + pause:
            nxt = ROUTE[min(i + 1, len(ROUTE) - 1)][0]
            view = _dir_view(ROUTE[max(i - 1, 0)][0], (x0, y0)) if i else 'Front'
            return x0, y0, view, False, walk_t, t - tt
        tt += pause
        if i + 1 == len(ROUTE):
            break
        (x1, y1) = ROUTE[i + 1][0]
        dist = math.hypot(x1 - x0, y1 - y0)
        dt = dist / roam_speed((x0, y0), (x1, y1))
        if t < tt + dt:
            f = (t - tt) / dt
            return x0 + (x1 - x0) * f, y0 + (y1 - y0) * f, _dir_view((x0, y0), (x1, y1)), True, t - tt, 0
        tt += dt
    return ROUTE[-1][0][0], ROUTE[-1][0][1], 'Back', False, 0, t - tt


def _dir_view(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    if abs(dx) > abs(dy):
        return 'Side' if dx > 0 else 'Left'
    return 'Front' if dy > 0 else 'Back'


def roam_duration():
    total = sum(p for _, p in ROUTE)
    for i in range(len(ROUTE) - 1):
        (x0, y0), (x1, y1) = ROUTE[i][0], ROUTE[i + 1][0]
        total += math.hypot(x1 - x0, y1 - y0) / roam_speed((x0, y0), (x1, y1))
    return total


def sec_roam(t, dur):
    global FIELD
    if FIELD is None:
        FIELD = make_field()
    img = FIELD.copy()
    header(img, '5. 자유 이동', '이동 방향에 따라 4방향 리그 전환 · 멈추면 대기 동작으로 부드럽게 블렌드')
    fr = to_frame(img)
    x, y, view, moving, wt, since_stop = roam_state(min(t, dur - 0.01))
    walk_pose = pose_for(view, 'walk', wt if moving else 0)
    idle_pose = pose_for(view, 'idle', t)
    if moving:
        pose = anim.blend(idle_pose, walk_pose, ease(wt / 0.15))
    else:
        pose = anim.blend(pose_for(view, 'walk', 0), idle_pose, ease(since_stop / 0.25))
    shadow(fr, x, y + 5, 58, 10, 0.22)
    blit(fr, render_char(view, pose, (x, y), ROAM_K))
    return fr


# --- recolour variants ------------------------------------------------------------------

def hue_shift(im, ranges):
    """ranges: list of (h_lo, h_hi, new_h, sat_mul, val_mul); hue in degrees."""
    a = np.asarray(im).astype(np.float32) / 255
    rgb = a[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    hsv = np.asarray(Image.fromarray((rgb * 255).astype(np.uint8)).convert('HSV')).astype(np.float32)
    h = hsv[..., 0] * 360 / 255
    s = hsv[..., 1] / 255
    out = hsv.copy()
    for lo, hi, nh, sm, vm in ranges:
        m = (h >= lo) & (h <= hi) & (s > 0.18) & (mx > 0.25)
        out[..., 0] = np.where(m, nh * 255 / 360, out[..., 0])
        out[..., 1] = np.where(m, np.clip(hsv[..., 1] * sm, 0, 255), out[..., 1])
        out[..., 2] = np.where(m, np.clip(hsv[..., 2] * vm, 0, 255), out[..., 2])
    rgb2 = np.asarray(Image.fromarray(out.astype(np.uint8), 'HSV').convert('RGB'))
    return Image.fromarray(np.dstack([rgb2, np.asarray(im)[..., 3]]))


VARIANTS = [
    ('농부 (원본)', None),
    ('변형 A (파란 멜빵)', [(70, 160, 212, 1.0, 1.0), (30, 62, 200, 0.25, 1.08)]),   # pale hat
    ('변형 B (빨간 멜빵)', [(70, 160, 356, 1.25, 1.0), (30, 62, 95, 0.6, 0.85)]),     # green hat
]
VAR_RIGS = None


def sec_variants(t, dur):
    global VAR_RIGS
    if VAR_RIGS is None:
        VAR_RIGS = []
        for name, rng in VARIANTS:
            rc = (lambda part, im, rng=rng: hue_shift(im, rng)) if rng else None
            VAR_RIGS.append({v: rig.Rig(*views[v], recolor=rc) for v in ('Front', 'Side')})
    img = Image.new('RGB', (W, H), BG)
    header(img, '6. 같은 리그 · 다른 캐릭터', '본/애니메이션은 그대로, 파츠 이미지만 교체 (여기선 색만 바꿔서 예시)')
    fr = to_frame(img)
    k = 0.6
    view = 'Front' if (t % 6) < 3 else 'Side'
    for i, (name, _) in enumerate(VARIANTS):
        x = 250 + i * 390
        origin = (x, 565)
        shadow(fr, x, 571, 72, 11)
        pose = anim.walk(view, t + i * 0.11)
        blit(fr, VAR_RIGS[i][view].render(pose, (W, H), origin, k=k))
    img = from_frame(fr)
    d = ImageDraw.Draw(img)
    for i, (name, _) in enumerate(VARIANTS):
        text(d, (250 + i * 390, 610), name, f_lab, anchor='ma')
    return to_frame(img)


def sec_end(t, dur):
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)
    text(d, (W // 2, 250), '정면 · 측면 · 뒷모습  3세트 파츠', f_title, anchor='ma')
    text(d, (W // 2, 330), '머리 / 몸통 / 팔 ×2 / 다리 ×2 / 꼬리', f_sub, fill=(120, 95, 80), anchor='ma')
    text(d, (W // 2, 372), 'Unity 2D Animation (SpriteSkin) 리그에 바로 넣을 수 있게 피벗 = 관절 위치로 정리', f_sub,
         fill=(120, 95, 80), anchor='ma')
    fr = to_frame(img)
    blit(fr, render_char('Front', anim.idle('Front', t), (W // 2, 660), 0.45))
    return fr


SECTIONS = [
    ('intro', sec_intro, 3.5),
    ('parts', sec_parts, 6.0),
    ('idle', sec_idle, 7.0),
    ('walk4', sec_walk4, 7.0),
    ('side', sec_side, 10.0),
    ('roam', sec_roam, roam_duration() + 0.5),
    ('variants', sec_variants, 7.0),
    ('end', sec_end, 3.0),
]
FADE = 0.35


def main():
    if ONLY:
        name, fn, dur = next(s for s in SECTIONS if s[0] == ONLY)
        for i, tt in enumerate(np.linspace(0, dur - 0.01, 6)):
            from_frame(fn(tt, dur)).save(f'chk_{name}_{i}.png')
        return
    total = sum(d for _, _, d in SECTIONS)
    n = int(total * FPS)
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    proc = subprocess.Popen([ff, '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS),
                             '-i', '-', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-preset', 'slow',
                             '-movflags', '+faststart', OUT], stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
    start = 0.0
    for name, fn, dur in SECTIONS:
        frames = int(round(dur * FPS))
        for i in range(frames):
            t = i / FPS
            fr = fn(t, dur)
            fade = min(1, t / FADE, (dur - t) / FADE)
            fr = fr * fade + np.array(BG, np.float32) * (1 - fade)
            proc.stdin.write(fr.clip(0, 255).astype(np.uint8).tobytes())
        start += dur
        print(name, 'done', flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', OUT, f'{total:.1f}s')


if __name__ == '__main__':
    main()
