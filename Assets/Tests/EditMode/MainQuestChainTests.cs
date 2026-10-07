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

    [Test]
    public void PaidTransactions_CountFurrowAndBagSeparately()
    {
        var furrow = Quest("furrow", QuestGoalType.UnlockFurrow);
        var bag = Quest("bag", QuestGoalType.ExpandBag);
        var upgrade = Quest("upgrade", QuestGoalType.Upgrade);

        var openFurrow = new CurrencyChange(null, -300, TransactionSource.PlotUnlock);
        var expandBag = new CurrencyChange(null, -10, TransactionSource.InventoryExpand);

        Assert.AreEqual(1, QuestProgressRules.From(furrow, openFurrow));
        Assert.AreEqual(0, QuestProgressRules.From(furrow, expandBag));
        Assert.AreEqual(1, QuestProgressRules.From(bag, expandBag));
        Assert.AreEqual(0, QuestProgressRules.From(upgrade, openFurrow), "고랑 열기는 업그레이드가 아님");
        Assert.AreEqual(0, QuestProgressRules.From(furrow, new CurrencyChange(null, 300, TransactionSource.PlotUnlock)), "받은 돈은 세지 않음");
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
