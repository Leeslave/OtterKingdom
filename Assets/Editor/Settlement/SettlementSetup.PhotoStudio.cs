using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 사진관 · 사진가 해달 찰칵이: 도감 진척도 50%부터 짓는 시설이고, 다 지으면(photo_studio_built) 찰칵이가 찾아온다 (StoryArrivalRules).
/// - 사진관 건물 행은 BuildingTable(SettlementSetup.Buildings.cs), 도감 진척도 조건 · 다 지으면 열리는 발전은 BuildingCollectionUnlocks
/// - 찰칵이 도감 항목은 PlazaLifeSetup.Entries, 광장 모습은 RiggedLookSetup(사진가 리그)
/// - 그림(임시): 사진관은 은퇴한 길드 접수소 그림 간판에 카메라를 그린 것 (Assets/Art/Settlement/Building_PhotoStudio.png — 같은 파일을 바꾸면 그대로 쓰임),
///   찰칵이 얼굴은 사진가 리그 앞모습(ArtSource/Otter/PhotographerRig/V1/unity-2.png)의 배경을 지운 것
/// 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    private const string PhotographerAsset = "Otter_Photographer";
    private const string PhotographerPortraitPath = "Assets/Art/Otter/ICON_Otter_Photographer.png";
    private const string PhotographerPrefabPath = PlazaPrefabFolder + "/PlazaOtter_PhotographerRigged.prefab";

    [MenuItem("Tools/Settlement/Apply Photo Studio")]
    public static void ApplyPhotoStudio()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[SettlementSetup] 정착 설정이 없습니다. Tools/Settlement/Create Data를 먼저 실행하세요.");
            return;
        }
        CreateBuildingData();
        var configSo = new SerializedObject(config);
        CreatePhotographer(configSo);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        PlazaLifeSetup.ApplyOtterEntries();
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementSetup] 사진관 · 찰칵이 적용 완료");
    }

    // 찰칵이: 사진관을 다 지으면 찾아오는 방문객 → 빈 집이 생기면 입주 (장난감 해달과 같은 흐름). 사진으로 남기는 기록 해달
    private static void CreatePhotographer(SerializedObject configSo)
    {
        string id = StoryArrivalRules.PhotographerOtterId;
        var entry = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/gb_{id}_arrival.asset").asset;
        var otter = LoadOrCreate<SettlementOtterDefinition>($"{DataFolder}/Otters/{PhotographerAsset}.asset").asset;

        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_entryId").stringValue = $"gb_{id}_arrival";
        entrySo.FindProperty("_otter").objectReferenceValue = otter;
        entrySo.FindProperty("_tag").stringValue = "첫 방문";
        entrySo.FindProperty("_message").stringValue = "사진관이 생겼다는 소식을 듣고 카메라를 메고 왔어요. 첫 사진은 광장이에요!";
        entrySo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = "찰칵이";
        so.FindProperty("_portrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(PhotographerPortraitPath);
        so.FindProperty("_plazaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PhotographerPrefabPath);
        so.FindProperty("_plazaTint").colorValue = Color.white;
        so.FindProperty("_trait").intValue = (int)OtterTrait.Recording;
        so.FindProperty("_toyVisitor").boolValue = true;
        so.FindProperty("_visitTier").intValue = 1;
        so.FindProperty("_homelessLine").stringValue = "현상할 방이 있으면 좋겠어요.\n빈 집이 생기면 불러 줘요!";
        so.FindProperty("_isBuilder").boolValue = false;
        so.FindProperty("_arrivalEntry").objectReferenceValue = entry;
        so.FindProperty("_moveInLine").stringValue = "창이 밝은 집이네요!\n여기서 살아도 될까요?";
        SetStrings(so.FindProperty("_lines"), new[] { "자, 웃어요! 찰칵!", "좋은 장면은 놓치지 않아요.", "오늘 광장 빛이 참 예뻐요." });
        so.ApplyModifiedPropertiesWithoutUndo();

        AddUnique(configSo.FindProperty("_otters"), otter);
        AddUnique(configSo.FindProperty("_guestbookEntries"), entry);
    }
}
