using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 장소를 살아 보이게 하는 연출 배치 (그림은 Tools/UIGen/scene_life_art.py, 좌표는 배경 그림을 분석해 정함):
/// - 광장: 집 굴뚝 연기, 날리는 꽃잎, 낮 햇살, 가장자리 어둡게
/// - 광산: 수정 반짝임, 입구 등불 일렁임, 가장자리 어둡게
/// - 낚시터: 물결 반짝임, 물고기 점프, 부두 기둥 잔물결, 등불·등대 일렁임, 낮 햇살, 가장자리 어둡게
/// - 밭: 개울·물레방아 물 반짝임, 등불 일렁임, 날리는 꽃잎, 낮 햇살, 가장자리 어둡게
/// 각 씬의 "SceneLife" 아래에 만든다 (굴뚝 연기는 집 아래). 여러 번 실행해도 결과가 같음.
/// </summary>
public static partial class SettlementSetup
{
    private const string SceneLifeName = "SceneLife";
    private const string ChimneySmokeName = "ChimneySmoke";
    private const string FishItemPath = "Assets/Scriptable Obejects/Inventory/Items/Fishing/고등어.asset";

    // 광산 수정 (월드): 배경 그림의 보라·하늘색 수정 덩어리
    private static readonly Vector2[] MineCrystals =
    {
        new Vector2(-4.16f, 4.86f), new Vector2(-3.66f, 4.97f), new Vector2(0.40f, 5.30f), new Vector2(1.06f, 5.40f),
        new Vector2(2.14f, 5.06f), new Vector2(2.60f, 3.38f), new Vector2(3.78f, 1.42f), new Vector2(4.20f, 1.50f),
        new Vector2(-3.82f, -0.67f), new Vector2(-4.39f, -1.17f), new Vector2(4.24f, -2.42f), new Vector2(3.59f, -2.84f),
        new Vector2(-3.33f, -5.48f),
    };
    private static readonly Vector2[] MineLanterns = { new Vector2(-0.55f, 5.30f), new Vector2(2.75f, 4.40f) };

    // 낚시터 물: 배경 그림(1024×1536, 96px)을 16칸으로 나눈 격자, 85% 넘게 물인 칸
    private static readonly Vector2 FishingWaterTopLeft = new Vector2(-5.333f, 8f);
    private const float FishingWaterCell = 0.6667f;
    private static readonly string[] FishingWaterRows =
    {
        "0000000111111111", "0000000011111111", "0000000001111111", "0000000001111111", "0000000011111111", "0000000111111111",
        "0000000111111111", "0000000011111111", "0000000000011111", "0000000000011111", "0000000000011111", "0000000000011111",
        "0000011001011111", "0000000111111111", "0000000011111111", "0000000011111111", "0000000001111111", "0000000001111111",
        "0000000000011111", "0000000000001111", "0000000000000111", "0000000000000011", "0000000000000001", "0000000000000000",
    };
    private static readonly Vector2[] FishingPosts = { new Vector2(-2.37f, -0.3f), new Vector2(-0.02f, -0.3f), new Vector2(1.77f, -0.25f) };
    private static readonly Vector2[] FishingLights = { new Vector2(-3.46f, 3.55f), new Vector2(-1.30f, 7.30f) };

    // 밭: 왼쪽 개울(폭포 → 아래)과 물레방아 물, 등불
    private static readonly Vector2[] FarmWater =
    {
        new Vector2(-3.90f, 4.26f), new Vector2(-3.45f, 3.06f), new Vector2(-3.90f, 2.47f), new Vector2(-4.20f, 1.72f),
        new Vector2(-4.27f, 0.82f), new Vector2(-4.35f, -0.37f), new Vector2(1.63f, 5.23f), new Vector2(2.38f, 5.01f),
    };
    private static readonly Vector2[] FarmLanterns =
    {
        new Vector2(-3.30f, 5.31f), new Vector2(-1.43f, 5.46f), new Vector2(3.50f, 4.86f), new Vector2(-3.93f, -3.51f),
    };

    // 광장 집 굴뚝 (집 그림 픽셀, 왼쪽 위 기준)
    private static readonly (string house, Vector2 pixel)[] Chimneys =
    {
        ("House_Blue_01", new Vector2(187.5f, 17.5f)),
        ("House_Red_01", new Vector2(203.5f, 20f)),
    };

    [MenuItem("Tools/Settlement/Setup Scene Life")]
    public static void PlaceSceneLife()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        ImportArt();

        BuildLife("Assets/Scenes/Plaza.unity", root =>
        {
            AddChimneySmoke();
            AddPetals(root, 6);
            AddSunShafts(root, 0.32f);
            AddVignette(root, 0.3f);
        });
        BuildLife(MineScenePath, root =>
        {
            var crystals = AddSparkles(root, "CrystalSparkles", new WorldArea(MineCrystals.ToList(), 0.12f), new Vector2(0.38f, 0.62f),
                new Vector2(0.12f, 0.4f), 0.85f, new Color(1f, 0.98f, 1f, 1f), 1f, 5);
            // 수정은 반짝일 때 보랏빛이 번짐
            SetHalo(crystals, new Color(0.78f, 0.62f, 1f, 0.75f));
            AddGlows(root, MineLanterns, 1.6f, 0.35f, 0.9f);
            AddVignette(root, 0.34f);
        });
        BuildLife("Assets/Scenes/Fishing.unity", root =>
        {
            var water = new WorldArea(FishingWaterTopLeft, FishingWaterCell, FishingWaterRows.ToList());
            AddSparkles(root, "WaterSparkles", water, new Vector2(0.18f, 0.34f), new Vector2(0.04f, 0.12f), 0.6f,
                new Color(1f, 1f, 1f, 1f), 0.3f, 1);
            AddFishJump(root, water);
            AddRipples(root, FishingPosts, 1.2f, 1);
            AddGlows(root, FishingLights, 1.8f, 0.25f, 0.9f);
            AddSunShafts(root, 0.26f);
            AddVignette(root, 0.28f);
        });
        BuildLife("Assets/Scenes/Farm.unity", root =>
        {
            AddSparkles(root, "StreamSparkles", new WorldArea(FarmWater.ToList(), 0.22f), new Vector2(0.22f, 0.36f),
                new Vector2(0.08f, 0.25f), 0.6f, new Color(1f, 1f, 1f, 1f), 0.3f, -5);
            AddGlows(root, FarmLanterns, 1.4f, 0.25f, 0.9f);
            AddPetals(root, 4);
            AddSunShafts(root, 0.32f);
            AddVignette(root, 0.28f);
        });
        Debug.Log("[SettlementSetup] 장소 연출 배치 완료");
    }

    private static void BuildLife(string scenePath, System.Action<Transform> build)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var old = GameObject.Find(SceneLifeName);
        if (old != null)
            Object.DestroyImmediate(old);
        var root = new GameObject(SceneLifeName).transform;
        build(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static T AddLife<T>(Transform root, string name) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        return go.AddComponent<T>();
    }

    private static SparkleField AddSparkles(Transform root, string name, WorldArea area, Vector2 size, Vector2 interval, float life,
        Color color, float nightAlpha, int order)
    {
        var view = AddLife<SparkleField>(root, name);
        var so = new SerializedObject(view);
        so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_Twinkle");
        so.FindProperty("_material").objectReferenceValue = GlowMaterial();
        so.FindProperty("_color").colorValue = color;
        so.FindProperty("_size").vector2Value = size;
        so.FindProperty("_lifeSeconds").floatValue = life;
        so.FindProperty("_interval").vector2Value = interval;
        so.FindProperty("_nightAlpha").floatValue = nightAlpha;
        so.FindProperty("_sortingOrder").intValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
        SetArea(view, "_area", area);
        return view;
    }

    private static void SetHalo(SparkleField view, Color color)
    {
        var so = new SerializedObject(view);
        so.FindProperty("_haloSprite").objectReferenceValue = LoadPropArt("FX_GlowSoft");
        so.FindProperty("_haloColor").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // WorldArea는 직렬화 클래스라 값을 통째로 넣음
    private static void SetArea(Object target, string field, WorldArea area)
    {
        var info = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        info.SetValue(target, area);
        EditorUtility.SetDirty(target);
    }

    private static void AddGlows(Transform root, Vector2[] points, float size, float dayAlpha, float nightAlpha)
    {
        var view = AddLife<GlowFlicker>(root, "LanternGlow");
        var so = new SerializedObject(view);
        var list = so.FindProperty("_points");
        list.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
            list.GetArrayElementAtIndex(i).vector2Value = points[i];
        so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_GlowSoft");
        so.FindProperty("_material").objectReferenceValue = GlowMaterial();
        so.FindProperty("_size").floatValue = size;
        so.FindProperty("_dayAlpha").floatValue = dayAlpha;
        so.FindProperty("_nightAlpha").floatValue = nightAlpha;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddRipples(Transform root, Vector2[] points, float width, int order)
    {
        var view = AddLife<RippleRings>(root, "PostRipples");
        var so = new SerializedObject(view);
        var list = so.FindProperty("_points");
        list.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
            list.GetArrayElementAtIndex(i).vector2Value = points[i];
        so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_Ripple");
        so.FindProperty("_width").floatValue = width;
        so.FindProperty("_sortingOrder").intValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddFishJump(Transform root, WorldArea water)
    {
        var view = AddLife<FishJumpView>(root, "FishJump");
        var fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>(FishItemPath);
        var so = new SerializedObject(view);
        so.FindProperty("_fishSprite").objectReferenceValue = fish != null ? fish.Icon : null;
        so.FindProperty("_rippleSprite").objectReferenceValue = LoadPropArt("FX_Ripple");
        so.FindProperty("_dropletSprite").objectReferenceValue = LoadPropArt("FX_Droplet");
        so.FindProperty("_fishSize").floatValue = 0.8f;
        so.FindProperty("_arc").vector2Value = new Vector2(1.1f, 1.2f);
        so.ApplyModifiedPropertiesWithoutUndo();
        SetArea(view, "_water", water);
    }

    private static void AddPetals(Transform root, int count)
    {
        var view = AddLife<DriftingPetals>(root, "Petals");
        var so = new SerializedObject(view);
        var sprites = so.FindProperty("_sprites");
        sprites.arraySize = 2;
        sprites.GetArrayElementAtIndex(0).objectReferenceValue = LoadPropArt("FX_Petal_0");
        sprites.GetArrayElementAtIndex(1).objectReferenceValue = LoadPropArt("FX_Petal_1");
        so.FindProperty("_count").intValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSunShafts(Transform root, float alpha)
    {
        var view = AddLife<SunShafts>(root, "SunShafts");
        var so = new SerializedObject(view);
        so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_Sunbeams");
        so.FindProperty("_material").objectReferenceValue = GlowMaterial();
        so.FindProperty("_alpha").floatValue = alpha;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddVignette(Transform root, float strength)
    {
        var view = AddLife<WorldVignette>(root, "Vignette");
        var so = new SerializedObject(view);
        so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_Vignette");
        so.FindProperty("_material").objectReferenceValue = GlowMaterial();
        so.FindProperty("_strength").floatValue = strength;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 집 그림 위 굴뚝 자리에 연기 (집 아래에 붙어 집이 생겨야 보임)
    private static void AddChimneySmoke()
    {
        var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var (house, pixel) in Chimneys)
        {
            var renderer = renderers.FirstOrDefault(r => r.name == house);
            if (renderer == null || renderer.sprite == null)
            {
                Debug.LogWarning($"[SettlementSetup] 광장에 '{house}'가 없어 굴뚝 연기를 건너뜁니다.");
                continue;
            }
            var old = renderer.transform.Find(ChimneySmokeName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var sprite = renderer.sprite;
            float x = (pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit;
            float y = (sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit;
            if (renderer.flipX)
                x = -x;
            var go = new GameObject(ChimneySmokeName);
            go.transform.SetParent(renderer.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var view = go.AddComponent<ChimneySmoke>();
            var so = new SerializedObject(view);
            so.FindProperty("_sprite").objectReferenceValue = LoadPropArt("FX_Smoke");
            so.FindProperty("_color").colorValue = new Color(1f, 1f, 1f, 0.85f);
            so.FindProperty("_size").floatValue = 1.9f;
            so.FindProperty("_rise").floatValue = 2.8f;
            so.FindProperty("_drift").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
