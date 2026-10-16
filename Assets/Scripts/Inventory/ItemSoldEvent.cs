/// <summary>
/// 판매 알림에 담기는 정보. TotalPrice는 판매가 × 개수 (재화 배율 적용 전)
/// </summary>
public readonly struct ItemSoldEvent
{
    public readonly ItemDefinition Item;
    public readonly int Amount;
    public readonly int TotalPrice;

    public ItemSoldEvent(ItemDefinition item, int amount, int totalPrice)
    {
        Item = item;
        Amount = amount;
        TotalPrice = totalPrice;
    }
}
