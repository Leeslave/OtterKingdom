using UnityEngine;

/// <summary>
/// 게임에서 일어나는 일을 듣고 AnalyticsLog에 남긴다 (상세기획서 12.2 필수 이벤트).
/// 게임이 시작될 때 저절로 하나 만들어져 씬을 넘어 유지된다. 전역 UI의 관리자들은 늦게 생길 수 있어 매 프레임 바뀌었는지만 보고 다시 붙는다.
/// 개발자 지급(TestGet)은 남기지 않는다. 생산 틱마다 남기지 않고, 가방에 들어온 순간만 남긴다.
/// </summary>
public class AnalyticsReporter : MonoBehaviour
{
    private CurrencyManager _currency;
    private InventoryManager _inventory;
    private Collection _collection;
    private ProfileManager _profile;
    private Settlement _settlement;

    private float _sessionStartedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(AnalyticsReporter));
        DontDestroyOnLoad(go);
        go.AddComponent<AnalyticsReporter>();
    }

    private void Awake()
    {
        StartSession(false);
    }

    private void OnEnable()
    {
        GameManager.HarvestStored += HandleHarvestStored;
        GameManager.FishCaught += HandleFishCaught;
        GameManager.MiningFound += HandleMiningFound;
        FairyShopPresenter.Purchased += HandleShopPurchased;
    }

    private void OnDisable()
    {
        GameManager.HarvestStored -= HandleHarvestStored;
        GameManager.FishCaught -= HandleFishCaught;
        GameManager.MiningFound -= HandleMiningFound;
        FairyShopPresenter.Purchased -= HandleShopPurchased;
        Bind(null, null, null, null, null);
    }

    private void Update()
    {
        var settlementManager = SettlementManager.Instance;
        var collectionManager = CollectionManager.Instance;
        Bind(CurrencyManager.Instance, InventoryManager.Instance,
            collectionManager != null ? collectionManager.Collection : null,
            ProfileManager.Instance,
            settlementManager != null ? settlementManager.Settlement : null);
    }

    // 백그라운드로 가면 세션 끝, 돌아오면 다시 시작 (백그라운드 시간은 플레이 시간에 넣지 않음)
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            EndSession("background");
        else
            StartSession(true);
    }

    private void OnApplicationQuit() => EndSession("quit");

    private void StartSession(bool resumed)
    {
        _sessionStartedAt = Time.realtimeSinceStartup;
        AnalyticsLog.Track("session_start", ("resumed", resumed));
    }

    private void EndSession(string reason)
    {
        AnalyticsLog.Track("session_end", ("duration", Time.realtimeSinceStartup - _sessionStartedAt), ("endReason", reason));
    }

    #region 구독 (관리자가 바뀌면 옛것에서 떼고 새것에 붙임)

    private void Bind(CurrencyManager currency, InventoryManager inventory, Collection collection, ProfileManager profile, Settlement settlement)
    {
        if (currency != _currency)
        {
            if (_currency != null) _currency.OnTransaction -= HandleTransaction;
            _currency = currency;
            if (_currency != null) _currency.OnTransaction += HandleTransaction;
        }
        if (inventory != _inventory)
        {
            if (_inventory != null) _inventory.OnItemSold -= HandleItemSold;
            _inventory = inventory;
            if (_inventory != null) _inventory.OnItemSold += HandleItemSold;
        }
        if (collection != _collection)
        {
            if (_collection != null) _collection.OnStateChanged -= HandleCollectionChanged;
            _collection = collection;
            if (_collection != null) _collection.OnStateChanged += HandleCollectionChanged;
        }
        if (profile != _profile)
        {
            if (_profile != null) _profile.OnLevelUp -= HandleLevelUp;
            _profile = profile;
            if (_profile != null) _profile.OnLevelUp += HandleLevelUp;
        }
        if (settlement != _settlement)
        {
            if (_settlement != null)
            {
                _settlement.OnStageChanged -= HandleStageChanged;
                _settlement.OnRequestCompleted -= HandleRequestCompleted;
                _settlement.OnConstructionFinished -= HandleConstructionFinished;
                _settlement.OnTaskFinished -= HandleTaskFinished;
            }
            _settlement = settlement;
            if (_settlement != null)
            {
                _settlement.OnStageChanged += HandleStageChanged;
                _settlement.OnRequestCompleted += HandleRequestCompleted;
                _settlement.OnConstructionFinished += HandleConstructionFinished;
                _settlement.OnTaskFinished += HandleTaskFinished;
            }
        }
    }

    #endregion

    #region 이벤트

    private void HandleHarvestStored(ItemDefinition item, int amount) => RewardClaimed("farm", item, amount);
    private void HandleFishCaught(ItemDefinition item) => RewardClaimed("fishing", item, 1);
    private void HandleMiningFound(ItemDefinition item) => RewardClaimed("mine", item, 1);

    private static void RewardClaimed(string facility, ItemDefinition item, int amount)
    {
        if (item == null)
            return;
        AnalyticsLog.Track("reward_claimed", ("facilityId", facility), ("itemId", item.ItemId), ("amount", amount), ("offlineOrigin", false));
    }

    private static void HandleShopPurchased(ShopProduct product, int quantity)
    {
        if (product == null)
            return;
        AnalyticsLog.Track("shop_purchased", ("productId", product.ProductId), ("quantity", quantity),
            ("currencyId", product.PriceCurrency != null ? product.PriceCurrency.CurrencyID : null), ("cost", product.Price * quantity));
    }

    private static void HandleTransaction(CurrencyChange change)
    {
        string currencyId = change.Currency != null ? change.Currency.CurrencyID : null;
        switch (change.Source)
        {
            case TransactionSource.FarmUpgrade:
            case TransactionSource.RodUpgrade:
            case TransactionSource.PickaxeUpgrade:
                AnalyticsLog.Track("upgrade_purchased", ("facilityType", change.Source.ToString()), ("currencyId", currencyId), ("cost", -change.Delta));
                break;
            case TransactionSource.PlotUnlock:
                AnalyticsLog.Track("plot_unlocked", ("currencyId", currencyId), ("cost", -change.Delta));
                break;
            // 나머지(판매·보상 등)는 각자의 이벤트로 남기고, 개발자 지급(TestGet/TestUse)은 지표에서 뺌
        }
    }

    private static void HandleItemSold(ItemSoldEvent sold)
    {
        AnalyticsLog.Track("items_sold", ("itemId", sold.Item != null ? sold.Item.ItemId : null), ("quantity", sold.Amount), ("totalValue", sold.TotalPrice));
    }

    private static void HandleCollectionChanged(CollectionEntry entry, CollectionState state)
    {
        if (entry == null)
            return;
        AnalyticsLog.Track("collection_discovered", ("entryId", entry.EntryId), ("state", state.ToString()));
    }

    private static void HandleLevelUp(int level)
    {
        AnalyticsLog.Track("level_up", ("level", level), ("playSeconds", Time.realtimeSinceStartup));
    }

    private static void HandleStageChanged(int stage)
    {
        AnalyticsLog.Track("village_stage_reached", ("stage", stage));
    }

    private static void HandleRequestCompleted(string requestId)
    {
        AnalyticsLog.Track("request_completed", ("requestId", requestId));
    }

    private static void HandleConstructionFinished(ConstructionJob job)
    {
        if (job == null)
            return;
        AnalyticsLog.Track("construction_finished", ("constructionId", job.ConstructionId), ("durationSec", job.Duration.TotalSeconds));
    }

    private static void HandleTaskFinished(SettlementTaskJob job)
    {
        if (job == null)
            return;
        AnalyticsLog.Track("settlement_task_finished", ("taskId", job.TaskId), ("workers", job.OtterIds.Count));
    }

    #endregion
}
