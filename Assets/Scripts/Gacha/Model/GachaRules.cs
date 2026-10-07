using System;
using System.Collections.Generic;

/// <summary>천장 기록 하나 (상시 또는 픽업 — 같은 종류의 배너끼리 이어짐)</summary>
public sealed class GachaPity
{
    /// <summary>마지막 에픽 뒤로 뽑은 수</summary>
    public int SinceEpic;

    /// <summary>마지막 레어 이상 뒤로 뽑은 수</summary>
    public int SinceRare;

    /// <summary>픽업 에픽을 놓쳐서(상시 에픽이 나와서) 다음 에픽은 픽업 확정</summary>
    public bool FeaturedGuaranteed;

    public void Set(int sinceEpic, int sinceRare, bool featuredGuaranteed)
    {
        SinceEpic = Math.Max(0, sinceEpic);
        SinceRare = Math.Max(0, sinceRare);
        FeaturedGuaranteed = featuredGuaranteed;
    }
}

/// <summary>한 번 뽑은 결과 (가방에 넣기 전)</summary>
public readonly struct GachaRoll
{
    public readonly ItemDefinition Item;
    public readonly int Tier;
    public readonly bool Featured;
    public readonly bool ByPity;

    public GachaRoll(ItemDefinition item, int tier, bool featured, bool byPity)
    {
        Item = item;
        Tier = tier;
        Featured = featured;
        ByPity = byPity;
    }
}

/// <summary>확률 공개표 한 줄 (천장 없이 한 번 뽑을 때)</summary>
public readonly struct GachaRate
{
    public readonly ItemDefinition Item;
    public readonly int Tier;
    public readonly bool Featured;
    public readonly double Chance;

    public GachaRate(ItemDefinition item, int tier, bool featured, double chance)
    {
        Item = item;
        Tier = tier;
        Featured = featured;
        Chance = chance;
    }
}

/// <summary>
/// 보물 조개 뽑기 규칙 (Docs/장난감_뽑기_기획.md):
/// - 등급: 에픽·레어 확률로 고르고, 나머지는 흔함
/// - 천장: 에픽 천장째(픽업 60 · 상시 80)는 에픽 확정, 레어 천장째(10)는 레어 이상 확정 → 10회 뽑기에 레어 이상 하나
/// - 픽업: 에픽·레어가 나오면 그 절반은 픽업 장난감. 픽업 에픽을 놓치면 다음 에픽은 픽업 확정 (다음 픽업 배너로 이어짐)
/// - 겹친 장난감은 가방에 들어가고(광장에 여러 개 둘 수 있음) 반짝 조각도 덤으로 줌
/// </summary>
public static class GachaRules
{
    /// <summary>겹친 장난감이 주는 반짝 조각 (흔함 · 레어 · 에픽)</summary>
    public static readonly int[] DuplicateShards = { 1, 5, 30 };

    /// <summary>교환소에서 고를 때 드는 반짝 조각 (흔함은 교환소에 없음)</summary>
    public const int RareExchangeShards = 60;
    public const int EpicExchangeShards = 300;

    /// <summary>지난 픽업 장난감 (기간이 끝난 배너의 픽업만 교환소에 나옴)</summary>
    public const int PastFeaturedExchangeShards = 600;

    /// <summary>
    /// 한 번 뽑는다: 천장 기록을 갱신하고 결과를 돌려준다. 같은 난수·같은 기록이면 늘 같은 결과
    /// </summary>
    public static GachaRoll Roll(GachaTable table, GachaPity pity, Random random)
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
        int tier;
        if (pity.SinceEpic >= table.EpicPity)
        {
            tier = GachaTable.Epic;
            byPity = true;
        }
        else
        {
            double r = random.NextDouble();
            tier = r < table.EpicRate ? GachaTable.Epic
                : r < table.EpicRate + table.RareRate ? GachaTable.Rare
                : GachaTable.Common;
            if (tier == GachaTable.Common && pity.SinceRare >= table.RarePity)
            {
                tier = GachaTable.Rare;
                byPity = true;
            }
        }

        tier = table.AvailableTier(tier);
        if (tier >= GachaTable.Rare)
            pity.SinceRare = 0;
        if (tier == GachaTable.Epic)
            pity.SinceEpic = 0;

        var featuredList = table.Featured(tier);
        var standardList = table.Standard(tier);
        bool featured;
        if (featuredList.Count == 0)
            featured = false;
        else if (standardList.Count == 0)
            featured = true;
        else if (tier == GachaTable.Epic && pity.FeaturedGuaranteed)
            featured = true;
        else
            featured = random.NextDouble() < table.FeaturedShare;

        // 픽업 에픽이 있는 표에서만: 놓치면 다음 에픽은 픽업 확정, 얻으면 풀림
        if (tier == GachaTable.Epic && featuredList.Count > 0)
            pity.FeaturedGuaranteed = !featured;

        var list = featured ? featuredList : standardList;
        return new GachaRoll(list[random.Next(list.Count)], tier, featured, byPity);
    }

    /// <summary>확률 공개표 (등급 높은 순 → 픽업 먼저 → 표 순서). 합은 1</summary>
    public static List<GachaRate> Rates(GachaTable table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));

        var result = new List<GachaRate>();
        for (int tier = GachaTable.Epic; tier >= GachaTable.Common; tier--)
        {
            double chance = table.TierChance(tier);
            if (chance <= 0)
                continue;

            var featured = table.Featured(tier);
            var standard = table.Standard(tier);
            double featuredPart = featured.Count == 0 ? 0 : standard.Count == 0 ? chance : chance * table.FeaturedShare;
            double standardPart = chance - featuredPart;
            foreach (var item in featured)
                result.Add(new GachaRate(item, tier, true, featuredPart / featured.Count));
            foreach (var item in standard)
                result.Add(new GachaRate(item, tier, false, standardPart / standard.Count));
        }
        return result;
    }

    /// <summary>에픽 확정까지 남은 수 (다음 뽑기가 1번째)</summary>
    public static int EpicPityLeft(GachaTable table, GachaPity pity) => Math.Max(1, table.EpicPity - pity.SinceEpic);

    /// <summary>골드 뽑기 값 = 기본 + 왕국 레벨 × 레벨당</summary>
    public static int GoldPullCost(int baseCost, int perLevel, int kingdomLevel) =>
        Math.Max(0, baseCost) + Math.Max(0, perLevel) * Math.Max(1, kingdomLevel);

    /// <summary>겹친 장난감의 반짝 조각</summary>
    public static int ShardsFor(int tier) => DuplicateShards[Math.Max(0, Math.Min(DuplicateShards.Length - 1, tier))];

    /// <summary>교환소 값 (흔함은 0 = 교환소에 없음)</summary>
    public static int ExchangeCost(int tier, bool pastFeatured)
    {
        if (pastFeatured)
            return PastFeaturedExchangeShards;
        return tier >= GachaTable.Epic ? EpicExchangeShards : tier == GachaTable.Rare ? RareExchangeShards : 0;
    }
}
