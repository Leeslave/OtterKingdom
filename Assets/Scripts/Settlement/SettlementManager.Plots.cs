using System;
using UnityEngine;

/// <summary>
/// 부탁으로 짓는 광장 건물(첫 집 · 두 번째 집 · 의자 · 벤치 · 가로등 · 공동사업 건물)의 자리 고르기.
/// 부탁의 [자리 고르기] → 건설 모드에서 자리를 고르고 [확인] → 자리를 꾸미기 격자에 놓고 같은 순간 건설을 시작 (TryStartAt).
/// 자리는 꾸미기 세이브, 공사·완료는 부탁 기록이 원본이다. 다 지은 건물은 꾸미기 모드에서 옮길 수 있다 (치울 수는 없음)
/// </summary>
public partial class SettlementManager
{
    /// <summary>이 건설을 짓는 자리 (자리를 고르지 않는 건설이면 null)</summary>
    public ConstructionPlotDefinition FindPlot(string constructionId)
    {
        var catalog = DecorManager.Instance != null ? DecorManager.Instance.Catalog : null;
        return catalog != null ? catalog.FindPlot(constructionId) : null;
    }

    /// <summary>이 부탁의 건설을 짓는 자리 (자리를 고르지 않는 부탁이면 null)</summary>
    public ConstructionPlotDefinition PlotOf(BoardRequestDefinition request) =>
        request != null && request.Construction != null ? FindPlot(request.Construction.ConstructionId) : null;

    /// <summary>이 자리에 짓는 부탁 (게시판 부탁 중)</summary>
    public BoardRequestDefinition RequestOf(ConstructionPlotDefinition plot)
    {
        if (plot == null || plot.Construction == null)
            return null;
        foreach (var request in _config.Requests)
        {
            if (request != null && request.Construction == plot.Construction)
                return request;
        }
        return null;
    }

    /// <summary>자리가 이미 광장 격자에 놓였는지 (공사 중이거나 다 지음)</summary>
    public bool IsPlotPlaced(ConstructionPlotDefinition plot) => FindPlaced(plot) != null;

    private PlacedDecor FindPlaced(ConstructionPlotDefinition plot)
    {
        var layout = PlazaLayout;
        if (layout == null || plot == null)
            return null;
        foreach (var placed in layout.Placed)
        {
            if (placed.Decor == plot)
                return placed;
        }
        return null;
    }

    private DecorLayout PlazaLayout
    {
        get
        {
            var decor = DecorManager.Instance;
            var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
            return board != null ? decor.GetLayout(board) : null;
        }
    }

    /// <summary>이 자리에 짓는 건설이 지금 공사 중이면 남은 시간 (아니면 false)</summary>
    public bool TryGetPlotRemaining(ConstructionPlotDefinition plot, out TimeSpan remaining)
    {
        var job = Settlement.Job;
        remaining = TimeSpan.Zero;
        if (plot == null || job == null || job.ConstructionId != plot.ConstructionId)
            return false;
        remaining = job.Remaining(NowTicks);
        return true;
    }

    /// <summary>
    /// 고른 자리에 놓고 부탁의 건설을 시작: 시작 확인 → 자리 확인 → 놓기 → 건설 시작(비용). 하나라도 안 되면 아무것도 바꾸지 않는다
    /// </summary>
    public ConstructionStartResult TryStartAt(BoardRequestDefinition request, Vector2Int origin, out DecorPlacementResult placement)
    {
        placement = DecorPlacementResult.Ok;
        var plot = PlotOf(request);
        if (plot == null)
            throw new InvalidOperationException($"[{request?.name}] 자리를 골라 짓는 건설이 아닙니다.");

        var check = CheckStart(request);
        if (check != ConstructionStartResult.Started)
            return check;
        var layout = PlazaLayout;
        if (layout == null)
        {
            placement = DecorPlacementResult.Unavailable;
            return ConstructionStartResult.NotAvailable;
        }
        if (IsPlotPlaced(plot))
            return ConstructionStartResult.NotAvailable;

        placement = layout.TryPlace(plot, origin, DecorRotation.R0, out var placed);
        if (placement != DecorPlacementResult.Ok)
            return ConstructionStartResult.NotAvailable;

        var result = TryStart(request);
        if (result != ConstructionStartResult.Started && result != ConstructionStartResult.Completed)
            layout.Remove(placed.InstanceId);
        return result;
    }

    /// <summary>시작하지 못한 이유를 화면에 보일 문장으로</summary>
    public string StartBlockText(BoardRequestDefinition request, ConstructionStartResult result)
    {
        var construction = request.Construction;
        switch (result)
        {
            case ConstructionStartResult.Busy:
                return "다른 공사가 끝나면 시작할 수 있어요";
            case ConstructionStartResult.NoBuilder:
                return "건설 해달이 있어야 지을 수 있어요";
            case ConstructionStartResult.WorkerBusy:
                return request.Requester != null
                    ? $"{request.Requester.DisplayName}{KoreanParticle.SubjectParticle(request.Requester.DisplayName)} 작업하러 가 있어요"
                    : "일할 해달이 작업하러 가 있어요";
            case ConstructionStartResult.NotEnoughGold:
                return $"골드가 {construction.RequiredGold - GoldBalance:N0} 모자라요";
            case ConstructionStartResult.NotEnoughItems:
                foreach (var cost in construction.RequiredItems)
                {
                    if (cost == null || cost.Item == null)
                        continue;
                    int lack = cost.Amount - ItemCount(cost.Item);
                    if (lack > 0)
                        return $"{cost.Item.DisplayName} {lack}개가 더 필요해요";
                }
                return "재료가 모자라요";
            default:
                return "지금은 지을 수 없어요";
        }
    }

    /// <summary>비용 한 줄 (예: 골드 150 · 목재 18 · 돌 8)</summary>
    public string ConstructionCostText(ConstructionDefinition construction)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (construction.RequiredGold > 0)
            parts.Add($"골드 {construction.RequiredGold:N0}");
        foreach (var cost in construction.RequiredItems)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                parts.Add($"{cost.Item.DisplayName} {cost.Amount}");
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "무료";
    }

    /// <summary>
    /// 불러온 뒤 한 번: 자리를 고르기 전에 지었거나 짓고 있던 건물(옛 세이브)을 원래 씬 자리(기본 칸)에 놓는다.
    /// 원래 자리에 다른 물건이 있으면 가까운 빈자리, 그것도 없으면 놓지 않고 씬 자리 그대로 둔다 (옮기기만 못 함)
    /// </summary>
    private void ReconcilePlots()
    {
        var decor = DecorManager.Instance;
        var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        if (board == null || decor.Catalog == null)
            return;
        var layout = decor.GetLayout(board);

        foreach (var plot in decor.Catalog.Plots)
        {
            if (plot == null || plot.Construction == null || IsPlotPlaced(plot))
                continue;
            var job = Settlement.Job;
            bool started = Settlement.HasDevelopment(plot.Construction.UnlockResultId)
                || (job != null && job.ConstructionId == plot.ConstructionId);
            if (!started)
                continue;

            // 원래 자리는 씬이 비워 두던 곳이라 월드 막힘은 보지 않음 (씬을 열기 전이면 막힘이 아직 맞지 않음)
            var origin = board.SaveOrigin + plot.DefaultCellFor(Settlement.HasDevelopment);
            if (layout.TryPlace(plot, origin, DecorRotation.R0, out _, ignoreBlocked: true) == DecorPlacementResult.Ok)
                continue;
            var center = origin + new Vector2Int(plot.Footprint.x / 2, plot.Footprint.y / 2);
            if (DecorEditSession.TryFindFreeNear(layout, plot, center, DecorRotation.R0, out var near)
                && layout.TryPlace(plot, near, DecorRotation.R0, out _) == DecorPlacementResult.Ok)
                Debug.LogWarning($"[SettlementManager] '{plot.DisplayName}'의 원래 자리가 막혀 {near}에 놓았습니다.");
            else
                Debug.LogWarning($"[SettlementManager] '{plot.DisplayName}'을(를) 놓을 자리가 없어 씬 자리에 그대로 둡니다.");
        }
    }
}
