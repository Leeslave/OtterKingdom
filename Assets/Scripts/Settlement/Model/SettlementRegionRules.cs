using System;

/// <summary>
/// 개간 지역 · 주민 작업 규칙 (데이터 + 상태 → 결과). 시간·저장·다른 시스템은 모름 — SettlementManager가 조율한다.
/// 지역의 단계는 발전과 작업 상태에서 정해진다 (따로 저장하지 않아 서로 어긋날 일이 없음).
/// </summary>
public static class SettlementRegionRules
{
    public static SettlementTaskState GetTaskState(SettlementTaskDefinition task, Settlement settlement)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        if (settlement.IsTaskCompleted(task.TaskId))
            return SettlementTaskState.Completed;
        if (settlement.TryGetTaskJob(task.TaskId, out _))
            return SettlementTaskState.Working;
        return settlement.HasDevelopment(task.RequiredDevelopment) ? SettlementTaskState.Available : SettlementTaskState.Locked;
    }

    public static RegionProgressState GetRegionState(DevelopableRegionDefinition region, Settlement settlement)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        if (IsSet(region.OperationalDevelopment) && settlement.HasDevelopment(region.OperationalDevelopment))
            return RegionProgressState.Operational;
        if (!settlement.HasDevelopment(region.DiscoverDevelopment))
            return RegionProgressState.Locked;
        if (!IsSet(region.PlayerClearDevelopment) || !settlement.HasDevelopment(region.PlayerClearDevelopment))
        {
            bool reachable = region.Zone == null || settlement.HasDevelopment(region.Zone.RequiredDevelopment);
            return reachable ? RegionProgressState.PlayerClearing : RegionProgressState.Discovered;
        }
        foreach (var task in region.PreparationTasks)
        {
            if (task != null && GetTaskState(task, settlement) == SettlementTaskState.Working)
                return RegionProgressState.WorkerPreparing;
        }
        return RegionProgressState.AwaitingWorkers;
    }

    /// <summary>후속 정비를 모두 끝냈는지 (작업이 없으면 true)</summary>
    public static bool ArePreparationsDone(DevelopableRegionDefinition region, Settlement settlement)
    {
        foreach (var task in region.PreparationTasks)
        {
            if (task != null && !settlement.IsTaskCompleted(task.TaskId))
                return false;
        }
        return true;
    }

    /// <summary>지금 손댈 후속 정비 (진행 중인 것 우선, 없으면 시작할 수 있는 것 중 앞의 것). 없으면 null</summary>
    public static SettlementTaskDefinition CurrentPreparation(DevelopableRegionDefinition region, Settlement settlement)
    {
        SettlementTaskDefinition available = null;
        foreach (var task in region.PreparationTasks)
        {
            if (task == null)
                continue;
            var state = GetTaskState(task, settlement);
            if (state == SettlementTaskState.Working)
                return task;
            if (state == SettlementTaskState.Available && available == null)
                available = task;
        }
        return available;
    }

    /// <summary>
    /// 작업에 보낼 수 있는 해달인지: 주민(Resident)이고, 지금 다른 작업 중이 아님.
    /// 건설 해달은 큰 공사 담당이라 주민 작업에 보내지 않는다. 공사 중인 해달은 SettlementManager가 따로 뺀다.
    /// </summary>
    public static bool CanWork(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (otter.IsBuilder)
            return false;
        return settlement.TryGetResidentState(otter.OtterId, out var state)
            && state == ResidentState.Resident
            && settlement.GetWorkState(otter.OtterId) == ResidentWorkState.Idle;
    }

    private static bool IsSet(string developmentId) => !string.IsNullOrEmpty(developmentId);
}
