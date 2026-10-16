using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장에서 하는 주민 작업 현장 (공동 공간 정비, 게시판 주변 정리). 작업 상태에 맞춰 보여 준다:
/// - 시작할 수 있음: 망치 표지판이 통통 → 누르면 작업 화면이 열림 (처음 한 번 "눌러서 해달을 보내요")
/// - 작업 중: 현장 위에 작업 이름과 남은 시간. 일하는 해달은 SettlementPlazaView가 이 현장의 자리로 보냄
/// 작업 상태는 SettlementManager가 정하고, 여기는 그리기만 한다. 개간 지역 현장(RegionTaskSiteView)과 달리 해달이 광장을 떠나지 않는다.
/// </summary>
public class PlazaTaskSiteView : MonoBehaviour
{
    private const float MarkerBobHeight = 0.12f;

    [Header("작업")]
    [SerializeField] private SettlementTaskDefinition _task;

    [Tooltip("생활 의뢰처럼 같은 작업 틀을 회차마다 하는 현장: 망치 표지판 없이(의뢰 화면에서 보냄) 진행 중인 회차만 보여 줌")]
    [SerializeField] private bool _repeatedWork;

    [Header("자리")]
    [Tooltip("주민 해달이 일할 자리 (보낸 수만큼. 모자라면 앞 자리 옆에 섬)")]
    [SerializeField] private List<Transform> _standPoints = new List<Transform>();

    [Tooltip("일하며 바라볼 곳 (현장 가운데)")]
    [SerializeField] private Transform _lookPoint;

    [Header("표지판")]
    [Tooltip("시작할 수 있을 때 통통 뛰는 망치 표지판")]
    [SerializeField] private SpriteRenderer _marker;

    [SerializeField] private Collider2D _markerTap;

    [Tooltip("처음 한 번 \"눌러서 해달을 보내요\" (비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Header("진행 표시")]
    [Tooltip("작업 중에만 보이는 말풍선")]
    [SerializeField] private GameObject _progressBubble;

    [Tooltip("말풍선 안의 작업 이름과 남은 시간")]
    [SerializeField] private TextMeshPro _progressText;

    private Vector3 _markerBase;
    private int _lastSeconds = -1;

    public SettlementTaskDefinition Task => _task;
    public Vector2 LookPoint => _lookPoint != null ? (Vector2)_lookPoint.position : (Vector2)transform.position;

    /// <summary>보낸 순서(index)의 해달이 설 자리. 자리가 모자라면 마지막 자리 옆으로 조금씩 비켜 섬</summary>
    public Vector2 StandPoint(int index)
    {
        if (_standPoints.Count == 0)
            return transform.position;
        if (index < _standPoints.Count)
            return _standPoints[index].position;
        var last = (Vector2)_standPoints[_standPoints.Count - 1].position;
        return last + new Vector2(0.6f * (index - _standPoints.Count + 1), -0.2f);
    }

    private void Awake()
    {
        _markerBase = _marker.transform.localPosition;
        _marker.gameObject.SetActive(false);
        _progressBubble.SetActive(false);
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded || _task == null)
            return;

        if (_repeatedWork)
        {
            UpdateMarker(manager, false);
            UpdateProgress(manager.FindRepeatedTaskJob(_task));
            return;
        }
        var state = manager.GetTaskState(_task);
        UpdateMarker(manager, state == SettlementTaskState.Available);
        UpdateProgress(state == SettlementTaskState.Working ? manager.GetTaskJob(_task) : null);
    }

    private void UpdateMarker(SettlementManager manager, bool visible)
    {
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
            manager.RequestTask(_task);
        }
    }

    private void UpdateProgress(SettlementTaskJob job)
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
        _progressText.text = $"{_task.Title}\n{SettlementManager.FormatShort(System.TimeSpan.FromSeconds(seconds))}";
    }
}
