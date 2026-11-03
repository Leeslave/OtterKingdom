using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 번영의 시대 산업 데이터 만들기 (개발자 A, Docs/번영의시대_개발분담.md):
/// 판재 · 석재 블록 아이템 + 목공소 · 채석 작업소 가공 레시피(광장 소품 그림 포함) + 주택 개량 그림 · 비용
/// + 마을 정비 만들기(왕국 표지판 꾸미기 · 가로등) + Resources/IndustryConfig. 여러 번 돌려도 같음.
/// 그림: python Tools/UIGen/industry_icons.py · industry_art.py (임시 그림)
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
    private const string HouseArtFolder = "Assets/Art/Buildings/Detail4";
    private const string ConfigPath = "Assets/Resources/IndustryConfig.asset";
    private const string WoodPath = "Assets/Scriptable Obejects/Inventory/Items/Mining/목재.asset";
    private const string StonePath = "Assets/Scriptable Obejects/Inventory/Items/Mining/돌.asset";
    private const string LampPath = "Assets/Scriptable Obejects/Inventory/Items/Decor/가로등.asset";
    private const string WorkshopPath = "Assets/Scriptable Obejects/Decor/Buildings/Building_Workshop.asset";
    private const string QuarryPath = "Assets/Scriptable Obejects/Decor/Buildings/Building_Quarry.asset";

    // (아이템 ID, 이름, 설명, 그림)
    private static readonly (string id, string name, string desc, string art)[] Items =
    {
        ("plank", "판재", "목공소에서 목재를 반듯하게 켠 널빤지. 왕국 창고에 모여 집 개량 · 공방 · 수레에 쓰여요.", "ICON_Item_Plank"),
        ("stone_block", "석재 블록", "채석 작업소에서 돌을 네모나게 다듬은 블록. 왕국 창고에 모여 길 · 광장 · 공공시설에 쓰여요.", "ICON_Item_StoneBlock"),
    };

    // (레시피 에셋, 시설, 기술, 원재료, 원재료 수, 가공품, 초, 처음 만든 기록, 작업대, 더미, 수레) — 기획서 6.2 · 6.3
    private static readonly (string asset, string building, string tech, string input, int count, string output, float seconds, string first,
        string bench, string pile, string cart)[] Recipes =
    {
        ("Recipe_Plank", WorkshopPath, TechIds.Sawmill, WoodPath, 4, "plank", 360f, "first_plank", "Prop_Sawhorse", "Pile_Plank", "Cart_Plank"),
        ("Recipe_StoneBlock", QuarryPath, TechIds.StoneCutting, StonePath, 6, "stone_block", 360f, "first_stone_block",
            "Prop_StoneBench", "Pile_StoneBlock", "Cart_StoneBlock"),
    };

    // 개량 주택 그림 (집마다 번갈아) — 쓰지 않던 Detail4 집 그림
    private static readonly string[] ImprovedHouses = { "HouseRed", "HouseBlue" };

    // 왕국 표지판 (마을 정비 C01 꾸미기)
    private const string SignItemId = "furniture_kingdom_sign";
    private const string SignItemPath = DecorItemFolder + "/왕국 표지판.asset";
    private const string SignDecorPath = DecorFolder + "/Decor_Furniture_KingdomSign.asset";

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
            recipes.Add(MakeRecipe(row, items[row.output]));

        var sign = MakeSign(lamp);

        var config = AssetDatabase.LoadAssetAtPath<IndustryConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<IndustryConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.EditorSet(recipes);
        var so = new SerializedObject(config);
        so.FindProperty("_emptyCartSprite").objectReferenceValue = Prop("Cart_Empty");
        so.FindProperty("_warehouseSprite").objectReferenceValue = Prop("Prop_WarehouseCrates");
        var houses = so.FindProperty("_improvedHouseSprites");
        houses.arraySize = ImprovedHouses.Length;
        for (int i = 0; i < ImprovedHouses.Length; i++)
            houses.GetArrayElementAtIndex(i).objectReferenceValue = CollectionSetup.ImportSprite(HouseArtFolder, ImprovedHouses[i]);
        so.ApplyModifiedPropertiesWithoutUndo();
        // 마을 정비 만들기: 왕국 표지판 (판재 2) · 가로등 (판재 1 + 석재 블록 1)
        SetCrafts(config, new List<CraftRecipe>
        {
            new CraftRecipe(sign, TechIds.TownPlanning, 2, 0, 200),
            new CraftRecipe(lamp, TechIds.TownPlanning, 1, 1, 100),
        });
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[IndustrySetup] 아이템 {items.Count} · 레시피 {recipes.Count} · 왕국 표지판 · {ConfigPath}");
    }

    private static Sprite Prop(string name) => CollectionSetup.ImportSprite(PropFolder, name);

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
            string bench, string pile, string cart) row, ItemDefinition output)
    {
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
        so.FindProperty("_input").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(row.input);
        so.FindProperty("_inputCount").intValue = row.count;
        so.FindProperty("_output").objectReferenceValue = output;
        so.FindProperty("_cycleSeconds").floatValue = row.seconds;
        so.FindProperty("_firstDevelopment").stringValue = row.first;
        so.FindProperty("_benchSprite").objectReferenceValue = Prop(row.bench);
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

    // 왕국 표지판: 아이템(가로등과 같은 분류) + 놓는 꾸미기 + 꾸미기 목록에 넣음
    private static ItemDefinition MakeSign(ItemDefinition lamp)
    {
        var sprite = Prop("Prop_KingdomSign");
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SignItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, SignItemPath);
        }
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = SignItemId;
        so.FindProperty("_displayName").stringValue = "왕국 표지판";
        so.FindProperty("_description").stringValue = "판재로 만든 조개 문장 표지판. 길목에 세우면 계획된 마을처럼 보여요.";
        so.FindProperty("_icon").objectReferenceValue = sprite;
        so.FindProperty("_category").objectReferenceValue = lamp.Category;
        so.FindProperty("_rarity").objectReferenceValue = lamp.Rarity;
        so.FindProperty("_isSellable").boolValue = false;
        so.FindProperty("_sellPrice").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(SignDecorPath);
        if (decor == null)
        {
            decor = ScriptableObject.CreateInstance<DecorDefinition>();
            AssetDatabase.CreateAsset(decor, SignDecorPath);
        }
        var decorSo = new SerializedObject(decor);
        decorSo.FindProperty("_item").objectReferenceValue = item;
        decorSo.ApplyModifiedPropertiesWithoutUndo();
        decor.SetupPlacement(Vector2Int.one, false, sprite);
        decor.SetupFurniture(true, false, PlazaSpotKind.Sit, null, 3);
        EditorUtility.SetDirty(decor);

        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(CatalogPath);
        if (catalog != null)
        {
            var catalogSo = new SerializedObject(catalog);
            var decors = catalogSo.FindProperty("_decors");
            bool found = false;
            for (int i = 0; i < decors.arraySize; i++)
                found |= decors.GetArrayElementAtIndex(i).objectReferenceValue == decor;
            if (!found)
            {
                decors.arraySize++;
                decors.GetArrayElementAtIndex(decors.arraySize - 1).objectReferenceValue = decor;
                catalogSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }
        }
        else
        {
            Debug.LogError($"[IndustrySetup] 꾸미기 목록이 없습니다: {CatalogPath}");
        }
        return item;
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

    [MenuItem("Tools/Industry/Dev — Learn P0 Techs (Play)")]
    public static void DevLearnP0()
    {
        foreach (var id in P0Techs)
            TechGate.DevLearn(id);
        Debug.Log("[IndustrySetup] 개발용으로 배움: " + string.Join(", ", P0Techs));
    }

    [MenuItem("Tools/Industry/Dev — Learn P0 Techs (Play)", true)]
    private static bool DevLearnP0Valid() => Application.isPlaying;
}
