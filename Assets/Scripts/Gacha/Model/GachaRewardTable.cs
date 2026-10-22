using System;
using System.Collections.Generic;

/// <summary>새 뽑기에서 나오는 것</summary>
public enum GachaRewardKind
{
    Materials, // 목재 · 돌 꾸러미 (흔함)
    Accessory, // 장신구 (일반 · 레어 · 에픽)
    Fragments, // 특수 해달 조각 (레어 · 에픽)
}

/// <summary>뽑기 한 번의 칸별 확률 (합 1). 장신구는 등급마다, 해달 조각은 레어 · 에픽</summary>
public readonly struct GachaRewardRates
{
    public readonly double Materials;
    public readonly double CommonAccessory;
    public readonly double RareAccessory;
    public readonly double EpicAccessory;
    public readonly double RareFragments;
    public readonly double EpicFragments;

    public GachaRewardRates(double materials, double commonAccessory, double rareAccessory, double epicAccessory,
        double rareFragments, double epicFragments)
    {
        double sum = materials + commonAccessory + rareAccessory + epicAccessory + rareFragments + epicFragments;
        if (materials < 0 || commonAccessory < 0 || rareAccessory < 0 || epicAccessory < 0 || rareFragments < 0 || epicFragments < 0
            || Math.Abs(sum - 1.0) > 1e-4)
            throw new ArgumentOutOfRangeException(nameof(materials), $"뽑기 확률은 0 이상, 합이 1이어야 합니다 (지금 {sum}).");
        Materials = materials;
        CommonAccessory = commonAccessory;
        RareAccessory = rareAccessory;
        EpicAccessory = epicAccessory;
        RareFragments = rareFragments;
        EpicFragments = epicFragments;
    }
}

/// <summary>
/// 배너 하나의 새 보상 표 (ScriptableObject 없이 테스트하려고 GachaRewardConfig에서 따로 만듦).
/// 칸(종류 × 등급): 재료(흔함) · 장신구(흔함 · 레어 · 에픽) · 해달 조각(레어 · 에픽)
/// </summary>
public sealed class GachaRewardTable
{
    public readonly struct Bundle
    {
        public readonly int Wood;
        public readonly int Stone;
        public readonly int Weight;

        public Bundle(int wood, int stone, int weight)
        {
            Wood = wood;
            Stone = stone;
            Weight = weight;
        }
    }

    private readonly List<Bundle> _bundles = new List<Bundle>();
    private readonly List<AccessoryDefinition>[] _accessories = { new List<AccessoryDefinition>(), new List<AccessoryDefinition>(), new List<AccessoryDefinition>() };
    private readonly List<SpecialOtterDefinition>[] _otters = { new List<SpecialOtterDefinition>(), new List<SpecialOtterDefinition>(), new List<SpecialOtterDefinition>() };
    private readonly List<SpecialOtterDefinition>[] _featured = { new List<SpecialOtterDefinition>(), new List<SpecialOtterDefinition>(), new List<SpecialOtterDefinition>() };

    public GachaBannerKind Kind { get; }
    public GachaRewardRates Rates { get; }
    public int EpicPity { get; }
    public int RarePity { get; }
    public float FeaturedShare { get; }
    public (int min, int max) RareFragments { get; }
    public (int min, int max) EpicFragments { get; }
    public int PityFragments { get; }

    public GachaRewardTable(GachaBannerKind kind, GachaRewardRates rates, int epicPity, int rarePity, float featuredShare,
        (int min, int max) rareFragments, (int min, int max) epicFragments, int pityFragments)
    {
        if (epicPity < 1 || rarePity < 1)
            throw new ArgumentOutOfRangeException(nameof(epicPity), "천장은 1 이상이어야 합니다.");
        Kind = kind;
        Rates = rates;
        EpicPity = epicPity;
        RarePity = rarePity;
        FeaturedShare = Math.Max(0f, Math.Min(1f, featuredShare));
        RareFragments = Range(rareFragments);
        EpicFragments = Range(epicFragments);
        PityFragments = Math.Max(1, pityFragments);
    }

    private static (int, int) Range((int min, int max) range)
    {
        int min = Math.Max(1, range.min);
        return (min, Math.Max(min, range.max));
    }

    public void AddBundle(int wood, int stone, int weight)
    {
        if (wood <= 0 && stone <= 0)
            throw new ArgumentException("빈 재료 꾸러미입니다.", nameof(wood));
        _bundles.Add(new Bundle(Math.Max(0, wood), Math.Max(0, stone), Math.Max(1, weight)));
    }

    public void AddAccessory(AccessoryDefinition accessory)
    {
        if (accessory == null)
            throw new ArgumentNullException(nameof(accessory));
        var list = _accessories[accessory.Tier];
        if (!list.Contains(accessory))
            list.Add(accessory);
    }

    public void AddOtter(SpecialOtterDefinition otter, bool featured)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        var list = featured ? _featured[otter.Tier] : _otters[otter.Tier];
        if (!list.Contains(otter))
            list.Add(otter);
    }

    public IReadOnlyList<Bundle> Bundles => _bundles;
    public IReadOnlyList<AccessoryDefinition> Accessories(int tier) => _accessories[tier];
    public IReadOnlyList<SpecialOtterDefinition> Otters(int tier) => _otters[tier];
    public IReadOnlyList<SpecialOtterDefinition> Featured(int tier) => _featured[tier];

    /// <summary>이 칸에서 나올 것이 있는지 (재료는 흔함만, 해달 조각은 레어 · 에픽만)</summary>
    public bool Has(GachaRewardKind kind, int tier)
    {
        switch (kind)
        {
            case GachaRewardKind.Materials: return tier == GachaTable.Common && _bundles.Count > 0;
            case GachaRewardKind.Accessory: return tier >= GachaTable.Common && tier <= GachaTable.Epic && _accessories[tier].Count > 0;
            default: return tier >= GachaTable.Rare && tier <= GachaTable.Epic && (_otters[tier].Count > 0 || _featured[tier].Count > 0);
        }
    }

    /// <summary>이 칸의 설정 확률 (나올 것이 없어도 그대로 — 빈 칸 몫은 GachaRewardRules가 옮김)</summary>
    public double RawChance(GachaRewardKind kind, int tier)
    {
        switch (kind)
        {
            case GachaRewardKind.Materials: return tier == GachaTable.Common ? Rates.Materials : 0;
            case GachaRewardKind.Accessory:
                return tier == GachaTable.Common ? Rates.CommonAccessory : tier == GachaTable.Rare ? Rates.RareAccessory : Rates.EpicAccessory;
            default: return tier == GachaTable.Rare ? Rates.RareFragments : tier == GachaTable.Epic ? Rates.EpicFragments : 0;
        }
    }

    public bool IsEmpty
    {
        get
        {
            foreach (var (kind, tier) in GachaRewardRules.Slots)
            {
                if (Has(kind, tier))
                    return false;
            }
            return true;
        }
    }
}
