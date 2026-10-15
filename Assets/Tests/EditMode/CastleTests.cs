using NUnit.Framework;
using UnityEngine;

/// <summary>성 승격: 행정 해달 뒤 · 질서도가 성 기준 이상일 때만, 한 번만 연다 (열린 뒤에는 다시 묻지 않음)</summary>
public class CastleTests
{
    private SettlementConfig _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<SettlementConfig>();
        _config.SetupDistricts(null, DistrictType.Residential, 60, 10, 10, 80, null);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    [Test]
    public void ShouldOpen_NeedsAdministratorAndScore_Once()
    {
        var settlement = new Settlement();
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 100), "행정 해달 전");

        settlement.UnlockDevelopment(DistrictGuideRules.Promotion);
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 79));
        Assert.IsTrue(CastleRules.ShouldOpen(_config, settlement, 80));

        settlement.UnlockDevelopment(CastleRules.Ready);
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 100), "이미 열림");
    }

    [Test]
    public void DevForget_RemovesDevelopmentAndRequest()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment(CastleRules.Ready);
        settlement.CompleteRequest(CastleRules.RequestId, CastleRules.Built);

        settlement.DevForget(CastleRules.Built, CastleRules.RequestId);
        settlement.DevForget(CastleRules.Ready, null);

        Assert.IsFalse(settlement.HasDevelopment(CastleRules.Built));
        Assert.IsFalse(settlement.HasDevelopment(CastleRules.Ready));
        Assert.IsFalse(settlement.IsCompleted(CastleRules.RequestId));
    }
}
