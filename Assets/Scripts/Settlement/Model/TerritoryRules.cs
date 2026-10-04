using System;
using System.Collections.Generic;

/// <summary>영토 한 단계가 어디까지 왔는지</summary>
public enum TerritoryStage
{
    Locked,   // 같은 방향의 앞 단계를 아직 넓히지 않음
    Clearing, // 숲 개간 중 (0~2번 끝냄)
    Cleared,  // 숲 개간을 다 끝냄 → 마을회관의 영토 확장 미션을 기다림
    Expanded, // 땅이 열림
}

/// <summary>지금 그 방향으로 숲 개간을 시작할 수 없는 이유</summary>
public enum TerritoryBlock
{
    None,          // 시작할 수 있음
    NoChance,      // 개간 기회가 없음 (레벨이 오를 때마다 한 번)
    OtherClearing, // 다른 곳을 개간하는 중 (한 번에 한 곳만)
    Mission,       // 숲 개간은 다 끝남 → 영토 확장 미션 차례
    AllDone,       // 이 방향은 모두 넓힘
}

/// <summary>
/// 영토 확장 규칙 (데이터 + 상태 → 결과). 진행의 원본은 발전 · 끝낸 작업 기록이라 따로 저장하지 않는다.
/// - 개간 기회: 왕국 Lv.(시작 레벨)에 1번, 그 뒤 레벨이 오를 때마다 1번씩 쌓임. 시작한 개간(진행 중 포함)만큼 씀
/// - 개간은 한 번에 한 곳만 (북쪽을 개간하는 동안 서쪽은 못 함). 방향은 기회가 생길 때마다 고름
/// - 한 단계 = 개간 3번 → 영토 확장 미션 → 땅이 열림. 같은 방향의 다음 단계는 그 뒤에
/// - 열린 땅은 칸으로 나뉜다: (서쪽 단계 i, 북쪽 단계 j) 칸은 서쪽을 i단계, 북쪽을 j단계까지 넓히면 열림 → 칸마다 발전
/// </summary>
public static class TerritoryRules
{
    /// <summary>어느 방향이든 처음으로 땅을 넓힘 (새 이웃 맞이하기가 여기서 열림)</summary>
    public const string FirstDevelopment = "territory_first";

    /// <summary>처음 넓힌 방향 (새 이웃의 집 자리가 그 땅에 생김)</summary>
    public static string HomeDevelopment(TerritoryDirection direction) =>
        direction == TerritoryDirection.West ? "territory_home_west" : "territory_home_north";

    /// <summary>지도 칸 (서쪽 단계, 북쪽 단계)이 열린 발전. (0, 0) = 처음 광장</summary>
    public static string CellDevelopment(int westTier, int northTier) => $"territory_cell_{westTier}_{northTier}";

    #region 찾기

    /// <summary>그 방향의 단계들 (단계 순)</summary>
    public static List<TerritoryExpansionDefinition> InDirection(SettlementConfig config, TerritoryDirection direction)
    {
        var list = new List<TerritoryExpansionDefinition>();
        foreach (var territory in config.Territories)
        {
            if (territory != null && territory.Direction == direction && !string.IsNullOrEmpty(territory.ExpansionId))
                list.Add(territory);
        }
        list.Sort((a, b) => a.Tier.CompareTo(b.Tier));
        return list;
    }

    /// <summary>이 작업이 어느 영토의 몇 번째 개간인지 (아니면 null)</summary>
    public static TerritoryExpansionDefinition FindByTask(SettlementConfig config, SettlementTaskDefinition task, out int index)
    {
        index = -1;
        if (task == null)
            return null;
        foreach (var territory in config.Territories)
        {
            if (territory == null)
                continue;
            for (int i = 0; i < territory.Clearings.Count; i++)
            {
                var step = territory.Clearings[i];
                if (step != null && step.Task != null && step.Task.TaskId == task.TaskId)
                {
                    index = i;
                    return territory;
                }
            }
        }
        return null;
    }

    /// <summary>이 사업이 영토 확장 미션이면 그 영토 (아니면 null)</summary>
    public static TerritoryExpansionDefinition FindByMission(SettlementConfig config, CommunityProjectDefinition project)
    {
        if (project == null)
            return null;
        foreach (var territory in config.Territories)
        {
            if (territory != null && territory.Mission != null && territory.Mission.ProjectId == project.ProjectId)
                return territory;
        }
        return null;
    }

    #endregion

    #region 단계

    public static bool IsExpanded(TerritoryExpansionDefinition territory, Settlement settlement) =>
        !string.IsNullOrEmpty(territory.ExpansionId) && settlement.HasDevelopment(territory.ExpandedDevelopment);

    /// <summary>그 개간을 끝냈는지 (끝낸 작업 기록 또는 결과 발전 — 옮긴 세이브는 발전만 있음)</summary>
    public static bool IsClearingDone(TerritoryExpansionDefinition territory, int index, Settlement settlement)
    {
        var step = territory.Clearings[index];
        if (step == null || step.Task == null)
            return true;
        return settlement.IsTaskCompleted(step.Task.TaskId) || settlement.HasDevelopment(territory.ClearingDevelopment(index + 1));
    }

    public static int ClearingsDone(TerritoryExpansionDefinition territory, Settlement settlement)
    {
        int done = 0;
        for (int i = 0; i < territory.Clearings.Count; i++)
        {
            if (IsClearingDone(territory, i, settlement))
                done++;
        }
        return done;
    }

    /// <summary>다음에 할 개간 (다 끝냈으면 -1)</summary>
    public static int NextClearingIndex(TerritoryExpansionDefinition territory, Settlement settlement)
    {
        for (int i = 0; i < territory.Clearings.Count; i++)
        {
            if (!IsClearingDone(territory, i, settlement))
                return i;
        }
        return -1;
    }

    public static TerritoryStage GetStage(TerritoryExpansionDefinition territory, SettlementConfig config, Settlement settlement)
    {
        if (territory == null)
            throw new ArgumentNullException(nameof(territory));
        if (IsExpanded(territory, settlement))
            return TerritoryStage.Expanded;
        foreach (var earlier in InDirection(config, territory.Direction))
        {
            if (earlier.Tier < territory.Tier && !IsExpanded(earlier, settlement))
                return TerritoryStage.Locked;
        }
        return NextClearingIndex(territory, settlement) < 0 ? TerritoryStage.Cleared : TerritoryStage.Clearing;
    }

    /// <summary>그 방향에서 지금 손댈 단계 (넓히지 않은 첫 단계. 모두 넓혔으면 null)</summary>
    public static TerritoryExpansionDefinition Current(SettlementConfig config, Settlement settlement, TerritoryDirection direction)
    {
        foreach (var territory in InDirection(config, direction))
        {
            if (!IsExpanded(territory, settlement))
                return territory;
        }
        return null;
    }

    /// <summary>그 방향으로 몇 단계까지 넓혔는지 (1단계부터 차례로)</summary>
    public static int ExpandedTier(SettlementConfig config, Settlement settlement, TerritoryDirection direction)
    {
        int tier = 0;
        foreach (var territory in InDirection(config, direction))
        {
            if (!IsExpanded(territory, settlement))
                break;
            tier = territory.Tier;
        }
        return tier;
    }

    #endregion

    #region 개간 기회 · 시작

    /// <summary>지금까지 생긴 개간 기회 (시작 레벨에 1번, 레벨이 오를 때마다 1번)</summary>
    public static int EarnedChances(SettlementConfig config, int playerLevel) =>
        Math.Max(0, playerLevel - config.TerritoryStartLevel + 1);

    /// <summary>쓴 개간 기회 = 시작한 개간 (끝냈거나 하는 중)</summary>
    public static int UsedChances(SettlementConfig config, Settlement settlement)
    {
        int used = 0;
        foreach (var territory in config.Territories)
        {
            if (territory == null)
                continue;
            for (int i = 0; i < territory.Clearings.Count; i++)
            {
                var step = territory.Clearings[i];
                if (step == null || step.Task == null)
                    continue;
                if (IsClearingDone(territory, i, settlement) || settlement.TryGetTaskJob(step.Task.TaskId, out _))
                    used++;
            }
        }
        return used;
    }

    public static int AvailableChances(SettlementConfig config, Settlement settlement, int playerLevel) =>
        Math.Max(0, EarnedChances(config, playerLevel) - UsedChances(config, settlement));

    /// <summary>지금 진행 중인 숲 개간 (없으면 null)</summary>
    public static TerritoryExpansionDefinition Working(SettlementConfig config, Settlement settlement, out int index)
    {
        index = -1;
        foreach (var territory in config.Territories)
        {
            if (territory == null)
                continue;
            for (int i = 0; i < territory.Clearings.Count; i++)
            {
                var step = territory.Clearings[i];
                if (step != null && step.Task != null && settlement.TryGetTaskJob(step.Task.TaskId, out _))
                {
                    index = i;
                    return territory;
                }
            }
        }
        return null;
    }

    /// <summary>그 방향으로 지금 개간을 시작할 수 있는지 (안 되면 이유)</summary>
    public static TerritoryBlock GetBlock(SettlementConfig config, Settlement settlement, int playerLevel, TerritoryDirection direction)
    {
        var current = Current(config, settlement, direction);
        if (current == null)
            return TerritoryBlock.AllDone;
        if (GetStage(current, config, settlement) == TerritoryStage.Cleared)
            return TerritoryBlock.Mission;
        if (Working(config, settlement, out _) != null)
            return TerritoryBlock.OtherClearing;
        return AvailableChances(config, settlement, playerLevel) > 0 ? TerritoryBlock.None : TerritoryBlock.NoChance;
    }

    /// <summary>이 작업(숲 개간)을 지금 시작할 수 있는지: 그 방향의 다음 개간 · 기회가 있음 · 다른 개간이 없음</summary>
    public static bool CanStart(SettlementTaskDefinition task, SettlementConfig config, Settlement settlement, int playerLevel)
    {
        var territory = FindByTask(config, task, out int index);
        if (territory == null || territory != Current(config, settlement, territory.Direction))
            return false;
        if (GetStage(territory, config, settlement) != TerritoryStage.Clearing || NextClearingIndex(territory, settlement) != index)
            return false;
        return GetBlock(config, settlement, playerLevel, territory.Direction) == TerritoryBlock.None;
    }

    #endregion

    #region 기록 맞추기

    /// <summary>
    /// 기록에서 따라오는 발전을 연다 (여러 번 해도 같음): 개간을 다 끝낸 단계의 미션 열림, 넓힌 단계로 열리는 칸,
    /// 처음 넓힘, 새 이웃 집 자리 방향 (한 방향만 넓혔으면 그쪽, 옮긴 세이브처럼 둘 다면 서쪽)
    /// </summary>
    /// <returns>무엇이든 열었으면 true</returns>
    public static bool Sync(SettlementConfig config, Settlement settlement)
    {
        bool changed = false;
        foreach (var territory in config.Territories)
        {
            if (territory == null || string.IsNullOrEmpty(territory.ExpansionId) || territory.Clearings.Count == 0)
                continue;
            if (NextClearingIndex(territory, settlement) < 0)
                changed |= Unlock(settlement, territory.ClearedDevelopment);
        }

        int west = ExpandedTier(config, settlement, TerritoryDirection.West);
        int north = ExpandedTier(config, settlement, TerritoryDirection.North);
        for (int i = 0; i <= west; i++)
        {
            for (int j = 0; j <= north; j++)
            {
                if (i > 0 || j > 0)
                    changed |= Unlock(settlement, CellDevelopment(i, j));
            }
        }
        if (west + north > 0)
        {
            changed |= Unlock(settlement, FirstDevelopment);
            if (!settlement.HasDevelopment(HomeDevelopment(TerritoryDirection.West))
                && !settlement.HasDevelopment(HomeDevelopment(TerritoryDirection.North)))
                changed |= Unlock(settlement, HomeDevelopment(west > 0 ? TerritoryDirection.West : TerritoryDirection.North));
        }
        return changed;
    }

    private static bool Unlock(Settlement settlement, string development)
    {
        if (settlement.HasDevelopment(development))
            return false;
        settlement.UnlockDevelopment(development);
        return true;
    }

    #endregion
}
