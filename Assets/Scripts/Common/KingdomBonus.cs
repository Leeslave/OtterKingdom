using System;

/// <summary>왕국 보유 효과 종류 (특성 건물이 주는 영구 효과). 에셋에 숫자로 저장되므로 순서를 바꾸지 않는다</summary>
public enum KingdomBonusKind
{
    SellPrice = 0,    // 판매가 +%
    ClearingWood = 1, // 영토 개간으로 얻는 목재 +%
    StoneYield = 2,   // 광장 바위·광산에서 얻는 돌 +%
    HarvestYield = 3, // 밭 수확량 +%
}

/// <summary>
/// 왕국 보유 효과를 한 곳에서 묻는 곳 (P4). 각 기능(판매·개간·채굴·수확)은 이것만 보고, 어떤 건물이 효과를 주는지는 모른다.
/// 값은 정착(SettlementManager)이 다 지은 시설 건물에서 합산해 Source로 넣어 준다. 없으면 효과 없음.
/// 꺼 둔 동안의 생산(오프라인)에는 적용하지 않는다
/// </summary>
public static class KingdomBonus
{
    /// <summary>종류별 합계 % (SettlementManager가 정함)</summary>
    public static Func<KingdomBonusKind, int> Source { get; set; }

    public static int Percent(KingdomBonusKind kind)
    {
        int percent = Source != null ? Source(kind) : 0;
        return Math.Max(0, percent);
    }

    /// <summary>판매가 한 개 (반올림, 늘 같은 값이라 화면 표시와 실제 판매가 같다)</summary>
    public static int SellPrice(int price)
    {
        int percent = Percent(KingdomBonusKind.SellPrice);
        if (percent <= 0 || price <= 0)
            return price;
        return (int)Math.Min(int.MaxValue, Math.Round(price * (100 + percent) / 100.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>얻는 개수에 효과를 더함: 정수 부분은 늘 더하고, 남는 소수는 그 확률로 1개 (평균이 정확히 +%)</summary>
    public static int Amount(KingdomBonusKind kind, int amount) => Amount(kind, amount, UnityEngine.Random.value);

    /// <param name="roll">0~1 난수 (테스트용)</param>
    public static int Amount(KingdomBonusKind kind, int amount, float roll)
    {
        int percent = Percent(kind);
        if (percent <= 0 || amount <= 0)
            return amount;
        double exact = amount * (100 + percent) / 100.0;
        int whole = (int)Math.Floor(exact);
        return roll < exact - whole ? whole + 1 : whole;
    }
}
