using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ProgressionTests
{
    // 테스트용 곡선: Lv.1→2 100, 2→3 200, 3→4 300, 최고 Lv.4
    private class Curve : ILevelCurve
    {
        public int MaxLevel => 4;
        public int ExpToNext(int level) => level >= MaxLevel ? 0 : level * 100;
    }

    private readonly Curve _curve = new Curve();
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private QuestDefinition CreateQuest(string id, int level = 1, QuestDefinition prerequisite = null, int exp = 0, float percent = 0f, QuestKind kind = QuestKind.Main)
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        _created.Add(quest);
        var so = new SerializedObject(quest);
        so.FindProperty("_questId").stringValue = id;
        so.FindProperty("_goal").intValue = 1;
        so.FindProperty("_requiredLevel").intValue = level;
        so.FindProperty("_prerequisite").objectReferenceValue = prerequisite;
        so.FindProperty("_expReward").intValue = exp;
        so.FindProperty("_expPercentOfLevel").floatValue = percent;
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    #region 레벨

    [Test]
    public void AddExp_CarriesOverflowAcrossSeveralLevels()
    {
        var progress = new LevelProgress();
        var reached = new List<int>();

        progress.AddExp(350, _curve, reached); // 100(→2) + 200(→3) + 50 남음

        Assert.AreEqual(3, progress.Level);
        Assert.AreEqual(50, progress.Exp);
        CollectionAssert.AreEqual(new[] { 2, 3 }, reached, "오른 레벨마다 한 번씩 알려야 합니다.");
        Assert.AreEqual(50f / 300f, progress.Ratio(_curve), 0.0001f);
    }

    [Test]
    public void AddExp_StopsAtMaxLevel()
    {
        var progress = new LevelProgress();
        var reached = new List<int>();

        progress.AddExp(10000, _curve, reached);

        Assert.AreEqual(4, progress.Level);
        Assert.AreEqual(0, progress.Exp, "최고 레벨에서는 경험치가 쌓이지 않습니다.");
        Assert.AreEqual(1f, progress.Ratio(_curve));
        progress.AddExp(100, _curve, reached);
        Assert.AreEqual(3, reached.Count);
    }

    [Test]
    public void Load_ClampsBrokenValues()
    {
        var progress = new LevelProgress();
        progress.Load(99, 5000, _curve);
        Assert.AreEqual(4, progress.Level);

        progress.Load(2, 999, _curve);
        Assert.AreEqual(199, progress.Exp, "다음 레벨 경험치를 넘을 수 없습니다.");
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.AddExp(-1, _curve, new List<int>()));
    }

    [Test]
    public void Cap_HoldsLevelWithFullBar_UntilRaised()
    {
        var progress = new LevelProgress();
        var reached = new List<int>();
        progress.SetCap(1, _curve, reached);

        progress.AddExp(250, _curve, reached);
        Assert.AreEqual(1, progress.Level, "큰 발전 전에는 경험치가 차도 레벨이 그대로");
        Assert.AreEqual(100, progress.Exp, "막대는 꽉 찬 채로 기다림");
        Assert.AreEqual(1f, progress.Ratio(_curve));
        Assert.IsEmpty(reached);

        progress.SetCap(int.MaxValue, _curve, reached);
        Assert.AreEqual(2, progress.Level, "제한이 풀리면 쌓아 둔 경험치로 바로 오름");
        CollectionAssert.AreEqual(new[] { 2 }, reached);
    }

    [Test]
    public void ReachLevel_TopsUpMissingExp_AndKeepsLeftover()
    {
        var progress = new LevelProgress();
        var reached = new List<int>();
        progress.SetCap(1, _curve, reached);
        progress.AddExp(40, _curve, reached);

        progress.ReachLevel(2, _curve, reached);
        Assert.AreEqual(2, progress.Level, "의자를 만들면 경험치가 모자라도 Lv.2");
        Assert.AreEqual(0, progress.Exp);
        CollectionAssert.AreEqual(new[] { 2 }, reached);

        progress.ReachLevel(2, _curve, reached);
        Assert.AreEqual(1, reached.Count, "이미 넘은 레벨이면 그대로");
    }

    #endregion

    #region 퀘스트 열림

    [Test]
    public void Chain_OpensAfterPreviousClaimedAndLevelReached()
    {
        var first = CreateQuest("harvest_1");
        var second = CreateQuest("harvest_2", level: 3, prerequisite: first);
        var log = new QuestLog();

        Assert.IsTrue(QuestProgressRules.IsAvailable(first, 1, log));
        Assert.IsFalse(QuestProgressRules.IsAvailable(second, 5, log), "앞 단계를 받기 전에는 닫혀 있어야 합니다.");

        log.AddProgress(first, 1);
        log.TryClaim(first);
        Assert.IsFalse(QuestProgressRules.IsAvailable(second, 2, log), "레벨이 모자라면 닫혀 있어야 합니다.");
        Assert.IsTrue(QuestProgressRules.IsAvailable(second, 3, log));
    }

    [Test]
    public void Completed_ShowsOnlyClaimedAtCurrentLevel_DailyAlways()
    {
        var early = CreateQuest("early");
        var now = CreateQuest("now");
        var open = CreateQuest("open");
        var daily = CreateQuest("daily", kind: QuestKind.Daily);
        var log = new QuestLog();
        foreach (var quest in new[] { early, now, open, daily })
            log.AddProgress(quest, 1);

        log.TryClaim(early, 1);
        log.TryClaim(now, 2);
        log.TryClaim(daily, 1);
        log.LoadRecord("old", 1, true); // 받은 레벨이 생기기 전 세이브
        var old = CreateQuest("old");

        Assert.IsFalse(QuestProgressRules.ShowsAsCompleted(early, 2, log), "이전 레벨에 받은 것은 빠져야 합니다.");
        Assert.IsTrue(QuestProgressRules.ShowsAsCompleted(now, 2, log));
        Assert.IsFalse(QuestProgressRules.ShowsAsCompleted(open, 2, log), "받지 않은 것은 완료 칸이 아닙니다.");
        Assert.IsTrue(QuestProgressRules.ShowsAsCompleted(daily, 2, log), "오늘 받은 일일은 레벨과 상관없이 남습니다.");
        Assert.IsFalse(QuestProgressRules.ShowsAsCompleted(old, 1, log), "받은 레벨을 모르는 옛 기록은 남기지 않습니다.");
    }

    [Test]
    public void ClaimedLevel_SurvivesSave()
    {
        var quest = CreateQuest("q");
        var log = new QuestLog();
        log.AddProgress(quest, 1);
        log.TryClaim(quest, 3);

        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(log, saved);
        var loaded = new QuestLog();
        QuestSaveConverter.Read(saved, loaded);

        Assert.AreEqual(3, loaded.GetClaimedLevel(quest));
        Assert.AreEqual(QuestStatus.Claimed, loaded.GetStatus(quest));
    }

    [Test]
    public void DailyExp_FollowsCurrentLevel()
    {
        var daily = CreateQuest("daily", exp: 5, percent: 10f, kind: QuestKind.Daily);
        var fixedExp = CreateQuest("main", exp: 40);

        Assert.AreEqual(10, daily.ExpFor(1, _curve), "Lv.1 → 100의 10%");
        Assert.AreEqual(30, daily.ExpFor(3, _curve), "Lv.3 → 300의 10%");
        Assert.AreEqual(5, daily.ExpFor(4, _curve), "최고 레벨이면 고정값");
        Assert.AreEqual(40, fixedExp.ExpFor(3, _curve));
    }

    [Test]
    public void DailyReset_ClearsOnlyGivenQuests_AndDaySurvivesSave()
    {
        var daily = CreateQuest("daily", kind: QuestKind.Daily);
        var main = CreateQuest("main");
        var log = new QuestLog();
        log.AddProgress(daily, 1);
        log.AddProgress(main, 1);

        log.Reset(new[] { daily }, 739000);

        Assert.AreEqual(QuestStatus.InProgress, log.GetStatus(daily));
        Assert.AreEqual(QuestStatus.Claimable, log.GetStatus(main), "성장 퀘스트는 그대로");

        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(log, saved);
        var loaded = new QuestLog();
        QuestSaveConverter.Read(saved, loaded);
        Assert.AreEqual(739000, loaded.DailyDay);
        Assert.IsFalse(loaded.Records.ContainsKey(QuestSaveConverter.DailyDayId), "날짜 줄은 퀘스트 기록이 아닙니다.");
    }

    [Test]
    public void TodayNumber_ChangesAt4AM()
    {
        var before = QuestManager.TodayNumber(new DateTime(2026, 10, 1, 3, 59, 0));
        var after = QuestManager.TodayNumber(new DateTime(2026, 10, 1, 4, 0, 0));

        Assert.AreEqual(before + 1, after);
    }

    [Test]
    public void ShopGoal_CountsQuantityBoughtAtOnce()
    {
        var shop = CreateQuest("shop");
        var shopSo = new SerializedObject(shop);
        shopSo.FindProperty("_goalType").enumValueIndex = (int)QuestGoalType.ShopPurchase;
        shopSo.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual(5, QuestProgressRules.FromShopPurchase(shop, 5), "한 번에 5개 사면 5");
        Assert.AreEqual(1, QuestProgressRules.FromShopPurchase(shop, 1));
        Assert.AreEqual(0, QuestProgressRules.FromShopPurchase(shop, 0));
        Assert.AreEqual(0, QuestProgressRules.From(shop, new ItemChangedEvent(null, 0, 10, ItemChangeReason.Purchase)),
            "가방에 들어온 개수로는 세지 않음 (묶음 상품이 부풀려지지 않게)");
        Assert.AreEqual(0, QuestProgressRules.From(shop, new ItemChangedEvent(null, 0, 3, ItemChangeReason.Harvest)));

        var harvest = CreateQuest("harvest");
        Assert.AreEqual(0, QuestProgressRules.FromShopPurchase(harvest, 5), "다른 퀘스트는 구매를 세지 않음");
    }

    #endregion
}
