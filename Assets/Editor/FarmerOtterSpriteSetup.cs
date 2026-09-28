using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-off tool: slices the FarmerOtter sprite sheets, builds AnimationClips,
// wires an AnimatorController (Idle / 3-way Walk / Harvest / idle actions),
// and creates or updates the prefab.
// Run via: OtterKingdom > Tools > Setup Farmer Otter Animations
public static class FarmerOtterSpriteSetup
{
    private const string SpriteDir = "Assets/Sprites/Characters/FarmerOtter";
    private const string SheetPath = SpriteDir + "/FarmerOtter_Sheet.png";
    private const string AnimDir = "Assets/Animations/FarmerOtter";
    private const string PrefabPath = "Assets/Prefabs/FarmerOtter.prefab";

    // Every sheet was re-packed offline (see 농부해달_애니메이션_작업기록.md)
    // into the same uniform grid: 8 columns of CellW x CellH, character scaled
    // to one shared size, feet on the same baseline and hat centred on each
    // cell's centre column. The raw AI sheets had frames spilling across cell
    // edges and a different character scale per sheet, which is what made the
    // otter change size between clips and show slivers of neighbouring frames.
    // Because each cell now has transparent padding baked in, slicing on the
    // exact grid is safe — no inset needed.
    private const int Cols = 8;
    private const int CellW = 416;
    private const int CellH = 240;

    // Keeps the otter at the same on-screen size as the original Walk sheet
    // (~171px tall at PPU 100); the re-packed frames are ~205px tall.
    private const float PixelsPerUnit = 120f;

    private static readonly (int row, string name)[] RowDefs =
    {
        (0, "Walk"),       // side view, facing right (left = flipX)
        (1, "Walk_Left"),  // side view, facing left — unused, flipX covers it
        (2, "Walk_Down"),  // front view, walking toward camera
        (3, "Walk_Up"),    // back view, walking away from camera
        (4, "Harvest"),
    };

    // The main sheet has no standing-still row anymore, so Idle is a single
    // neutral side-view pose (Harvest frame 0). Side view on purpose: the
    // idle actions below are side view too, so Idle -> action doesn't snap
    // the otter to face the camera and back.
    private const string IdleSourceRow = "Harvest";
    private const int IdleSourceCol = 0;

    // Random idle flavor actions played while wandering (see
    // FarmerOtterController.randomActionTriggers). Each sheet is 8 cols x
    // 4 rows: row 0 = side facing right, row 1 = side facing left, row 2 =
    // front, row 3 = back. Only row 0 is wired up (left = flipX); the other
    // rows are still sliced so they exist as sprites if needed later.
    private const int IdleActionRows = 4;
    private const int IdleActionUsedRow = 0;

    private static readonly (string path, string trigger)[] IdleActionSheets =
    {
        ($"{SpriteDir}/FarmerOtter_IdleAction_Net.png", "Net"),
        ($"{SpriteDir}/FarmerOtter_IdleAction_Stretch.png", "Stretch"),
        ($"{SpriteDir}/FarmerOtter_IdleAction_Eat.png", "Eat"),
        ($"{SpriteDir}/FarmerOtter_IdleAction_Squat.png", "Squat"),
    };

    // Must match FarmerOtterController.WalkDir.
    private const int WalkDirSide = 0;
    private const int WalkDirDown = 1;
    private const int WalkDirUp = 2;

    // Per-state playback speed multipliers, driven from the Inspector by
    // FarmerOtterController (names must match its *SpeedHash fields).
    private const string WalkSpeedParam = "WalkAnimSpeed";
    private const string HarvestSpeedParam = "HarvestAnimSpeed";
    private const string IdleActionSpeedParam = "IdleActionAnimSpeed";

    [MenuItem("OtterKingdom/Tools/Setup Farmer Otter Animations")]
    public static void Run()
    {
        // Safe to re-run: wipe previously generated clips/controller first so
        // CreateAsset doesn't fail against paths that already exist. The
        // prefab is updated in place instead (see BuildOrUpdatePrefab).
        if (AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.DeleteAsset(AnimDir);
        AssetDatabase.Refresh();
        Directory.CreateDirectory(AnimDir);

        var mainRows = SliceSheet(SheetPath, RowDefs.Select(r => r.name).ToArray());

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var rowDef in RowDefs)
        {
            clips[rowDef.name] = BuildClip(rowDef.name, mainRows[rowDef.name]);
        }
        var idleSprite = mainRows[IdleSourceRow][IdleSourceCol];
        clips["Idle"] = BuildClip("Idle", new List<Sprite> { idleSprite });

        var idleActionClips = new Dictionary<string, AnimationClip>();
        foreach (var sheet in IdleActionSheets)
        {
            var rowNames = Enumerable.Range(0, IdleActionRows).Select(r => r.ToString()).ToArray();
            var rows = SliceSheet(sheet.path, rowNames);
            idleActionClips[sheet.trigger] = BuildClip($"IdleAction_{sheet.trigger}", rows[IdleActionUsedRow.ToString()]);
        }

        var controller = BuildController(clips, idleActionClips);
        BuildOrUpdatePrefab(controller, idleSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FarmerOtterSpriteSetup] Done.");
    }

    // Slices a uniform CellW x CellH grid (one row per entry in rowNames,
    // top to bottom) and returns each row's frames in column order. Sprites
    // are named "{sheet}_{rowName}_{col}".
    private static Dictionary<string, List<Sprite>> SliceSheet(string path, string[] rowNames)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // Re-packed sheets are 3328px wide; the 2048 default would downscale
        // them and blur every frame.
        importer.maxTextureSize = 4096;

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        if (texW != CellW * Cols || texH != CellH * rowNames.Length)
        {
            Debug.LogError($"[FarmerOtterSpriteSetup] {path} is {texW}x{texH}, expected " +
                           $"{CellW * Cols}x{CellH * rowNames.Length}. Re-pack the sheet first.");
        }

        string baseName = Path.GetFileNameWithoutExtension(path);
        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < rowNames.Length; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{baseName}_{rowNames[row]}_{col}",
                    rect = new Rect(col * CellW, texH - (row + 1) * CellH, CellW, CellH),
                    // Centre pivot: the hat sits on the centre column, so
                    // flipX mirrors the otter in place without a sideways jump.
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }
#pragma warning disable CS0618 // spritesheet is deprecated but still the simplest API here
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        var result = new Dictionary<string, List<Sprite>>();
        foreach (var rowName in rowNames)
        {
            var list = new List<Sprite>();
            for (int col = 0; col < Cols; col++)
            {
                string name = $"{baseName}_{rowName}_{col}";
                list.Add(all.First(s => s.name == name));
            }
            result[rowName] = list;
        }
        return result;
    }

    private static AnimationClip BuildClip(string name, List<Sprite> frames)
    {
        var clip = new AnimationClip { frameRate = 10f };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        string path = $"{AnimDir}/FarmerOtter_{name}.anim";
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController BuildController(
        Dictionary<string, AnimationClip> clips,
        Dictionary<string, AnimationClip> idleActionClips)
    {
        string path = $"{AnimDir}/FarmerOtter.controller";
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;

        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("WalkDir", AnimatorControllerParameterType.Int);
        controller.AddParameter("Harvest", AnimatorControllerParameterType.Trigger);
        AddSpeedParameter(controller, WalkSpeedParam);
        AddSpeedParameter(controller, HarvestSpeedParam);
        AddSpeedParameter(controller, IdleActionSpeedParam);

        var idleState = sm.AddState("Idle");
        idleState.motion = clips["Idle"];
        sm.defaultState = idleState;

        // One walk state per direction; WalkDir picks which one plays while
        // IsMoving is true, and switching WalkDir mid-walk (vertical leg ->
        // horizontal leg of FarmerOtterController.MoveTo) hops between them.
        var walkStates = new (int dir, AnimatorState state)[]
        {
            (WalkDirSide, sm.AddState("Walk")),
            (WalkDirDown, sm.AddState("Walk_Down")),
            (WalkDirUp, sm.AddState("Walk_Up")),
        };
        walkStates[0].state.motion = clips["Walk"];
        walkStates[1].state.motion = clips["Walk_Down"];
        walkStates[2].state.motion = clips["Walk_Up"];

        foreach (var (dir, state) in walkStates)
        {
            UseSpeedParameter(state, WalkSpeedParam);

            var idleToWalk = idleState.AddTransition(state);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0f;
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.AddCondition(AnimatorConditionMode.Equals, dir, "WalkDir");

            var walkToIdle = state.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0f;
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

            foreach (var (otherDir, other) in walkStates)
            {
                if (other == state) continue;
                var walkToWalk = state.AddTransition(other);
                walkToWalk.hasExitTime = false;
                walkToWalk.duration = 0f;
                walkToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
                walkToWalk.AddCondition(AnimatorConditionMode.Equals, otherDir, "WalkDir");
            }
        }

        var harvestState = sm.AddState("Harvest");
        harvestState.motion = clips["Harvest"];
        UseSpeedParameter(harvestState, HarvestSpeedParam);

        var anyToHarvest = sm.AddAnyStateTransition(harvestState);
        anyToHarvest.hasExitTime = false;
        anyToHarvest.duration = 0f;
        anyToHarvest.AddCondition(AnimatorConditionMode.If, 0, "Harvest");

        var harvestToIdle = harvestState.AddTransition(idleState);
        harvestToIdle.hasExitTime = true;
        harvestToIdle.exitTime = 1f;
        harvestToIdle.duration = 0f;

        // Random idle flavor actions — same Any State -> trigger -> clip ->
        // exit time 1 -> Idle pattern as Harvest, so
        // FarmerOtterController.PlayRandomActionOrWait's generic "wait until
        // back in Idle" logic works unchanged for these too.
        foreach (var kvp in idleActionClips)
        {
            string trigger = kvp.Key;

            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var state = sm.AddState(trigger);
            state.motion = kvp.Value;
            UseSpeedParameter(state, IdleActionSpeedParam);

            var anyToState = sm.AddAnyStateTransition(state);
            anyToState.hasExitTime = false;
            anyToState.duration = 0f;
            anyToState.AddCondition(AnimatorConditionMode.If, 0, trigger);

            var stateToIdle = state.AddTransition(idleState);
            stateToIdle.hasExitTime = true;
            stateToIdle.exitTime = 1f;
            stateToIdle.duration = 0f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddSpeedParameter(AnimatorController controller, string name)
    {
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
    }

    private static void UseSpeedParameter(AnimatorState state, string param)
    {
        state.speedParameter = param;
        state.speedParameterActive = true;
    }

    // Updates the existing prefab in place when there is one: recreating it
    // would give its components new fileIDs and break the Farm scene's
    // instance overrides (position/scale, sorting order, the added
    // FarmerOtterController and its waypoint wiring).
    private static void BuildOrUpdatePrefab(AnimatorController controller, Sprite defaultSprite)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            root.GetComponent<SpriteRenderer>().sprite = defaultSprite;
            root.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var go = new GameObject("FarmerOtter");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
    }
}
