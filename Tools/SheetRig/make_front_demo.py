"""Renders the front-rig demo video (1280x720, 30 fps): pajama / miner / fisher / chief
otters rigged from single front illustrations (front_rig + front_anim).

Usage: python make_front_demo.py [out.mp4]      (default ArtSource/Otter/FrontRig/otter_front_demo.mp4)
       python make_front_demo.py x <section>    (6 check frames of one section as PNGs)
"""
import math
import os
import subprocess
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import imageio_ffmpeg

import cut_parts as cp
import front_anim as fa
import front_rig as fr
import make_demo as md
from make_demo import W, H, FPS, BG, ease, blit, text, header, shadow, to_frame, from_frame

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'FrontRig',
                                                           'otter_front_demo.mp4')
ONLY = sys.argv[2] if len(sys.argv) > 2 else None

NAMES = ['Pajama', 'Miner', 'Fisher', 'Chief']
KOR = {n: fr.CHARS[n]['name'] for n in NAMES}
RIGS = {n: fr.make_rig(n) for n in NAMES}
X4 = [175, 485, 795, 1105]
K = 0.62          # standard on-screen scale (rig px -> screen px)


def draw(fr_, name, pose, origin, k=K, alpha=1.0):
    blit(fr_, RIGS[name].render(pose, (W, H), origin, k=k), alpha)


def names_row(img, xs, y, sub=None, names=NAMES):
    d = ImageDraw.Draw(img)
    for n, x in zip(names, xs):
        text(d, (x, y), KOR[n], md.f_lab, anchor='ma')
        if sub:
            text(d, (x, y + 30), sub[n], md.f_small, fill=(130, 105, 90), anchor='ma')


SOURCES = {}


def source(name, height):
    key = (name, height)
    if key not in SOURCES:
        spec = fr.CHARS[name]
        im = Image.open(os.path.join(fr.SRC_DIR, spec['prefix'] + '_Front.png')).convert('RGBA')
        im = im.crop(im.getbbox())
        SOURCES[key] = im.resize((round(im.width * height / im.height), height), Image.LANCZOS)
    return SOURCES[key]


def sec_intro(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '새 해달 4종 리깅', '정면 일러스트 한 장씩을 파츠로 나눠서 본으로 움직입니다')
    for i, n in enumerate(NAMES):
        a = ease((t - 0.2 - 0.25 * i) / 0.5)
        if a <= 0:
            continue
        im = source(n, 330).copy()
        im.putalpha(im.getchannel('A').point(lambda p: int(p * a)))
        img.paste(im, (X4[i] - im.width // 2, 230 + int((1 - a) * 25)), im)
    names_row(img, X4, 590, {n: '원본 일러스트' for n in NAMES})
    return to_frame(img)


def explode_pose(name, amt):
    """Push every part away from the body centre by `amt` px (rig units)."""
    rg = RIGS[name]
    body = rg.bones['Body'].pivot
    cx, cy = body[0], body[1] - 60 * rg.bones['Body'].pivot[1] / 400
    pose = {}
    for bname, spr in rg.sprites.items():
        if bname == 'Body':
            continue
        px = spr.x0 + spr.img.shape[1] / 2
        py = spr.y0 + spr.img.shape[0] / 2
        dx, dy = px - cx, py - cy
        dn = math.hypot(dx, dy) or 1
        b = rg.bones[bname]
        # children of a moved part already move with it
        if b.parent in ('Arm_L', 'Head', 'Rod'):
            continue
        pose[bname] = dict(dx=dx / dn * amt, dy=dy / dn * amt)
    return pose


def sec_parts(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '1. 파츠 분리', '머리 · 몸통 · 팔 · 다리 + 캐릭터별 소품 (방울 / 곡괭이 / 낚싯대 · 찌 / 스카프)')
    fr_ = to_frame(img)
    amt = 22 * (1 - ease((t - 3.0) / 1.4)) * ease(t / 0.8)
    for i, n in enumerate(NAMES):
        draw(fr_, n, explode_pose(n, amt), (X4[i], 560))
    img = from_frame(fr_)
    names_row(img, X4, 605)
    return to_frame(img)


def sec_idle(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '2. 대기 — 숨쉬기 · 눈 깜빡임', '눈 감은 얼굴은 같은 머리 파츠의 교체 스프라이트 (Head_Blink)')
    fr_ = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr_, X4[i], 566, 72, 11)
        draw(fr_, n, fa.idle(n, t), (X4[i], 560))
    img = from_frame(fr_)
    names_row(img, X4, 605)
    return to_frame(img)


def sec_walk(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '3. 걷기 (정면)', '팔 흔들기 · 발 디딤 · 골반 흔들림 — 낚싯대는 흔들리지 않게 반대로 보정, 찌는 진자처럼')
    fr_ = to_frame(img)
    for i, n in enumerate(NAMES):
        tt = t + i * 0.08
        shadow(fr_, X4[i], 566, 72, 11)
        draw(fr_, n, fa.walk(n, tt), (X4[i], 560))
    img = from_frame(fr_)
    names_row(img, X4, 605)
    return to_frame(img)


G4 = [(330, 330), (950, 330), (330, 655), (950, 655)]


def sec_actions(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '4. 캐릭터별 동작', '')
    d = ImageDraw.Draw(img)
    for (x, y), n in zip(G4, NAMES):
        text(d, (x + 150, y - 150), KOR[n], md.f_lab, anchor='la')
        text(d, (x + 150, y - 118), fa.ACTION_KOR[n], md.f_sub, fill=(150, 110, 70), anchor='la')
    fr_ = to_frame(img)
    for (x, y), n in zip(G4, NAMES):
        shadow(fr_, x, y + 5, 62, 10)
        draw(fr_, n, fa.action(n, t), (x, y), k=0.5)
    return fr_


def sec_closeups(t, dur):
    """Each action big, one after another."""
    seg = dur / 4
    i = min(3, int(t / seg))
    n = NAMES[i]
    tt = t - i * seg
    img = Image.new('RGB', (W, H), BG)
    header(img, f'5. {KOR[n]} — {fa.ACTION_KOR[n]}', '')
    fr_ = to_frame(img)
    shadow(fr_, W // 2, 676, 110, 16)
    draw(fr_, n, fa.action(n, tt + (fa.ACTION_PERIOD[n] * 0.85 if n == 'Pajama' else 0)), (W // 2, 670), k=0.95)
    fade = min(1.0, tt / 0.2, (seg - tt) / 0.2) if 0 < i < 3 or tt > 0.2 else 1.0
    if i > 0 and tt < 0.2 or i < 3 and seg - tt < 0.2:
        fr_ = fr_ * fade + np.array(BG, np.float32) * (1 - fade)
    return fr_


def make_village():
    rng = np.random.default_rng(11)
    img = Image.new('RGB', (W, H), (150, 190, 110))
    d = ImageDraw.Draw(img)
    for _ in range(420):
        x, y = rng.integers(0, W), rng.integers(110, H)
        c = tuple(int(v) for v in (np.array([120, 165, 85]) + rng.integers(-12, 12, 3)))
        d.ellipse([x, y, x + rng.integers(6, 16), y + rng.integers(3, 7)], fill=c)
    # path (chief) in the middle, pond left of the fisher (the rod reaches left),
    # rocks left of the miner (the pickaxe is in the left paw), pillow under the pajama otter
    d.rounded_rectangle([560, 110, 720, H], 40, fill=(215, 190, 145))
    d.ellipse([120, 540, 410, 690], fill=(110, 170, 200), outline=(80, 130, 160), width=5)
    d.ellipse([170, 570, 290, 610], fill=(150, 200, 225))
    for x, y, r in [(820, 600, 44), (880, 626, 30), (770, 630, 26)]:
        d.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=(150, 150, 160), outline=(95, 95, 110), width=4)
        d.ellipse([x - r * 0.5, y - r * 0.6, x, y - r * 0.2], fill=(185, 185, 195))
    d.rounded_rectangle([1005, 312, 1125, 350], 18, fill=(200, 185, 230), outline=(140, 120, 180), width=4)
    d.rectangle([0, 0, W, 110], fill=BG)
    return img.filter(ImageFilter.GaussianBlur(0.6))


VILLAGE = None


def sec_village(t, dur):
    global VILLAGE
    if VILLAGE is None:
        VILLAGE = make_village()
    img = VILLAGE.copy()
    header(img, '6. 마을에서', '광부는 바위 앞 · 낚시꾼은 연못가 · 촌장은 길에서 인사 · 잠옷 해달은 베개 옆에서 꾸벅')
    fr_ = to_frame(img)
    actors = []
    # chief walks down the path toward the camera, then waves
    walk_t = 4.0
    if t < walk_t:
        cx, cy = 640, 300 + (470 - 300) * (t / walk_t)
        chief_pose = fa.walk('Chief', t)
    else:
        cx, cy = 640, 470
        chief_pose = fa.action('Chief', t - walk_t)
    actors.append((cy, 'Chief', chief_pose, (cx, cy), 0.42 + 0.06 * min(1, t / walk_t)))
    actors.append((640, 'Miner', fa.action('Miner', t), (975, 640), 0.42))
    actors.append((610, 'Fisher', fa.action('Fisher', t + 0.5), (470, 610), 0.42))
    actors.append((335, 'Pajama', fa.action('Pajama', t + 1.0), (1065, 335), 0.34))
    for y, n, pose, origin, k in sorted(actors, key=lambda a: a[0]):
        shadow(fr_, origin[0], origin[1] + 5, 55 * k / 0.42, 9, 0.22)
        draw(fr_, n, pose, origin, k=k)
    return fr_


def sec_end(t, dur):
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)
    text(d, (W // 2, 120), '정면 리그 4종 — 파츠 + rig_layout.json', md.f_title, anchor='ma')
    text(d, (W // 2, 190), 'ArtSource/Otter/FrontRig/{PajamaOtter, MinerOtter, FishingOtter, ChiefOtter}', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    text(d, (W // 2, 228), '측면 · 뒷모습 일러스트가 생기면 같은 방식으로 4방향 리그로 확장 가능', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    fr_ = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr_, X4[i], 606, 70, 11)
        draw(fr_, n, fa.idle(n, t + i * 0.5), (X4[i], 600))
    return fr_


SECTIONS = [
    ('intro', sec_intro, 3.5),
    ('parts', sec_parts, 5.0),
    ('idle', sec_idle, 5.0),
    ('walk', sec_walk, 6.0),
    ('actions', sec_actions, 8.0),
    ('closeups', sec_closeups, 14.0),
    ('village', sec_village, 12.0),
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
