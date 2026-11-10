"""번영의 시대 산업 자재 임시 아이콘 (스티커 스타일: 코코아 외곽선 · 파스텔). 정식 그림을 받으면 바꿈.

python Tools/UIGen/industry_icons.py → Assets/Art/Item/Industry/ICON_Item_Plank.png · ICON_Item_StoneBlock.png · ICON_Item_Ingot.png
"""
import os

from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Art', 'Item', 'Industry')
COCOA = (75, 46, 34, 255)
SIZE = 512
LINE = 18


def plank():
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 판재 세 장을 비스듬히 쌓음 (아래 → 위)
    boards = [(70, 300, 450, 380), (90, 220, 470, 300), (60, 140, 440, 220)]
    tops = [(214, 150, 96), (231, 172, 115), (240, 189, 132)]
    for (x0, y0, x1, y1), color in zip(boards, tops):
        d.rounded_rectangle((x0, y0, x1, y1), radius=18, fill=color + (255,), outline=COCOA, width=LINE)
        # 나뭇결
        mid = (y0 + y1) // 2
        d.line((x0 + 60, mid - 8, x1 - 120, mid - 8), fill=(190, 128, 78, 255), width=7)
        d.line((x0 + 150, mid + 12, x1 - 50, mid + 12), fill=(190, 128, 78, 255), width=7)
        # 끝 단면
        d.rounded_rectangle((x1 - 46, y0 + 14, x1 - 16, y1 - 14), radius=8, fill=(250, 214, 160, 255))
    # 새싹 장식 (목재 아이콘과 맞춤)
    d.ellipse((380, 380, 440, 430), fill=(120, 190, 90, 255), outline=COCOA, width=10)
    d.ellipse((420, 370, 476, 420), fill=(140, 205, 105, 255), outline=COCOA, width=10)
    return im


def stone_block():
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 반듯한 돌 블록 (윗면 · 앞면 · 옆면)
    top = [(120, 170), (300, 110), (430, 170), (250, 230)]
    front = [(120, 170), (250, 230), (250, 420), (120, 360)]
    side = [(250, 230), (430, 170), (430, 360), (250, 420)]
    d.polygon(top, fill=(222, 222, 214, 255))
    d.polygon(front, fill=(176, 176, 170, 255))
    d.polygon(side, fill=(150, 150, 146, 255))
    for face in (top, front, side):
        d.line(face + [face[0]], fill=COCOA, width=LINE, joint='curve')
    # 깎은 자국
    d.line((150, 260, 220, 292), fill=(140, 140, 134, 255), width=8)
    d.line((290, 300, 390, 266), fill=(120, 120, 116, 255), width=8)
    d.line((200, 165, 290, 135), fill=(196, 196, 188, 255), width=8)
    return im


def ingot():
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 주괴 두 개 (뒤 → 앞): 사다리꼴 윗면 · 앞면 · 옆면 + 반짝
    for dx, dy in ((70, -40), (-10, 70)):
        top = [(150 + dx, 170 + dy), (330 + dx, 170 + dy), (380 + dx, 210 + dy), (100 + dx, 210 + dy)]
        front = [(100 + dx, 210 + dy), (380 + dx, 210 + dy), (360 + dx, 290 + dy), (120 + dx, 290 + dy)]
        d.polygon(front, fill=(214, 150, 92, 255))
        d.polygon(top, fill=(246, 196, 128, 255))
        for face in (top, front):
            d.line(face + [face[0]], fill=COCOA, width=LINE, joint='curve')
        d.line((150 + dx, 232 + dy, 250 + dx, 232 + dy), fill=(250, 226, 180, 255), width=10)
    # 반짝
    d.polygon([(400, 92), (412, 120), (440, 132), (412, 144), (400, 172), (388, 144), (360, 132), (388, 120)], fill=(255, 250, 220, 255))
    return im


def gear(d, cx, cy, r, teeth, fill):
    import math
    pts = []
    for i in range(teeth * 2):
        a = math.pi * i / teeth
        rr = r if i % 2 == 0 else r * 0.78
        for k in (-0.22, 0.22):
            b = a + k * math.pi / teeth
            pts.append((cx + rr * math.cos(b), cy + rr * math.sin(b)))
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=COCOA, width=LINE, joint='curve')
    d.ellipse((cx - r * 0.32, cy - r * 0.32, cx + r * 0.32, cy + r * 0.32), fill=(120, 120, 128, 255), outline=COCOA, width=LINE)


def precision():
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    gear(d, 300, 220, 150, 9, (206, 214, 224, 255))
    gear(d, 150, 360, 95, 7, (240, 196, 128, 255))
    # 나사
    d.rounded_rectangle((330, 330, 380, 470), radius=10, fill=(190, 196, 206, 255), outline=COCOA, width=LINE)
    for y in (360, 390, 420, 450):
        d.line((330, y, 380, y - 12), fill=COCOA, width=6)
    d.rounded_rectangle((300, 300, 410, 345), radius=12, fill=(214, 220, 230, 255), outline=COCOA, width=LINE)
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make in (('ICON_Item_Plank', plank), ('ICON_Item_StoneBlock', stone_block), ('ICON_Item_Ingot', ingot),
                       ('ICON_Item_Precision', precision)):
        path = os.path.join(OUT, name + '.png')
        make().resize((256, 256), Image.LANCZOS).save(path)
        print(path)


if __name__ == '__main__':
    main()
