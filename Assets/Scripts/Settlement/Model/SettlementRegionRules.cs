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
    /// 건설 해달은 큰 공사 담당, 전문 해달(광부·농부)은 맡은 곳에서만 일하므로 주민 작업에 보내지 않는다.
    /// 공사 중인 해달은 SettlementManager가 따로 뺀다.
    /// </summary>
    public static bool CanWork(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (otter.IsBuilder || otter.IsSpecialist)
            return false;
        return settlement.TryGetResidentState(otter.OtterId, out var state)
            && state == ResidentState.Resident
            && settlement.GetWorkState(otter.OtterId) == ResidentWorkState.Idle;
    }

    #region 전문 해달

    /// <summary>
    /// 이 지역에서 생산할 수 있는지 = 운영 중이고, 전문 해달이 있는 지역이면 그 해달이 여기 배치되어 일하는 중.
    /// 클릭·자동·오프라인 생산이 모두 이 조건을 본다 (가방·재료 같은 기존 조건은 따로)
    /// </summary>
    public static bool CanProduce(DevelopableRegionDefinition region, SettlementOtterDefinition specialist, Settlement settlement)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));
        if (GetRegionState(region, settlement) != RegionProgressState.Operational)
            return false;
        if (specialist == null)
            return true;
        return settlement.TryGetSpecialist(specialist.OtterId, out var record)
            && record.State == SpecialistState.Working
            && record.RegionId == region.RegionId;
    }

    /// <summary>운영 중인데 전문 해달을 아직 배치하지 않은 지역인지 (그 장소는 "광장에서 만나 배치해 주세요" 안내)</summary>
    public static bool IsWaitingForSpecialist(DevelopableRegionDefinition region, SettlementOtterDefinition specialist, Settlement settlement)
    {
        return specialist != null
            && GetRegionState(region, settlement) == RegionProgressState.Operational
            && settlement.GetSpecialistState(specialist.OtterId) < SpecialistState.Assigned;
    }

    /// <summary>광장에 나와야 하는 전문 해달인지: 찾아왔고(처지가 있음) 아직 배치하지 않음</summary>
    public static bool IsVisitingPlaza(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        return otter.IsSpecialist
            && settlement.TryGetResidentState(otter.OtterId, out _)
            && settlement.GetSpecialistState(otter.OtterId) < SpecialistState.Assigned;
    }

    /// <summary>광장에서 처음 만남: 만남 기록을 남기고, 전문 해달이면 광장에 와 있는 처지로</summary>
    /// <returns>처음 만났으면 true</returns>
    public static bool Meet(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (otter.IsSpecialist && IsVisitingPlaza(otter, settlement))
            settlement.AdvanceSpecialist(otter.OtterId, SpecialistState.AtPlaza);
        return settlement.MarkMet(otter.OtterId);
    }

    /// <summary>광장에 와 있는 전문 해달을 일할 지역에 배치할 수 있는지 (지역이 운영 중이어야 함)</summary>
    public static bool CanAssign(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        return otter.IsSpecialist
            && settlement.GetSpecialistState(otter.OtterId) == SpecialistState.AtPlaza
            && GetRegionState(otter.WorkRegion, settlement) == RegionProgressState.Operational;
    }

    /// <summary>배치: 처지를 Assigned로 확정하고 주민이 된다 (생산은 그 장소의 안내를 끝낸 뒤)</summary>
    /// <returns>이번에 배치했으면 true (이미 배치했거나 조건이 안 되면 false)</returns>
    public static bool Assign(SettlementOtterDefinition otter, Settlement settlement)
    {
        if (!CanAssign(otter, settlement))
            return false;
        settlement.AdvanceSpecialist(otter.OtterId, SpecialistState.Assigned, otter.WorkRegion.RegionId);
        settlement.SetResident(otter.OtterId, ResidentState.Resident);
        return true;
    }

    /// <summary>배치된 전문 해달이 일하기 시작함 (그 지역의 생산 발전이 열림)</summary>
    /// <returns>이번에 시작했으면 true</returns>
    public static bool StartWork(SettlementOtterDefinition otter, Settlement settlement, long nowUtcTicks)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (!otter.IsSpecialist || settlement.GetSpecialistState(otter.OtterId) != SpecialistState.Assigned)
            return false;
        settlement.AdvanceSpecialist(otter.OtterId, SpecialistState.Working, otter.WorkRegion.RegionId, nowUtcTicks);
        if (IsSet(otter.WorkRegion.ProductionDevelopment))
            settlement.UnlockDevelopment(otter.WorkRegion.ProductionDevelopment);
        return true;
    }

    /// <summary>
    /// 전문 해달이 생기기 전 세이브: 이미 운영 중인 지역의 전문 해달은 바로 일하는 중으로 (하던 생산을 멈추지 않게).
    /// - 그 해달이 아직 오지 않았는데 지역은 운영 중 (옛 세이브)
    /// - 그 해달의 배치 부탁이 이미 끝나 있음 (정착 진행 전 세이브는 모든 부탁을 끝낸 것으로 불러옴)
    /// 새 흐름에서 찾아왔지만 아직 만나지 않은 해달은 그대로 둔다
    /// </summary>
    /// <returns>옮겼으면 true</returns>
    public static bool MigrateLegacy(SettlementOtterDefinition otter, bool assignRequestCompleted, Settlement settlement, long nowUtcTicks)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (!otter.IsSpecialist || settlement.GetSpecialistState(otter.OtterId) >= SpecialistState.Assigned
            || GetRegionState(otter.WorkRegion, settlement) != RegionProgressState.Operational)
            return false;
        bool arrived = settlement.TryGetResidentState(otter.OtterId, out _);
        if (arrived && !assignRequestCompleted)
            return false;

        settlement.SetResident(otter.OtterId, ResidentState.Resident);
        settlement.MarkMet(otter.OtterId);
        settlement.AdvanceSpecialist(otter.OtterId, SpecialistState.Working, otter.WorkRegion.RegionId, nowUtcTicks);
        if (IsSet(otter.WorkRegion.ProductionDevelopment))
            settlement.UnlockDevelopment(otter.WorkRegion.ProductionDevelopment);
        return true;
    }

    #endregion

    private static bool IsSet(string developmentId) => !string.IsNullOrEmpty(developmentId);
}
