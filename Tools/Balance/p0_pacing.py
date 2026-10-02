# 정착·개척 초반 속도 시뮬레이터: 새 게임 → 첫 집 → 의자(Lv.2, 광산 발견) → 광산 길 열기(Lv.3, 새 해달)
# → 새 이웃의 집 → 농경지 개간(밭 해금)까지 몇 분 걸리는지.
# 실제 데이터 값으로 플레이어 행동을 흉내 내 여러 번 돌리고 중앙값·느린 쪽(90%) 시간을 보여 준다.
# 실행: python Tools/Balance/p0_pacing.py
#
# 값의 출처 (바뀌면 여기도 고칠 것)
#   건설 비용·시간  SettlementSetup.cs Constructions (Assets/Scriptable Obejects/Settlement/Constructions/*.asset)
#   시작 재료·골드  SettlementConfig.asset (목재 8, 돌 5, 골드 100)
#   광장 줍기  나뭇가지 2곳 ×2개/60초, 바위 2곳 ×3개/150초, 사과나무 2그루 (3번에 목재 4, 120초, 25% 사과)  SettlementSetup.Plaza.cs
#   광산 개척  장애물 4개 (바위 2: 돌 2씩, 나무 2: 목재 2씩)  SettlementSetup.Mine.cs
#   채굴 15~30초마다, 다이아몬드 30% (곡괭이 레벨당 +5%), 다이아몬드 50골드, 곡괭이 Lv2 100골드  MiningBalanceData.asset
#   퀘스트 골드 보상  QuestSetup.cs (Lv1: 바위 돌 6개 30, 재료 10개 30, 판매 10 → 30 / Lv2: 광석 5개 30, 광석 20개 80, 재료 40개 80,
#                     판매 1000 → 100, 업그레이드 1회 100, 일일 판매 1000 → 200)
#   왕국 레벨  의자 완성 = Lv.2, 광산 길 열기 = Lv.3 (그 전에는 퀘스트 경험치가 차도 레벨이 묶임)
import random
import statistics

HOUSE1 = dict(gold=0, wood=8, stone=5, secs=10)
CHAIR = dict(gold=50, wood=6, stone=0, secs=8)
MINE_PATH = dict(gold=0, wood=0, stone=0, secs=40)   # 광산에 가서 장애물 4개를 치우는 시간 (이동 포함)
HOUSE2 = dict(gold=150, wood=18, stone=8, secs=30)
FARMLAND = dict(gold=200, wood=16, stone=10, secs=45)
STEPS = [("첫 집", HOUSE1), ("의자", CHAIR), ("광산 길", MINE_PATH), ("새 이웃의 집", HOUSE2), ("개간", FARMLAND)]
MINE_PATH_REWARD = dict(wood=4, stone=4)

BRANCHES, BRANCH_AMOUNT, BRANCH_COOLDOWN = 2, 2, 60
ROCKS, ROCK_AMOUNT, ROCK_COOLDOWN = 2, 3, 150
TREES, TREE_WOOD, TREE_REST, TREE_SHAKES = 2, 4, 120, 3
APPLE_CHANCE, APPLE_PRICE = 0.25, 10
MINE_INTERVAL = (15, 30)
DIAMOND_CHANCE, DIAMOND_PER_LEVEL, DIAMOND_PRICE = 0.30, 0.05, 50
PICKAXE_LV2_COST = 100

TUTORIAL_SECS = 60   # 튜토리얼·게시판을 보고 첫 집을 시작하기까지
MINE_ON_DELAY = 10   # 광산 길을 연 뒤 입구를 눌러 채굴을 켜기까지
HORIZON = 3600


def run(check_every, upgrade_pickaxe, rng):
    t = 0
    gold, wood, stone = 100, 8, 5
    pickaxe = 1
    mining_on = False
    next_find = None
    branch_ready = [0] * BRANCHES
    rock_ready = [0] * ROCKS
    tree_ready = [0] * TREES
    ores = gathered = sales = upgrades = 0
    rock_stone = 0       # 광장 바위에서 얻은 돌 (1레벨 퀘스트)
    level = 1            # 의자 → 2, 광산 길 → 3
    mine_open_at = None
    claimed = set()
    step = 0             # STEPS 중 다음에 시작할 것
    job_end = None
    marks = {}
    diamonds = 0
    waits = {"gold": 0, "wood": 0, "stone": 0}

    def quests():
        nonlocal gold
        table = [("rock1", 1, rock_stone >= 6, 30), ("gather1", 1, gathered >= 10, 30), ("sales1", 1, sales >= 10, 30),
                 ("mine1", 2, ores >= 5, 30), ("mine2", 2, ores >= 25, 80), ("gather2", 2, gathered >= 50, 80),
                 ("sales2", 2, sales >= 1200, 100), ("upgrade1", 2, upgrades >= 1, 100), ("daily_sales", 2, sales >= 1200, 200)]
        for key, need_level, done, reward in table:
            if level >= need_level and done and key not in claimed:
                claimed.add(key)
                gold += reward

    while t < HORIZON and step <= len(STEPS):
        # 채굴은 어느 씬에서든 진행 (화면을 보지 않아도)
        if mining_on and t >= next_find:
            ores += 1
            if rng.random() < DIAMOND_CHANCE + DIAMOND_PER_LEVEL * (pickaxe - 1):
                diamonds += 1
            else:
                stone += 1
            next_find = t + rng.uniform(*MINE_INTERVAL)
        if job_end is not None and t >= job_end:
            job_end = None
            name = STEPS[step - 1][0]
            marks[name + " 완성"] = t
            if name == "의자":
                level = 2
            elif name == "광산 길":
                level = 3
                mine_open_at = t
                wood += MINE_PATH_REWARD["wood"]
                stone += MINE_PATH_REWARD["stone"]
            if step == len(STEPS):
                break

        if t % check_every == 0:
            if not mining_on and mine_open_at is not None and t >= mine_open_at + MINE_ON_DELAY:
                mining_on = True
                next_find = t + rng.uniform(*MINE_INTERVAL)
            for i in range(BRANCHES):
                if t >= branch_ready[i]:
                    wood += BRANCH_AMOUNT
                    gathered += BRANCH_AMOUNT
                    branch_ready[i] = t + BRANCH_COOLDOWN
            for i in range(ROCKS):
                if t >= rock_ready[i]:
                    stone += ROCK_AMOUNT
                    rock_stone += ROCK_AMOUNT
                    gathered += ROCK_AMOUNT
                    rock_ready[i] = t + ROCK_COOLDOWN
            for i in range(TREES):
                if t >= tree_ready[i]:
                    wood += TREE_WOOD
                    gathered += TREE_WOOD
                    apples = sum(rng.random() < APPLE_CHANCE for _ in range(TREE_SHAKES))
                    gathered += apples
                    gold += apples * APPLE_PRICE
                    sales += apples * APPLE_PRICE
                    tree_ready[i] = t + TREE_REST
            # 다이아몬드는 팔고 돌은 모음
            if diamonds:
                gold += diamonds * DIAMOND_PRICE
                sales += diamonds * DIAMOND_PRICE
                diamonds = 0
            quests()
            if upgrade_pickaxe and pickaxe == 1 and mining_on and gold >= PICKAXE_LV2_COST:
                gold -= PICKAXE_LV2_COST
                pickaxe = 2
                upgrades += 1
                quests()

            if job_end is None and step < len(STEPS) and t >= TUTORIAL_SECS:
                name, cost = STEPS[step]
                lacking = [key for key in ("gold", "wood", "stone") if (gold, wood, stone)[("gold", "wood", "stone").index(key)] < cost[key]]
                for key in lacking:
                    waits[key] += check_every
                if not lacking:
                    gold -= cost["gold"]
                    wood -= cost["wood"]
                    stone -= cost["stone"]
                    job_end = t + cost["secs"]
                    marks[name + " 시작"] = t
                    step += 1
        t += 1
    marks["_waits"] = waits
    return marks


def report(title, check_every, upgrade_pickaxe, runs=500):
    rng = random.Random(7)
    results = [run(check_every, upgrade_pickaxe, rng) for _ in range(runs)]
    print(f"\n■ {title}")
    for key in ["첫 집 완성", "의자 완성", "광산 길 완성", "새 이웃의 집 완성", "개간 완성"]:
        times = [r[key] for r in results if key in r]
        if not times:
            print(f"  {key:10s}  도달 못 함")
            continue
        times.sort()
        median = statistics.median(times) / 60
        slow = times[int(len(times) * 0.9) - 1] / 60
        missing = runs - len(times)
        note = f"  (1시간 안에 못 한 경우 {missing}번)" if missing else ""
        label = {"의자 완성": "의자 완성 (Lv.2)", "광산 길 완성": "광산 길 (Lv.3)", "개간 완성": "밭 해금"}.get(key, key)
        print(f"  {label:14s}  중앙 {median:5.1f}분   느린 쪽(90%) {slow:5.1f}분{note}")
    for res, label in (("gold", "골드"), ("wood", "목재"), ("stone", "돌")):
        avg = statistics.mean(r["_waits"][res] for r in results) / 60
        print(f"  기다린 이유: {label} 부족 평균 {avg:4.1f}분")


if __name__ == "__main__":
    report("열심히 하는 플레이어 (5초마다 확인)", 5, False)
    report("열심히 + 채굴 시작 뒤 곡괭이 강화", 5, True)
    report("가끔 들어오는 플레이어 (2분마다 확인)", 120, False)
