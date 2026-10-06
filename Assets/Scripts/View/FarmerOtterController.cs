using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

// Drives the farmer otter NPC in the Farm scene: wanders between hand-placed
// waypoints while the farmer is idle, and breaks off to walk to the slot the
// farmer is harvesting (FarmService.TryGetHarvestTarget) and play the harvest
// animation there. It only shows the work: FarmService harvests and replants
// on its own timer in every zone scene, so the animation finishing is never
// what puts crops in the bag (being in the farm or elsewhere gives the same
// result).
// Waypoints are wired by hand in the Inspector. Slot anchors are registered
// per plot in `plotSlotAnchors`: any unlocked plot with no entry is filled in
// automatically on Start from that plot's FurrowSlotView objects, and plots
// unlocked mid-session are added through FarmService.PlotUnlocked — so they
// show up in the Inspector list without manual wiring. Hand-assigned entries
// are left alone (see 농부해달_애니메이션_작업기록.md).
//
// Toys (DecorBoardView.Active): while waiting for crops the otter sometimes
// walks to a placed toy and plays beside it (random idle actions facing the
// toy while it bounces/wiggles). A harvest that becomes ready interrupts play
// immediately. Walks that would cross a toy take a grid detour instead of
// the usual vertical-then-horizontal line.
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
    // The farmer's target this otter is showing (so a new target is noticed).
    private int shownPlot = -1;
    private int shownSlot = -1;
    private FarmService subscribedFarmService;
    private DecorPlaySession playSession;
    private readonly List<Vector2> detour = new List<Vector2>();

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
        // The farm upgrade belongs to the farm, so the button only exists in
        // the farm scene (same as the mine's pickaxe button).
        if (GameManager.Instance != null) GameManager.Instance.ShowFarmUpgradeButton();
        RegisterAnchors();
        activeRoutine = StartCoroutine(WanderRoutine());
        GameManager.HarvestStored += HandleHarvestStored;
    }

    private void OnDestroy()
    {
        if (subscribedFarmService != null) subscribedFarmService.PlotUnlocked -= RegisterPlotAnchors;
        GameManager.HarvestStored -= HandleHarvestStored;
        ReleasePlay();
    }

    // The harvest flies from the farmer to the bag (with "+N").
    private void HandleHarvestStored(ItemDefinition item, int amount)
    {
        RewardFly.FromWorld(item.Icon, transform.position + Vector3.up * 0.8f, RewardTarget.Bag, amount, amount);
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
        // Placed but the farm's first-time guide isn't done yet: no harvesting.
        if (!GameManager.CanProduceIn(GameManager.FarmZoneId)) return;

        var farmService = GameManager.Instance.FarmService;
        if (!farmService.TryGetHarvestTarget(out int plotIndex, out int slotIndex))
        {
            shownPlot = shownSlot = -1;
            return;
        }
        // Already shown this harvest (the animation ended before the timer): wait for the next.
        if (plotIndex == shownPlot && slotIndex == shownSlot) return;
        var anchors = FindAnchors(plotIndex);
        if (anchors == null || anchors.slotAnchors == null || slotIndex >= anchors.slotAnchors.Length
            || anchors.slotAnchors[slotIndex] == null) return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        ReleasePlay(); // harvesting beats playing
        activeRoutine = StartCoroutine(HarvestRoutine(plotIndex, slotIndex));
    }

    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            var board = DecorBoardView.Active;
            if (board != null && board.RollWantsToPlay()
                && board.TryReservePlay(transform.position, board.CanStandOnGrid, out var session))
            {
                playSession = session;
                yield return PlayRoutine(session);
                ReleasePlay();
                continue;
            }

            if (wanderWaypoints == null || wanderWaypoints.Length == 0)
            {
                yield return null;
                continue;
            }

            var target = wanderWaypoints[Random.Range(0, wanderWaypoints.Length)];
            // A waypoint walled off by toys is skipped this time (harvest and
            // play still walk through if they must).
            bool blocked = target != null && board != null && !board.CanReachWithoutToys(transform.position, target.position);
            if (target != null && !blocked)
            {
                yield return MoveTo(target.position);
            }

            SetMoving(false);
            yield return PlayRandomActionOrWait();
        }
    }

    // Walk up to the toy, face it and keep doing idle actions until the play
    // time runs out (or the toy is put away).
    private IEnumerator PlayRoutine(DecorPlaySession session)
    {
        yield return MoveTo(session.StandPoint);
        if (!session.IsValid) yield break;

        SetMoving(false);
        FaceTowards(session.LookPoint);
        session.Begin();

        float endTime = Time.time + session.Seconds;
        while (Time.time < endTime && session.IsValid)
        {
            yield return PlayRandomActionOrWait();
            FaceTowards(session.LookPoint);
        }
    }

    private void ReleasePlay()
    {
        if (playSession == null) return;
        playSession.Release();
        playSession = null;
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

    // Shows one harvest: walk to the slot, play the animation once. The crop
    // goes into the bag on FarmService's timer, not here — if the farmer
    // already moved on (or the player changed the crop) while we walked, the
    // otter just goes back to wandering and picks up the current target.
    private IEnumerator HarvestRoutine(int plotIndex, int slotIndex)
    {
        isHarvesting = true;
        shownPlot = plotIndex;
        shownSlot = slotIndex;

        var farmService = GameManager.Instance.FarmService;
        Transform anchor = FindAnchors(plotIndex).slotAnchors[slotIndex];
        yield return MoveTo(anchor.position);

        if (!farmService.TryGetHarvestTarget(out int nowPlot, out int nowSlot) || nowPlot != plotIndex || nowSlot != slotIndex)
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

        isHarvesting = false;
        activeRoutine = StartCoroutine(WanderRoutine());
    }

    // Moves vertically first, then horizontally, instead of a straight
    // diagonal line — a diagonal path cuts across the furrow's raised bed
    // art at an angle and made the otter's legs clip behind it; approaching
    // along the row (Y) before stepping sideways into the slot (X) avoids that.
    //
    // A placed toy on that line: follow a grid detour around it instead, each
    // leg still walked vertical-then-horizontal.
    private IEnumerator MoveTo(Vector3 destination)
    {
        var board = DecorBoardView.Active;
        if (board != null && board.TryFindDetour(transform.position, destination, detour))
        {
            var points = new List<Vector2>(detour);
            foreach (var point in points)
            {
                yield return MoveL(new Vector3(point.x, point.y, destination.z));
            }
            yield break;
        }

        yield return MoveL(destination);
    }

    private IEnumerator MoveL(Vector3 destination)
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
