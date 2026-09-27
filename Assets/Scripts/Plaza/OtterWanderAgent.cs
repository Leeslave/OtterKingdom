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
public class OtterWanderAgent : MonoBehaviour
{
    public enum State { Idle, Walk }

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

    public State CurrentState { get; private set; } = State.Idle;
    public float WalkSpeed { get; private set; }
    // Direction of the current path segment (zero while idle). Stable for a
    // whole segment, so visuals keyed off it don't jitter frame to frame.
    public Vector2 MoveDirection { get; private set; }

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
        }
    }

    private bool TryStartWalk()
    {
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

            pathIndex = 0;
            stateTimer = 0f;
            walkTimeLimit = PathLength(from) / Mathf.Max(WalkSpeed, 0.01f) * StuckTimeMultiplier + StuckGraceSeconds;
            CurrentState = State.Walk;
            MoveDirection = (path[0] - from).normalized;
            return true;
        }
        return false;
    }

    private void UpdateWalk(float deltaTime)
    {
        stateTimer += deltaTime;
        if (stateTimer > walkTimeLimit)
        {
            EnterIdle(settings.RollIdleSeconds());
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
            EnterIdle(settings.RollIdleSeconds());
        }
    }

    private void EnterIdle(float duration)
    {
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
