using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-off tool: slices the FarmerOtter sprite sheet, builds AnimationClips per
// row, wires an AnimatorController (Idle/Walk/Harvest), and drops a prefab.
// Run via: Unity -batchmode -executeMethod FarmerOtterSpriteSetup.Run -quit
public static class FarmerOtterSpriteSetup
{
    private const string SheetPath = "Assets/Sprites/Characters/FarmerOtter/FarmerOtter_Sheet.png";
    private const string AnimDir = "Assets/Animations/FarmerOtter";
    private const string PrefabDir = "Assets/Prefabs";
    private const int Cols = 8;

    // Frames sit edge-to-edge in the sheet with zero gap between them, so
    // slicing at the exact naive column boundary lets bilinear filtering
    // sample a sliver of the neighboring frame at render time (shows up as a
    // faint "ghost" of the next pose beside the character, worst on Harvest
    // since its pose reaches closest to the frame edge). Insetting each
    // sliced rect moves the sampled texture away from that boundary.
    private const float ColumnInsetPx = 2f;
    private const float RowInsetPx = 1f;

    private static readonly (int row, string name)[] RowDefs =
    {
        (0, "Walk"),
        (1, "Walk_Alt"),
        (2, "Idle"),
        (3, "Idle_Back"),
        (4, "Harvest"),
    };

    // Random idle flavor actions played while wandering (see
    // FarmerOtterController.randomActionTriggers). Each sheet is its own
    // 8-col x 4-row grid: row 0 = side-facing (same framing as Walk/Harvest),
    // row 1 = side alt, row 2 = front close-up bust, row 3 = back view. Only
    // row 0 is wired up for now, same as Walk_Alt/Idle_Back being left unused
    // on the main sheet — the others are available if a future scene needs
    // front/back facing.
    private const string IdleActionDir = "Assets/Sprites/Characters/FarmerOtter";
    private const int IdleActionCols = 8;
    private const int IdleActionRows = 4;
    private const int IdleActionUsedRow = 0;

    private static readonly (string path, string trigger)[] IdleActionSheets =
    {
        ($"{IdleActionDir}/FarmerOtter_IdleAction_Net.png", "Net"),
        ($"{IdleActionDir}/FarmerOtter_IdleAction_Stretch.png", "Stretch"),
        ($"{IdleActionDir}/FarmerOtter_IdleAction_Eat.png", "Eat"),
    };

    [MenuItem("OtterKingdom/Tools/Setup Farmer Otter Animations")]
    public static void Run()
    {
        // Safe to re-run: wipe previously generated clips/controller/prefab
        // first so CreateAsset doesn't fail against paths that already exist.
        if (AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.DeleteAsset(AnimDir);
        string prefabPath = $"{PrefabDir}/FarmerOtter.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);
        AssetDatabase.Refresh();

        SliceSheet();

        var spritesByRow = LoadSlicedSprites();
        Directory.CreateDirectory(AnimDir);

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var rowDef in RowDefs)
        {
            clips[rowDef.name] = BuildClip(rowDef.name, spritesByRow[rowDef.name]);
        }

        var idleActionClips = new Dictionary<string, AnimationClip>();
        foreach (var sheet in IdleActionSheets)
        {
            var frames = SliceIdleActionSheetRow(sheet.path, IdleActionUsedRow);
            idleActionClips[sheet.trigger] = BuildClip($"IdleAction_{sheet.trigger}", frames);
        }

        var controller = BuildController(clips, idleActionClips);
        BuildPrefab(controller, spritesByRow["Idle"][0]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FarmerOtterSpriteSetup] Done.");
    }

    private static void SliceSheet()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        float cw = tex.width / (float)Cols;
        float ch = tex.height / (float)RowDefs.Length;

        var metas = new List<SpriteMetaData>();
        foreach (var rowDef in RowDefs)
        {
            for (int col = 0; col < Cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"FarmerOtter_{rowDef.name}_{col}",
                    rect = new Rect(
                        col * cw + ColumnInsetPx,
                        tex.height - (rowDef.row + 1) * ch + RowInsetPx,
                        cw - ColumnInsetPx * 2f,
                        ch - RowInsetPx * 2f),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }
        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    // Slices one idle-action sheet (8x4) and returns just the requested row's
    // 8 frames, in order. All 4 rows are sliced (not just the used one) so
    // the other framings stay available as sprite sub-assets if needed later.
    private static List<Sprite> SliceIdleActionSheetRow(string path, int usedRow)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        float cw = tex.width / (float)IdleActionCols;
        float ch = tex.height / (float)IdleActionRows;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < IdleActionRows; row++)
        {
            for (int col = 0; col < IdleActionCols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{baseName}_{row}_{col}",
                    rect = new Rect(
                        col * cw + ColumnInsetPx,
                        tex.height - (row + 1) * ch + RowInsetPx,
                        cw - ColumnInsetPx * 2f,
                        ch - RowInsetPx * 2f),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }
        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        var frames = new List<Sprite>();
        for (int col = 0; col < IdleActionCols; col++)
        {
            string name = $"{baseName}_{usedRow}_{col}";
            frames.Add(all.First(s => s.name == name));
        }
        return frames;
    }

    private static Dictionary<string, List<Sprite>> LoadSlicedSprites()
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToList();
        var result = new Dictionary<string, List<Sprite>>();
        foreach (var rowDef in RowDefs)
        {
            var list = new List<Sprite>();
            for (int col = 0; col < Cols; col++)
            {
                string name = $"FarmerOtter_{rowDef.name}_{col}";
                list.Add(all.First(s => s.name == name));
            }
            result[rowDef.name] = list;
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
        controller.AddParameter("Harvest", AnimatorControllerParameterType.Trigger);

        var idleState = sm.AddState("Idle");
        idleState.motion = clips["Idle"];
        sm.defaultState = idleState;

        var walkState = sm.AddState("Walk");
        walkState.motion = clips["Walk"];

        var harvestState = sm.AddState("Harvest");
        harvestState.motion = clips["Harvest"];

        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.1f;
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.1f;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        var anyToHarvest = sm.AddAnyStateTransition(harvestState);
        anyToHarvest.hasExitTime = false;
        anyToHarvest.duration = 0.05f;
        anyToHarvest.AddCondition(AnimatorConditionMode.If, 0, "Harvest");

        var harvestToIdle = harvestState.AddTransition(idleState);
        harvestToIdle.hasExitTime = true;
        harvestToIdle.exitTime = 1f;
        harvestToIdle.duration = 0.1f;

        // Random idle flavor actions (Net/Stretch/Eat) — same Any State ->
        // trigger -> clip -> exit time 1 -> Idle pattern as Harvest, so
        // FarmerOtterController.PlayRandomActionOrWait's generic "wait until
        // back in Idle" logic works unchanged for these too.
        foreach (var kvp in idleActionClips)
        {
            string trigger = kvp.Key;
            var clip = kvp.Value;

            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var state = sm.AddState(trigger);
            state.motion = clip;

            var anyToState = sm.AddAnyStateTransition(state);
            anyToState.hasExitTime = false;
            anyToState.duration = 0.05f;
            anyToState.AddCondition(AnimatorConditionMode.If, 0, trigger);

            var stateToIdle = state.AddTransition(idleState);
            stateToIdle.hasExitTime = true;
            stateToIdle.exitTime = 1f;
            stateToIdle.duration = 0.1f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void BuildPrefab(AnimatorController controller, Sprite defaultSprite)
    {
        Directory.CreateDirectory(PrefabDir);
        var go = new GameObject("FarmerOtter");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = defaultSprite;
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/FarmerOtter.prefab");
        Object.DestroyImmediate(go);
    }
}
