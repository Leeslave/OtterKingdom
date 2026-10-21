"""Every special action's props for the 주인공, copied from the otter that has them (new guids).

    python Tools/HeroRig/props.py

QuarterRigView turns on an action when its prop is in the rig folder: rod (뱃사람 - fishing), hammer + log + chip
(야구 해달 - building), rock_* + stone_chip (광부 - mining), flash_* + sparkle (사진가 - photos), sweep_dust (청소부 -
sweeping). The smiling head (ringing the bell) and the music note (playing when seated) are made from the 주인공's
own head by make_sleep_parts.py; the tool-holding arms by tool_arms.py.
"""
import pathlib
import re
import shutil
import subprocess
import sys
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[2]
RES = ROOT / "Assets/Resources"
HERO = RES / "HeroRigV1"
PROPS = {
    "FisherRigV1": ["rod"],
    "BaseballRigV1": ["hammer", "log", "chip"],
    "MinerRigV1": ["rock_0", "rock_1", "rock_2", "rock_rubble", "stone_chip"],
    "PhotographerRigV1": ["flash_star", "flash_glow", "sparkle"],
    "CleanerRigV1": ["sweep_dust"],
}


def main():
    for rig, names in PROPS.items():
        for name in names:
            src = RES / rig / f"{name}.png"
            shutil.copyfile(src, HERO / src.name)
            meta = src.with_name(src.name + ".meta").read_text(encoding="utf-8")
            dst = HERO / (src.name + ".meta")
            if not dst.exists():
                dst.write_text(re.sub(r"^guid: \w+", f"guid: {uuid.uuid4().hex}", meta, flags=re.M), encoding="utf-8")
    sleep = ROOT / "Tools/QuarterRig/make_sleep_parts.py"
    subprocess.run([sys.executable, str(sleep), "--rig", HERO.name, "--fill", "nearest", "--smile"], check=True)
    subprocess.run([sys.executable, str(sleep), "--rig", HERO.name, "--fill", "nearest", "--note"], check=True)
    subprocess.run([sys.executable, str(ROOT / "Tools/HeroRig/tool_arms.py")], check=True)


if __name__ == "__main__":
    main()
