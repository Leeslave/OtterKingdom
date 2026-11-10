using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>임시 메모리 DB와 매니저만 사용한다. 실제 세이브/씬/프리팹은 변경하지 않는다.</summary>
public class ProsperityQuestFlowUnityTests
{
    private readonly List<Object> _objects = new List<Object>();
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
    private ProfileManager _profile;
    private QuestManager _quests;
    private QuestDatabase _database;
    private LevelTable _curve;

    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    private static void Call(object owner, string method) => owner.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
    private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
    private T Component<T>() where T : Component { var root = new GameObject(typeof(T).Name); root.SetActive(false); _objects.Add(root); return root.AddComponent<T>(); }

    [SetUp]
    public void SetUp()
    {
        foreach (var type in new[] { typeof(ProfileManager), typeof(QuestManager), typeof(ProsperityManager), typeof(SettlementManager) })
        {
            var property = type.GetProperty("Instance");
            _singletons[type] = property.GetValue(null);
            property.SetValue(null, null);
        }
        _curve = Make<LevelTable>();
        Set(_curve, "_levels", Enumerable.Range(1, 49).Select(_ => new LevelTable.Entry(1003, null, 0)).ToList());
        _profile = Component<ProfileManager>();
        Set(_profile, "_levelTable", _curve);
        Call(_profile, "Awake");
        _profile.LoadFromSave(new ProfileSaveData { name = "검증왕", level = 23 });
        _database = Make<QuestDatabase>();
        var rows = new List<QuestDefinition>();
        QuestDefinition previous = null;
        for (int level = 22; level <= 49; level++)
        foreach (string key in new[] { "daily", "harvest", "decor", "sales" })
        {
            var row = Make<QuestDefinition>();
            Set(row, "_questId", $"main_lv{level}_{key}");
            Set(row, "_title", key);
            Set(row, "_kind", QuestKind.Main);
            Set(row, "_requiredLevel", level);
            Set(row, "_sortOrder", rows.Count);
            Set(row, "_goalType", key == "sales" ? QuestGoalType.EarnFromSales : key == "daily" ? QuestGoalType.CompleteDaily : key == "harvest" ? QuestGoalType.Harvest : QuestGoalType.PlaceDecor);
            Set(row, "_goal", 10);
            Set(row, "_rewardAmount", 101);
            Set(row, "_expReward", key == "sales" ? 253 : 250);
            Set(row, "_prerequisite", previous);
            rows.Add(row); previous = row;
        }
        Set(_database, "_quests", rows);
        _quests = Component<QuestManager>();
        Set(_quests, "_database", _database);
        Set(_quests, "_profileManager", _profile);
        Call(_quests, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        // 비활성 테스트 오브젝트에서도 런타임 복사본을 확실히 정리한다.
        if (_quests != null)
        {
            foreach (var row in _quests.Database.Quests.Where(q => ProsperityQuestPlan.IsNew(q.QuestId)).ToArray()) Object.DestroyImmediate(row);
            if (_quests.Database != _database) Object.DestroyImmediate(_quests.Database);
            Set(_quests, "_originalQuestDatabase", null);
        }
        foreach (var obj in _objects.AsEnumerable().Reverse()) if (obj != null) Object.DestroyImmediate(obj);
        _objects.Clear();
        foreach (var pair in _singletons) pair.Key.GetProperty("Instance").SetValue(null, pair.Value);
        _singletons.Clear();
    }

    private void Load(params QuestSaveEntry[] extra)
    {
        var entries = new List<QuestSaveEntry> { new QuestSaveEntry(QuestSaveConverter.MainChainId, QuestManager.MainChainVersion, false) };
        foreach (var row in _database.Quests.Where(q => q.RequiredLevel < 23))
            entries.Add(new QuestSaveEntry(row.QuestId, row.Goal, true, row.RequiredLevel));
        entries.AddRange(extra);
        _quests.LoadFromSave(entries);
    }

    [Test]
    public void NewP0ClonesDatabase_PreservesBudgets_AndSelectsOnlyNewChain()
    {
        int originalCount = _database.Quests.Count;
        Load();
        Assert.That(_database.Quests.Count, Is.EqualTo(originalCount));
        Assert.That(_quests.Database.Quests.Count, Is.EqualTo(originalCount + 25));
        Assert.That(_quests.CurrentMainQuest.QuestId, Is.EqualTo("prosperity_main_23_1"));
        Assert.That(_quests.IsAvailable(_quests.Database.Quests.First(q => q.QuestId == "main_lv23_daily")), Is.False);
        for (int level = 23; level <= 27; level++)
        {
            var rows = _quests.Database.Quests.Where(q => ProsperityQuestPlan.IsNew(q.QuestId) && q.RequiredLevel == level).ToArray();
            Assert.That(rows.Sum(q => q.ExpFor(level, _curve)), Is.EqualTo(1003));
            Assert.That(rows.Sum(q => q.RewardAmount), Is.EqualTo(404));
        }
    }

    [Test]
    public void PartialLegacyCurrentLevelFinishesBeforeNextNewLevel()
    {
        Load(new QuestSaveEntry("main_lv23_harvest", 7, false));
        Assert.That(_quests.Log.Records[ProsperityQuestPlan.TransitionId].Progress, Is.EqualTo(24));
        Assert.That(_quests.Log.Records["main_lv23_harvest"].Progress, Is.EqualTo(7));
        _profile.LoadFromSave(new ProfileSaveData { name = "검증왕", level = 24 });
        var first = _quests.Database.Quests.First(q => q.QuestId == "prosperity_main_24_1");
        Assert.That(_quests.IsAvailable(first), Is.False);
        _quests.Log.LoadRecord("main_lv23_sales", 10, true, 23);
        Assert.That(_quests.IsAvailable(first), Is.True);
    }

    [Test]
    public void MainRewardsCannotBeClaimedTwice_AndNextStepIsStrictlySequential()
    {
        Load();
        var first = _quests.CurrentMainQuest;
        var second = _quests.Database.Quests.First(q => q.QuestId == "prosperity_main_23_2");
        Assert.That(_quests.IsAvailable(second), Is.False);
        _quests.Log.SetProgressAtLeast(first, 1);
        Assert.That(_quests.TryClaim(first), Is.True);
        int xp = _profile.Progress.Exp;
        Assert.That(xp, Is.EqualTo(200));
        Assert.That(_quests.TryClaim(first), Is.False);
        Assert.That(_profile.Progress.Exp, Is.EqualTo(xp));
        Assert.That(_quests.IsAvailable(second), Is.True);
    }

    [Test]
    public void LastNew27BridgesToExisting28_WithoutClaimingLegacy27()
    {
        Load();
        _profile.LoadFromSave(new ProfileSaveData { name = "검증왕", level = 28 });
        var first28 = _quests.Database.Quests.First(q => q.QuestId == "main_lv28_daily");
        Assert.That(_quests.IsAvailable(first28), Is.False);
        _quests.Log.LoadRecord("prosperity_main_27_5", 1, true, 27);
        Assert.That(_quests.IsAvailable(first28), Is.True);
        Assert.That(_quests.Log.Records.ContainsKey("main_lv27_sales"), Is.False);
    }

    [Test]
    public void StockGoalTracksConsumptionBeforeCompletion_AndPreservesReachedGoal()
    {
        Load();
        var stock = _quests.Database.Quests.First(q => q.QuestId == "prosperity_main_24_4");
        var log = _quests.Log;
        log.SetObservedProgress(stock, 4);
        log.SetObservedProgress(stock, 1);
        Assert.That(log.GetProgress(stock), Is.EqualTo(1));
        log.SetObservedProgress(stock, 6);
        log.SetObservedProgress(stock, 0);
        Assert.That(log.GetStatus(stock), Is.EqualTo(QuestStatus.Claimable));
        Assert.That(log.GetProgress(stock), Is.EqualTo(6));
    }

    [Test]
    public void Existing28KeepsOriginalDatabaseAndProgress()
    {
        _profile.LoadFromSave(new ProfileSaveData { name = "검증왕", level = 28 });
        Load(new QuestSaveEntry("main_lv27_sales", 10, true, 27), new QuestSaveEntry("main_lv28_daily", 2, false));
        Assert.That(_quests.Database, Is.SameAs(_database));
        Assert.That(_quests.Log.Records["main_lv28_daily"].Progress, Is.EqualTo(2));
        Assert.That(_quests.Log.Records[ProsperityQuestPlan.TransitionId].Progress, Is.Zero);
    }
}
