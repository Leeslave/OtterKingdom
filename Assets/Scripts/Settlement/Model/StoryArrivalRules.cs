using System;

/// <summary>
/// 장난감 방문을 대신하는 확정 등장 (가챠 개편 4단계, Docs/장난감삭제_해달가챠_응원동료_기획안.md 6장).
/// 장난감으로만 오던 해달은 생활 진행 기록(발전)이 열리면 한 번 찾아와 방문객이 된다 — 빈 집이 생기면 예전처럼 광장에서 말을 걸어 입주.
/// 여러 조건이 같이 열리면 표 순서대로 한 번에 한 마리. 이미 온 적 있는 해달(주민 · 방문 기록)은 다시 부르지 않는다
/// </summary>
public static class StoryArrivalRules
{
    /// <summary>(해달 ID, 찾아오는 조건 발전, 오는 이유 한 줄)</summary>
    public static readonly (string otterId, string development, string reason)[] Arrivals =
    {
        ("otter_toy_duribeon", TerritoryRules.FirstDevelopment, "넓어진 땅을 구경하러 왔어요"),
        ("otter_toy_sallang", "first_gathering_complete", "첫 모임 소식을 듣고 봄바람을 타고 왔어요"),
        ("otter_toy_yeongcha", FarmCrateCollectedDevelopment, "수확 상자를 나르는 모습에 반해서 왔어요"),
        ("otter_toy_jaejal", "p4_shop_built", "새 가게 소문을 듣고 수다 떨러 왔어요"),
        ("otter_toy_kkomkkom", SettlementManager.FarmSprinklerPlacedDevelopment, "스프링클러를 구경하러 왔어요"),
    };

    /// <summary>광장 농장 수확 상자에서 처음 거둠 (SettlementManager.Farm이 남김)</summary>
    public const string FarmCrateCollectedDevelopment = "farm_crate_collected";

    /// <summary>지금 찾아올 해달 (없으면 null): 조건이 열렸고, 데이터에 있고, 아직 한 번도 온 적 없는 첫 해달</summary>
    public static SettlementOtterDefinition Next(SettlementConfig config, Settlement settlement)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        foreach (var (otterId, development, _) in Arrivals)
        {
            if (!settlement.HasDevelopment(development) || settlement.TryGetResidentState(otterId, out _))
                continue;
            var otter = config.FindOtter(otterId);
            if (otter != null)
                return otter;
        }
        return null;
    }

    /// <summary>찾아오게 한다 (방문객 + 첫 방문 방명록). 찾아온 해달 (없으면 null)</summary>
    public static SettlementOtterDefinition ArriveNext(SettlementConfig config, Settlement settlement)
    {
        var otter = Next(config, settlement);
        if (otter != null)
            SettlementRules.Arrive(otter, ResidentState.Visitor, settlement);
        return otter;
    }

    /// <summary>이 해달이 오는 이유 (표에 없으면 null)</summary>
    public static string ReasonOf(string otterId)
    {
        foreach (var (id, _, reason) in Arrivals)
        {
            if (id == otterId)
                return reason;
        }
        return null;
    }
}
