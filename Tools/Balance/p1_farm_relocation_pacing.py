# 밭 재배치(밭 = Lv.10 농업 지역) 뒤 Lv.1 → Lv.11 속도 시뮬레이터.
# 메인 퀘스트를 순서대로 하나씩 하고, 그 레벨의 메인을 다 하면 다음 레벨 (도전 · 일일의 덤 경험치는 빼서 느린 쪽으로 봄).
# 실제 데이터 값으로 플레이어 행동을 흉내 내 여러 번 돌리고 레벨마다 도달 시간(중앙값 · 느린 쪽 90%)과 무엇을 기다렸는지 보여 준다.
# 실행: python Tools/Balance/p1_farm_relocation_pacing.py   (비교: --old  = 예전처럼 Lv.4에 밭)
#
# 값의 출처 (바뀌면 여기도 고칠 것)
#   광장 줍기 · 채굴 · 시작 재료        p0_pacing.py와 같음 (SettlementSetup.Plaza.cs, MiningBalanceData.asset: 15~30초, 다이아 30% +5%/곡괭이, 50골드)
#   곡괭이 강화 100 · 200 · 400 · 800    MiningBalanceData.pickaxeUpgradeCosts
#   건설                                 Constructions/*.asset (게시판 보강 300g·목12·돌6·30초, 접수 창구 600·24·12·60초, 마을 길드 1200·36·24·120초, 개간 200·16·10·45초)
#   공동사업                             Projects/*.asset (첫 비축 300·목12·돌6, 이웃 800·24·16, 환영 150·목6, 첫 모임 300·목8·당근12, 영토 1단계 500·40·15)
#   영토 개간                            1단계 3번 (3 · 4 · 5분, 목재 30 · 35 · 40), 기회 = Lv.6에 1번 + 레벨마다 1번
#   상점                                 축구공 800골드, 목재 10개 300골드 (FairyShopSetup.cs)
#   당근                                 30초 · 5개 · 6골드, 밭 1 = 3칸 (CropDefinition_Carrot.asset)
#   퀘스트 골드                          QuestSetup.cs 표 (아래 STEPS의 reward)
import argparse
import random
import statistics

BRANCHES, BRANCH_AMOUNT, BRANCH_COOLDOWN = 2, 2, 60
ROCKS, ROCK_AMOUNT, ROCK_COOLDOWN = 2, 3, 150
TREES, TREE_WOOD, TREE_REST, TREE_SHAKES = 2, 4, 120, 3
APPLE_CHANCE, APPLE_PRICE = 0.25, 10
MINE_INTERVAL = (15, 30)
DIAMOND_CHANCE, DIAMOND_PER_LEVEL, DIAMOND_PRICE = 0.30, 0.05, 50
PICKAXE_COSTS = [100, 200, 400, 800]
SOCCER_BALL = 800
WOOD_BUNDLE, WOOD_BUNDLE_PRICE = 10, 300
CARROT_SECS, CARROT_YIELD, CARROT_PRICE, CARROT_SLOTS = 30, 5, 6, 3
CLEAR_SECS = [180, 240, 300]
CLEAR_WOOD = [30, 35, 40]
HORIZON = 8 * 3600

# (레벨, 이름, 종류, 값, 퀘스트 골드)
#   build: dict(gold, wood, stone, carrot, secs)   ores/sales: 이 단계가 열린 뒤 쌓을 개수 · 골드   wait: 초
#   toys: 축구공 개수   upgrade: 곡괭이 1단계   territory: 영토 1단계 (개간 3번 + 미션)   farm: 밭이 일하기 시작
NEW = [
    (1, "첫 집", "build", dict(gold=0, wood=8, stone=5, secs=10), 30),
    (1, "재료 10개", "gather", 10, 30),
    (1, "판매 10골드", "sales", 10, 30),
    (1, "의자", "build", dict(gold=50, wood=6, stone=0, secs=8), 50),
    (2, "광산 가 보기", "wait", 20, 30),
    (2, "바위 돌 6개", "gather", 6, 30),
    (2, "광산 길", "build", dict(gold=0, wood=0, stone=0, secs=40, mine=True), 50),
    (3, "깡깡이 배치", "wait", 10, 50),
    (3, "광석 5개", "ores", 5, 30),
    (3, "새 이웃의 집", "build", dict(gold=150, wood=18, stone=8, secs=30), 100),
    (4, "요정 만나기", "wait", 30, 50),
    (4, "장난감 2개", "toys", 2, 300),
    (4, "광석 20개", "ores", 20, 80),
    (4, "판매 300골드", "sales", 300, 50),
    (5, "곡괭이 강화", "upgrade", 1, 300),
    (5, "게시판 보강", "build", dict(gold=300, wood=12, stone=6, secs=30), 100),
    (5, "광석 60개", "ores", 60, 150),
    (6, "또박이 배치", "wait", 10, 50),
    (6, "첫 구역 정하기", "wait", 30, 150),
    (6, "쉴 자리", "wait", 120, 50),
    (6, "상점에서 사기 (목재 묶음)", "buy_wood", 1, 100),
    (6, "길드 접수 창구", "build", dict(gold=600, wood=24, stone=12, secs=60), 200),
    (7, "마을 길드", "build", dict(gold=1200, wood=36, stone=24, secs=120), 300),
    (7, "첫 비축", "build", dict(gold=300, wood=12, stone=6, secs=180), 100),
    (7, "광석 50개", "ores", 50, 200),
    (8, "첫 영토", "territory", None, 200),
    (8, "넓힌 땅 구역 정하기", "wait", 30, 200),
    (8, "판매 3,000골드", "sales", 3000, 200),
    (9, "새 이웃 맞이하기", "build", dict(gold=800, wood=24, stone=16, secs=300), 200),
    (9, "환영 공간", "build", dict(gold=150, wood=6, stone=0, secs=120), 200),
    (9, "일일 1개", "wait", 300, 100),
    (9, "광석 150개", "ores", 150, 300),
    (10, "농업 지역 정하기", "wait", 30, 200),
    (10, "밭 개간", "build", dict(gold=200, wood=16, stone=10, secs=45 + 180), 100),
    (10, "새싹이 배치", "farm", None, 50),
    (10, "당근 3개", "carrots", 3, 30),
    (10, "첫 모임", "build", dict(gold=300, wood=8, stone=0, carrot=12, secs=300), 300),
    (10, "판매 10,000골드", "sales", 10000, 500),
    (11, "Lv.11 도착", "wait", 0, 0),
]

# 비교: 예전 흐름 (Lv.4에 밭, 장난감 · 영토 · 구역 없음). 퀘스트 골드는 대략
OLD = [s for s in NEW if s[0] <= 3] + [
    (4, "밭 개간", "build", dict(gold=200, wood=16, stone=10, secs=45 + 180), 100),
    (4, "새싹이 배치", "farm", None, 50),
    (4, "당근 3개", "carrots", 3, 30),
    (4, "판매 300골드", "sales", 300, 50),
    (5, "게시판 보강", "build", dict(gold=300, wood=12, stone=6, secs=30), 100),
    (5, "작물 60개", "carrots", 60, 100),
    (6, "또박이 배치", "wait", 10, 50),
    (6, "쉴 자리", "wait", 120, 50),
    (6, "감자 20개", "carrots", 20, 100),
    (6, "길드 접수 창구", "build", dict(gold=600, wood=24, stone=12, secs=60), 200),
    (7, "마을 길드", "build", dict(gold=1200, wood=36, stone=24, secs=120), 300),
    (7, "첫 비축", "build", dict(gold=300, wood=12, stone=6, secs=180), 100),
    (7, "광석 50개", "ores", 50, 200),
    (8, "첫 영토", "territory", None, 200),
    (8, "고구마 30개", "carrots", 30, 200),
    (8, "판매 3,000골드", "sales", 3000, 200),
    (9, "새 이웃 맞이하기", "build", dict(gold=800, wood=24, stone=16, secs=300), 200),
    (9, "환영 공간", "build", dict(gold=150, wood=6, stone=0, secs=120), 200),
    (9, "일일 1개", "wait", 300, 100),
    (9, "작물 300개", "carrots", 300, 300),
    (10, "첫 모임", "build", dict(gold=300, wood=8, stone=0, carrot=12, secs=300), 300),
    (10, "판매 10,000골드", "sales", 10000, 500),
    (11, "Lv.11 도착", "wait", 0, 0),
]


def run(steps, check_every, rng):
    t = 0
    gold, wood, stone, carrots = 100, 8, 5, 0
    pickaxe = 1
    mining, farming = False, False
    next_find, next_crop = None, None
    branch_ready, rock_ready, tree_ready = [0] * BRANCHES, [0] * ROCKS, [0] * TREES
    gathered = ores = sales = carrots_made = 0
    reached = {}
    waits = {"골드": 0, "목재": 0, "돌": 0, "당근": 0, "시간(공사 · 개간)": 0, "모으기(광석 · 판매 · 수확)": 0}
    i = 0
    level = 1
    busy_until = 0
    counter_base = None
    clear_index = 0
    chances = 0

    def sell(amount):
        nonlocal gold, sales
        gold += amount
        sales += amount

    while t < HORIZON and i < len(steps):
        # 채굴 · 밭은 늘 돌아감
        if mining and t >= next_find:
            ores += 1
            if rng.random() < DIAMOND_CHANCE + DIAMOND_PER_LEVEL * (pickaxe - 1):
                sell(DIAMOND_PRICE)
            else:
                stone += 1
            next_find = t + rng.uniform(*MINE_INTERVAL)
        if farming and t >= next_crop:
            made = CARROT_YIELD * CARROT_SLOTS
            carrots += made
            carrots_made += made
            next_crop = t + CARROT_SECS

        if t % check_every == 0:
            for k in range(BRANCHES):
                if t >= branch_ready[k]:
                    wood += BRANCH_AMOUNT; gathered += BRANCH_AMOUNT; branch_ready[k] = t + BRANCH_COOLDOWN
            for k in range(ROCKS):
                if t >= rock_ready[k]:
                    stone += ROCK_AMOUNT; gathered += ROCK_AMOUNT; rock_ready[k] = t + ROCK_COOLDOWN
            for k in range(TREES):
                if t >= tree_ready[k]:
                    wood += TREE_WOOD; gathered += TREE_WOOD
                    apples = sum(rng.random() < APPLE_CHANCE for _ in range(TREE_SHAKES))
                    sell(apples * APPLE_PRICE)
                    tree_ready[k] = t + TREE_REST
            # 지시가 없는 남는 당근은 팜 (첫 모임 재료 12개는 남김)
            keep = 12 if any(s[1] == "첫 모임" for s in steps[i:]) else 0
            if carrots > keep:
                sell((carrots - keep) * CARROT_PRICE)
                carrots = keep

            lv, name, kind, value, reward = steps[i]
            if lv > level:
                level = lv
                reached[f"Lv.{level}"] = t
                if level >= 6:
                    chances += 1 if level > 6 else 1
            done = False
            if t < busy_until:
                waits["시간(공사 · 개간)"] += check_every
            elif kind == "build":
                need = dict(골드=value["gold"], 목재=value["wood"], 돌=value["stone"], 당근=value.get("carrot", 0))
                have = dict(골드=gold, 목재=wood, 돌=stone, 당근=carrots)
                lacking = [k for k in need if have[k] < need[k]]
                # 목재만 모자라고 골드가 넉넉하면 상점에서 목재를 삼
                if lacking == ["목재"] and level >= 4 and gold >= need["골드"] + WOOD_BUNDLE_PRICE:
                    gold -= WOOD_BUNDLE_PRICE; wood += WOOD_BUNDLE
                    lacking = []
                if lacking:
                    for k in lacking:
                        waits[k] += check_every
                else:
                    gold -= need["골드"]; wood -= need["목재"]; stone -= need["돌"]; carrots -= need["당근"]
                    busy_until = t + value["secs"]
                    if value.get("mine"):
                        mining = True
                        next_find = busy_until + 10
                    done = True
            elif kind == "wait":
                busy_until = t + value
                done = True
            elif kind in ("ores", "sales", "gather", "carrots"):
                current = dict(ores=ores, sales=sales, gather=gathered, carrots=carrots_made)[kind]
                if counter_base is None:
                    counter_base = current
                if current - counter_base >= value:
                    done = True
                    counter_base = None
                else:
                    waits["모으기(광석 · 판매 · 수확)"] += check_every
            elif kind == "toys":
                if gold >= SOCCER_BALL * value:
                    gold -= SOCCER_BALL * value
                    done = True
                else:
                    waits["골드"] += check_every
            elif kind == "buy_wood":
                if gold >= WOOD_BUNDLE_PRICE:
                    gold -= WOOD_BUNDLE_PRICE; wood += WOOD_BUNDLE
                    done = True
                else:
                    waits["골드"] += check_every
            elif kind == "upgrade":
                cost = PICKAXE_COSTS[pickaxe - 1]
                if gold >= cost:
                    gold -= cost; pickaxe += 1
                    done = True
                else:
                    waits["골드"] += check_every
            elif kind == "territory":
                # 기회가 생기는 대로 개간 3번 → 미션 (골드 500 · 목재 40 · 돌 15)
                if clear_index < 3:
                    if chances > 0:
                        chances -= 1
                        busy_until = t + CLEAR_SECS[clear_index]
                        wood += CLEAR_WOOD[clear_index]
                        clear_index += 1
                    else:
                        waits["시간(공사 · 개간)"] += check_every
                elif gold >= 500 and wood >= 40 and stone >= 15:
                    gold -= 500; wood -= 40; stone -= 15
                    done = True
                else:
                    for k, have, need in (("골드", gold, 500), ("목재", wood, 40), ("돌", stone, 15)):
                        if have < need:
                            waits[k] += check_every
            elif kind == "farm":
                farming = True
                next_crop = t + 10
                done = True

            if done:
                gold += reward
                reached[name] = t
                i += 1
        t += 1
    reached["_waits"] = waits
    return reached


def report(title, steps, check_every, keys, runs=300):
    rng = random.Random(7)
    results = [run(steps, check_every, rng) for _ in range(runs)]
    print(f"\n■ {title}")
    for key in keys:
        times = sorted(r[key] for r in results if key in r)
        if not times:
            print(f"  {key:16s}  {HORIZON // 3600}시간 안에 못 함")
            continue
        median = statistics.median(times) / 60
        slow = times[int(len(times) * 0.9) - 1] / 60
        missing = runs - len(times)
        note = f"  ({HORIZON // 3600}시간 안에 못 한 경우 {missing}번)" if missing else ""
        print(f"  {key:16s}  중앙 {median:6.1f}분   느린 쪽(90%) {slow:6.1f}분{note}")
    total = sum(sum(r["_waits"].values()) for r in results) or 1
    print("  기다린 이유 (평균):", ", ".join(f"{k} {statistics.mean(r['_waits'][k] for r in results) / 60:.0f}분"
                                       for k in results[0]["_waits"]))


# 제안: 밭이 없는 Lv.4~9의 모으기 목표를 광산 속도(광석 약 160개/시간)에 맞춤
TUNED_VALUES = {"장난감 2개": ("toys", 1), "광석 20개": ("ores", 10), "광석 60개": ("ores", 25), "광석 50개": ("ores", 30),
                "판매 3,000골드": ("sales", 1500), "광석 150개": ("ores", 60)}
TUNED = [(lv, name, *TUNED_VALUES[name], reward) if name in TUNED_VALUES else (lv, name, kind, value, reward)
         for lv, name, kind, value, reward in NEW]


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--old", action="store_true", help="예전 흐름(Lv.4 밭)도 같이")
    parser.add_argument("--tuned", action="store_true", help="목표를 줄인 제안도 같이")
    args = parser.parse_args()
    levels = [f"Lv.{n}" for n in range(4, 12)]
    report("새 흐름 · 열심히 (5초마다 확인)", NEW, 5, levels + ["밭 개간"])
    report("새 흐름 · 가끔 (2분마다 확인)", NEW, 120, levels + ["밭 개간"])
    if args.tuned:
        report("제안(목표 줄임) · 열심히 (5초마다 확인)", TUNED, 5, levels + ["밭 개간"])
        report("제안(목표 줄임) · 가끔 (2분마다 확인)", TUNED, 120, levels + ["밭 개간"])
    if args.old:
        report("예전 흐름 · 열심히 (5초마다 확인)", OLD, 5, levels + ["밭 개간"])
        report("예전 흐름 · 가끔 (2분마다 확인)", OLD, 120, levels + ["밭 개간"])
