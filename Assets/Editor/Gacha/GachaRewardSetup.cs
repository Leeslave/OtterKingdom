using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 가챠 개편 1단계 데이터 (Docs/가챠개편_특수해달조각_장신구_기획.md): 특수 해달 6종 · 장신구 9개(3종 × 3등급) · 새 보상 설정.
/// 이미 있는 에셋은 사람이 고친 값을 지키기 위해 덮어쓰지 않고, 보상 설정의 목록 · 재료 참조만 맞춘다. 수치는 모두 가안
/// </summary>
public static class GachaRewardSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Gacha";
    private const string OtterFolder = DataFolder + "/SpecialOtters";
    private const string AccessoryFolder = DataFolder + "/Accessories";
    internal const string RewardConfigPath = DataFolder + "/GachaRewardConfig.asset";
    private const string WoodPath = "Assets/Scriptable Obejects/Inventory/Items/Mining/목재.asset";
    private const string StonePath = "Assets/Scriptable Obejects/Inventory/Items/Mining/돌.asset";

    // 특수 해달: (에셋, ID, 이름, 등급(1 레어 · 2 에픽), 버프, 1~5성 %, 소개)
    private static readonly (string asset, string id, string name, int tier, KingdomBonusKind bonus, int[] percents, string description)[] Otters =
    {
        ("Bard", "special_bard", "음유시인 해달", GachaTable.Rare, KingdomBonusKind.MoveSpeed, new[] { 4, 6, 8, 10, 12 },
            "노래로 마을에 활기를 불어넣어요. 편성하면 주민들이 더 빨리 걸어요."),
        ("Gardener", "special_gardener", "정원사 해달", GachaTable.Rare, KingdomBonusKind.GrowSpeed, new[] { 4, 6, 8, 10, 12 },
            "흙을 읽을 줄 아는 해달이에요. 편성하면 새로 심는 작물이 빨리 자라요."),
        ("Forester", "special_forester", "산림관리사 해달", GachaTable.Rare, KingdomBonusKind.WoodYield, new[] { 5, 7, 10, 12, 15 },
            "숲의 나무를 아끼며 가꿔요. 편성하면 목재를 더 얻어요."),
        ("Geologist", "special_geologist", "지질학자 해달", GachaTable.Rare, KingdomBonusKind.StoneYield, new[] { 5, 7, 10, 12, 15 },
            "돌의 결을 보고 쉽게 쪼개요. 편성하면 돌을 더 얻어요."),
        ("Accountant", "special_accountant", "회계사 해달", GachaTable.Epic, KingdomBonusKind.SellPrice, new[] { 3, 5, 6, 8, 10 },
            "장부 정리라면 누구에게도 지지 않아요. 편성하면 물건을 더 비싸게 팔아요."),
        ("Architect", "special_architect", "건축가 해달", GachaTable.Epic, KingdomBonusKind.BuildSpeed, new[] { 4, 6, 8, 10, 12 },
            "설계도를 그려 공사를 도와요. 편성하면 새 공사가 빨리 끝나요."),
    };

    // 장신구: (이름, ID 앞부분, 효과, 일반 · 레어 · 에픽 강화 0 %, 소개)
    private static readonly (string asset, string id, string name, AccessoryEffect effect, int[] percents, string description)[] Accessories =
    {
        ("FeatherHat", "acc_feather_hat", "깃털 모자", AccessoryEffect.MoveSpeed, new[] { 8, 12, 18 }, "가벼운 깃털 모자. 낀 해달이 더 빨리 걸어요."),
        ("SparkleGloves", "acc_sparkle_gloves", "반짝 장갑", AccessoryEffect.WorkTime, new[] { 6, 9, 14 }, "손에 착 붙는 장갑. 낀 해달이 맡은 일을 더 빨리 끝내요."),
        ("LuckyNecklace", "acc_lucky_necklace", "행운 목걸이", AccessoryEffect.Yield, new[] { 6, 9, 14 }, "작은 조개 목걸이. 낀 해달이 얻는 양이 늘어나요."),
    };

    private static readonly string[] TierNames = { "", "레어 ", "에픽 " };
    private static readonly string[] TierKeys = { "common", "rare", "epic" };

    // 재료 꾸러미: (목재, 돌, 무게)
    private static readonly (int wood, int stone, int weight)[] Bundles = { (20, 15, 3), (50, 40, 1) };

    [MenuItem("Tools/Gacha/Create Companion Data")]
    public static void CreateData()
    {
        EnsureFolder(OtterFolder);
        EnsureFolder(AccessoryFolder);

        var otters = new List<SpecialOtterDefinition>();
        foreach (var row in Otters)
        {
            var (otter, isNew) = LoadOrCreate<SpecialOtterDefinition>($"{OtterFolder}/SpecialOtter_{row.asset}.asset");
            if (isNew)
            {
                otter.Setup(row.id, row.name, row.tier, row.bonus, row.percents, row.description);
                EditorUtility.SetDirty(otter);
            }
            otters.Add(otter);
        }

        var accessories = new List<AccessoryDefinition>();
        foreach (var row in Accessories)
        {
            for (int tier = GachaTable.Common; tier <= GachaTable.Epic; tier++)
            {
                var (accessory, isNew) = LoadOrCreate<AccessoryDefinition>($"{AccessoryFolder}/Accessory_{row.asset}_{TierKeys[tier]}.asset");
                if (isNew)
                {
                    accessory.Setup($"{row.id}_{TierKeys[tier]}", TierNames[tier] + row.name, tier, row.effect, row.percents[tier], row.description);
                    EditorUtility.SetDirty(accessory);
                }
                accessories.Add(accessory);
            }
        }

        var (config, configIsNew) = LoadOrCreate<GachaRewardConfig>(RewardConfigPath);
        // 재료 꾸러미는 처음 만들 때만 (확률 · 수량은 에셋에서 고친 값을 지킴)
        List<GachaRewardConfig.MaterialBundle> bundles = null;
        if (configIsNew)
        {
            bundles = new List<GachaRewardConfig.MaterialBundle>();
            foreach (var (wood, stone, weight) in Bundles)
                bundles.Add(new GachaRewardConfig.MaterialBundle { wood = wood, stone = stone, weight = weight });
        }
        config.Setup(AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodPath), AssetDatabase.LoadAssetAtPath<ItemDefinition>(StonePath),
            bundles, otters, accessories);
        if (config.Wood == null || config.Stone == null)
            Debug.LogWarning("[GachaRewardSetup] 목재 · 돌 아이템을 찾을 수 없습니다.");
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GachaRewardSetup] 특수 해달 {otters.Count}종 · 장신구 {accessories.Count}개 · 보상 설정 완료");
    }

    // 2단계: 그림이 오기 전 임시 초상화 (쓰지 않는 해달 얼굴), 픽업 해달
    private static readonly (string asset, string portrait)[] PlaceholderPortraits =
    {
        ("Bard", "ICON_Otter_Cast_Lilac"), ("Gardener", "ICON_Otter_Cast_Sprout"), ("Forester", "ICON_Otter_Cast_Forest"),
        ("Geologist", "ICON_Otter_Cast_Explorer"), ("Accountant", "ICON_Otter_Cast_Honey"), ("Architect", "ICON_Otter_Cast_Sky"),
    };
    private const string PickupOtterAsset = "Architect";
    private const string PlaceholderIconPath = "Assets/Art/UI/Currency/Shard.png";
    private const string OtterArtFolder = "Assets/Art/Otter";
    private const string GlobalUIPath = "Assets/Resources/GlobalUI.prefab";

    /// <summary>
    /// 가챠 개편 2단계: 비어 있는 초상화 · 대신 쓰는 그림을 채우고, 픽업 배너에 픽업 해달을 넣고,
    /// 전역 UI의 뽑기 매니저에 보상 설정을 연결하며 골드 배너를 목록에서 뺀다 (골드 배너 에셋은 남김).
    /// 배치 모드: -executeMethod GachaRewardSetup.ConnectToGame
    /// </summary>
    [MenuItem("Tools/Gacha/Connect Companion Data")]
    public static void ConnectToGame()
    {
        var config = AssetDatabase.LoadAssetAtPath<GachaRewardConfig>(RewardConfigPath);
        if (config == null)
        {
            Debug.LogError("[GachaRewardSetup] 보상 설정이 없습니다. Tools/Gacha/Create Companion Data를 먼저 실행하세요.");
            return;
        }
        foreach (var (asset, portrait) in PlaceholderPortraits)
        {
            var otter = AssetDatabase.LoadAssetAtPath<SpecialOtterDefinition>($"{OtterFolder}/SpecialOtter_{asset}.asset");
            if (otter == null)
                continue;
            var so = new SerializedObject(otter);
            FillIfEmpty(so, "_portrait", ImportSprite(OtterArtFolder, portrait));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderIconPath);
        var configSo = new SerializedObject(config);
        FillIfEmpty(configSo, "_otterFallbackIcon", placeholder);
        FillIfEmpty(configSo, "_accessoryFallbackIcon", placeholder);
        configSo.ApplyModifiedPropertiesWithoutUndo();

        var pickup = CreateOtterPickupBanner();
        AssetDatabase.SaveAssets();

        var root = PrefabUtility.LoadPrefabContents(GlobalUIPath);
        try
        {
            var manager = root.GetComponentInChildren<GachaManager>(true);
            if (manager == null)
            {
                Debug.LogError("[GachaRewardSetup] 전역 UI에 뽑기 매니저가 없습니다.");
                return;
            }
            var so = new SerializedObject(manager);
            so.FindProperty("_rewardConfig").objectReferenceValue = config;
            RemoveGoldBanners(so.FindProperty("_banners"));
            if (pickup != null)
                AddUnique(so.FindProperty("_banners"), pickup);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GlobalUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[GachaRewardSetup] 보상 설정을 전역 UI 뽑기 매니저에 연결했습니다 (골드 배너는 목록에서 뺌).");
    }

    /// <summary>
    /// 첫 특수 해달 픽업 (건축가, 2026-10-22 ~ 11-04): 지난 봄바람 픽업을 복사해 그림 · 조개 · 뽑기권 · 값은 그대로 쓰고,
    /// 장난감 목록은 비우고 픽업 해달만 넣음. 이미 있으면 그대로 (사람이 고친 값을 지킴)
    /// </summary>
    private static GachaBannerDefinition CreateOtterPickupBanner()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(OtterPickupBannerPath);
        if (existing != null)
            return existing;
        var otter = AssetDatabase.LoadAssetAtPath<SpecialOtterDefinition>($"{OtterFolder}/SpecialOtter_{PickupOtterAsset}.asset");
        if (otter == null || !AssetDatabase.CopyAsset(GachaSetup.PickupBannerPath, OtterPickupBannerPath))
        {
            Debug.LogWarning("[GachaRewardSetup] 특수 해달 픽업 배너를 만들지 못했습니다 (건축가 해달 · 봄바람 픽업 배너 확인).");
            return null;
        }
        var banner = AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(OtterPickupBannerPath);
        var so = new SerializedObject(banner);
        so.FindProperty("_bannerId").stringValue = "pickup_architect";
        so.FindProperty("_title").stringValue = "건축가 픽업";
        so.FindProperty("_tabTitle").stringValue = "건축가";
        so.FindProperty("_subtitle").stringValue = "에픽 건축가 해달 조각 확률 UP!\n별빛 포인트를 모으면 조각 30개";
        so.FindProperty("_startDate").stringValue = "2026-10-22";
        so.FindProperty("_endDate").stringValue = "2026-11-04";
        so.FindProperty("_pool").arraySize = 0;
        so.FindProperty("_featured").arraySize = 0;
        so.FindProperty("_featuredOtter").objectReferenceValue = null;
        var list = so.FindProperty("_featuredSpecialOtters");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = otter;
        so.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    internal const string OtterPickupBannerPath = DataFolder + "/Banners/Banner_Pickup_Architect.asset";

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

    /// <summary>뽑기 매니저 배너 목록에서 골드 배너를 뺀다</summary>
    internal static void RemoveGoldBanners(SerializedProperty banners)
    {
        for (int i = banners.arraySize - 1; i >= 0; i--)
        {
            var banner = banners.GetArrayElementAtIndex(i).objectReferenceValue as GachaBannerDefinition;
            if (banner == null || banner.IsGoldBanner)
                banners.DeleteArrayElementAtIndex(i);
        }
    }
}
