using System.Collections.Generic;
using UnityEngine;

/// <summary>왕국 화면 [발전] 탭의 다음 목표 한 장 (무엇을 할지 + [가 보기]가 어디로 가는지)</summary>
public enum KingdomGoalKind { Info, Request, Project, MineStory, Scouting, AllDone }

public readonly struct KingdomGoal
{
    public KingdomGoalKind Kind { get; }
    public string Title { get; }
    public string Text { get; }
    public Sprite Icon { get; }
    public BoardRequestDefinition Request { get; }
    public CommunityProjectDefinition Project { get; }
    public bool CanGo => Kind != KingdomGoalKind.Info && Kind != KingdomGoalKind.AllDone;

    public KingdomGoal(KingdomGoalKind kind, string title, string text, Sprite icon = null,
        BoardRequestDefinition request = null, CommunityProjectDefinition project = null)
    {
        Kind = kind;
        Title = title;
        Text = text;
        Icon = icon;
        Request = request;
        Project = project;
    }
}

/// <summary>다음 레벨에 필요한 마을 부탁과 갱도 돌파 조건</summary>
public readonly struct LevelGate
{
    public int NextLevel { get; }
    public int Exp { get; }
    public int Need { get; }
    public BoardRequestDefinition Request { get; }
    public int MineFloor { get; }
    public string MineObjective => $"갱도 지하 {MineFloor}층 돌파하기";
    public string Objectives => Request == null ? MineObjective
        : MineFloor == 0 ? $"‘{Request.Title}’ 완료하기" : $"{MineObjective}\n‘{Request.Title}’ 완료하기";
    /// <summary>경험치는 다 찼는데 레벨업 조건이 남은 상태</summary>
    public bool Blocked => Exp >= Need;

    public LevelGate(int nextLevel, int exp, int need, BoardRequestDefinition request, int mineFloor)
    {
        NextLevel = nextLevel;
        Exp = exp;
        Need = need;
        Request = request;
        MineFloor = mineFloor;
    }
}

/// <summary>지금 하는 일 한 줄 (건설 · 주민 작업 · 정찰)</summary>
public readonly struct KingdomWork
{
    public string Title { get; }
    public string Detail { get; }
    public Sprite Icon { get; }
    /// <summary>0~1 (모르면 -1)</summary>
    public float Progress { get; }
    public string Remaining { get; }

    public KingdomWork(string title, string detail, Sprite icon, float progress, string remaining)
    {
        Title = title;
        Detail = detail;
        Icon = icon;
        Progress = progress;
        Remaining = remaining;
    }
}

/// <summary>
/// 왕국 화면(KingdomSheet): 예전 마을회관 발전 현황 화면이 그리던 값을 계산해 돌려준다 (그리기는 KingdomSheet).
/// 다음 목표 · 레벨 조건 · 영토 확장 · 지금 하는 일 · 완료한 발전 · 이사 지시서
/// </summary>
public partial class SettlementManager
{
    public const string AllDoneMessage = "현재 준비된 마을 발전을 모두 마쳤어요!\n밭·광산에서 생산하고, 광장을 꾸미고, 도감을 채워 봐요.";

    private KingdomSheet _kingdomSheet;

    public bool IsKingdomOpen => _kingdomSheet != null && _kingdomSheet.IsOpen;

    /// <summary>왕국 화면을 연다 (프로필 레벨 · 왕국 단계 칩 · 마을회관 건물 · 게시판 "마을 발전 현황")</summary>
    public void OpenKingdom(KingdomSheet.Tab tab = KingdomSheet.Tab.Progress, ProductionSheet.Tab work = ProductionSheet.Tab.Production)
    {
        if (!IsLoaded) return;
        if (_kingdomSheet == null) _kingdomSheet = gameObject.AddComponent<KingdomSheet>();
        _kingdomSheet.Open(this, tab, work);
    }

    /// <summary>상단바 레벨 · 왕국 단계 칩: 마을회관이 있으면 왕국 화면, 없으면 다음 레벨 조건을 알림</summary>
    public void OpenKingdomOrHint()
    {
        if (!IsLoaded)
            return;
        if (IsTownHallOpen)
        {
            OpenKingdom();
            return;
        }
        var game = GameManager.Instance;
        if (game == null)
            return;
        game.ShowAlert(TryGetLevelGate(out var gate)
            ? $"Lv.{gate.NextLevel} 조건\n{gate.Objectives}"
            : "마을회관을 지으면 왕국 화면에서 발전 · 주민 · 구역을 볼 수 있어요.");
    }

    public void CloseKingdom()
    {
        if (_kingdomSheet != null) _kingdomSheet.Close();
    }

    /// <summary>마을회관 그림 (창 제목 아이콘): 마을회관 공사의 아이콘</summary>
    public Sprite TownHallIcon
    {
        get
        {
            foreach (var request in _config.Requests)
            {
                if (request != null && request.Construction != null && request.Construction.name == "con_town_hall")
                    return request.Construction.Icon;
            }
            return null;
        }
    }

    #region 다음 목표

    /// <summary>회관의 다음 목표 (정찰 → 공동사업 → 광산 이야기 → 모두 마침 → 부탁 → 개간 먼저 → 레벨 잠금)</summary>
    public KingdomGoal NextGoal()
    {
        var target = CurrentRequest;
        var project = target == null ? ActiveProject : null;
        bool mine = project == null && target == null && HasMineCommunityStory && !MineCommunity.exported;
        bool scouting = target == null && HasIslandMap && PlayerLevel >= SettlementRules.FishingEventLevel
            && HasDevelopment(SettlementQuestGate.FarmDevelopment) && !HasDevelopment("fishing_found");
        if (scouting)
            return new KingdomGoal(KingdomGoalKind.Scouting, "발견한 바닷가로 길 내기",
                "섬 지도의 남쪽으로 정찰대를 보내요. 발견한 땅을 개간하고 영토 확장 미션을 마치면 선착장을 복구할 수 있어요.");
        if (project != null)
            return new KingdomGoal(KingdomGoalKind.Project, ProjectTitle(project), ProjectNextText(project), project.Icon, project: project);
        if (mine)
            return new KingdomGoal(KingdomGoalKind.MineStory, MineCommunityTitle, MineCommunityStatus, TownHallIcon);
        if (AreAllMainDone)
            return new KingdomGoal(KingdomGoalKind.AllDone, "모두 마쳤어요", AllDoneMessage);
        if (target != null)
            return new KingdomGoal(KingdomGoalKind.Request, target.Title, target.Description, RequestIcon(target), target);
        if (TerritoryFirstGoal(out var capTitle, out var capText))
            return new KingdomGoal(KingdomGoalKind.Info, capTitle, capText, RequestIcon(LevelCapRequest));
        if (FindLevelLockedRequest(out int lockedLevel) is BoardRequestDefinition locked)
            return new KingdomGoal(KingdomGoalKind.Info, locked.Title, $"왕국 Lv.{lockedLevel}에 열려요. 퀘스트로 레벨을 올려요.", RequestIcon(locked));
        return new KingdomGoal(KingdomGoalKind.Info, "다음 발전을 준비하고 있어요", "진행 중인 일이 끝나면 새 부탁이 열려요.");
    }

    /// <summary>[가 보기]: 그 목표를 하러 가는 기존 화면을 엶</summary>
    public void GoTo(KingdomGoal goal)
    {
        switch (goal.Kind)
        {
            case KingdomGoalKind.Scouting: OpenSouthernMap(); break;
            case KingdomGoalKind.MineStory: OpenMineCommunity(); break;
            case KingdomGoalKind.Project: RequestProject(goal.Project); break;
            case KingdomGoalKind.Request: RequestOpen(goal.Request); break;
        }
    }

    public static Sprite RequestIcon(BoardRequestDefinition request) =>
        request == null ? null : request.Icon != null ? request.Icon : request.Construction != null ? request.Construction.Icon : null;

    // 레벨을 묶은 부탁이 아직 게시판에 없고(앞 발전 전) 숲 개간으로 이어갈 수 있을 때: 먼 레벨 잠금(선착장 Lv.15) 대신 그 부탁과 개간을 알림.
    // 4차 QA: Lv.10에서 경험치가 넘쳐도 "다음 목표: 선착장 Lv.15"만 보여 숲 개간 → 영토 확장 → 첫 모임 길을 몰랐음
    private bool TerritoryFirstGoal(out string title, out string text)
    {
        title = text = null;
        var cap = LevelCapRequest;
        if (cap == null || string.IsNullOrEmpty(cap.RequiredDevelopment) || Settlement.HasDevelopment(cap.RequiredDevelopment))
            return false;
        bool canClear = OpenTerritoryMissions.Count > 0 || WorkingTerritory(out _) != null
            || (TerritoryChances > 0 && AnyTerritoryLeft);
        if (!canClear)
            return false;
        title = cap.Title;
        text = $"끝내면 다음 레벨(Lv.{cap.KingdomLevel})로 올라가요.\n먼저 숲을 개간해 땅을 넓혀요. (아래 땅 넓히기)";
        return true;
    }

    // 공동사업 한 줄: 주민 대사 + 지금 할 일 (레벨이 모자라면 필요한 레벨)
    private string ProjectNextText(CommunityProjectDefinition project)
    {
        var status = GetProjectStatus(project);
        string step;
        if (status.Phase == ProjectPhase.NeedsLevel)
            step = $"Lv.{project.RequiredLevel}부터 시작할 수 있어요 (지금 Lv.{PlayerLevel}).";
        else if (status.Phase == ProjectPhase.Delivering)
            step = "재료를 조금씩 나눠 넣을 수 있어요.";
        else
        {
            var stage = CurrentStage(project);
            step = stage != null ? $"지금 할 일: {stage.Label}" : "마무리하는 중이에요.";
        }
        return string.IsNullOrEmpty(project.Line) ? step : $"{project.Line}\n{step}";
    }

    #endregion

    #region 레벨 조건

    /// <summary>다음 레벨이 마을 부탁이나 갱도에 묶여 있으면 그 조건 (경험치가 덜 찼어도 알려 줌)</summary>
    public bool TryGetLevelGate(out LevelGate gate)
    {
        gate = default;
        var profile = ProfileManager.Instance;
        if (!IsLoaded || profile == null || profile.LevelTable == null)
            return false;
        var progress = profile.Progress;
        if (progress.Level >= profile.LevelTable.MaxLevel || progress.Level < progress.Cap
            || progress.Level >= CastleReleaseLevelCap(progress.Level))
            return false;
        int nextLevel = progress.Level + 1;
        var request = LevelCapRequest;
        if (request != null && request.KingdomLevel != nextLevel)
            request = null;
        var floors = MineMazeLinkConfig.Instance?.Floors;
        int mineFloor = MineMazeRules.FindBlockingFloor(floors, Settlement, progress.Level);
        if (mineFloor != 0 && floors.FloorConfig(mineFloor - 1).GateKingdomLevel != progress.Level)
            mineFloor = 0;
        if (request == null && mineFloor == 0)
            return false;
        gate = new LevelGate(nextLevel, progress.Exp, profile.LevelTable.ExpToNext(progress.Level), request, mineFloor);
        return true;
    }

    #endregion

    #region 땅 넓히기

    /// <summary>영토 확장 카드: 미션이 열려 있으면 그 미션(가 보기), 아니면 개간 기회 안내. 보일 게 없으면 false</summary>
    public bool TryGetTerritoryCard(out string title, out string text, out CommunityProjectDefinition mission)
    {
        var missions = OpenTerritoryMissions;
        mission = missions.Count > 0 ? missions[0] : null;
        if (mission != null)
        {
            var status = GetProjectStatus(mission);
            string step = status.Phase == ProjectPhase.Delivering ? "숲 개간을 마쳤어요. 재료를 나눠 넣으면 땅이 열려요." : "마무리하는 중이에요.";
            title = mission.Title;
            text = string.IsNullOrEmpty(mission.Line) ? step : $"{mission.Line}\n{step}";
            return true;
        }
        title = text = null;
        // 첫 개간(Lv.6)부터 보임. 일반 개간 기회는 TerritoryStartLevel(Lv.7)부터
        if (!AnyTerritoryLeft || _config.Territories.Count == 0
            || PlayerLevel < Mathf.Min(TerritoryRules.TutorialLevel, _config.TerritoryStartLevel))
            return false;
        title = "숲 개간";
        text = TerritoryChanceText();
        return true;
    }

    #endregion

    #region 지금 하는 일 · 완료한 발전

    /// <summary>건설 한 건 + 주민 작업 + 정찰 (남은 시간 · 진행)</summary>
    public void CollectKingdomWork(List<KingdomWork> result)
    {
        result.Clear();
        long now = NowTicks;
        var job = Settlement.Job;
        var building = JobRequest;
        if (job != null && building != null)
        {
            var construction = building.Construction;
            string label = string.IsNullOrEmpty(construction.ProgressLabel) ? construction.DisplayName : construction.ProgressLabel;
            result.Add(new KingdomWork(label, job.WaitingForWorker ? "건설 해달이 가는 중" : "짓는 중", construction.Icon,
                job.WaitingForWorker ? 0f : job.Progress(now), job.WaitingForWorker ? "가는 중" : FormatShort(job.Remaining(now))));
        }
        foreach (var taskJob in Settlement.TaskJobs)
        {
            var task = _config.FindTask(taskJob.TaskId);
            if (task == null)
                continue;
            var names = new List<string>();
            Sprite icon = null;
            foreach (var otterId in taskJob.OtterIds)
            {
                var otter = _config.FindOtter(otterId);
                names.Add(otter != null ? otter.DisplayName : otterId);
                if (icon == null && otter != null)
                    icon = otter.Portrait;
            }
            result.Add(new KingdomWork(task.Title, string.Join(" · ", names), icon, taskJob.Progress(now),
                FormatShort(taskJob.Remaining(now))));
        }
        if (Settlement.Scouting.Destination != null)
        {
            var destination = ScoutDestination(Settlement.Scouting.Destination);
            int count = Settlement.Scouting.Workers.Count;
            result.Add(new KingdomWork($"섬 정찰 · {destination?.DisplayName}", $"정찰대 {count}명", null, -1f, "탐험 중"));
        }
    }

    /// <summary>완료한 발전: 끝낸 부탁(건물은 건물 이름) · 공동사업 · 넓힌 영토 (아이콘과 함께)</summary>
    public void CollectCompletedDevelopments(List<(string name, Sprite icon)> result)
    {
        result.Clear();
        foreach (var request in SettlementBoardRules.SortRequests(_config))
        {
            if (!Settlement.IsCompleted(request.RequestId))
                continue;
            bool building = request.Construction != null && request.Construction.TargetType != ConstructionTarget.Clearing;
            result.Add((building ? request.Construction.DisplayName : request.Title, RequestIcon(request)));
        }
        foreach (var project in CommunityProjectRules.Sorted(_config))
        {
            if (!CommunityProjectRules.IsCompleted(project, Settlement))
                continue;
            result.Add((project.Repeatable ? $"{project.Title} ×{Settlement.ProjectCycle(project.ProjectId)}" : project.Title, project.Icon));
        }
        foreach (var territory in _config.Territories)
        {
            if (territory != null && territory.Mission != null && TerritoryRules.IsExpanded(territory, Settlement))
                result.Add((territory.Mission.Title, territory.Mission.Icon));
        }
    }

    /// <summary>메인 발전 진행 (끝낸 메인 부탁 / 전체)</summary>
    public (int done, int total) MainProgress()
    {
        int total = 0;
        int done = 0;
        foreach (var request in _config.Requests)
        {
            if (request == null || request.Category != RequestCategory.Main)
                continue;
            total++;
            if (Settlement.IsCompleted(request.RequestId))
                done++;
        }
        return (done, total);
    }

    #endregion

    #region 구역

    /// <summary>이사 지시서 한 줄: "이사 지시서 #1 · 건물 2채를 주거 지역으로 0/2" (없으면 null)</summary>
    public string RelocationSummary(out string reward)
    {
        reward = null;
        var relocation = RelocationOrder;
        if (relocation == null || !IsAdministrator)
            return null;
        int done = RelocationDoneCount();
        int moving = Settlement.RelocationMoves.Count;
        string state = moving > 0 ? $" · 이사 중 {moving}채" : "";
        reward = $"보상 골드 {RelocationRules.RewardGold(_config, relocation)} · 경험치 {_config.RelocationExp}";
        return $"이사 지시서 #{relocation.Serial} · {Districts.DisplayName(relocation.Type)}으로 {done}/{relocation.InstanceIds.Count}{state}";
    }

    /// <summary>[구역] 탭에 빨간 점: 아직 용도를 안 정한 땅이 있거나 이사 지시서가 있음</summary>
    public bool NeedsDistrictAttention => IsAdministrator && (PendingDistrictBlocks().Count > 0 || RelocationOrder != null);

    /// <summary>이 블록을 이 용도로 바꾸면 (확인 창 미리보기)</summary>
    public DistrictChangePreview PreviewDistrict(DistrictBlockDefinition block, DistrictType type) =>
        DistrictEffects.Preview(_config, Settlement, DistrictPlacements(), block, type);

    #endregion
}
