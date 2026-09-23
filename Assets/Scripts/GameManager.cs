using System;
using System.Globalization;
using UnityEngine;

// M1 vertical-slice bootstrap: loads/creates the save, wires the plain-C#
// services, drives the farm production tick, and exposes a temporary OnGUI
// debug UI so the loop (plant -> grow -> harvest -> sell -> upgrade) can
// be verified with zero scene/prefab setup. Replace the OnGUI block with real
// uGUI views in M4; the services underneath should not need to change.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly Rect DebugPanelRect = new Rect(20, 20, 440, 560);
    public static readonly Rect SeedPromptRect = new Rect(480, 20, 320, 220);

    public bool IsDebugPanelOpen => showDebugPanel;
    public bool IsSeedPromptOpen => pendingPlotIndex >= 0;
    public FarmService FarmService => farmService;

    private bool showDebugPanel;
    private int pendingPlotIndex = -1;
    private int pendingSlotIndex = -1;

    [Header("Data (optional — falls back to built-in defaults if empty)")]
    [SerializeField] private CropDefinition[] cropDefinitions;
    [SerializeField] private FarmBalanceData farmBalance;

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

    private float autoSaveTimer;
    private float pendingOfflineElapsedSec;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureDefaultData();

        saveService = new SaveService();
        save = saveService.Load() ?? CreateNewSave();

        ComputePendingOfflineElapsed();

        CurrencyManager.Instance.LoadFromSave(save, new[] { goldCurrency });
        CurrencyHud.Show(goldCurrency);
        inventoryService = new InventoryService(save.inventory);
        farmService = new FarmService(save, cropDefinitions, farmBalance);
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
            var crop = farmService.GetCrop(stack.itemId);
            total += (crop != null ? crop.sellPrice : 0) * stack.quantity;
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
        pendingPlotIndex = plotIndex;
        pendingSlotIndex = slotIndex;
    }

    // Shared harvest entry point — used by the debug-panel button and by
    // FarmerOtterController once its harvest animation finishes.
    public void HarvestSlot(int plotIndex, int slotIndex)
    {
        var harvested = farmService.Harvest(plotIndex, slotIndex);
        if (harvested.Count == 0) return;

        inventoryService.AddRange(harvested);
        SaveNow();
    }

    // A slot is "covered" by a screen point if that point lands inside any
    // currently-open OnGUI panel — used by FurrowSlotView so clicking a panel
    // button doesn't also register as a click on whatever slot is underneath.
    public bool IsScreenPointOverUI(Vector2 screenPos)
    {
        Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        if (showDebugPanel && DebugPanelRect.Contains(guiPos)) return true;
        if (IsSeedPromptOpen && SeedPromptRect.Contains(guiPos)) return true;
        return false;
    }

    private void OnGUI()
    {
        if (IsSeedPromptOpen)
        {
            DrawSeedPrompt();
        }

        if (!showDebugPanel) return;

        GUILayout.BeginArea(DebugPanelRect, GUI.skin.box);

        GUILayout.Label($"코인: {CurrencyManager.Instance.GetCurrency(goldCurrency)}");
        GUILayout.Label($"농사 레벨: {save.farmLevel}");

        GUILayout.Space(10);
        GUILayout.Label("=== 밭 1 ===");

        var plot = farmService.Plots[0];
        for (int slotIndex = 0; slotIndex < plot.SlotCount; slotIndex++)
        {
            DrawSlotDebugRow(0, slotIndex);
        }

        GUILayout.Space(10);
        GUILayout.Label("=== 가방 ===");
        if (inventoryService.Items.Count == 0)
        {
            GUILayout.Label("아직 모은 생산물이 없어요");
        }
        else
        {
            foreach (var item in inventoryService.Items)
            {
                var crop = farmService.GetCrop(item.itemId);
                string name = crop != null ? crop.displayName : item.itemId;
                GUILayout.Label($"{name} x{item.quantity}");
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

    private void DrawSeedPrompt()
    {
        GUILayout.BeginArea(SeedPromptRect, GUI.skin.box);
        GUILayout.Label("심을 작물을 선택하세요");

        foreach (var crop in cropDefinitions)
        {
            if (crop == null) continue;
            if (GUILayout.Button(crop.displayName))
            {
                farmService.Plant(pendingPlotIndex, pendingSlotIndex, crop.cropId);
                pendingPlotIndex = -1;
                pendingSlotIndex = -1;
                SaveNow();
            }
        }

        if (GUILayout.Button("취소"))
        {
            pendingPlotIndex = -1;
            pendingSlotIndex = -1;
        }

        GUILayout.EndArea();
    }
}
