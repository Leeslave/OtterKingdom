using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CropPricing의 배율로 소모형 모종 작물(감자·고구마·딸기)의 수확물 판매가를 다시 정한다.
/// 모종 하나 값 = 그 모종을 파는 요정 상점 상품의 골드 가격 ÷ 묶음 개수, 수확량 = 작물 데이터의 한 번 수확량.
/// CropPricing 에셋의 배율을 바꾸면 바로, 상점 가격·수확량을 바꿨으면 메뉴로 다시 맞춘다.
/// </summary>
[InitializeOnLoad]
public static class CropPricingSetup
{
    private const string PricingPath = "Assets/Data/Crops/CropPricing.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";

    static CropPricingSetup()
    {
        CropPricing.Changed += pricing => Apply(pricing, false);
    }

    [MenuItem("Tools/Farm/Apply Crop Pricing")]
    public static void ApplyFromMenu()
    {
        var pricing = AssetDatabase.LoadAssetAtPath<CropPricing>(PricingPath);
        if (pricing == null)
        {
            pricing = ScriptableObject.CreateInstance<CropPricing>();
            AssetDatabase.CreateAsset(pricing, PricingPath);
            Debug.Log($"[CropPricing] 배율 에셋을 만들었습니다: {PricingPath}");
        }
        Apply(pricing, true);
    }

    /// <param name="report">바뀐 것이 없어도 결과를 로그로 남길지 (메뉴)</param>
    public static void Apply(CropPricing pricing, bool report)
    {
        if (pricing == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var products = LoadAll<ShopProduct>();
        var items = LoadAll<ItemDefinition>();
        var lines = new List<string>();
        bool changed = false;
        foreach (var crop in LoadAll<CropDefinition>())
        {
            if (crop.seedType != SeedType.Consumable)
                continue;
            var product = products.Find(p => p.Item != null && p.Item.ItemId == crop.SeedItemId && p.PriceCurrency == gold);
            var item = items.Find(i => i.ItemId == crop.cropId);
            if (product == null || item == null)
            {
                Debug.LogWarning($"[CropPricing] {crop.displayName}: 골드로 파는 모종 상품({crop.SeedItemId}) 또는 수확물 아이템({crop.cropId})이 없어 건너뜁니다.");
                continue;
            }

            double seedPrice = (double)product.Price / product.BundleSize;
            int price = CropPricing.SellPriceFor(seedPrice, crop.yieldCount, pricing.HarvestToSeedRatio);
            if (item.SellPrice != price)
            {
                changed = true;
                var so = new SerializedObject(item);
                so.FindProperty("_sellPrice").intValue = price;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
            // 작물 데이터의 판매가는 판매에 쓰이지 않지만 헷갈리지 않게 같은 값으로
            if (crop.sellPrice != price)
            {
                changed = true;
                crop.sellPrice = price;
                EditorUtility.SetDirty(crop);
            }
            lines.Add($"{crop.displayName}: 모종 {seedPrice:0.#} × {pricing.HarvestToSeedRatio:0.##} ÷ {crop.yieldCount}개 = 한 개 {price}골드");
        }
        if (changed)
            AssetDatabase.SaveAssets();
        if (report || changed)
            Debug.Log("[CropPricing] 소모형 모종 작물 판매가\n" + string.Join("\n", lines));
    }

    private static List<T> LoadAll<T>() where T : Object
    {
        var result = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                result.Add(asset);
        }
        return result;
    }
}
