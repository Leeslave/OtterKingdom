using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공동사업 한 단계에서 할 일. 에셋에 숫자로 저장되므로 순서를 바꾸지 말고 뒤에만 추가한다.
/// 단계의 완료는 따로 저장하지 않고 기존 원본 기록(건설 완료 부탁, 치운 장애물, 끝낸 주민 작업, 집의 입주민, 발전)에서 계산한다.
/// </summary>
public enum ProjectActionKind
{
    CompleteConstruction = 0, // 건설 큐에서 짓기 (비용은 납품으로 이미 냄)
    ClearObstacles = 1,       // 플레이어가 광장의 장애물을 직접 치움
    CompleteRegionTask = 2,   // 주민 해달을 보내 정비
    SettleResident = 3,       // 새 이웃을 만나 그 집에 입주시킴
    PlaceWelcomeProp = 4,     // 생활 소품 하나를 골라 설치 (선택지 = 건설 부탁 여러 개)
    HoldGathering = 5,        // 플레이어가 [모임 열기]를 누름
}

/// <summary>공동사업의 단계 하나 (재료 납품 뒤에 차례로)</summary>
[Serializable]
public class ProjectStageDefinition
{
    [Tooltip("단계 ID (예: build_box). 저장하지 않음")]
    [SerializeField] private string _stageId;

    [SerializeField] private ProjectActionKind _action;

    [Tooltip("단계 이름 (예: 상자 설치)")]
    [SerializeField] private string _label;

    [Tooltip("이 단계에서 할 일 안내 (예: 건설 해달이 회관 앞에 상자를 놓아요)")]
    [TextArea]
    [SerializeField] private string _hint;

    [Header("짓기 / 소품")]
    [Tooltip("짓기: 이 부탁의 건설 (비용 0, 필요 발전 = 사업의 납품 완료 발전)")]
    [SerializeField] private BoardRequestDefinition _construction;

    [Tooltip("소품: 고를 수 있는 건설 부탁들 (같은 비용·시간). 하나만 지으면 완료")]
    [SerializeField] private List<BoardRequestDefinition> _options = new List<BoardRequestDefinition>();

    [Header("장애물")]
    [Tooltip("치울 장애물 ID (광장의 P3ObstacleView와 같게)")]
    [SerializeField] private List<string> _obstacleIds = new List<string>();

    [Tooltip("장애물을 다 치우면 열리는 발전 (정비 작업의 필요 발전)")]
    [SerializeField] private string _clearedDevelopment;

    [Header("정비")]
    [SerializeField] private SettlementTaskDefinition _task;

    [Header("입주")]
    [Tooltip("입주할 해달 (집이 완성되면 찾아옴)")]
    [SerializeField] private SettlementOtterDefinition _resident;

    [Tooltip("입주할 집의 배치 자리 ID (예: slot_plaza_expand_01_house)")]
    [SerializeField] private string _houseSlotId;

    public string StageId => _stageId;
    public ProjectActionKind Action => _action;
    public string Label => _label;
    public string Hint => _hint;
    public BoardRequestDefinition Construction => _construction;
    public IReadOnlyList<BoardRequestDefinition> Options => _options;
    public IReadOnlyList<string> ObstacleIds => _obstacleIds;
    public string ClearedDevelopment => _clearedDevelopment;
    public SettlementTaskDefinition Task => _task;
    public SettlementOtterDefinition Resident => _resident;
    public string HouseSlotId => _houseSlotId;

    public ProjectStageDefinition() { }

    public ProjectStageDefinition(string stageId, ProjectActionKind action, string label)
    {
        _stageId = stageId;
        _action = action;
        _label = label;
    }

    /// <summary>테스트·설정 도구용</summary>
    public ProjectStageDefinition With(BoardRequestDefinition construction = null, IEnumerable<BoardRequestDefinition> options = null,
        IEnumerable<string> obstacleIds = null, string clearedDevelopment = null, SettlementTaskDefinition task = null,
        SettlementOtterDefinition resident = null, string houseSlotId = null, string hint = null)
    {
        _construction = construction;
        if (options != null)
            _options = new List<BoardRequestDefinition>(options);
        if (obstacleIds != null)
            _obstacleIds = new List<string>(obstacleIds);
        _clearedDevelopment = clearedDevelopment;
        _task = task;
        _resident = resident;
        _houseSlotId = houseSlotId;
        _hint = hint;
        return this;
    }
}

/// <summary>
/// 마을 공동사업 하나 (P3: 첫 비축 → 광장 확장 → 새 이웃 → 환영 소품 → 첫 마을 모임 → 반복 "마을 생활 준비").
/// 마을회관이 생긴 뒤 순서대로 하나씩 열린다: 앞 사업의 결과 발전 + 플레이어 레벨. 레벨이 모자라도 하던 사업은 끝낼 수 있다.
/// 1) 재료(골드·아이템)를 나눠서 납품 (부분 납품은 저장됨, 되돌려 받지 않음) — 다 내면 "납품 완료" 발전이 열림
/// 2) 단계를 차례로 (건설 큐 · 장애물 · 주민 정비 · 입주 · 소품 · 모임). 단계 완료 = 기존 원본 기록
/// 3) 다 끝나면 결과 발전 + 고정 경험치 (사업 ID + 회차로 한 번만) + 방명록
/// 반복 사업은 단계 없이 납품만 하고, 회차마다 제목·재료를 돌려 쓰고 비용은 상한까지만 오른다.
/// </summary>
[CreateAssetMenu(fileName = "Project", menuName = "Game Data/Settlement/Community Project")]
public class CommunityProjectDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: p3_supply_01)")]
    [SerializeField] private string _projectId;

    [Tooltip("순서 (작을수록 먼저)")]
    [SerializeField] private int _order;

    [Header("내용")]
    [SerializeField] private string _title;

    [Tooltip("부탁하는 해달의 짧은 대사")]
    [TextArea]
    [SerializeField] private string _line;

    [SerializeField] private SettlementOtterDefinition _requester;

    [SerializeField] private Sprite _icon;

    [Tooltip("끝나면 어떻게 되는지 (예: 회관 앞에 비축 상자가 생겨요)")]
    [SerializeField] private string _resultPreview;

    [Tooltip("다 끝났을 때 알림 (예: 우리 마을의 첫 비축을 마쳤어요!)")]
    [SerializeField] private string _completionMessage;

    [Header("열리는 조건")]
    [Tooltip("이 플레이어 레벨부터 시작할 수 있음 (끝내는 데는 레벨이 필요 없음)")]
    [Min(1)]
    [SerializeField] private int _requiredLevel = 1;

    [Tooltip("이 발전이 있어야 열림 (앞 사업의 결과 발전, 첫 사업은 마을회관)")]
    [SerializeField] private string _requiredDevelopment;

    [Header("재료 (단계 비용까지 포함. 건설에서 다시 내지 않음)")]
    [Min(0)]
    [SerializeField] private int _gold;

    [SerializeField] private List<ItemAmount> _items = new List<ItemAmount>();

    [Header("단계")]
    [SerializeField] private List<ProjectStageDefinition> _stages = new List<ProjectStageDefinition>();

    [Header("결과")]
    [Tooltip("다 끝나면 열리는 발전 (예: supply_ready). 다음 사업의 열리는 조건")]
    [SerializeField] private string _resultDevelopment;

    [Tooltip("다 끝나면 한 번 주는 경험치 (가변 아님)")]
    [Min(0)]
    [SerializeField] private int _xpReward;

    [SerializeField] private GuestbookEntryDefinition _completionEntry;

    [Header("반복 사업 (첫 모임 뒤)")]
    [Tooltip("켜면 끝날 때마다 다음 회차가 바로 준비됨 (단계 없이 납품만)")]
    [SerializeField] private bool _repeatable;

    [Tooltip("회차마다 돌려 쓰는 제목 (식탁 준비 / 휴식 공간 정돈 / 회관 물품 보충)")]
    [SerializeField] private List<string> _cycleTitles = new List<string>();

    [Tooltip("회차마다 돌려 쓰는 재료 (비우면 위 재료). 회차 % 개수")]
    [SerializeField] private List<ProjectCycleCost> _cycleCosts = new List<ProjectCycleCost>();

    [Tooltip("회차마다 비용이 이만큼 늘어남 (0.25 = 25%)")]
    [Min(0f)]
    [SerializeField] private float _costGrowthPerCycle = 0.25f;

    [Tooltip("비용 배율의 상한 (2 = 처음의 두 배까지)")]
    [Min(1f)]
    [SerializeField] private float _maxCostMultiplier = 2f;

    [Tooltip("반복 사업 경험치: 받는 순간 레벨에서 다음 레벨까지 필요한 경험치의 % (0이면 고정 경험치)")]
    [Range(0f, 100f)]
    [SerializeField] private float _xpPercentOfLevel;

    [Tooltip("누적 이 횟수를 끝내면 열리는 발전 (회관 앞 장식 1단계). 0이면 없음")]
    [Min(0)]
    [SerializeField] private int _milestoneCycles = 3;

    [SerializeField] private string _milestoneDevelopment;

    public string ProjectId => _projectId;
    public int Order => _order;
    public string Title => _title;
    public string Line => _line;
    public SettlementOtterDefinition Requester => _requester;
    public Sprite Icon => _icon;
    public string ResultPreview => _resultPreview;
    public string CompletionMessage => _completionMessage;
    public int RequiredLevel => Mathf.Max(1, _requiredLevel);
    public string RequiredDevelopment => _requiredDevelopment;
    public int Gold => Mathf.Max(0, _gold);
    public IReadOnlyList<ItemAmount> Items => _items;
    public IReadOnlyList<ProjectStageDefinition> Stages => _stages;
    public string ResultDevelopment => _resultDevelopment;
    public int XpReward => Mathf.Max(0, _xpReward);
    public GuestbookEntryDefinition CompletionEntry => _completionEntry;
    public bool Repeatable => _repeatable;
    public IReadOnlyList<string> CycleTitles => _cycleTitles;
    public IReadOnlyList<ProjectCycleCost> CycleCosts => _cycleCosts;
    public float CostGrowthPerCycle => Mathf.Max(0f, _costGrowthPerCycle);
    public float MaxCostMultiplier => Mathf.Max(1f, _maxCostMultiplier);
    public float XpPercentOfLevel => _xpPercentOfLevel;
    public int MilestoneCycles => Mathf.Max(0, _milestoneCycles);
    public string MilestoneDevelopment => _milestoneDevelopment;

    /// <summary>재료를 다 내면 열리는 발전 (단계 건설·정비가 이걸 조건으로 둠). 데이터가 아니라 ID에서 정해짐</summary>
    public string PaidDevelopment => _projectId + "_paid";

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string projectId, int order, string title, int requiredLevel, string requiredDevelopment,
        int gold, IEnumerable<ItemAmount> items, IEnumerable<ProjectStageDefinition> stages, string resultDevelopment, int xpReward)
    {
        _projectId = projectId;
        _order = order;
        _title = title;
        _requiredLevel = requiredLevel;
        _requiredDevelopment = requiredDevelopment;
        _gold = gold;
        _items = items != null ? new List<ItemAmount>(items) : new List<ItemAmount>();
        _stages = stages != null ? new List<ProjectStageDefinition>(stages) : new List<ProjectStageDefinition>();
        _resultDevelopment = resultDevelopment;
        _xpReward = xpReward;
    }

    /// <summary>테스트·설정 도구용</summary>
    public void SetupRepeat(IEnumerable<string> titles, IEnumerable<ProjectCycleCost> costs, float growth, float maxMultiplier,
        float xpPercent, int milestoneCycles, string milestoneDevelopment)
    {
        _repeatable = true;
        _cycleTitles = titles != null ? new List<string>(titles) : new List<string>();
        _cycleCosts = costs != null ? new List<ProjectCycleCost>(costs) : new List<ProjectCycleCost>();
        _costGrowthPerCycle = growth;
        _maxCostMultiplier = maxMultiplier;
        _xpPercentOfLevel = xpPercent;
        _milestoneCycles = milestoneCycles;
        _milestoneDevelopment = milestoneDevelopment;
    }

    /// <summary>테스트·설정 도구용</summary>
    public void SetupText(string line, string resultPreview, string completionMessage, SettlementOtterDefinition requester,
        Sprite icon, GuestbookEntryDefinition entry)
    {
        _line = line;
        _resultPreview = resultPreview;
        _completionMessage = completionMessage;
        _requester = requester;
        _icon = icon;
        _completionEntry = entry;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_projectId))
            Debug.LogWarning($"[{name}] ProjectId가 비어 있습니다.", this);
        if (!_repeatable && string.IsNullOrWhiteSpace(_resultDevelopment))
            Debug.LogWarning($"[{name}] 결과 발전이 비어 있습니다 (다음 사업이 열리지 않음).", this);
    }
}

/// <summary>반복 사업 한 회차의 재료</summary>
[Serializable]
public class ProjectCycleCost
{
    [Min(0)]
    [SerializeField] private int _gold;

    [SerializeField] private List<ItemAmount> _items = new List<ItemAmount>();

    public int Gold => Mathf.Max(0, _gold);
    public IReadOnlyList<ItemAmount> Items => _items;

    public ProjectCycleCost() { }

    public ProjectCycleCost(int gold, IEnumerable<ItemAmount> items)
    {
        _gold = gold;
        _items = items != null ? new List<ItemAmount>(items) : new List<ItemAmount>();
    }
}
