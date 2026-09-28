// One row of the sale UI. Crops and fishing catches both end up in the same
// inventory, so the sale list is built from this instead of CropDefinition.
public class SellableItem
{
    public readonly string itemId;
    public readonly string displayName;
    public readonly int sellPrice;
    public readonly TransactionSource source;

    public SellableItem(string itemId, string displayName, int sellPrice, TransactionSource source)
    {
        this.itemId = itemId;
        this.displayName = displayName;
        this.sellPrice = sellPrice;
        this.source = source;
    }
}
