/// <summary>새 뽑기 보상 문구 (결과 카드 · 확률 공개 · 교환소가 같이 씀)</summary>
public static class GachaRewardText
{
    private static readonly string[] AccessoryTiers = { "일반", "레어", "에픽" };
    private static readonly string[] OtterTiers = { "흔함", "레어", "에픽" };

    /// <summary>등급 이름: 장신구 일반 · 레어 · 에픽, 해달 조각 레어 · 에픽, 재료는 "재료"</summary>
    public static string TierName(GachaRewardKind kind, int tier)
    {
        int t = tier < 0 ? 0 : tier > 2 ? 2 : tier;
        return kind == GachaRewardKind.Materials ? "재료" : kind == GachaRewardKind.Accessory ? AccessoryTiers[t] : OtterTiers[t];
    }

    /// <summary>편성 버프 한 줄 ("주민 이동속도 +4%")</summary>
    public static string Bonus(KingdomBonusKind kind, int percent)
    {
        switch (kind)
        {
            case KingdomBonusKind.MoveSpeed: return $"주민 이동속도 +{percent}%";
            case KingdomBonusKind.GrowSpeed: return $"작물 성장 시간 −{percent}%";
            case KingdomBonusKind.WoodYield: return $"목재 +{percent}%";
            case KingdomBonusKind.StoneYield: return $"돌 +{percent}%";
            case KingdomBonusKind.SellPrice: return $"판매가 +{percent}%";
            case KingdomBonusKind.BuildSpeed: return $"공사 시간 −{percent}%";
            case KingdomBonusKind.HarvestYield: return $"수확량 +{percent}%";
            case KingdomBonusKind.FishYield: return $"물고기 +{percent}%";
            case KingdomBonusKind.ProductionSpeed: return $"생산 속도 +{percent}%";
            default: return $"+{percent}%";
        }
    }

    /// <summary>장신구 효과 한 줄 ("이동속도 +8%")</summary>
    public static string Effect(AccessoryEffect effect, int percent)
    {
        switch (effect)
        {
            case AccessoryEffect.MoveSpeed: return $"이동속도 +{percent}%";
            case AccessoryEffect.WorkTime: return $"작업 시간 −{percent}%";
            default: return $"얻는 양 +{percent}%";
        }
    }

    public static string Materials(int wood, int stone) =>
        wood > 0 && stone > 0 ? $"목재 {wood} · 돌 {stone}" : wood > 0 ? $"목재 {wood}" : $"돌 {stone}";

    public static string FragmentName(SpecialOtterDefinition otter) => otter != null ? $"{otter.DisplayName} 조각" : "해달 조각";
}
