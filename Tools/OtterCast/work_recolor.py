"""광산 · 낚시터에서 일하는 해달 그림(곡괭이질 · 낚싯대)을 광장의 새 모습(cast.json의 Miner · Fisher) 색으로 다시 칠한다.

농부 그림에는 곡괭이질 · 낚시 동작이 없어서, 일하는 시트는 원래 그림을 쓰고 털 · 모자 · 옷 색과 소품만 맞춘다.
시트를 그 자리에서 바꾸므로(메타 · 자른 칸 그대로) 광산 해달 · 광산 말풍선 속 해달 · 낚시터 해달이 한꺼번에 바뀐다.
원본은 처음 돌릴 때 ArtSource/Otter/WorkOriginal에 남겨 두고, 다시 돌리면 늘 원본에서 새로 칠한다.

    python Tools/OtterCast/work_recolor.py [--root <프로젝트>] [--preview]
"""
import json, math, os, shutil, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ORIGINAL = os.path.join(ROOT, 'ArtSource', 'Otter', 'WorkOriginal')
OUTLINE = (58, 28, 18)

# 캐릭터: 모습(cast.json id), 시트(경로, 칸 폭, 칸 높이, 줄마다 보는 방향), 바꿀 색 영역 → cast.json 색 키
WORKERS = {
    'Miner': {
        'sheets': [
            ('Assets/Sprites/Characters/MinerOtter/MinerOtter_Mine.png', 308, 276, ['side_r']),
            ('Assets/Sprites/Characters/MinerOtter/MinerOtter_Walk.png', 212, 272, ['side_r', 'side_l', 'front', 'back']),
        ],
        'hat': 'helmet',
        'clothes': 'navy',
    },
    'Fisher': {
        'sheets': [
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_Walk.png', 428, 276, ['side_r', 'side_l', 'front', 'back']),
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_Cast.png', 428, 276, ['side_r']),
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_Bite.png', 428, 276, ['side_r']),
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_Pull.png', 428, 276, ['side_r']),
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_CatchFish.png', 428, 276, ['side_r']),
            ('Assets/Sprites/Characters/FishingOtter/FishingOtter_CatchTrash.png', 428, 276, ['side_r']),
        ],
        'hat': 'bucket',
        'clothes': 'vest',
    },
}

KEEP, FUR, HAT, CLOTHES = 0, 1, 2, 3


def hsv_of(rgb):
    h, s, v = cv2.cvtColor(np.array([[rgb]], np.float32) / 255, cv2.COLOR_RGB2HSV)[0, 0]
    return float(h), float(s), float(v)


def classify(rgba, hat_kind, clothes_kind):
    hv = cv2.cvtColor(rgba[..., :3].astype(np.float32) / 255, cv2.COLOR_RGB2HSV)
    h, s, v = hv[..., 0], hv[..., 1], hv[..., 2]
    solid = rgba[..., 3] > 8
    dark = v < 0.22
    white = (s < 0.16) & (v > 0.55)
    pinkish = (h <= 8.5) | (h >= 340)
    blush = pinkish & (v >= 0.92) & (s > 0.2) & (s < 0.55)
    mouth = pinkish & (s >= 0.6) & (v >= 0.55)
    keep = ~solid | dark | white | blush | mouth
    label = np.zeros(h.shape, np.uint8)
    label[((h <= 31) | (h >= 330)) & (s >= 0.12) & ~keep] = FUR
    if hat_kind == 'helmet':
        hat = (h > 31) & (h <= 62) & (s >= 0.42) & (v >= 0.35)
    else:  # 벙거지: 카키 (바지도 비슷한 색이라 위쪽 가장 큰 덩어리만)
        hat = (h > 31) & (h <= 66) & (s >= 0.12) & (s < 0.62) & (v >= 0.3)
    hat &= ~keep
    comps, n = ndimage.label(hat)
    if n:
        sizes = ndimage.sum(hat, comps, range(1, n + 1))
        tops = ndimage.minimum(np.indices(hat.shape)[0], comps, range(1, n + 1))
        # 모자 = 가장 큰 덩어리와, 그보다 위쪽에 있는 덩어리(귀에 잘린 조각)
        big = 1 + int(np.argmax(sizes))
        big_bottom = ndimage.maximum(np.indices(hat.shape)[0], comps, big)
        for i in range(1, n + 1):
            if i == big or (tops[i - 1] < big_bottom and sizes[i - 1] > 30):
                label[comps == i] = HAT
    if clothes_kind == 'navy':
        clothes = (h >= 190) & (h <= 260) & (s >= 0.22) & (v >= 0.2)
    else:  # 조끼: 청록 · 회녹색
        clothes = (h >= 100) & (h <= 185) & (s >= 0.06) & (v >= 0.25) & (v <= 0.8)
    label[clothes & ~keep & (label == KEEP)] = CLOTHES
    return label, hv


def recolor(rgba, label, hv, targets, refs):
    out = rgba.copy()
    h, s, v = hv[..., 0], hv[..., 1], hv[..., 2]
    for cls, target in targets.items():
        mask = label == cls
        if not mask.any() or target is None:
            continue
        rh, rs, rv = refs[cls]
        th, ts, tv = hsv_of(target)
        dh = (h[mask] - rh + 180) % 360 - 180
        nh = (th + dh * 0.5) % 360
        ns = np.clip(s[mask] * (ts / max(rs, 0.05)), 0, 1)
        nv = v[mask] * (tv / rv) if tv <= rv else v[mask] + (tv - rv)
        nv = np.clip(nv, 0, 1)
        rgb = cv2.cvtColor(np.stack([nh, ns, nv], -1).astype(np.float32)[None], cv2.COLOR_HSV2RGB)[0]
        out[..., :3][mask] = np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8)
    return out


def fish_pin(d, cx, cy, r, color):
    w = max(2, int(r * 0.17))
    d.polygon([(cx + r * 0.45, cy), (cx + r * 1.05, cy - r * 0.5), (cx + r * 1.05, cy + r * 0.5)],
              fill=tuple(int(c * 0.85) for c in color), outline=OUTLINE, width=w)
    d.ellipse([cx - r, cy - r * 0.55, cx + r * 0.6, cy + r * 0.55], fill=color, outline=OUTLINE, width=w)
    d.ellipse([cx - r * 0.62, cy - r * 0.2, cx - r * 0.38, cy + r * 0.04], fill=OUTLINE)


def add_pin(cell, label, view, color):
    """낚시꾼: 모자 옆에 물고기 핀 (뒷모습 빼고)"""
    if view == 'back':
        return cell
    ys, xs = np.nonzero(label == HAT)
    if len(xs) < 80:
        return cell
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    w = x1 - x0
    # 벙거지 모자의 몸통 쪽 (챙 아래로 내려가면 이마에 붙어 보임)
    px = x0 + w * (0.70 if view == 'front' else 0.42 if view == 'side_r' else 0.58)
    py = y0 + (y1 - y0) * 0.40
    scale = 2
    layer = Image.new('RGBA', (cell.shape[1] * scale, cell.shape[0] * scale), (0, 0, 0, 0))
    fish_pin(ImageDraw.Draw(layer), px * scale, py * scale, w * 0.12 * scale, tuple(color))
    layer = layer.resize((cell.shape[1], cell.shape[0]), Image.LANCZOS)
    img = Image.fromarray(cell)
    img.alpha_composite(layer)
    return np.array(img)


def sheet_refs(sheets, hat_kind, clothes_kind):
    """시트 전체에서 영역마다 가운데 색 (같은 캐릭터는 같은 기준으로 칠해야 칸마다 색이 안 튐)"""
    acc = {FUR: [], HAT: [], CLOTHES: []}
    for path, _, _, _ in sheets:
        rgba = np.array(Image.open(path).convert('RGBA'))
        label, hv = classify(rgba, hat_kind, clothes_kind)
        for cls in acc:
            m = label == cls
            if m.any():
                acc[cls].append(hv[m][::5])
    refs = {}
    for cls, parts in acc.items():
        allpx = np.concatenate(parts) if parts else np.array([[0, 0.3, 0.6]], np.float32)
        refs[cls] = (float(np.median(allpx[:, 0])), float(np.median(allpx[:, 1])), float(np.median(allpx[:, 2])))
    return refs


def main():
    args = sys.argv[1:]
    root = ROOT
    if '--root' in args:
        root = args[args.index('--root') + 1]
    cast = {c['id']: c for c in json.load(open(os.path.join(HERE, 'cast.json'), encoding='utf-8'))}
    os.makedirs(ORIGINAL, exist_ok=True)
    previews = []
    for look_id, worker in WORKERS.items():
        look = cast[look_id]
        originals = []
        for rel, cw, ch, views in worker['sheets']:
            orig = os.path.join(ORIGINAL, os.path.basename(rel))
            if not os.path.exists(orig):
                shutil.copyfile(os.path.join(ROOT, rel), orig)  # 처음 한 번: 원본 남기기
            originals.append((orig, cw, ch, views))
        refs = sheet_refs(originals, worker['hat'], worker['clothes'])
        targets = {FUR: look.get('fur'), HAT: look.get('hat'), CLOTHES: look.get('overall')}
        pin = next((x['color'] for x in look.get('hatwear', []) if x['kind'] == 'fish'), None)
        for (orig, cw, ch, views), (rel, _, _, _) in zip(originals, worker['sheets']):
            sheet = np.array(Image.open(orig).convert('RGBA'))
            out = sheet.copy()
            for r, view in enumerate(views):
                for c in range(sheet.shape[1] // cw):
                    y, x = r * ch, c * cw
                    cell = sheet[y:y + ch, x:x + cw]
                    if cell[..., 3].max() == 0:
                        continue
                    label, hv = classify(cell, worker['hat'], worker['clothes'])
                    done = recolor(cell, label, hv, targets, refs)
                    if pin is not None:
                        done = add_pin(done, label, view, pin)
                    out[y:y + ch, x:x + cw] = done
            img = Image.fromarray(out)
            img.save(os.path.join(root, rel), optimize=True)
            previews.append(img.crop((0, 0, min(img.width, cw * 4), ch)))
            print('recolored', rel)
    if '--preview' in args:
        w = max(p.width for p in previews)
        sheet = Image.new('RGBA', (w, sum(p.height for p in previews)), (70, 70, 70, 255))
        y = 0
        for p in previews:
            sheet.alpha_composite(p, (0, y))
            y += p.height
        out = os.path.join(root, 'work_preview.png')
        sheet.save(out)
        print(out)


if __name__ == '__main__':
    main()
