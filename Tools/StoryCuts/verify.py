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
    # Compare the displayed hero to the actual configured plaza prefab, not a legacy assignment.
    hero = (DATA / "Settlement/Otters/Otter_First.asset").read_text(encoding="utf-8-sig")
    prefab_id = reference(hero, "plazaPrefab")
    prefab = next(p for p in (ROOT / "Assets/Prefabs/Plaza").glob("*.prefab") if guid(p) == prefab_id)
    rig_folder = re.search(r"^  resourceFolder: (.+)$", prefab.read_text(encoding="utf-8-sig"), re.M)[1]
    assert rig_folder == "HeroRigV1", "The configured hero and portrait generator must use the same rig"
    hero_guid = reference(hero, "portrait")
    for path in (portraits[hero_guid], ROOT / "Assets/Art/Otter/ICON_Otter_Cast_Explorer.png",
                 ROOT / "Assets/Art/Settlement/ICON_Otter_Snack.png"):
        with Image.open(path) as image:
            expected = build.rig_portrait(rig_folder, image.size)
            assert image.convert("RGBA").tobytes() == expected.tobytes(), f"Legacy hero image: {path}"
    ui = (ROOT / "Assets/Resources/GlobalUI.prefab").read_text(encoding="utf-8-sig")
    blocks = re.split(r"(?=^--- !u!)", ui, flags=re.M)
    topbar = next(b for b in blocks if "guid: f4f52034c887ee248bf8042f66d8364f," in b)
    portrait_id = re.search(r"  _portrait: \{fileID: (\d+)\}", topbar)[1]
    portrait_image = next(b for b in blocks if re.match(r"--- !u!\d+ &" + portrait_id + r"\b", b))
    assert re.search(r"  m_Sprite: .*guid: (\w+)", portrait_image)[1] == hero_guid
    tutorial = (ROOT / "Assets/Resources/TutorialStyle.asset").read_text(encoding="utf-8-sig")
    assert reference(tutorial, "guidePortrait") == hero_guid
    print(f"Otter art: {entries} stories, {cuts} rig-rendered cuts, matching transparent detail/profile portraits passed")
    print("Hero UI: actual plaza rig, initial profile icon, tutorial portrait and legacy image aliases passed")


if __name__ == "__main__":
    main()
