using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

// Drives the farmer otter NPC in the Farm scene: wanders between hand-placed
// waypoints while nothing is ready to harvest, and breaks off to walk to and
// harvest the nearest AwaitingHarvest slot across every unlocked plot as soon
// as one appears (GameManager.HarvestSlot then replants the same crop).
// Waypoints are wired by hand in the Inspector. Slot anchors are registered
// per plot in `plotSlotAnchors`: any unlocked plot with no entry is filled in
// automatically on Start from that plot's FurrowSlotView objects, and plots
// unlocked mid-session are added through FarmService.PlotUnlocked — so they
// show up in the Inspector list without manual wiring. Hand-assigned entries
// are left alone (see 농부해달_애니메이션_작업기록.md).
//
// Relies on FarmerOtter.controller's exact parameter/state names: bool
// "IsMoving", int "WalkDir" (see WalkDir below), trigger "Harvest", state "Harvest" auto-returning to "Idle"
// after one loop (hasExitTime, exitTime = 1). `randomActionTriggers` expects
// any additional action clips to be wired the same way (Any State -> trigger
// -> clip state -> exit time 1 -> Idle) once those sprites exist.
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class FarmerOtterController : MonoBehaviour
{
    [Serializable]
    public class PlotSlotAnchors
    {
        public int plotIndex;
        [Tooltip("Index-matched to slot 0/1/2.")]
        public Transform[] slotAnchors;
    }

    [Header("Wander waypoints (hand-placed empty Transforms)")]
    [SerializeField] private Transform[] wanderWaypoints;
    [SerializeField] private Vector2 wanderWaitRangeSec = new Vector2(1.5f, 4f);

    [Header("Random action at each waypoint")]
    [Tooltip("Animator trigger names for idle flavor animations. Each must be wired " +
             "Any State -> trigger -> clip -> exit time 1 -> Idle, same as Harvest " +
             "(FarmerOtterSpriteSetup wires Net/Stretch/Eat/Squat this way already). " +
             "Leave empty to just wait in Idle instead.")]
    [SerializeField] private string[] randomActionTriggers = { "Net", "Stretch", "Eat", "Squat" };

    [Header("Harvest slot anchors per plot (auto-filled for unlocked plots)")]
    [SerializeField] private List<PlotSlotAnchors> plotSlotAnchors = new List<PlotSlotAnchors>();

    // Pre-multi-plot scenes serialized plot 1's anchors as a flat array;
    // migrated into plotSlotAnchors on Start.
    [SerializeField, HideInInspector, FormerlySerializedAs("slotAnchors")]
    private Transform[] legacySlotAnchors;

    [Header("Movement")]
    [Tooltip("Walking speed in world units per second.")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float arriveThreshold = 0.05f;

    [Header("Animation playback speed (1 = 10fps as authored, lower = slower)")]
    [Tooltip("Walk_* clips. Lower this together with moveSpeed so the feet don't slide.")]
    [SerializeField, Range(0.1f, 3f)] private float walkAnimSpeed = 1f;
    [Tooltip("Harvest clip. Slower = the otter spends longer harvesting each slot.")]
    [SerializeField, Range(0.1f, 3f)] private float harvestAnimSpeed = 1f;
    [Tooltip("Random idle actions (Net/Stretch/Eat/Squat).")]
    [SerializeField, Range(0.1f, 3f)] private float idleActionAnimSpeed = 1f;

    [Header("Render order")]
    // Furrow plots are sortingOrder 0 and their crop slots are sortingOrder 1
    // (see FurrowSlotSetup.cs) — the otter needs to sit above both so a slot's
    // crop sprite doesn't draw over its legs when it walks up to a plant.
    [SerializeField] private int spriteSortingOrder = 2;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int WalkDirHash = Animator.StringToHash("WalkDir");
    private static readonly int HarvestHash = Animator.StringToHash("Harvest");
    private static readonly int WalkSpeedHash = Animator.StringToHash("WalkAnimSpeed");
    private static readonly int HarvestSpeedHash = Animator.StringToHash("HarvestAnimSpeed");
    private static readonly int IdleActionSpeedHash = Animator.StringToHash("IdleActionAnimSpeed");

    // Values of the Animator's "WalkDir" int — must match FarmerOtterSpriteSetup.
    private enum WalkDir { Side = 0, Down = 1, Up = 2 }

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Coroutine activeRoutine;
    private bool isHarvesting;
    private FarmService subscribedFarmService;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = spriteSortingOrder;
        ApplyAnimSpeeds();
    }

    // Lets the speed sliders be tuned live in Play Mode.
    private void OnValidate()
    {
        if (animator != null) ApplyAnimSpeeds();
    }

    private void ApplyAnimSpeeds()
    {
        animator.SetFloat(WalkSpeedHash, walkAnimSpeed);
        animator.SetFloat(HarvestSpeedHash, harvestAnimSpeed);
        animator.SetFloat(IdleActionSpeedHash, idleActionAnimSpeed);
    }

    private void Start()
    {
        RegisterAnchors();
        activeRoutine = StartCoroutine(WanderRoutine());
    }

    private void OnDestroy()
    {
        if (subscribedFarmService != null) subscribedFarmService.PlotUnlocked -= RegisterPlotAnchors;
    }

    private void RegisterAnchors()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;
        var farmService = GameManager.Instance.FarmService;

        if (legacySlotAnchors != null && legacySlotAnchors.Length > 0 && FindAnchors(0) == null)
        {
            plotSlotAnchors.Add(new PlotSlotAnchors { plotIndex = 0, slotAnchors = legacySlotAnchors });
        }
        legacySlotAnchors = null;

        for (int plotIndex = 0; plotIndex < farmService.Plots.Count; plotIndex++)
        {
            if (farmService.IsPlotUnlocked(plotIndex)) RegisterPlotAnchors(plotIndex);
        }

        farmService.PlotUnlocked += RegisterPlotAnchors;
        subscribedFarmService = farmService;
    }

    // Adds an entry for `plotIndex` built from that plot's FurrowSlotView
    // transforms, unless one already exists (hand-assigned or from an earlier
    // call). Also the FarmService.PlotUnlocked handler.
    private void RegisterPlotAnchors(int plotIndex)
    {
        if (FindAnchors(plotIndex) != null) return;

        var anchors = new Transform[PlotSaveData.SlotCount];
        bool foundAny = false;
        foreach (var view in FindObjectsByType<FurrowSlotView>(FindObjectsSortMode.None))
        {
            if (view.PlotIndex != plotIndex) continue;
            if (view.SlotIndex < 0 || view.SlotIndex >= anchors.Length) continue;
            anchors[view.SlotIndex] = view.transform;
            foundAny = true;
        }

        if (!foundAny)
        {
            Debug.LogWarning($"[FarmerOtterController] No FurrowSlotView found for plot {plotIndex} — " +
                             "run OtterKingdom > Tools > Setup Plot Unlock.");
            return;
        }

        plotSlotAnchors.Add(new PlotSlotAnchors { plotIndex = plotIndex, slotAnchors = anchors });
    }

    private PlotSlotAnchors FindAnchors(int plotIndex) => plotSlotAnchors.Find(p => p.plotIndex == plotIndex);

    private void Update()
    {
        if (isHarvesting) return;
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        if (!FindNearestAwaitingHarvestSlot(out int plotIndex, out int slotIndex)) return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(HarvestRoutine(plotIndex, slotIndex));
    }

    private bool FindNearestAwaitingHarvestSlot(out int bestPlot, out int bestSlot)
    {
        var farmService = GameManager.Instance.FarmService;
        bestPlot = -1;
        bestSlot = -1;
        float bestDist = float.MaxValue;

        foreach (var entry in plotSlotAnchors)
        {
            if (entry.slotAnchors == null || !farmService.IsPlotUnlocked(entry.plotIndex)) continue;

            int count = Mathf.Min(entry.slotAnchors.Length, PlotSaveData.SlotCount);
            for (int i = 0; i < count; i++)
            {
                if (entry.slotAnchors[i] == null) continue;
                if (farmService.GetSlotState(entry.plotIndex, i) != FurrowSlotState.AwaitingHarvest) continue;
                // Bag too full for this yield — the slot waits until the player sells.
                if (!GameManager.Instance.CanStoreHarvest(entry.plotIndex, i)) continue;

                float dist = Vector2.Distance(transform.position, entry.slotAnchors[i].position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPlot = entry.plotIndex;
                    bestSlot = i;
                }
            }
        }
        return bestPlot >= 0;
    }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            if (wanderWaypoints == null || wanderWaypoints.Length == 0)
            {
                yield return null;
                continue;
            }

            var target = wanderWaypoints[Random.Range(0, wanderWaypoints.Length)];
            if (target != null)
            {
                yield return MoveTo(target.position);
            }

            SetMoving(false);
            yield return PlayRandomActionOrWait();
        }
    }

    private IEnumerator PlayRandomActionOrWait()
    {
        if (randomActionTriggers == null || randomActionTriggers.Length == 0)
        {
            yield return new WaitForSeconds(Random.Range(wanderWaitRangeSec.x, wanderWaitRangeSec.y));
            yield break;
        }

        string trigger = randomActionTriggers[Random.Range(0, randomActionTriggers.Length)];
        animator.SetTrigger(trigger);

        // Same auto-return-to-Idle pattern as Harvest: wait for the transition
        // out of Idle to start, then wait for the action clip to finish and
        // hand control back to Idle.
        yield return null;
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0));
    }

    private IEnumerator HarvestRoutine(int plotIndex, int slotIndex)
    {
        isHarvesting = true;

        var farmService = GameManager.Instance.FarmService;
        Transform anchor = FindAnchors(plotIndex).slotAnchors[slotIndex];
        yield return MoveTo(anchor.position);

        // Slot may have been harvested by something else (e.g. the debug
        // panel button) or cleared by the player's crop change while we were
        // walking over — bail out quietly.
        if (farmService.GetSlotState(plotIndex, slotIndex) != FurrowSlotState.AwaitingHarvest)
        {
            isHarvesting = false;
            activeRoutine = StartCoroutine(WanderRoutine());
            yield break;
        }

        SetMoving(false);
        animator.SetTrigger(HarvestHash);

        // Wait for the Any State -> Harvest transition to start, then for the
        // Harvest state to finish and hand control back to Idle.
        yield return null;
        yield return new WaitUntil(() =>
            !animator.GetCurrentAnimatorStateInfo(0).IsName("Harvest") && !animator.IsInTransition(0));

        GameManager.Instance.HarvestSlot(plotIndex, slotIndex);

        isHarvesting = false;
        activeRoutine = StartCoroutine(WanderRoutine());
    }

    // Moves vertically first, then horizontally, instead of a straight
    // diagonal line — a diagonal path cuts across the furrow's raised bed
    // art at an angle and made the otter's legs clip behind it; approaching
    // along the row (Y) before stepping sideways into the slot (X) avoids that.
    private IEnumerator MoveTo(Vector3 destination)
    {
        yield return MoveAxis(new Vector3(transform.position.x, destination.y, destination.z));
        yield return MoveAxis(destination);
    }

    private IEnumerator MoveAxis(Vector3 destination)
    {
        if (Vector2.Distance(transform.position, destination) <= arriveThreshold)
        {
            transform.position = destination;
            yield break;
        }

        SetWalkDir(destination);
        SetMoving(true);
        FaceTowards(destination);

        while (Vector2.Distance(transform.position, destination) > arriveThreshold)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
            FaceTowards(destination);
            yield return null;
        }

        transform.position = destination;
    }

    private void SetMoving(bool moving) => animator.SetBool(IsMovingHash, moving);

    // MoveTo walks one axis at a time, so each leg is either purely vertical
    // (front/back walk) or purely horizontal (side walk, mirrored by flipX).
    private void SetWalkDir(Vector3 destination)
    {
        Vector2 delta = destination - transform.position;
        WalkDir dir = Mathf.Abs(delta.y) > Mathf.Abs(delta.x)
            ? (delta.y < 0f ? WalkDir.Down : WalkDir.Up)
            : WalkDir.Side;
        animator.SetInteger(WalkDirHash, (int)dir);
    }

    private void FaceTowards(Vector3 destination)
    {
        float dx = destination.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.001f) return;
        spriteRenderer.flipX = dx < 0f;
    }
}
