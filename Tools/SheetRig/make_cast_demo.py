"""Renders the cast demo video (1280x720, 30 fps): farmer / chief / miner / fisher,
all driven by the same rig and clips.

Usage: python make_cast_demo.py [out.mp4]       (default ArtSource/Otter/SheetRig/otter_cast_demo.mp4)
       python make_cast_demo.py x <section>     (6 check frames of one section as PNGs)
"""
import math
import os
import subprocess
import sys
import numpy as np
from PIL import Image, ImageDraw
import imageio_ffmpeg

import anim
import cut_parts as cp
import rig
import variants
import make_demo as md
from make_demo import W, H, FPS, BG, INK, ease, blit, text, header, shadow, to_frame, from_frame

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'SheetRig',
                                                           'otter_cast_demo.mp4')
ONLY = sys.argv[2] if len(sys.argv) > 2 else None

NAMES = ['Farmer', 'Chief', 'Miner', 'Fisher']
KOR = {'Farmer': '농부 해달', 'Chief': '촌장 해달', 'Miner': '광부 해달', 'Fisher': '낚시 해달'}
FACE = {'Farmer': '원본 그대로 (반쯤 감긴 눈)', 'Chief': '웃는 눈 · 동그란 안경 · 하얀 눈썹/수염',
        'Miner': '반짝이는 큰 눈 · 헤드램프 · 볼에 검댕', 'Fisher': '동글 점눈 · 벌린 입 · 주근깨 · 찌'}

_farmer = cp.cut_all()
CAST = {n: rig.build_views(_farmer if n == 'Farmer' else variants.build(n, _farmer)) for n in NAMES}
RIGS = {n: {v: rig.Rig(*CAST[n][v]) for v in ('Front', 'Side', 'Back')} for n in NAMES}


def draw(fr, name, view, pose, origin, k):
    rg = RIGS[name]['Side' if view == 'Left' else view]
    blit(fr, rg.render(pose, (W, H), origin, k=k, flip=(view == 'Left')))


def names_row(img, xs, y, sub=None):
    d = ImageDraw.Draw(img)
    for n, x in zip(NAMES, xs):
        text(d, (x, y), KOR[n], md.f_lab, anchor='ma')
        if sub:
            text(d, (x, y + 30), sub[n], md.f_small, fill=(130, 105, 90), anchor='ma')


X4 = [175, 485, 795, 1105]


def sec_intro(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '해달 캐릭터 4종 — 같은 리그', '농부 파츠에서 얼굴 · 모자 · 옷만 다시 칠함.  본 / 피벗 / 애니메이션은 전부 공유')
    fr = to_frame(img)
    for i, n in enumerate(NAMES):
        a = ease((t - 0.2 - 0.25 * i) / 0.5)
        if a <= 0:
            continue
        y = 560 + (1 - a) * 25
        shadow(fr, X4[i], y + 6, 70, 11, 0.16 * a)
        blit(fr, RIGS[n]['Front'].render(anim.idle('Front', t + i * 0.5), (W, H), (X4[i], y), k=0.6), alpha=a)
    img = from_frame(fr)
    names_row(img, X4, 605)
    return to_frame(img)


TURN = ['Front', 'Side', 'Back', 'Left']


def sec_turn(t, dur):
    img = Image.new('RGB', (W, H), BG)
    view = TURN[int(t / 1.6) % 4]
    header(img, '1. 4방향 파츠', f'정면 → 측면 → 뒷모습 → 왼쪽(측면 반전).  지금: {md.VIEW_KOR[view]}')
    fr = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr, X4[i], 566, 70, 11)
        draw(fr, n, view, md.pose_for(view, 'idle', t + i * 0.5), (X4[i], 560), 0.6)
    img = from_frame(fr)
    names_row(img, X4, 605)
    return to_frame(img)


def sec_faces(t, dur):
    img = Image.new('RGB', (W, H), BG)
    header(img, '2. 얼굴 바리에이션', '눈 모양만 바꿔도 성격이 달라짐 — 전부 동글동글한 선 · 볼터치는 유지')
    fr = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr, X4[i], 556, 85, 13)
        blit(fr, RIGS[n]['Front'].render(anim.idle('Front', t + i * 0.5), (W, H), (X4[i], 550), k=0.8))
    img = from_frame(fr)
    names_row(img, X4, 585, FACE)
    return to_frame(img)


def sec_walk(t, dur):
    img = Image.new('RGB', (W, H), BG)
    view = TURN[int(t / 2.5) % 4]
    header(img, '3. 걷기 — 같은 애니메이션', f'4명 모두 같은 클립을 재생.  지금: {md.VIEW_KOR[view]}')
    k = 0.6
    for i in range(4):
        off = {'Side': -1, 'Left': 1}.get(view, 0) * md.side_px_per_sec(k) * t
        md.ground(img, 568, X4[i] - 130, X4[i] + 130, off)
    fr = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr, X4[i], 571, 72, 11)
        draw(fr, n, view, md.pose_for(view, 'walk', t + i * 0.07), (X4[i], 565), k)
    img = from_frame(fr)
    names_row(img, X4, 610)
    return to_frame(img)


def sec_parade(t, dur):
    """All four walk across the screen in a line, side view."""
    img = Image.new('RGB', (W, H), BG)
    header(img, '4. 행진', '측면 걷기 (발 구르기) — 발이 땅에 고정되는 속도로 이동')
    k = 0.5
    gy = 590
    md.ground(img, gy + 3, 0, W, 0)
    fr = to_frame(img)
    speed = md.side_px_per_sec(k)
    for rank in reversed(range(4)):   # leader (farmer) drawn last
        x = 130 - rank * 240 + speed * t
        shadow(fr, x, gy + 5, 60, 10, 0.2)
        draw(fr, NAMES[rank], 'Side', anim.walk('Side', t + rank * 0.13), (x, gy), k)
    return fr


def sec_roam(t, dur):
    if md.FIELD is None:
        md.FIELD = md.make_field()
    img = md.FIELD.copy()
    header(img, '5. 마을 산책', '각자 다른 지점에서 출발 · 방향에 따라 리그 전환 · 모퉁이에서 잠깐 쉼')
    fr = to_frame(img)
    loop = md.roam_duration()
    actors = []
    for i, n in enumerate(NAMES):
        tt = (t + i * loop / 4) % loop
        x, y, view, moving, wt, since = md.roam_state(tt)
        if moving:
            pose = anim.blend(md.pose_for(view, 'idle', t), md.pose_for(view, 'walk', wt), ease(wt / 0.15))
        else:
            pose = anim.blend(md.pose_for(view, 'walk', 0), md.pose_for(view, 'idle', t + i), ease(since / 0.25))
        actors.append((y, x, n, view, pose))
    for y, x, n, view, pose in sorted(actors):
        shadow(fr, x, y + 5, 58, 10, 0.22)
        draw(fr, n, view, pose, (x, y), md.ROAM_K)
    return fr


def sec_end(t, dur):
    img = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(img)
    text(d, (W // 2, 120), '캐릭터마다 파츠 22장 + rig_layout.json', md.f_title, anchor='ma')
    text(d, (W // 2, 190), 'ArtSource/Otter/SheetRig/{FarmerOtter, ChiefOtter, MinerOtter, FishingOtter}', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    text(d, (W // 2, 228), '본 구조 · 피벗이 같아서 Unity 리그 하나로 스프라이트만 교체하면 됨', md.f_sub,
         fill=(120, 95, 80), anchor='ma')
    fr = to_frame(img)
    for i, n in enumerate(NAMES):
        shadow(fr, X4[i], 606, 70, 11)
        blit(fr, RIGS[n]['Front'].render(anim.idle('Front', t + i * 0.5), (W, H), (X4[i], 600), k=0.6))
    return fr


SECTIONS = [
    ('intro', sec_intro, 3.5),
    ('turn', sec_turn, 6.4),
    ('faces', sec_faces, 5.0),
    ('walk', sec_walk, 10.0),
    ('parade', sec_parade, 9.0),
    ('roam', sec_roam, 16.0),
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
            fr = fn(t, dur)
            fade = min(1, t / FADE, (dur - t) / FADE)
            fr = fr * fade + np.array(BG, np.float32) * (1 - fade)
            proc.stdin.write(fr.clip(0, 255).astype(np.uint8).tobytes())
        print(name, 'done', flush=True)
    proc.stdin.close()
    proc.wait()
    print('wrote', OUT, f'{total:.1f}s')


if __name__ == '__main__':
    main()
