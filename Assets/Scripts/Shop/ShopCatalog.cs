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

    /// <returns>빈 칸을 뺀 상품: 탭 순서(탭이 없는 재료가 맨 앞) → 필요 레벨 낮은 순 → SortOrder 순</returns>
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
            // 모종이 레벨 순이 아니었음(고구마 Lv.8 · 딸기 Lv.11 · 무 Lv.5 …, 4차 QA) → 같은 탭 안에서는 지금 살 수 있는 것부터
            // List.Sort는 안정 정렬이 아니라 목록 순서로 마지막 동점을 가름
            var group = new Dictionary<ShopProduct, int>();
            var order = new Dictionary<ShopProduct, int>();
            for (int i = 0; i < result.Count; i++)
            {
                var tab = FindTab(result[i]);
                group[result[i]] = tab != null ? _tabs.IndexOf(tab) : -1;
                order[result[i]] = i;
            }
            result.Sort((a, b) =>
            {
                int c = group[a].CompareTo(group[b]);
                if (c == 0) c = a.RequiredLevel.CompareTo(b.RequiredLevel);
                if (c == 0) c = a.SortOrder.CompareTo(b.SortOrder);
                return c != 0 ? c : order[a].CompareTo(order[b]);
            });
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
