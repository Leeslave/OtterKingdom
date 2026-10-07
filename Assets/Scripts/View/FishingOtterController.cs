using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The fishing-scene otter. Off duty it wanders the dock (Idle <-> Walk, paths
// from PlazaWalkableArea). While GameManager's fishing flag is on it walks to
// the FishingSpotView and loops:
//   Cast -> Wait (bite delay, 10-30s) -> Bite -> Pull -> CatchFish/CatchTrash -> Cast ...
// What gets hooked is rolled when the bite happens (so the right reaction
// plays) but only goes into the inventory once Pull finishes — stopping
// before that throws the catch away.
//
// The fishing flag (FishingService.IsActive, saved) is the single source of
// truth: the spot's "start" prompt and this otter's "stop" prompt only flip
// it, and Update() follows it. So a save that was fishing puts the otter
// straight back on the spot on the next visit.
//
// Off duty it may also walk up to a placed toy (DecorBoardView.Active) and
// play beside it for a while; going fishing ends play right away.
//
// The transform position is the otter's feet (sprites are pivoted there).
[RequireComponent(typeof(SpriteFrameAnimator))]
[RequireComponent(typeof(Collider2D))]
public class FishingOtterController : MonoBehaviour
{
    // Clip names — FishingSceneSetup builds SpriteFrameAnimator clips with these.
    public const string ClipIdle = "Idle";
    public const string ClipWalkRight = "WalkRight";
    public const string ClipWalkLeft = "WalkLeft";
    public const string ClipWalkDown = "WalkDown";
    public const string ClipWalkUp = "WalkUp";
    public const string ClipCast = "Cast";
    public const string ClipWait = "Wait";
    public const string ClipBite = "Bite";
    public const string ClipPull = "Pull";
    public const string ClipCatchFish = "CatchFish";
    public const string ClipCatchTrash = "CatchTrash";

    // Order matters: everything from GoToSpot on counts as "on fishing duty".
    private enum Phase { Idle, Walk, WalkToPlay, Play, GoToSpot, Cast, Wait, Bite, Pull, Reaction }

    // Walking longer than expected * this (+ grace) counts as stuck.
    private const float StuckTimeMultiplier = 2f;
    private const float StuckGraceSeconds = 1f;

    [Header("Scene references")]
    [SerializeField] private PlazaWalkableArea walkableArea;
    [SerializeField] private FishingSpotView spot;

    [Header("Wandering")]
    [Tooltip("World units per second.")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 1.2f;
    [SerializeField] private Vector2 idleSeconds = new Vector2(2f, 5f);
    [Tooltip("Straight-line distance to each wander destination, world units.")]
    [SerializeField] private Vector2 wanderDistance = new Vector2(1f, 3.5f);
    [SerializeField, Min(1)] private int maxDestinationTries = 10;

    [Header("Rendering")]
    [Tooltip("Above the background (0) and the fishing spot circle (1).")]
    [SerializeField] private int spriteSortingOrder = 2;

    private SpriteFrameAnimator frameAnimator;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private Camera mainCamera;

    private Phase phase = Phase.Idle;
    private float timer;
    private readonly List<Vector2> path = new List<Vector2>();
    private int pathIndex;
    private float walkTimer;
    private float walkTimeLimit;
    private bool facingLeft;
    private string pendingCatch;
    private DecorPlaySession playSession;

    private bool IsOnFishingDuty => phase >= Phase.GoToSpot;

    private static bool FishingActive =>
        GameManager.Instance != null && GameManager.Instance.FishingService.IsActive;

    private void Awake()
    {
        frameAnimator = GetComponent<SpriteFrameAnimator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        spriteRenderer.sortingOrder = spriteSortingOrder;
    }

    private void Start()
    {
        GameManager.FishCaught += HandleFishCaught;
        if (FishingActive)
        {
            SnapToSpot();
            BeginCast();
        }
        else
        {
            EnterIdle();
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool active = FishingActive;
        if (active && !IsOnFishingDuty) BeginGoToSpot();
        else if (!active && IsOnFishingDuty) StopFishing();

        if (active) HandleClick();

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Idle:
                timer -= dt;
                if (timer <= 0f && !TryBeginPlay() && !TryBeginWander()) EnterIdle();
                break;

            case Phase.Walk:
                if (StepWalk(dt)) EnterIdle();
                break;

            case Phase.WalkToPlay:
                if (!playSession.IsValid) EnterIdle();
                else if (StepWalk(dt)) BeginPlay();
                break;

            case Phase.Play:
                timer -= dt;
                if (timer <= 0f || !playSession.IsValid) EnterIdle();
                break;

            case Phase.GoToSpot:
                if (StepWalk(dt))
                {
                    SnapToSpot();
                    BeginCast();
                }
                break;

            case Phase.Cast:
                if (frameAnimator.IsFinished)
                {
                    phase = Phase.Wait;
                    timer = GameManager.Instance.FishingService.RollBiteDelaySec();
                    frameAnimator.Play(ClipWait);
                }
                break;

            case Phase.Wait:
                timer -= dt;
                if (timer <= 0f)
                {
                    pendingCatch = GameManager.Instance.FishingService.RollCatch();
                    phase = Phase.Bite;
                    frameAnimator.Play(ClipBite, restart: true);
                }
                break;

            case Phase.Bite:
                if (frameAnimator.IsFinished)
                {
                    phase = Phase.Pull;
                    frameAnimator.Play(ClipPull, restart: true);
                }
                break;

            case Phase.Pull:
                if (frameAnimator.IsFinished)
                {
                    GameManager.Instance.AddFishingCatch(pendingCatch);
                    bool isFish = GameManager.Instance.FishingService.IsFish(pendingCatch);
                    pendingCatch = null;
                    phase = Phase.Reaction;
                    frameAnimator.Play(isFish ? ClipCatchFish : ClipCatchTrash, restart: true);
                }
                break;

            case Phase.Reaction:
                if (frameAnimator.IsFinished) BeginCast();
                break;
        }
    }

    // ----------------------------------------------------------------- input

    // Only reachable while on fishing duty — off duty the otter isn't clickable.
    private void HandleClick()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        if (!bodyCollider.OverlapPoint(worldPos)) return;

        string message = "낚시를 그만할까요?";
        if (phase == Phase.Bite || phase == Phase.Pull) message += "\n낚는 중이던 것은 버려져요.";
        GameManager.Instance.ShowConfirm("낚시 중단", message, () => GameManager.Instance.SetFishingActive(false));
    }

    // --------------------------------------------------------------- fishing

    private void OnDestroy()
    {
        GameManager.FishCaught -= HandleFishCaught;
        ReleasePlay();
    }

    // The catch flies from the otter to the bag (with "+1").
    private void HandleFishCaught(ItemDefinition item)
    {
        RewardFly.FromWorld(item.Icon, transform.position + Vector3.up * 0.8f, RewardTarget.Bag, 1, 1);
    }

    private void BeginGoToSpot()
    {
        ReleasePlay();
        pendingCatch = null;
        Vector2 from = transform.position;
        Vector2 target = spot.FeetPosition;

        path.Clear();
        if (walkableArea == null || !walkableArea.TryFindPath(from, target, path)) path.Clear();
        // The grid path ends on the nearest cell centre; finish on the exact spot.
        if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], target) > 0.01f) path.Add(target);

        StartWalking(from);
        phase = Phase.GoToSpot;
    }

    private void BeginCast()
    {
        phase = Phase.Cast;
        facingLeft = false;
        spriteRenderer.flipX = false;
        frameAnimator.Play(ClipCast, restart: true);
    }

    // Anything hooked but not yet landed is thrown away; the otter just
    // stands up from the spot and starts wandering again.
    private void StopFishing()
    {
        pendingCatch = null;
        EnterIdle();
    }

    private void SnapToSpot()
    {
        Vector2 p = spot.FeetPosition;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    // -------------------------------------------------------------- wandering

    private void EnterIdle()
    {
        ReleasePlay();
        phase = Phase.Idle;
        timer = RandomInRange(idleSeconds);
        path.Clear();
        pathIndex = 0;
        // Idle is a side view facing right; mirror it to keep the last facing.
        spriteRenderer.flipX = facingLeft;
        frameAnimator.Play(ClipIdle);
    }

    // ------------------------------------------------------------------ toys

    private bool TryBeginPlay()
    {
        var board = DecorBoardView.Active;
        if (board == null || walkableArea == null || !board.RollWantsToPlay()) return false;

        Vector2 from = transform.position;
        if (!board.TryReservePlay(from, walkableArea.IsWalkable, out var session)) return false;

        if (!walkableArea.TryFindPath(from, session.StandPoint, path) || path.Count == 0)
        {
            session.Release();
            return false;
        }

        playSession = session;
        StartWalking(from);
        phase = Phase.WalkToPlay;
        return true;
    }

    private void BeginPlay()
    {
        phase = Phase.Play;
        timer = playSession.Seconds;
        facingLeft = playSession.LookPoint.x < transform.position.x;
        spriteRenderer.flipX = facingLeft;
        frameAnimator.Play(ClipIdle);
        playSession.Begin();
    }

    private void ReleasePlay()
    {
        if (playSession == null) return;
        playSession.Release();
        playSession = null;
    }

    private bool TryBeginWander()
    {
        if (walkableArea == null) return false;

        Vector2 from = transform.position;
        float min = Mathf.Min(wanderDistance.x, wanderDistance.y);
        float max = Mathf.Max(wanderDistance.x, wanderDistance.y);
        for (int i = 0; i < maxDestinationTries; i++)
        {
            if (!walkableArea.TryPickDestination(from, min, max, out Vector2 destination)) continue;
            if (!walkableArea.TryFindPath(from, destination, path) || path.Count == 0) continue;

            StartWalking(from);
            phase = Phase.Walk;
            return true;
        }
        return false;
    }

    private void StartWalking(Vector2 from)
    {
        pathIndex = 0;
        walkTimer = 0f;
        walkTimeLimit = PathLength(from) / walkSpeed * StuckTimeMultiplier + StuckGraceSeconds;
        spriteRenderer.flipX = false;
    }

    // Moves along `path`; true once the end is reached (or the walk is
    // clearly stuck, in which case the caller treats it as arrived).
    private bool StepWalk(float deltaTime)
    {
        walkTimer += deltaTime;
        if (walkTimer > walkTimeLimit) return true;

        // Carry leftover distance across waypoints so speed stays constant
        // through corners.
        float remaining = walkSpeed * deltaTime;
        Vector2 position = transform.position;
        Vector2 direction = Vector2.zero;
        while (remaining > 0f && pathIndex < path.Count)
        {
            Vector2 toTarget = path[pathIndex] - position;
            float distance = toTarget.magnitude;
            if (distance > 0.0001f) direction = toTarget / distance;

            if (distance <= remaining)
            {
                position = path[pathIndex];
                remaining -= distance;
                pathIndex++;
            }
            else
            {
                position += direction * remaining;
                remaining = 0f;
            }
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (direction != Vector2.zero) PlayWalkClip(direction);
        return pathIndex >= path.Count;
    }

    private void PlayWalkClip(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            facingLeft = direction.x < 0f;
            frameAnimator.Play(facingLeft ? ClipWalkLeft : ClipWalkRight);
        }
        else
        {
            frameAnimator.Play(direction.y < 0f ? ClipWalkDown : ClipWalkUp);
        }
    }

    private float PathLength(Vector2 from)
    {
        float length = 0f;
        Vector2 prev = from;
        foreach (var p in path)
        {
            length += Vector2.Distance(prev, p);
            prev = p;
        }
        return length;
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
