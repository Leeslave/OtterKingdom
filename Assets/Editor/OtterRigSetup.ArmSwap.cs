using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Config B: front/back-only rig from ArtSource/Otter/FarmerOtter_RigPartsSheetB.png,
// repacked into FarmerOtter_RigPartsB.png by Tools/SpriteRepack/repack_otter_rig_parts_b.py.
// The sheet draws 3 poses per arm (hand swung out / hanging / swung in); the walk
// swaps between them on top of the bone animation instead of rotating one arm sprite.
// There is no side view, so walking left/right shows the front view.
// Run via: OtterKingdom > Tools > Setup Otter Rig Test B (Arm Swap)
public static partial class OtterRigSetup
{
    // Rects and arm pivots printed by repack_otter_rig_parts_b.py — re-paste both whenever it runs.
    private static readonly Dictionary<string, RectInt> RectsB = new Dictionary<string, RectInt>
    {
        { "Head_Front", new RectInt(4, 561, 408, 271) },
        { "Body_Front", new RectInt(416, 621, 256, 211) },
        { "Leg_Front_L", new RectInt(676, 676, 107, 156) },
        { "Leg_Front_R", new RectInt(787, 676, 106, 156) },
        { "Tail_Front", new RectInt(897, 737, 87, 95) },
        { "Arm_Front_R_In", new RectInt(4, 435, 131, 122) },
        { "Arm_Front_R_Mid", new RectInt(139, 420, 94, 137) },
        { "Arm_Front_R_Out", new RectInt(237, 421, 102, 136) },
        { "Arm_Front_L_Out", new RectInt(343, 431, 131, 126) },
        { "Arm_Front_L_Mid", new RectInt(478, 421, 98, 136) },
        { "Arm_Front_L_In", new RectInt(580, 424, 111, 133) },
        { "Head_Back", new RectInt(4, 148, 398, 268) },
        { "Body_Back", new RectInt(406, 213, 255, 203) },
        { "Leg_Back_L", new RectInt(665, 263, 108, 153) },
        { "Leg_Back_R", new RectInt(777, 263, 107, 153) },
        { "Tail_Back", new RectInt(888, 298, 93, 118) },
        { "Arm_Back_R_In", new RectInt(4, 11, 110, 133) },
        { "Arm_Back_R_Mid", new RectInt(118, 7, 106, 137) },
        { "Arm_Back_R_Out", new RectInt(228, 10, 123, 134) },
        { "Arm_Back_L_Out", new RectInt(355, 10, 112, 134) },
        { "Arm_Back_L_Mid", new RectInt(471, 6, 107, 138) },
        { "Arm_Back_L_In", new RectInt(582, 6, 131, 138) },
    };

    // Shoulder point of each arm pose, so swapping poses keeps the shoulder on its bone.
    private static readonly Dictionary<string, Vector2> ArmPivotsB = new Dictionary<string, Vector2>
    {
        { "Arm_Front_R_In", new Vector2(0.756f, 0.846f) },
        { "Arm_Front_R_Mid", new Vector2(0.463f, 0.851f) },
        { "Arm_Front_R_Out", new Vector2(0.334f, 0.825f) },
        { "Arm_Front_L_Out", new Vector2(0.763f, 0.838f) },
        { "Arm_Front_L_Mid", new Vector2(0.578f, 0.850f) },
        { "Arm_Front_L_In", new Vector2(0.282f, 0.822f) },
        { "Arm_Back_R_In", new Vector2(0.717f, 0.817f) },
        { "Arm_Back_R_Mid", new Vector2(0.558f, 0.847f) },
        { "Arm_Back_R_Out", new Vector2(0.260f, 0.851f) },
        { "Arm_Back_L_Out", new Vector2(0.703f, 0.827f) },
        { "Arm_Back_L_Mid", new Vector2(0.468f, 0.849f) },
        { "Arm_Back_L_In", new Vector2(0.224f, 0.837f) },
    };

    private static Vector2 PivotOfB(string sprite)
    {
        if (ArmPivotsB.TryGetValue(sprite, out var pivot)) return pivot;
        if (sprite.StartsWith("Head_")) return new Vector2(0.5f, 0.08f);
        if (sprite.StartsWith("Body_")) return new Vector2(0.5f, 0.4f);
        if (sprite.StartsWith("Leg_")) return new Vector2(0.5f, 0.85f);
        if (sprite == "Tail_Front") return new Vector2(0.3f, 0.75f); // peeks out behind the body, lower right
        return new Vector2(0.5f, 0.85f);                              // Tail_Back hangs from its top
    }

    private static float TailAngleB(string sprite) => sprite == "Tail_Front" ? -60f : -90f;

    // Layout in source pixels: centre line x=230, feet y=480. Arms are placed by
    // their shoulder pivot (AtPivot), everything else by its centre.
    private static readonly (string name, int[] dirs, Part[] parts)[] ViewsB =
    {
        ("Down", new[] { DirDown, DirRight, DirLeft }, new[]
        {
            new Part("Hips", "Body_Front", null, 230, 325, 20),
            new Part("Head", "Head_Front", "Hips", 230, 128, 40),
            Part.AtPivot("Arm_L", "Arm_Front_L_Mid", "Hips", 130, 262, 30),
            Part.AtPivot("Arm_R", "Arm_Front_R_Mid", "Hips", 330, 262, 30),
            new Part("Leg_L", "Leg_Front_L", "Hips", 197, 402, 10),
            new Part("Leg_R", "Leg_Front_R", "Hips", 263, 402, 10),
            new Part("Tail", "Tail_Front", "Hips", 322, 398, 0),
        }),
        ("Up", new[] { DirUp }, new[]
        {
            new Part("Hips", "Body_Back", null, 230, 325, 20),
            new Part("Head", "Head_Back", "Hips", 230, 135, 40),
            Part.AtPivot("Arm_L", "Arm_Back_L_Mid", "Hips", 112, 262, 10),
            Part.AtPivot("Arm_R", "Arm_Back_R_Mid", "Hips", 348, 262, 10),
            new Part("Leg_L", "Leg_Back_L", "Hips", 197, 402, 0),
            new Part("Leg_R", "Leg_Back_R", "Hips", 263, 402, 0),
            new Part("Tail", "Tail_Back", "Hips", 230, 405, 30),
        }),
    };

    private static RigConfig ConfigB() => new RigConfig
    {
        MenuTitle = "Setup Otter Rig Test B (Arm Swap)",
        LogTag = "[OtterRigSetup B]",
        RootName = "FarmerOtterRigB",
        ClipPrefix = "OtterRigB_",
        TexturePath = "Assets/Art/Otter/RigB/FarmerOtter_RigPartsB.png",
        AnimDir = "Assets/Animations/OtterRigB",
        ControllerPath = "Assets/Animations/OtterRigB/FarmerOtterRigB.controller",
        PrefabPath = PrefabDir + "/FarmerOtterRigB.prefab",
        ScenePath = "Assets/Scenes/RiggingTestScene_ArmSwap.unity",
        // ~487px tall at source resolution -> ~1.75 units, same size as rig A.
        PixelsPerUnit = 280f,
        PartRects = RectsB,
        PivotOf = PivotOfB,
        TailAngle = TailAngleB,
        LayoutCenterX = 230f,
        LayoutFeetY = 480f,
        Views = ViewsB,
        // The pose swaps carry most of the swing; the bones only add a little sway.
        FrontArmSwing = 5f,
        ExtraCurves = ArmSwapCurves,
    };

    [MenuItem("OtterKingdom/Tools/Setup Otter Rig Test B (Arm Swap)")]
    public static void RunArmSwap() => Build(ConfigB());

    // Walk: 4 steps per cycle, both hands pointing the same way at each step (the left
    // arm swings out while the right swings in), so the arms read as swinging with the
    // body's sway. Step 1 lines up with the left foot's lift. Idle: both arms hang.
    private static void ArmSwapCurves(AnimationClip clip, ViewRig rig, bool walk, float period)
    {
        string facing = rig.name == "Up" ? "Back" : "Front";
        string[] left = walk ? new[] { "Mid", "Out", "Mid", "In" } : new[] { "Mid" };
        string[] right = walk ? new[] { "Mid", "In", "Mid", "Out" } : new[] { "Mid" };
        SwapSprites(clip, rig, "Arm_L", left.Select(p => $"Arm_{facing}_L_{p}").ToArray(), period);
        SwapSprites(clip, rig, "Arm_R", right.Select(p => $"Arm_{facing}_R_{p}").ToArray(), period);
    }

    // Steps through the sprites evenly over one loop (object curves hold each key).
    private static void SwapSprites(AnimationClip clip, ViewRig rig, string bone, string[] sprites, float period)
    {
        Transform renderer = rig.go.transform.Find($"Sprites/{bone}_Sprite");
        string path = AnimationUtility.CalculateTransformPath(renderer, rig.go.transform.parent);
        var binding = EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite");

        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i <= sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = period * i / sprites.Length,
                value = s_Sprites[sprites[i % sprites.Length]],
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
    }
}
