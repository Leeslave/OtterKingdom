using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 모델(QuestLog)과 퀘스트 화면을 연결한다. 위쪽은 왕국 레벨과 경험치.
/// 목록은 지금 열린 퀘스트만: 보상 받을 수 있는 것 → 메인(지금 할 일) → 일일 → 도전 → 완료(이번 레벨에 받은 메인 → 오늘 받은 일일 → 이번 레벨에 받은 도전).
/// 완료는 남은 퀘스트 아래에 [완료] + 체크 + 흐리게 표시된다 (QuestRowView). 레벨이 오르면 받은 성장 퀘스트는 목록에서 빠진다.
/// </summary>
public class QuestPresenter : MonoBehaviour
{
    [Header("목록")]
    [SerializeField] private QuestRowView _rowPrefab;
    [SerializeField] private Transform _rowParent;
    [Tooltip("열 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;

    [Header("왕국 레벨")]
    [Tooltip("\"Lv.3\"")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [Tooltip("\"120 / 300\" (최고 레벨이면 \"MAX\")")]
    [SerializeField] private TextMeshProUGUI _expText;
    [SerializeField] private ProgressBarView _expBar;
    [Tooltip("다음 메인 퀘스트 안내")]
    [SerializeField] private TextMeshProUGUI _subtitleText;

    [Header("버튼")]
    [SerializeField] private Button _claimAllButton;
    [SerializeField] private Image _claimAllImage;
    [SerializeField] private TextMeshProUGUI _claimAllLabel;
    [SerializeField] private Button _closeButton;

    [Header("모두 받기 모양")]
    [SerializeField] private Sprite _claimAllActiveSprite;
    [SerializeField] private Sprite _claimAllIdleSprite;
    [SerializeField] private Color _claimAllActiveTextColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _claimAllIdleTextColor = new Color32(0x9C, 0x7C, 0x66, 0xFF);

    [Header("화면")]
    [Tooltip("퀘스트 화면 루트 (배경 딤 + 열기/닫기 연출)")]
    [SerializeField] private UIPopupAnimator _screen;

    private readonly List<QuestRowView> _rows = new List<QuestRowView>();
    private readonly List<QuestDefinition> _ordered = new List<QuestDefinition>();

    private QuestManager _manager;
    private ProfileManager _profile;
    private SettlementManager _settlement;
    private RectTransform _levelGateCard;
    private TextMeshProUGUI _levelGateHeading;
    private TextMeshProUGUI _levelGateAction;
    private TextMeshProUGUI _levelGateDetail;
    private Button _levelGateButton;
    private Action _goToLevelGate;
    private Vector2 _listOffsetMax;
    private bool _glyphsReady;

    private const float LevelGateHeight = 174f;
    private const float LevelGateGap = 14f;
    private RectTransform ListRect => (RectTransform)_scrollRect.transform;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Close);
        _claimAllButton.onClick.AddListener(() => _manager.ClaimAll());
    }

    private void OnEnable()
    {
        if (_levelGateCard == null)
            BuildLevelGateCard();

        _manager = QuestManager.Instance;
        _manager.OnChanged += Refresh;
        _profile = ProfileManager.Instance;
        if (_profile != null)
            _profile.OnExpChanged += Refresh;
        _settlement = SettlementManager.Instance;
        if (_settlement != null)
            _settlement.OnChanged += RefreshLevelGate;

        if (!_glyphsReady)
            PrepareGlyphs();

        Refresh();
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnChanged -= Refresh;
        if (_profile != null)
            _profile.OnExpChanged -= Refresh;
        if (_settlement != null)
            _settlement.OnChanged -= RefreshLevelGate;
    }

    public void Open()
    {
        _screen.Show();
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close() => _screen.Hide();

    private void Refresh()
    {
        var log = _manager.Log;
        var quests = _manager.Database.Quests;

        // Quests는 이미 정렬 순서이므로 묶음별로 나눠 담기만 하면 안정 정렬이 된다
        _ordered.Clear();
        AddGroup(quests, q => log.GetStatus(q) == QuestStatus.Claimable);
        AddGroup(quests, q => q.Kind == QuestKind.Main && log.GetStatus(q) == QuestStatus.InProgress);
        AddGroup(quests, q => q.Kind == QuestKind.Daily && log.GetStatus(q) == QuestStatus.InProgress);
        AddGroup(quests, q => q.Kind == QuestKind.Challenge && log.GetStatus(q) == QuestStatus.InProgress);
        // 완료는 남은 퀘스트 아래로 (이번 레벨에 받은 메인·도전, 오늘 받은 일일만)
        AddGroup(quests, q => q.Kind == QuestKind.Main && _manager.ShowsAsCompleted(q));
        AddGroup(quests, q => q.Kind == QuestKind.Daily && _manager.ShowsAsCompleted(q));
        AddGroup(quests, q => q.Kind == QuestKind.Challenge && _manager.ShowsAsCompleted(q));

        while (_rows.Count < _ordered.Count)
        {
            var row = Instantiate(_rowPrefab, _rowParent);
            row.OnClaimClicked += HandleClaimClicked;
            _rows.Add(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool used = i < _ordered.Count;
            _rows[i].gameObject.SetActive(used);
            if (used)
                _rows[i].Bind(_ordered[i], log.GetProgress(_ordered[i]), log.GetStatus(_ordered[i]), _manager.ExpFor(_ordered[i]));
        }

        RefreshLevel();

        bool canClaim = _manager.ClaimableCount > 0;
        _claimAllButton.interactable = canClaim;
        _claimAllImage.sprite = canClaim ? _claimAllActiveSprite : _claimAllIdleSprite;
        _claimAllLabel.color = canClaim ? _claimAllActiveTextColor : _claimAllIdleTextColor;
    }

    private void AddGroup(IReadOnlyList<QuestDefinition> quests, System.Predicate<QuestDefinition> match)
    {
        foreach (var quest in quests)
        {
            if (_manager.IsAvailable(quest) && match(quest))
                _ordered.Add(quest);
        }
    }

    private void RefreshLevel()
    {
        if (_profile == null)
            return;

        var progress = _profile.Progress;
        int need = _profile.LevelTable.ExpToNext(progress.Level);
        _levelText.text = $"Lv.{progress.Level}";
        _expText.text = need > 0 ? $"{progress.Exp:N0} / {need:N0}" : "MAX";
        _expBar.SetRatio(progress.Ratio(_profile.LevelTable));
        RefreshSubtitle();
        RefreshLevelGate();
    }

    private string _defaultSubtitle;

    private void RefreshSubtitle()
    {
        if (_subtitleText == null)
            return;
        _defaultSubtitle ??= _subtitleText.text;

        var main = _manager.CurrentMainQuest;
        _subtitleText.enableAutoSizing = true;
        _subtitleText.fontSizeMin = 18f;
        _subtitleText.text = main != null ? $"다음 메인: {main.Title}\n{main.Description}" : _defaultSubtitle;
    }

    // 기존 퀘스트 화면과 에디터에서 다시 생성한 화면 양쪽에서 목록 바로 위에 배치한다.
    private void BuildLevelGateCard()
    {
        _listOffsetMax = ListRect.offsetMax;
        var card = new GameObject("LevelGate", typeof(RectTransform), typeof(Image));
        card.layer = ListRect.gameObject.layer;
        _levelGateCard = (RectTransform)card.transform;
        _levelGateCard.SetParent(ListRect.parent, false);
        _levelGateCard.anchorMin = new Vector2(0f, 1f);
        _levelGateCard.anchorMax = Vector2.one;
        _levelGateCard.offsetMin = new Vector2(ListRect.offsetMin.x, _listOffsetMax.y - LevelGateHeight);
        _levelGateCard.offsetMax = _listOffsetMax;

        var background = card.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(background, RuntimeUIKit.Style.Card, Color.white);
        background.color = new Color32(0xFF, 0xDF, 0xA0, 0xFF);
        background.raycastTarget = false;

        _levelGateHeading = CreateLevelGateLabel(30, true, 12f, 48f);
        _levelGateHeading.color = new Color32(0x8F, 0x3A, 0x22, 0xFF);
        _levelGateAction = CreateLevelGateLabel(40, true, 48f, 98f);
        _levelGateDetail = CreateLevelGateLabel(26, false, 94f, 156f);
        _levelGateDetail.textWrappingMode = TextWrappingModes.Normal;
        _levelGateButton = RuntimeUIKit.CreateButton(card.transform, "", () => _screen.Hide(_goToLevelGate),
            RuntimeUIKit.ButtonKind.Primary);
        var buttonRect = (RectTransform)_levelGateButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-26f, -12f);
        buttonRect.sizeDelta = new Vector2(224f, 86f);
        card.SetActive(false);
    }

    private TextMeshProUGUI CreateLevelGateLabel(int size, bool title, float top, float bottom)
    {
        var label = RuntimeUIKit.CreateLabel(_levelGateCard, string.Empty, size, title, TextAlignmentOptions.MidlineLeft);
        label.gameObject.layer = _levelGateCard.gameObject.layer;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(32f, -bottom);
        rect.offsetMax = new Vector2(-32f, -top);
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private void RefreshLevelGate()
    {
        LevelGate gate = default;
        bool show = _settlement != null && _settlement.TryGetLevelGate(out gate);
        if (_levelGateCard.gameObject.activeSelf != show)
        {
            _levelGateCard.gameObject.SetActive(show);
            ListRect.offsetMax = show ? _listOffsetMax - new Vector2(0f, LevelGateHeight + LevelGateGap) : _listOffsetMax;
        }
        if (!show)
            return;

        var request = gate.Request;
        _levelGateHeading.text = gate.Blocked
            ? $"Lv.{gate.NextLevel} 레벨업 조건  ·  경험치 충족"
            : $"Lv.{gate.NextLevel} 레벨업 조건";
        _levelGateAction.text = gate.MineFloor != 0 ? gate.MineObjective : LevelGateAction(request);
        var mine = gate.MineFloor != 0 ? MineLevelGateGoal(gate.MineFloor) : default;
        var village = request != null ? LevelGateDetail(request) : default;
        var next = village.action != null ? village : mine;
        _levelGateDetail.text = gate.MineFloor == 0 ? village.detail
            : request != null ? $"마을 조건 · {LevelGateAction(request)}\n{next.detail ?? village.detail}" : mine.detail;
        _goToLevelGate = next.action;
        _levelGateButton.gameObject.SetActive(_goToLevelGate != null);
        if (_goToLevelGate != null)
            RuntimeUIKit.SetButtonLabel(_levelGateButton, next.label);
        var detailRect = _levelGateDetail.rectTransform;
        var actionRect = _levelGateAction.rectTransform;
        float right = _goToLevelGate != null ? -268f : -32f;
        detailRect.offsetMax = new Vector2(right, detailRect.offsetMax.y);
        actionRect.offsetMax = new Vector2(right, actionRect.offsetMax.y);
    }

    private (string detail, string label, Action action) MineLevelGateGoal(int floor)
    {
        var link = MineMazeLinkConfig.Instance;
        if (!_settlement.HasDevelopment(MineMazeRules.FoundDevelopment))
        {
            if (!_settlement.Settlement.IsCompleted(MineMazeRules.BoardOtterRequest)
                && _settlement.Config.FindRequest(MineMazeRules.BoardOtterRequest) is BoardRequestDefinition board)
            {
                var objective = LevelGateDetail(board);
                return ($"먼저 할 일 · {LevelGateAction(board)}", objective.label, objective.action);
            }
            var miner = _settlement.Config.FindSpecialist(_settlement.Config.FindRegionByZoneId(GameManager.MineZoneId));
            if (miner != null && _settlement.CanAssignSpecialist(miner))
                return ("먼저 광부를 배치해요", "광부 만나기",
                    () => GoToPlaza(() => SettlementPlazaView.Active?.FocusOtter(miner.OtterId)));
            if (!ZoneAccess.IsOpen(link.MineZone))
            {
                var goal = _settlement.NextGoal();
                return goal.CanGo ? ($"먼저 할 일 · {goal.Title}", "가 보기", () => GoToPlaza(() => _settlement.GoTo(goal)))
                    : ("먼저 광산으로 가는 길을 열어요", "부탁 보기", () => GoToPlaza(() => _settlement.RequestBoard(true)));
            }
            if (GameManager.Instance?.HasMineMazePickaxe == false)
                return ("먼저 광산 곡괭이를 강화해요", "광산 가기", () => GoToZone(link.MineZone,
                    () => GameManager.Instance?.OpenPickaxeUpgrade()));
            return ("광부를 배치하고 광산에서 채굴을 시작해요", "광산 가기", () => GoToZone(link.MineZone));
        }
        if (floor >= 3 && !_settlement.HasDevelopment(SettlementQuestGate.FishingDevelopment))
            return ("먼저 선착장을 수리하고 낚시터를 열어요", "선착장 준비", () => GoToPlaza(() =>
            {
                var dock = _settlement.Config.FindRequest("req_fishing_dock");
                if (dock != null && _settlement.GetStatus(dock) == RequestStatus.Available)
                    _settlement.RequestOpen(dock);
                else if (_settlement.HasDevelopment(SettlementQuestGate.FarmDevelopment))
                    _settlement.OpenSouthernMap();
                else
                    _settlement.OpenDistrictExpansionMap(DistrictType.Agricultural);
            }));
        var config = link.Floors.FloorConfig(floor - 1);
        if (config.GasSource && _settlement.HasMineCommunityStory && !_settlement.MineCommunity.treated)
            return ("먼저 게시판에서 임시 진료를 시작해요", "부탁 보기", () => GoToPlaza(_settlement.OpenMineCommunity));

        int target = floor;
        for (int i = 1; i < floor; i++)
        {
            if (_settlement.HasDevelopment(MineMazeRules.FloorClearedDevelopment(i)))
                continue;
            target = i;
            break;
        }
        var targetConfig = link.Floors.FloorConfig(target - 1);
        var pickaxe = targetConfig.Rework.Pickaxes[targetConfig.GatePickaxeTier];
        string detail = target < floor ? $"먼저 갱도 지하 {target}층을 돌파해요"
            : (InventoryManager.Instance?.MineMazeState?.pickaxeTier ?? 0) < targetConfig.GatePickaxeTier
                ? $"{pickaxe.DisplayName} 필요"
                : config.GasSource ? "갱도에서 가스 발생원을 제거해요" : "계단을 막은 큰 돌을 부숴요";
        return (detail, "갱도 가기", () => GoToZone(link.MazeZone,
            () => FindAnyObjectByType<MineMazeGame>()?.ShowLevelGoal(target)));
    }

    private static string LevelGateAction(BoardRequestDefinition request)
    {
        if (request.ClearZone != null)
            return $"{request.ClearZone.DisplayName} 장애물 치우기";
        if (request.Building != null)
            return request.BuildingCount > 1
                ? $"{request.Building.DisplayName} {request.BuildingCount}개 짓기"
                : $"{request.Building.DisplayName} 짓기";
        if (request.Construction != null)
            return $"{request.Construction.DisplayName} 만들기";
        if (request.Action == RequestAction.SettleOtters)
            return $"{request.Title} · 해달 입주시키기";
        return $"‘{request.Title}’ 완료하기";
    }

    private (string detail, string label, Action action) LevelGateDetail(BoardRequestDefinition request)
    {
        var status = _settlement.GetStatus(request);
        if (status == RequestStatus.Locked)
            return LevelGatePrerequisite(request);
        if (IsRequestInProgress(request))
            return ($"진행 중  ·  {request.Title}", null, null);
        if (request.ClearZone != null && ZoneAccess.IsOpen(request.ClearZone)
            && ZoneAccess.IsUncleared(request.ClearZone))
            return ($"{request.ClearZone.DisplayName}에서 길을 열어요", "가 보기", () => GoToZone(request.ClearZone));
        if (request.Building != null && _settlement.CheckBuilding(request.Building) == BuildingBlock.None
            && _settlement.HasDistrictFor(_settlement.RequiredDistrictFor(request.Building)))
            return ($"꾸미기 모드 → 건물  ·  {request.Title}", "자리 고르기",
                () => GoToPlaza(() => DecorModePresenter.RequestBuild(request.Building)));
        return ($"해달 게시판  ·  {request.Title}", "부탁 보기", () => GoToPlaza(() => _settlement.RequestOpen(request)));
    }

    private bool IsRequestInProgress(BoardRequestDefinition request) =>
        _settlement.GetStatus(request) == RequestStatus.Building || _settlement.ActiveBuildingOf(request.Building) != null;

    private (string detail, string label, Action action) LevelGatePrerequisite(BoardRequestDefinition request)
    {
        // 레벨 조건까지 이어지는 앞선 부탁과 일터의 첫 생산을 확인한다.
        while (!string.IsNullOrEmpty(request.RequiredDevelopment) && !_settlement.HasDevelopment(request.RequiredDevelopment))
        {
            BoardRequestDefinition previous = null;
            foreach (var candidate in _settlement.Config.Requests)
            {
                if (candidate == null || candidate.Order >= request.Order)
                    continue;
                var building = candidate.Building;
                if (building != null && ProductionRules.FirstProducedDevelopment(building.BuildingId) == request.RequiredDevelopment)
                {
                    if (_settlement.Settlement.CountBuilt(building.BuildingId) > 0)
                        return ($"먼저 할 일  ·  {building.DisplayName}에서 첫 생산하기", "일터 보기",
                            () => _settlement.OpenKingdom(KingdomSheet.Tab.Work));
                    previous = candidate;
                    break;
                }
                if (SettlementRules.CompletionDevelopment(candidate) == request.RequiredDevelopment)
                {
                    previous = candidate;
                    break;
                }
            }
            if (previous == null)
                break;
            if (_settlement.GetStatus(previous) != RequestStatus.Locked)
            {
                if (IsRequestInProgress(previous))
                    return ($"먼저 완료할 일  ·  {previous.Title} (진행 중)", null, null);
                return ($"먼저 할 일  ·  {LevelGateAction(previous)}", "부탁 보기",
                    () => GoToPlaza(() => _settlement.RequestOpen(previous)));
            }
            request = previous;
        }

        var goal = _settlement.NextGoal();
        if (goal.CanGo)
            return ($"먼저 할 일  ·  {goal.Title}", "가 보기", () => GoToPlaza(() => _settlement.GoTo(goal)));
        var otter = _settlement.PendingIntroOtter;
        return otter != null
            ? ($"먼저 할 일  ·  광장에서 {otter.DisplayName}에게 말 걸기", "해달 만나기",
                () => GoToPlaza(() => SettlementPlazaView.Active?.FocusOtter(otter.OtterId)))
            : ("게시판에서 앞선 부탁을 완료해요", "게시판 열기", () => GoToPlaza(() => _settlement.RequestBoard(true)));
    }

    private void GoToPlaza(Action onArrived)
    {
        var root = GlobalUIRoot.Instance;
        if (root != null)
            GoToZone(ZoneLookup.FindByScene(root.Zones, ZoneTutorials.Plaza), onArrived);
    }

    private void GoToZone(ZoneDefinition zone, Action onArrived = null)
    {
        if (zone == null)
            return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == zone.SceneName)
            onArrived?.Invoke();
        else
            FindAnyObjectByType<SceneNavigator>()?.TryGo(zone, onArrived);
    }

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (동적 폰트).
    // 스크롤 목록이 LateUpdate에서 레이아웃을 강제로 갱신하는 도중에 새 글자가 한꺼번에 추가되면
    // D3D12 에디터에서 GPU가 멈춰 크래시가 났다 (Unity 6000.3.8, 퀘스트 화면을 열 때 재현).
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789/ ,.():·→‘’다음 해금보상 받기진행 중완료일일도전메인경험치LvMAX")
            .Append("레벨업 조건 충족먼저 할 일게시판에서 앞선 부탁을 완료해요꾸미기 모드 건물해달 짓기 만들기 장애물 치우기 개 첫 생산하기 광장에서 에게 말 걸기 입주시키기")
            .Append("마을 조건갱도 지하 층 돌파하기광산에서 오래된 갱도를 발견해요선착장을 수리하고 낚시터를 열어요계단을 막은 큰 돌을 부숴요가스 발생원을 제거해요")
            .Append("먼저 광산 곡괭이를 강화해요 광부를 배치하고 채굴을 시작해요 임시 진료를 시작해요 자리 고르기 부탁 보기 선착장 준비 갱도 가기 광산 가기 일터 보기 해달 만나기 게시판 열기 필요 광부 만나기 광산으로 가는 길을 열어요");
        foreach (var quest in _manager.Database.Quests)
            text.Append(quest.Title).Append(quest.Description);
        if (_settlement != null)
        {
            foreach (var request in _settlement.Config.Requests)
            {
                if (request != null)
                    text.Append(request.Title).Append(request.Building?.DisplayName).Append(request.Construction?.DisplayName)
                        .Append(request.ClearZone?.DisplayName);
            }
            foreach (var project in _settlement.Config.Projects)
            {
                if (project != null)
                    text.Append(project.Title);
            }
            foreach (var otter in _settlement.Config.Otters)
            {
                if (otter != null)
                    text.Append(otter.DisplayName);
            }
        }

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var label in _rowPrefab.GetComponentsInChildren<TMP_Text>(true))
            fonts.Add(label.font);
        fonts.Add(_levelGateHeading.font);
        fonts.Add(_levelGateDetail.font);
        var floors = MineMazeLinkConfig.Instance?.Floors;
        if (floors != null)
            foreach (var pickaxe in floors.Rework.Pickaxes)
                text.Append(pickaxe.DisplayName);

        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }

        _glyphsReady = true;
    }

    private void HandleClaimClicked(QuestRowView row)
    {
        _manager.TryClaim(row.Quest);
    }
}
