using NUnit.Framework;
using UnityEditor;

/// <summary>본 게임 연결: 오래된 갱도 발견 조건 (MineMazeRules) · 본 게임 왕국 레벨로 층 돌파 조건</summary>
public partial class MineMazeTests
{
    private const string MainMazeConfigPath = "Assets/Scriptable Obejects/MineMaze/MineMazeConfig.asset";

    [TestCase(9, 1)]
    [TestCase(13, 2)]
    [TestCase(16, 3)]
    [TestCase(18, 4)]
    [TestCase(20, 5)]
    [TestCase(21, 0)]
    public void MainGameFloorCap_UsesAuthoredGateLevelAndKeepsPastLevels(int level, int floor)
    {
        var config = AssetDatabase.LoadAssetAtPath<MineMazeConfig>(MainMazeConfigPath);
        Assert.IsNotNull(config);
        var settlement = new Settlement();
        Assert.AreEqual(floor, MineMazeRules.FindBlockingFloor(config, settlement, level));
        if (floor == 0)
            return;

        settlement.UnlockDevelopment(MineMazeRules.FloorClearedDevelopment(floor));
        int next = floor < config.FloorCount ? floor + 1 : 0;
        Assert.AreEqual(next, MineMazeRules.FindBlockingFloor(config, settlement, level));
    }

    [Test]
    public void MainGameFloorCap_RestoresLegacyAndMultiFloorClears()
    {
        var config = AssetDatabase.LoadAssetAtPath<MineMazeConfig>(MainMazeConfigPath);
        Assert.IsNotNull(config);
        var settlement = new Settlement();
        var save = new MineMazeSave { savedUtcTicks = 1, floorCleared = true };
        MineMazeRules.RestoreClearedFloors(config, save, settlement);
        Assert.AreEqual(2, MineMazeRules.FindBlockingFloor(config, settlement, 9));

        save.floors.Add(new MazeFloorSave { floor = 1, gateDamage = config.FloorConfig(1).GateHits });
        save.floors.Add(new MazeFloorSave { floor = 2, gateDamage = 1 });
        MineMazeRules.RestoreClearedFloors(config, save, settlement);
        Assert.AreEqual(3, MineMazeRules.FindBlockingFloor(config, settlement, 13));
    }

    // (이미 찾음, 왕국 레벨, 곡괭이 레벨, 강화 레벨, 채굴 중, 광부 배치, 게시판 해달 배치)
    [TestCase(false, 5, 2, 2, true, true, true, ExpectedResult = true, TestName = "Discover_AllConditions")]
    [TestCase(false, 9, 5, 2, true, true, true, ExpectedResult = true, TestName = "Discover_LaterLevelStillFinds")]
    [TestCase(true, 5, 2, 2, true, true, true, ExpectedResult = false, TestName = "Discover_OnlyOnce")]
    [TestCase(false, 4, 2, 2, true, true, true, ExpectedResult = false, TestName = "Discover_NeedsLevel5")]
    [TestCase(false, 5, 1, 2, true, true, true, ExpectedResult = false, TestName = "Discover_NeedsPickaxeUpgrade")]
    [TestCase(false, 5, 2, 2, false, true, true, ExpectedResult = false, TestName = "Discover_NeedsMiningOn")]
    [TestCase(false, 5, 2, 2, true, false, true, ExpectedResult = false, TestName = "Discover_NeedsMinerWorking")]
    [TestCase(false, 5, 2, 2, true, true, false, ExpectedResult = false, TestName = "Discover_WaitsForBoardOtter")]
    [TestCase(false, 5, 2, 1, true, true, true, ExpectedResult = true, TestName = "Discover_UpgradeMeansAtLeastLevel2")]
    [TestCase(false, 5, 1, 1, true, true, true, ExpectedResult = false, TestName = "Discover_UnupgradedPickaxeNeverCounts")]
    public bool Discover(bool found, int level, int pickaxe, int upgraded, bool mining, bool working, bool boardOtter) =>
        MineMazeRules.ShouldDiscover(found, level, pickaxe, upgraded, mining, working, boardOtter);

    [Test]
    public void MainGameKingdomLevel_ReplacesTestLevelForGate()
    {
        var save = Stock(0);
        save.pickaxeTier = 1;
        save.kingdomLevel = 8;
        var model = Model(save: save);
        model.UseKingdomLevel(9);
        Assert.AreEqual(9, model.KingdomLevel);
        WalkToStairs(model);
        Assert.IsTrue(model.HitGate(out _));

        model = Model(save: save);
        model.UseKingdomLevel(5);
        WalkToStairs(model);
        Assert.IsFalse(model.HitGate(out string reason));
        StringAssert.Contains("왕국 Lv.9", reason);
    }
}
