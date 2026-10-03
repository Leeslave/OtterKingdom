using UnityEngine;

/// <summary>
/// 퀘스트 하나 (제목, 목표, 보상). 진행 상태는 QuestLog가 가진다.
/// </summary>
[CreateAssetMenu(fileName = "Quest", menuName = "Game Data/Quest/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (세이브에 저장됨, 예: quest_harvest_1). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField]
    private string _questId;

    [Tooltip("제목 (예: 내가 키운 첫 수확)")]
    [SerializeField]
    private string _title;

    [Tooltip("제목 아래 조건 설명 (예: 작물 3개 수확하기)")]
    [SerializeField]
    private string _description;

    [Tooltip("목록 정렬 순서 (작을수록 위). 받을 수 있는 퀘스트가 먼저, 받은 퀘스트는 맨 아래로 간다")]
    [SerializeField]
    private int _sortOrder;

    [Header("열림")]
    [Tooltip("성장(한 번) 또는 일일(매일 초기화)")]
    [SerializeField]
    private QuestKind _kind = QuestKind.Main;

    [Tooltip("이 레벨부터 목록에 나타나고 진행이 쌓인다")]
    [Min(1)]
    [SerializeField]
    private int _requiredLevel = 1;

    [Tooltip("이 퀘스트의 보상을 받아야 나타남 (체인의 앞 단계). 비우면 레벨만 봄")]
    [SerializeField]
    private QuestDefinition _prerequisite;

    [Header("시각 요소 (UI)")]
    [Tooltip("왼쪽 살구색 칸에 들어갈 그림")]
    [SerializeField]
    private Sprite _icon;

    [Header("목표")]
    [Tooltip("무엇을 셀지")]
    [SerializeField]
    private QuestGoalType _goalType;

    [Tooltip("목표 수치 (개수, 골드, 횟수)")]
    [Min(1)]
    [SerializeField]
    private int _goal = 1;

    [Tooltip("수확·낚시만: 이 분류의 아이템만 센다 (예: 물고기 → 쓰레기 제외). 비우면 전부")]
    [SerializeField]
    private ItemCategory _itemFilter;

    [Tooltip("도감 등록만: 이 탭의 항목만 센다 (예: 해달). 비우면 전부")]
    [SerializeField]
    private CollectionTab _collectionTab;

    [Tooltip("수확·낚시·채굴만: 이 아이템만 센다 (예: 당근). 비우면 분류 필터만 봄")]
    [SerializeField]
    private ItemDefinition _item;

    [Tooltip("건설 완료만: 이 건물들만 센다 (예: 집 두 채). 비우면 개간을 뺀 모든 건물")]
    [SerializeField]
    private ConstructionDefinition[] _constructions;

    [Tooltip("건설 완료만: 건설 해달이 참여한 공사만 센다")]
    [SerializeField]
    private bool _builderOnly;

    [Header("보상")]
    [Tooltip("보상 재화 (골드)")]
    [SerializeField]
    private Currency _rewardCurrency;

    [Tooltip("보상 금액")]
    [Min(0)]
    [SerializeField]
    private int _rewardAmount;

    [Tooltip("보상 경험치 (왕국 레벨)")]
    [Min(0)]
    [SerializeField]
    private int _expReward;

    [Tooltip("0보다 크면 경험치를 '받는 순간의 레벨에서 다음 레벨까지 필요한 경험치의 %'로 줌 (일일 퀘스트가 높은 레벨에서도 의미 있게)")]
    [Range(0f, 100f)]
    [SerializeField]
    private float _expPercentOfLevel;

    public string QuestId => _questId;
    public string Title => _title;
    public string Description => _description;
    public int SortOrder => _sortOrder;
    public Sprite Icon => _icon;
    public QuestGoalType GoalType => _goalType;
    public int Goal => Mathf.Max(1, _goal);
    public ItemCategory ItemFilter => _itemFilter;
    public CollectionTab CollectionTab => _collectionTab;
    public ItemDefinition Item => _item;
    public System.Collections.Generic.IReadOnlyList<ConstructionDefinition> Constructions =>
        _constructions ?? System.Array.Empty<ConstructionDefinition>();
    public bool BuilderOnly => _builderOnly;

    /// <summary>진행이 알림으로 쌓이지 않고 저장된 기록(만난 해달, 다 지은 건물)으로 매번 다시 세는지</summary>
    public bool CountsFromRecords => _goalType == QuestGoalType.CompleteConstruction || _goalType == QuestGoalType.MeetOtter;
    public Currency RewardCurrency => _rewardCurrency;
    public int RewardAmount => _rewardAmount;
    public QuestKind Kind => _kind;
    public int RequiredLevel => Mathf.Max(1, _requiredLevel);
    public QuestDefinition Prerequisite => _prerequisite;

    /// <summary>이 레벨에서 받을 경험치</summary>
    public int ExpFor(int level, ILevelCurve curve)
    {
        if (_expPercentOfLevel <= 0f || curve == null)
            return _expReward;

        int need = curve.ExpToNext(level);
        return need <= 0 ? _expReward : Mathf.Max(1, Mathf.RoundToInt(need * _expPercentOfLevel / 100f));
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_questId))
            Debug.LogWarning($"[{name}] QuestId가 비어 있습니다.", this);

        if (_rewardAmount > 0 && _rewardCurrency == null)
            Debug.LogWarning($"[{name}] 보상 금액이 있는데 보상 재화가 비어 있습니다.", this);
    }
}
