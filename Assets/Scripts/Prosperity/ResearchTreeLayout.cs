using System;
using System.Collections.Generic;

/// <summary>연구 트리의 분야 탭 (기술 ID 앞 글자: I · X → 산업, L → 물류, C → 도시, E → 경제)</summary>
public enum TechBranch { Industry, Logistics, City, Economy }

public enum TreeColumn { Left, Center, Right }

/// <summary>트리 한 칸. Ghost = 다른 분야의 선행 기술 (흐리게, 누르면 그 분야로)</summary>
public readonly struct TreeSlot
{
    public string TechId { get; }
    public TreeColumn Column { get; }
    public bool Ghost { get; }

    public TreeSlot(string techId, TreeColumn column, bool ghost = false)
    {
        TechId = techId;
        Column = column;
        Ghost = ghost;
    }
}

/// <summary>단계 띠 하나 (레벨대가 비슷한 기술 묶음) — 줄마다 칸 여러 개</summary>
public sealed class TreeBand
{
    public IReadOnlyList<IReadOnlyList<TreeSlot>> Rows { get; }

    public TreeBand(params TreeSlot[][] rows)
    {
        Rows = Array.ConvertAll(rows, row => (IReadOnlyList<TreeSlot>)Array.AsReadOnly(row));
    }
}

/// <summary>
/// 왕국 발전 [연구] 탭의 트리 배치 (분야마다 단계 · 줄 · 열을 손으로 정한 표). 순수 C#.
/// 참고: Whiteout Survival(분야 트리 · 레벨로 열리는 단계), 쿠키런: 킹덤(연구소 레벨마다 기술 묶음) — 시안 Docs/왕국화면_개편_시안.md 8장.
/// 기술을 늘리면 ProsperityResearch.Techs와 여기 표에 한 줄씩 (ResearchTreeLayoutTests가 빠진 기술을 잡음).
/// 다른 분야의 선행은 같은 줄 옆에 Ghost 칸으로 두고 선으로 잇는다 (한 화면에 분야를 다 그리면 선이 엉킴)
/// </summary>
public static class ResearchTreeLayout
{
    private const TreeColumn L = TreeColumn.Left;
    private const TreeColumn C = TreeColumn.Center;
    private const TreeColumn R = TreeColumn.Right;

    public static readonly IReadOnlyList<TechBranch> Branches = new[] { TechBranch.Industry, TechBranch.Logistics, TechBranch.City, TechBranch.Economy };

    private static TreeSlot S(string id, TreeColumn column) => new TreeSlot(id, column);
    private static TreeSlot G(string id, TreeColumn column) => new TreeSlot(id, column, true);

    private static readonly Dictionary<TechBranch, TreeBand[]> Layouts = new Dictionary<TechBranch, TreeBand[]>
    {
        [TechBranch.Industry] = new[]
        {
            new TreeBand(new[] { S(TechIds.Sawmill, C) }, new[] { S(TechIds.StoneCutting, C) }),
            new TreeBand(new[] { S(TechIds.Smelting, C) }, new[] { S(TechIds.StandardTools, L) }, new[] { S(TechIds.WaterPower, L) }),
            new TreeBand(new[] { S(TechIds.PrecisionWork, C), G(TechIds.Warehouse, R) },
                new[] { S(TechIds.IndustrialLogistics, C), G(TechIds.BetterCart, R) },
                new[] { S(TechIds.CoProduction, C) }, new[] { S(TechIds.RoyalIndustry, C) }),
        },
        [TechBranch.Logistics] = new[]
        {
            new TreeBand(new[] { G(TechIds.Sawmill, L), S(TechIds.Warehouse, C) }, new[] { S(TechIds.Cart, C) }),
            new TreeBand(new[] { S(TechIds.Dispatch, C), S(TechIds.Depot, R) }, new[] { S(TechIds.AutoSupply, L) }),
            new TreeBand(new[] { S(TechIds.BetterCart, C) }),
        },
        [TechBranch.City] = new[]
        {
            new TreeBand(new[] { G(TechIds.Sawmill, L), S(TechIds.TownPlanning, C) }, new[] { S(TechIds.BetterHouses, L), G(TechIds.Warehouse, C) }),
            new TreeBand(new[] { S(TechIds.Paving, R) }, new[] { G(TechIds.Smelting, C), S(TechIds.PublicSpaces, R) }),
            new TreeBand(new[] { S(TechIds.Administration, L) },
                new[] { S(TechIds.Academy, L), G(TechIds.PrecisionWork, C), S(TechIds.Capital, R) },
                new[] { S(TechIds.Palace, C) }),
        },
        [TechBranch.Economy] = new[]
        {
            new TreeBand(new[] { G(TechIds.Sawmill, L), S(TechIds.Market, C) }),
        },
    };

    public static IReadOnlyList<TreeBand> For(TechBranch branch) => Layouts[branch];

    public static TechBranch BranchOf(string techId)
    {
        if (string.IsNullOrEmpty(techId))
            throw new ArgumentNullException(nameof(techId));
        switch (techId[0])
        {
            case 'L': return TechBranch.Logistics;
            case 'C': return TechBranch.City;
            case 'E': return TechBranch.Economy;
            default: return TechBranch.Industry; // I · X(융합: 산업 물류 체계)
        }
    }

    public static string Name(TechBranch branch)
    {
        switch (branch)
        {
            case TechBranch.Logistics: return "물류";
            case TechBranch.City: return "도시";
            case TechBranch.Economy: return "경제";
            default: return "산업";
        }
    }

    /// <summary>그 분야 화면에 진짜로 놓인 기술 (Ghost 빼고, 위에서 아래 순서)</summary>
    public static List<string> TechsOf(TechBranch branch)
    {
        var result = new List<string>();
        foreach (var band in Layouts[branch])
            foreach (var row in band.Rows)
                foreach (var slot in row)
                    if (!slot.Ghost)
                        result.Add(slot.TechId);
        return result;
    }

    /// <summary>단계 띠의 레벨대 (Ghost 빼고). 기술을 못 찾으면 (0, 0)</summary>
    public static (int min, int max) Levels(TreeBand band)
    {
        int min = int.MaxValue;
        int max = 0;
        foreach (var row in band.Rows)
            foreach (var slot in row)
            {
                if (slot.Ghost)
                    continue;
                var tech = ProsperityResearch.Find(slot.TechId);
                if (tech == null)
                    continue;
                min = Math.Min(min, tech.Level);
                max = Math.Max(max, tech.Level);
            }
        return min == int.MaxValue ? (0, 0) : (min, max);
    }
}
