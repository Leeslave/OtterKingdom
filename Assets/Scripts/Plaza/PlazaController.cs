using System.Collections.Generic;
using UnityEngine;

// Plaza bootstrap: spawns `settings.otterCount` otters on valid, spread-out
// walkable positions (some around the initial camera focus so they're on
// screen at entry) and hands each one the shared walkable area.
//
// Spawning happens once per controller lifetime (Start runs once), so
// disabling/re-enabling the plaza never adds more otters. Otters placed
// under `otterRoot` by hand in the scene count toward the total and are
// initialised too.
public class PlazaController : MonoBehaviour
{
    [SerializeField] private PlazaSettings settings;
    [SerializeField] private PlazaWalkableArea walkableArea;
    [Tooltip("Parent for spawned otters.")]
    [SerializeField] private Transform otterRoot;
    [Tooltip("Otters near this point are guaranteed to be visible on entry.")]
    [SerializeField] private Transform initialCameraFocus;
    [Tooltip("Fallback positions (children) used when random placement fails.")]
    [SerializeField] private Transform spawnPointRoot;

    private readonly List<Vector2> occupied = new List<Vector2>();
    private bool spawned;

    private void Start()
    {
        SpawnOtters();
    }

    private void SpawnOtters()
    {
        if (spawned) return;
        spawned = true;

        if (settings == null || walkableArea == null || otterRoot == null)
        {
            Debug.LogError($"[{nameof(PlazaController)}] Settings, Walkable Area and Otter Root must all be assigned.", this);
            return;
        }
        if (!walkableArea.HasWalkableCells)
        {
            Debug.LogError($"[{nameof(PlazaController)}] Walkable area has no walkable cells — no otters spawned.", this);
            return;
        }

        occupied.Clear();

        // Otters already in the scene under otterRoot: keep and initialise.
        var existing = otterRoot.GetComponentsInChildren<OtterWanderAgent>();
        foreach (var agent in existing)
        {
            agent.Initialize(walkableArea, settings);
            occupied.Add(agent.transform.position);
        }

        int toSpawn = settings.otterCount - existing.Length;
        if (toSpawn > 0 && !HasUsablePrefab())
        {
            Debug.LogError($"[{nameof(PlazaController)}] PlazaSettings has no otter prefab with an {nameof(OtterWanderAgent)}.", this);
            return;
        }

        int spawnedNearCamera = 0;
        for (int i = 0; i < toSpawn; i++)
        {
            bool nearCamera = initialCameraFocus != null && spawnedNearCamera < settings.spawnNearCameraCount;
            if (!TryFindSpawnPosition(nearCamera, out Vector2 position))
            {
                Debug.LogWarning($"[{nameof(PlazaController)}] Couldn't find a free spawn position for otter #{i + 1} — skipped.", this);
                continue;
            }
            if (nearCamera) spawnedNearCamera++;

            SpawnOtter(position, existing.Length + i);
        }
    }

    private bool TryFindSpawnPosition(bool nearCamera, out Vector2 position)
    {
        float spacing = settings.SpawnSpacing;
        for (int attempt = 0; attempt < settings.maxSpawnAttempts; attempt++)
        {
            bool found = nearCamera
                ? walkableArea.TryGetRandomPointNear(initialCameraFocus.position, settings.SpawnNearCameraRadius, 1, out position)
                : walkableArea.TryGetRandomPoint(out position);
            if (found && IsFarEnough(position, spacing)) return true;
        }

        // Near-camera placement failing still deserves a spot elsewhere.
        if (nearCamera && TryFindSpawnPosition(false, out position)) return true;

        if (spawnPointRoot != null)
        {
            foreach (Transform point in spawnPointRoot)
            {
                position = point.position;
                if (walkableArea.IsWalkable(position) && IsFarEnough(position, spacing)) return true;
            }
        }

        position = default;
        return false;
    }

    private bool IsFarEnough(Vector2 position, float spacing)
    {
        float sqr = spacing * spacing;
        foreach (var other in occupied)
        {
            if ((other - position).sqrMagnitude < sqr) return false;
        }
        return true;
    }

    private void SpawnOtter(Vector2 position, int index)
    {
        GameObject prefab = PickPrefab();
        var instance = Instantiate(prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity, otterRoot);
        instance.name = $"{prefab.name}_{index + 1:00}";

        var agent = instance.GetComponent<OtterWanderAgent>();
        agent.Initialize(walkableArea, settings);
        occupied.Add(position);
    }

    private bool HasUsablePrefab()
    {
        if (settings.otterPrefabs == null) return false;
        foreach (var prefab in settings.otterPrefabs)
        {
            if (prefab != null && prefab.GetComponent<OtterWanderAgent>() != null) return true;
        }
        return false;
    }

    private GameObject PickPrefab()
    {
        var usable = new List<GameObject>();
        foreach (var prefab in settings.otterPrefabs)
        {
            if (prefab != null && prefab.GetComponent<OtterWanderAgent>() != null) usable.Add(prefab);
        }
        return usable[Random.Range(0, usable.Count)];
    }
}
