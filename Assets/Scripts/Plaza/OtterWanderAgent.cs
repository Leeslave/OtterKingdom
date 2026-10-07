using System.Collections.Generic;
using UnityEngine;

// Idle -> Walk -> Idle loop for one plaza otter. The transform position is
// the otter's feet; all movement queries go through PlazaWalkableArea.
//
// Each otter rolls its own idle durations and walk speed, so a group spawned
// on the same frame drifts out of sync immediately. A path is computed once
// per destination (never per frame). If no destination/path is found within
// the try budget, or a walk takes far longer than it should (stuck), the
// otter just idles again and retries later — no immediate re-search loop.
//
// Keeps running while off-screen; nothing here depends on the camera.
//
// Toys: when choosing the next walk, the otter may instead reserve a placed
// toy (DecorBoardView.Active), walk to a spot beside it and Play there for a
// while. The reservation is always released when play ends or is cut short.
// Placing/moving a toy bumps PlazaWalkableArea.Version; a walk planned on an
// older version is abandoned so the otter re-plans around the new obstacle.
//
// Tasks: AssignTask sends the otter to a spot (e.g. a house it is building)
// where it stays in Play facing the work until ClearTask — the same look as
// playing with a toy, without a toy reservation.
//
// Crowd: destinations near another otter (or where one is heading) are
// skipped, and an idle otter standing on top of another one moves off
// (PlazaCrowd). Passing each other while walking is fine.
//
// Habits: an otter with a trait sometimes goes to stand near its favourite
// kind of place (a woodcutter by the trees, a recorder by the board...) and
// does the actions that fit it there (SetHabit, from SettlementPlazaView).
//
// Spots: otherwise it may reserve a free place at a PlazaSpotView (sit on a
// chair or bench, eat at the table, gather under a lamp at night), walk
// there and stay a while. Sitting hops up onto the seat and back down to the
// walkable floor in front of it before doing anything else.
public class OtterWanderAgent : MonoBehaviour, IPlazaCrowdMember
{
    // Idle otters closer than this many body widths step aside.
    private const float OverlapInWidths = 0.8f;
    // After stepping aside (or failing to), wait this long before checking again.
    private const float OverlapRetrySeconds = 1.5f;
    private float nextOverlapCheck;

    public enum State { Idle, Walk, Play }

    // Walking to a task (e.g. building a house) is hurried, so the otter
    // gets there while the work is still going on.
    private const float TaskWalkSpeedMultiplier = 2.5f;

    // Pause before re-planning after the obstacles changed mid-walk.
    private const float ReplanDelaySeconds = 0.3f;

    // Walking longer than expected * this (+ grace) counts as stuck.
    private const float StuckTimeMultiplier = 2f;
    private const float StuckGraceSeconds = 1f;

    private PlazaWalkableArea area;
    private PlazaSettings settings;
    private bool initialized;

    private readonly List<Vector2> path = new List<Vector2>();
    private int pathIndex;
    private float stateTimer;
    private float walkTimeLimit;
    private int pathVersion;
    private DecorPlaySession playSession;
    private bool walkingToPlay;

    // Habit: favourite places of this otter's trait
    private const float HabitChance = 0.3f;
    private const float HabitRadius = 1.6f;
    private const float HabitMinDistance = 0.6f;
    private static readonly Vector2 HabitSeconds = new Vector2(6f, 12f);
    private System.Func<Transform> pickHabitTarget;
    private IReadOnlyList<string> habitActions;
    private bool walkingToHabit;
    private bool atHabit;

    // Visiting a chair / table / lamp
    private enum SpotPhase { None, HopIn, Stay, HopOut }
    private const float HopSeconds = 0.3f;
    private const float HopHeight = 0.45f;
    private PlazaSpotSession spotSession;
    private bool walkingToSpot;
    private SpotPhase spotPhase;
    private float hopTimer;
    private Vector2 hopFrom;
    private Vector2 hopTo;

    private bool hasTask;
    private bool walkingToTask;
    private Vector2 taskStandPoint;
    private Vector2 taskLookPoint;

    public State CurrentState { get; private set; } = State.Idle;

    public Vector2 Position => transform.position;
    public Vector2 Goal => CurrentState == State.Walk && path.Count > 0 ? path[path.Count - 1] : Position;
    public float WalkSpeed { get; private set; }
    // Direction of the current path segment (zero while idle). Stable for a
    // whole segment, so visuals keyed off it don't jitter frame to frame.
    public Vector2 MoveDirection { get; private set; }
    // Where to face while playing (the toy).
    public Vector2 LookTarget { get; private set; }

    // Ground height used for front/back sorting: the feet, except while
    // sitting up on a chair (then just in front of the chair).
    public float DepthY => spotSession != null && spotSession.Hops && spotPhase != SpotPhase.None ? spotSession.DepthY : transform.position.y;

    // Actions to pick from while staying at a spot or a favourite place (null = any).
    public IReadOnlyList<string> PreferredActions => spotSession != null && spotPhase == SpotPhase.Stay ? spotSession.Actions
        : atHabit ? habitActions : null;

    // Favourite places of this otter's trait: pickTarget returns one (or null if none now), actions fit them.
    public void SetHabit(System.Func<Transform> pickTarget, IReadOnlyList<string> actions)
    {
        pickHabitTarget = pickTarget;
        habitActions = actions;
    }

    // Sitting on a chair / standing at a spot (for bubbles etc.).
    public bool IsAtSpot => spotSession != null && spotPhase != SpotPhase.None;

    // Working at the task spot (not walking there).
    public bool IsOnTask => hasTask && CurrentState == State.Play && playSession == null;

    // Go to standPoint and stay there facing lookPoint until ClearTask.
    public void AssignTask(Vector2 standPoint, Vector2 lookPoint)
    {
        bool same = hasTask && standPoint == taskStandPoint;
        hasTask = true;
        taskStandPoint = standPoint;
        taskLookPoint = lookPoint;
        if (initialized && !same) EnterIdle(0f);
    }

    // Put the otter on its task spot right away (restoring a manager at its
    // station on scene load, or a walk there that never arrived).
    public void WarpToTask()
    {
        if (!hasTask) return;
        ReleasePlay();
        ReleaseSpot();
        SetFeetPosition(taskStandPoint);
        if (initialized) EnterTask();
    }

    public void ClearTask()
    {
        if (!hasTask) return;
        hasTask = false;
        walkingToTask = false;
        if (initialized && CurrentState != State.Walk) EnterIdle(settings.RollIdleSeconds());
    }

    public void Initialize(PlazaWalkableArea walkableArea, PlazaSettings plazaSettings)
    {
        area = walkableArea;
        settings = plazaSettings;
        WalkSpeed = settings.RollWalkSpeed();
        EnterIdle(settings.RollIdleSeconds());
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        switch (CurrentState)
        {
            case State.Idle:
                stateTimer -= Time.deltaTime;
                // Standing on another otter: move off now instead of waiting out the idle
                if (stateTimer > 0f && !hasTask && Time.time >= nextOverlapCheck
                    && PlazaCrowd.IsOverlapping(this, settings.otterBodyWidth * OverlapInWidths))
                {
                    stateTimer = 0f;
                    nextOverlapCheck = Time.time + OverlapRetrySeconds;
                }
                if (stateTimer <= 0f && !TryStartWalk())
                {
                    EnterIdle(settings.RollIdleSeconds());
                }
                break;

            case State.Walk:
                UpdateWalk(Time.deltaTime);
                break;

            case State.Play:
                if (spotSession != null)
                {
                    UpdateSpot(Time.deltaTime);
                    break;
                }
                if (atHabit)
                {
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) EnterIdle(settings.RollIdleSeconds());
                    break;
                }
                if (playSession == null)
                {
                    // Task: stays until ClearTask
                    if (!hasTask) EnterIdle(settings.RollIdleSeconds());
                    break;
                }
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f || !playSession.IsValid)
                {
                    EnterIdle(settings.RollIdleSeconds());
                }
                break;
        }
    }

    private void OnEnable()
    {
        PlazaCrowd.Register(this);
    }

    private void OnDisable()
    {
        PlazaCrowd.Unregister(this);
        ReleasePlay();
        ReleaseSpot();
    }

    // Reserve a toy and a spot beside it, then walk there.
    private bool TryStartPlayWalk()
    {
        var board = DecorBoardView.Active;
        if (board == null || !board.RollWantsToPlay()) return false;

        Vector2 from = transform.position;
        if (!board.TryReservePlay(from, area.IsWalkable, out var session)) return false;

        if (!area.TryFindPath(from, session.StandPoint, path) || path.Count == 0)
        {
            session.Release();
            return false;
        }

        playSession = session;
        walkingToPlay = true;
        BeginWalk(from);
        return true;
    }

    private void EnterPlay()
    {
        walkingToPlay = false;
        CurrentState = State.Play;
        MoveDirection = Vector2.zero;
        LookTarget = playSession.LookPoint;
        stateTimer = playSession.Seconds;
        path.Clear();
        pathIndex = 0;
        playSession.Begin();
    }

    private void ReleasePlay()
    {
        walkingToPlay = false;
        if (playSession == null) return;
        playSession.Release();
        playSession = null;
    }

    // Reserve a seat / place at a spot (chair, table, lamp), then walk in front of it.
    private bool TryStartSpotWalk()
    {
        if (!PlazaSpotView.RollWantsToVisit()) return false;

        Vector2 from = transform.position;
        if (!PlazaSpotView.TryReserve(from, area.IsWalkable, out var session)) return false;

        if (!area.TryFindPath(from, session.StandPoint, path) || path.Count == 0)
        {
            session.Release();
            return false;
        }

        spotSession = session;
        walkingToSpot = true;
        BeginWalk(from);
        return true;
    }

    private void EnterSpot()
    {
        walkingToSpot = false;
        CurrentState = State.Play;
        MoveDirection = Vector2.zero;
        LookTarget = spotSession.LookPoint;
        stateTimer = spotSession.Seconds;
        path.Clear();
        pathIndex = 0;
        if (spotSession.Hops)
            BeginHop(transform.position, spotSession.SeatPoint, SpotPhase.HopIn);
        else
            spotPhase = SpotPhase.Stay;
    }

    private void BeginHop(Vector2 from, Vector2 to, SpotPhase phase)
    {
        spotPhase = phase;
        hopFrom = from;
        hopTo = to;
        hopTimer = 0f;
    }

    private void UpdateSpot(float deltaTime)
    {
        switch (spotPhase)
        {
            case SpotPhase.HopIn:
            case SpotPhase.HopOut:
                hopTimer += deltaTime;
                float k = Mathf.Clamp01(hopTimer / HopSeconds);
                Vector2 p = Vector2.Lerp(hopFrom, hopTo, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * HopHeight);
                SetFeetPosition(p);
                if (k < 1f) break;
                if (spotPhase == SpotPhase.HopOut) EnterIdle(settings.RollIdleSeconds());
                else spotPhase = SpotPhase.Stay;
                break;

            default:
                stateTimer -= deltaTime;
                if (stateTimer > 0f && spotSession.IsValid) break;
                // Down from the chair to the floor in front of it before wandering again
                if (spotSession.Hops) BeginHop(transform.position, spotSession.StandPoint, SpotPhase.HopOut);
                else EnterIdle(settings.RollIdleSeconds());
                break;
        }
    }

    private void ReleaseSpot()
    {
        walkingToSpot = false;
        if (spotSession == null) return;
        // Cut short while up on a chair: back on the walkable floor first
        if (spotSession.Hops && spotPhase != SpotPhase.None) SetFeetPosition(spotSession.StandPoint);
        spotPhase = SpotPhase.None;
        spotSession.Release();
        spotSession = null;
    }

    // Walk to a free spot near one of the trait's favourite places
    private bool TryStartHabitWalk()
    {
        if (pickHabitTarget == null || Random.value >= HabitChance) return false;
        var target = pickHabitTarget();
        if (target == null) return false;

        Vector2 center = target.position;
        Vector2 from = transform.position;
        for (int i = 0; i < 4; i++)
        {
            if (!area.TryGetRandomPointNear(center, HabitRadius, 12, out Vector2 point)) return false;
            if ((point - center).sqrMagnitude < HabitMinDistance * HabitMinDistance) continue;
            if (!PlazaCrowd.IsFree(point, settings.SpawnSpacing, this)) continue;
            if (!area.TryFindPath(from, point, path) || path.Count == 0) continue;

            LookTarget = center;
            walkingToHabit = true;
            BeginWalk(from);
            return true;
        }
        return false;
    }

    private void EnterHabit()
    {
        walkingToHabit = false;
        atHabit = true;
        CurrentState = State.Play;
        MoveDirection = Vector2.zero;
        stateTimer = Random.Range(HabitSeconds.x, HabitSeconds.y);
        path.Clear();
        pathIndex = 0;
    }

    private bool TryStartWalk()
    {
        if (hasTask) return TryStartTaskWalk();
        if (TryStartPlayWalk()) return true;
        if (TryStartSpotWalk()) return true;
        if (TryStartHabitWalk()) return true;

        Vector2 from = transform.position;
        for (int i = 0; i < settings.maxDestinationTries; i++)
        {
            if (!area.TryPickDestination(from, settings.MinDestinationDistance, settings.MaxDestinationDistance, out Vector2 destination))
            {
                continue;
            }
            if (!PlazaCrowd.IsFree(destination, settings.SpawnSpacing, this))
            {
                continue;
            }
            if (!area.TryFindPath(from, destination, path) || path.Count == 0)
            {
                continue;
            }

            BeginWalk(from);
            return true;
        }
        return false;
    }

    private bool TryStartTaskWalk()
    {
        Vector2 from = transform.position;
        if ((taskStandPoint - from).sqrMagnitude <= settings.ArriveDistance * settings.ArriveDistance)
        {
            EnterTask();
            return true;
        }
        if (!area.TryFindPath(from, taskStandPoint, path) || path.Count == 0) return false;

        walkingToTask = true;
        BeginWalk(from);
        return true;
    }

    private void EnterTask()
    {
        walkingToTask = false;
        CurrentState = State.Play;
        MoveDirection = Vector2.zero;
        LookTarget = taskLookPoint;
        path.Clear();
        pathIndex = 0;
    }

    private void BeginWalk(Vector2 from)
    {
        pathIndex = 0;
        stateTimer = 0f;
        pathVersion = area.Version;
        walkTimeLimit = PathLength(from) / Mathf.Max(CurrentWalkSpeed, 0.01f) * StuckTimeMultiplier + StuckGraceSeconds;
        CurrentState = State.Walk;
        MoveDirection = (path[0] - from).normalized;
    }

    private void UpdateWalk(float deltaTime)
    {
        stateTimer += deltaTime;
        if (stateTimer > walkTimeLimit)
        {
            EnterIdle(settings.RollIdleSeconds());
            return;
        }
        if (area.Version != pathVersion)
        {
            // A toy was placed/moved/removed: this path may now cross it.
            EnterIdle(ReplanDelaySeconds);
            return;
        }

        // Carry leftover distance across waypoints so speed stays constant
        // through corners.
        float remaining = CurrentWalkSpeed * deltaTime;
        Vector2 position = transform.position;
        while (remaining > 0f && pathIndex < path.Count)
        {
            Vector2 target = path[pathIndex];
            Vector2 toTarget = target - position;
            float distance = toTarget.magnitude;
            bool isFinal = pathIndex == path.Count - 1;

            if (isFinal && distance <= settings.ArriveDistance)
            {
                pathIndex++;
                break;
            }

            if (distance <= remaining)
            {
                position = target;
                remaining -= distance;
                pathIndex++;
                if (pathIndex < path.Count) MoveDirection = (path[pathIndex] - position).normalized;
            }
            else
            {
                position += toTarget / distance * remaining;
                remaining = 0f;
            }
        }

        SetFeetPosition(position);

        if (pathIndex >= path.Count)
        {
            if (walkingToTask && hasTask) EnterTask();
            else if (walkingToPlay && playSession != null && playSession.IsValid) EnterPlay();
            else if (walkingToSpot && spotSession != null && spotSession.IsValid) EnterSpot();
            else if (walkingToHabit) EnterHabit();
            else EnterIdle(settings.RollIdleSeconds());
        }
    }

    // Speed of the current walk (faster on the way to a task). Visuals match the walk clip to it.
    public float CurrentWalkSpeed => walkingToTask ? WalkSpeed * TaskWalkSpeedMultiplier : WalkSpeed;

    private void EnterIdle(float duration)
    {
        ReleasePlay();
        ReleaseSpot();
        walkingToHabit = false;
        atHabit = false;
        walkingToTask = false;
        CurrentState = State.Idle;
        MoveDirection = Vector2.zero;
        stateTimer = duration;
        path.Clear();
        pathIndex = 0;
    }

    private void SetFeetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
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

    private void OnDrawGizmos()
    {
        if (CurrentState != State.Walk || area == null || !area.ShowAgentPaths) return;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Vector3 prev = transform.position;
        for (int i = pathIndex; i < path.Count; i++)
        {
            Vector3 p = new Vector3(path[i].x, path[i].y, prev.z);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.DrawWireSphere(prev, 0.15f);
    }
}
