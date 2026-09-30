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
public class OtterWanderAgent : MonoBehaviour
{
    public enum State { Idle, Walk, Play }

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

    public State CurrentState { get; private set; } = State.Idle;
    public float WalkSpeed { get; private set; }
    // Direction of the current path segment (zero while idle). Stable for a
    // whole segment, so visuals keyed off it don't jitter frame to frame.
    public Vector2 MoveDirection { get; private set; }
    // Where to face while playing (the toy).
    public Vector2 LookTarget { get; private set; }

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
                if (stateTimer <= 0f && !TryStartWalk())
                {
                    EnterIdle(settings.RollIdleSeconds());
                }
                break;

            case State.Walk:
                UpdateWalk(Time.deltaTime);
                break;

            case State.Play:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f || !playSession.IsValid)
                {
                    EnterIdle(settings.RollIdleSeconds());
                }
                break;
        }
    }

    private void OnDisable()
    {
        ReleasePlay();
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

    private bool TryStartWalk()
    {
        if (TryStartPlayWalk()) return true;

        Vector2 from = transform.position;
        for (int i = 0; i < settings.maxDestinationTries; i++)
        {
            if (!area.TryPickDestination(from, settings.MinDestinationDistance, settings.MaxDestinationDistance, out Vector2 destination))
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

    private void BeginWalk(Vector2 from)
    {
        pathIndex = 0;
        stateTimer = 0f;
        pathVersion = area.Version;
        walkTimeLimit = PathLength(from) / Mathf.Max(WalkSpeed, 0.01f) * StuckTimeMultiplier + StuckGraceSeconds;
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
        float remaining = WalkSpeed * deltaTime;
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
            if (walkingToPlay && playSession != null && playSession.IsValid) EnterPlay();
            else EnterIdle(settings.RollIdleSeconds());
        }
    }

    private void EnterIdle(float duration)
    {
        ReleasePlay();
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
