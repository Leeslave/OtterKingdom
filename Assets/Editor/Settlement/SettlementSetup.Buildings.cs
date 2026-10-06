using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 자리를 골라 짓는 건물 (P4, Docs/특성사회성장_P4_기획반영.md): 건물 에셋을 만들고 꾸미기 카탈로그의 건물 목록에 넣는다.
/// - 작은 집: 기존 집 그림(완성 · 공사 단계)을 가져오기 설정을 바꾸지 않고 빌려 씀
/// - 특성 건물(해달 상점 · 목공소 · 채석 작업소 · 농업 창고): 임시 그림 `python Tools/UIGen/p4_buildings_art.py` (가져올 때 아래 가운데 피벗)
/// 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    private const string BuildingFolder = "Assets/Scriptable Obejects/Decor/Buildings";
    private const string DecorCatalogPath = "Assets/Scriptable Obejects/Decor/DecorCatalog.asset";
    private const string BuildingArtPrefix = ArtFolder + "/Building_";
    private const float BuildingArtPixelsPerUnit = 100f;
    private static readonly Vector2 BuildingArtPivot = new Vector2(0.5f, 0.02f);

    // 에셋 이름, ID, 이름, 설명, 종류, 차지하는 칸, 그림 가로 비율, 완성 그림, 공사 단계 그림, 아이콘(비우면 완성 그림),
    // 레벨, 필요 발전, 특성, 최대 수, 골드, 목재, 돌, 비용 증가, 공사 시간(초)
    private static readonly (string asset, string id, string name, string description, BuildingKind kind, Vector2Int footprint, float widthFill,
        string sprite, string[] stages, string icon, int level, string development, (OtterTrait trait, int count)[] traits, int maxCount,
        int gold, int wood, int stone, float growth, float seconds)[] BuildingTable =
    {
        ("Building_SmallHouse", "bld_small_house", "작은 집", "장난감을 보고 찾아온 해달이 살 수 있는 집이에요.", BuildingKind.Home,
            new Vector2Int(4, 3), 0.9f, "Assets/Art/Plaza/Props/Prop_House_Blue.png",
            new[] { "Stage_House_Blue_0", "Stage_House_Blue_1", "Stage_House_Blue_2", "Stage_House_Blue_3" }, "ICON_House_Blue",
            0, TownHallDevelopment, new (OtterTrait, int)[0], 0, 600, 20, 10, 0.3f, 180f),
        // Lv.10~15 해달의 부탁 (SettlementSetup.P4Story.cs) — 그 부탁이 열려야 지을 수 있음 (필요 발전 = 입주 부탁의 결과)
        ("Building_Shop", "bld_shop", "해달 상점", "짤랑이의 가게. 요정 상점이 이곳으로 이사하고, 물건을 더 비싸게 팔아요.", BuildingKind.Facility,
            new Vector2Int(4, 3), 0.95f, BuildingArtPrefix + "Shop.png", new string[0], null,
            0, P4TraderSettled, new[] { (OtterTrait.Trading, 1) }, 1, 2000, 40, 20, 0f, 300f),
        ("Building_Workshop", "bld_workshop", "목공소", "숲을 개간할 때 얻는 목재가 늘어나요.", BuildingKind.Facility,
            new Vector2Int(4, 3), 0.95f, BuildingArtPrefix + "Workshop.png", new string[0], null,
            0, P4LumberSettled, new[] { (OtterTrait.Woodcutting, 2) }, 1, 3000, 60, 30, 0f, 360f),
        ("Building_Quarry", "bld_quarry", "채석 작업소", "광장 바위와 광산에서 얻는 돌이 늘어나요.", BuildingKind.Facility,
            new Vector2Int(4, 3), 0.95f, BuildingArtPrefix + "Quarry.png", new string[0], null,
            0, P4MinerSettled, new[] { (OtterTrait.Mining, 2) }, 1, 4000, 60, 60, 0f, 420f),
        ("Building_Granary", "bld_granary", "농업 창고", "밭에서 거두는 작물이 늘어나요.", BuildingKind.Facility,
            new Vector2Int(4, 3), 0.95f, BuildingArtPrefix + "Granary.png", new string[0], null,
            0, P4FarmerSettled, new[] { (OtterTrait.Farming, 2) }, 1, 5000, 80, 50, 0f, 480f),
    };

    // 시설의 왕국 보유 효과 (%), 요정 상점이 이사하는지
    private static readonly Dictionary<string, ((KingdomBonusKind kind, int percent)[] bonuses, bool fairy)> BuildingEffects =
        new Dictionary<string, ((KingdomBonusKind, int)[], bool)>
        {
            { "bld_shop", (new[] { (KingdomBonusKind.SellPrice, 10) }, true) },
            { "bld_workshop", (new[] { (KingdomBonusKind.ClearingWood, 20) }, false) },
            { "bld_quarry", (new[] { (KingdomBonusKind.StoneYield, 20) }, false) },
            { "bld_granary", (new[] { (KingdomBonusKind.HarvestYield, 10) }, false) },
        };

    [MenuItem("Tools/Settlement/Apply Buildings")]
    public static void ApplyBuildings()
    {
        CreateBuildingData();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 건물 적용 완료: {BuildingTable.Length}종");
    }

    private static void CreateBuildingData()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorCatalogPath);
        if (catalog == null)
        {
            Debug.LogError("[SettlementSetup] 꾸미기 카탈로그가 없습니다. 꾸미기 데이터를 먼저 만드세요.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(BuildingFolder))
            AssetDatabase.CreateFolder("Assets/Scriptable Obejects/Decor", "Buildings");

        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var buildings = new List<BuildingDefinition>();
        foreach (var row in BuildingTable)
        {
            var building = LoadOrCreate<BuildingDefinition>($"{BuildingFolder}/{row.asset}.asset").asset;
            var traits = new List<TraitRequirement>();
            foreach (var (trait, count) in row.traits)
                traits.Add(new TraitRequirement(trait, count));
            var items = new List<ItemAmount>();
            if (row.wood > 0 && wood != null)
                items.Add(new ItemAmount(wood, row.wood));
            if (row.stone > 0 && stone != null)
                items.Add(new ItemAmount(stone, row.stone));
            building.Setup(row.id, row.name, row.kind, row.level, row.development, traits, row.maxCount, row.gold, items, row.growth, row.seconds);

            // 새 건물 그림만 피벗을 맞춰 가져옴 (빌려 쓰는 집 그림은 그대로)
            var sprite = row.sprite.StartsWith(BuildingArtPrefix)
                ? ImportP3Sprite(row.sprite, BuildingArtPixelsPerUnit, BuildingArtPivot)
                : AssetDatabase.LoadAssetAtPath<Sprite>(row.sprite);
            building.SetupPlacement(row.footprint, false, sprite);

            var bonuses = new List<BuildingBonus>();
            bool fairy = false;
            if (BuildingEffects.TryGetValue(row.id, out var effects))
            {
                foreach (var (kind, percent) in effects.bonuses)
                    bonuses.Add(new BuildingBonus(kind, percent));
                fairy = effects.fairy;
            }
            building.SetupEffects(bonuses, fairy);

            var so = new SerializedObject(building);
            so.FindProperty("_description").stringValue = row.description;
            so.FindProperty("_icon").objectReferenceValue = string.IsNullOrEmpty(row.icon) ? null : LoadPropArt(row.icon);
            so.FindProperty("_widthFill").floatValue = row.widthFill;
            var stages = so.FindProperty("_stageSprites");
            stages.arraySize = row.stages.Length;
            for (int i = 0; i < row.stages.Length; i++)
                stages.GetArrayElementAtIndex(i).objectReferenceValue = LoadPropArt(row.stages[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(building);
            buildings.Add(building);
        }

        var catalogSo = new SerializedObject(catalog);
        var list = catalogSo.FindProperty("_buildings");
        foreach (var building in buildings)
            AddUnique(list, building);
        catalogSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BuildingDefinition LoadBuilding(string asset) =>
        AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{BuildingFolder}/{asset}.asset");
}
