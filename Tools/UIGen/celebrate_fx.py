# 축하 연출용 빛 그림 (흰색, 색은 코드에서 입힘). 날아다니는 조각 대신 빛과 반짝임으로 은은하게.
# - Assets/Resources/UI/Celebrate/Rays.png    팝업 뒤 천천히 도는 빛줄기
# - Assets/Resources/UI/Celebrate/Glow.png    팝업 뒤 따뜻한 빛 번짐 (검게 칠해 빛 둘레 그늘로도 씀)
# - Assets/Resources/UI/Celebrate/Twinkle.png 팝업 둘레에서 차례로 반짝이는 별빛
# - Assets/Art/Settlement/FX_Twinkle.png      (월드용 같은 별빛) 집이 완성될 때 집 위에서 반짝임
# 실행: python Tools/UIGen/celebrate_fx.py
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
UI_OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Celebrate")
WORLD_OUT = os.path.join(ROOT, "Assets", "Art", "Settlement")


def white_with_alpha(alpha):
    img = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
    img.putalpha(alpha)
    return img


def radial(size, power):
    """가운데 1 → 가장자리 0"""
    w, h = size
    a = Image.new("L", size, 0)
    px = a.load()
    for y in range(h):
        for x in range(w):
            dx = (x + 0.5 - w / 2) / (w / 2)
            dy = (y + 0.5 - h / 2) / (h / 2)
            d = math.hypot(dx, dy)
            if d < 1:
                px[x, y] = int(255 * (1 - d) ** power)
    return a


def rays():
    n = 512
    count = 14
    wedges = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(wedges)
    c = n / 2
    for i in range(count):
        a = i * 2 * math.pi / count
        half = math.radians(5.5)
        r = n * 0.75
        d.polygon([(c, c), (c + math.cos(a - half) * r, c + math.sin(a - half) * r), (c + math.cos(a + half) * r, c + math.sin(a + half) * r)], fill=255)
    wedges = wedges.filter(ImageFilter.GaussianBlur(6))
    # 바깥으로 갈수록 흐려지고, 한가운데도 살짝 비움 (빛줄기만 보이게)
    fade = radial((n, n), 1.3)
    hole = radial((n, n), 6).point(lambda v: 255 - int(v * 0.6))
    alpha = ImageChops.multiply(ImageChops.multiply(wedges, fade), hole)
    white_with_alpha(alpha).save(os.path.join(UI_OUT, "Rays.png"))


def glow():
    white_with_alpha(radial((256, 256), 2.2)).save(os.path.join(UI_OUT, "Glow.png"))


def twinkle():
    """가운데가 밝고 긴 십자 + 짧은 대각 빛살 + 부드러운 번짐"""
    n = 512
    c = n / 2
    spikes = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(spikes)
    for i, (length, width) in enumerate(((0.48, 0.035), (0.48, 0.035), (0.22, 0.022), (0.22, 0.022))):
        for sign in (1, -1):
            angle = (0 if i == 0 else math.pi / 2 if i == 1 else math.pi / 4 if i == 2 else -math.pi / 4)
            ax, ay = math.cos(angle) * sign, math.sin(angle) * sign
            px, py = -ay, ax
            tip = (c + ax * n * length, c + ay * n * length)
            base_l = (c + px * n * width, c + py * n * width)
            base_r = (c - px * n * width, c - py * n * width)
            d.polygon([base_l, tip, base_r], fill=255)
    spikes = spikes.filter(ImageFilter.GaussianBlur(3))
    core = radial((n, n), 5)
    halo = radial((n, n), 2.5).point(lambda v: int(v * 0.45))
    alpha = ImageChops.lighter(ImageChops.lighter(spikes, core), halo)
    img = white_with_alpha(alpha).resize((128, 128), Image.LANCZOS)
    img.save(os.path.join(UI_OUT, "Twinkle.png"))
    img.save(os.path.join(WORLD_OUT, "FX_Twinkle.png"))


def main():
    os.makedirs(UI_OUT, exist_ok=True)
    rays()
    glow()
    twinkle()
    print("saved celebrate fx")


if __name__ == "__main__":
    main()
