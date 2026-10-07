using System;
using System.Collections.Generic;

/// <summary>
/// 배너 하나의 뽑기 표: 등급별 상시·픽업 장난감과 확률·천장 (ScriptableObject 없이 테스트하려고 배너에서 따로 만듦).
/// 등급: 0 흔함 · 1 레어 · 2 에픽 (ItemRarity.Tier)
/// </summary>
public sealed class GachaTable
{
    public const int Common = 0;
    public const int Rare = 1;
    public const int Epic = 2;
    public const int TierCount = 3;

    private readonly List<ItemDefinition>[] _standard = { new List<ItemDefinition>(), new List<ItemDefinition>(), new List<ItemDefinition>() };
    private readonly List<ItemDefinition>[] _featured = { new List<ItemDefinition>(), new List<ItemDefinition>(), new List<ItemDefinition>() };

    public GachaBannerKind Kind { get; }
    public float EpicRate { get; }
    public float RareRate { get; }
    public float FeaturedShare { get; }
    public int EpicPity { get; }
    public int RarePity { get; }

    public GachaTable(GachaBannerKind kind, float epicRate, float rareRate, float featuredShare, int epicPity, int rarePity)
    {
        if (epicRate < 0f || rareRate < 0f || epicRate + rareRate > 1f)
            throw new ArgumentOutOfRangeException(nameof(epicRate), "에픽·레어 확률은 0 이상, 합이 1 이하여야 합니다.");
        if (epicPity < 1 || rarePity < 1)
            throw new ArgumentOutOfRangeException(nameof(epicPity), "천장은 1 이상이어야 합니다.");

        Kind = kind;
        EpicRate = epicRate;
        RareRate = rareRate;
        FeaturedShare = Math.Max(0f, Math.Min(1f, featuredShare));
        EpicPity = epicPity;
        RarePity = rarePity;
    }

    /// <summary>배너 데이터로 표를 만든다 (픽업 목록에도 있는 장난감은 픽업으로만 넣음)</summary>
    public static GachaTable From(GachaBannerDefinition banner)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));

        var table = new GachaTable(banner.Kind, banner.EpicRate, banner.RareRate, banner.FeaturedShare, banner.EpicPity, banner.RarePity);
        foreach (var item in banner.Featured)
        {
            if (item != null)
                table.Add(item, TierOf(item), true);
        }
        foreach (var item in banner.Pool)
        {
            if (item != null && !banner.IsFeatured(item))
                table.Add(item, TierOf(item), false);
        }
        return table;
    }

    /// <summary>아이템의 뽑기 등급 (희귀도가 없으면 흔함, 에픽보다 높으면 에픽)</summary>
    public static int TierOf(ItemDefinition item)
    {
        var rarity = item != null ? item.Rarity : null;
        return rarity == null ? Common : Math.Max(Common, Math.Min(Epic, rarity.Tier));
    }

    public void Add(ItemDefinition item, int tier, bool featured)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (tier < Common || tier > Epic)
            throw new ArgumentOutOfRangeException(nameof(tier));

        var list = featured ? _featured[tier] : _standard[tier];
        if (!list.Contains(item))
            list.Add(item);
    }

    public IReadOnlyList<ItemDefinition> Standard(int tier) => _standard[tier];
    public IReadOnlyList<ItemDefinition> Featured(int tier) => _featured[tier];

    /// <summary>이 등급에 나올 장난감이 하나라도 있는지</summary>
    public bool HasTier(int tier) => _standard[tier].Count > 0 || _featured[tier].Count > 0;

    public bool IsEmpty => !HasTier(Common) && !HasTier(Rare) && !HasTier(Epic);

    /// <summary>
    /// 장난감이 있는 등급으로 맞춤: 비어 있으면 한 단계씩 내려가며 찾고, 아래에도 없으면 위로 (빈 표는 프로그래머 실수)
    /// </summary>
    public int AvailableTier(int tier)
    {
        for (int t = tier; t >= Common; t--)
        {
            if (HasTier(t))
                return t;
        }
        for (int t = tier + 1; t <= Epic; t++)
        {
            if (HasTier(t))
                return t;
        }
        throw new InvalidOperationException("뽑을 장난감이 하나도 없는 배너입니다.");
    }

    /// <summary>천장 없이 한 번 뽑을 때 이 등급이 나올 확률 (빈 등급의 몫은 아래 등급이 가져감)</summary>
    public double TierChance(int tier)
    {
        double epic = EpicRate, rare = RareRate;
        double common = Math.Max(0.0, 1.0 - epic - rare);
        double[] raw = { common, rare, epic };
        double result = 0;
        for (int t = Common; t <= Epic; t++)
        {
            if (raw[t] > 0 && AvailableTier(t) == tier)
                result += raw[t];
        }
        return result;
    }
}
