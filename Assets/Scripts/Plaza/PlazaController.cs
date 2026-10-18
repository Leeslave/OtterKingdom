using System.Collections.Generic;
using UnityEngine;

// Plaza bootstrap: spawns `settings.otterCount` otters on valid, spread-out
// walkable positions (some around the initial camera focus so they're on
// screen at entry) and hands each one the shared walkable area.
//
// Spawning happens once per controller lifetime, so disabling/re-enabling
// the plaza never adds more otters. Otters placed under `otterRoot` by hand
// in the scene count toward the total and are initialised too.
// Below `settings.otterMinKingdomLevel` the spawned otters wait and appear
// on reaching it (at random spots, not around the camera focus).
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
    private readonly List<GameObject> unpicked = new List<GameObject>();
    private bool spawned;
    private bool spawnLate;
    private int placedCount;

    private void Start()
    {
        if (settings == null || walkableArea == null || otterRoot == null)
        {
            Debug.LogError($"[{nameof(PlazaController)}] Settings, Walkable Area and Otter Root must all be assigned.", this);
            spawned = true;
            return;
        }
        if (!walkableArea.HasWalkableCells)
        {
            Debug.LogError($"[{nameof(PlazaController)}] Walkable area has no walkable cells — no otters spawned.", this);
            spawned = true;
            return;
        }

        // Otters already in the scene under otterRoot: keep and initialise.
        var existing = otterRoot.GetComponentsInChildren<OtterWanderAgent>();
        foreach (var agent in existing)
            agent.Initialize(walkableArea, settings);
        placedCount = existing.Length;
        TrySpawn();
    }

    // Waits for the kingdom level (the save may load after Start).
    private void Update()
    {
        if (!spawned) TrySpawn();
    }

    private void TrySpawn()
    {
        // 정착 광장에서는 기록 없는 장식용 해달을 추가하지 않는다.
        // 매니저의 저장 로딩 순서와 무관하게 씬의 진행 뷰로 판정한다.
        if (FindAnyObjectByType<SettlementPlazaView>(FindObjectsInactive.Include) != null)
        {
            spawned = true;
            return;
        }
        if (GameManager.KingdomLevel < settings.otterMinKingdomLevel)
        {
            spawnLate = true;
            return;
        }
        // Still loading in the first second counts as entering the plaza.
        if (Time.timeSinceLevelLoad < 1f) spawnLate = false;
        SpawnOtters();
    }

    private void SpawnOtters()
    {
        if (spawned) return;
        spawned = true;

        // Keep clear of every otter already here (placed ones and, when spawning late, settlement otters).
        occupied.Clear();
        foreach (var agent in otterRoot.GetComponentsInChildren<OtterWanderAgent>())
            occupied.Add(agent.transform.position);

        int toSpawn = settings.otterCount - placedCount;
        if (toSpawn > 0 && !HasUsablePrefab())
        {
            Debug.LogError($"[{nameof(PlazaController)}] PlazaSettings has no otter prefab with an {nameof(OtterWanderAgent)}.", this);
            return;
        }

        int spawnedNearCamera = 0;
        for (int i = 0; i < toSpawn; i++)
        {
            bool nearCamera = !spawnLate && initialCameraFocus != null && spawnedNearCamera < settings.spawnNearCameraCount;
            if (!TryFindSpawnPosition(nearCamera, out Vector2 position))
            {
                Debug.LogWarning($"[{nameof(PlazaController)}] Couldn't find a free spawn position for otter #{i + 1} — skipped.", this);
                continue;
            }
            if (nearCamera) spawnedNearCamera++;

            SpawnOtter(position, placedCount + i);
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

    // Each prefab once (random order) before any repeats, so a cast of
    // distinct characters never shows the same otter twice.
    private GameObject PickPrefab()
    {
        if (unpicked.Count == 0)
        {
            foreach (var prefab in settings.otterPrefabs)
            {
                if (prefab != null && prefab.GetComponent<OtterWanderAgent>() != null) unpicked.Add(prefab);
            }
        }
        int index = Random.Range(0, unpicked.Count);
        var picked = unpicked[index];
        unpicked.RemoveAt(index);
        return picked;
    }
}
