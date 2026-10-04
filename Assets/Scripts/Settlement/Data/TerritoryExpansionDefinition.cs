using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>영토를 넓히는 방향 (광장은 지도의 남동쪽 끝에서 시작해 서쪽·북쪽으로 넓어진다)</summary>
public enum TerritoryDirection
{
    West = 0,
    North = 1,
}

/// <summary>숲 개간 한 번: 주민 해달을 보내는 작업 + 끝나면 받는 재료 (나무를 많이)</summary>
[Serializable]
public class TerritoryClearingStep
{
    [Tooltip("주민 작업 (필요 해달 수 · 시간 · 결과 발전 = {영토ID}_c{번호})")]
    [SerializeField] private SettlementTaskDefinition _task;

    [Tooltip("개간이 끝나면 가방에 넣어 주는 재료")]
    [SerializeField] private List<ItemAmount> _rewards = new List<ItemAmount>();

    public SettlementTaskDefinition Task => _task;
    public IReadOnlyList<ItemAmount> Rewards => _rewards;

    public TerritoryClearingStep() { }

    public TerritoryClearingStep(SettlementTaskDefinition task, IEnumerable<ItemAmount> rewards)
    {
        _task = task;
        _rewards = rewards != null ? new List<ItemAmount>(rewards) : new List<ItemAmount>();
    }
}

/// <summary>
/// 영토 확장 한 단계 (예: 서쪽 1단계). 진행: 숲 개간 3번(왕국 레벨이 오를 때마다 한 번씩 할 수 있음)
/// → 마을회관에 영토 확장 미션(재료 납품, 공동사업과 같은 납품 규칙) → 그 땅이 열리고 걷기 영역·카메라 범위가 넓어진다.
/// 같은 방향의 다음 단계는 앞 단계를 넓힌 뒤에 열린다. 진행은 모두 발전 기록으로 남는다 (세이브 형식을 바꾸지 않음).
/// </summary>
[CreateAssetMenu(fileName = "Territory", menuName = "Game Data/Settlement/Territory Expansion")]
public class TerritoryExpansionDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("ID (예: territory_west_1). 넓힌 뒤의 발전 ID와 같음")]
    [SerializeField] private string _expansionId;

    [SerializeField] private TerritoryDirection _direction;

    [Tooltip("이 방향의 몇 번째 단계인지 (1부터)")]
    [Min(1)]
    [SerializeField] private int _tier = 1;

    [Header("내용")]
    [Tooltip("땅 이름 (예: 서쪽 숲)")]
    [SerializeField] private string _displayName;

    [Header("숲 개간 (차례대로)")]
    [SerializeField] private List<TerritoryClearingStep> _clearings = new List<TerritoryClearingStep>();

    [Header("영토 확장 미션 (마을회관)")]
    [Tooltip("재료 납품 (단계 없음). 필요 발전 = ClearedDevelopment, 결과 발전 = ExpansionId")]
    [SerializeField] private CommunityProjectDefinition _mission;

    public string ExpansionId => _expansionId;
    public TerritoryDirection Direction => _direction;
    public int Tier => Mathf.Max(1, _tier);
    public string DisplayName => _displayName;
    public IReadOnlyList<TerritoryClearingStep> Clearings => _clearings;
    public CommunityProjectDefinition Mission => _mission;

    /// <summary>숲 개간을 모두 끝냈을 때 열리는 발전 (미션이 열리는 조건)</summary>
    public string ClearedDevelopment => _expansionId + "_cleared";

    /// <summary>땅을 넓혔을 때 열리는 발전 (= 미션의 결과 발전)</summary>
    public string ExpandedDevelopment => _expansionId;

    /// <summary>개간 한 번의 결과 발전 (번호는 1부터)</summary>
    public string ClearingDevelopment(int number) => $"{_expansionId}_c{number}";

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string expansionId, TerritoryDirection direction, int tier, string displayName,
        IEnumerable<TerritoryClearingStep> clearings, CommunityProjectDefinition mission)
    {
        _expansionId = expansionId;
        _direction = direction;
        _tier = tier;
        _displayName = displayName;
        _clearings = clearings != null ? new List<TerritoryClearingStep>(clearings) : new List<TerritoryClearingStep>();
        _mission = mission;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_expansionId))
            Debug.LogWarning($"[{name}] ExpansionId가 비어 있습니다.", this);
        if (_mission != null && _mission.ResultDevelopment != _expansionId)
            Debug.LogWarning($"[{name}] 미션의 결과 발전이 '{_expansionId}'가 아닙니다 (땅이 열리지 않음).", this);
    }
}
