using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class QuestTests
{
    private readonly List<Object> _created = new List<Object>();

    private ItemCategory _crops;
    private ItemCategory _fish;
    private ItemDefinition _carrot;
    private ItemDefinition _mackerel;
    private ItemDefinition _trash;
    private CollectionTab _otterTab;
    private CollectionTab _vegetableTab;
    private QuestLog _log;

    [SetUp]
    public void SetUp()
    {
        _crops = CreateCategory("Crops");
        _fish = CreateCategory("Fish");
        _carrot = CreateItem("crop_carrot", _crops);
        _mackerel = CreateItem("fish_mackerel", _fish);
        _trash = CreateItem("trash_basic", null);
        _otterTab = Track(ScriptableObject.CreateInstance<CollectionTab>());
        _vegetableTab = Track(ScriptableObject.CreateInstance<CollectionTab>());
        _log = new QuestLog();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    #region 준비 도우미

    private T Track<T>(T obj) where T : Object
    {
        _created.Add(obj);
        return obj;
    }

    private ItemCategory CreateCategory(string id)
    {
        var category = Track(ScriptableObject.CreateInstance<ItemCategory>());
        var so = new SerializedObject(category);
        so.FindProperty("_categoryId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return category;
    }

    private ItemDefinition CreateItem(string id, ItemCategory category)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_category").objectReferenceValue = category;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private QuestDefinition CreateQuest(string id, QuestGoalType type, int goal, ItemCategory filter = null, CollectionTab tab = null)
    {
        var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
        var so = new SerializedObject(quest);
        so.FindProperty("_questId").stringValue = id;
        so.FindProperty("_goalType").enumValueIndex = (int)type;
        so.FindProperty("_goal").intValue = goal;
        so.FindProperty("_itemFilter").objectReferenceValue = filter;
        so.FindProperty("_collectionTab").objectReferenceValue = tab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private CollectionEntry CreateEntry(CollectionTab tab)
    {
        var entry = Track(ScriptableObject.CreateInstance<CollectionEntry>());
        var so = new SerializedObject(entry);
        so.FindProperty("_entryId").stringValue = "entry";
        so.FindProperty("_tab").objectReferenceValue = tab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return entry;
    }

    #endregion

    #region QuestLog

    [Test]
    public void AddProgress_StopsAtGoalAndBecomesClaimable()
    {
        // Arrange
        var quest = CreateQuest("q", QuestGoalType.Harvest, 3);

        // Act
        _log.AddProgress(quest, 2);
        Assert.AreEqual(QuestStatus.InProgress, _log.GetStatus(quest));
        _log.AddProgress(quest, 5);

        // Assert
        Assert.AreEqual(3, _log.GetProgress(quest), "목표를 넘으면 안 됩니다.");
        Assert.AreEqual(QuestStatus.Claimable, _log.GetStatus(quest));
    }

    [Test]
    public void TryClaim_OnlyOnceAndOnlyWhenComplete()
    {
        // Arrange
        var quest = CreateQuest("q", QuestGoalType.Harvest, 2);
        _log.AddProgress(quest, 1);

        // Act & Assert
        Assert.IsFalse(_log.TryClaim(quest), "달성 전에는 받을 수 없습니다.");
        _log.AddProgress(quest, 1);
        Assert.IsTrue(_log.TryClaim(quest));
        Assert.IsFalse(_log.TryClaim(quest), "같은 보상을 두 번 받으면 안 됩니다.");
        Assert.AreEqual(QuestStatus.Claimed, _log.GetStatus(quest));
    }

    [Test]
    public void AddProgress_AfterClaim_DoesNothing()
    {
        // Arrange
        var quest = CreateQuest("q", QuestGoalType.Harvest, 1);
        _log.AddProgress(quest, 1);
        _log.TryClaim(quest);
        int changes = 0;
        _log.OnChanged += _ => changes++;

        // Act
        bool changed = _log.AddProgress(quest, 1);

        // Assert
        Assert.IsFalse(changed);
        Assert.AreEqual(0, changes);
        Assert.AreEqual(QuestStatus.Claimed, _log.GetStatus(quest));
    }

    [Test]
    public void AddProgress_Negative_Throws()
    {
        var quest = CreateQuest("q", QuestGoalType.Harvest, 1);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => _log.AddProgress(quest, -1));
    }

    [Test]
    public void SaveRoundTrip_KeepsProgressClaimedAndUnknownIds()
    {
        // Arrange
        var inProgress = CreateQuest("a", QuestGoalType.Harvest, 5);
        var claimed = CreateQuest("b", QuestGoalType.Harvest, 1);
        _log.AddProgress(inProgress, 2);
        _log.AddProgress(claimed, 1);
        _log.TryClaim(claimed);
        _log.LoadRecord("removed_quest", 7, false);

        // Act
        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(_log, saved);
        var loaded = new QuestLog();
        QuestSaveConverter.Read(saved, loaded);

        // Assert
        Assert.AreEqual(2, loaded.GetProgress(inProgress));
        Assert.AreEqual(QuestStatus.Claimed, loaded.GetStatus(claimed));
        Assert.IsTrue(loaded.Records.ContainsKey("removed_quest"), "DB에서 빠진 퀘스트 기록도 잃으면 안 됩니다.");
    }

    [Test]
    public void GoalLoweredAfterSave_ProgressIsClampedAndClaimable()
    {
        // Arrange: 저장 뒤 기획이 목표를 10 → 3으로 낮춤
        var quest = CreateQuest("q", QuestGoalType.Harvest, 3);
        _log.LoadRecord("q", 8, false);

        // Assert
        Assert.AreEqual(3, _log.GetProgress(quest));
        Assert.AreEqual(QuestStatus.Claimable, _log.GetStatus(quest));
    }

    #endregion

    #region 진행 규칙

    [Test]
    public void Harvest_CountsOnlyHarvestIncreasesInFilter()
    {
        var quest = CreateQuest("q", QuestGoalType.Harvest, 10, _crops);

        Assert.AreEqual(3, QuestProgressRules.From(quest, new ItemChangedEvent(_carrot, 2, 5, ItemChangeReason.Harvest)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_carrot, 0, 5, ItemChangeReason.Load)), "세이브 복원은 세지 않습니다.");
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_carrot, 5, 2, ItemChangeReason.Sell)), "줄어든 것은 세지 않습니다.");
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_mackerel, 0, 1, ItemChangeReason.Harvest)), "분류가 다르면 세지 않습니다.");
    }

    [Test]
    public void Catch_FishFilter_IgnoresTrash()
    {
        var quest = CreateQuest("q", QuestGoalType.Catch, 10, _fish);

        Assert.AreEqual(1, QuestProgressRules.From(quest, new ItemChangedEvent(_mackerel, 0, 1, ItemChangeReason.Fishing)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_trash, 0, 1, ItemChangeReason.Fishing)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_carrot, 0, 1, ItemChangeReason.Harvest)), "수확은 낚시 퀘스트가 아닙니다.");
    }

    [Test]
    public void EarnFromSales_CountsTotalPrice()
    {
        var quest = CreateQuest("q", QuestGoalType.EarnFromSales, 500);
        var other = CreateQuest("h", QuestGoalType.Harvest, 5);
        var sold = new ItemSoldEvent(_carrot, 4, 60);

        Assert.AreEqual(60, QuestProgressRules.From(quest, sold));
        Assert.AreEqual(0, QuestProgressRules.From(other, sold));
    }

    [Test]
    public void Upgrade_CountsUpgradeSpendsOnly()
    {
        var quest = CreateQuest("q", QuestGoalType.Upgrade, 3);

        Assert.AreEqual(1, QuestProgressRules.From(quest, new CurrencyChange(null, -100, TransactionSource.FarmUpgrade)));
        Assert.AreEqual(1, QuestProgressRules.From(quest, new CurrencyChange(null, -200, TransactionSource.RodUpgrade)));
        Assert.AreEqual(1, QuestProgressRules.From(quest, new CurrencyChange(null, -400, TransactionSource.PickaxeUpgrade)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new CurrencyChange(null, -50, TransactionSource.InventoryExpand)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new CurrencyChange(null, 100, TransactionSource.FarmUpgrade)), "획득은 업그레이드가 아닙니다.");
    }

    [Test]
    public void CollectionRegister_CountsCollectedInTabOnly()
    {
        var quest = CreateQuest("q", QuestGoalType.CollectionRegister, 2, tab: _otterTab);
        var otter = CreateEntry(_otterTab);
        var carrot = CreateEntry(_vegetableTab);

        Assert.AreEqual(1, QuestProgressRules.From(quest, otter, CollectionState.Collected));
        Assert.AreEqual(0, QuestProgressRules.From(quest, otter, CollectionState.Visited), "방문 흔적은 만난 것이 아닙니다.");
        Assert.AreEqual(0, QuestProgressRules.From(quest, carrot, CollectionState.Collected), "다른 탭 항목은 세지 않습니다.");
    }

    #endregion
}
