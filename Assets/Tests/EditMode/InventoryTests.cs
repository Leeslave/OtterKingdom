using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class InventoryTests
{
    private Inventory _inventory;
    private ItemDefinition _carrot;
    private ItemDefinition _fish;

    // 각 [Test] 실행 직전마다 호출
    [SetUp]
    public void SetUp()
    {
        _inventory = new Inventory(capacity: 16, maxCapacity: 40);
        _carrot = CreateItem("Carrot", maxStack: 10);
        _fish = CreateItem("Mackerel", maxStack: 10);
    }

    // 각 [Test] 실행 직후마다 호출 (만든 SO 정리)
    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_carrot);
        UnityEngine.Object.DestroyImmediate(_fish);
    }

    private static ItemDefinition CreateItem(string id, int maxStack)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_maxStack").intValue = maxStack;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    [Test]
    public void Add_NewItem_CountIncreases()
    {
        // Act
        int added = _inventory.Add(_carrot, 3, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(3, added);
        Assert.AreEqual(3, _inventory.GetCount(_carrot));
    }

    [Test]
    public void Add_OverMaxStack_ClampsToMaxStack()
    {
        //Arrange
        _inventory.Add(_carrot, 8, ItemChangeReason.Test);
        //Act
        int added = _inventory.Add(_carrot, 5, ItemChangeReason.Test);

        //Assert
        Assert.AreEqual(2, added);
        Assert.AreEqual(10, _inventory.GetCount(_carrot));
    }

    [Test]
    public void Add_FiresEventWithCorrectCounts()
    {
        // Arrange
        _inventory.Add(_carrot, 2, ItemChangeReason.Test);
        ItemChangedEvent? received = null;
        _inventory.OnItemChanged += e => received = e;

        // Act
        _inventory.Add(_carrot, 3, ItemChangeReason.Harvest);

        // Assert
        Assert.IsTrue(received.HasValue, "이벤트가 발생하지 않았습니다.");
        Assert.AreEqual(2, received.Value.OldCount);
        Assert.AreEqual(5, received.Value.NewCount);
        Assert.AreEqual(ItemChangeReason.Harvest, received.Value.Reason);
    }

    [Test]
    public void GetCount_UnownedItem_ReturnsZero()
    {
        // Act & Assert
        Assert.AreEqual(0, _inventory.GetCount(_carrot));
    }

    [Test]
    public void Add_WhenFull_ReturnsZeroAndNoEvent()
    {
        // Arrange
        _inventory.Add(_carrot, 10, ItemChangeReason.Test);
        bool fired = false;
        _inventory.OnItemChanged += e => fired = true;

        // Act
        int added = _inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(0, added);
        Assert.AreEqual(10, _inventory.GetCount(_carrot));
        Assert.IsFalse(fired, "변화가 없는데 이벤트가 발생했습니다.");
    }

    [Test]
    public void Add_NullItem_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _inventory.Add(null, 1, ItemChangeReason.Test));
    }

    [Test]
    public void TryRemove_NotEnough_ReturnsFalseAndKeepsCount()
    {
        // Arrange
        _inventory.Add(_carrot, 3, ItemChangeReason.Test);

        // Act
        bool removed = _inventory.TryRemove(_carrot, 5, ItemChangeReason.Sell);

        // Assert
        Assert.IsFalse(removed);
        Assert.AreEqual(3, _inventory.GetCount(_carrot));
    }

    [Test]
    public void TryRemove_ToZero_RemovesKey()
    {
        // Arrange
        _inventory.Add(_carrot, 3, ItemChangeReason.Test);

        // Act
        bool removed = _inventory.TryRemove(_carrot, 3, ItemChangeReason.Sell);

        // Assert
        Assert.IsTrue(removed);
        Assert.IsFalse(_inventory.Counts.ContainsKey(_carrot), "0개가 됐는데 키가 남아 있습니다.");
    }

    [Test]
    public void TryRemove_FiresEventWithCorrectCounts()
    {
        // Arrange
        _inventory.Add(_carrot, 5, ItemChangeReason.Test);
        ItemChangedEvent? received = null;
        _inventory.OnItemChanged += e => received = e;

        // Act
        _inventory.TryRemove(_carrot, 2, ItemChangeReason.Sell);

        // Assert
        Assert.IsTrue(received.HasValue, "이벤트가 발생하지 않았습니다.");
        Assert.AreEqual(5, received.Value.OldCount);
        Assert.AreEqual(3, received.Value.NewCount);
        Assert.AreEqual(ItemChangeReason.Sell, received.Value.Reason);
    }

    #region 슬롯 용량

    [Test]
    public void Constructor_ZeroCapacity_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(0, 10));
    }

    [Test]
    public void Add_NewItemWhenNoFreeSlot_ReturnsZero()
    {
        // Arrange
        var inventory = new Inventory(capacity: 1, maxCapacity: 10);
        inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Act
        int added = inventory.Add(_fish, 1, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(0, added);
        Assert.AreEqual(0, inventory.GetCount(_fish));
    }

    [Test]
    public void Add_ExistingItemWhenNoFreeSlot_StillStacks()
    {
        // Arrange
        var inventory = new Inventory(capacity: 1, maxCapacity: 10);
        inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Act
        int added = inventory.Add(_carrot, 2, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(2, added);
        Assert.AreEqual(3, inventory.GetCount(_carrot));
    }

    [Test]
    public void TryRemove_ToZero_FreesSlot()
    {
        // Arrange
        _inventory.Add(_carrot, 3, ItemChangeReason.Test);
        int freeBefore = _inventory.FreeSlots;

        // Act
        _inventory.TryRemove(_carrot, 3, ItemChangeReason.Sell);

        // Assert
        Assert.AreEqual(freeBefore + 1, _inventory.FreeSlots);
    }

    [Test]
    public void ExpandCapacity_IncreasesAndFiresEvent()
    {
        // Arrange
        int? received = null;
        _inventory.OnCapacityChanged += capacity => received = capacity;

        // Act
        int expanded = _inventory.ExpandCapacity(4);

        // Assert
        Assert.AreEqual(4, expanded);
        Assert.AreEqual(20, _inventory.Capacity);
        Assert.AreEqual(20, received, "이벤트로 새 용량이 전달되지 않았습니다.");
    }

    [Test]
    public void ExpandCapacity_OverMax_ClampsToMax()
    {
        // Arrange
        var inventory = new Inventory(capacity: 38, maxCapacity: 40);

        // Act
        int expanded = inventory.ExpandCapacity(4);

        // Assert
        Assert.AreEqual(2, expanded);
        Assert.AreEqual(40, inventory.Capacity);
    }

    #endregion

    #region 획득 순서

    [Test]
    public void GetAcquiredOrder_LaterAdd_IsGreater()
    {
        // Act
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        _inventory.Add(_fish, 1, ItemChangeReason.Test);

        // Assert
        Assert.Greater(_inventory.GetAcquiredOrder(_fish), _inventory.GetAcquiredOrder(_carrot));
    }

    [Test]
    public void GetAcquiredOrder_AddAgain_Updates()
    {
        // Act
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        _inventory.Add(_fish, 1, ItemChangeReason.Test);
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.Greater(_inventory.GetAcquiredOrder(_carrot), _inventory.GetAcquiredOrder(_fish));
    }

    [Test]
    public void GetAcquiredOrder_RemovedToZero_ReturnsZero()
    {
        // Arrange
        _inventory.Add(_carrot, 2, ItemChangeReason.Test);

        // Act
        _inventory.TryRemove(_carrot, 2, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(0, _inventory.GetAcquiredOrder(_carrot));
    }

    [Test]
    public void GetAcquiredOrder_AddFailed_DoesNotUpdate()
    {
        // Arrange: 가득 찬 당근은 더 들어가지 않음
        _inventory.Add(_carrot, 10, ItemChangeReason.Test);
        _inventory.Add(_fish, 1, ItemChangeReason.Test);

        // Act
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.Greater(_inventory.GetAcquiredOrder(_fish), _inventory.GetAcquiredOrder(_carrot));
    }

    #endregion

    #region 새 아이템 (N 표시)

    [Test]
    public void Add_NewKind_IsNewAndFiresEvent()
    {
        // Arrange
        bool? received = null;
        _inventory.OnHasNewItemsChanged += has => received = has;

        // Act
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.IsTrue(_inventory.HasNewItems);
        Assert.IsTrue(_inventory.IsNew(_carrot));
        Assert.AreEqual(true, received);
    }

    [Test]
    public void Add_ExistingKindAfterSeen_IsNotNew()
    {
        // Arrange
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        _inventory.MarkAllSeen();

        // Act
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.IsFalse(_inventory.HasNewItems);
    }

    [Test]
    public void MarkAllSeen_ClearsAndFiresEventOnce()
    {
        // Arrange
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        _inventory.Add(_fish, 1, ItemChangeReason.Test);
        int falseEvents = 0;
        _inventory.OnHasNewItemsChanged += has => { if (!has) falseEvents++; };

        // Act
        _inventory.MarkAllSeen();
        _inventory.MarkAllSeen();

        // Assert
        Assert.IsFalse(_inventory.HasNewItems);
        Assert.AreEqual(1, falseEvents);
    }

    [Test]
    public void TryRemove_UnseenToZero_NoLongerNew()
    {
        // Arrange
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        bool? received = null;
        _inventory.OnHasNewItemsChanged += has => received = has;

        // Act
        _inventory.TryRemove(_carrot, 1, ItemChangeReason.Test);

        // Assert
        Assert.IsFalse(_inventory.HasNewItems);
        Assert.AreEqual(false, received);
    }

    [Test]
    public void Add_SecondNewKind_DoesNotFireAgain()
    {
        // Arrange: 이미 N이 켜져 있으면 뱃지 상태는 그대로
        _inventory.Add(_carrot, 1, ItemChangeReason.Test);
        int events = 0;
        _inventory.OnHasNewItemsChanged += _ => events++;

        // Act
        _inventory.Add(_fish, 1, ItemChangeReason.Test);

        // Assert
        Assert.AreEqual(0, events);
        Assert.IsTrue(_inventory.IsNew(_fish));
    }

    #endregion
}
