using UnityEngine;

/// <summary>
/// 도감 탭 하나 (채소 / 어류 / 해달). 탭마다 다른 문구(획득 완료 / 등록 완료 등)와 "방문 흔적" 사용 여부를 가진다.
/// </summary>
[CreateAssetMenu(fileName = "CollectionTab", menuName = "Game Data/Collection/Collection Tab")]
public class CollectionTab : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (예: Vegetable)")]
    [SerializeField]
    private string _tabId;

    [Tooltip("탭에 표시될 이름 (예: 채소)")]
    [SerializeField]
    private string _displayName;

    [Tooltip("탭 정렬 순서 (작을수록 왼쪽)")]
    [SerializeField]
    private int _sortOrder;

    [Header("상태 문구")]
    [Tooltip("수집 현황 앞 글자 (예: 수집 → \"수집 6/12\", 등록 → \"등록 4/12\")")]
    [SerializeField]
    private string _countLabel = "수집";

    [Tooltip("아직 얻지 못한 상태 (예: 미획득, 미발견)")]
    [SerializeField]
    private string _unknownLabel = "미획득";

    [Tooltip("얻은 상태 (예: 획득 완료, 등록 완료)")]
    [SerializeField]
    private string _collectedLabel = "획득 완료";

    [Tooltip("설명 박스 두 번째 줄의 제목 (예: 획득 장소, 좋아하는 것)")]
    [SerializeField]
    private string _extraLabel = "획득 장소";

    [Header("범례")]
    [Tooltip("맨 아래 범례의 '미획득' 옆에 보일 실루엣")]
    [SerializeField]
    private Sprite _unknownIcon;

    [Header("방문 흔적")]
    [Tooltip("켜면 '방문 흔적' 상태를 쓴다 (해달 탭: 오프라인 동안 왔다 감)")]
    [SerializeField]
    private bool _supportsVisits;

    public string TabId => _tabId;
    public string DisplayName => _displayName;
    public int SortOrder => _sortOrder;
    public string CountLabel => _countLabel;
    public string UnknownLabel => _unknownLabel;
    public string CollectedLabel => _collectedLabel;
    public string ExtraLabel => _extraLabel;
    public Sprite UnknownIcon => _unknownIcon;
    public bool SupportsVisits => _supportsVisits;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_tabId))
            Debug.LogWarning($"[{name}] TabId가 비어 있습니다.", this);
    }
}
