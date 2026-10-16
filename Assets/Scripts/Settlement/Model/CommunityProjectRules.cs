using System;
using System.Collections.Generic;

/// <summary>공동사업이 지금 어디까지 왔는지</summary>
public enum ProjectPhase
{
    Locked,     // 앞 사업(또는 마을회관)이 아직
    NeedsLevel, // 열렸지만 플레이어 레벨이 모자람 (손대기 전에만. 하던 사업은 레벨과 상관없이 끝냄)
    Delivering, // 재료를 넣는 중
    Stage,      // 단계를 하는 중 (StageIndex)
    Completed,
}

/// <summary>지금 단계의 상태</summary>
public enum ProjectStageState
{
    Ready,   // 플레이어가 할 일이 있음 (건설 시작 · 장애물 치우기 · 주민 보내기 · 입주 · 소품 고르기 · 모임 열기)
    Busy,    // 건설 큐를 다른 공사가 쓰는 중 (재료는 그대로 보존, 끝나면 시작)
    Working, // 공사·정비가 진행 중
    Waiting, // 해달이 찾아오는 중 등 플레이어가 기다림
    Done,
}

/// <summary>공동사업 하나의 지금 상태 (기록에서 계산. 저장하지 않음)</summary>
public readonly struct ProjectStatus
{
    public readonly ProjectPhase Phase;
    /// <summary>지금 단계 (Stage일 때)</summary>
    public readonly int StageIndex;
    public readonly ProjectStageState StageState;

    public ProjectStatus(ProjectPhase phase, int stageIndex = -1, ProjectStageState stageState = ProjectStageState.Ready)
    {
        Phase = phase;
        StageIndex = stageIndex;
        StageState = stageState;
    }
}

/// <summary>필요한 재료 한 줄 (골드는 Item이 null)</summary>
public readonly struct ProjectMaterial
{
    public readonly ItemDefinition Item;
    public readonly int Required;
    public readonly int Delivered;

    public ProjectMaterial(ItemDefinition item, int required, int delivered)
    {
        Item = item;
        Required = required;
        Delivered = delivered;
    }

    public bool IsGold => Item == null;
    public int Remaining => Math.Max(0, Required - Delivered);

    /// <summary>지금 넣을 수 있는 양: 남은 양과 가진 양 중 작은 것</summary>
    public int Deliverable(int owned) => Math.Max(0, Math.Min(Remaining, owned));
}

/// <summary>
/// 공동사업 규칙 (데이터 + 상태 → 결과). 돈·가방·레벨은 값으로만 받는다 — SettlementManager가 조율한다.
/// - 사업은 순서대로 하나씩: 앞 사업의 결과 발전이 있고 끝내지 않은 첫 사업이 "지금 사업"
/// - 단계 완료는 원본 기록에서 계산 (건설 완료 부탁, 치운 장애물 기록, 끝낸 작업, 집의 입주민, 모임 기록)
/// - 반복 사업은 회차마다 재료가 돌아가며 바뀌고, 비용 배율은 상한까지만 오른다
/// </summary>
public static class CommunityProjectRules
{
    /// <summary>장애물 치움 기록 (SettlementManager.MarkObstacleCleared와 같은 이름)</summary>
    public static string ObstacleFlag(string obstacleId) => "cleared_" + obstacleId;

    /// <summary>모임을 연 기록</summary>
    public static string GatheringFlag(CommunityProjectDefinition project) => "gathering_held_" + project.ProjectId;

    /// <summary>사업 보상 기록 키 (사업 ID#회차)</summary>
    public static string RewardKey(CommunityProjectDefinition project, int cycle) => $"{project.ProjectId}#{cycle}";

    /// <summary>집 인스턴스 ID (자리마다 한 채)</summary>
    public static string HouseInstanceId(string slotId) => "house_" + slotId;

    #region 지금 사업

    /// <summary>
    /// 지금 사업: 순서대로 보아 열리는 조건(앞 사업의 결과 발전)이 되고 아직 끝내지 않은 첫 사업 (반복 사업은 끝이 없음).
    /// 열린 사업이 없으면 null (마을회관 전)
    /// </summary>
    public static CommunityProjectDefinition FindActive(SettlementConfig config, Settlement settlement)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        foreach (var project in Sorted(config))
        {
            if (!project.Repeatable && IsCompleted(project, settlement))
                continue;
            return settlement.HasDevelopment(project.RequiredDevelopment) ? project : null;
        }
        return null;
    }

    /// <summary>순서대로 (order → ID)</summary>
    public static List<CommunityProjectDefinition> Sorted(SettlementConfig config)
    {
        var sorted = new List<CommunityProjectDefinition>();
        foreach (var project in config.Projects)
        {
            if (project != null && !string.IsNullOrEmpty(project.ProjectId))
                sorted.Add(project);
        }
        sorted.Sort((a, b) =>
        {
            int order = a.Order.CompareTo(b.Order);
            return order != 0 ? order : string.CompareOrdinal(a.ProjectId, b.ProjectId);
        });
        return sorted;
    }

    /// <summary>한 번만 하는 사업을 끝냈는지 (반복 사업은 한 번이라도 끝냈는지)</summary>
    public static bool IsCompleted(CommunityProjectDefinition project, Settlement settlement)
    {
        var progress = settlement.GetProject(project.ProjectId);
        if (progress == null)
            return false;
        return project.Repeatable ? progress.Cycle > 0 : progress.Completed;
    }

    /// <summary>손을 댔는지 (재료를 넣었거나 소품을 골랐음) — 레벨이 모자라도 하던 사업은 이어서 함</summary>
    public static bool IsStarted(CommunityProjectDefinition project, Settlement settlement)
    {
        var progress = settlement.GetProject(project.ProjectId);
        if (progress == null)
            return false;
        if (progress.Gold > 0 || !string.IsNullOrEmpty(progress.Choice))
            return true;
        foreach (var pair in progress.Items)
        {
            if (pair.Value > 0)
                return true;
        }
        return false;
    }

    public static ProjectStatus GetStatus(CommunityProjectDefinition project, Settlement settlement, int playerLevel)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (!project.Repeatable && IsCompleted(project, settlement))
            return new ProjectStatus(ProjectPhase.Completed);
        if (!settlement.HasDevelopment(project.RequiredDevelopment))
            return new ProjectStatus(ProjectPhase.Locked);
        if (playerLevel < project.RequiredLevel && !IsStarted(project, settlement))
            return new ProjectStatus(ProjectPhase.NeedsLevel);
        if (!IsDelivered(project, settlement))
            return new ProjectStatus(ProjectPhase.Delivering);

        var stages = project.Stages;
        for (int i = 0; i < stages.Count; i++)
        {
            var state = StageState(project, stages[i], settlement);
            if (state != ProjectStageState.Done)
                return new ProjectStatus(ProjectPhase.Stage, i, state);
        }
        // 모든 단계를 끝냄 → SettlementManager가 결과를 주고 Completed로 (반복 사업은 다음 회차)
        return new ProjectStatus(ProjectPhase.Stage, stages.Count, ProjectStageState.Done);
    }

    /// <summary>모든 단계를 끝내 결과를 받을 차례인지 (아직 결과를 받지 않음)</summary>
    public static bool IsReadyToComplete(CommunityProjectDefinition project, Settlement settlement, int playerLevel)
    {
        var status = GetStatus(project, settlement, playerLevel);
        return status.Phase == ProjectPhase.Stage && status.StageIndex >= project.Stages.Count;
    }

    #endregion

    #region 단계

    public static ProjectStageState StageState(CommunityProjectDefinition project, ProjectStageDefinition stage, Settlement settlement)
    {
        switch (stage.Action)
        {
            case ProjectActionKind.CompleteConstruction:
                return ConstructionState(stage.Construction, settlement);

            case ProjectActionKind.PlaceWelcomeProp:
            {
                foreach (var option in stage.Options)
                {
                    if (option != null && settlement.IsCompleted(option.RequestId))
                        return ProjectStageState.Done;
                }
                foreach (var option in stage.Options)
                {
                    if (option != null && settlement.Job != null && settlement.Job.RequestId == option.RequestId)
                        return ProjectStageState.Working;
                }
                return settlement.Job != null ? ProjectStageState.Busy : ProjectStageState.Ready;
            }

            case ProjectActionKind.ClearObstacles:
                return AreObstaclesCleared(stage, settlement) ? ProjectStageState.Done : ProjectStageState.Ready;

            case ProjectActionKind.CompleteRegionTask:
            {
                var task = stage.Task;
                if (task == null || settlement.IsTaskCompleted(task.TaskId))
                    return ProjectStageState.Done;
                if (settlement.TryGetTaskJob(task.TaskId, out _))
                    return ProjectStageState.Working;
                return settlement.HasDevelopment(task.RequiredDevelopment) ? ProjectStageState.Ready : ProjectStageState.Waiting;
            }

            case ProjectActionKind.SettleResident:
            {
                var otter = stage.Resident;
                if (otter == null)
                    return ProjectStageState.Done;
                if (IsSettled(stage, settlement))
                    return ProjectStageState.Done;
                // 집이 있고 해달을 광장에서 만났으면 말을 걸어 입주
                return settlement.FindHouseBySlot(stage.HouseSlotId) != null && settlement.HasMet(otter.OtterId)
                    ? ProjectStageState.Ready
                    : ProjectStageState.Waiting;
            }

            case ProjectActionKind.HoldGathering:
                return settlement.HasFlag(GatheringFlag(project)) ? ProjectStageState.Done : ProjectStageState.Ready;

            default:
                return ProjectStageState.Done;
        }
    }

    private static ProjectStageState ConstructionState(BoardRequestDefinition request, Settlement settlement)
    {
        if (request == null || settlement.IsCompleted(request.RequestId))
            return ProjectStageState.Done;
        if (settlement.Job != null && settlement.Job.RequestId == request.RequestId)
            return ProjectStageState.Working;
        return settlement.Job != null ? ProjectStageState.Busy : ProjectStageState.Ready;
    }

    public static bool AreObstaclesCleared(ProjectStageDefinition stage, Settlement settlement)
    {
        foreach (var id in stage.ObstacleIds)
        {
            if (!string.IsNullOrEmpty(id) && !settlement.HasFlag(ObstacleFlag(id)))
                return false;
        }
        return true;
    }

    public static int CountObstaclesCleared(ProjectStageDefinition stage, Settlement settlement)
    {
        int count = 0;
        foreach (var id in stage.ObstacleIds)
        {
            if (!string.IsNullOrEmpty(id) && settlement.HasFlag(ObstacleFlag(id)))
                count++;
        }
        return count;
    }

    /// <summary>그 집 자리에 그 해달이 입주했는지 (집의 입주민 + 주민 처지 둘 다)</summary>
    public static bool IsSettled(ProjectStageDefinition stage, Settlement settlement)
    {
        var otter = stage.Resident;
        var house = settlement.FindHouseBySlot(stage.HouseSlotId);
        return otter != null && house != null && house.ResidentId == otter.OtterId
            && settlement.TryGetResidentState(otter.OtterId, out var state) && state == ResidentState.Resident;
    }

    /// <summary>이 사업에서 지금 짓는(또는 지을) 건설 부탁. 소품은 고른 것</summary>
    public static BoardRequestDefinition StageConstruction(CommunityProjectDefinition project, ProjectStageDefinition stage, Settlement settlement)
    {
        if (stage.Action == ProjectActionKind.CompleteConstruction)
            return stage.Construction;
        if (stage.Action != ProjectActionKind.PlaceWelcomeProp)
            return null;
        var progress = settlement.GetProject(project.ProjectId);
        string choice = progress?.Choice;
        foreach (var option in stage.Options)
        {
            if (option != null && option.RequestId == choice)
                return option;
        }
        return null;
    }

    #endregion

    #region 재료

    /// <summary>이 회차의 비용 배율 (반복 사업: 1 + 증가율 × 회차, 상한까지)</summary>
    public static float CostMultiplier(CommunityProjectDefinition project, int cycle)
    {
        if (!project.Repeatable)
            return 1f;
        return Math.Min(project.MaxCostMultiplier, 1f + project.CostGrowthPerCycle * Math.Max(0, cycle));
    }

    /// <summary>필요한 재료 (골드 먼저) + 이미 넣은 양</summary>
    public static void CollectMaterials(CommunityProjectDefinition project, Settlement settlement, List<ProjectMaterial> result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        result.Clear();
        var progress = settlement.GetProject(project.ProjectId);
        int cycle = progress?.Cycle ?? 0;
        float multiplier = CostMultiplier(project, cycle);

        int baseGold = project.Gold;
        IReadOnlyList<ItemAmount> baseItems = project.Items;
        if (project.Repeatable && project.CycleCosts.Count > 0)
        {
            var cost = project.CycleCosts[cycle % project.CycleCosts.Count];
            if (cost != null)
            {
                baseGold = cost.Gold;
                baseItems = cost.Items;
            }
        }

        int gold = Scale(baseGold, multiplier);
        if (gold > 0)
            result.Add(new ProjectMaterial(null, gold, Math.Min(gold, progress?.Gold ?? 0)));
        foreach (var cost in baseItems)
        {
            if (cost == null || cost.Item == null || cost.Amount <= 0)
                continue;
            int required = Scale(cost.Amount, multiplier);
            result.Add(new ProjectMaterial(cost.Item, required, Math.Min(required, progress?.Delivered(cost.Item.ItemId) ?? 0)));
        }
    }

    private static int Scale(int amount, float multiplier) => amount <= 0 ? 0 : (int)Math.Ceiling(amount * multiplier - 0.0001f);

    /// <summary>재료를 다 넣었는지</summary>
    public static bool IsDelivered(CommunityProjectDefinition project, Settlement settlement)
    {
        var materials = new List<ProjectMaterial>();
        CollectMaterials(project, settlement, materials);
        foreach (var material in materials)
        {
            if (material.Remaining > 0)
                return false;
        }
        return true;
    }

    /// <summary>이 회차의 제목 (반복 사업은 회차마다 돌려 씀)</summary>
    public static string Title(CommunityProjectDefinition project, Settlement settlement)
    {
        if (!project.Repeatable || project.CycleTitles.Count == 0)
            return project.Title;
        int cycle = settlement.ProjectCycle(project.ProjectId);
        string cycleTitle = project.CycleTitles[cycle % project.CycleTitles.Count];
        return string.IsNullOrEmpty(project.Title) ? cycleTitle : $"{project.Title} · {cycleTitle}";
    }

    #endregion
}
