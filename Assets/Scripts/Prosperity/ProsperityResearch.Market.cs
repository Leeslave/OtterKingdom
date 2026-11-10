using System;
using System.Collections.Generic;

public sealed class ProsperityMarketOrder
{
    public string Id { get; }
    public string Title { get; }
    public string ItemId { get; }
    public int Amount { get; }
    public int Gold { get; }

    public ProsperityMarketOrder(string id, string title, string itemId, int amount, int gold)
    {
        Id = id; Title = title; ItemId = itemId; Amount = amount; Gold = gold;
    }
}

/// <summary>가방 전용 거래. TryExchange는 실패 시 상태를 바꾸지 않고 성공 시 전량 소비·전액 지급한다.</summary>
public interface IProsperityMarketWallet
{
    // 미설정·미등록 아이템은 -1. 정상 보유 수는 0 이상.
    int Count(string itemId);
    bool CanReceiveGold(int amount);
    bool TryExchange(string itemId, int amount, int gold);
}

public enum MarketOrderResult
{
    Success, UnknownOrder, NeedsMarket, NeedsLevel, AlreadyCompleted, AlreadyAccepted,
    NotAccepted, NotEnoughItems, GoldCapacity, NotReady, Busy,
}

public sealed partial class ProsperityResearch
{
    public const string CarrotOrder = "market_carrot_01";
    public const string WoodOrder = "market_wood_01";
    public static readonly IReadOnlyList<ProsperityMarketOrder> MarketOrders = Array.AsReadOnly(new[]
    {
        new ProsperityMarketOrder(CarrotOrder, "장터의 신선한 당근", "crop_carrot", 12, 90),
        new ProsperityMarketOrder(WoodOrder, "상인의 포장 목재", "material_wood", 20, 75),
    });
    private bool _marketBusy;

    public static ProsperityMarketOrder FindMarketOrder(string id)
    {
        foreach (var order in MarketOrders) if (order.Id == id) return order;
        return null;
    }

    private MarketOrderResult CheckMarketAccess(string id, int level)
    {
        if (_marketBusy) return MarketOrderResult.Busy;
        if (FindMarketOrder(id) == null) return MarketOrderResult.UnknownOrder;
        if (Data.marketCompleted.Contains(id)) return MarketOrderResult.AlreadyCompleted;
        if (!Data.introCompleted || !HasTech(TechIds.Market)) return MarketOrderResult.NeedsMarket;
        if (level < 21) return MarketOrderResult.NeedsLevel;
        return MarketOrderResult.Success;
    }

    public MarketOrderResult AcceptMarketOrder(string id, int level)
    {
        var result = CheckMarketAccess(id, level);
        if (result != MarketOrderResult.Success) return result;
        if (Data.marketAccepted.Contains(id)) return MarketOrderResult.AlreadyAccepted;
        Data.marketAccepted.Add(id);
        Changed?.Invoke();
        return MarketOrderResult.Success;
    }

    public MarketOrderResult CancelMarketOrder(string id, int level)
    {
        var result = CheckMarketAccess(id, level);
        if (result != MarketOrderResult.Success) return result;
        if (!Data.marketAccepted.Remove(id)) return MarketOrderResult.NotAccepted;
        Changed?.Invoke();
        return MarketOrderResult.Success;
    }

    public MarketOrderResult CheckMarketDelivery(string id, int level, IProsperityMarketWallet wallet)
    {
        var result = CheckMarketAccess(id, level);
        if (result != MarketOrderResult.Success) return result;
        if (!Data.marketAccepted.Contains(id)) return MarketOrderResult.NotAccepted;
        var order = FindMarketOrder(id);
        int owned = wallet != null ? wallet.Count(order.ItemId) : -1;
        if (owned < 0) return MarketOrderResult.NotReady;
        if (owned < order.Amount) return MarketOrderResult.NotEnoughItems;
        if (!wallet.CanReceiveGold(order.Gold)) return MarketOrderResult.GoldCapacity;
        return MarketOrderResult.Success;
    }

    /// <summary>실행 중 재진입을 막으며 성공한 거래만 완료 기록을 남긴다. 호출자는 저장을 전체 거래 이후로 미룬다.</summary>
    public MarketOrderResult DeliverMarketOrder(string id, int level, IProsperityMarketWallet wallet)
    {
        var result = CheckMarketDelivery(id, level, wallet);
        if (result != MarketOrderResult.Success) return result;
        var order = FindMarketOrder(id);
        _marketBusy = true;
        try
        {
            if (!wallet.TryExchange(order.ItemId, order.Amount, order.Gold)) return MarketOrderResult.NotReady;
            Data.marketCompleted.Add(id);
            Data.marketAccepted.Remove(id);
            Changed?.Invoke();
            return MarketOrderResult.Success;
        }
        finally { _marketBusy = false; }
    }
}
