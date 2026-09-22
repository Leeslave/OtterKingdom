using System;
using System.Globalization;
using UnityEngine;

// M1 vertical-slice bootstrap: loads/creates the save, wires the plain-C#
// services, drives the farm production tick, and exposes a temporary OnGUI
// debug UI so the loop (plant -> produce -> harvest -> sell -> upgrade) can
// be verified with zero scene/prefab setup. Replace the OnGUI block with real
// uGUI views in M4; the services underneath should not need to change.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static readonly Rect DebugPanelRect = new Rect(20, 20, 440, 560);

    public bool IsDebugPanelOpen => showDebugPanel;

    private bool showDebugPanel;

    [Header("Data (optional — falls back to built-in defaults if empty)")]
    [SerializeField] private CropDefinition[] cropDefinitions;
    [SerializeField] private FarmBalanceData farmBalance;

    [Header("Save")]
    [SerializeField] private float autoSaveIntervalSec = 30f;

    private const string DefaultOtterId = "otter_001";
    private const string PlotId = "plot_1";
    private const string DefaultCropId = "crop_carrot";

    private SaveData save;
    private SaveService saveService;
    private FarmService farmService;
    private InventoryService inventoryService;
    private EconomyService economyService;

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

        economyService = new EconomyService(save);
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
            carrot.baseDurationSec = 60f;
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
            state = PlotState.Idle,
            workerId = DefaultOtterId
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
        saveService.Save(save);
    }

    public void ToggleDebugPanel()
    {
        showDebugPanel = !showDebugPanel;
    }

    private void OnGUI()
    {
        if (!showDebugPanel) return;

        GUILayout.BeginArea(DebugPanelRect, GUI.skin.box);

        GUILayout.Label($"코인: {economyService.Coins}");
        GUILayout.Label($"농사 레벨: {save.farmLevel}");

        GUILayout.Space(10);
        GUILayout.Label("=== 밭 1 ===");

        var plot = farmService.Plots[0];
        var data = plot.Data;
        GUILayout.Label($"상태: {data.state}");

        if (string.IsNullOrEmpty(data.activeCropId))
        {
            if (GUILayout.Button("당근 심기"))
            {
                farmService.SelectCrop(0, DefaultCropId);
            }
        }
        else
        {
            var crop = farmService.GetCrop(data.activeCropId);
            GUILayout.Label($"작물: {(crop != null ? crop.displayName : data.activeCropId)}");
            if (data.state == PlotState.Producing)
            {
                GUILayout.Label($"남은 시간: {data.remainingSec:F1}초");
            }
            GUILayout.Label($"완료 회차: {data.storedCompletedCycles}");
        }

        GUI.enabled = data.storedItems.Count > 0;
        if (GUILayout.Button("수확하기"))
        {
            var harvested = farmService.Harvest(0);
            inventoryService.AddRange(harvested);
            SaveNow();
        }
        GUI.enabled = true;

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
            int total = economyService.SellAll(inventoryService, id =>
            {
                var c = farmService.GetCrop(id);
                return c != null ? c.sellPrice : 0;
            });
            Debug.Log($"[GameManager] 판매 완료: +{total} 코인");
            SaveNow();
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label("=== 강화 ===");
        if (farmService.CanUpgrade)
        {
            GUI.enabled = economyService.Coins >= farmService.NextUpgradeCost;
            if (GUILayout.Button($"농사 강화 ({farmService.NextUpgradeCost} 코인)"))
            {
                if (farmService.TryUpgrade(economyService))
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
}
