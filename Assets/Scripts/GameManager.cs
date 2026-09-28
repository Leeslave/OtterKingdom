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
// Every zone scene (Farm, Fishing) has its own GameManager with the same data
// assigned, so the save, coins, inventory and sale UI are shared. The farm
// keeps ticking in the fishing scene too — crops grow wherever the player is.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly Rect DebugPanelRect = new Rect(20, 20, 440, 560);

    public bool IsDebugPanelOpen => showDebugPanel;
    public FarmService FarmService => farmService;
    public IReadOnlyList<CropDefinition> Crops => cropDefinitions;
    public FishingService FishingService => fishingService;
    public IReadOnlyList<SellableItem> SellableItems => sellableItems;
    public int CoinBalance => CurrencyManager.Instance.GetCurrency(goldCurrency);

    private bool showDebugPanel;

    [Header("Data (optional — falls back to built-in defaults if empty)")]
    [SerializeField] private CropDefinition[] cropDefinitions;
    [SerializeField] private FarmBalanceData farmBalance;
    [SerializeField] private FishingBalanceData fishingBalance;

    [Header("Save")]
    [SerializeField] private float autoSaveIntervalSec = 30f;

    [Header("Currency")]
    [SerializeField] private Currency goldCurrency;

    private const string DefaultOtterId = "otter_001";
    private const string PlotId = "plot_1";
    private const string DefaultCropId = "crop_carrot";

    private SaveData save;
    private SaveService saveService;
    private FarmService farmService;
    private InventoryService inventoryService;
    private FishingService fishingService;
    private GameUI gameUI;
    private readonly List<SellableItem> sellableItems = new List<SellableItem>();

    private float autoSaveTimer;
    private float pendingOfflineElapsedSec;

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
        Instance = this;

        EnsureDefaultData();

        saveService = new SaveService();
        var loadStatus = saveService.Load(out save);
        if (save == null) save = CreateNewSave();

        ComputePendingOfflineElapsed();

        CurrencyManager.Instance.LoadFromSave(save, new[] { goldCurrency });
        CurrencyHud.Show(goldCurrency);
        inventoryService = new InventoryService(save.inventory);
        farmService = new FarmService(save, cropDefinitions, farmBalance);
        fishingService = new FishingService(save, fishingBalance);
        BuildSellableItems();
        gameUI = GameUI.Create(this);

        ReportLoadStatus(loadStatus);
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

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveNow();
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
    }

    // Sale UI order: crops first, then fishing catches.
    private void BuildSellableItems()
    {
        sellableItems.Clear();
        foreach (var crop in cropDefinitions)
        {
            if (crop == null) continue;
            sellableItems.Add(new SellableItem(crop.cropId, crop.displayName, crop.sellPrice, TransactionSource.CropSale));
        }
        sellableItems.Add(new SellableItem(fishingBalance.fishItemId, fishingBalance.fishDisplayName,
            fishingBalance.fishSellPrice, TransactionSource.FishingSale));
        sellableItems.Add(new SellableItem(fishingBalance.trashItemId, fishingBalance.trashDisplayName,
            fishingBalance.trashSellPrice, TransactionSource.FishingSale));
    }

    private SellableItem FindSellable(string itemId) => sellableItems.Find(i => i.itemId == itemId);

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
    }

    private void SaveNow()
    {
        CurrencyManager.Instance.SaveToSave(save);
        saveService.Save(save);
    }

    private void SellAllForGold()
    {
        int total = 0;
        foreach (var stack in inventoryService.Items)
        {
            var item = FindSellable(stack.itemId);
            total += (item != null ? item.sellPrice : 0) * stack.quantity;
        }

        if (total <= 0) return;

        CurrencyManager.Instance.Add(goldCurrency, total, TransactionSource.CropSale);
        save.lifetimeSales += total;
        inventoryService.Clear();
        Debug.Log($"[GameManager] 판매 완료: +{total} 코인");
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

    public int GetItemQuantity(string itemId) => inventoryService.GetQuantity(itemId);

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

    public bool SellItem(SellableItem item, int quantity)
    {
        if (item == null || quantity <= 0) return false;
        if (!inventoryService.Remove(item.itemId, quantity)) return false;

        int total = item.sellPrice * quantity;
        CurrencyManager.Instance.Add(goldCurrency, total, item.source);
        save.lifetimeSales += total;
        SaveNow();
        return true;
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

    // Called when the pull animation lands the catch.
    public void AddFishingCatch(string itemId)
    {
        inventoryService.Add(itemId, 1);
        SaveNow();
    }

    public bool TryUpgradeRod()
    {
        if (!fishingService.TryUpgradeRod(CurrencyManager.Instance, goldCurrency)) return false;
        SaveNow();
        return true;
    }

    // Shared harvest entry point — used by the debug-panel button and by
    // FarmerOtterController once its harvest animation finishes. A successful
    // harvest immediately replants the same crop; if a consumable crop is out
    // of seeds the slot is left empty and the player is told.
    public void HarvestSlot(int plotIndex, int slotIndex)
    {
        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        var harvested = farmService.Harvest(plotIndex, slotIndex);
        if (harvested.Count == 0) return;

        inventoryService.AddRange(harvested);

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
        if (inventoryService.Items.Count == 0)
        {
            GUILayout.Label("아직 모은 생산물이 없어요");
        }
        else
        {
            foreach (var stack in inventoryService.Items)
            {
                var item = FindSellable(stack.itemId);
                string name = item != null ? item.displayName : stack.itemId;
                GUILayout.Label($"{name} x{stack.quantity}");
            }
        }

        GUI.enabled = inventoryService.Items.Count > 0;
        if (GUILayout.Button("전체 판매"))
        {
            SellAllForGold();
            SaveNow();
        }
        GUI.enabled = true;

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
