"""Mining props for the 광부 해달's rig (Codex's MinerRigV1): what QuarterRigView's Loop.Mine needs.

    python Tools/MiningProps/build.py                 # MinerRigV1
    python Tools/MiningProps/build.py --rig <folder>

Game art as it is: the settlement's rock in its three crack stages and its rubble (Art/Settlement/
Prop_Rock_0..2, Prop_Rock_Rubble; each blow of the pickaxe cracks it a stage, the last breaks it) and the
stone chip effect (FX_StoneChip). Copied with their own import settings (pivot at the rock's base) as
rock_0..2, rock_rubble and stone_chip; the pickaxe is part of the rig's arm. Also turns on workWhenIdle
in the plaza prefab, so it mines while standing about. Rerun after rebuilding the rig.
"""
import argparse
import pathlib
import re
import shutil
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Settlement"
PROPS = {"rock_0": "Prop_Rock_0", "rock_1": "Prop_Rock_1", "rock_2": "Prop_Rock_2", "rock_rubble": "Prop_Rock_Rubble",
         "stone_chip": "FX_StoneChip"}
PREFABS = {"MinerRigV1": ROOT / "Assets/Prefabs/Plaza/PlazaOtter_MinerRigged.prefab"}


def copy(src, dst):
    shutil.copyfile(src, dst)
    meta = dst.with_name(dst.name + ".meta")
    old = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
    text = src.with_name(src.name + ".meta").read_text(encoding="utf-8").replace("\r\n", "\n")
    text = re.sub(r"^guid: \w+", f"guid: {old.group(1) if old else uuid.uuid4().hex}", text, flags=re.M)
    meta.write_text(text, encoding="utf-8", newline="\r\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rig", default="MinerRigV1", help="folder under Assets/Resources")
    out = ROOT / "Assets/Resources" / parser.parse_args().rig
    for name, source in PROPS.items():
        copy(ART / f"{source}.png", out / f"{name}.png")
    prefab = PREFABS.get(out.name)
    if prefab is not None:
        text = prefab.read_text(encoding="utf-8")
        if "workWhenIdle" not in text:
            nl = "\r\n" if "\r\n" in text else "\n"
            line = f"  walkAnimReferenceSpeed: 1.5{nl}"
            text = text.replace(line, line + f"  workWhenIdle: 1{nl}", 1)
            prefab.write_text(text, encoding="utf-8", newline="")
    print(f"{out.relative_to(ROOT)}: {', '.join(PROPS)}")


if __name__ == "__main__":
    main()
