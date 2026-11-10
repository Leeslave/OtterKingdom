using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>번영의 시대 퀘스트 목표 (가공 · 왕국 창고 · 마을 정비 · 꾸미기 놓기 아이템)</summary>
public class IndustryQuestTests
{
    private readonly List<Object> _created = new List<Object>();
    private ItemDefinition _plank;
    private ItemDefinition _stoneBlock;
    private ItemDefinition _sign;
    private ItemDefinition _lamp;

    [SetUp]
    public void SetUp()
    {
        _plank = CreateItem("plank");
        _stoneBlock = CreateItem("stone_block");
        _sign = CreateItem("furniture_kingdom_sign");
        _lamp = CreateItem("furniture_lamp");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private T Track<T>(T obj) where T : Object
    {
        _created.Add(obj);
        return obj;
    }

    private ItemDefinition CreateItem(string id)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private QuestDefinition CreateQuest(QuestGoalType type, ItemDefinition item = null)
    {
        var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
        var so = new SerializedObject(quest);
        so.FindProperty("_questId").stringValue = type.ToString();
        so.FindProperty("_goalType").enumValueIndex = (int)type;
        so.FindProperty("_goal").intValue = 5;
        so.FindProperty("_item").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private DecorDefinition CreateDecor(ItemDefinition item)
    {
        var decor = Track(ScriptableObject.CreateInstance<DecorDefinition>());
        var so = new SerializedObject(decor);
        so.FindProperty("_item").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        return decor;
    }

    [Test]
    public void Process_CountsMadeProducts_FilteredByItem()
    {
        var any = CreateQuest(QuestGoalType.Process);
        var planks = CreateQuest(QuestGoalType.Process, _plank);
        Assert.AreEqual(3, QuestProgressRules.FromProcessed(any, _stoneBlock, 3));
        Assert.AreEqual(0, QuestProgressRules.FromProcessed(planks, _stoneBlock, 3), "판재 퀘스트는 석재 블록을 안 셈");
        Assert.AreEqual(2, QuestProgressRules.FromProcessed(planks, _plank, 2));
        Assert.AreEqual(0, QuestProgressRules.FromProcessed(CreateQuest(QuestGoalType.Harvest), _plank, 2), "다른 목표는 0");
    }

    [Test]
    public void StoreInWarehouse_CountsCollectAndCartOnly()
    {
        var quest = CreateQuest(QuestGoalType.StoreInWarehouse, _plank);
        Assert.AreEqual(4, QuestProgressRules.FromWarehouse(quest, "plank", 4, "collect"));
        Assert.AreEqual(2, QuestProgressRules.FromWarehouse(quest, "plank", 2, "cart"));
        Assert.AreEqual(0, QuestProgressRules.FromWarehouse(quest, "plank", 9, "test"), "시험 지급은 안 셈");
        Assert.AreEqual(0, QuestProgressRules.FromWarehouse(quest, "plank", -6, "house"), "쓴 것은 안 셈");
        Assert.AreEqual(0, QuestProgressRules.FromWarehouse(quest, "stone_block", 3, "cart"), "다른 가공품");
    }

    [Test]
    public void CraftTown_CountsCraftedItemsOnly()
    {
        var quest = CreateQuest(QuestGoalType.CraftTown, _sign);
        Assert.AreEqual(1, QuestProgressRules.From(quest, new ItemChangedEvent(_sign, 0, 1, ItemChangeReason.Crafting)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_sign, 0, 1, ItemChangeReason.Purchase)), "상점에서 산 것은 안 셈");
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(_lamp, 0, 1, ItemChangeReason.Crafting)), "다른 꾸미기");
    }

    [Test]
    public void PlaceDecor_WithItem_CountsThatDecorOnly_WithoutItem_CountsAll()
    {
        var sign = new PlacedDecor(1, CreateDecor(_sign), Vector2Int.zero, DecorRotation.R0);
        var lamp = new PlacedDecor(2, CreateDecor(_lamp), Vector2Int.zero, DecorRotation.R0);
        var anyDecor = CreateQuest(QuestGoalType.PlaceDecor);
        var signOnly = CreateQuest(QuestGoalType.PlaceDecor, _sign);
        Assert.AreEqual(1, QuestProgressRules.From(anyDecor, lamp), "기존 가구 퀘스트는 그대로");
        Assert.AreEqual(1, QuestProgressRules.From(signOnly, sign));
        Assert.AreEqual(0, QuestProgressRules.From(signOnly, lamp));
    }

    [Test]
    public void ImproveHouse_CountsFromRecords()
    {
        Assert.IsTrue(CreateQuest(QuestGoalType.ImproveHouse).CountsFromRecords, "앞서 개량한 집도 들어가게 기록으로 다시 셈");
        Assert.IsFalse(CreateQuest(QuestGoalType.Process).CountsFromRecords);
    }
}
