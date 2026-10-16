using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SaveSnapshotTests
{
    private const string FileName = "save_snapshot_test.json";
    private SaveService service;

    [SetUp]
    public void SetUp()
    {
        service = new SaveService(FileName);
        Cleanup();
    }

    [TearDown]
    public void TearDown() => Cleanup();

    private void Cleanup()
    {
        foreach (var file in Directory.GetFiles(Application.persistentDataPath, FileName + "*"))
            File.Delete(file);
        if (Directory.Exists(service.SnapshotRoot)) Directory.Delete(service.SnapshotRoot, true);
    }

    private static SaveData CreateSave(int farmLevel)
    {
        var save = new SaveData { farmLevel = farmLevel };
        save.plots.Add(new PlotSaveData());
        return save;
    }

    [Test]
    public void Wipe_MovesSaveIntoSnapshot_AndNextLoadIsNewGame()
    {
        Assert.IsTrue(service.Save(CreateSave(3)));

        string name = service.WipeToSnapshot();

        Assert.IsNotNull(name);
        Assert.IsFalse(service.HasSaveFiles);
        Assert.AreEqual(SaveLoadStatus.NoSave, service.Load(out _));
        Assert.IsTrue(service.TryReadSnapshot(name, out var snapshot));
        Assert.AreEqual(3, snapshot.farmLevel);
    }

    [Test]
    public void Wipe_WithNoSave_ReturnsNull()
    {
        Assert.IsNull(service.WipeToSnapshot());
        Assert.AreEqual(0, service.ListSnapshots().Count);
    }

    [Test]
    public void Restore_BringsSnapshotBack_AndKeepsReplacedSave()
    {
        service.Save(CreateSave(3));
        string wiped = service.WipeToSnapshot();
        service.Save(CreateSave(1)); // the fresh game played after the wipe

        Assert.IsTrue(service.RestoreSnapshot(wiped));

        Assert.AreEqual(SaveLoadStatus.Loaded, service.Load(out var restored));
        Assert.AreEqual(3, restored.farmLevel);
        // The restored snapshot stays, and the fresh save was snapshotted too.
        var names = service.ListSnapshots();
        Assert.AreEqual(2, names.Count);
        Assert.Contains(wiped, names);
        string other = names[0] == wiped ? names[1] : names[0];
        Assert.IsTrue(service.TryReadSnapshot(other, out var replaced));
        Assert.AreEqual(1, replaced.farmLevel);
    }

    [Test]
    public void Delete_RemovesSnapshot()
    {
        service.Save(CreateSave(2));
        string name = service.WipeToSnapshot();

        service.DeleteSnapshot(name);

        Assert.AreEqual(0, service.ListSnapshots().Count);
    }
}
