using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 건설 모드 (Docs/건설모드_전환_작업기록.md): 부탁으로 짓는 광장 건물을 건설 모드에서 자리를 골라 짓게 하고, 게시판을 제자리에서 키운다.
/// - 건설 자리 에셋(ConstructionPlotDefinition)을 만들어 꾸미기 카탈로그의 _plots에 넣음. 기본 칸 · 미리보기 그림 · 크기는 씬의 건물에서 읽음
///   (첫 집 · 두 번째 집 · 의자 · 벤치 · 가로등 · 공동사업의 식탁 · 비축 상자 · 새 이웃의 집 · 환영 소품)
/// - 광장 씬의 공사 현장(가로등은 새 빈 오브젝트)에 ConstructionPlotAnchor를 붙여 함께 옮길 것을 연결
/// - 게시판 → 마을회관 → 마을 길드: 접수소·회관 묶음을 게시판 자리로 옮기고, 게시판 그림은 회관이 생기면 숨김. 건물을 눌러도 게시판이 열림
/// - 부탁 · 건설 · 방명록 · 큰 부탁 · 메인 퀘스트 문구를 표(SettlementSetup.cs)와 맞추고, 가로등 부탁을 만듦. 공동사업 문구의 "회관 앞" → 고른 자리 · 길드
/// - 나중에 고정 자리에 나타나는 요정 자리는 꾸미기 막음으로 처음부터 비워 둠
/// Setup Plaza · Setup P2 Plaza · Setup P3 Plaza 끝에서도 씬 부분을 부른다. 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    private const string PlotFolder = "Assets/Scriptable Obejects/Decor/Plots";
    private const string ConstructionFolder = "Assets/Scriptable Obejects/Settlement/Constructions";
    private const string QuestAssetFolder = "Assets/Scriptable Obejects/Quest/Quests";
    private const string PlotReservationsName = "PlotReservations";
    private const string LampSiteName = "Site_Lamp";

    // 에셋, 건설, 묶음 기준(공사 현장, PlazaRoot 아래 경로), 미리보기 그림, 함께 옮길 것, 칸 수
    // 기본 칸은 묶음의 발자국·꾸미기 막음을 덮는 칸으로 계산 (광장 격자: 칸 1, 왼쪽 아래 (-89, -17.4), 세이브 기준 칸 (77, 3))
    private static readonly (string asset, string construction, string site, string sprite, string[] followers, Vector2Int size)[] PlotTable =
    {
        ("Plot_House1", "con_house_1", "Settlement/Site_House1", "Props/House_Blue_01",
            new[] { "Props/House_Blue_01", "Settlement/NightLights/NightGlow_House_Blue_01" }, new Vector2Int(6, 4)),
        ("Plot_House2", "con_house_2", "Settlement/Site_House2", "Props/House_Red_01",
            new[] { "Props/House_Red_01", "Settlement/NightLights/NightGlow_House_Red_01" }, new Vector2Int(7, 4)),
        ("Plot_Chair", "con_chair", "Settlement/Site_Chair", "Props/Bench_01",
            new[] { "Props/Bench_01" }, new Vector2Int(3, 2)),
        ("Plot_RestCorner", "con_rest_corner", "Settlement/P2/Site_RestCorner", "Settlement/P2/RestCorner_Bench",
            new[] { "Settlement/P2/RestCorner_Bench" }, new Vector2Int(3, 2)),
        // 가로등은 공사 현장이 없어 늘 켜진 빈 오브젝트(Site_Lamp)가 묶음 기준 (가로등은 다 지어야 켜짐)
        ("Plot_Lamp", "con_lamp", "Settlement/" + LampSiteName, "Props/Lamp_01",
            new[] { "Props/Lamp_01", "Settlement/NightLights/NightGlow_Lamp_01" }, new Vector2Int(1, 1)),
        // 공동사업: 모임 자리는 식탁을 따라감
        ("Plot_CommunityTable", "con_p3_table", "Settlement/P3/Site_CommunityTable", "Settlement/P3/CommunityTable",
            new[] { "Settlement/P3/CommunityTable", "Settlement/P3/Gathering" }, new Vector2Int(5, 2)),
        ("Plot_SupplyBox", "con_p3_supply_box", "Settlement/P3/Site_SupplyBox", "Settlement/P3/SupplyBox",
            new[] { "Settlement/P3/SupplyBox" }, new Vector2Int(3, 2)),
        // 새 이웃의 집 · 환영 소품: 기본 자리는 처음 넓힌 영토 쪽 (TerritoryAnchor의 서쪽 · 북쪽 자리)
        ("Plot_P3House", "con_p3_house", "Settlement/P3/Neighborhood/Site_P3House", "Settlement/P3/Neighborhood/House_Blue_P3",
            new[] { "Settlement/P3/Neighborhood/House_Blue_P3" }, new Vector2Int(6, 4)),
        ("Plot_WelcomePot", "con_p3_flowerpot", "Settlement/P3/Neighborhood/Site_WelcomePot", "Settlement/P3/Neighborhood/WelcomePot",
            new[] { "Settlement/P3/Neighborhood/WelcomePot" }, new Vector2Int(1, 1)),
        ("Plot_WelcomeClothesline", "con_p3_clothesline", "Settlement/P3/Neighborhood/Site_WelcomeClothesline",
            "Settlement/P3/Neighborhood/WelcomeClothesline",
            new[] { "Settlement/P3/Neighborhood/WelcomeClothesline" }, new Vector2Int(4, 1)),
    };

    // 나중에 고정 자리에 나타나는 소품 (PlazaRoot 아래 경로, 요정은 씬에서 찾음): 그 소품의 꾸미기 막음(없으면 발자국)을 처음부터 켜 둠
    private static readonly string[] PlotReservedProps = new string[0];

    // 문구를 표와 맞출 부탁 · 건설 · 방명록 (가로등은 새로 만듦)
    private static readonly string[] BuildingModeConstructions = { "con_chair", "con_rest_corner", "con_board_upgrade", "con_guild_office", "con_town_hall", "con_lamp" };
    private static readonly string[] BuildingModeEntries = { "gb_rest_corner", "gb_board_upgrade", "gb_guild_office", "gb_town_hall", "gb_lamp" };
    private static readonly string[] BuildingModeRequests =
        { "req_chair", "req_rest_corner", "req_upgrade_board", "req_build_guild_office", "req_upgrade_town_hall", SettlementMigration.LampRequestId };

    // 메인 퀘스트 (QuestSetup 표와 같은 문구): 에셋, 제목, 설명, 아이콘
    private static readonly (string asset, string title, string description, string icon)[] BuildingModeQuests =
    {
        ("M05_Board", "부탁이 많아졌어요", "게시판을 마을회관으로 넓히기", "ICON_GuildOffice"),
        ("M06_Guild", "큰 부탁도 함께", "길드 접수 창구 만들기", "ICON_GuildOffice"),
        ("M07_TownHall", "우리 마을의 길드", "마을 길드로 넓히기", "ICON_TownHall"),
    };

    // 공동사업 · 생활 의뢰 문구 (SettlementSetup.P3.cs 표와 같음): 회관 앞 → 고른 자리 / 마을 길드
    private static readonly (string from, string to)[] P3TextFixes =
    {
        ("회관 앞에 우리 마을의 첫 비축 상자가 생겼어요.", "광장에 우리 마을의 첫 비축 상자가 생겼어요."),
        ("마을회관 앞 식탁에 모두 모여 첫 모임을 열었어요.", "광장 식탁에 모두 모여 첫 모임을 열었어요."),
        ("건설 해달이 회관 앞에 비축 상자를 놓아요.", "고른 자리에 건설 해달이 비축 상자를 놓아요."),
        ("건설 해달이 회관 앞에 공동 식탁을 놓아요.", "고른 자리에 건설 해달이 공동 식탁을 놓아요."),
        ("회관 앞에 비축 상자가 생겨요", "광장에 비축 상자가 생겨요"),
        ("회관 앞에 비축 상자가 생겼어요.", "광장에 비축 상자가 생겼어요."),
        ("회관 앞에 공동 식탁이 생기고", "광장에 공동 식탁이 생기고"),
        ("회관에서 마을 생활 준비를", "길드에서 마을 생활 준비를"),
        ("회관 주변 정리", "길드 주변 정리"),
        ("회관 앞을 쓸고", "길드 앞을 쓸고"),
        ("회관 앞이 깔끔해졌어요!", "길드 앞이 깔끔해졌어요!"),
        ("회관은 생겼는데", "길드는 생겼는데"),
        ("회관 살림을", "길드 살림을"),
        ("3번 마치면 회관 앞이", "3번 마치면 길드 앞이"),
        ("회관 물품 보충", "길드 물품 보충"),
        ("회관 앞이 어수선해요.", "길드 앞이 어수선해요."),
    };

    [MenuItem("Tools/Settlement/Apply Building Plots")]
    public static void ApplyBuildingPlots()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ApplyBuildingModeData();
        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        ApplyBoardExpansion(plazaRoot);
        if (!ApplyPlotAnchors(plazaRoot))
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 건설 모드 적용 완료: 건설 자리 {PlotTable.Length}곳, 게시판 확장");
    }

    #region 데이터

    // 부탁 · 건설 · 방명록 표의 해당 줄을 에셋에 다시 쓰고 가로등 부탁을 설정에 넣음, 큰 부탁 · 퀘스트 · 공동사업 문구
    private static void ApplyBuildingModeData()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var otters = config.Otters.Where(o => o != null).ToDictionary(o => o.OtterId);

        var entries = new Dictionary<string, GuestbookEntryDefinition>();
        foreach (var e in Entries)
        {
            var entry = AssetDatabase.LoadAssetAtPath<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/{e.id}.asset");
            if (entry != null)
                entries[e.id] = entry;
        }
        for (int i = 0; i < Entries.Length; i++)
        {
            if (!BuildingModeEntries.Contains(Entries[i].id))
                continue;
            var entry = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/{Entries[i].id}.asset").asset;
            ApplyEntryRow(i, entry, otters);
            entries[Entries[i].id] = entry;
        }

        var constructions = new Dictionary<string, ConstructionDefinition>();
        for (int i = 0; i < Constructions.Length; i++)
        {
            constructions[Constructions[i].id] = BuildingModeConstructions.Contains(Constructions[i].id)
                ? ApplyConstructionRow(i, wood, stone)
                : AssetDatabase.LoadAssetAtPath<ConstructionDefinition>($"{DataFolder}/Constructions/{Constructions[i].id}.asset");
        }

        var configSo = new SerializedObject(config);
        for (int i = 0; i < Requests.Length; i++)
        {
            if (!BuildingModeRequests.Contains(Requests[i].id))
                continue;
            var request = ApplyRequestRow(i, otters, constructions, entries);
            AddUnique(configSo.FindProperty("_requests"), request);
        }
        AddUnique(configSo.FindProperty("_guestbookEntries"), entries["gb_lamp"]);
        configSo.ApplyModifiedPropertiesWithoutUndo();

        var group = AssetDatabase.LoadAssetAtPath<MilestoneGroupDefinition>($"{GroupFolder}/Group_town_council.asset");
        if (group != null)
        {
            var groupSo = new SerializedObject(group);
            groupSo.FindProperty("_title").stringValue = GroupTitle;
            groupSo.FindProperty("_description").stringValue = GroupDescription;
            groupSo.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var q in BuildingModeQuests)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>($"{QuestAssetFolder}/{q.asset}.asset");
            if (quest == null)
                continue;
            var so = new SerializedObject(quest);
            so.FindProperty("_title").stringValue = q.title;
            so.FindProperty("_description").stringValue = q.description;
            so.FindProperty("_icon").objectReferenceValue = LoadArt(q.icon);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        int fixedTexts = 0;
        foreach (var folder in new[] { "Projects", "Tasks", "LifeRequests", "Guestbook", "Requests", "Constructions" })
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{DataFolder}/{folder}" }))
                fixedTexts += ReplaceTexts(AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)), P3TextFixes);
        }
        Debug.Log($"[SettlementSetup] 공동사업 문구 {fixedTexts}곳을 고쳤습니다.");
    }

    // 에셋의 모든 문자열 칸에서 바꿈 (배열 · 중첩 포함). 바꾼 칸 수
    private static int ReplaceTexts(Object asset, (string from, string to)[] fixes)
    {
        if (asset == null)
            return 0;
        var so = new SerializedObject(asset);
        var it = so.GetIterator();
        int count = 0;
        while (it.Next(true))
        {
            if (it.propertyType != SerializedPropertyType.String || string.IsNullOrEmpty(it.stringValue))
                continue;
            string text = it.stringValue;
            foreach (var (from, to) in fixes)
                text = text.Replace(from, to);
            if (text == it.stringValue)
                continue;
            it.stringValue = text;
            count++;
        }
        if (count > 0)
            so.ApplyModifiedPropertiesWithoutUndo();
        return count;
    }

    #endregion

    #region 게시판 → 마을회관 → 마을 길드

    // 접수소(= 마을회관 그림) · 회관(= 마을 길드 그림) 묶음을 게시판 자리로 옮기고, 게시판이 그 건물로 커지게 함
    private static void ApplyBoardExpansion(Transform plazaRoot)
    {
        var root = plazaRoot.Find(RootName);
        var board = root.Find("Board");
        var p2 = root.Find(P2RootName);
        var hallBuilding = p2.Find("GuildOffice");   // 마을회관 (게시판 확장)
        var guildBuilding = p2.Find("TownHall");     // 마을 길드 (회관 확장)
        if (board == null || hallBuilding == null || guildBuilding == null)
        {
            Debug.LogError("[SettlementSetup] 게시판 · 접수소 · 회관 중 없는 것이 있어 게시판 확장을 만들지 못했습니다.");
            return;
        }

        var group = new List<Transform> { hallBuilding, guildBuilding, p2.Find("Site_GuildOffice"), p2.Find("Site_TownHall"), p2.Find("DecorBlock") };
        var p3 = root.Find(P3RootName);
        if (p3 != null)
        {
            group.Add(p3.Find("TaskSite_HallTidy"));
            group.Add(p3.Find("HallDecor"));
        }
        Vector3 delta = board.position - hallBuilding.position;
        if (delta.sqrMagnitude > 0.0001f)
        {
            foreach (var t in group)
            {
                if (t != null)
                    t.position += delta;
            }
        }
        foreach (var t in group)
        {
            if (t == null)
                continue;
            foreach (var prop in t.GetComponentsInChildren<PlazaProp>(true))
            {
                prop.SetDepthOffset(prop.DepthOffset);
                EditorUtility.SetDirty(prop.GetComponent<SpriteRenderer>());
            }
        }

        // 공사 현장: 마을회관 = 접수소 터(주춧돌), 접수 창구 = 게시판 보강 현장(먼지만), 마을 길드 = 회관 현장
        SetSiteConstruction(p2.Find("Site_GuildOffice"), "con_board_upgrade");
        SetSiteConstruction(p2.Find("Site_BoardUpgrade"), "con_guild_office");
        SetSiteConstruction(p2.Find("Site_TownHall"), "con_town_hall");
        var oldAnchor = p2.Find("Site_GuildOffice").GetComponent<ConstructionPlotAnchor>();
        if (oldAnchor != null)
            Object.DestroyImmediate(oldAnchor);

        // 마을회관은 게시판 확장으로 나타나 마을 길드가 되면 숨김
        AddGate(hallBuilding.gameObject, BoardUpgradeDevelopment, true, true);
        SetSuperseded(hallBuilding.gameObject, TownHallDevelopment);
        AddGate(guildBuilding.gameObject, TownHallDevelopment, true, true);

        // 게시판 그림은 회관이 생기면 숨기고, 커진 건물을 눌러도 게시판이 열림 (큰 부탁 · 발전 현황은 게시판 카드에서)
        var swapSo = new SerializedObject(board.GetComponent<DevelopmentSpriteSwap>());
        swapSo.FindProperty("_developmentId").stringValue = BoardUpgradeDevelopment;
        swapSo.FindProperty("_hideWhenUnlocked").boolValue = true;
        swapSo.ApplyModifiedPropertiesWithoutUndo();
        var taps = new List<Collider2D>();
        foreach (var building in new[] { guildBuilding, hallBuilding })
        {
            var facility = building.GetComponent<SettlementFacilityPropView>();
            if (facility != null)
            {
                var tap = new SerializedObject(facility).FindProperty("_tapArea").objectReferenceValue as Collider2D;
                Object.DestroyImmediate(facility);
                if (tap != null)
                    taps.Add(tap);
            }
            else
            {
                var tap = building.GetComponent<Collider2D>();
                if (tap != null)
                    taps.Add(tap);
            }
        }
        var boardSo = new SerializedObject(board.GetComponent<SettlementBoardPropView>());
        var list = boardSo.FindProperty("_buildingTapAreas");
        list.arraySize = taps.Count;
        for (int i = 0; i < taps.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = taps[i];
        boardSo.ApplyModifiedPropertiesWithoutUndo();

        var hallRenderer = guildBuilding.GetComponent<SpriteRenderer>();
        Debug.Log($"[SettlementSetup] 게시판 확장: 마을 길드 그림 영역 {hallRenderer.bounds.min} ~ {hallRenderer.bounds.max}");
    }

    private static void SetSiteConstruction(Transform site, string constructionId)
    {
        var view = site != null ? site.GetComponent<ConstructionSiteView>() : null;
        if (view == null)
            return;
        var so = new SerializedObject(view);
        so.FindProperty("_constructionId").stringValue = constructionId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 건설 자리

    /// <summary>건설 자리 에셋 · 카탈로그 · 광장 씬의 묶음과 미리 비워 둘 자리를 맞춘다 (씬 저장은 부른 쪽)</summary>
    private static bool ApplyPlotAnchors(Transform plazaRoot)
    {
        var board = Object.FindAnyObjectByType<DecorBoardView>(FindObjectsInactive.Include);
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorCatalogPath);
        if (board == null || board.Board == null || catalog == null)
        {
            Debug.LogError("[SettlementSetup] 광장 꾸미기 격자나 카탈로그가 없어 건설 자리를 만들지 못했습니다.");
            return false;
        }
        if (!AssetDatabase.IsValidFolder(PlotFolder))
            AssetDatabase.CreateFolder("Assets/Scriptable Obejects/Decor", "Plots");
        EnsureLampSite(plazaRoot);

        Vector2 gridOrigin = board.transform.position;
        float cell = board.CellSize;
        var plots = new List<ConstructionPlotDefinition>();
        foreach (var row in PlotTable)
        {
            var site = plazaRoot.Find(row.site);
            var art = plazaRoot.Find(row.sprite);
            var construction = AssetDatabase.LoadAssetAtPath<ConstructionDefinition>($"{ConstructionFolder}/{row.construction}.asset");
            if (site == null || art == null || construction == null)
            {
                Debug.LogError($"[SettlementSetup] {row.asset}: 공사 현장 '{row.site}' · 그림 '{row.sprite}' · 건설 '{row.construction}' 중 없는 것이 있습니다.");
                continue;
            }
            var followers = new List<Transform>();
            foreach (var path in row.followers)
            {
                var follower = plazaRoot.Find(path);
                if (follower != null)
                    followers.Add(follower);
                else
                    Debug.LogWarning($"[SettlementSetup] {row.asset}: 함께 옮길 '{path}'이(가) 없습니다.");
            }

            // 기본 칸: 묶음의 발자국 · 꾸미기 막음을 가로 가운데로 덮고, 아랫줄은 발자국 앞쪽이 든 칸
            var bounds = FootprintBounds(site, followers, art);
            var layoutCell = new Vector2Int(
                Mathf.RoundToInt((bounds.center.x - row.size.x * cell * 0.5f - gridOrigin.x) / cell),
                Mathf.FloorToInt((bounds.yMin - gridOrigin.y) / cell + 0.001f));
            var area = new Rect(gridOrigin + (Vector2)layoutCell * cell, (Vector2)row.size * cell);
            var pivotOffset = (Vector2)site.position - new Vector2(area.center.x, area.yMin);
            var defaultCell = layoutCell - board.Board.SaveOrigin;

            // 영토 묶음 안: 북쪽을 먼저 넓히면 북쪽 자리 (서쪽 · 북쪽 자리 차이만큼)
            string alternateDevelopment = null;
            var alternateCell = default(Vector2Int);
            var territory = site.GetComponentInParent<TerritoryAnchor>(true);
            if (territory != null)
            {
                var tSo = new SerializedObject(territory);
                Vector3 west = tSo.FindProperty("_westPosition").vector3Value;
                Vector3 north = tSo.FindProperty("_northPosition").vector3Value;
                var shift = new Vector2Int(Mathf.RoundToInt((north.x - west.x) / cell), Mathf.RoundToInt((north.y - west.y) / cell));
                alternateDevelopment = tSo.FindProperty("_northDevelopment").stringValue;
                alternateCell = defaultCell + shift;
                if (((Vector2)(territory.transform.position - west)).sqrMagnitude > 0.0001f)
                    Debug.LogWarning($"[SettlementSetup] {row.asset}: 영토 묶음이 서쪽 자리에 있지 않습니다 (기본 칸이 서쪽 기준이 아님).");
            }

            var plot = LoadOrCreate<ConstructionPlotDefinition>($"{PlotFolder}/{row.asset}.asset").asset;
            var renderer = art.GetComponent<SpriteRenderer>();
            plot.SetupPlacement(row.size, false, renderer != null ? renderer.sprite : null);
            plot.SetupPlot(construction, defaultCell, pivotOffset, new Vector2(art.lossyScale.x, art.lossyScale.y), alternateDevelopment, alternateCell);
            EditorUtility.SetDirty(plot);
            plots.Add(plot);
            Debug.Log($"[SettlementSetup] {row.asset}: 기본 칸 {defaultCell} (격자 {layoutCell}) · 발밑 {pivotOffset}"
                + (alternateDevelopment != null ? $" · {alternateDevelopment} {alternateCell}" : ""));

            var anchor = site.GetComponent<ConstructionPlotAnchor>();
            if (anchor == null)
                anchor = site.gameObject.AddComponent<ConstructionPlotAnchor>();
            var so = new SerializedObject(anchor);
            so.FindProperty("_plot").objectReferenceValue = plot;
            SetTransforms(so.FindProperty("_followers"), followers);
            so.FindProperty("_reservations").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 카탈로그는 이 표대로 (게시판 자리로 옮긴 접수소 자리는 뺌)
        var catalogSo = new SerializedObject(catalog);
        var list = catalogSo.FindProperty("_plots");
        list.arraySize = plots.Count;
        for (int i = 0; i < plots.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = plots[i];
        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        foreach (var guid in AssetDatabase.FindAssets("t:ConstructionPlotDefinition", new[] { PlotFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!plots.Contains(AssetDatabase.LoadAssetAtPath<ConstructionPlotDefinition>(path)))
                AssetDatabase.DeleteAsset(path);
        }

        BuildPlotReservations(plazaRoot);
        return true;
    }

    // 가로등 묶음 기준: 늘 켜진 빈 오브젝트를 가로등 발밑에
    private static void EnsureLampSite(Transform plazaRoot)
    {
        var root = plazaRoot.Find(RootName);
        var lamp = plazaRoot.Find("Props/Lamp_01");
        if (lamp == null)
            return;
        var site = root.Find(LampSiteName);
        if (site == null)
        {
            site = new GameObject(LampSiteName).transform;
            site.SetParent(root, false);
        }
        site.position = lamp.position;
    }

    // 묶음의 Blocked 발자국 · 꾸미기 막음을 감싸는 영역 (없으면 그림 아래쪽)
    private static Rect FootprintBounds(Transform site, List<Transform> followers, Transform art)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var points = new List<Vector2>();
        foreach (var root in new[] { site }.Concat(followers))
        {
            foreach (var polygon in root.GetComponentsInChildren<PlazaAreaPolygon>(true))
            {
                if (polygon.Kind != PlazaAreaKind.Blocked)
                    continue;
                polygon.GetWorldPoints(points);
                foreach (var p in points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            foreach (var block in root.GetComponentsInChildren<DecorBlockArea>(true))
            {
                min = Vector2.Min(min, block.WorldRect.min);
                max = Vector2.Max(max, block.WorldRect.max);
            }
        }
        if (min.x > max.x)
        {
            var renderer = art.GetComponent<SpriteRenderer>();
            var b = renderer != null ? renderer.bounds : new Bounds(art.position, Vector3.one);
            return new Rect(b.min.x, art.position.y, b.size.x, 0.5f);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // 나중에 고정 자리에 나타나는 소품 자리를 처음부터 비워 둠 (소품이 꺼져 있는 동안에도 켜진 막음)
    private static void BuildPlotReservations(Transform plazaRoot)
    {
        var root = plazaRoot.Find(RootName);
        var old = root.Find(PlotReservationsName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject(PlotReservationsName).transform;
        holder.SetParent(root, false);

        var targets = new List<Transform>();
        foreach (var path in PlotReservedProps)
        {
            var target = plazaRoot.Find(path);
            if (target != null)
                targets.Add(target);
            else
                Debug.LogWarning($"[SettlementSetup] 미리 비워 둘 '{path}'이(가) 없습니다.");
        }
        var fairy = Object.FindAnyObjectByType<FairyNpcView>(FindObjectsInactive.Include);
        if (fairy != null)
            targets.Add(fairy.transform);

        foreach (var target in targets)
        {
            if (!TryGetReservedRect(target, out var rect))
            {
                Debug.LogWarning($"[SettlementSetup] '{target.name}'에 막음·발자국이 없어 미리 비워 두지 못했습니다.");
                continue;
            }
            var go = new GameObject($"Reserve_{target.name}");
            go.transform.SetParent(holder, false);
            go.transform.position = rect.center;
            var area = go.AddComponent<DecorBlockArea>();
            var so = new SerializedObject(area);
            so.FindProperty("_size").vector2Value = rect.size;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 소품의 꾸미기 막음 영역, 없으면 Blocked 발자국을 감싸는 영역 (월드)
    private static bool TryGetReservedRect(Transform target, out Rect rect)
    {
        var blocks = target.GetComponentsInChildren<DecorBlockArea>(true);
        if (blocks.Length > 0)
        {
            rect = blocks[0].WorldRect;
            for (int i = 1; i < blocks.Length; i++)
                rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, blocks[i].WorldRect.xMin), Mathf.Min(rect.yMin, blocks[i].WorldRect.yMin),
                    Mathf.Max(rect.xMax, blocks[i].WorldRect.xMax), Mathf.Max(rect.yMax, blocks[i].WorldRect.yMax));
            return true;
        }

        var points = new List<Vector2>();
        bool any = false;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var polygon in target.GetComponentsInChildren<PlazaAreaPolygon>(true))
        {
            if (polygon.Kind != PlazaAreaKind.Blocked)
                continue;
            polygon.GetWorldPoints(points);
            foreach (var p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                any = true;
            }
        }
        rect = any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        return any;
    }

    #endregion
}
