using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SaveRepairTests
{
    private const string FileName = "save_repair_test.json";

    [TearDown]
    public void TearDown()
    {
        foreach (var file in Directory.GetFiles(Application.persistentDataPath, FileName + "*"))
            File.Delete(file);
    }

    [Test]
    public void Sanitize_FixesMissingListsAndOddNumbers()
    {
        var save = new SaveData
        {
            quests = null,
            profile = null,
            farmLevel = 0,
            lifetimeSales = -5,
            miningSecToNextFind = float.NaN,
            offlineFishingProgressSec = -3f,
        };
        save.plots.Add(new PlotSaveData { slots = PlotSaveData.CreateEmptySlots() });
        save.plots.Add(null);
        save.plots[0].slots[1] = null;
        save.plots[0].slots[2].remainingSec = float.PositiveInfinity;

        var fixes = SaveDataSanitizer.Sanitize(save);

        Assert.IsNotEmpty(fixes);
        Assert.IsNotNull(save.quests);
        Assert.AreEqual(1, save.profile.level);
        Assert.AreEqual(1, save.farmLevel);
        Assert.AreEqual(0, save.lifetimeSales);
        Assert.AreEqual(0f, save.miningSecToNextFind);
        Assert.AreEqual(0f, save.offlineFishingProgressSec);
        Assert.AreEqual(1, save.plots.Count, "빈 밭 줄은 버림");
        Assert.AreEqual(FurrowSlotState.Empty, save.plots[0].slots[1].state);
        Assert.AreEqual(0f, save.plots[0].slots[2].remainingSec);
    }

    [Test]
    public void Sanitize_LeavesGoodSaveAlone()
    {
        var save = new SaveData { farmLevel = 2, lifetimeSales = 30, miningSecToNextFind = 12f };
        save.plots.Add(new PlotSaveData { slots = PlotSaveData.CreateEmptySlots() });

        Assert.IsEmpty(SaveDataSanitizer.Sanitize(save));
        Assert.AreEqual(2, save.farmLevel);
        Assert.AreEqual(12f, save.miningSecToNextFind);
    }

    [Test]
    public void Load_KeepsOnlyNewestCorruptFiles()
    {
        string dir = Application.persistentDataPath;
        string[] stamps = { "20260101-000000", "20260102-000000", "20260103-000000", "20260104-000000", "20260105-000000" };
        foreach (var stamp in stamps)
            File.WriteAllText(Path.Combine(dir, $"{FileName}.corrupt-{stamp}"), "x");

        new SaveService(FileName).Load(out _);

        var left = Directory.GetFiles(dir, FileName + "*.corrupt-*");
        Assert.AreEqual(SaveService.KeptCorruptFiles, left.Length);
        Assert.IsFalse(File.Exists(Path.Combine(dir, $"{FileName}.corrupt-20260101-000000")), "가장 오래된 것부터 지움");
        Assert.IsTrue(File.Exists(Path.Combine(dir, $"{FileName}.corrupt-20260105-000000")));
    }
}
