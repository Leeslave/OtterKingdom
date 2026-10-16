"""바닷가 길만 기존 territory_layout.json에 덧붙임 (지도 · 숲 · 부지 칸은 그대로).

build_territory_art.py를 처음부터 다시 돌리면 숲 배치 · 부지 칸까지 바뀌므로, 이미 쓰고 있는 배치에
- 길 칸(SHORE_PATH)을 가리는 늘 남는 숲(wild_*) 나무를 길 숲 묶음(shore_path_k, 바닷가와 같이 사라짐)으로 옮기고
- 길 걷기 영역(바닷가와 같이 열림)을 넣고
- 길 · 길에 걸친 늘 남는 숲의 그늘 그림만 다시 만든다.
여러 번 돌려도 같다 (먼저 길 묶음을 되돌림). 그다음 Unity: Tools/Settlement/Rebuild Plaza Territory

python Tools/MapExpansion/add_shore_path.py
"""
import json
import os
import shutil
import sys
import tempfile

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_territory_art as B  # noqa: E402


def main():
    with open(B.OUT_LAYOUT, encoding="utf-8") as f:
        layout = json.load(f)
    groups = layout["groups"]
    by_id = {g["id"]: g for g in groups}
    path_rects = [B.cell_rect_px(r) for r in B.SHORE_PATH]

    def wild_id(x, y):
        return f"wild_{int((x + B.MAP_OX) // B.WILD_TILE)}_{int((y + B.MAP_OY) // B.WILD_TILE)}"

    # 1) 전에 덧붙인 길을 되돌림 (나무는 제 숲 묶음으로: 늘 남는 숲 또는 개간 조각)
    for g in [g for g in groups if g["id"].startswith("shore_path_")]:
        for t in g["trees"]:
            home_id = g["id"][len("shore_path_over_"):] if g["id"].startswith("shore_path_over_") else wild_id(t["x"], t["y"])
            home = by_id.get(home_id)
            if home is None:
                home = {"id": home_id, "development": "", "stumpsUntil": "", "trees": [], "stumps": []}
                groups.append(home)
                by_id[home_id] = home
            home["trees"].append(t)
        groups.remove(g)
        del by_id[g["id"]]
    layout["regions"] = [r for r in layout["regions"] if not r["id"].startswith("shore_path_")]

    # 2) 길 칸을 가리는 나무를 길 묶음으로 (발밑이 길 아래쪽이어도 줄기 · 잎이 길에 걸치면 — 나무는 발밑에서 위로 그려짐)
    #    늘 남는 숲 나무 → shore_path_k (바닷가가 열리면 사라짐)
    #    개간 조각 나무 → shore_path_over_<조각> (바닷가가 열리거나 그 조각을 개간하면 사라짐 — 먼저 오는 쪽)
    sizes = {s["name"]: (s["width"], s["height"]) for s in layout["sprites"]}

    def covers(t):
        w, h = sizes.get(t["sprite"], (0, 0))
        w, h = w * t["scale"], h * t["scale"]
        x0, x1 = t["x"] - w * 0.35, t["x"] + w * 0.35
        y0, y1 = t["y"] - h * 0.75, t["y"]
        return any(x1 > r[0] + 20 and x0 < r[2] - 20 and y1 > r[1] + 20 and y0 < r[3] - 20 for r in path_rects)

    def path_tile(t):
        for k, r in enumerate(path_rects, start=1):
            if B.in_rect(r, t["x"], t["y"]):
                return k
        # 길 밖에 발밑이 있으면 가장 가까운 길 칸
        return 1 + min(range(len(path_rects)), key=lambda i: abs((path_rects[i][0] + path_rects[i][2]) / 2 - t["x"])
                       + abs((path_rects[i][1] + path_rects[i][3]) / 2 - t["y"]))

    touched = set()
    path_groups = {}
    for g in list(groups):
        keep = []
        for t in g["trees"]:
            if not covers(t):
                keep.append(t)
                continue
            if g["development"]:
                pid = f"shore_path_over_{g['id']}"
                extra = {"alsoDevelopment": g["development"]}
            else:
                pid = f"shore_path_{path_tile(t)}"
                extra = {}
                touched.add(g["id"])
            pg = path_groups.setdefault(pid, {"id": pid, "development": B.SHORE_DEVELOPMENT, "stumpsUntil": "", **extra,
                                              "trees": [], "stumps": []})
            pg["trees"].append(t)
        g["trees"] = keep
    # 길에 걸친 늘 남는 숲 타일도 그늘을 다시 (나무가 안 옮겨졌어도)
    for rect in path_rects:
        for x in (rect[0], rect[2] - 1):
            for y in (rect[1], rect[3] - 1):
                touched.add(wild_id(x, y))
    # 비게 된 늘 남는 숲만 뺌 (개간 조각은 그루터기 · 그늘이 있어 남김)
    path_groups = [path_groups[k] for k in sorted(path_groups)]
    groups = [g for g in groups if g["trees"] or g["development"]] + path_groups

    # 3) 걷기 영역
    world = Image.open(B.OUT_MAP).convert("RGB")
    sea = B.sea_mask(world)
    walk = B.erode_land(sea, B.WALK_FROM_SEA)
    shore_at = next(i for i, r in enumerate(layout["regions"]) if r["id"] == "shore") + 1
    for k, r in enumerate(B.SHORE_PATH, start=1):
        rect = B.cell_rect_px(r)
        layout["regions"].insert(shore_at + k - 1, {"id": f"shore_path_{k}", "development": B.SHORE_DEVELOPMENT,
                                                    "rect": B.px_rect_json(rect),
                                                    "polygons": B.region_polygons(walk, rect, B.CELL_OVERLAP)})

    # 4) 그늘: 임시 폴더에 다 만들고 길 · 걸친 숲 것만 가져옴
    patch_list = []
    for d, t, rect, clearings, prev in B.tiers():
        for k, p in enumerate(B.patches(rect, clearings, prev), start=1):
            patch_list.append({"id": f"{d['key']}_{t}_{k}", "rect": p})
    for k, rect in enumerate(path_rects, start=1):
        patch_list.append({"id": f"shore_path_{k}", "rect": rect})
    redo = touched | {pg["id"] for pg in path_groups if not pg.get("alsoDevelopment")}
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

    layout["groups"] = groups
    with open(B.OUT_LAYOUT, "w", encoding="utf-8") as f:
        f.write(json.dumps(layout, ensure_ascii=False, indent=1))
    moved = sum(len(pg["trees"]) for pg in path_groups)
    print("길 묶음:", {pg["id"]: len(pg["trees"]) for pg in path_groups})
    print(f"바닷가 길: 나무 {moved}그루를 길 숲으로, 그늘 다시 {sorted(redo)}")


if __name__ == "__main__":
    main()
