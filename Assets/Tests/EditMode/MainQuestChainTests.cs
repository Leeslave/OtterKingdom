using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 레벨별 메인 퀘스트 (성장곡선 기획서): 새 목표 종류(정착 단계·장소 가 보기·고랑 열기·가방 확장·일일 끝내기·특정 상품 사기),
/// 메인 체인이 생기기 전 세이브에서 지나온 레벨의 메인은 넘기기, 체인 저장 표시.
/// </summary>
public class MainQuestChainTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private QuestDefinition Quest(string id, QuestGoalType type, QuestKind kind = QuestKind.Main, int level = 1,
        string target = "", ItemDefinition item = null, int goal = 1)
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        _created.Add(quest);
        var so = new SerializedObject(quest);
        so.FindProperty("_questId").stringValue = id;
        so.FindProperty("_goalType").enumValueIndex = (int)type;
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.FindProperty("_requiredLevel").intValue = level;
        so.FindProperty("_target").stringValue = target;
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_goal").intValue = goal;
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private ItemDefinition Item()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        _created.Add(item);
        return item;
    }

    // Lv.5가 한꺼번에 열리지 않게: 게시판 보강 → 또박이 배치 → 첫 구역 → … → 광산 연락(오래된 갱도). 실제 퀘스트 에셋의 체인
    [Test]
    public void Level5Chain_BoardOtterBeforeMineCall()
    {
        const string folder = "Assets/Scriptable Obejects/Quest/Quests/";
        var clerk = AssetDatabase.LoadAssetAtPath<QuestDefinition>(folder + "M06_Clerk.asset");
        var found = AssetDatabase.LoadAssetAtPath<QuestDefinition>(folder + "M05_MazeFound.asset");
        var board = AssetDatabase.LoadAssetAtPath<QuestDefinition>(folder + "M05_Board.asset");
        Assert.IsNotNull(clerk);
        Assert.IsNotNull(found);
        Assert.AreEqual(board, clerk.Prerequisite, "게시판 보강 바로 다음이 또박이 배치");
        Assert.AreEqual(5, clerk.RequiredLevel);
        Assert.AreEqual(MineMazeRules.BoardOtterRequest, clerk.Target);
        bool clerkFirst = false;
        for (var q = found.Prerequisite; q != null && q.RequiredLevel >= 5; q = q.Prerequisite)
            clerkFirst |= q == clerk;
        Assert.IsTrue(clerkFirst, "오래된 갱도 찾기보다 또박이 배치가 먼저");
    }

    // Lv.10~15 해달의 부탁이 메인 안에 (재설계 1차): 레벨을 올리는 부탁마다 그 바로 아래 레벨 메인의 마지막 단계(경험치 0),
    // 같은 레벨의 다른 메인 경험치 합 = 그 레벨 필요 경험치 (Lv.10은 첫 모임 공동사업 425를 뺀 만큼). 실제 에셋
    [Test]
    public void Level10To15_FacilityRequestClosesEachLevel()
    {
        var database = AssetDatabase.LoadAssetAtPath<QuestDatabase>("Assets/Scriptable Obejects/Quest/QuestDatabase.asset");
        var table = AssetDatabase.LoadAssetAtPath<LevelTable>("Assets/Scriptable Obejects/Profile/LevelTable.asset");
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>("Assets/Scriptable Obejects/Settlement/SettlementConfig.asset");
        Assert.IsNotNull(database);
        Assert.IsNotNull(table);
        Assert.IsNotNull(config);

        for (int level = 10; level <= 15; level++)
        {
            BoardRequestDefinition gate = null;
            foreach (var request in config.Requests)
            {
                if (request != null && request.KingdomLevel == level + 1)
                    gate = request;
            }
            Assert.IsNotNull(gate, $"Lv.{level + 1}로 올리는 부탁");

            var mains = new List<QuestDefinition>();
            foreach (var quest in database.Quests)
            {
                if (quest != null && quest.Kind == QuestKind.Main && quest.RequiredLevel == level)
                    mains.Add(quest);
            }
            QuestDefinition last = null;
            int exp = 0;
            foreach (var quest in mains)
            {
                bool followed = false;
                foreach (var other in mains)
                    followed |= other.Prerequisite == quest;
                if (!followed)
                {
                    Assert.IsNull(last, $"Lv.{level} 메인은 한 줄");
                    last = quest;
                }
                exp += quest.ExpFor(level, null);
            }
            Assert.IsNotNull(last);
            Assert.AreEqual(QuestGoalType.Milestone, last.GoalType, $"Lv.{level} 마지막 메인");
            Assert.AreEqual(gate.RequestId, last.Target, $"Lv.{level} 마지막 메인 = 다음 레벨로 올리는 부탁");
            Assert.AreEqual(0, last.ExpFor(level, null), "끝내면 바로 다음 레벨이라 경험치 0");
            int project = level == 10 ? 425 : 0;
            Assert.AreEqual(table.ExpToNext(level), exp + project, $"Lv.{level} 메인 경험치 합");
        }
    }

    [Test]
    public void PaidTransactions_CountBagNotFurrow()
    {
        var furrow = Quest("furrow", QuestGoalType.UnlockFurrow);
        var bag = Quest("bag", QuestGoalType.ExpandBag);
        var upgrade = Quest("upgrade", QuestGoalType.Upgrade);

        var openFurrow = new CurrencyChange(null, -300, TransactionSource.PlotUnlock);
        var expandBag = new CurrencyChange(null, -10, TransactionSource.InventoryExpand);

        Assert.AreEqual(0, QuestProgressRules.From(furrow, openFurrow), "고랑은 거래가 아니라 열린 고랑 수로 셈");
        Assert.AreEqual(0, QuestProgressRules.From(furrow, expandBag));
        Assert.AreEqual(1, QuestProgressRules.From(bag, expandBag));
        Assert.AreEqual(0, QuestProgressRules.From(upgrade, openFurrow), "고랑 열기는 업그레이드가 아님");
        Assert.AreEqual(0, QuestProgressRules.From(bag, new CurrencyChange(null, 10, TransactionSource.InventoryExpand)), "받은 돈은 세지 않음");
    }

    [Test]
    public void OpenedFurrows_CountFurrowsOpenedBeforeTheQuest()
    {
        var third = Quest("furrow_3", QuestGoalType.UnlockFurrow, target: "3");
        var noOrder = Quest("furrow_any", QuestGoalType.UnlockFurrow);

        Assert.AreEqual(0, QuestProgressRules.FromOpenedFurrows(third, 2), "셋째 고랑이 아직 안 열림");
        Assert.AreEqual(1, QuestProgressRules.FromOpenedFurrows(third, 3));
        Assert.AreEqual(1, QuestProgressRules.FromOpenedFurrows(third, 6), "체인보다 고랑을 먼저 다 열어도 끝남 (8차 QA: 체인이 막혔던 경우)");
        Assert.AreEqual(0, QuestProgressRules.FromOpenedFurrows(noOrder, 0));
        Assert.AreEqual(1, QuestProgressRules.FromOpenedFurrows(noOrder, 1), "순번이 없으면 첫 고랑");
        Assert.AreEqual(0, QuestProgressRules.FromOpenedFurrows(Quest("harvest", QuestGoalType.Harvest), 6));
    }

    [Test]
    public void OpenedFurrows_RecountFinishesQuestWithoutDoubling()
    {
        var quest = Quest("furrow_5", QuestGoalType.UnlockFurrow, target: "5");
        var log = new QuestLog();

        log.SetProgressAtLeast(quest, QuestProgressRules.FromOpenedFurrows(quest, 6));
        Assert.AreEqual(QuestStatus.Claimable, log.GetStatus(quest));
        Assert.IsFalse(log.SetProgressAtLeast(quest, QuestProgressRules.FromOpenedFurrows(quest, 6)), "다시 세도 그대로");
    }

    [Test]
    public void VisitZone_CountsOnlyItsScene()
    {
        var mine = Quest("visit_mine", QuestGoalType.VisitZone, target: "Mine");

        Assert.AreEqual(1, QuestProgressRules.FromArrival(mine, "Mine"));
        Assert.AreEqual(0, QuestProgressRules.FromArrival(mine, "Farm"));
        Assert.AreEqual(0, QuestProgressRules.FromArrival(Quest("harvest", QuestGoalType.Harvest), "Mine"));
    }

    [Test]
    public void CompleteDaily_CountsDailyClaimsOnly()
    {
        var goal = Quest("daily_goal", QuestGoalType.CompleteDaily);
        var daily = Quest("daily", QuestGoalType.Harvest, QuestKind.Daily);
        var challenge = Quest("challenge", QuestGoalType.Harvest, QuestKind.Challenge);

        Assert.AreEqual(1, QuestProgressRules.FromClaimed(goal, daily));
        Assert.AreEqual(0, QuestProgressRules.FromClaimed(goal, challenge));
    }

    [Test]
    public void CompleteDaily_CountsDailiesClaimedTodayFromRecord()
    {
        // Lv.9엔 일일이 하나뿐이라, 이 퀘스트가 열리기 전에 받은 오늘의 일일도 기록으로 셈
        var goal = Quest("daily_goal", QuestGoalType.CompleteDaily);
        var other = Quest("mine", QuestGoalType.Mine);

        Assert.AreEqual(1, QuestProgressRules.FromClaimedDailies(goal, 1));
        Assert.AreEqual(0, QuestProgressRules.FromClaimedDailies(goal, 0));
        Assert.AreEqual(0, QuestProgressRules.FromClaimedDailies(other, 1));
    }

    [Test]
    public void PickaxeUpgrade_CountsPickaxeLevelFromRecord()
    {
        // 곡괭이 강화는 4번이 끝: 미리 다 올려도 대상이 "pickaxe"인 업그레이드 퀘스트는 곡괭이 레벨로 끝남
        var pickaxe = Quest("pickaxe", QuestGoalType.Upgrade, target: QuestProgressRules.PickaxeTarget);
        var anyUpgrade = Quest("upgrade", QuestGoalType.Upgrade, goal: 2);

        Assert.AreEqual(0, QuestProgressRules.FromPickaxeLevel(pickaxe, 1), "강화 전 (Lv.1)");
        Assert.AreEqual(4, QuestProgressRules.FromPickaxeLevel(pickaxe, 5));
        Assert.AreEqual(0, QuestProgressRules.FromPickaxeLevel(anyUpgrade, 5), "대상이 없는 업그레이드 퀘스트는 예전처럼 열린 동안의 강화만");
    }

    [Test]
    public void ShopPurchase_WithItem_CountsOnlyThatItem()
    {
        var potatoSeed = Item();
        var other = Item();
        var buyPotato = Quest("buy_potato", QuestGoalType.ShopPurchase, item: potatoSeed);
        var buyAny = Quest("buy_any", QuestGoalType.ShopPurchase);

        Assert.AreEqual(2, QuestProgressRules.FromShopPurchase(buyPotato, potatoSeed, 2));
        Assert.AreEqual(0, QuestProgressRules.FromShopPurchase(buyPotato, other, 2));
        Assert.AreEqual(2, QuestProgressRules.FromShopPurchase(buyAny, other, 2));
    }

    [Test]
    public void OldSave_SkipsMainQuestsOfPassedLevels_ButKeepsCurrentLevel()
    {
        var lv1 = Quest("m1", QuestGoalType.Milestone, level: 1, target: "req_first_house");
        var lv3 = Quest("m3", QuestGoalType.Harvest, level: 3, goal: 5);
        var lv5 = Quest("m5", QuestGoalType.Harvest, level: 5);
        var challenge = Quest("c1", QuestGoalType.Harvest, QuestKind.Challenge, level: 1);
        var log = new QuestLog();

        int skipped = QuestProgressRules.SkipPassedMainQuests(new[] { lv1, lv3, lv5, challenge }, 5, log);

        Assert.AreEqual(2, skipped);
        Assert.AreEqual(QuestStatus.Claimed, log.GetStatus(lv1));
        Assert.AreEqual(QuestStatus.Claimed, log.GetStatus(lv3));
        Assert.AreEqual(QuestStatus.InProgress, log.GetStatus(lv5), "지금 레벨의 메인은 남음");
        Assert.AreEqual(QuestStatus.InProgress, log.GetStatus(challenge), "도전은 그대로");
        Assert.AreEqual(0, log.GetClaimedLevel(lv1), "넘긴 것은 받은 레벨을 모름 → 완료 칸에 남지 않음");
    }

    [Test]
    public void HasMainChain_OnlyWithMilestoneMainQuests()
    {
        Assert.IsFalse(QuestProgressRules.HasMainChain(new[] { Quest("old", QuestGoalType.Harvest) }), "옛 데이터");
        Assert.IsTrue(QuestProgressRules.HasMainChain(new[] { Quest("new", QuestGoalType.Milestone, target: "req_chair") }));
    }

    [Test]
    public void MainChainReady_IsSavedAndLoaded()
    {
        var log = new QuestLog();
        log.MarkMainChainReady();
        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(log, saved);

        var loaded = new QuestLog();
        QuestSaveConverter.Read(saved, loaded);

        Assert.IsTrue(loaded.MainChainReady);
        Assert.IsFalse(new QuestLog().MainChainReady, "옛 세이브(표시 없음)는 false");
    }

    [Test]
    public void VisitedZones_AreSavedAndLoaded_NotAsQuestRecords()
    {
        var log = new QuestLog();
        Assert.IsTrue(log.MarkVisited("Mine"));
        Assert.IsFalse(log.MarkVisited("Mine"), "같은 장소는 한 번만");
        log.MarkVisited("Farm");
        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(log, saved);

        var loaded = new QuestLog();
        QuestSaveConverter.Read(saved, loaded);

        Assert.IsTrue(loaded.HasVisited("Mine"));
        Assert.IsTrue(loaded.HasVisited("Farm"));
        Assert.IsFalse(loaded.HasVisited("Fishing"));
        Assert.AreEqual(0, loaded.Records.Count, "가 본 장소 줄은 퀘스트 기록이 아닙니다.");
    }
}
