# 보물 조개 뽑기(장난감 뽑기) 그림 가져오기.
# ChatGPT로 만든 원본(Assets/Art/Gacha에 넣어 둔 것)을 게임용 크기로 다듬어 제자리에 넣고,
# 원본은 ArtSource/Gacha에 webp로 보관한다 (Unity가 읽지 않는 곳).
# - 장난감 13종: Assets/Art/Item/Toy/ICON_Toy_*.png (512, 투명 여백 정리 후 가운데)
# - 조개 4종:    Assets/Art/Gacha/Shell_*.png (640)
# - 해달 3포즈:  Assets/Art/Gacha/Otter_Float_*.png (캔버스 그대로 1152x768 — 포즈를 바꿔 끼워도 몸 위치가 같게)
# - 배경 2장:    Assets/Art/Gacha/BG_Sea_*.png (원본 크기)
# - 배너 2장:    Assets/Art/Gacha/Banner_*.png (1200x800)
# - 한정 해달:   Assets/Art/Otter/ICON_Otter_Sallang.png (512)
# - 뽑기권·조각: Assets/Art/UI/Currency/{TicketPickup,TicketStandard,Shard}.png (512)
# 실행: python Tools/UIGen/import_gacha_art.py   (원본이 Assets/Art/Gacha에 없으면 ArtSource/Gacha에서 읽음)
import os
import shutil

from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
INBOX = os.path.join(ROOT, "Assets", "Art", "Gacha")
ARCHIVE = os.path.join(ROOT, "ArtSource", "Gacha")

# (원본 파일 이름(확장자 제외), 결과 경로(Assets 기준), 방식, 크기)
# 방식: icon = 투명 여백을 잘라 정사각형 가운데에 맞춤 / canvas = 캔버스 그대로 줄임 / fit = 비율 유지로 줄임
TABLE = [
    ("알록달록 파스텔 비치볼-1", "Art/Item/Toy/ICON_Toy_BeachBall", "icon", 512),
    ("귀여운 노란 고무오리-2", "Art/Item/Toy/ICON_Toy_RubberDuck", "icon", 512),
    ("아쿠아 모래양동이와 노란 삽-3", "Art/Item/Toy/ICON_Toy_SandBucket", "icon", 512),
    ("코랄빛 말랑 요요-4", "Art/Item/Toy/ICON_Toy_Yoyo", "icon", 512),
    ("따뜻한 종이배 아이콘-5", "Art/Item/Toy/ICON_Toy_PaperBoat", "icon", 512),
    ("알록달록 장난감 북-6", "Art/Item/Toy/ICON_Toy_Drum", "icon", 512),
    ("비눗방울 장난감 세트-7", "Art/Item/Toy/ICON_Toy_Bubbles", "icon", 512),
    ("작은 파스텔 회전목마-8", "Art/Item/Toy/ICON_Toy_Carousel", "icon", 512),
    ("리본 꼬리 파스텔 다이아몬드 연-9", "Art/Item/Toy/ICON_Toy_Kite", "icon", 512),
    ("파스텔 미니 열기구 장난감-10", "Art/Item/Toy/ICON_Toy_HotAirBalloon", "icon", 512),
    ("벚꽃 바람개비 장난감-11", "Art/Item/Toy/ICON_Toy_CherryPinwheel", "icon", 512),
    ("벚꽃잎 모양 연-12", "Art/Item/Toy/ICON_Toy_PetalKite", "icon", 512),
    ("파스텔 나비 모빌 장난감-13", "Art/Item/Toy/ICON_Toy_ButterflyMobile", "icon", 512),
    ("포근한 아이보리 보물 조개-1", "Art/Gacha/Shell_Common", "icon", 640),
    ("진주빛 하늘색 조개-2", "Art/Gacha/Shell_Rare", "icon", 640),
    ("황금빛 가리비 껍데기 아이콘-3", "Art/Gacha/Shell_Epic", "icon", 640),
    ("파스텔 벚꽃무늬 핑크 조개 아이콘-4", "Art/Gacha/Shell_Pickup", "icon", 640),
    ("편안히 누운 돌 든 바다수달-2", "Art/Gacha/Otter_Float_Rest", "canvas", 1152),
    ("조개를 두드리려는 흥겨운 수달-3", "Art/Gacha/Otter_Float_Ready", "canvas", 1152),
    ("배 위를 바라보는 놀란 수달-4", "Art/Gacha/Otter_Float_Surprise", "canvas", 1152),
    ("포근한 파스텔 바다 배경", "Art/Gacha/BG_Sea_Day", "fit", 1536),
    ("별빛 아래 잔잔한 보랏빛 밤바다", "Art/Gacha/BG_Sea_Night", "fit", 1536),
    ("8c892324-4973-450f-a291-48f348f3d430", "Art/Gacha/Banner_Pickup_Spring", "fit", 1200),
    ("f9fd73b7-b8b5-4fb3-ac3e-04c03e3cce11", "Art/Gacha/Banner_Standard", "fit", 1200),
    ("사랑이의 벚꽃 바람", "Art/Otter/ICON_Otter_Sallang", "icon", 512),
    ("별빛 조개 소환 티켓", "Art/UI/Currency/TicketPickup", "icon", 512),
    ("황금 조개 소환 티켓", "Art/UI/Currency/TicketStandard", "icon", 512),
    ("b357726b-a5c6-4cb7-b578-141e7596f1e0", "Art/UI/Currency/Shard", "icon", 512),
]


def find_source(name):
    for folder, ext in ((INBOX, ".png"), (ARCHIVE, ".webp"), (ARCHIVE, ".png")):
        path = os.path.join(folder, name + ext)
        if os.path.exists(path):
            return path
    return None


def icon(im, size):
    margin = size // 32
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()  # 흐린 점은 무시
    im = im.crop(bbox)
    k = (size - 2 * margin) / max(im.size)
    im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(im, ((size - im.width) // 2, (size - im.height) // 2))
    return out


def fit(im, longest):
    k = min(1.0, longest / max(im.size))
    if k >= 1.0:
        return im
    return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)


def main():
    os.makedirs(ARCHIVE, exist_ok=True)
    for name, target, mode, size in TABLE:
        src = find_source(name)
        if src is None:
            print(f"!! 원본 없음: {name}")
            continue
        im = Image.open(src)
        im = im.convert("RGBA") if mode in ("icon", "canvas") else im.convert("RGB")
        if mode == "icon":
            out = icon(im, size)
        else:
            out = fit(im, size)
        dst = os.path.join(ROOT, "Assets", target + ".png")
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        out.save(dst, optimize=True)

        # 원본 보관 (webp, 손실 압축 — 다시 다듬을 때 쓸 참고본)
        archived = os.path.join(ARCHIVE, name + ".webp")
        if src.startswith(INBOX):
            Image.open(src).save(archived, quality=92, method=6)
            os.remove(src)
        print(f"{os.path.basename(target):28s} {out.size[0]}x{out.size[1]}  <- {name}")

    # 원본을 다 옮겼으면 빈 받은 편지함 폴더 정리 (게임용 그림이 같은 폴더에 남아 있으면 그대로 둠)
    leftovers = [f for f in os.listdir(INBOX) if not f.endswith(".meta")] if os.path.isdir(INBOX) else []
    print("남은 파일:", leftovers)


if __name__ == "__main__":
    main()
