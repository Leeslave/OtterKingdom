using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장을 정착 진행에 맞춘다 (광장 씬에 하나).
/// - 발전에 따라 집·벤치·잡목 등을 켜고 끔 (DevelopmentGate) → 걷기 영역 다시 계산
/// - 정착 해달(첫 해달, 방문 해달, 정착 후보, 배치를 기다리는 전문 해달, 관리 해달)을 광장에 내보냄 (재접속해도 한 마리씩).
///   · 이미 만난 해달은 씬을 열거나 상태가 바뀌면 바로 복원 (팝업이 떠 있어도 사라져 있지 않게)
///   · 아직 만나지 않은 해달은 방문 대기 줄에 넣고, 화면 전환·완료 팝업·레벨업·돌아옴 보상 창·튜토리얼·확인 대화가
///     모두 닫힌 뒤 광장 가장자리에서 걸어 들어옴 = 이때가 "처음 만난" 순간 (SettlementManager.Meet)
///   · 대기 줄은 세이브(주민 목록 + 만남 기록)에서 매번 다시 만들므로, 씬을 떠나거나 꺼도 다음에 한 번만 다시 찾아옴
///   · 아무도 만난 적 없는 새 게임의 첫 해달·옛 세이브는 모인 곳에서 바로 시작 (첫 화면)
/// - 배치한 전문 해달은 길 끝으로 걸어 나가 광장에서 빠짐 (그 뒤로는 일하는 곳에만 있음)
/// - 관리 역할을 맡은 해달은 근무 자리(게시판 옆)로 걸어가 그 자리에 있음. 길을 못 찾으면 잠시 뒤 그 자리로 옮김
/// - 광장 주민 작업(공동 공간 정비 등)에 보낸 해달은 광장의 그 현장에서 일함 (다른 장소 작업은 길 끝으로 나감)
/// - 건설 해달을 내보내고, 건설 중이면 현장으로 보내 일하게 함 (건설 해달이 필요 없는 첫 집은 부탁한 해달이 직접)
/// - 정착 해달에 말풍선(탭하면 한마디, 정착 후보의 "!")을 붙임
/// - 요정(P3): 농부 파견으로 방문이 예약되면, 새 해달과 같은 대기 규칙으로 안전한 때(파견 대화·완료 팝업·화면 전환이 끝난 뒤) 나타남
///   (요정 NPC의 DevelopmentGate가 도착 발전으로 켜지고 카메라가 비춤). 다른 장소에 있으면 다음 광장 방문의 안전한 때
/// 기존 광장 코드(PlazaController 배회, A*, 깊이 정렬)는 그대로 쓰고 그 위에서 켜고 끄기만 한다.
/// </summary>
// GameManager.Start(0)가 세이브를 불러온 뒤, PlazaController.Start(0) 다음에 광장을 맞춤
[DefaultExecutionOrder(50)]
public class SettlementPlazaView : MonoBehaviour, IDecorBlocker
{
    // 완성 순간 카메라가 현장으로 미끄러져 가는 시간 (별빛은 그 뒤에)
    private const float CameraArriveSeconds = 0.6f;
    // [만나러 가기]로 카메라를 보낸 해달이 서 있는 시간 (초)
    private const float FocusHoldSeconds = 8f;
    // 주민 작업에 보낸 해달이 광장을 떠나기 시작한 뒤 길을 못 찾아도 이만큼 지나면 내보냄
    private const float LeaveLimitSeconds = 12f;
    // 관리 해달이 근무 자리로 가는 길을 못 찾아도 이만큼 지나면 그 자리로 옮김
    private const float StationLimitSeconds = 12f;

    public static SettlementPlazaView Active { get; private set; }

    [Header("광장")]
    [SerializeField] private PlazaWalkableArea _walkableArea;
    [Tooltip("새 집·공사 현장을 비출 카메라")]
    [SerializeField] private PlazaCameraController _camera;
    [SerializeField] private PlazaSettings _plazaSettings;
    [Tooltip("내보낸 해달을 넣을 곳")]
    [SerializeField] private Transform _otterRoot;
    [Tooltip("처음 들어올 때 해달들이 모여 있을 곳 (카메라 첫 화면)")]
    [SerializeField] private Transform _gatherPoint;
    [Tooltip("모이는 곳 둘레로 꾸미기 물건·건물을 놓지 못하게 비워 둘 크기 (월드 단위). 0이면 비우지 않음")]
    [SerializeField] private Vector2 _gatherClearSize = new Vector2(3.5f, 2.5f);
    [Tooltip("새로 찾아온 해달이 나타나는 곳 (광장 가장자리 길)")]
    [SerializeField] private Transform _arrivalPoint;

    [Header("발전")]
    [Tooltip("이 아래의 DevelopmentGate를 모두 다룸 (꺼져 있어도 찾음)")]
    [SerializeField] private List<Transform> _gateRoots = new List<Transform>();

    [Header("건설")]
    [SerializeField] private List<ConstructionSiteView> _sites = new List<ConstructionSiteView>();
    [Tooltip("건설 해달 (오기 전에는 꺼져 있음)")]
    [SerializeField] private BuilderOtterController _builder;

    [Header("광장 주민 작업 · 관리 해달 (P2)")]
    [Tooltip("광장에서 하는 주민 작업 현장 (공동 공간 정비, 게시판 주변 정리)")]
    [SerializeField] private List<PlazaTaskSiteView> _taskSites = new List<PlazaTaskSiteView>();
    [Tooltip("관리 해달의 근무 자리 (게시판 옆)")]
    [SerializeField] private List<ManagementStationView> _stations = new List<ManagementStationView>();

    [Header("영토 확장")]
    [Tooltip("숲 개간 자리 (방향마다 하나). 개간하러 보낸 해달은 그 자리에서 숲을 향해 일함")]
    [SerializeField] private List<TerritorySiteView> _territorySites = new List<TerritorySiteView>();

    [Header("공동사업 (P3)")]
    [Tooltip("공동사업 화면의 [현장 보기]가 비출 곳 (이름 = 사업 단계 ID, 예: clear_brush)")]
    [SerializeField] private List<Transform> _focusPoints = new List<Transform>();

    [Header("해달 말풍선")]
    [SerializeField] private Sprite _speechBubble;
    [SerializeField] private Sprite _alertBubble;
    [SerializeField] private Sprite _hammerBubble;
    [SerializeField] private TMP_FontAsset _speechFont;

    private readonly List<DevelopmentGate> _gates = new List<DevelopmentGate>();
    private readonly List<TerritoryAnchor> _anchors = new List<TerritoryAnchor>();
    private readonly Dictionary<string, OtterWanderAgent> _spawned = new Dictionary<string, OtterWanderAgent>();
    private readonly Dictionary<string, SettlementOtterView> _views = new Dictionary<string, SettlementOtterView>();
    private readonly List<string> _leftForWork = new List<string>();
    private readonly Dictionary<string, float> _leavingSince = new Dictionary<string, float>();
    // 근무 자리로 가기 시작한 시각 (길을 못 찾아도 이만큼 지나면 옮김)
    private readonly Dictionary<string, float> _stationSince = new Dictionary<string, float>();
    // 아직 만나지 않아 안전한 때를 기다리는 해달 (중복 없음)
    private readonly List<string> _visitQueue = new List<string>();
    private readonly List<string> _presenting = new List<string>();
    private const float VisitorIntroductionSeconds = 15f;
    private float _nextVisitorTime;
    private SettlementManager _manager;
    private SceneNavigator _navigator;
    private bool _builderInitialized;
    // 막 이사를 시작한 건물의 옛 자리 (건설 해달이 들러 건물을 이고 감). 씬을 다시 열면 없음 → 새 자리에서 바로 일함
    private readonly Dictionary<int, RectInt> _carryFrom = new Dictionary<int, RectInt>();
    private readonly List<int> _staleCarry = new List<int>();

    /// <summary>방문 대기 줄 (확인용)</summary>
    public IReadOnlyList<string> VisitQueue => _visitQueue;

    // 마지막으로 새 해달을 비춰 소개한 때 (그동안은 다른 안내를 띄우지 않게)
    private float _introducedAt = float.NegativeInfinity;

    /// <summary>새 해달을 막 소개하는 중 (카메라가 비추는 동안 밭 안내가 겹쳐 뜨지 않게. 줄에서 기다리는 해달은 안내가 끝난 뒤에 옴)</summary>
    public bool IsIntroducingVisitor => _presenting.Count > 0 || Time.unscaledTime < _introducedAt + FocusHoldSeconds;

    private void Awake()
    {
        Active = this;
        foreach (var root in _gateRoots)
        {
            if (root != null)
            {
                _gates.AddRange(root.GetComponentsInChildren<DevelopmentGate>(true));
                _anchors.AddRange(root.GetComponentsInChildren<TerritoryAnchor>(true));
            }
        }
        _builder.gameObject.SetActive(false);
    }

    private void Start()
    {
        _manager = SettlementManager.Instance;
        _navigator = FindAnyObjectByType<SceneNavigator>();
        if (_manager == null)
        {
            // 정착 매니저가 없는 테스트: 다 보이는 광장
            ApplyGates(_ => true, false);
            _walkableArea.Rebuild();
            return;
        }

        ApplyAll(true);
        _manager.OnLoaded += HandleLoaded;
        _manager.OnChanged += HandleChanged;
        _manager.OnDevelopmentUnlocked += HandleDevelopmentUnlocked;
        _manager.OnConstructionStarted += HandleConstructionStarted;
        _manager.OnRequestCompleted += HandleRequestCompleted;
        _manager.OnRoleAssigned += HandleRoleAssigned;
        _manager.OnRelocationStarted += HandleRelocationStarted;
        _manager.OnRelocationChanged += HandleRelocationChanged;
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnLevelUp += HandleLevelUp;
    }

    // 공사 과정 (집이 올라오고, 치울 것이 하나씩 사라짐)
    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded)
            return;
        if (_visitQueue.Count > 0 && Time.unscaledTime >= _nextVisitorTime && IsSafeToPresent())
            PresentVisitors();
        // 요정은 새 해달이 다 들어온 다음 차례 (둘이 한꺼번에 화면을 차지하지 않게)
        else if (_visitQueue.Count == 0 && Time.unscaledTime >= _nextVisitorTime && _manager.IsFairyComing && IsSafeToPresent())
            _manager.TryMarkFairyArrived();
        RemoveOttersAtWork();
        KeepManagersAtStation();
        var job = _manager.Settlement.Job;
        var site = job != null ? FindSite(job.ConstructionId) : null;
        if (site == null)
        {
            // 현장이 광장에 없는 공사(낚시터 선착장): 일꾼이 걸어갈 곳이 없으니 기다리지 않고 바로 시작
            if (job != null && job.WaitingForWorker)
                _manager.BeginJobWork();
            return;
        }
        // 일할 해달이 현장에 도착하면 그때부터 공사 시간이 흐름
        if (job.WaitingForWorker && IsWorkerAtSite())
            _manager.BeginJobWork();
        site.SetProgress(job.Progress(SettlementManager.NowTicks), true);
    }

    private bool IsWorkerAtSite()
    {
        var request = _manager.JobRequest;
        if (request == null || request.Construction.NeedsBuilder)
            return _builderInitialized && _builder.IsWorking;
        return request.Requester != null && _spawned.TryGetValue(request.Requester.OtterId, out var agent)
            && agent != null && agent.IsOnTask;
    }

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;
        if (_manager != null)
        {
            _manager.OnLoaded -= HandleLoaded;
            _manager.OnChanged -= HandleChanged;
            _manager.OnDevelopmentUnlocked -= HandleDevelopmentUnlocked;
            _manager.OnConstructionStarted -= HandleConstructionStarted;
            _manager.OnRequestCompleted -= HandleRequestCompleted;
            _manager.OnRoleAssigned -= HandleRoleAssigned;
            _manager.OnRelocationStarted -= HandleRelocationStarted;
            _manager.OnRelocationChanged -= HandleRelocationChanged;
        }
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    /// <summary>진행 말풍선을 띄울 곳 (이 광장에 그 현장이 없으면 false)</summary>
    public bool TryGetSiteAnchor(string constructionId, out Vector3 world)
    {
        var site = FindSite(constructionId);
        world = site != null ? site.BubbleAnchor : default;
        return site != null;
    }

    #region 적용

    private void HandleLoaded() => ApplyAll(true);

    // 왕국 레벨이 오르면 레벨로 열린 부탁의 해달이 찾아올 수 있음
    private void HandleLevelUp(int level)
    {
        if (_manager != null && _manager.IsLoaded)
            SyncOtters(false);
    }

    /// <summary>지금 광장에 나와도 되는 해달인지 (또박이가 오기 전에는 처음 네 해달만, EarlyOtterRules)</summary>
    private bool CanAppearYet(SettlementOtterDefinition otter) =>
        EarlyOtterRules.CanAppear(otter.OtterId, _manager.Settlement);

    // 광산 배치로 함께 오는 물감이 · 뚝딱이와 생산 필수 해달은 일반 방문객 소개 대기 없이 바로 광장에 있다.
    private static bool IsImmediateWorker(SettlementOtterDefinition otter) =>
        EarlyOtterRules.IsImmediate(otter.OtterId);

    private void HandleDevelopmentUnlocked(string developmentId)
    {
        // 새로 생긴 집은 통통 튀어나오고, 길이 바뀌었으니 걷기 영역을 다시 계산
        // (튀는 동안은 크기가 0에서 시작해 발자국·걷기 다각형이 작게 잡히므로 다 튄 뒤 한 번 더)
        ApplyGates(_manager.HasDevelopment, true);
        _walkableArea.Rebuild();
        StartCoroutine(RebuildWalkableAfterPop());

        foreach (var gate in _gates)
        {
            if (gate != null && gate.DevelopmentId == developmentId && gate.FocusOnUnlock)
            {
                // 집 몸통쯤 (발밑보다 조금 위)
                _camera.PanTo((Vector2)gate.transform.position + Vector2.up * 1.5f);
                break;
            }
        }
    }

    private System.Collections.IEnumerator RebuildWalkableAfterPop()
    {
        yield return new WaitForSeconds(DevelopmentGate.PopSeconds + 0.05f);
        _walkableArea.Rebuild();
    }

    // 완성 순간: 먼지가 펑 (집은 DevelopmentGate가 통 튀어나오게 함), 카메라가 현장에 살짝 다가간 뒤 집 위로 별빛
    // 완료 팝업은 이 순간을 본 뒤에 뜸 (SettlementPresenter가 CelebrationSeconds만큼 기다림)
    private void HandleRequestCompleted(BoardRequestDefinition request)
    {
        var site = request.Construction != null ? FindSite(request.Construction.ConstructionId) : null;
        if (site == null)
            return;
        site.PlayCompleteBurst(CameraArriveSeconds);
        _camera.Celebrate((Vector2)site.transform.position + Vector2.up * 1.2f);
    }

    /// <summary>이 부탁의 완성 순간을 광장에서 보여 주는지 (완료 팝업을 조금 늦게 띄우려고)</summary>
    public bool CelebratesHere(BoardRequestDefinition request) =>
        request.Construction != null && FindSite(request.Construction.ConstructionId) != null;

    // 건설 해달이 일하러 가는 모습이 보이도록 현장을 비춤
    private void HandleConstructionStarted(BoardRequestDefinition request)
    {
        var site = request.Construction != null ? FindSite(request.Construction.ConstructionId) : null;
        if (site != null)
            _camera.PanTo(site.StandPoint);
    }

    private void HandleChanged()
    {
        // 만난 해달은 바로 맞추고(돌아온 주민은 가장자리에서 걸어 들어옴), 처음 오는 해달은 대기 줄에 넣음.
        // 부탁 완료로 해달이 찾아오는 순간에는 완료 팝업이 아직 줄에 들어오기 전이라, 실제로 내보내는 것은 다음 Update부터 (IsSafeToPresent)
        SyncOtters(false);
        UpdateConstruction();
    }

    private void ApplyAll(bool initial)
    {
        ApplyGates(_manager.HasDevelopment, false);
        ConvertDefaultProps();
        _walkableArea.Rebuild();
        SyncOtters(initial);
        UpdateConstruction();
    }

    // 광장에 처음부터 놓인 소품(Props 아래 그루터기 · 덤불 · 통나무 · 벤치 · 가로등 · 피크닉 매트) 중 상점 가구와 같은 그림인 것은
    // 세이브마다 한 번 같은 자리의 꾸미기 가구로 바꿔 (그 자리에 놓음. 치우면 가방으로) 상점 가구처럼 옮기거나 보관할 수 있게 한다.
    // 바꾼 소품은 그 뒤로 늘 숨김. 그 자리에 아직 놓을 수 없으면(잠긴 땅 등) 소품 그대로 두고 다음에 다시 봄
    private const string DefaultPropsRoot = "Props";
    private const string DefaultPropFlagPrefix = "decor_prop_";

    private void ConvertDefaultProps()
    {
        var decor = DecorManager.Instance;
        var board = DecorBoardView.Active;
        if (!_manager.IsLoaded || decor == null || board == null || board.Layout == null)
            return;

        bool placedAny = false;
        foreach (var prop in FindObjectsByType<PlazaProp>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var parent = prop.transform.parent;
            if (parent == null || parent.name != DefaultPropsRoot)
                continue;
            var furniture = FurnitureWithSprite(decor.Catalog, prop.GetComponent<SpriteRenderer>().sprite);
            if (furniture == null)
                continue;
            string flag = DefaultPropFlagPrefix + prop.name;
            if (_manager.Settlement.HasFlag(flag))
            {
                prop.gameObject.SetActive(false);
                continue;
            }

            // 소품의 발밑(가운데 아래)이 가구 칸의 아래 가운데가 되게
            var size = furniture.Footprint;
            var ground = (Vector2)prop.transform.position;
            var origin = board.WorldToCell(ground + new Vector2(-(size.x - 1) * 0.5f * board.CellSize, 0.01f));
            // 소품 자신의 발자국이 그 칸을 막고 있으므로 월드 쪽 막힘은 보지 않음 (구역 · 잠김 · 겹침은 봄)
            if (board.Layout.Check(furniture, origin, DecorRotation.R0, 0, true) != DecorPlacementResult.Ok)
                continue;
            // 놓인 가구는 가방에 들지 않음 (치우면 그때 가방으로)
            board.Layout.TryPlace(furniture, origin, DecorRotation.R0, out _, true);
            _manager.Settlement.SetFlag(flag);
            prop.gameObject.SetActive(false);
            placedAny = true;
        }
        if (placedAny)
            SettlementManager.RequestSave();
    }

    private static DecorDefinition FurnitureWithSprite(DecorCatalog catalog, Sprite sprite)
    {
        if (catalog == null || sprite == null)
            return null;
        foreach (var decor in catalog.Decors)
        {
            if (decor != null && decor.IsFurniture && decor.Item != null && decor.WorldSprite == sprite)
                return decor;
        }
        return null;
    }

    private void ApplyGates(System.Func<string, bool> isUnlocked, bool animate)
    {
        // 처음 넓힌 영토 쪽으로 이웃집 묶음을 먼저 옮김 (그 뒤에 걷기 영역을 계산하므로)
        foreach (var anchor in _anchors)
        {
            if (anchor != null)
                anchor.Apply(isUnlocked);
        }
        // 영토 묶음 안의 건물(새 이웃의 집 · 환영 소품)도 플레이어가 고른 자리로 (묶음이 부모를 옮겼으면 되돌림)
        ConstructionPlotAnchor.ReapplyAll();
        foreach (var gate in _gates)
        {
            if (gate != null)
                gate.Apply(isUnlocked, animate);
        }
    }

    #endregion

    #region 해달 — 복원과 방문 대기

    /// <summary>
    /// 주민 목록에 맞춰 광장 해달을 맞춘다. 만난 해달은 바로 내보내고, 처음 오는 해달은 대기 줄에 넣는다.
    /// 첫 해달 · 광부 · 농부 · 물감이 · 뚝딱이는 바로 나타나고, 나머지 미소개 해달은 대기 줄에서 한 마리씩 소개한다
    /// </summary>
    /// <param name="initial">씬을 연 직후 (복원은 모인 곳에서, 아니면 돌아오는 해달이 가장자리에서 걸어 들어옴)</param>
    private void SyncOtters(bool initial)
    {
        if (!_manager.IsLoaded)
            return;

        var config = _manager.Config;
        var settlement = _manager.Settlement;
        bool nobodyMet = settlement.MetOtters.Count == 0;
        List<SettlementOtterDefinition> metNow = null;
        foreach (var otterId in settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            if (otter == null || IsShown(otter) || IsAway(otter) || (!otter.IsBuilder && otter.PlazaPrefab == null)
                || !CanAppearYet(otter))
                continue;

            // 첫 해달과 생산 필수 해달은 즉시 소개. 일반 방문객은 한 마리씩.
            bool firstIntroduction = nobodyMet && otter == config.FirstOtter;
            bool immediateWorker = IsImmediateWorker(otter);
            if (!settlement.HasMet(otterId) && !firstIntroduction && !immediateWorker)
            {
                if (!_visitQueue.Contains(otterId) && !_presenting.Contains(otterId))
                    _visitQueue.Add(otterId);
                continue;
            }

            if (!Show(otter, !initial && !immediateWorker))
                continue;
            _visitQueue.Remove(otterId);
            if (!settlement.HasMet(otterId))
            {
                (metNow ??= new List<SettlementOtterDefinition>()).Add(otter);
                _nextVisitorTime = Time.unscaledTime + VisitorIntroductionSeconds;
            }
        }

        // 즉시 등장한 해달의 만남을 남김 (OnChanged가 재진입하므로 생성이 끝난 뒤에).
        if (metNow != null)
        {
            foreach (var otter in metNow)
                _manager.Meet(otter);
        }
    }

    // 지금 새 해달을 보여 줘도 되는지: 입력을 차지하는 화면(화면 전환, 완료 팝업, 레벨업, 돌아옴 보상 창, 튜토리얼, 확인 대화, 게시판·상점)이 없음
    private bool IsSafeToPresent()
    {
        if (_navigator != null && _navigator.IsTraveling)
            return false;
        if (SettlementPresenter.IsCelebrating || SettlementPresenter.IsPopupOpen || LevelUpPresenter.IsBusy)
            return false;
        if (TutorialOverlay.IsShowing)
            return false;
        if (GameManager.Instance != null && GameManager.Instance.IsModalOpen)
            return false;
        if (CommunityProjectPresenter.IsOpen || GatheringDirector.IsPlaying)
            return false;
        return FairyShopPresenter.Instance == null || !FairyShopPresenter.Instance.IsOpen;
    }

    // 대기 줄에서 한 마리만 가장자리로 들어오게 하고, 만남을 남긴 뒤 비춘다.
    private void PresentVisitors()
    {
        _presenting.Clear();
        _presenting.Add(_visitQueue[0]);
        _visitQueue.RemoveAt(0);
        _nextVisitorTime = Time.unscaledTime + VisitorIntroductionSeconds;

        var config = _manager.Config;
        var settlement = _manager.Settlement;
        SettlementOtterDefinition focus = null;
        foreach (var otterId in _presenting)
        {
            var otter = config.FindOtter(otterId);
            if (otter == null || settlement.HasMet(otterId) || !settlement.TryGetResidentState(otterId, out _))
                continue;
            // 그새 광장을 떠난 해달은 다시 기다림
            if (IsAway(otter) || !CanAppearYet(otter))
            {
                _visitQueue.Add(otterId);
                continue;
            }
            if (!IsShown(otter) && !Show(otter, true))
            {
                _visitQueue.Add(otterId);
                continue;
            }
            focus = otter;
            // 만남 기록 → OnChanged → SyncOtters가 다시 불려도 이미 내보내 만난 해달이라 그대로
            _manager.Meet(otter);
        }
        _presenting.Clear();

        // 장난감 방문객도 새로 만나는 한 마리를 비춰 소개한다.
        if (focus != null)
        {
            _introducedAt = Time.unscaledTime;
            FocusOtter(focus.OtterId);
        }
        UpdateConstruction();
    }

    private bool IsShown(SettlementOtterDefinition otter) =>
        otter.IsBuilder ? _builderInitialized : _spawned.ContainsKey(otter.OtterId);

    /// <param name="arriving">광장 가장자리에서 걸어 들어옴 (아니면 모인 곳에서 시작)</param>
    /// <returns>내보냈으면 true</returns>
    private bool Show(SettlementOtterDefinition otter, bool arriving)
    {
        if (otter.IsBuilder)
            return ShowBuilder(arriving);

        // 근무 중인 관리 해달은 씬을 열면 근무 자리에 바로 있음
        var station = StationOf(otter);
        Vector2 position;
        if (station != null && !arriving)
            position = station.StandPoint;
        else if (!TryFindSpawnPoint(arriving, out position))
            return false;
        if (!Spawn(otter, position))
            return false;
        if (station != null)
        {
            var agent = _spawned[otter.OtterId];
            agent.AssignTask(station.StandPoint, station.LookPoint);
            if (!arriving)
                agent.WarpToTask();
            else
                _stationSince[otter.OtterId] = Time.time;
        }
        return true;
    }

    // 광장에 없는 해달: 다른 장소의 주민 작업을 하러 갔거나, 배치되어 일하는 곳으로 간 전문 해달
    // (광장 현장의 주민 작업은 광장에서 일하므로 여기 있음)
    private bool IsAway(SettlementOtterDefinition otter)
    {
        // 광장 농장이 열리면 농부는 광장 밭에서 일함 (PlazaFarmView가 움직임)
        if (IsFarmWorker(otter.OtterId))
            return false;
        if (otter.IsSpecialist && !_manager.IsVisitingPlaza(otter))
            return true;
        return _manager.Settlement.GetWorkState(otter.OtterId) == ResidentWorkState.Working
            && _manager.PlazaTaskOf(otter.OtterId) == null;
    }

    /// <summary>광장 밭에서 일하는 농부 (농장이 열리고 농부를 배치한 뒤)</summary>
    public bool IsFarmWorker(string otterId) =>
        _manager != null && _manager.FarmOpen && _manager.HasFarmWorker && _manager.FarmConfig.FarmerOtterId == otterId;

    /// <summary>광장에 나온 해달의 걷기 (없으면 null) — 광장 농장의 농부</summary>
    public OtterWanderAgent FindAgent(string otterId) =>
        _spawned.TryGetValue(otterId, out var agent) && agent != null ? agent : null;

    private bool IsAway(string otterId)
    {
        var otter = _manager.Config.FindOtter(otterId);
        return otter != null && IsAway(otter);
    }

    /// <returns>내보냈으면 true</returns>
    private bool Spawn(SettlementOtterDefinition otter, Vector2 position)
    {
        var instance = Instantiate(otter.PlazaPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity, _otterRoot);
        instance.name = $"Settlement_{otter.OtterId}";
        var agent = instance.GetComponent<OtterWanderAgent>();
        if (agent == null)
        {
            Debug.LogError($"[SettlementPlazaView] '{otter.name}'의 광장 프리팹에 OtterWanderAgent가 없습니다.", otter);
            Destroy(instance);
            return false;
        }
        agent.Initialize(_walkableArea, _plazaSettings);
        agent.OtterId = otter.OtterId;
        agent.SetHabit(() => PickHabitTarget(otter.Trait), OtterHabits.Actions(otter.Trait));
        if (otter.PlazaTint != Color.white)
        {
            // 같은 프리팹을 쓰는 해달 구분용 임시 색
            foreach (var sprite in instance.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.color *= otter.PlazaTint;
        }
        _spawned[otter.OtterId] = agent;
        _views[otter.OtterId] = AttachView(instance, otter);
        return true;
    }

    // 특성마다 좋아하는 곳 하나 (지금 광장에 켜져 있는 것 중 무작위, 없으면 null)
    private readonly List<Transform> _habitCandidates = new List<Transform>();

    private Transform PickHabitTarget(OtterTrait trait)
    {
        _habitCandidates.Clear();
        OtterHabits.CollectTargets(trait, _arrivalPoint, _habitCandidates);
        return _habitCandidates.Count > 0 ? _habitCandidates[Random.Range(0, _habitCandidates.Count)] : null;
    }

    private SettlementOtterView AttachView(GameObject target, SettlementOtterDefinition otter)
    {
        var view = target.AddComponent<SettlementOtterView>();
        view.Init(otter, _plazaSettings.otterHeight, _speechBubble, _alertBubble, _hammerBubble, _speechFont);
        return view;
    }

    /// <summary>해달이 모이고 새 해달이 서 있는 곳(광장 가운데 조개 자리)에 물건·건물이 오지 않게 비워 둠</summary>
    public bool TryGetDecorBlock(out Rect worldRect)
    {
        worldRect = default;
        if (_gatherPoint == null || _gatherClearSize.x <= 0f || _gatherClearSize.y <= 0f)
            return false;
        worldRect = new Rect((Vector2)_gatherPoint.position - _gatherClearSize * 0.5f, _gatherClearSize);
        return true;
    }

    /// <summary>광장의 이 해달 쪽으로 카메라를 옮김 (안내 띠 "할 말이 있대요")</summary>
    public bool FocusOtter(string otterId)
    {
        Transform target = null;
        if (_spawned.TryGetValue(otterId, out var agent) && agent != null)
        {
            target = agent.transform;
            // 카메라가 가는 동안 걸어가 버려 누르기 어려웠음 (4 · 6차 QA) → 잠깐 그 자리에 세움
            agent.HoldStill(FocusHoldSeconds);
        }
        else if (_builderInitialized && _views.TryGetValue(otterId, out var view) && view != null)
            target = view.transform;
        if (target == null)
            return false;
        _camera.PanTo((Vector2)target.position + Vector2.up * 1f);
        return true;
    }

    /// <summary>이 공사 현장 쪽으로 카메라를 옮김 (공동사업 [현장 보기])</summary>
    public bool FocusSite(string constructionId)
    {
        var site = FindSite(constructionId);
        if (site == null)
            return false;
        _camera.PanTo(site.StandPoint);
        return true;
    }

    /// <summary>이름이 같은 공동사업 지점 쪽으로 카메라를 옮김 (장애물·정비 현장)</summary>
    public bool FocusPoint(string pointName)
    {
        foreach (var point in _focusPoints)
        {
            if (point != null && point.name == pointName)
            {
                _camera.PanTo(point.position);
                return true;
            }
        }
        return false;
    }

    /// <summary>광장의 이 해달 (아직 안 나왔으면 null). 튜토리얼 강조 등</summary>
    public Transform FindOtter(string otterId) =>
        _spawned.TryGetValue(otterId, out var agent) && agent != null ? agent.transform : null;

    // 다른 해달과 겹치지 않는 자리 (못 찾으면 겹쳐도 아무 데나)
    private bool TryFindSpawnPoint(bool arriving, out Vector2 position)
    {
        var anchor = arriving && _arrivalPoint != null ? _arrivalPoint : _gatherPoint;
        float radius = _plazaSettings.SpawnNearCameraRadius;
        for (int i = 0; i < 20; i++)
        {
            bool found = anchor != null
                ? _walkableArea.TryGetRandomPointNear(anchor.position, radius, 5, out position)
                : _walkableArea.TryGetRandomPoint(out position);
            if (found && PlazaCrowd.IsFree(position, _plazaSettings.SpawnSpacing, null))
                return true;
        }
        if (anchor != null && _walkableArea.TryGetRandomPointNear(anchor.position, radius, 20, out position))
            return true;
        return _walkableArea.TryGetRandomPoint(out position);
    }

    /// <returns>이번에 처음 내보냈으면 true</returns>
    private bool ShowBuilder(bool arriving)
    {
        if (_builderInitialized)
            return false;
        _builderInitialized = true;

        if (TryFindSpawnPoint(arriving, out Vector2 position))
            _builder.transform.position = new Vector3(position.x, position.y, _builder.transform.position.z);
        _builder.gameObject.SetActive(true);
        _builder.Initialize(_walkableArea);

        var builderDef = _manager.Config.FindBuilder();
        if (builderDef != null && !_views.ContainsKey(builderDef.OtterId))
            _views[builderDef.OtterId] = AttachView(_builder.gameObject, builderDef);
        return true;
    }

    #endregion

    #region 관리 해달

    // 역할을 맡긴 순간: 그 해달이 근무 자리로 걸어감 (도착하지 못해도 맡긴 것은 이미 저장됨)
    private void HandleRoleAssigned(ManagementRoleDefinition role)
    {
        if (role.Otter == null || !_spawned.TryGetValue(role.Otter.OtterId, out var agent) || agent == null)
            return;
        var station = FindStation(role.StationId);
        if (station == null)
            return;
        agent.AssignTask(station.StandPoint, station.LookPoint);
        _stationSince[role.Otter.OtterId] = Time.time;
        FocusOtter(role.Otter.OtterId);
    }

    // 근무 자리로 가는 길을 못 찾아 오래 걸리면 그 자리로 옮김
    private void KeepManagersAtStation()
    {
        if (_stationSince.Count == 0)
            return;
        _leftForWork.Clear();
        foreach (var pair in _stationSince)
        {
            if (!_spawned.TryGetValue(pair.Key, out var agent) || agent == null || agent.IsOnTask)
                _leftForWork.Add(pair.Key);
            else if (Time.time - pair.Value >= StationLimitSeconds)
            {
                agent.WarpToTask();
                _leftForWork.Add(pair.Key);
            }
        }
        foreach (var otterId in _leftForWork)
            _stationSince.Remove(otterId);
    }

    // 역할을 맡아 근무 중이면 그 근무 자리 (아니면 null)
    private ManagementStationView StationOf(SettlementOtterDefinition otter)
    {
        var role = _manager.AssignedRoleOf(otter);
        if (role == null || !_manager.Settlement.TryGetRole(role.RoleId, out var assignment))
            return null;
        return FindStation(assignment.StationId);
    }

    private ManagementStationView FindStation(string stationId)
    {
        foreach (var station in _stations)
        {
            if (station != null && station.StationId == stationId)
                return station;
        }
        return null;
    }

    #endregion

    #region 건설

    private void UpdateConstruction()
    {
        var job = _manager.Settlement.Job;
        foreach (var site in _sites)
        {
            // 터·먼지는 해달이 도착해 일을 시작하면
            bool building = job != null && job.ConstructionId == site.ConstructionId && !job.WaitingForWorker;
            site.SetBuilding(building);
            if (building)
                site.SetProgress(job.Progress(SettlementManager.NowTicks), false);
        }

        // 건설 해달이 필요 없는 공사(첫 집)는 부탁한 해달이 직접 현장에서 일함
        var request = _manager.JobRequest;
        var jobSite = job != null ? FindSite(job.ConstructionId) : null;
        bool byBuilder = request == null || request.Construction.NeedsBuilder;
        string workerId = jobSite != null && !byBuilder && request.Requester != null ? request.Requester.OtterId : null;
        foreach (var pair in _spawned)
        {
            if (pair.Value == null)
                continue;
            // 광장 농부는 PlazaFarmView가 일을 줌
            if (IsFarmWorker(pair.Key))
                continue;
            bool working = pair.Key == workerId;
            var otter = _manager.Config.FindOtter(pair.Key);
            var station = otter != null ? StationOf(otter) : null;
            var plazaTask = _manager.PlazaTaskOf(pair.Key);
            // 주민 작업에 보낸 해달·배치한 전문 해달은 광장 가장자리 길로 걸어 나감 (도착하면 사라짐)
            if (IsAway(pair.Key))
                pair.Value.AssignTask(_arrivalPoint.position, _arrivalPoint.position);
            else if (working)
                pair.Value.AssignTask(jobSite.StandPoint, jobSite.LookPoint);
            else if (plazaTask != null && TryGetTaskStand(plazaTask, pair.Key, out Vector2 stand, out Vector2 look))
            {
                working = true;
                pair.Value.AssignTask(stand, look);
            }
            else if (station != null)
                pair.Value.AssignTask(station.StandPoint, station.LookPoint);
            else
                pair.Value.ClearTask();
            if (_views.TryGetValue(pair.Key, out var view) && view != null)
                view.SetWorking(working);
        }

        if (!_builderInitialized)
            return;
        if (jobSite != null && byBuilder)
            _builder.SetWorkSite(jobSite);
        else if (TryGetRelocationWork(out int movingId, out Vector2 relocationStand, out Vector2 relocationLook))
        {
            // 공사가 없으면 이사하는 건물 앞에서 일함 (구역·이사). 막 시작한 이사면 먼저 옛 자리에서 건물을 이고 옴
            if (!(_carryFrom.TryGetValue(movingId, out var fromArea) && TryStartCarry(movingId, fromArea, relocationStand, relocationLook)))
                _builder.SetWork(movingId, relocationStand, relocationLook);
        }
        else
            _builder.SetWorkSite(null);
    }

    private void HandleRelocationStarted(int instanceId, RectInt fromArea)
    {
        _carryFrom[instanceId] = fromArea;
        UpdateConstruction();
    }

    private void HandleRelocationChanged()
    {
        // 끝난 이사의 옛 자리는 지움 (건설 해달이 다른 공사로 바빠 못 들른 것)
        _staleCarry.Clear();
        foreach (var id in _carryFrom.Keys)
        {
            if (!_manager.Settlement.IsRelocating(id))
                _staleCarry.Add(id);
        }
        foreach (var id in _staleCarry)
            _carryFrom.Remove(id);
        if (_builderInitialized)
            UpdateConstruction();
    }

    // 옛 자리에 건물 그림을 남겨 두고(건설 해달이 들러 들고 감), 새 자리의 건물은 해달이 내려놓을 때까지 숨김
    private bool TryStartCarry(int instanceId, RectInt fromArea, Vector2 stand, Vector2 look)
    {
        _carryFrom.Remove(instanceId);
        var board = DecorBoardView.Active;
        var view = board != null ? board.FindView(instanceId) : null;
        if (view == null)
            return false;
        // 게시판 부탁 건물(건설 자리)은 씬의 건물 묶음이 그림을 그림
        string plotId = view is PlacedPlotView ? view.Placed.Decor.SaveId : null;
        var anchor = ConstructionPlotAnchor.Find(plotId);
        var body = LargestSprite(anchor != null ? anchor.VisibleRenderers() : view.GetComponentsInChildren<SpriteRenderer>());
        if (body == null)
            return false;

        var from = board.AreaWorldRect(fromArea);
        var ghost = new GameObject($"CarryGhost_{instanceId}").AddComponent<SpriteRenderer>();
        ghost.sprite = body.sprite;
        ghost.flipX = body.flipX;
        ghost.sortingLayerID = body.sortingLayerID;
        ghost.sortingOrder = PlazaDepth.SortingOrderFor(from.yMin);
        ghost.transform.SetPositionAndRotation(body.transform.position + (Vector3)(from.min - view.WorldRect.min), body.transform.rotation);
        ghost.transform.localScale = body.transform.lossyScale;
        SetCarryHidden(instanceId, plotId, true);

        var pickup = new Vector2(from.center.x + from.width * 0.25f, from.yMin - 0.5f);
        _builder.SetCarryWork(instanceId, pickup, stand, look, body.sprite,
            () =>
            {
                if (ghost != null)
                    Destroy(ghost.gameObject);
            },
            () => SetCarryHidden(instanceId, plotId, false));
        return true;
    }

    private static void SetCarryHidden(int instanceId, string plotId, bool hidden)
    {
        var anchor = ConstructionPlotAnchor.Find(plotId);
        var board = DecorBoardView.Active;
        if (anchor != null)
            anchor.SetHidden(hidden);
        else if (board != null)
            board.SetHidden(instanceId, hidden);
    }

    // 건물 모습에서 가장 큰 그림 (공사 막대 · 요정 같은 작은 것 말고 건물 본체)
    private static SpriteRenderer LargestSprite(IEnumerable<SpriteRenderer> renderers)
    {
        SpriteRenderer best = null;
        float bestArea = 0f;
        foreach (var renderer in renderers)
        {
            if (renderer.sprite == null || !renderer.enabled)
                continue;
            var size = renderer.bounds.size;
            if (size.x * size.y > bestArea)
            {
                bestArea = size.x * size.y;
                best = renderer;
            }
        }
        return best;
    }

    // 옮기는 중인 건물 하나 (먼저 시작한 것): 건물 앞 가운데에 서서 건물을 봄
    private bool TryGetRelocationWork(out int instanceId, out Vector2 stand, out Vector2 look)
    {
        instanceId = 0;
        stand = look = default;
        var board = DecorBoardView.Active;
        var plaza = _manager.PlazaBoard;
        if (board == null || plaza == null || board.Board != plaza)
            return false;
        var layout = DecorManager.Instance.GetLayout(plaza);
        long earliest = long.MaxValue;
        foreach (var move in _manager.Settlement.RelocationMoves)
        {
            if (move.StartUtcTicks >= earliest || !layout.TryGet(move.InstanceId, out var placed))
                continue;
            earliest = move.StartUtcTicks;
            instanceId = move.InstanceId;
            var rect = board.AreaWorldRect(placed.Area);
            stand = new Vector2(rect.center.x + rect.width * 0.25f, rect.yMin - 0.5f);
            look = rect.center;
        }
        return instanceId != 0;
    }

    // 광장 주민 작업에서 이 해달이 설 자리 (보낸 순서대로). 생활 의뢰처럼 같은 작업 틀을 회차마다 하는 경우도 그 해달의 작업으로 찾음
    private bool TryGetTaskStand(SettlementTaskDefinition task, string otterId, out Vector2 stand, out Vector2 look)
    {
        stand = look = default;
        var site = FindTaskSite(task);
        var territorySite = site == null ? FindTerritorySite(task) : null;
        var job = _manager.Settlement.FindTaskJobOf(otterId);
        if (site == null && territorySite == null || job == null)
            return false;
        int index = 0;
        for (int i = 0; i < job.OtterIds.Count; i++)
        {
            if (job.OtterIds[i] == otterId)
                index = i;
        }
        stand = site != null ? site.StandPoint(index) : territorySite.StandPoint(index);
        look = site != null ? site.LookPoint : territorySite.LookPoint;
        return true;
    }

    private TerritorySiteView FindTerritorySite(SettlementTaskDefinition task)
    {
        foreach (var site in _territorySites)
        {
            if (site != null && site.Handles(task))
                return site;
        }
        return null;
    }

    private PlazaTaskSiteView FindTaskSite(SettlementTaskDefinition task)
    {
        foreach (var site in _taskSites)
        {
            if (site != null && site.Task == task)
                return site;
        }
        return null;
    }

    // 주민 작업에 보낸 해달·배치한 전문 해달이 광장 가장자리에 닿으면 광장에서 내보냄 (길을 못 찾아 오래 걸려도 내보냄)
    private void RemoveOttersAtWork()
    {
        _leftForWork.Clear();
        foreach (var pair in _spawned)
        {
            if (pair.Value == null || !IsAway(pair.Key))
            {
                _leavingSince.Remove(pair.Key);
                continue;
            }
            if (!_leavingSince.TryGetValue(pair.Key, out float since))
                _leavingSince[pair.Key] = since = Time.time;
            if (pair.Value.IsOnTask || Time.time - since >= LeaveLimitSeconds)
                _leftForWork.Add(pair.Key);
        }
        foreach (var otterId in _leftForWork)
        {
            Destroy(_spawned[otterId].gameObject);
            _spawned.Remove(otterId);
            _views.Remove(otterId);
            _leavingSince.Remove(otterId);
        }
    }

    private ConstructionSiteView FindSite(string constructionId)
    {
        foreach (var site in _sites)
        {
            if (site != null && site.ConstructionId == constructionId)
                return site;
        }
        return null;
    }

    #endregion
}
