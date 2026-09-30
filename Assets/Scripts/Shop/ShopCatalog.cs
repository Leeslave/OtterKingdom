using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 요정 상점의 상품 목록과 탭 (전체 다음에 오는 탭: 씨앗 / 낚시 / 장난감 …).
/// 탭은 아이템 분류로 거른다. 탭 이름은 분류 이름과 달라도 된다 (분류 "모종" → 탭 "씨앗").
/// </summary>
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Game Data/Shop/Shop Catalog")]
public class ShopCatalog : ScriptableObject
{
    [Serializable]
    public class Tab
    {
        [Tooltip("탭과 카드 칩에 보일 이름 (예: 씨앗)")]
        [SerializeField] private string _label;
        [Tooltip("이 탭에 보일 아이템의 분류 (하위 분류 포함)")]
        [SerializeField] private ItemCategory _category;

        public string Label => _label;
        public ItemCategory Category => _category;

        public Tab() { }

        public Tab(string label, ItemCategory category)
        {
            _label = label;
            _category = category;
        }
    }

    [Tooltip("\"전체\" 다음에 올 탭 (순서대로)")]
    [SerializeField]
    private List<Tab> _tabs = new List<Tab>();

    [Tooltip("파는 상품 전부")]
    [SerializeField]
    private List<ShopProduct> _products = new List<ShopProduct>();

    public IReadOnlyList<Tab> Tabs => _tabs;

    /// <returns>빈 칸을 뺀 상품 (SortOrder 순)</returns>
    public List<ShopProduct> Products
    {
        get
        {
            var result = new List<ShopProduct>();
            foreach (var product in _products)
            {
                if (product != null && product.Item != null && product.PriceCurrency != null)
                    result.Add(product);
            }
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }
    }

    /// <returns>상품이 속한 탭 (카드의 분류 칩). 어느 탭에도 없으면 null</returns>
    public Tab FindTab(ShopProduct product)
    {
        if (product == null || product.Item == null)
            return null;

        foreach (var tab in _tabs)
        {
            if (tab != null && tab.Category != null && tab.Category.Contains(product.Item))
                return tab;
        }
        return null;
    }
}
