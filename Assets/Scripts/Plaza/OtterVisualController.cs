using UnityEngine;
using UnityEngine.Rendering;

// Look-only side of a plaza otter: drives the Animator from
// OtterWanderAgent's state, mirrors the sprite for left/right, and orders
// otters so the one with the lower feet Y draws in front. Never moves the
// transform (the Animator has root motion off and its clips only swap
// sprites). Depth uses PlazaDepth, the same rule as PlazaProp, so otters
// pass behind trees/houses and in front of them correctly.
//
// Works with FarmerOtter.controller's parameters (bool IsMoving, int
// WalkDir, float WalkAnimSpeed) but checks they exist first, so a prefab
// with a different or missing controller still wanders as a static sprite.
//
// A prefab without an Animator controller can use a SpriteFrameAnimator
// instead, with clips named Idle / WalkRight / WalkLeft / WalkDown / WalkUp
// (the miner's plaza look reuses its mine clips). It has its own left walk,
// so the sprite is never mirrored.
//
// Sorting goes through a SortingGroup on the root so any child renderers
// (e.g. a future shadow) move in depth together with the body.
//
// While the agent plays with a toy the otter faces it and loops the idle
// action triggers that exist on the controller (FarmerOtter: Squat/Stretch/
// Net/Eat; visitors: Eat/Happy, Yawn/Sleep, Paint/Wave — see
// OtterVisitorSpriteSetup), each returning to Idle before the next one fires.
[RequireComponent(typeof(OtterWanderAgent))]
public class OtterVisualController : MonoBehaviour
{
    // Values of the Animator's "WalkDir" int — must match FarmerOtterSpriteSetup.
    private enum WalkDir { Side = 0, Down = 1, Up = 2 }

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int WalkDirHash = Animator.StringToHash("WalkDir");
    private static readonly int WalkAnimSpeedHash = Animator.StringToHash("WalkAnimSpeed");
    private static readonly string[] PlayTriggerNames =
    {
        "Squat", "Stretch", "Net", "Eat",
        "Happy", "Yawn", "Sleep", "Paint", "Wave",
    };
    // Short breather in Idle between two play actions.
    private const float PlayActionGapSeconds = 0.4f;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Optional. Leave empty (or without a controller) to show a static sprite.")]
    [SerializeField] private Animator animator;
    [SerializeField] private SortingGroup sortingGroup;
    [Tooltip("Optional. Code-driven clips (Idle, WalkRight, WalkLeft, WalkDown, WalkUp) used when there is no Animator controller.")]
    [SerializeField] private SpriteFrameAnimator frameAnimator;

    [Header("Facing")]
    [Tooltip("True if the unflipped sprite faces right.")]
    [SerializeField] private bool spriteFacesRight = true;
    [Tooltip("Only re-evaluate left/right when |horizontal| / |move| is at least this, so near-vertical walks don't flip back and forth.")]
    [SerializeField, Range(0f, 1f)] private float minHorizontalRatioToFlip = 0.3f;

    [Header("Walk clips")]
    [Tooltip("Use Walk_Down / Walk_Up for mostly-vertical movement. Off = always side walk.")]
    [SerializeField] private bool useVerticalWalkClips = true;
    [Tooltip("Vertical must beat horizontal by this factor to switch to up/down (and vice versa), avoiding flicker on diagonals.")]
    [SerializeField, Min(1f)] private float directionHysteresis = 1.3f;
    [Tooltip("Walk speed (world units/s) at which the walk clip plays at 1x. FarmerOtter's clips were tuned for 1.5.")]
    [SerializeField, Min(0.01f)] private float walkAnimReferenceSpeed = 1.5f;

    private OtterWanderAgent agent;
    private bool hasIsMoving;
    private bool hasWalkDir;
    private bool hasWalkAnimSpeed;
    private WalkDir currentDir = WalkDir.Side;
    private readonly System.Collections.Generic.List<int> playTriggers = new System.Collections.Generic.List<int>();
    private float playActionTimer;
    private bool facingRight = true;

    private bool UsesFrameClips => frameAnimator != null && (animator == null || animator.runtimeAnimatorController == null);

    private void Awake()
    {
        agent = GetComponent<OtterWanderAgent>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
        if (frameAnimator == null) frameAnimator = GetComponentInChildren<SpriteFrameAnimator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController != null)
            {
                foreach (var p in animator.parameters)
                {
                    if (p.nameHash == IsMovingHash && p.type == AnimatorControllerParameterType.Bool) hasIsMoving = true;
                    else if (p.nameHash == WalkDirHash && p.type == AnimatorControllerParameterType.Int) hasWalkDir = true;
                    else if (p.nameHash == WalkAnimSpeedHash && p.type == AnimatorControllerParameterType.Float) hasWalkAnimSpeed = true;
                    else if (p.type == AnimatorControllerParameterType.Trigger && System.Array.IndexOf(PlayTriggerNames, p.name) >= 0) playTriggers.Add(p.nameHash);
                }
            }
            if (!hasIsMoving)
            {
                Debug.LogWarning($"[{nameof(OtterVisualController)}] Animator on '{name}' has no bool 'IsMoving' — showing a static sprite.", this);
            }
        }
    }

    private void LateUpdate()
    {
        bool walking = agent.CurrentState == OtterWanderAgent.State.Walk;
        Vector2 dir = agent.MoveDirection;

        if (walking && dir.sqrMagnitude > 0f)
        {
            UpdateFacing(dir);
            UpdateWalkDir(dir);
        }

        if (hasIsMoving) animator.SetBool(IsMovingHash, walking);
        if (hasWalkAnimSpeed) animator.SetFloat(WalkAnimSpeedHash, agent.CurrentWalkSpeed / walkAnimReferenceSpeed);
        if (UsesFrameClips) frameAnimator.Play(FrameClip(walking));

        if (agent.CurrentState == OtterWanderAgent.State.Play) UpdatePlay();

        UpdateSorting();
    }

    private void UpdatePlay()
    {
        float dx = agent.LookTarget.x - transform.position.x;
        if (spriteRenderer != null && !UsesFrameClips && Mathf.Abs(dx) > 0.01f)
        {
            spriteRenderer.flipX = (dx > 0f) != spriteFacesRight;
        }

        if (playTriggers.Count == 0) return;

        // Fire the next action only once the previous one has handed back to Idle.
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Idle") || animator.IsInTransition(0))
        {
            playActionTimer = PlayActionGapSeconds;
            return;
        }

        playActionTimer -= Time.deltaTime;
        if (playActionTimer > 0f) return;

        animator.SetTrigger(playTriggers[Random.Range(0, playTriggers.Count)]);
        playActionTimer = PlayActionGapSeconds;
    }

    private void UpdateFacing(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) < minHorizontalRatioToFlip * dir.magnitude) return;

        facingRight = dir.x > 0f;
        // Frame clips have their own left walk, so the sprite isn't mirrored.
        if (spriteRenderer != null && !UsesFrameClips) spriteRenderer.flipX = facingRight != spriteFacesRight;
    }

    private string FrameClip(bool walking)
    {
        if (!walking) return "Idle";
        switch (currentDir)
        {
            case WalkDir.Down: return "WalkDown";
            case WalkDir.Up: return "WalkUp";
            default: return facingRight ? "WalkRight" : "WalkLeft";
        }
    }

    private void UpdateWalkDir(Vector2 dir)
    {
        if (!hasWalkDir && !UsesFrameClips) return;

        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        WalkDir next = currentDir;

        if (!useVerticalWalkClips)
        {
            next = WalkDir.Side;
        }
        else if (currentDir == WalkDir.Side)
        {
            if (ay > ax * directionHysteresis) next = dir.y < 0f ? WalkDir.Down : WalkDir.Up;
        }
        else
        {
            if (ax > ay * directionHysteresis) next = WalkDir.Side;
            else if (ay > 0f) next = dir.y < 0f ? WalkDir.Down : WalkDir.Up;
        }

        currentDir = next;
        if (hasWalkDir) animator.SetInteger(WalkDirHash, (int)currentDir);
    }

    private void UpdateSorting()
    {
        int order = PlazaDepth.SortingOrderFor(transform.position.y);
        if (sortingGroup != null) sortingGroup.sortingOrder = order;
        else if (spriteRenderer != null) spriteRenderer.sortingOrder = order;
    }
}
