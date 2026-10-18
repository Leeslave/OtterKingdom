using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 광산 미로 테스트 씬 만들기 (지하 1층, Docs/광산_미로탐험과_왕도성장_기획.md 4.4~4.7). 본 게임과 따로 도는 씬 하나 + 데이터 에셋.
/// 1) 그림 가져오기: 갱도 4장 · 막힌 벽(Assets/Art/MineMaze/Views), 상자 · 계단(Objects, python Tools/UIGen/mine_maze_art.py)
/// 2) 설정 · 그림 묶음 에셋 (아래 지도 · 상자 표, 여러 번 실행해도 표대로 다시 씀). 돌 규칙 · 광석은 광산 개편(MineRework) 1층 것을 씀
/// 3) 씬 Assets/Scenes/MineMaze.unity
/// 메뉴: Tools/Mine Maze/Build Test Scene · 배치: -executeMethod MineMazeSetup.Build
/// 광산 개편 데이터가 없으면 먼저 Tools/Mine Rework/Build Test Scene
/// </summary>
public static partial class MineMazeSetup
{
    private const string ArtFolder = "Assets/Art/MineMaze";
    private const string DataFolder = "Assets/Scriptable Obejects/MineMaze";
    private const string ReworkFolder = "Assets/Scriptable Obejects/MineRework";
    private const string ScenePath = "Assets/Scenes/MineMaze.unity";
    // 지도 해달 그림이 오기 전 임시 얼굴
    private const string MapOtterIcon = "Assets/Art/Otter/ICON_Otter_Cast_Ink.png";

    // 지하 1층 지도 ('#' 벽 · '.' 길 · 'S' 입구 · 'E' 계단 · 'R' 길을 막는 돌 · 'T' 보물 상자). 입구 동쪽 세 칸에 첫 돌
    private static readonly string[] Floor1 =
    {
        "#################",
        "#T..#.....R...#T#",
        "#.#.#.###.#.#.#.#",
        "#.#...#...#.#...#",
        "#.#####.#####.###",
        "#...R...#...#...#",
        "###.###.#.#.###.#",
        "#...#T..R.#.....#",
        "#.###.#######.#.#",
        "#.....#...R...#.#",
        "#.#####.#.###.#.#",
        "#.#T....#...#.R.#",
        "#.#######.#.#.#.#",
        "#S..R.....#...#E#",
        "#################",
    };

    // 보물 상자 하나: (광석 id, 최소, 최대, 확률)
    private static readonly (string id, int min, int max, float chance)[] ChestLoot =
    {
        ("ore_shell_copper", 5, 8, 1f),
        ("ore_wave_iron", 1, 2, 1f),
        ("relic_shell_fossil", 1, 1, 0.25f),
    };

    [MenuItem("Tools/Mine Maze/Build Test Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var rework = AssetDatabase.LoadAssetAtPath<MineReworkConfig>($"{ReworkFolder}/MineReworkConfig.asset");
        var reworkArt = AssetDatabase.LoadAssetAtPath<MineReworkArt>($"{ReworkFolder}/MineReworkArt.asset");
        if (rework == null || reworkArt == null)
        {
            Debug.LogError("[MineMazeSetup] 광산 개편 데이터가 없습니다. 먼저 Tools/Mine Rework/Build Test Scene");
            return;
        }
        ImportArt();
        BuildData(rework);
        BuildScene();
        Debug.Log($"[MineMazeSetup] 광산 미로 테스트 씬 만듦: {ScenePath}");
    }

    #region 그림

    private static void ImportArt()
    {
        foreach (var name in new[] { "VIEW_F", "VIEW_FL", "VIEW_FR", "VIEW_FLR", "VIEW_WALL" })
            ImportSprite($"{ArtFolder}/Views/{name}.png", 100f, new Vector2(0.5f, 0.5f), 2048);
        // 물건: 보이는 폭 2유닛 · 발밑 = 아래 가운데
        ImportSprite($"{ArtFolder}/Objects/CHEST_closed.png", 256f, new Vector2(0.5f, 0.06f), 1024);
        ImportSprite($"{ArtFolder}/Objects/CHEST_open.png", 256f, new Vector2(0.5f, 0.06f), 1024);
        ImportSprite($"{ArtFolder}/Objects/STAIRS_down.png", 320f, new Vector2(0.5f, 0.04f), 1024);
    }

    private static void ImportSprite(string path, float ppu, Vector2 pivot, int maxSize)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning($"[MineMazeSetup] 그림이 없습니다: {path}");
            return;
        }
        importer.maxTextureSize = maxSize;
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = ppu;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Sprite Art(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[MineMazeSetup] 그림이 없습니다: {path}");
        return sprite;
    }

    #endregion

    #region 데이터

    private static void BuildData(MineReworkConfig rework)
    {
        if (!AssetDatabase.IsValidFolder(DataFolder))
            AssetDatabase.CreateFolder("Assets/Scriptable Obejects", "MineMaze");

        var config = Asset<MineMazeConfig>($"{DataFolder}/MineMazeConfig.asset");
        var so = new SerializedObject(config);
        so.FindProperty("_rework").objectReferenceValue = rework;
        so.FindProperty("_layer").objectReferenceValue = rework.Layers.Count > 0 ? rework.Layers[0] : null;
        so.FindProperty("_floorName").stringValue = "지하 1층";
        var map = so.FindProperty("_map");
        map.arraySize = Floor1.Length;
        for (int i = 0; i < Floor1.Length; i++)
            map.GetArrayElementAtIndex(i).stringValue = Floor1[i];
        so.FindProperty("_rockRespawnSeconds").floatValue = 60f;
        var loot = so.FindProperty("_chestLoot");
        loot.arraySize = ChestLoot.Length;
        for (int i = 0; i < ChestLoot.Length; i++)
        {
            var row = ChestLoot[i];
            var element = loot.GetArrayElementAtIndex(i);
            var ore = rework.FindOre(row.id);
            if (ore == null)
                Debug.LogWarning($"[MineMazeSetup] 광석이 없습니다: {row.id}");
            element.FindPropertyRelative("_ore").objectReferenceValue = ore;
            element.FindPropertyRelative("_min").intValue = row.min;
            element.FindPropertyRelative("_max").intValue = row.max;
            element.FindPropertyRelative("_chance").floatValue = row.chance;
        }
        so.FindProperty("_mapOtterRuns").intValue = 2;
        so.FindProperty("_testKingdomLevel").intValue = 13;
        so.FindProperty("_gateKingdomLevel").intValue = 9;
        so.FindProperty("_gatePickaxeTier").intValue = 1;
        so.FindProperty("_gateHits").intValue = 30;
        so.FindProperty("_mapOtterEarlyCells").intValue = 40;
        so.ApplyModifiedPropertiesWithoutUndo();
        ConfigureAdditionalFloors(config, rework);

        var art = Asset<MineMazeArt>($"{DataFolder}/MineMazeArt.asset");
        var aso = new SerializedObject(art);
        Set(aso, "_viewForward", Art($"{ArtFolder}/Views/VIEW_F.png"));
        Set(aso, "_viewLeft", Art($"{ArtFolder}/Views/VIEW_FL.png"));
        Set(aso, "_viewRight", Art($"{ArtFolder}/Views/VIEW_FR.png"));
        Set(aso, "_viewBoth", Art($"{ArtFolder}/Views/VIEW_FLR.png"));
        Set(aso, "_wall", Art($"{ArtFolder}/Views/VIEW_WALL.png"));
        Set(aso, "_chestClosed", Art($"{ArtFolder}/Objects/CHEST_closed.png"));
        Set(aso, "_chestOpen", Art($"{ArtFolder}/Objects/CHEST_open.png"));
        Set(aso, "_stairs", Art($"{ArtFolder}/Objects/STAIRS_down.png"));
        Set(aso, "_mapOtter", Art(MapOtterIcon));
        aso.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
    }

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;

    #endregion

    #region 씬

    private static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 열면 메모리의 에셋이 내려가므로 경로로 다시 불러옴
        var config = AssetDatabase.LoadAssetAtPath<MineMazeConfig>($"{DataFolder}/MineMazeConfig.asset");
        var mazeArt = AssetDatabase.LoadAssetAtPath<MineMazeArt>($"{DataFolder}/MineMazeArt.asset");
        var art = AssetDatabase.LoadAssetAtPath<MineReworkArt>($"{ReworkFolder}/MineReworkArt.asset");

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        // 갱도 그림 가장자리 색 (위 · 아래 남는 곳)
        camera.backgroundColor = new Color(0.035f, 0.06f, 0.075f);
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        cameraGo.AddComponent<AudioListener>();

        var view = new GameObject("View").AddComponent<SpriteRenderer>();
        view.sprite = Art($"{ArtFolder}/Views/VIEW_F.png");
        view.sortingOrder = -100;
        var wall = new GameObject("Wall").AddComponent<SpriteRenderer>();
        wall.sprite = Art($"{ArtFolder}/Views/VIEW_WALL.png");
        wall.sortingOrder = -99;
        wall.enabled = false;

        var rock = new GameObject("Rock").transform;
        rock.gameObject.AddComponent<VeinRockView>();
        var chest = new GameObject("Chest").AddComponent<SpriteRenderer>();
        chest.sprite = mazeArt.ChestClosed;
        chest.sortingOrder = 400;
        chest.enabled = false;
        var stairs = new GameObject("Stairs").AddComponent<SpriteRenderer>();
        stairs.sprite = mazeArt.Stairs;
        stairs.sortingOrder = 300;
        stairs.enabled = false;
        var fade = new GameObject("Fade").AddComponent<SpriteRenderer>();
        fade.sortingOrder = 850;
        fade.color = new Color(0f, 0f, 0f, 0f);

        var fx = new GameObject("Fx").AddComponent<MineFx>();
        var sfx = new GameObject("Sfx").AddComponent<MineSfx>();

        var game = new GameObject("MineMazeGame").AddComponent<MineMazeGame>();
        var so = new SerializedObject(game);
        so.FindProperty("_config").objectReferenceValue = config;
        so.FindProperty("_mazeArt").objectReferenceValue = mazeArt;
        so.FindProperty("_art").objectReferenceValue = art;
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_view").objectReferenceValue = view;
        so.FindProperty("_wall").objectReferenceValue = wall;
        so.FindProperty("_rockSpot").objectReferenceValue = rock;
        so.FindProperty("_chest").objectReferenceValue = chest;
        so.FindProperty("_stairs").objectReferenceValue = stairs;
        so.FindProperty("_fade").objectReferenceValue = fade;
        so.FindProperty("_fx").objectReferenceValue = fx;
        so.FindProperty("_sfx").objectReferenceValue = sfx;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    #endregion
}
