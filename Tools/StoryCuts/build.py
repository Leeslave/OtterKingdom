"""Draw the otter story cuts for the collection (도감 해달 이야기) into Assets/Art/Collection/Cutscenes.

    python Tools/StoryCuts/build.py              # every cut
    python Tools/StoryCuts/build.py otter_sleepy # cuts whose name starts with this
    python Tools/StoryCuts/build.py --sheet out.png   # also a contact sheet of what was drawn

Each cut is CUT_<entryId>_<A|B|C>.png (1536x1024), the name CollectionSetup.CreateStories looks for.
Moment photos (도감 순간 탭) go to Assets/Art/Collection/Moments/MOMENT_<name>.png, plus SIL_Moment.png.
Otters are the plaza rigs (RiggedLookSetup) drawn by rig.py; places and props are game art (scene.py).
"""
import argparse
import sys

from PIL import Image, ImageDraw

import scene as s
import worldmap
from scene import Cut, img, fit, map_bg, portrait_bg

OUT = s.ROOT / "Assets/Art/Collection/Cutscenes"
CUTS = {}


def cut(name):
    def register(fn):
        CUTS[name] = fn
        return fn
    return register


# Map spots (Plaza_WorldMap.jpg px)
PLAZA = (1900, 1620)
MEADOW = (900, 1050)
BEACH = (1500, 3330)
SHORE = (2350, 3500)
EAST_SANDS = (3500, 2700)

WAVE = -2.5          # near arm raised (paw by the cheek), drawn over the head
UP = dict(nearArm=-2.9, farArm=2.6, armOverHead=True)   # both arms up


# ---------------------------------------------------------------------------------------------
# 해달이 — the first otter, explorer (Walker)
@cut("otter_first_A")
def first_a():
    c = Cut(map_bg(*BEACH, zoom=1.15))
    c.prop("Gacha/Shell_Common.png", 1060, 860, 90, shadow=.8)
    c.otter("WalkerRigV1", 640, 820, 470)
    c.scatter("Settlement/FX_Twinkle.png", (900, 700, 1500, 1000), 7, 46, seed=4, alpha=.8)
    return c.grade((255, 226, 190), .25).vignette(.35)


@cut("otter_first_B")
def first_b():
    c = Cut(map_bg(*PLAZA, zoom=1.0))
    c.prop("Settlement/Prop_TownHall.png", 760, 430, 420, shadow=.9)
    c.prop("Plaza/Props/Prop_Tree.png", 180, 470, 420)
    c.prop("Plaza/Props/Prop_Tree.png", 1380, 420, 380, flip=True)
    c.otter("QuarterRigV14", 330, 690, 300)
    c.otter("FisherRigV1", 1210, 680, 300, flip=True)
    c.otter("MinerRigV1", 1020, 620, 280, flip=True)
    c.otter("BaseballRigV1", 470, 600, 270)
    c.otter("WalkerRigV1", 760, 960, 450, nearArm=WAVE, armOverHead=True, head="smile")
    return c.vignette(.3)


# 물감이 — painter (Plaid)
@cut("otter_painter_A")
def painter_a():
    c = Cut(map_bg(1700, 1450, zoom=1.1))
    c.prop("Settlement/Prop_TownHall.png", 1120, 420, 380, shadow=.9)
    c.prop("Plaza/Props/Prop_Tree.png", 260, 430, 380)
    painting = map_bg(1900, 1620, zoom=1.0).resize((420, 280))
    c.put(s.easel(400, 470, painting), 980, 930)
    c.otter("PlaidRigV1", 520, 910, 440)
    c.fx("Settlement/FX_Butterfly_0.png", [(1300, 600)], 70)
    return c.vignette(.3)


@cut("otter_painter_B")
def painter_b():
    c = Cut(map_bg(1700, 1450, zoom=1.6))
    c.blur_back(1024, 6)
    t = Cut(map_bg(1900, 1620, zoom=1.0))
    t.prop("Settlement/Prop_TownHall.png", 768, 560, 520)
    t.otter("PlaidRigV1", 1140, 900, 260)
    t.otter("WalkerRigV1", 420, 900, 260)
    picture = t.canvas.resize((840, 560))
    c.put(s.easel(860, 1000, picture), 900, 1110)
    c.otter("PlaidRigV1", 280, 1060, 620, head="smile", nearArm=-1.2)
    c.scatter("Settlement/FX_Sparkle.png", (500, 100, 1400, 600), 6, 60, seed=9)
    return c.vignette(.35)


# 꾸벅이 — sleepy woodcutter (BearPajama)
@cut("otter_sleepy_A")
def sleepy_a():
    c = Cut(map_bg(*MEADOW, zoom=1.05))
    c.prop("Plaza/Territory/Forest_Oak_0.png", 260, 470, 470, shadow=.7)
    c.prop("Plaza/Territory/Forest_Pine_0.png", 1340, 430, 430, shadow=.6)
    c.prop("Plaza/Territory/Forest_Oak_1.png", 900, 380, 360, shadow=.6)
    c.prop("Plaza/Props/Prop_Stump.png", 1040, 860, 200, shadow=.9)
    c.prop("Settlement/FX_Axe.png", 1040, 720, 120, angle=-25)
    c.otter("BearPajamaRigV1", 620, 900, 450, head="smile")
    c.scatter("Settlement/FX_WoodChip.png", (930, 650, 1180, 820), 6, 34, seed=2)
    return c.vignette(.3)


@cut("otter_sleepy_B")
def sleepy_b():
    c = Cut(map_bg(*MEADOW, zoom=1.3))
    c.prop("Plaza/Territory/Forest_Oak_0.png", 1180, 560, 620, shadow=.6)
    c.prop("Plaza/Props/Prop_Bench.png", 720, 900, 380, shadow=.9)
    c.otter("BearPajamaRigV1", 700, 790, 480, shadow=False, sit=1, droop=1, head="sleep")
    c.zzz(860, 300, 80)
    c.scatter("Settlement/FX_Leaf.png", (200, 80, 1400, 500), 5, 50, seed=5)
    return c.grade((255, 224, 170), .25).vignette(.35)


# 또박이 — notice-board keeper (Reception)
@cut("otter_receptionist_A")
def clerk_a():
    c = Cut(map_bg(2100, 1500, zoom=1.1))
    c.prop("Settlement/Prop_Board_Upgraded.png", 900, 840, 560, shadow=.8)
    c.otter("ReceptionRigV1", 450, 900, 450, nearArm=-1.0)
    return c.vignette(.3)


@cut("otter_receptionist_B")
def clerk_b():
    c = Cut(map_bg(2100, 1500, zoom=1.5))
    c.prop("Settlement/Prop_Board_Upgraded.png", 1050, 760, 780)
    c.blur_back(1024, 5)
    note = s.paper(300, 360, lines=0, pin=(230, 90, 80, 255))
    c.put(note, 980, 560, (.5, .5), angle=6)
    c.put(fit(img("Item/Vegetable/Carrot.png"), 210), 980, 570, (.5, .5), angle=6)
    c.otter("ReceptionRigV1", 430, 1080, 640, head="sleep", nearArm=-.6)
    c.fx("Settlement/FX_Droplet.png", [(560, 300)], 70)
    return c.vignette(.35)


# 포근이 — neighbour who moved in (Flower)
@cut("otter_p3_neighbor_A")
def neighbor_a():
    c = Cut(map_bg(2400, 1300, zoom=1.05))
    c.prop("Settlement/Stage_House_Red_3.png", 1050, 640, 520, shadow=.9)
    c.prop("Settlement/Prop_SupplyBox.png", 330, 900, 210, shadow=.9)
    c.otter("FlowerRigV1", 640, 920, 440, held=[dict(img=img("Settlement/Prop_SupplyBox.png"), height=130, at=(30, -95), layer=30)])
    return c.vignette(.3)


@cut("otter_p3_neighbor_B")
def neighbor_b():
    c = Cut(map_bg(2400, 1300, zoom=1.05))
    c.prop("Plaza/Props/Prop_House_Red_OtterStyle.png", 900, 700, 560, shadow=.9)
    c.prop("Plaza/Props/Prop_Lamp.png", 1380, 790, 330)
    c.otter("FlowerRigV1", 520, 930, 430, head="sleep", nearArm=-.4)
    c.night()
    c.prop("Settlement/Night_House_Red.png", 900, 700, 560)
    c.prop("Settlement/Night_Lamp.png", 1380, 790, 330)
    c.light(860, 560, 380, strength=.35).light(1310, 560, 200, strength=.5)
    c.scatter("Settlement/FX_Firefly.png", (100, 100, 1500, 700), 9, 40, seed=7)
    c.scatter("Settlement/FX_Twinkle.png", (60, 30, 1500, 220), 8, 30, seed=11, alpha=.8)
    return c.vignette(.4, (20, 20, 50))


# 깡깡이 — miner (Miner)
@cut("otter_miner_A")
def miner_a():
    c = Cut(portrait_bg("MineRework/Backgrounds/BG_01_pebble.png", .55))
    c.prop("Resources/MinerRigV1/rock_0.png", 1000, 860, 300, shadow=.9)
    c.otter("MinerRigV1", 600, 900, 450, nearArm=-2.6, armOverHead=True)
    c.scatter("Resources/MinerRigV1/stone_chip.png", (880, 560, 1150, 760), 6, 40, seed=3)
    c.fx("Settlement/FX_Sparkle.png", [(1060, 640)], 90)
    return c.vignette(.4)


@cut("otter_miner_B")
def miner_b():
    c = Cut(portrait_bg("MineRework/Backgrounds/BG_01_pebble.png", .7, zoom=1.3))
    c.blur_back(1024, 5)
    c.glow(900, 330, 520, alpha=.8)
    c.otter("MinerRigV1", 760, 1080, 680, head="smile", nearArm=-2.9, armOverHead=True,
            held=[dict(img=img("Item/Ore/Stone.png"), height=95, at=(-120, -250), layer=30)])
    c.scatter("Settlement/FX_Sparkle.png", (650, 150, 1150, 500), 7, 70, seed=4)
    return c.vignette(.4)


# 뚝딱이 — builder (Baseball)
@cut("otter_builder_A")
def builder_a():
    c = Cut(map_bg(2600, 1800, zoom=1.05))
    c.prop("Settlement/Stage_House_Blue_1.png", 1000, 760, 560, shadow=.9)
    c.prop("Resources/BaseballRigV1/log.png", 560, 930, 110, shadow=.9)
    c.otter("BaseballRigV1", 420, 900, 440, nearArm=-2.6, armOverHead=True,
            held=[dict(img=img("Resources/BaseballRigV1/hammer.png"), height=150, dy=-40, angle=-30)])
    c.scatter("Settlement/FX_WoodChip.png", (480, 760, 680, 900), 5, 34, seed=6)
    return c.vignette(.3)


@cut("otter_builder_B")
def builder_b():
    c = Cut(map_bg(2600, 1800, zoom=1.05))
    c.prop("Settlement/Stage_House_Blue_3.png", 950, 780, 600, shadow=.9)
    c.otter("BaseballRigV1", 420, 930, 450, head="smile", **UP)
    c.scatter("Settlement/FX_Sparkle.png", (700, 160, 1300, 520), 7, 70, seed=8)
    return c.vignette(.3)


# 찰칵이 — photographer (Photographer); C shows the photo with eyes closed
@cut("otter_photographer_A")
def photo_a():
    c = Cut(map_bg(*PLAZA, zoom=1.05))
    c.prop("Settlement/Building_PhotoStudio.png", 760, 520, 440, shadow=.9)
    c.otter("HeroRigV1", 1150, 900, 420, flip=True)
    c.otter("PhotographerRigV1", 400, 920, 450, nearArm=WAVE, armOverHead=True)
    return c.vignette(.3)


@cut("otter_photographer_B")
def photo_b():
    c = Cut(map_bg(*PLAZA, zoom=1.05))
    c.prop("Settlement/Building_PhotoStudio.png", 760, 520, 440, shadow=.9)
    c.otter("HeroRigV1", 1150, 900, 420, flip=True, head="smile")
    c.otter("PhotographerRigV1", 400, 920, 450, nearArm=WAVE, armOverHead=True)
    c.glow(470, 420, 700, (255, 255, 230), .95)
    c.fx("Resources/PhotographerRigV1/flash_star.png", [(470, 420)], 260)
    c.grade((255, 255, 255), .35, "screen")
    return c


@cut("otter_photographer_C")
def photo_c():
    c = Cut(map_bg(*PLAZA, zoom=1.6))
    c.blur_back(1024, 6)
    shot = Cut(map_bg(*PLAZA, zoom=1.05))
    shot.prop("Settlement/Building_PhotoStudio.png", 560, 420, 400, shadow=.9)
    shot.otter("HeroRigV1", 820, 960, 640, flip=True, head="sleep")
    photo = s.paper(560, 400, inner=shot.canvas.resize((560, 400)), color=(255, 255, 255, 255), radius=8)
    c.put(photo, 1030, 470, (.5, .5), angle=-6)
    c.otter("PhotographerRigV1", 380, 1080, 640, head="smile", nearArm=-1.4)
    c.fx("Settlement/FX_Sparkle.png", [(1300, 220), (760, 260, .7)], 70)
    return c.vignette(.35)


# 보들이 — sprout whisperer (Flower)
def sprout_rows(c, y0, rows=2, step=230, size=150, kinds=("Strawberry", "Radish", "Beet", "Potato")):
    for r in range(rows):
        for i in range(6):
            x = 140 + i * step + (r % 2) * step / 2
            c.prop("Farm/Plaza/FARM_Soil_1.png", x, y0 + r * 150 + 50, width=size * 1.4)
            c.prop(f"Item/Vegetable/ICON_Seedling_{kinds[(i + r) % len(kinds)]}.png", x, y0 + r * 150 + 10, size)


@cut("otter_toy_bodeul_A")
def bodeul_a():
    c = Cut(map_bg(*MEADOW, zoom=1.1))
    c.prop("Settlement/Prop_FarmSign.png", 1360, 470, 220, shadow=.8)
    sprout_rows(c, 520)
    c.otter("FlowerRigV1", 700, 960, 440, head="smile", nearArm=-.9)
    return c.vignette(.3)


@cut("otter_toy_bodeul_B")
def bodeul_b():
    c = Cut(map_bg(*MEADOW, zoom=1.6))
    c.blur_back(1024, 5)
    c.prop("Farm/Plaza/FARM_Soil_1.png", 1000, 960, width=520)
    c.prop("Item/Vegetable/ICON_Seedling_Radish.png", 1000, 880, 380)
    c.scatter("Settlement/FX_Sparkle.png", (820, 380, 1240, 760), 6, 60, seed=12)
    c.otter("FlowerRigV1", 470, 1060, 620, head="smile", nearArm=-1.25)
    return c.grade((255, 230, 190), .2).vignette(.35)


# 데굴이 — pebble collector (Stripe)
@cut("otter_toy_degul_A")
def degul_a():
    c = Cut(map_bg(*SHORE, zoom=1.2))
    for x, y, k in ((980, 820, 140), (1180, 900, 110), (1300, 760, 90), (300, 880, 120)):
        c.prop("Settlement/Prop_Pebbles.png", x, y, k)
    c.otter("StripeRigV1", 640, 900, 450, nearArm=-1.2, held=[dict(img=img("Item/Ore/Stone.png"), height=90, dy=-10)])
    return c.vignette(.3)


@cut("otter_toy_degul_B")
def degul_b():
    c = Cut(map_bg(*SHORE, zoom=1.7))
    c.blur_back(1024, 5)
    for x, k, path in ((780, 130, "Item/Ore/Stone.png"), (1020, 110, "Settlement/Prop_Rock_1.png"), (1260, 140, "MineRework/Ores/ORE_pearl.png")):
        c.prop(path, x, 900, k, shadow=.9)
    c.fx("Settlement/FX_Sparkle.png", [(1300, 740), (1210, 800, .6)], 70)
    c.otter("StripeRigV1", 330, 1060, 620, head="smile", nearArm=WAVE, armOverHead=True)
    return c.vignette(.35)


# 두리번 — pathfinder (Hoodie)
@cut("otter_toy_duribeon_A")
def duribeon_a():
    c = Cut(map_bg(3200, 900, zoom=1.0))
    for x, y, h, p in ((140, 420, 430, "Pine_0"), (420, 330, 360, "Pine_1"), (1150, 360, 400, "Oak_1"), (1420, 450, 440, "Pine_0"), (820, 260, 300, "Oak_0")):
        c.prop(f"Plaza/Territory/Forest_{p}.png", x, y, h, shadow=.6)
    c.otter("HoodieRigV1", 760, 920, 450, nearArm=WAVE, armOverHead=True)
    return c.vignette(.35)


@cut("otter_toy_duribeon_B")
def duribeon_b():
    c = Cut(map_bg(3200, 900, zoom=1.5))
    c.blur_back(1024, 6)
    c.put(worldmap.held_map(720, 540, angle=-4), 1040, 500, (.5, .5))
    c.fx("Settlement/FX_Sparkle.png", [(1240, 300)], 60)
    c.otter("HoodieRigV1", 360, 1070, 640, head="smile", nearArm=-1.3)
    return c.vignette(.35)


# 재잘이 — chatterbox (Stripe)
@cut("otter_toy_jaejal_A")
def jaejal_a():
    c = Cut(map_bg(2200, 1900, zoom=1.05))
    c.prop("Settlement/Building_Shop.png", 1050, 600, 470, shadow=.9)
    c.otter("StripeRigV1", 520, 920, 450, head="smile", nearArm=WAVE, armOverHead=True)
    c.bubble(700, 400, 280, icons=("Settlement/ICON_House_Red.png", "Settlement/Building_Shop.png"))
    return c.vignette(.3)


@cut("otter_toy_jaejal_B")
def jaejal_b():
    c = Cut(map_bg(2200, 1900, zoom=1.05))
    c.prop("Settlement/Prop_CommunityTable.png", 760, 720, 330, shadow=.9)
    c.otter("QuarterRigV14", 330, 690, 330)
    c.otter("FisherRigV1", 1190, 700, 330, flip=True)
    c.otter("MerchantRigV1", 1000, 560, 290, flip=True)
    c.otter("StripeRigV1", 560, 960, 430, head="smile", nearArm=WAVE, armOverHead=True)
    c.bubble(350, 300, 220, icons=("Item/Vegetable/Carrot.png",))
    c.bubble(1200, 300, 220, icons=("Item/Fish/Mackerel.png",))
    c.bubble(700, 480, 260, icons=("Settlement/FX_Sparkle.png", "Settlement/FX_Sparkle.png", "Settlement/FX_Sparkle.png"))
    return c.vignette(.3)


# 짤랑이 — coin-loving trader (Merchant)
@cut("otter_toy_jjalrang_A")
def jjalrang_a():
    c = Cut(map_bg(2200, 1900, zoom=1.05))
    c.prop("Settlement/Building_Shop.png", 1000, 640, 520, shadow=.9)
    c.otter("MerchantRigV1", 480, 920, 450, head="smile", nearArm=WAVE, armOverHead=True)
    c.fx("UI/Currency/Gold.png", [(720, 400), (790, 330, .8), (650, 300, .7)], 80, angle_range=20)
    return c.vignette(.3)


@cut("otter_toy_jjalrang_B")
def jjalrang_b():
    c = Cut(map_bg(2200, 1900, zoom=1.6))
    c.blur_back(1024, 6)
    c.glow(1000, 520, 520)
    c.prop("Settlement/Prop_SupplyBox.png", 1050, 900, 300, shadow=.9)
    c.prop("Item/Accessory/ICON_Accessory_LuckyNecklace.png", 1050, 640, 220)
    c.scatter("Settlement/FX_Sparkle.png", (850, 300, 1250, 700), 6, 60, seed=13)
    c.otter("MerchantRigV1", 420, 1070, 640, head="smile", nearArm=-1.2,
            held=[dict(img=img("UI/Currency/Gold.png"), height=90, dy=-6)])
    return c.vignette(.35)


# 끄적이 — night note-taker (Quiet)
@cut("otter_toy_kkeujeok_A")
def kkeujeok_a():
    c = Cut(map_bg(2100, 1500, zoom=1.05))
    c.prop("Plaza/Props/Prop_House_Blue_OtterStyle.png", 300, 500, 400, shadow=.8)
    c.prop("Settlement/Prop_Board_Upgraded.png", 900, 760, 460, shadow=.8)
    c.prop("Plaza/Props/Prop_Lamp.png", 1320, 840, 380)
    c.otter("QuietRigV1", 620, 940, 440, nearArm=-1.0)
    c.night()
    c.prop("Settlement/Night_House_Blue.png", 300, 500, 400)
    c.prop("Settlement/Night_Lamp.png", 1320, 840, 380)
    c.light(1240, 580, 260, strength=.5).light(300, 380, 200, strength=.3).light(620, 760, 260, strength=.2)
    c.scatter("Settlement/FX_Twinkle.png", (40, 20, 1500, 200), 10, 30, seed=14, alpha=.85)
    c.scatter("Settlement/FX_Firefly.png", (200, 300, 1400, 800), 6, 40, seed=15)
    return c.vignette(.45, (15, 15, 45))


@cut("otter_toy_kkeujeok_B")
def kkeujeok_b():
    c = Cut(map_bg(2100, 1500, zoom=1.6))
    c.prop("Plaza/Props/Prop_Lamp.png", 1300, 960, 820)
    c.blur_back(1024, 4)
    c.put(s.notebook(460, 320), 900, 700, (.5, .5), angle=-8)
    c.otter("QuietRigV1", 430, 1070, 640, head="smile", nearArm=-1.1)
    c.night()
    c.prop("Settlement/Night_Lamp.png", 1300, 960, 820)
    c.light(1120, 420, 380, strength=.5).light(860, 680, 300, strength=.35)
    c.scatter("Settlement/FX_Firefly.png", (500, 100, 1500, 700), 7, 44, seed=16)
    return c.vignette(.45, (15, 15, 45))


# 꼼꼼이 — careful tinkerer (Knit)
@cut("otter_toy_kkomkkom_A")
def kkomkkom_a():
    c = Cut(map_bg(*MEADOW, zoom=1.1))
    sprout_rows(c, 330, rows=1, kinds=("Radish", "Beet"))
    c.prop("Farm/Plaza/FARM_Sprinkler.png", 1000, 860, 280, shadow=.8)
    c.otter("KnitRigV1", 600, 940, 450, nearArm=-1.0, head_angle=.12)
    return c.vignette(.3)


@cut("otter_toy_kkomkkom_B")
def kkomkkom_b():
    c = Cut(map_bg(*MEADOW, zoom=1.1))
    sprout_rows(c, 330, rows=1, kinds=("Radish", "Beet"))
    c.prop("Farm/Plaza/FARM_Sprinkler.png", 1000, 860, 280, shadow=.8)
    pts = []
    for i in range(14):
        a = -3.0 + i * .23
        for r in (180, 300):
            pts.append((1000 + r * 1.3 * __import__("math").cos(a), 640 + r * .7 * __import__("math").sin(a), .9))
    c.fx("Settlement/FX_Droplet.png", pts, 40)
    c.otter("KnitRigV1", 520, 940, 450, head="smile", **UP)
    c.scatter("Settlement/FX_Sparkle.png", (850, 250, 1300, 500), 3, 60, seed=17)
    return c.vignette(.3)


# 콩콩이 — hammering builder (Artisan)
@cut("otter_toy_kongkong_A")
def kongkong_a():
    c = Cut(map_bg(2600, 1800, zoom=1.05))
    c.prop("Settlement/Building_Workyard.png", 1050, 560, 470, shadow=.9)
    c.prop("Resources/ArtisanRigV1/log.png", 690, 920, 120, shadow=.9)
    c.otter("ArtisanRigV1", 460, 910, 450, nearArm=-2.9, farArm=2.4, armOverHead=True,
            held=[dict(img=img("Resources/ArtisanRigV1/sledge.png"), height=300, dy=-130, angle=-160)])
    c.scatter("Settlement/FX_WoodChip.png", (600, 760, 820, 900), 6, 34, seed=18)
    return c.vignette(.3)


@cut("otter_toy_kongkong_B")
def kongkong_b():
    c = Cut(map_bg(2600, 1800, zoom=1.05))
    for i in range(4):
        c.prop("Plaza/Props/Prop_Fence_Rising.png", 700 + i * 200, 520 + i * 110, 230)
    c.scatter("Settlement/FX_Sparkle.png", (700, 300, 1400, 700), 6, 60, seed=19)
    c.otter("ArtisanRigV1", 420, 950, 460, head="smile", nearArm=WAVE, armOverHead=True)
    return c.vignette(.3)


# 쿵쿵이 — big lumberjack (Stripe, drawn bigger)
@cut("otter_toy_kungkung_A")
def kungkung_a():
    c = Cut(map_bg(3100, 1100, zoom=1.0))
    for x, y, h, p in ((150, 450, 440, "Oak_0"), (1380, 420, 460, "Pine_1"), (1080, 320, 360, "Oak_1")):
        c.prop(f"Plaza/Territory/Forest_{p}.png", x, y, h, shadow=.6)
    c.otter("StripeRigV1", 680, 990, 640)
    c.fx("Settlement/FX_Dust.png", [(500, 960), (880, 960, .8)], 90, alpha=.8)
    c.scatter("Settlement/FX_Leaf.png", (200, 100, 1400, 500), 6, 50, seed=20)
    return c.vignette(.3)


@cut("otter_toy_kungkung_B")
def kungkung_b():
    c = Cut(map_bg(3100, 1100, zoom=1.0))
    c.prop("Plaza/Territory/Forest_Pine_0.png", 1300, 440, 460, shadow=.6)
    c.prop("Plaza/Territory/Forest_Stump.png", 1150, 860, 200, shadow=.9)
    c.otter("StripeRigV1", 640, 980, 600, head="smile", **UP,
            held=[dict(img=img("Plaza/Props/Prop_Log.png"), height=190, at=(0, -390), layer=30)])
    c.scatter("Settlement/FX_Leaf.png", (200, 100, 1400, 500), 6, 50, seed=21)
    return c.vignette(.3)


# 퐁당이 — diver (Diver)
@cut("otter_toy_pongdang_A")
def pongdang_a():
    c = Cut(map_bg(1900, 3530, zoom=1.15))
    c.otter("DiverRigV1", 760, 780, 470, head="smile", shadow=False, **UP)
    # the sea covers the legs: re-paste the water band below the waist
    sea = map_bg(1900, 3530, zoom=1.15).convert("RGBA").crop((0, 680, 1536, 1024))
    c.canvas.alpha_composite(sea, (0, 680))
    c.fx("Settlement/FX_Ripple.png", [(760, 690, 1.0)], 300, alpha=.9)
    c.scatter("Settlement/FX_Droplet.png", (560, 220, 960, 520), 10, 40, seed=22)
    return c.vignette(.3)


@cut("otter_toy_pongdang_B")
def pongdang_b():
    c = Cut(map_bg(*BEACH, zoom=1.6))
    c.blur_back(1024, 5)
    c.glow(980, 520, 460, (255, 250, 230))
    c.otter("DiverRigV1", 520, 1070, 640, head="smile", nearArm=-1.25,
            held=[dict(img=img("MineRework/Ores/ORE_pearl.png"), height=120, dy=-4)])
    c.prop("Gacha/Shell_Rare.png", 1060, 860, 240, shadow=.8)
    c.scatter("Settlement/FX_Sparkle.png", (900, 380, 1260, 700), 6, 60, seed=23)
    return c.vignette(.35)


# 살랑이 — rides the cherry-blossom wind (Flower)
@cut("otter_toy_sallang_A")
def sallang_a():
    c = Cut(map_bg(*PLAZA, zoom=1.0))
    c.prop("Settlement/Prop_AppleTree_0.png", 1230, 520, 460, shadow=.7)
    c.otter("FlowerRigV1", 640, 920, 450, nearArm=WAVE, armOverHead=True,
            held=[dict(img=img("Item/Toy/ICON_Toy_CherryPinwheel.png"), height=170, dy=-60, layer=30)])
    c.scatter("Settlement/FX_Petal_0.png", (0, 0, 1536, 900), 18, 46, seed=24)
    c.scatter("Settlement/FX_Petal_1.png", (0, 0, 1536, 900), 14, 46, seed=25)
    return c.grade((255, 225, 235), .2).vignette(.3)


@cut("otter_toy_sallang_B")
def sallang_b():
    c = Cut(map_bg(*PLAZA, zoom=1.6))
    c.blur_back(1024, 6)
    c.glow(1000, 500, 520, (255, 220, 235))
    c.put(fit(img("Settlement/FX_Petal_0.png"), 260), 1000, 500, (.5, .5), angle=20)
    c.scatter("Settlement/FX_Sparkle.png", (840, 330, 1180, 680), 5, 50, seed=26)
    c.otter("FlowerRigV1", 430, 1070, 640, head="smile", nearArm=-1.25)
    c.scatter("Settlement/FX_Petal_0.png", (0, 0, 1536, 900), 14, 50, seed=27)
    c.scatter("Settlement/FX_Petal_1.png", (0, 0, 1536, 900), 10, 50, seed=28)
    return c.grade((255, 225, 235), .2).vignette(.35)


# 영차 — hauler (Knit)
@cut("otter_toy_yeongcha_A")
def yeongcha_a():
    c = Cut(map_bg(*MEADOW, zoom=1.05))
    sprout_rows(c, 280, rows=1, kinds=("Potato", "SweetPotato"))
    c.prop("Farm/Plaza/FARM_Crate.png", 1180, 860, 200, shadow=.9)
    c.prop("Farm/Plaza/FARM_Crate.png", 1330, 880, 180, shadow=.9)
    c.otter("KnitRigV1", 620, 960, 460, **UP,
            held=[dict(img=img("Farm/Plaza/FARM_Crate.png"), height=200, at=(0, -400), layer=30)])
    c.fx("Settlement/FX_Droplet.png", [(800, 420)], 50)
    return c.vignette(.3)


@cut("otter_toy_yeongcha_B")
def yeongcha_b():
    c = Cut(map_bg(1600, 2400, zoom=1.0))
    c.prop("Plaza/Props/Prop_Tree.png", 200, 500, 420, shadow=.6)
    c.prop("Plaza/Props/Prop_Lamp.png", 1380, 600, 300)
    c.otter("KnitRigV1", 620, 900, 440, head="smile")
    c.otter("HeroRigV1", 930, 900, 430, head="smile")
    c.grade((255, 196, 140), .35)
    c.scatter("Settlement/FX_Leaf.png", (200, 80, 1400, 400), 4, 46, seed=29)
    return c.vignette(.4, (80, 30, 10))


# ---------------------------------------------------------------------------------------------
# 순간포착 사진 (도감 순간 탭) — MOMENT_<entryId>.png. 함께한 해달은 그때그때 다르므로 그림 속 동료는 대표 모습
MOMENTS = s.ROOT / "Assets/Art/Collection/Moments"


@cut("moment_sleepy_bench")
def moment_sleepy_bench():
    c = Cut(map_bg(*MEADOW, zoom=1.3))
    c.prop("Plaza/Territory/Forest_Oak_0.png", 1240, 520, 600, shadow=.6)
    c.prop("Plaza/Props/Prop_Bench.png", 720, 920, width=700, shadow=.9)
    c.otter("BearPajamaRigV1", 590, 700, 380, shadow=False, sit=1, droop=1, head="sleep", head_angle=.3)
    c.otter("QuarterRigV14", 860, 745, 370, shadow=False, sit=1, droop=1, head="sleep", head_angle=.3, flip=True)
    c.zzz(500, 210, 64).zzz(930, 250, 58)
    c.scatter("Settlement/FX_Leaf.png", (150, 80, 1400, 450), 5, 50, seed=31)
    return c.grade((255, 220, 170), .3).vignette(.4)


@cut("moment_secret_table")
def moment_secret_table():
    c = Cut(map_bg(2200, 1900, zoom=1.15))
    c.otter("HoodieRigV1", 780, 640, 360, head="smile", head_angle=-.25)
    c.prop("Settlement/Prop_CommunityTable.png", 780, 860, 400, shadow=.9)
    c.otter("StripeRigV1", 470, 960, 440, head="smile", nearArm=-1.6, head_angle=.18)
    c.otter("FlowerRigV1", 1090, 960, 440, head="smile", nearArm=WAVE, armOverHead=True, flip=True)
    c.fx("Settlement/UI_Bubble_Alert.png", [(860, 170)], 110)
    c.scatter("Settlement/FX_Twinkle.png", (380, 260, 1200, 520), 7, 46, seed=32)
    return c.vignette(.35)


@cut("moment_night_notes")
def moment_night_notes():
    c = Cut(map_bg(2100, 1500, zoom=1.4))
    c.prop("Plaza/Props/Prop_Lamp.png", 1430, 900, 640)
    model, _ = s.rig.render("QuarterRigV14", 1.0, dict(head="smile"))
    face = model.crop((0, 0, model.width, int(model.height * .62)))
    book = s.notebook(440, 300, lines=0)
    sketch = fit(face, 200)
    book.alpha_composite(sketch, (int(book.width * .75 - sketch.width / 2), 50))
    c.otter("QuietRigV1", 290, 980, 520, head="smile", nearArm=-1.1)
    c.put(book, 640, 640, (.5, .5), angle=-6)
    c.otter("QuarterRigV14", 1110, 980, 500, head="smile", flip=True)
    c.night()
    c.prop("Settlement/Night_Lamp.png", 1430, 900, 640)
    c.light(1320, 460, 340, strength=.5).light(680, 640, 320, strength=.3)
    c.scatter("Settlement/FX_Firefly.png", (300, 100, 1500, 700), 8, 40, seed=33)
    return c.vignette(.45, (15, 15, 45))


@cut("moment_lamp_map")
def moment_lamp_map():
    c = Cut(map_bg(2100, 1500, zoom=1.4))
    c.prop("Plaza/Props/Prop_Lamp.png", 210, 900, 640)
    c.otter("HoodieRigV1", 500, 990, 500, head="smile", nearArm=-1.2, head_angle=.2)
    c.otter("KnitRigV1", 1260, 990, 500, head="smile", nearArm=-1.2, head_angle=.2, flip=True)
    c.night()
    c.prop("Settlement/Night_Lamp.png", 210, 900, 640)
    c.light(300, 460, 360, strength=.5).light(880, 600, 380, strength=.4)
    # 지도는 등불 빛 아래라 밤 색을 덜 탐 (밤 색 위에 따로 붙이고 살짝만 어둡게)
    held = worldmap.held_map(460, 345, angle=3)
    dim = Image.new("RGBA", held.size, (40, 40, 90, 0))
    dim.putalpha(held.getchannel("A").point(lambda a: int(a * .18)))
    held.alpha_composite(dim)
    c.put(held, 880, 600, (.5, .5))
    c.fx("Settlement/FX_Sparkle.png", [(880, 365), (1010, 390, .6), (750, 395, .6)], 64)
    c.scatter("Settlement/FX_Firefly.png", (300, 100, 1500, 700), 8, 40, seed=34)
    return c.vignette(.45, (15, 15, 45))


def moment_silhouette(path):
    """도감 순간 탭의 못 찍은 칸: 사진 한 장 실루엣 (다른 실루엣과 같은 짙은 갈색)"""
    im = Image.new("RGBA", (256, 256))
    d = ImageDraw.Draw(im)
    brown = (91, 58, 44, 255)
    d.rounded_rectangle((28, 52, 228, 214), 16, fill=brown)
    d.rounded_rectangle((44, 68, 212, 176), 8, fill=(0, 0, 0, 0))
    d.polygon([(52, 168), (102, 112), (138, 148), (160, 128), (204, 168)], fill=brown)
    d.ellipse((150, 80, 174, 104), fill=brown)
    im.rotate(-6, resample=Image.BICUBIC).save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("only", nargs="*")
    ap.add_argument("--sheet")
    ap.add_argument("--out", default=str(OUT))
    args = ap.parse_args()
    names = [n for n in CUTS if not args.only or any(n.startswith(o) for o in args.only)]
    drawn = []
    for n in names:
        moment = n.startswith("moment_")
        path = f"{MOMENTS if moment and args.out == str(OUT) else args.out}/{'MOMENT_' + n[len('moment_'):] if moment else 'CUT_' + n}.png"
        CUTS[n]().save(path)
        drawn.append((n, path))
        print("drew", path)
    if not args.only or any("moment".startswith(o) or o.startswith("moment") for o in args.only):
        moment_silhouette(s.ROOT / "Assets/Art/UI/Collection/Silhouettes/SIL_Moment.png")
    if args.sheet:
        tw, th = 384, 256
        cols = 4
        sheet = Image.new("RGB", (tw * cols, (th + 18) * ((len(drawn) + cols - 1) // cols)), "white")
        d = ImageDraw.Draw(sheet)
        for i, (n, p) in enumerate(drawn):
            x, y = (i % cols) * tw, (i // cols) * (th + 18)
            sheet.paste(Image.open(p).resize((tw, th)), (x, y))
            d.text((x + 4, y + th + 2), n, fill="black")
        sheet.save(args.sheet)


if __name__ == "__main__":
    main()
