using NUnit.Framework;
using UnityEngine;

/// <summary>성 승격: 행정 해달 뒤 · 성 레벨 · 질서도가 성 기준 이상일 때만, 한 번만 연다 (열린 뒤에는 다시 묻지 않음)</summary>
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
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 100, 20), "행정 해달 전");

        settlement.UnlockDevelopment(DistrictGuideRules.Promotion);
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 79, 20));
        Assert.IsTrue(CastleRules.ShouldOpen(_config, settlement, 80, 20));

        settlement.UnlockDevelopment(CastleRules.Ready);
        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 100, 20), "이미 열림");
    }

    [Test]
    public void ShouldOpen_WaitsForCastleLevel()
    {
        // 집이 모두 중앙(주거)이면 Lv.8에 질서도 80이 되지만, 성은 길드 지부가 갈 상업 지역(Lv.11~12) 뒤인 성 레벨부터
        _config.SetupLevelGates(16, 10);
        var settlement = new Settlement();
        settlement.UnlockDevelopment(DistrictGuideRules.Promotion);

        Assert.IsFalse(CastleRules.ShouldOpen(_config, settlement, 100, 15));
        Assert.IsTrue(CastleRules.ShouldOpen(_config, settlement, 80, 16));
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
