using NUnit.Framework;

public class SaveMigrationTests
{
    private static SaveData OldSave(int version)
    {
        var save = new SaveData { schemaVersion = version };
        save.plots.Add(new PlotSaveData { slots = PlotSaveData.CreateEmptySlots() });
        return save;
    }

    [Test]
    public void Version1_PlayedSave_SkipsGuidesAndKeepsSettlementOpen()
    {
        var save = OldSave(1);
        save.plots[0].slots[0].state = FurrowSlotState.Growing;

        var applied = SaveMigrations.Run(save);

        Assert.AreEqual(3, applied.Count);
        Assert.IsTrue(save.firstPlantGuideDone, "이미 심은 세이브는 첫 심기 안내 없음");
        CollectionAssert.IsSubsetOf(ZoneTutorials.AllIds, save.tutorialsDone);
        CollectionAssert.IsSubsetOf(ZoneTutorials.FeatureIds, save.tutorialsDone);
        Assert.IsTrue(save.settlement.legacyComplete);
        Assert.AreEqual(SaveData.CurrentSchemaVersion, save.schemaVersion);
    }

    [Test]
    public void Version1_UntouchedSave_StillGetsFirstPlantGuide()
    {
        var save = OldSave(1);

        SaveMigrations.Run(save);

        Assert.IsFalse(save.firstPlantGuideDone);
    }

    [Test]
    public void Version3_OnlyRunsLaterSteps()
    {
        var save = OldSave(3);

        var applied = SaveMigrations.Run(save);

        CollectionAssert.AreEqual(new[] { "legacy settlement" }, applied);
        Assert.IsEmpty(save.tutorialsDone, "3 이상은 튜토리얼 단계를 건너뜀");
        Assert.IsTrue(save.settlement.legacyComplete);
    }

    [Test]
    public void Version3_WithStartedSettlement_IsNotMarkedLegacy()
    {
        var save = OldSave(3);
        save.settlement.initialized = true;

        SaveMigrations.Run(save);

        Assert.IsFalse(save.settlement.legacyComplete);
    }

    [Test]
    public void CurrentSave_IsLeftAlone()
    {
        var save = OldSave(SaveData.CurrentSchemaVersion);

        Assert.IsEmpty(SaveMigrations.Run(save));
        Assert.IsFalse(save.settlement.legacyComplete);
    }
}
