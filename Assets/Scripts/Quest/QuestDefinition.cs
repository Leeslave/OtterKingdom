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

    [Header("보상")]
    [Tooltip("보상 재화 (골드)")]
    [SerializeField]
    private Currency _rewardCurrency;

    [Tooltip("보상 금액")]
    [Min(0)]
    [SerializeField]
    private int _rewardAmount;

    public string QuestId => _questId;
    public string Title => _title;
    public string Description => _description;
    public int SortOrder => _sortOrder;
    public Sprite Icon => _icon;
    public QuestGoalType GoalType => _goalType;
    public int Goal => Mathf.Max(1, _goal);
    public ItemCategory ItemFilter => _itemFilter;
    public CollectionTab CollectionTab => _collectionTab;
    public Currency RewardCurrency => _rewardCurrency;
    public int RewardAmount => _rewardAmount;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_questId))
            Debug.LogWarning($"[{name}] QuestId가 비어 있습니다.", this);

        if (_rewardAmount > 0 && _rewardCurrency == null)
            Debug.LogWarning($"[{name}] 보상 금액이 있는데 보상 재화가 비어 있습니다.", this);
    }
}
