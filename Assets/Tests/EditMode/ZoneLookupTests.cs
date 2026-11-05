using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ZoneLookupTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
        typeof(SettlementManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
    }

    private ZoneDefinition CreateZone(string id, string sceneName, int sortOrder = 0, bool available = true, string requiredDevelopment = "")
    {
        var zone = ScriptableObject.CreateInstance<ZoneDefinition>();
        _created.Add(zone);
        var so = new SerializedObject(zone);
        so.FindProperty("_zoneId").stringValue = id;
        so.FindProperty("_sceneName").stringValue = sceneName;
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_isAvailable").boolValue = available;
        so.FindProperty("_requiredDevelopment").stringValue = requiredDevelopment;
        so.ApplyModifiedPropertiesWithoutUndo();
        return zone;
    }

    [Test]
    public void FindByScene_ReturnsMatchingZone()
    {
        // Arrange
        var plaza = CreateZone("Plaza", "Plaza");
        var farm = CreateZone("Farm", "Farm");

        // Act & Assert
        Assert.AreEqual(farm, ZoneLookup.FindByScene(new[] { plaza, farm }, "Farm"));
    }

    [Test]
    public void FindByScene_UnknownScene_ReturnsNull()
    {
        // Arrange: 테스트 씬 같은 장소가 아닌 씬
        var plaza = CreateZone("Plaza", "Plaza");

        // Act & Assert
        Assert.IsNull(ZoneLookup.FindByScene(new[] { plaza }, "InventoryTestScene"));
    }

    [Test]
    public void FindByScene_LockedZoneWithoutScene_IsNeverMatched()
    {
        // Arrange: 광산처럼 씬이 비어 있는 장소가 이름 없는 씬과 짝지어지면 안 됨
        var mine = CreateZone("Mine", "", available: false);

        // Act & Assert
        Assert.IsNull(ZoneLookup.FindByScene(new[] { mine }, ""));
    }

    [Test]
    public void Sorted_OrdersBySortOrderAndSkipsNull()
    {
        // Arrange
        var fishing = CreateZone("Fishing", "Fishing", sortOrder: 2);
        var plaza = CreateZone("Plaza", "Plaza", sortOrder: 0);
        var farm = CreateZone("Farm", "Farm", sortOrder: 1);

        // Act
        var sorted = ZoneLookup.Sorted(new[] { fishing, null, plaza, farm });

        // Assert
        CollectionAssert.AreEqual(new[] { plaza, farm, fishing }, sorted);
    }

    [Test]
    public void IsAvailable_FalseWhenSceneMissingOrTurnedOff()
    {
        // Arrange
        var noScene = CreateZone("Mine", "", available: true);
        var turnedOff = CreateZone("Cave", "Cave", available: false);
        var open = CreateZone("Farm", "Farm");

        // Act & Assert
        Assert.IsFalse(noScene.IsAvailable);
        Assert.IsFalse(turnedOff.IsAvailable);
        Assert.IsTrue(open.IsAvailable);
    }

    [Test]
    public void ShowsOnTravelMap_NoSettlementManager_KeepsFarm()
    {
        // Arrange: 정착 매니저가 없는 테스트 씬은 정착 조건을 보지 않음
        var farm = CreateZone("Farm", "Farm");

        // Act & Assert
        Assert.IsTrue(ZoneAccess.ShowsOnTravelMap(farm));
    }

    [Test]
    public void ShowsOnTravelMap_BeforeFarmDiscovered_HidesLockedFarmOnly()
    {
        // Arrange: 밭이 열리기 전. 다른 장소는 잠겨 있어도 카드가 남아 여는 조건을 알려 줌
        var farm = CreateZone("Farm", "Farm", requiredDevelopment: SettlementManager.FarmDiscoveredDevelopment);
        var mine = CreateZone("Mine", "Mine", requiredDevelopment: "mine_discovered");
        LoadManager(new SettlementSaveData { version = SettlementSaveData.CurrentVersion, initialized = true });

        // Act & Assert
        Assert.IsFalse(ZoneAccess.ShowsOnTravelMap(farm), "잠긴 밭 카드는 남지 않음");
        Assert.IsFalse(ZoneAccess.IsOpen(mine));
        Assert.IsTrue(ZoneAccess.ShowsOnTravelMap(mine), "잠긴 광산 카드는 그대로");
    }

    [Test]
    public void ShowsOnTravelMap_OldSaveWithoutAgriculturalDistrict_KeepsFarmScene()
    {
        // Arrange: 밭을 연 옛 세이브 (농업 지역을 정할 때까지 밭 씬을 그대로 씀)
        var farm = CreateZone("Farm", "Farm", requiredDevelopment: SettlementManager.FarmDiscoveredDevelopment);
        var manager = LoadManager(FarmSave(migrated: false));

        // Act & Assert
        Assert.IsFalse(manager.FarmOpen);
        Assert.IsTrue(ZoneAccess.IsOpen(farm));
        Assert.IsTrue(ZoneAccess.ShowsOnTravelMap(farm));
    }

    [Test]
    public void ShowsOnTravelMap_PlazaFarmOpen_HidesFarm()
    {
        // Arrange: 밭이 광장으로 옮겨 온 세이브
        var farm = CreateZone("Farm", "Farm", requiredDevelopment: SettlementManager.FarmDiscoveredDevelopment);
        var manager = LoadManager(FarmSave(migrated: true));

        // Act & Assert
        Assert.IsTrue(manager.FarmOpen);
        Assert.IsFalse(ZoneAccess.ShowsOnTravelMap(farm));
    }

    private static SettlementSaveData FarmSave(bool migrated)
    {
        var save = new SettlementSaveData { version = SettlementSaveData.CurrentVersion, initialized = true };
        save.unlockedDevelopments.Add(SettlementManager.FarmDiscoveredDevelopment);
        save.unlockedDevelopments.Add(SettlementQuestGate.FarmDevelopment);
        save.farm.migrated = migrated;
        return save;
    }

    private SettlementManager LoadManager(SettlementSaveData save)
    {
        var config = ScriptableObject.CreateInstance<SettlementConfig>();
        _created.Add(config);
        var farmConfig = ScriptableObject.CreateInstance<PlazaFarmConfig>();
        _created.Add(farmConfig);
        var configObject = new SerializedObject(config);
        configObject.FindProperty("_farm").objectReferenceValue = farmConfig;
        configObject.ApplyModifiedPropertiesWithoutUndo();

        var go = new GameObject("SettlementManager");
        _created.Add(go);
        go.SetActive(false);
        var manager = go.AddComponent<SettlementManager>();
        var managerObject = new SerializedObject(manager);
        managerObject.FindProperty("_config").objectReferenceValue = config;
        managerObject.ApplyModifiedPropertiesWithoutUndo();
        typeof(SettlementManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        manager.LoadFromSave(save);
        return manager;
    }
}
