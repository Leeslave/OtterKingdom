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
}
