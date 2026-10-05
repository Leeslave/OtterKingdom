"""Exports every otter of cast_rig.CAST in the front-rig format (export_front.export_rig):
parts/, atlas, rig_layout.json, parts_overview.png per otter, plus cast.json (all
recipes) and cast_lineup.png.

Output: ArtSource/Otter/FrontRig/Cast/
Usage: python export_cast.py
"""
import json
import os
from PIL import Image, ImageDraw, ImageFont

import cast_rig as cr
import cut_parts as cp
import export_front as ef
import rig

OUT = os.path.join(cp.ROOT, 'ArtSource', 'Otter', 'FrontRig', 'Cast')
SRC = 'ArtSource/Otter/FrontRig/Source/{}_Front.png'
PREFIX = {n: ef.fr.CHARS[n]['prefix'] for n in ef.fr.CHARS}


def main(lineup_only=False):
    os.makedirs(OUT, exist_ok=True)
    if lineup_only:
        lineup({o['prefix']: cr.build_otter(o) for o in cr.CAST})
        return
    built = {}
    for o in cr.CAST:
        b = cr.build_otter(o)
        built[o['prefix']] = b
        src = SRC.format(PREFIX[o['head']]) + ' (head) + ' + SRC.format(PREFIX[o['body']]) + ' (outfit)'
        ef.export_rig(os.path.join(OUT, o['prefix']), o['prefix'], f"{o['name']} — {o['role']}", o['prefix'],
                      b['ims'], b['bones'], b['alts'], b['s'], src)
    keys = ('prefix', 'name', 'role', 'head', 'body', 'prop', 'eyes', 'fur', 'hat', 'colors', 'hair_front',
            'hair_back', 'hair', 'tie', 'items', 'body_items', 'icol', 'action')
    recipes = [{k: o[k] for k in keys} for o in cr.CAST]
    with open(os.path.join(OUT, 'cast.json'), 'w', encoding='utf-8') as f:
        json.dump(recipes, f, ensure_ascii=False, indent=2)
    lineup(built)


def lineup(built):
    font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20)
    small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    cols, cw, ch = 5, 300, 380
    rows = (len(cr.CAST) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cw, rows * ch), (250, 247, 240))
    d = ImageDraw.Draw(sheet)
    for i, o in enumerate(cr.CAST):
        r = cr.make_rig(built[o['prefix']])
        pose = cr.idle(o, 0.0)
        pose.get('Head', {}).pop('alt', None)   # eyes open for the line-up
        im = rig.to_pil(r.render(pose, (cw, ch - 50), (cw // 2, ch - 62), k=0.55))
        x, y = (i % cols) * cw, (i // cols) * ch
        sheet.paste(im, (x, y), im)
        d.text((x + cw // 2, y + ch - 52), f"{i + 1}. {o['name']}", font=font, fill=(70, 45, 35), anchor='ma')
        d.text((x + cw // 2, y + ch - 26), o['role'], font=small, fill=(140, 110, 90), anchor='ma')
    sheet.save(os.path.join(OUT, 'cast_lineup.png'))


if __name__ == '__main__':
    import sys
    main(lineup_only='lineup' in sys.argv[1:])
