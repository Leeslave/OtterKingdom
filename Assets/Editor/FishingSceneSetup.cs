using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the fishing zone:
//   1. slices the FishingOtter sheets (re-packed by
//      Tools/SpriteRepack/repack_fishing_otter.py) and creates/updates the
//      FishingOtter prefab with its SpriteFrameAnimator clips,
//   2. creates FishingBalanceData if missing,
//   3. builds and saves Assets/Scenes/Fishing.unity: GameManager (same data
//      as the farm scene, so save/coins/inventory are shared), CurrencyManager, background,
//      the dashed fishing spot at the end of the dock, the walkable dock
//      polygon, the otter and a camera.
// Run via: OtterKingdom > Tools > Setup Fishing Scene
//
// Safe to re-run: the prefab is updated in place (keeps its GUID) and the
// balance asset is kept, so Inspector tweaks on it survive. The scene is
// rebuilt after a confirmation — hand edits to it (polygon, spot position)
// are lost, so change the pixel constants below instead once they're final.
public static class FishingSceneSetup
{
    private const string SpriteDir = "Assets/Sprites/Characters/FishingOtter";
    private const string BackgroundPath = "Assets/Art/Fishing/fishing_background.png";
    private const string SlotEmptyPath = "Assets/Art/Farm/Crop/slot_empty.png";
    private const string PrefabPath = "Assets/Prefabs/Fishing/FishingOtter.prefab";
    private const string BalancePath = "Assets/Data/Fishing/FishingBalanceData.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string CropsDir = "Assets/Data/Crops";
    private const string ScenePath = "Assets/Scenes/Fishing.unity";

    // Re-packed grid (see the repack script's layout.json): 8 columns of
    // CellW x CellH, hat centred on the cell's centre column, feet on a
    // baseline BaselineFromTop px below the cell top. The pivot sits exactly
    // on the feet, so the otter's transform position is where it stands.
    private const int Cols = 8;
    private const int CellW = 428;
    private const int CellH = 276;
    private const int BaselineFromTop = 258;

    // Neutral pose is 220px from hat top to feet -> ~1.52 world units tall.
    private const float OtterPixelsPerUnit = 145f;

    // 1024x1536 background at PPU 96 is exactly 16 units tall = ortho size 8
    // (same camera as the farm scene). On a 9:16 screen ~80px is cropped
    // from each side.
    private const float BackgroundPixelsPerUnit = 96f;
    private const float CameraOrthoSize = 8f;
    private static readonly Color CameraBackground = new Color(0.10f, 0.62f, 0.85f);

    // Layout in background pixels (origin top-left). The spot is on the dock's
    // right end so the bobber (~1 unit right of the feet) lands in the water
    // just past the edge.
    private static readonly Vector2 SpotPx = new Vector2(665f, 655f);
    private static readonly Vector2 OtterStartPx = new Vector2(380f, 645f);
    // Feet-walkable area: the dock deck plus the sand strip to its left.
    // Edit it in the Scene view afterwards (select WalkableArea/Dock).
    private static readonly Vector2[] WalkablePx =
    {
        new Vector2(85f, 590f), new Vector2(690f, 590f),
        new Vector2(690f, 695f), new Vector2(85f, 695f),
    };
    // The dock is narrow, so less clearance than the plaza's default.
    private const float WalkableClearance = 0.25f;

    // slot_empty.png: 580x197 at PPU 104.5 with a bottom-centre pivot; the
    // dashed oval is 497px wide and its centre is 98.5px above the bottom.
    private const float SlotPixelsPerUnit = 104.5f;
    private const float SlotOvalWidthPx = 497f;
    private const float SlotOvalCentreFromBottomPx = 98.5f;
    private const float SpotCircleWidth = 1.3f; // world units

    private const int SpotSortingOrder = 1;

    private struct ClipDef
    {
        public string name;
        public string sheet;
        public int row;
        public int firstFrame;
        public int lastFrame;
        public float fps;
        public bool loop;

        public ClipDef(string name, string sheet, int row, int firstFrame, int lastFrame, float fps, bool loop)
        {
            this.name = name;
            this.sheet = sheet;
            this.row = row;
            this.firstFrame = firstFrame;
            this.lastFrame = lastFrame;
            this.fps = fps;
            this.loop = loop;
        }
    }

    // sheet name -> row count
    private static readonly (string name, int rows)[] Sheets =
    {
        ("FishingOtter_Walk", 4),       // right / left / front (down) / back (up)
        ("FishingOtter_Cast", 1),       // 0 rod on shoulder, 1 swing, 2-7 bobber waiting
        ("FishingOtter_Bite", 1),       // "!" bite, reeling in
        ("FishingOtter_Pull", 1),       // lifting the rod out
        ("FishingOtter_CatchFish", 1),  // happy
        ("FishingOtter_CatchTrash", 1), // boot, sulking
    };

    private static readonly ClipDef[] Clips =
    {
        new ClipDef(FishingOtterController.ClipIdle, "FishingOtter_Cast", 0, 0, 0, 1f, true),
        new ClipDef(FishingOtterController.ClipWalkRight, "FishingOtter_Walk", 0, 0, 7, 10f, true),
        new ClipDef(FishingOtterController.ClipWalkLeft, "FishingOtter_Walk", 1, 0, 7, 10f, true),
        new ClipDef(FishingOtterController.ClipWalkDown, "FishingOtter_Walk", 2, 0, 7, 10f, true),
        new ClipDef(FishingOtterController.ClipWalkUp, "FishingOtter_Walk", 3, 0, 7, 10f, true),
        new ClipDef(FishingOtterController.ClipCast, "FishingOtter_Cast", 0, 0, 2, 5f, false),
        new ClipDef(FishingOtterController.ClipWait, "FishingOtter_Cast", 0, 2, 7, 4f, true),
        new ClipDef(FishingOtterController.ClipBite, "FishingOtter_Bite", 0, 0, 7, 8f, false),
        new ClipDef(FishingOtterController.ClipPull, "FishingOtter_Pull", 0, 0, 7, 8f, false),
        new ClipDef(FishingOtterController.ClipCatchFish, "FishingOtter_CatchFish", 0, 0, 7, 5f, false),
        new ClipDef(FishingOtterController.ClipCatchTrash, "FishingOtter_CatchTrash", 0, 0, 7, 5f, false),
    };

    [MenuItem("OtterKingdom/Tools/Setup Fishing Scene")]
    public static void Run()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Setup Fishing Scene",
                $"{ScenePath} 이(가) 이미 있습니다. 씬을 다시 만들까요?\n" +
                "(FishingOtter 프리팹은 갱신되고, FishingBalanceData는 유지됩니다)",
                "다시 만들기", "취소"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var frames = new Dictionary<string, List<List<Sprite>>>();
        foreach (var (sheetName, rows) in Sheets)
        {
            var sliced = SliceSheet($"{SpriteDir}/{sheetName}.png", rows);
            if (sliced == null) return;
            frames[sheetName] = sliced;
        }

        GameObject otterPrefab = BuildOrUpdateOtterPrefab(frames);
        FishingBalanceData balance = LoadOrCreateBalance();

        Sprite background = ImportSingleSprite(BackgroundPath, BackgroundPixelsPerUnit, new Vector2(0.5f, 0.5f), 2048);
        Sprite slotEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(SlotEmptyPath);
        bool goldExists = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath) != null;
        if (background == null || slotEmpty == null || !goldExists)
        {
            Debug.LogError($"[FishingSceneSetup] Missing asset: background={background != null}, " +
                           $"slot_empty={slotEmpty != null}, Gold={goldExists}.");
            return;
        }

        BuildScene(background, slotEmpty, otterPrefab);
        AssetDatabase.SaveAssets();
    }

    // --- Assets ------------------------------------------------------------

    // Returns [row][col] sprites, named "{sheet}_{row}_{col}".
    private static List<List<Sprite>> SliceSheet(string path, int rows)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[FishingSceneSetup] Sprite sheet not found at {path}.");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = OtterPixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // Sheets are 3424px wide; the 2048 default would downscale them.
        importer.maxTextureSize = 4096;

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        if (texW != CellW * Cols || texH != CellH * rows)
        {
            Debug.LogError($"[FishingSceneSetup] {path} is {texW}x{texH}, expected " +
                           $"{CellW * Cols}x{CellH * rows}. Re-run repack_fishing_otter.py and update CellW/CellH.");
            return null;
        }

        var pivot = new Vector2(0.5f, (CellH - BaselineFromTop) / (float)CellH);
        string baseName = Path.GetFileNameWithoutExtension(path);
        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{baseName}_{row}_{col}",
                    rect = new Rect(col * CellW, texH - (row + 1) * CellH, CellW, CellH),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot
                });
            }
        }
#pragma warning disable CS0618 // spritesheet is deprecated but still the simplest API here
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        var result = new List<List<Sprite>>();
        for (int row = 0; row < rows; row++)
        {
            var list = new List<Sprite>();
            for (int col = 0; col < Cols; col++) list.Add(all[$"{baseName}_{row}_{col}"]);
            result.Add(list);
        }
        return result;
    }

    private static Sprite ImportSingleSprite(string path, float pixelsPerUnit, Vector2 pivot, int maxSize)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Updated in place when it exists (keeps its GUID and any extra
    // components); clips and collider are rewritten every run.
    private static GameObject BuildOrUpdateOtterPrefab(Dictionary<string, List<List<Sprite>>> frames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("FishingOtter");

        var spriteRenderer = GetOrAdd<SpriteRenderer>(root);
        spriteRenderer.sprite = frames["FishingOtter_Cast"][0][0];

        var animator = GetOrAdd<SpriteFrameAnimator>(root);
        var animSo = new SerializedObject(animator);
        var clipsProp = animSo.FindProperty("clips");
        clipsProp.arraySize = Clips.Length;
        for (int i = 0; i < Clips.Length; i++)
        {
            var def = Clips[i];
            var element = clipsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("name").stringValue = def.name;
            element.FindPropertyRelative("fps").floatValue = def.fps;
            element.FindPropertyRelative("loop").boolValue = def.loop;

            var rowFrames = frames[def.sheet][def.row];
            var framesProp = element.FindPropertyRelative("frames");
            framesProp.arraySize = def.lastFrame - def.firstFrame + 1;
            for (int f = def.firstFrame; f <= def.lastFrame; f++)
            {
                framesProp.GetArrayElementAtIndex(f - def.firstFrame).objectReferenceValue = rowFrames[f];
            }
        }
        animSo.ApplyModifiedPropertiesWithoutUndo();

        // Tap target for "stop fishing": the body, feet at the origin.
        var collider = GetOrAdd<BoxCollider2D>(root);
        collider.size = new Vector2(0.9f, 1.4f);
        collider.offset = new Vector2(0f, 0.7f);

        GetOrAdd<FishingOtterController>(root);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(root);
        else Object.DestroyImmediate(root);
        return saved;
    }

    private static FishingBalanceData LoadOrCreateBalance()
    {
        var balance = AssetDatabase.LoadAssetAtPath<FishingBalanceData>(BalancePath);
        if (balance != null) return balance;

        balance = ScriptableObject.CreateInstance<FishingBalanceData>();
        Directory.CreateDirectory(Path.GetDirectoryName(BalancePath));
        AssetDatabase.CreateAsset(balance, BalancePath);
        AssetDatabase.SaveAssets();
        return balance;
    }

    // Same crops as the farm scene, in growth-time order (carrot, cucumber,
    // potato, sweet potato) — that's the farm's Inspector order too.
    private static CropDefinition[] LoadCrops()
    {
        return AssetDatabase.FindAssets("t:CropDefinition", new[] { CropsDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CropDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(c => c != null)
            .OrderBy(c => c.baseDurationSec)
            .ToArray();
    }

    // --- Scene -------------------------------------------------------------

    private static void BuildScene(Sprite background, Sprite slotEmpty, GameObject otterPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Load the ScriptableObjects only AFTER NewScene: opening a scene in
        // Single mode unloads assets nothing references, and an object loaded
        // before it comes back as a destroyed ("fake null") instance — which
        // SerializedObject silently stores as None. That is how Gold ended up
        // missing on the first run.
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var balance = AssetDatabase.LoadAssetAtPath<FishingBalanceData>(BalancePath);
        Vector2 imageSize = new Vector2(background.texture.width, background.texture.height);
        Vector2 ToWorld(Vector2 px) => new Vector2(
            (px.x - imageSize.x * 0.5f) / BackgroundPixelsPerUnit,
            (imageSize.y * 0.5f - px.y) / BackgroundPixelsPerUnit);

        // GameManager: same data as Farm.unity so both scenes share one save.
        var gameManagerGo = new GameObject("GameManager");
        var gameManager = gameManagerGo.AddComponent<GameManager>();
        var gmSo = new SerializedObject(gameManager);
        var crops = LoadCrops();
        var cropsProp = gmSo.FindProperty("cropDefinitions");
        cropsProp.arraySize = crops.Length;
        for (int i = 0; i < crops.Length; i++) cropsProp.GetArrayElementAtIndex(i).objectReferenceValue = crops[i];
        gmSo.FindProperty("fishingBalance").objectReferenceValue = balance;
        gmSo.FindProperty("goldCurrency").objectReferenceValue = gold;
        gmSo.ApplyModifiedPropertiesWithoutUndo();

        gmSo.Update();
        foreach (var field in new[] { "goldCurrency", "fishingBalance" })
        {
            if (gmSo.FindProperty(field).objectReferenceValue == null)
            {
                Debug.LogError($"[FishingSceneSetup] GameManager.{field} could not be assigned — " +
                               "assign it by hand in the Inspector and save the scene.", gameManager);
            }
        }

        // CurrencyManager isn't created on demand, so every zone scene that can
        // be played directly carries one (the duplicate is destroyed in Awake).
        var currencyManagerGo = new GameObject("CurrencyManager");
        var currencyManager = currencyManagerGo.AddComponent<CurrencyManager>();
        var cmSo = new SerializedObject(currencyManager);
        var currenciesProp = cmSo.FindProperty("_allCurrencies");
        currenciesProp.arraySize = 1;
        currenciesProp.GetArrayElementAtIndex(0).objectReferenceValue = gold;
        cmSo.ApplyModifiedPropertiesWithoutUndo();

        var root = new GameObject("FishingRoot");

        var bgGo = CreateChild("Background", root.transform);
        var bgRenderer = bgGo.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = background;
        bgRenderer.sortingOrder = 0;

        // Spot: root = the otter's feet; the dashed oval child is centred on it.
        var spotGo = CreateChild("FishingSpot", root.transform);
        spotGo.transform.position = ToWorld(SpotPx);
        var circleGo = CreateChild("Circle", spotGo.transform);
        float circleScale = SpotCircleWidth / (SlotOvalWidthPx / SlotPixelsPerUnit);
        circleGo.transform.localScale = new Vector3(circleScale, circleScale, 1f);
        circleGo.transform.localPosition = new Vector3(0f, -SlotOvalCentreFromBottomPx / SlotPixelsPerUnit * circleScale, 0f);
        var circleRenderer = circleGo.AddComponent<SpriteRenderer>();
        circleRenderer.sprite = slotEmpty;
        circleRenderer.sortingOrder = SpotSortingOrder;
        var circleCollider = circleGo.AddComponent<BoxCollider2D>(); // auto-fits the sprite
        var spot = spotGo.AddComponent<FishingSpotView>();
        var spotSo = new SerializedObject(spot);
        spotSo.FindProperty("circleRenderer").objectReferenceValue = circleRenderer;
        spotSo.FindProperty("circleCollider").objectReferenceValue = circleCollider;
        spotSo.ApplyModifiedPropertiesWithoutUndo();

        var walkableGo = CreateChild("WalkableArea", root.transform);
        var walkableArea = walkableGo.AddComponent<PlazaWalkableArea>();
        var dockGo = CreateChild("Dock", walkableGo.transform);
        var polygon = dockGo.AddComponent<PlazaAreaPolygon>();
        var polySo = new SerializedObject(polygon);
        polySo.FindProperty("kind").enumValueIndex = (int)PlazaAreaKind.Walkable;
        var points = polySo.FindProperty("points");
        points.arraySize = WalkablePx.Length;
        for (int i = 0; i < WalkablePx.Length; i++) points.GetArrayElementAtIndex(i).vector2Value = ToWorld(WalkablePx[i]);
        polySo.ApplyModifiedPropertiesWithoutUndo();
        var areaSo = new SerializedObject(walkableArea);
        var roots = areaSo.FindProperty("polygonRoots");
        roots.arraySize = 1;
        roots.GetArrayElementAtIndex(0).objectReferenceValue = walkableGo.transform;
        areaSo.FindProperty("clearance").floatValue = WalkableClearance;
        areaSo.ApplyModifiedPropertiesWithoutUndo();

        var otter = (GameObject)PrefabUtility.InstantiatePrefab(otterPrefab, root.transform);
        otter.transform.position = ToWorld(OtterStartPx);
        var controllerSo = new SerializedObject(otter.GetComponent<FishingOtterController>());
        controllerSo.FindProperty("walkableArea").objectReferenceValue = walkableArea;
        controllerSo.FindProperty("spot").objectReferenceValue = spot;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraOrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CameraBackground;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cameraGo.AddComponent<AudioListener>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = dockGo;
        Debug.Log($"[FishingSceneSetup] Built {ScenePath} ({crops.Length} crops). Press Play and tap the dashed circle on the dock.");
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
}
