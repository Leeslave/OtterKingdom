using System;
using System.Collections.Generic;
using System.Linq;
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
//   Fish   - only rigs with a rod (Resources/<folder>/rod.png, the sailor):
//            swing the rod back and cast, wait with the float bobbing on its
//            ripple, a bite pulls the tip down, then a strike lifts the rod,
//            and back to waiting. Rod, line, float and ripple are made here.
//   Build  - only rigs with a hammer (Resources/<folder>/hammer.png, the
//            야구 해달): crouch at a log (log.png) on the ground in front, hammer
//            it twice with chips (chip.png) flying off each hit, look the work
//            over, and again. With a sledgehammer instead (sledge.png, the
//            작업반장): heave it up over the head in both paws, bring it down in
//            one heavy blow that jolts the body, chips flying wide, and again.
//   Photo  - only rigs with a flash (Resources/<folder>/flash_star.png, the
//            사진가; the camera is part of its arm), paparazzi style like the
//            raccoon reporters of 주간너굴: crouch, glance both ways, camera up to
//            the eye, a burst of three flashes, then a look at the shots on the
//            camera and a little hop with a sparkle over the head.
//   Ring   - only rigs with a smiling head (Resources/<folder>/front-head-smile.png,
//            the 행상인; the bell is part of its far arm): stands where it is,
//            raises the bell and rings it, beckoning with the other paw and
//            bobbing along, eyes smiling, calling the customers in.
//   Sweep  - only rigs with a dust puff (Resources/<folder>/sweep_dust.png, the
//            청소부; the broom is part of its far arm): stands where it is and
//            sweeps the broom to and fro, dust puffing up from the bristles at the
//            end of each stroke, looking down at the work.
//   Mine   - only rigs with a rock (Resources/<folder>/rock_0.png, the 광부;
//            the pickaxe is part of its arm): crouch at a rock in front, swing
//            the pickaxe in both paws from behind, over the top and down onto it three times - each blow cracks it a stage
//            (rock_1, rock_2) with stone chips flying (stone_chip), the third
//            breaks it to rubble (rock_rubble) - straighten up, and a new rock.
// A rig can have any number of these (the 주인공 has them all). One whose tool is
// not drawn into its own arm gets an arm holding it for that action:
// front-<arm>-<tag>.png (tag mine, photo, ring, sweep, or play for the guitar
// when seated - Tools/HeroRig/tool_arms.py) is shown instead of the arm while it
// runs.
//
// SetSitting(true) settles onto a seat whose surface is this transform (a
// chair or bench spot): bottom on the seat, legs stretched out in front, and
// (a rig with fx-note.png, the 음악가: legs crossed, eyes softly closed, swaying
// as it strums its guitar, music notes floating up - instead of)
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
    // how busily the hands fiddle (0..1, small quick taps on top of the arm angles), and fishing: the
    // rod's tilt (rad from pointing forward, + = tip down), whether it's in the paw, how far the line
    // has dropped to the water (0..1) and how hard a fish is biting (0..1); building: whether the hammer is in
    // the paw and the log on the ground (0/1), and a hit (1 at the blow, chips flying off as it falls to 0);
    // mining: the rock's stage (0 whole .. 2 cracked, 3 rubble; held until the next key, not blended, -1 = no rock);
    // two-handed: the near arm's joint moved (rig px, y down) and how far (0..1) the far arm turns to grab the
    // tool's handle just above the near paw (GripOnHandle, in the near arm's own space).
    private readonly struct Key
    {
        public readonly float t, hips, body, head, near, far, tail, pack, spread, crop, kick, fiddle, rod, hold, line, bite, hammer, hit, stone,
            nearX, nearY, grip, flash, up, sparkle, ring, smile, dust;
        public Key(float t, float hips, float body, float head, float near, float far, float tail, float pack, float spread, float crop,
            float kick = 0f, float fiddle = 0f, float rod = 0f, float hold = 0f, float line = 0f, float bite = 0f, float hammer = 0f, float hit = 0f,
            float stone = -1f, float nearX = 0f, float nearY = 0f, float grip = 0f, float flash = 0f, float up = 0f, float sparkle = 0f,
            float ring = 0f, float smile = 0f, float dust = 0f)
        {
            this.flash = flash; this.up = up; this.sparkle = sparkle; this.ring = ring; this.smile = smile; this.dust = dust;
            this.t = t; this.hips = hips; this.body = body; this.head = head; this.near = near;
            this.far = far; this.tail = tail; this.pack = pack; this.spread = spread; this.crop = crop; this.kick = kick;
            this.fiddle = fiddle; this.rod = rod; this.hold = hold; this.line = line; this.bite = bite; this.hammer = hammer; this.hit = hit;
            this.stone = stone; this.nearX = nearX; this.nearY = nearY; this.grip = grip;
        }
    }
    // Where the far paw takes hold of a tool held in the near paw: on the handle a little above it (towards the head) (near arm space, rig px, y down).
    private static readonly Vector2 GripOnHandle = new Vector2(5f, -30f);

    public enum Loop { None, Kick, Puzzle, Fish, Build, Mine, Photo, Ring, Sweep }

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
    // Fishing (the arm angles are the sailor's approved rod pose): cast once, then loop from FishLoopFrom.
    private static readonly Key[] FishKeys =
    {
        //       t     hips   body   head   near    far    tail   pack spread crop kick fiddle  rod   hold  line  bite
        new Key(0f,     0f,   0f,    0f,    -.6f,   .3f,   0f,    0f,  0f,   0f,  0f,  0f,  -1.40f, 1f,   0f,   0f),
        new Key(.30f,   2f, -.05f, -.04f, -1.35f,   .50f, -.05f,  0f,  0f,   0f,  0f,  0f,  -2.10f, 1f,   0f,   0f),  // rod back over the shoulder
        new Key(.55f,  -2f,  .06f,  .04f,  -.75f,   .80f,  .10f,  0f,  0f,   0f,  0f,  0f,   -.25f, 1f,   0f,   0f),  // cast; the line drops after
        new Key(.85f,   0f,   0f,    0f,    -.95f,   .75f,  0f,    0f,  0f,   0f,  0f,  0f,   -.36f, 1f,   1f,   0f),  // waiting
        new Key(2.60f,  0f,   0f,    0f,    -.95f,   .75f,  0f,    0f,  0f,   0f,  0f,  0f,   -.36f, 1f,   1f,   0f),
        new Key(2.85f,  0f,  .04f,  .07f,  -.86f,   .78f,  .04f,  0f,  0f,   0f,  0f,  0f,   -.16f, 1f,   1f,   1f),  // a bite pulls the tip down
        new Key(3.05f,  0f,  .03f,  .06f,  -.90f,   .76f,  .02f,  0f,  0f,   0f,  0f,  0f,   -.20f, 1f,   1f,  .7f),
        new Key(3.35f, -3f, -.07f, -.06f, -1.30f,   .55f, -.10f,  0f,  0f,   0f,  0f,  0f,  -1.00f, 1f,   1f,   0f),  // strike!
        new Key(3.80f,  0f,   0f,    0f,    -.95f,   .75f,  0f,    0f,  0f,   0f,  0f,  0f,   -.36f, 1f,   1f,   0f),  // = waiting
    };
    private const float FishLoopFrom = .85f;
    // Rod in rig px: drawn this long, gripped this far up from its butt, this thick; the water is a little below
    // the feet. Line, float and ripple are drawn at these sizes too.
    private const float RodLength = 200f, RodGrip = 15f, RodThickness = 12f, WaterY = -8f;
    private const float LineWidth = 1.6f, FloatSize = 11f, RippleWidth = 30f;
    // The rod sits over the body but under the paw holding it and the head (it swings back behind the head when
    // casting): same order as the near arm, a hair further from the camera. Line and float go on top.
    private const int RodOrderFront = 6, LineOrder = 9, RodOrderRear = 1, RippleOrder = -1;
    private const float RodBehindArm = .01f;
    private static readonly Color LineColour = new Color32(0x65, 0x4b, 0x36, 0xff);
    // Building: crouch at the log, two blows, a look at the work; loops from BuildLoopFrom.
    private static Key B(float t, float hips, float body, float head, float near, float far, float tail, float hit = 0f) =>
        new Key(t, hips, body, head, near, far, tail, 0f, 4f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, hit);
    private static readonly Key[] BuildKeys =
    {
        //    t     hips   body   head   near   far    tail   hit
        B(0f,     0f,    0f,    0f,    -.3f,   0f,    0f),
        B(.40f,  34f,   .08f,  .10f,  -2.4f,  -.35f,  .12f),         // crouched, hammer up
        B(.56f,  40f,   .15f,  .15f,  -1.05f, -.45f,  .16f),
        B(.60f,  42f,   .16f,  .16f,  -.90f,  -.45f,  .16f,  1f),    // bang
        B(.82f,  37f,   .11f,  .12f,  -1.3f,  -.40f,  .12f),
        B(1.00f, 36f,   .08f,  .10f,  -2.4f,  -.35f,  .12f),         // up again
        B(1.16f, 40f,   .15f,  .15f,  -1.05f, -.45f,  .16f),
        B(1.20f, 42f,   .16f,  .16f,  -.90f,  -.45f,  .16f,  1f),    // bang
        B(1.42f, 37f,   .11f,  .12f,  -1.3f,  -.40f,  .12f),
        B(1.90f, 24f,  -.02f, -.06f,  -1.5f,  -.25f,  .05f),         // looks the work over
        B(2.30f, 34f,   .08f,  .10f,  -2.4f,  -.35f,  .12f),         // = the .40 key
    };
    private const float BuildLoopFrom = .40f;
    // The sledgehammer: heaved up over the head with both paws, then one heavy blow.
    private static Key S(float t, float hips, float body, float head, float near, float far, float tail, float hit = 0f) =>
        new Key(t, hips, body, head, near, far, tail, 0f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, hit);
    private static readonly Key[] SledgeKeys =
    {
        //    t     hips   body   head   near   far    tail   hit
        S(0f,     0f,    0f,    0f,    -.3f,  -.2f,   0f),
        S(.50f,  10f,  -.06f, -.10f,  -1.2f,  -.9f,  .06f),          // takes it in both paws
        S(1.10f,  4f,  -.14f, -.18f,  -3.7f,  -3.4f, -.08f),         // heaved up over the shoulder, head behind
        S(1.30f,  2f,  -.16f, -.20f,  -3.9f,  -3.6f, -.10f),         // ...and a breath
        S(1.46f, 14f,   .16f,  .12f,  -1.45f, -1.2f,  .16f),
        S(1.50f, 20f,   .22f,  .18f,  -1.20f, -1.0f,  .22f,  1f),    // BANG
        S(1.62f, 26f,   .18f,  .12f,  -1.15f, -.95f,  .16f, .6f),    // the jolt
        S(1.95f, 22f,   .14f,  .10f,  -1.25f, -1.05f, .14f),
        S(2.40f, 10f,  -.06f, -.10f,  -1.2f,  -.9f,  .06f),          // = the .50 key
    };
    private const float SledgeLoopFrom = .50f;
    // Mining with the 작업반장's swing (its SledgeKeys, converted by Tools/MiningProps/mine_keys.py: the miner's arm
    // turns about the paw, so the paw is moved where the 작업반장's would be and the pickaxe turned to match): take
    // the pickaxe in both paws, heave it up over the shoulder, a breath, one heavy blow - three blows a rock.
    private static Key M(float t, float hips, float body, float head, float near, float nearX, float nearY, float grip, float tail,
        float stone, float hit = 0f) =>
        new Key(t, hips, body, head, near, 0f, tail, 0f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, hit, stone, nearX, nearY, grip);
    private static readonly Key[] MineKeys =
    {
        //    t, hips, body, head, near, nearX, nearY, grip, tail, stone, hit
        M(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.06f, 0f),
        M(0.5f, 10f, -0.06f, -0.1f, 1.67f, 85.9f, 10.7f, 1f, 0.06f, 0f),
        M(1.1f, 4f, -0.14f, -0.18f, -0.83f, -1.8f, -61.9f, 1f, -0.08f, 0f),
        M(1.3f, 2f, -0.16f, -0.2f, -1.03f, -11.3f, -54.6f, 1f, -0.1f, 0f),
        M(1.46f, 14f, 0.16f, 0.12f, 1.42f, 89.6f, -3.8f, 1f, 0.16f, 0f),
        M(1.5f, 20f, 0.22f, 0.18f, 1.67f, 85.9f, 10.7f, 1f, 0.22f, 1f, 1f),   // BANG
        M(1.62f, 26f, 0.18f, 0.12f, 1.72f, 84.8f, 13.5f, 1f, 0.16f, 1f, 0.6f),
        M(1.95f, 22f, 0.14f, 0.1f, 1.62f, 86.9f, 7.9f, 1f, 0.14f, 1f),
        M(2.4f, 10f, -0.06f, -0.1f, 1.67f, 85.9f, 10.7f, 1f, 0.06f, 1f),
        M(3f, 4f, -0.14f, -0.18f, -0.83f, -1.8f, -61.9f, 1f, -0.08f, 1f),
        M(3.2f, 2f, -0.16f, -0.2f, -1.03f, -11.3f, -54.6f, 1f, -0.1f, 1f),
        M(3.36f, 14f, 0.16f, 0.12f, 1.42f, 89.6f, -3.8f, 1f, 0.16f, 1f),
        M(3.4f, 20f, 0.22f, 0.18f, 1.67f, 85.9f, 10.7f, 1f, 0.22f, 2f, 1f),   // BANG
        M(3.52f, 26f, 0.18f, 0.12f, 1.72f, 84.8f, 13.5f, 1f, 0.16f, 2f, 0.6f),
        M(3.85f, 22f, 0.14f, 0.1f, 1.62f, 86.9f, 7.9f, 1f, 0.14f, 2f),
        M(4.3f, 10f, -0.06f, -0.1f, 1.67f, 85.9f, 10.7f, 1f, 0.06f, 2f),
        M(4.9f, 4f, -0.14f, -0.18f, -0.83f, -1.8f, -61.9f, 1f, -0.08f, 2f),
        M(5.1f, 2f, -0.16f, -0.2f, -1.03f, -11.3f, -54.6f, 1f, -0.1f, 2f),
        M(5.26f, 14f, 0.16f, 0.12f, 1.42f, 89.6f, -3.8f, 1f, 0.16f, 2f),
        M(5.3f, 20f, 0.22f, 0.18f, 1.67f, 85.9f, 10.7f, 1f, 0.22f, 3f, 1f),   // BANG
        M(5.42f, 26f, 0.18f, 0.12f, 1.72f, 84.8f, 13.5f, 1f, 0.16f, 3f, 0.6f),
        M(5.75f, 22f, 0.14f, 0.1f, 1.62f, 86.9f, 7.9f, 1f, 0.14f, 3f),
        M(6.3f, 0f, -0.04f, -0.1f, 0f, 0f, 0f, 0f, 0.06f, 3f),   // straightens up, looks at the rubble
        M(6.7f, 10f, -0.06f, -0.1f, 1.67f, 85.9f, 10.7f, 1f, 0.06f, 0f),   // a new rock; = the .50 key
    };
    private const float MineLoopFrom = .50f;
    // Photo: the camera raised to the eye (near arm turned up, drawn over the head) and lowered to look at it.
    private const float NearUp = -.9f, FarUp = -.75f, NearLook = .45f;
    private static Key P(float t, float hips, float body, float head, float near, float far, float fiddle, float flash, float up, float sparkle) =>
        new Key(t, hips, body, head, near, far, 0f, 0f, 4f, 0f, 0f, fiddle, 0f, 0f, 0f, 0f, 0f, 0f, -1f, 0f, 0f, 0f, flash, up, sparkle);
    private static readonly Key[] PhotoKeys =
    {
        //    t     hips   body   head   near   far   fiddle flash up sparkle
        P(0f,     0f,    0f,    0f,     0f,    0f,   0f,   0f,  0f,  0f),
        P(.45f,  12f,   .08f, -.14f,   .15f,  .10f,  0f,   0f,  0f,  0f),   // crouches, glances one way
        P(.85f,  12f,   .08f,  .12f,   .15f,  .10f,  0f,   0f,  0f,  0f),   // ...and the other: nobody looking
        P(1.15f, 14f,   .12f,  .04f, NearUp, FarUp, .35f,  0f,  1f,  0f),   // camera up to the eye
        P(1.45f, 14f,   .12f,  .04f, NearUp, FarUp, .35f,  0f,  1f,  0f),   // zooming in
        P(1.50f, 13f,   .07f,  .02f, NearUp, FarUp,  0f,   1f,  1f,  0f),   // flash!
        P(1.62f, 14f,   .11f,  .04f, NearUp, FarUp,  0f,   0f,  1f,  0f),
        P(1.72f, 13f,   .07f,  .02f, NearUp, FarUp,  0f,   1f,  1f,  0f),   // flash!
        P(1.84f, 14f,   .11f,  .04f, NearUp, FarUp,  0f,   0f,  1f,  0f),
        P(1.94f, 13f,   .07f,  .02f, NearUp, FarUp,  0f,   1f,  1f,  0f),   // flash!
        P(2.10f, 14f,   .11f,  .04f, NearUp, FarUp,  0f,   0f,  1f,  0f),
        P(2.40f,  4f,   .06f,  .20f,  NearLook, .05f, 0f,  0f,  0f,  0f),   // looks at the shots on the camera
        P(2.85f, -8f,  -.04f, -.08f,  NearLook, -.10f, 0f, 0f,  0f,  1f),   // a little hop: got it!
        P(3.10f,  0f,   0f,   -.04f,  NearLook, 0f,  0f,   0f,  0f,  .6f),
        P(3.40f,  0f,   0f,    0f,     .15f,  .10f,  0f,   0f,  0f,  0f),
        P(3.80f, 12f,   .08f, -.14f,   .15f,  .10f,  0f,   0f,  0f,  0f),   // = the .45 key
    };
    private const float PhotoLoopFrom = .45f;
    // The lens in the near arm sprite (arm space, rig px, y down); flash and sparkle sizes (rig px).
    private static readonly Vector2 LensInArm = new Vector2(79f, 36f);
    private static readonly Vector2 SparkleOverHead = new Vector2(70f, -190f);
    private const float FlashSize = 110f, FlashGlowSize = 190f, SparkleSize = 40f;
    private const int FlashOrder = 11;
    // Ringing the bell: the far arm raised and shaken, the near paw beckoning, a bob in time; looped from RingLoopFrom.
    private static Key R(float t, float hips, float body, float head, float near, float ring, float smile) =>
        new Key(t, hips, body, head, near, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, ring, smile);
    private static readonly Key[] RingKeys =
    {
        //    t     hips  body   head   near   ring smile
        R(0f,     0f,   0f,    0f,    0f,    0f,  0f),
        R(.30f,   2f,  -.03f,  .06f, -.30f,  1f,  1f),   // bell up, eyes smiling
        R(1.40f,  2f,  -.05f, -.05f, -.45f,  1f,  1f),   // calling one way...
        R(2.50f,  2f,  -.03f,  .06f, -.30f,  1f,  1f),   // ...and the other; = the .30 key
    };
    private const float RingLoopFrom = .30f;
    // Sweeping: the far arm (holding the broom) swung to and fro about the shoulder, the body leaning into each
    // stroke; dust at the end of each stroke. Looped from SweepLoopFrom (two strokes).
    private static Key W(float t, float hips, float body, float head, float near, float far, float dust = 0f) =>
        new Key(t, hips, body, head, near, far, 0f, 0f, 6f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, dust);
    private static readonly Key[] SweepKeys =
    {
        //    t     hips  body   head   near   far    dust
        W(0f,     0f,   0f,    0f,    0f,    0f),
        W(.35f,   6f,  .05f,  .10f,  .15f, -.50f),          // broom back, looking down at the floor
        W(.62f,   8f, -.04f,  .14f, -.10f,  .42f,  1f),     // swish!
        W(.70f,   7f, -.03f,  .13f, -.08f,  .38f, .85f),
        W(.97f,   6f,  .05f,  .10f,  .15f, -.50f),          // and back
        W(1.24f,  8f, -.04f,  .16f, -.10f,  .42f,  1f),     // swish!
        W(1.32f,  7f, -.03f,  .15f, -.08f,  .38f, .85f),
        W(1.59f,  6f,  .05f,  .10f,  .15f, -.50f),          // = the .35 key
    };
    private const float SweepLoopFrom = .35f;
    // The bristles' tip in the far arm sprite (arm space, rig px, y down); dust puff size and drift (rig px).
    private static readonly Vector2 BroomTipInArm = new Vector2(32f, 118f);
    private const float DustSize = 50f, DustDrift = 30f;
    // Far arm raised by RingRaise (rad, counter-clockwise lifts the paw) and shaken by RingShake RingRate times a
    // second; the near paw beckons; the body bobs twice a ring.
    private const float RingRaise = .8f, RingShake = .28f, RingRate = 4.5f, RingBeckon = .25f, RingBob = 2.5f;
    // The rock stands on the ground just in front of the feet (rig px, y down: + = nearer the camera), its top
    // where the pickaxe tip lands; chips fly up from there.
    private const float RockX = 115f, RockY = 10f, RockHeight = 52f, RockTop = -36f, StoneChipSize = 14f, StoneChipFlight = 34f;
    private static readonly Vector3 RockInFront = Vector3.back * .01f;   // over the body (same order), under the arm
    private const int RockOrder = 5;
    // Its head is further up the handle; the log lies further out and is bigger; the chips fly wider.
    private const float SledgeHeadReach = 117f, SledgeLogX = 97f, SledgeLogY = -29f, SledgeLogHeight = 36f, SledgeChipFlight = 52f;
    // The log lies on the ground in front of the feet, its top (log.png's pivot) where the blows land (rig px,
    // y down; set for the 야구 해달's arm); the hammer head is this far up the handle from the grip (the
    // sprite's pivot), and the hammer runs on along the forearm (bend: degrees forward from it).
    private const float LogX = 22f, LogY = -26f, LogHeight = 34f, HammerHeadReach = 46f, HammerBend = 0f;
    private const float ChipSize = 22f, ChipFlight = 30f;
    // Hammer over the near arm but under the head; the log in front of the body; chips on top.
    private const int HammerOrderFront = 7, HammerOrderRear = 1, LogOrderFront = 5, LogOrderRear = -1, ChipOrder = 9;

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
    // Playing on the seat (PlaysSeated): the near leg crossed over the far one - its hip moved under the far
    // thigh and its foot turned up past horizontal (rig px y up, degrees from pointing down) - with a sway, a strum
    // of the guitar arm and the tail keeping time; a note every PlayNoteEvery seconds from PlayNoteStart.
    private static readonly Vector2 CrossNearHip = new Vector2(4f, 40f);
    private const float CrossNearAngle = 120f, CrossFarAngle = 70f;
    private const float PlaySway = .6f, PlayBody = .03f, PlayHead = .07f, PlayHeadTilt = .08f, PlayStrum = .07f, PlayStrumRate = 2.2f;
    private const float PlayNoteEvery = .8f;
    private static readonly Vector2 PlayNoteStart = new Vector2(.55f, 1.45f);
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
        public float handReach;   // shoulder joint to the paw holding things, in the arm's own units
        public SpriteRenderer farArm, nearArm;   // two-handed: the far arm comes in front of the body to grab the handle
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
    private Sprite smileHead, noteSprite;
    // Arms holding a tool, by "<arm>-<tag>" (front view), and the front arms as drawn.
    private static readonly string[] ToolTags = { "mine", "photo", "ring", "sweep", "play" };
    private readonly Dictionary<string, Sprite> toolArms = new Dictionary<string, Sprite>();
    private Sprite nearArmSprite, farArmSprite;
    private readonly List<Loop> workLoops = new List<Loop>();
    // Sits down to play its guitar instead of dozing (fx-note.png in its folder - the 음악가).
    public bool PlaysSeated => noteSprite != null;
    private Sprite awakeHead, sleepHead, zSprite, rodSprite, hammerSprite;
    private bool sledge;   // the hammer is a sledgehammer (sledge.png): the heavy two-handed swing
    private SpriteRenderer rod, fishLine, fishFloat, ripple;
    private SpriteRenderer hammer, log;
    private Sprite[] rockStages;   // rock_0..2, rock_rubble: the 광부's rock
    private SpriteRenderer rock;
    private SpriteRenderer flashStar, flashGlow, sparkle;
    private readonly SpriteRenderer[] dustPuffs = new SpriteRenderer[2];   // the 청소부's sweeping dust   // the 사진가's flash and its "got it!" sparkle
    private readonly SpriteRenderer[] stoneChips = new SpriteRenderer[3];
    private readonly SpriteRenderer[] chips = new SpriteRenderer[2];
    private readonly List<SpriteRenderer> zs = new List<SpriteRenderer>();
    private readonly List<float> zAges = new List<float>();

    public bool FacingLeft => facingLeft;
    // Widest part (the hat brim, 270 rig px) in world units, e.g. for the ground shadow.
    public float Width => 2.7f * characterScale * Mathf.Abs(transform.lossyScale.x);
    public bool IsActing => action != null;
    public Loop CurrentLoop => action == KickKeys ? Loop.Kick : action == PuzzleKeys ? Loop.Puzzle : action == FishKeys ? Loop.Fish
        : action == BuildKeys || action == SledgeKeys ? Loop.Build : action == MineKeys ? Loop.Mine : action == PhotoKeys ? Loop.Photo : action == RingKeys ? Loop.Ring : action == SweepKeys ? Loop.Sweep : Loop.None;
    // Only an otter with a rod (rod.png in its folder - the sailor) can fish.
    public bool CanFish => rodSprite != null;
    // Only an otter with a hammer (hammer.png in its folder - the mechanic) builds.
    public bool CanBuild => hammerSprite != null;
    // Only an otter with a rock to break (rock_0.png in its folder - the 광부) mines.
    public bool CanMine => rockStages != null;
    // Only an otter with a flash (flash_star.png in its folder - the 사진가) takes photos.
    public bool CanPhoto => flashStar != null;
    // Only an otter with a smiling head (front-head-smile.png in its folder - the 행상인) rings its bell.
    public bool CanRing => smileHead != null;
    // Only an otter with a dust puff (sweep_dust.png in its folder - the 청소부) sweeps.
    public bool CanSweep => dustPuffs[0] != null;
    // This otter's own work (fishing, building, ...), Loop.None for an otter without one; with several, the first.
    public Loop WorkLoop => workLoops.Count > 0 ? workLoops[0] : Loop.None;
    // Every work it can do, and one of them at random (the 주인공 does them all).
    public IReadOnlyList<Loop> WorkLoops => workLoops;
    public Loop RandomWorkLoop => workLoops.Count > 0 ? workLoops[UnityEngine.Random.Range(0, workLoops.Count)] : Loop.None;
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
        if (loop == Loop.Fish && !CanFish) return;
        if (loop == Loop.Build && !CanBuild) return;
        if (loop == Loop.Mine && !CanMine) return;
        if (loop == Loop.Photo && !CanPhoto) return;
        if (loop == Loop.Ring && !CanRing) return;
        if (loop == Loop.Sweep && !CanSweep) return;
        walking = false;
        action = loop == Loop.Kick ? KickKeys : loop == Loop.Puzzle ? PuzzleKeys : loop == Loop.Fish ? FishKeys
            : loop == Loop.Mine ? MineKeys : loop == Loop.Photo ? PhotoKeys : loop == Loop.Ring ? RingKeys : loop == Loop.Sweep ? SweepKeys : sledge ? SledgeKeys : BuildKeys;
        actionTime = 0f;
        actionLoops = true;
        actionLoopFrom = loop == Loop.Kick ? 0f : loop == Loop.Puzzle ? PuzzleLoopFrom : loop == Loop.Fish ? FishLoopFrom
            : loop == Loop.Mine ? MineLoopFrom : loop == Loop.Photo ? PhotoLoopFrom : loop == Loop.Ring ? RingLoopFrom : loop == Loop.Sweep ? SweepLoopFrom : sledge ? SledgeLoopFrom : BuildLoopFrom;
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
        smileHead = Resources.Load<Sprite>(resourceFolder + "/front-head-smile");
        zSprite = Resources.Load<Sprite>(resourceFolder + "/fx-z");
        noteSprite = Resources.Load<Sprite>(resourceFolder + "/fx-note");
        rodSprite = Resources.Load<Sprite>(resourceFolder + "/rod");
        if (rodSprite != null) BuildRod();
        hammerSprite = Resources.Load<Sprite>(resourceFolder + "/hammer");
        if (hammerSprite == null) { hammerSprite = Resources.Load<Sprite>(resourceFolder + "/sledge"); sledge = hammerSprite != null; }
        if (hammerSprite != null) BuildHammer();
        var puff = Resources.Load<Sprite>(resourceFolder + "/sweep_dust");
        if (puff != null)
            for (int i = 0; i < dustPuffs.Length; i++)
            {
                dustPuffs[i] = NewChild("Dust", visual.transform).AddComponent<SpriteRenderer>();
                dustPuffs[i].sprite = puff;
                dustPuffs[i].sortingOrder = FlashOrder;
                dustPuffs[i].gameObject.SetActive(false);
            }
        var star = Resources.Load<Sprite>(resourceFolder + "/flash_star");
        if (star != null) BuildFlash(star);
        var rock0 = Resources.Load<Sprite>(resourceFolder + "/rock_0");
        if (rock0 != null) BuildRock(rock0);
        foreach (var tag in ToolTags)
            foreach (var arm in new[] { "nearArm", "farArm" })
            {
                var held = Resources.Load<Sprite>($"{resourceFolder}/front-{arm}-{tag}");
                if (held != null) toolArms[$"{arm}-{tag}"] = held;
            }
        nearArmSprite = front.nearArm != null ? front.nearArm.sprite : null;
        farArmSprite = front.farArm != null ? front.farArm.sprite : null;
        workLoops.Clear();
        if (CanFish) workLoops.Add(Loop.Fish);
        if (CanBuild) workLoops.Add(Loop.Build);
        if (CanMine) workLoops.Add(Loop.Mine);
        if (CanPhoto) workLoops.Add(Loop.Photo);
        if (CanRing) workLoops.Add(Loop.Ring);
        if (CanSweep) workLoops.Add(Loop.Sweep);
        ApplyPose();
    }

    private void OnDisable()
    {
        if (visual == null) return;
        if (Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
        visual = null;
        heldCrop = null;
        frontHead = null;
        rod = fishLine = fishFloat = ripple = null;
        hammer = log = null;
        rock = null;
        flashStar = flashGlow = sparkle = null;
        for (int i = 0; i < dustPuffs.Length; i++) dustPuffs[i] = null;
        rockStages = null;
        toolArms.Clear();
        workLoops.Clear();
        for (int i = 0; i < stoneChips.Length; i++) stoneChips[i] = null;
        chips[0] = chips[1] = null;
        foreach (var made in madeForFishing) if (made != null) { if (Application.isPlaying) Destroy(made); else DestroyImmediate(made); }
        madeForFishing.Clear();
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
            if (b.name == "nearArm") v.handReach = sprite.pivot.y / sprite.pixelsPerUnit * .82f * tuned.scaleY;
            if (b.name == "farArm") v.farArm = renderer;
            if (b.name == "nearArm") v.nearArm = renderer;
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

    // Rod (the rig's rod.png), line, float and ripple, hidden until fishing. The last three are drawn here.
    private void BuildRod()
    {
        rod = NewChild("Rod", visual.transform).AddComponent<SpriteRenderer>();
        rod.sprite = rodSprite;
        fishLine = NewChild("FishingLine", visual.transform).AddComponent<SpriteRenderer>();
        fishLine.sprite = Made(Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4));
        fishLine.color = LineColour;
        fishLine.sortingOrder = LineOrder;
        fishFloat = NewChild("Float", visual.transform).AddComponent<SpriteRenderer>();
        fishFloat.sprite = Made(DrawnSprite(32, 32, (x, y) =>
        {
            float d = new Vector2(x - 15.5f, y - 15.5f).magnitude;
            if (d > 15.5f) return Color.clear;
            if (d > 12.5f) return LineColour * .6f + Color.black * .4f;
            return y > 15.5f ? new Color32(0xe0, 0x4a, 0x3c, 0xff) : new Color32(0xfb, 0xf8, 0xf2, 0xff);
        }));
        fishFloat.sortingOrder = LineOrder;
        ripple = NewChild("Ripple", visual.transform).AddComponent<SpriteRenderer>();
        ripple.sprite = Made(DrawnSprite(96, 32, (x, y) =>
        {
            var e = new Vector2((x - 47.5f) / 46f, (y - 15.5f) / 14f).magnitude;
            return Mathf.Abs(e - .88f) < .1f ? new Color(.51f, .72f, .77f, 1f) : Color.clear;
        }));
        ripple.sortingOrder = RippleOrder;
        foreach (var r in new[] { rod, fishLine, fishFloat, ripple }) r.gameObject.SetActive(false);
    }

    // Hammer (in the paw), the log it works on and two chips, hidden until building.
    private void BuildHammer()
    {
        hammer = NewChild("Hammer", visual.transform).AddComponent<SpriteRenderer>();
        hammer.sprite = hammerSprite;
        log = NewChild("Log", visual.transform).AddComponent<SpriteRenderer>();
        log.sprite = Resources.Load<Sprite>(resourceFolder + "/log");
        var chip = Resources.Load<Sprite>(resourceFolder + "/chip");
        for (int i = 0; i < chips.Length; i++)
        {
            chips[i] = NewChild("Chip", visual.transform).AddComponent<SpriteRenderer>();
            chips[i].sprite = chip;
            chips[i].sortingOrder = ChipOrder;
        }
        hammer.gameObject.SetActive(false);
        log.gameObject.SetActive(false);
        foreach (var c in chips) c.gameObject.SetActive(false);
    }

    // The hammer in the near paw, its handle running on from the forearm; the log on the ground in front;
    // chips flying up off the log from where the head lands, fading as the hit falls off.
    private void PoseHammer(View v, Key a)
    {
        bool show = a.hammer > .5f;
        if (hammer.gameObject.activeSelf != show) hammer.gameObject.SetActive(show);
        if (log.gameObject.activeSelf != (show && log.sprite != null)) log.gameObject.SetActive(show && log.sprite != null);
        bool flying = show && a.hit > .02f && chips[0].sprite != null;
        foreach (var c in chips) if (c.gameObject.activeSelf != flying) c.gameObject.SetActive(flying);
        if (!show) return;
        var arm = v.joints["nearArm"];
        Vector3 hand = visual.transform.InverseTransformPoint(arm.TransformPoint(Vector3.down * v.handReach));
        Vector3 down = visual.transform.InverseTransformDirection(arm.TransformDirection(Vector3.down));
        float angle = Mathf.Atan2(down.y, down.x) * Mathf.Rad2Deg - 90f + HammerBend;
        // The sledge goes under the paws and behind the head (heaved up, it is over the shoulder): the near
        // arm's order, a hair further from the camera, like the rod.
        hammer.transform.localPosition = hand + (sledge ? Vector3.forward * RodBehindArm : Vector3.zero);
        hammer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        hammer.sortingOrder = facingRear ? HammerOrderRear : sledge ? RodOrderFront : HammerOrderFront;
        if (log.sprite != null)
        {
            log.transform.localPosition = new Vector3(sledge ? SledgeLogX : LogX, -(sledge ? SledgeLogY : LogY), 0f) / RigPixelsPerUnit;
            log.transform.localScale = Vector3.one * ((sledge ? SledgeLogHeight : LogHeight) / RigPixelsPerUnit / log.sprite.bounds.size.y);
            log.sortingOrder = facingRear ? LogOrderRear : LogOrderFront;
        }
        if (!flying) return;
        Vector3 head = hand + hammer.transform.localRotation * Vector3.up * ((sledge ? SledgeHeadReach : HammerHeadReach) / RigPixelsPerUnit);
        float t = 1f - a.hit, flight = sledge ? SledgeChipFlight : ChipFlight;
        for (int i = 0; i < chips.Length; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var offset = new Vector3(side * flight * t, flight * (1.6f * t - 1.2f * t * t), 0f) / RigPixelsPerUnit;
            chips[i].transform.localPosition = head + offset;
            chips[i].transform.localRotation = Quaternion.Euler(0f, 0f, side * 240f * t);
            chips[i].transform.localScale = Vector3.one * (ChipSize / RigPixelsPerUnit / chips[i].sprite.bounds.size.y);
            chips[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(a.hit * 1.5f));
        }
    }

    // The flash (a star on a soft glow) and the sparkle, hidden until taking photos.
    private void BuildFlash(Sprite star)
    {
        flashGlow = NewChild("FlashGlow", visual.transform).AddComponent<SpriteRenderer>();
        flashGlow.sprite = Resources.Load<Sprite>(resourceFolder + "/flash_glow");
        flashStar = NewChild("Flash", visual.transform).AddComponent<SpriteRenderer>();
        flashStar.sprite = star;
        sparkle = NewChild("Sparkle", visual.transform).AddComponent<SpriteRenderer>();
        sparkle.sprite = Resources.Load<Sprite>(resourceFolder + "/sparkle");
        foreach (var r in new[] { flashGlow, flashStar, sparkle }) { r.sortingOrder = FlashOrder; r.gameObject.SetActive(false); }
    }

    // Flash at the lens, sparkle over the head; the near arm over the head while the camera is up.
    private void PoseFlash(View v, Key a)
    {
        if (v.nearArm != null)
        {
            int head = v.bones.First(b => b.name == "head").layer;
            int order = a.up > .5f ? head + 1 : v.bones.First(b => b.name == "nearArm").layer;
            if (v.nearArm.sortingOrder != order) v.nearArm.sortingOrder = order;
        }
        bool flash = a.flash > .02f;
        foreach (var r in new[] { flashStar, flashGlow }) if (r != null && r.gameObject.activeSelf != flash && r.sprite != null) r.gameObject.SetActive(flash);
        if (flash)
        {
            var arm = v.joints["nearArm"];
            Vector3 lens = visual.transform.InverseTransformPoint(arm.TransformPoint(new Vector3(LensInArm.x, -LensInArm.y, 0f) / RigPixelsPerUnit));
            flashStar.transform.localPosition = lens;
            flashStar.transform.localScale = Vector3.one * (FlashSize * (.6f + .6f * a.flash) / RigPixelsPerUnit / flashStar.sprite.bounds.size.y);
            flashStar.transform.localRotation = Quaternion.Euler(0f, 0f, 20f * a.flash);
            flashStar.color = new Color(1f, 1f, 1f, a.flash);
            if (flashGlow.sprite != null)
            {
                flashGlow.transform.localPosition = lens;
                flashGlow.transform.localScale = Vector3.one * (FlashGlowSize * a.flash / RigPixelsPerUnit / flashGlow.sprite.bounds.size.y);
                flashGlow.color = new Color(1f, 1f, .9f, a.flash);
            }
        }
        bool pop = a.sparkle > .02f && sparkle.sprite != null;
        if (sparkle.gameObject.activeSelf != pop) sparkle.gameObject.SetActive(pop);
        if (pop)
        {
            var head = v.joints["head"];
            sparkle.transform.localPosition = visual.transform.InverseTransformPoint(head.TransformPoint(new Vector3(SparkleOverHead.x, -SparkleOverHead.y, 0f) / RigPixelsPerUnit));
            sparkle.transform.localScale = Vector3.one * (SparkleSize * (.5f + .5f * a.sparkle) / RigPixelsPerUnit / sparkle.sprite.bounds.size.y);
            sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, 30f * (1f - a.sparkle));
            sparkle.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a.sparkle * 1.5f));
        }
    }

    // Dust from the broom's tip at the end of a stroke: two puffs drifting up and apart, growing and fading.
    private void PoseDust(View v, Key a)
    {
        bool show = a.dust > .02f && v.joints.ContainsKey("farArm");
        foreach (var d in dustPuffs) if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
        if (!show) return;
        var arm = v.joints["farArm"];
        Vector3 tip = visual.transform.InverseTransformPoint(arm.TransformPoint(new Vector3(BroomTipInArm.x, -BroomTipInArm.y, 0f) / RigPixelsPerUnit));
        tip.y = Mathf.Max(tip.y, 0f);
        float t = 1f - a.dust;
        for (int i = 0; i < dustPuffs.Length; i++)
        {
            float side = i == 0 ? -.6f : 1f;
            var d = dustPuffs[i];
            d.transform.localPosition = tip + new Vector3(side * DustDrift * (.3f + t), DustDrift * (.2f + t) * (i == 0 ? .7f : 1f), 0f) / RigPixelsPerUnit;
            d.transform.localScale = Vector3.one * (DustSize * (.6f + .8f * t) * (i == 0 ? .8f : 1f) / RigPixelsPerUnit / d.sprite.bounds.size.y);
            d.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a.dust * 1.3f));
        }
    }

    // The rock (its crack stages) and the stone chips, hidden until mining.
    private void BuildRock(Sprite rock0)
    {
        rockStages = new[] { rock0, Resources.Load<Sprite>(resourceFolder + "/rock_1"), Resources.Load<Sprite>(resourceFolder + "/rock_2"),
            Resources.Load<Sprite>(resourceFolder + "/rock_rubble") };
        for (int i = 1; i < rockStages.Length; i++) if (rockStages[i] == null) rockStages[i] = rockStages[i - 1];
        rock = NewChild("Rock", visual.transform).AddComponent<SpriteRenderer>();
        rock.sortingOrder = RockOrder;
        var chip = Resources.Load<Sprite>(resourceFolder + "/stone_chip");
        for (int i = 0; i < stoneChips.Length; i++)
        {
            stoneChips[i] = NewChild("StoneChip", visual.transform).AddComponent<SpriteRenderer>();
            stoneChips[i].sprite = chip;
            stoneChips[i].sortingOrder = ChipOrder;
            stoneChips[i].gameObject.SetActive(false);
        }
        rock.gameObject.SetActive(false);
    }

    // The rock in front at its stage; chips fly up off its top (left, up, right) as the hit falls off.
    private void PoseRock(Key a)
    {
        bool show = a.stone >= 0f;
        if (rock.gameObject.activeSelf != show) rock.gameObject.SetActive(show);
        bool flying = show && a.hit > .02f && stoneChips[0].sprite != null;
        foreach (var c in stoneChips) if (c.gameObject.activeSelf != flying) c.gameObject.SetActive(flying);
        if (!show) return;
        rock.sprite = rockStages[Mathf.Clamp(Mathf.RoundToInt(a.stone), 0, rockStages.Length - 1)];
        // Sized by the whole rock, so the rubble keeps the rock's scale.
        rock.transform.localScale = Vector3.one * (RockHeight / RigPixelsPerUnit / rockStages[0].bounds.size.y);
        rock.transform.localPosition = new Vector3(RockX, -RockY, 0f) / RigPixelsPerUnit + RockInFront;
        if (!flying) return;
        var top = new Vector3(RockX - 6f, -RockTop, 0f) / RigPixelsPerUnit;
        float t = 1f - a.hit;
        for (int i = 0; i < stoneChips.Length; i++)
        {
            float side = i - 1f;
            var offset = new Vector3(side * StoneChipFlight * t, StoneChipFlight * (1.7f - .4f * Mathf.Abs(side)) * t - StoneChipFlight * 1.3f * t * t, 0f);
            stoneChips[i].transform.localPosition = top + offset / RigPixelsPerUnit;
            stoneChips[i].transform.localRotation = Quaternion.Euler(0f, 0f, (side + .5f) * 300f * t);
            stoneChips[i].transform.localScale = Vector3.one * (StoneChipSize / RigPixelsPerUnit / stoneChips[i].sprite.bounds.size.y);
            stoneChips[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(a.hit * 1.5f));
        }
    }

    private readonly List<UnityEngine.Object> madeForFishing = new List<UnityEngine.Object>();
    private T Made<T>(T thing) where T : UnityEngine.Object
    {
        thing.hideFlags = HideFlags.HideAndDontSave;
        madeForFishing.Add(thing);
        return thing;
    }

    private Sprite DrawnSprite(int w, int h, System.Func<int, int, Color> colour)
    {
        var texture = Made(new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = colour(x, y);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), h);
    }

    // Rod from the paw, line down to the water with the float on it, ripple around it; all in the rig's own space.
    private void PoseRod(View v, Key a)
    {
        bool hold = a.hold > .5f, wet = hold && a.line > .02f;
        if (rod.gameObject.activeSelf != hold) rod.gameObject.SetActive(hold);
        foreach (var r in new[] { fishLine, fishFloat, ripple })
            if (r.gameObject.activeSelf != wet) r.gameObject.SetActive(wet);
        if (!hold) return;
        var arm = v.joints["nearArm"];
        Vector3 hand = visual.transform.InverseTransformPoint(arm.TransformPoint(Vector3.down * v.handReach));
        float tilt = a.rod + a.line * .015f * Mathf.Sin(clock * 2f);
        var along = new Vector3(Mathf.Cos(tilt), -Mathf.Sin(tilt), 0f);
        float length = RodLength / RigPixelsPerUnit, grip = RodGrip / RigPixelsPerUnit;
        rod.transform.localPosition = hand - along * grip + Vector3.forward * RodBehindArm;
        rod.transform.localRotation = Quaternion.Euler(0f, 0f, -tilt * Mathf.Rad2Deg);
        rod.transform.localScale = new Vector3(length / rodSprite.bounds.size.x, RodThickness / RigPixelsPerUnit / rodSprite.bounds.size.y, 1f);
        rod.sortingOrder = facingRear ? RodOrderRear : RodOrderFront;
        if (!wet) return;
        Vector3 tip = hand + along * (length - grip);
        float bob = (1f - a.bite) * .02f * Mathf.Sin(clock * 2.4f) - a.bite * .07f;
        var water = new Vector3(tip.x, WaterY / RigPixelsPerUnit, 0f);
        Vector3 end = Vector3.Lerp(tip, water + Vector3.up * bob, a.line);
        Vector3 span = end - tip;
        fishLine.transform.localPosition = (tip + end) / 2f;
        fishLine.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
        fishLine.transform.localScale = new Vector3(span.magnitude, LineWidth / RigPixelsPerUnit, 1f);
        fishFloat.transform.localPosition = end;
        fishFloat.transform.localScale = Vector3.one * (FloatSize / RigPixelsPerUnit);
        ripple.transform.localPosition = water;
        ripple.transform.localScale = Vector3.one * (RippleWidth / RigPixelsPerUnit / 3f * (1f + a.bite * .7f + .08f * Mathf.Sin(clock * 2.4f)));
        ripple.color = new Color(1f, 1f, 1f, a.line);
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
        // Building faces the camera (the work in front of it), like harvesting.
        if (action == BuildKeys || action == SledgeKeys || action == MineKeys || action == PhotoKeys || action == RingKeys || action == SweepKeys) facingRear = false;
        front.root.SetActive(!facingRear);
        rear.root.SetActive(facingRear);
        var v = facingRear ? rear : front;
        float w = walkBlend;
        float p = phase * Mathf.PI * 2f;
        var a = action != null ? Sample(action, actionTime) : default;
        if (heldCrop != null) PoseHeldCrop(a);
        float sb = sitBlend * sitBlend * (3f - 2f * sitBlend);
        float nod = Mathf.Repeat(nodTime / NodSeconds, 1f);
        float play = PlaysSeated ? sb : 0f;
        float droop = play > 0f ? 0f : sb * Droop(nod);
        if (toolArms.Count > 0) HoldTool(play > .5f);
        if (frontHead != null)
            frontHead.sprite = sitting && sleepHead != null && sb > .5f && (PlaysSeated || nod < BlinkFrom || nod >= BlinkTo) ? sleepHead
                : smileHead != null && a.smile > .5f ? smileHead : awakeHead;
        foreach (var b in v.bones)
        {
            var joint = v.joints[b.name];
            var tuned = v.TuningOf(b.name);
            if (IsLeg(b)) { PoseLeg(joint, b.name == "nearLeg", w, a.spread, sb, tuned, b.name == "nearLeg" ? a.kick : 0f, play); continue; }
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
                case "nearArm": angle += a.near + a.fiddle * FiddleNear * Mathf.Sin(clock * FiddleTapsPerSecond * Mathf.PI * 2f); x += a.nearX; y += a.nearY;
                    angle += a.ring * RingBeckon * Mathf.Sin(clock * RingRate * .5f * Mathf.PI * 2f); break;
                case "farArm": angle += a.far + a.fiddle * FiddleFar * Mathf.Sin(clock * FiddleTapsPerSecond * Mathf.PI * 2f + 1.3f);
                    angle += a.ring * (-RingRaise + RingShake * Mathf.Sin(clock * RingRate * Mathf.PI * 2f)); break;
                case "tail": angle += a.tail; break;
                case "backpack": angle += a.pack; break;
            }
            if (b.name == "head") angle += a.fiddle * FiddleHead * Mathf.Sin(clock * 1.3f);
            if (b.name == "hips") y -= a.ring * RingBob * Mathf.Abs(Mathf.Sin(clock * RingRate * .5f * Mathf.PI * 2f));
            // Sitting and dozing: settled on the seat, hands in the lap, slow deep breaths, nodding off.
            switch (b.name)
            {
                case "hips": y += SitHipsDrop * sb; break;
                case "body": y += sb * Mathf.Sin(clock * 1.7f) * 1.4f; angle += .035f * droop; break;
                case "head": y += 10f * droop; angle += .28f * droop; break;
                case "nearArm": angle += -.4f * sb * (1f - play); break;
                case "farArm": angle += .18f * sb * (1f - play); break;
                case "tail": angle += .22f * sb; break;
            }
            // Playing: swaying to the tune, strumming (the guitar arm), the tail keeping time.
            if (play > 0f)
            {
                float beat = clock * PlaySway * Mathf.PI * 2f;
                switch (b.name)
                {
                    case "body": angle += play * PlayBody * Mathf.Sin(beat); break;
                    case "head": angle += play * (PlayHeadTilt + PlayHead * Mathf.Sin(beat - .6f)); break;
                    case "nearArm": angle += play * PlayStrum * Mathf.Sin(clock * PlayStrumRate * Mathf.PI * 2f); break;
                    case "tail": angle += play * .1f * Mathf.Sin(beat * 2f); break;
                }
            }
            x += tuned.x;
            y += tuned.y;
            angle += tuned.rotation * Mathf.Deg2Rad;
            if (b.name == "head") angle += a.line * .025f * Mathf.Sin(clock * 1.5f);
            joint.localPosition = new Vector3(x, -y, 0f) / RigPixelsPerUnit;
            joint.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
        }
        if (a.grip > 0f) GripWithFarPaw(v, a.grip);
        if (v.farArm != null && v.nearArm != null)
        {
            // Holding the handle the far paw is in front of the body: just under the near arm.
            bool front = a.grip > .5f;
            int order = front ? v.nearArm.sortingOrder : v.bones.First(b => b.name == "farArm").layer;
            if (v.farArm.sortingOrder != order) v.farArm.sortingOrder = order;
            var at = v.farArm.transform.localPosition;
            float z = front ? RodBehindArm : 0f;
            if (at.z != z) v.farArm.transform.localPosition = new Vector3(at.x, at.y, z);
        }
        if (rod != null) PoseRod(v, a);
        if (hammer != null) PoseHammer(v, a);
        if (rock != null) PoseRock(action != null ? a : new Key(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f));
        if (flashStar != null) PoseFlash(v, action != null ? a : new Key(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f));
        if (dustPuffs[0] != null) PoseDust(v, action != null ? a : new Key(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f));
    }

    // The arm holding this action's tool (front-<arm>-<tag>.png) in place of the arm as drawn, while it runs.
    private void HoldTool(bool playing)
    {
        var loop = CurrentLoop;
        string tag = loop == Loop.Mine ? "mine" : loop == Loop.Photo ? "photo" : loop == Loop.Ring ? "ring"
            : loop == Loop.Sweep ? "sweep" : playing ? "play" : null;
        ShowArm(front.nearArm, nearArmSprite, tag == null ? null : "nearArm-" + tag);
        ShowArm(front.farArm, farArmSprite, tag == null ? null : "farArm-" + tag);
    }

    private void ShowArm(SpriteRenderer arm, Sprite drawn, string key)
    {
        if (arm == null) return;
        var sprite = key != null && toolArms.TryGetValue(key, out var held) ? held : drawn;
        if (arm.sprite != sprite) arm.sprite = sprite;
    }

    // Two-handed: turn the far arm (about its shoulder) to point at the handle below the near paw.
    private static void GripWithFarPaw(View v, float grip)
    {
        if (!v.joints.TryGetValue("farArm", out var far) || !v.joints.TryGetValue("nearArm", out var near)) return;
        Vector3 handle = far.parent.InverseTransformPoint(near.TransformPoint(new Vector3(GripOnHandle.x, -GripOnHandle.y, 0f) / RigPixelsPerUnit));
        Vector3 dir = handle - far.localPosition;
        // The arm hangs along -y; turned by z (counter-clockwise) it points along (sin z, -cos z).
        float aim = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
        float z = Mathf.LerpAngle(far.localEulerAngles.z, aim, grip);
        far.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    // Rigid leg with its sole on the ground: slides back during stance, lifts and swings forward otherwise.
    private void PoseLeg(Transform joint, bool near, float w, float spread, float sit, PartTuning tuned, float kick, float cross = 0f)
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
        float degrees = near ? Mathf.Lerp(SitNearAngle, CrossNearAngle, cross) : Mathf.Lerp(SitFarAngle, CrossFarAngle, cross);
        float length = SitLegLength * SitLegForeshorten;
        var toFoot = new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), -Mathf.Cos(degrees * Mathf.Deg2Rad));
        var seated = (near ? Vector2.Lerp(SitNearHip, CrossNearHip, cross) : SitFarHip) + toFoot * length;

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
        if (sitting && sitBlend >= 1f && zSprite != null && (PlaysSeated || (nod > .1f && nod < NodSinkEnd)))
        {
            zTimer -= dt;
            if (zTimer <= 0f)
            {
                zTimer = PlaysSeated ? PlayNoteEvery : ZEverySeconds;
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
            var pos = (PlaysSeated ? PlayNoteStart : ZStart) + new Vector2(ZRise.x * t + .12f * Mathf.Sin(t * 7f), ZRise.y * t);
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
            z.sprite = noteSprite != null ? noteSprite : zSprite;
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
            Mathf.Lerp(k0.kick, k1.kick, u), Mathf.Lerp(k0.fiddle, k1.fiddle, u), Mathf.Lerp(k0.rod, k1.rod, u),
            Mathf.Lerp(k0.hold, k1.hold, u), Mathf.Lerp(k0.line, k1.line, u), Mathf.Lerp(k0.bite, k1.bite, u),
            Mathf.Lerp(k0.hammer, k1.hammer, u), Mathf.Lerp(k0.hit, k1.hit, u), u >= 1f ? k1.stone : k0.stone,
            Mathf.Lerp(k0.nearX, k1.nearX, u), Mathf.Lerp(k0.nearY, k1.nearY, u), Mathf.Lerp(k0.grip, k1.grip, u),
            Mathf.Lerp(k0.flash, k1.flash, u), u >= 1f ? k1.up : k0.up, Mathf.Lerp(k0.sparkle, k1.sparkle, u),
            Mathf.Lerp(k0.ring, k1.ring, u), Mathf.Lerp(k0.smile, k1.smile, u), Mathf.Lerp(k0.dust, k1.dust, u));
    }

    private static bool IsLeg(Bone b) => b.name.EndsWith("Leg", StringComparison.Ordinal);
}
