using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public class PlazaMomentTests
{
    private static PlazaMoment SleepyBench => PlazaMomentRules.ForOtter("otter_sleepy");
    private static PlazaMoment SecretTable => PlazaMomentRules.ForOtter("otter_toy_jaejal");

    [Test]
    public void IsOpen_OnlyAfterPhotoStudioBuilt()
    {
        var developments = new HashSet<string>();
        Assert.IsFalse(PlazaMomentRules.IsOpen(developments.Contains));

        developments.Add(StoryArrivalRules.PhotoStudioBuiltDevelopment);
        Assert.IsTrue(PlazaMomentRules.IsOpen(developments.Contains));
    }

    [Test]
    public void CanStart_NeedsTheRightSpotSeatsAndCompanions()
    {
        Assert.IsTrue(PlazaMomentRules.CanStart(SleepyBench, PlazaSpotKind.Sit, freeSeats: 1, freeCompanions: 3));

        Assert.IsFalse(PlazaMomentRules.CanStart(SleepyBench, null, 1, 3), "자리에 없음");
        Assert.IsFalse(PlazaMomentRules.CanStart(SleepyBench, PlazaSpotKind.Eat, 1, 3), "다른 종류의 자리");
        Assert.IsFalse(PlazaMomentRules.CanStart(SleepyBench, PlazaSpotKind.Sit, 0, 3), "옆자리가 없음");
        Assert.IsFalse(PlazaMomentRules.CanStart(SleepyBench, PlazaSpotKind.Sit, 1, 0), "부를 해달이 없음");

        // 재잘이는 동료 둘 (웃는 둘 + 얼굴을 들이미는 셋째)
        Assert.IsFalse(PlazaMomentRules.CanStart(SecretTable, PlazaSpotKind.Eat, 1, 5));
        Assert.IsTrue(PlazaMomentRules.CanStart(SecretTable, PlazaSpotKind.Eat, 2, 2));
    }

    [Test]
    public void EveryMoment_HasBeatsForItsCast()
    {
        foreach (var moment in PlazaMomentRules.All)
        {
            Assert.Greater(moment.Beats.Count, 0, moment.EntryId);
            foreach (var (speaker, line) in moment.Beats)
            {
                Assert.LessOrEqual(speaker, moment.Companions, $"{moment.EntryId}: 없는 해달의 말");
                Assert.IsFalse(string.IsNullOrWhiteSpace(line), moment.EntryId);
            }
        }
    }

    [Test]
    public void FormatDate_IsDotted()
    {
        Assert.AreEqual("2026.10.24", PlazaMomentRules.FormatDate(new DateTime(2026, 10, 24, 21, 5, 0)));
    }

    [Test]
    public void EveryMoment_HasCollectionEntryAndOtter()
    {
        var database = AssetDatabase.LoadAssetAtPath<CollectionDatabase>("Assets/Scriptable Obejects/Collection/CollectionDatabase.asset");
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>("Assets/Scriptable Obejects/Settlement/SettlementConfig.asset");
        Assert.IsNotNull(database);
        Assert.IsNotNull(config);

        foreach (var moment in PlazaMomentRules.All)
        {
            Assert.IsTrue(database.TryGet(moment.EntryId, out var entry), $"도감에 없음: {moment.EntryId}");
            Assert.AreEqual("Moment", entry.Tab.TabId, moment.EntryId);
            Assert.IsNotNull(entry.Portrait, $"{moment.EntryId}: 사진이 없음");
            Assert.IsNotNull(entry.Silhouette, $"{moment.EntryId}: 실루엣이 없음");
            Assert.IsFalse(string.IsNullOrEmpty(entry.Hint), $"{moment.EntryId}: 힌트가 없음");
            Assert.IsNotNull(config.FindOtter(moment.OtterId), $"주인공 해달이 없음: {moment.OtterId}");
        }
    }
}
