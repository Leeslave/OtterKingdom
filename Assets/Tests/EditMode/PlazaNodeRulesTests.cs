using System;
using NUnit.Framework;

public class PlazaNodeRulesTests
{
    [TestCase(1, 1)]
    [TestCase(2, 1)]
    [TestCase(3, 2)]
    [TestCase(4, 2)]
    [TestCase(5, 3)]
    public void Damage_GrowsEveryTwoPickaxeLevels(int level, int expected)
    {
        Assert.AreEqual(expected, PlazaNodeRules.Damage(level));
        Assert.AreEqual(expected * PlazaNodeRules.CritMultiplier, PlazaNodeRules.CritDamage(level));
    }

    [Test]
    public void Damage_LevelBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlazaNodeRules.Damage(0));
    }

    [Test]
    public void CrackStage_StartsIntact_ThenCracksAfterFirstHit()
    {
        Assert.AreEqual(0, PlazaNodeRules.CrackStage(6, 6, 3), "안 맞은 바위는 멀쩡함");
        Assert.AreEqual(1, PlazaNodeRules.CrackStage(5, 6, 3), "한 번만 맞아도 금이 보임");
        Assert.AreEqual(1, PlazaNodeRules.CrackStage(4, 6, 3));
        Assert.AreEqual(2, PlazaNodeRules.CrackStage(3, 6, 3));
        Assert.AreEqual(2, PlazaNodeRules.CrackStage(1, 6, 3));
        Assert.AreEqual(2, PlazaNodeRules.CrackStage(-2, 6, 3), "넘치게 깎여도 마지막 그림");
    }

    [Test]
    public void CrackStage_SingleSprite_AlwaysZero()
    {
        Assert.AreEqual(0, PlazaNodeRules.CrackStage(1, 6, 1));
    }

    [Test]
    public void ShakeWood_LastShakeGivesBonus()
    {
        Assert.AreEqual(1, PlazaNodeRules.ShakeWood(0, 3, 1, 1));
        Assert.AreEqual(1, PlazaNodeRules.ShakeWood(1, 3, 1, 1));
        Assert.AreEqual(2, PlazaNodeRules.ShakeWood(2, 3, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlazaNodeRules.ShakeWood(3, 3, 1, 1));
    }
}
