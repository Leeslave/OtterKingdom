using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ProsperityMarketTests
{
    [Test]
    public void AllSevenP0Techs_AreReachableWithoutAdvancesOrOrderRewards()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(27, true, true);
        model.SyncPlanReward(true);
        foreach (var tech in ProsperityResearch.Techs)
            if (tech.Level <= 27)
                Assert.AreEqual(ResearchResult.Success, model.TryLearn(tech.Id, 27), tech.Id);
        Assert.AreEqual(7, model.Data.learned.Count);
        Assert.AreEqual(2, model.Data.rp);
    }

    [Test]
    public void TransactionGate_SavesOnlyAfterItemsGoldAndCompletionAreTogether()
    {
        var model = Started();
        var wallet = Stocked();
        var gate = new ProsperitySaveGate();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        int writes = 0;
        Action save = () =>
        {
            Assert.IsFalse(gate.TryDefer());
            writes++;
            Assert.AreEqual(88, wallet.Count("crop_carrot"));
            Assert.AreEqual(90, wallet.Gold);
            Assert.Contains(ProsperityResearch.CarrotOrder, model.Data.marketCompleted);
        };
        wallet.DuringExchange = () => Assert.IsTrue(gate.TryDefer(), "가방·골드 이벤트의 저장은 완료 기록까지 대기");
        model.Changed += () => Assert.IsTrue(gate.TryDefer());
        using (gate.Begin(save))
        {
            model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet);
            Assert.AreEqual(0, writes);
        }
        Assert.AreEqual(1, writes);
    }

    [Test]
    public void TransactionGate_NestedAndDuplicateDispose_FlushesOnceAndCanBeReused()
    {
        var gate = new ProsperitySaveGate();
        int writes = 0;
        var outer = gate.Begin(() => writes++);
        var inner = gate.Begin(() => Assert.Fail("안쪽 scope는 별도로 저장하지 않음"));
        Assert.IsTrue(gate.TryDefer());
        inner.Dispose();
        inner.Dispose();
        Assert.AreEqual(0, writes);
        outer.Dispose();
        outer.Dispose();
        Assert.AreEqual(1, writes);
        Assert.IsFalse(gate.TryDefer());
        using (gate.Begin(() => writes++)) { }
        Assert.AreEqual(1, writes, "실패한 거래처럼 저장 요청이 없으면 쓰지 않음");
        using (gate.Begin(() => writes++)) Assert.IsTrue(gate.TryDefer());
        Assert.AreEqual(2, writes);
    }

    private sealed class Wallet : IProsperityMarketWallet
    {
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        public int Gold;
        public int Capacity = int.MaxValue;
        public bool FailExchange;
        public int Exchanges;
        public Action DuringExchange;
        public int Count(string id) => Items.TryGetValue(id, out int count) ? count : 0;
        public bool CanReceiveGold(int amount) => (long)Gold + amount <= Capacity;
        public bool TryExchange(string id, int amount, int gold)
        {
            if (FailExchange || Count(id) < amount || !CanReceiveGold(gold)) return false;
            Items[id] -= amount;
            Gold += gold;
            Exchanges++;
            DuringExchange?.Invoke();
            return true;
        }
    }

    private static ProsperityResearch Started(bool learnMarket = true)
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(25, true, true);
        model.TryLearn(TechIds.Sawmill, 25);
        if (learnMarket) model.TryLearn(TechIds.Market, 25);
        return model;
    }

    private static Wallet Stocked()
    {
        var wallet = new Wallet();
        wallet.Items["crop_carrot"] = 100;
        wallet.Items["material_wood"] = 100;
        return wallet;
    }

    [Test]
    public void Orders_RequireRealResearchAndLevel_WithoutChargingOnAccept()
    {
        var model = Started(false);
        Assert.AreEqual(MarketOrderResult.NeedsMarket, model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25));
        model.TryLearn(TechIds.Market, 25);
        Assert.AreEqual(MarketOrderResult.NeedsLevel, model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 20));
        int rp = model.Data.rp;
        Assert.AreEqual(MarketOrderResult.Success, model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 21));
        Assert.AreEqual(MarketOrderResult.AlreadyAccepted, model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 21));
        Assert.AreEqual(rp, model.Data.rp);
        Assert.AreEqual(1, model.Data.marketAccepted.Count);
    }

    [Test]
    public void UnknownAndUnacceptedOrders_DoNotConsumeOrPay()
    {
        var model = Started();
        var wallet = Stocked();
        Assert.AreEqual(MarketOrderResult.UnknownOrder, model.DeliverMarketOrder("unknown", 25, wallet));
        Assert.AreEqual(MarketOrderResult.UnknownOrder, model.AcceptMarketOrder(null, 25));
        Assert.AreEqual(MarketOrderResult.NotAccepted, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(0, wallet.Exchanges);
        Assert.AreEqual(0, wallet.Gold);
        Assert.AreEqual(100, wallet.Count("crop_carrot"));
    }

    [Test]
    public void DeliverBothOrders_ConsumesExactQuantitiesAndPaysOnce_WithoutChangingRp()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptAdvance(ProsperityResearch.SalesAdvance, 25);
        int rp = model.Data.rp;
        foreach (var order in ProsperityResearch.MarketOrders)
        {
            Assert.AreEqual(MarketOrderResult.Success, model.AcceptMarketOrder(order.Id, 25));
            Assert.AreEqual(MarketOrderResult.Success, model.DeliverMarketOrder(order.Id, 25, wallet));
            Assert.AreEqual(MarketOrderResult.AlreadyCompleted, model.DeliverMarketOrder(order.Id, 25, wallet));
            Assert.AreEqual(MarketOrderResult.AlreadyCompleted, model.AcceptMarketOrder(order.Id, 25));
            Assert.AreEqual(MarketOrderResult.AlreadyCompleted, model.CancelMarketOrder(order.Id, 25));
        }
        Assert.AreEqual(88, wallet.Count("crop_carrot"));
        Assert.AreEqual(80, wallet.Count("material_wood"));
        Assert.AreEqual(165, wallet.Gold);
        Assert.AreEqual(2, wallet.Exchanges);
        Assert.AreEqual(rp, model.Data.rp);
        Assert.AreEqual(0, model.Data.advance.progress, "주문 보상은 판매 실적이 아님");
        Assert.IsEmpty(model.Data.marketAccepted);
        Assert.AreEqual(2, model.Data.marketCompleted.Count);
    }

    [Test]
    public void InsufficientItems_AreNotPartiallyDelivered_AndExistingStockIsUsable()
    {
        var model = Started();
        var wallet = Stocked();
        wallet.Items["crop_carrot"] = 11;
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.NotEnoughItems, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(11, wallet.Count("crop_carrot"));
        Assert.AreEqual(0, wallet.Exchanges);
        Assert.AreEqual(0, wallet.Gold);
        wallet.Items["crop_carrot"] = 12;
        Assert.AreEqual(MarketOrderResult.Success, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(0, wallet.Count("crop_carrot"));
    }

    [Test]
    public void GoldCapacity_BlocksDeliveryWithoutLosingItems_ThenAllowsExactFit()
    {
        var model = Started();
        var wallet = Stocked();
        wallet.Capacity = 100;
        wallet.Gold = 11;
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.GoldCapacity, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(100, wallet.Count("crop_carrot"));
        Assert.AreEqual(11, wallet.Gold);
        wallet.Gold = 10;
        Assert.AreEqual(MarketOrderResult.Success, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(100, wallet.Gold);
    }

    [Test]
    public void CancelBeforeDelivery_HasNoCost_AndCanBeAcceptedAgain()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.Success, model.CancelMarketOrder(ProsperityResearch.CarrotOrder, 25));
        Assert.AreEqual(MarketOrderResult.NotAccepted, model.CancelMarketOrder(ProsperityResearch.CarrotOrder, 25));
        Assert.AreEqual(MarketOrderResult.NotAccepted, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(100, wallet.Count("crop_carrot"));
        Assert.AreEqual(0, wallet.Gold);
        Assert.AreEqual(MarketOrderResult.Success, model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25));
    }

    [Test]
    public void WalletFailure_KeepsOrderAccepted_AndRetryDoesNotDuplicate()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        wallet.FailExchange = true;
        Assert.AreEqual(MarketOrderResult.NotReady, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.Contains(ProsperityResearch.CarrotOrder, model.Data.marketAccepted);
        Assert.IsEmpty(model.Data.marketCompleted);
        Assert.AreEqual(100, wallet.Count("crop_carrot"));
        wallet.FailExchange = false;
        Assert.AreEqual(MarketOrderResult.Success, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(1, wallet.Exchanges);
    }

    [Test]
    public void ReentrantDeliveryAndCancel_AreRejectedWhileWalletNotifiesListeners()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        model.AcceptMarketOrder(ProsperityResearch.WoodOrder, 25);
        wallet.DuringExchange = () =>
        {
            Assert.AreEqual(MarketOrderResult.Busy, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
            Assert.AreEqual(MarketOrderResult.Busy, model.DeliverMarketOrder(ProsperityResearch.WoodOrder, 25, wallet));
            Assert.AreEqual(MarketOrderResult.Busy, model.CancelMarketOrder(ProsperityResearch.CarrotOrder, 25));
        };
        Assert.AreEqual(MarketOrderResult.Success, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(1, wallet.Exchanges);
        Assert.AreEqual(90, wallet.Gold);
        Assert.Contains(ProsperityResearch.WoodOrder, model.Data.marketAccepted);
    }

    [Test]
    public void CompletionNotification_SeesItemsGoldAndOrderCommittedTogether()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        int changes = 0;
        model.Changed += () =>
        {
            changes++;
            Assert.AreEqual(88, wallet.Count("crop_carrot"));
            Assert.AreEqual(90, wallet.Gold);
            Assert.Contains(ProsperityResearch.CarrotOrder, model.Data.marketCompleted);
            Assert.IsEmpty(model.Data.marketAccepted);
        };
        model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet);
        Assert.AreEqual(1, changes);
    }

    [Test]
    public void AcceptedAndCompletedRecords_SurviveReload_WithoutPayingAgain()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        model.AcceptMarketOrder(ProsperityResearch.WoodOrder, 25);
        var restored = new ProsperityResearch(model.Data);
        Assert.AreEqual(MarketOrderResult.Success, restored.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        var completed = new ProsperityResearch(model.Data);
        Assert.AreEqual(MarketOrderResult.AlreadyCompleted, completed.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.Contains(ProsperityResearch.WoodOrder, completed.Data.marketAccepted);
        Assert.AreEqual(90, wallet.Gold);
    }

    [Test]
    public void LateClickAfterCancellation_RechecksOrderState()
    {
        var model = Started();
        var wallet = Stocked();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.Success, model.CheckMarketDelivery(ProsperityResearch.CarrotOrder, 25, wallet));
        model.CancelMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.NotAccepted, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, wallet));
        Assert.AreEqual(0, wallet.Exchanges);
    }

    [Test]
    public void MissingWallet_ReturnsNotReadyWithoutChangingOrder()
    {
        var model = Started();
        model.AcceptMarketOrder(ProsperityResearch.CarrotOrder, 25);
        Assert.AreEqual(MarketOrderResult.NotReady, model.DeliverMarketOrder(ProsperityResearch.CarrotOrder, 25, null));
        Assert.Contains(ProsperityResearch.CarrotOrder, model.Data.marketAccepted);
    }

    [Test]
    public void OldOrCorruptMarketLists_AreRepairedWithoutReplayingRewards()
    {
        var old = new ProsperitySaveData { rp = 4, marketAccepted = null, marketCompleted = null };
        new ProsperityResearch(old);
        Assert.AreEqual(4, old.rp);
        Assert.IsEmpty(old.marketAccepted);
        Assert.IsEmpty(old.marketCompleted);
        old.marketAccepted.AddRange(new[] { null, ProsperityResearch.CarrotOrder, ProsperityResearch.CarrotOrder, "future-order" });
        old.marketCompleted.AddRange(new[] { "", ProsperityResearch.CarrotOrder, ProsperityResearch.CarrotOrder });
        Assert.IsTrue(ProsperityResearch.Sanitize(old));
        CollectionAssert.AreEqual(new[] { "future-order" }, old.marketAccepted);
        CollectionAssert.AreEqual(new[] { ProsperityResearch.CarrotOrder }, old.marketCompleted);
        Assert.IsFalse(ProsperityResearch.Sanitize(old));
        Assert.AreEqual(4, old.rp);
    }
}
