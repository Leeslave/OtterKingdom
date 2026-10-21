using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the mine zone:
//   0. creates the mining data (balance, ore items, bag tab) — see
//      MiningDataSetup,
//   1. slices the MinerOtter sheets (re-packed by
//      Tools/SpriteRepack/repack_miner_otter.py) and creates/updates the
//      MinerOtter prefab with its SpriteFrameAnimator clips,
//   2. builds and saves Assets/Scenes/Mine.unity: the GameManager prefab,
//      CurrencyManager, InventoryManager, GlobalUI, background, the dashed
//      oval at the mine mouth (with the tunnel point the otter fades into),
//      the mining bubble (the mining clip plays inside it), the walkable sand
//      polygon, the otter and a camera — and adds the scene to Build Settings.
// Run via: OtterKingdom > Tools > Setup Mine Scene
//
// Safe to re-run: the prefab is updated in place (keeps its GUID). The scene
// is rebuilt after a confirmation — hand edits to it (polygon, positions) are
// lost, so change the pixel constants below instead once they're final.
public static class MineSceneSetup
{
    private const string SpriteDir = "Assets/Sprites/Characters/MinerOtter";
    private const string BackgroundPath = "Assets/Art/Mine/mine_background.png";
    private const string BubblePath = "Assets/Art/Mine/mine_emote_bubble.png";
    private const string SlotEmptyPath = "Assets/Art/Farm/Crop/slot_empty.png";
    private const string PrefabPath = "Assets/Prefabs/Mine/MinerOtter.prefab";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";
    private const string GlobalUIPrefabPath = "Assets/Resources/GlobalUI.prefab";
    private const string InventoryConfigPath = "Assets/Scriptable Obejects/Inventory/InventoryConfig.asset";
    private static readonly string[] CurrencyPaths = { "Assets/Scriptable Obejects/Gold.asset", "Assets/Scriptable Obejects/Gem.asset" };
    private const string ScenePath = "Assets/Scenes/Mine.unity";

    // Re-packed sheets (see the repack script's layout.json): 8 columns, each
    // sheet with its own cell size; feet on a baseline BaselineFromTop px
    // below the cell top, where the pivot goes. Same character scale as the
    // fishing otter (220px hat top -> feet at PPU 145 = ~1.52 units).
    private const int Cols = 8;
    private const float OtterPixelsPerUnit = 145f;
    private const string WalkSheet = "MinerOtter_Walk";
    private const string MineSheet = "MinerOtter_Mine";
    private static readonly (string name, int rows, int cellW, int cellH, int baselineFromTop)[] Sheets =
    {
        (WalkSheet, 4, 212, 272, 263), // right / left / front (down) / back (up)
        (MineSheet, 1, 308, 276, 265), // raise -> strike -> recover
    };

    // Idle has no sheet of its own: the first mining frame is a calm side
    // view facing right, like the fishing otter's Cast 0.
    private static readonly (string clip, string sheet, int row, int first, int last, float fps)[] OtterClips =
    {
        (MinerOtterController.ClipIdle, MineSheet, 0, 0, 0, 1f),
        (MinerOtterController.ClipWalkRight, WalkSheet, 0, 0, 7, 10f),
        (MinerOtterController.ClipWalkLeft, WalkSheet, 1, 0, 7, 10f),
        (MinerOtterController.ClipWalkDown, WalkSheet, 2, 0, 7, 10f),
        (MinerOtterController.ClipWalkUp, WalkSheet, 3, 0, 7, 10f),
    };
    private const float MineClipFps = 8f;

    // Same camera/background scale as the fishing scene: 1024x1536 at PPU 96
    // is 16 units tall = ortho size 8.
    private const float BackgroundPixelsPerUnit = 96f;
    private const float CameraOrthoSize = 8f;
    private static readonly Color CameraBackground = new Color(0.45f, 0.64f, 0.27f);

    // Layout in background pixels (origin top-left).
    // Entrance: the sand just inside the timber frame, left of the rails.
    private static readonly Vector2 EntrancePx = new Vector2(585f, 400f);
    // Deeper in the tunnel; the otter fades out walking from the oval to here.
    private static readonly Vector2 InsidePx = new Vector2(600f, 325f);
    // Where the bubble's tail points: the dark tunnel just inside the left post.
    private static readonly Vector2 BubbleTailTipPx = new Vector2(525f, 265f);
    private static readonly Vector2 OtterStartPx = new Vector2(450f, 750f);
    // Feet-walkable area: the sand path in front of the mine, down to about
    // two thirds of the screen, keeping off the cart, fences and rocks.
    // Edit it in the Scene view afterwards (select WalkableArea/Sand).
    private static readonly Vector2[] WalkablePx =
    {
        new Vector2(525f, 350f), new Vector2(640f, 350f), new Vector2(645f, 580f),
        new Vector2(780f, 600f), new Vector2(760f, 720f), new Vector2(800f, 820f),
        new Vector2(760f, 950f), new Vector2(700f, 1150f), new Vector2(300f, 1150f),
        new Vector2(250f, 1000f), new Vector2(270f, 880f), new Vector2(210f, 760f),
        new Vector2(210f, 640f), new Vector2(290f, 580f), new Vector2(300f, 450f),
        new Vector2(500f, 430f),
    };
    private const float WalkableClearance = 0.25f;

    // slot_empty.png: see FishingSceneSetup for these numbers.
    private const float SlotPixelsPerUnit = 104.5f;
    private const float SlotOvalWidthPx = 497f;
    private const float SlotOvalCentreFromBottomPx = 98.5f;
    private const float EntranceCircleWidth = 1.2f; // world units

    // mine_emote_bubble.png (cropped to its outline): 1231x858, rounded body
    // centred at BubbleBodyCentrePx, tail tip at BubbleTailTipSrcPx.
    private static readonly Vector2 BubbleBodyCentrePx = new Vector2(550f, 429f);
    private static readonly Vector2 BubbleTailTipSrcPx = new Vector2(1226f, 433f);
    private const float BubbleBodyWidthPx = 1100f;
    private const float BubbleBodyWidth = 2.4f; // world units
    // The mining otter inside, relative to its normal size (~1.3 units tall).
    private const float BubbleOtterScale = 0.85f;

    private const int EntranceSortingOrder = 1;
    private const int BubbleSortingOrder = 3;
    private const int BubbleOtterSortingOrder = 4;

    [MenuItem("OtterKingdom/Tools/Setup Mine Scene")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[MineSceneSetup] Stop Play mode first.");
            return;
        }
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Setup Mine Scene",
                $"{ScenePath} 이(가) 이미 있습니다. 씬을 다시 만들까요?\n(MinerOtter 프리팹은 갱신됩니다)",
                "다시 만들기", "취소"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!MiningDataSetup.Run()) return;

        var frames = new Dictionary<string, List<List<Sprite>>>();
        foreach (var sheet in Sheets)
        {
            var sliced = SliceSheet(sheet.name, sheet.rows, sheet.cellW, sheet.cellH, sheet.baselineFromTop);
            if (sliced == null) return;
            frames[sheet.name] = sliced;
        }

        GameObject otterPrefab = BuildOrUpdateOtterPrefab(frames);

        float bubblePpu = BubbleBodyWidthPx / BubbleBodyWidth;
        Sprite background = ImportSingleSprite(BackgroundPath, BackgroundPixelsPerUnit, new Vector2(0.5f, 0.5f), 2048);
        Sprite bubble = ImportSingleSprite(BubblePath, bubblePpu, PivotOf(BubblePath, BubbleBodyCentrePx), 2048);
        Sprite slotEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(SlotEmptyPath);
        bool gameManagerExists = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath) != null;
        if (background == null || bubble == null || slotEmpty == null || !gameManagerExists)
        {
            Debug.LogError($"[MineSceneSetup] Missing asset: background={background != null}, bubble={bubble != null}, " +
                           $"slot_empty={slotEmpty != null}, GameManager prefab={gameManagerExists}.");
            return;
        }

        BuildScene(background, bubble, bubblePpu, slotEmpty, otterPrefab, frames[MineSheet][0]);
        RegisterBuildScene();
        AssetDatabase.SaveAssets();
    }

    // --- Assets ------------------------------------------------------------

    // Returns [row][col] sprites, named "{sheet}_{row}_{col}".
    private static List<List<Sprite>> SliceSheet(string sheetName, int rows, int cellW, int cellH, int baselineFromTop)
    {
        string path = $"{SpriteDir}/{sheetName}.png";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[MineSceneSetup] Sprite sheet not found at {path}.");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = OtterPixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // The mine sheet is 2464px wide; the 2048 default would downscale it.
        importer.maxTextureSize = 4096;

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        if (texW != cellW * Cols || texH != cellH * rows)
        {
            Debug.LogError($"[MineSceneSetup] {path} is {texW}x{texH}, expected " +
                           $"{cellW * Cols}x{cellH * rows}. Re-run repack_miner_otter.py and update Sheets.");
            return null;
        }

        var pivot = new Vector2(0.5f, (cellH - baselineFromTop) / (float)cellH);
        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = $"{sheetName}_{row}_{col}",
                    rect = new Rect(col * cellW, texH - (row + 1) * cellH, cellW, cellH),
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
            for (int col = 0; col < Cols; col++) list.Add(all[$"{sheetName}_{row}_{col}"]);
            result.Add(list);
        }
        return result;
    }

    // Normalised pivot for a point given in image pixels (origin top-left).
    private static Vector2 PivotOf(string path, Vector2 px)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return new Vector2(0.5f, 0.5f);
        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
        return new Vector2(px.x / w, 1f - px.y / h);
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
        importer.alphaIsTransparency = true;
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
    // components); clips are rewritten every run.
    private static GameObject BuildOrUpdateOtterPrefab(Dictionary<string, List<List<Sprite>>> frames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("MinerOtter");

        var spriteRenderer = GetOrAdd<SpriteRenderer>(root);
        spriteRenderer.sprite = frames[MineSheet][0][0];

        var animator = GetOrAdd<SpriteFrameAnimator>(root);
        var animSo = new SerializedObject(animator);
        var clipsProp = animSo.FindProperty("clips");
        clipsProp.arraySize = OtterClips.Length;
        for (int i = 0; i < OtterClips.Length; i++)
        {
            var def = OtterClips[i];
            WriteClip(clipsProp.GetArrayElementAtIndex(i), def.clip, def.fps,
                frames[def.sheet][def.row].GetRange(def.first, def.last - def.first + 1));
        }
        animSo.ApplyModifiedPropertiesWithoutUndo();

        GetOrAdd<MinerOtterController>(root);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(root);
        else Object.DestroyImmediate(root);
        return saved;
    }

    private static void WriteClip(SerializedProperty element, string name, float fps, List<Sprite> clipFrames)
    {
        element.FindPropertyRelative("name").stringValue = name;
        element.FindPropertyRelative("fps").floatValue = fps;
        element.FindPropertyRelative("loop").boolValue = true;
        var framesProp = element.FindPropertyRelative("frames");
        framesProp.arraySize = clipFrames.Count;
        for (int f = 0; f < clipFrames.Count; f++)
            framesProp.GetArrayElementAtIndex(f).objectReferenceValue = clipFrames[f];
    }

    // --- Scene -------------------------------------------------------------

    private static void BuildScene(Sprite background, Sprite bubble, float bubblePpu, Sprite slotEmpty,
        GameObject otterPrefab, List<Sprite> mineFrames)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Load assets only AFTER NewScene — see FishingSceneSetup.BuildScene
        // (objects loaded before it can come back as destroyed instances).
        var gameManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath);
        var globalUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIPrefabPath);
        var inventoryConfig = AssetDatabase.LoadAssetAtPath<InventoryConfig>(InventoryConfigPath);
        Vector2 imageSize = new Vector2(background.texture.width, background.texture.height);
        Vector2 ToWorld(Vector2 px) => new Vector2(
            (px.x - imageSize.x * 0.5f) / BackgroundPixelsPerUnit,
            (imageSize.y * 0.5f - px.y) / BackgroundPixelsPerUnit);

        // Managers: same set as the fishing scene, so save, coins, bag and
        // the global UI are shared.
        var gameManager = (GameObject)PrefabUtility.InstantiatePrefab(gameManagerPrefab, scene);
        gameManager.name = "GameManager";

        var currencyManager = new GameObject("CurrencyManager").AddComponent<CurrencyManager>();
        var cmSo = new SerializedObject(currencyManager);
        var currenciesProp = cmSo.FindProperty("_allCurrencies");
        currenciesProp.arraySize = CurrencyPaths.Length;
        for (int i = 0; i < CurrencyPaths.Length; i++)
            currenciesProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Currency>(CurrencyPaths[i]);
        cmSo.ApplyModifiedPropertiesWithoutUndo();

        var inventoryManager = new GameObject("InventoryManager").AddComponent<InventoryManager>();
        var imSo = new SerializedObject(inventoryManager);
        imSo.FindProperty("_config").objectReferenceValue = inventoryConfig;
        imSo.ApplyModifiedPropertiesWithoutUndo();
        if (inventoryConfig == null)
            Debug.LogError($"[MineSceneSetup] {InventoryConfigPath} not found — assign InventoryManager's Config by hand.", inventoryManager);

        if (globalUIPrefab != null)
        {
            var globalUI = (GameObject)PrefabUtility.InstantiatePrefab(globalUIPrefab, scene);
            globalUI.name = "GlobalUI";
        }
        else
        {
            Debug.LogWarning($"[MineSceneSetup] {GlobalUIPrefabPath} not found — run Tools > Navigation > Build Global UI.");
        }

        var root = new GameObject("MineRoot");

        var bgGo = CreateChild("Background", root.transform);
        var bgRenderer = bgGo.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = background;
        bgRenderer.sortingOrder = 0;

        // Entrance: root = the otter's feet; the dashed oval child is centred
        // on it, and Inside marks where it fades out in the tunnel.
        var entranceGo = CreateChild("MineEntrance", root.transform);
        entranceGo.transform.position = ToWorld(EntrancePx);
        var circleGo = CreateChild("Circle", entranceGo.transform);
        float circleScale = EntranceCircleWidth / (SlotOvalWidthPx / SlotPixelsPerUnit);
        circleGo.transform.localScale = new Vector3(circleScale, circleScale, 1f);
        circleGo.transform.localPosition = new Vector3(0f, -SlotOvalCentreFromBottomPx / SlotPixelsPerUnit * circleScale, 0f);
        var circleRenderer = circleGo.AddComponent<SpriteRenderer>();
        circleRenderer.sprite = slotEmpty;
        circleRenderer.sortingOrder = EntranceSortingOrder;
        var circleCollider = circleGo.AddComponent<BoxCollider2D>(); // auto-fits the sprite
        var insideGo = CreateChild("Inside", entranceGo.transform);
        insideGo.transform.position = ToWorld(InsidePx);
        var entrance = entranceGo.AddComponent<MineEntranceView>();
        var entranceSo = new SerializedObject(entrance);
        entranceSo.FindProperty("circleRenderer").objectReferenceValue = circleRenderer;
        entranceSo.FindProperty("circleCollider").objectReferenceValue = circleCollider;
        entranceSo.FindProperty("inside").objectReferenceValue = insideGo.transform;
        entranceSo.ApplyModifiedPropertiesWithoutUndo();

        // Emote: the root sits on the bubble body's centre (the sprite pivot),
        // placed so the tail tip lands on BubbleTailTipPx. The mining otter's
        // cell is centred in the body.
        var emoteGo = CreateChild("MiningEmote", root.transform);
        Vector2 tailOffset = new Vector2(BubbleTailTipSrcPx.x - BubbleBodyCentrePx.x,
            BubbleBodyCentrePx.y - BubbleTailTipSrcPx.y) / bubblePpu;
        emoteGo.transform.position = ToWorld(BubbleTailTipPx) - tailOffset;
        var bubbleGo = CreateChild("Bubble", emoteGo.transform);
        var bubbleRenderer = bubbleGo.AddComponent<SpriteRenderer>();
        bubbleRenderer.sprite = bubble;
        bubbleRenderer.sortingOrder = BubbleSortingOrder;
        var bubbleCollider = bubbleGo.AddComponent<BoxCollider2D>();

        var (_, _, _, mineCellH, mineBaseline) = Sheets.First(s => s.name == MineSheet);
        var bubbleOtterGo = CreateChild("MiningOtter", emoteGo.transform);
        float feetBelowCentre = (mineBaseline - mineCellH * 0.5f) / OtterPixelsPerUnit * BubbleOtterScale;
        bubbleOtterGo.transform.localPosition = new Vector3(0f, -feetBelowCentre, 0f);
        bubbleOtterGo.transform.localScale = new Vector3(BubbleOtterScale, BubbleOtterScale, 1f);
        var bubbleOtterRenderer = bubbleOtterGo.AddComponent<SpriteRenderer>();
        bubbleOtterRenderer.sprite = mineFrames[0];
        bubbleOtterRenderer.sortingOrder = BubbleOtterSortingOrder;
        var bubbleOtterAnimator = bubbleOtterGo.AddComponent<SpriteFrameAnimator>();
        var bubbleAnimSo = new SerializedObject(bubbleOtterAnimator);
        var bubbleClips = bubbleAnimSo.FindProperty("clips");
        bubbleClips.arraySize = 1;
        WriteClip(bubbleClips.GetArrayElementAtIndex(0), MineEmoteView.ClipMine, MineClipFps, mineFrames);
        bubbleAnimSo.ApplyModifiedPropertiesWithoutUndo();

        var emote = emoteGo.AddComponent<MineEmoteView>();
        var emoteSo = new SerializedObject(emote);
        emoteSo.FindProperty("bubbleRenderer").objectReferenceValue = bubbleRenderer;
        emoteSo.FindProperty("otterRenderer").objectReferenceValue = bubbleOtterRenderer;
        emoteSo.FindProperty("otterAnimator").objectReferenceValue = bubbleOtterAnimator;
        emoteSo.FindProperty("bubbleCollider").objectReferenceValue = bubbleCollider;
        emoteSo.ApplyModifiedPropertiesWithoutUndo();

        var walkableGo = CreateChild("WalkableArea", root.transform);
        var walkableArea = walkableGo.AddComponent<PlazaWalkableArea>();
        var sandGo = CreateChild("Sand", walkableGo.transform);
        var polygon = sandGo.AddComponent<PlazaAreaPolygon>();
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
        var controllerSo = new SerializedObject(otter.GetComponent<MinerOtterController>());
        controllerSo.FindProperty("walkableArea").objectReferenceValue = walkableArea;
        controllerSo.FindProperty("entrance").objectReferenceValue = entrance;
        controllerSo.FindProperty("emote").objectReferenceValue = emote;
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
        Selection.activeGameObject = sandGo;
        Debug.Log($"[MineSceneSetup] Built {ScenePath}. Press Play and tap the dashed oval at the mine entrance.");
    }

    // The navigator can only load scenes that are in Build Settings.
    private static void RegisterBuildScene()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
