using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자리를 골라 짓는 건물 (P4): 꾸미기 모드의 건물 탭에서 자리를 정하면 비용을 내고 그 자리에 공사가 시작된다.
/// 놓인 자리는 꾸미기 격자(광장), 공사·완성 기록은 Settlement.Buildings가 원본. 다 지은 집은 빈 집이 된다 (장난감 해달 입주)
/// </summary>
public partial class SettlementManager
{
    // 돌아옴 팝업에 알릴, 이번 실행에서 다 지은 건물 (끝난 시각과 함께. 알리면 비움)
    private readonly List<(BuildingRecord record, long endTicks)> _finishedBuildings = new List<(BuildingRecord, long)>();
    private readonly List<BuildingRecord> _dueBuildings = new List<BuildingRecord>();

    /// <summary>건물 공사가 시작됐을 때</summary>
    public event Action<BuildingRecord> OnBuildingStarted;

    /// <summary>건물을 다 지었을 때</summary>
    public event Action<BuildingRecord> OnBuildingFinished;

    /// <summary>지을 수 있는 건물 목록 (꾸미기 카탈로그의 건물)</summary>
    public IReadOnlyList<BuildingDefinition> BuildingCatalog =>
        DecorManager.Instance != null && DecorManager.Instance.Catalog != null
            ? DecorManager.Instance.Catalog.Buildings
            : (IReadOnlyList<BuildingDefinition>)Array.Empty<BuildingDefinition>();

    public BuildingDefinition FindBuilding(string buildingId) =>
        DecorManager.Instance != null && DecorManager.Instance.Catalog != null ? DecorManager.Instance.Catalog.FindBuilding(buildingId) : null;

    /// <summary>이 배치 개체의 공사·완성 기록 (건물이 아니거나 기록이 없으면 null)</summary>
    public BuildingRecord BuildingRecordOf(int instanceId) => Settlement.FindBuilding(instanceId);

    /// <summary>지금 지을 수 있는지 (안 되면 첫 이유)</summary>
    public BuildingBlock CheckBuilding(BuildingDefinition building) =>
        BuildingRules.CheckStart(_config, Settlement, building, PlayerLevel, HasBuilder, GoldBalance, ItemCount);

    /// <summary>이 건물이 열렸는지 (비용·공사 자리와 상관없이)</summary>
    public BuildingBlock CheckBuildingUnlocked(BuildingDefinition building) =>
        BuildingRules.CheckUnlocked(_config, Settlement, building, PlayerLevel);

    public int BuildingGoldCost(BuildingDefinition building) => BuildingRules.GoldCost(Settlement, building);

    public List<ItemAmount> BuildingItemCosts(BuildingDefinition building) => BuildingRules.ItemCosts(Settlement, building);

    /// <summary>비용 한 줄 (예: 골드 600 · 목재 20 · 돌 10)</summary>
    public string BuildingCostText(BuildingDefinition building)
    {
        var parts = new List<string>();
        int gold = BuildingGoldCost(building);
        if (gold > 0)
            parts.Add($"골드 {gold:N0}");
        foreach (var cost in BuildingItemCosts(building))
            parts.Add($"{cost.Item.DisplayName} {cost.Amount}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "무료";
    }

    /// <summary>못 짓는 이유를 화면에 보일 문장으로</summary>
    public string BuildingBlockText(BuildingDefinition building, BuildingBlock block)
    {
        switch (block)
        {
            case BuildingBlock.Level:
                return $"왕국 Lv.{building.RequiredLevel}부터 지을 수 있어요";
            case BuildingBlock.Development:
                return "마을이 더 발전하면 지을 수 있어요";
            case BuildingBlock.Traits:
                return $"특성 해달이 모자라요 ({TraitNeedText(building)})";
            case BuildingBlock.MaxCount:
                return "더 지을 수 없어요";
            case BuildingBlock.Busy:
                return "다른 건물을 짓는 중이에요";
            case BuildingBlock.NoBuilder:
                return "건설 해달이 있어야 지을 수 있어요";
            case BuildingBlock.Gold:
                return $"골드가 {BuildingGoldCost(building) - GoldBalance:N0} 모자라요";
            case BuildingBlock.Items:
                foreach (var cost in BuildingItemCosts(building))
                {
                    int lack = cost.Amount - ItemCount(cost.Item);
                    if (lack > 0)
                        return $"{cost.Item.DisplayName} {lack}개가 더 필요해요";
                }
                return "재료가 모자라요";
            default:
                return "";
        }
    }

    /// <summary>특성 조건 진행 (예: 나무캐기 1/2 · 손재주 2/2)</summary>
    public string TraitNeedText(BuildingDefinition building)
    {
        var parts = new List<string>();
        foreach (var requirement in building.Traits)
        {
            if (requirement != null)
                parts.Add($"{OtterTraits.DisplayName(requirement.Trait)} {Math.Min(TraitCount(requirement.Trait), requirement.Count)}/{requirement.Count}");
        }
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 고른 자리에 건물을 놓고 공사를 시작: 확인 → 자리 확인 → 비용 → 놓기 → 공사 기록 → 저장. 하나라도 안 되면 아무것도 바꾸지 않는다
    /// </summary>
    public BuildingBlock TryStartBuilding(BuildingDefinition building, Vector2Int origin, out DecorPlacementResult placement)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));

        placement = DecorPlacementResult.Ok;
        var block = CheckBuilding(building);
        if (block != BuildingBlock.None)
            return block;

        var decor = DecorManager.Instance;
        var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        if (board == null)
        {
            placement = DecorPlacementResult.Unavailable;
            return BuildingBlock.None;
        }
        placement = decor.GetLayout(board).Check(building, origin, DecorRotation.R0);
        if (placement != DecorPlacementResult.Ok)
            return BuildingBlock.None;

        // 비용은 놓기 전에 계산 (놓으면 같은 건물 수가 늘어 비용이 바뀜)
        int gold = BuildingGoldCost(building);
        var items = BuildingItemCosts(building);
        placement = decor.TryPlaceBuilding(board, building, origin, out var placed);
        if (placement != DecorPlacementResult.Ok)
            return BuildingBlock.None;
        PayCost(gold, items);

        BuildingRules.Start(Settlement, building, placed.InstanceId, NowTicks,
            DevTimers.Duration(KingdomBonus.Shorten(KingdomBonusKind.BuildSpeed, building.BuildSeconds)));
        var record = Settlement.FindBuilding(placed.InstanceId);
        OnBuildingStarted?.Invoke(record);
        SaveRequested?.Invoke();
        return BuildingBlock.None;
    }

    /// <summary>다 지은 시설의 이 효과 합계 (%). KingdomBonus가 이 값을 묻는다</summary>
    public int BonusPercent(KingdomBonusKind kind) => IsLoaded ? BuildingRules.BonusPercent(Settlement, FindBuilding, kind) : 0;

    // 건물 · 입주 부탁(P4): 기록으로 조건을 채운 부탁을 끝냄 (한 프레임에 하나 — 끝내면 다음 부탁이 열려 다음 프레임에 다시 봄)
    private void CompleteRecordRequests()
    {
        var request = SettlementRules.FindRecordComplete(_config, Settlement);
        if (request != null)
            Complete(request);
    }

    /// <summary>집 짓기 안내에 쓸 집 건물 (카탈로그의 첫 집)</summary>
    public BuildingDefinition HomeBuilding
    {
        get
        {
            foreach (var building in BuildingCatalog)
            {
                if (building != null && building.Kind == BuildingKind.Home)
                    return building;
            }
            return null;
        }
    }

    /// <summary>이 건물을 짓는 중이면 그 기록 (없으면 null)</summary>
    public BuildingRecord ActiveBuildingOf(BuildingDefinition building)
    {
        if (building == null)
            return null;
        foreach (var record in Settlement.Buildings)
        {
            if (!record.Built && record.BuildingId == building.BuildingId)
                return record;
        }
        return null;
    }

    // 시간이 된 공사를 끝냄 (불러올 때는 알림 없이 — 돌아옴 팝업이 알림)
    private void FinishDueBuildings(bool notify = true)
    {
        BuildingRules.FinishDue(Settlement, NowTicks, _dueBuildings);
        if (_dueBuildings.Count == 0)
            return;

        BuildingRules.SyncHomes(Settlement, FindBuilding);
        foreach (var record in _dueBuildings)
        {
            _finishedBuildings.Add((record, record.EndUtcTicks));
            OnBuildingFinished?.Invoke(record);
            var building = FindBuilding(record.BuildingId);
            if (notify && building != null)
                GameNotices.Post(new GameNotice(building.Kind == BuildingKind.Home
                    ? $"{building.DisplayName} 완성! 빈 집이 생겼어요"
                    : $"{building.DisplayName} 완성!"));
        }
        SaveRequested?.Invoke();
    }

    // 돌아옴 팝업: 비운 동안 다 지은 건물, 아직 짓는 중인 건물
    private void CollectBuildingNews(long leftAtTicks, long nowTicks, List<string> lines)
    {
        FinishDueBuildings();
        foreach (var (record, endTicks) in _finishedBuildings)
        {
            var building = FindBuilding(record.BuildingId);
            if (building != null && endTicks >= leftAtTicks)
                lines.Add($"{building.DisplayName} 완성!");
        }
        _finishedBuildings.Clear();
        foreach (var record in Settlement.Buildings)
        {
            var building = record.Built ? null : FindBuilding(record.BuildingId);
            if (building != null)
                lines.Add($"{building.DisplayName} 짓는 중 · {FormatRemaining(record.Remaining(nowTicks))} 남았어요");
        }
    }

    /// <summary>
    /// 불러온 뒤 한 번: 공사 기록과 광장 격자의 자리를 맞춘다 (꾸미기는 정착보다 먼저 불러옴).
    /// 기록은 있는데 자리가 없으면(격자가 바뀜) 광장 가운데 근처 빈자리에 다시 놓고, 자리는 있는데 기록이 없으면 다 지은 것으로 남긴다
    /// </summary>
    private void ReconcileBuildings()
    {
        var decor = DecorManager.Instance;
        var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        if (board == null)
            return;
        var layout = decor.GetLayout(board);

        foreach (var placed in layout.Placed)
        {
            if (placed.Decor is BuildingDefinition building && Settlement.FindBuilding(placed.InstanceId) == null)
            {
                long now = NowTicks;
                Settlement.StartBuilding(placed.InstanceId, building.BuildingId, now, now);
                Settlement.FinishBuilding(placed.InstanceId);
            }
        }

        foreach (var record in new List<BuildingRecord>(Settlement.Buildings))
        {
            if (layout.TryGet(record.InstanceId, out var existing) && existing.Decor.SaveId == record.BuildingId)
                continue;
            var building = FindBuilding(record.BuildingId);
            // 처음 광장(세이브 기준 칸에서 24 × 33칸)의 가운데쯤부터 찾음
            var center = board.SaveOrigin + new Vector2Int(12, 16);
            if (building == null || !DecorEditSession.TryFindFreeNear(layout, building, center, DecorRotation.R0, out var origin)
                || !layout.LoadPlaced(record.InstanceId, building, origin, DecorRotation.R0))
            {
                Debug.LogWarning($"[SettlementManager] 건물 '{record.BuildingId}'({record.InstanceId})의 자리를 찾지 못해 기록을 지웁니다.");
                Settlement.RemoveBuilding(record.InstanceId);
                continue;
            }
            Debug.LogWarning($"[SettlementManager] 건물 '{record.BuildingId}'({record.InstanceId})의 자리가 없어 {origin}에 다시 놓았습니다.");
        }
        BuildingRules.SyncHomes(Settlement, FindBuilding);
    }
}
