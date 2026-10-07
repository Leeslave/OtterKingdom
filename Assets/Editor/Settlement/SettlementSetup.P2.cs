using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// P2 게시판 성장 · 관리 해달 · 마을회관 (구현 지시서: 해달왕국_P2_구현지시서.md, 작업기록: Docs/P2_게시판성장_작업기록.md).
/// - 데이터: 관리 역할(게시판 관리), 큰 부탁 묶음(마을 회의소 마련하기), 부탁의 분류·콘텐츠 버전·역할·주민 작업 연결, 설정의 P2 칸
///   (부탁·건설·작업·방명록 표는 SettlementSetup.cs / SettlementSetup.Tasks.cs에 함께 있음)
/// - 그림: Tools/UIGen/p2_sources → 또박이 얼굴·걷기 시트, 보강 게시판, 접수소, 마을회관, 아이콘 (SettlementSetup.P2Art)
/// - 광장: 게시판 보강 현장(같은 게시판이 보강 그림으로), 관리 해달 근무 자리, 게시판 주변 정리 현장, 공동 공간 정비 현장, 접수소 → 마을회관 부지, 바닷가 벤치
/// - 화면: 큰 부탁 팝업, 마을회관 발전 현황 팝업 (BuildPopups가 부름)
/// 한 번에 적용: Tools/Settlement/Apply P2 Progression (여러 번 실행해도 결과가 같음)
/// </summary>
public static partial class SettlementSetup
{
    #region ID · 표

    internal const string ReceptionistOtterId = "otter_receptionist";
    internal const string BoardUpgradeDevelopment = "board_upgraded";
    internal const string BoardManagedDevelopment = "receptionist_assigned";
    internal const string CommonSpaceDevelopment = "common_space_ready";
    internal const string GuildDevelopment = "guild_office_built";
    internal const string TownHallDevelopment = "town_hall_built";
    internal const string RestCornerDevelopment = "rest_corner_built";
    internal const string BoardAreaDevelopment = "board_area_tidy";
    internal const string ClerkPortraitName = "ICON_Otter_Clerk";

    private const int P2ContentVersion = 2;
    private const int ResidentRequestSlots = 2;
    private const string BoardRoleId = "role_board_manager";
    private const string BoardStationId = "station_board";
    private const string RoleFolder = DataFolder + "/Roles";
    private const string GroupFolder = DataFolder + "/Groups";
    private const string ClerkEntryPath = CollectionEntryFolder + "/Entry_OtterClerk.asset";

    // P2 부탁의 분류 · 맡길 역할 · 끝낼 주민 작업 (나머지 칸은 SettlementSetup.cs의 Requests 표). 표에 없는 부탁 = 메인 · 콘텐츠 버전 0
    private static readonly (string id, RequestCategory category, string role, string task)[] P2Requests =
    {
        ("req_upgrade_board", RequestCategory.Main, "", ""),
        ("req_assign_receptionist", RequestCategory.Main, BoardRoleId, ""),
        ("req_prepare_common_space", RequestCategory.Main, "", "task_common_space"),
        ("req_build_guild_office", RequestCategory.Main, "", ""),
        ("req_upgrade_town_hall", RequestCategory.Main, "", ""),
        ("req_rest_corner", RequestCategory.Resident, "", ""),
        ("req_tidy_board_area", RequestCategory.Resident, "", "task_tidy_board_area"),
    };

    // 큰 부탁 "마을 회의소 마련하기"의 단계 (부탁 ID 순서)
    private static readonly string[] TownCouncilSteps =
        { "req_upgrade_board", "req_assign_receptionist", "req_prepare_common_space", "req_build_guild_office", "req_upgrade_town_hall" };

    #endregion

    [MenuItem("Tools/Settlement/Apply P2 Progression")]
    public static void ApplyP2Progression()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 그림 → 또박이 광장 프리팹(걷기 시트) → 도감·정착 데이터(P2 표) · 전역 UI(게시판 구역, 큰 부탁·마을회관 화면) → 광장
        ImportP2Art();
        OtterVisitorSpriteSetup.BuildClerk();
        GlobalUISetup.Run();
        PlaceP2Plaza();
        Debug.Log("[SettlementSetup] P2 적용 완료: 그림, 또박이 광장 프리팹, 도감·정착 데이터, 전역 UI, 광장 P2 현장");
    }

    #region 데이터

    // CreateData 끝에서 부름 (작업은 CreateRegionData가 먼저 만듦)
    private static void CreateP2Data(SerializedObject configSo, Dictionary<string, SettlementOtterDefinition> otters, List<BoardRequestDefinition> requests)
    {
        EnsureFolder(RoleFolder);
        EnsureFolder(GroupFolder);

        var role = LoadOrCreate<ManagementRoleDefinition>($"{RoleFolder}/Role_board_manager.asset").asset;
        var roleSo = new SerializedObject(role);
        roleSo.FindProperty("_roleId").stringValue = BoardRoleId;
        roleSo.FindProperty("_displayName").stringValue = "게시판 관리";
        roleSo.FindProperty("_otter").objectReferenceValue = otters[ReceptionistOtterId];
        roleSo.FindProperty("_stationId").stringValue = BoardStationId;
        roleSo.FindProperty("_requiredDevelopment").stringValue = BoardUpgradeDevelopment;
        roleSo.FindProperty("_resultDevelopment").stringValue = BoardManagedDevelopment;
        roleSo.FindProperty("_askLine").stringValue = "게시판에 부탁이 잔뜩이네요!\n제가 맡아서 정리해도 될까요?";
        roleSo.FindProperty("_confirmLabel").stringValue = "게시판 관리 맡기기";
        var working = new[] { "부탁을 정리해 두었어요!", "새 부탁이 왔는지 볼까요?", "오늘도 게시판은 반짝반짝!" };
        var lines = roleSo.FindProperty("_workingLines");
        lines.arraySize = working.Length;
        for (int i = 0; i < working.Length; i++)
            lines.GetArrayElementAtIndex(i).stringValue = working[i];
        roleSo.ApplyModifiedPropertiesWithoutUndo();

        // 작업 ID → 작업 (CreateRegionData가 설정에 넣어 둠)
        var tasks = new Dictionary<string, SettlementTaskDefinition>();
        var taskList = configSo.FindProperty("_tasks");
        for (int i = 0; i < taskList.arraySize; i++)
        {
            if (taskList.GetArrayElementAtIndex(i).objectReferenceValue is SettlementTaskDefinition task)
                tasks[task.TaskId] = task;
        }

        var byId = new Dictionary<string, BoardRequestDefinition>();
        foreach (var request in requests)
        {
            byId[request.RequestId] = request;
            var extra = P2Requests.FirstOrDefault(p => p.id == request.RequestId);
            bool isP2 = extra.id != null;
            var so = new SerializedObject(request);
            so.FindProperty("_category").enumValueIndex = (int)(isP2 ? extra.category : RequestCategory.Main);
            so.FindProperty("_contentVersion").intValue = isP2 ? P2ContentVersion : 0;
            so.FindProperty("_assignRole").objectReferenceValue = isP2 && extra.role == BoardRoleId ? role : null;
            so.FindProperty("_completionTask").objectReferenceValue = isP2 && !string.IsNullOrEmpty(extra.task) ? tasks[extra.task] : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var group = LoadOrCreate<MilestoneGroupDefinition>($"{GroupFolder}/Group_town_council.asset").asset;
        var groupSo = new SerializedObject(group);
        groupSo.FindProperty("_groupId").stringValue = "group_town_council";
        groupSo.FindProperty("_title").stringValue = "마을 회의소 마련하기";
        groupSo.FindProperty("_description").stringValue = "게시판을 키우고 회의소를 세우기까지,\n마을이 함께 이루는 큰 부탁이에요.";
        groupSo.FindProperty("_icon").objectReferenceValue = LoadArt("ICON_TownHall");
        groupSo.FindProperty("_visibleDevelopment").stringValue = GuildDevelopment;
        SetList(groupSo.FindProperty("_steps"), TownCouncilSteps.Select(id => byId[id]).ToList());
        groupSo.ApplyModifiedPropertiesWithoutUndo();

        configSo.FindProperty("_boardUpgradeDevelopment").stringValue = BoardUpgradeDevelopment;
        configSo.FindProperty("_boardManagedDevelopment").stringValue = BoardManagedDevelopment;
        configSo.FindProperty("_guildDevelopment").stringValue = GuildDevelopment;
        configSo.FindProperty("_townHallDevelopment").stringValue = TownHallDevelopment;
        configSo.FindProperty("_residentRequestSlots").intValue = ResidentRequestSlots;
        SetList(configSo.FindProperty("_roles"), new List<ManagementRoleDefinition> { role });
        SetList(configSo.FindProperty("_milestoneGroups"), new List<MilestoneGroupDefinition> { group });
    }

    // 또박이: 역할을 맡기면 등록되는 도감 항목 (CollectionSetup.CreateData가 먼저 만듦)
    private static void LinkReceptionist(Dictionary<string, SettlementOtterDefinition> otters)
    {
        var entry = AssetDatabase.LoadAssetAtPath<CollectionEntry>(ClerkEntryPath);
        if (entry == null)
            Debug.LogWarning($"[SettlementSetup] 도감 항목이 없습니다: {ClerkEntryPath} (CollectionSetup.CreateData를 먼저 실행하세요)");
        var so = new SerializedObject(otters[ReceptionistOtterId]);
        so.FindProperty("_collectionEntry").objectReferenceValue = entry;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 광장

    private const string P2RootName = "P2";

    // 바닥 그림 픽셀 (Plaza_Ground.png, 왼쪽 위 원점). 실제 광장 배치를 그림으로 뽑아 빈 곳을 고름
    private static readonly Vector2 GuildPlotPixel = new Vector2(825, 692);      // 오른쪽 풀밭 (가로등·요정·사과나무 사이)
    private static readonly Vector2 CommonSpacePixel = new Vector2(215, 705);    // 왼쪽 모래밭 (울타리·사과나무 사이)
    private static readonly Vector2 RestCornerPixel = new Vector2(800, 1010);    // 바닷가 모래
    // 게시판 기준 (게시판 발밑 = 원점, 바닥 픽셀 단위. 아래로 +)
    private static readonly Vector2 BoardStationOffset = new Vector2(78, 62);
    private static readonly Vector2 BoardBuilderOffset = new Vector2(-72, 58);
    private static readonly Vector2 BoardBubbleOffset = new Vector2(0, 120);
    private static readonly Vector2 BoardTidyMarkerOffset = new Vector2(-82, -46);
    private static readonly Vector2[] BoardTidyStandOffsets = { new Vector2(-40, 64), new Vector2(10, 74) };
    private static readonly Vector2[] BoardLeafOffsets = { new Vector2(-36, 22), new Vector2(-8, 34), new Vector2(26, 26), new Vector2(50, 16) };
    private static readonly Vector2 BoardPebbleOffset = new Vector2(-58, 22);

    [MenuItem("Tools/Settlement/Setup P2 Plaza")]
    public static void PlaceP2Plaza()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        var root = plazaRoot.Find(RootName);
        if (root == null)
        {
            Debug.LogError("[SettlementSetup] 광장에 Settlement가 없습니다. Tools/Settlement/Setup Plaza를 먼저 실행하세요.");
            return;
        }
        var bounds = GroundBounds(plazaRoot.Find("Background").GetComponent<SpriteRenderer>());
        Vector3 ToWorld(Vector2 px) => new Vector3(bounds.min.x + px.x / GroundPixelsPerUnit, bounds.max.y - px.y / GroundPixelsPerUnit, 0f);
        BuildP2(root, plazaRoot.Find("Props"), root.GetComponent<SettlementPlazaView>(), ToWorld);
        ApplyPlotAnchors(plazaRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 광장 P2 배치 완료");
    }

    // Settlement 아래 P2를 다시 만들고 SettlementPlazaView의 현장·작업 현장·근무 자리 목록을 맞춤 (PlacePlaza도 끝에서 부름)
    private static void BuildP2(Transform root, Transform props, SettlementPlazaView view, System.Func<Vector2, Vector3> toWorld)
    {
        var old = root.Find(P2RootName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var p2 = new GameObject(P2RootName).transform;
        p2.SetParent(root, false);

        var board = root.Find("Board");
        Vector3 boardPos = board.position;
        Vector3 FromBoard(Vector2 px) => boardPos + new Vector3(px.x / GroundPixelsPerUnit, -px.y / GroundPixelsPerUnit, 0f);
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);

        var sites = new List<ConstructionSiteView>();
        var taskSites = new List<PlazaTaskSiteView>();

        // 1) 게시판 보강: 현장(먼지) + 보강되면 같은 게시판 오브젝트가 보강된 그림으로 (탭·발자국·부탁은 그대로라 공사 중에도 씀)
        var (boardSite, boardSiteSo) = CreateSite(p2, "Site_BoardUpgrade", "con_board_upgrade", boardPos,
            FromBoard(BoardBuilderOffset), FromBoard(BoardBubbleOffset), 2.6f);
        boardSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(boardSite);
        var swap = board.GetComponent<DevelopmentSpriteSwap>();
        if (swap == null)
            swap = board.gameObject.AddComponent<DevelopmentSpriteSwap>();
        var swapSo = new SerializedObject(swap);
        swapSo.FindProperty("_developmentId").stringValue = BoardUpgradeDevelopment;
        swapSo.FindProperty("_renderer").objectReferenceValue = board.GetComponent<SpriteRenderer>();
        swapSo.FindProperty("_unlockedSprite").objectReferenceValue = LoadPropArt("Prop_Board_Upgraded");
        swapSo.ApplyModifiedPropertiesWithoutUndo();

        // 2) 관리 해달 근무 자리 (게시판 오른쪽 앞)
        var station = new GameObject("BoardStation").AddComponent<ManagementStationView>();
        station.transform.SetParent(p2, false);
        station.transform.position = FromBoard(BoardStationOffset);
        var stationLook = Point(station.transform, "LookPoint", boardPos + new Vector3(0f, 0.6f, 0f));
        var stationSo = new SerializedObject(station);
        stationSo.FindProperty("_stationId").stringValue = BoardStationId;
        stationSo.FindProperty("_standPoint").objectReferenceValue = station.transform;
        stationSo.FindProperty("_lookPoint").objectReferenceValue = stationLook;
        stationSo.ApplyModifiedPropertiesWithoutUndo();

        // 3) 게시판 주변 정리 (선택 주민 부탁): 낙엽·잔돌 → 정리하면 사라짐
        var leaves = new GameObject("BoardLeaves").transform;
        leaves.SetParent(p2, false);
        foreach (var offset in BoardLeafOffsets)
            FlatSprite(leaves, "Leaf", LoadPropArt("FX_Leaf"), FromBoard(offset), 1.4f);
        FlatSprite(leaves, "Pebbles", LoadPropArt("Prop_Pebbles"), FromBoard(BoardPebbleOffset), 0.45f);
        AddGate(leaves.gameObject, BoardAreaDevelopment, false);
        taskSites.Add(BuildPlazaTaskSite(p2, "TaskSite_BoardArea", "task_tidy_board_area", config,
            FromBoard(BoardTidyMarkerOffset), BoardTidyStandOffsets.Select(FromBoard).ToArray(), boardPos, "hint_board_area"));

        // 4) 공동 공간 정비 (메인): 흩어진 통나무·돌 → 정비하면 둘러앉는 통나무 의자와 그루터기
        Vector3 common = toWorld(CommonSpacePixel);
        var clutter = new GameObject("CommonSpace_Clutter").transform;
        clutter.SetParent(p2, false);
        InstantiateProp("Log", clutter, common + new Vector3(-0.5f, 0.2f, 0f), PropScale(props, "Log") * 0.75f, true);
        InstantiateProp("Rock", clutter, common + new Vector3(0.9f, -0.15f, 0f), Vector3.one * 0.6f, false);
        InstantiateProp("Rock", clutter, common + new Vector3(-1.3f, -0.45f, 0f), Vector3.one * 0.5f, true);
        AddGate(clutter.gameObject, CommonSpaceDevelopment, false);
        var tidy = new GameObject("CommonSpace_Seats").transform;
        tidy.SetParent(p2, false);
        InstantiateProp("Log", tidy, common + new Vector3(-1.2f, 0.1f, 0f), PropScale(props, "Log") * 0.7f, false);
        InstantiateProp("Log", tidy, common + new Vector3(1.2f, 0.05f, 0f), PropScale(props, "Log") * 0.7f, true);
        InstantiateProp("Stump", tidy, common + new Vector3(0f, -0.3f, 0f), PropScale(props, "Stump") * 0.8f, false);
        AddGate(tidy.gameObject, CommonSpaceDevelopment, true, true);
        AddDecorBlock(tidy, tidy.InverseTransformPoint(common), new Vector2(3.6f, 1.6f));
        taskSites.Add(BuildPlazaTaskSite(p2, "TaskSite_CommonSpace", "task_common_space", config,
            common + new Vector3(0f, 1.6f, 0f),
            new[] { common + new Vector3(-0.8f, -1.6f, 0f), common + new Vector3(0.8f, -1.7f, 0f) }, common, "hint_common_space"));

        // 5) 접수소 → 마을회관 (같은 부지): 접수소는 회관이 생기면 숨김
        Vector3 plot = toWorld(GuildPlotPixel);
        var guildGroup = AssetDatabase.LoadAssetAtPath<MilestoneGroupDefinition>($"{GroupFolder}/Group_town_council.asset");
        var guild = BuildFacility(p2, "GuildOffice", "Prop_GuildOffice", plot, 1.5f, FacilityKind.Milestone, guildGroup);
        AddGate(guild.gameObject, GuildDevelopment, true, true);
        SetSuperseded(guild.gameObject, TownHallDevelopment);
        var hall = BuildFacility(p2, "TownHall", "Prop_TownHall", plot, 1.8f, FacilityKind.TownHall, null);
        AddGate(hall.gameObject, TownHallDevelopment, true, true);
        var guildRenderer = guild.GetComponent<SpriteRenderer>();
        var (guildSite, guildSiteSo) = CreateSite(p2, "Site_GuildOffice", "con_guild_office", plot,
            plot + new Vector3(-guildRenderer.bounds.size.x * 0.55f, -0.6f, 0f), plot + new Vector3(0f, -1.6f, 0f), guildRenderer.bounds.size.x * 0.8f);
        // 건설 예정지: 바닥에 깔린 주춧돌 (공사 내내)
        var foundationSprite = LoadPropArt("Prop_Foundation");
        var foundation = CreateSprite("Scaffold", guildSite.transform, foundationSprite, plot, true);
        foundation.transform.localScale = Vector3.one * (guildRenderer.bounds.size.x * 1.05f / foundationSprite.bounds.size.x);
        var foundationSo = new SerializedObject(foundation.GetComponent<PlazaProp>());
        foundationSo.FindProperty("flat").boolValue = true;
        foundationSo.ApplyModifiedPropertiesWithoutUndo();
        foundation.gameObject.SetActive(false);
        guildSiteSo.FindProperty("_scaffold").objectReferenceValue = foundation.gameObject;
        guildSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(guildSite);
        var (hallSite, hallSiteSo) = CreateSite(p2, "Site_TownHall", "con_town_hall", plot,
            plot + new Vector3(-guildRenderer.bounds.size.x * 0.6f, -0.7f, 0f), plot + new Vector3(0f, -1.6f, 0f), guildRenderer.bounds.size.x);
        hallSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(hallSite);
        AddDecorBlock(p2, p2.InverseTransformPoint(plot + new Vector3(0f, 1f, 0f)), new Vector2(guildRenderer.bounds.size.x * 1.1f, 2.4f));

        // 6) 바닷가 벤치 (선택 주민 부탁)
        Vector3 rest = toWorld(RestCornerPixel);
        var bench = InstantiateProp("Bench", p2, rest, PropScale(props, "Bench"), true);
        bench.name = "RestCorner_Bench";
        AddGate(bench, RestCornerDevelopment, true, true);
        var (restSite, restSiteSo) = CreateSite(p2, "Site_RestCorner", "con_rest_corner", rest,
            rest + new Vector3(1.5f, -0.3f, 0f), rest + new Vector3(0f, 2.2f, 0f), 2.4f);
        restSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(restSite);

        LinkP2ToView(view, sites, taskSites, new List<ManagementStationView> { station });

        // P3 (공동사업 현장·광장 확장): P2 목록을 다시 쓴 뒤에 붙임
        BuildP3(root, props, view, root.parent.Find("Background").GetComponent<SpriteRenderer>(), toWorld);
    }

    // 지난 P2(지운 것) 칸을 빼고 새 현장을 붙임
    private static void LinkP2ToView(SettlementPlazaView view, List<ConstructionSiteView> sites, List<PlazaTaskSiteView> taskSites,
        List<ManagementStationView> stations)
    {
        var so = new SerializedObject(view);
        var siteList = so.FindProperty("_sites");
        var kept = new List<ConstructionSiteView>();
        for (int i = 0; i < siteList.arraySize; i++)
        {
            if (siteList.GetArrayElementAtIndex(i).objectReferenceValue is ConstructionSiteView site && site != null && !sites.Contains(site))
                kept.Add(site);
        }
        kept.AddRange(sites);
        SetList(siteList, kept);
        SetList(so.FindProperty("_taskSites"), taskSites);
        SetList(so.FindProperty("_stations"), stations);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 광장 주민 작업 현장: 망치 표지판(누르면 작업 화면) + 일할 자리 + 남은 시간 말풍선
    private static PlazaTaskSiteView BuildPlazaTaskSite(Transform parent, string name, string taskId, SettlementConfig config,
        Vector3 markerPoint, Vector3[] standPoints, Vector3 lookPoint, string hintFlag)
    {
        var site = new GameObject(name).transform;
        site.SetParent(parent, false);
        site.position = lookPoint;
        var stands = new List<Transform>();
        for (int i = 0; i < standPoints.Length; i++)
            stands.Add(Point(site, $"Stand{i + 1}", standPoints[i]));
        var look = Point(site, "LookPoint", lookPoint);

        var marker = CreateSprite("Marker", site, LoadPropArt("UI_Bubble_Hammer"), markerPoint, false);
        marker.sortingOrder = OverlayOrder - 5;
        var tap = marker.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 0.8f;
        tap.offset = new Vector2(0f, 0.45f);
        var hint = CreateTapHint(site, hintFlag, "눌러서 해달을 보내요!", site.InverseTransformPoint(markerPoint) + new Vector3(0f, 1.1f, 0f));

        var bubble = CreateSprite("ProgressBubble", site, LoadPropArt("UI_Bubble_Speech"), markerPoint, false);
        bubble.transform.localScale = Vector3.one * 0.85f;
        bubble.sortingOrder = OverlayOrder - 4;
        var progress = CreateWorldText("Text", bubble.transform, new Vector3(0f, 1.05f, 0f), "공동 공간 정비\n0:30");
        progress.outlineWidth = 0f;
        progress.fontSize = 2.6f;
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 1.8f;
        progress.fontSizeMax = 2.6f;
        progress.rectTransform.sizeDelta = new Vector2(3.4f, 1.2f);
        progress.sortingOrder = OverlayOrder - 3;

        var view = site.gameObject.AddComponent<PlazaTaskSiteView>();
        var so = new SerializedObject(view);
        so.FindProperty("_task").objectReferenceValue = config != null ? config.FindTask(taskId) : null;
        SetList(so.FindProperty("_standPoints"), stands);
        so.FindProperty("_lookPoint").objectReferenceValue = look;
        so.FindProperty("_marker").objectReferenceValue = marker;
        so.FindProperty("_markerTap").objectReferenceValue = tap;
        so.FindProperty("_hint").objectReferenceValue = hint;
        so.FindProperty("_progressBubble").objectReferenceValue = bubble.gameObject;
        so.FindProperty("_progressText").objectReferenceValue = progress;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 마을 시설: 그림(발밑 피벗) + 바닥 발자국(그림 폭의 80% × 안쪽 깊이) + 탭 영역. 앞뒤 정렬은 발자국 가운데 기준 (벽 앞 해달이 가려지지 않게)
    private static SettlementFacilityPropView BuildFacility(Transform parent, string name, string art, Vector3 position,
        float footprintDepth, FacilityKind kind, MilestoneGroupDefinition group)
    {
        var renderer = CreateSprite(name, parent, LoadPropArt(art), position, true);
        var instance = renderer.gameObject;
        var bounds = renderer.bounds;
        AddFootprint(instance.transform, new Rect(-bounds.size.x * 0.4f, -0.15f, bounds.size.x * 0.8f, footprintDepth));
        SetDepthOffset(renderer, footprintDepth * 0.5f);

        var tap = instance.AddComponent<BoxCollider2D>();
        tap.size = new Vector2(bounds.size.x * 0.8f, bounds.size.y * 0.85f);
        tap.offset = new Vector2(0f, bounds.size.y * 0.45f);

        var view = instance.AddComponent<SettlementFacilityPropView>();
        var so = new SerializedObject(view);
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static GameObject InstantiateProp(string prop, Transform parent, Vector3 position, Vector3 scale, bool flipX)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/Prop_{prop}.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
        instance.transform.localScale = scale;
        if (flipX)
        {
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.flipX = true;
            // 발자국도 좌우로 뒤집음
            foreach (var polygon in instance.GetComponentsInChildren<PlazaAreaPolygon>(true))
                polygon.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        return instance;
    }

    // 광장에 이미 있는 같은 소품의 크기 (없으면 1)
    private static Vector3 PropScale(Transform props, string kind)
    {
        if (props == null)
            return Vector3.one;
        var existing = props.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(kind + "_"));
        return existing != null ? existing.localScale : Vector3.one;
    }

    // 바닥에 깔린 작은 그림 (낙엽·잔돌): 해달이 위로 지나감
    private static void FlatSprite(Transform parent, string name, Sprite sprite, Vector3 position, float scale)
    {
        var renderer = CreateSprite(name, parent, sprite, position, true);
        renderer.transform.localScale = Vector3.one * scale;
        var so = new SerializedObject(renderer.GetComponent<PlazaProp>());
        so.FindProperty("flat").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSuperseded(GameObject target, string development)
    {
        var so = new SerializedObject(target.GetComponent<DevelopmentGate>());
        so.FindProperty("_supersededBy").stringValue = development;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 화면

    // 게시판 부탁 목록의 구역 제목 템플릿 (메인 발전 / 주민 부탁 / 완료한 부탁)
    private static TextMeshProUGUI BuildSectionHeader(RectTransform content)
    {
        var header = Label("SectionHeaderTemplate", content, _titleFont, "지금 할 부탁", 32, LightBrown, TextAlignmentOptions.BottomLeft);
        header.margin = new Vector4(12, 0, 0, 4);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        header.gameObject.SetActive(false);
        return header;
    }

    // 큰 부탁: 제목판 · 설명 · "3 / 5 단계" · 단계 5줄 · 안내 · [가 보기]
    private static MilestonePopupView BuildMilestonePopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "MilestonePopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(920, 1300));
        panel.pivot = new Vector2(0.5f, 0.5f);

        var title = BuildTitleBoard(panel, "마을 회의소 마련하기");
        var close = BuildCloseButton(panel);

        var description = Label("Description", panel, _bodyFont, "게시판을 키우고 회의소를 세우기까지,\n마을이 함께 이루는 큰 부탁이에요.", 28, Body, TextAlignmentOptions.Center, 20);
        description.textWrappingMode = TextWrappingModes.Normal;
        TopBand(description.rectTransform, 60, 60, 120, 96);
        var progress = Label("Progress", panel, _titleFont, "0 / 5 단계", 36, Green, TextAlignmentOptions.Center, 24);
        TopBand(progress.rectTransform, 60, 60, 222, 52);

        var steps = new List<MilestoneStepRowView>();
        for (int i = 0; i < 5; i++)
            steps.Add(BuildStepRow(panel, i));

        var note = Label("Note", panel, _bodyFont, "다음: \"부탁이 많아졌어요\"", 28, Body, TextAlignmentOptions.Center, 18);
        note.textWrappingMode = TextWrappingModes.Normal;
        TopBand(note.rectTransform, 60, 60, 950, 80);

        var (go, goLabel, goImage) = BuildButton(panel, "GoButton", Common("UI_Button_Coin"), "가 보기", 40, Cocoa);
        Place(goImage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 44), new Vector2(520, 112));

        var view = screen.gameObject.AddComponent<MilestonePopupView>();
        Set(view, "_animator", animator);
        Set(view, "_titleText", title);
        Set(view, "_descriptionText", description);
        Set(view, "_progressText", progress);
        var so = new SerializedObject(view);
        SetList(so.FindProperty("_steps"), steps);
        so.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "_noteText", note);
        Set(view, "_goButton", go);
        Set(view, "_goLabel", goLabel);
        Set(view, "_closeButton", close);

        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    private static MilestoneStepRowView BuildStepRow(RectTransform panel, int index)
    {
        var row = CreateImage($"Step{index + 1}", panel, Common("UI_Button_Paper"), false);
        TopBand(row.rectTransform, 50, 50, 300 + index * 128, 112);
        var group = row.gameObject.AddComponent<CanvasGroup>();

        var highlight = CreateImage("Highlight", row.rectTransform, Common("UI_Chip_Selected"), false);
        Stretch(highlight.rectTransform, -4);
        highlight.color = new Color(1f, 1f, 1f, 0.9f);

        var circle = CreateImage("Number", row.rectTransform, Common("UI_RoundButton_Cream"), false);
        Place(circle.rectTransform, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(80, 80));
        var number = Label("Text", circle.rectTransform, _titleFont, $"{index + 1}", 34, Cocoa);
        Stretch(number.rectTransform, 6);

        var title = Label("Title", row.rectTransform, _titleFont, "부탁이 많아졌어요", 32, Cocoa, TextAlignmentOptions.Left, 20);
        title.rectTransform.anchorMin = new Vector2(0, 0);
        title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.offsetMin = new Vector2(116, 8);
        title.rectTransform.offsetMax = new Vector2(-200, -8);

        var chip = CreateImage("State", row.rectTransform, Common("UI_Tag_Highlight"), false);
        chip.pixelsPerUnitMultiplier = 1.6f;
        Place(chip.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(150, 54));
        var state = Label("Text", chip.rectTransform, _titleFont, "다음", 26, Cocoa, TextAlignmentOptions.Center, 16);
        Stretch(state.rectTransform, 4);
        state.rectTransform.offsetMin = new Vector2(4, 8);

        var check = CreateImage("Check", row.rectTransform, Common("UI_Button_Confirm"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(-8, 8), new Vector2(48, 48));

        var view = row.gameObject.AddComponent<MilestoneStepRowView>();
        Set(view, "_numberText", number);
        Set(view, "_titleText", title);
        Set(view, "_stateText", state);
        Set(view, "_check", check.gameObject);
        Set(view, "_highlight", highlight.gameObject);
        Set(view, "_group", group);
        return view;
    }

    // 마을회관: 정착 단계 · 마을 발전 n/m · 주민 현황 칩 3개 · 지금 하는 일 · 다음 목표 [가 보기] (또는 모두 마침 + 이동·꾸미기·도감) · 완료한 발전
    private static TownHallPopupView BuildTownHallPopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "TownHallPopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(940, 1420));
        panel.pivot = new Vector2(0.5f, 0.5f);

        BuildTitleBoard(panel, "마을회관");
        var close = BuildCloseButton(panel);

        var stage = Label("Stage", panel, _titleFont, "05 · 작은 마을", 32, Cocoa, TextAlignmentOptions.Left, 22);
        TopBand(stage.rectTransform, 64, 470, 130, 56);
        var progress = Label("Progress", panel, _bodyFont, "마을 발전 0 / 12", 28, Green, TextAlignmentOptions.Right, 20);
        TopBand(progress.rectTransform, 470, 64, 130, 56);

        SectionTitle(panel, "주민 현황", 196);
        var labor = CreateRect("Labor", panel);
        TopBand(labor, 50, 50, 246, 76);
        Row(labor, 16, TextAnchor.MiddleCenter);
        var settled = BuildStatChip(labor, "Settled", "정착 주민");
        var available = BuildStatChip(labor, "Available", "보낼 수 있음");
        var workingCount = BuildStatChip(labor, "Working", "작업 중");

        SectionTitle(panel, "지금 하는 일", 340);
        var work = Label("Work", panel, _bodyFont, "지금 진행 중인 일이 없어요.", 26, Body, TextAlignmentOptions.TopLeft, 18);
        work.textWrappingMode = TextWrappingModes.Normal;
        TopBand(work.rectTransform, 74, 64, 390, 140);

        SectionTitle(panel, "다음 목표", 546);
        var card = CreateImage("NextCard", panel, Common("UI_Box_Inset"), false);
        TopBand(card.rectTransform, 50, 50, 596, 260);

        var next = CreateRect("Next", card.rectTransform);
        Stretch(next, 0);
        var nextTitle = Label("Title", next, _titleFont, "우리 마을의 회의소", 34, Cocoa, TextAlignmentOptions.TopLeft, 22);
        TopBand(nextTitle.rectTransform, 28, 240, 24, 52);
        var nextDescription = Label("Description", next, _bodyFont, "", 26, Body, TextAlignmentOptions.TopLeft, 18);
        nextDescription.textWrappingMode = TextWrappingModes.Normal;
        TopBand(nextDescription.rectTransform, 28, 240, 84, 150);
        var (go, _, goImage) = BuildButton(next, "GoButton", Common("UI_Ribbon_Peach"), "가 보기", 32, Cocoa);
        goImage.pixelsPerUnitMultiplier = 1.4f;
        Place(goImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(200, 96));

        var done = CreateRect("AllDone", card.rectTransform);
        Stretch(done, 0);
        var doneText = Label("Message", done, _titleFont, "현재 준비된 마을 발전을 모두 마쳤어요!", 28, Cocoa, TextAlignmentOptions.Center, 18);
        doneText.textWrappingMode = TextWrappingModes.Normal;
        TopBand(doneText.rectTransform, 24, 24, 18, 120);
        var buttons = CreateRect("Buttons", done);
        BottomBand(buttons, 20, 96);
        buttons.offsetMin = new Vector2(24, buttons.offsetMin.y);
        buttons.offsetMax = new Vector2(-24, buttons.offsetMax.y);
        Row(buttons, 18, TextAnchor.MiddleCenter);
        var travel = BuildRowButton(buttons, "TravelButton", "생산하러 가기", 270);
        var decor = BuildRowButton(buttons, "DecorButton", "꾸미기", 230);
        var codex = BuildRowButton(buttons, "CodexButton", "도감", 230);
        done.gameObject.SetActive(false);

        SectionTitle(panel, "완료한 발전", 878);
        var completed = Label("Completed", panel, _bodyFont, "아직 완료한 발전이 없어요.", 26, Body, TextAlignmentOptions.TopLeft, 18);
        completed.textWrappingMode = TextWrappingModes.Normal;
        TopBand(completed.rectTransform, 74, 64, 928, 420);

        var view = screen.gameObject.AddComponent<TownHallPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_stageText", stage);
        Set(view, "_progressText", progress);
        Set(view, "_settledText", settled);
        Set(view, "_availableText", available);
        Set(view, "_workingText", workingCount);
        Set(view, "_workText", work);
        Set(view, "_nextGroup", next.gameObject);
        Set(view, "_nextTitleText", nextTitle);
        Set(view, "_nextDescriptionText", nextDescription);
        Set(view, "_goButton", go);
        Set(view, "_doneGroup", done.gameObject);
        Set(view, "_doneText", doneText);
        Set(view, "_travelButton", travel);
        Set(view, "_decorButton", decor);
        Set(view, "_codexButton", codex);
        Set(view, "_completedText", completed);
        Set(view, "_closeButton", close);

        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    // 패널 위에 걸친 제목판 (게시판과 같은 모양). 제목 글자를 돌려줌
    private static TextMeshProUGUI BuildTitleBoard(RectTransform panel, string text)
    {
        var titleBoard = CreateImage("TitleBoard", panel, ImportSprite(CollectionSpriteFolder, "UI_TitleBoard"), false);
        Place(titleBoard.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 54), new Vector2(560, 124));
        var title = Label("Title", titleBoard.rectTransform, _titleFont, text, 46, Cocoa, TextAlignmentOptions.Center, 28);
        Stretch(title.rectTransform, 10);
        title.rectTransform.offsetMin = new Vector2(10, 18);
        return title;
    }

    private static void SectionTitle(RectTransform panel, string text, float top)
    {
        var label = Label($"Section_{text}", panel, _titleFont, text, 30, LightBrown, TextAlignmentOptions.Left, 20);
        TopBand(label.rectTransform, 64, 64, top, 44);
    }

    // "정착 주민  3명" 칩 (값 글자를 돌려줌)
    private static TextMeshProUGUI BuildStatChip(RectTransform parent, string name, string key)
    {
        var bg = CreateImage(name, parent, Common("UI_Chip_Normal"), false);
        bg.pixelsPerUnitMultiplier = 1.4f;
        var element = bg.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 266;
        element.preferredHeight = 72;
        var keyText = Label("Key", bg.rectTransform, _bodyFont, key, 24, Body, TextAlignmentOptions.Left, 16);
        TopLeft(keyText.rectTransform, 20, 10, 150, 52);
        var valueText = Label("Value", bg.rectTransform, _titleFont, "0명", 30, Cocoa, TextAlignmentOptions.Right, 18);
        Place(valueText.rectTransform, new Vector2(1, 1), new Vector2(-20, -10), new Vector2(90, 52));
        valueText.rectTransform.pivot = new Vector2(1, 1);
        return valueText;
    }

    private static Button BuildRowButton(RectTransform parent, string name, string text, float width)
    {
        var (button, _, image) = BuildButton(parent, name, Common("UI_Button_Paper"), text, 28, Cocoa);
        image.pixelsPerUnitMultiplier = 1.4f;
        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = 92;
        return button;
    }

    #endregion
}
