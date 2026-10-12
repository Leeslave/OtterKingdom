"""Render the captured V11 cycle beside the saved V10 GIF."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
out = root / 'ArtSource/Otter/QuarterRig'
frames = [Image.open(p).convert('RGB').resize((600, 444), Image.Resampling.LANCZOS)
          for p in sorted((root / 'tmp/quarter-rig-frames').glob('*.png'))]
frames[0].save(out / 'walk-v11.gif', save_all=True, append_images=frames[1:],
               duration=[30, 40] * 12, loop=0, optimize=False)
old = Image.open(out / 'walk-v10.gif')
comparison = []
for i, frame in enumerate(frames):
    old.seek(i)
    canvas = Image.new('RGB', (900, 371), '#f5f3eb')
    canvas.paste(old.convert('RGB').resize((450, 333)), (0, 38))
    canvas.paste(frame.resize((450, 333)), (450, 38))
    draw = ImageDraw.Draw(canvas)
    draw.text((20, 13), 'V10 / previous', fill='#283f35')
    draw.text((470, 13), 'V11 / larger motion', fill='#283f35')
    comparison.append(canvas)
comparison[0].save(out / 'motion-comparison-v11.gif', save_all=True,
                   append_images=comparison[1:], duration=[30, 40] * 12,
                   loop=0, optimize=False)
