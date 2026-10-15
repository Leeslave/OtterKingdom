using UnityEditor;
using UnityEngine;

/// <summary>
/// 밭 해금을 농업 지역으로 (구역·이사 5막, Docs/밭해금_재배치_기획.md). 표(SettlementSetup · QuestSetup · ShopSetup)는 이미 새 값이고,
/// 전체 설정을 다시 돌리지 않고 이미 있는 에셋만 새 값으로 맞춘다 (전체 재생성은 다른 데이터를 지울 수 있어서).
/// 여러 번 실행해도 같다. 메뉴: Tools/Settlement/Apply Farm Relocation · 배치 모드: -executeMethod SettlementSetup.ApplyFarmRelocation
/// </summary>
public static partial class SettlementSetup
{
    [MenuItem("Tools/Settlement/Apply Farm Relocation")]
    public static void ApplyFarmRelocation()
    {
        // 부탁: 밭 개간은 농업 지역 뒤 (순서는 성 뒤), 게시판 보강은 요정이 온 뒤, 새 이웃의 집은 밭을 발견하지 않음
        Patch<BoardRequestDefinition>($"{RequestFolder}/req_farmland.asset", so =>
        {
            so.FindProperty("_requiredDevelopment").stringValue = FarmDiscoverDevelopment;
            so.FindProperty("_order").intValue = 15;
        });
        Patch<BoardRequestDefinition>($"{RequestFolder}/req_assign_farmer.asset", so => so.FindProperty("_order").intValue = 16);
        Patch<BoardRequestDefinition>($"{RequestFolder}/req_upgrade_board.asset",
            so => so.FindProperty("_requiredDevelopment").stringValue = SettlementQuestGate.FairyDevelopment);
        Patch<BoardRequestDefinition>($"{RequestFolder}/req_neighbor_house.asset",
            so => so.FindProperty("_completionMessage").stringValue = "새 이웃의 집 완성!\n물감이가 이웃이 됐어요.");

        // 요정: 광부를 배치하면 초대 (농부 대신)
        Patch<SettlementOtterDefinition>($"{DataFolder}/Otters/Otter_Miner.asset", so => so.FindProperty("_assignDevelopment").stringValue = FairyInvitedDevelopment);
        Patch<SettlementOtterDefinition>($"{DataFolder}/Otters/Otter_Farmer.asset", so => so.FindProperty("_assignDevelopment").stringValue = "");

        // 밭 장소 · 밭 지역: 발견 = 농업 지역
        Patch<ZoneDefinition>("Assets/Scriptable Obejects/Navigation/Zone_Farm.asset", so =>
        {
            so.FindProperty("_requiredDevelopment").stringValue = FarmDiscoverDevelopment;
            var text = so.FindProperty("_developmentLockedSubtitle");
            if (text != null) text.stringValue = "농업 지역을 정하면 열려요";
        });
        Patch<ScriptableObject>($"{DataFolder}/Regions/Region_farm.asset", so => so.FindProperty("_discoverDevelopment").stringValue = FarmDiscoverDevelopment);

        // 농업 지역은 처음부터 고를 수 있음 (밭을 여는 쪽이 되었으므로)
        Patch<SettlementConfig>(ConfigPath, so =>
        {
            var unlocks = so.FindProperty("_districtTypeUnlocks");
            for (int i = unlocks.arraySize - 1; i >= 0; i--)
            {
                if (unlocks.GetArrayElementAtIndex(i).FindPropertyRelative("_district").enumValueIndex == (int)DistrictType.Agricultural)
                    unlocks.DeleteArrayElementAtIndex(i);
            }
        });

        // 모종: 밭이 열린 뒤 레벨로
        foreach (var (asset, level) in new[] { ("Seed_Potato", 11), ("Seed_SweetPotato", 12), ("Seed_Strawberry", 13) })
            Patch<ScriptableObject>($"Assets/Scriptable Obejects/Shop/Products/{asset}.asset", so => so.FindProperty("_requiredLevel").intValue = level);

        AssetDatabase.SaveAssets();
        QuestSetup.CreateData();
        Debug.Log("[SettlementSetup] 밭 재배치: 부탁 · 요정 · 밭 장소 · 농업 지역 · 모종 · 퀘스트 적용 완료");
    }

    private static void Patch<T>(string path, System.Action<SerializedObject> change) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogError($"[SettlementSetup] 없는 에셋: {path}");
            return;
        }
        var so = new SerializedObject(asset);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
}
