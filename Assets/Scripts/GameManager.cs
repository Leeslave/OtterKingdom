using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

// M1 vertical-slice bootstrap: loads/creates the save, wires the plain-C#
// services, drives the farm production tick, and exposes a temporary OnGUI
// debug UI so the loop (plant -> grow -> harvest -> sell -> upgrade) can
// be verified with zero scene/prefab setup. Replace the OnGUI block with real
// uGUI views in M4; the services underneath should not need to change.
//
// Every zone scene (Farm, Fishing, Mine, Plaza) has its own instance of the
// GameManager prefab (OtterKingdom > Tools > Setup GameManager Prefab), so the
// save, coins, inventory and sale UI are shared and the data can't drift. The
// farm keeps ticking in every scene — crops grow and the working farmer
// harvests and replants them wherever the player is (FarmService); the farm
// scene's otter only shows it.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly Rect DebugPanelRect = new Rect(20, 20, 440, 560);

    public bool IsDebugPanelOpen => showDebugPanel;
    // A zone popup (offline report, confirm, alert) is up — e.g. new plaza
    // visitors wait for it to close before walking in.
    public bool IsModalOpen => gameUI != null && gameUI.IsModalOpen;
    public FarmService FarmService => farmService;
    public IReadOnlyList<CropDefinition> Crops => cropDefinitions;
    public FishingService FishingService => fishingService;
    public MiningService MiningService => miningService;
    public Inventory Bag => InventoryManager.Instance.Inventory;
    public int CoinBalance => CurrencyManager.Instance.GetCurrency(goldCurrency);
    public Currency GoldCurrency => goldCurrency;

    private bool showDebugPanel;

    [Header("Data (optional — falls back to built-in defaults if empty)")]
    [SerializeField] private CropDefinition[] cropDefinitions;
    [SerializeField] private FarmBalanceData farmBalance;
    [SerializeField] private FishingBalanceData fishingBalance;
    [SerializeField] private MiningBalanceData miningBalance;
    [SerializeField] private OtterVisitBalanceData otterVisitBalance;

    [Header("Save")]
    [SerializeField] private float autoSaveIntervalSec = 30f;

    [Header("Offline")]
    [Tooltip("Shorter absences give no offline production (and no return popup).")]
    [SerializeField] private float minOfflineAbsenceSec = 180f;

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
    // Same for the collection and quests (their LoadFromSave adds too),
    // the profile (so a later scene can't roll the level back) and the
    // settlement (it grants the new-game materials once).
    private static CollectionManager loadedCollection;
    private static QuestManager loadedQuests;
    private static ProfileManager loadedProfile;
    private static SettlementManager loadedSettlement;
    private static DecorManager loadedDecor;

    // Offline production covers the time the app was closed, which ends when
    // the app starts — not when the first zone scene with a GameManager opens
    // (the game starts in the plaza, which has none). Handled once per launch;
    // later zone scenes only catch the farm up on time spent elsewhere.
    private static DateTime appLaunchUtc;
    private static bool launchAbsenceHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnAppLaunch()
    {
        appLaunchUtc = GameClock.UtcNow;
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
    // The pending time includes the app being closed (first zone scene after
    // launch): crops only grow through it. Otherwise it is a scene load, and
    // the farmer keeps working through it like any online time.
    private bool pendingElapsedIsLaunch;
    private double pendingAbsenceSec;
    // The farmer harvested something this frame: save once at the end.
    private bool farmChanged;
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
        // Odd values (missing lists, negative/NaN timers, level 0) are fixed
        // before anything reads the save.
        var fixes = SaveDataSanitizer.Sanitize(save);
        if (fixes.Count > 0) Debug.LogWarning($"[GameManager] Fixed save values: {string.Join(", ", fixes)}");
        if (save.schemaVersion > SaveData.CurrentSchemaVersion)
            Debug.LogWarning($"[GameManager] Save is from a newer version (schema {save.schemaVersion} > {SaveData.CurrentSchemaVersion}).");
        // Older saves are brought up to the current schema here, once
        // (first-plant guide, zone tutorials, legacy settlement — SaveMigrations).
        var migrated = SaveMigrations.Run(save);
        if (migrated.Count > 0) Debug.Log($"[GameManager] Save migrated: {string.Join(", ", migrated)}");

        ComputePendingOfflineElapsed();

        LoadCurrenciesFromSave();
        // Gold shows in GlobalUI's top bar now; the old CurrencyHud strip is no longer shown.
        bool seedsMoved = LoadInventoryOnce();
        // After the bag, so the collection also marks what the bag holds, and
        // before Start's offline harvest, so quests count it.
        LoadGlobalProgressOnce();
        farmService = new FarmService(save, cropDefinitions, farmBalance, new FarmHost(this));
        farmService.Harvested += HandleFarmHarvested;
        farmService.NoticeRaised += HandleFarmNotice;
        fishingService = new FishingService(save, fishingBalance);
        miningService = new MiningService(save, miningBalance);
        offlineProduction = new OfflineProductionService(cropDefinitions, farmBalance, fishingBalance,
            otterVisitBalance, miningBalance);
        gameUI = GameUI.Create(this);
        // Set here, before any view's Start asks for the first-plant guide.
        zoneTutorialPending = ZoneTutorials.Has(CurrentZoneId) && !save.tutorialsDone.Contains(CurrentZoneId);

        ReportLoadStatus(loadStatus);

        // Seeds just moved out of save.seeds into the bag — write that out now
        // so a crash can't grant them again from the old file.
        if (seedsMoved) SaveNow();
    }

    private void ReportLoadStatus(SaveLoadStatus status)
    {
        if (status == SaveLoadStatus.RecoveredFromBackup || status == SaveLoadStatus.Corrupted)
            AnalyticsLog.Track("save_recovery", ("reason", status.ToString()),
                ("outcome", status == SaveLoadStatus.RecoveredFromBackup ? "backup" : "asked"));
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
        AnalyticsLog.Track("save_recovery", ("reason", nameof(SaveLoadStatus.Corrupted)), ("outcome", "start_over"));
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
        // GlobalUI is auto-created after the first scene's Awake (scene
        // loaded), so its managers may have missed Awake's load. Load them now,
        // before the scene's own Starts read the settlement (plaza houses).
        LoadGlobalProgressOnce();

        if (pendingOfflineElapsedSec > 0f)
        {
            // App closed: growth only (offline production has its own rules).
            // Scene load: the farmer kept working, like anywhere else.
            if (pendingElapsedIsLaunch) farmService.Grow(pendingOfflineElapsedSec);
            else farmService.Tick(pendingOfflineElapsedSec);
            pendingOfflineElapsedSec = 0f;
            farmChanged = false;
            SaveNow();
        }

        if (pendingAbsenceSec > 0)
        {
            RunOfflineProduction(pendingAbsenceSec);
            pendingAbsenceSec = 0;
        }

        if (zoneTutorialPending) StartCoroutine(PlayZoneTutorialWhenReady());
        // This zone's guide was already seen (e.g. reset by the dev tool): a
        // specialist placed here starts working right away.
        else StartSpecialistWork();
        if (CurrentZoneId == ZoneTutorials.Plaza && !save.tutorialsDone.Contains(ZoneTutorials.FairyShop))
            StartCoroutine(PlayFairyShopTutorialWhenReady());
    }

    private void Update()
    {
        farmService.Tick(Time.deltaTime);
        TickMining(Time.deltaTime);

        autoSaveTimer += Time.deltaTime;
        if (farmChanged || autoSaveTimer >= autoSaveIntervalSec)
        {
            autoSaveTimer = 0f;
            farmChanged = false;
            SaveNow();
        }
    }

    // ------------------------------------------------------------- farm work

    // A harvest went into the bag (from any zone): saved at the end of the
    // frame, so a crash can't hand it out twice or lose the replant.
    private void HandleFarmHarvested(int plotIndex, int slotIndex, string cropId, int amount)
    {
        farmChanged = true;
    }

    // Seed shortage / full bag, once when it starts — a non-blocking notice
    // in whichever zone the player is (GameNotices waits for a safe moment).
    private void HandleFarmNotice(FarmNotice notice)
    {
        farmChanged = true;
        GameNotices.Post(FarmNoticeText.Build(notice, cropId =>
        {
            var crop = farmService.GetCrop(cropId);
            return crop != null ? crop.displayName : cropId;
        }, InventoryManager.Instance != null && Bag.FreeSlots > 0));
    }

    private class FarmHost : IFarmHost
    {
        private readonly GameManager game;

        public FarmHost(GameManager game) => this.game = game;

        public int SeedCount(string cropId) => game.GetSeedCount(cropId);

        public bool TryConsumeSeed(string cropId) => game.TryConsumeSeed(cropId);

        public bool CanStoreHarvest(string cropId, int amount) =>
            game.TryFindItem(cropId, out var item) && game.Bag.GetAddableAmount(item) >= amount;

        public void StoreHarvest(string cropId, int amount)
        {
            if (!game.TryFindItem(cropId, out var item)) return;
            // Harvest bonus (granary, KingdomBonus) on the farmer's harvest only — offline farming stays as is.
            int added = game.Bag.Add(item, KingdomBonus.Amount(KingdomBonusKind.HarvestYield, amount), ItemChangeReason.Harvest);
            if (added > 0) HarvestStored?.Invoke(item, added);
        }

        public bool CanFarmerWork => CanProduceIn(FarmZoneId);
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
        SettlementManager.SaveRequested += SaveNow;
        DecorManager.SaveRequested += SaveNow;
        FairyShopPresenter.Opened += HandleFairyShopOpened;
        InventoryManager.Instance.OnItemSold += HandleItemSold;
    }

    private void OnDisable()
    {
        SceneNavigator.BeforeLeave -= SaveNow;
        SettlementManager.SaveRequested -= SaveNow;
        DecorManager.SaveRequested -= SaveNow;
        FairyShopPresenter.Opened -= HandleFairyShopOpened;
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
            pausedAtUtc = GameClock.UtcNow;
            SaveNow();
            return;
        }

        if (pausedAtUtc == null) return;
        // From the latest time already credited: a clock turned back and
        // forward again doesn't pay the same hours twice (see SaveService).
        var since = pausedAtUtc.Value;
        if (TryParseSaveTime(save.lastSaveUtc, out var lastSave) && lastSave > since) since = lastSave;
        double away = (GameClock.UtcNow - since).TotalSeconds;
        pausedAtUtc = null;
        if (away <= 0) return;

        // In the background the app is away like when it's closed: crops grow,
        // the online farmer doesn't harvest (offline production covers it).
        // Both count for at most 8 hours.
        farmService.Grow((float)OfflineProductionService.Credited(away));
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
        // The level first: quests decide what's open from it.
        var profile = ProfileManager.Instance;
        if (profile != null && loadedProfile != profile)
        {
            loadedProfile = profile;
            save.profile ??= new ProfileSaveData();
            profile.LoadFromSave(save.profile);
        }

        // Placed toys and decor regions: after the bag (storage = owned - placed),
        // before the settlement (an operational region unlocks its decor region).
        var decor = DecorManager.Instance;
        if (decor != null && loadedDecor != decor)
        {
            loadedDecor = decor;
            save.decor ??= new DecorSaveData();
            decor.LoadFromSave(save.decor);
        }

        // After the bag (the new-game materials go in it), before quests.
        var settlement = SettlementManager.Instance;
        if (settlement != null && loadedSettlement != settlement)
        {
            loadedSettlement = settlement;
            save.settlement ??= new SettlementSaveData();
            // P3 전 세이브: 요정 상점 안내를 이미 본 세이브는 요정을 그대로 둠 (농부 파견 전이어도)
            bool fairyShopSeen = save.tutorialsDone != null &&
                (save.tutorialsDone.Contains(ZoneTutorials.FairyShop) || save.tutorialsDone.Contains(ZoneTutorials.FairyShopIntro));
            settlement.LoadFromSave(save.settlement, fairyShopSeen);
        }

        var collection = CollectionManager.Instance;
        if (collection != null && loadedCollection != collection)
        {
            loadedCollection = collection;
            save.collection ??= new List<CollectionSaveEntry>();
            collection.LoadFromSave(save.collection);
            // Specialists assigned before the collection was loaded (old saves moved over on load).
            if (settlement != null) settlement.SyncSpecialistCollection();
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
        // Growth while away counts for at most 8 hours, like offline
        // production (OfflineProductionService.MaxCreditedSec) — a clock
        // pushed forward gets no more. A rolled-back clock just yields
        // elapsed <= 0, which Tick() already treats as a no-op.
        if (string.IsNullOrEmpty(save.lastSaveUtc)) return;

        if (!TryParseSaveTime(save.lastSaveUtc, out var last)) return;

        double elapsed = (GameClock.UtcNow - last).TotalSeconds;
        if (elapsed > 0)
        {
            pendingOfflineElapsedSec = (float)OfflineProductionService.Credited(elapsed);
        }

        if (!launchAbsenceHandled)
        {
            launchAbsenceHandled = true;
            pendingElapsedIsLaunch = true;
            pendingAbsenceSec = Math.Max(0, (appLaunchUtc - last.ToUniversalTime()).TotalSeconds);
        }
    }

    private static bool TryParseSaveTime(string text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(text)) return false;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) return false;
        utc = parsed.ToUniversalTime();
        return true;
    }

    // ------------------------------------------------------------- offline

    // Registered crops and rod catches for time the game wasn't running (at
    // least minOfflineAbsenceSec), then the return popup. Saved right away so a crash can't hand them out twice.
    private void RunOfflineProduction(double absenceSec)
    {
        if (absenceSec < minOfflineAbsenceSec) return;

        // 전문 해달(농부·광부·낚시꾼)이 일하기 전에는 그 장소의 오프라인 생산도 없음
        var report = offlineProduction.Run(save, absenceSec, new OfflineBag(this),
            CanProduceIn(FarmZoneId), CanProduceIn(MineZoneId), CanProduceIn(FishingZoneId));
        var settlement = SettlementManager.Instance;
        if (settlement != null)
        {
            settlement.CollectAbsenceNews(absenceSec, report.SettlementNews);
            report.NextGoal = settlement.NextGoalTitle;
        }
        SaveNow();
        AnalyticsLog.Track("offline_settled", ("elapsedSec", absenceSec),
            ("simulatedSec", OfflineProductionService.Credited(absenceSec)),
            ("capped", absenceSec > OfflineProductionService.MaxCreditedSec), ("newRewards", report.HasAnything));
        if (report.HasAnything) gameUI.ShowOfflineReport(report, ItemDisplayName);
    }

    // ---- farm NPC registrations (what grows offline)

    public bool IsOfflineFarmUnlocked => offlineProduction.IsFarmUnlocked(save) && CanProduceIn(FarmZoneId);

    // ------------------------------------------------------ production gate

    // Zone ids (= scene names) of the zones whose production waits for a
    // specialist otter (farmer / miner / fisher) to be assigned and start working.
    public const string FarmZoneId = "Farm";
    public const string MineZoneId = "Mine";
    public const string FishingZoneId = "Fishing";

    // The settlement decides: a zone with a developable region produces only
    // once its specialist is working there. Click, automatic and offline
    // production all check this. Test scenes without a settlement always can.
    public static bool CanProduceIn(string zoneId)
    {
        var settlement = SettlementManager.Instance;
        return settlement == null || settlement.CanProduceIn(zoneId);
    }

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
        WriteCurrenciesToSave();
        InventoryManager.Instance.WriteToSave(save.inventory);
        save.inventoryCapacity = Bag.Capacity;
        LoadGlobalProgressOnce();
        if (CollectionManager.Instance != null) CollectionManager.Instance.WriteToSave(save.collection);
        if (QuestManager.Instance != null) QuestManager.Instance.WriteToSave(save.quests);
        if (ProfileManager.Instance != null) ProfileManager.Instance.WriteToSave(save.profile ??= new ProfileSaveData());
        if (SettlementManager.Instance != null) SettlementManager.Instance.WriteToSave(save.settlement ??= new SettlementSaveData());
        if (DecorManager.Instance != null) DecorManager.Instance.WriteToSave(save.decor ??= new DecorSaveData());
        saveService.Save(save);
    }

    // The currency system doesn't know about SaveData — balances are copied
    // in and out here by currency ID. Every currency is saved (gold and
    // shells); a currency this scene's CurrencyManager hasn't used yet keeps
    // its saved entry untouched until it is first used.
    private void LoadCurrenciesFromSave()
    {
        var saved = new List<KeyValuePair<string, int>>();
        foreach (var entry in save.currencies)
        {
            if (entry != null)
                saved.Add(new KeyValuePair<string, int>(entry.currencyId, entry.amount));
        }
        CurrencyManager.Instance.LoadSavedBalances(saved);
        // Gold always has a wallet (old behaviour: missing from the save = 0).
        CurrencyManager.Instance.GetCurrency(goldCurrency);
    }

    private void WriteCurrenciesToSave()
    {
        foreach (var pair in CurrencyManager.Instance.Balances)
        {
            var entry = save.currencies.Find(c => c.currencyId == pair.Key.CurrencyID);
            if (entry == null)
            {
                entry = new CurrencyBalance { currencyId = pair.Key.CurrencyID };
                save.currencies.Add(entry);
            }
            entry.amount = pair.Value;
        }
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

    // Called by PlotView / FurrowSlotView when the player taps a locked plot
    // or furrow — offers the next furrow in order (one at a time), or says
    // which kingdom level opens it.
    public void RequestFurrowUnlockPrompt()
    {
        if (!farmService.TryGetNextFurrow(out _, out _, out var unlock)) return;
        if (KingdomLevel < unlock.requiredLevel)
        {
            gameUI.ShowAlert($"왕국 레벨 {unlock.requiredLevel}에\n다음 고랑을 열 수 있어요.");
            return;
        }
        gameUI.ShowFurrowUnlockPrompt(unlock.cost);
    }

    public static int KingdomLevel => ProfileManager.Instance != null ? ProfileManager.Instance.Level : 1;

    // Planting by hand with no seed. Points at the fairy shop, or says the
    // fairy who sells seeds is on her way if she hasn't come to the plaza yet.
    // A seed the shop only sells from a later kingdom level says which level.
    public static string NoSeedMessage(CropDefinition crop)
    {
        string head = $"{crop.displayName} 모종이 없어요.\n";
        var shop = FairyShopPresenter.Instance;
        int level = shop != null ? shop.RequiredLevelFor(crop.SeedItemId) : 0;
        if (FairyAccess.IsShopOpen && KingdomLevel < level)
            return head + $"왕국 레벨 {level}부터 요정 상점에서 살 수 있어요.";
        return head + FarmNoticeText.WhereToBuySeeds;
    }

    public PlantResult PlantFromPrompt(int plotIndex, int slotIndex, string cropId)
    {
        // No farmer working yet: nothing happens in the farm (plots stay hidden too).
        if (!CanProduceIn(FarmZoneId)) return PlantResult.Failed;
        bool firstCrop = IsFirstPlantGuideActive;
        var result = farmService.Plant(plotIndex, slotIndex, cropId);
        if (result == PlantResult.Planted)
        {
            // The very first crop grows in 10 seconds (design doc 4.2), once.
            if (firstCrop) farmService.SetGrowSeconds(plotIndex, slotIndex, FirstCropGrowSeconds);
            CompleteFirstPlantGuide();
            SaveNow();
        }
        return result;
    }

    // ------------------------------------------------------ first-plant guide

    // The very first guide (design doc 4.2): "당근을 심어 볼까?" with the
    // first plot highlighted, until the player plants anything.
    public const int FirstPlantGuidePlotIndex = 0;
    private const float FirstCropGrowSeconds = 10f;
    private const string FirstPlantGuideMessage = "당근을 심어 볼까?";

    public bool IsFirstPlantGuideActive => !save.firstPlantGuideDone;

    // Called by the guide plot's PlotView, so the guide only shows in the farm
    // scene (the fishing and mine scenes have a GameManager but no plots).
    // While the farm's zone tutorial is still to come, the guide waits and
    // shows when the tutorial ends.
    public void ShowFirstPlantGuide()
    {
        if (!IsFirstPlantGuideActive) return;
        if (zoneTutorialPending)
        {
            firstPlantGuideDeferred = true;
            return;
        }
        gameUI.ShowGuide(FirstPlantGuideMessage);
    }

    private void CompleteFirstPlantGuide()
    {
        if (!IsFirstPlantGuideActive) return;
        save.firstPlantGuideDone = true;
        gameUI.HideGuide();
    }

    public void DevResetFirstPlantGuide()
    {
        save.firstPlantGuideDone = false;
        ShowFirstPlantGuide();
        SaveNow();
    }

    // ------------------------------------------------------- zone tutorials

    // First visit to each zone (new games only): a step-by-step overlay that
    // points out the zone's objects and the shared UI. Seen or skipped, it is
    // marked in save.tutorialsDone and never comes back on its own.
    private bool zoneTutorialPending;
    private bool firstPlantGuideDeferred;

    private static string CurrentZoneId => SceneManager.GetActiveScene().name;

    private IEnumerator PlayZoneTutorialWhenReady()
    {
        // Let the scene's views and GlobalUI finish their first layout, then
        // wait out the arrival fade and any popup (offline report, save error).
        yield return null;
        yield return null;
        var navigator = FindAnyObjectByType<SceneNavigator>();
        while (gameUI.IsModalOpen || (navigator != null && navigator.IsTraveling)) yield return null;
        // A zone that still has to be cleared (the mine before its path is
        // open) teaches its real controls only after the player opens it.
        while (ZoneClearingView.IsWaiting) yield return null;
        // ...and after the "path opened" and level-up popups, not on top of them.
        while (gameUI.IsModalOpen || SettlementPresenter.IsCelebrating || LevelUpPresenter.IsBusy) yield return null;

        TutorialOverlay.Play(ZoneTutorials.For(CurrentZoneId, gameUI), CompleteZoneTutorial);
    }

    private void CompleteZoneTutorial()
    {
        zoneTutorialPending = false;
        if (!save.tutorialsDone.Contains(CurrentZoneId)) save.tutorialsDone.Add(CurrentZoneId);
        // The mine / farm guide is the specialist's first-production guide:
        // finishing (or skipping) it is what starts production.
        StartSpecialistWork();
        SaveNow();

        if (firstPlantGuideDeferred)
        {
            firstPlantGuideDeferred = false;
            ShowFirstPlantGuide();
        }
    }

    // A specialist (miner / farmer) placed in this zone starts working.
    private void StartSpecialistWork()
    {
        var settlement = SettlementManager.Instance;
        if (settlement != null && settlement.IsLoaded) settlement.StartSpecialistWork(CurrentZoneId);
    }

    // ---------------------------------------------------- feature tutorials

    // The fairy shop opens later than the plaza's first tutorial (with the
    // farm), so it gets its own short guide: in the plaza, once the fairy is
    // there and nothing else (zone tutorial, popups, level-up, travel) is on
    // screen. Flow: a short intro card → a highlight on the fairy that
    // doesn't block input → the player actually opens the shop = done.
    // Reading or skipping the card is not completion: only a real shop open
    // (FairyShopPresenter.Opened, from anywhere, before or after the guide)
    // saves FairyShop. Skip hides the guide for this plaza visit; leaving the
    // plaza or quitting also leaves it unfinished, so it comes back on the
    // next visit (the intro card only once: FairyShopIntro).
    private const float FeatureTutorialPollSeconds = 0.5f;
    private TutorialPointer fairyPointer;

    private IEnumerator PlayFairyShopTutorialWhenReady()
    {
        var navigator = FindAnyObjectByType<SceneNavigator>();
        FairyNpcView fairy = null;
        while (true)
        {
            yield return new WaitForSecondsRealtime(FeatureTutorialPollSeconds);
            if (save.tutorialsDone.Contains(ZoneTutorials.FairyShop)) yield break;
            if (FairyShopPresenter.Instance != null && FairyShopPresenter.Instance.IsOpen)
            {
                CompleteFairyShopTutorial();
                yield break;
            }
            if (zoneTutorialPending || gameUI.IsModalOpen || (navigator != null && navigator.IsTraveling)) continue;
            if (SettlementPresenter.IsCelebrating || LevelUpPresenter.IsBusy || TutorialOverlay.IsShowing) continue;
            fairy = FindAnyObjectByType<FairyNpcView>();
            if (fairy != null) break;
        }

        if (!save.tutorialsDone.Contains(ZoneTutorials.FairyShopIntro))
        {
            bool finished = false;
            bool skipped = false;
            TutorialOverlay.Play(ZoneTutorials.FairyShopSteps(fairy), wasSkipped =>
            {
                finished = true;
                skipped = wasSkipped;
            });
            while (!finished) yield return null;
            if (skipped || save.tutorialsDone.Contains(ZoneTutorials.FairyShop)) yield break;
            save.tutorialsDone.Add(ZoneTutorials.FairyShopIntro);
            SaveNow();
        }

        if (fairy == null || save.tutorialsDone.Contains(ZoneTutorials.FairyShop)) yield break;
        fairyPointer = TutorialPointer.Show(() => TutorialTargets.World(fairy), ZoneTutorials.FairyShopPointerMessage);
    }

    private void HandleFairyShopOpened()
    {
        CompleteFairyShopTutorial();
        if (fairyPointer != null) fairyPointer.Close();
        fairyPointer = null;
    }

    private void CompleteFairyShopTutorial()
    {
        if (save.tutorialsDone.Contains(ZoneTutorials.FairyShop)) return;
        save.tutorialsDone.Add(ZoneTutorials.FairyShop);
        SaveNow();
    }

    // Forgets every zone tutorial and replays this zone's right away.
    public void DevResetZoneTutorials()
    {
        save.tutorialsDone.Clear();
        SaveNow();
        if (zoneTutorialPending || !ZoneTutorials.Has(CurrentZoneId)) return;

        zoneTutorialPending = true;
        StartCoroutine(PlayZoneTutorialWhenReady());
    }

    public FurrowUnlockResult TryUnlockNextFurrow()
    {
        var result = farmService.TryUnlockNextFurrow(KingdomLevel, CurrencyManager.Instance, goldCurrency);
        if (result == FurrowUnlockResult.Opened) SaveNow();
        return result;
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

    // Plain "알림" popup with [확인] — e.g. why a locked zone on the travel map can't be entered yet.
    public void ShowAlert(string message)
    {
        gameUI.ShowAlert(message);
    }

    // Bottom speech bubble (e.g. "길을 막은 나무와 돌을 톡톡 눌러 치워요!"
    // while a zone still has to be cleared).
    public void ShowGuide(string message)
    {
        gameUI.ShowGuide(message);
    }

    public void HideGuide()
    {
        gameUI.HideGuide();
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
        // Until the fisher is assigned and working there, the dock can't start.
        if (active && !CanProduceIn(FishingZoneId)) return;
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
            FishCaught?.Invoke(item);
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

    // A find went into the bag while playing (any scene; not offline finds).
    // Static so GlobalUI can listen across scene changes: the mine scene's
    // otter bubble shows it there, a GlobalUI toast everywhere else.
    public static event Action<ItemDefinition> MiningFound;

    // A harvest went into the bag (item, amount) / a catch landed in the bag.
    // The farm and dock otters show it as icons flying to the bag.
    public static event Action<ItemDefinition, int> HarvestStored;
    public static event Action<ItemDefinition> FishCaught;

    // Mining runs in every zone scene once the otter is in the mine, like
    // the farm growing everywhere — not only while the mine scene is open.
    private void TickMining(float deltaSec)
    {
        if (!CanProduceIn(MineZoneId)) return;
        string find = miningService.Tick(deltaSec);
        if (find == null) return;

        var item = AddMiningFind(find);
        if (item != null) MiningFound?.Invoke(item);
    }

    public void SetMiningActive(bool active)
    {
        if (miningService.IsActive == active) return;
        // Only the working miner can go in (stopping is always allowed).
        if (active && !CanProduceIn(MineZoneId)) return;
        miningService.SetActive(active);
        SaveNow();
    }

    // Called once by the farm scene (FarmerOtterController); other scenes have no farm button.
    public void ShowFarmUpgradeButton()
    {
        gameUI.ShowFarmUpgradeButton();
    }

    // Same rules as the pickaxe/rod upgrade: coins, levels 1..5, offline farming from level 2.
    public bool TryUpgradeFarm()
    {
        if (!farmService.TryUpgrade(CurrencyManager.Instance, goldCurrency)) return false;
        SaveNow();
        return true;
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

    // Called by TickMining each time a find comes up. Same rules as
    // AddFishingCatch: no room in the bag drops it with one alert per
    // full-bag spell. Returns the item that went into the bag (for the
    // pop-up above the bubble), or null if nothing did.
    public ItemDefinition AddMiningFind(string itemId)
    {
        if (!TryFindItem(itemId, out var item)) return null;

        if (Bag.GetAddableAmount(item) > 0)
        {
            // Stone bonus (quarry, KingdomBonus): sometimes one extra stone per find.
            int count = itemId == miningBalance.stoneItemId ? KingdomBonus.Amount(KingdomBonusKind.StoneYield, 1) : 1;
            Bag.Add(item, count, ItemChangeReason.Mining);
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

    // Whether the whole yield of this ready slot fits in the bag (the farmer
    // skips slots that don't; they wait until the player sells).
    public bool CanStoreHarvest(int plotIndex, int slotIndex)
    {
        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        return crop != null && TryFindItem(crop.cropId, out var item) &&
               Bag.GetAddableAmount(item) >= crop.yieldCount;
    }

    // Debug-panel harvest: the same model harvest the farmer does (bag,
    // replant or wait for seeds), right now. The farmer otter never calls
    // this — FarmService harvests on its own wherever the player is.
    public void HarvestSlot(int plotIndex, int slotIndex)
    {
        if (!CanProduceIn(FarmZoneId)) return;
        if (farmService.HarvestNow(plotIndex, slotIndex)) SaveNow();
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

        GUILayout.Label($"골드: {CurrencyManager.Instance.GetCurrency(goldCurrency)}");
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
            if (GUILayout.Button($"농사 강화 ({farmService.NextUpgradeCost} 골드)"))
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
