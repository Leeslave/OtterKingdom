using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// M1 vertical-slice bootstrap: loads/creates the save, wires the plain-C#
// services, drives the farm production tick, and exposes a temporary OnGUI
// debug UI so the loop (plant -> grow -> harvest -> sell -> upgrade) can
// be verified with zero scene/prefab setup. Replace the OnGUI block with real
// uGUI views in M4; the services underneath should not need to change.
//
// Every zone scene (Farm, Fishing, Mine, Plaza) has its own instance of the
// GameManager prefab (OtterKingdom > Tools > Setup GameManager Prefab), so the
// save, coins, inventory and sale UI are shared and the data can't drift. The
// farm keeps ticking in every scene — crops grow wherever the player is, but
// only the farm scene's otter harvests them.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly Rect DebugPanelRect = new Rect(20, 20, 440, 560);

    public bool IsDebugPanelOpen => showDebugPanel;
    public FarmService FarmService => farmService;
    public IReadOnlyList<CropDefinition> Crops => cropDefinitions;
    public FishingService FishingService => fishingService;
    public MiningService MiningService => miningService;
    public Inventory Bag => InventoryManager.Instance.Inventory;
    public int CoinBalance => CurrencyManager.Instance.GetCurrency(goldCurrency);

    private bool showDebugPanel;

    [Header("Data (optional — falls back to built-in defaults if empty)")]
    [SerializeField] private CropDefinition[] cropDefinitions;
    [SerializeField] private FarmBalanceData farmBalance;
    [SerializeField] private FishingBalanceData fishingBalance;
    [SerializeField] private MiningBalanceData miningBalance;
    [SerializeField] private OtterVisitBalanceData otterVisitBalance;

    [Header("Save")]
    [SerializeField] private float autoSaveIntervalSec = 30f;

    [Header("Currency")]
    [SerializeField] private Currency goldCurrency;

    private const string DefaultOtterId = "otter_001";
    private const string PlotId = "plot_1";
    private const string DefaultCropId = "crop_carrot";
    // Save ids renamed since release: old -> new. Applied to the bag before it loads.
    private static readonly (string from, string to)[] LegacyItemIds =
    {
        ("fish_basic", "fish_mackerel"),
    };

    // InventoryManager survives scene loads but every zone scene's GameManager
    // reads the save again, and LoadFromSave adds to the bag — so only the
    // first GameManager to meet a given InventoryManager fills it.
    private static InventoryManager loadedInventory;
    // Same for the collection and quests (their LoadFromSave adds too).
    private static CollectionManager loadedCollection;
    private static QuestManager loadedQuests;

    // Offline production covers the time the app was closed, which ends when
    // the app starts — not when the first zone scene with a GameManager opens
    // (the game starts in the plaza, which has none). Handled once per launch;
    // later zone scenes only catch the farm up on time spent elsewhere.
    private static DateTime appLaunchUtc;
    private static bool launchAbsenceHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnAppLaunch()
    {
        appLaunchUtc = DateTime.UtcNow;
        launchAbsenceHandled = false;
    }

    private SaveData save;
    private SaveService saveService;
    private FarmService farmService;
    private FishingService fishingService;
    private MiningService miningService;
    private OfflineProductionService offlineProduction;
    private GameUI gameUI;
    private readonly HashSet<string> warnedMissingItems = new HashSet<string>();
    private bool fullBagCatchAlertShown;
    private bool fullBagFindAlertShown;

    private float autoSaveTimer;
    private float pendingOfflineElapsedSec;
    private double pendingAbsenceSec;
    // Set while the app is in the background (mobile), to measure the absence.
    private DateTime? pausedAtUtc;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Without a currency nothing can start (HUD, sale, upgrades). Stop
        // here with one clear error instead of leaving Instance half-built,
        // which made every view throw a NullReferenceException each frame.
        if (goldCurrency == null)
        {
            Debug.LogError("[GameManager] Gold Currency is not assigned — drag " +
                           "'Assets/Scriptable Obejects/Gold.asset' into this GameManager's Gold Currency field.", this);
            enabled = false;
            return;
        }

        // CurrencyManager is placed in each zone scene (DontDestroyOnLoad, runs
        // first via DefaultExecutionOrder) — it is not created on demand.
        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("[GameManager] No CurrencyManager in the scene — add one with Gold in its " +
                           "All Currencies list.", this);
            enabled = false;
            return;
        }
        // Same for the bag: InventoryManager (DontDestroyOnLoad, runs first)
        // owns it, with its InventoryConfig/ItemDatabase assigned.
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[GameManager] No InventoryManager in the scene — add one with " +
                           "'Assets/Scriptable Obejects/Inventory/InventoryConfig.asset' as its Config.", this);
            enabled = false;
            return;
        }
        Instance = this;

        EnsureDefaultData();

        saveService = new SaveService();
        var loadStatus = saveService.Load(out save);
        if (save == null) save = CreateNewSave();

        ComputePendingOfflineElapsed();

        LoadGoldFromSave();
        CurrencyHud.Show(goldCurrency);
        bool seedsMoved = LoadInventoryOnce();
        // After the bag, so the collection also marks what the bag holds, and
        // before Start's offline harvest, so quests count it.
        LoadGlobalProgressOnce();
        farmService = new FarmService(save, cropDefinitions, farmBalance, GetSeedCount, TryConsumeSeed);
        fishingService = new FishingService(save, fishingBalance);
        miningService = new MiningService(save, miningBalance);
        offlineProduction = new OfflineProductionService(cropDefinitions, farmBalance, fishingBalance,
            otterVisitBalance, miningBalance);
        gameUI = GameUI.Create(this);

        ReportLoadStatus(loadStatus);

        // Seeds just moved out of save.seeds into the bag — write that out now
        // so a crash can't grant them again from the old file.
        if (seedsMoved) SaveNow();
    }

    private void ReportLoadStatus(SaveLoadStatus status)
    {
        switch (status)
        {
            case SaveLoadStatus.RecoveredFromBackup:
                gameUI.ShowAlert("저장 데이터에 문제가 있어서\n이전 저장 시점으로 복구했어요.");
                break;

            case SaveLoadStatus.Corrupted:
                // Freeze the world (farm ticks, otter coroutines) while the
                // player decides — the uGUI popup runs on unscaled time.
                // SaveService already refuses to write in this state.
                Time.timeScale = 0f;
                gameUI.ShowChoice("저장 데이터 오류",
                    "저장 데이터를 불러오지 못했어요.\n새로 시작하면 이전 진행 상황은 사라져요.",
                    "새로 시작", StartOverAfterCorruption,
                    "종료", QuitGame);
                break;
        }
    }

    private void StartOverAfterCorruption()
    {
        saveService.DiscardCorruptedAndStartOver();
        Time.timeScale = 1f;
        SaveNow();
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Start()
    {
        if (pendingOfflineElapsedSec > 0f)
        {
            farmService.Tick(pendingOfflineElapsedSec);
            pendingOfflineElapsedSec = 0f;
            SaveNow();
        }

        if (pendingAbsenceSec > 0)
        {
            RunOfflineProduction(pendingAbsenceSec);
            pendingAbsenceSec = 0;
        }
    }

    private void Update()
    {
        farmService.Tick(Time.deltaTime);

        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveIntervalSec)
        {
            autoSaveTimer = 0f;
            SaveNow();
        }
    }

    // The GameManager dies with its zone scene, so save before the navigator
    // loads the next one. The next zone's GameManager reads this save, and
    // lastSaveUtc lets the farm catch up on time spent in scenes without one.
    // The Instance check skips a duplicate that Awake is about to destroy.
    // Sales from anywhere (bag popup, TrySell, SellAll) come through
    // OnItemSold, so lifetimeSales is kept up here and nowhere else.
    private void OnEnable()
    {
        if (Instance != this) return;
        SceneNavigator.BeforeLeave += SaveNow;
        InventoryManager.Instance.OnItemSold += HandleItemSold;
    }

    private void OnDisable()
    {
        SceneNavigator.BeforeLeave -= SaveNow;
        if (InventoryManager.Instance != null) InventoryManager.Instance.OnItemSold -= HandleItemSold;
    }

    private void HandleItemSold(ItemSoldEvent e)
    {
        save.lifetimeSales += e.TotalPrice;
    }

    // Back from the background: planted slots grow through the absence (up
    // to AwaitingHarvest, like on launch) and offline production runs for it.
    // Frame time is capped, so Update alone would drop that time.
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            pausedAtUtc = DateTime.UtcNow;
            SaveNow();
            return;
        }

        if (pausedAtUtc == null) return;
        double away = (DateTime.UtcNow - pausedAtUtc.Value).TotalSeconds;
        pausedAtUtc = null;
        if (away <= 0) return;

        farmService.Tick((float)away);
        RunOfflineProduction(away);
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }

    private void EnsureDefaultData()
    {
        if (cropDefinitions == null || cropDefinitions.Length == 0)
        {
            var carrot = ScriptableObject.CreateInstance<CropDefinition>();
            carrot.cropId = DefaultCropId;
            carrot.displayName = "당근";
            carrot.baseDurationSec = 30f;
            carrot.yieldCount = 5;
            carrot.sellPrice = 2;
            carrot.seedType = SeedType.Permanent;
            cropDefinitions = new[] { carrot };
            Debug.Log("[GameManager] No CropDefinition assigned — using built-in carrot default.");
        }

        if (farmBalance == null)
        {
            farmBalance = ScriptableObject.CreateInstance<FarmBalanceData>();
            Debug.Log("[GameManager] No FarmBalanceData assigned — using built-in defaults.");
        }

        if (fishingBalance == null)
        {
            fishingBalance = ScriptableObject.CreateInstance<FishingBalanceData>();
            Debug.Log("[GameManager] No FishingBalanceData assigned — using built-in defaults.");
        }

        if (miningBalance == null)
        {
            miningBalance = ScriptableObject.CreateInstance<MiningBalanceData>();
            Debug.Log("[GameManager] No MiningBalanceData assigned — using built-in defaults.");
        }

        if (otterVisitBalance == null)
        {
            otterVisitBalance = ScriptableObject.CreateInstance<OtterVisitBalanceData>();
        }
    }

    // ------------------------------------------------------------- inventory

    // Returns true if seeds were moved into the bag (the save needs writing).
    private bool LoadInventoryOnce()
    {
        var manager = InventoryManager.Instance;
        if (loadedInventory == manager) return false;
        loadedInventory = manager;

        save.inventory ??= new List<ItemStack>();
        RenameLegacyItemIds(save.inventory);
        manager.LoadFromSave(save.inventory, save.inventoryCapacity);

        return MoveLegacySeedsToBag() | GrantStarterSeeds();
    }

    // CollectionManager and QuestManager live on GlobalUI. Also called from
    // SaveNow: if GlobalUI was only auto-created after this Awake, loading
    // there keeps WriteToSave from replacing the saved lists with empty ones.
    private void LoadGlobalProgressOnce()
    {
        var collection = CollectionManager.Instance;
        if (collection != null && loadedCollection != collection)
        {
            loadedCollection = collection;
            save.collection ??= new List<CollectionSaveEntry>();
            collection.LoadFromSave(save.collection);
        }

        var quests = QuestManager.Instance;
        if (quests != null && loadedQuests != quests)
        {
            loadedQuests = quests;
            save.quests ??= new List<QuestSaveEntry>();
            quests.LoadFromSave(save.quests);
        }
    }

    private static void RenameLegacyItemIds(List<ItemStack> stacks)
    {
        foreach (var stack in stacks)
        {
            foreach (var (from, to) in LegacyItemIds)
            {
                if (stack.itemId == from) stack.itemId = to;
            }
        }
    }

    // Before seeds were bag items, consumable stock lived in save.seeds keyed
    // by cropId. Move each entry into the bag as its seed_* item. An entry
    // whose seed item has no ItemDefinition yet stays put until it does.
    private bool MoveLegacySeedsToBag()
    {
        save.seeds ??= new List<ItemStack>();
        save.starterSeedsGranted ??= new List<string>();
        bool moved = false;

        for (int i = save.seeds.Count - 1; i >= 0; i--)
        {
            var stack = save.seeds[i];
            var crop = Array.Find(cropDefinitions, c => c != null && c.cropId == stack.itemId);
            if (crop == null || !TryFindItem(crop.SeedItemId, out var seedItem)) continue;

            if (stack.quantity > 0) PutInBagIgnoringCapacity(seedItem, stack.quantity);
            // Having an entry at all meant the starter stock was already given.
            if (!save.starterSeedsGranted.Contains(crop.cropId)) save.starterSeedsGranted.Add(crop.cropId);
            save.seeds.RemoveAt(i);
            moved = true;
        }
        return moved;
    }

    // Each consumable crop's starting seeds go in the bag the first time the
    // save sees that crop. Skipped (and retried next launch) while the seed
    // item has no ItemDefinition.
    private bool GrantStarterSeeds()
    {
        bool granted = false;
        foreach (var crop in cropDefinitions)
        {
            if (crop == null || crop.seedType != SeedType.Consumable) continue;
            if (save.starterSeedsGranted.Contains(crop.cropId)) continue;
            if (!TryFindItem(crop.SeedItemId, out var seedItem)) continue;

            if (crop.initialSeedCount > 0) PutInBagIgnoringCapacity(seedItem, crop.initialSeedCount);
            save.starterSeedsGranted.Add(crop.cropId);
            granted = true;
        }
        return granted;
    }

    // Seeds the player already owned must not vanish because the bag is full,
    // so a new kind goes in over capacity (the same way LoadFromSave keeps an
    // over-full old save).
    private void PutInBagIgnoringCapacity(ItemDefinition item, int amount)
    {
        if (Bag.GetCount(item) > 0 || Bag.FreeSlots > 0) Bag.Add(item, amount, ItemChangeReason.Grant);
        else Bag.LoadItem(item, amount);
    }

    // Save ids -> ItemDefinition. An id with no definition yet is logged once
    // and treated as "can't go in the bag" by every caller.
    private bool TryFindItem(string itemId, out ItemDefinition item)
    {
        if (InventoryManager.Instance.Config.ItemDatabase.TryGet(itemId, out item)) return true;

        if (warnedMissingItems.Add(itemId))
            Debug.LogWarning($"[GameManager] No ItemDefinition for '{itemId}' in the ItemDatabase.");
        return false;
    }

    private int GetSeedCount(string cropId)
    {
        var crop = farmService.GetCrop(cropId);
        return crop != null && TryFindItem(crop.SeedItemId, out var seedItem) ? Bag.GetCount(seedItem) : 0;
    }

    private bool TryConsumeSeed(string cropId)
    {
        var crop = farmService.GetCrop(cropId);
        return crop != null && TryFindItem(crop.SeedItemId, out var seedItem) &&
               Bag.TryRemove(seedItem, 1, ItemChangeReason.Plant);
    }

    private SaveData CreateNewSave()
    {
        var data = new SaveData();
        data.plots.Add(new PlotSaveData
        {
            plotId = PlotId,
            unlocked = true,
            workerId = DefaultOtterId,
            slots = PlotSaveData.CreateEmptySlots()
        });
        data.otters.Add(new OtterSaveData
        {
            instanceId = DefaultOtterId,
            speciesId = "otter_base",
            unlocked = true
        });
        return data;
    }

    private void ComputePendingOfflineElapsed()
    {
        // No 8h cap / monotonic-clock guard yet (design doc 8.2-8.3) — that
        // hardening is M3 scope. A rolled-back clock just yields elapsed <= 0,
        // which Tick() already treats as a no-op.
        if (string.IsNullOrEmpty(save.lastSaveUtc)) return;

        if (!DateTime.TryParse(save.lastSaveUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var last))
        {
            return;
        }

        double elapsed = (DateTime.UtcNow - last).TotalSeconds;
        if (elapsed > 0)
        {
            pendingOfflineElapsedSec = (float)elapsed;
        }

        if (!launchAbsenceHandled)
        {
            launchAbsenceHandled = true;
            pendingAbsenceSec = Math.Max(0, (appLaunchUtc - last.ToUniversalTime()).TotalSeconds);
        }
    }

    // ------------------------------------------------------------- offline

    // Registered crops and rod catches for time the game wasn't running, then
    // the return popup. Saved right away so a crash can't hand them out twice.
    private void RunOfflineProduction(double absenceSec)
    {
        var report = offlineProduction.Run(save, absenceSec, new OfflineBag(this));
        SaveNow();
        if (report.HasAnything) gameUI.ShowOfflineReport(report, ItemDisplayName);
    }

    // ---- farm NPC registrations (what grows offline)

    public bool IsOfflineFarmUnlocked => offlineProduction.IsFarmUnlocked(save);

    public int OfflineFarmRegistrationLimit => OfflineProductionService.RegistrationLimit(save);

    // Null for an unused registration.
    public CropDefinition GetOfflineCrop(int index)
    {
        if (index < 0 || index >= save.offlineFarmSlots.Count) return null;
        string cropId = save.offlineFarmSlots[index].cropId;
        return string.IsNullOrEmpty(cropId) ? null : farmService.GetCrop(cropId);
    }

    // cropId null/empty clears the registration. Changing the crop restarts
    // its grow cycle.
    public void SetOfflineCrop(int index, string cropId)
    {
        if (index < 0 || index >= OfflineFarmRegistrationLimit) return;
        while (save.offlineFarmSlots.Count <= index) save.offlineFarmSlots.Add(new OfflineFarmSlotSaveData());

        var slot = save.offlineFarmSlots[index];
        if (slot.cropId == cropId) return;
        slot.cropId = string.IsNullOrEmpty(cropId) ? null : cropId;
        slot.progressSec = 0f;
        SaveNow();
    }

    // Called by OfflineFarmNpcView when the player taps the farm NPC.
    public void RequestOfflineFarmPrompt()
    {
        gameUI.ShowOfflineFarmPrompt();
    }

    public float OfflineSlowdown => farmBalance.offlineSlowdown;

    private string ItemDisplayName(string itemId) =>
        TryFindItem(itemId, out var item) ? item.DisplayName : itemId;

    private class OfflineBag : IOfflineBag
    {
        private readonly GameManager game;

        public OfflineBag(GameManager game) => this.game = game;

        public int GetCount(string itemId) =>
            game.TryFindItem(itemId, out var item) ? game.Bag.GetCount(item) : 0;

        public bool TryRemove(string itemId, int amount) =>
            game.TryFindItem(itemId, out var item) && game.Bag.TryRemove(item, amount, ItemChangeReason.Plant);

        public int Add(string itemId, int amount)
        {
            if (!game.TryFindItem(itemId, out var item)) return 0;
            var reason = game.fishingService.IsFish(itemId) || itemId == game.fishingBalance.trashItemId
                ? ItemChangeReason.Fishing
                : game.miningService.IsOre(itemId) ? ItemChangeReason.Mining : ItemChangeReason.Harvest;
            return game.Bag.Add(item, amount, reason);
        }
    }

    private void SaveNow()
    {
        WriteGoldToSave();
        InventoryManager.Instance.WriteToSave(save.inventory);
        save.inventoryCapacity = Bag.Capacity;
        LoadGlobalProgressOnce();
        if (CollectionManager.Instance != null) CollectionManager.Instance.WriteToSave(save.collection);
        if (QuestManager.Instance != null) QuestManager.Instance.WriteToSave(save.quests);
        saveService.Save(save);
    }

    // The currency system doesn't know about SaveData — the balance is
    // copied in and out here. Only Gold is ours; other entries are kept.
    private void LoadGoldFromSave()
    {
        var entry = save.currencies.Find(c => c.currencyId == goldCurrency.CurrencyID);
        CurrencyManager.Instance.SetBalance(goldCurrency, entry != null ? entry.amount : 0);
    }

    private void WriteGoldToSave()
    {
        var entry = save.currencies.Find(c => c.currencyId == goldCurrency.CurrencyID);
        if (entry == null)
        {
            entry = new CurrencyBalance { currencyId = goldCurrency.CurrencyID };
            save.currencies.Add(entry);
        }
        entry.amount = CurrencyManager.Instance.GetCurrency(goldCurrency);
    }

    public void ToggleDebugPanel()
    {
        showDebugPanel = !showDebugPanel;
    }

    // Called by FurrowSlotView when the player taps an empty slot — opens the
    // crop-selection prompt for that slot instead of planting directly.
    public void RequestPlantPrompt(int plotIndex, int slotIndex)
    {
        gameUI.ShowSeedPrompt(plotIndex, slotIndex);
    }

    // Called by FurrowSlotView when the player taps a planted (growing or
    // awaiting-harvest) slot.
    public void RequestCropChangePrompt(int plotIndex, int slotIndex)
    {
        gameUI.ShowCropChangePrompt(plotIndex, slotIndex);
    }

    // Called by PlotView when the player taps a locked plot — opens the
    // coin-unlock confirmation prompt for that plot.
    public void RequestUnlockPrompt(int plotIndex)
    {
        if (farmService.IsPlotUnlocked(plotIndex)) return;
        gameUI.ShowUnlockPrompt(plotIndex);
    }

    public static string NoSeedMessage(CropDefinition crop) => $"{crop.displayName}의 모종이 없습니다!";

    public PlantResult PlantFromPrompt(int plotIndex, int slotIndex, string cropId)
    {
        var result = farmService.Plant(plotIndex, slotIndex, cropId);
        if (result == PlantResult.Planted) SaveNow();
        return result;
    }

    public bool TryUnlockPlot(int plotIndex)
    {
        if (!farmService.TryUnlockPlot(plotIndex, CurrencyManager.Instance, goldCurrency)) return false;
        SaveNow();
        return true;
    }

    // Player's "change crop" confirmation — the old crop is thrown away.
    public void DiscardSlot(int plotIndex, int slotIndex)
    {
        if (farmService.ClearSlot(plotIndex, slotIndex)) SaveNow();
    }

    // ------------------------------------------------------------- fishing

    // Generic 예/아니오 popup — used by the fishing spot (start) and the
    // fishing otter (stop).
    public void ShowConfirm(string title, string message, Action onYes)
    {
        gameUI.ShowConfirm(title, message, onYes);
    }

    // Called once by the fishing scene; the farm scene has no rod button.
    public void ShowRodUpgradeButton()
    {
        gameUI.ShowRodUpgradeButton();
    }

    // Single switch for "the otter is on fishing duty". FishingOtterController
    // and FishingSpotView both follow this flag rather than each other.
    public void SetFishingActive(bool active)
    {
        if (fishingService.IsActive == active) return;
        fishingService.SetActive(active);
        SaveNow();
    }

    // Called when the pull animation lands the catch. With no room in the bag
    // the catch is thrown back; the player is told once per full-bag spell,
    // not on every cast. A catch with no ItemDefinition yet is dropped too
    // (TryFindItem logs it) — that isn't the bag's fault, so no alert.
    public void AddFishingCatch(string itemId)
    {
        if (!TryFindItem(itemId, out var item)) return;

        if (Bag.GetAddableAmount(item) > 0)
        {
            Bag.Add(item, 1, ItemChangeReason.Fishing);
            fullBagCatchAlertShown = false;
            SaveNow();
            return;
        }

        if (!fullBagCatchAlertShown)
        {
            fullBagCatchAlertShown = true;
            gameUI.ShowAlert("가방이 가득 차서\n잡은 것을 놓아줬어요.");
        }
    }

    // -------------------------------------------------------------- mining

    // Single switch for "the miner otter is inside the mine", like
    // SetFishingActive. MinerOtterController, MineEntranceView and
    // MineEmoteView all follow this flag.
    public bool IsMiningActive => miningService.IsActive;

    public void SetMiningActive(bool active)
    {
        if (miningService.IsActive == active) return;
        miningService.SetActive(active);
        SaveNow();
    }

    // Called once by the mine scene; other scenes have no pickaxe button.
    public void ShowPickaxeUpgradeButton()
    {
        gameUI.ShowPickaxeUpgradeButton();
    }

    public bool TryUpgradePickaxe()
    {
        if (!miningService.TryUpgradePickaxe(CurrencyManager.Instance, goldCurrency)) return false;
        SaveNow();
        return true;
    }

    // Called by MinerOtterController each time a find comes up. Same rules as
    // AddFishingCatch: no room in the bag drops it with one alert per
    // full-bag spell. Returns the item that went into the bag (for the
    // pop-up above the bubble), or null if nothing did.
    public ItemDefinition AddMiningFind(string itemId)
    {
        if (!TryFindItem(itemId, out var item)) return null;

        if (Bag.GetAddableAmount(item) > 0)
        {
            Bag.Add(item, 1, ItemChangeReason.Mining);
            fullBagFindAlertShown = false;
            SaveNow();
            return item;
        }

        if (!fullBagFindAlertShown)
        {
            fullBagFindAlertShown = true;
            gameUI.ShowAlert("가방이 가득 차서\n캔 광석을 두고 왔어요.");
        }
        return null;
    }

#if UNITY_EDITOR
    // Dev tool (OtterKingdom > Dev > Offline Test) — reads and edits the live
    // save while playing, since this GameManager would overwrite file edits.
    public SaveData DevSave => save;

    public void DevSetLevels(int farmLevel, int rodLevel)
    {
        save.farmLevel = Mathf.Clamp(farmLevel, 1, farmBalance.maxFarmLevel);
        save.rodLevel = Mathf.Clamp(rodLevel, 1, fishingBalance.MaxRodLevel);
        SaveNow();
    }

    // Locks every plot but the first again and empties its slots (crops are
    // lost, no seed refund). Views and the farmer otter re-read the unlock
    // state every frame, so this shows up without a reload.
    public void DevRelockExtraPlots()
    {
        RelockExtraPlots(save);
        SaveNow();
    }

    public static void RelockExtraPlots(SaveData data)
    {
        for (int i = 1; i < data.plots.Count; i++)
        {
            data.plots[i].unlocked = false;
            foreach (var slot in data.plots[i].slots)
            {
                slot.cropId = null;
                slot.state = FurrowSlotState.Empty;
                slot.remainingSec = 0f;
            }
        }
    }
#endif

    public bool TryUpgradeRod()
    {
        if (!fishingService.TryUpgradeRod(CurrencyManager.Instance, goldCurrency)) return false;
        SaveNow();
        return true;
    }

    // Whether the whole yield of this ready slot fits in the bag. The farmer
    // otter skips slots that don't, so it doesn't walk to them forever.
    public bool CanStoreHarvest(int plotIndex, int slotIndex)
    {
        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        return crop != null && TryFindItem(crop.cropId, out var item) &&
               Bag.GetAddableAmount(item) >= crop.yieldCount;
    }

    // Shared harvest entry point — used by the debug-panel button and by
    // FarmerOtterController once its harvest animation finishes. If the yield
    // doesn't fit in the bag nothing happens and the slot stays ready. A
    // successful harvest immediately replants the same crop; if a consumable
    // crop is out of seeds the slot is left empty and the player is told.
    public void HarvestSlot(int plotIndex, int slotIndex)
    {
        if (!CanStoreHarvest(plotIndex, slotIndex)) return;

        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        var harvested = farmService.Harvest(plotIndex, slotIndex);
        if (harvested.Count == 0) return;

        foreach (var stack in harvested)
        {
            if (TryFindItem(stack.itemId, out var item)) Bag.Add(item, stack.quantity, ItemChangeReason.Harvest);
        }

        if (crop != null && farmService.Plant(plotIndex, slotIndex, crop.cropId) == PlantResult.NoSeed)
        {
            gameUI.ShowAlert(NoSeedMessage(crop));
        }

        SaveNow();
    }

    // A slot is "covered" by a screen point if that point lands inside any
    // currently-open OnGUI panel — used by FurrowSlotView so clicking a panel
    // button doesn't also register as a click on whatever slot is underneath.
    public bool IsScreenPointOverUI(Vector2 screenPos)
    {
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        if (showDebugPanel && DebugPanelRect.Contains(guiPos)) return true;
        return gameUI != null && gameUI.IsBlocking(screenPos);
    }

    private void OnGUI()
    {
        if (!showDebugPanel) return;

        GUILayout.BeginArea(DebugPanelRect, GUI.skin.box);

        GUILayout.Label($"코인: {CurrencyManager.Instance.GetCurrency(goldCurrency)}");
        GUILayout.Label($"농사 레벨: {save.farmLevel}");

        for (int plotIndex = 0; plotIndex < farmService.Plots.Count; plotIndex++)
        {
            GUILayout.Space(10);
            GUILayout.Label($"=== 밭 {plotIndex + 1} ===");

            if (!farmService.IsPlotUnlocked(plotIndex))
            {
                GUILayout.Label("잠김");
                continue;
            }

            var plot = farmService.Plots[plotIndex];
            for (int slotIndex = 0; slotIndex < plot.SlotCount; slotIndex++)
            {
                DrawSlotDebugRow(plotIndex, slotIndex);
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("=== 가방 ===");
        GUILayout.Label($"칸 {Bag.UsedSlots}/{Bag.Capacity}");
        if (Bag.UsedSlots == 0)
        {
            GUILayout.Label("아직 모은 생산물이 없어요");
        }
        else
        {
            foreach (var pair in Bag.Counts)
            {
                GUILayout.Label($"{pair.Key.DisplayName} x{pair.Value}");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("=== 강화 ===");
        if (farmService.CanUpgrade)
        {
            GUI.enabled = CurrencyManager.Instance.GetCurrency(goldCurrency) >= farmService.NextUpgradeCost;
            if (GUILayout.Button($"농사 강화 ({farmService.NextUpgradeCost} 코인)"))
            {
                if (farmService.TryUpgrade(CurrencyManager.Instance, goldCurrency))
                {
                    SaveNow();
                }
            }
            GUI.enabled = true;
        }
        else
        {
            GUILayout.Label("최대 레벨");
        }

        GUILayout.Space(10);
        if (GUILayout.Button("지금 저장"))
        {
            SaveNow();
        }

        GUILayout.EndArea();
    }

    private void DrawSlotDebugRow(int plotIndex, int slotIndex)
    {
        var state = farmService.GetSlotState(plotIndex, slotIndex);
        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        string cropName = crop != null ? crop.displayName : "-";

        switch (state)
        {
            case FurrowSlotState.Empty:
                GUILayout.Label($"슬롯 {slotIndex + 1}: 비어있음");
                break;
            case FurrowSlotState.Growing:
                float remaining = farmService.GetSlotRemainingSec(plotIndex, slotIndex);
                GUILayout.Label($"슬롯 {slotIndex + 1}: {cropName} 성장 중 (남은 시간 {remaining:F1}초)");
                break;
            case FurrowSlotState.AwaitingHarvest:
                GUILayout.BeginHorizontal();
                GUILayout.Label($"슬롯 {slotIndex + 1}: {cropName} 수확 대기");
                // Manual override — FarmerOtterController normally does this.
                if (GUILayout.Button("수확 (수동)", GUILayout.Width(140)))
                {
                    HarvestSlot(plotIndex, slotIndex);
                }
                GUILayout.EndHorizontal();
                break;
        }
    }
}
