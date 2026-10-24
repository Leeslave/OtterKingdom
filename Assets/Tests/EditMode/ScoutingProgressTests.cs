using System;
using NUnit.Framework;

public class ScoutingProgressTests
{
    private static readonly long Now = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc).Ticks;
    private static readonly TimeSpan Trip = TimeSpan.FromSeconds(90);

    [Test]
    public void Return_ReleasesWorkersBeforeReport_AndCannotRewardTwice()
    {
        var state = new ScoutingProgress();
        Assert.IsTrue(state.TryStart("south_1", new[] { "resident" }, _ => true, Now, Trip));
        Assert.IsTrue(state.HasWorker("resident"));
        Assert.IsNull(state.Tick(Now + Trip.Ticks - 1));
        Assert.AreEqual("south_1", state.Tick(Now + Trip.Ticks));
        Assert.IsFalse(state.HasWorker("resident"));
        Assert.IsTrue(state.HasDiscovered("south_1"));
        Assert.IsNull(state.Tick(Now + Trip.Ticks * 2));
        Assert.AreEqual(1, state.Reports.Count);
    }

    [Test]
    public void Offline_ReloadReturnsExactlyOnce_AndKeepsUnreadReport()
    {
        var state = new ScoutingProgress();
        state.TryStart("south_1", new[] { "one", "two" }, _ => true, Now, Trip);
        var loaded = new ScoutingProgress(state.Save());
        Assert.AreEqual("south_1", loaded.Tick(Now + TimeSpan.FromHours(20).Ticks));
        var reloaded = new ScoutingProgress(loaded.Save());
        Assert.IsNull(reloaded.Tick(Now + TimeSpan.FromHours(21).Ticks));
        Assert.AreEqual(1, reloaded.Reports.Count);
        Assert.AreEqual(0, reloaded.Workers.Count);
        reloaded.ReadReport("south_1");
        Assert.AreEqual(0, new ScoutingProgress(reloaded.Save()).Reports.Count);
    }

    [Test]
    public void BusyOrDuplicateWorker_RejectsWholeParty()
    {
        var state = new ScoutingProgress();
        Assert.IsFalse(state.TryStart("west_1", new[] { "free", "busy" }, id => id == "free", Now, Trip));
        Assert.IsNull(state.Destination);
        Assert.IsFalse(state.HasWorker("free"));
        Assert.IsFalse(state.TryStart("west_1", new[] { "free", "free" }, _ => true, Now, Trip));
        Assert.IsFalse(state.TryStart("west_1", Array.Empty<string>(), _ => true, Now, Trip));
        Assert.IsFalse(state.TryStart("west_1", new[] { "a", "b", "c" }, _ => true, Now, Trip));
    }

    [Test]
    public void OneParty_AndDiscoveredDestinationCannotBeStartedAgain()
    {
        var state = new ScoutingProgress();
        Assert.IsTrue(state.TryStart("south_1", new[] { "one" }, _ => true, Now, Trip));
        Assert.IsFalse(state.TryStart("west_1", new[] { "two" }, _ => true, Now, Trip));
        state.Tick(Now + Trip.Ticks);
        Assert.IsFalse(state.TryStart("south_1", new[] { "one" }, _ => true, Now + Trip.Ticks, Trip));
        Assert.IsTrue(state.TryStart("west_1", new[] { "one" }, _ => true, Now + Trip.Ticks, Trip));
    }

    [Test]
    public void Cancel_HasNoDiscovery_AndRestartTakesFullDuration()
    {
        var state = new ScoutingProgress();
        state.TryStart("west_1", new[] { "one" }, _ => true, Now, Trip);
        Assert.IsTrue(state.Cancel());
        Assert.IsFalse(state.Cancel());
        Assert.IsFalse(state.HasWorker("one"));
        Assert.IsFalse(state.HasDiscovered("west_1"));
        Assert.AreEqual(0, state.Reports.Count);
        Assert.IsTrue(state.TryStart("west_1", new[] { "one" }, _ => true, Now + Trip.Ticks / 2, Trip));
        Assert.IsNull(state.Tick(Now + Trip.Ticks));
        Assert.AreEqual("west_1", state.Tick(Now + Trip.Ticks * 3 / 2));
    }

    [Test]
    public void ClockMovedBackwards_DoesNotFinishOrDuplicateDiscovery()
    {
        var state = new ScoutingProgress();
        state.TryStart("west_1", new[] { "one" }, _ => true, Now, Trip);
        Assert.IsNull(state.Tick(Now - Trip.Ticks));
        Assert.AreEqual("west_1", state.Destination);
        state.Tick(Now + Trip.Ticks);
        Assert.IsNull(state.Tick(Now));
        Assert.AreEqual(1, state.Reports.Count);
    }

    [Test]
    public void SaveAndInputLists_AreIndependentCopies()
    {
        var state = new ScoutingProgress();
        var workers = new[] { "one" };
        state.TryStart("west_1", workers, _ => true, Now, Trip);
        workers[0] = "changed";
        var saved = state.Save();
        saved.job.workers.Clear();
        Assert.IsTrue(state.HasWorker("one"));
        Assert.AreEqual(1, state.Workers.Count);
    }

    [Test]
    public void CorruptSave_DuplicateWorkersOrInvalidTime_ReleasesParty()
    {
        var save = new ScoutingSaveData { job = new ScoutingJobSaveData
        {
            regionId = "west_1", startUtcTicks = Now, endUtcTicks = Now + Trip.Ticks,
            workers = new System.Collections.Generic.List<string> { "one", "one" }
        }};
        Assert.IsNull(new ScoutingProgress(save).Destination);
        save.job.workers.RemoveAt(1);
        save.job.endUtcTicks = Now - 1;
        Assert.IsNull(new ScoutingProgress(save).Destination);
        save.job.endUtcTicks = long.MaxValue;
        Assert.IsNull(new ScoutingProgress(save).Destination);
    }

    [Test]
    public void InvalidDuration_OrTickOverflow_IsRejected()
    {
        var state = new ScoutingProgress();
        Assert.IsFalse(state.TryStart("west_1", new[] { "one" }, _ => true, Now, TimeSpan.Zero));
        Assert.IsFalse(state.TryStart("west_1", new[] { "one" }, _ => true, DateTime.MaxValue.Ticks, Trip));
        Assert.IsFalse(state.TryStart("west_1", new[] { "one" }, _ => true, 0, Trip));
        Assert.IsFalse(state.TryStart("west_1", new[] { "" }, _ => true, Now, Trip));
    }

    [Test]
    public void Migration_DiscoversOldLandWithoutGivingReportOrReward()
    {
        var state = new ScoutingProgress();
        Assert.IsTrue(state.Discover("old_land"));
        Assert.IsFalse(state.Discover("old_land"));
        state.MarkMigrated();
        var loaded = new ScoutingProgress(state.Save());
        Assert.AreEqual(1, loaded.Version);
        Assert.IsTrue(loaded.HasDiscovered("old_land"));
        Assert.AreEqual(0, loaded.Reports.Count);
    }

    [Test]
    public void FastTimer_ChangesReturnTimeWithoutLosingResidents()
    {
        var state = new ScoutingProgress();
        state.TryStart("west_1", new[] { "one" }, _ => true, Now, Trip);
        state.Shorten(Now, TimeSpan.FromSeconds(5));
        Assert.IsNull(state.Tick(Now + TimeSpan.FromSeconds(4).Ticks));
        Assert.AreEqual("west_1", state.Tick(Now + TimeSpan.FromSeconds(5).Ticks));
    }
}
