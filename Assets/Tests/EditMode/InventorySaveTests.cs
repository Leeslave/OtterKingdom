using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class InventorySaveTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<ItemStack> _unknown = new List<ItemStack>();
    private readonly List<ItemStack> _written = new List<ItemStack>();

    private ItemDefinition _carrot;
    private ItemDefinition _potato;
    private ItemDatabase _database;

    [SetUp]
    public void SetUp()
    {
        _carrot = CreateItem("crop_carrot", maxStack: 10);
        _potato = CreateItem("crop_potato", maxStack: 10);
        _database = CreateDatabase(_carrot, _potato);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
        _unknown.Clear();
        _written.Clear();
    }

    private ItemDefinition CreateItem(string id, int maxStack)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        _created.Add(item);
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_maxStack").intValue = maxStack;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private ItemDatabase CreateDatabase(params ItemDefinition[] items)
    {
        var database = ScriptableObject.CreateInstance<ItemDatabase>();
        _created.Add(database);
        var so = new SerializedObject(database);
        var list = so.FindProperty("_items");
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return database;
    }

    #region ItemDatabase

    [Test]
    public void Database_TryGet_FindsById()
    {
        Assert.IsTrue(_database.TryGet("crop_carrot", out var item));
        Assert.AreEqual(_carrot, item);
        Assert.IsFalse(_database.TryGet("crop_unknown", out _));
    }

    [Test]
    public void Database_FindDuplicateIds_ReportsEachOnce()
    {
        var duplicate = CreateItem("crop_carrot", maxStack: 10);

        var result = ItemDatabase.FindDuplicateIds(new[] { _carrot, duplicate, _potato, duplicate });

        CollectionAssert.AreEqual(new[] { "crop_carrot" }, result);
    }

    #endregion

    #region 저장 / 복원

    [Test]
    public void RoundTrip_KeepsCountsAndAcquiredOrder()
    {
        // Arrange: 감자 → 당근 순서로 획득
        var source = new Inventory(capacity: 4, maxCapacity: 4);
        source.Add(_potato, 2, ItemChangeReason.Test);
        source.Add(_carrot, 5, ItemChangeReason.Test);

        // Act
        InventorySaveConverter.Write(source, null, _written);
        var restored = new Inventory(capacity: 4, maxCapacity: 4);
        InventorySaveConverter.Read(_written, _database, restored, _unknown);

        // Assert
        Assert.AreEqual(2, restored.GetCount(_potato));
        Assert.AreEqual(5, restored.GetCount(_carrot));
        Assert.Greater(restored.GetAcquiredOrder(_carrot), restored.GetAcquiredOrder(_potato), "최신순 유지");
    }

    [Test]
    public void Read_UnknownId_IsKeptAndWrittenBack()
    {
        // Arrange
        var saved = new List<ItemStack> { new ItemStack("fish_basic", 3), new ItemStack("crop_carrot", 1) };
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);

        // Act
        InventorySaveConverter.Read(saved, _database, inventory, _unknown);
        InventorySaveConverter.Write(inventory, _unknown, _written);

        // Assert
        Assert.AreEqual(1, inventory.UsedSlots);
        Assert.AreEqual(1, _unknown.Count);
        Assert.IsTrue(_written.Exists(s => s.itemId == "fish_basic" && s.quantity == 3), "모르는 아이템도 다시 저장");
    }

    [Test]
    public void Read_OverCapacity_KeepsEverything()
    {
        // Arrange: 옛 세이브가 용량(1칸)보다 많은 종류를 가짐
        var saved = new List<ItemStack> { new ItemStack("crop_carrot", 1), new ItemStack("crop_potato", 1) };
        var inventory = new Inventory(capacity: 1, maxCapacity: 1);

        // Act
        InventorySaveConverter.Read(saved, _database, inventory, _unknown);

        // Assert: 버리지 않고, 새 종류 추가만 막힘
        Assert.AreEqual(2, inventory.UsedSlots);
        Assert.AreEqual(0, inventory.GetAddableAmount(CreateItem("crop_new", maxStack: 10)));
    }

    [Test]
    public void Read_DuplicateIds_AreMerged()
    {
        var saved = new List<ItemStack> { new ItemStack("crop_carrot", 2), new ItemStack("crop_carrot", 3) };
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);

        InventorySaveConverter.Read(saved, _database, inventory, _unknown);

        Assert.AreEqual(5, inventory.GetCount(_carrot));
    }

    [Test]
    public void Read_DoesNotMarkItemsAsNew()
    {
        var saved = new List<ItemStack> { new ItemStack("crop_carrot", 1) };
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);

        InventorySaveConverter.Read(saved, _database, inventory, _unknown);

        Assert.IsFalse(inventory.HasNewItems);
    }

    [Test]
    public void Read_IntoNonEmptyInventory_Throws()
    {
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(_carrot, 1, ItemChangeReason.Test);

        Assert.Throws<InvalidOperationException>(() =>
            InventorySaveConverter.Read(new List<ItemStack>(), _database, inventory, _unknown));
    }

    #endregion

    #region 넣을 수 있는 개수

    [Test]
    public void GetAddableAmount_ExistingItem_IsRoomToMaxStack()
    {
        var inventory = new Inventory(capacity: 1, maxCapacity: 1);
        inventory.Add(_carrot, 7, ItemChangeReason.Test);

        Assert.AreEqual(3, inventory.GetAddableAmount(_carrot), "칸이 꽉 차도 이미 가진 종류는 쌓임");
        Assert.AreEqual(0, inventory.GetAddableAmount(_potato), "새 종류는 빈 칸이 없으면 0");
    }

    #endregion
}
