"""동쪽 블록(공터 오른쪽 아래)만 기존 territory_layout.json에 덧붙임 (지도 · 다른 숲 · 부지 칸은 그대로).

공터의 동쪽 (북동 1단계 아래, 공터 아랫줄까지)은 늘 남는 숲이라 개간할 수 없어 마을 모양을 네모로 잡기 어려웠다.
build_territory_art.py의 DIRECTIONS에 동쪽(East, 1단계, 개간 3번)을 넣었으니, 처음부터 다시 돌리지 않고(숲 배치 · 부지 칸이 바뀜)
- 동쪽 칸 영역에 발밑이 있는 늘 남는 숲(wild_*) 나무를 개간 조각 e_1_1 ~ e_1_3 (공터 쪽부터)으로 옮기고 그루터기를 고르고
- 걷기 영역 · 개간 자리 · 새 이웃 집 자리 · 구역 블록(district_east, 부지 칸) · 단계 정보를 넣고
- 바닷가 길을 다시 덧붙인 뒤 (add_shore_path: 길에 걸친 동쪽 나무는 shore_path_over_e_1_k = 바닷가 · 그 개간 중 먼저 오는 쪽)
- 동쪽 조각 · 나무를 내준 늘 남는 숲의 그늘 그림만 다시 만든다.
여러 번 돌려도 같다 (먼저 동쪽을 되돌림). 그다음 Unity: Tools/Settlement/Apply Map Expansion → Apply Districts

python Tools/MapExpansion/add_east_block.py
"""
import json
import os
import random
import shutil
import sys
import tempfile

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_territory_art as B  # noqa: E402
import add_shore_path  # noqa: E402

KEY = "e"
EAST = next(d for d in B.DIRECTIONS if d["key"] == KEY)


def wild_id(x, y):
    return f"wild_{int((x + B.MAP_OX) // B.WILD_TILE)}_{int((y + B.MAP_OY) // B.WILD_TILE)}"


def load():
    with open(B.OUT_LAYOUT, encoding="utf-8") as f:
        return json.load(f)


def save(layout):
    with open(B.OUT_LAYOUT, "w", encoding="utf-8") as f:
        f.write(json.dumps(layout, ensure_ascii=False, indent=1))


def home_group(groups, by_id, gid):
    g = by_id.get(gid)
    if g is None:
        g = {"id": gid, "development": "", "stumpsUntil": "", "trees": [], "stumps": []}
        groups.append(g)
        by_id[gid] = g
    return g


def main():
    layout = load()
    groups = layout["groups"]
    by_id = {g["id"]: g for g in groups}
    prefix = f"{KEY}_1_"

    # 1) 바닷가 길을 되돌림 (add_shore_path 1단계와 같음) — 길 묶음의 나무는 제 묶음으로
    for g in [g for g in groups if g["id"].startswith("shore_path_")]:
        for t in g["trees"]:
            hid = g["id"][len("shore_path_over_"):] if g["id"].startswith("shore_path_over_") else wild_id(t["x"], t["y"])
            home_group(groups, by_id, hid)["trees"].append(t)
        groups.remove(g)
        del by_id[g["id"]]
    layout["regions"] = [r for r in layout["regions"] if not r["id"].startswith("shore_path_")]

    # 2) 전에 덧붙인 동쪽을 되돌림
    for g in [g for g in groups if g["id"].startswith(prefix)]:
        for t in g["trees"]:
            home_group(groups, by_id, wild_id(t["x"], t["y"]))["trees"].append(t)
        groups.remove(g)
        del by_id[g["id"]]
    layout["regions"] = [r for r in layout["regions"] if r.get("direction") != EAST["direction"]]
    layout["sites"] = [s for s in layout["sites"] if s["direction"] != EAST["direction"]]
    layout["homes"]["list"] = [h for h in layout["homes"]["list"] if h["direction"] != EAST["direction"]]
    layout["districts"] = [d for d in layout["districts"] if d["block"] != EAST["block"]]
    layout["tiers"] = [t for t in layout["tiers"] if t["direction"] != EAST["direction"]]

    # 3) 동쪽 개간 조각: 발밑이 조각 안인 늘 남는 숲 나무를 옮김 (공터 쪽 조각이 1번)
    start_px = B.cell_rect_px(B.START)
    rect, clearings = EAST["tiers"][0]
    expansion = B.expansion_id(KEY, 1)
    patch_rects = B.patches(rect, clearings, start_px)
    east = []
    touched = set()
    for k, pr in enumerate(patch_rects, start=1):
        east.append({"id": f"{prefix}{k}", "development": f"{expansion}_c{k}", "stumpsUntil": expansion, "trees": [], "stumps": [],
                     "rect": pr})
    for g in groups:
        if not g["id"].startswith("wild_"):
            continue
        keep = []
        for t in g["trees"]:
            dest = next((e for e in east if B.in_rect(e["rect"], t["x"], t["y"])), None)
            if dest is None:
                keep.append(t)
            else:
                dest["trees"].append(t)
                touched.add(g["id"])
        g["trees"] = keep
    # 그루터기 (build_forest와 같은 방식, 조각마다 서로 200px 떨어진 나무 자리 4곳까지)
    rng = random.Random(B.SEED + 5)
    for e in east:
        cands = [(t["x"], t["y"]) for t in e["trees"] if t["sprite"] != "Forest_Bush"]
        rng.shuffle(cands)
        chosen = []
        for p in cands:
            if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 200 ** 2 for q in chosen):
                chosen.append(p)
            if len(chosen) >= B.STUMPS_PER_PATCH:
                break
        e["stumps"] = [{"sprite": "Forest_Stump", "x": p[0], "y": p[1], "scale": round(rng.uniform(0.26, 0.34), 3),
                        "flip": rng.random() < 0.5, "tint": 1.0} for p in chosen]
        e.pop("rect")
    groups[:] = [g for g in groups if g["trees"] or g["development"]]
    # 개간 조각은 다른 방향 조각 뒤 · 늘 남는 숲 앞 (build_territory_art.py와 같은 차례)
    first_wild = next((i for i, g in enumerate(groups) if g["id"].startswith("wild_")), len(groups))
    groups[first_wild:first_wild] = east

    # 4) 걷기 영역 · 개간 자리 · 이웃집 자리 · 구역 블록 · 단계
    world = Image.open(B.OUT_MAP).convert("RGB")
    sea = B.sea_mask(world)
    walk = B.erode_land(sea, B.WALK_FROM_SEA)
    rect_px = B.cell_rect_px(rect)
    shore_at = next(i for i, r in enumerate(layout["regions"]) if r["id"] == "shore")
    layout["regions"].insert(shore_at, {"id": f"{KEY}_1", "direction": EAST["direction"], "tier": 1, "development": expansion,
                                        "rect": B.px_rect_json(rect_px),
                                        "polygons": B.region_polygons(walk, rect_px, B.CELL_OVERLAP)})
    site = next(s for s in B.build_sites(walk) if s["direction"] == EAST["direction"])
    layout["sites"].append(site)
    home = next(h for h in B.build_homes(walk)["list"] if h["direction"] == EAST["direction"])
    layout["homes"]["list"].append(home)
    sand = B.sand_cells(world)
    taken = [(s["x"], s["y"], s["w"], s["h"]) for d in layout["districts"] for s in d["slots"]]
    large, small = B.SLOT_TARGETS[0]
    slots = B.place_slots(sand, rect, large, small, taken)
    layout["districts"].append({"block": EAST["block"], "direction": EAST["direction"],
                                "stages": [{"development": expansion, "x": rect[0], "y": rect[1], "w": rect[2], "h": rect[3]}],
                                "slots": [{"x": s[0], "y": s[1], "w": s[2], "h": s[3], "stage": 0} for s in slots]})
    layout["tiers"].append({"direction": EAST["direction"], "key": KEY, "tier": 1, "clearings": clearings})
    save(layout)

    # 5) 바닷가 길 다시 (길에 걸친 동쪽 나무 → shore_path_over_e_1_k)
    add_shore_path.main()

    # 6) 그늘: 동쪽 조각 · 나무를 내준 늘 남는 숲 (임시 폴더에 다 만들고 그것만 가져옴)
    layout = load()
    groups = layout["groups"]
    patch_list = []
    for d, t, r, c, prev in B.tiers():
        for k, p in enumerate(B.patches(r, c, prev), start=1):
            patch_list.append({"id": f"{d['key']}_{t}_{k}", "rect": p})
    for k, r in enumerate(B.SHORE_PATH, start=1):
        patch_list.append({"id": f"shore_path_{k}", "rect": B.cell_rect_px(r)})
    redo = {e["id"] for e in east} | touched
    for g in groups:
        if g["id"].startswith("wild_"):
            # 동쪽 칸 영역에 걸친 늘 남는 숲 타일은 그늘 자리가 바뀜
            for x in (rect_px[0], rect_px[2] - 1):
                for y in (rect_px[1], rect_px[3] - 1):
                    if g["id"] == wild_id(x, y):
                        redo.add(g["id"])
    tmp = tempfile.mkdtemp()
    out_dir = B.OUT_DIR
    try:
        B.OUT_DIR = tmp
        scratch = [dict(g) for g in groups]
        B.build_shades(sea, scratch, patch_list)
    finally:
        B.OUT_DIR = out_dir
    for g, s in zip(groups, scratch):
        if g["id"] not in redo:
            continue
        if "shade" in s:
            shutil.copyfile(os.path.join(tmp, f"{s['shade']['sprite']}.png"), os.path.join(out_dir, f"{s['shade']['sprite']}.png"))
            g["shade"] = s["shade"]
        else:
            g.pop("shade", None)
    shutil.rmtree(tmp, ignore_errors=True)
    save(layout)
    print("동쪽 조각:", {e["id"]: len(e["trees"]) for e in east}, "그루터기", sum(len(e["stumps"]) for e in east))
    print("동쪽 개간 자리", (site["x"], site["y"]), "· 이웃집 자리", (home["x"], home["y"]), "· 부지 칸", slots)
    print("그늘 다시:", sorted(redo))


if __name__ == "__main__":
    main()
