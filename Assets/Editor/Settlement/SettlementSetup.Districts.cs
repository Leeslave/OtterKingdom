using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

//// <summary>
/// 구역·이사: 구역 후보 블록(중앙 공터 + 용도를 고르는 블록 4개), 건물마다 들어갈 구역, 용도가 열리는 시설, 질서도 · 구역 보너스 기본값.
/// 블록 위치는 기획안의 지도 배치안(주거 = 북동, 상업 = 남쪽 바닷가 쪽, 생산 = 북서 숲, 농업 = 서쪽)을 따른다.
/// 블록이 커지는 단계(그 방향 영토 확장 단계)와 부지 칸은 지도 도구가 만든다 (territory_layout.json의 districts — 모래길을 피해 풀밭에 줄 맞춤).
/// 좌표는 광장 꾸미기 격자의 세이브 좌표 (칸 − 격자 기준 칸 (77, 3)). 바닥 픽셀 = ((x + 0.8) × 40, (33.6 − y) × 40).
/// 여러 번 실행해도 결과가 같다 (Docs/구역이사_작업기록.md)
/// </summary>
public static partial class SettlementSetup
{
    private const string DistrictFolder = DataFolder + "/Districts";

    // ID, 이름, 정해진 용도, 예시 용도 (칸 · 부지 칸은 지도 배치에서)
    private static readonly (string id, string name, DistrictType fixedType, DistrictType suggested)[] DistrictTable =
    {
        ("district_center", "중앙 공터", DistrictType.Central, DistrictType.Central),
        ("district_northeast", "북동쪽 언덕", DistrictType.None, DistrictType.Residential),
        ("district_south", "남쪽 바닷길", DistrictType.None, DistrictType.Commercial),
        ("district_northwest", "북서쪽 숲", DistrictType.None, DistrictType.Production),
        ("district_west", "서쪽 들판", DistrictType.None, DistrictType.Agricultural),
    };

    // 용도가 열리는 시설 (그 시설이 생겨야 쓸모 있는 구역은 그때 함께 열림). 주거는 처음부터. 수치 · 조건은 임시
    private static readonly DistrictTypeUnlock[] DistrictTypeUnlockTable =
    {
        new DistrictTypeUnlock(DistrictType.Commercial, "p4_trader_settled", "장사하는 해달이 마을에 살게 되면 상업 지역을 정할 수 있어요"),
        new DistrictTypeUnlock(DistrictType.Production, "p4_lumber_settled", "나무 하는 해달이 마을에 살게 되면 생산 지역을 정할 수 있어요"),
    };

    // 건물 · 부탁 건물 자리가 들어갈 구역 (에셋 이름). 표에 없으면 None = 어디에 놓아도 됨 (가로등 · 식탁 · 비축 상자 · 환영 소품)
    private static readonly Dictionary<string, DistrictType> BuildingDistricts = new Dictionary<string, DistrictType>
    {
        // 주거: 집, 쉬는 자리
        { "Plot_House1", DistrictType.Residential },
        { "Plot_House2", DistrictType.Residential },
        { "Plot_P3House", DistrictType.Residential },
        { "Building_SmallHouse", DistrictType.Residential },
        { "Plot_Chair", DistrictType.Residential },
        { "Plot_RestCorner", DistrictType.Residential },
        // 상업: 가게 (요정 상점이 함께 이사함)
        { "Building_Shop", DistrictType.Commercial },
        // 생산: 공방 · 채석장 · 곡물창고 (+ 건설소 · 어업 작업소)
        { "Building_Workshop", DistrictType.Production },
        { "Building_Quarry", DistrictType.Production },
        { "Building_Granary", DistrictType.Production },
        { "Building_Workyard", DistrictType.Production },
        { "Building_Fishery", DistrictType.Production },
        // 중앙: 기록관 (행정)
        { "Building_Archive", DistrictType.Central },
    };

    // 구역 보너스 (임시): 용도, 효과, 정하기만 해도 %, 맞는 건물마다 %, 최대 %
    private static readonly DistrictBonusEntry[] DistrictBonusTable =
    {
        new DistrictBonusEntry(DistrictType.Residential, KingdomBonusKind.VisitSpeed, 0, 5, 20),
        new DistrictBonusEntry(DistrictType.Commercial, KingdomBonusKind.SellPrice, 0, 5, 10),
        new DistrictBonusEntry(DistrictType.Production, KingdomBonusKind.ProductionSpeed, 0, 5, 20),
        new DistrictBonusEntry(DistrictType.Agricultural, KingdomBonusKind.GrowSpeed, 10, 0, 10),
    };

    private const int DistrictOrderMatchWeight = 60;
    private const int DistrictOrderPerBlock = 10;
    private const int DistrictRelocationOrder = 10;
    private const int DistrictCastleOrder = 80;

    [MenuItem("Tools/Settlement/Apply Districts")]
    public static void ApplyDistricts()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[SettlementSetup] 정착 설정이 없습니다. Tools/Settlement/Create Data를 먼저 실행하세요.");
            return;
        }
        var layout = LoadTerritoryLayout();
        if (layout == null || layout.districts == null)
        {
            Debug.LogError("[SettlementSetup] 지도 배치에 구역 블록이 없습니다 (python Tools/MapExpansion/build_territory_art.py).");
            return;
        }
        EnsureFolder(DistrictFolder);

        var blocks = new List<DistrictBlockDefinition>();
        int slotCount = 0;
        foreach (var row in DistrictTable)
        {
            var data = System.Array.Find(layout.districts, d => d.block == row.id);
            if (data == null)
            {
                Debug.LogError($"[SettlementSetup] 지도 배치에 구역 블록 '{row.id}'이(가) 없습니다.");
                continue;
            }
            var stages = new List<DistrictStage>();
            foreach (var stage in data.stages)
                stages.Add(new DistrictStage(stage.development, new[] { new RectInt(stage.x, stage.y, stage.w, stage.h) }));
            var slots = new List<DistrictSlot>();
            for (int i = 0; i < data.slots.Length; i++)
            {
                var s = data.slots[i];
                slots.Add(new DistrictSlot($"{row.id}_{i + 1:00}", new RectInt(s.x, s.y, s.w, s.h), s.stage));
            }
            slotCount += slots.Count;
            var block = LoadOrCreate<DistrictBlockDefinition>($"{DistrictFolder}/District_{row.id.Substring("district_".Length)}.asset").asset;
            block.Setup(row.id, row.name, row.fixedType, row.suggested, stages, slots);
            EditorUtility.SetDirty(block);
            blocks.Add(block);
        }

        config.SetupDistricts(blocks, DistrictType.Residential, DistrictOrderMatchWeight, DistrictOrderPerBlock,
            DistrictRelocationOrder, DistrictCastleOrder, DistrictBonusTable);
        config.SetupDistrictTypeUnlocks(DistrictTypeUnlockTable);
        EditorUtility.SetDirty(config);

        int tagged = ApplyBuildingDistricts();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 구역 적용 완료: 블록 {blocks.Count}개 · 부지 칸 {slotCount}개 · 구역 건물 {tagged}개");
    }

    /// <summary>건물 · 부탁 건물 자리에 구역을 붙임 (표에 없으면 None)</summary>
    private static int ApplyBuildingDistricts()
    {
        int tagged = 0;
        foreach (var folder in new[] { BuildingFolder, PlotFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:DecorDefinition", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DecorDefinition>(path);
                var district = BuildingDistricts.TryGetValue(asset.name, out var type) ? type : DistrictType.None;
                if (asset is BuildingDefinition building)
                    building.SetupDistrict(district);
                else if (asset is ConstructionPlotDefinition plot)
                    plot.SetupDistrict(district);
                else
                    continue;
                EditorUtility.SetDirty(asset);
                if (district != DistrictType.None)
                    tagged++;
            }
        }
        foreach (var name in BuildingDistricts.Keys)
        {
            if (AssetDatabase.LoadAssetAtPath<DecorDefinition>($"{BuildingFolder}/{name}.asset") == null
                && AssetDatabase.LoadAssetAtPath<DecorDefinition>($"{PlotFolder}/{name}.asset") == null)
                Debug.LogWarning($"[SettlementSetup] 구역을 붙일 건물이 없습니다: {name}");
        }
        return tagged;
    }
}
