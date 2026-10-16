"""Encode actual browser-rendered rear-direction frames into loop previews."""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--version', default='v1')
version = parser.parse_args().version

root = Path(__file__).resolve().parents[2]
out = root / 'ArtSource/Otter/QuarterRig/RearStudy'
sets = {}
for direction, folder in [('ne', 'quarter-rig-frames'), ('nw', 'quarter-rig-nw')]:
    frames = [Image.open(p).convert('RGB').resize((600, 444), Image.Resampling.LANCZOS)
              for p in sorted((root / 'tmp' / folder).glob('*.png'))]
    assert len(frames) == 24
    frames[0].save(out / f'walk-{direction}-{version}.gif', save_all=True,
                   append_images=frames[1:], duration=[30, 40] * 12, loop=0, optimize=False)
    sets[direction] = frames
combined = []
for left, right in zip(sets['nw'], sets['ne']):
    frame = Image.new('RGB', (900, 370), '#f5f3eb')
    frame.paste(left.resize((450, 333)), (0, 37))
    frame.paste(right.resize((450, 333)), (450, 37))
    draw = ImageDraw.Draw(frame)
    draw.text((20, 12), 'NW / rear three-quarter', fill='#283f35')
    draw.text((470, 12), 'NE / rear three-quarter', fill='#283f35')
    combined.append(frame)
combined[0].save(out / f'walk-nw-ne-{version}.gif', save_all=True, append_images=combined[1:],
                 duration=[30, 40] * 12, loop=0, optimize=False)
