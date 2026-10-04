using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// P3 화면 (코드로 만듦, 씬을 넘어 유지): 공동사업 카드 · 생활 의뢰 · 완료 알림.
/// - 공동사업: 제목 · 주민 대사 · 단계 · 재료(필요 / 넣음 / 보유 + [넣기]) · [가능한 만큼 넣기](실제로 쓸 양을 보여 주고 확인) ·
///   지금 단계의 버튼([건설 시작] / [주민 배정] / [현장 보기] / [만나러 가기] / 소품 고르기 / [모임 열기]) · 결과 미리보기
/// - 생활 의뢰: 건네주기 [건네주기] / 주민 작업 (주민 고르기) · [다른 의뢰로] (손대기 전만)
/// - 사업·의뢰를 끝내거나 이웃이 입주하면 막지 않는 알림 띠 (GameNotices)
/// 규칙은 SettlementManager에 있고, 여기는 받은 값만 그리고 누른 것을 알린다.
/// </summary>
public class CommunityProjectPresenter : MonoBehaviour
{
    // 전역 UI(100) 위, 장소 확인 팝업(200) 아래
    private const int SortingOrder = 120;
    private const float PanelWidth = 960f;
    private const float RefreshSeconds = 0.4f;
    private const string PlazaScene = "Plaza";
    private const string NoRefundNote = "넣은 재료는 돌려받을 수 없어요.";

    private static CommunityProjectPresenter _instance;

    /// <summary>공동사업·생활 의뢰 화면이 떠 있음 (새 해달 방문·요정 등장은 닫힌 뒤에)</summary>
    public static bool IsOpen => _instance != null && _instance.AnyOpen;

    private SettlementManager _manager;
    private float _refreshTimer;

    // 공동사업
    private GameObject _projectRoot;
    private TextMeshProUGUI _projectTitle;
    private TextMeshProUGUI _projectLine;
    private TextMeshProUGUI _projectStages;
    private RectTransform _materialsBox;
    private TextMeshProUGUI _projectNote;
    private TextMeshProUGUI _projectResult;
    private Button _deliverAllButton;
    private Button _actionButton;
    private RectTransform _choiceRow;
    private Button _replayButton;
    private GameObject _confirmRoot;
    private TextMeshProUGUI _confirmText;
    private CommunityProjectDefinition _project;
    private Action _action;
    private readonly List<MaterialRow> _materialRows = new List<MaterialRow>();
    private readonly List<ProjectMaterial> _materials = new List<ProjectMaterial>();
    private readonly List<BoardRequestDefinition> _choices = new List<BoardRequestDefinition>();

    // 생활 의뢰
    private GameObject _lifeRoot;
    private TextMeshProUGUI _lifeTitle;
    private TextMeshProUGUI _lifeLine;
    private TextMeshProUGUI _lifeNote;
    private TextMeshProUGUI _lifeReward;
    private Button _lifeGiveButton;
    private Button _lifeSwapButton;
    private RectTransform _workerBox;
    private int _lifeSerial = -1;
    private readonly List<SettlementOtterDefinition> _workers = new List<SettlementOtterDefinition>();
    private int _workerSignature = -1;

    private class MaterialRow
    {
        public GameObject Root;
        public Image Icon;
        public TextMeshProUGUI Label;
        public Button Button;
    }

    private bool AnyOpen => (_projectRoot != null && _projectRoot.activeSelf) || (_lifeRoot != null && _lifeRoot.activeSelf);

    #region 만들기

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureExists();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureExists();

    // 정착 매니저(전역 UI)가 있는 장소 씬에서만
    private static void EnsureExists()
    {
        if (_instance != null || SettlementManager.Instance == null)
            return;
        var canvas = RuntimeUIKit.CreateCanvas(nameof(CommunityProjectPresenter), SortingOrder, true);
        _instance = canvas.gameObject.AddComponent<CommunityProjectPresenter>();
        _instance.Build(canvas.transform);
    }

    private void Build(Transform root)
    {
        BuildProject(root);
        BuildLife(root);
    }

    private void BuildProject(Transform root)
    {
        var dim = RuntimeUIKit.CreateDim(root, "ProjectPopup");
        _projectRoot = dim.gameObject;
        var panel = RuntimeUIKit.CreatePanel(dim, PanelWidth);

        _projectTitle = RuntimeUIKit.CreateLabel(panel, string.Empty, RuntimeUIKit.TitleFontSize, true);
        _projectLine = RuntimeUIKit.CreateLabel(panel, string.Empty);
        _projectStages = RuntimeUIKit.CreateLabel(panel, string.Empty, 34);
        _projectStages.color = RuntimeUIKit.Style.TextColor;

        var box = new GameObject("Materials", typeof(RectTransform), typeof(VerticalLayoutGroup));
        box.transform.SetParent(panel, false);
        var layout = box.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _materialsBox = (RectTransform)box.transform;

        _projectNote = RuntimeUIKit.CreateLabel(panel, string.Empty, 34);
        _projectResult = RuntimeUIKit.CreateLabel(panel, string.Empty, 32);

        _choiceRow = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        var row = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        _deliverAllButton = RuntimeUIKit.CreateButton(row, "가능한 만큼 넣기", AskDeliverAll, RuntimeUIKit.ButtonKind.Option, flexible: true);
        _actionButton = RuntimeUIKit.CreateButton(row, string.Empty, () => _action?.Invoke(), RuntimeUIKit.ButtonKind.Primary, flexible: true);
        var bottom = RuntimeUIKit.CreateRow(panel, 96f);
        _replayButton = RuntimeUIKit.CreateButton(bottom, "첫 모임 다시 보기", ReplayGathering, RuntimeUIKit.ButtonKind.Option, 380f, height: 96f);
        RuntimeUIKit.CreateButton(bottom, "닫기", CloseProject, RuntimeUIKit.ButtonKind.Secondary, 260f, height: 96f);

        // [가능한 만큼 넣기] 확인 (실제로 쓸 양 · 골드 포함 여부 · 돌려받지 않음)
        var confirmDim = RuntimeUIKit.CreateDim(dim, "Confirm");
        _confirmRoot = confirmDim.gameObject;
        var confirm = RuntimeUIKit.CreatePanel(confirmDim, 820f);
        RuntimeUIKit.CreateLabel(confirm, "재료 넣기", RuntimeUIKit.TitleFontSize, true);
        _confirmText = RuntimeUIKit.CreateLabel(confirm, string.Empty);
        var confirmRow = RuntimeUIKit.CreateRow(confirm, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(confirmRow, "넣기", ConfirmDeliverAll, RuntimeUIKit.ButtonKind.Primary, flexible: true);
        RuntimeUIKit.CreateButton(confirmRow, "취소", () => _confirmRoot.SetActive(false), RuntimeUIKit.ButtonKind.Secondary, flexible: true);
        _confirmRoot.SetActive(false);

        _projectRoot.SetActive(false);
    }

    private void BuildLife(Transform root)
    {
        var dim = RuntimeUIKit.CreateDim(root, "LifeRequestPopup");
        _lifeRoot = dim.gameObject;
        var panel = RuntimeUIKit.CreatePanel(dim, PanelWidth);
        _lifeTitle = RuntimeUIKit.CreateLabel(panel, string.Empty, RuntimeUIKit.TitleFontSize, true);
        _lifeLine = RuntimeUIKit.CreateLabel(panel, string.Empty);
        _lifeNote = RuntimeUIKit.CreateLabel(panel, string.Empty, 34);
        _lifeReward = RuntimeUIKit.CreateLabel(panel, string.Empty, 32);

        var box = new GameObject("Workers", typeof(RectTransform), typeof(VerticalLayoutGroup));
        box.transform.SetParent(panel, false);
        var layout = box.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _workerBox = (RectTransform)box.transform;

        var row = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        _lifeGiveButton = RuntimeUIKit.CreateButton(row, "건네주기", GiveLife, RuntimeUIKit.ButtonKind.Primary, flexible: true);
        _lifeSwapButton = RuntimeUIKit.CreateButton(row, "다른 의뢰로", SwapLife, RuntimeUIKit.ButtonKind.Option, flexible: true);
        RuntimeUIKit.CreateButton(row, "닫기", CloseLife, RuntimeUIKit.ButtonKind.Secondary, 220f);
        _lifeRoot.SetActive(false);
    }

    #endregion

    #region 연결

    private void OnDestroy()
    {
        Unbind();
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        // 전역 UI가 다시 만들어지면 새 매니저에 다시 연결
        if (_manager != SettlementManager.Instance)
            Bind(SettlementManager.Instance);
        if (_manager == null || !AnyOpen)
            return;
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f)
            return;
        _refreshTimer = RefreshSeconds;
        RefreshOpen();
    }

    private void Bind(SettlementManager manager)
    {
        Unbind();
        _manager = manager;
        if (_manager == null)
            return;
        _manager.OnProjectRequested += OpenProject;
        _manager.OnProjectCompleted += HandleProjectCompleted;
        _manager.OnLifeRequestRequested += OpenLife;
        _manager.OnLifeRequestCompleted += HandleLifeCompleted;
        _manager.OnResidentMovedIn += HandleMovedIn;
        PrepareGlyphs();
    }

    private void Unbind()
    {
        if (_manager == null)
            return;
        _manager.OnProjectRequested -= OpenProject;
        _manager.OnProjectCompleted -= HandleProjectCompleted;
        _manager.OnLifeRequestRequested -= OpenLife;
        _manager.OnLifeRequestCompleted -= HandleLifeCompleted;
        _manager.OnResidentMovedIn -= HandleMovedIn;
        _manager = null;
    }

    private void RefreshOpen()
    {
        if (_projectRoot.activeSelf && _project != null)
            FillProject();
        if (_lifeRoot.activeSelf)
            FillLife();
    }

    #endregion

    #region 공동사업

    private void OpenProject(CommunityProjectDefinition project)
    {
        if (project == null)
            return;
        _project = project;
        _confirmRoot.SetActive(false);
        _lifeRoot.SetActive(false);
        _projectRoot.SetActive(true);
        FillProject();
    }

    private void CloseProject()
    {
        _projectRoot.SetActive(false);
        _project = null;
    }

    private void FillProject()
    {
        var project = _project;
        var status = _manager.GetProjectStatus(project);
        if (status.Phase == ProjectPhase.Completed || status.Phase == ProjectPhase.Locked)
        {
            // 끝났으면 다음 사업으로 (반복 사업은 다음 회차)
            var next = _manager.ActiveProject;
            if (next == null || next == project && status.Phase != ProjectPhase.Stage)
            {
                CloseProject();
                return;
            }
            _project = project = next;
            status = _manager.GetProjectStatus(project);
        }

        _projectTitle.text = _manager.ProjectTitle(project);
        string speaker = project.Requester != null ? project.Requester.DisplayName + ": " : string.Empty;
        _projectLine.text = string.IsNullOrEmpty(project.Line) ? string.Empty : $"{speaker}“{project.Line}”";
        _projectStages.text = StagesText(project, status);
        FillMaterials(project, status);
        _projectResult.text = ResultText(project);
        _replayButton.gameObject.SetActive(_manager.HasHeldGathering);

        _action = null;
        _choiceRow.gameObject.SetActive(false);
        _deliverAllButton.gameObject.SetActive(status.Phase == ProjectPhase.Delivering);
        _deliverAllButton.interactable = _manager.DeliverAllPreview(project) != null;
        SetAction(null, false);

        switch (status.Phase)
        {
            case ProjectPhase.NeedsLevel:
                _projectNote.text = $"Lv.{project.RequiredLevel}부터 시작할 수 있어요. (지금 Lv.{_manager.PlayerLevel})\n생활 의뢰·성장 퀘스트·생산으로 경험치를 모아요.";
                SetAction("의뢰 보기", true, () =>
                {
                    CloseProject();
                    _manager.RequestBoard(true);
                });
                break;

            case ProjectPhase.Delivering:
                _projectNote.text = NoRefundNote;
                break;

            case ProjectPhase.Stage:
                FillStage(project, status);
                break;
        }
    }

    // "재료 ✓ → 상자 설치 → 완료" (지금 단계는 [ ])
    private string StagesText(CommunityProjectDefinition project, ProjectStatus status)
    {
        var text = new StringBuilder();
        bool delivered = status.Phase == ProjectPhase.Stage;
        text.Append(delivered ? "재료 ✓" : "[재료 모으기]");
        for (int i = 0; i < project.Stages.Count; i++)
        {
            text.Append("  →  ");
            string label = project.Stages[i].Label;
            if (delivered && i < status.StageIndex)
                text.Append(label).Append(" ✓");
            else if (delivered && i == status.StageIndex)
                text.Append('[').Append(label).Append(']');
            else
                text.Append(label);
        }
        return text.ToString();
    }

    private string ResultText(CommunityProjectDefinition project)
    {
        var text = new StringBuilder();
        if (!string.IsNullOrEmpty(project.ResultPreview))
            text.Append("완성하면: ").Append(project.ResultPreview);
        if (!project.Repeatable && project.XpReward > 0)
            text.Append(text.Length > 0 ? " · " : string.Empty).Append("경험치 +").Append(project.XpReward.ToString("N0"));
        else if (project.Repeatable)
            text.Append(text.Length > 0 ? " · " : string.Empty).Append("경험치 조금 · 완료 기록");
        return text.ToString();
    }

    private void FillMaterials(CommunityProjectDefinition project, ProjectStatus status)
    {
        _manager.CollectProjectMaterials(project, _materials);
        while (_materialRows.Count < _materials.Count)
            _materialRows.Add(CreateMaterialRow(_materialRows.Count));
        bool delivering = status.Phase == ProjectPhase.Delivering;
        var gold = _manager.Config.GoldCurrency;
        for (int i = 0; i < _materialRows.Count; i++)
        {
            var row = _materialRows[i];
            bool visible = i < _materials.Count;
            row.Root.SetActive(visible);
            if (!visible)
                continue;
            var material = _materials[i];
            int owned = _manager.OwnedAmount(material);
            string name = material.IsGold ? "골드" : material.Item.DisplayName;
            var icon = material.IsGold ? (gold != null ? gold.Icon : null) : material.Item.Icon;
            row.Icon.sprite = icon;
            row.Icon.enabled = icon != null;
            row.Label.text = material.Remaining <= 0
                ? $"{name}  {material.Required:N0} / {material.Required:N0} ✓"
                : $"{name}  넣음 {material.Delivered:N0} / {material.Required:N0} · 보유 {owned:N0}";
            row.Button.gameObject.SetActive(delivering);
            row.Button.interactable = delivering && material.Deliverable(owned) > 0;
        }
    }

    private MaterialRow CreateMaterialRow(int index)
    {
        var rowRect = RuntimeUIKit.CreateRow(_materialsBox, 88f);
        var row = new MaterialRow { Root = rowRect.gameObject };
        row.Icon = RuntimeUIKit.CreateIcon(rowRect, null, 72f);
        row.Label = RuntimeUIKit.CreateLabel(rowRect, string.Empty, 34, false, TextAlignmentOptions.Left);
        row.Label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        row.Button = RuntimeUIKit.CreateButton(rowRect, "넣기", () => Deliver(index), RuntimeUIKit.ButtonKind.Option, 170f, height: 84f);
        return row;
    }

    private void Deliver(int index)
    {
        if (_project == null)
            return;
        _manager.TryDeliver(_project, index);
        FillProject();
    }

    private void AskDeliverAll()
    {
        if (_project == null)
            return;
        string preview = _manager.DeliverAllPreview(_project);
        if (preview == null)
            return;
        _confirmText.text = $"{preview}\n을(를) 넣을까요?\n{NoRefundNote}";
        _confirmRoot.SetActive(true);
    }

    private void ConfirmDeliverAll()
    {
        _confirmRoot.SetActive(false);
        if (_project == null)
            return;
        _manager.TryDeliverAll(_project);
        FillProject();
    }

    // 지금 단계의 안내와 버튼
    private void FillStage(CommunityProjectDefinition project, ProjectStatus status)
    {
        if (status.StageIndex >= project.Stages.Count)
        {
            _projectNote.text = "마무리하는 중이에요…";
            return;
        }
        var stage = project.Stages[status.StageIndex];
        bool inPlaza = SettlementPlazaView.Active != null;
        string hint = string.IsNullOrEmpty(stage.Hint) ? string.Empty : stage.Hint + "\n";

        if (status.StageState == ProjectStageState.Busy)
        {
            var busy = _manager.JobRequest;
            var job = _manager.Settlement.Job;
            string label = busy != null && busy.Construction != null ? busy.Construction.DisplayName : "다른 공사";
            string time = job != null ? (job.WaitingForWorker ? "가는 중" : SettlementManager.FormatShort(job.Remaining(SettlementManager.NowTicks))) : string.Empty;
            _projectNote.text = $"재료 준비 완료 · 건설 대기\n지금 {label} 공사 중 ({time}). 끝나면 시작할 수 있어요.";
            return;
        }

        switch (stage.Action)
        {
            case ProjectActionKind.CompleteConstruction:
                if (status.StageState == ProjectStageState.Working)
                    FillWorking(stage.Construction, hint);
                else
                {
                    var construction = stage.Construction != null ? stage.Construction.Construction : null;
                    _projectNote.text = hint + (construction != null ? $"{FormatDuration(construction.DurationSeconds)} 걸려요. 재료는 이미 냈어요." : string.Empty);
                    SetAction("건설 시작", true, () => Advance(project));
                }
                break;

            case ProjectActionKind.PlaceWelcomeProp:
                if (status.StageState == ProjectStageState.Working)
                {
                    FillWorking(CommunityProjectRules.StageConstruction(project, stage, _manager.Settlement), hint);
                    break;
                }
                var chosen = CommunityProjectRules.StageConstruction(project, stage, _manager.Settlement);
                if (chosen != null)
                {
                    _projectNote.text = hint + $"고른 소품: {chosen.Construction.DisplayName}";
                    SetAction("건설 시작", true, () => Advance(project));
                    break;
                }
                _projectNote.text = hint + "하나를 골라 주세요. 비용·시간은 같아요.";
                ShowChoices(project, stage);
                break;

            case ProjectActionKind.ClearObstacles:
            {
                int done = CommunityProjectRules.CountObstaclesCleared(stage, _manager.Settlement);
                _projectNote.text = hint + $"치운 것 {done} / {stage.ObstacleIds.Count}";
                SetPlazaAction(inPlaza, "현장 보기", () => Focus(stage.StageId));
                break;
            }

            case ProjectActionKind.CompleteRegionTask:
                if (status.StageState == ProjectStageState.Working)
                {
                    var job = _manager.GetTaskJob(stage.Task);
                    _projectNote.text = hint + $"{stage.Task.Title} 중 · {(job != null ? SettlementManager.FormatShort(job.Remaining(SettlementManager.NowTicks)) : string.Empty)}";
                    SetPlazaAction(inPlaza, "현장 보기", () => Focus(stage.StageId));
                }
                else
                {
                    var labor = _manager.GetLaborSummary();
                    _projectNote.text = hint + $"주민 {stage.Task.RequiredWorkers}명 · {FormatDuration(stage.Task.DurationSeconds)}"
                        + (labor.Available < stage.Task.RequiredWorkers ? $"\n지금 보낼 수 있는 주민이 없어요. (작업 중 {labor.Working}명) 작업이 끝나면 보낼 수 있어요." : string.Empty);
                    SetPlazaAction(inPlaza, "주민 배정", () => Advance(project));
                }
                break;

            case ProjectActionKind.SettleResident:
            {
                var otter = stage.Resident;
                string name = otter != null ? otter.DisplayName : "새 이웃";
                if (status.StageState == ProjectStageState.Ready)
                {
                    _projectNote.text = hint + $"{name}에게 말을 걸어 입주를 도와주세요.";
                    SetPlazaAction(inPlaza, "만나러 가기", () =>
                    {
                        CloseProject();
                        if (otter != null && SettlementPlazaView.Active != null)
                            SettlementPlazaView.Active.FocusOtter(otter.OtterId);
                    });
                }
                else
                    _projectNote.text = hint + $"{name}{KoreanParticle.SubjectParticle(name)} 광장으로 찾아오고 있어요.";
                break;
            }

            case ProjectActionKind.HoldGathering:
                _projectNote.text = hint + "모두 모였어요! 준비가 되면 모임을 열어요.";
                SetPlazaAction(inPlaza, "모임 열기", () =>
                {
                    CloseProject();
                    _manager.TryHoldGathering(project);
                });
                break;
        }
    }

    private void FillWorking(BoardRequestDefinition request, string hint)
    {
        var job = _manager.Settlement.Job;
        string label = request != null && request.Construction != null ? request.Construction.ProgressLabel : "공사";
        string time = job != null ? (job.WaitingForWorker ? "가는 중" : SettlementManager.FormatShort(job.Remaining(SettlementManager.NowTicks))) : string.Empty;
        _projectNote.text = hint + $"{label} · {time}";
        bool inPlaza = SettlementPlazaView.Active != null;
        SetPlazaAction(inPlaza, "현장 보기", () =>
        {
            CloseProject();
            if (request != null && request.Construction != null && SettlementPlazaView.Active != null)
                SettlementPlazaView.Active.FocusSite(request.Construction.ConstructionId);
        });
    }

    private void ShowChoices(CommunityProjectDefinition project, ProjectStageDefinition stage)
    {
        _choiceRow.gameObject.SetActive(true);
        _choices.Clear();
        foreach (var option in stage.Options)
        {
            if (option != null && option.Construction != null)
                _choices.Add(option);
        }
        // 버튼은 선택지 수만큼 (처음 한 번 만들고 다시 씀)
        while (_choiceRow.childCount < _choices.Count)
        {
            int index = _choiceRow.childCount;
            RuntimeUIKit.CreateButton(_choiceRow, string.Empty, () => Choose(index), RuntimeUIKit.ButtonKind.Option, flexible: true);
        }
        for (int i = 0; i < _choiceRow.childCount; i++)
        {
            var child = _choiceRow.GetChild(i).gameObject;
            child.SetActive(i < _choices.Count);
            if (i < _choices.Count)
                RuntimeUIKit.SetButtonLabel(child.GetComponent<Button>(), _choices[i].Construction.DisplayName);
        }
    }

    private void Choose(int index)
    {
        if (_project == null || index < 0 || index >= _choices.Count)
            return;
        var result = _manager.TryChooseWelcomeProp(_project, _choices[index]);
        if (result == ProjectActionResult.Done)
            CloseProject();
        else
            FillProject();
    }

    private void Advance(CommunityProjectDefinition project)
    {
        var result = _manager.TryAdvanceProject(project);
        // 건설을 시작하면 건설 해달이 일하러 가는 모습이 보이게, 주민 고르기는 그 화면이 뜨게 닫음
        if (result == ProjectActionResult.Done || result == ProjectActionResult.OpenTask)
            CloseProject();
        else
            FillProject();
    }

    // 광장에서 할 일: 광장이면 그 버튼, 아니면 [광장으로]
    private void SetPlazaAction(bool inPlaza, string label, Action action)
    {
        if (inPlaza)
            SetAction(label, true, action);
        else
            SetAction("광장으로", PlazaZone != null, () =>
            {
                CloseProject();
                GoToPlaza();
            });
    }

    private void SetAction(string label, bool enabled, Action action = null)
    {
        _action = action;
        bool visible = !string.IsNullOrEmpty(label);
        _actionButton.gameObject.SetActive(visible);
        if (!visible)
            return;
        RuntimeUIKit.SetButtonLabel(_actionButton, label);
        _actionButton.interactable = enabled;
    }

    private void Focus(string key)
    {
        CloseProject();
        if (SettlementPlazaView.Active != null)
            SettlementPlazaView.Active.FocusPoint(key);
    }

    private void ReplayGathering()
    {
        CloseProject();
        if (SettlementPlazaView.Active == null)
        {
            GameNotices.Post(new GameNotice("첫 모임은 광장에서 다시 볼 수 있어요.", "광장으로", GoToPlaza));
            return;
        }
        _manager.RequestGatheringReplay();
    }

    private void HandleProjectCompleted(CommunityProjectDefinition project, int xp)
    {
        if (_projectRoot.activeSelf && _project == project && !project.Repeatable)
            CloseProject();
        string message = string.IsNullOrEmpty(project.CompletionMessage) ? $"{project.Title} 완료!" : project.CompletionMessage;
        if (xp > 0)
            message += $"\n경험치 +{xp:N0}";
        var next = _manager.ActiveProject;
        GameNotices.Post(next != null && next != project
            ? new GameNotice(message, "다음 사업", () => _manager.RequestProject(_manager.ActiveProject ?? project))
            : new GameNotice(message));
    }

    private void HandleMovedIn(SettlementOtterDefinition otter)
    {
        string name = otter.DisplayName;
        GameNotices.Post(new GameNotice($"{name}{KoreanParticle.SubjectParticle(name)} 새 집에 입주했어요!\n함께 일할 주민이 한 명 늘었어요."));
    }

    #endregion

    #region 생활 의뢰

    private void OpenLife(int serial)
    {
        _lifeSerial = serial;
        _workerSignature = -1;
        _projectRoot.SetActive(false);
        _lifeRoot.SetActive(true);
        FillLife();
    }

    private void CloseLife()
    {
        _lifeRoot.SetActive(false);
        _lifeSerial = -1;
    }

    private void FillLife()
    {
        if (!_manager.Settlement.TryGetLifeRequest(_lifeSerial, out var record))
        {
            CloseLife();
            return;
        }
        var template = _manager.FindLifeTemplate(record);
        if (template == null)
        {
            CloseLife();
            return;
        }
        var item = _manager.LifeRequestItem(record);
        _lifeTitle.text = template.Title;
        _lifeLine.text = $"“{LifeRequestRules.Line(template, item, record.Amount)}”";
        var profile = ProfileManager.Instance;
        int xp = profile != null ? LifeRequestRules.XpFor(template, profile.Level, profile.LevelTable) : 0;
        _lifeReward.text = $"보상: 골드 {template.Gold:N0} · 경험치 +{xp:N0}";

        var job = _manager.LifeWorkJob(record);
        bool deliver = template.Kind == LifeRequestKind.Deliver;
        _lifeGiveButton.gameObject.SetActive(deliver);
        _lifeSwapButton.interactable = job == null;
        if (deliver)
        {
            int owned = item != null ? _manager.ItemCount(item) : 0;
            _lifeNote.text = item != null ? $"{item.DisplayName} {record.Amount}개 필요 · 보유 {owned}" : string.Empty;
            _lifeGiveButton.interactable = item != null && owned >= record.Amount;
            SetWorkers(null);
            return;
        }

        if (job != null)
        {
            _lifeNote.text = $"{template.Task.Title} 중 · {SettlementManager.FormatShort(job.Remaining(SettlementManager.NowTicks))}";
            SetWorkers(null);
            return;
        }
        _workers.Clear();
        _manager.CollectResidents(_workers);
        _workers.RemoveAll(w => !_manager.CanAssign(w));
        _lifeNote.text = _workers.Count > 0
            ? $"보낼 주민을 골라요 · {FormatDuration(template.Task.DurationSeconds)}"
            : "지금 보낼 수 있는 주민이 없어요. 작업이 끝나면 보낼 수 있어요.";
        SetWorkers(_workers);
    }

    // 주민 버튼 (같은 목록이면 다시 만들지 않음)
    private void SetWorkers(List<SettlementOtterDefinition> workers)
    {
        int signature = 17;
        if (workers != null)
        {
            foreach (var w in workers)
                signature = signature * 31 + w.GetInstanceID();
        }
        if (signature == _workerSignature)
            return;
        _workerSignature = signature;
        for (int i = _workerBox.childCount - 1; i >= 0; i--)
            Destroy(_workerBox.GetChild(i).gameObject);
        _workerBox.gameObject.SetActive(workers != null && workers.Count > 0);
        if (workers == null)
            return;
        foreach (var worker in workers)
        {
            var otter = worker;
            RuntimeUIKit.CreateButton(_workerBox, $"{otter.DisplayName} 보내기", () => SendWorker(otter), RuntimeUIKit.ButtonKind.Option, height: 92f);
        }
    }

    private void SendWorker(SettlementOtterDefinition otter)
    {
        if (_manager.TryStartLifeWork(_lifeSerial, otter) == TaskStartResult.Started)
            CloseLife();
        else
            FillLife();
    }

    private void GiveLife()
    {
        if (_manager.TryCompleteLifeDelivery(_lifeSerial))
            CloseLife();
        else
            FillLife();
    }

    private void SwapLife()
    {
        if (_manager.TrySwapLifeRequest(_lifeSerial))
        {
            CloseLife();
            _manager.RequestBoard(true);
        }
    }

    private void HandleLifeCompleted(LifeRequestTemplate template, int gold, int xp)
    {
        GameNotices.Post(new GameNotice($"생활 의뢰 '{template.Title}'을(를) 마쳤어요!\n골드 +{gold:N0} · 경험치 +{xp:N0}"));
    }

    #endregion

    #region 이동 · 글자

    private static ZoneDefinition PlazaZone =>
        GlobalUIRoot.Instance != null ? ZoneLookup.FindByScene(GlobalUIRoot.Instance.Zones, PlazaScene) : null;

    private static void GoToPlaza()
    {
        var navigator = FindAnyObjectByType<SceneNavigator>();
        var zone = PlazaZone;
        if (navigator != null && zone != null)
            navigator.TryGo(zone);
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        if (total < 60)
            return $"{total}초";
        return total % 60 == 0 ? $"{total / 60}분" : $"{total / 60}분 {total % 60}초";
    }

    // 처음 보는 한글을 미리 폰트에 넣음 (데이터의 제목·대사·안내 전부)
    private void PrepareGlyphs()
    {
        var config = _manager.Config;
        var text = new StringBuilder("0123456789:/ ,.!?%·→✓[]()“”…+가능한만큼넣기닫기재료모으기넣음보유필요완성하면경험치조금완료기록건설시작현장보기주민배정만나러가기모임열기광장으로의뢰보기다음사업첫다시돌려받을수없어요넣을까요을를취소고른소품하나골라주세요비용시간은같아요치운것중대기지금공사가는끝나면있어요준비걸려요이미냈어요보낼명작업에게말걸어입주도와찾아오고있모두모였어마무리하는건네주기다른로보상골드개새집했함께일할늘었생활마쳤부터시작할Lv성장퀘스트생산으로모아요");
        text.Append(NoRefundNote);
        foreach (var project in config.Projects)
        {
            if (project == null)
                continue;
            text.Append(project.Title).Append(project.Line).Append(project.ResultPreview).Append(project.CompletionMessage);
            if (project.Requester != null)
                text.Append(project.Requester.DisplayName);
            foreach (var title in project.CycleTitles)
                text.Append(title);
            foreach (var stage in project.Stages)
            {
                text.Append(stage.Label).Append(stage.Hint);
                if (stage.Task != null)
                    text.Append(stage.Task.Title);
                if (stage.Resident != null)
                    text.Append(stage.Resident.DisplayName);
                foreach (var option in stage.Options)
                {
                    if (option != null && option.Construction != null)
                        text.Append(option.Construction.DisplayName).Append(option.Construction.ProgressLabel);
                }
                if (stage.Construction != null && stage.Construction.Construction != null)
                    text.Append(stage.Construction.Construction.DisplayName).Append(stage.Construction.Construction.ProgressLabel);
            }
            foreach (var item in project.Items)
            {
                if (item != null && item.Item != null)
                    text.Append(item.Item.DisplayName);
            }
            foreach (var cost in project.CycleCosts)
            {
                if (cost == null)
                    continue;
                foreach (var item in cost.Items)
                {
                    if (item != null && item.Item != null)
                        text.Append(item.Item.DisplayName);
                }
            }
        }
        foreach (var template in config.LifeRequests)
        {
            if (template == null)
                continue;
            text.Append(template.Title).Append(template.Line);
            if (template.Task != null)
                text.Append(template.Task.Title);
            foreach (var item in template.Items)
            {
                if (item != null)
                    text.Append(item.DisplayName);
            }
        }
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                text.Append(otter.DisplayName);
        }
        RuntimeUIKit.PrepareGlyphs(text.ToString());
    }

    #endregion
}
