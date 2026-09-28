using UnityEngine;

[CreateAssetMenu(fileName = "ItemCategory", menuName = "Game Data/Inventory/Item Category")]
public class ItemCategory : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID")]
    [SerializeField]
    private string _categoryId;

    [Tooltip("탭에 표시될 이름")]
    [SerializeField]
    private string _displayName;

    [Header("탭 설정")]
    [Tooltip("탭 정렬 순서(작을수록 왼쪽)")]
    [SerializeField]
    private int _sortOrder;

    [Tooltip("체크하면 모든 카테고리의 아이템을 보여주는 '전체' 탭이 됨 (아이템에는 지정하지 않음)")]
    [SerializeField]
    private bool _showsAllItems;

    public string CategoryId => _categoryId;
    public string DisplayName => _displayName;
    public int SortOrder => _sortOrder;
    public bool ShowsAllItems => _showsAllItems;

    /// <summary>이 탭에 해당 아이템을 보여줘야 하는지</summary>
    public bool Contains(ItemDefinition item)
    {
        return item != null && (_showsAllItems || item.Category == this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_categoryId))
            Debug.LogWarning($"[{name}] CategoryID가 비어 있습니다.", this);
    }
}
