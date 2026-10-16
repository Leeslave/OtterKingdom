using System.Collections.Generic;
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
    }

    private ZoneDefinition CreateZone(string id, string sceneName, int sortOrder = 0, bool available = true)
    {
        var zone = ScriptableObject.CreateInstance<ZoneDefinition>();
        _created.Add(zone);
        var so = new SerializedObject(zone);
        so.FindProperty("_zoneId").stringValue = id;
        so.FindProperty("_sceneName").stringValue = sceneName;
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_isAvailable").boolValue = available;
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
}
