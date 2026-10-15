"""Put a new musician part sheet into the rig, where build.cjs can't run (its crop boxes fit the first sheet).

    python Tools/MusicianRig/swap_atlas.py      # reads ArtSource/Otter/MusicianRig/V1/atlas.png

The sheet is Codex's layout: two rows (front, rear) of six parts - head, body, free arm, arm with the
guitar, leg, tail. Each part is found as its own island (faint glow between parts ignored), trimmed, and
stretched to fill the rig's existing sprite of that part (the size build.cjs gave it from the bones), so
pivots, metas and rig.json stay as they are. The rear tail is mirrored, like build.cjs does.
Also writes the parts to ArtSource/Otter/MusicianRig/V1/parts. Rerun make_sleep_parts.py afterwards.
"""
import pathlib

import cv2
import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/Otter/MusicianRig/V1"
OUT = ROOT / "Assets/Resources/MusicianRigV1"
TYPES = ["head", "body", "arm", "guitar", "leg", "tail"]
SPRITES = {"head": ["head"], "body": ["body"], "arm": ["farArm"], "guitar": ["nearArm"], "leg": ["nearLeg", "farLeg"], "tail": ["tail"]}


def parts(atlas):
    """{(row, type): RGBA} - the twelve parts, left to right in each row."""
    alpha = atlas[..., 3]
    n, labels, stats, _ = cv2.connectedComponentsWithStats((alpha >= 128).astype(np.uint8), 8)
    islands = [i for i in range(1, n) if stats[i, 4] > 3000]
    h = atlas.shape[0]
    out = {}
    for row in (0, 1):
        mine = [i for i in islands if (stats[i, 1] + stats[i, 3] / 2 >= h / 2) == bool(row)]
        mine.sort(key=lambda i: stats[i, 0])
        assert len(mine) == 6, f"row {row}: {len(mine)} parts"
        for kind, i in zip(TYPES, mine):
            keep = cv2.dilate((labels == i).astype(np.uint8), np.ones((7, 7), np.uint8)).astype(bool)
            piece = atlas.copy()
            piece[..., 3] = np.where(keep, piece[..., 3], 0)
            piece[..., 3] = np.where(piece[..., 3] < 8, 0, piece[..., 3])
            x, y, w, hh = cv2.boundingRect((piece[..., 3] > 0).astype(np.uint8))
            out[(row, kind)] = piece[y:y + hh, x:x + w]
    return out


def main():
    atlas = np.array(Image.open(ART / "atlas.png").convert("RGBA"))
    (ART / "parts").mkdir(exist_ok=True)
    for (row, kind), image in parts(atlas).items():
        view = "rear" if row else "front"
        if row and kind == "tail":
            image = image[:, ::-1]
        Image.fromarray(image).save(ART / "parts" / f"{view}-{kind}.png")
        for name in SPRITES[kind]:
            dst = OUT / f"{view}-{name}.png"
            size = Image.open(dst).size
            Image.fromarray(image).resize(size, Image.LANCZOS).save(dst, optimize=True)
            print(dst.name, size)


if __name__ == "__main__":
    main()
