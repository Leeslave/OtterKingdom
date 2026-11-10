/// <summary>장터 주문과 실제 가방·골드 연결. 각 P0 주문은 한 종류의 재료만 전량 납품한다.</summary>
public sealed partial class ProsperityManager
{
    public int MarketItemCount(string itemId) => new MarketWallet().Count(itemId);

    public string MarketItemName(string itemId)
    {
        var inventory = InventoryManager.Instance;
        return inventory != null && inventory.Config != null && inventory.Config.ItemDatabase != null
            && inventory.Config.ItemDatabase.TryGet(itemId, out var item) ? item.DisplayName : itemId;
    }

    public MarketOrderResult CheckMarketDelivery(string id) =>
        Research.CheckMarketDelivery(id, GameManager.KingdomLevel, new MarketWallet());

    public MarketOrderResult DeliverMarketOrder(string id)
    {
        var game = GameManager.Instance;
        if (game == null) return MarketOrderResult.NotReady;
        using (game.DeferProsperitySave())
            return Research.DeliverMarketOrder(id, GameManager.KingdomLevel, new MarketWallet());
    }

    private sealed class MarketWallet : IProsperityMarketWallet
    {
        public int Count(string itemId)
        {
            var game = GameManager.Instance;
            return game != null && game.TryFindItem(itemId, out var item) ? game.Bag.GetCount(item) : -1;
        }

        public bool CanReceiveGold(int amount)
        {
            var game = GameManager.Instance;
            var currency = CurrencyManager.Instance;
            return amount > 0 && game != null && game.GoldCurrency != null && currency != null
                && (long)currency.GetCurrency(game.GoldCurrency) + amount <= game.GoldCurrency.MaxCapacity;
        }

        public bool TryExchange(string itemId, int amount, int gold)
        {
            var game = GameManager.Instance;
            if (game == null || amount <= 0 || !CanReceiveGold(gold)
                || !game.TryFindItem(itemId, out var item) || game.Bag.GetCount(item) < amount) return false;
            if (!game.Bag.TryRemove(item, amount, ItemChangeReason.Delivery)) return false;
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(game.GoldCurrency, gold, TransactionSource.MarketOrderReward));
            return true;
        }
    }
}
