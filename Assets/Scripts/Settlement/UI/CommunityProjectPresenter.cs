using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// P3 화면 (코드로 만듦, 씬을 넘어 유지): 공동사업 카드 · 생활 의뢰 · 완료 알림.
/// - 공동사업 (일터 · 왕국 창과 같은 부품, 시안 Docs/Images/왕국화면_시안/10~11): 리본 제목 · 회차 · X /
///   주민 얼굴 + 말풍선 / 단계 표시(① 재료 모으기 → ② … 끝냄 초록 · 지금 금색) / 재료 카드(진행 막대 · [넣기 n] · 완료 · [부족 n]) /
///   지금 단계 안내 칸 · 소품 고르기 카드 / 완성하면 카드(결과 · 경험치) / 큰 버튼 하나([가능한 만큼 넣기 · 실제로 넣을 양] 또는
///   지금 단계의 [건설 시작] / [주민 배정] / [현장 보기] / [만나러 가기] / [모임 열기]) / 첫 모임 다시 보기
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
    private const string DoneColor = "#5E8C3A";
    private const string CurrentColor = "#C0702A";

    private static CommunityProjectPresenter _instance;

    /// <summary>공동사업·생활 의뢰 화면이 떠 있음 (새 해달 방문·요정 등장은 닫힌 뒤에)</summary>
    public static bool IsOpen => _instance != null && _instance.AnyOpen;

    private SettlementManager _manager;
    private float _refreshTimer;

    // 공동사업
    private GameObject _projectRoot;
    private RectTransform _projectPanel;
    private TextMeshProUGUI _projectTitle;
    private TextMeshProUGUI _projectSubtitle;
    private GameObject _speakerRow;
    private RectTransform _speakerTile;
    private TextMeshProUGUI _speakerName;
    private TextMeshProUGUI _projectLine;
    private RectTransform _stepper;
    private string _stepperKey;
    private GameObject _materialsHeader;
    private RectTransform _materialsBox;
    private GameObject _noteRoot;
    private TextMeshProUGUI _projectNote;
    private GameObject _resultRoot;
    private Image _resultIcon;
    private TextMeshProUGUI _projectResult;
    private RuntimeUICards.TagView _xpTag;
    private Button _deliverAllButton;
    private Button _actionButton;
    private RectTransform _choiceRow;
    private readonly List<ChoiceCard> _choiceCards = new List<ChoiceCard>();
    private GameObject _replayRow;
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

    // 재료 카드 한 장: 아이콘 칸 · 이름 · 보유 · 진행 막대 · 넣음/필요 · 오른쪽 [넣기 n] / 완료 / [부족 n]
    private class MaterialRow
    {
        public GameObject Root;
        public RectTransform Tile;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Owned;
        public RectTransform Bar;
        public TextMeshProUGUI Amount;
        public Button Deliver;
        public Button Short;
        public RuntimeUICards.TagView Done;
        public float Fraction;
    }

    // 소품 고르기 카드 한 장 (그림 + 이름, 누르면 고름)
    private class ChoiceCard
    {
        public GameObject Root;
        public Image Icon;
        public TextMeshProUGUI Name;
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
        _projectPanel = panel;
        var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(46, 46, 70, 52); // 위는 리본이 걸침
        panelLayout.spacing = 16f;

        // 리본 제목 · X · 회차
        _projectTitle = RuntimeUIWindow.AddRibbon(panel, string.Empty);
        RuntimeUIWindow.AddClose(panel, CloseProject);
        _projectSubtitle = RuntimeUICards.Text(panel, string.Empty, 26, muted: true, alignment: TextAlignmentOptions.Center);

        // 주민 얼굴 + 말풍선
        var speaker = RuntimeUICards.Row(panel, 16f);
        _speakerRow = speaker.gameObject;
        _speakerTile = RuntimeUICards.IconTile(speaker, null, 108f, filled: true);
        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        bubble.transform.SetParent(speaker, false);
        RuntimeUIKit.ApplySprite(bubble.GetComponent<Image>(), RuntimeUIKit.Style.Bubble, Color.white);
        bubble.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var bubbleLayout = bubble.GetComponent<VerticalLayoutGroup>();
        bubbleLayout.padding = new RectOffset(26, 26, 14, 22);
        bubbleLayout.spacing = 4f;
        bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;
        _speakerName = RuntimeUICards.Text(bubble.transform, string.Empty, 22, true, true);
        _projectLine = RuntimeUICards.Text(bubble.transform, string.Empty, 26);

        // 단계 표시 (FillStepper가 단계가 바뀔 때만 다시 만듦)
        var stepper = new GameObject("Stepper", typeof(RectTransform), typeof(LayoutElement));
        stepper.transform.SetParent(panel, false);
        stepper.GetComponent<LayoutElement>().preferredHeight = 108f;
        _stepper = (RectTransform)stepper.transform;

        // 재료
        var header = RuntimeUICards.Row(panel, 12f);
        _materialsHeader = header.gameObject;
        RuntimeUICards.Text(header, "필요한 재료", 30, true);
        RuntimeUICards.Flexible(header);
        RuntimeUICards.Text(header, NoRefundNote, 22, muted: true);
        var box = new GameObject("Materials", typeof(RectTransform), typeof(VerticalLayoutGroup));
        box.transform.SetParent(panel, false);
        var layout = box.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _materialsBox = (RectTransform)box.transform;

        // 지금 단계 안내 · 소품 고르기
        var note = RuntimeUICards.Inset(panel, 6f);
        _noteRoot = note.gameObject;
        _projectNote = RuntimeUICards.Text(note, string.Empty, 26);
        _choiceRow = RuntimeUICards.Row(panel, 16f, 250f);

        // 완성하면
        var result = RuntimeUICards.Inset(panel, 0f);
        _resultRoot = result.gameObject;
        var resultRow = RuntimeUICards.Row(result, 18f);
        _resultIcon = RuntimeUICards.Icon(resultRow, null, 84f);
        var resultText = RuntimeUICards.Column(resultRow, 4f);
        RuntimeUICards.Text(resultText, "완성하면", 26, true);
        _projectResult = RuntimeUICards.Text(resultText, string.Empty, 24);
        _xpTag = RuntimeUICards.Tag(resultRow, string.Empty, RuntimeUICards.TagKind.Gold, 22);

        // 큰 버튼 하나 (재료를 넣는 중이면 [가능한 만큼 넣기], 아니면 지금 단계의 버튼)
        _deliverAllButton = RuntimeUIKit.CreateButton(panel, "가능한 만큼 넣기", AskDeliverAll, RuntimeUIKit.ButtonKind.Primary);
        _actionButton = RuntimeUIKit.CreateButton(panel, string.Empty, () => _action?.Invoke(), RuntimeUIKit.ButtonKind.Primary);
        var replay = RuntimeUICards.Row(panel, 0f);
        _replayRow = replay.gameObject;
        RuntimeUICards.Flexible(replay);
        RuntimeUICards.Button(replay, "첫 모임 다시 보기", ReplayGathering, RuntimeUIKit.ButtonKind.Option, 320f, 68f, 26);
        RuntimeUICards.Flexible(replay);

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
        var glyphs = new StringBuilder(project.Title).Append(project.Line).Append(project.ResultPreview).Append(project.CompletionMessage)
            .Append("계획 확정하기");
        foreach (var stage in project.Stages) glyphs.Append(stage.Label).Append(stage.Hint);
        RuntimeUIKit.PrepareGlyphs(glyphs.ToString());
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
        // 납품으로 바로 끝나는 사업(영토 확장 미션)은 완료 알림에서 이미 화면을 닫음
        var project = _project;
        if (project == null)
            return;
        var status = _manager.GetProjectStatus(project);
        if ((status.Phase == ProjectPhase.Completed || status.Phase == ProjectPhase.Locked)
            && (_manager.IsTerritoryMission(project) || project.ProjectId == ProsperityResearch.PlanProjectId))
        {
            // 영토 확장 미션은 공동사업 순서와 따로: 끝났으면 닫음
            CloseProject();
            return;
        }
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

        FillHeader(project);
        FillStepper(project, status);
        FillMaterials(project, status);
        FillResult(project);
        _replayRow.SetActive(project.ProjectId != ProsperityResearch.PlanProjectId && _manager.HasHeldGathering);

        _action = null;
        _choiceRow.gameObject.SetActive(false);
        bool delivering = status.Phase == ProjectPhase.Delivering;
        string preview = delivering ? _manager.DeliverAllPreview(project) : null;
        _deliverAllButton.gameObject.SetActive(delivering);
        _deliverAllButton.interactable = preview != null;
        RuntimeUIKit.SetButtonLabel(_deliverAllButton, preview != null ? $"가능한 만큼 넣기 · {preview}" : "넣을 수 있는 재료가 없어요");
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
                _projectNote.text = string.Empty; // "돌려받을 수 없어요"는 재료 머리에
                break;

            case ProjectPhase.Stage:
                FillStage(project, status);
                break;
        }
        _noteRoot.SetActive(!string.IsNullOrEmpty(_projectNote.text));

        // 막대는 칸 폭이 정해진 뒤에 채움
        LayoutRebuilder.ForceRebuildLayoutImmediate(_projectPanel);
        foreach (var row in _materialRows)
            if (row.Root.activeSelf)
                RuntimeUICards.SetBar(row.Bar, row.Fraction);
    }

    // 리본 = 사업 이름, 아래 = 반복 사업의 이번 회차 · 주민 얼굴과 말풍선
    private void FillHeader(CommunityProjectDefinition project)
    {
        string title = !string.IsNullOrEmpty(project.Title) ? project.Title : _manager.ProjectTitle(project);
        _projectTitle.text = title;
        string subtitle = string.Empty;
        if (project.Repeatable && project.CycleTitles.Count > 0)
        {
            int cycle = _manager.Settlement.ProjectCycle(project.ProjectId);
            subtitle = $"{project.CycleTitles[cycle % project.CycleTitles.Count]} · {cycle + 1}번째";
        }
        _projectSubtitle.text = subtitle;
        _projectSubtitle.gameObject.SetActive(subtitle.Length > 0);

        var requester = project.Requester;
        bool talk = !string.IsNullOrEmpty(project.Line);
        _speakerRow.SetActive(talk);
        if (!talk)
            return;
        SetTile(_speakerTile, requester != null ? requester.Portrait : null, true);
        _speakerTile.gameObject.SetActive(requester != null && requester.Portrait != null);
        _speakerName.text = requester != null ? requester.DisplayName : string.Empty;
        _speakerName.gameObject.SetActive(requester != null);
        _projectLine.text = project.Line;
    }

    private static void SetTile(RectTransform tile, Sprite icon, bool filled)
    {
        var style = RuntimeUIKit.Style;
        RuntimeUIKit.ApplySprite(tile.GetComponent<Image>(), filled ? style.SlotFilled : style.Slot, new Color(0.97f, 0.9f, 0.82f));
        var picture = tile.Find("Icon").GetComponent<Image>();
        picture.sprite = icon;
        picture.enabled = icon != null;
    }

    // 단계 표시: ① 재료 모으기 → ② … (끝냄 초록 · 지금 금색 · 남음 옅게). 단계 · 지금 위치가 바뀔 때만 다시 만듦
    private void FillStepper(CommunityProjectDefinition project, ProjectStatus status)
    {
        var labels = new List<string> { "재료 모으기" };
        foreach (var stage in project.Stages)
            labels.Add(stage.Label);
        int current = status.Phase == ProjectPhase.Stage ? 1 + Mathf.Min(status.StageIndex, project.Stages.Count - 1) : 0;
        string key = string.Join("|", labels) + "#" + current;
        if (key == _stepperKey)
            return;
        _stepperKey = key;
        for (int i = _stepper.childCount - 1; i >= 0; i--)
            Destroy(_stepper.GetChild(i).gameObject);

        var style = RuntimeUIKit.Style;
        var done = new Color32(0x7F, 0xA8, 0x6C, 0xFF);
        var now = new Color32(0xF2, 0xC9, 0x4C, 0xFF);
        var todo = new Color32(0xF0, 0xE8, 0xDC, 0xFF);
        int n = labels.Count;
        for (int i = 0; i < n - 1; i++)
        {
            var line = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            line.SetParent(_stepper, false);
            line.anchorMin = new Vector2((i + 0.5f) / n, 1f);
            line.anchorMax = new Vector2((i + 1.5f) / n, 1f);
            line.offsetMin = new Vector2(34f, -36f);
            line.offsetMax = new Vector2(-34f, -30f);
            var image = line.GetComponent<Image>();
            image.color = i < current ? (Color)done : new Color32(0xD6, 0xC6, 0xB4, 0xFF);
            image.raycastTarget = false;
        }
        for (int i = 0; i < n; i++)
        {
            var dot = new GameObject("Step", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            dot.SetParent(_stepper, false);
            dot.anchorMin = dot.anchorMax = new Vector2((i + 0.5f) / n, 1f);
            dot.anchoredPosition = new Vector2(0f, -33f);
            dot.sizeDelta = new Vector2(58f, 58f);
            var image = dot.GetComponent<Image>();
            Color color = i < current ? (Color)done : i == current ? (Color)now : (Color)todo;
            RuntimeUIKit.ApplySprite(image, style.Knob, color);
            image.color = color; // ApplySprite는 그림이 있으면 흰색으로 둠
            image.raycastTarget = false;
            var number = RuntimeUICards.Text(dot, (i + 1).ToString(), 26, true, alignment: TextAlignmentOptions.Center);
            RuntimeUIKit.Stretch(number.rectTransform);
            number.color = i < current ? Color.white : i == current ? style.TextColor : RuntimeUICards.Muted;

            var label = RuntimeUICards.Text(_stepper, labels[i], 22, i == current, alignment: TextAlignmentOptions.Center);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2((float)i / n, 1f);
            rect.anchorMax = new Vector2((float)(i + 1) / n, 1f);
            rect.offsetMin = new Vector2(4f, -104f);
            rect.offsetMax = new Vector2(-4f, -70f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.color = i < current ? (Color)done : i == current ? style.TextColor : RuntimeUICards.Muted;
        }
    }

    // 완성하면: 결과 미리보기 · 경험치 (반복 사업은 "경험치 조금")
    private void FillResult(CommunityProjectDefinition project)
    {
        string xp = !project.Repeatable && project.XpReward > 0 ? $"경험치 +{project.XpReward:N0}" : project.Repeatable ? "경험치 조금" : string.Empty;
        bool show = !string.IsNullOrEmpty(project.ResultPreview) || xp.Length > 0;
        _resultRoot.SetActive(show);
        if (!show)
            return;
        _resultIcon.sprite = project.Icon;
        _resultIcon.enabled = project.Icon != null;
        _projectResult.text = string.IsNullOrEmpty(project.ResultPreview) ? "마을에 완료 기록이 남아요" : project.ResultPreview;
        _xpTag.Set(xp, RuntimeUICards.TagKind.Gold);
    }

    private void FillMaterials(CommunityProjectDefinition project, ProjectStatus status)
    {
        // 재료를 넣는 중 · 레벨을 기다리는 중에만 (단계로 넘어가면 단계 표시가 "재료 모으기" 완료로 보여 줌)
        bool show = status.Phase == ProjectPhase.Delivering || status.Phase == ProjectPhase.NeedsLevel;
        _materialsHeader.SetActive(show);
        _materialsBox.gameObject.SetActive(show);
        if (!show)
            return;
        _manager.CollectProjectMaterials(project, _materials);
        while (_materialRows.Count < _materials.Count)
            _materialRows.Add(CreateMaterialRow(_materialRows.Count));
        bool delivering = status.Phase == ProjectPhase.Delivering;
        var gold = _manager.Config.GoldCurrency;
        var style = RuntimeUIKit.Style;
        for (int i = 0; i < _materialRows.Count; i++)
        {
            var row = _materialRows[i];
            bool visible = i < _materials.Count;
            row.Root.SetActive(visible);
            if (!visible)
                continue;
            var material = _materials[i];
            int owned = _manager.OwnedAmount(material);
            bool full = material.Remaining <= 0;
            int deliverable = material.Deliverable(owned);
            SetTile(row.Tile, material.IsGold ? (gold != null ? gold.Icon : null) : material.Item.Icon, full);
            row.Name.text = material.IsGold ? "골드" : material.Item.DisplayName;
            row.Owned.text = $"보유 {owned:N0}";
            row.Owned.color = !full && deliverable <= 0 ? style.WarningColor : (Color)RuntimeUICards.Muted;
            row.Amount.text = $"{material.Delivered:N0} / {material.Required:N0}";
            row.Amount.color = full ? new Color32(0x5E, 0x8C, 0x3A, 0xFF) : style.TextColor;
            row.Fraction = material.Required > 0 ? (float)material.Delivered / material.Required : 1f;
            // 오른쪽: 다 넣음 → 완료, 넣을 수 있음 → [넣기 n], 가진 게 없음 → [부족 n] (잠김)
            row.Done.Set(full ? "완료" : string.Empty, RuntimeUICards.TagKind.On);
            row.Deliver.gameObject.SetActive(delivering && !full && deliverable > 0);
            RuntimeUIKit.SetButtonLabel(row.Deliver, $"넣기 {deliverable:N0}");
            row.Short.gameObject.SetActive(delivering && !full && deliverable <= 0);
            RuntimeUIKit.SetButtonLabel(row.Short, $"부족 {material.Remaining - owned:N0}");
        }
    }

    private MaterialRow CreateMaterialRow(int index)
    {
        var card = RuntimeUICards.Card(_materialsBox, false);
        var row = new MaterialRow { Root = card.gameObject };
        row.Tile = RuntimeUICards.IconTile(card, null, 96f);
        var column = RuntimeUICards.Column(card, 8f);
        var nameRow = RuntimeUICards.Row(column, 12f);
        row.Name = RuntimeUICards.Text(nameRow, string.Empty, 30, true);
        row.Owned = RuntimeUICards.Text(nameRow, string.Empty, 22, muted: true);
        RuntimeUICards.Flexible(nameRow);
        var barRow = RuntimeUICards.Row(column, 12f);
        row.Bar = RuntimeUICards.Bar(barRow, 24f);
        row.Amount = RuntimeUICards.Text(barRow, string.Empty, 24, true);
        row.Amount.textWrappingMode = TextWrappingModes.NoWrap;
        var right = RuntimeUICards.Column(card, 0f, 180f, TextAnchor.MiddleCenter);
        row.Deliver = RuntimeUICards.Button(right, "넣기", () => Deliver(index), RuntimeUIKit.ButtonKind.Primary, 172f, 80f, 30);
        row.Short = RuntimeUICards.Button(right, "부족", null, RuntimeUIKit.ButtonKind.Secondary, 172f, 80f, 28);
        row.Short.interactable = false;
        row.Done = RuntimeUICards.Tag(right, string.Empty, RuntimeUICards.TagKind.On, 26);
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
        _confirmText.text = $"{preview}\n모두 넣을까요?\n{NoRefundNote}";
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
                    SetStartAction(project, stage.Construction);
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
                    SetStartAction(project, chosen);
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
            case ProjectActionKind.ConfirmPlan:
                _projectNote.text = hint;
                SetAction("계획 확정하기", true, () => Advance(project));
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
        // 카드는 선택지 수만큼 (처음 한 번 만들고 다시 씀)
        while (_choiceCards.Count < _choices.Count)
            _choiceCards.Add(CreateChoiceCard(_choiceCards.Count));
        for (int i = 0; i < _choiceCards.Count; i++)
        {
            var card = _choiceCards[i];
            bool visible = i < _choices.Count;
            card.Root.SetActive(visible);
            if (!visible)
                continue;
            var construction = _choices[i].Construction;
            var icon = construction.Icon != null ? construction.Icon : _choices[i].Icon;
            card.Icon.sprite = icon;
            card.Icon.enabled = icon != null;
            card.Name.text = construction.DisplayName;
        }
    }

    private ChoiceCard CreateChoiceCard(int index)
    {
        var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(_choiceRow, false);
        RuntimeUIKit.ApplySprite(go.GetComponent<Image>(), RuntimeUIKit.Style.Card, Color.white);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 22, 30);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var card = new ChoiceCard { Root = go };
        card.Icon = RuntimeUICards.Icon(go.transform, null, 140f);
        card.Name = RuntimeUICards.Text(go.transform, string.Empty, 28, true, alignment: TextAlignmentOptions.Center);
        go.GetComponent<Button>().onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            Choose(index);
        });
        return card;
    }

    private void Choose(int index)
    {
        if (_project == null || index < 0 || index >= _choices.Count)
            return;
        var project = _project;
        var result = _manager.TryChooseWelcomeProp(project, _choices[index]);
        if (result == ProjectActionResult.NeedsPlace)
            ChoosePlace(project);
        else if (result == ProjectActionResult.Done)
            CloseProject();
        else
            FillProject();
    }

    private void Advance(CommunityProjectDefinition project)
    {
        var result = _manager.TryAdvanceProject(project);
        // 건설을 시작하면 건설 해달이 일하러 가는 모습이 보이게, 주민 고르기는 그 화면이 뜨게 닫음
        if (result == ProjectActionResult.NeedsPlace)
            ChoosePlace(project);
        else if (result == ProjectActionResult.Done || result == ProjectActionResult.OpenTask)
            CloseProject();
        else
            FillProject();
    }

    // 짓기 단계의 버튼: 자리를 골라 짓는 건물이면 [자리 고르기] (광장이 아니면 [광장으로]), 아니면 [건설 시작]
    private void SetStartAction(CommunityProjectDefinition project, BoardRequestDefinition request)
    {
        var plot = _manager.PlotOf(request);
        if (plot != null && !_manager.IsPlotPlaced(plot))
            SetPlazaAction(SettlementPlazaView.Active != null, "자리 고르기", () => Advance(project));
        else
            SetAction("건설 시작", true, () => Advance(project));
    }

    // 건설 모드에서 자리를 고르면 그때 공사가 시작됨 (재료는 납품으로 이미 냄)
    private void ChoosePlace(CommunityProjectDefinition project)
    {
        var request = _manager.PlaceRequestOf(project);
        CloseProject();
        if (request == null)
            return;
        if (SettlementPlazaView.Active != null)
            DecorModePresenter.RequestPlace(request);
        else
            GoToPlaza();
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
        // 영토 확장: 새 땅은 광장이 비춰 줌 (다음 사업 버튼 없음)
        if (_manager.IsTerritoryMission(project) || project.ProjectId == ProsperityResearch.PlanProjectId)
        {
            GameNotices.Post(new GameNotice(message));
            return;
        }
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
        text.Append("자리 고르기필요한 재료부족번째마을에 완료 기록이 남아요넣을 수 있는 재료가 없어요");
        var projects = new List<CommunityProjectDefinition>(config.Projects);
        // 영토 확장 미션도 같은 화면에서 열림
        foreach (var territory in config.Territories)
        {
            if (territory != null && territory.Mission != null)
                projects.Add(territory.Mission);
        }
        foreach (var project in projects)
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
