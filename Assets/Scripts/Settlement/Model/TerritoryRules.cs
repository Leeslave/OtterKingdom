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
    OtherDirection, // 다른 방향을 개간하다 멈춤 → 그 방향 3번을 먼저 (한 땅씩 차례로)
    Mission,       // 숲 개간은 다 끝남 → 영토 확장 미션 차례
    AllDone,       // 이 방향은 모두 넓힘
    NeedsScouting,
}

/// <summary>
/// 영토 확장 규칙 (데이터 + 상태 → 결과). 진행의 원본은 발전 · 끝낸 작업 기록이라 따로 저장하지 않는다.
/// - 개간 기회: 왕국 Lv.(시작 레벨)에 1번, 그 뒤 레벨이 오를 때마다 1번씩 쌓임 (TerritoryBonusChanceUntilLevel까지는 2번). 시작한 개간(진행 중 포함)만큼 씀
/// - 개간은 한 번에 한 곳만 (북쪽을 개간하는 동안 서쪽은 못 함). 한 방향을 시작하면 그 땅의 개간을 다 마칠 때까지 그 방향만
///   (여러 방향에 흩어 놓아 땅이 하나도 안 열리는 일이 없게. 옛 세이브처럼 이미 흩어져 있으면 많이 한 방향부터)
/// - 한 단계 = 개간 N번 → 영토 확장 미션 → 땅이 열림. 같은 방향의 다음 단계는 그 뒤에
/// - 방향 = 중앙 공터 둘레의 구역 블록 하나, 단계 = 그 블록이 커지는 단계 (땅 · 걷기 영역 · 카메라 · 부지 칸이 그 단계의 발전으로 열림)
/// </summary>
public static class TerritoryRules
{
    /// <summary>어느 방향이든 처음으로 땅을 넓힘 (새 이웃 맞이하기가 여기서 열림)</summary>
    public const string FirstDevelopment = "territory_first";
    public const string TutorialReady = "territory_tutorial_ready";
    public const string TutorialDone = "territory_tutorial_done";
    public const string TutorialExpansion = "territory_ne_1";
    /// <summary>첫 개간(오른쪽 숲 한 번 = 땅이 열림, 기회를 쓰지 않음)이 열리는 왕국 레벨. 일반 개간 기회는 그 뒤 SettlementConfig.TerritoryStartLevel부터</summary>
    public const int TutorialLevel = 6;

    public static bool IsTutorial(TerritoryExpansionDefinition territory, Settlement settlement) =>
        territory != null && territory.ExpansionId == TutorialExpansion && settlement.HasDevelopment(TutorialReady);

    // 왕국 Lv.6 + 게시판 보강(마을회관) 뒤에 열린다. 이미 개간을 시작한 저장은 기존 진행·사용 기회를 그대로 유지한다.
    public static bool UnlockTutorial(SettlementConfig config, Settlement settlement, int playerLevel)
    {
        if (playerLevel < TutorialLevel || settlement.HasDevelopment(TutorialReady) || !settlement.IsCompleted("req_upgrade_board")
            || settlement.HasDevelopment(FirstDevelopment) || UsedChances(config, settlement) != 0)
            return false;
        foreach (var territory in config.Territories)
            if (territory != null && territory.ExpansionId == TutorialExpansion && !IsExpanded(territory, settlement))
                return Unlock(settlement, TutorialReady);
        return false;
    }

    /// <summary>첫 개간 한 번으로 오른쪽 땅을 연다. 생략한 숲 조각도 함께 걷어 내며 반복 호출은 무효.</summary>
    public static TerritoryExpansionDefinition CompleteTutorial(SettlementConfig config, Settlement settlement)
    {
        if (!settlement.HasDevelopment(TutorialReady) || settlement.HasDevelopment(TutorialDone)) return null;
        foreach (var territory in config.Territories)
        {
            if (!IsTutorial(territory, settlement) || territory.Clearings.Count == 0 || !IsClearingDone(territory, 0, settlement)) continue;
            settlement.UnlockDevelopment(TutorialDone);
            for (int i = 0; i < territory.Clearings.Count; i++)
                settlement.UnlockDevelopment(territory.ClearingDevelopment(i + 1));
            settlement.UnlockDevelopment(territory.ClearedDevelopment);
            settlement.UnlockDevelopment(territory.ExpandedDevelopment);
            Sync(config, settlement);
            return territory;
        }
        return null;
    }

    /// <summary>처음 넓힌 방향을 고를 때 여러 방향이 한꺼번에 열린 세이브(옮긴 세이브)에서 앞에 두는 순서</summary>
    private static readonly TerritoryDirection[] HomePreference =
    {
        TerritoryDirection.NorthEast, TerritoryDirection.South, TerritoryDirection.West, TerritoryDirection.NorthWest, TerritoryDirection.East,
        TerritoryDirection.North,
    };

    /// <summary>방향의 짧은 이름 (발전 ID에 씀: territory_ne_1 등)</summary>
    public static string DirectionKey(TerritoryDirection direction)
    {
        switch (direction)
        {
            case TerritoryDirection.NorthEast: return "ne";
            case TerritoryDirection.South: return "s";
            case TerritoryDirection.NorthWest: return "nw";
            case TerritoryDirection.West: return "w";
            case TerritoryDirection.East: return "e";
            default: return "north";
        }
    }

    /// <summary>처음 넓힌 방향 (새 이웃의 집 자리가 그 땅에 생김)</summary>
    public static string HomeDevelopment(TerritoryDirection direction) => $"territory_home_{DirectionKey(direction)}";

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

    /// <summary>지금까지 생긴 개간 기회 (시작 레벨에 1번, 레벨이 오를 때마다 1번. 보너스 레벨까지는 레벨마다 1번 더)</summary>
    public static int EarnedChances(SettlementConfig config, int playerLevel)
    {
        int start = config.TerritoryStartLevel;
        int bonus = Math.Max(0, Math.Min(playerLevel, config.TerritoryBonusChanceUntilLevel) - start + 1);
        return Math.Max(0, playerLevel - start + 1) + bonus;
    }

    /// <summary>쓴 개간 기회 = 시작한 개간 (끝냈거나 하는 중)</summary>
    public static int UsedChances(SettlementConfig config, Settlement settlement)
    {
        int used = 0;
        foreach (var territory in config.Territories)
        {
            if (territory == null || IsTutorial(territory, settlement))
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
        ScoutingRules.UsesScouting(settlement) ? ScoutedClearings(config, settlement)
            : Math.Max(0, EarnedChances(config, playerLevel) - UsedChances(config, settlement));

    private static int ScoutedClearings(SettlementConfig config, Settlement settlement)
    {
        int count = 0;
        foreach (var territory in config.Territories)
            if (territory != null && !IsExpanded(territory, settlement)
                && ScoutingRules.CanClear(config, settlement, territory))
                count += Math.Max(0, territory.Clearings.Count - ClearingsDone(territory, settlement));
        return count;
    }

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

    /// <summary>
    /// 개간을 시작했지만 다 마치지 않은 땅 (그 방향만 이어서 개간할 수 있음). 여러 곳이면 많이 한 곳 → 앞 방향 순. 없으면 null
    /// </summary>
    public static TerritoryExpansionDefinition Started(SettlementConfig config, Settlement settlement)
    {
        TerritoryExpansionDefinition best = null;
        int bestDone = 0;
        foreach (var direction in HomePreference)
        {
            var territory = Current(config, settlement, direction);
            if (territory == null || IsTutorial(territory, settlement) || GetStage(territory, config, settlement) != TerritoryStage.Clearing)
                continue;
            int done = ClearingsDone(territory, settlement);
            if (done > bestDone)
            {
                best = territory;
                bestDone = done;
            }
        }
        return best;
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
        var started = Started(config, settlement);
        if (started != null && started != current)
            return TerritoryBlock.OtherDirection;
        if (IsTutorial(current, settlement) && !settlement.HasDevelopment(TutorialDone))
            return TerritoryBlock.None;
        if (!ScoutingRules.CanClear(config, settlement, current))
            return TerritoryBlock.NeedsScouting;
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
    /// 기록에서 따라오는 발전을 연다 (여러 번 해도 같음): 개간을 다 끝낸 단계의 미션 열림, 처음 넓힘,
    /// 새 이웃 집 자리 방향 (처음 넓힌 쪽. 옮긴 세이브처럼 여러 방향이면 북동 → 남 → 서 → 북서 순)
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

        TerritoryDirection? first = null;
        bool hasHome = false;
        foreach (var direction in HomePreference)
        {
            hasHome |= settlement.HasDevelopment(HomeDevelopment(direction));
            if (first == null && ExpandedTier(config, settlement, direction) > 0)
                first = direction;
        }
        if (first != null)
        {
            changed |= Unlock(settlement, FirstDevelopment);
            if (!hasHome)
                changed |= Unlock(settlement, HomeDevelopment(first.Value));
        }
        return changed;
    }

    /// <summary>아직 넓힐 단계가 남은 방향이 있는지</summary>
    public static bool AnyLeft(SettlementConfig config, Settlement settlement)
    {
        foreach (var territory in config.Territories)
        {
            if (territory != null && !string.IsNullOrEmpty(territory.ExpansionId) && !IsExpanded(territory, settlement))
                return true;
        }
        return false;
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
