using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 성 승격 (구역·이사 기획안 5막): 건설(con_castle) · 큰 부탁(req_build_castle) 에셋, 정착 단계 "해달 왕국",
/// 광장의 성 공사 현장(마을 길드 현장 옆)과 성 그림(있으면). 여러 번 실행해도 같다 (수치는 처음 만들 때만, 고친 값은 지킴).
/// 성 그림: Assets/Art/Settlement/Prop_Castle.png — 없으면 공사는 되고 마을 길드 그림이 그대로 남음 (그림을 넣고 다시 실행)
/// 메뉴: Tools/Settlement/Apply Castle · 배치 모드: -executeMethod SettlementSetup.ApplyCastle
/// </summary>
public static partial class SettlementSetup
{
    private const string CastleConstructionId = "con_castle";
    private const string CastleStageName = "해달 왕국";
    private const string CastleArt = "Prop_Castle";
    private const string RequestFolder = DataFolder + "/Requests";

    [MenuItem("Tools/Settlement/Apply Castle")]
    public static void ApplyCastle()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var construction = CreateCastleConstruction();
        var request = CreateCastleRequest(construction);

        var so = new SerializedObject(config);
        var requests = so.FindProperty("_requests");
        bool listed = false;
        for (int i = 0; i < requests.arraySize; i++)
            listed |= requests.GetArrayElementAtIndex(i).objectReferenceValue == request;
        if (!listed)
        {
            requests.arraySize++;
            requests.GetArrayElementAtIndex(requests.arraySize - 1).objectReferenceValue = request;
        }
        var stages = so.FindProperty("_stageNames");
        bool hasStage = false;
        for (int i = 0; i < stages.arraySize; i++)
            hasStage |= stages.GetArrayElementAtIndex(i).stringValue == CastleStageName;
        if (!hasStage)
        {
            stages.arraySize++;
            stages.GetArrayElementAtIndex(stages.arraySize - 1).stringValue = CastleStageName;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        // 단계 번호 = "해달 왕국"의 자리
        var stageSo = new SerializedObject(request);
        for (int i = 0; i < stages.arraySize; i++)
        {
            if (stages.GetArrayElementAtIndex(i).stringValue == CastleStageName)
                stageSo.FindProperty("_stageOnComplete").intValue = i;
        }
        stageSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        PlaceCastleInPlaza();
        Debug.Log("[SettlementSetup] 성 승격: 건설 · 부탁 · 단계 · 광장 현장 적용 완료");
    }

    // 마을 길드 건설을 복사해 성으로 (재료는 길드의 1.6배, 비용 · 시간은 임시값)
    private static ConstructionDefinition CreateCastleConstruction()
    {
        string path = $"{ConstructionFolder}/{CastleConstructionId}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ConstructionDefinition>(path);
        if (existing != null)
            return existing;
        AssetDatabase.CopyAsset($"{ConstructionFolder}/con_town_hall.asset", path);
        var construction = AssetDatabase.LoadAssetAtPath<ConstructionDefinition>(path);
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = CastleConstructionId;
        so.FindProperty("_displayName").stringValue = "왕국의 성";
        so.FindProperty("_requiredGold").intValue = 3000;
        var items = so.FindProperty("_requiredItems");
        for (int i = 0; i < items.arraySize; i++)
        {
            var amount = items.GetArrayElementAtIndex(i).FindPropertyRelative("_amount");
            amount.intValue = Mathf.RoundToInt(amount.intValue * 1.6f);
        }
        so.FindProperty("_durationSeconds").floatValue = 180f;
        so.FindProperty("_progressLabel").stringValue = "왕국의 성을 세우는 중";
        so.FindProperty("_progressHint").stringValue = "다 지으면 마을 길드가 왕국의 성이 돼요";
        so.FindProperty("_unlockResultId").stringValue = CastleRules.Built;
        so.ApplyModifiedPropertiesWithoutUndo();
        construction.name = CastleConstructionId;
        return construction;
    }

    // 마을 길드 부탁을 복사해 성 부탁으로: 또박이(행정 해달)가 부탁, 성 준비(질서도 80)가 열려야 보임
    private static BoardRequestDefinition CreateCastleRequest(ConstructionDefinition construction)
    {
        string path = $"{RequestFolder}/{CastleRules.RequestId}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>(path);
        if (existing != null)
            return existing;
        AssetDatabase.CopyAsset($"{RequestFolder}/req_upgrade_town_hall.asset", path);
        var request = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>(path);
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = CastleRules.RequestId;
        so.FindProperty("_order").intValue = 17; // 밭 개간(15) · 새싹이(16) 뒤 — 성은 Lv.16부터라 게시판 다음 목표가 밭을 먼저 가리키게
        so.FindProperty("_title").stringValue = "왕국의 성을 세워요";
        so.FindProperty("_description").stringValue = "구역이 반듯하게 정리됐어요!\n마을 길드를 넓혀 해달 왕국의 성을 세워요.";
        var receptionist = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>($"{DataFolder}/Otters/Otter_Receptionist.asset");
        if (receptionist != null)
            so.FindProperty("_requester").objectReferenceValue = receptionist;
        so.FindProperty("_requiredDevelopment").stringValue = CastleRules.Ready;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.FindProperty("_completionEntry").objectReferenceValue = null;
        so.FindProperty("_completionMessage").stringValue = "왕국의 성이 완성됐어요!\n이제 우리 마을은 해달 왕국이에요.";
        so.ApplyModifiedPropertiesWithoutUndo();
        request.name = CastleRules.RequestId;
        return request;
    }

    // 광장: 마을 길드 현장을 복사해 성 현장 (건설 해달 · 먼지 · 말풍선), 성 그림이 있으면 길드 건물을 복사해 성으로 바꿔 끼움
    private static void PlaceCastleInPlaza()
    {
        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        ConstructionSiteView hallSite = null, castleSite = null;
        foreach (var site in Object.FindObjectsByType<ConstructionSiteView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (site.ConstructionId == "con_town_hall") hallSite = site;
            if (site.ConstructionId == CastleConstructionId) castleSite = site;
        }
        if (hallSite == null)
        {
            Debug.LogError("[SettlementSetup] 광장에 마을 길드 현장(Site_TownHall)이 없어 성 현장을 만들지 못했습니다.");
            return;
        }
        if (castleSite == null)
        {
            var copy = Object.Instantiate(hallSite.gameObject, hallSite.transform.parent);
            copy.name = "Site_Castle";
            SetSiteConstruction(copy.transform, CastleConstructionId);
            castleSite = copy.GetComponent<ConstructionSiteView>();
        }
        var view = Object.FindAnyObjectByType<SettlementPlazaView>(FindObjectsInactive.Include);
        var viewSo = new SerializedObject(view);
        var sites = viewSo.FindProperty("_sites");
        bool listed = false;
        for (int i = 0; i < sites.arraySize; i++)
            listed |= sites.GetArrayElementAtIndex(i).objectReferenceValue == castleSite;
        if (!listed)
        {
            sites.arraySize++;
            sites.GetArrayElementAtIndex(sites.arraySize - 1).objectReferenceValue = castleSite;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var guild = hallSite.transform.parent.Find("TownHall");
        var art = LoadPropArt(CastleArt);
        if (guild != null && art != null)
        {
            // 이미 있으면 그림 · 크기만 다시 맞춤 (그림을 바꾼 뒤 다시 실행)
            var existing = hallSite.transform.parent.Find("Castle");
            var castle = existing != null ? existing.gameObject : Object.Instantiate(guild.gameObject, guild.parent);
            castle.name = "Castle";
            var renderer = castle.GetComponent<SpriteRenderer>();
            renderer.sprite = art;
            // 길드보다 조금 크게 (같은 자리 · 같은 발밑)
            var guildSprite = guild.GetComponent<SpriteRenderer>().sprite;
            if (guildSprite != null)
                castle.transform.localScale = guild.localScale * (guildSprite.bounds.size.x / art.bounds.size.x) * 1.12f;
            castle.transform.position = guild.position;
            AddGate(castle, CastleRules.Built, true, true);
            SetSuperseded(castle, "");
            if (guild.GetComponent<DevelopmentGate>() != null)
                SetSuperseded(guild.gameObject, CastleRules.Built);
            EditorUtility.SetDirty(renderer);
        }
        else if (art == null)
        {
            Debug.LogWarning($"[SettlementSetup] 성 그림({ArtFolder}/{CastleArt}.png)이 없어 마을 길드 그림이 그대로 남습니다. 그림을 넣고 다시 실행하세요.");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
