using System.Collections.Generic;
using UnityEngine;

// The mine-scene otter. Off duty it wanders the sand in front of the mine
// (Idle <-> Walk, paths from PlazaWalkableArea). When mining is turned on it
// walks to the MineEntranceView oval, then on into the tunnel while fading
// out; once inside it is hidden and the MineEmoteView bubble shows instead.
// While inside, every find interval (15-30s) it rolls diamond or stone into
// the bag and the bubble pops the item's icon. Leaving the scene or stopping
// throws away the time towards the next find.
// When mining is turned off it fades back in at the tunnel, walks out to the
// oval and goes back to wandering.
//
// GameManager.IsMiningActive (saved) is the single source of truth, as with
// fishing: the oval's "start" prompt and the emote's "stop" prompt only flip
// it and Update() follows. A save that was mining starts the scene with the
// otter already inside.
//
// The transform position is the otter's feet (sprites are pivoted there).
[RequireComponent(typeof(SpriteFrameAnimator))]
public class MinerOtterController : MonoBehaviour
{
    // Clip names — MineSceneSetup builds SpriteFrameAnimator clips with these.
    public const string ClipIdle = "Idle";
    public const string ClipWalkRight = "WalkRight";
    public const string ClipWalkLeft = "WalkLeft";
    public const string ClipWalkDown = "WalkDown";
    public const string ClipWalkUp = "WalkUp";

    // Order matters: everything from GoToEntrance on counts as "on mining duty".
    private enum Phase { Idle, Walk, GoToEntrance, Entering, Inside }

    // Walking longer than expected * this (+ grace) counts as stuck.
    private const float StuckTimeMultiplier = 2f;
    private const float StuckGraceSeconds = 1f;

    [Header("Scene references")]
    [SerializeField] private PlazaWalkableArea walkableArea;
    [SerializeField] private MineEntranceView entrance;
    [SerializeField] private MineEmoteView emote;

    [Header("Wandering")]
    [Tooltip("World units per second.")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 1.2f;
    [SerializeField] private Vector2 idleSeconds = new Vector2(2f, 5f);
    [Tooltip("Straight-line distance to each wander destination, world units.")]
    [SerializeField] private Vector2 wanderDistance = new Vector2(1f, 3.5f);
    [SerializeField, Min(1)] private int maxDestinationTries = 10;

    [Header("Entering / leaving the tunnel")]
    [Tooltip("Speed multiplier for the fade walk between the oval and the inside point.")]
    [SerializeField, Min(0.01f)] private float tunnelSpeedScale = 0.6f;

    [Header("Rendering")]
    [Tooltip("Above the background (0) and the entrance oval (1).")]
    [SerializeField] private int spriteSortingOrder = 2;

    private SpriteFrameAnimator frameAnimator;
    private SpriteRenderer spriteRenderer;

    private Phase phase = Phase.Idle;
    private float timer;
    private readonly List<Vector2> path = new List<Vector2>();
    private int pathIndex;
    private float walkTimer;
    private float walkTimeLimit;
    private float currentSpeed;
    private bool facingLeft;
    // Fade walks only: true while walking out of the tunnel (fading in).
    private bool leavingTunnel;
    private float tunnelDistance;

    private bool IsOnMiningDuty => phase >= Phase.GoToEntrance;

    private static bool MiningActive =>
        GameManager.Instance != null && GameManager.Instance.IsMiningActive;

    private void Awake()
    {
        frameAnimator = GetComponent<SpriteFrameAnimator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = spriteSortingOrder;
    }

    private void Start()
    {
        if (MiningActive) EnterInside(snap: true);
        else EnterIdle();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool active = MiningActive;
        if (active && !IsOnMiningDuty) BeginGoToEntrance();
        else if (!active && IsOnMiningDuty) StopMining();

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Idle:
                timer -= dt;
                if (timer <= 0f && !TryBeginWander()) EnterIdle();
                break;

            case Phase.Walk:
                if (StepWalk(dt))
                {
                    // A walk out of the tunnel ends back in full view.
                    if (leavingTunnel) SetAlpha(1f);
                    leavingTunnel = false;
                    EnterIdle();
                }
                else if (leavingTunnel)
                {
                    SetAlpha(1f - RemainingDistance() / tunnelDistance);
                }
                break;

            case Phase.GoToEntrance:
                if (StepWalk(dt))
                {
                    SnapTo(entrance.FeetPosition);
                    BeginEntering();
                }
                break;

            case Phase.Entering:
                if (StepWalk(dt)) EnterInside(snap: true);
                else SetAlpha(RemainingDistance() / tunnelDistance);
                break;

            case Phase.Inside:
                timer -= dt;
                if (timer <= 0f)
                {
                    var mining = GameManager.Instance.MiningService;
                    var found = GameManager.Instance.AddMiningFind(mining.RollFind());
                    if (found != null && emote != null) emote.ShowFind(found.Icon);
                    timer = mining.RollFindIntervalSec();
                }
                break;
        }
    }

    // ---------------------------------------------------------------- mining

    private void BeginGoToEntrance()
    {
        // Turned back on while still walking out: turn around mid-fade.
        if (leavingTunnel)
        {
            leavingTunnel = false;
            BeginEntering();
            return;
        }

        SetAlpha(1f);
        Vector2 from = transform.position;
        Vector2 target = entrance.FeetPosition;

        path.Clear();
        if (walkableArea == null || !walkableArea.TryFindPath(from, target, path)) path.Clear();
        // The grid path ends on the nearest cell centre; finish on the exact spot.
        if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], target) > 0.01f) path.Add(target);

        StartWalking(from, walkSpeed);
        phase = Phase.GoToEntrance;
    }

    private void BeginEntering()
    {
        Vector2 from = transform.position;
        path.Clear();
        path.Add(entrance.InsidePosition);
        tunnelDistance = Mathf.Max(0.01f, Vector2.Distance(entrance.FeetPosition, entrance.InsidePosition));
        StartWalking(from, walkSpeed * tunnelSpeedScale);
        phase = Phase.Entering;
    }

    private void EnterInside(bool snap)
    {
        if (snap) SnapTo(entrance.InsidePosition);
        phase = Phase.Inside;
        timer = GameManager.Instance != null ? GameManager.Instance.MiningService.RollFindIntervalSec() : 0f;
        path.Clear();
        spriteRenderer.enabled = false;
        if (emote != null) emote.SetVisible(true);
    }

    // Stopped on the way to the oval: just start wandering from here.
    // Stopped while (going) inside: fade back in and walk out to the oval,
    // then wander. That walk counts as off duty, so turning mining back on
    // mid-way sends the otter straight back in.
    private void StopMining()
    {
        if (emote != null) emote.SetVisible(false);
        spriteRenderer.enabled = true;

        if (phase == Phase.GoToEntrance)
        {
            SetAlpha(1f);
            EnterIdle();
            return;
        }

        Vector2 from = transform.position;
        path.Clear();
        path.Add(entrance.FeetPosition);
        tunnelDistance = Mathf.Max(0.01f, Vector2.Distance(entrance.InsidePosition, entrance.FeetPosition));
        leavingTunnel = true;
        SetAlpha(1f - RemainingDistance(from) / tunnelDistance);
        StartWalking(from, walkSpeed * tunnelSpeedScale);
        phase = Phase.Walk;
    }

    private void SnapTo(Vector2 p)
    {
        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }

    private void SetAlpha(float alpha)
    {
        var c = spriteRenderer.color;
        c.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = c;
    }

    // Straight-line distance from `from` (default: the otter) to the end of
    // the current one-point fade walk.
    private float RemainingDistance(Vector2? from = null)
    {
        if (path.Count == 0) return 0f;
        Vector2 p = from ?? (Vector2)transform.position;
        return Vector2.Distance(p, path[path.Count - 1]);
    }

    // -------------------------------------------------------------- wandering

    private void EnterIdle()
    {
        phase = Phase.Idle;
        timer = RandomInRange(idleSeconds);
        path.Clear();
        pathIndex = 0;
        // Idle is a side view facing right; mirror it to keep the last facing.
        spriteRenderer.flipX = facingLeft;
        frameAnimator.Play(ClipIdle);
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

            StartWalking(from, walkSpeed);
            phase = Phase.Walk;
            return true;
        }
        return false;
    }

    private void StartWalking(Vector2 from, float speed)
    {
        currentSpeed = speed;
        pathIndex = 0;
        walkTimer = 0f;
        walkTimeLimit = PathLength(from) / currentSpeed * StuckTimeMultiplier + StuckGraceSeconds;
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
        float remaining = currentSpeed * deltaTime;
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
