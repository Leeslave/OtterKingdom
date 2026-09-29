using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CollectionTests
{
    private readonly List<Object> _created = new List<Object>();

    private CollectionTab _vegetable;
    private CollectionTab _otter;
    private ItemDefinition _carrotItem;
    private ItemDefinition _trashItem;
    private CollectionEntry _carrot;
    private CollectionEntry _farmer;
    private CollectionDatabase _database;
    private Collection _collection;

    [SetUp]
    public void SetUp()
    {
        _vegetable = CreateTab("Vegetable", supportsVisits: false);
        _otter = CreateTab("Otter", supportsVisits: true);
        _carrotItem = CreateItem("crop_carrot");
        _trashItem = CreateItem("trash_basic");
        _carrot = CreateEntry("crop_carrot", _vegetable, _carrotItem);
        _farmer = CreateEntry("otter_farmer", _otter, null);
        _database = CreateDatabase(new[] { _vegetable, _otter }, new[] { _carrot, _farmer });
        _collection = new Collection();
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

    private CollectionTab CreateTab(string id, bool supportsVisits)
    {
        var tab = Track(ScriptableObject.CreateInstance<CollectionTab>());
        var so = new SerializedObject(tab);
        so.FindProperty("_tabId").stringValue = id;
        so.FindProperty("_supportsVisits").boolValue = supportsVisits;
        so.ApplyModifiedPropertiesWithoutUndo();
        return tab;
    }

    private ItemDefinition CreateItem(string id)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private CollectionEntry CreateEntry(string id, CollectionTab tab, ItemDefinition linkedItem)
    {
        var entry = Track(ScriptableObject.CreateInstance<CollectionEntry>());
        var so = new SerializedObject(entry);
        so.FindProperty("_entryId").stringValue = id;
        so.FindProperty("_tab").objectReferenceValue = tab;
        so.FindProperty("_linkedItem").objectReferenceValue = linkedItem;
        so.ApplyModifiedPropertiesWithoutUndo();
        return entry;
    }

    private CollectionDatabase CreateDatabase(CollectionTab[] tabs, CollectionEntry[] entries)
    {
        var db = Track(ScriptableObject.CreateInstance<CollectionDatabase>());
        var so = new SerializedObject(db);
        var tabList = so.FindProperty("_tabs");
        tabList.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
            tabList.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
        var entryList = so.FindProperty("_entries");
        entryList.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
            entryList.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return db;
    }

    #endregion

    #region 모델

    [Test]
    public void GetState_NewEntry_IsUnknown()
    {
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_carrot));
    }

    [Test]
    public void Collect_FiresEventOnceAndStaysCollected()
    {
        // Arrange
        int fired = 0;
        _collection.OnStateChanged += (entry, state) => fired++;

        // Act
        bool first = _collection.Collect(_carrot);
        bool second = _collection.Collect(_carrot);

        // Assert
        Assert.IsTrue(first);
        Assert.IsFalse(second, "이미 획득한 항목은 다시 알리지 않아야 합니다.");
        Assert.AreEqual(1, fired);
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_carrot));
    }

    [Test]
    public void MarkVisited_OnTabWithoutVisits_IsIgnored()
    {
        // Act
        bool changed = _collection.MarkVisited(_carrot);

        // Assert
        Assert.IsFalse(changed);
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_carrot));
    }

    [Test]
    public void MarkVisited_ThenRegister_BecomesCollected()
    {
        // Act
        Assert.IsTrue(_collection.MarkVisited(_farmer));
        Assert.AreEqual(CollectionState.Visited, _collection.GetState(_farmer));
        Assert.IsTrue(_collection.Collect(_farmer));

        // Assert
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_farmer));
    }

    [Test]
    public void MarkVisited_AfterCollected_DoesNotGoBack()
    {
        // Arrange
        _collection.Collect(_farmer);

        // Act
        bool changed = _collection.MarkVisited(_farmer);

        // Assert
        Assert.IsFalse(changed);
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_farmer));
    }

    [Test]
    public void Count_CountsOnlyGivenState()
    {
        // Arrange
        _collection.Collect(_carrot);
        _collection.MarkVisited(_farmer);
        var all = new[] { _carrot, _farmer };

        // Act & Assert
        Assert.AreEqual(1, _collection.Count(all, CollectionState.Collected));
        Assert.AreEqual(1, _collection.Count(all, CollectionState.Visited));
    }

    #endregion

    #region 자동 획득

    [Test]
    public void AutoCollect_LinkedItemAdded_CollectsEntry()
    {
        // Arrange
        var e = new ItemChangedEvent(_carrotItem, 0, 3, ItemChangeReason.Harvest);

        // Act
        bool collected = CollectionAutoCollector.Handle(_collection, _database, e);

        // Assert
        Assert.IsTrue(collected);
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_carrot));
    }

    [Test]
    public void AutoCollect_ItemRemoved_IsIgnored()
    {
        // Arrange: 팔아서 줄어든 변화
        var e = new ItemChangedEvent(_carrotItem, 3, 1, ItemChangeReason.Sell);

        // Act & Assert
        Assert.IsFalse(CollectionAutoCollector.Handle(_collection, _database, e));
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_carrot));
    }

    [Test]
    public void AutoCollect_ItemWithoutEntry_IsIgnored()
    {
        // Arrange: 쓰레기는 도감에 없음
        var e = new ItemChangedEvent(_trashItem, 0, 1, ItemChangeReason.Fishing);

        // Act & Assert
        Assert.IsFalse(CollectionAutoCollector.Handle(_collection, _database, e));
    }

    [Test]
    public void CollectOwned_ItemsAlreadyInBag_AreCollected()
    {
        // Arrange: 도감이 생기기 전 세이브로 가방에 이미 있는 경우
        var inventory = new Inventory(capacity: 4, maxCapacity: 4);
        inventory.LoadItem(_carrotItem, 5);

        // Act
        int count = CollectionAutoCollector.CollectOwned(_collection, _database, inventory);

        // Assert
        Assert.AreEqual(1, count);
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_carrot));
    }

    #endregion

    #region 세이브

    [Test]
    public void Save_WriteThenRead_RestoresStates()
    {
        // Arrange
        _collection.Collect(_carrot);
        _collection.MarkVisited(_farmer);
        var saved = new List<CollectionSaveEntry>();

        // Act
        CollectionSaveConverter.Write(_collection, saved);
        var restored = new Collection();
        CollectionSaveConverter.Read(saved, restored);

        // Assert
        Assert.AreEqual(CollectionState.Collected, restored.GetState(_carrot));
        Assert.AreEqual(CollectionState.Visited, restored.GetState(_farmer));
    }

    [Test]
    public void Save_UnknownId_IsKeptForNextWrite()
    {
        // Arrange: 지금 DB에 없는 항목 (삭제됐거나 아직 추가되지 않음)
        var saved = new List<CollectionSaveEntry> { new CollectionSaveEntry("otter_future", CollectionState.Collected) };

        // Act
        CollectionSaveConverter.Read(saved, _collection);
        var written = new List<CollectionSaveEntry>();
        CollectionSaveConverter.Write(_collection, written);

        // Assert
        Assert.AreEqual(1, written.Count);
        Assert.AreEqual("otter_future", written[0].entryId);
    }

    [Test]
    public void Save_BrokenLines_AreSkipped()
    {
        // Arrange
        var saved = new List<CollectionSaveEntry>
        {
            null,
            new CollectionSaveEntry("", CollectionState.Collected),
            new CollectionSaveEntry("crop_carrot", (CollectionState)99),
        };

        // Act
        CollectionSaveConverter.Read(saved, _collection);

        // Assert
        Assert.AreEqual(0, _collection.States.Count);
    }

    #endregion

    [Test]
    public void Database_FindByItemAndTabOrder()
    {
        // Act & Assert
        Assert.AreEqual(_carrot, _database.FindByItem(_carrotItem));
        Assert.IsNull(_database.FindByItem(_trashItem));
        CollectionAssert.AreEqual(new[] { _farmer }, _database.GetEntries(_otter));
    }
}
