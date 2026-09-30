using UnityEngine;

/// <summary>
/// 요정 상점의 상품 하나 (예: 감자 모종 1개 = 골드 100, 지렁이 미끼 10개 묶음 = 골드 50).
/// 탭·분류 칩은 아이템의 분류, 희귀도 칩은 아이템의 희귀도를 따른다.
/// </summary>
[CreateAssetMenu(fileName = "ShopProduct", menuName = "Game Data/Shop/Shop Product")]
public class ShopProduct : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (예: shop_seed_potato)")]
    [SerializeField]
    private string _productId;

    [Tooltip("상점 안 정렬 순서 (작을수록 앞)")]
    [SerializeField]
    private int _sortOrder;

    [Header("파는 것")]
    [Tooltip("가방에 들어오는 아이템")]
    [SerializeField]
    private ItemDefinition _item;

    [Tooltip("한 번 사면 받는 개수 (묶음 상품이면 이름 뒤에 \"x10\"처럼 붙음)")]
    [Min(1)]
    [SerializeField]
    private int _bundleSize = 1;

    [Header("가격")]
    [Tooltip("내는 재화 (골드 또는 조개)")]
    [SerializeField]
    private Currency _priceCurrency;

    [Tooltip("묶음 하나의 가격")]
    [Min(1)]
    [SerializeField]
    private int _price = 1;

    public string ProductId => _productId;
    public int SortOrder => _sortOrder;
    public ItemDefinition Item => _item;
    public int BundleSize => Mathf.Max(1, _bundleSize);
    public Currency PriceCurrency => _priceCurrency;
    public int Price => Mathf.Max(1, _price);

    /// <summary>카드에 보일 이름 (묶음이면 "지렁이 미끼 x10")</summary>
    public string DisplayName
    {
        get
        {
            string name = _item != null ? _item.DisplayName : this.name;
            return BundleSize > 1 ? $"{name} x{BundleSize}" : name;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_productId))
            Debug.LogWarning($"[{name}] ProductId가 비어 있습니다.", this);
        if (_item == null)
            Debug.LogWarning($"[{name}] 아이템이 비어 있습니다.", this);
        if (_priceCurrency == null)
            Debug.LogWarning($"[{name}] 가격 재화가 비어 있습니다.", this);
    }
}
