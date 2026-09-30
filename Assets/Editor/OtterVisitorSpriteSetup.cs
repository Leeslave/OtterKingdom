using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// One-off tool: slices the plaza visitor sheets (Snack / Sleepy / Painter),
// builds their AnimationClips and AnimatorControllers, creates or updates a
// PlazaOtter_{name} prefab per visitor and adds it to PlazaSettings so the
// plaza spawns them alongside the farmer-look PlazaOtter.
// Run via: OtterKingdom > Tools > Setup Otter Visitor Animations
//
// The sheets were re-packed offline by Tools/SpriteRepack/repack_otter_visitors.py
// into one uniform grid (4 cols x 6 rows of CellW x CellH, feet on a shared
// baseline, body on each cell's centre column), so slicing on the exact grid
// with a centre pivot is safe and flipX mirrors the otter in place. The raw
// AI sheets were not on an exact grid and must not be sliced directly.
//
// Controllers follow FarmerOtter.controller's parameters (IsMoving, WalkDir,
// WalkAnimSpeed, action triggers) so OtterVisualController drives them as-is:
// side walk faces right and left is flipX, not a separate clip.
public static class OtterVisitorSpriteSetup
{
    private const string SpriteDir = "Assets/Sprites/Characters/Visitors";
    private const string AnimDir = "Assets/Animations/Visitors";
    private const string PrefabDir = "Assets/Prefabs/Plaza";
    private const string SettingsPath = "Assets/Data/Plaza/PlazaSettings.asset";

    private const int Cols = 4;
    private const int Rows = 6;
    private const int CellW = 240;
    private const int CellH = 260;
    // Feet baseline, in px from the top of each cell (layout.json).
    private const int BaselineFromTop = 249;

    // The visitors are ~220px tall (hat/ears -> feet), so PPU 132 keeps them
    // at the FarmerOtter's ~1.67 world units (PlazaSettings.otterHeight).
    private const float PixelsPerUnit = 132f;

    // Centre pivot -> feet: the Visual child is raised by this so the prefab
    // root sits exactly on the feet, like PlazaSceneSetup's PlazaOtter.
    private const float FeetOffset = (BaselineFromTop - CellH * 0.5f) / PixelsPerUnit;

    // Must match OtterVisualController.WalkDir.
    private const int WalkDirSide = 0;
    private const int WalkDirDown = 1;
    private const int WalkDirUp = 2;

    private const string WalkSpeedParam = "WalkAnimSpeed";
    private const string IdleActionSpeedParam = "IdleActionAnimSpeed";

    // One clip per sheet row. frames = column order to play; fps = playback
    // rate; loops = how many times an action plays before handing back to
    // Idle (ignored for Idle/walks, which loop until the state changes).
    private class ClipDef
    {
        public int row;
        public string name;
        public int[] frames;
        public float fps;
        public int loops = 1;
        public bool isAction;
    }

    private class VisitorDef
    {
        public string name;
        public ClipDef[] actions;
    }

    // Idle cols 1/3 are blinks, so they're only flashed briefly between holds.
    private static readonly int[] IdleFrames = { 0, 0, 0, 1, 2, 2, 2, 3 };
    private static readonly int[] AllFrames = { 0, 1, 2, 3 };
    private const float IdleFps = 3f;
    private const float WalkFps = 6f;

    // Action trigger names must be in OtterVisualController.PlayTriggerNames.
    private static readonly VisitorDef[] Visitors =
    {
        new VisitorDef
        {
            name = "Snack",
            actions = new[]
            {
                new ClipDef { row = 4, name = "Eat", frames = AllFrames, fps = 4f, loops = 2, isAction = true },
                new ClipDef { row = 5, name = "Happy", frames = AllFrames, fps = 4f, loops = 2, isAction = true },
            }
        },
        new VisitorDef
        {
            name = "Sleepy",
            actions = new[]
            {
                new ClipDef { row = 4, name = "Yawn", frames = AllFrames, fps = 3f, loops = 1, isAction = true },
                new ClipDef { row = 5, name = "Sleep", frames = AllFrames, fps = 2f, loops = 3, isAction = true },
            }
        },
        new VisitorDef
        {
            name = "Painter",
            actions = new[]
            {
                new ClipDef { row = 4, name = "Paint", frames = AllFrames, fps = 4f, loops = 2, isAction = true },
                // Col 3 raises the other paw, which pops when looped; skip it.
                new ClipDef { row = 5, name = "Wave", frames = new[] { 2, 0, 1, 0 }, fps = 4f, loops = 2, isAction = true },
            }
        },
    };

    private static ClipDef[] BaseClips => new[]
    {
        new ClipDef { row = 0, name = "Idle", frames = IdleFrames, fps = IdleFps },
        new ClipDef { row = 1, name = "Walk_Down", frames = AllFrames, fps = WalkFps },
        new ClipDef { row = 2, name = "Walk_Up", frames = AllFrames, fps = WalkFps },
        new ClipDef { row = 3, name = "Walk", frames = AllFrames, fps = WalkFps },
    };

    [MenuItem("OtterKingdom/Tools/Setup Otter Visitor Animations")]
    public static void Run()
    {
        // Safe to re-run: wipe previously generated clips/controllers first so
        // CreateAsset doesn't fail against paths that already exist. Prefabs
        // are updated in place instead (see BuildOrUpdatePrefab).
        if (AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.DeleteAsset(AnimDir);
        AssetDatabase.Refresh();
        Directory.CreateDirectory(AnimDir);
        Directory.CreateDirectory(PrefabDir);

        var prefabs = new List<GameObject>();
        foreach (var visitor in Visitors)
        {
            string sheetPath = $"{SpriteDir}/{visitor.name}.png";
            var grid = SliceSheet(sheetPath);
            if (grid == null) continue;

            string dir = $"{AnimDir}/{visitor.name}";
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var def in BaseClips.Concat(visitor.actions))
            {
                clips[def.name] = BuildClip($"{dir}/{visitor.name}_{def.name}.anim", def, grid[def.row]);
            }

            var controller = BuildController($"{dir}/{visitor.name}.controller", clips, visitor.actions);
            prefabs.Add(BuildOrUpdatePrefab(visitor.name, controller, grid[0][0]));
        }

        AddToPlazaSettings(prefabs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[OtterVisitorSpriteSetup] Done — {prefabs.Count} visitor prefab(s) in {PrefabDir}.");
    }

    // Slices the uniform Cols x Rows grid and returns grid[row][col]. Sprites
    // are named "{sheet}_{row}_{col}".
    private static Sprite[][] SliceSheet(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[OtterVisitorSpriteSetup] Missing sheet {path}.");
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        if (texW != CellW * Cols || texH != CellH * Rows)
        {
            Debug.LogError($"[OtterVisitorSpriteSetup] {path} is {texW}x{texH}, expected " +
                           $"{CellW * Cols}x{CellH * Rows}. Re-pack the sheet first.");
            return null;
        }

        string baseName = Path.GetFileNameWithoutExtension(path);
        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{baseName}_{row}_{col}",
                    rect = new Rect(col * CellW, texH - (row + 1) * CellH, CellW, CellH),
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

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        var grid = new Sprite[Rows][];
        for (int row = 0; row < Rows; row++)
        {
            grid[row] = new Sprite[Cols];
            for (int col = 0; col < Cols; col++)
            {
                grid[row][col] = all[$"{baseName}_{row}_{col}"];
            }
        }
        return grid;
    }

    private static AnimationClip BuildClip(string path, ClipDef def, Sprite[] row)
    {
        var clip = new AnimationClip { frameRate = def.fps };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        // The extra closing key repeats the last frame so every frame gets a
        // full 1/fps (otherwise the last one would flash for zero time).
        int n = def.frames.Length;
        var keyframes = new ObjectReferenceKeyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / def.fps,
                value = row[def.frames[Mathf.Min(i, n - 1)]]
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // Same layout as FarmerOtter.controller minus Harvest: Idle <-> 3-way
    // Walk picked by WalkDir, and Any State -> action trigger -> exit time ->
    // Idle, so OtterVisualController's "wait until back in Idle" logic works.
    private static AnimatorController BuildController(
        string path, Dictionary<string, AnimationClip> clips, ClipDef[] actions)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;

        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("WalkDir", AnimatorControllerParameterType.Int);
        AddSpeedParameter(controller, WalkSpeedParam);
        AddSpeedParameter(controller, IdleActionSpeedParam);

        var idleState = sm.AddState("Idle");
        idleState.motion = clips["Idle"];
        sm.defaultState = idleState;

        var walkStates = new (int dir, AnimatorState state)[]
        {
            (WalkDirSide, sm.AddState("Walk")),
            (WalkDirDown, sm.AddState("Walk_Down")),
            (WalkDirUp, sm.AddState("Walk_Up")),
        };
        foreach (var (dir, state) in walkStates)
        {
            state.motion = clips[state.name];
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

        foreach (var action in actions)
        {
            controller.AddParameter(action.name, AnimatorControllerParameterType.Trigger);

            var state = sm.AddState(action.name);
            state.motion = clips[action.name];
            UseSpeedParameter(state, IdleActionSpeedParam);

            var anyToState = sm.AddAnyStateTransition(state);
            anyToState.hasExitTime = false;
            anyToState.duration = 0f;
            anyToState.canTransitionToSelf = false;
            anyToState.AddCondition(AnimatorConditionMode.If, 0, action.name);

            var stateToIdle = state.AddTransition(idleState);
            stateToIdle.hasExitTime = true;
            stateToIdle.exitTime = action.loops;
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

    // Same shape as PlazaSceneSetup's PlazaOtter: root (SortingGroup +
    // OtterWanderAgent + OtterVisualController) on the feet, Visual child
    // with the SpriteRenderer/Animator. An existing prefab is updated in place
    // so Inspector tweaks and references (PlazaSettings) keep working.
    private static GameObject BuildOrUpdatePrefab(string visitorName, AnimatorController controller, Sprite defaultSprite)
    {
        string prefabPath = $"{PrefabDir}/PlazaOtter_{visitorName}.prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject($"PlazaOtter_{visitorName}");

        var sortingGroup = GetOrAdd<SortingGroup>(root);
        GetOrAdd<OtterWanderAgent>(root);
        var visual = GetOrAdd<OtterVisualController>(root);

        var body = root.transform.Find("Visual");
        if (body == null)
        {
            body = new GameObject("Visual").transform;
            body.SetParent(root.transform, false);
        }
        body.localPosition = new Vector3(0f, FeetOffset, 0f);

        var spriteRenderer = GetOrAdd<SpriteRenderer>(body.gameObject);
        spriteRenderer.sprite = defaultSprite;
        var animator = GetOrAdd<Animator>(body.gameObject);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var so = new SerializedObject(visual);
        so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("sortingGroup").objectReferenceValue = sortingGroup;
        so.ApplyModifiedPropertiesWithoutUndo();

        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(root);
        else Object.DestroyImmediate(root);
        return saved;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void AddToPlazaSettings(List<GameObject> prefabs)
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(SettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"[OtterVisitorSpriteSetup] {SettingsPath} not found — run Setup Plaza Scene, then re-run this " +
                             "or add the PlazaOtter_* prefabs to PlazaSettings.otterPrefabs by hand.");
            return;
        }

        var list = (settings.otterPrefabs ?? new GameObject[0]).ToList();
        int before = list.Count;
        foreach (var prefab in prefabs)
        {
            if (prefab != null && !list.Contains(prefab)) list.Add(prefab);
        }
        if (list.Count == before) return;

        settings.otterPrefabs = list.ToArray();
        EditorUtility.SetDirty(settings);
    }
}
