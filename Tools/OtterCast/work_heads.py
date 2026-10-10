"""광산 · 낚시터에서 일하는 해달(곡괭이질 · 낚싯대) 그림에 광장 새 모습의 머리(농부 얼굴 · 모자 · 소품)를 얹는다.

농부 그림에는 곡괭이질 · 낚시 동작이 없어서 몸과 동작은 원래 일하는 시트를 쓰고(색은 work_recolor.py처럼 새 모습 색으로),
머리만 새 모습(farmer_cast.py가 만든 농부 바탕 칸)에서 잘라 칸마다 옛 머리 자리에 맞춰 붙인다.
옛 머리 자리 = 옛 모자 덩어리(광부 헬멧 · 낚시 벙거지)의 가운데와 위, 아래는 옷이 시작하는 곳.
원본은 ArtSource/Otter/WorkOriginal (work_recolor.py가 남김). 시트를 그 자리에서 바꿈.

    python Tools/OtterCast/work_heads.py [--root <프로젝트>] [--preview]
"""
import json, os, sys
import numpy as np
import cv2
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import farmer_cast as fc          # noqa: E402
import work_recolor as wr         # noqa: E402

ROOT = fc.ROOT


def cast_heads(look):
    """새 모습의 머리 그림: 방향마다 (RGBA, 목 줄 y). 농부 본 시트의 쉬는 · 걷는 칸에서 옷 위쪽만"""
    sheet = np.array(Image.open(os.path.join(fc.SRC, 'FarmerOtter_Sheet.png')).convert('RGBA'))
    picks = {'side_r': (4, 0), 'side_l': (1, 0), 'front': (2, 0), 'back': (3, 0)}
    heads = {}
    for view, (row, col) in picks.items():
        cell = sheet[row * fc.CELL_H:(row + 1) * fc.CELL_H, col * fc.CELL_W:(col + 1) * fc.CELL_W]
        out = fc.make_cell(cell, look, view)
        label, _ = fc.classify(cell)
        oy, ox = np.nonzero(label == fc.OVERALL)
        neck = int(np.percentile(oy, 2)) if len(oy) else int(fc.CELL_H * 0.55)
        hy, hx = np.nonzero((label == fc.HAT) | (label == fc.BAND))
        head = out.copy()
        head[neck:, :, 3] = 0
        # 목 줄 아래 몇 칸은 흐리게 (붙인 자리 이음매)
        fade = 8
        for i in range(fade):
            y = neck - fade + i
            if 0 <= y < head.shape[0]:
                head[y, :, 3] = (head[y, :, 3].astype(np.float32) * (1 - (i + 1) / (fade + 1))).astype(np.uint8)
        heads[view] = {
            'img': head,
            'neck': neck,
            'hat': (hx.min(), hy.min(), hx.max(), hy.max()),
        }
    return heads


def old_head(cell, worker):
    """옛 머리 자리: (모자 가운데 x, 모자 위 y, 모자 폭, 목 y) — 못 찾으면 None"""
    label, _ = wr.classify(cell, worker['hat'], worker['clothes'])
    # 모자는 캐릭터 위쪽 45% 안에서만 (낚시꾼 바지도 카키색)
    ay, _ = np.nonzero(cell[..., 3] > 8)
    top_limit = ay.min() + (ay.max() - ay.min()) * 0.45
    hat = label == wr.HAT
    hat[int(top_limit):] = False
    comps, n = ndimage.label(hat)
    if n == 0:
        return None
    sizes = ndimage.sum(hat, comps, range(1, n + 1))
    big = 1 + int(np.argmax(sizes))
    # 헬멧은 고글 · 등에 잘려 여러 조각: 가장 큰 조각과 가로로 겹치고 그보다 위에 있는 조각도 모자
    bys, bxs = np.nonzero(comps == big)
    keep = comps == big
    for i in range(1, n + 1):
        if i == big or sizes[i - 1] < 20:
            continue
        ys, xs = np.nonzero(comps == i)
        if xs.max() >= bxs.min() and xs.min() <= bxs.max() and ys.min() <= bys.max():
            keep |= comps == i
    hat = keep
    hy, hx = np.nonzero(hat)
    if len(hx) < 80:
        return None
    x0, x1, y0, y1 = hx.min(), hx.max(), hy.min(), hy.max()
    # 목: 모자 아래에서 옷(멜빵바지 · 조끼 · 줄무늬)이 처음 나오는 줄
    hv = cv2.cvtColor(cell[..., :3].astype(np.float32) / 255, cv2.COLOR_RGB2HSV)
    stripes = (hv[..., 0] >= 195) & (hv[..., 0] <= 240) & (hv[..., 1] >= 0.25) & (cell[..., 3] > 8)
    clothes = (label == wr.CLOTHES) | stripes
    cy, cx = np.nonzero(clothes[y1:, max(0, x0 - 20):x1 + 20])
    neck = y1 + (int(np.percentile(cy, 3)) if len(cy) > 30 else int((y1 - y0) * 1.1))
    # 지울 옛 모자: 모자 조각을 메워(고글 · 띠 포함) 테두리까지 조금 넓힘
    erase = ndimage.binary_fill_holes(ndimage.binary_closing(hat, iterations=3))
    erase = ndimage.binary_dilation(erase, iterations=3)
    return (x0 + x1) / 2, y0, x1 - x0, neck, erase


def paste_head(cell, original, worker, heads, view, size_mul):
    found = old_head(original, worker)  # 옛 머리 자리는 다시 칠하기 전 그림에서 (색 규칙이 원래 색 기준)
    if found is None:
        return cell
    cx, top, hat_w, neck, erase = found
    head = heads[view]
    hx0, hy0, hx1, hy1 = head['hat']
    # 새 머리 높이(모자 꼭대기 → 목)를 옛 머리 높이에 맞춤
    old_h = max(10, neck - top)
    new_h = head['neck'] - hy0
    # 높이와 모자 폭 중 큰 쪽에 맞춤 (웅크린 칸은 머리가 낮고 넓어 높이만 맞추면 옛 얼굴이 옆으로 보임)
    scale = max(old_h / new_h, hat_w / max(1, hx1 - hx0) * 0.95) * size_mul
    img = Image.fromarray(head['img'])
    crop = img.crop((0, hy0, img.width, head['neck']))
    # 칸 밖으로 나가지 않게 (광부 걷기 칸은 좁고 뒷모습은 챙이 넓음)
    bbox = crop.getbbox()
    if bbox:
        span = max(abs(bbox[0] - (hx0 + hx1) / 2), abs(bbox[2] - (hx0 + hx1) / 2))
        room = min(cx, cell.shape[1] - cx) - 2
        if span * scale > room > 0:
            scale = room / span
    crop = crop.resize((max(1, int(crop.width * scale)), max(1, int(crop.height * scale))), Image.LANCZOS)
    new_cx = ((hx0 + hx1) / 2) * scale
    ox = int(round(cx - new_cx))
    oy = int(round(neck - crop.height))
    cleared = cell.copy()
    cleared[erase, 3] = 0  # 옛 모자는 지우고 (새 머리보다 넓은 부분이 삐져나오지 않게)
    base = Image.fromarray(cleared)
    layer = Image.new('RGBA', base.size, (0, 0, 0, 0))
    layer.paste(crop, (ox, oy), crop)
    base.alpha_composite(layer)
    return np.array(base)


def process(look, worker, heads, sheet_img, cw, ch, views, size_mul, recolor_refs):
    sheet = np.array(sheet_img)
    out = sheet.copy()
    targets = {wr.FUR: look.get('fur'), wr.HAT: look.get('hat'), wr.CLOTHES: look.get('overall')}
    for r, view in enumerate(views):
        for c in range(sheet.shape[1] // cw):
            y, x = r * ch, c * cw
            cell = sheet[y:y + ch, x:x + cw]
            if cell[..., 3].max() == 0:
                continue
            label, hv = wr.classify(cell, worker['hat'], worker['clothes'])
            body = wr.recolor(cell, label, hv, targets, recolor_refs)
            out[y:y + ch, x:x + cw] = paste_head(body, cell, worker, heads, view, size_mul)
    return Image.fromarray(out)


SIZE = {'Miner': 1.08, 'Fisher': 1.05}  # 옛 머리 테두리가 안 삐져나오게 조금 크게


def main():
    args = sys.argv[1:]
    root = ROOT
    if '--root' in args:
        root = args[args.index('--root') + 1]
    cast = {c['id']: c for c in json.load(open(os.path.join(HERE, 'cast.json'), encoding='utf-8'))}
    previews = []
    for look_id, worker in wr.WORKERS.items():
        look = cast[look_id]
        heads = cast_heads(look)
        originals = [(os.path.join(wr.ORIGINAL, os.path.basename(rel)), cw, ch, views) for rel, cw, ch, views in worker['sheets']]
        for orig, _, _, _ in originals:
            if not os.path.exists(orig):
                raise SystemExit(f'원본이 없습니다: {orig} (work_recolor.py를 먼저 한 번)')
        refs = wr.sheet_refs(originals, worker['hat'], worker['clothes'])
        for (orig, cw, ch, views), (rel, _, _, _) in zip(originals, worker['sheets']):
            img = process(look, worker, heads, Image.open(orig).convert('RGBA'), cw, ch, views, SIZE[look_id], refs)
            img.save(os.path.join(root, rel), optimize=True)
            previews.append(img.crop((0, 0, min(img.width, cw * 4), ch)))
            print('heads', rel)
    if '--preview' in args:
        w = max(p.width for p in previews)
        sheet = Image.new('RGBA', (w, sum(p.height for p in previews)), (70, 70, 70, 255))
        y = 0
        for p in previews:
            sheet.alpha_composite(p, (0, y))
            y += p.height
        sheet.save(os.path.join(root, 'heads_preview.png'))


if __name__ == '__main__':
    main()
