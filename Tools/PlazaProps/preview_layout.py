# Renders Assets/Art/Plaza/plaza_layout.json over the ground image so the
# prop placement / footprints / walkable polygon can be tuned without Unity.
# Draw order matches the game: flat props, then everything else by base Y.
#
# Usage (from repo root): python Tools/PlazaProps/preview_layout.py [out.png] [--no-overlay]
from PIL import Image, ImageDraw
import json, sys

ART = 'Assets/Art/Plaza'
layout = json.load(open(f'{ART}/plaza_layout.json', encoding='utf-8'))
pivots = {p['name']: p for p in json.load(open(f'{ART}/Props/plaza_props_pivots.json'))['props']}
defs = {p['name']: p for p in layout['props']}
out_path = next((a for a in sys.argv[1:] if not a.startswith('--')), 'plaza_preview.png')
overlay = '--no-overlay' not in sys.argv

ground = Image.open(f'{ART}/Plaza_Ground.png').convert('RGBA')
canvas = ground.copy()
ov = Image.new('RGBA', canvas.size)
d = ImageDraw.Draw(ov)

def pairs(flat):
    return list(zip(flat[0::2], flat[1::2]))

def prop_to_ground(place, px, py):
    """Prop-PNG pixel -> ground pixel, honouring pivot, scale and flip."""
    piv, s = pivots[place['prop']], defs[place['prop']]['scale']
    ox = px - piv['pivotX'] * piv['width']
    oy = py - (1 - piv['pivotY']) * piv['height']
    if place['flipX']:
        ox = -ox
    return place['x'] + ox * s, place['y'] + oy * s

order = sorted(layout['placements'], key=lambda p: (not defs[p['prop']]['flat'], p['y']))
for place in order:
    piv, s = pivots[place['prop']], defs[place['prop']]['scale']
    im = Image.open(f"{ART}/Props/Prop_{place['prop']}.png").convert('RGBA')
    if place['flipX']:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    tl = prop_to_ground(place, piv['width'] if place['flipX'] else 0, 0)
    canvas.alpha_composite(im, (round(tl[0]), round(tl[1])))
    if overlay:
        fp = [prop_to_ground(place, x, y) for x, y in pairs(defs[place['prop']]['footprint'])]
        d.polygon(fp, fill=(255, 0, 0, 70), outline=(255, 0, 0, 255))
        d.ellipse([place['x'] - 3, place['y'] - 3, place['x'] + 3, place['y'] + 3], fill=(255, 0, 255, 255))

if overlay:
    d.polygon(pairs(layout['walkable']), outline=(0, 255, 0, 255))
    for b in layout['blocked']:
        d.polygon(pairs(b['points']), fill=(255, 0, 0, 70), outline=(255, 0, 0, 255))
    # Otter scale reference (1.67 x 1.2 world units) next to the focus point.
    ppu = layout['groundPixelsPerUnit']
    fx, fy = layout['initialFocus']
    d.rectangle([fx - 0.6 * ppu, fy - 1.67 * ppu, fx + 0.6 * ppu, fy], outline=(255, 255, 0, 255), width=2)
    canvas = Image.alpha_composite(canvas, ov)

canvas.save(out_path)
print('saved', out_path)
