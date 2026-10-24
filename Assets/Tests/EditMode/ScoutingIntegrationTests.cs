using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ScoutingIntegrationTests
{
    [Test]
    public void SendingWorkersScouting_DoesNotEraseAlreadyStoredProduction()
    {
        Assert.AreEqual(900f, ProductionRules.AccruedAmount(900f, 60, 60), "일손 감소로 보관 상한이 낮아져도 기존 900개는 보존");
        Assert.AreEqual(25f, ProductionRules.AccruedAmount(25f, 0, 3600), "모두 정찰 중이어도 기존 재료는 보존");
    }

    [Test]
    public void Production_FirstItemAfterOneMinute_AndCapsAtEightHours()
    {
        Assert.AreEqual(1f, ProductionRules.AccruedAmount(0f, 60, 60));
        Assert.AreEqual(480f, ProductionRules.AccruedAmount(0f, 60, 24 * 3600));
        Assert.AreEqual(0f, ProductionRules.AccruedAmount(float.NaN, 60, 0));
    }

    private static SettlementSaveData Save()
    {
        long now = new DateTime(2026, 10, 24).Ticks;
        return new SettlementSaveData
        {
            initialized = true,
            residents = new List<ResidentSaveData> { new ResidentSaveData { otterId = "resident", state = ResidentState.Resident } },
            scouting = new ScoutingSaveData { version = 1, job = new ScoutingJobSaveData
            {
                regionId = "south_1", startUtcTicks = now, endUtcTicks = now + TimeSpan.FromSeconds(90).Ticks,
                workers = new List<string> { "resident" }
            }}
        };
    }

    [Test]
    public void Scouting_UsesExistingWorkState_WithoutRemovingResident()
    {
        var settlement = new Settlement();
        settlement.Load(Save());
        Assert.AreEqual(ResidentWorkState.Working, settlement.GetWorkState("resident"));
        Assert.AreEqual(1, settlement.ResidentCount);
        Assert.Throws<InvalidOperationException>(() => settlement.StartTask("clearing", new[] { "resident" }, 1, 2));
        Assert.IsFalse(settlement.TryGetTaskJob("clearing", out _));
    }

    [Test]
    public void CorruptSave_WithTaskAndScout_PreservesTaskAndCancelsScout()
    {
        var saved = Save();
        saved.tasks.Add(new SettlementTaskSaveData { taskId = "clearing", startUtcTicks = 1, endUtcTicks = 2,
            assignedOtterIds = new List<string> { "resident" } });
        var settlement = new Settlement();
        settlement.Load(saved);
        Assert.IsNull(settlement.Scouting.Destination);
        Assert.IsTrue(settlement.TryGetTaskJob("clearing", out _));
        Assert.AreEqual(ResidentWorkState.Working, settlement.GetWorkState("resident"));
        Assert.AreEqual(1, settlement.ResidentCount);
    }

    [Test]
    public void CorruptSave_WithManagementRole_DoesNotSendClerkScouting()
    {
        var saved = Save();
        saved.roles.Add(new RoleAssignmentSaveData { roleId = "clerk", otterId = "resident", stationId = "hall" });
        var settlement = new Settlement();
        settlement.Load(saved);
        Assert.IsNull(settlement.Scouting.Destination);
        Assert.IsTrue(settlement.HasRole("resident"));
    }

    [Test]
    public void Visitor_CannotBecomeScoutByEditingSave()
    {
        var saved = Save();
        saved.residents[0].state = ResidentState.Visitor;
        var settlement = new Settlement();
        settlement.Load(saved);
        Assert.IsNull(settlement.Scouting.Destination);
        Assert.AreEqual(0, settlement.ResidentCount);
    }

    [Test]
    public void SaveRoundTrip_PreservesScouting_AndReturnFreesExistingWorkState()
    {
        var original = new Settlement();
        original.Load(Save());
        original.UnlockDevelopment("old_building");
        var save = new SettlementSaveData();
        original.Write(save);
        var loaded = new Settlement();
        loaded.Load(save);
        Assert.AreEqual("south_1", loaded.Scouting.Destination);
        loaded.Scouting.Tick(loaded.Scouting.EndUtcTicks);
        Assert.AreEqual(ResidentWorkState.Idle, loaded.GetWorkState("resident"));
        Assert.IsTrue(loaded.HasDevelopment("old_building"));
        Assert.AreEqual(1, loaded.ResidentCount);
        Assert.AreEqual(1, loaded.Scouting.Reports.Count);
    }
}
