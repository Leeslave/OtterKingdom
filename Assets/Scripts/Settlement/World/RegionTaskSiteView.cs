using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 개간 지역의 후속 정비 현장 (광산 입구 앞). 지역 단계에 맞춰 보여 준다:
/// - 주민을 기다림: 망치 표지판이 통통 → 누르면 작업 화면이 열림 (처음 한 번 "눌러서 해달을 보내요")
/// - 정비 중: 보낸 주민 해달이 걸어와 일하고(망치 말풍선), 현장 위에 남은 시간
/// - 끝남: 해달이 걸어 나가 사라짐 (광장으로 돌아감)
/// 작업 상태는 SettlementManager가 정하고, 여기는 그리기만 한다.
/// </summary>
public class RegionTaskSiteView : MonoBehaviour
{
    private const float MarkerBobHeight = 0.12f;
    // 작업이 시작된 지 이보다 오래면 씬을 열었을 때 해달이 이미 현장에서 일하는 중
    private const float WalkInGraceSeconds = 4f;

    [Header("지역")]
    [SerializeField] private DevelopableRegionDefinition _region;

    [Header("자리")]
    [Tooltip("주민 해달이 일할 자리 (보낸 수만큼. 모자라면 앞 자리 옆에 섬)")]
    [SerializeField] private List<Transform> _standPoints = new List<Transform>();

    [Tooltip("일하며 바라볼 곳 (치운 자리 가운데)")]
    [SerializeField] private Transform _lookPoint;

    [Tooltip("해달이 걸어 들어오고 나가는 곳 (화면 아래쪽 길)")]
    [SerializeField] private Transform _entryPoint;

    [Header("표지판")]
    [Tooltip("주민을 기다릴 때 통통 뛰는 망치 표지판")]
    [SerializeField] private SpriteRenderer _marker;

    [SerializeField] private Collider2D _markerTap;

    [Tooltip("처음 한 번 \"눌러서 해달을 보내요\" (비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Header("진행 표시")]
    [Tooltip("정비 중에만 보이는 말풍선")]
    [SerializeField] private GameObject _progressBubble;

    [Tooltip("말풍선 안의 작업 이름과 남은 시간")]
    [SerializeField] private TextMeshPro _progressText;

    [Header("해달")]
    [Tooltip("이 장소에서의 해달 크기 (광장 해달 대비)")]
    [SerializeField] private float _workerScale = 0.6f;

    [Tooltip("이 장소에서 발밑에서 머리 위까지 (말풍선 위치)")]
    [SerializeField] private float _workerHeight = 1f;

    [Tooltip("걷는 빠르기 (초당 월드 단위)")]
    [SerializeField] private float _walkSpeed = 1.2f;

    [Tooltip("이 장소의 앞뒤 정렬 기준 (광부·장애물의 GroundDepthSort와 같게)")]
    [SerializeField] private int _depthBase = 1000;

    [SerializeField] private Sprite _speechBubble;
    [SerializeField] private Sprite _alertBubble;
    [SerializeField] private Sprite _hammerBubble;
    [SerializeField] private TMP_FontAsset _font;

    private readonly Dictionary<string, RegionWorkerView> _workers = new Dictionary<string, RegionWorkerView>();
    private readonly List<string> _gone = new List<string>();
    private Vector3 _markerBase;
    private int _lastSeconds = -1;

    private void Awake()
    {
        _markerBase = _marker.transform.localPosition;
        _marker.gameObject.SetActive(false);
        _progressBubble.SetActive(false);
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        var state = manager.GetRegionState(_region);
        var task = manager.CurrentPreparation(_region);
        var job = manager.GetTaskJob(task);

        UpdateMarker(manager, state == RegionProgressState.AwaitingWorkers ? task : null);
        UpdateWorkers(manager, job);
        UpdateProgress(task, job);
    }

    private void UpdateMarker(SettlementManager manager, SettlementTaskDefinition waitingTask)
    {
        bool visible = waitingTask != null;
        if (_marker.gameObject.activeSelf != visible)
            _marker.gameObject.SetActive(visible);
        if (_hint != null)
            _hint.Allowed = visible;
        if (!visible)
            return;

        _marker.transform.localPosition = _markerBase + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 3.5f)) * MarkerBobHeight);
        if (PlazaTapInput.TryGetTap(out Vector2 world) && _markerTap.OverlapPoint(world))
        {
            if (_hint != null)
                _hint.MarkDone();
            manager.RequestTask(waitingTask);
        }
    }

    // 작업 중인 해달은 현장에, 작업이 끝난 해달은 걸어 나감
    private void UpdateWorkers(SettlementManager manager, SettlementTaskJob job)
    {
        if (job != null)
        {
            bool justStarted = (SettlementManager.NowTicks - job.StartUtcTicks) / (float)System.TimeSpan.TicksPerSecond < WalkInGraceSeconds;
            for (int i = 0; i < job.OtterIds.Count; i++)
            {
                string otterId = job.OtterIds[i];
                if (_workers.TryGetValue(otterId, out var existing) && existing != null && !existing.IsLeaving)
                    continue;
                var otter = manager.Config.FindOtter(otterId);
                if (otter == null || otter.PlazaPrefab == null)
                    continue;
                Vector2 stand = StandPoint(i);
                Vector2 from = justStarted ? (Vector2)_entryPoint.position : stand;
                var worker = RegionWorkerView.Spawn(otter, transform, from, _workerScale, _workerHeight, _depthBase, _walkSpeed,
                    _speechBubble, _alertBubble, _hammerBubble, _font);
                worker.WorkAt(stand, _lookPoint.position);
                _workers[otterId] = worker;
            }
        }

        _gone.Clear();
        foreach (var pair in _workers)
        {
            if (pair.Value == null)
                _gone.Add(pair.Key);
            else if (!pair.Value.IsLeaving && (job == null || !job.HasOtter(pair.Key)))
                pair.Value.Leave(_entryPoint.position);
        }
        foreach (var id in _gone)
            _workers.Remove(id);
    }

    // 자리가 모자라면 마지막 자리 옆으로 조금씩 비켜 섬
    private Vector2 StandPoint(int index)
    {
        if (index < _standPoints.Count)
            return _standPoints[index].position;
        var last = (Vector2)_standPoints[_standPoints.Count - 1].position;
        return last + new Vector2(0.6f * (index - _standPoints.Count + 1), -0.2f);
    }

    private void UpdateProgress(SettlementTaskDefinition task, SettlementTaskJob job)
    {
        bool visible = job != null;
        if (_progressBubble.activeSelf != visible)
            _progressBubble.SetActive(visible);
        if (!visible)
        {
            _lastSeconds = -1;
            return;
        }

        int seconds = Mathf.CeilToInt((float)job.Remaining(SettlementManager.NowTicks).TotalSeconds);
        if (seconds == _lastSeconds)
            return;
        _lastSeconds = seconds;
        _progressText.text = $"{task.Title}\n{SettlementManager.FormatShort(System.TimeSpan.FromSeconds(seconds))}";
    }
}
