using UnityEngine;

/// <summary>
/// 도감 항목 하나 (당근, 고등어, 농부 해달 등).
/// 채소·어류는 연결된 아이템이 가방에 처음 들어오면 자동으로 획득된다.
/// </summary>
[CreateAssetMenu(fileName = "CollectionEntry", menuName = "Game Data/Collection/Collection Entry")]
public class CollectionEntry : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (세이브에 저장됨, 예: crop_carrot, otter_farmer). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField]
    private string _entryId;

    [Tooltip("이 항목이 표시될 탭")]
    [SerializeField]
    private CollectionTab _tab;

    [Tooltip("표시 이름. 비우면 연결된 아이템의 이름을 쓴다")]
    [SerializeField]
    private string _displayName;

    [Tooltip("탭 안 정렬 순서 (작을수록 앞)")]
    [SerializeField]
    private int _sortOrder;

    [Header("시각 요소 (UI)")]
    [Tooltip("획득했을 때 보일 그림. 비우면 연결된 아이템의 아이콘을 쓴다")]
    [SerializeField]
    private Sprite _portrait;

    [Tooltip("아직 얻지 못했을 때 보일 짙은 갈색 실루엣")]
    [SerializeField]
    private Sprite _silhouette;

    [Header("설명")]
    [Tooltip("이름 아래 한 줄 소개 (예: 밭에서 자라는 아삭한 채소.)")]
    [SerializeField]
    private string _tagline;

    [Tooltip("설명 박스 첫 줄")]
    [TextArea(2, 4)]
    [SerializeField]
    private string _description;

    [Tooltip("설명 박스 두 번째 줄의 값. 제목은 탭이 정함 (예: 획득 장소 → 밭, 좋아하는 것 → 당근)")]
    [SerializeField]
    private string _extraValue;

    [Tooltip("아직 못 만났을 때 보이는 힌트 (예: 레어 장난감을 놓으면 찾아와요 · 밤에만). 비우면 탭 기본 문구")]
    [TextArea(1, 3)]
    [SerializeField]
    private string _hint;

    [Header("자동 획득")]
    [Tooltip("이 아이템을 수확·낚시 등으로 처음 얻으면 자동으로 획득 (해달처럼 아이템이 아닌 항목은 비움)")]
    [SerializeField]
    private ItemDefinition _linkedItem;

    [Tooltip("이 레벨에 도달하면 자동으로 등록 (0이면 레벨로 해금하지 않음. 예: 농부 해달 2, 낚시꾼 해달 10)")]
    [SerializeField]
    private int _unlockLevel;

    [Header("이야기")]
    [Tooltip("해금 후 도감에서 볼 수 있는 컷씬. 비우면 이야기 없음")]
    [SerializeField]
    private CollectionStory _story;

    public string EntryId => _entryId;
    public CollectionTab Tab => _tab;
    public int SortOrder => _sortOrder;
    public Sprite Silhouette => _silhouette;
    public string Tagline => _tagline;
    public string Description => _description;
    public string ExtraValue => _extraValue;
    public string Hint => _hint;
    public ItemDefinition LinkedItem => _linkedItem;
    public int UnlockLevel => _unlockLevel;
    public CollectionStory Story => _story;
    public bool HasStory => _story != null;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(_displayName) ? _displayName
        : _linkedItem != null ? _linkedItem.DisplayName
        : name;

    public Sprite Portrait =>
        _portrait != null ? _portrait
        : _linkedItem != null ? _linkedItem.Icon
        : null;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_entryId))
            Debug.LogWarning($"[{name}] EntryId가 비어 있습니다.", this);

        if (_tab == null)
            Debug.LogWarning($"[{name}] Tab이 비어 있습니다.", this);

        _unlockLevel = Mathf.Max(0, _unlockLevel);
    }
}
