using System;

/// <summary>새 뽑기 한 번의 결과 (가방 · 명부에 넣기 전)</summary>
public readonly struct GachaReward
{
    public readonly GachaRewardKind Kind;
    public readonly int Tier;
    public readonly bool Featured;
    public readonly bool ByPity;
    public readonly int Wood;
    public readonly int Stone;
    public readonly AccessoryDefinition Accessory;
    public readonly SpecialOtterDefinition Otter;
    public readonly int Fragments;

    private GachaReward(GachaRewardKind kind, int tier, bool featured, bool byPity, int wood, int stone,
        AccessoryDefinition accessory, SpecialOtterDefinition otter, int fragments)
    {
        Kind = kind;
        Tier = tier;
        Featured = featured;
        ByPity = byPity;
        Wood = wood;
        Stone = stone;
        Accessory = accessory;
        Otter = otter;
        Fragments = fragments;
    }

    public static GachaReward Materials(int wood, int stone, bool byPity) =>
        new GachaReward(GachaRewardKind.Materials, GachaTable.Common, false, byPity, wood, stone, null, null, 0);

    public static GachaReward ForAccessory(AccessoryDefinition accessory, bool byPity) =>
        new GachaReward(GachaRewardKind.Accessory, accessory.Tier, false, byPity, 0, 0, accessory, null, 0);

    public static GachaReward ForFragments(SpecialOtterDefinition otter, int count, bool featured, bool byPity) =>
        new GachaReward(GachaRewardKind.Fragments, otter.Tier, featured, byPity, 0, 0, null, otter, count);

    public bool IsEpic => Tier >= GachaTable.Epic;
    public bool IsRareOrBetter => Tier >= GachaTable.Rare;
}

/// <summary>
/// 새 뽑기 규칙 (Docs/가챠개편_특수해달조각_장신구_기획.md 2장). 등급 · 천장 · 픽업은 장난감 뽑기(GachaRules)와 같은 틀:
/// - 칸(종류 × 등급)을 확률로 고름. 나올 것이 없는 칸의 몫은 같은 등급의 다른 종류 → 아래 등급 → 위 등급 순으로 옮김
/// - 레어 천장째(10)는 레어 이상(레어 · 에픽 칸 중 확률대로) 확정
/// - 에픽 천장째(상시 80 · 픽업 60)는 에픽 해달 조각(PityFragments개) 확정. 에픽 천장은 에픽 해달 조각을 얻을 때만 다시 셈
/// - 픽업: 해달 조각이 나오면 FeaturedShare만큼 픽업 해달. 픽업 에픽 조각을 놓치면 다음 에픽 조각은 픽업 확정
/// </summary>
public static class GachaRewardRules
{
    /// <summary>모든 칸 (확률을 이 순서로 쌓음)</summary>
    public static readonly (GachaRewardKind kind, int tier)[] Slots =
    {
        (GachaRewardKind.Materials, GachaTable.Common),
        (GachaRewardKind.Accessory, GachaTable.Common),
        (GachaRewardKind.Accessory, GachaTable.Rare),
        (GachaRewardKind.Fragments, GachaTable.Rare),
        (GachaRewardKind.Accessory, GachaTable.Epic),
        (GachaRewardKind.Fragments, GachaTable.Epic),
    };

    public static GachaReward Roll(GachaRewardTable table, GachaPity pity, Random random)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        if (pity == null)
            throw new ArgumentNullException(nameof(pity));
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        pity.SinceEpic++;
        pity.SinceRare++;

        bool byPity = false;
        (GachaRewardKind kind, int tier) slot;
        if (pity.SinceEpic >= table.EpicPity && table.Has(GachaRewardKind.Fragments, GachaTable.Epic))
        {
            slot = (GachaRewardKind.Fragments, GachaTable.Epic);
            byPity = true;
        }
        else
        {
            slot = Pick(table, random.NextDouble(), GachaTable.Common);
            if (slot.tier == GachaTable.Common && pity.SinceRare >= table.RarePity)
            {
                slot = Pick(table, random.NextDouble(), GachaTable.Rare);
                byPity = true;
            }
        }

        slot = Resolve(table, slot.kind, slot.tier);
        if (slot.tier >= GachaTable.Rare)
            pity.SinceRare = 0;

        switch (slot.kind)
        {
            case GachaRewardKind.Materials:
            {
                var bundle = PickBundle(table, random);
                return GachaReward.Materials(bundle.Wood, bundle.Stone, byPity);
            }
            case GachaRewardKind.Accessory:
            {
                var list = table.Accessories(slot.tier);
                return GachaReward.ForAccessory(list[random.Next(list.Count)], byPity);
            }
            default:
            {
                if (slot.tier == GachaTable.Epic)
                    pity.SinceEpic = 0;
                var featuredList = table.Featured(slot.tier);
                var standardList = table.Otters(slot.tier);
                bool featured;
                if (featuredList.Count == 0)
                    featured = false;
                else if (standardList.Count == 0)
                    featured = true;
                else if (slot.tier == GachaTable.Epic && pity.FeaturedGuaranteed)
                    featured = true;
                else
                    featured = random.NextDouble() < table.FeaturedShare;
                if (slot.tier == GachaTable.Epic && featuredList.Count > 0)
                    pity.FeaturedGuaranteed = !featured;

                var list = featured ? featuredList : standardList;
                var otter = list[random.Next(list.Count)];
                var range = slot.tier == GachaTable.Epic ? table.EpicFragments : table.RareFragments;
                int count = byPity && slot.tier == GachaTable.Epic ? table.PityFragments : random.Next(range.min, range.max + 1);
                return GachaReward.ForFragments(otter, count, featured, byPity);
            }
        }
    }

    // minTier 이상 칸 중에서 설정 확률대로 하나 (roll: 0~1)
    private static (GachaRewardKind, int) Pick(GachaRewardTable table, double roll, int minTier)
    {
        double total = 0;
        foreach (var (kind, tier) in Slots)
        {
            if (tier >= minTier)
                total += table.RawChance(kind, tier);
        }
        double target = roll * total, sum = 0;
        (GachaRewardKind, int) last = (GachaRewardKind.Materials, GachaTable.Common);
        foreach (var (kind, tier) in Slots)
        {
            if (tier < minTier)
                continue;
            double chance = table.RawChance(kind, tier);
            if (chance <= 0)
                continue;
            last = (kind, tier);
            sum += chance;
            if (target < sum)
                return last;
        }
        return last;
    }

    /// <summary>나올 것이 있는 칸으로 맞춤: 같은 등급의 다른 종류 → 아래 등급 → 위 등급 (빈 표는 프로그래머 실수)</summary>
    public static (GachaRewardKind kind, int tier) Resolve(GachaRewardTable table, GachaRewardKind kind, int tier)
    {
        if (table.Has(kind, tier))
            return (kind, tier);
        for (int t = tier; t >= GachaTable.Common; t--)
        {
            foreach (var (k, slotTier) in Slots)
            {
                if (slotTier == t && table.Has(k, t))
                    return (k, t);
            }
        }
        for (int t = tier + 1; t <= GachaTable.Epic; t++)
        {
            foreach (var (k, slotTier) in Slots)
            {
                if (slotTier == t && table.Has(k, t))
                    return (k, t);
            }
        }
        throw new InvalidOperationException("뽑을 것이 하나도 없는 배너입니다.");
    }

    /// <summary>천장 없이 한 번 뽑을 때 이 칸이 나올 확률 (빈 칸의 몫까지 합친 값). 모든 칸의 합은 1</summary>
    public static double Chance(GachaRewardTable table, GachaRewardKind kind, int tier)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        double result = 0;
        foreach (var (k, t) in Slots)
        {
            if (Resolve(table, k, t) == (kind, tier))
                result += table.RawChance(k, t);
        }
        return result;
    }

    private static GachaRewardTable.Bundle PickBundle(GachaRewardTable table, Random random)
    {
        int total = 0;
        foreach (var bundle in table.Bundles)
            total += bundle.Weight;
        int target = random.Next(total);
        foreach (var bundle in table.Bundles)
        {
            if (target < bundle.Weight)
                return bundle;
            target -= bundle.Weight;
        }
        return table.Bundles[table.Bundles.Count - 1];
    }
}
