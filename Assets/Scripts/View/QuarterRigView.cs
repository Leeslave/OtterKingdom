using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Look-only cutout rig for the quarter-view (V14) otter: builds the front
// (SE) and rear (NE) part hierarchies from Resources/<resourceFolder>
// (rig.json + one sprite per part, see Tools/QuarterRig) and poses them in
// code — idle breathing, and a sole-anchored walk whose step rate follows
// the walk speed. West-facing views are the east ones mirrored.
//
// Actions are code-posed too, as keyframed offsets on top of the idle pose:
// PlayHarvest crouches at the crop, tugs twice, pops it out holding the crop
// up (sprite given by the caller, e.g. the crop's item icon) and tucks it
// into the backpack. IsActing is true until it is back to idle.
// PlayLoop runs a toy loop until it is changed or stopped:
//   Kick   - keepy-uppy beside a ball: wind up, kick the near leg forward
//            (swinging from the hip), balance with the arms - one kick per two
//            bounces of the toy (PlacedDecorView's Bounce, 2.2 a second),
//            landing on the second touchdown when started with the toy.
//   Puzzle - crouch at a puzzle beside the feet, hands on it fiddling with
//            the pieces, head tilting while it thinks, then a little "got it"
//            lift of the arm before going back to it.
//
// SetSitting(true) settles onto a seat whose surface is this transform (a
// chair or bench spot): bottom on the seat, legs stretched out in front, and
// dozing - the head sinks slowly with the eyes closed (front-head-sleep),
// jerks back up with a startled blink, and a "z" (fx-z) floats up now and then.
//
// This transform is the feet. It never moves itself: OtterVisualController
// feeds it from OtterWanderAgent (SetWalking / Face), like the Animator on
// the sprite otters, and does the depth sorting on the root SortingGroup.
// The parts sit in their own SortingGroup so they draw as one piece.
//
// The part objects are rebuilt on enable and never saved; in edit mode they
// are hidden and show the idle pose, so prefabs and scenes still preview.
//
// Optional Resources/<resourceFolder>/tuning.json (saved by the rig editor,
// Tools/QuarterRig/rig-editor.html) nudges single parts per view: an offset
// (rig px, y down) and rotation (degrees, clockwise) on the joint, which
// carries the parts attached to it, and a scale on that part's sprite alone.
// The editor previews the same rig code, so what it shows is what Unity draws.
[ExecuteAlways]
public sealed class QuarterRigView : MonoBehaviour
{
    [SerializeField] private string resourceFolder = "QuarterRigV14";
    [Tooltip("0.503 makes the standing otter ~1.67 world units tall (PlazaSettings.otterHeight).")]
    [SerializeField, Min(.1f)] private float characterScale = .503f;
    [Tooltip("Walk speed (world units/s) at which the step cycle plays at its authored length.")]
    [SerializeField, Min(.01f)] private float walkReferenceSpeed = 1.1f;
    [Tooltip("Seconds to blend between standing and walking, so stopping mid-stride doesn't pop.")]
    [SerializeField, Min(0f)] private float walkBlendSeconds = .12f;
    [SerializeField] private bool startFacingLeft;
    [Tooltip("Sorting order of the whole rig (its parts sort inside it). Farm: above the crop slots.")]
    [SerializeField] private int sortingOrder;
    [Tooltip("Height of a crop held in the hand, in rig pixels (the otter is ~330).")]
    [SerializeField, Min(1f)] private float heldCropHeight = 95f;

    // Rig pixels (y down) per world unit before characterScale; the parts are imported at PPU 200 = 2 px per rig px.
    private const float RigPixelsPerUnit = 100f;
    // Far foot rests a little higher (further back on the ground).
    private const float FarLegRestY = 4f;

    // One action key: time (s) and offsets from the idle pose - hips drop (rig px, + = down),
    // angles (rad, + = clockwise: near arm swings out/back, far arm in), feet spread (rig px),
    // the held crop's scale (0 = hidden), the near leg's kick (degrees forward, swung from the hip)
    // and how busily the hands fiddle (0..1, small quick taps on top of the arm angles).
    private readonly struct Key
    {
        public readonly float t, hips, body, head, near, far, tail, pack, spread, crop, kick, fiddle;
        public Key(float t, float hips, float body, float head, float near, float far, float tail, float pack, float spread, float crop,
            float kick = 0f, float fiddle = 0f)
        {
            this.t = t; this.hips = hips; this.body = body; this.head = head; this.near = near;
            this.far = far; this.tail = tail; this.pack = pack; this.spread = spread; this.crop = crop; this.kick = kick;
            this.fiddle = fiddle;
        }
    }

    public enum Loop { None, Kick, Puzzle }

    private static readonly Key[] HarvestKeys =
    {
        //       t     hips   body   head   near   far    tail   pack  spread crop
        new Key(0f,     0f,   0f,    0f,    0f,    0f,    0f,    0f,   0f,   0f),
        new Key(.32f,  36f,  .06f,  .10f, -.35f,  .30f,  .18f,   0f,   9f,   0f),   // squat, hands to the crop
        new Key(.52f,  28f, -.07f,  .02f, -.20f,  .18f,  .08f,   0f,   9f,   0f),   // tug
        new Key(.70f,  36f,  .06f,  .10f, -.35f,  .30f,  .18f,   0f,   9f,   0f),
        new Key(.90f,  26f, -.10f, -.02f, -.16f,  .14f,  .06f,   0f,   9f,   0f),   // tug harder
        new Key(1.06f, 38f,  .05f,  .12f, -.40f,  .34f,  .20f,   0f,  10f,   0f),   // strain
        new Key(1.26f,-10f, -.07f, -.10f, -1.80f, -.60f, -.12f, -.04f,  2f,  1.2f),  // pop! up it comes
        new Key(1.42f,  0f, -.03f, -.06f, -1.62f, -.45f, -.06f,   0f,   0f,   1f),   // held up in front of the chest
        new Key(1.78f,  0f, -.02f, -.05f, -1.66f, -.40f, -.04f,   0f,   0f,   1f),
        new Key(2.06f,  3f,  .04f,  .03f,  1.0f,  .10f,  .03f,  .12f,  0f,   0f),   // into the backpack
        new Key(2.24f,  0f,   0f,    0f,    0f,    0f,  -.03f, -.07f,  0f,   0f),
        new Key(2.40f,  0f,   0f,    0f,    0f,    0f,    0f,    0f,   0f,   0f),
    };
    // Keepy-uppy, looped: two of the toy's bounces (1 / 2.2 s each) per kick, the kick landing on the second.
    private static readonly Key[] KickKeys =
    {
        //       t     hips   body   head   near   far    tail   pack  spread crop  kick
        new Key(0f,     0f,   0f,    0f,    0f,    0f,    0f,    0f,   0f,   0f,   0f),
        new Key(.27f,   4f, -.02f,   0f,   .22f, -.15f,  .05f,   0f,   0f,   0f, -18f),  // wind up, leg back
        new Key(.455f, -7f, -.07f, -.06f,  .55f, -.45f,  .16f, -.05f,  0f,   0f,  58f),  // kick on the bounce
        new Key(.62f,  -2f, -.03f, -.03f,  .30f, -.25f,  .08f,   0f,   0f,   0f,  28f),  // follow through
        new Key(.909f,  0f,   0f,    0f,    0f,    0f,    0f,    0f,   0f,   0f,   0f),
    };
    // Kicks swing the leg about a hip this far above the sole.
    private const float KickHipHeight = 64f;

    // Puzzle on the ground beside the feet: squat into reach (first key), then loop from PuzzleLoopFrom.
    private static readonly Key[] PuzzleKeys =
    {
        //       t     hips   body   head   near   far    tail   pack  spread crop  kick  fiddle
        new Key(0f,     0f,   0f,    0f,    0f,    0f,    0f,    0f,   0f,   0f,   0f,  0f),
        new Key(.30f,  24f,  .06f,  .12f, -.70f, -.50f,  .10f,   0f,   6f,   0f,   0f,  1f),  // squat, hands on the pieces
        new Key(1.20f, 22f,  .04f,  .02f, -.65f, -.45f,  .14f,   0f,   6f,   0f,   0f,  1f),  // head up a little: hmm
        new Key(1.90f, 24f,  .07f,  .14f, -.72f, -.52f,  .10f,   0f,   6f,   0f,   0f,  1f),
        new Key(2.15f,  6f, -.04f, -.10f, -1.50f, -.90f, -.10f, -.03f,  3f,   0f,   0f,  0f),  // got it! arm up
        new Key(2.45f, 10f, -.02f, -.06f, -1.30f, -.80f, -.04f,   0f,   3f,   0f,   0f,  0f),
        new Key(2.90f, 24f,  .06f,  .12f, -.70f, -.50f,  .10f,   0f,   6f,   0f,   0f,  1f),  // = the .30 key, back to it
    };
    private const float PuzzleLoopFrom = .30f;
    // Fiddling taps: a quick wobble of both forearms (rad) and a slow head tilt.
    private const float FiddleTapsPerSecond = 3f, FiddleNear = .12f, FiddleFar = .09f, FiddleHead = .035f;

    // From here on the crop is going into the backpack, so it draws behind the body.
    private const float HarvestStashTime = 1.95f;
    // Held-crop sorting inside the rig: over the body while shown, under it while stashed.
    private const int HeldCropOrderShown = 9;
    private const int HeldCropOrderStashed = 1;

    // Sitting, in rig px (y up): the bottom settles onto the seat point and the legs stretch out in
    // front - in this view diagonally down-right, towards the camera - from hips tucked under the belly, slightly
    // foreshortened. They stay behind the body, like standing.
    private const float SitHipsDrop = 30f;
    private static readonly Vector2 SitNearHip = new Vector2(-31.4f, 23.9f), SitFarHip = new Vector2(17.2f, 26.3f);
    private const float SitNearAngle = 75.5f, SitFarAngle = 80.5f;  // degrees from pointing down
    private const float SitLegLength = 72f, SitLegForeshorten = .95f;
    private const float SitBlendSeconds = .25f;
    // One nod: the head sinks for most of it, jerks back up, then settles.
    private const float NodSeconds = 3.4f;
    private const float NodSinkEnd = .72f, NodJerkEnd = .78f;
    // The eyes pop open just after the jerk.
    private const float BlinkFrom = .74f, BlinkTo = .86f;
    // Zzz: a "z" every so often while the head sinks; each rises and fades out.
    private const float ZEverySeconds = .95f;
    private const float ZLifeSeconds = 1.7f;
    private static readonly Vector2 ZStart = new Vector2(.85f, 2.55f), ZRise = new Vector2(.45f, 1.0f);
    private const int ZOrder = 10;

    [Serializable] private class Bone { public string name, parent; public float[] pos; public int layer; }
    [Serializable] private class LegSolver { public float stanceFraction, cycleSeconds, stepHalfWidth, lift, nearX, farX, swingAngle; }
    [Serializable] private class Rig { public LegSolver legSolver; public Bone[] bones, rearBones; }
    [Serializable] private class PartTuning { public string part; public float x, y, rotation, scaleX = 1f, scaleY = 1f; }
    [Serializable] private class Tuning { public PartTuning[] front, rear; }
    private static readonly PartTuning NoTuning = new PartTuning();

    private sealed class View
    {
        public GameObject root;
        public Bone[] bones;
        public readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        public readonly Dictionary<string, PartTuning> tuning = new Dictionary<string, PartTuning>();
        public PartTuning TuningOf(string part) => tuning.TryGetValue(part, out var t) ? t : NoTuning;
    }

    private LegSolver legs;
    private View front, rear;
    private GameObject visual;
    private bool walking;
    private float walkSpeed;
    private float walkBlend;
    private float clock, phase;
    private bool facingRear, facingLeft;
    private SortingGroup sorting;
    private SpriteRenderer heldCrop;
    private Vector3 handOffset;
    private Key[] action;
    private float actionTime;
    private bool actionLoops;
    private float actionLoopFrom;
    private bool sitting;
    private float sitBlend, nodTime, zTimer;
    private SpriteRenderer frontHead;
    private Sprite awakeHead, sleepHead, zSprite;
    private readonly List<SpriteRenderer> zs = new List<SpriteRenderer>();
    private readonly List<float> zAges = new List<float>();

    public bool FacingLeft => facingLeft;
    // Widest part (the hat brim, 270 rig px) in world units, e.g. for the ground shadow.
    public float Width => 2.7f * characterScale * Mathf.Abs(transform.lossyScale.x);
    public bool IsActing => action != null;
    public Loop CurrentLoop => action == KickKeys ? Loop.Kick : action == PuzzleKeys ? Loop.Puzzle : Loop.None;
    public bool IsSitting => sitting;

    public int SortingOrder
    {
        get => sortingOrder;
        set { sortingOrder = value; if (sorting != null) sorting.sortingOrder = value; }
    }

    // Pull a crop out of the ground at the feet and put it in the backpack (~2.4 s, front view).
    public void PlayHarvest(Sprite crop)
    {
        walking = false;
        facingRear = false;
        action = HarvestKeys;
        actionTime = 0f;
        actionLoops = false;
        if (heldCrop != null) heldCrop.sprite = crop;
    }

    // Start a toy loop (from its beginning, so a Kick started with the toy's bounce stays in time),
    // or with Loop.None stop one that is running (a harvest is left alone).
    public void PlayLoop(Loop loop)
    {
        if (loop == Loop.None)
        {
            if (actionLoops) StopAction();
            return;
        }
        walking = false;
        action = loop == Loop.Kick ? KickKeys : PuzzleKeys;
        actionTime = 0f;
        actionLoops = true;
        actionLoopFrom = loop == Loop.Kick ? 0f : PuzzleLoopFrom;
    }

    public void StopAction()
    {
        action = null;
        actionLoops = false;
        if (heldCrop != null) heldCrop.gameObject.SetActive(false);
    }

    // Sit down on the seat here (this transform = seat surface) and doze, or get back up.
    public void SetSitting(bool sit)
    {
        if (sit == sitting) return;
        sitting = sit;
        if (!sit) return;
        walking = false;
        facingRear = false;
        StopAction();
        nodTime = 0f;
        zTimer = ZEverySeconds * .5f;
    }

    // Walk animation on/off; speed (world units/s) sets the step rate.
    public void SetWalking(bool isWalking, float speed)
    {
        walking = isWalking;
        walkSpeed = speed;
    }

    // Face a direction: away from the camera when it points up, mirrored when it points left.
    // Mostly-vertical directions (|x| below minHorizontalRatio of the length) keep the current left/right,
    // and horizontal ones keep the current front/back, so neither flips back and forth.
    public void Face(Vector2 direction, float minHorizontalRatio = .3f)
    {
        float length = direction.magnitude;
        if (length < .0001f) return;
        if (Mathf.Abs(direction.y) > .05f * length) facingRear = direction.y > 0f;
        if (Mathf.Abs(direction.x) >= minHorizontalRatio * length) facingLeft = direction.x < 0f;
    }

    private void OnEnable()
    {
        facingLeft = startFacingLeft;
        var data = Resources.Load<TextAsset>(resourceFolder + "/rig");
        if (data == null) { Debug.LogError($"[{nameof(QuarterRigView)}] Resources/{resourceFolder}/rig.json missing", this); return; }
        var rig = JsonUtility.FromJson<Rig>(data.text);
        var tuningData = Resources.Load<TextAsset>(resourceFolder + "/tuning");
        var tuning = tuningData != null ? JsonUtility.FromJson<Tuning>(tuningData.text) : null;
        legs = rig.legSolver;
        visual = NewChild("Rig (generated)", transform);
        sorting = visual.AddComponent<SortingGroup>();
        sorting.sortingOrder = sortingOrder;
        front = Build(rig.bones, false, tuning?.front);
        rear = Build(rig.rearBones, true, tuning?.rear);
        sleepHead = Resources.Load<Sprite>(resourceFolder + "/front-head-sleep");
        zSprite = Resources.Load<Sprite>(resourceFolder + "/fx-z");
        ApplyPose();
    }

    private void OnDisable()
    {
        if (visual == null) return;
        if (Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
        visual = null;
        heldCrop = null;
        frontHead = null;
        action = null;
        zs.Clear();
        zAges.Clear();
    }

    private static GameObject NewChild(string label, Transform parent)
    {
        var go = new GameObject(label);
        if (!Application.isPlaying) go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(parent, false);
        return go;
    }

    private View Build(Bone[] bones, bool back, PartTuning[] tuning)
    {
        var v = new View { bones = bones, root = NewChild(back ? "Rear" : "Front", visual.transform) };
        if (tuning != null)
            foreach (var t in tuning)
                if (!string.IsNullOrEmpty(t.part)) v.tuning[t.part] = t;
        foreach (var b in bones)
        {
            // Legs are posed against the ground, not the bobbing hips.
            var parent = IsLeg(b) || string.IsNullOrEmpty(b.parent) ? v.root.transform : v.joints[b.parent];
            var joint = NewChild(b.name, parent).transform;
            v.joints.Add(b.name, joint);
            if (b.name == "hips") continue;
            var sprite = Resources.Load<Sprite>($"{resourceFolder}/{(back ? "rear-" : "front-")}{b.name}");
            if (sprite == null) { Debug.LogError($"[{nameof(QuarterRigView)}] Missing rig part: {b.name}", this); continue; }
            // The sprite sits on its own child so a part's scale doesn't carry over to the parts attached to it.
            var tuned = v.TuningOf(b.name);
            var spriteObject = NewChild("Sprite", joint);
            spriteObject.transform.localScale = new Vector3(tuned.scaleX, tuned.scaleY, 1f);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = b.layer;
            if (!back && b.name == "nearArm") AddHeldCrop(joint, sprite, tuned.scaleY);
            if (!back && b.name == "head") { frontHead = renderer; awakeHead = sprite; }
        }
        return v;
    }

    // A hidden sprite in the near hand for crops; the hand is near the tip of the arm sprite.
    private void AddHeldCrop(Transform arm, Sprite armSprite, float armScale)
    {
        heldCrop = NewChild("HeldCrop", arm).AddComponent<SpriteRenderer>();
        heldCrop.sortingOrder = HeldCropOrderShown;
        heldCrop.gameObject.SetActive(false);
        handOffset = Vector3.down * (armSprite.pivot.y / armSprite.pixelsPerUnit * .85f * armScale);
    }

    private void Update()
    {
        if (visual == null) return;
        if (Application.isPlaying)
        {
            float dt = Time.deltaTime;
            walkBlend = walkBlendSeconds > 0f ? Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt / walkBlendSeconds) : walking ? 1f : 0f;
            clock += dt;
            // Step phase in cycles; only advances while walking so the stride resumes where it stopped.
            // Bigger otters take longer steps, so the reference speed scales with the rig.
            float reference = walkReferenceSpeed * Mathf.Abs(transform.lossyScale.y);
            if (walking) phase += dt / legs.cycleSeconds * Mathf.Max(walkSpeed, .01f) / reference;
            if (action != null)
            {
                actionTime += dt;
                float length = action[action.Length - 1].t;
                if (actionTime >= length)
                {
                    if (actionLoops) actionTime = actionLoopFrom + (actionTime - length);
                    else StopAction();
                }
            }
            sitBlend = Mathf.MoveTowards(sitBlend, sitting ? 1f : 0f, dt / SitBlendSeconds);
            if (sitting) nodTime += dt;
            UpdateZs(dt);
        }
        ApplyPose();
    }

    private void ApplyPose()
    {
        visual.transform.localScale = new Vector3(facingLeft ? -characterScale : characterScale, characterScale, 1f);
        front.root.SetActive(!facingRear);
        rear.root.SetActive(facingRear);
        var v = facingRear ? rear : front;
        float w = walkBlend;
        float p = phase * Mathf.PI * 2f;
        var a = action != null ? Sample(action, actionTime) : default;
        if (heldCrop != null) PoseHeldCrop(a);
        float sb = sitBlend * sitBlend * (3f - 2f * sitBlend);
        float nod = Mathf.Repeat(nodTime / NodSeconds, 1f);
        float droop = sb * Droop(nod);
        if (frontHead != null)
            frontHead.sprite = sitting && sleepHead != null && sb > .5f && (nod < BlinkFrom || nod >= BlinkTo) ? sleepHead : awakeHead;
        foreach (var b in v.bones)
        {
            var joint = v.joints[b.name];
            var tuned = v.TuningOf(b.name);
            if (IsLeg(b)) { PoseLeg(joint, b.name == "nearLeg", w, a.spread, sb, tuned, b.name == "nearLeg" ? a.kick : 0f); continue; }
            float x = b.pos[0], y = b.pos[1], angle = 0f;
            switch (b.name)
            {
                case "hips": y += 8f - 3.8f * w * (1f - Mathf.Cos(2f * p)); break;
                case "body": y += (1f - w) * Mathf.Sin(clock * 3f) * .6f; angle = -.04f * Mathf.Sin(p) * w; break;
                case "head": angle = .024f * Mathf.Sin(p - .4f) * w; break;
                case "nearArm": angle = .38f * Mathf.Sin(p - .15f) * w; break;
                case "farArm":
                    angle = -.38f * Mathf.Sin(p - .15f) * w;
                    // From behind the far arm swings inward, tucked against the back.
                    if (facingRear) { y += 1.2f * Mathf.Sin(p - .15f) * w; angle = -.18f - .10f * Mathf.Sin(p - .15f) * w; }
                    break;
                case "tail": angle = .13f * Mathf.Sin(p - .8f) * w + (1f - w) * .025f * Mathf.Sin(clock * 2f); break;
                case "backpack": angle = .045f * Mathf.Sin(p - .6f) * w; break;
            }
            switch (b.name)
            {
                case "hips": y += a.hips; break;
                case "body": angle += a.body; break;
                case "head": angle += a.head; break;
                case "nearArm": angle += a.near + a.fiddle * FiddleNear * Mathf.Sin(clock * FiddleTapsPerSecond * Mathf.PI * 2f); break;
                case "farArm": angle += a.far + a.fiddle * FiddleFar * Mathf.Sin(clock * FiddleTapsPerSecond * Mathf.PI * 2f + 1.3f); break;
                case "tail": angle += a.tail; break;
                case "backpack": angle += a.pack; break;
            }
            if (b.name == "head") angle += a.fiddle * FiddleHead * Mathf.Sin(clock * 1.3f);
            // Sitting and dozing: settled on the seat, hands in the lap, slow deep breaths, nodding off.
            switch (b.name)
            {
                case "hips": y += SitHipsDrop * sb; break;
                case "body": y += sb * Mathf.Sin(clock * 1.7f) * 1.4f; angle += .035f * droop; break;
                case "head": y += 10f * droop; angle += .28f * droop; break;
                case "nearArm": angle += -.4f * sb; break;
                case "farArm": angle += .18f * sb; break;
                case "tail": angle += .22f * sb; break;
            }
            x += tuned.x;
            y += tuned.y;
            angle += tuned.rotation * Mathf.Deg2Rad;
            joint.localPosition = new Vector3(x, -y, 0f) / RigPixelsPerUnit;
            joint.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
        }
    }

    // Rigid leg with its sole on the ground: slides back during stance, lifts and swings forward otherwise.
    private void PoseLeg(Transform joint, bool near, float w, float spread, float sit, PartTuning tuned, float kick)
    {
        float q = Mathf.Repeat(phase + (near ? 0f : .5f), 1f);
        bool stance = q < legs.stanceFraction;
        float u = stance ? q / legs.stanceFraction : (q - legs.stanceFraction) / (1f - legs.stanceFraction);
        float ease = u * u * (3f - 2f * u);
        float travel = legs.stepHalfWidth * w * (stance ? 1f - 2f * u : -1f + 2f * ease);
        float lift = stance ? 0f : legs.lift * w * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.3f);
        var stand = new Vector2((near ? legs.nearX - spread : legs.farX + spread) + travel, (near ? 0f : FarLegRestY) + lift);
        float swing = (stance ? 0f : -legs.swingAngle * w * Mathf.Sin(2f * Mathf.PI * u)) * Mathf.Rad2Deg;

        // Seated: the sole sits a leg's length out from the hip, the leg turned to point there
        // (the sprite's pivot is the sole, its up axis runs back to the hip).
        float degrees = near ? SitNearAngle : SitFarAngle;
        float length = SitLegLength * SitLegForeshorten;
        var toFoot = new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), -Mathf.Cos(degrees * Mathf.Deg2Rad));
        var seated = (near ? SitNearHip : SitFarHip) + toFoot * length;

        var position = Vector2.Lerp(stand, seated, sit) + new Vector2(tuned.x, -tuned.y);
        float rotation = Mathf.Lerp(swing, degrees, sit) - tuned.rotation;
        if (kick != 0f)
        {
            // Swing about the hip: the sole moves forward and up, the leg turned to keep pointing at the hip.
            var hip = position + Vector2.up * KickHipHeight;
            position = hip + new Vector2(Mathf.Sin(kick * Mathf.Deg2Rad), -Mathf.Cos(kick * Mathf.Deg2Rad)) * KickHipHeight;
            rotation += kick;
        }
        joint.localPosition = position / RigPixelsPerUnit;
        joint.localRotation = Quaternion.Euler(0f, 0f, rotation);
        joint.localScale = new Vector3(1f, Mathf.Lerp(1f, SitLegForeshorten, sit), 1f);
    }

    // Shown only during the action's crop keys: gripped by the leaves so it hangs mostly below the hand,
    // and kept upright (with a little wobble) whatever the arm does.
    private void PoseHeldCrop(Key a)
    {
        bool show = a.crop > .01f && heldCrop.sprite != null;
        if (heldCrop.gameObject.activeSelf != show) heldCrop.gameObject.SetActive(show);
        if (!show) return;
        heldCrop.sortingOrder = actionTime >= HarvestStashTime ? HeldCropOrderStashed : HeldCropOrderShown;
        var bounds = heldCrop.sprite.bounds;
        float height = heldCropHeight / RigPixelsPerUnit;
        float scale = height / bounds.size.y * a.crop;
        heldCrop.transform.localScale = Vector3.one * scale;
        heldCrop.transform.rotation = visual.transform.rotation * Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(clock * 9f));
        // Centre the crop .42 of its height below the hand, in the arm's (rotated) space.
        var arm = heldCrop.transform.parent;
        Vector3 hand = arm.TransformPoint(handOffset);
        Vector3 down = visual.transform.TransformVector(Vector3.down * height * a.crop * .42f);
        Vector3 pivotToCentre = heldCrop.transform.TransformVector(bounds.center);
        heldCrop.transform.position = hand + down - pivotToCentre;
    }

    // How far the head has sunk (0..1) at this point of a nod; a little negative just after the jerk.
    private static float Droop(float nod)
    {
        if (nod < NodSinkEnd) return Smooth(nod / NodSinkEnd);
        if (nod < NodJerkEnd) return Mathf.Lerp(1f, -.25f, Smooth((nod - NodSinkEnd) / (NodJerkEnd - NodSinkEnd)));
        return Mathf.Lerp(-.25f, 0f, Smooth((nod - NodJerkEnd) / (1f - NodJerkEnd)));
    }

    private static float Smooth(float u) => u * u * (3f - 2f * u);

    // Zzz above the head while dozing: rise, drift sideways, grow and fade out. Pooled.
    private void UpdateZs(float dt)
    {
        float nod = Mathf.Repeat(nodTime / NodSeconds, 1f);
        if (sitting && sitBlend >= 1f && zSprite != null && nod > .1f && nod < NodSinkEnd)
        {
            zTimer -= dt;
            if (zTimer <= 0f)
            {
                zTimer = ZEverySeconds;
                SpawnZ();
            }
        }
        for (int i = 0; i < zs.Count; i++)
        {
            var z = zs[i];
            if (!z.gameObject.activeSelf) continue;
            float age = zAges[i] += dt;
            float t = age / ZLifeSeconds;
            if (t >= 1f) { z.gameObject.SetActive(false); continue; }
            var pos = ZStart + new Vector2(ZRise.x * t + .12f * Mathf.Sin(t * 7f), ZRise.y * t);
            z.transform.localPosition = pos;
            z.transform.localScale = Vector3.one * Mathf.Lerp(.7f, 1.15f, t);
            z.transform.localRotation = Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(t * 5f));
            // Under the mirrored rig the letter would read backwards.
            z.flipX = facingLeft;
            z.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 6f) * (1f - Smooth(Mathf.Clamp01((t - .55f) / .45f))));
        }
    }

    private void SpawnZ()
    {
        int i = zs.FindIndex(z => !z.gameObject.activeSelf);
        if (i < 0)
        {
            var z = NewChild("Z", visual.transform).AddComponent<SpriteRenderer>();
            z.sprite = zSprite;
            z.sortingOrder = ZOrder;
            zs.Add(z);
            zAges.Add(0f);
            i = zs.Count - 1;
        }
        zAges[i] = 0f;
        zs[i].gameObject.SetActive(true);
    }

    // Smoothstep between the two keys around t.
    private static Key Sample(Key[] keys, float t)
    {
        int i = 1;
        while (i < keys.Length - 1 && keys[i].t < t) i++;
        Key k0 = keys[i - 1], k1 = keys[i];
        float u = Mathf.Clamp01((t - k0.t) / Mathf.Max(k1.t - k0.t, .0001f));
        u = u * u * (3f - 2f * u);
        return new Key(t, Mathf.Lerp(k0.hips, k1.hips, u), Mathf.Lerp(k0.body, k1.body, u), Mathf.Lerp(k0.head, k1.head, u),
            Mathf.Lerp(k0.near, k1.near, u), Mathf.Lerp(k0.far, k1.far, u), Mathf.Lerp(k0.tail, k1.tail, u),
            Mathf.Lerp(k0.pack, k1.pack, u), Mathf.Lerp(k0.spread, k1.spread, u), Mathf.Lerp(k0.crop, k1.crop, u),
            Mathf.Lerp(k0.kick, k1.kick, u), Mathf.Lerp(k0.fiddle, k1.fiddle, u));
    }

    private static bool IsLeg(Bone b) => b.name.EndsWith("Leg", StringComparison.Ordinal);
}
