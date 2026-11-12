using System;
using System.Collections.Generic;

/// <summary>구역 효과 한 줄 (왕국 화면 [구역] 탭)</summary>
public readonly struct DistrictEffectLine
{
    public DistrictType Type { get; }
    public KingdomBonusKind Kind { get; }
    /// <summary>그 용도의 구역이 있는지 (없으면 보너스 0)</summary>
    public bool HasDistrict { get; }
    /// <summary>맞는 구역에 들어선 건물 수</summary>
    public int Matching { get; }
    public int Percent { get; }
    /// <summary>최대 % (0이면 제한 없음)</summary>
    public int MaxPercent { get; }
    /// <summary>건물마다 % (0이면 구역만 있으면 기본 %)</summary>
    public int PerBuilding { get; }

    public DistrictEffectLine(DistrictType type, KingdomBonusKind kind, bool hasDistrict, int matching, int percent, int maxPercent, int perBuilding)
    {
        Type = type;
        Kind = kind;
        HasDistrict = hasDistrict;
        Matching = matching;
        Percent = percent;
        MaxPercent = maxPercent;
        PerBuilding = perBuilding;
    }
}

/// <summary>용도를 바꾸면 달라지는 것 (확인 창에 그대로 보여 줌)</summary>
public class DistrictChangePreview
{
    public int OrderBefore;
    public int OrderAfter;
    /// <summary>맞는 구역에 있다가 이 변경으로 맞지 않게 되는 건물 수</summary>
    public int Lost;
    /// <summary>이 변경으로 맞는 구역이 되는 건물 수</summary>
    public int Gained;
    public readonly List<(KingdomBonusKind kind, int before, int after)> Bonuses = new List<(KingdomBonusKind, int, int)>();
}

/// <summary>
/// 구역 효과를 글로 (왕국 화면 · 용도 고르기 · 꾸미기 미리보기). 계산은 DistrictRules 그대로, 여기는 묶고 이름만 붙인다. 순수 C#.
/// </summary>
public static class DistrictEffects
{
    /// <summary>효과 이름: "판매가" · "밭 자라는 시간"</summary>
    public static string Name(KingdomBonusKind kind)
    {
        switch (kind)
        {
            case KingdomBonusKind.SellPrice: return "판매가";
            case KingdomBonusKind.ClearingWood: return "개간 목재";
            case KingdomBonusKind.StoneYield: return "돌";
            case KingdomBonusKind.HarvestYield: return "수확량";
            case KingdomBonusKind.FishYield: return "물고기";
            case KingdomBonusKind.BuildSpeed: return "공사 시간";
            case KingdomBonusKind.VisitSpeed: return "해달 방문 속도";
            case KingdomBonusKind.ProductionSpeed: return "생산 속도";
            case KingdomBonusKind.GrowSpeed: return "밭 자라는 시간";
            case KingdomBonusKind.MoveSpeed: return "이동 속도";
            case KingdomBonusKind.WoodYield: return "목재";
            default: return "보너스";
        }
    }

    /// <summary>"판매가 +5%" · "밭 자라는 시간 -10%"</summary>
    public static string Label(KingdomBonusKind kind, int percent) => $"{Name(kind)} {Amount(kind, percent)}";

    /// <summary>"+20%" · "-10%" · "0%" (줄이는 효과는 -)</summary>
    public static string Amount(KingdomBonusKind kind, int percent) =>
        percent == 0 ? "0%" : (kind == KingdomBonusKind.BuildSpeed || kind == KingdomBonusKind.GrowSpeed ? "-" : "+") + percent + "%";

    /// <summary>용도를 고를 때 한 줄: "건물마다 판매가 +5% (최대 10%)" · "밭 자라는 시간 -10%"</summary>
    public static string Describe(SettlementConfig config, DistrictType type)
    {
        var parts = new List<string>();
        foreach (var entry in config.DistrictBonuses)
        {
            if (entry == null || entry.District != type)
                continue;
            if (entry.PerBuildingPercent > 0)
                parts.Add($"맞는 건물마다 {Label(entry.Kind, entry.PerBuildingPercent)}" + (entry.MaxPercent > 0 ? $" (최대 {entry.MaxPercent}%)" : ""));
            else if (entry.FlatPercent > 0)
                parts.Add(Label(entry.Kind, entry.FlatPercent));
        }
        return string.Join(" · ", parts);
    }

    /// <summary>용도마다 지금 효과 (설정 표 순서)</summary>
    public static List<DistrictEffectLine> Lines(SettlementConfig config, Settlement settlement, DistrictOrder order)
    {
        var lines = new List<DistrictEffectLine>();
        foreach (var entry in config.DistrictBonuses)
        {
            if (entry == null)
                continue;
            bool has = DistrictRules.HasDistrict(config, settlement, entry.District);
            int matching = order != null ? order.MatchingOf(entry.District) : 0;
            int percent = has ? DistrictRules.EntryPercent(entry, matching) : 0;
            lines.Add(new DistrictEffectLine(entry.District, entry.Kind, has, matching, percent, entry.MaxPercent, entry.PerBuildingPercent));
        }
        return lines;
    }

    /// <summary>
    /// 새 특성 건물 하나가 맞는 구역에 들어서면: "생산 속도 +15% → +20%" (이미 최대면 "생산 속도 +20% (최대)", 효과가 없으면 null)
    /// </summary>
    public static string NextBuildingText(SettlementConfig config, Settlement settlement, DistrictOrder order, DistrictType type)
    {
        var parts = new List<string>();
        foreach (var entry in config.DistrictBonuses)
        {
            if (entry == null || entry.District != type || entry.PerBuildingPercent <= 0)
                continue;
            int matching = order != null ? order.MatchingOf(type) : 0;
            int now = DistrictRules.EntryPercent(entry, matching);
            int next = DistrictRules.EntryPercent(entry, matching + 1);
            parts.Add(next > now ? $"{Name(entry.Kind)} {Amount(entry.Kind, now)} → {Amount(entry.Kind, next)}" : $"{Label(entry.Kind, now)} (최대)");
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    /// <summary>이 블록을 이 용도로 바꾸면 질서도 · 보너스가 어떻게 되는지 (세이브는 안 바꿈)</summary>
    public static DistrictChangePreview Preview(SettlementConfig config, Settlement settlement, IReadOnlyList<DistrictPlacement> placements,
        DistrictBlockDefinition block, DistrictType type)
    {
        if (block == null)
            throw new ArgumentNullException(nameof(block));
        var change = new DistrictOverride(block.BlockId, type);
        var before = DistrictRules.Evaluate(config, settlement, placements);
        var after = DistrictRules.Evaluate(config, settlement, placements, change);
        var old = DistrictRules.TypeOf(block, settlement);
        var preview = new DistrictChangePreview
        {
            OrderBefore = before.Score,
            OrderAfter = after.Score,
            Lost = old != DistrictType.None && old != type ? Math.Max(0, before.MatchingOf(old) - after.MatchingOf(old)) : 0,
            Gained = Math.Max(0, after.MatchingOf(type) - before.MatchingOf(type)),
        };
        var kinds = new List<KingdomBonusKind>();
        foreach (var entry in config.DistrictBonuses)
        {
            if (entry != null && !kinds.Contains(entry.Kind))
                kinds.Add(entry.Kind);
        }
        foreach (var kind in kinds)
        {
            int a = DistrictRules.BonusPercent(config, settlement, before, kind);
            int b = DistrictRules.BonusPercent(config, settlement, after, kind, change);
            if (a != b)
                preview.Bonuses.Add((kind, a, b));
        }
        return preview;
    }

    /// <summary>확인 창 줄들: "· 맞지 않게 되는 건물 2채 (이사로 옮길 수 있어요)" …</summary>
    public static List<string> PreviewLines(DistrictChangePreview preview)
    {
        var lines = new List<string>();
        if (preview.Lost > 0)
            lines.Add($"· 맞지 않게 되는 건물 {preview.Lost}채 (이사로 옮길 수 있어요)");
        if (preview.Gained > 0)
            lines.Add($"· 맞는 구역이 되는 건물 {preview.Gained}채");
        foreach (var (kind, before, after) in preview.Bonuses)
            lines.Add($"· {Name(kind)} {Amount(kind, before)} → {Amount(kind, after)}");
        if (preview.OrderBefore != preview.OrderAfter)
            lines.Add($"· 왕국 질서도 {preview.OrderBefore} → {preview.OrderAfter}");
        return lines;
    }
}
