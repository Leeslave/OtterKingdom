"""Verify every otter story uses the rig artwork and keeps its Unity sprite references."""
import re
from pathlib import Path

from PIL import Image

import build


ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/Scriptable Obejects"


def guid(path):
    return re.search(r"^guid: (.+)$", Path(str(path) + ".meta").read_text(encoding="utf-8-sig"), re.M)[1]


def field(text, name):
    return re.search(rf"^  _{name}: (.+)$", text, re.M)[1]


def reference(text, name):
    return re.search(r"guid: (\w+)", field(text, name))[1]


def main():
    portraits = {}
    for asset in (ROOT / "Assets/Art/Otter").glob("*.png"):
        portraits[guid(asset)] = asset
    otters = {}
    for asset in (DATA / "Settlement/Otters").glob("*.asset"):
        text = asset.read_text(encoding="utf-8-sig")
        otters[field(text, "otterId")] = reference(text, "portrait")
    entries = cuts = 0
    for entry in (DATA / "Collection/Entries").glob("*.asset"):
        text = entry.read_text(encoding="utf-8-sig")
        entry_id = field(text, "entryId")
        story = DATA / f"Collection/Stories/Story_{entry.stem}.asset"
        if not entry_id.startswith("otter_") or not story.exists():
            continue
        entries += 1
        assert reference(text, "story") == guid(story), entry_id
        portrait = reference(text, "portrait")
        assert portrait == otters[entry_id], f"Different detail/profile portrait: {entry_id}"
        with Image.open(portraits[portrait]) as image:
            assert image.mode == "RGBA" and image.getchannel("A").getextrema() == (0, 255), entry_id
        story_text = story.read_text(encoding="utf-8-sig")
        cut_section = story_text.split("  _cuts:", 1)[1].split("  _lines:", 1)[0]
        refs = re.findall(r"guid: (\w+)", cut_section)
        assert refs, entry_id
        for index, sprite in enumerate(refs):
            name = f"{entry_id}_{chr(65 + index)}"
            assert name in build.CUTS, f"Missing native art source: {name}"
            image_path = ROOT / f"Assets/Art/Collection/Cutscenes/CUT_{name}.png"
            assert sprite == guid(image_path), f"Stale story cut: {name}"
            with Image.open(image_path) as image:
                assert image.size == (1536, 1024), name
                image.verify()
            cuts += 1
        for index in re.findall(r"  - _cut: (\d+)", story_text):
            assert int(index) < len(refs), entry_id
    print(f"Otter art: {entries} stories, {cuts} rig-rendered cuts, matching transparent detail/profile portraits passed")


if __name__ == "__main__":
    main()
