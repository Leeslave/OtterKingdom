using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 큰 부탁 화면을 SettlementManager와 잇고, 마을회관 요청(OnTownHallRequested)은 왕국 화면(KingdomSheet)으로 넘긴다 (전역 UI 루트에 붙음).
/// 예전 발전 현황 화면(TownHallPopupView)은 씬에 남아 있지만 열지 않는다 (왕국 화면이 대신함).
/// 두 화면 모두 기존 기록(부탁·작업·발전·주민)에서 계산해 보여 줄 뿐, 결제·건설·보상을 따로 하지 않는다.
/// [가 보기]는 그 부탁을 하러 가는 기존 화면을 연다 (SettlementManager.RequestOpen → 게시판 쪽이 건설·대화·작업 화면을 엶).
/// </summary>
public class TownHallPresenter : MonoBehaviour
{
    private const float RefreshSeconds = 0.5f;

    [Header("화면")]
    [SerializeField] private MilestonePopupView _milestone;
    [Tooltip("예전 발전 현황 화면 (열지 않음. 열려 있으면 닫기만)")]
    [SerializeField] private TownHallPopupView _hall;

    private SettlementManager _manager;
    private readonly List<(BoardRequestDefinition request, MilestoneStepState state)> _steps = new List<(BoardRequestDefinition, MilestoneStepState)>();
    private readonly List<(string title, MilestoneStepState state)> _stepRows = new List<(string, MilestoneStepState)>();
    private BoardRequestDefinition _milestoneTarget;
    private float _refreshTimer;
    private bool _glyphsReady;

    private void OnEnable()
    {
        _milestone.OnGoClicked += HandleMilestoneGo;
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
        if (_manager == null || !_milestone.IsOpen)
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
            note = done >= _steps.Count ? "모든 단계를 마쳤어요! 게시판에서 발전 현황을 볼 수 있어요." : "다음 단계가 열리기를 기다리고 있어요.";
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

    #region 마을회관 → 왕국 화면

    // 예전 발전 현황 화면 대신 왕국 화면(탭 4개)을 엶. 다음 목표 · 영토 · 지금 하는 일 등은 SettlementManager.Kingdom이 계산
    private void OpenHall()
    {
        if (_hall.IsOpen)
            _hall.Hide();
        _manager.OpenKingdom();
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
        var text = new StringBuilder("0123456789:/ ,.!?()·\"명단계완료진행중다음아직가보기지금하고있어요모든마쳤어요마을회관길드게시판에서눌러발전현황볼수열리기를기다리고")
            .Append(SettlementManager.AllDoneMessage).Append("다음 발전을 준비하고 있어요진행 중인 일이 끝나면 새 부탁이 열려요.지금 진행 중인 일이 없어요.아직 완료한 발전이 없어요.가는 중")
            .Append("끝내면 다음 레벨(Lv.)로 올라가요.먼저 숲을 개간해 땅을 넓혀요. (아래 영토 확장)")
            .Append("주민 현황정착 주민보낼 수 있음작업 중왕국 질서도이사 지시서이사하기구역 정하기영토 확장숲 개간완료한 발전개");
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
        // 숲 개간 줄 (SettlementManager.TerritoryChanceText): 땅 이름 · 진행 · 기회
        text.Append("이 땅을 다 마치면 다른 방향도 개간할 수 있어요개간 기회번공터 가장자리의 점선 원을 눌러 해달을 보내요레벨이 오르면 생겨요");
        foreach (var territory in config.Territories)
        {
            if (territory != null)
                text.Append(territory.DisplayName);
        }
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
