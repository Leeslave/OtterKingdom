using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 밭 채소 10종 · 바다 물고기 9종을 더하고 낚싯대 강화를 20레벨로 (Docs/채소_물고기_확장.md).
/// 채소: 수확물 · 모종 아이템, 작물 정의(밭 그림), 요정 상점 모종(값 · 왕국 레벨), 도감 항목, 게임 매니저 작물 목록.
/// 물고기: 아이템, 도감 항목, FishingBalanceData의 물고기 표(등급 · 낚싯대 레벨 · 무게) · 강화 값 · 물고기 확률 끝.
/// 밭 강화 20레벨은 FarmBalanceData 기본값(코드).
/// 그림: python Tools/HarvestArt/make_harvest_art.py (기존 채소 · 고등어 그림을 다시 칠한 임시 그림).
/// 이 표가 기준이라 다시 돌리면 값을 덮어씀. 배치 모드: -executeMethod HarvestExpansionSetup.Apply
/// </summary>
public static class HarvestExpansionSetup
{
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string RarityFolder = "Assets/Scriptable Obejects/Inventory/Rarity";
    private const string CropFolder = "Assets/Data/Crops";
    private const string CropPricingPath = CropFolder + "/CropPricing.asset";
    private const string FishingBalancePath = "Assets/Data/Fishing/FishingBalanceData.asset";
    private const string ProductFolder = "Assets/Scriptable Obejects/Shop/Products";
    private const string EntryFolder = "Assets/Scriptable Obejects/Collection/Entries";
    private const string TabFolder = "Assets/Scriptable Obejects/Collection/Tabs";
    private const string CollectionDatabasePath = "Assets/Scriptable Obejects/Collection/CollectionDatabase.asset";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string VegetableArt = "Assets/Art/Item/Vegetable";
    private const string FieldArt = "Assets/Art/Farm/Crop";
    private const string FishArt = "Assets/Art/Item/Fish";

    private static readonly string[] RarityAssets = { "Common", "Rare", "Epic" };

    // 채소: (그림 Id, 작물 Id(crop_ 뒤), 이름, 바탕 밭 그림, 등급, 자라는 초, 한 번 수확량, 모종 값, 왕국 레벨, 소개, 도감 설명)
    // 등급이 높을수록 모종이 비싸고 오래 자라지만 한 번 거두는 값(모종값 × 1.5)이 커서 칸당 벌이가 큼
    private static readonly (string art, string id, string name, string baseField, int tier, float seconds, int yield, int seedPrice, int level,
        string tagline, string description)[] Crops =
    {
        ("Radish", "radish", "무", "carrot", 0, 75, 4, 120, 5,
            "하얗고 시원한 뿌리채소.", "국을 끓이면 시원해서 해달들이 그릇째 마셔요."),
        ("Taro", "taro", "토란", "potato", 0, 90, 3, 180, 6,
            "털옷을 입은 동글동글 알뿌리.", "삶으면 미끈미끈 고소해져요."),
        ("Zucchini", "zucchini", "애호박", "cucumber", 0, 95, 4, 220, 8,
            "연둣빛 길쭉한 호박.", "부침개로 부치면 광장에 고소한 냄새가 퍼져요."),
        ("Raspberry", "raspberry", "산딸기", "strawberry", 0, 100, 5, 260, 9,
            "들에서 따 먹던 새콤한 열매.", "한 알 먹으면 입이 오므라들 만큼 새콤해요."),
        ("Beet", "beet", "비트", "carrot", 1, 150, 4, 500, 10,
            "속까지 새빨간 뿌리채소.", "자르면 손이 빨개져서 해달들이 서로 놀려요."),
        ("PurplePotato", "purple_potato", "자색 감자", "potato", 1, 170, 3, 650, 12,
            "보랏빛으로 물든 귀한 감자.", "삶아도 보라색 그대로라 다들 신기해해요."),
        ("WhiteStrawberry", "white_strawberry", "하얀 딸기", "strawberry", 1, 200, 5, 900, 14,
            "눈처럼 하얀 귀한 딸기.", "하얀데 더 달아서 처음 먹으면 깜짝 놀라요."),
        ("Blueberry", "blueberry", "블루베리", "strawberry", 2, 300, 5, 2000, 15,
            "보석처럼 반짝이는 파란 열매.", "눈이 맑아진다며 기록 해달들이 특히 좋아해요."),
        ("GoldenCarrot", "golden_carrot", "황금 당근", "carrot", 2, 360, 3, 3000, 17,
            "햇빛을 머금은 황금빛 당근.", "한 입 베어 물면 반짝 빛이 나는 것 같대요."),
        ("GoldenPotato", "golden_potato", "황금 감자", "potato", 2, 420, 3, 4000, 19,
            "흙 속에서 찾은 보물 같은 감자.", "캐는 날이면 해달들이 모두 밭으로 모여요."),
    };

    // 물고기: (그림 Id(빈칸 = 기존 고등어), 아이템 Id, 이름, 등급, 낚싯대 레벨, 무게, 판매가, 소개, 도감 설명)
    private static readonly (string art, string id, string name, int tier, int rod, float weight, int price, string tagline, string description)[] Fishes =
    {
        ("", "fish_mackerel", "고등어", 0, 1, 100, 30, "", ""),
        ("Sardine", "fish_sardine", "정어리", 0, 1, 100, 25,
            "떼 지어 다니는 작은 은빛 물고기.", "한 마리 낚으면 근처에 친구가 잔뜩 있어요."),
        ("HorseMackerel", "fish_horse_mackerel", "전갱이", 0, 3, 100, 35,
            "꼬리 쪽에 단단한 비늘이 있는 물고기.", "구우면 고소해서 해달들의 단골 반찬이에요."),
        ("Saury", "fish_saury", "꽁치", 0, 5, 100, 40,
            "길쭉하고 날렵한 가을 물고기.", "너무 길어서 배 위에 올리면 삐죽 나와요."),
        ("Rockfish", "fish_rockfish", "우럭", 0, 7, 100, 55,
            "바위 틈에 숨어 사는 듬직한 물고기.", "입이 커서 미끼를 한입에 꿀꺽해요."),
        ("SeaBream", "fish_sea_bream", "도미", 1, 6, 15, 150,
            "붉은 빛이 고운 바다의 귀한 손님.", "잔칫날이면 해달들이 꼭 찾는 물고기예요."),
        ("Salmon", "fish_salmon", "연어", 1, 9, 15, 200,
            "먼 바다를 돌아 돌아온 물고기.", "속살이 주황빛이라 해달들이 반해요."),
        ("Flounder", "fish_flounder", "광어", 1, 12, 15, 260,
            "모래 바닥에 납작 엎드린 물고기.", "두 눈이 한쪽에 몰려 있어서 늘 한쪽만 봐요."),
        ("Tuna", "fish_tuna", "참치", 2, 15, 3, 800,
            "넓은 바다를 누비는 힘센 물고기.", "낚으면 낚싯대가 활처럼 휘어요!"),
        ("GoldenMackerel", "fish_golden_mackerel", "황금 고등어", 2, 18, 3, 1500,
            "전설로만 듣던 금빛 고등어.", "비늘 하나하나가 해처럼 반짝여요."),
    };

    // 밭 강화와 같은 표 (FarmBalanceData.upgradeCostByLevel)
    private static readonly int[] RodUpgradeCosts =
    {
        100, 200, 400, 800, 1200, 1700, 2400, 3300, 4500, 6000,
        8000, 10500, 14000, 18500, 24000, 31000, 40000, 52000, 67000,
    };

    [MenuItem("Tools/Farm/Apply Crops And Fish")]
    public static void Apply()
    {
        var rarities = RarityAssets.Select(r => AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/{r}.asset")).ToArray();
        var cropsCategory = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Crops.asset");
        var seedCategory = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Seedlings.asset");
        var fishCategory = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Fish.asset");
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var pricing = AssetDatabase.LoadAssetAtPath<CropPricing>(CropPricingPath);
        float ratio = pricing != null ? pricing.HarvestToSeedRatio : 1.5f;
        var vegetableTab = AssetDatabase.LoadAssetAtPath<CollectionTab>($"{TabFolder}/Tab_Vegetable.asset");
        var fishTab = AssetDatabase.LoadAssetAtPath<CollectionTab>($"{TabFolder}/Tab_Fish.asset");

        var items = new List<ItemDefinition>();
        var products = new List<ShopProduct>();
        var entries = new List<CollectionEntry>();
        var cropDefs = new List<CropDefinition>();

        // 딸기는 레어 (모종이 비싸고 늦게 열림)
        var strawberry = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/Farming/딸기.asset");
        if (strawberry != null)
            SetItemRarity(strawberry, rarities[1]);

        for (int i = 0; i < Crops.Length; i++)
        {
            var c = Crops[i];
            var rarity = rarities[c.tier];
            int sell = CropPricing.SellPriceFor(c.seedPrice, c.yield, ratio);

            var crop = Item($"Farming/{c.name}", $"crop_{c.id}", c.name, c.tagline, cropsCategory, rarity, true, sell,
                ImportSprite(VegetableArt, $"ICON_Crop_{c.art}"));
            var seed = Item($"Farming/{c.name} 모종", $"seed_{c.id}", $"{c.name} 모종",
                $"밭에 심으면 {c.name}{KoreanParticle.SubjectParticle(c.name)} 자라요. 심을 때마다 하나씩 써요.",
                seedCategory, rarity, false, c.seedPrice / 2, ImportSprite(VegetableArt, $"ICON_Seedling_{c.art}"));
            items.Add(crop);
            items.Add(seed);

            var (def, _) = LoadOrCreate<CropDefinition>($"{CropFolder}/CropDefinition_{c.art}.asset");
            def.cropId = $"crop_{c.id}";
            def.displayName = c.name;
            def.baseDurationSec = c.seconds;
            def.yieldCount = c.yield;
            def.sellPrice = sell;
            def.seedType = SeedType.Consumable;
            def.initialSeedCount = 0;
            def.spriteTint = Color.white;
            def.seedSprites = FieldSprites(c.id, "seed", c.baseField);
            def.sproutSprites = FieldSprites(c.id, "sprout", c.baseField);
            def.grownSprites = FieldSprites(c.id, "grown", c.baseField);
            EditorUtility.SetDirty(def);
            cropDefs.Add(def);

            var (product, _) = LoadOrCreate<ShopProduct>($"{ProductFolder}/Seed_{c.art}.asset");
            var productSo = new SerializedObject(product);
            productSo.FindProperty("_productId").stringValue = $"shop_seed_{c.id}";
            productSo.FindProperty("_sortOrder").intValue = 10 + i;
            productSo.FindProperty("_item").objectReferenceValue = seed;
            productSo.FindProperty("_bundleSize").intValue = 1;
            productSo.FindProperty("_priceCurrency").objectReferenceValue = gold;
            productSo.FindProperty("_price").intValue = c.seedPrice;
            productSo.FindProperty("_requiredLevel").intValue = c.level;
            productSo.ApplyModifiedPropertiesWithoutUndo();
            products.Add(product);

            entries.Add(Entry($"Entry_{c.art}", $"crop_{c.id}", vegetableTab, crop, c.tagline, c.description, "밭", 10 + i));
        }

        var fishTable = new List<FishingBalanceData.FishEntry>();
        for (int i = 0; i < Fishes.Length; i++)
        {
            var f = Fishes[i];
            var rarity = rarities[f.tier];
            ItemDefinition fish;
            if (string.IsNullOrEmpty(f.art))
            {
                // 기존 고등어: 등급만 (판매가 · 그림은 그대로)
                fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/Fishing/{f.name}.asset");
                if (fish != null)
                    SetItemRarity(fish, rarity);
            }
            else
            {
                fish = Item($"Fishing/{f.name}", f.id, f.name, f.tagline, fishCategory, rarity, true, f.price,
                    ImportSprite(FishArt, $"ICON_Fish_{f.art}"));
                string where = f.rod > 1 ? $"바다 낚시터 · 낚싯대 Lv.{f.rod}" : "바다 낚시터";
                entries.Add(Entry($"Entry_Fish{f.art}", f.id, fishTab, fish, f.tagline, f.description, where, 10 + i));
            }
            if (fish != null)
                items.Add(fish);
            fishTable.Add(new FishingBalanceData.FishEntry(f.id, f.tier, f.rod, f.weight));
        }

        ApplyFishingBalance(fishTable);
        AddToDatabases(items, products, entries);
        AddCropsToGameManager(cropDefs);
        SilhouetteGenerator.FillMissing(entries);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HarvestExpansionSetup] 채소 {Crops.Length}종 · 물고기 {Fishes.Length - 1}종 · 낚싯대 Lv.{RodUpgradeCosts.Length + 1}까지");
    }

    private static void SetItemRarity(ItemDefinition item, ItemRarity rarity)
    {
        var so = new SerializedObject(item);
        so.FindProperty("_rarity").objectReferenceValue = rarity;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemDefinition Item(string path, string id, string name, string description, ItemCategory category, ItemRarity rarity,
        bool sellable, int sellPrice, Sprite icon)
    {
        var (item, _) = LoadOrCreate<ItemDefinition>($"{ItemFolder}/{path}.asset");
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = name;
        so.FindProperty("_description").stringValue = description;
        so.FindProperty("_category").objectReferenceValue = category;
        so.FindProperty("_rarity").objectReferenceValue = rarity;
        so.FindProperty("_isSellable").boolValue = sellable;
        so.FindProperty("_sellPrice").intValue = sellPrice;
        so.FindProperty("_maxStack").intValue = 999;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    // 밭 그림 3칸: 바탕 채소 그림과 같은 불러오기 설정 (기준점 아래 가운데 · PPU)
    private static Sprite[] FieldSprites(string id, string stage, string baseField)
    {
        var sprites = new Sprite[3];
        for (int n = 0; n < 3; n++)
        {
            string path = $"{FieldArt}/crop_{id}_{stage}_{n}.png";
            string basePath = $"{FieldArt}/crop_{baseField}_{stage}_{n}.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var baseImporter = (TextureImporter)AssetImporter.GetAtPath(basePath);
            if (importer != null && baseImporter != null)
            {
                var settings = new TextureImporterSettings();
                baseImporter.ReadTextureSettings(settings);
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.textureShape = TextureImporterShape.Texture2D;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            sprites[n] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[n] == null)
                Debug.LogWarning($"[HarvestExpansionSetup] 밭 그림이 없습니다: {path}");
        }
        return sprites;
    }

    private static CollectionEntry Entry(string asset, string id, CollectionTab tab, ItemDefinition item, string tagline, string description,
        string extra, int order)
    {
        var (entry, _) = LoadOrCreate<CollectionEntry>($"{EntryFolder}/{asset}.asset");
        var so = new SerializedObject(entry);
        so.FindProperty("_entryId").stringValue = id;
        so.FindProperty("_tab").objectReferenceValue = tab;
        so.FindProperty("_displayName").stringValue = "";
        so.FindProperty("_sortOrder").intValue = order;
        so.FindProperty("_tagline").stringValue = tagline;
        so.FindProperty("_description").stringValue = description;
        so.FindProperty("_extraValue").stringValue = extra;
        so.FindProperty("_linkedItem").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        return entry;
    }

    private static void ApplyFishingBalance(List<FishingBalanceData.FishEntry> table)
    {
        var balance = AssetDatabase.LoadAssetAtPath<FishingBalanceData>(FishingBalancePath);
        if (balance == null)
        {
            Debug.LogError($"[HarvestExpansionSetup] {FishingBalancePath}가 없습니다.");
            return;
        }
        balance.fishTable = table.ToArray();
        balance.rodUpgradeCosts = (int[])RodUpgradeCosts.Clone();
        balance.maxFishChance = 0.95f; // Lv.10에 95%, 그 뒤는 새 물고기 · 레어 확률로
        balance.rareLuckPerRodLevel = 0.05f;
        EditorUtility.SetDirty(balance);
    }

    private static void AddToDatabases(List<ItemDefinition> items, List<ShopProduct> products, List<CollectionEntry> entries)
    {
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database != null)
        {
            var so = new SerializedObject(database);
            foreach (var item in items)
                AddUnique(so.FindProperty("_items"), item);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(FairyShopSetup.CatalogPath);
        if (catalog != null)
        {
            var so = new SerializedObject(catalog);
            foreach (var product in products)
                AddUnique(so.FindProperty("_products"), product);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var collection = AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDatabasePath);
        if (collection != null)
        {
            var so = new SerializedObject(collection);
            foreach (var entry in entries)
                AddUnique(so.FindProperty("_entries"), entry);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 모든 씬이 같은 게임 매니저 프리팹을 씀 (작물 목록 = 심기 창 순서)
    private static void AddCropsToGameManager(List<CropDefinition> crops)
    {
        var root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);
        try
        {
            var manager = root.GetComponentInChildren<GameManager>(true);
            var so = new SerializedObject(manager);
            var list = so.FindProperty("cropDefinitions");
            foreach (var crop in crops)
                AddUnique(list, crop);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AddUnique(SerializedProperty list, Object value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
    }
}
