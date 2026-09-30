using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates the mining data the mine scene needs and hooks it into the shared
// assets. Run as part of OtterKingdom > Tools > Setup Mine Scene.
//   - MiningBalanceData asset, assigned to the GameManager prefab
//   - "광산" bag tab + "광석" chip (ItemCategory), added to the bag
//     presenter's category list on the GlobalUI prefab and on
//     InventoryTestScene (Build Global UI copies its bag from there)
//   - 다이아몬드 (ore_diamond) and 돌 (ore_stone) ItemDefinitions, registered
//     in the ItemDatabase
// Assets that already exist are left as they are (Inspector tweaks such as
// prices survive a re-run); only missing links are added.
public static class MiningDataSetup
{
    private const string BalancePath = "Assets/Data/Mining/MiningBalanceData.asset";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";
    private const string GlobalUIPrefabPath = "Assets/Resources/GlobalUI.prefab";
    private const string InventoryScenePath = "Assets/Scenes/InventoryTestScene.unity";
    private const string CategoryDir = "Assets/Scriptable Obejects/Inventory/Category";
    private const string ItemDir = "Assets/Scriptable Obejects/Inventory/Items/Mining";
    private const string RarityDir = "Assets/Scriptable Obejects/Inventory/Rarity";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string DiamondIconPath = "Assets/Art/Item/Ore/Diamond.png";
    private const string StoneIconPath = "Assets/Art/Item/Ore/Stone.png";

    // Tabs: 농사 0, 낚시 1, 광산 2.
    private const int MiningTabSortOrder = 2;

    public static bool Run()
    {
        var balance = LoadOrCreateBalance();

        var miningTab = LoadOrCreateCategory("Mining", "Mining", "광산", MiningTabSortOrder, null);
        var ore = LoadOrCreateCategory("Ore", "Ore", "광석", 0, miningTab);

        var common = AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityDir}/Common.asset");
        var rare = AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityDir}/Rare.asset");
        var diamondIcon = ImportIcon(DiamondIconPath);
        var stoneIcon = ImportIcon(StoneIconPath);
        if (common == null || rare == null || diamondIcon == null || stoneIcon == null)
        {
            Debug.LogError($"[MiningDataSetup] Missing asset: Common={common != null}, Rare={rare != null}, " +
                           $"Diamond icon={diamondIcon != null}, Stone icon={stoneIcon != null}.");
            return false;
        }

        var diamond = LoadOrCreateItem("다이아몬드", balance.diamondItemId, "다이아몬드",
            "광산 깊은 곳에서 캔 반짝이는 보석이다.", diamondIcon, ore, rare, 50);
        var stone = LoadOrCreateItem("돌", balance.stoneItemId, "돌",
            "광산에서 캔 평범한 돌이다.", stoneIcon, ore, common, 1);

        AddToDatabase(diamond, stone);
        AssignBalanceToGameManager(balance);
        AddCategoriesToGlobalUI(miningTab, ore);
        AddCategoriesToInventoryScene(miningTab, ore);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static MiningBalanceData LoadOrCreateBalance()
    {
        var balance = AssetDatabase.LoadAssetAtPath<MiningBalanceData>(BalancePath);
        if (balance != null) return balance;

        balance = ScriptableObject.CreateInstance<MiningBalanceData>();
        Directory.CreateDirectory(Path.GetDirectoryName(BalancePath));
        AssetDatabase.CreateAsset(balance, BalancePath);
        return balance;
    }

    private static ItemCategory LoadOrCreateCategory(string assetName, string id, string displayName, int sortOrder,
        ItemCategory parent)
    {
        string path = $"{CategoryDir}/{assetName}.asset";
        var category = AssetDatabase.LoadAssetAtPath<ItemCategory>(path);
        if (category != null) return category;

        category = ScriptableObject.CreateInstance<ItemCategory>();
        var so = new SerializedObject(category);
        so.FindProperty("_categoryId").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_parent").objectReferenceValue = parent;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(category, path);
        return category;
    }

    private static ItemDefinition LoadOrCreateItem(string assetName, string id, string displayName, string description,
        Sprite icon, ItemCategory category, ItemRarity rarity, int sellPrice)
    {
        string path = $"{ItemDir}/{assetName}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item != null) return item;

        Directory.CreateDirectory(ItemDir);
        item = ScriptableObject.CreateInstance<ItemDefinition>();
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_description").stringValue = description;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_category").objectReferenceValue = category;
        so.FindProperty("_rarity").objectReferenceValue = rarity;
        so.FindProperty("_isSellable").boolValue = true;
        so.FindProperty("_sellPrice").intValue = sellPrice;
        so.FindProperty("_maxStack").intValue = 999;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static Sprite ImportIcon(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void AddToDatabase(params ItemDefinition[] items)
    {
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database == null)
        {
            Debug.LogError($"[MiningDataSetup] {ItemDatabasePath} not found — add the ore items to the ItemDatabase by hand.");
            return;
        }

        var so = new SerializedObject(database);
        var list = so.FindProperty("_items");
        foreach (var item in items)
        {
            if (database.Items.Contains(item)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignBalanceToGameManager(MiningBalanceData balance)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPrefabPath) == null) return;

        var root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);
        var so = new SerializedObject(root.GetComponent<GameManager>());
        var field = so.FindProperty("miningBalance");
        if (field.objectReferenceValue == null)
        {
            field.objectReferenceValue = balance;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddCategoriesToGlobalUI(params ItemCategory[] categories)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIPrefabPath) == null) return;

        var root = PrefabUtility.LoadPrefabContents(GlobalUIPrefabPath);
        bool changed = false;
        foreach (var presenter in root.GetComponentsInChildren<InventoryPresenter>(true))
            changed |= AddCategories(presenter, categories);
        if (changed) PrefabUtility.SaveAsPrefabAsset(root, GlobalUIPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddCategoriesToInventoryScene(params ItemCategory[] categories)
    {
        if (!File.Exists(InventoryScenePath)) return;

        var scene = EditorSceneManager.OpenScene(InventoryScenePath, OpenSceneMode.Additive);
        bool changed = false;
        foreach (var go in scene.GetRootGameObjects())
        {
            foreach (var presenter in go.GetComponentsInChildren<InventoryPresenter>(true))
            {
                // The GlobalUI prefab instance there is handled through the prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(presenter)) continue;
                changed |= AddCategories(presenter, categories);
            }
        }
        if (changed) EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static bool AddCategories(InventoryPresenter presenter, ItemCategory[] categories)
    {
        var so = new SerializedObject(presenter);
        var list = so.FindProperty("_categories");
        var existing = Enumerable.Range(0, list.arraySize)
            .Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue)
            .ToList();

        bool changed = false;
        foreach (var category in categories)
        {
            if (existing.Contains(category)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = category;
            changed = true;
        }
        if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }
}
