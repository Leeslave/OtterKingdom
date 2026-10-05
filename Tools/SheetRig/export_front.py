"""Exports the front-view rigs (front_rig): per character one PNG per part (incl. the
blinking head), a packed atlas, a layout JSON (atlas rect + Unity pivot + bone parent
+ sort order + rest position + sprite swaps) and a labelled overview sheet.

Output: ArtSource/Otter/FrontRig/<Prefix>/
Usage: python export_front.py
"""
import json
import os
from PIL import Image, ImageDraw, ImageFont

import cut_parts as cp
import front_rig as fr

ROOT_OUT = os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'FrontRig')
PAD = 4
ATLAS_W = 1024
KOR = {'Head': '머리', 'Head_Blink': '머리 (눈 감음)', 'Body': '몸통', 'Arm_L': '팔 L', 'Arm_R': '팔 R',
       'Leg_L': '다리 L', 'Leg_R': '다리 R', 'Pompom': '방울', 'Rod': '낚싯대', 'Bobber': '찌', 'Scarf': '스카프'}


def export(name):
    spec = fr.CHARS[name]
    parts, s = fr.cut(name)
    ims = fr.to_images(parts)
    bones, _, alts = fr.build(name, (ims, s))
    export_rig(os.path.join(ROOT_OUT, spec['prefix']), spec['prefix'], spec['name'], name, ims, bones, alts, s,
               f"ArtSource/Otter/FrontRig/Source/{spec['prefix']}_Front.png")


def export_rig(out, prefix, title, character, ims, bones, alts, s, source):
    """Writes parts/, the atlas, rig_layout.json and parts_overview.png for one rig.
    ims: {part: PIL} on one canvas (working px); bones: rig.Bone list (rig units)."""
    os.makedirs(os.path.join(out, 'parts'), exist_ok=True)
    root = next(b for b in bones if b.name == 'Root').pivot
    k = fr.rig.S   # bone pivots are in rig units; images are in working px

    entries = []
    sprite_of = {b.part: b for b in bones if b.part}
    for pname, im in ims.items():
        b = sprite_of.get(pname) or sprite_of[pname.split('_')[0]]   # Head_Blink hangs on Head
        x0, y0, x1, y1 = im.getbbox()
        x0, y0, x1, y1 = x0 - 2, y0 - 2, x1 + 2, y1 + 2
        crop = im.crop((x0, y0, x1, y1))
        crop.save(os.path.join(out, 'parts', pname + '.png'))
        w, h = crop.size
        px, py = b.pivot[0] * k, b.pivot[1] * k
        entries.append(dict(name=pname, bone=b.name, parent=b.parent, order=b.layer, image=crop,
                            pivot=[round((px - x0) / w, 4), round(1 - (py - y0) / h, 4)],
                            position=[round((b.pivot[0] - root[0]) * k, 1), round((root[1] - b.pivot[1]) * k, 1)]))

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
    atlas_file = f"{prefix}_FrontRigParts.png"
    atlas.save(os.path.join(out, atlas_file))

    layout = dict(
        character=character,
        source=source,
        note='Front view only. rect = atlas rect with Unity bottom-left origin. pivot = normalized joint '
             'position (Unity convention). position = joint relative to the feet centre (Root), px, y up. '
             'Parents are listed before children; order = sorting order. swaps = alternative sprites for a '
             'bone (Head_Blink for blinking).',
        scale=round(s, 4),
        atlas=dict(file=atlas_file, width=ATLAS_W, height=atlas_h),
        bones=[dict(name=b.name, parent=b.parent, part=b.part,
                    position=[round((b.pivot[0] - root[0]) * k, 1), round((root[1] - b.pivot[1]) * k, 1)])
               for b in bones],
        parts=[dict(name=e['name'], bone=e['bone'], parent=e['parent'], order=e['order'],
                    rect=[e['xy'][0], atlas_h - e['xy'][1] - e['image'].height, *e['image'].size],
                    pivot=e['pivot'], position=e['position']) for e in entries],
        swaps={bone: {key: f'{bone}_Blink' if key == 'blink' else key for key in ks} for bone, ks in alts.items()},
    )
    with open(os.path.join(out, 'rig_layout.json'), 'w', encoding='utf-8') as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)
    overview(entries, out, title)
    print(f'{prefix}: {len(entries)} parts -> {out} (atlas {ATLAS_W}x{atlas_h})')


def overview(entries, out, title):
    font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    es = sorted(entries, key=lambda e: -e['order'])
    h = max(e['image'].height for e in es) + 70
    w = sum(e['image'].width + 24 for e in es) + 24
    sheet = Image.new('RGBA', (w, h), (250, 247, 240, 255))
    d = ImageDraw.Draw(sheet)
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
        sheet.paste(ck, (x, top))
        px = x + e['pivot'][0] * im.width
        py = top + (1 - e['pivot'][1]) * im.height
        d.ellipse([px - 6, py - 6, px + 6, py + 6], fill=(255, 205, 50), outline=(80, 50, 30), width=2)
        d.text((x + im.width / 2, top + im.height + 4), KOR.get(e['name'], e['name']), font=small,
               fill=(90, 60, 45), anchor='ma')
        x += im.width + 24
    d.text((24, 8), title, font=font, fill=(70, 45, 35))
    sheet.convert('RGB').save(os.path.join(out, 'parts_overview.png'))


if __name__ == '__main__':
    for n in fr.CHARS:
        export(n)
