using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Game Data/Inventory/Item")]
public class ItemDefinition : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (예: veg_carrot)")]
    [SerializeField]
    private string _itemId;

    [Tooltip("화면에 표시될 이름")]
    [SerializeField]
    private string _displayName;

    [Tooltip("상세 패널에 표시될 설명")]
    [TextArea(3, 6)]
    [SerializeField]
    private string _description;

    [Header("시각 요소 (UI)")]
    [Tooltip("슬롯과 상세 패널에 표시될 아이콘")]
    [SerializeField]
    private Sprite _icon;

    [Header("분류")]
    [Tooltip("가장 아래 단계의 소분류 (예: 작물). 부모 탭(농사)에도 자동으로 표시됨")]
    [SerializeField]
    private ItemCategory _category;

    [Tooltip("희귀도 (상세 패널의 \"채소 · 흔함\" 표시)")]
    [SerializeField]
    private ItemRarity _rarity;

    [Header("수치")]
    [Tooltip("끄면 판매 목록/전체 판매에서 빠지고 상세 패널에 가격을 표시하지 않음 (예: 모종)")]
    [SerializeField]
    private bool _isSellable = true;

    [Tooltip("판매 가격 (0 이상)")]
    [SerializeField]
    private int _sellPrice;

    [Tooltip("최대 보유 수 (1 이상)")]
    [SerializeField]
    private int _maxStack = 999;

    public string ItemId => _itemId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public ItemCategory Category => _category;
    public ItemRarity Rarity => _rarity;

    public bool IsSellable => _isSellable;
    public int SellPrice => _sellPrice;
    public int MaxStack => _maxStack;

    /// <summary>가방 칸을 차지하는지 (분류가 정함, 분류가 없으면 차지함)</summary>
    public bool UsesBagCapacity => _category == null || _category.UsesBagCapacity;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_itemId))
            Debug.LogWarning($"[{name}] ItemId가 비어있습니다.", this);

        if (_category == null)
            Debug.LogWarning($"[{name}] Category가 비어있습니다.", this);
        else if (_category.ShowsAllItems)
            Debug.LogWarning($"[{name}] '전체' 카테고리는 탭 전용입니다. 실제 소분류(작물, 모종 등)를 지정하세요.", this);

        if (_rarity == null)
            Debug.LogWarning($"[{name}] Rarity가 비어있습니다.", this);

        _sellPrice = Mathf.Max(0, _sellPrice);
        _maxStack = Mathf.Max(1, _maxStack);
    }
}
