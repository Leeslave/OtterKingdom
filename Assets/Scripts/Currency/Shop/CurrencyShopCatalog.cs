using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충전 상점의 상품 목록 (골드 충전, 조개 충전).
/// </summary>
[CreateAssetMenu(fileName = "CurrencyShopCatalog", menuName = "Game Data/Currency/Currency Shop Catalog")]
public class CurrencyShopCatalog : ScriptableObject
{
    [Tooltip("골드 충전 화면 상품 (조개로 삼)")]
    [SerializeField]
    private List<CurrencyPack> _goldPacks = new List<CurrencyPack>();

    [Tooltip("조개 충전 화면 상품 (현금)")]
    [SerializeField]
    private List<CurrencyPack> _gemPacks = new List<CurrencyPack>();

    /// <returns>빈 칸을 뺀 골드 상품 (SortOrder 순)</returns>
    public List<CurrencyPack> GoldPacks => Sorted(_goldPacks);

    /// <returns>빈 칸을 뺀 조개 상품 (SortOrder 순)</returns>
    public List<CurrencyPack> GemPacks => Sorted(_gemPacks);

    private static List<CurrencyPack> Sorted(List<CurrencyPack> packs)
    {
        var result = new List<CurrencyPack>();
        foreach (var pack in packs)
        {
            if (pack != null)
                result.Add(pack);
        }

        result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return result;
    }
}
