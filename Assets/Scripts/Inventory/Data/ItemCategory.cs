using System.Collections.Generic;
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
    [Tooltip("정렬 순서(작을수록 왼쪽). 탭끼리, 같은 탭 안의 칩끼리 비교")]
    [SerializeField]
    private int _sortOrder;

    [Tooltip("체크하면 모든 카테고리의 아이템을 보여주는 '전체' 탭이 됨 (아이템에는 지정하지 않음)")]
    [SerializeField]
    private bool _showsAllItems;

    [Header("가방 칸")]
    [Tooltip("끄면 이 분류(와 하위 분류)의 아이템은 가방 칸을 차지하지 않음 (예: 꾸미기 장난감 — 광장에 놓는 물건이라 보관 한도와 무관)")]
    [SerializeField]
    private bool _usesBagCapacity = true;

    [Header("계층")]
    [Tooltip("비우면 탭, 지정하면 그 탭 안의 소분류 칩")]
    [SerializeField]
    private ItemCategory _parent;

    public string CategoryId => _categoryId;
    public string DisplayName => _displayName;
    public int SortOrder => _sortOrder;
    public bool ShowsAllItems => _showsAllItems;
    public ItemCategory Parent => _parent;
    public bool IsRoot => _parent == null;

    /// <summary>이 분류의 아이템이 가방 칸을 차지하는지 (자신이나 부모 중 하나라도 끄면 차지하지 않음)</summary>
    public bool UsesBagCapacity
    {
        get
        {
            var c = this;
            for (int depth = 0; c != null && depth < MaxDepth; depth++)
            {
                if (!c._usesBagCapacity)
                    return false;
                c = c._parent;
            }
            return true;
        }
    }

    // 부모를 따라 올라가는 반복의 안전 한도. 실수로 부모가 순환(A→B→A)해도 에디터가 멈추지 않게
    private const int MaxDepth = 16;

    /// <summary>가장 위의 탭 (자신이 탭이면 자신)</summary>
    public ItemCategory Root
    {
        get
        {
            var c = this;
            for (int depth = 0; c._parent != null && depth < MaxDepth; depth++)
                c = c._parent;
            return c;
        }
    }

    /// <summary>이 탭(또는 칩)에 해당 아이템을 보여줘야 하는지</summary>
    public bool Contains(ItemDefinition item)
    {
        if (item == null)
            return false;

        if (_showsAllItems)
            return true;

        // 아이템의 카테고리부터 부모 쪽으로 올라가며 자신을 찾음 (작물 → 농사)
        var c = item.Category;
        for (int depth = 0; c != null && depth < MaxDepth; depth++)
        {
            if (c == this)
                return true;
            c = c.Parent;
        }

        return false;
    }

    private bool HasParentCycle()
    {
        var visited = new HashSet<ItemCategory>();
        for (var c = this; c != null; c = c._parent)
        {
            if (!visited.Add(c))
                return true;
        }
        return false;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_categoryId))
            Debug.LogWarning($"[{name}] CategoryID가 비어 있습니다.", this);

        if (HasParentCycle())
            Debug.LogWarning($"[{name}] 부모가 순환합니다 (자기 자신으로 되돌아옴). 부모 설정을 확인하세요.", this);

        if (_showsAllItems && _parent != null)
            Debug.LogWarning($"[{name}] '전체' 카테고리는 부모를 가질 수 없습니다 (탭 전용).", this);
    }
}
