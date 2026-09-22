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

    private static readonly (int row, string name)[] RowDefs =
    {
        (0, "Walk"),
        (1, "Walk_Alt"),
        (2, "Idle"),
        (3, "Idle_Back"),
        (4, "Harvest"),
    };

    [MenuItem("OtterKingdom/Tools/Setup Farmer Otter Animations")]
    public static void Run()
    {
        SliceSheet();

        var spritesByRow = LoadSlicedSprites();
        Directory.CreateDirectory(AnimDir);

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var rowDef in RowDefs)
        {
            clips[rowDef.name] = BuildClip(rowDef.name, spritesByRow[rowDef.name]);
        }

        var controller = BuildController(clips);
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
                    rect = new Rect(col * cw, tex.height - (rowDef.row + 1) * ch, cw, ch),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }
        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
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

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
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
