"""Photo props for the 사진가 해달's rig (Codex's PhotographerRigV1): what QuarterRigView's Loop.Photo needs.

    python Tools/PhotoProps/build.py                 # PhotographerRigV1
    python Tools/PhotoProps/build.py --rig CleanerRigV1     # the 청소부: its sweeping dust (FX_Dust -> sweep_dust)

Game art as it is, copied with its own import settings: the shutter flash is the twinkle on the soft glow
(Art/Settlement/FX_Twinkle, FX_GlowSoft -> flash_star, flash_glow) and the "got it!" pop over the head
the sparkle (FX_Sparkle -> sparkle). The camera is part of the rig's arm. Also turns on workWhenIdle in
the plaza prefab, so it takes photos now and then while standing about. Rerun after rebuilding the rig.
"""
import argparse
import pathlib
import re
import shutil
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/Art/Settlement"
# Per rig: its props (name in its folder -> game art) - the 사진가's flash, the 청소부's sweeping dust.
RIGS = {"PhotographerRigV1": {"flash_star": "FX_Twinkle", "flash_glow": "FX_GlowSoft", "sparkle": "FX_Sparkle"},
        "CleanerRigV1": {"sweep_dust": "FX_Dust"}}
PREFABS = {"PhotographerRigV1": ROOT / "Assets/Prefabs/Plaza/PlazaOtter_PhotographerRigged.prefab",
           "CleanerRigV1": ROOT / "Assets/Prefabs/Plaza/PlazaOtter_CleanerRigged.prefab"}


def copy(src, dst):
    shutil.copyfile(src, dst)
    meta = dst.with_name(dst.name + ".meta")
    old = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M) if meta.exists() else None
    text = src.with_name(src.name + ".meta").read_text(encoding="utf-8").replace("\r\n", "\n")
    text = re.sub(r"^guid: \w+", f"guid: {old.group(1) if old else uuid.uuid4().hex}", text, flags=re.M)
    meta.write_text(text, encoding="utf-8", newline="\r\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rig", default="PhotographerRigV1", help="folder under Assets/Resources")
    out = ROOT / "Assets/Resources" / parser.parse_args().rig
    PROPS = RIGS[out.name]
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
