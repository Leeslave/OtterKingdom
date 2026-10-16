"""Exports the rig parts of every character: one PNG per part, a packed atlas, a
layout JSON (atlas rect + Unity pivot + bone parent + sort order + rest position)
and a labelled overview sheet, plus a lineup of all characters.

The farmer parts are cut from the sheet (cut_parts); the others are repainted
from them (variants), so all characters share the same bones and pivots.

Output: ArtSource/Otter/SheetRig/<Character>Otter/, ArtSource/Otter/SheetRig/cast_lineup.png
Usage: python export_parts.py
"""
import json
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

import anim
import cut_parts as cp
import rig
import variants

ROOT_OUT = os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'SheetRig')
# character -> output prefix (matches the existing Assets/Sprites/Characters folders)
PREFIX = {'Farmer': 'FarmerOtter', 'Chief': 'ChiefOtter', 'Miner': 'MinerOtter', 'Fisher': 'FishingOtter'}
PAD = 4
ATLAS_W = 1024
KOR = {'Head': '머리', 'Body': '몸통', 'Arm_L': '팔 L', 'Arm_R': '팔 R', 'Leg_L': '다리 L', 'Leg_R': '다리 R',
       'Tail': '꼬리', 'Arm_Near': '앞팔', 'Arm_Far': '뒷팔', 'Leg_Near': '앞다리', 'Leg_Far': '뒷다리',
       'Foot_Near': '앞발', 'Foot_Far': '뒷발'}
VIEW_KOR = {'Front': '정면', 'Side': '측면 (왼쪽은 좌우 반전)', 'Back': '뒷모습'}


def main():
    farmer = cp.cut_all()
    casts = {name: farmer if name == 'Farmer' else variants.build(name, farmer) for name in PREFIX}
    for name, parts in casts.items():
        export(name, parts)
    lineup(casts)


def export(name, parts):
    OUT = os.path.join(ROOT_OUT, PREFIX[name])
    os.makedirs(os.path.join(OUT, 'parts'), exist_ok=True)
    views = rig.build_views(parts)
    entries = []
    for view, (bones, images) in views.items():
        root = next(b for b in bones if b.name == 'Root').pivot
        for b in bones:
            if not b.part:
                continue
            im = images[b.part]
            x0, y0, x1, y1 = im.getbbox()
            x0, y0, x1, y1 = x0 - 2, y0 - 2, x1 + 2, y1 + 2
            crop = im.crop((x0, y0, x1, y1))
            pname = f'{view}_{b.part}'
            crop.save(os.path.join(OUT, 'parts', pname + '.png'))
            w, h = crop.size
            px, py = b.pivot
            entries.append(dict(
                name=pname, view=view, bone=b.name, parent=b.parent, order=b.layer, image=crop,
                pivot=[round((px - x0) / w, 4), round(1 - (py - y0) / h, 4)],
                # pivot position relative to the view's Root (feet centre), pixels, y up
                position=[round(px - root[0], 1), round(root[1] - py, 1)],
                tail_tip=None if not b.chain else [round(b.chain[0][0] * cp.SCALE - root[0], 1),
                                                    round(root[1] - b.chain[0][1] * cp.SCALE, 1)],
            ))

    # shelf-pack, tallest first
    x = y = PAD
    row_h = 0
    for e in sorted(entries, key=lambda e: -e['image'].height):
        w, h = e['image'].size
        if x + w + PAD > ATLAS_W:
            x, y, row_h = PAD, y + row_h + PAD, 0
        e['xy'] = (x, y)
        x += w + PAD
        row_h = max(row_h, h)
    atlas_h = (y + row_h + PAD + 3) // 4 * 4
    atlas = Image.new('RGBA', (ATLAS_W, atlas_h))
    for e in entries:
        atlas.paste(e['image'], e['xy'])
    atlas_file = f'{PREFIX[name]}_SheetRigParts.png'
    atlas.save(os.path.join(OUT, atlas_file))

    layout = dict(
        source='Assets/Sprites/Characters/FarmerOtter/FarmerOtter_Sheet.png',
        note='Pixels are 2x the sheet. rect = atlas rect with Unity bottom-left origin. '
             'pivot = normalized joint position (Unity convention). position = joint relative to the '
             'feet centre (Root), y up. Parents are listed before children; order = sorting order.',
        character=name,
        atlas=dict(file=atlas_file, width=ATLAS_W, height=atlas_h),
        # every bone incl. ones without a sprite (side view: Hip carries the body and both legs)
        bones=[dict(view=v, name=b.name, parent=b.parent, part=b.part,
                    position=[round(b.pivot[0] - root[0], 1), round(root[1] - b.pivot[1], 1)])
               for v, (bones, _) in views.items()
               for root in [next(x for x in bones if x.name == 'Root').pivot] for b in bones],
        parts=[dict(name=e['name'], view=e['view'], bone=e['bone'], parent=e['parent'], order=e['order'],
                    rect=[e['xy'][0], atlas_h - e['xy'][1] - e['image'].height, *e['image'].size],
                    pivot=e['pivot'], position=e['position'],
                    **({'tail_tip': e['tail_tip'], 'tail_bones': 3} if e['tail_tip'] else {}))
               for e in entries],
    )
    with open(os.path.join(OUT, 'rig_layout.json'), 'w', encoding='utf-8') as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)

    overview(entries, OUT)
    print(f'{name}: {len(entries)} parts -> {OUT} (atlas {ATLAS_W}x{atlas_h})')


def overview(entries, OUT):
    """Labelled sheet: one row per view, parts on a checkerboard with their pivot marked."""
    font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    rows = []
    for view in ('Front', 'Side', 'Back'):
        es = [e for e in entries if e['view'] == view]
        es.sort(key=lambda e: -e['order'])
        h = max(e['image'].height for e in es) + 60
        w = sum(e['image'].width + 24 for e in es) + 24
        row = Image.new('RGBA', (w, h), (250, 247, 240, 255))
        d = ImageDraw.Draw(row)
        x = 24
        for e in es:
            im = e['image']
            ck = Image.new('RGBA', im.size, (255, 255, 255, 255))
            cd = ImageDraw.Draw(ck)
            for yy in range(0, im.height, 10):
                for xx in range(0, im.width, 10):
                    if (xx // 10 + yy // 10) % 2:
                        cd.rectangle([xx, yy, xx + 9, yy + 9], fill=(228, 228, 228, 255))
            ck.alpha_composite(im)
            top = 40
            row.paste(ck, (x, top))
            px = x + e['pivot'][0] * im.width
            py = top + (1 - e['pivot'][1]) * im.height
            d.ellipse([px - 6, py - 6, px + 6, py + 6], fill=(255, 205, 50), outline=(80, 50, 30), width=2)
            d.text((x + im.width / 2, top + im.height + 4), KOR[e['bone']], font=small, fill=(90, 60, 45),
                   anchor='ma')
            x += im.width + 24
        d.text((24, 8), VIEW_KOR[view], font=font, fill=(70, 45, 35))
        rows.append(row)
    W = max(r.width for r in rows)
    sheet = Image.new('RGB', (W, sum(r.height for r in rows)), (250, 247, 240))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y), r)
        y += r.height
    sheet.save(os.path.join(OUT, 'parts_overview.png'))


CAST_KOR = {'Farmer': '농부', 'Chief': '촌장', 'Miner': '광부', 'Fisher': '낚시꾼'}


def lineup(casts):
    """All characters in front / side / back, standing."""
    font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 24)
    cw, ch = 250, 330
    sheet = Image.new('RGB', (cw * 3 * len(casts) + 40 * (len(casts) - 1), ch + 50), (250, 247, 240))
    d = ImageDraw.Draw(sheet)
    for i, (name, parts) in enumerate(casts.items()):
        views = rig.build_views(parts)
        x0 = i * (cw * 3 + 40)
        for j, v in enumerate(('Front', 'Side', 'Back')):
            r = rig.Rig(*views[v])
            im = rig.to_pil(r.render(anim.idle(v, 0), (832, 480), (416, 452), k=0.6)).crop((291, 140, 541, 470))
            sheet.paste(im, (x0 + j * cw, 40), im)
        d.text((x0 + cw * 1.5, 6), f'{CAST_KOR[name]} ({PREFIX[name]})', font=font, fill=(70, 45, 35), anchor='ma')
    sheet.save(os.path.join(ROOT_OUT, 'cast_lineup.png'))


if __name__ == '__main__':
    main()
