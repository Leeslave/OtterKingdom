using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class InventorySlotLayoutTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<SlotData> _result = new List<SlotData>();

    private ItemCategory _vegetable;
    private ItemCategory _fish;

    private ItemCategory _all;

    [SetUp]
    public void SetUp()
    {
        _all = CreateCategory(sortOrder: -1, showsAll: true);
        _vegetable = CreateCategory(sortOrder: 0);
        _fish = CreateCategory(sortOrder: 1);
    }

    private ItemCategory CreateCategory(int sortOrder, bool showsAll = false)
    {
        var category = Track(ScriptableObject.CreateInstance<ItemCategory>());
        var so = new SerializedObject(category);
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_showsAllItems").boolValue = showsAll;
        so.ApplyModifiedPropertiesWithoutUndo();
        return category;
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

    private ItemDefinition CreateItem(string id, ItemCategory category, ItemRarity rarity = null)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_category").objectReferenceValue = category;
        so.FindProperty("_rarity").objectReferenceValue = rarity;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private ItemRarity CreateRarity(int tier)
    {
        var rarity = Track(ScriptableObject.CreateInstance<ItemRarity>());
        var so = new SerializedObject(rarity);
        so.FindProperty("_tier").intValue = tier;
        so.ApplyModifiedPropertiesWithoutUndo();
        return rarity;
    }

    private int CountState(SlotState state)
    {
        int count = 0;
        foreach (var data in _result)
            if (data.State == state) count++;
        return count;
    }

    [Test]
    public void Build_ShowsOnlyItemsOfSelectedCategory()
    {
        // Arrange
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        var carrot = CreateItem("Carrot", _vegetable);
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _vegetable, _result);

        // Assert
        Assert.AreEqual(1, CountState(SlotState.Item));
        Assert.AreEqual(carrot, _result[0].Item);
    }

    [Test]
    public void Build_EmptySlotsAreSharedAcrossCategories()
    {
        // Arrange: 용량 4 중 채소 1 + 어류 1 사용 → 빈 칸 2
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(CreateItem("Carrot", _vegetable), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _vegetable, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Empty), "다른 카테고리 아이템도 공유 칸을 차지해야 합니다.");
    }

    [Test]
    public void Build_LockedSlotsFillUpToMaxCapacity()
    {
        // Arrange
        var inventory = new Inventory(capacity: 16, maxCapacity: 40);

        // Act
        InventorySlotLayout.Build(inventory, _vegetable, _result);

        // Assert
        Assert.AreEqual(16, CountState(SlotState.Empty));
        Assert.AreEqual(24, CountState(SlotState.Locked));
        Assert.AreEqual(40, _result.Count);
    }

    [Test]
    public void Build_OrderIsItemsThenEmptyThenLocked()
    {
        // Arrange
        var inventory = new Inventory(capacity: 2, maxCapacity: 3);
        inventory.Add(CreateItem("Carrot", _vegetable), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _vegetable, _result);

        // Assert
        Assert.AreEqual(SlotState.Item, _result[0].State);
        Assert.AreEqual(SlotState.Empty, _result[1].State);
        Assert.AreEqual(SlotState.Locked, _result[2].State);
    }

    [Test]
    public void Build_SortsByRarityDescendingThenId()
    {
        // Arrange
        var inventory = new Inventory(capacity: 3, maxCapacity: 3);
        var common = CreateRarity(0);
        var epic = CreateRarity(2);
        var potato = CreateItem("Potato", _vegetable, common);
        var carrot = CreateItem("Carrot", _vegetable, common);
        var goldenCarrot = CreateItem("GoldenCarrot", _vegetable, epic);
        inventory.Add(potato, 1, ItemChangeReason.Test);
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(goldenCarrot, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _vegetable, _result);

        // Assert
        Assert.AreEqual(goldenCarrot, _result[0].Item, "희귀한 아이템이 먼저");
        Assert.AreEqual(carrot, _result[1].Item, "같은 희귀도면 ID순");
        Assert.AreEqual(potato, _result[2].Item);
    }

    [Test]
    public void Build_AllCategory_ShowsItemsOfEveryCategory()
    {
        // Arrange
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(CreateItem("Carrot", _vegetable), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Item));
        Assert.AreEqual(2, CountState(SlotState.Empty));
    }

    [Test]
    public void Build_AllCategory_GroupsByCategorySortOrder()
    {
        // Arrange: 어류가 더 희귀해도 카테고리 순서(채소 0 → 어류 1)가 먼저
        var inventory = new Inventory(capacity: 2, maxCapacity: 2);
        var epicFish = CreateItem("Mackerel", _fish, CreateRarity(2));
        var carrot = CreateItem("Carrot", _vegetable, CreateRarity(0));
        inventory.Add(epicFish, 1, ItemChangeReason.Test);
        inventory.Add(carrot, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, _result);

        // Assert
        Assert.AreEqual(carrot, _result[0].Item);
        Assert.AreEqual(epicFish, _result[1].Item);
    }
}
