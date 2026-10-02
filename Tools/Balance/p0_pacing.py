# 정착 진행 P0 속도 시뮬레이터: 새 게임 → 첫 집 → 새 이웃의 집 → 농경지 개간(밭 해금)까지 몇 분 걸리는지.
# 실제 데이터 값으로 플레이어 행동을 흉내 내 여러 번 돌리고 중앙값·느린 쪽(90%) 시간을 보여 준다.
# 실행: python Tools/Balance/p0_pacing.py
#
# 값의 출처 (바뀌면 여기도 고칠 것)
#   건설 비용·시간  Assets/Scriptable Obejects/Settlement/Constructions/*.asset
#   시작 재료·골드  SettlementConfig.asset (목재 8, 돌 5, 골드 100)
#   나뭇가지 4곳 ×2개/60초, 돌무더기 2곳 ×1개/90초   SettlementSetup.Plaza.cs
#   채굴 15~30초마다, 다이아몬드 30% (곡괭이 레벨당 +5%), 다이아몬드 50골드, 곡괭이 Lv2 100골드  MiningBalanceData.asset
#   퀘스트 골드 보상  QuestSetup.cs (Lv1: 광석 5개 30, 재료 10개 30, 판매 200 → 50 / Lv2: 광석 20개 80, 재료 40개 80, 판매 1000 → 100, 업그레이드 1회 100, 일일 판매 1000 → 200)
import random
import statistics

HOUSE1 = dict(gold=0, wood=8, stone=5, secs=10)
HOUSE2 = dict(gold=150, wood=18, stone=8, secs=30)
FARMLAND = dict(gold=200, wood=16, stone=10, secs=45)

BRANCHES, BRANCH_AMOUNT, BRANCH_COOLDOWN = 4, 2, 60
PEBBLES, PEBBLE_AMOUNT, PEBBLE_COOLDOWN = 2, 1, 90
MINE_INTERVAL = (15, 30)
DIAMOND_CHANCE, DIAMOND_PER_LEVEL, DIAMOND_PRICE = 0.30, 0.05, 50
PICKAXE_LV2_COST = 100

TUTORIAL_SECS = 60   # 튜토리얼·게시판을 보고 첫 집을 시작하기까지
MINE_ON_AT = 120     # 광산에 가서 채굴을 켜는 시각
HORIZON = 3600


def run(check_every, upgrade_pickaxe, rng):
    t = 0
    gold, wood, stone = 100, 8, 5
    pickaxe = 1
    mining_on = False
    next_find = None
    branch_ready = [0] * BRANCHES
    pebble_ready = [0] * PEBBLES
    ores = gathered = sales = upgrades = 0
    level = 1
    claimed = set()
    stage = 0            # 0 첫 집 전, 1 첫 집 공사, 2 새 이웃의 집 전, 3 공사, 4 개간 전, 5 공사, 6 끝
    job_end = None
    marks = {}
    diamonds = 0
    waits = {"gold": 0, "wood": 0, "stone": 0}

    def quests():
        nonlocal gold, level
        lv1 = [("mine1", ores >= 5, 30), ("gather1", gathered >= 10, 30), ("sales1", sales >= 200, 50)]
        for key, done, reward in lv1:
            if done and key not in claimed:
                claimed.add(key)
                gold += reward
        if level == 1 and {"mine1", "gather1", "sales1"} <= claimed:
            level = 2
        if level >= 2:
            lv2 = [("mine2", ores >= 25, 80), ("gather2", gathered >= 50, 80), ("sales2", sales >= 1200, 100),
                   ("upgrade1", upgrades >= 1, 100), ("daily_sales", sales >= 1200, 200)]
            for key, done, reward in lv2:
                if done and key not in claimed:
                    claimed.add(key)
                    gold += reward

    while t < HORIZON and stage < 6:
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
            stage += 1
            marks[{2: "첫 집 완성", 4: "새 이웃의 집 완성", 6: "밭 해금"}[stage]] = t

        if t % check_every == 0:
            if not mining_on and t >= MINE_ON_AT:
                mining_on = True
                next_find = t + rng.uniform(*MINE_INTERVAL)
            for i in range(BRANCHES):
                if t >= branch_ready[i]:
                    wood += BRANCH_AMOUNT
                    gathered += BRANCH_AMOUNT
                    branch_ready[i] = t + BRANCH_COOLDOWN
            for i in range(PEBBLES):
                if t >= pebble_ready[i]:
                    stone += PEBBLE_AMOUNT
                    gathered += PEBBLE_AMOUNT
                    pebble_ready[i] = t + PEBBLE_COOLDOWN
            # 다이아몬드는 팔고 돌은 모음
            if diamonds:
                gold += diamonds * DIAMOND_PRICE
                sales += diamonds * DIAMOND_PRICE
                diamonds = 0
            quests()
            if upgrade_pickaxe and pickaxe == 1 and gold >= PICKAXE_LV2_COST and stage >= 2:
                gold -= PICKAXE_LV2_COST
                pickaxe = 2
                upgrades += 1
                quests()

            def start(cost, name):
                nonlocal gold, wood, stone, job_end, stage
                if gold >= cost["gold"] and wood >= cost["wood"] and stone >= cost["stone"]:
                    gold -= cost["gold"]
                    wood -= cost["wood"]
                    stone -= cost["stone"]
                    job_end = t + cost["secs"]
                    stage += 1
                    marks[name] = t

            if stage == 0 and t >= TUTORIAL_SECS:
                start(HOUSE1, "첫 집 시작")
            elif stage in (2, 4):
                cost = HOUSE2 if stage == 2 else FARMLAND
                # 무엇이 모자라 기다리는지 (확인 간격만큼 센다)
                for key, have in (("gold", gold), ("wood", wood), ("stone", stone)):
                    if have < cost[key]:
                        waits[key] += check_every
                start(cost, "새 이웃의 집 시작" if stage == 2 else "개간 시작")
        t += 1
    marks["_waits"] = waits
    return marks


def report(title, check_every, upgrade_pickaxe, runs=500):
    rng = random.Random(7)
    results = [run(check_every, upgrade_pickaxe, rng) for _ in range(runs)]
    print(f"\n■ {title}")
    for key in ["첫 집 시작", "첫 집 완성", "새 이웃의 집 시작", "새 이웃의 집 완성", "개간 시작", "밭 해금"]:
        times = [r[key] for r in results if key in r]
        if not times:
            print(f"  {key:10s}  도달 못 함")
            continue
        times.sort()
        median = statistics.median(times) / 60
        slow = times[int(len(times) * 0.9) - 1] / 60
        missing = runs - len(times)
        note = f"  (1시간 안에 못 한 경우 {missing}번)" if missing else ""
        print(f"  {key:10s}  중앙 {median:5.1f}분   느린 쪽(90%) {slow:5.1f}분{note}")
    for res, label in (("gold", "골드"), ("wood", "목재"), ("stone", "돌")):
        avg = statistics.mean(r["_waits"][res] for r in results) / 60
        print(f"  기다린 이유: {label} 부족 평균 {avg:4.1f}분")


if __name__ == "__main__":
    report("열심히 하는 플레이어 (5초마다 확인)", 5, False)
    report("열심히 + 첫 집 뒤 곡괭이 강화", 5, True)
    report("가끔 들어오는 플레이어 (2분마다 확인)", 120, False)
