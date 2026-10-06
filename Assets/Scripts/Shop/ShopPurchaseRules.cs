using System;

public enum ShopPurchaseResult
{
    Bought,
    NotEnoughCurrency, // 재화 부족 → 부족 팝업에서 충전으로
    BagFull,           // 가방에 자리가 없음 (새 종류인데 빈 칸이 없거나, 최대 보유 수)
    Locked,            // 아직 왕국 레벨이 안 됨 (ShopProduct.RequiredLevel)
}

/// <summary>
/// 요정 상점 구매 규칙 (UI 없이 테스트 가능하도록 분리): 살 수 있는 최대 수량, 합계.
/// </summary>
public static class ShopPurchaseRules
{
    /// <summary>한 번에 고를 수 있는 최대 묶음 수 (너무 큰 수를 고르지 않도록)</summary>
    public const int MaxQuantityPerPurchase = 99;

    /// <summary>
    /// 가방에 들어갈 수 있는 만큼의 최대 묶음 수. addable: 가방에 더 넣을 수 있는 아이템 수 (Inventory.GetAddableAmount).
    /// 한 묶음도 못 넣으면 0
    /// </summary>
    public static int MaxQuantity(ShopProduct product, int addable)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));

        int byBag = Math.Max(0, addable) / product.BundleSize;
        return Math.Min(byBag, MaxQuantityPerPurchase);
    }

    /// <returns>묶음 가격 × 수량 (int 범위를 넘으면 int 최대값 — 그 금액은 누구도 낼 수 없으므로 부족으로 처리됨)</returns>
    public static int TotalPrice(ShopProduct product, int quantity)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "수량은 1 이상이어야 합니다.");

        return (int)Math.Min((long)product.Price * quantity, int.MaxValue);
    }

    /// <returns>받는 아이템 개수 (묶음 크기 × 수량)</returns>
    public static int TotalItems(ShopProduct product, int quantity)
    {
        if (product == null) throw new ArgumentNullException(nameof(product));

        return (int)Math.Min((long)product.BundleSize * quantity, int.MaxValue);
    }
}
