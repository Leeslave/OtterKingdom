using NUnit.Framework;
using UnityEngine;

public class MiningTickTests
{
    private MiningBalanceData _balance;

    [SetUp]
    public void SetUp()
    {
        _balance = ScriptableObject.CreateInstance<MiningBalanceData>();
        _balance.findIntervalSec = new Vector2(10f, 10f);
        _balance.baseDiamondChance = 0f; // 늘 돌
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_balance);
    }

    [Test]
    public void NotMining_FindsNothing()
    {
        var mining = new MiningService(new SaveData(), _balance);

        Assert.IsNull(mining.Tick(100f));
    }

    [Test]
    public void Mining_FindsAfterInterval()
    {
        var save = new SaveData();
        var mining = new MiningService(save, _balance);
        mining.SetActive(true);

        Assert.IsNull(mining.Tick(9f));
        Assert.AreEqual(_balance.stoneItemId, mining.Tick(1.5f));
        Assert.IsNull(mining.Tick(5f), "다음 캐기까지 다시 10초");
    }

    [Test]
    public void ProgressSurvivesSceneChange()
    {
        var save = new SaveData();
        var inMine = new MiningService(save, _balance);
        inMine.SetActive(true);
        inMine.Tick(7f);

        // 씬을 옮기면 GameManager(와 MiningService)가 새로 생기지만 남은 시간은 세이브에 있음
        var inPlaza = new MiningService(save, _balance);
        Assert.AreEqual(_balance.stoneItemId, inPlaza.Tick(3.5f), "광장에서도 이어서 캠");
    }

    [Test]
    public void Stopping_ThrowsAwayProgress()
    {
        var save = new SaveData();
        var mining = new MiningService(save, _balance);
        mining.SetActive(true);
        mining.Tick(9f);

        mining.SetActive(false);
        mining.SetActive(true);

        Assert.IsNull(mining.Tick(5f), "다시 켜면 처음부터");
    }
}
