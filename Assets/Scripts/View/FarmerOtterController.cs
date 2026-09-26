using System.Collections;
using UnityEngine;

// Drives the farmer otter NPC in the Farm scene: wanders between hand-placed
// waypoints while nothing is ready to harvest, and breaks off to walk to and
// harvest the nearest AwaitingHarvest slot in `plotIndex` as soon as one
// appears. Waypoints and slot anchors are wired in the Inspector rather than
// looked up by name, since both are scene-specific placement done by hand
// after running the FarmerOtterSpriteSetup / FurrowSlotSetup editor tools
// (see 농부해달_애니메이션_작업기록.md).
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
    [Header("Farm target")]
    [SerializeField] private int plotIndex;

    [Header("Wander waypoints (hand-placed empty Transforms)")]
    [SerializeField] private Transform[] wanderWaypoints;
    [SerializeField] private Vector2 wanderWaitRangeSec = new Vector2(1.5f, 4f);

    [Header("Random action at each waypoint")]
    [Tooltip("Animator trigger names for idle flavor animations. Each must be wired " +
             "Any State -> trigger -> clip -> exit time 1 -> Idle, same as Harvest " +
             "(FarmerOtterSpriteSetup wires Net/Stretch/Eat/Squat this way already). " +
             "Leave empty to just wait in Idle instead.")]
    [SerializeField] private string[] randomActionTriggers = { "Net", "Stretch", "Eat", "Squat" };

    [Header("Harvest slot anchors (index-matched to slot 0/1/2)")]
    [SerializeField] private Transform[] slotAnchors;

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
        activeRoutine = StartCoroutine(WanderRoutine());
    }

    private void Update()
    {
        if (isHarvesting) return;
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        int slotIndex = FindNearestAwaitingHarvestSlot();
        if (slotIndex < 0) return;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(HarvestRoutine(slotIndex));
    }

    private int FindNearestAwaitingHarvestSlot()
    {
        var farmService = GameManager.Instance.FarmService;
        int count = slotAnchors != null ? slotAnchors.Length : 0;

        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (slotAnchors[i] == null) continue;
            if (farmService.GetSlotState(plotIndex, i) != FurrowSlotState.AwaitingHarvest) continue;

            float dist = Vector2.Distance(transform.position, slotAnchors[i].position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
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

    private IEnumerator HarvestRoutine(int slotIndex)
    {
        isHarvesting = true;

        var farmService = GameManager.Instance.FarmService;
        Transform anchor = slotAnchors[slotIndex];
        yield return MoveTo(anchor.position);

        // Slot may have been harvested by something else (e.g. the debug
        // panel button) while we were walking over — bail out quietly.
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
