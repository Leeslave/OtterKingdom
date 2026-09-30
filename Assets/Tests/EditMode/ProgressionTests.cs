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
    public void ShopAndDecorGoals_CountOncePerAction()
    {
        var shop = CreateQuest("shop");
        var shopSo = new SerializedObject(shop);
        shopSo.FindProperty("_goalType").enumValueIndex = (int)QuestGoalType.ShopPurchase;
        shopSo.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual(1, QuestProgressRules.From(shop, new ItemChangedEvent(null, 0, 10, ItemChangeReason.Purchase)), "10개를 한 번에 사도 1번");
        Assert.AreEqual(0, QuestProgressRules.From(shop, new ItemChangedEvent(null, 0, 3, ItemChangeReason.Harvest)));
    }

    #endregion
}
