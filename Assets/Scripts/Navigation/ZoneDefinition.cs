using UnityEngine;

/// <summary>
/// 이동할 수 있는 장소 하나 (광장, 밭, 낚시터, 광산). 에셋을 추가하면 이동 팝업에 카드가 생긴다.
/// </summary>
[CreateAssetMenu(fileName = "Zone", menuName = "Game Data/Navigation/Zone")]
public class ZoneDefinition : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (예: Plaza)")]
    [SerializeField]
    private string _zoneId;

    [Tooltip("이동 팝업에 표시될 이름 (예: 광장)")]
    [SerializeField]
    private string _displayName;

    [Tooltip("이름 아래 설명 (예: 요정상점 · 해달 구경)")]
    [SerializeField]
    private string _subtitle;

    [Tooltip("이용할 수 없을 때 설명 대신 보일 문구")]
    [SerializeField]
    private string _lockedSubtitle = "준비 중";

    [Header("시각 요소 (UI)")]
    [Tooltip("이동 카드와 네비게이션 가운데 버튼에 표시될 아이콘")]
    [SerializeField]
    private Sprite _icon;

    [Header("씬")]
    [Tooltip("불러올 씬 이름 (Build Settings에 등록돼 있어야 함). 비우면 준비 중")]
    [SerializeField]
    private string _sceneName;

    [Tooltip("끄면 '준비 중'으로 잠김")]
    [SerializeField]
    private bool _isAvailable = true;

    [Header("정렬")]
    [Tooltip("이동 팝업 정렬 순서 (작을수록 앞)")]
    [SerializeField]
    private int _sortOrder;

    public string ZoneId => _zoneId;
    public string DisplayName => _displayName;
    public string Subtitle => _subtitle;
    public string LockedSubtitle => _lockedSubtitle;
    public Sprite Icon => _icon;
    public string SceneName => _sceneName;
    public int SortOrder => _sortOrder;

    /// <summary>켜져 있고 씬도 지정돼 있어야 이동 가능</summary>
    public bool IsAvailable => _isAvailable && !string.IsNullOrWhiteSpace(_sceneName);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_zoneId))
            Debug.LogWarning($"[{name}] ZoneId가 비어 있습니다.", this);

        if (_isAvailable && string.IsNullOrWhiteSpace(_sceneName))
            Debug.LogWarning($"[{name}] 이용 가능으로 켜져 있지만 씬 이름이 비어 있습니다 (준비 중으로 표시됨).", this);
    }
}
