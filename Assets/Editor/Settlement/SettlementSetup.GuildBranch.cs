using UnityEditor;
using UnityEngine;

/// <summary>
/// 길드 분가 (구역·이사 기획안 5막): 마을 길드가 왕국의 성이 되면 길드는 상업 지역으로 나가 "길드 지부"를 연다.
/// 특성 건물(bld_guild_branch, 상업 지역에만) + 게시판 큰 부탁(req_guild_branch, 성 완성 뒤, 또박이). 그림은 예전 접수소 그림(Prop_GuildOffice).
/// 여러 번 실행해도 같다 (처음 만들 때만 값을 넣음). 메뉴: Tools/Settlement/Apply Guild Branch · 배치: -executeMethod SettlementSetup.ApplyGuildBranch
/// </summary>
public static partial class SettlementSetup
{
    private const string GuildBranchId = "bld_guild_branch";
    private const string GuildBranchRequestId = "req_guild_branch";
    private const string GuildBranchDevelopment = "guild_branch_built";

    [MenuItem("Tools/Settlement/Apply Guild Branch")]
    public static void ApplyGuildBranch()
    {
        var building = CreateGuildBranchBuilding();
        var request = CreateGuildBranchRequest(building);

        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>("Assets/Scriptable Obejects/Decor/DecorCatalog.asset");
        AddToList(catalog, "_buildings", building);
        AddToList(AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath), "_requests", request);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementSetup] 길드 분가: 길드 지부 건물 · 부탁 적용 완료");
    }

    private static BuildingDefinition CreateGuildBranchBuilding()
    {
        string path = $"{BuildingFolder}/Building_GuildBranch.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (existing != null)
            return existing;
        AssetDatabase.CopyAsset($"{BuildingFolder}/Building_Shop.asset", path);
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        var so = new SerializedObject(building);
        so.FindProperty("_buildingId").stringValue = GuildBranchId;
        so.FindProperty("_buildingName").stringValue = "길드 지부";
        so.FindProperty("_description").stringValue = "성에서 나온 길드가 상업 지역에 연 지부. 장사가 활발해져 물건을 더 비싸게 팔아요.";
        var branchArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/Detail4/GuildBranch.png");
        so.FindProperty("_icon").objectReferenceValue = branchArt;
        so.FindProperty("_worldSprite").objectReferenceValue = branchArt;
        so.FindProperty("_footprint").vector2IntValue = new Vector2Int(6, 4);
        so.FindProperty("_requiredDevelopment").stringValue = CastleRules.Built;
        so.FindProperty("_traits").arraySize = 0;
        so.FindProperty("_maxCount").intValue = 1;
        so.FindProperty("_gold").intValue = 2500;
        var items = so.FindProperty("_items");
        if (items.arraySize >= 2)
        {
            items.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 50;
            items.GetArrayElementAtIndex(1).FindPropertyRelative("_amount").intValue = 30;
        }
        so.FindProperty("_buildSeconds").floatValue = 300f;
        var bonuses = so.FindProperty("_bonuses");
        bonuses.arraySize = 1;
        bonuses.GetArrayElementAtIndex(0).FindPropertyRelative("_kind").enumValueIndex = (int)KingdomBonusKind.SellPrice;
        bonuses.GetArrayElementAtIndex(0).FindPropertyRelative("_percent").intValue = 5;
        so.FindProperty("_hostsFairyShop").boolValue = false;
        so.FindProperty("_district").enumValueIndex = (int)DistrictType.Commercial;
        so.ApplyModifiedPropertiesWithoutUndo();
        building.name = "Building_GuildBranch";
        return building;
    }

    private static BoardRequestDefinition CreateGuildBranchRequest(BuildingDefinition building)
    {
        string path = $"{RequestFolder}/{GuildBranchRequestId}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>(path);
        if (existing != null)
            return existing;
        AssetDatabase.CopyAsset($"{RequestFolder}/req_p4_shop.asset", path);
        var request = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>(path);
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = GuildBranchRequestId;
        so.FindProperty("_order").intValue = 18; // 성(17) 뒤
        so.FindProperty("_title").stringValue = "길드가 새 집을 찾아요";
        so.FindProperty("_description").stringValue = "마을 길드가 왕국의 성이 됐어요!\n길드는 상업 지역에 지부를 열고 싶대요.";
        so.FindProperty("_icon").objectReferenceValue = LoadArt("ICON_GuildOffice");
        var receptionist = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>($"{DataFolder}/Otters/Otter_Receptionist.asset");
        if (receptionist != null)
            so.FindProperty("_requester").objectReferenceValue = receptionist;
        so.FindProperty("_requiredDevelopment").stringValue = CastleRules.Built;
        so.FindProperty("_building").objectReferenceValue = building;
        so.FindProperty("_resultDevelopment").stringValue = GuildBranchDevelopment;
        so.FindProperty("_arrivals").arraySize = 0;
        so.FindProperty("_completionEntry").objectReferenceValue = null;
        so.FindProperty("_completionMessage").stringValue = "길드 지부가 문을 열었어요!\n상업 지역이 더 북적여요. (판매가 +5%)";
        so.FindProperty("_kingdomLevel").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        request.name = GuildBranchRequestId;
        return request;
    }

    private static void AddToList(Object owner, string property, Object item)
    {
        var so = new SerializedObject(owner);
        var list = so.FindProperty(property);
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }
}
