using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 큰 부탁 화면 · 마을회관 발전 현황 화면을 SettlementManager와 잇는다 (전역 UI 루트에 붙음).
/// 두 화면 모두 기존 기록(부탁·작업·발전·주민)에서 계산해 보여 줄 뿐, 결제·건설·보상을 따로 하지 않는다.
/// [가 보기]는 그 부탁을 하러 가는 기존 화면을 연다 (SettlementManager.RequestOpen → 게시판 쪽이 건설·대화·작업 화면을 엶).
/// </summary>
public class TownHallPresenter : MonoBehaviour
{
    private const float RefreshSeconds = 0.5f;
    private const string AllDoneMessage = "현재 준비된 마을 발전을 모두 마쳤어요!\n밭·광산에서 생산하고, 광장을 꾸미고, 도감을 채워 봐요.";

    [Header("화면")]
    [SerializeField] private MilestonePopupView _milestone;
    [SerializeField] private TownHallPopupView _hall;
    [Tooltip("모두 마쳤을 때 생산(이동)·꾸미기·도감을 여는 전역 UI")]
    [SerializeField] private GlobalUIPresenter _globalUI;

    private SettlementManager _manager;
    private readonly List<(BoardRequestDefinition request, MilestoneStepState state)> _steps = new List<(BoardRequestDefinition, MilestoneStepState)>();
    private readonly List<(string title, MilestoneStepState state)> _stepRows = new List<(string, MilestoneStepState)>();
    private BoardRequestDefinition _milestoneTarget;
    private BoardRequestDefinition _hallTarget;
    // P3: 회관의 다음 목표가 공동사업일 때 (메인 부탁을 다 끝낸 뒤)
    private CommunityProjectDefinition _hallProject;
    // 영토 확장 미션 (숲 개간을 다 끝낸 단계가 있을 때)
    private CommunityProjectDefinition _territoryMission;
    private float _refreshTimer;
    private bool _glyphsReady;

    private void OnEnable()
    {
        _milestone.OnGoClicked += HandleMilestoneGo;
        _hall.OnGoClicked += HandleHallGo;
        _hall.OnTravelClicked += HandleTravel;
        _hall.OnDecorClicked += HandleDecor;
        _hall.OnCodexClicked += HandleCodex;
        _hall.OnTerritoryClicked += HandleTerritory;
    }

    // SettlementManager가 같은 오브젝트에 붙어 있어 깨어나는 순서가 같을 수 있으므로 Start에서 연결
    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null)
            return;
        _manager.OnMilestoneRequested += OpenMilestone;
        _manager.OnTownHallRequested += OpenHall;
        _manager.OnChanged += RefreshOpen;
    }

    private void OnDisable()
    {
        _milestone.OnGoClicked -= HandleMilestoneGo;
        _hall.OnGoClicked -= HandleHallGo;
        _hall.OnTravelClicked -= HandleTravel;
        _hall.OnDecorClicked -= HandleDecor;
        _hall.OnCodexClicked -= HandleCodex;
        _hall.OnTerritoryClicked -= HandleTerritory;
        if (_manager != null)
        {
            _manager.OnMilestoneRequested -= OpenMilestone;
            _manager.OnTownHallRequested -= OpenHall;
            _manager.OnChanged -= RefreshOpen;
        }
    }

    // 남은 시간이 매초 바뀌므로 열린 화면을 짧은 간격으로 다시 그림
    private void Update()
    {
        if (_manager == null || (!_milestone.IsOpen && !_hall.IsOpen))
            return;
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f)
            return;
        _refreshTimer = RefreshSeconds;
        RefreshOpen();
    }

    private void RefreshOpen()
    {
        if (_manager == null)
            return;
        if (_milestone.IsOpen && _milestone.Group != null)
            OpenMilestone(_milestone.Group);
        if (_hall.IsOpen)
            OpenHall();
    }

    #region 큰 부탁

    private void OpenMilestone(MilestoneGroupDefinition group)
    {
        if (!_glyphsReady)
            PrepareGlyphs();

        var settlement = _manager.Settlement;
        SettlementBoardRules.CollectSteps(group, settlement, _steps);
        _stepRows.Clear();
        foreach (var (request, state) in _steps)
            _stepRows.Add((StepTitle(request), state));

        int done = SettlementBoardRules.CountDone(group, settlement);
        _milestoneTarget = SettlementBoardRules.CurrentStep(group, settlement);
        string note;
        if (_milestoneTarget == null)
            note = done >= _steps.Count ? "모든 단계를 마쳤어요! 마을회관을 눌러 발전 현황을 볼 수 있어요." : "다음 단계가 열리기를 기다리고 있어요.";
        else if (_manager.GetStatus(_milestoneTarget) == RequestStatus.Building)
            note = $"지금 \"{_milestoneTarget.Title}\"을(를) 하고 있어요.";
        else
            note = $"다음: \"{_milestoneTarget.Title}\"";
        _milestone.Show(group, $"{done} / {_steps.Count} 단계", _stepRows, note, "가 보기", _milestoneTarget != null);
    }

    private void HandleMilestoneGo()
    {
        var target = _milestoneTarget;
        if (target == null)
            return;
        _milestone.Hide();
        if (_hall.IsOpen)
            _hall.Hide();
        _manager.RequestOpen(target);
    }

    #endregion

    #region 마을회관

    private void OpenHall()
    {
        if (!_glyphsReady)
            PrepareGlyphs();

        var config = _manager.Config;
        var settlement = _manager.Settlement;
        int mainTotal = 0;
        int mainDone = 0;
        foreach (var request in config.Requests)
        {
            if (request == null || request.Category != RequestCategory.Main)
                continue;
            mainTotal++;
            if (settlement.IsCompleted(request.RequestId))
                mainDone++;
        }

        _hall.Show($"{settlement.Stage + 1:00} · {_manager.StageName}", $"마을 발전 {mainDone} / {mainTotal}",
            _manager.GetLaborSummary(), WorkText(), CompletedText());

        _hallTarget = _manager.CurrentRequest;
        _hallProject = _hallTarget == null ? _manager.ActiveProject : null;
        if (_hallProject != null)
            _hall.ShowNext(_manager.ProjectTitle(_hallProject), ProjectNextText(_hallProject), true);
        else if (_manager.AreAllMainDone)
            _hall.ShowAllDone(AllDoneMessage);
        else if (_hallTarget != null)
            _hall.ShowNext(_hallTarget.Title, _hallTarget.Description, true);
        else if (_manager.FindLevelLockedRequest(out int lockedLevel) is BoardRequestDefinition locked)
            _hall.ShowNext(locked.Title, $"왕국 Lv.{lockedLevel}에 열려요. 퀘스트로 레벨을 올려요.", false);
        else
            _hall.ShowNext("다음 발전을 준비하고 있어요", "진행 중인 일이 끝나면 새 부탁이 열려요.", false);
        ShowTerritory();
    }

    // 영토 확장 카드: 미션이 열려 있으면 [가 보기], 아니면 개간 기회 안내 (개간 레벨 전·다 넓혔으면 숨김)
    private void ShowTerritory()
    {
        var missions = _manager.OpenTerritoryMissions;
        _territoryMission = missions.Count > 0 ? missions[0] : null;
        if (_territoryMission != null)
        {
            var status = _manager.GetProjectStatus(_territoryMission);
            string step = status.Phase == ProjectPhase.Delivering ? "숲 개간을 마쳤어요. 재료를 나눠 넣으면 땅이 열려요." : "마무리하는 중이에요.";
            _hall.ShowTerritory(_territoryMission.Title, string.IsNullOrEmpty(_territoryMission.Line) ? step : $"{_territoryMission.Line}\n{step}", true);
            return;
        }
        bool anyLeft = _manager.CurrentTerritory(TerritoryDirection.West) != null || _manager.CurrentTerritory(TerritoryDirection.North) != null;
        if (!anyLeft || _manager.Config.Territories.Count == 0 || _manager.PlayerLevel < _manager.Config.TerritoryStartLevel)
        {
            _hall.HideTerritory();
            return;
        }
        _hall.ShowTerritory("숲 개간", _manager.TerritoryChanceText(), false);
    }

    private void HandleTerritory()
    {
        var mission = _territoryMission;
        if (mission == null)
            return;
        _hall.Hide();
        _manager.RequestProject(mission);
    }

    // 지금 하는 일: 건설 한 건 + 주민 작업과 남은 시간
    private string WorkText()
    {
        var settlement = _manager.Settlement;
        long now = SettlementManager.NowTicks;
        var text = new StringBuilder();
        var job = settlement.Job;
        var building = _manager.JobRequest;
        if (job != null && building != null)
        {
            string label = string.IsNullOrEmpty(building.Construction.ProgressLabel) ? building.Construction.DisplayName : building.Construction.ProgressLabel;
            text.Append("· ").Append(label).Append("  ")
                .Append(job.WaitingForWorker ? "가는 중" : SettlementManager.FormatShort(job.Remaining(now)));
        }
        foreach (var taskJob in settlement.TaskJobs)
        {
            var task = _manager.Config.FindTask(taskJob.TaskId);
            if (task == null)
                continue;
            if (text.Length > 0)
                text.Append('\n');
            text.Append("· ").Append(task.Title).Append(" (").Append(WorkerNames(taskJob)).Append(")  ")
                .Append(SettlementManager.FormatShort(taskJob.Remaining(now)));
        }
        return text.Length > 0 ? text.ToString() : "지금 진행 중인 일이 없어요.";
    }

    private string WorkerNames(SettlementTaskJob job)
    {
        var names = new List<string>();
        foreach (var otterId in job.OtterIds)
        {
            var otter = _manager.Config.FindOtter(otterId);
            names.Add(otter != null ? otter.DisplayName : otterId);
        }
        return string.Join(", ", names);
    }

    // 완료한 발전: 끝낸 부탁을 순서대로 (건물은 건물 이름)
    private string CompletedText()
    {
        var names = new List<string>();
        foreach (var request in SettlementBoardRules.SortRequests(_manager.Config))
        {
            if (_manager.Settlement.IsCompleted(request.RequestId))
                names.Add(StepTitle(request));
        }
        // P3: 끝낸 공동사업 (반복 사업은 끝낸 횟수)
        foreach (var project in CommunityProjectRules.Sorted(_manager.Config))
        {
            if (!CommunityProjectRules.IsCompleted(project, _manager.Settlement))
                continue;
            names.Add(project.Repeatable ? $"{project.Title} ×{_manager.Settlement.ProjectCycle(project.ProjectId)}" : project.Title);
        }
        // 넓힌 영토
        foreach (var territory in _manager.Config.Territories)
        {
            if (territory != null && territory.Mission != null && TerritoryRules.IsExpanded(territory, _manager.Settlement))
                names.Add(territory.Mission.Title);
        }
        return names.Count > 0 ? string.Join(" · ", names) : "아직 완료한 발전이 없어요.";
    }

    // 공동사업 한 줄: 주민 대사 + 지금 할 일 (레벨이 모자라면 필요한 레벨)
    private string ProjectNextText(CommunityProjectDefinition project)
    {
        var status = _manager.GetProjectStatus(project);
        string step;
        if (status.Phase == ProjectPhase.NeedsLevel)
            step = $"Lv.{project.RequiredLevel}부터 시작할 수 있어요 (지금 Lv.{_manager.PlayerLevel}).";
        else if (status.Phase == ProjectPhase.Delivering)
            step = "재료를 조금씩 나눠 넣을 수 있어요.";
        else
        {
            var stage = _manager.CurrentStage(project);
            step = stage != null ? $"지금 할 일: {stage.Label}" : "마무리하는 중이에요.";
        }
        return string.IsNullOrEmpty(project.Line) ? step : $"{project.Line}\n{step}";
    }

    private void HandleHallGo()
    {
        if (_hallProject != null)
        {
            var project = _hallProject;
            _hall.Hide();
            _manager.RequestProject(project);
            return;
        }
        var target = _hallTarget;
        if (target == null)
            return;
        _hall.Hide();
        _manager.RequestOpen(target);
    }

    private void HandleTravel()
    {
        _hall.Hide();
        if (_globalUI != null)
            _globalUI.OpenTravel();
    }

    private void HandleDecor()
    {
        _hall.Hide();
        if (_globalUI != null)
            _globalUI.OpenDecor();
    }

    private void HandleCodex()
    {
        _hall.Hide();
        if (_globalUI != null)
            _globalUI.OpenCodex();
    }

    #endregion

    // 건물을 짓는 부탁은 건물 이름, 아니면 부탁 제목
    private static string StepTitle(BoardRequestDefinition request) =>
        request.Construction != null && request.Construction.TargetType != ConstructionTarget.Clearing
            ? request.Construction.DisplayName
            : request.Title;

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (스크롤 목록 갱신 중 글자가 추가되면 D3D12 에디터가 멈춤)
    private void PrepareGlyphs()
    {
        var config = _manager.Config;
        var text = new StringBuilder("0123456789:/ ,.!?()·\"명단계완료진행중다음아직가보기지금하고있어요모든마쳤어요마을회관을눌러발전현황볼수열리기를기다리고")
            .Append(AllDoneMessage).Append("다음 발전을 준비하고 있어요진행 중인 일이 끝나면 새 부탁이 열려요.지금 진행 중인 일이 없어요.아직 완료한 발전이 없어요.가는 중");
        foreach (var request in config.Requests)
        {
            if (request == null)
                continue;
            text.Append(request.Title).Append(request.Description);
            if (request.Construction != null)
                text.Append(request.Construction.DisplayName).Append(request.Construction.ProgressLabel);
        }
        foreach (var task in config.Tasks)
        {
            if (task != null)
                text.Append(task.Title);
        }
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                text.Append(otter.DisplayName);
        }
        foreach (var group in config.MilestoneGroups)
        {
            if (group != null)
                text.Append(group.Title).Append(group.Description);
        }
        text.Append("부터 시작할 수 있어요지금 Lv재료를 조금씩 나눠 넣을 수 있어요할 일마무리하는 중이에요×");
        foreach (var project in config.Projects)
        {
            if (project == null)
                continue;
            text.Append(project.Title).Append(project.Line);
            foreach (var title in project.CycleTitles)
                text.Append(title);
            foreach (var stage in project.Stages)
                text.Append(stage.Label);
        }
        for (int i = 0; i < config.StageCount; i++)
            text.Append(config.StageName(i));
        text.Append("영토 확장숲 개간을 마쳤어요. 재료를 나눠 넣으면 땅이 열려요.부터 광장 가장자리의 숲을 개간할 수 있어요.")
            .Append("개간 기회번 · 광장 가장자리의 점선 원을 눌러 해달을 보내요.레벨이 오르면 개간 기회가 생겨요.중이에요.");
        foreach (var territory in config.Territories)
        {
            if (territory == null)
                continue;
            text.Append(territory.DisplayName);
            if (territory.Mission != null)
                text.Append(territory.Mission.Title).Append(territory.Mission.Line);
            foreach (var step in territory.Clearings)
            {
                if (step != null && step.Task != null)
                    text.Append(step.Task.Title);
            }
        }

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var root in new Component[] { _milestone, _hall })
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                fonts.Add(label.font);
        }
        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }
        _glyphsReady = true;
    }
}
