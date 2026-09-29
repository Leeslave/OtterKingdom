using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class InventorySlotLayoutTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<SlotData> _result = new List<SlotData>();

    // 전체 / 농사(작물, 모종) / 낚시(물고기)
    private ItemCategory _all;
    private ItemCategory _farming;
    private ItemCategory _crop;
    private ItemCategory _seedling;
    private ItemCategory _fishing;
    private ItemCategory _fish;

    [SetUp]
    public void SetUp()
    {
        _all = CreateCategory(sortOrder: -1, showsAll: true);
        _farming = CreateCategory(sortOrder: 0);
        _crop = CreateCategory(sortOrder: 0, parent: _farming);
        _seedling = CreateCategory(sortOrder: 1, parent: _farming);
        _fishing = CreateCategory(sortOrder: 1);
        _fish = CreateCategory(sortOrder: 0, parent: _fishing);
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

    private ItemCategory CreateCategory(int sortOrder, bool showsAll = false, ItemCategory parent = null)
    {
        var category = Track(ScriptableObject.CreateInstance<ItemCategory>());
        var so = new SerializedObject(category);
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_showsAllItems").boolValue = showsAll;
        so.FindProperty("_parent").objectReferenceValue = parent;
        so.ApplyModifiedPropertiesWithoutUndo();
        return category;
    }

    private ItemDefinition CreateItem(string id, ItemCategory category, ItemRarity rarity = null, string displayName = null)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName ?? id;
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

    #region 필터

    [Test]
    public void Build_TabFilter_IncludesSubCategoryItems()
    {
        // Arrange
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(CreateItem("Carrot", _crop), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("CarrotSeedling", _seedling), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _farming, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Item), "농사 탭 = 작물 + 모종");
    }

    [Test]
    public void Build_SubCategoryFilter_ExcludesSiblings()
    {
        // Arrange
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        var carrot = CreateItem("Carrot", _crop);
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("CarrotSeedling", _seedling), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _crop, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(1, CountState(SlotState.Item));
        Assert.AreEqual(carrot, _result[0].Item);
    }

    [Test]
    public void Build_AllCategory_ShowsItemsOfEveryCategory()
    {
        // Arrange
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(CreateItem("Carrot", _crop), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Item));
        Assert.AreEqual(2, CountState(SlotState.Empty));
    }

    #endregion

    #region 빈 칸 / 잠긴 칸

    [Test]
    public void Build_EmptySlotsAreSharedAcrossCategories()
    {
        // Arrange: 용량 4 중 작물 1 + 물고기 1 사용 → 빈 칸 2
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.Add(CreateItem("Carrot", _crop), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Mackerel", _fish), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _farming, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Empty), "다른 카테고리 아이템도 공유 칸을 차지해야 합니다.");
    }

    [Test]
    public void Build_LockedSlotsFillUpToMaxCapacity()
    {
        // Arrange
        var inventory = new Inventory(capacity: 16, maxCapacity: 40);

        // Act
        InventorySlotLayout.Build(inventory, _farming, ItemSortMode.Rarity, _result);

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
        inventory.Add(CreateItem("Carrot", _crop), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _farming, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(SlotState.Item, _result[0].State);
        Assert.AreEqual(SlotState.Empty, _result[1].State);
        Assert.AreEqual(SlotState.Locked, _result[2].State);
    }

    #endregion

    #region 정렬

    [Test]
    public void Build_Rarity_SortsDescendingThenId()
    {
        // Arrange
        var inventory = new Inventory(capacity: 3, maxCapacity: 3);
        var common = CreateRarity(0);
        var potato = CreateItem("Potato", _crop, common);
        var carrot = CreateItem("Carrot", _crop, common);
        var goldenCarrot = CreateItem("GoldenCarrot", _crop, CreateRarity(2));
        inventory.Add(potato, 1, ItemChangeReason.Test);
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(goldenCarrot, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _crop, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(goldenCarrot, _result[0].Item, "희귀한 아이템이 먼저");
        Assert.AreEqual(carrot, _result[1].Item, "같은 희귀도면 ID순");
        Assert.AreEqual(potato, _result[2].Item);
    }

    [Test]
    public void Build_AllCategory_RarityComesBeforeCategoryOrder()
    {
        // Arrange: 낚시 탭이 농사보다 뒤지만, 선택한 정렬 기준(등급)이 우선
        var inventory = new Inventory(capacity: 2, maxCapacity: 2);
        var epicFish = CreateItem("Mackerel", _fish, CreateRarity(2));
        var carrot = CreateItem("Carrot", _crop, CreateRarity(0));
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(epicFish, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(epicFish, _result[0].Item);
        Assert.AreEqual(carrot, _result[1].Item);
    }

    [Test]
    public void Build_SameKey_TieBreaksByTabThenSubCategory()
    {
        // Arrange: 모두 같은 등급 → 탭 순서(농사 → 낚시), 같은 탭이면 소분류 순서(작물 → 모종)
        var inventory = new Inventory(capacity: 3, maxCapacity: 3);
        var fish = CreateItem("A_Mackerel", _fish);
        var seedling = CreateItem("B_Seedling", _seedling);
        var carrot = CreateItem("C_Carrot", _crop);
        inventory.Add(fish, 1, ItemChangeReason.Test);
        inventory.Add(seedling, 1, ItemChangeReason.Test);
        inventory.Add(carrot, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(carrot, _result[0].Item);
        Assert.AreEqual(seedling, _result[1].Item);
        Assert.AreEqual(fish, _result[2].Item);
    }

    [Test]
    public void Build_Recent_NewestFirst()
    {
        // Arrange: 당근 → 감자 → 당근 다시 획득
        var inventory = new Inventory(capacity: 2, maxCapacity: 2);
        var carrot = CreateItem("Carrot", _crop);
        var potato = CreateItem("Potato", _crop);
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(potato, 1, ItemChangeReason.Test);
        inventory.Add(carrot, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _crop, ItemSortMode.Recent, _result);

        // Assert
        Assert.AreEqual(carrot, _result[0].Item);
        Assert.AreEqual(potato, _result[1].Item);
    }

    [Test]
    public void Build_Name_SortsByDisplayName()
    {
        // Arrange: ID순과 이름순이 반대가 되도록
        var inventory = new Inventory(capacity: 3, maxCapacity: 3);
        var potato = CreateItem("A", _crop, displayName: "감자");
        var carrot = CreateItem("B", _crop, displayName: "당근");
        var sweetPotato = CreateItem("C", _crop, displayName: "고구마");
        inventory.Add(carrot, 1, ItemChangeReason.Test);
        inventory.Add(potato, 1, ItemChangeReason.Test);
        inventory.Add(sweetPotato, 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _crop, ItemSortMode.Name, _result);

        // Assert
        Assert.AreEqual(potato, _result[0].Item);
        Assert.AreEqual(sweetPotato, _result[1].Item);
        Assert.AreEqual(carrot, _result[2].Item);
    }

    #endregion

    #region 카테고리 계층 안전장치

    [Test]
    public void Root_OfSubCategory_ReturnsTab()
    {
        // Act & Assert
        Assert.AreEqual(_farming, _seedling.Root);
        Assert.AreEqual(_farming, _farming.Root);
    }

    [Test]
    public void Contains_ParentCycle_DoesNotHang()
    {
        // Arrange: 실수로 부모가 순환 (A → B → A)
        var a = CreateCategory(sortOrder: 0);
        var b = CreateCategory(sortOrder: 0, parent: a);
        SetParent(a, b);
        var item = CreateItem("Loop", a);

        // Act & Assert: 무한 반복 없이 끝나야 함
        Assert.IsTrue(a.Contains(item));
        Assert.IsFalse(_farming.Contains(item));
        Assert.IsNotNull(a.Root);
    }

    [Test]
    public void Build_ParentCycle_DoesNotHang()
    {
        // Arrange
        var a = CreateCategory(sortOrder: 0);
        var b = CreateCategory(sortOrder: 0, parent: a);
        SetParent(a, b);
        var inventory = new Inventory(capacity: 2, maxCapacity: 2);
        inventory.Add(CreateItem("Loop1", a), 1, ItemChangeReason.Test);
        inventory.Add(CreateItem("Loop2", b), 1, ItemChangeReason.Test);

        // Act
        InventorySlotLayout.Build(inventory, _all, ItemSortMode.Rarity, _result);

        // Assert
        Assert.AreEqual(2, CountState(SlotState.Item));
    }

    private static void SetParent(ItemCategory category, ItemCategory parent)
    {
        var so = new SerializedObject(category);
        so.FindProperty("_parent").objectReferenceValue = parent;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
