"""공동사업 창 개편 시안 (지금: 가운데 패널에 글줄 · 버튼을 세로로 쌓음 — CommunityProjectPresenter).
실제 게임 UI 부품 · 폰트 · 그림으로 1080x1920. 이름 · 수치는 예시 (마을 생활 준비 · 마을회관 물품 보충).
usage: python project_mockup.py <out_dir>
  10_project_deliver  재료 모으기: 리본 제목 · 주민 말풍선 · 단계 표시 · 재료 카드(진행 막대 · 넣기 / 완료 / 부족) · 완성 보상 · [가능한 만큼 넣기]
  11_project_stage    단계 진행: 재료 완료 → 소품 고르기(카드 3장) → [건설 시작]"""
import os
import sys

from PIL import Image, ImageDraw

import kingdom_mockup as km
from kingdom_mockup import (W, H, COCOA, MUTED, GREEN, RED, WHITE, T, tw, put, put_c, nine, tag, button, tile, check, load,
                            sprite, UI, inset, bar, card)

ART = km.ART
GOLD = (0xF2, 0xC9, 0x4C, 255)


def art(rel, size):
    return load(os.path.join(ART, rel), size)


def popup(height, title, subtitle):
    """가운데 창: 어두운 막 + 크림 패널 + 위에 걸친 리본 제목 + 모서리 X (일터 · 왕국 창과 같은 부품)"""
    img = km.backdrop().filter(km.ImageFilter.GaussianBlur(6))  # 캡처 속 다른 창이 비치지 않게
    img.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 128)))
    pw = 980
    px = (W - pw) // 2
    py = (H - height) // 2 + 30
    put(img, nine("Inventory/UI_Inventory_Panel", pw, height, (252, 245, 230), (72, 72, 72, 80)), px, py)
    rw = tw(title, 46, True) + 140
    put(img, nine("Common/UI_Ribbon_Peach", rw, 104, (245, 199, 173)), (W - rw) // 2, py - 52)
    T(img, (W // 2, py - 4), title, 46, True, anchor="mm")
    put(img, sprite("Inventory/UI_Inventory_CloseButton"), px + pw + 20 - 96, py - 34)
    T(img, (W // 2, py + 82), subtitle, 28, False, MUTED, "mm")
    return img, px, py, pw


def speaker(img, x, y, w, portrait, name, line):
    """주민 얼굴 + 말풍선"""
    put(img, nine("Inventory/UI_Inventory_Slot_Filled", 112, 112), x, y)
    put_c(img, art(portrait, 92), x + 56, y + 52)
    bx = x + 132
    bw = w - 132
    put(img, nine("Common/UI_Button_Paper", bw, 112), bx, y)
    d = ImageDraw.Draw(img)
    d.polygon([(bx + 4, y + 44), (bx - 18, y + 58), (bx + 4, y + 72)], fill=(255, 251, 244, 255))
    d.line([(bx + 2, y + 44), (bx - 18, y + 58), (bx + 2, y + 72)], fill=COCOA, width=6, joint="curve")
    T(img, (bx + 26, y + 18), name, 24, True, MUTED)
    T(img, (bx + 26, y + 52), line, 26, False, COCOA)


def stepper(img, x, y, w, steps, current):
    """단계: 끝냄(초록 · 체크) / 지금(금색) / 남음(회색)을 선으로 이음"""
    n = len(steps)
    gap = w / n
    d = ImageDraw.Draw(img)
    cy = y + 30
    for i in range(n - 1):
        x0 = x + gap * (i + 0.5) + 34
        x1 = x + gap * (i + 1.5) - 34
        d.line([(x0, cy), (x1, cy)], fill=(0x7F, 0xA8, 0x6C, 255) if i < current else (0xD6, 0xC6, 0xB4, 255), width=7)
    for i, label in enumerate(steps):
        cx = x + gap * (i + 0.5)
        if i < current:
            check(img, cx, cy, True, 26)
        elif i == current:
            d.ellipse([cx - 30, cy - 30, cx + 30, cy + 30], fill=GOLD, outline=COCOA, width=5)
            T(img, (cx, cy - 2), str(i + 1), 28, True, COCOA, "mm")
        else:
            d.ellipse([cx - 26, cy - 26, cx + 26, cy + 26], fill=(0xF3, 0xEA, 0xDE, 255), outline=(0xC9, 0xB5, 0xA3, 255), width=4)
            T(img, (cx, cy - 2), str(i + 1), 24, True, MUTED, "mm")
        T(img, (cx, cy + 56), label, 23, i == current, COCOA if i <= current else MUTED, "mm")


def material(img, x, y, w, icon, name, delivered, required, owned, state):
    """재료 한 줄: 아이콘 · 이름 · 막대(넣음/필요) · 보유 · 오른쪽 [넣기] / 완료 / 부족"""
    h = 128
    card(img, x, y, w, h)
    tile(img, x + 20, y + 14, 96, icon, filled=state == "done")
    T(img, (x + 136, y + 22), name, 30, True)
    T(img, (x + 136 + tw(name, 30, True) + 14, y + 28), f"보유 {owned:,}", 22, False, RED if state == "short" else MUTED)
    bw = w - 136 - 340
    bar(img, x + 136, y + 72, bw, delivered / required, 24, None)
    T(img, (x + 136 + bw + 14, y + 84), f"{delivered:,} / {required:,}", 24, True, GREEN if state == "done" else COCOA, "lm")
    if state == "done":
        tag(img, x + w - 26, y + 44, "완료", "on", 26, anchor="r")
    elif state == "ok":
        button(img, x + w - 186, y + 26, 166, 76, "넣기 1", "primary", 30)
    else:
        button(img, x + w - 186, y + 26, 166, 76, "부족 8", "secondary", 28)
    return y + h + 14


def deliver():
    img, px, py, pw = popup(1250, "마을 생활 준비", "마을회관 물품 보충 · 3번 중 2번째")
    x, w = px + 44, pw - 88
    y = py + 120
    speaker(img, x, y, w, "Otter/ICON_Otter_Cast_Sailor.png", "또박이", "마을 살림을 조금씩 채워 두면 마을이 더 든든해져요.")
    y += 140
    stepper(img, x, y, w, ["재료 모으기", "물품 정리", "완성"], 0)
    y += 120
    T(img, (x + 6, y), "필요한 재료", 30, True)
    T(img, (x + w - 6, y + 4), "넣은 재료는 돌려받을 수 없어요", 22, False, MUTED, "ra")
    y += 50
    y = material(img, x, y, w, art("UI/Currency/Gold.png", 80), "골드", 2000, 2000, 18450, "done")
    y = material(img, x, y, w, art("Settlement/Prop_Rock_0.png", 80), "돌", 15, 16, 999, "ok")
    y = material(img, x, y, w, art("Settlement/ICON_Item_Apple.png", 80), "사과", 0, 8, 0, "short")
    T(img, (x + 136, y - 4), "사과는 광장 사과나무를 흔들면 떨어져요", 22, False, MUTED)
    y += 46
    # 완성 보상
    inset(img, x, y, w, 132)
    put_c(img, art("Settlement/Prop_FlowerPot.png", 92), x + 70, y + 66)
    T(img, (x + 136, y + 22), "완성하면", 26, True)
    T(img, (x + 136, y + 60), "3번 마치면 마을회관 앞이 꽃으로 꾸며져요", 24, False, COCOA)
    tag(img, x + w - 20, y + 20, "경험치 +120", "gold", 22, anchor="r")
    y += 160
    button(img, x, y, w, 104, "가능한 만큼 넣기 · 돌 1", "primary", 36)
    T(img, (W // 2, y + 140), "첫 모임 다시 보기", 24, False, MUTED, "mm")
    return img


def stage():
    img, px, py, pw = popup(1060, "환영 준비", "새 이웃 맞이하기")
    x, w = px + 44, pw - 88
    y = py + 120
    speaker(img, x, y, w, "Otter/ICON_Otter_Cast_Sailor.png", "또박이", "새 이웃이 오면 반겨 줄 소품을 하나 골라 볼까요?")
    y += 140
    stepper(img, x, y, w, ["재료 모으기", "소품 고르기", "입주", "모임"], 1)
    y += 120
    T(img, (x + 6, y), "소품 하나를 골라 주세요", 30, True)
    T(img, (x + w - 6, y + 4), "비용 · 시간은 같아요", 22, False, MUTED, "ra")
    y += 52
    cw = (w - 2 * 18) // 3
    for i, (icon, name) in enumerate([("Settlement/Prop_FlowerPot.png", "꽃 화분"), ("Settlement/ICON_Lamp.png", "가로등"),
                                      ("Settlement/Prop_Clothesline.png", "빨랫줄")]):
        cx = x + i * (cw + 18)
        sel = i == 0
        card(img, cx, y, cw, 250, ring=sel)
        put_c(img, art(icon, 140), cx + cw // 2, y + 100)
        T(img, (cx + cw // 2, y + 204), name, 28, True, anchor="mm")
        if sel:
            check(img, cx + cw - 30, y + 30, True, 22)
    y += 280
    inset(img, x, y, w, 100)
    T(img, (x + 30, y + 50), "공사 2분 · 재료는 이미 냈어요", 26, False, COCOA, "lm")
    tag(img, x + w - 20, y + 26, "재료 완료", "on", 22, anchor="r")
    y += 130
    button(img, x, y, w, 104, "건설 시작", "primary", 38)
    return img


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in [("10_project_deliver", deliver), ("11_project_stage", stage)]:
        fn().convert("RGB").save(os.path.join(out, name + ".png"))
        print("saved", name)


if __name__ == "__main__":
    main()
