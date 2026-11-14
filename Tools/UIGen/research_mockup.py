"""왕국 발전 [연구] 탭 — 기술 트리 시안 (지금은 기술 카드 24장이 세로 목록).
실제 게임 UI 부품 · 폰트 · 그림으로 1080x1920. 기술 · 선행 · 레벨 · RP는 ProsperityResearch.Techs 그대로, 진행 상태는 예시
(Lv.29 · 3 RP · 제재 기술 · 공동 창고 · 장터 주문 · 마을 정비 · 석재 규격화 · 운송 수레 연구함).
usage: python research_mockup.py <out_dir>
  07_tree_industry  분야 탭 [산업]: 단계 띠(레벨) · 연구 가능 빛 · 고른 기술의 선행 길 · 다른 분야 선행 노드 · 접힌 시트
  08_tree_detail    시트 펼침: 연구하면 달라지는 것(기능 · 해달 · 광장) · 조건 체크리스트 · 다음에 열리는 것 · [연구하기]
  09_tree_city      [도시]: 다른 분야 선행이 남은 기술 → 조건에 [산업 탭으로] · 버튼 잠김"""
import os
import sys

from PIL import Image, ImageDraw

import kingdom_mockup as km
from kingdom_mockup import (W, H, COCOA, MUTED, GREEN, WHITE, CX0, CX1, CW, T, tw, put, put_c, nine, tag, button, tile,
                            check, art, load, finish, content_layer, sprite, UI)

GOLD = (0xF2, 0xC9, 0x4C, 255)
LINE_DONE = (0x7F, 0xA8, 0x6C, 255)
LINE_OPEN = COCOA
LINE_LOCK = (0xC9, 0xB5, 0xA3, 255)
BRANCH = {"I": ("산업", (0xE0, 0x8A, 0x5A)), "L": ("물류", (0x5B, 0x9B, 0xD5)), "C": ("도시", (0x8C, 0xB8, 0x6E)),
          "E": ("경제", (0xE6, 0xB8, 0x3C)), "X": ("융합", (0xA8, 0x8B, 0xC9))}

# ProsperityResearch.Techs (이름 · 레벨 · RP · 선행)
TECHS = {
    "I01": ("제재 기술", 20, 1, []), "L01": ("공동 창고", 20, 1, ["I01"]), "E01": ("장터 주문", 21, 1, ["I01"]),
    "C01": ("마을 정비", 22, 1, ["I01"]), "I02": ("석재 규격화", 23, 2, ["I01"]), "L02": ("운송 수레", 24, 2, ["L01"]),
    "C02": ("주거 개량 1차", 25, 2, ["C01", "L01"]), "I03": ("금속 제련", 28, 2, ["I02"]), "L03": ("운송 배차", 29, 1, ["L02"]),
    "I04": ("표준 공구", 30, 1, ["I03"]), "L04": ("원료 자동 공급", 31, 1, ["L03"]), "C03": ("도로 포장", 32, 1, ["C01"]),
    "I05": ("수력 기계", 33, 1, ["I04"]), "L05": ("집산소", 34, 1, ["L01"]), "C04": ("공공 시설", 35, 1, ["C03", "I03"]),
    "I06": ("정밀 공방", 36, 2, ["I03", "L01"]), "L06": ("개량 수레", 38, 1, ["L03"]), "C05": ("왕국 행정", 40, 2, ["C02", "I03"]),
    "X01": ("산업 물류 체계", 42, 2, ["I06", "L06"]), "I07": ("협업 생산", 43, 1, ["X01"]),
    "I08": ("왕립 산업단지", 45, 2, ["I07", "I06"]), "C06": ("왕립 학술원", 46, 2, ["C05", "I06"]),
    "C07": ("수도 계획", 48, 1, ["C04", "C05"]), "C08": ("왕궁 증축", 50, 2, ["C06", "C07"]),
}
EFFECT = {"I03": "건설소에서 조개 구리를 금속 주괴로 가공해요", "C02": "기존 집을 개량 주택으로 바꿔요"}
ICON = {
    "I01": "Item/Industry/ICON_Item_Plank.png", "I02": "Item/Industry/ICON_Item_StoneBlock.png", "I03": "Item/Industry/ICON_Item_Ingot.png",
    "I04": "Industry/Prop_Sawhorse.png", "I05": "Industry/Prop_Kiln.png", "I06": "Item/Industry/ICON_Item_Precision.png",
    "I07": "Industry/Pile_Precision_2.png", "I08": "Industry/Prop_RoyalBanner.png", "X01": "Industry/Cart_Precision.png",
    "L01": "Industry/Prop_WarehouseCrates.png", "L02": "Industry/Cart_Empty.png", "L03": "Industry/Cart_Plank.png",
    "L04": "Industry/Cart_StoneBlock.png", "L05": "Industry/Pile_StoneBlock_2.png", "L06": "Industry/Cart_Pennant.png",
    "C01": "Industry/Prop_KingdomSign.png", "C02": "Industry/TownHouse_Red.png", "C03": "Industry/ICON_PavingRoad.png",
    "C04": "Industry/Prop_Fountain.png", "C05": "Industry/TownHouse_Blue.png", "C06": "Industry/Academy.png",
    "C07": "Industry/Prop_CapitalFlag.png", "C08": "Industry/Palace.png", "E01": "UI/Currency/Gold.png",
}
LEVEL, RP = 29, 3
LEARNED = {"I01", "L01", "E01", "C01", "I02", "L02"}
TABS = ["연구", "공동사업", "물류", "기록"]


def state(tid):
    """done / open(연구 가능) / prereq(선행 필요) / level(레벨 잠김)"""
    if tid in LEARNED:
        return "done"
    name, level, cost, pre = TECHS[tid]
    if any(p not in LEARNED for p in pre):
        return "prereq"
    if level > LEVEL:
        return "level"
    return "open"


def icon_of(tid, size):
    return load(os.path.join(km.ART, ICON[tid]), size)


# ------------------------------------------------------------------ 그리기 부품
def ring(L, cx, cy, r, color, width=7):
    ImageDraw.Draw(L).rounded_rectangle([cx - r, cy - r, cx + r, cy + r], radius=int(r * 0.42), outline=color, width=width)


def node(L, cx, cy, tid, size=96, selected=False, shown=()):
    """노드: 둥근 칸 + 오른쪽에 이름 · Lv · RP · 다른 분야 선행 (선이 글을 덮지 않게)"""
    st = state(tid)
    x, y = cx - size // 2, cy - size // 2
    if st == "open":
        glow = Image.new("RGBA", (size + 56, size + 56), (0, 0, 0, 0))
        ImageDraw.Draw(glow).ellipse([0, 0, size + 55, size + 55], fill=(0xF2, 0xC9, 0x4C, 80))
        put(L, glow, x - 28, y - 28)
    tile(L, x, y, size, icon_of(tid, size), filled=st in ("done", "open"), locked=st in ("prereq", "level"))
    if st == "done":
        check(L, x + size - 4, y + 6, True, 18)
    if st == "open" or selected:
        ring(L, cx, cy - 2, size // 2 + 8, GOLD, 8 if selected else 6)
    name, level, cost, pre = TECHS[tid]
    lx = x + size + 18
    lit = st in ("done", "open")
    cross = [p for p in pre if p not in shown]  # 이 화면에 선으로 이어지지 않은 선행만 글로
    lines = 2 + len(cross)
    ty = cy - lines * 16
    T(L, (lx, ty), name, 26, True, COCOA if lit else MUTED)
    sub = "연구 완료" if st == "done" else f"Lv.{level} · {cost} RP"
    T(L, (lx, ty + 36), sub, 20, False, GREEN if st == "done" else (COCOA if st == "open" else MUTED))
    for i, p in enumerate(cross):
        yy = ty + 66 + i * 28
        branch, color = BRANCH[p[0]]
        ImageDraw.Draw(L).ellipse([lx, yy + 3, lx + 18, yy + 21], fill=color + (255,), outline=COCOA, width=2)
        done = p in LEARNED
        T(L, (lx + 26, yy), f"+ {branch} · {TECHS[p][0]}" + (" 완료" if done else ""), 19, False, GREEN if done else MUTED)


def edge(L, a, b, kind, size=96):
    """a(위) → b(아래): 아래로 내려가다 옆으로 꺾어 내려감 (꺾는 곳은 아래 노드 바로 위)"""
    (ax, ay), (bx, by) = a, b
    color = {"done": LINE_DONE, "open": LINE_OPEN, "lock": LINE_LOCK}[kind]
    d = ImageDraw.Draw(L)
    y0 = ay + size // 2 + 6
    y1 = by - size // 2 - 12
    turn = y1 - 34
    pts = [(ax, y0), (ax, turn), (bx, turn), (bx, y1)] if ax != bx else [(ax, y0), (bx, y1)]
    w = 8 if kind != "lock" else 6
    if kind == "lock":
        for (x0_, y0_), (x1_, y1_) in zip(pts, pts[1:]):
            n = max(1, int(max(abs(x1_ - x0_), abs(y1_ - y0_)) // 18))
            for i in range(0, n, 2):
                t0, t1 = i / n, min(1, (i + 1) / n)
                d.line([(x0_ + (x1_ - x0_) * t0, y0_ + (y1_ - y0_) * t0), (x0_ + (x1_ - x0_) * t1, y0_ + (y1_ - y0_) * t1)], fill=color, width=w)
    else:
        d.line(pts, fill=color, width=w, joint="curve")
    d.polygon([(bx - 12, y1 - 10), (bx + 12, y1 - 10), (bx, y1 + 6)], fill=color)


def edge_kind(src, dst):
    if src in LEARNED and dst in LEARNED:
        return "done"
    return "open" if src in LEARNED else "lock"


# ================================================================== v2: 단계 띠 + 선행 길 강조 + 상세 시트
# 참고: Whiteout Survival(분야 트리 3개 · 연구소 레벨로 열리는 단계) · 쿠키런: 킹덤(연구소 레벨마다 기술 묶음이 열림 · 앞 단계를 끝내야 다음).
# 원칙: 분야는 탭으로 나눠 선이 엉키지 않게 / 레벨 조건은 노드마다가 아니라 "단계 띠"로 / 지금 할 수 있는 것을 먼저 보여 줌(빛 · 바로가기)
#       / 고르면 선행 길만 진하게 / 다른 분야 선행은 그 자리에 흐린 "다른 분야" 노드 / 아래 시트에서 조건 체크리스트 + 무엇이 달라지는지

NODE = 100
COLS = {"l": 270, "c": 540, "r": 810}
HILITE = (0xE8, 0xA8, 0x3A, 255)
EXTRA = {
    # 연구하면 달라지는 것 (기능 · 해달 · 광장) — 기획서 "연구 결과는 3중 보상"
    "I03": [("Item/Industry/ICON_Item_Ingot.png", "기능", "건설소 가공 줄\n조개 구리 → 주괴"),
            ("Otter/ICON_Otter_Cast_Miner.png", "해달", "가마 앞에서\n망치질해요"),
            ("Industry/Prop_Kiln.png", "광장", "건설소 옆에\n제련 가마")],
    "C05": [("Industry/TownHouse_Blue.png", "기능", "개량 주택 →\n마을 주택"),
            ("Otter/ICON_Otter_Cast_Sailor.png", "해달", "또박이가\n집집마다 방문"),
            ("Settlement/ICON_Lamp.png", "광장", "현관 등불 ·\n꽃 화분")],
}
EFFECT["C05"] = "개량 주택을 마을 주택으로 한 번 더 개량해요"


def v2_header(L, y, branch):
    """연구 포인트 + 연구 가능 바로가기 / 분야 탭 (아이콘 · 이름 · 진행 막대)"""
    h = 92
    put(L, nine("Inventory/UI_Inventory_Box", CW, h), CX0, y)
    put_c(L, load(os.path.join(UI, "Currency", "Shard.png"), 56), CX0 + 56, y + h // 2 - 4)
    T(L, (CX0 + 96, y + h // 2 - 4), f"연구 포인트 {RP}", 34, True, COCOA, "lm")
    T(L, (CX0 + 96 + tw(f"연구 포인트 {RP}", 34, True) + 12, y + h // 2), "RP", 24, False, MUTED, "lm")
    tag(L, CX1 - 24, y + 20, "연구 가능 2개 ›", "gold", 26, anchor="r")
    y += h + 20
    tabs = [("I", "산업", "Item/Industry/ICON_Item_Ingot.png", 2, 9), ("L", "물류", "Industry/Cart_Empty.png", 2, 8),
            ("C", "도시", "Industry/Prop_KingdomSign.png", 1, 8), ("E", "경제", "UI/Currency/Gold.png", 1, 8)]
    gap = 14
    tw_ = (CW - gap * 3) // 4
    for i, (key, name, icon, done, total) in enumerate(tabs):
        x = CX0 + i * (tw_ + gap)
        sel = key == branch
        put(L, nine("Inventory/UI_Inventory_Tab_Selected" if sel else "Inventory/UI_Inventory_Tab_Normal", tw_, 116), x, y)
        put_c(L, load(os.path.join(km.ART, icon), 46), x + 44, y + 46)
        T(L, (x + 78, y + 46), name, 29, True, COCOA if sel else MUTED, "lm")
        T(L, (x + tw_ - 22, y + 48), f"{done}/{total}", 20, False, MUTED, "rm")
        km.bar(L, x + 24, y + 74, tw_ - 48, done / total, 12)
        if key == "C":
            km.dot(L, x + tw_ - 14, y + 10, 12)  # 연구 가능이 있는 분야
    return y + 116 + 24


def band(L, y, title, levels, status, kind):
    """단계 띠: "2단계 · Lv.28~33" + 상태 (완료 / 지금 / Lv.36에 열려요). 잠긴 단계는 흐리게"""
    h = 64
    face = {"done": (0xE4, 0xF0, 0xDA), "now": (0xFF, 0xF1, 0xD0), "lock": (0xEE, 0xE6, 0xDC)}[kind]
    d = ImageDraw.Draw(L)
    d.rounded_rectangle([CX0 + 4, y, CX1 - 4, y + h], radius=24, fill=face + (255,), outline=(0xE0, 0xC8, 0xA8, 255), width=3)
    T(L, (CX0 + 30, y + h // 2 - 2), title, 28, True, COCOA if kind != "lock" else MUTED, "lm")
    T(L, (CX0 + 30 + tw(title, 28, True) + 14, y + h // 2), levels, 22, False, MUTED, "lm")
    tag(L, CX1 - 22, y + 8, status, {"done": "on", "now": "gold", "lock": "normal"}[kind], 22, anchor="r")
    if kind == "lock":
        lock = load(os.path.join(UI, "Inventory", "LockIcon.png"), 34)
        put(L, lock, CX1 - 30 - tw(status, 22, True) - 40 - 44, y + 14)
    return y + h


def v2_node(L, cx, cy, tid, selected=False, dim=False, ghost=False):
    """노드: 둥근 칸 + 아래 이름. 연구 가능은 빛 + "2 RP", 레벨로 잠기면 "Lv.30", 선행이 남으면 자물쇠. ghost = 다른 분야 선행"""
    st = state(tid)
    s = NODE if not ghost else 84
    x, y = cx - s // 2, cy - s // 2
    layer = Image.new("RGBA", L.size, (0, 0, 0, 0))
    if ghost:
        d = ImageDraw.Draw(layer)
        branch, color = BRANCH[tid[0]]
        d.rounded_rectangle([x - 4, y - 4, x + s + 4, y + s + 4], radius=28, fill=(255, 251, 244, 200), outline=color + (255,), width=4)
        ic = icon_of(tid, int(s * 0.7))
        ic.putalpha(ic.getchannel("A").point(lambda v: int(v * 0.75)))
        put_c(layer, ic, cx, cy)
        if st == "done":
            check(layer, x + s - 2, y + 4, True, 15)
        T(layer, (cx, y + s + 20), f"{branch} · {TECHS[tid][0]}", 20, False, GREEN if st == "done" else MUTED, "mm")
        L.alpha_composite(layer)
        return
    if st == "open" and not dim:
        glow = Image.new("RGBA", (s + 64, s + 64), (0, 0, 0, 0))
        ImageDraw.Draw(glow).ellipse([0, 0, s + 63, s + 63], fill=(0xF2, 0xC9, 0x4C, 90))
        put(layer, glow, x - 32, y - 32)
    tile(layer, x, y, s, icon_of(tid, s), filled=st in ("done", "open"), locked=st == "prereq")
    if st == "done":
        check(layer, x + s - 4, y + 6, True, 19)
    if st == "open" or selected:
        ring(layer, cx, cy - 2, s // 2 + 9, HILITE if selected else GOLD, 9 if selected else 6)
    name, level, cost, _ = TECHS[tid]
    T(layer, (cx, y + s + 22), name, 23, True, COCOA if st in ("done", "open") else MUTED, "mm")
    if st == "open":
        tag(layer, x + s + 26, y - 12, f"{cost} RP", "gold", 20, anchor="r")
    elif st == "level":
        tag(layer, x + s + 26, y - 12, f"Lv.{level}", "normal", 20, anchor="r")
    if dim:
        layer.putalpha(layer.getchannel("A").point(lambda v: int(v * 0.45)))
    L.alpha_composite(layer)


def v2_edge(L, a, b, kind, ghost_src=False):
    """a → b. kind: path(고른 기술의 선행 길, 진하게) · done · open · lock · next(고른 기술로 열리는 것, 점선)"""
    (ax, ay), (bx, by) = a, b
    color = {"path": HILITE, "done": LINE_DONE, "open": LINE_OPEN, "lock": LINE_LOCK, "next": (0xD9, 0x9A, 0x55, 255)}[kind]
    w = {"path": 10, "done": 7, "open": 7, "lock": 5, "next": 6}[kind]
    d = ImageDraw.Draw(L)
    y0 = ay + (42 if ghost_src else NODE // 2 + 38)
    y1 = by - NODE // 2 - 14
    if ghost_src and ay == by:  # 같은 줄 옆의 다른 분야 노드 → 옆으로
        x0 = ax + (50 if bx > ax else -50)
        x1 = bx - (NODE // 2 + 14) * (1 if bx > ax else -1)
        pts = [(x0, ay), (x1, by)]
        tip = [(x1, by - 12), (x1, by + 12), (x1 + (14 if bx > ax else -14), by)]
    else:
        turn = y1 - 30
        pts = [(ax, y0), (ax, turn), (bx, turn), (bx, y1)] if ax != bx else [(ax, y0), (bx, y1)]
        tip = [(bx - 12, y1 - 10), (bx + 12, y1 - 10), (bx, y1 + 6)]
    if kind in ("lock", "next") or ghost_src:
        for (x0_, y0_), (x1_, y1_) in zip(pts, pts[1:]):
            n = max(1, int(max(abs(x1_ - x0_), abs(y1_ - y0_)) // 16))
            for i in range(0, n, 2):
                t0, t1 = i / n, min(1, (i + 1) / n)
                d.line([(x0_ + (x1_ - x0_) * t0, y0_ + (y1_ - y0_) * t0), (x0_ + (x1_ - x0_) * t1, y0_ + (y1_ - y0_) * t1)], fill=color, width=w)
    else:
        d.line(pts, fill=color, width=w, joint="curve")
    d.polygon(tip, fill=color)


def tree_layout(L, bands, nodes, edges, selected, ghosts=(), scroll=0):
    """bands: [(제목, 레벨, 상태글, kind, [줄마다 [(tid, 열)]])] → 노드 자리 계산 후 선 · 노드를 그림"""
    pos = {}
    y = bands[0][5] - scroll
    band_rows = []
    band_draws = []
    for title, levels, status, kind, rows, _ in bands:
        band_draws.append((y, title, levels, status, kind))  # 선 위에 그림 (선이 띠 뒤로 지나가게)
        y = y + 64 + 34
        for row in rows:
            cy = y + NODE // 2 + 10
            for tid, col in row:
                pos[tid] = (COLS[col], cy)
            y = cy + NODE // 2 + 92
        band_rows.append(kind)
        y += 14
    sel_pre = set(TECHS[selected][3]) if selected else set()
    # 선행 길: 고른 기술까지 거슬러 올라가는 길
    path = set()
    stack = [selected] if selected else []
    while stack:
        t = stack.pop()
        for p in TECHS[t][3]:
            if p in pos:
                path.add((p, t))
                stack.append(p)
    for a, b in edges:
        if a not in pos or b not in pos:
            continue
        if (a, b) in path:
            kind = "path"
        elif a == selected:
            kind = "next"
        else:
            kind = edge_kind(a, b)
        v2_edge(L, pos[a], pos[b], kind, ghost_src=a in ghosts)
    for args in band_draws:
        band(L, *args)
    lock_kinds = {tid for (_, _, _, kind, rows, _) in bands if kind == "lock" for row in rows for tid, _ in row}
    for tid, (x, yy) in pos.items():
        v2_node(L, x, yy, tid, selected=tid == selected, dim=tid in lock_kinds and tid != selected, ghost=tid in ghosts)
    return pos


def v2_sheet(img, tid, y, button_label=None, enabled=True, requirements=None):
    """상세 시트: 이름 · 분야 · 효과 / 달라지는 것 3칸 / 조건 체크리스트 / 다음에 열리는 것 / [연구하기]"""
    name, level, cost, pre = TECHS[tid]
    x0, w = 52, W - 104
    h = H - 50 - y
    shadow = Image.new("RGBA", (w + 40, 60), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([0, 20, w + 39, 59], radius=30, fill=(60, 35, 20, 60))
    put(img, shadow.filter(km.ImageFilter.GaussianBlur(10)), x0 - 20, y - 30)
    put(img, nine("Inventory/UI_Inventory_Panel", w, h, (252, 245, 230), (72, 72, 72, 80)), x0, y)
    ImageDraw.Draw(img).rounded_rectangle([W // 2 - 50, y + 16, W // 2 + 50, y + 26], radius=5, fill=(0xD9, 0xC4, 0xAA, 255))
    yy = y + 44
    tile(img, x0 + 34, yy, 128, icon_of(tid, 128), filled=True)
    T(img, (x0 + 186, yy + 6), name, 40, True)
    bx = x0 + 186 + tw(name, 40, True) + 16
    branch, color = BRANCH[tid[0]]
    tag(img, bx, yy + 8, branch, "normal", 22)
    T(img, (x0 + 186, yy + 62), EFFECT.get(tid, ""), 25, False, MUTED)
    yy += 150
    # 달라지는 것 3칸
    T(img, (x0 + 40, yy), "연구하면", 26, True)
    yy += 42
    cw = (w - 80 - 2 * 16) // 3
    for i, (icon, label, text) in enumerate(EXTRA.get(tid, [])):
        cx = x0 + 40 + i * (cw + 16)
        km.inset(img, cx, yy, cw, 150)
        put_c(img, load(os.path.join(km.ART, icon), 76), cx + 58, yy + 75)
        T(img, (cx + 108, yy + 24), label, 24, True, COCOA)
        T(img, (cx + 108, yy + 60), text, 20, False, MUTED, spacing=6)
    yy += 150 + 26
    # 조건
    T(img, (x0 + 40, yy), "조건", 26, True)
    yy += 44
    for ok, text, action in requirements or []:
        check(img, x0 + 62, yy + 14, ok, 17)
        T(img, (x0 + 96, yy + 14), text, 25, False, COCOA if ok else (0xC6, 0x5F, 0x4E, 255), "lm")
        if action:
            tag(img, x0 + w - 40, yy - 6, action, "normal", 22, anchor="r")
        yy += 50
    yy += 6
    opens = [TECHS[t][0] for t, v in TECHS.items() if tid in v[3]]
    T(img, (x0 + 40, yy), "다음에 열려요  " + " · ".join(opens), 23, False, MUTED)
    label = button_label or f"연구하기 · {cost} RP"
    button(img, x0 + 40, y + h - 130, w - 80, 100, label, "primary" if enabled else "secondary", 38)


INDUSTRY_EDGES = [("I01", "I02"), ("I02", "I03"), ("I03", "I04"), ("I03", "I06"), ("I04", "I05"), ("I06", "X01"),
                  ("X01", "I07"), ("I07", "I08"), ("L01", "I06"), ("L06", "X01")]
CITY_EDGES = [("C01", "C02"), ("C01", "C03"), ("C02", "C05"), ("C03", "C04"), ("C05", "C06"), ("C05", "C07"),
              ("C04", "C07"), ("C06", "C08"), ("C07", "C08"), ("L01", "C02"), ("I03", "C04"), ("I03", "C05"), ("I06", "C06")]


def industry_bands(y):
    return [("1단계", "Lv.20~25", "완료", "done", [[("I01", "c")], [("I02", "c")]], y),
            ("2단계", "Lv.28~33", "지금", "now", [[("I03", "c")], [("I04", "l")], [("I05", "l")]], y),
            ("3단계", "Lv.36~45", "Lv.36에 열려요", "lock", [[("L01", "r"), ("I06", "c")], [("L06", "r"), ("X01", "c")], [("I07", "c")], [("I08", "c")]], y)]


# ------------------------------------------------------------------ 07 산업 트리 (시트 접힘)
def scrolled_tree(img, branch, bands_fn, edges, selected, ghosts, scroll=0):
    """분야 탭 · 연구 포인트는 위에 고정, 아래 트리만 스크롤"""
    head = content_layer()
    y = v2_header(head, 350, branch)
    tree = content_layer()
    tree_layout(tree, bands_fn(y + 6), None, edges, selected, ghosts=ghosts, scroll=scroll)
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rectangle([0, y, W, km.VIEW[3]], fill=255)
    clipped = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    clipped.paste(tree, (0, 0), Image.composite(tree.getchannel("A"), Image.new("L", (W, H), 0), mask))
    km.finish(img, head, fade=False)
    img.alpha_composite(clipped)
    return y


def v2_industry():
    img = km.window("왕국 발전", None, TABS, 0)
    scrolled_tree(img, "I", industry_bands, INDUSTRY_EDGES, "I03", {"L01", "L06"})
    # 접힌 시트: 고른 기술 한 줄 + 버튼 (위로 끌면 08처럼 펼침)
    collapsed_sheet(img, "I03", "조건 3/3 · 다음에 열려요 4개", True, "연구하기 · 2 RP", True)
    return img


# ------------------------------------------------------------------ 08 상세 시트 펼침
def v2_detail():
    img = v2_industry()
    img.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 70)), (0, 0))
    v2_sheet(img, "I03", 880, requirements=[(True, "석재 규격화 연구 완료", None), (True, "왕국 Lv.28 (지금 Lv.29)", None),
                                              (True, "연구 포인트 2 (가진 것 3)", None)])
    return img


# ------------------------------------------------------------------ 09 도시: 다른 분야 선행 · 조건 부족
def city_bands(y):
    return [("1단계", "Lv.22~25", "지금", "now", [[("C01", "c")], [("C02", "l"), ("L01", "c")]], y),
            ("2단계", "Lv.32~35", "Lv.32에 열려요", "lock", [[("C03", "r")], [("I03", "c"), ("C04", "r")]], y),
            ("3단계", "Lv.40~50", "Lv.40에 열려요", "lock", [[("C05", "l")], [("C06", "l"), ("I06", "c"), ("C07", "r")], [("C08", "c")]], y)]


def collapsed_sheet(img, tid, line, ok, label, enabled):
    y0 = 1620
    x0, w = 52, W - 104
    put(img, nine("Inventory/UI_Inventory_Panel", w, H - 50 - y0, (252, 245, 230), (72, 72, 72, 80)), x0, y0)
    ImageDraw.Draw(img).rounded_rectangle([W // 2 - 50, y0 + 16, W // 2 + 50, y0 + 26], radius=5, fill=(0xD9, 0xC4, 0xAA, 255))
    tile(img, x0 + 30, y0 + 48, 120, icon_of(tid, 120), filled=True)
    T(img, (x0 + 172, y0 + 58), TECHS[tid][0], 36, True)
    T(img, (x0 + 172, y0 + 108), line, 23, False, GREEN if ok else (0xC6, 0x5F, 0x4E, 255))
    button(img, x0 + w - 300, y0 + 64, 266, 96, label, "primary" if enabled else "secondary", 30)


# ------------------------------------------------------------------ 09 도시: 두 갈래가 모임 · 다른 분야 선행 노드 · 조건이 남은 기술
def v2_city():
    img = km.window("왕국 발전", None, TABS, 0)
    scrolled_tree(img, "C", city_bands, CITY_EDGES, "C05", {"L01", "I03", "I06"}, scroll=470)
    collapsed_sheet(img, "C05", "조건 1/3 · 산업 · 금속 제련 · Lv.40 필요", False, "선행 필요", False)
    return img


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in [("07_tree_industry", v2_industry), ("08_tree_detail", v2_detail), ("09_tree_city", v2_city)]:
        fn().convert("RGB").save(os.path.join(out, name + ".png"))
        print("saved", name)


if __name__ == "__main__":
    main()
