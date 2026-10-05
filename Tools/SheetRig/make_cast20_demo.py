"""Renders the 20-otter cast demo (1280x720, 30 fps) from cast_rig.CAST.

Usage: python make_cast20_demo.py [out.mp4]      (default ArtSource/Otter/FrontRig/Cast/otter_cast20_demo.mp4)
       python make_cast20_demo.py x <section>    (6 check frames of one section as PNGs)
"""
import os
import subprocess
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import imageio_ffmpeg

import cast_rig as cr
import cut_parts as cp
import front_rig as fr
import make_demo as md
from make_demo import W, H, FPS, BG, ease, text, header, to_frame, from_frame

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'FrontRig', 'Cast',
                                                           'otter_cast20_demo.mp4')
ONLY = sys.argv[2] if len(sys.argv) > 2 else None

CAST = cr.CAST
RIGS = [cr.make_rig(cr.build_otter(o)) for o in CAST]
BASES = {n: fr.make_rig(n) for n in ('Pajama', 'Miner', 'Fisher', 'Chief')}


def blit_at(frame, prem, x0, y0, alpha=1.0):
    """Composite a small premultiplied canvas onto the frame at (x0, y0), clipped."""
    h, w = prem.shape[:2]
    fx0, fy0 = max(0, x0), max(0, y0)
    fx1, fy1 = min(W, x0 + w), min(H, y0 + h)
    if fx1 <= fx0 or fy1 <= fy0:
        return
    p = prem[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0]
    a = p[..., 3:4] / 255 * alpha
    region = frame[fy0:fy1, fx0:fx1]
    frame[fy0:fy1, fx0:fx1] = p[..., :3] * alpha + region * (1 - a)


def shadow(frame, cx, cy, rx, ry, a=0.16):
    x0, x1 = int(max(0, cx - rx * 1.3)), int(min(W, cx + rx * 1.3))
    y0, y1 = int(max(0, cy - ry * 1.3)), int(min(H, cy + ry * 1.3))
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.ogrid[y0:y1, x0:x1]
    m = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    frame[y0:y1, x0:x1] *= (1 - np.clip(1.3 - m, 0, 1) * a * 0.6)[..., None]


def draw(frame, rg, pose, feet, k, box=(300, 300), alpha=1.0):
    """Render rg in a small canvas around the feet point and composite it."""
    bw, bh = box
    canvas = rg.render(pose, (bw, bh), (bw // 2, bh - 12), k=k)
    blit_at(frame, canvas, int(feet[0] - bw // 2), int(feet[1] - bh + 12), alpha)


# 5 x 4 grid
GX = [128 + 256 * c for c in range(5)]
GY = [262, 410, 558, 706]     # label lines (feet sit 34 px above)
GK = 0.22


def grid(t, fn, title, sub, label=None):
    img = Image.new('RGB', (W, H), BG)
    header(img, title, sub)
    fr_ = to_frame(img)
    for i, o in enumerate(CAST):
        x, y = GX[i % 5], GY[i // 5] - 34
        shadow(fr_, x, y + 3, 36, 5)
        draw(fr_, RIGS[i], fn(o, t), (x, y), GK, box=(256, 150))
    img = from_frame(fr_)
    d = ImageDraw.Draw(img)
    for i, o in enumerate(CAST):
        x, y = GX[i % 5], GY[i // 5] - 30
        text(d, (x, y), o['name'], md.f_small, anchor='ma')
        if label:
            text(d, (x, y + 18), label(o), md.f_small, fill=(160, 115, 70), anchor='ma')
    return to_frame(img)


def sec_intro(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '해달 20마리 — 역할 20개', '정면 일러스트 4종을 같은 리그로 조합  ·  눈 · 눈썹은 원본 그대로  ·  머리 모양 / 갈기 / 색을 다양하게')
    fr_ = to_frame(img)
    xs = [260, 513, 767, 1020]
    names = ['잠옷 해달', '광부 해달', '낚시 해달', '촌장 해달']
    for i, rg in enumerate(BASES.values()):
        a = ease((t - 0.2 - 0.2 * i) / 0.5)
        if a <= 0:
            continue
        shadow(fr_, xs[i], 545, 70, 11, 0.16 * a)
        draw(fr_, rg, {}, (xs[i], 540), 0.55, box=(330, 340), alpha=a)
    img = from_frame(fr_)
    d = ImageDraw.Draw(img)
    for i, n in enumerate(names):
        text(d, (xs[i], 570), n, md.f_lab, anchor='ma')
    a = ease((t - 1.6) / 0.6)
    if a > 0:
        col = tuple(int(BG[c] + ((150, 100, 60)[c] - BG[c]) * a) for c in range(3))
        text(d, (W // 2, 640), '→  머리 × 옷 조합 + 털색 · 머리 모양 · 갈기 · 역할 소품으로 20마리', md.f_sub, fill=col,
             anchor='ma')
    return to_frame(img)


def sec_idle(t, dur):
    return grid(t, cr.idle, '1. 대기', '20마리 모두 같은 본 구조 · 같은 대기 애니메이션 (눈 깜빡임 타이밍만 다름)',
                label=lambda o: o['role'])


def sec_walk(t, dur):
    return grid(t, cr.walk, '2. 걷기', '같은 정면 걷기 클립 — 머리카락 · 소품은 머리 파츠에 붙어서 같이 움직임')


def sec_action(t, dur):
    used = []
    for o in CAST:
        if cr.ACTION_KOR[o['action']] not in used:
            used.append(cr.ACTION_KOR[o['action']])
    return grid(t, cr.action, '3. 캐릭터별 동작', ' · '.join(used),
                label=lambda o: cr.ACTION_KOR[o['action']])


SEG = 1.7


def sec_closeups(t, dur):
    i = min(len(CAST) - 1, int(t / SEG))
    o = CAST[i]
    tt = t - i * SEG
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)
    text(d, (690, 190), f"{i + 1:02d}", md.f_sub, fill=(190, 150, 110))
    text(d, (690, 225), o['name'], md.f_title)
    text(d, (692, 290), o['role'], md.f_sub, fill=(150, 110, 70))
    for j, line in enumerate(cr.recipe_kor(o)):
        text(d, (692, 350 + j * 34), line, md.f_small, fill=(110, 85, 70))
    fr_ = to_frame(img)
    shadow(fr_, 380, 655, 120, 17)
    draw(fr_, RIGS[i], cr.action(o, tt), (380, 650), 1.0, box=(620, 640))
    fade = min(1.0, tt / 0.18, (SEG - tt) / 0.18)
    if (i > 0 or tt > 0.18) and (i < len(CAST) - 1 or SEG - tt > 0.18):
        fr_ = fr_ * fade + np.array(BG, np.float32) * (1 - fade)
    return fr_


def make_village():
    rng = np.random.default_rng(5)
    img = Image.new('RGB', (W, H), (150, 190, 110))
    d = ImageDraw.Draw(img)
    for _ in range(500):
        x, y = rng.integers(0, W), rng.integers(110, H)
        c = tuple(int(v) for v in (np.array([120, 165, 85]) + rng.integers(-12, 12, 3)))
        d.ellipse([x, y, x + rng.integers(6, 16), y + rng.integers(3, 7)], fill=c)
    d.rounded_rectangle([560, 110, 720, H], 40, fill=(215, 190, 145))
    d.ellipse([60, 560, 360, 700], fill=(110, 170, 200), outline=(80, 130, 160), width=5)
    for x, y, r in [(1130, 600, 40), (1185, 625, 28), (1085, 632, 24)]:
        d.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=(150, 150, 160), outline=(95, 95, 110), width=4)
    d.rectangle([0, 0, W, 110], fill=BG)
    return img.filter(ImageFilter.GaussianBlur(0.6))


VILLAGE = None
# feet positions, roughly by role (fishers by the pond, miners by the rocks)
SPOTS = {
    'StargazerOtter': (930, 250), 'StorytellerOtter': (1020, 268), 'PillowMakerOtter': (1110, 252),
    'BlacksmithOtter': (1040, 470), 'CarpenterOtter': (880, 400), 'FossilHunterOtter': (1150, 560),
    'MechanicOtter': (960, 600), 'ExplorerOtter': (520, 520), 'PhotographerOtter': (430, 690),
    'EntomologistOtter': (250, 560), 'GardenerOtter': (120, 470), 'BeekeeperOtter': (380, 610),
    'PostmanOtter': (680, 470), 'TeacherOtter': (210, 270), 'BakerOtter': (300, 380),
    'PainterOtter': (90, 650), 'MusicianOtter': (640, 330), 'LibrarianOtter': (420, 290),
    'ShopkeeperOtter': (780, 600), 'TailorOtter': (1210, 660),
}


def sec_village(t, dur):
    global VILLAGE
    if VILLAGE is None:
        VILLAGE = make_village()
    img = VILLAGE.copy()
    header(img, '4. 해달 마을', '20마리가 각자 일터에서 — 정원사는 꽃밭, 화석 발굴가는 바위 옆, 별지기들은 언덕 위')
    fr_ = to_frame(img)
    actors = sorted(((SPOTS[o['prefix']], i) for i, o in enumerate(CAST)), key=lambda a: a[0][1])
    for (x, y), i in actors:
        k = 0.2 + 0.12 * (y - 240) / 450
        shadow(fr_, x, y + 3, 120 * k, 18 * k, 0.22)
        draw(fr_, RIGS[i], cr.action(CAST[i], t), (x, y), k, box=(220, 200))
    return fr_


def sec_end(t, dur):
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)
    text(d, (W // 2, 140), '역할 20개 × (파츠 + rig_layout.json)', md.f_title, anchor='ma')
    text(d, (W // 2, 210), 'ArtSource/Otter/FrontRig/Cast/<이름>  ·  조합표: cast.json', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    text(d, (W // 2, 248), '새 조합은 cast_rig.CAST에 한 줄 추가 → export_cast.py', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    fr_ = to_frame(img)
    for j, i in enumerate((0, 4, 9, 12, 18)):
        x = 180 + j * 230
        shadow(fr_, x, 606, 60, 10)
        draw(fr_, RIGS[i], cr.idle(CAST[i], t), (x, 600), 0.5, box=(320, 340))
    return fr_


SECTIONS = [
    ('intro', sec_intro, 4.0),
    ('idle', sec_idle, 4.5),
    ('walk', sec_walk, 5.0),
    ('action', sec_action, 8.0),
    ('closeups', sec_closeups, SEG * len(CAST)),
    ('village', sec_village, 10.0),
    ('end', sec_end, 3.5),
]
FADE = 0.35


def main():
    if ONLY:
        name, fn, dur = next(s for s in SECTIONS if s[0] == ONLY)
        for i, tt in enumerate(np.linspace(0, dur - 0.01, 6)):
            from_frame(fn(tt, dur)).save(f'chk_{name}_{i}.png')
        return
    total = sum(d for _, _, d in SECTIONS)
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    proc = subprocess.Popen([ff, '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS),
                             '-i', '-', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-preset', 'slow',
                             '-movflags', '+faststart', OUT], stdin=subprocess.PIPE, stderr=subprocess.DEVNULL)
    for name, fn, dur in SECTIONS:
        for i in range(int(round(dur * FPS))):
            t = i / FPS
            f = fn(t, dur)
            fade = min(1, t / FADE, (dur - t) / FADE)
            f = f * fade + np.array(BG, np.float32) * (1 - fade)
            proc.stdin.write(f.clip(0, 255).astype(np.uint8).tobytes())
        print(name, 'done', flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', OUT, f'{total:.1f}s')


if __name__ == '__main__':
    main()
