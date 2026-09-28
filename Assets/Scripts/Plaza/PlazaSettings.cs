using UnityEngine;

// Tuning for the plaza's wandering otters. Distances and speeds are given in
// multiples of the otter's body height/width (as the plaza spec does) and
// converted to world units with otterHeight/otterBodyWidth, so swapping in a
// differently-sized prefab only means updating those two numbers.
// Camera zoom/drag values live on PlazaCameraController instead.
[CreateAssetMenu(fileName = "PlazaSettings", menuName = "OtterKingdom/Plaza Settings")]
public class PlazaSettings : ScriptableObject
{
    [Header("Otters")]
    [Tooltip("Prefabs to spawn from (picked at random). Each needs OtterWanderAgent; OtterVisualController is optional.")]
    public GameObject[] otterPrefabs;
    [Min(0)] public int otterCount = 5;

    [Header("Otter size (world units, feet pivot)")]
    [Tooltip("Standing height from feet to top of the hat. FarmerOtter: ~200px at PPU 120.")]
    [Min(0.01f)] public float otterHeight = 1.67f;
    [Tooltip("Visual body width, used for spawn spacing.")]
    [Min(0.01f)] public float otterBodyWidth = 1.2f;

    [Header("Spawning")]
    [Tooltip("Minimum distance between spawn positions, in body widths.")]
    [Min(0f)] public float spawnSpacingInWidths = 1.2f;
    [Tooltip("How many of the otters are placed around InitialCameraFocus so they are visible on entry.")]
    [Min(0)] public int spawnNearCameraCount = 3;
    [Tooltip("Radius around InitialCameraFocus for those otters, in body heights.")]
    [Min(0f)] public float spawnNearCameraRadiusInHeights = 3f;
    [Tooltip("Random placement attempts per otter before falling back to SpawnPoints.")]
    [Min(1)] public int maxSpawnAttempts = 30;

    [Header("Behaviour")]
    [Tooltip("Idle duration in seconds, rolled independently each time.")]
    public Vector2 idleSeconds = new Vector2(2f, 5f);
    [Tooltip("Walk speed in body heights per second, rolled once per otter.")]
    public Vector2 walkSpeedInHeights = new Vector2(0.5f, 0.8f);
    [Tooltip("Straight-line distance to each new destination, in body heights.")]
    public Vector2 destinationDistanceInHeights = new Vector2(2f, 5f);
    [Tooltip("Stop when this close to the final destination, in body heights.")]
    [Min(0f)] public float arriveDistanceInHeights = 0.1f;
    [Tooltip("Destination picks per idle before giving up and idling again.")]
    [Min(1)] public int maxDestinationTries = 10;

    public float SpawnSpacing => otterBodyWidth * spawnSpacingInWidths;
    public float SpawnNearCameraRadius => otterHeight * spawnNearCameraRadiusInHeights;
    public float ArriveDistance => otterHeight * arriveDistanceInHeights;
    public float MinDestinationDistance => otterHeight * Mathf.Min(destinationDistanceInHeights.x, destinationDistanceInHeights.y);
    public float MaxDestinationDistance => otterHeight * Mathf.Max(destinationDistanceInHeights.x, destinationDistanceInHeights.y);

    public float RollIdleSeconds() => RandomInRange(idleSeconds);
    public float RollWalkSpeed() => otterHeight * RandomInRange(walkSpeedInHeights);

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
