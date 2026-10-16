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

    #region 해금 조건 (도감_이벤트컷씬_기획서 1.1)

    [TestCase(ItemChangeReason.Purchase)]
    [TestCase(ItemChangeReason.Grant)]
    [TestCase(ItemChangeReason.Load)]
    public void AutoCollect_NotProducedByPlayer_IsIgnored(ItemChangeReason reason)
    {
        // Arrange: 사거나 받거나 세이브에서 복원된 것은 해금 조건이 아님
        var e = new ItemChangedEvent(_carrotItem, 0, 3, reason);

        // Act & Assert
        Assert.IsFalse(CollectionAutoCollector.Handle(_collection, _database, e));
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_carrot));
    }

    [TestCase(ItemChangeReason.Harvest)]
    [TestCase(ItemChangeReason.Fishing)]
    [TestCase(ItemChangeReason.Mining)]
    public void AutoCollect_ProducedByPlayer_Collects(ItemChangeReason reason)
    {
        var e = new ItemChangedEvent(_carrotItem, 0, 1, reason);

        Assert.IsTrue(CollectionAutoCollector.Handle(_collection, _database, e));
    }

    [Test]
    public void LevelUnlock_BeforeLevel_DoesNothing_AtLevel_Registers()
    {
        // Arrange: 농부 해달 2레벨 해금
        SetUnlockLevel(_farmer, 2);

        // Act & Assert
        Assert.AreEqual(0, CollectionLevelUnlocker.Unlock(_collection, _database, 1));
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_farmer));

        Assert.AreEqual(1, CollectionLevelUnlocker.Unlock(_collection, _database, 2));
        Assert.AreEqual(CollectionState.Collected, _collection.GetState(_farmer));

        Assert.AreEqual(0, CollectionLevelUnlocker.Unlock(_collection, _database, 5), "이미 등록한 항목은 다시 세지 않음");
    }

    [Test]
    public void LevelUnlock_EntryWithoutLevel_IsIgnored()
    {
        // 레벨 해금이 아닌 항목(당근)은 몇 레벨이든 그대로
        Assert.AreEqual(0, CollectionLevelUnlocker.Unlock(_collection, _database, 99));
        Assert.AreEqual(CollectionState.Unknown, _collection.GetState(_carrot));
    }

    private void SetUnlockLevel(CollectionEntry entry, int level)
    {
        var so = new SerializedObject(entry);
        so.FindProperty("_unlockLevel").intValue = level;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 이야기

    private CollectionStory AttachStory(CollectionEntry entry, params string[] lines)
    {
        var story = Track(ScriptableObject.CreateInstance<CollectionStory>());
        var storySo = new SerializedObject(story);
        var list = storySo.FindProperty("_lines");
        list.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_text").stringValue = lines[i];
        storySo.ApplyModifiedPropertiesWithoutUndo();

        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_story").objectReferenceValue = story;
        entrySo.ApplyModifiedPropertiesWithoutUndo();
        return story;
    }

    [Test]
    public void NewStory_OnlyAfterCollected_AndUntilWatched()
    {
        // Arrange
        AttachStory(_carrot, "{이름}님, 이것 좀 보세요!");

        // Act & Assert
        Assert.IsFalse(_collection.HasNewStory(_carrot), "해금 전에는 볼 수 없음");

        _collection.Collect(_carrot);
        Assert.IsTrue(_collection.HasNewStory(_carrot));
        Assert.AreEqual(1, _collection.CountNewStories(_database.Entries));

        Assert.IsTrue(_collection.MarkStoryWatched(_carrot));
        Assert.IsFalse(_collection.HasNewStory(_carrot));
        Assert.IsFalse(_collection.MarkStoryWatched(_carrot), "두 번째는 처음이 아님");
    }

    [Test]
    public void NewStory_EntryWithoutStory_IsNeverNew()
    {
        _collection.Collect(_carrot);

        Assert.IsFalse(_collection.HasNewStory(_carrot));
    }

    [Test]
    public void StoriesChanged_FiresOnCollectWithStory_AndOnWatch()
    {
        // Arrange
        AttachStory(_carrot, "대사");
        int fired = 0;
        _collection.OnStoriesChanged += () => fired++;

        // Act
        _collection.Collect(_carrot);
        _collection.MarkStoryWatched(_carrot);
        _collection.Collect(_farmer); // 이야기 없는 항목은 알리지 않음

        // Assert
        Assert.AreEqual(2, fired);
    }

    [Test]
    public void Save_StoryWatched_IsRestored_OldSaveStartsUnwatched()
    {
        // Arrange
        AttachStory(_carrot, "대사");
        AttachStory(_farmer, "대사");
        _collection.Collect(_carrot);
        _collection.Collect(_farmer);
        _collection.MarkStoryWatched(_carrot);
        var saved = new List<CollectionSaveEntry>();

        // Act
        CollectionSaveConverter.Write(_collection, saved);
        var restored = new Collection();
        CollectionSaveConverter.Read(saved, restored);

        // Assert
        Assert.IsFalse(restored.HasNewStory(_carrot), "본 이야기는 다시 N이 뜨지 않음");
        Assert.IsTrue(restored.HasNewStory(_farmer));

        // 옛 세이브 (storyWatched 필드 없음) → 안 본 것으로 시작
        var old = JsonUtility.FromJson<CollectionSaveEntry>("{\"entryId\":\"crop_carrot\",\"state\":2}");
        var fromOld = new Collection();
        CollectionSaveConverter.Read(new List<CollectionSaveEntry> { old }, fromOld);
        Assert.IsTrue(fromOld.HasNewStory(_carrot));
    }

    [Test]
    public void FormatLine_ReplacesEveryNameToken()
    {
        string line = CollectionStory.FormatLine("{이름}님, {이름}님! 이것 좀 보세요!", "해달왕");

        Assert.AreEqual("해달왕님, 해달왕님! 이것 좀 보세요!", line);
    }

    #endregion
}
