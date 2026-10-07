using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 영토 확장: 광장 가장자리의 점선 원 → 주민 해달 파견 → 숲 개간(시간, 목재를 많이) → 3번 끝내면 마을회관에 영토 확장 미션
/// → 재료 납품 → 그 땅이 열림 (걷기 영역·카메라 범위). 규칙은 TerritoryRules, 진행 기록은 발전과 끝낸 작업이라 세이브 형식은 그대로.
/// - 숲 개간 = 광장 주민 작업 (기존 작업 화면·노동력·오프라인 진행). 시작 조건만 여기서 더 본다 (기회 · 한 번에 한 곳)
/// - 영토 확장 미션 = 단계 없는 공동사업 (기존 납품 규칙·화면·보상 기록). 공동사업 순서와 따로 열린다
/// </summary>
public partial class SettlementManager
{
    /// <summary>P3의 광장 동쪽 확장 (지금은 없음). 이 발전이 있는 세이브는 서쪽 1단계를 넓힌 것으로 옮김</summary>
    public const string LegacyPlazaExpandDevelopment = "plaza_expand_01";

    private int _territoryVersion;
    private readonly List<CommunityProjectDefinition> _territoryMissions = new List<CommunityProjectDefinition>();
    private readonly Dictionary<string, string> _clearingRewardText = new Dictionary<string, string>();

    /// <summary>땅을 넓혔을 때 (광장이 새 땅을 비춤)</summary>
    public event Action<TerritoryExpansionDefinition> OnTerritoryExpanded;

    /// <summary>영토 표시를 다시 그려야 할 때마다 바뀜 (정착 기록·레벨이 바뀌면). 광장의 점선 원·카메라 범위가 봄</summary>
    public int TerritoryVersion => _territoryVersion;

    #region 조회

    public bool IsTerritoryTask(SettlementTaskDefinition task) => TerritoryRules.FindByTask(_config, task, out _) != null;

    public bool IsTerritoryMission(CommunityProjectDefinition project) => TerritoryRules.FindByMission(_config, project) != null;

    /// <summary>그 방향에서 지금 손댈 단계 (모두 넓혔으면 null)</summary>
    public TerritoryExpansionDefinition CurrentTerritory(TerritoryDirection direction) =>
        TerritoryRules.Current(_config, Settlement, direction);

    public TerritoryStage GetTerritoryStage(TerritoryExpansionDefinition territory) =>
        TerritoryRules.GetStage(territory, _config, Settlement);

    /// <summary>그 방향으로 지금 개간을 시작할 수 없는 이유 (None이면 시작할 수 있음)</summary>
    public TerritoryBlock GetTerritoryBlock(TerritoryDirection direction) =>
        TerritoryRules.GetBlock(_config, Settlement, PlayerLevel, direction);

    /// <summary>남은 개간 기회</summary>
    public int TerritoryChances => TerritoryRules.AvailableChances(_config, Settlement, PlayerLevel);

    public int TerritoryClearingsDone(TerritoryExpansionDefinition territory) => TerritoryRules.ClearingsDone(territory, Settlement);

    /// <summary>그 단계의 다음 숲 개간 작업 (다 끝냈으면 null)</summary>
    public SettlementTaskDefinition NextTerritoryClearing(TerritoryExpansionDefinition territory)
    {
        if (territory == null)
            return null;
        int index = TerritoryRules.NextClearingIndex(territory, Settlement);
        return index >= 0 ? territory.Clearings[index].Task : null;
    }

    /// <summary>진행 중인 숲 개간 (없으면 null)</summary>
    public TerritoryExpansionDefinition WorkingTerritory(out SettlementTaskDefinition task)
    {
        var territory = TerritoryRules.Working(_config, Settlement, out int index);
        task = territory != null ? territory.Clearings[index].Task : null;
        return territory;
    }

    /// <summary>지금 재료를 넣을 수 있는 영토 확장 미션들 (숲 개간을 다 끝낸 단계. 서쪽 먼저)</summary>
    public IReadOnlyList<CommunityProjectDefinition> OpenTerritoryMissions
    {
        get
        {
            _territoryMissions.Clear();
            foreach (TerritoryDirection direction in Enum.GetValues(typeof(TerritoryDirection)))
            {
                var territory = CurrentTerritory(direction);
                if (territory != null && territory.Mission != null && GetTerritoryStage(territory) == TerritoryStage.Cleared)
                    _territoryMissions.Add(territory.Mission);
            }
            return _territoryMissions;
        }
    }

    /// <summary>개간 기회 안내 한 줄 (마을회관)</summary>
    public string TerritoryChanceText()
    {
        int chances = TerritoryChances;
        var working = WorkingTerritory(out var task);
        if (working != null)
            return $"{task.Title} 중이에요.";
        if (PlayerLevel < _config.TerritoryStartLevel)
            return $"Lv.{_config.TerritoryStartLevel}부터 광장 가장자리의 숲을 개간할 수 있어요.";
        return chances > 0
            ? $"개간 기회 {chances}번 · 광장 가장자리의 점선 원을 눌러 해달을 보내요."
            : "레벨이 오르면 개간 기회가 생겨요.";
    }

    /// <summary>개간이 끝났을 때 작업 화면이 띄울 말 (영토 개간이 아니면 null)</summary>
    public string TerritoryDoneMessage(SettlementTaskDefinition task)
    {
        var territory = TerritoryRules.FindByTask(_config, task, out int index);
        if (territory == null)
            return null;
        int total = territory.Clearings.Count;
        var text = new StringBuilder();
        text.Append(index + 1 >= total
            ? $"{territory.DisplayName} 개간을 모두 마쳤어요!"
            : $"{territory.DisplayName} 개간 {index + 1}/{total} 끝!");
        if (_clearingRewardText.TryGetValue(task.TaskId, out string reward) && !string.IsNullOrEmpty(reward))
            text.Append('\n').Append(reward);
        if (index + 1 >= total)
            text.Append("\n마을 길드에서 영토 확장 미션을 할 수 있어요.");
        return text.ToString();
    }

    #endregion

    #region 개간 · 미션 처리

    // 주민 작업 상태에 영토 조건을 더함: 시작할 수 있는 숲 개간만 Available
    private SettlementTaskState TerritoryTaskState(SettlementTaskDefinition task, SettlementTaskState state)
    {
        if (state != SettlementTaskState.Available || !IsTerritoryTask(task))
            return state;
        return TerritoryRules.CanStart(task, _config, Settlement, PlayerLevel) ? state : SettlementTaskState.Locked;
    }

    // 숲 개간이 끝남: 재료(목재)를 가방에 → 3번 다 끝냈으면 미션이 열림
    private void FinishTerritoryClearing(SettlementTaskDefinition task)
    {
        var territory = TerritoryRules.FindByTask(_config, task, out int index);
        if (territory == null)
            return;
        var text = new StringBuilder();
        var bag = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        foreach (var reward in territory.Clearings[index].Rewards)
        {
            if (reward == null || reward.Item == null || reward.Amount <= 0)
                continue;
            // 개간 목재 효과(목공소, KingdomBonus)
            int amount = KingdomBonus.Amount(KingdomBonusKind.ClearingWood, reward.Amount);
            int added = bag != null ? bag.Add(reward.Item, amount, ItemChangeReason.Clearing) : 0;
            if (text.Length > 0)
                text.Append(" · ");
            text.Append(reward.Item.DisplayName).Append(" +").Append(added);
            if (bag != null && added < amount)
                text.Append($" (가방이 가득해 {amount - added}개는 못 받았어요)");
        }
        _clearingRewardText[task.TaskId] = text.ToString();
        TerritoryRules.Sync(_config, Settlement);
    }

    // 다 낸 영토 확장 미션을 끝내고 (결과 발전 = 땅) 칸 발전을 맞춘다. 불러올 때(silent)는 알림 없이
    private void UpdateTerritory(bool silent)
    {
        foreach (var territory in _config.Territories)
        {
            var mission = territory != null ? territory.Mission : null;
            if (mission == null || GetTerritoryStage(territory) != TerritoryStage.Cleared)
                continue;
            if (CommunityProjectRules.IsDelivered(mission, Settlement) && !Settlement.HasDevelopment(mission.PaidDevelopment))
                Settlement.UnlockDevelopment(mission.PaidDevelopment);
            if (!CommunityProjectRules.IsReadyToComplete(mission, Settlement, PlayerLevel))
                continue;
            CompleteProject(mission, silent);
            TerritoryRules.Sync(_config, Settlement);
            if (!silent)
                OnTerritoryExpanded?.Invoke(territory);
        }
        TerritoryRules.Sync(_config, Settlement);
    }

    // P3의 동쪽 확장을 끝낸 세이브: 그 땅이 없어졌으므로 서쪽 1단계를 넓힌 것으로 (새 이웃의 집도 서쪽으로). 보상은 다시 주지 않음
    private void MigrateTerritory()
    {
        if (!Settlement.HasDevelopment(LegacyPlazaExpandDevelopment))
            return;
        var first = TerritoryRules.InDirection(_config, TerritoryDirection.West);
        if (first.Count == 0 || TerritoryRules.IsExpanded(first[0], Settlement))
            return;
        var territory = first[0];
        for (int i = 0; i < territory.Clearings.Count; i++)
            Settlement.UnlockDevelopment(territory.ClearingDevelopment(i + 1));
        if (territory.Mission != null)
        {
            Settlement.MarkProjectCompleted(territory.Mission.ProjectId);
            Settlement.AddReward(CommunityProjectRules.RewardKey(territory.Mission, 0));
            if (!string.IsNullOrEmpty(territory.Mission.PaidDevelopment))
                Settlement.UnlockDevelopment(territory.Mission.PaidDevelopment);
        }
        Settlement.UnlockDevelopment(territory.ExpandedDevelopment);
        TerritoryRules.Sync(_config, Settlement);
        Debug.Log("[SettlementManager] 광장 동쪽 확장을 끝낸 세이브: 서쪽 1단계를 넓힌 것으로 옮겼습니다.");
    }

    #endregion
}
