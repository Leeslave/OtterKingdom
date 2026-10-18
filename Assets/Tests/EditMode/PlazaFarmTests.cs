using System.Collections.Generic;
using NUnit.Framework;

/// <summary>광장 농장 규칙 (PlazaFarm): 놓인 칸 맞추기 · 심기 · 자람(칸마다 속도) · 수확 · 상자 넣기 · 거두기(가방이 차면 남음)</summary>
public class PlazaFarmTests
{
    private PlazaFarm _farm;

    [SetUp]
    public void SetUp()
    {
        _farm = new PlazaFarm(new PlazaFarmSaveData());
        _farm.Sync(new[] { 1, 2 }, new[] { 10 });
    }

    [Test]
    public void Sync_AddsNewPartsAndRemovesGoneOnes()
    {
        _farm.Plant(1, "carrot", 30f);

        bool changed = _farm.Sync(new[] { 1, 3 }, new int[0]);

        Assert.IsTrue(changed);
        Assert.AreEqual("carrot", _farm.Field(1).cropId, "남은 밭은 그대로");
        Assert.IsNull(_farm.Field(2));
        Assert.IsNotNull(_farm.Field(3));
        Assert.AreEqual(0, _farm.Crates.Count);
        Assert.IsFalse(_farm.Sync(new[] { 1, 3 }, new int[0]), "같으면 바뀐 것 없음");
    }

    [Test]
    public void Grow_UsesEachFieldsSpeed()
    {
        _farm.Plant(1, "carrot", 30f);
        _farm.Plant(2, "carrot", 30f);

        int done = _farm.Grow(20f, f => f.instanceId == 1 ? 1.5f : 1f);

        Assert.AreEqual(1, done);
        Assert.IsTrue(_farm.Field(1).ready);
        Assert.IsFalse(_farm.Field(2).ready);
        Assert.AreEqual(10f, _farm.Field(2).remainingSec, 0.001f);
    }

    [Test]
    public void Grow_SkipsEmptyAndReadyFields()
    {
        _farm.Plant(1, "carrot", 0f);

        Assert.IsTrue(_farm.Field(1).ready, "0초 = 바로 다 자람");
        Assert.AreEqual(0, _farm.Grow(100f, _ => 1f));
        Assert.IsTrue(string.IsNullOrEmpty(_farm.Field(2).cropId));
    }

    [Test]
    public void Harvest_OnlyReadyField_EmptiesIt()
    {
        _farm.Plant(1, "carrot", 30f);
        Assert.IsNull(_farm.Harvest(1), "덜 자람");

        _farm.Grow(30f, _ => 1f);

        Assert.AreEqual("carrot", _farm.Harvest(1));
        Assert.IsTrue(string.IsNullOrEmpty(_farm.Field(1).cropId));
        Assert.IsFalse(_farm.Field(1).ready);
        Assert.IsNull(_farm.Harvest(1), "두 번 거두지 않음");
    }

    [Test]
    public void Plant_ClearsWaitingSeed()
    {
        _farm.Field(1).waitingCropId = "potato";

        _farm.Plant(1, "potato", 60f);

        Assert.IsNull(_farm.Field(1).waitingCropId);
    }

    [Test]
    public void Deposit_StopsAtCapacity()
    {
        Assert.AreEqual(3, _farm.Deposit(10, "carrot", 3, 5));
        Assert.AreEqual(2, _farm.Deposit(10, "potato", 4, 5), "남은 자리만큼");
        Assert.AreEqual(0, _farm.Deposit(10, "carrot", 1, 5));
        Assert.AreEqual(5, PlazaFarm.Count(_farm.Crate(10)));
        Assert.AreEqual(0, _farm.FreeSpace(5));
        Assert.AreEqual(0, _farm.Deposit(99, "carrot", 1, 5), "없는 상자");
    }

    [Test]
    public void Collect_LeavesWhatTheBagCannotTake()
    {
        _farm.Deposit(10, "carrot", 4, 20);
        _farm.Deposit(10, "potato", 3, 20);

        var taken = _farm.Collect(10, (id, count) => id == "carrot" ? 2 : count);

        Assert.AreEqual(2, taken.Count);
        Assert.AreEqual(5, Total(taken));
        Assert.AreEqual(2, PlazaFarm.Count(_farm.Crate(10)), "가방에 못 들어간 당근 2개는 상자에");
        Assert.AreEqual(1, _farm.Crate(10).items.Count, "다 꺼낸 묶음은 지움");
    }

    [Test]
    public void SetSoil_NeverBelowZero()
    {
        _farm.SetSoil(1, 2);
        Assert.AreEqual(2, _farm.Field(1).soil);
        _farm.SetSoil(1, -1);
        Assert.AreEqual(0, _farm.Field(1).soil);
    }

    private static int Total(List<FarmStackSaveData> stacks)
    {
        int n = 0;
        foreach (var s in stacks)
            n += s.count;
        return n;
    }
}
