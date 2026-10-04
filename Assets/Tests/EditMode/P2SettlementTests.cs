using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// P2 게시판 성장 · 관리 해달 · 마을회관: 역할 맡기기(중복 없음), 노동력 제외, 주민 작업 → 부탁 완료(소급),
/// 게시판 등급·주민 부탁 칸, 큰 부탁 단계, 세이브 왕복, 버전 옮기기(P2를 대신 끝내지 않음).
/// </summary>
public class P2SettlementTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<GameObject> _objects = new List<GameObject>();

    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _painter;
    private SettlementOtterDefinition _builder;
    private SettlementOtterDefinition _farmer;
    private SettlementOtterDefinition _clerk;
    private ManagementRoleDefinition _role;
    private SettlementTaskDefinition _commonTask;
    private SettlementTaskDefinition _tidyTask;
    private BoardRequestDefinition _oldRequest;
    private BoardRequestDefinition _upgradeBoard;
    private BoardRequestDefinition _assignClerk;
    private BoardRequestDefinition _commonSpace;
    private BoardRequestDefinition _guildOffice;
    private BoardRequestDefinition _restCorner;
    private BoardRequestDefinition _tidyBoard;
    private MilestoneGroupDefinition _group;
    private SettlementConfig _config;

    [SetUp]
    public void SetUp()
    {
        var farm = Create<DevelopableRegionDefinition>();
        Set(farm, "_regionId", "region_farm");
        _first = Otter("otter_first", null, false);
        _painter = Otter("otter_painter", null, false);
        _builder = Otter("otter_builder", null, true);
        _farmer = Otter("otter_farmer", farm, false);
        _clerk = Otter("otter_receptionist", null, false);

        _role = Create<ManagementRoleDefinition>();
        Set(_role, "_roleId", "role_board_manager");
        SetRef(_role, "_otter", _clerk);
        Set(_role, "_stationId", "station_board");
        Set(_role, "_requiredDevelopment", "board_upgraded");
        Set(_role, "_resultDevelopment", "receptionist_assigned");

        _commonTask = Task("task_common_space", "receptionist_assigned", "common_space_ready");
        _tidyTask = Task("task_tidy_board_area", "receptionist_assigned", "board_area_tidy");

        _oldRequest = Request("req_farmland", 5, RequestCategory.Main, 0, "", Construction("con_farmland", "farmland"));
        _upgradeBoard = Request("req_upgrade_board", 7, RequestCategory.Main, 2, "farm_working", Construction("con_board_upgrade", "board_upgraded"));
        _assignClerk = Request("req_assign_receptionist", 8, RequestCategory.Main, 2, "board_upgraded", null);
        SetRef(_assignClerk, "_assignRole", _role);
        _commonSpace = Request("req_prepare_common_space", 9, RequestCategory.Main, 2, "receptionist_assigned", null);
        SetRef(_commonSpace, "_completionTask", _commonTask);
        _guildOffice = Request("req_build_guild_office", 10, RequestCategory.Main, 2, "common_space_ready", Construction("con_guild_office", "guild_office_built"));
        _restCorner = Request("req_rest_corner", 20, RequestCategory.Resident, 2, "receptionist_assigned", Construction("con_rest_corner", "rest_corner_built"));
        _tidyBoard = Request("req_tidy_board_area", 21, RequestCategory.Resident, 2, "receptionist_assigned", null);
        SetRef(_tidyBoard, "_completionTask", _tidyTask);

        _group = Create<MilestoneGroupDefinition>();
        Set(_group, "_groupId", "group_town_council");
        Set(_group, "_visibleDevelopment", "guild_office_built");
        SetList(_group, "_steps", new Object[] { _upgradeBoard, _assignClerk, _commonSpace, _guildOffice });

        _config = Create<SettlementConfig>();
        SetRef(_config, "_firstOtter", _first);
        SetList(_config, "_otters", new Object[] { _first, _painter, _builder, _farmer, _clerk });
        SetList(_config, "_requests", new Object[] { _tidyBoard, _restCorner, _guildOffice, _commonSpace, _assignClerk, _upgradeBoard, _oldRequest });
        SetList(_config, "_tasks", new Object[] { _commonTask, _tidyTask });
        SetList(_config, "_roles", new Object[] { _role });
        SetList(_config, "_milestoneGroups", new Object[] { _group });
        Set(_config, "_boardUpgradeDevelopment", "board_upgraded");
        Set(_config, "_boardManagedDevelopment", "receptionist_assigned");
        Set(_config, "_guildDevelopment", "guild_office_built");
        Set(_config, "_townHallDevelopment", "town_hall_built");
        SetInt(_config, "_residentRequestSlots", 2);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _objects)
            Object.DestroyImmediate(go);
        _objects.Clear();
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
        typeof(SettlementManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private static void Set(Object target, string property, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string property, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetList(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private SettlementOtterDefinition Otter(string id, DevelopableRegionDefinition workRegion, bool builder)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_workRegion").objectReferenceValue = workRegion;
        so.FindProperty("_isBuilder").boolValue = builder;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private SettlementTaskDefinition Task(string id, string requires, string result)
    {
        var task = Create<SettlementTaskDefinition>();
        var so = new SerializedObject(task);
        so.FindProperty("_taskId").stringValue = id;
        so.FindProperty("_requiredWorkers").intValue = 1;
        so.FindProperty("_durationSeconds").floatValue = 30f;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_resultDevelopment").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private ConstructionDefinition Construction(string id, string result)
    {
        var construction = Create<ConstructionDefinition>();
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = id;
        so.FindProperty("_durationSeconds").floatValue = 30f;
        so.FindProperty("_needsBuilder").boolValue = true;
        so.FindProperty("_unlockResultId").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return construction;
    }

    private BoardRequestDefinition Request(string id, int order, RequestCategory category, int contentVersion, string requires,
        ConstructionDefinition construction)
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_title").stringValue = id;
        so.FindProperty("_order").intValue = order;
        so.FindProperty("_category").enumValueIndex = (int)category;
        so.FindProperty("_contentVersion").intValue = contentVersion;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
    }

    // 농부가 일하기 시작한 P1 끝 상태 (주민: 몽실·물감이·뚝딱이·새싹이)
    private static Settlement P1Done()
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_first", ResidentState.Resident);
        settlement.SetResident("otter_painter", ResidentState.Resident);
        settlement.SetResident("otter_builder", ResidentState.Resident);
        settlement.SetResident("otter_farmer", ResidentState.Resident);
        settlement.UnlockDevelopment("farmland");
        settlement.UnlockDevelopment("farm_working");
        settlement.CompleteRequest("req_farmland", "farmland");
        settlement.MarkMet("otter_first");
        return settlement;
    }

    // 게시판을 보강했고 또박이가 찾아와 광장에서 만난 상태
    private static Settlement ClerkMet()
    {
        var settlement = P1Done();
        settlement.CompleteRequest("req_upgrade_board", "board_upgraded");
        settlement.SetResident("otter_receptionist", ResidentState.SpecialNpc);
        settlement.MarkMet("otter_receptionist");
        return settlement;
    }

    // 전역 UI 없이 정착 매니저만 (Awake를 직접 부름)
    private SettlementManager Manager(SettlementSaveData save)
    {
        var go = new GameObject("SettlementManager");
        _objects.Add(go);
        go.SetActive(false);
        var manager = go.AddComponent<SettlementManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_config").objectReferenceValue = _config;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(SettlementManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        manager.LoadFromSave(save);
        return manager;
    }

    private static SettlementSaveData Saved(Settlement settlement)
    {
        var save = new SettlementSaveData();
        settlement.Write(save);
        return save;
    }

    #endregion

    #region 관리 역할

    [Test]
    public void Role_NeedsMeetingAndUpgradedBoard()
    {
        var settlement = P1Done();
        Assert.IsFalse(SettlementRoleRules.CanAssign(_role, settlement), "게시판 보강 전");

        settlement.CompleteRequest("req_upgrade_board", "board_upgraded");
        settlement.SetResident("otter_receptionist", ResidentState.SpecialNpc);
        Assert.IsFalse(SettlementRoleRules.CanAssign(_role, settlement), "찾아왔지만 광장에서 아직 만나지 않음 (방문 대기)");

        settlement.MarkMet("otter_receptionist");
        Assert.IsTrue(SettlementRoleRules.CanAssign(_role, settlement));
    }

    [Test]
    public void Role_AssignTwice_OnlyOnce_AndOpensResult()
    {
        var settlement = ClerkMet();

        Assert.IsTrue(SettlementRoleRules.Assign(_role, settlement));
        Assert.IsFalse(SettlementRoleRules.Assign(_role, settlement), "연타해도 한 번만");
        Assert.AreEqual(1, settlement.Roles.Count);
        Assert.IsTrue(settlement.HasDevelopment("receptionist_assigned"));
        Assert.IsTrue(settlement.TryGetRole("role_board_manager", out var assignment));
        Assert.AreEqual("otter_receptionist", assignment.OtterId);
        Assert.AreEqual("station_board", assignment.StationId);
    }

    [Test]
    public void Role_OneOtterHoldsOneRole()
    {
        var settlement = ClerkMet();
        Assert.IsTrue(settlement.AssignRole("role_board_manager", "otter_receptionist", "station_board"));
        Assert.IsFalse(settlement.AssignRole("role_other", "otter_receptionist", "station_other"), "한 해달은 한 자리만");
        Assert.IsFalse(settlement.AssignRole("role_board_manager", "otter_painter", "station_board"), "한 역할은 한 해달만");
    }

    [Test]
    public void RoleHolder_IsNotSentToResidentTasks()
    {
        var settlement = ClerkMet();
        // 주민이 되더라도 역할을 맡은 해달은 노동력에서 빠짐
        settlement.SetResident("otter_receptionist", ResidentState.Resident);
        Assert.IsTrue(SettlementRegionRules.CanWork(_clerk, settlement), "역할을 맡기 전에는 보낼 수 있는 주민");

        SettlementRoleRules.Assign(_role, settlement);
        Assert.IsFalse(SettlementRegionRules.CanWork(_clerk, settlement));
        Assert.IsFalse(SettlementRegionRules.CanWork(_builder, settlement), "건설 해달은 그대로 제외");
        Assert.IsFalse(SettlementRegionRules.CanWork(_farmer, settlement), "전문 해달은 그대로 제외");
        Assert.IsTrue(SettlementRegionRules.CanWork(_painter, settlement));
    }

    [Test]
    public void Save_RoundTrip_KeepsRolesAndVersion()
    {
        var settlement = ClerkMet();
        SettlementRoleRules.Assign(_role, settlement);
        settlement.SetVersion(SettlementSaveData.CurrentVersion);

        var loaded = new Settlement();
        loaded.Load(Saved(settlement));

        Assert.AreEqual(SettlementSaveData.CurrentVersion, loaded.Version);
        Assert.IsTrue(loaded.TryGetRole("role_board_manager", out var assignment));
        Assert.AreEqual("otter_receptionist", assignment.OtterId);
        Assert.AreEqual("station_board", assignment.StationId);
        Assert.IsTrue(loaded.HasRole("otter_receptionist"));
    }

    [Test]
    public void Load_DropsBrokenRoleLines()
    {
        var save = new SettlementSaveData
        {
            roles = new List<RoleAssignmentSaveData>
            {
                null,
                new RoleAssignmentSaveData { roleId = "", otterId = "otter_a" },
                new RoleAssignmentSaveData { roleId = "role_a", otterId = "otter_a", stationId = "s" },
                new RoleAssignmentSaveData { roleId = "role_a", otterId = "otter_b", stationId = "s" }, // 같은 역할 두 번
                new RoleAssignmentSaveData { roleId = "role_b", otterId = "otter_a", stationId = "s" }, // 같은 해달 두 역할
            },
        };
        var settlement = new Settlement();
        settlement.Load(save);

        Assert.AreEqual(1, settlement.Roles.Count);
        Assert.IsTrue(settlement.TryGetRole("role_a", out var role));
        Assert.AreEqual("otter_a", role.OtterId);
    }

    #endregion

    #region 부탁 · 주민 작업

    [Test]
    public void Action_IsExactlyOne()
    {
        Assert.AreEqual(RequestAction.Construction, _upgradeBoard.Action);
        Assert.AreEqual(RequestAction.AssignRole, _assignClerk.Action);
        Assert.AreEqual(RequestAction.ResidentTask, _commonSpace.Action);

        SetRef(_commonSpace, "_construction", Construction("con_x", "x"));
        Assert.IsFalse(_commonSpace.IsValidAction, "건설과 주민 작업을 함께 채우면 잘못된 데이터");
        Assert.AreEqual(RequestAction.None, _commonSpace.Action);
    }

    [Test]
    public void CompletionDevelopment_FollowsAction()
    {
        Assert.AreEqual("board_upgraded", SettlementRules.CompletionDevelopment(_upgradeBoard));
        Assert.AreEqual("receptionist_assigned", SettlementRules.CompletionDevelopment(_assignClerk));
        Assert.AreEqual("common_space_ready", SettlementRules.CompletionDevelopment(_commonSpace));
    }

    [Test]
    public void TaskRequest_IsInProgress_WhileTaskRuns()
    {
        var settlement = ClerkMet();
        SettlementRoleRules.Assign(_role, settlement);
        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_commonSpace, settlement));

        settlement.StartTask("task_common_space", new[] { "otter_painter" }, 0, 100);
        Assert.AreEqual(RequestStatus.Building, SettlementRules.GetStatus(_commonSpace, settlement));
    }

    [Test]
    public void Reconcile_CompletesFromTaskAndRoleRecords_Once()
    {
        var settlement = ClerkMet();
        // 역할은 맡겼고 작업도 끝났는데 부탁 완료 전에 꺼진 세이브
        settlement.AssignRole("role_board_manager", "otter_receptionist", "station_board");
        settlement.UnlockDevelopment("receptionist_assigned");
        settlement.StartTask("task_common_space", new[] { "otter_painter" }, 0, 100);
        settlement.FinishTask("task_common_space", "common_space_ready");

        var completed = new List<BoardRequestDefinition>();
        SettlementMigration.ReconcileRecords(_config, settlement, completed);
        CollectionAssert.AreEquivalent(new[] { _assignClerk, _commonSpace }, completed);
        Assert.IsTrue(settlement.IsCompleted("req_assign_receptionist"));
        Assert.IsTrue(settlement.IsCompleted("req_prepare_common_space"));

        SettlementMigration.ReconcileRecords(_config, settlement, completed);
        Assert.AreEqual(0, completed.Count, "다시 불러와도 한 번만");
        Assert.IsFalse(settlement.IsCompleted("req_tidy_board_area"), "기록이 없는 부탁은 그대로");
    }

    [Test]
    public void FindCurrent_IgnoresOptionalResidentRequests()
    {
        var settlement = ClerkMet();
        SettlementRoleRules.Assign(_role, settlement);
        settlement.CompleteRequest("req_assign_receptionist", "receptionist_assigned");
        // 주민 부탁이 순서상 앞에 있어도
        SetInt(_restCorner, "_order", 1);

        Assert.AreEqual(_commonSpace, SettlementRules.FindCurrent(_config, settlement), "주민 부탁은 메인 진행(안내 띠·다음 목표)이 아님");
    }

    #endregion

    #region 게시판 · 큰 부탁

    [Test]
    public void Board_Tier_FollowsDevelopments()
    {
        var settlement = P1Done();
        Assert.AreEqual(BoardTier.Basic, SettlementBoardRules.GetTier(_config, settlement));
        settlement.UnlockDevelopment("board_upgraded");
        Assert.AreEqual(BoardTier.Upgraded, SettlementBoardRules.GetTier(_config, settlement));
        settlement.UnlockDevelopment("receptionist_assigned");
        Assert.AreEqual(BoardTier.Managed, SettlementBoardRules.GetTier(_config, settlement));
        settlement.UnlockDevelopment("guild_office_built");
        Assert.AreEqual(BoardTier.Guild, SettlementBoardRules.GetTier(_config, settlement));
    }

    [Test]
    public void Board_BeforeManager_OneListWithoutResidentRequests()
    {
        var settlement = P1Done();
        var rows = new List<BoardRequestRow>();
        SettlementBoardRules.CollectRows(_config, settlement, rows);

        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual(_oldRequest, rows[0].Request, "순서대로");
        Assert.AreEqual(RequestStatus.Completed, rows[0].Status);
        Assert.AreEqual(_upgradeBoard, rows[1].Request);
        Assert.AreEqual(RequestStatus.Available, rows[1].Status);
    }

    [Test]
    public void Board_ResidentSlots_NeverHideMainOrInProgress()
    {
        var settlement = ClerkMet();
        SettlementRoleRules.Assign(_role, settlement);
        settlement.CompleteRequest("req_assign_receptionist", "receptionist_assigned");
        // 주민 부탁 칸을 1개로 줄이고, 주민 부탁 하나는 진행 중
        SetInt(_config, "_residentRequestSlots", 1);
        settlement.StartTask("task_tidy_board_area", new[] { "otter_painter" }, 0, 100);

        var rows = new List<BoardRequestRow>();
        SettlementBoardRules.CollectRows(_config, settlement, rows);

        var main = rows.FindAll(r => r.Section == BoardSection.Main);
        var resident = rows.FindAll(r => r.Section == BoardSection.Resident);
        Assert.AreEqual(1, main.Count);
        Assert.AreEqual(_commonSpace, main[0].Request, "메인 부탁은 칸과 상관없이 보임");
        Assert.AreEqual(1, resident.Count, "칸 1개를 진행 중인 부탁이 차지 → 할 수 있는 주민 부탁은 안 보임");
        Assert.AreEqual(_tidyBoard, resident[0].Request);
        Assert.AreEqual(RequestStatus.Building, resident[0].Status);

        SetInt(_config, "_residentRequestSlots", 0);
        SettlementBoardRules.CollectRows(_config, settlement, rows);
        Assert.IsTrue(rows.Exists(r => r.Request == _tidyBoard), "칸이 0이어도 진행 중인 부탁은 늘 보임");
    }

    [Test]
    public void Board_Managed_SectionsAreOrderedAndStable()
    {
        var settlement = ClerkMet();
        SettlementRoleRules.Assign(_role, settlement);
        settlement.CompleteRequest("req_assign_receptionist", "receptionist_assigned");

        var rows = new List<BoardRequestRow>();
        SettlementBoardRules.CollectRows(_config, settlement, rows);
        var sections = rows.ConvertAll(r => r.Section);
        Assert.AreEqual(new[] { BoardSection.Main, BoardSection.Resident, BoardSection.Resident,
            BoardSection.Completed, BoardSection.Completed, BoardSection.Completed }, sections.ToArray());
        Assert.AreEqual(_restCorner, rows[1].Request, "주민 부탁은 순서(20 → 21)대로");
        Assert.AreEqual(_tidyBoard, rows[2].Request);

        var again = new List<BoardRequestRow>();
        SettlementBoardRules.CollectRows(_config, settlement, again);
        Assert.AreEqual(rows.ConvertAll(r => r.Request), again.ConvertAll(r => r.Request), "다시 그려도 같은 순서");
    }

    [Test]
    public void Milestone_StepsComeFromRecords()
    {
        var settlement = ClerkMet();
        Assert.IsFalse(SettlementBoardRules.IsGroupVisible(_group, settlement), "접수소 전에는 안 보임");

        var steps = new List<(BoardRequestDefinition, MilestoneStepState)>();
        SettlementBoardRules.CollectSteps(_group, settlement, steps);
        Assert.AreEqual(MilestoneStepState.Done, steps[0].Item2);
        Assert.AreEqual(MilestoneStepState.Next, steps[1].Item2);
        Assert.AreEqual(MilestoneStepState.Locked, steps[2].Item2);
        Assert.AreEqual(1, SettlementBoardRules.CountDone(_group, settlement));
        Assert.AreEqual(_assignClerk, SettlementBoardRules.CurrentStep(_group, settlement));
    }

    #endregion

    #region 세이브 옮기기

    [Test]
    public void OldSaveCompleteAll_DoesNotCompleteP2()
    {
        var settlement = new Settlement();
        SettlementRules.CompleteAll(_config, settlement);

        Assert.IsTrue(settlement.IsCompleted("req_farmland"), "그때 있던 부탁은 끝낸 것으로");
        foreach (var request in new[] { _upgradeBoard, _assignClerk, _commonSpace, _guildOffice, _restCorner, _tidyBoard })
            Assert.IsFalse(settlement.IsCompleted(request.RequestId), $"{request.RequestId}는 처음부터");
        Assert.IsFalse(settlement.HasDevelopment("board_upgraded"));
    }

    [Test]
    public void Migrate_OnlyOnce_AndGivesNothing()
    {
        var settlement = P1Done();
        Assert.AreEqual(0, settlement.Version);

        Assert.IsTrue(SettlementMigration.MigrateToCurrent(settlement));
        Assert.IsFalse(SettlementMigration.MigrateToCurrent(settlement), "한 번만");
        Assert.AreEqual(SettlementSaveData.CurrentVersion, settlement.Version);
        Assert.IsFalse(settlement.HasDevelopment("board_upgraded"));
        Assert.AreEqual(0, settlement.Roles.Count);
        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_upgradeBoard, settlement), "farm_working이 있으니 P2 첫 부탁만 열림");
        Assert.AreEqual(RequestStatus.Locked, SettlementRules.GetStatus(_assignClerk, settlement));
    }

    #endregion

    #region 매니저

    [Test]
    public void Manager_LoadOldSave_OpensOnlyFirstP2Request()
    {
        var old = Saved(P1Done());
        old.version = 0;
        old.roles = null; // 필드가 없던 세이브
        var manager = Manager(old);

        Assert.AreEqual(SettlementSaveData.CurrentVersion, manager.Settlement.Version);
        Assert.AreEqual(_upgradeBoard, manager.CurrentRequest);
        Assert.AreEqual(BoardTier.Basic, manager.BoardTier);
        Assert.IsFalse(manager.IsTownHallOpen);
    }

    [Test]
    public void Manager_AssignRoleTwice_OnceAndCompletesRequest()
    {
        var manager = Manager(Saved(ClerkMet()));
        int completed = 0;
        int assigned = 0;
        manager.OnRequestCompleted += _ => completed++;
        manager.OnRoleAssigned += _ => assigned++;

        Assert.IsTrue(manager.CanAssignRole(_clerk));
        Assert.IsTrue(manager.TryAssignRole(_role));
        Assert.IsFalse(manager.TryAssignRole(_role), "연타");
        Assert.AreEqual(1, assigned);
        Assert.AreEqual(1, completed);
        Assert.IsTrue(manager.Settlement.IsCompleted("req_assign_receptionist"));
        Assert.AreEqual(_role, manager.AssignedRoleOf(_clerk));
        Assert.AreEqual(BoardTier.Managed, manager.BoardTier);
    }

    [Test]
    public void Manager_PlazaTaskFinish_CompletesRequestOnce()
    {
        var settlement = ClerkMet();
        settlement.AssignRole("role_board_manager", "otter_receptionist", "station_board");
        settlement.UnlockDevelopment("receptionist_assigned");
        settlement.CompleteRequest("req_assign_receptionist", "receptionist_assigned");
        var manager = Manager(Saved(settlement));
        Assert.IsTrue(manager.IsPlazaTask(_commonTask));

        manager.Settlement.StartTask("task_common_space", new[] { "otter_painter" }, SettlementManager.NowTicks, SettlementManager.NowTicks + 1000);
        Assert.AreEqual(_commonTask, manager.PlazaTaskOf("otter_painter"));
        bool announced = false;
        manager.OnTaskFinished += (_, elsewhere) => announced = elsewhere;
        manager.DevFinishTasks();

        Assert.IsTrue(manager.Settlement.IsCompleted("req_prepare_common_space"));
        Assert.IsTrue(manager.HasDevelopment("common_space_ready"));
        Assert.IsTrue(announced, "부탁 완료 팝업이 대신 알림");
        Assert.AreEqual(_guildOffice, manager.CurrentRequest);
    }

    [Test]
    public void Manager_LaborSummary_MatchesWhoCanBeSent()
    {
        var settlement = ClerkMet();
        settlement.SetResident("otter_receptionist", ResidentState.Resident);
        settlement.AssignRole("role_board_manager", "otter_receptionist", "station_board");
        settlement.UnlockDevelopment("receptionist_assigned");
        settlement.StartTask("task_tidy_board_area", new[] { "otter_painter" }, SettlementManager.NowTicks, SettlementManager.NowTicks + 1_000_000_000L);
        var manager = Manager(Saved(settlement));

        var labor = manager.GetLaborSummary();
        var candidates = new List<SettlementOtterDefinition>();
        manager.CollectResidents(candidates);
        int canSend = candidates.FindAll(manager.CanAssign).Count;

        Assert.AreEqual(5, labor.Settled, "정착 주민 = 몽실·물감이·뚝딱이·새싹이·또박이");
        Assert.AreEqual(2, labor.Workforce, "건설·전문·관리 해달 제외 = 몽실·물감이");
        Assert.AreEqual(canSend, labor.Available, "작업 화면에서 실제로 보낼 수 있는 수와 같음");
        Assert.AreEqual(1, labor.Available);
        Assert.AreEqual(1, labor.Working);
    }

    #endregion
}
