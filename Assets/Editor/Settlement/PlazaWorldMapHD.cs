using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 최초 지도 좌표에 정렬한 해달 그림체 맵을 네 조각으로 표시한다.
/// 각 조각 4072×3984, PPU 80으로 기존 지도의 월드 크기를 유지한다.
/// 조각: Assets/Art/Plaza/AlignedOtterMap/{TL,TR,BL,BR}_Placement.png.
/// 메뉴: Tools/Plaza/Apply HD World Map. 영토 설정(SettlementSetup.Territory)도 지도를 만든 뒤 이것을 부른다.
/// </summary>
public static class PlazaWorldMapHD
{
    private const string TileFolder = "Assets/Art/Plaza/AlignedOtterMap";
    private static readonly string[,] TileNames = { { "TL", "TR" }, { "BL", "BR" } };
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";
    private const string MapObjectName = "WorldMap";
    private const string OriginalMapPath = "Assets/Art/Plaza/AlignedOtterMap/Plaza_Aligned_4072.png";
    private const string TileObjectPrefix = "HD_";
    private const int Rows = 2;
    private const int Columns = 2;
    private const float PixelsPerUnit = 80f;
    private const int MaxTextureSize = 4096;

    [MenuItem("Tools/Plaza/Apply HD World Map")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PlazaWorldMapHD] Play 모드를 끄고 실행하세요.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        SpriteRenderer map = null;
        foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.name == MapObjectName)
                map = renderer;
        }
        if (map == null)
        {
            Debug.LogError("[PlazaWorldMapHD] 광장 씬에 WorldMap이 없습니다. Tools/Settlement 영토 설정을 먼저 실행하세요.");
            return;
        }
        var background = GameObject.Find("PlazaRoot").transform.Find("Background").GetComponent<SpriteRenderer>();
        var importer = (TextureImporter)AssetImporter.GetAtPath(OriginalMapPath);
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 40f;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            importer.ClearPlatformTextureSettings(platform);
        var originalSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(originalSettings);
        originalSettings.spriteAlignment = (int)SpriteAlignment.Center;
        originalSettings.spritePivot = new Vector2(0.5f, 0.5f);
        originalSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(originalSettings);
        importer.SaveAndReimport();
        // 이전에 바꾼 그림의 크기가 아닌 최초 지형 좌표로 복구한다.
        var layout = JsonUtility.FromJson<MapLayout>(File.ReadAllText("Assets/Art/Plaza/territory_layout.json"));
        Bounds ground = SettlementSetup.GroundBounds(background);
        map.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OriginalMapPath);
        map.transform.localScale = Vector3.one;
        map.transform.position = new Vector3(
            ground.min.x + (layout.map.x0 + layout.map.x1) * 0.5f / layout.groundPixelsPerUnit,
            ground.max.y - (layout.map.y0 + layout.map.y1) * 0.5f / layout.groundPixelsPerUnit,
            background.transform.position.z);
        background.enabled = false;
        if (!ApplyTiles(map))
            return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PlazaWorldMapHD] 광장 지도를 고해상도 조각 4개로 바꿨습니다.");
    }

#pragma warning disable 0649
    [System.Serializable] private class MapLayout
    {
        public float groundPixelsPerUnit;
        public PixelRect map;
    }
    [System.Serializable] private class PixelRect
    {
        public float x0, y0, x1, y1;
    }
#pragma warning restore 0649

    /// <summary>지도 그림을 조각으로 바꾼다 (조각이 없으면 원래 지도 그대로)</summary>
    /// <returns>조각을 깔았으면 true</returns>
    internal static bool ApplyTiles(SpriteRenderer map)
    {
        var tiles = new Sprite[Rows, Columns];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                tiles[r, c] = ImportTile($"{TileFolder}/{TileNames[r, c]}_Placement.png");
                if (tiles[r, c] == null)
                    return false;
            }
        }

        // 다시 실행해도 같게: 전에 깐 조각을 지우고 새로
        for (int i = map.transform.childCount - 1; i >= 0; i--)
        {
            var child = map.transform.GetChild(i);
            if (child.name.StartsWith(TileObjectPrefix))
                Object.DestroyImmediate(child.gameObject);
        }

        // 부모의 위치·크기를 보존하고 현재 지도 Sprite의 범위를 정확히 채운다.
        Vector2 total = map.sprite != null ? (Vector2)map.sprite.bounds.size : new Vector2(101.8f, 99.6f);
        Vector2 tileSize = new Vector2(total.x / Columns, total.y / Rows);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                var renderer = new GameObject($"{TileObjectPrefix}{r}_{c}").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(map.transform, false);
                // 지도 가운데 기준, 0_0 = 왼쪽 위
                renderer.transform.localPosition = new Vector3(
                    -total.x * 0.5f + tileSize.x * (c + 0.5f),
                    total.y * 0.5f - tileSize.y * (r + 0.5f),
                    0f);
                renderer.sprite = tiles[r, c];
                Vector2 nativeSize = renderer.sprite.bounds.size;
                renderer.transform.localScale = new Vector3(tileSize.x / nativeSize.x, tileSize.y / nativeSize.y, 1f);
                renderer.sortingLayerID = map.sortingLayerID;
                renderer.sortingOrder = map.sortingOrder;
                renderer.sharedMaterial = map.sharedMaterial;
            }
        }
        map.enabled = false;
        return true;
    }

    // 가운데 피벗 · 사각 메시 · 가장자리 고정 · 압축 없음으로 윤곽과 이음새 보존.
    private static Sprite ImportTile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[PlazaWorldMapHD] 조각 그림이 없습니다: {path}");
            return null;
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.maxTextureSize = MaxTextureSize;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            importer.ClearPlatformTextureSettings(platform);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
