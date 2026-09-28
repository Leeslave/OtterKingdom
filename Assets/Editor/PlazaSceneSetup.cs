using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the plaza from the layout data:
//   1. imports Plaza_Ground.png (floor only) and each Props/Prop_*.png with
//      its ground pivot and a per-prop PPU so it lands at the layout scale,
//   2. creates/updates one prefab per prop (sprite + PlazaProp + a Blocked
//      "Footprint" polygon), plus the PlazaOtter prefab and PlazaSettings,
//   3. builds and saves Assets/Scenes/Plaza.unity: ground, props placed per
//      the layout, walkable polygon, spawn points, camera.
// Run via: OtterKingdom > Tools > Setup Plaza Scene
//
// Source of truth is Assets/Art/Plaza/plaza_layout.json (all coordinates in
// ground-image pixels) and Props/plaza_props_pivots.json (written by
// Tools/PlazaProps/slice_plaza_props.py). Tools/PlazaProps/preview_layout.py
// renders the same data without Unity for quick layout tuning.
//
// Prop prefabs are rebuilt in place every run so footprint/scale edits in the
// JSON take effect; the otter prefab and PlazaSettings are only created when
// missing, so Inspector tweaks on them survive. The scene is rebuilt after a
// confirmation.
public static class PlazaSceneSetup
{
    private const string ArtDir = "Assets/Art/Plaza";
    private const string GroundPath = ArtDir + "/Plaza_Ground.png";
    private const string LayoutPath = ArtDir + "/plaza_layout.json";
    private const string PropsDir = ArtDir + "/Props";
    private const string PivotsPath = PropsDir + "/plaza_props_pivots.json";
    private const string PrefabDir = "Assets/Prefabs/Plaza";
    private const string OtterPrefabPath = PrefabDir + "/PlazaOtter.prefab";
    private const string FarmerOtterPrefabPath = "Assets/Prefabs/FarmerOtter.prefab";
    private const string FarmerOtterControllerPath = "Assets/Animations/FarmerOtter/FarmerOtter.controller";
    private const string SettingsPath = "Assets/Data/Plaza/PlazaSettings.asset";
    private const string ScenePath = "Assets/Scenes/Plaza.unity";

    // FarmerOtter frames are 240px tall cells with a centre pivot and the
    // feet baseline ~10px above the cell bottom (Tools/SpriteRepack), at
    // PPU 120: (120 - 10) / 120. The Visual child is raised by this so the
    // prefab root sits exactly on the feet.
    private const float OtterFeetOffset = 110f / 120f;

    // At ground PPU 40 the 1024x1536 ground is 25.6 x 38.4 world units. With
    // the ~1.67-unit otter and ortho size 9 on a 9:16 portrait screen, the
    // otter is ~9% of the screen height and the map ~2.5 screens wide, ~2.1 tall.
    private const float CameraOrthoSize = 9f;
    private static readonly Color CameraBackground = new Color(0.19f, 0.30f, 0.47f);

#pragma warning disable 0649 // fields are filled by JsonUtility
    [Serializable] private class LayoutData
    {
        public float groundPixelsPerUnit;
        public float[] initialFocus;
        public float[] walkable;
        public BlockedData[] blocked;
        public float[] spawnPoints;
        public PropDef[] props;
        public Placement[] placements;
    }

    [Serializable] private class BlockedData
    {
        public string name;
        public float[] points;
    }

    [Serializable] private class PropDef
    {
        public string name;
        public float scale;   // ground px per prop px
        public bool flat;
        public float[] footprint; // prop px, origin top-left
    }

    [Serializable] private class Placement
    {
        public string prop;
        public float x;
        public float y;
        public bool flipX;
        public float scale;   // multiplier on the prop's scale; 0 / missing = 1
    }

    [Serializable] private class PivotFile
    {
        public PivotData[] props;
    }

    [Serializable] private class PivotData
    {
        public string name;
        public int width;
        public int height;
        public float pivotX; // normalised, from left
        public float pivotY; // normalised, from bottom
    }
#pragma warning restore 0649

    [MenuItem("OtterKingdom/Tools/Setup Plaza Scene")]
    public static void Run()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Setup Plaza Scene",
                $"{ScenePath} 이(가) 이미 있습니다. 씬을 다시 만들까요?\n" +
                "(오브젝트 프리팹은 JSON 기준으로 갱신되고, PlazaOtter 프리팹과 PlazaSettings는 유지됩니다)",
                "다시 만들기", "취소"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        LayoutData layout = LoadJson<LayoutData>(LayoutPath);
        PivotFile pivotFile = LoadJson<PivotFile>(PivotsPath);
        if (layout == null || pivotFile == null) return;

        var pivots = new Dictionary<string, PivotData>();
        foreach (var p in pivotFile.props) pivots[p.name] = p;

        Sprite ground = ImportSprite(GroundPath, layout.groundPixelsPerUnit, new Vector2(0.5f, 0.5f), 2048);
        if (ground == null)
        {
            Debug.LogError($"[PlazaSceneSetup] Ground sprite not found at {GroundPath}.");
            return;
        }

        var propPrefabs = new Dictionary<string, GameObject>();
        foreach (var def in layout.props)
        {
            if (!pivots.TryGetValue(def.name, out var pivot))
            {
                Debug.LogError($"[PlazaSceneSetup] No pivot data for prop '{def.name}' — re-run slice_plaza_props.py.");
                return;
            }
            var prefab = BuildPropPrefab(def, pivot, layout.groundPixelsPerUnit);
            if (prefab == null) return;
            propPrefabs[def.name] = prefab;
        }

        GameObject otterPrefab = LoadOrCreateOtterPrefab();
        if (otterPrefab == null) return;

        PlazaSettings settings = LoadOrCreateSettings(otterPrefab);
        BuildScene(layout, ground, propPrefabs, settings);
    }

    private static T LoadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[PlazaSceneSetup] Missing {path}.");
            return null;
        }
        return JsonUtility.FromJson<T>(File.ReadAllText(path));
    }

    // --- Assets ------------------------------------------------------------

    private static Sprite ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, int maxSize)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject BuildPropPrefab(PropDef def, PivotData pivot, float groundPpu)
    {
        string spritePath = $"{PropsDir}/Prop_{def.name}.png";
        // PPU chosen so 1 prop px = def.scale ground px in world space.
        Sprite sprite = ImportSprite(spritePath, groundPpu / def.scale, new Vector2(pivot.pivotX, pivot.pivotY), 1024);
        if (sprite == null)
        {
            Debug.LogError($"[PlazaSceneSetup] Prop sprite not found at {spritePath}.");
            return null;
        }

        string prefabPath = $"{PrefabDir}/Prop_{def.name}.prefab";
        Directory.CreateDirectory(PrefabDir);

        // Rebuild contents inside the existing prefab asset (keeps its GUID,
        // so scene instances stay linked) or start a fresh one.
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject($"Prop_{def.name}");

        var spriteRenderer = GetOrAdd<SpriteRenderer>(root);
        spriteRenderer.sprite = sprite;

        var prop = GetOrAdd<PlazaProp>(root);
        var propSo = new SerializedObject(prop);
        propSo.FindProperty("flat").boolValue = def.flat;
        propSo.ApplyModifiedPropertiesWithoutUndo();

        Transform footprintTransform = root.transform.Find("Footprint");
        GameObject footprint = footprintTransform != null ? footprintTransform.gameObject : CreateChild("Footprint", root.transform);
        var polygon = GetOrAdd<PlazaAreaPolygon>(footprint);
        float unitsPerPropPx = def.scale / groundPpu;
        var local = new List<Vector2>();
        for (int i = 0; i + 1 < def.footprint.Length; i += 2)
        {
            local.Add(new Vector2(
                (def.footprint[i] - pivot.pivotX * pivot.width) * unitsPerPropPx,
                ((1f - pivot.pivotY) * pivot.height - def.footprint[i + 1]) * unitsPerPropPx));
        }
        SetPolygon(polygon, PlazaAreaKind.Blocked, local);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(root);
        else Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject LoadOrCreateOtterPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(OtterPrefabPath);
        if (existing != null) return existing;

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FarmerOtterControllerPath);
        var farmerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmerOtterPrefabPath);
        var farmerRenderer = farmerPrefab != null ? farmerPrefab.GetComponent<SpriteRenderer>() : null;
        Sprite defaultSprite = farmerRenderer != null ? farmerRenderer.sprite : null;
        if (controller == null || defaultSprite == null)
        {
            Debug.LogError("[PlazaSceneSetup] FarmerOtter controller/prefab missing — run " +
                           "OtterKingdom > Tools > Setup Farmer Otter Animations first.");
            return null;
        }

        var root = new GameObject("PlazaOtter");
        var sortingGroup = root.AddComponent<SortingGroup>();
        root.AddComponent<OtterWanderAgent>();
        var visual = root.AddComponent<OtterVisualController>();

        var body = CreateChild("Visual", root.transform);
        body.transform.localPosition = new Vector3(0f, OtterFeetOffset, 0f);
        var spriteRenderer = body.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = defaultSprite;
        var animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var so = new SerializedObject(visual);
        so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("sortingGroup").objectReferenceValue = sortingGroup;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, OtterPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static PlazaSettings LoadOrCreateSettings(GameObject otterPrefab)
    {
        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(SettingsPath);
        if (settings != null) return settings;

        settings = ScriptableObject.CreateInstance<PlazaSettings>();
        settings.otterPrefabs = new[] { otterPrefab };
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    // --- Scene -------------------------------------------------------------

    private static void BuildScene(LayoutData layout, Sprite ground, Dictionary<string, GameObject> propPrefabs, PlazaSettings settings)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Vector2 imageSize = new Vector2(ground.texture.width, ground.texture.height);
        float ppu = layout.groundPixelsPerUnit;
        Vector2 ToWorld(float px, float py) => PixelToWorld(px, py, imageSize, ppu);

        var plazaRoot = new GameObject("PlazaRoot");

        var bgGo = CreateChild("Background", plazaRoot.transform);
        var bgRenderer = bgGo.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = ground;
        bgRenderer.sortingOrder = PlazaDepth.BackgroundOrder;

        var propsRoot = CreateChild("Props", plazaRoot.transform);
        var propCounts = new Dictionary<string, int>();
        foreach (var place in layout.placements)
        {
            if (!propPrefabs.TryGetValue(place.prop, out var prefab))
            {
                Debug.LogWarning($"[PlazaSceneSetup] Placement uses unknown prop '{place.prop}' — skipped.");
                continue;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, propsRoot.transform);
            propCounts.TryGetValue(place.prop, out int n);
            propCounts[place.prop] = ++n;
            instance.name = $"{place.prop}_{n:00}";
            Vector2 pos = ToWorld(place.x, place.y);
            instance.transform.position = new Vector3(pos.x, pos.y, 0f);
            // Scaling the transform scales sprite and footprint together;
            // negative X mirrors both.
            float scale = place.scale > 0f ? place.scale : 1f;
            instance.transform.localScale = new Vector3(place.flipX ? -scale : scale, scale, 1f);
        }

        var walkableGo = CreateChild("WalkableArea", plazaRoot.transform);
        var walkableArea = walkableGo.AddComponent<PlazaWalkableArea>();
        var mainWalkable = CreateChild("MainPlaza", walkableGo.transform);
        SetPolygon(mainWalkable.AddComponent<PlazaAreaPolygon>(), PlazaAreaKind.Walkable, PixelsToWorld(layout.walkable, ToWorld));

        var blockedGo = CreateChild("BlockedAreas", plazaRoot.transform);
        if (layout.blocked != null)
        {
            foreach (var b in layout.blocked)
            {
                var go = CreateChild(string.IsNullOrEmpty(b.name) ? "Blocked" : b.name, blockedGo.transform);
                SetPolygon(go.AddComponent<PlazaAreaPolygon>(), PlazaAreaKind.Blocked, PixelsToWorld(b.points, ToWorld));
            }
        }

        var areaSo = new SerializedObject(walkableArea);
        var roots = areaSo.FindProperty("polygonRoots");
        roots.arraySize = 3;
        roots.GetArrayElementAtIndex(0).objectReferenceValue = walkableGo.transform;
        roots.GetArrayElementAtIndex(1).objectReferenceValue = blockedGo.transform;
        roots.GetArrayElementAtIndex(2).objectReferenceValue = propsRoot.transform;
        areaSo.ApplyModifiedPropertiesWithoutUndo();

        var spawnRoot = CreateChild("SpawnPoints", plazaRoot.transform);
        var spawnPoints = PixelsToWorld(layout.spawnPoints, ToWorld);
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            var point = CreateChild($"SpawnPoint_{i + 1:00}", spawnRoot.transform);
            point.transform.position = spawnPoints[i];
        }

        var otters = CreateChild("Otters", plazaRoot.transform);

        var focus = CreateChild("InitialCameraFocus", plazaRoot.transform);
        focus.transform.position = ToWorld(layout.initialFocus[0], layout.initialFocus[1]);

        var controllerGo = CreateChild("PlazaController", plazaRoot.transform);
        var controller = controllerGo.AddComponent<PlazaController>();
        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("settings").objectReferenceValue = settings;
        controllerSo.FindProperty("walkableArea").objectReferenceValue = walkableArea;
        controllerSo.FindProperty("otterRoot").objectReferenceValue = otters.transform;
        controllerSo.FindProperty("initialCameraFocus").objectReferenceValue = focus.transform;
        controllerSo.FindProperty("spawnPointRoot").objectReferenceValue = spawnRoot.transform;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Vector3 focusPos = focus.transform.position;
        cameraGo.transform.position = new Vector3(focusPos.x, focusPos.y, -10f);
        var cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraOrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CameraBackground;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cameraGo.AddComponent<AudioListener>();
        var cameraController = cameraGo.AddComponent<PlazaCameraController>();
        var cameraSo = new SerializedObject(cameraController);
        cameraSo.FindProperty("boundsSource").objectReferenceValue = bgRenderer;
        cameraSo.FindProperty("initialFocus").objectReferenceValue = focus.transform;
        cameraSo.FindProperty("desiredOrthographicSize").floatValue = CameraOrthoSize;
        cameraSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = walkableGo;
        Debug.Log($"[PlazaSceneSetup] Built {ScenePath} with {layout.placements.Length} props. Press Play to watch the otters; drag to pan.");
    }

    // --- Helpers -----------------------------------------------------------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetPolygon(PlazaAreaPolygon polygon, PlazaAreaKind kind, List<Vector2> localPoints)
    {
        var so = new SerializedObject(polygon);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        var points = so.FindProperty("points");
        points.arraySize = localPoints.Count;
        for (int i = 0; i < localPoints.Count; i++)
        {
            points.GetArrayElementAtIndex(i).vector2Value = localPoints[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<Vector2> PixelsToWorld(float[] flat, Func<float, float, Vector2> toWorld)
    {
        var result = new List<Vector2>();
        if (flat == null) return result;
        for (int i = 0; i + 1 < flat.Length; i += 2) result.Add(toWorld(flat[i], flat[i + 1]));
        return result;
    }

    // Ground sits at the origin with a centre pivot; image Y points down.
    private static Vector2 PixelToWorld(float px, float py, Vector2 imageSize, float ppu)
    {
        return new Vector2((px - imageSize.x * 0.5f) / ppu, (imageSize.y * 0.5f - py) / ppu);
    }
}
