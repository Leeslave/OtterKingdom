using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 가챠 개편 4단계: 장난감을 게임에서 없앤다 (Docs/가챠개편_특수해달조각_장신구_기획.md 6장). 한 번 실행하면 되고, 다시 실행해도 결과가 같다.
/// - 장난감 아이템(분류 Toy) · 그 꾸미기 물건 · 요정 상점 장난감 상품을 목록에서 빼고 에셋을 지움 (장난감 그림 파일은 남김)
/// - 배너의 장난감 목록 · 살랑이의 좋아하는 장난감을 비움, 요정 상점 · 가방의 "장난감" 탭을 뺌
/// - 퀘스트 · 방명록 · 도감 · 건물 · 반짝 조각 설명의 장난감 문구를 바꿈 (만드는 쪽 표도 이미 바꿈)
/// 옛 세이브의 장난감은 보상 없이 사라짐: 가방은 InventoryManager.IsRetiredItem, 놓인 것은 꾸미기 목록에서 빠져 건너뜀.
/// 배치 모드: -executeMethod ToyRemovalSetup.RemoveToys
/// </summary>
public static class ToyRemovalSetup
{
    private const string ToyCategoryPath = "Assets/Scriptable Obejects/Inventory/Category/Toy.asset";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string ShopCatalogPath = "Assets/Scriptable Obejects/Shop/ShopCatalog.asset";
    private const string BannerFolder = "Assets/Scriptable Obejects/Gacha/Banners";
    private const string QuestFolder = "Assets/Scriptable Obejects/Quest/Quests";
    private const string ShardPath = "Assets/Scriptable Obejects/Gacha/Currencies/Shard.asset";
    private const string SallangPath = "Assets/Scriptable Obejects/Settlement/Otters/Otter_Toy_Sallang.asset";
    private const string GlobalUIPath = "Assets/Resources/GlobalUI.prefab";

    [MenuItem("Tools/Gacha/Remove Toys")]
    public static void RemoveToys()
    {
        var toyCategory = AssetDatabase.LoadAssetAtPath<ItemCategory>(ToyCategoryPath);
        var toys = new HashSet<ItemDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && (item.Category == toyCategory && toyCategory != null || InventoryManager.IsRetiredItem(item.ItemId)))
                toys.Add(item);
        }
        var decors = new List<DecorDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:DecorDefinition"))
        {
            var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (decor != null && !(decor is BuildingDefinition) && decor.Item != null && toys.Contains(decor.Item))
                decors.Add(decor);
        }
        var products = new List<ShopProduct>();
        foreach (var guid in AssetDatabase.FindAssets("t:ShopProduct"))
        {
            var product = AssetDatabase.LoadAssetAtPath<ShopProduct>(AssetDatabase.GUIDToAssetPath(guid));
            if (product != null && product.Item != null && toys.Contains(product.Item))
                products.Add(product);
        }

        RemoveFrom(ItemDatabasePath, "_items", o => toys.Contains(o as ItemDefinition));
        RemoveFrom(DecorSetup.CatalogPath, "_decors", o => decors.Contains(o as DecorDefinition));
        RemoveFrom(ShopCatalogPath, "_products", o => products.Contains(o as ShopProduct));
        RemoveShopTab(toyCategory);
        ClearBanners();
        ClearSallang();
        RewriteTexts();
        RemoveInventoryTab(toyCategory);

        // 목록 · 화면 데이터 · 문구도 다시 맞춤 (기록관 효과 · 방명록 · 도감 힌트 · 건물 설명)
        SettlementSetup.ApplyP4Story();
        SettlementSetup.ApplyTraitOtters();
        PlazaLifeSetup.RefreshEntryTexts();
        AssetDatabase.SaveAssets();

        int deleted = 0;
        foreach (var asset in new List<Object>(decors).Concat(products).Concat(toys))
        {
            if (asset != null && AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset)))
                deleted++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ToyRemovalSetup] 장난감 {toys.Count}종 · 꾸미기 {decors.Count} · 상품 {products.Count}을 없앴습니다 (에셋 {deleted}개 지움).");
    }

    private static void RemoveFrom(string path, string property, System.Func<Object, bool> match)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (asset == null)
            return;
        var so = new SerializedObject(asset);
        var list = so.FindProperty(property);
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var value = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value == null || match(value))
                list.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RemoveShopTab(ItemCategory toyCategory)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<Object>(ShopCatalogPath);
        if (catalog == null || toyCategory == null)
            return;
        var so = new SerializedObject(catalog);
        var tabs = so.FindProperty("_tabs");
        for (int i = tabs.arraySize - 1; i >= 0; i--)
        {
            if (tabs.GetArrayElementAtIndex(i).FindPropertyRelative("_category").objectReferenceValue == toyCategory)
                tabs.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearBanners()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:GachaBannerDefinition", new[] { BannerFolder }))
        {
            var banner = AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(banner);
            so.FindProperty("_pool").arraySize = 0;
            so.FindProperty("_featured").arraySize = 0;
            so.FindProperty("_featuredOtter").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ClearSallang()
    {
        var sallang = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>(SallangPath);
        if (sallang == null)
            return;
        var so = new SerializedObject(sallang);
        so.FindProperty("_favoriteToy").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 이미 있는 에셋의 문구 (만드는 쪽 표는 새 에셋에만 쓰므로 여기서 맞춤)
    private static void RewriteTexts()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { QuestFolder }))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest == null || quest.GoalType != QuestGoalType.PlaceDecor)
                continue;
            var so = new SerializedObject(quest);
            var description = so.FindProperty("_description");
            description.stringValue = description.stringValue.Replace("가구나 장난감 ", "가구 ").Replace("장난감 ", "가구 ");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var shard = AssetDatabase.LoadAssetAtPath<Currency>(ShardPath);
        if (shard != null)
        {
            shard.Description = GachaSetup.ShardDescription;
            EditorUtility.SetDirty(shard);
        }
    }

    // 가방 화면의 분류 칩에서 장난감을 뺌 (꾸미기 탭 안 소분류)
    private static void RemoveInventoryTab(ItemCategory toyCategory)
    {
        if (toyCategory == null)
            return;
        var root = PrefabUtility.LoadPrefabContents(GlobalUIPath);
        try
        {
            bool changed = false;
            foreach (var presenter in root.GetComponentsInChildren<InventoryPresenter>(true))
            {
                var so = new SerializedObject(presenter);
                var list = so.FindProperty("_categories");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == toyCategory)
                    {
                        list.DeleteArrayElementAtIndex(i);
                        changed = true;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, GlobalUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
