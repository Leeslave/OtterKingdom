using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장을 정착 진행에 맞춘다 (광장 씬에 하나).
/// - 발전에 따라 집·벤치·잡목 등을 켜고 끔 (DevelopmentGate) → 걷기 영역 다시 계산
/// - 정착 해달(첫 해달, 방문 해달, 정착 후보)을 광장에 내보냄 (재접속해도 한 마리씩)
/// - 건설 해달을 내보내고, 건설 중이면 현장으로 보내 일하게 함 (건설 해달이 필요 없는 첫 집은 부탁한 해달이 직접)
/// - 정착 해달에 말풍선(탭하면 한마디, 정착 후보의 "!")을 붙임
/// 기존 광장 코드(PlazaController 배회, A*, 깊이 정렬)는 그대로 쓰고 그 위에서 켜고 끄기만 한다.
/// </summary>
// GameManager.Start(0)가 세이브를 불러온 뒤, PlazaController.Start(0) 다음에 광장을 맞춤
[DefaultExecutionOrder(50)]
public class SettlementPlazaView : MonoBehaviour
{
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
    [Tooltip("새로 찾아온 해달이 나타나는 곳 (광장 가장자리 길)")]
    [SerializeField] private Transform _arrivalPoint;

    [Header("발전")]
    [Tooltip("이 아래의 DevelopmentGate를 모두 다룸 (꺼져 있어도 찾음)")]
    [SerializeField] private List<Transform> _gateRoots = new List<Transform>();

    [Header("건설")]
    [SerializeField] private List<ConstructionSiteView> _sites = new List<ConstructionSiteView>();
    [Tooltip("건설 해달 (오기 전에는 꺼져 있음)")]
    [SerializeField] private BuilderOtterController _builder;

    [Header("해달 말풍선")]
    [SerializeField] private Sprite _speechBubble;
    [SerializeField] private Sprite _alertBubble;
    [SerializeField] private Sprite _hammerBubble;
    [SerializeField] private TMP_FontAsset _speechFont;

    private readonly List<DevelopmentGate> _gates = new List<DevelopmentGate>();
    private readonly Dictionary<string, OtterWanderAgent> _spawned = new Dictionary<string, OtterWanderAgent>();
    private readonly Dictionary<string, SettlementOtterView> _views = new Dictionary<string, SettlementOtterView>();
    private SettlementManager _manager;
    private bool _builderInitialized;

    private void Awake()
    {
        Active = this;
        foreach (var root in _gateRoots)
        {
            if (root != null)
                _gates.AddRange(root.GetComponentsInChildren<DevelopmentGate>(true));
        }
        _builder.gameObject.SetActive(false);
    }

    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null)
        {
            // 정착 매니저가 없는 테스트: 다 보이는 광장
            ApplyGates(_ => true, false);
            _walkableArea.Rebuild();
            return;
        }

        ApplyAll(false);
        _manager.OnLoaded += HandleLoaded;
        _manager.OnChanged += HandleChanged;
        _manager.OnDevelopmentUnlocked += HandleDevelopmentUnlocked;
        _manager.OnConstructionStarted += HandleConstructionStarted;
        _manager.OnRequestCompleted += HandleRequestCompleted;
    }

    // 공사 과정 (집이 올라오고, 치울 것이 하나씩 사라짐)
    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded)
            return;
        var job = _manager.Settlement.Job;
        var site = job != null ? FindSite(job.ConstructionId) : null;
        if (site != null)
            site.SetProgress(job.Progress(SettlementManager.NowTicks), true);
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
        }
    }

    /// <summary>진행 말풍선을 띄울 곳 (이 광장에 그 현장이 없으면 false)</summary>
    public bool TryGetSiteAnchor(string constructionId, out Vector3 world)
    {
        var site = FindSite(constructionId);
        world = site != null ? site.BubbleAnchor : default;
        return site != null;
    }

    #region 적용

    private void HandleLoaded() => ApplyAll(false);

    private void HandleDevelopmentUnlocked(string developmentId)
    {
        // 새로 생긴 집은 통통 튀어나오고, 길이 바뀌었으니 걷기 영역을 다시 계산
        ApplyGates(_manager.HasDevelopment, true);
        _walkableArea.Rebuild();

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

    // 완성 순간 먼지가 펑 (집은 DevelopmentGate가 통 튀어나오게 함)
    private void HandleRequestCompleted(BoardRequestDefinition request)
    {
        var site = request.Construction != null ? FindSite(request.Construction.ConstructionId) : null;
        if (site != null)
            site.PlayCompleteBurst();
    }

    // 건설 해달이 일하러 가는 모습이 보이도록 현장을 비춤
    private void HandleConstructionStarted(BoardRequestDefinition request)
    {
        var site = request.Construction != null ? FindSite(request.Construction.ConstructionId) : null;
        if (site != null)
            _camera.PanTo(site.StandPoint);
    }

    private void HandleChanged()
    {
        SpawnSettlementOtters(true);
        UpdateConstruction();
    }

    private void ApplyAll(bool animate)
    {
        ApplyGates(_manager.HasDevelopment, animate);
        _walkableArea.Rebuild();
        SpawnSettlementOtters(false);
        UpdateConstruction();
    }

    private void ApplyGates(System.Func<string, bool> isUnlocked, bool animate)
    {
        foreach (var gate in _gates)
        {
            if (gate != null)
                gate.Apply(isUnlocked(gate.DevelopmentId), animate);
        }
    }

    #endregion

    #region 해달

    /// <param name="arriving">지금 막 찾아온 해달이면 광장 가장자리에서 나타남</param>
    private void SpawnSettlementOtters(bool arriving)
    {
        if (!_manager.IsLoaded)
            return;

        var config = _manager.Config;
        foreach (var otterId in _manager.Settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            if (otter == null)
                continue;

            if (otter.IsBuilder)
            {
                ShowBuilder(arriving);
                continue;
            }
            if (otter.PlazaPrefab == null || _spawned.ContainsKey(otterId))
                continue;

            if (TryFindSpawnPoint(arriving, out Vector2 position))
                Spawn(otter, position);
        }
    }

    private void Spawn(SettlementOtterDefinition otter, Vector2 position)
    {
        var instance = Instantiate(otter.PlazaPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity, _otterRoot);
        instance.name = $"Settlement_{otter.OtterId}";
        var agent = instance.GetComponent<OtterWanderAgent>();
        if (agent == null)
        {
            Debug.LogError($"[SettlementPlazaView] '{otter.name}'의 광장 프리팹에 OtterWanderAgent가 없습니다.", otter);
            Destroy(instance);
            return;
        }
        agent.Initialize(_walkableArea, _plazaSettings);
        _spawned[otter.OtterId] = agent;
        _views[otter.OtterId] = AttachView(instance, otter);
    }

    private SettlementOtterView AttachView(GameObject target, SettlementOtterDefinition otter)
    {
        var view = target.AddComponent<SettlementOtterView>();
        view.Init(otter, _plazaSettings.otterHeight, _speechBubble, _alertBubble, _hammerBubble, _speechFont);
        return view;
    }

    /// <summary>광장의 이 해달 쪽으로 카메라를 옮김 (안내 띠 "할 말이 있대요")</summary>
    public bool FocusOtter(string otterId)
    {
        Transform target = null;
        if (_spawned.TryGetValue(otterId, out var agent) && agent != null)
            target = agent.transform;
        else if (_builderInitialized && _views.TryGetValue(otterId, out var view) && view != null)
            target = view.transform;
        if (target == null)
            return false;
        _camera.PanTo((Vector2)target.position + Vector2.up * 1f);
        return true;
    }

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

    private void ShowBuilder(bool arriving)
    {
        if (_builderInitialized)
            return;
        _builderInitialized = true;

        if (TryFindSpawnPoint(arriving, out Vector2 position))
            _builder.transform.position = new Vector3(position.x, position.y, _builder.transform.position.z);
        _builder.gameObject.SetActive(true);
        _builder.Initialize(_walkableArea);

        var builderDef = _manager.Config.FindBuilder();
        if (builderDef != null && !_views.ContainsKey(builderDef.OtterId))
            _views[builderDef.OtterId] = AttachView(_builder.gameObject, builderDef);
    }

    #endregion

    #region 건설

    private void UpdateConstruction()
    {
        var job = _manager.Settlement.Job;
        foreach (var site in _sites)
        {
            bool building = job != null && job.ConstructionId == site.ConstructionId;
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
            bool working = pair.Key == workerId;
            if (working)
                pair.Value.AssignTask(jobSite.StandPoint, jobSite.LookPoint);
            else
                pair.Value.ClearTask();
            if (_views.TryGetValue(pair.Key, out var view) && view != null)
                view.SetWorking(working);
        }

        if (!_builderInitialized)
            return;
        _builder.SetWorkSite(jobSite != null && byBuilder ? jobSite : null);
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
