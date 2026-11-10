using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 번영의 시대 산업 데이터 만들기 (개발자 A, Docs/번영의시대_개발분담.md):
/// 판재 · 석재 블록 · 금속 주괴 · 정밀 부품 + 목공소(판재 · 정밀 공방) · 채석 작업소 · 건설소(제련) 가공 레시피(광장 소품 그림 포함)
/// + 주택 개량 1 · 2단계 그림 · 비용 + 마을 정비 만들기(왕국 표지판 · 가로등 · 분수 · 꽃밭) + 광장 돌길 · 큰길 공사
/// + Resources/IndustryConfig. 여러 번 돌려도 같음.
/// 그림: python Tools/UIGen/industry_icons.py · industry_art.py · plaza_paving.py (임시 그림, 돌길 자리는 paving_placement.json)
/// </summary>
public static class IndustrySetup
{
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Industry";
    private const string DecorItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Decor";
    private const string DecorFolder = "Assets/Scriptable Obejects/Decor/Decors";
    private const string CatalogPath = "Assets/Scriptable Obejects/Decor/DecorCatalog.asset";
    private const string RecipeFolder = "Assets/Scriptable Obejects/Industry";
    private const string ArtFolder = "Assets/Art/Item/Industry";
    private const string PropFolder = "Assets/Art/Industry";
    private const string SettlementArtFolder = "Assets/Art/Settlement";
    private const string HouseArtFolder = "Assets/Art/Buildings/Detail4";
    private const string ConfigPath = "Assets/Resources/IndustryConfig.asset";
    private const string PavingPlacementPath = "Tools/UIGen/paving_placement.json";
    private const string WoodPath = "Assets/Scriptable Obejects/Inventory/Items/Mining/목재.asset";
    private const string StonePath = "Assets/Scriptable Obejects/Inventory/Items/Mining/돌.asset";
    private const string CopperPath = "Assets/Scriptable Obejects/Inventory/Items/Mining/MineItem_ore_shell_copper.asset";
    private const string LampPath = "Assets/Scriptable Obejects/Inventory/Items/Decor/가로등.asset";
    private const string WorkshopPath = "Assets/Scriptable Obejects/Decor/Buildings/Building_Workshop.asset";
    private const string QuarryPath = "Assets/Scriptable Obejects/Decor/Buildings/Building_Quarry.asset";
    private const string WorkyardPath = "Assets/Scriptable Obejects/Decor/Buildings/Building_Workyard.asset";

    // (아이템 ID, 이름, 설명, 그림)
    private static readonly (string id, string name, string desc, string art)[] Items =
    {
        ("plank", "판재", "목공소에서 목재를 반듯하게 켠 널빤지. 왕국 창고에 모여 집 개량 · 공방 · 수레에 쓰여요.", "ICON_Item_Plank"),
        ("stone_block", "석재 블록", "채석 작업소에서 돌을 네모나게 다듬은 블록. 왕국 창고에 모여 길 · 광장 · 공공시설에 쓰여요.", "ICON_Item_StoneBlock"),
        ("ingot", "금속 주괴", "건설소 가마에서 조개 구리를 녹여 부은 주괴. 왕국 창고에 모여 마을 주택 · 분수 · 고급 설비에 쓰여요.", "ICON_Item_Ingot"),
        ("precision_part", "정밀 부품", "목공소 정밀 공방에서 판재와 주괴로 깎아 만든 톱니 · 나사. 학술원 · 왕궁 같은 큰 사업에 쓰여요.", "ICON_Item_Precision"),
    };

    // (레시피 에셋, 시설, 기술, 원재료, 원재료 수, 가공품, 초, 처음 만든 기록, 작업대, 더미, 수레) — 기획서 6.2 · 6.3
    private static readonly (string asset, string building, string tech, string input, int count, string output, float seconds, string first,
        string bench, string pile, string cart)[] Recipes =
    {
        ("Recipe_Plank", WorkshopPath, TechIds.Sawmill, WoodPath, 4, "plank", 360f, "first_plank", "Prop_Sawhorse", "Pile_Plank", "Cart_Plank"),
        ("Recipe_StoneBlock", QuarryPath, TechIds.StoneCutting, StonePath, 6, "stone_block", 360f, "first_stone_block",
            "Prop_StoneBench", "Pile_StoneBlock", "Cart_StoneBlock"),
        // 제련 공방 = 건설소에 붙인 가마 (새 건물 대신 기존 건물 업그레이드, 기획서 6.4). 재료는 갱도의 조개 구리 (가방에서)
        ("Recipe_Ingot", WorkyardPath, TechIds.Smelting, CopperPath, 3, "ingot", 480f, "first_ingot", "Prop_Kiln", "Pile_Ingot", "Cart_Ingot"),
        // 정밀 공방 = 목공소 고급화 (둘째 줄, 기획서 6.4). 판재 2 + 주괴 2를 창고에서 바로 → 정밀 부품, 12분
        ("Recipe_Precision", WorkshopPath, TechIds.PrecisionWork, "item:plank", 2, "precision_part", 720f, "first_precision_part",
            "", "Pile_Precision", "Cart_Precision"),
    };

    // 레시피 덧붙임: (가공 줄 ID, 둘째 재료 아이템 ID, 수, 창고에서 바로, 소품 자리)
    private static readonly Dictionary<string, (string processorId, string extra, int extraCount, bool fromWarehouse, int slot)> RecipeExtras =
        new Dictionary<string, (string, string, int, bool, int)>
        {
            { "Recipe_Precision", ("bld_workshop_precision", "ingot", 2, true, 1) },
        };

    // 개량 주택 그림 (집마다 번갈아) — 쓰지 않던 Detail4 집 그림. 마을 주택 미리보기도 같은 순서
    private static readonly string[] ImprovedHouses = { "HouseRed", "HouseBlue" };
    private static readonly string[] TownHouseIcons = { "TownHouse_Red", "TownHouse_Blue" };

    // 마을 정비 꾸미기: (아이템 ID, 에셋 이름, 이름, 설명, 그림, 칸, 모이는 자리, 아늑함)
    private static readonly (string id, string asset, string name, string desc, string art, Vector2Int footprint, bool gather, int cozy)[] Furniture =
    {
        ("furniture_kingdom_sign", "KingdomSign", "왕국 표지판", "판재로 만든 조개 문장 표지판. 길목에 세우면 계획된 마을처럼 보여요.",
            "Prop_KingdomSign", Vector2Int.one, false, 3),
        ("furniture_fountain", "Fountain", "광장 분수", "석재 블록과 금속 주괴로 만든 분수. 밤이면 해달들이 둘레에 모여 이야기해요.",
            "Prop_Fountain", new Vector2Int(2, 2), true, 10),
        ("furniture_flowerbed", "Flowerbed", "돌 꽃밭", "석재 블록으로 두른 꽃밭. 길가에 두면 광장이 화사해져요.",
            "Prop_Flowerbed", new Vector2Int(2, 1), false, 4),
    };

    // 광장 바닥 공사: (ID, 이름, 설명, 기술, 먼저 깔 공사, 석재, 판재, 골드, 아이콘, 조각 이름 앞부분)
    private static readonly (string id, string name, string desc, string tech, string requires, int stone, int plank, int gold, string icon, string prefix)[] Pavings =
    {
        ("town_paving_plaza_center", "중앙 광장 돌길", "광장 가운데 흙 마당에 돌을 깔아요", TechIds.TownPlanning, "", 12, 0, 1500, "ICON_Paving", "PlazaCenter"),
        ("town_paving_roads", "왕국 큰길", "광장에서 뻗은 흙길을 따라 돌을 깔아요", TechIds.Paving, "town_paving_plaza_center", 20, 0, 3000,
            "ICON_PavingRoad", "Road_"),
    };

    [MenuItem("Tools/Industry/Create Data")]
    public static void CreateData()
    {
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodPath);
        var lamp = AssetDatabase.LoadAssetAtPath<ItemDefinition>(LampPath);
        if (wood == null || lamp == null)
        {
            Debug.LogError($"[IndustrySetup] 목재 · 가로등 아이템이 없습니다: {WoodPath} · {LampPath}");
            return;
        }
        Directory.CreateDirectory(ItemFolder);
        Directory.CreateDirectory(RecipeFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));

        var items = new Dictionary<string, ItemDefinition>();
        foreach (var row in Items)
            items[row.id] = MakeItem(row, wood);

        var recipes = new List<ProcessingRecipeDefinition>();
        foreach (var row in Recipes)
            recipes.Add(MakeRecipe(row, items));

        var furniture = new Dictionary<string, ItemDefinition>();
        foreach (var row in Furniture)
            furniture[row.id] = MakeFurniture(row, lamp);

        var config = AssetDatabase.LoadAssetAtPath<IndustryConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<IndustryConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.EditorSet(recipes);
        var so = new SerializedObject(config);
        so.FindProperty("_emptyCartSprite").objectReferenceValue = Prop("Cart_Empty");
        so.FindProperty("_cartPennant").objectReferenceValue = Prop("Cart_Pennant");
        so.FindProperty("_warehouseSprite").objectReferenceValue = Prop("Prop_WarehouseCrates");
        SetSprites(so.FindProperty("_improvedHouseSprites"), HouseArtFolder, ImprovedHouses);
        SetSprites(so.FindProperty("_townHouseIcons"), PropFolder, TownHouseIcons);
        so.FindProperty("_porchLamp").objectReferenceValue = CollectionSetup.ImportSprite(SettlementArtFolder, "ICON_Lamp");
        so.FindProperty("_porchFlowers").objectReferenceValue = CollectionSetup.ImportSprite(SettlementArtFolder, "Prop_FlowerPot");
        so.ApplyModifiedPropertiesWithoutUndo();
        // 마을 정비 만들기 (C01 표지판 · 가로등, C04 분수 · 꽃밭)
        SetCrafts(config, new List<CraftRecipe>
        {
            new CraftRecipe(furniture["furniture_kingdom_sign"], TechIds.TownPlanning, 2, 0, 200),
            new CraftRecipe(lamp, TechIds.TownPlanning, 1, 1, 100),
            new CraftRecipe(furniture["furniture_flowerbed"], TechIds.PublicSpaces, 0, 3, 400),
            new CraftRecipe(furniture["furniture_fountain"], TechIds.PublicSpaces, 0, 8, 1500, 2),
        });
        SetPavings(config);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[IndustrySetup] 아이템 {items.Count} · 레시피 {recipes.Count} · 꾸미기 {furniture.Count} · 돌길 {Pavings.Length} · {ConfigPath}");
    }

    private static Sprite Prop(string name) => CollectionSetup.ImportSprite(PropFolder, name);

    private static void SetSprites(SerializedProperty list, string folder, string[] names)
    {
        list.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = CollectionSetup.ImportSprite(folder, names[i]);
    }

    private static ItemDefinition MakeItem((string id, string name, string desc, string art) row, ItemDefinition wood)
    {
        string path = $"{ItemFolder}/Industry_{row.id}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = row.id;
        so.FindProperty("_displayName").stringValue = row.name;
        so.FindProperty("_description").stringValue = row.desc;
        so.FindProperty("_icon").objectReferenceValue = CollectionSetup.ImportSprite(ArtFolder, row.art);
        so.FindProperty("_category").objectReferenceValue = wood.Category;
        so.FindProperty("_rarity").objectReferenceValue = wood.Rarity;
        // 왕국 창고 전용 (가방 · 판매 목록에 안 들어감)
        so.FindProperty("_isSellable").boolValue = false;
        so.FindProperty("_sellPrice").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static ProcessingRecipeDefinition MakeRecipe(
        (string asset, string building, string tech, string input, int count, string output, float seconds, string first,
            string bench, string pile, string cart) row, Dictionary<string, ItemDefinition> items)
    {
        var output = items[row.output];
        // 재료: 에셋 경로, 또는 "item:ID" = 이번에 만든 창고 자재
        var input = row.input.StartsWith("item:") ? items[row.input.Substring(5)] : AssetDatabase.LoadAssetAtPath<ItemDefinition>(row.input);
        string path = $"{RecipeFolder}/{row.asset}.asset";
        var recipe = AssetDatabase.LoadAssetAtPath<ProcessingRecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<ProcessingRecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }
        var so = new SerializedObject(recipe);
        so.FindProperty("_building").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(row.building);
        so.FindProperty("_techId").stringValue = row.tech;
        so.FindProperty("_input").objectReferenceValue = input;
        so.FindProperty("_inputCount").intValue = row.count;
        so.FindProperty("_output").objectReferenceValue = output;
        so.FindProperty("_cycleSeconds").floatValue = row.seconds;
        so.FindProperty("_firstDevelopment").stringValue = row.first;
        so.FindProperty("_benchSprite").objectReferenceValue = string.IsNullOrEmpty(row.bench) ? null : Prop(row.bench);
        RecipeExtras.TryGetValue(row.asset, out var extra);
        so.FindProperty("_processorId").stringValue = extra.processorId ?? "";
        so.FindProperty("_extraInput").objectReferenceValue = extra.extra != null ? items[extra.extra] : null;
        so.FindProperty("_extraInputCount").intValue = extra.extraCount;
        so.FindProperty("_fromWarehouse").boolValue = extra.fromWarehouse;
        so.FindProperty("_propSlot").intValue = extra.slot;
        so.FindProperty("_cartSprite").objectReferenceValue = Prop(row.cart);
        var piles = so.FindProperty("_pileSprites");
        piles.arraySize = 3;
        for (int i = 0; i < 3; i++)
            piles.GetArrayElementAtIndex(i).objectReferenceValue = Prop($"{row.pile}_{i}");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        if (!recipe.IsValid)
            Debug.LogError($"[IndustrySetup] {row.asset}: 시설 · 재료가 비었습니다", recipe);
        return recipe;
    }

    // 마을 정비 꾸미기: 아이템(가로등과 같은 분류) + 놓는 꾸미기 + 꾸미기 목록에 넣음
    private static ItemDefinition MakeFurniture(
        (string id, string asset, string name, string desc, string art, Vector2Int footprint, bool gather, int cozy) row, ItemDefinition lamp)
    {
        var sprite = Prop(row.art);
        string itemPath = $"{DecorItemFolder}/{row.name}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, itemPath);
        }
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = row.id;
        so.FindProperty("_displayName").stringValue = row.name;
        so.FindProperty("_description").stringValue = row.desc;
        so.FindProperty("_icon").objectReferenceValue = sprite;
        so.FindProperty("_category").objectReferenceValue = lamp.Category;
        so.FindProperty("_rarity").objectReferenceValue = lamp.Rarity;
        so.FindProperty("_isSellable").boolValue = false;
        so.FindProperty("_sellPrice").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        string decorPath = $"{DecorFolder}/Decor_Furniture_{row.asset}.asset";
        var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(decorPath);
        if (decor == null)
        {
            decor = ScriptableObject.CreateInstance<DecorDefinition>();
            AssetDatabase.CreateAsset(decor, decorPath);
        }
        var decorSo = new SerializedObject(decor);
        decorSo.FindProperty("_item").objectReferenceValue = item;
        decorSo.ApplyModifiedPropertiesWithoutUndo();
        decor.SetupPlacement(row.footprint, false, sprite);
        // 분수: 가로등처럼 밤에 둘레에 모임 (차지한 칸 비율 자리)
        var spots = row.gather ? new[] { new Vector2(-0.2f, 0.3f), new Vector2(1.2f, 0.3f), new Vector2(0.5f, -0.35f) } : null;
        decor.SetupFurniture(true, row.gather, row.gather ? PlazaSpotKind.Gather : PlazaSpotKind.Sit, spots, row.cozy);
        EditorUtility.SetDirty(decor);
        AddToCatalog(decor);
        return item;
    }

    private static void AddToCatalog(DecorDefinition decor)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[IndustrySetup] 꾸미기 목록이 없습니다: {CatalogPath}");
            return;
        }
        var catalogSo = new SerializedObject(catalog);
        var decors = catalogSo.FindProperty("_decors");
        for (int i = 0; i < decors.arraySize; i++)
        {
            if (decors.GetArrayElementAtIndex(i).objectReferenceValue == decor)
                return;
        }
        decors.arraySize++;
        decors.GetArrayElementAtIndex(decors.arraySize - 1).objectReferenceValue = decor;
        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    // 광장 바닥 공사: 조각 그림 · 자리는 plaza_paving.py가 쓴 paving_placement.json
    private static void SetPavings(IndustryConfig config)
    {
        if (!File.Exists(PavingPlacementPath))
        {
            Debug.LogError($"[IndustrySetup] 돌길 자리가 없습니다: {PavingPlacementPath} (python Tools/UIGen/plaza_paving.py)");
            return;
        }
        var placements = ParsePlacements(File.ReadAllText(PavingPlacementPath));
        var so = new SerializedObject(config);
        var list = so.FindProperty("_pavings");
        list.arraySize = Pavings.Length;
        for (int p = 0; p < Pavings.Length; p++)
        {
            var info = Pavings[p];
            var row = list.GetArrayElementAtIndex(p);
            row.FindPropertyRelative("_id").stringValue = info.id;
            row.FindPropertyRelative("_displayName").stringValue = info.name;
            row.FindPropertyRelative("_description").stringValue = info.desc;
            row.FindPropertyRelative("_techId").stringValue = info.tech;
            row.FindPropertyRelative("_requires").stringValue = info.requires;
            row.FindPropertyRelative("_stoneBlocks").intValue = info.stone;
            row.FindPropertyRelative("_planks").intValue = info.plank;
            row.FindPropertyRelative("_gold").intValue = info.gold;
            row.FindPropertyRelative("_icon").objectReferenceValue = Prop(info.icon);
            var pieces = row.FindPropertyRelative("_pieces");
            var names = new List<string>();
            foreach (var key in placements.Keys)
            {
                if (key == info.prefix || (info.prefix.EndsWith("_") && key.StartsWith(info.prefix)))
                    names.Add(key);
            }
            names.Sort(System.StringComparer.Ordinal);
            pieces.arraySize = names.Count;
            for (int i = 0; i < names.Count; i++)
            {
                var (center, size) = placements[names[i]];
                var piece = pieces.GetArrayElementAtIndex(i);
                piece.FindPropertyRelative("_sprite").objectReferenceValue = ImportLarge($"{PropFolder}/Paving_{names[i]}.png");
                piece.FindPropertyRelative("_mapCenter").vector2Value = center;
                piece.FindPropertyRelative("_mapSize").vector2Value = size;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [System.Serializable]
    private class PlacementRow
    {
        public string name;
        public float u, v, width, height;
    }

    // {"이름": {"u": .., "v": .., "width": .., "height": ..}, ...} — JsonUtility가 사전을 못 읽어 직접 나눔
    private static Dictionary<string, (Vector2 center, Vector2 size)> ParsePlacements(string json)
    {
        var result = new Dictionary<string, (Vector2, Vector2)>();
        var entries = System.Text.RegularExpressions.Regex.Matches(json, "\"([A-Za-z0-9_]+)\"\\s*:\\s*(\\{[^}]*\\})");
        foreach (System.Text.RegularExpressions.Match entry in entries)
        {
            var row = JsonUtility.FromJson<PlacementRow>(entry.Groups[2].Value);
            result[entry.Groups[1].Value] = (new Vector2(row.u, row.v), new Vector2(row.width, row.height));
        }
        return result;
    }

    // 지도 위에 까는 큰 그림 (최대 3.6k px): 줄이지 않게 4096까지, 사각 메시
    private static Sprite ImportLarge(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogError($"[IndustrySetup] 그림이 없습니다: {path} (python Tools/UIGen/plaza_paving.py)");
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        // 돌 수천 개 모양으로 촘촘한 메시 · 물리 외곽선을 만들지 않게 (사각 한 장)
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetCrafts(IndustryConfig config, List<CraftRecipe> crafts)
    {
        var field = typeof(IndustryConfig).GetField("_crafts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(config, crafts);
    }

    // ---------------------------------------------------------------- 개발 도구 (플레이 중)

    private static readonly string[] P0Techs =
    {
        TechIds.Sawmill, TechIds.StoneCutting, TechIds.Warehouse, TechIds.Cart, TechIds.TownPlanning, TechIds.BetterHouses,
    };

    private static readonly string[] P1Techs =
    {
        TechIds.Smelting, TechIds.StandardTools, TechIds.WaterPower, TechIds.PrecisionWork, TechIds.Dispatch, TechIds.AutoSupply,
        TechIds.Depot, TechIds.BetterCart, TechIds.Paving, TechIds.PublicSpaces, TechIds.Administration, TechIds.IndustrialLogistics,
    };

    [MenuItem("Tools/Industry/Dev — Learn P0 Techs (Play)")]
    public static void DevLearnP0() => DevLearn(P0Techs);

    [MenuItem("Tools/Industry/Dev — Learn P0 + P1 Techs (Play)")]
    public static void DevLearnP1()
    {
        DevLearn(P0Techs);
        DevLearn(P1Techs);
    }

    private static void DevLearn(string[] techs)
    {
        foreach (var id in techs)
            TechGate.DevLearn(id);
        Debug.Log("[IndustrySetup] 개발용으로 배움: " + string.Join(", ", techs));
    }

    [MenuItem("Tools/Industry/Dev — Learn P0 Techs (Play)", true)]
    [MenuItem("Tools/Industry/Dev — Learn P0 + P1 Techs (Play)", true)]
    private static bool DevLearnValid() => Application.isPlaying;
}
