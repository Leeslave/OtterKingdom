using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임에서 일어나는 일에 효과음을 붙인다 (상세기획서 10.2). 소리를 내는 곳을 여기 한 곳에 모아,
/// 각 기능 코드는 소리를 모른다. 버튼 클릭음만 ButtonPressFeedback이 낸다.
/// 게임이 시작될 때 저절로 하나 만들어져 씬을 넘어 유지된다. 전역 UI의 관리자들은 늦게 생길 수 있어 매 프레임 바뀌었는지만 보고 다시 붙는다.
/// </summary>
public class GameSoundEvents : MonoBehaviour
{
    private CurrencyManager _currency;
    private InventoryManager _inventory;
    private ProfileManager _profile;
    private Settlement _settlement;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(GameSoundEvents));
        DontDestroyOnLoad(go);
        go.AddComponent<GameSoundEvents>();
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
        Bind(null, null, null, null);
    }

    private void Update()
    {
        var settlementManager = SettlementManager.Instance;
        Bind(CurrencyManager.Instance, InventoryManager.Instance, ProfileManager.Instance,
            settlementManager != null ? settlementManager.Settlement : null);
    }

    private void Bind(CurrencyManager currency, InventoryManager inventory, ProfileManager profile, Settlement settlement)
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
                _settlement.OnConstructionFinished -= HandleConstructionFinished;
                _settlement.OnTaskFinished -= HandleTaskFinished;
            }
            _settlement = settlement;
            if (_settlement != null)
            {
                _settlement.OnConstructionFinished += HandleConstructionFinished;
                _settlement.OnTaskFinished += HandleTaskFinished;
            }
        }
    }

    // 농부·낚시꾼은 다른 장소에 있어도 일하므로, 수확·낚시 소리는 그 장소를 보고 있을 때만 (광산 발견은 어디서나 알림이 떠서 소리도 냄)
    private static void HandleHarvestStored(ItemDefinition item, int amount) => PlayIn(GameManager.FarmZoneId, SfxKind.Harvest);
    private static void HandleFishCaught(ItemDefinition item) => PlayIn(GameManager.FishingZoneId, SfxKind.Fish);

    private static void PlayIn(string zoneScene, SfxKind kind)
    {
        if (SceneManager.GetActiveScene().name == zoneScene)
            AudioManager.Play(kind);
    }
    private static void HandleMiningFound(ItemDefinition item) => AudioManager.Play(SfxKind.Find);
    private static void HandleShopPurchased(ShopProduct product, int quantity) => AudioManager.Play(SfxKind.Coin);
    private static void HandleItemSold(ItemSoldEvent sold) => AudioManager.Play(SfxKind.Coin);
    private static void HandleLevelUp(int level) => AudioManager.Play(SfxKind.LevelUp);
    private static void HandleConstructionFinished(ConstructionJob job) => AudioManager.Play(SfxKind.Complete);
    private static void HandleTaskFinished(SettlementTaskJob job) => AudioManager.Play(SfxKind.Complete);

    private static void HandleTransaction(CurrencyChange change)
    {
        switch (change.Source)
        {
            case TransactionSource.FarmUpgrade:
            case TransactionSource.RodUpgrade:
            case TransactionSource.PickaxeUpgrade:
            case TransactionSource.PlotUnlock:
            case TransactionSource.InventoryExpand:
            case TransactionSource.DecorExpand:
                AudioManager.Play(SfxKind.Upgrade);
                break;
            // 보상으로 재화를 받을 때 (퀘스트·부탁·레벨 보상). 판매·구매는 각자의 알림에서 냄
            case TransactionSource.QuestReward:
            case TransactionSource.RequestReward:
            case TransactionSource.LevelReward:
                if (change.Delta > 0)
                    AudioManager.Play(SfxKind.Coin);
                break;
        }
    }
}
