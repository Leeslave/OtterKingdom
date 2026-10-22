using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 광장 농장 (Docs/농장_설계형_기획.md): 농장 부품 건물 4종(밭 칸 · 수확 상자 · 흙길 · 스프링클러) + 농장 설정(흙 3단계 · 건물 보상 해금).
/// 그림: Tools/UIGen/farm_parts_art.py → Assets/Art/Farm/Plaza. 여러 번 실행해도 표대로 다시 씀.
/// 메뉴: Tools/Settlement/Apply Plaza Farm · 배치: -executeMethod SettlementSetup.ApplyPlazaFarm
/// </summary>
public static partial class SettlementSetup
{
    private const string FarmArtFolder = "Assets/Art/Farm/Plaza";
    private const string FarmPartFolder = BuildingFolder + "/Farm";
    private const string PlazaFarmConfigPath = DataFolder + "/PlazaFarmConfig.asset";

    // 부품: (ID, 이름, 설명, 쓰임, 그림, 크기, 골드, 목재, 돌, 짓는 초, 바로 놓임, 최대, 비용 오름)
    private static readonly (string id, string name, string desc, FarmPartKind kind, string art, int size, int gold, int wood, int stone,
        float seconds, bool instant, int max, float growth)[] FarmPartTable =
    {
        ("bld_farm_field", "밭 칸", "작물 하나를 심는 밭. 농부가 다 자란 작물을 거둬 상자에 넣어요.", FarmPartKind.Field, "FARM_Soil_0",
            3, 150, 6, 0, 30f, false, 0, 0.25f),
        ("bld_farm_crate", "수확 상자", "농부가 거둔 작물을 넣는 상자. 눌러서 거둬요. 밭 가까이 두면 농부가 덜 걸어요.", FarmPartKind.Crate, "FARM_Crate",
            1, 50, 4, 0, 1f, true, 4, 0.5f),
        ("bld_farm_path", "흙길", "농부가 길 위에서는 빨리 걸어요. 밭과 상자를 이어 주세요.", FarmPartKind.Path, "FARM_Path_Dirt",
            1, 0, 0, 1, 1f, true, 0, 0f),
        ("bld_farm_sprinkler", "스프링클러", "둘레 밭에 물을 뿌려 작물이 빨리 자라요.", FarmPartKind.Sprinkler, "FARM_Sprinkler",
            1, 300, 4, 6, 20f, false, 0, 0.3f),
    };

    [MenuItem("Tools/Settlement/Apply Plaza Farm")]
    public static void ApplyPlazaFarm()
    {
        ImportFarmArt();
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>("Assets/Scriptable Obejects/Decor/DecorCatalog.asset");
        foreach (var row in FarmPartTable)
            AddToList(catalog, "_buildings", CreateFarmPart(row, wood, stone));

        var config = CreatePlazaFarmConfig(wood, stone);
        var settlement = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var so = new SerializedObject(settlement);
        so.FindProperty("_farm").objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settlement);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementSetup] 광장 농장: 부품 4종 · 농장 설정 적용 완료");
    }

    private static void ImportFarmArt()
    {
        foreach (var path in Directory.GetFiles(FarmArtFolder, "*.png"))
        {
            string p = path.Replace('\\', '/');
            AssetDatabase.ImportAsset(p);
            var importer = (TextureImporter)AssetImporter.GetAtPath(p);
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            int cells = Path.GetFileName(p).StartsWith("FARM_Soil") ? 3 : 1;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = width / (float)cells;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static Sprite FarmArt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{FarmArtFolder}/{name}.png");

    private static BuildingDefinition CreateFarmPart(
        (string id, string name, string desc, FarmPartKind kind, string art, int size, int gold, int wood, int stone,
            float seconds, bool instant, int max, float growth) row, ItemDefinition wood, ItemDefinition stone)
    {
        Directory.CreateDirectory(FarmPartFolder);
        string path = $"{FarmPartFolder}/Building_{row.id}.asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }
        var sprite = FarmArt(row.art);
        var so = new SerializedObject(building);
        so.FindProperty("_buildingId").stringValue = row.id;
        so.FindProperty("_buildingName").stringValue = row.name;
        so.FindProperty("_description").stringValue = row.desc;
        so.FindProperty("_icon").objectReferenceValue = sprite;
        so.FindProperty("_worldSprite").objectReferenceValue = sprite;
        so.FindProperty("_footprint").vector2IntValue = new Vector2Int(row.size, row.size);
        // 농장 부품은 정사각형이라 돌리지 않음
        so.FindProperty("_canRotate").boolValue = false;
        so.FindProperty("_kind").enumValueIndex = (int)BuildingKind.Facility;
        so.FindProperty("_requiredLevel").intValue = 0;
        so.FindProperty("_requiredDevelopment").stringValue = "farmland";
        so.FindProperty("_maxCount").intValue = row.max;
        so.FindProperty("_gold").intValue = row.gold;
        var items = so.FindProperty("_items");
        var costs = new List<(ItemDefinition, int)>();
        if (row.wood > 0) costs.Add((wood, row.wood));
        if (row.stone > 0) costs.Add((stone, row.stone));
        items.arraySize = costs.Count;
        for (int i = 0; i < costs.Count; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue = costs[i].Item1;
            items.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = costs[i].Item2;
        }
        so.FindProperty("_costGrowth").floatValue = row.growth;
        so.FindProperty("_buildSeconds").floatValue = row.seconds;
        so.FindProperty("_district").enumValueIndex = (int)DistrictType.Agricultural;
        so.FindProperty("_farmPart").enumValueIndex = (int)row.kind;
        so.FindProperty("_instant").boolValue = row.instant;
        so.FindProperty("_widthFill").floatValue = 1f;
        so.FindProperty("_bonuses").arraySize = 0;
        so.FindProperty("_traits").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(building);
        return building;
    }

    private static PlazaFarmConfig CreatePlazaFarmConfig(ItemDefinition wood, ItemDefinition stone)
    {
        var config = AssetDatabase.LoadAssetAtPath<PlazaFarmConfig>(PlazaFarmConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PlazaFarmConfig>();
            AssetDatabase.CreateAsset(config, PlazaFarmConfigPath);
        }
        var so = new SerializedObject(config);
        var soils = so.FindProperty("_soils");
        // (이름, 시간 배수, 하나 더, 필요한 건물, 필요한 발전, 골드, 목재, 돌, 그림)
        var rows = new[]
        {
            ("보통 흙", 1f, 0f, "", "", 0, 0, 0, "FARM_Soil_0"),
            ("기름진 흙", 0.8f, 0.05f, "bld_granary", "", 300, 10, 0, "FARM_Soil_1"),
            ("황금 흙", 0.6f, 0.15f, "", CastleRules.Built, 1500, 0, 20, "FARM_Soil_2"),
        };
        soils.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, duration, bonus, building, development, gold, w, s, art) = rows[i];
            var e = soils.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("_name").stringValue = name;
            e.FindPropertyRelative("_durationMultiplier").floatValue = duration;
            e.FindPropertyRelative("_bonusChance").floatValue = bonus;
            e.FindPropertyRelative("_requiredBuilding").stringValue = building;
            e.FindPropertyRelative("_requiredDevelopment").stringValue = development;
            e.FindPropertyRelative("_gold").intValue = gold;
            var items = e.FindPropertyRelative("_items");
            var costs = new List<(ItemDefinition, int)>();
            if (w > 0) costs.Add((wood, w));
            if (s > 0) costs.Add((stone, s));
            items.arraySize = costs.Count;
            for (int k = 0; k < costs.Count; k++)
            {
                items.GetArrayElementAtIndex(k).FindPropertyRelative("_item").objectReferenceValue = costs[k].Item1;
                items.GetArrayElementAtIndex(k).FindPropertyRelative("_amount").intValue = costs[k].Item2;
            }
            e.FindPropertyRelative("_sprite").objectReferenceValue = FarmArt(art);
        }
        Unlock(so, "_fastBuild", "bld_workyard", "건설소");
        Unlock(so, "_bigCrate", "bld_granary", "농업 창고");
        Unlock(so, "_cart", "bld_workyard", "건설소");
        Unlock(so, "_pathUnlock", "bld_granary", "농업 창고");
        Unlock(so, "_sprinklerUnlock", "bld_workshop", "목공소");
        Unlock(so, "_stonePath", "bld_quarry", "채석 작업소");
        so.FindProperty("_stonePathSprite").objectReferenceValue = FarmArt("FARM_Path_Stone");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void Unlock(SerializedObject so, string field, string building, string label)
    {
        var p = so.FindProperty(field);
        p.FindPropertyRelative("_requiredBuilding").stringValue = building;
        p.FindPropertyRelative("_label").stringValue = label;
    }
}
