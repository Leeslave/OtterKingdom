using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 광장 가장자리의 숲 개간 자리 (방향마다 하나, 그 방향에서 지금 넓힐 단계의 끝으로 옮겨 감). 영토 상태에 맞춰 보여 준다:
/// - 개간할 수 있음 (기회가 있고 다른 개간이 없음): 점선 원 + 도끼 말풍선 + "서쪽 숲 개간 1/3" → 누르면 작업 화면 (주민 해달 고르기)
/// - 이 방향을 개간하는 중: 점선 원 + 남은 시간 말풍선. 보낸 해달은 SettlementPlazaView가 이 자리로 보내 숲을 향해 일하게 함
/// - 숲 개간을 다 끝냄: 점선 원 + "영토 확장 미션" → 누르면 미션 화면 (마을회관에서도 열 수 있음)
/// - 기회가 없음 · 다른 곳을 개간하는 중 · 다 넓힘: 숨김
/// 상태는 SettlementManager가 정하고 여기는 그리기만 한다.
/// </summary>
public class TerritorySiteView : MonoBehaviour
{
    private const float MarkerBobHeight = 0.12f;
    private const float RingTurnDegreesPerSecond = 12f;

    [SerializeField] private TerritoryDirection _direction;

    [Header("자리")]
    [Tooltip("단계마다 이 자리가 놓일 곳 (월드, 0 = 1단계)")]
    [SerializeField] private List<Vector3> _tierPositions = new List<Vector3>();

    [Tooltip("보낸 해달이 설 곳 (자리 기준)")]
    [SerializeField] private List<Vector2> _standOffsets = new List<Vector2>();

    [Tooltip("일하며 바라볼 곳 (자리 기준, 숲 쪽)")]
    [SerializeField] private Vector2 _lookOffset;

    [Header("그림")]
    [Tooltip("바닥의 점선 원")]
    [SerializeField] private SpriteRenderer _ring;

    [Tooltip("통통 뛰는 말풍선 (개간 = 도끼)")]
    [SerializeField] private SpriteRenderer _marker;

    [SerializeField] private Sprite _clearSprite;
    [Tooltip("미션 차례일 때의 말풍선")]
    [SerializeField] private Sprite _missionSprite;

    [Tooltip("누르는 곳 (점선 원 + 말풍선)")]
    [SerializeField] private Collider2D _tapArea;

    [SerializeField] private TextMeshPro _label;

    [Tooltip("처음 한 번 \"눌러서 해달을 보내요\" (비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Header("진행 표시")]
    [SerializeField] private GameObject _progressBubble;
    [SerializeField] private TextMeshPro _progressText;

    private enum Mode { Hidden, Clear, Working, Mission }

    private SettlementManager _manager;
    private Vector3 _markerBase;
    private Color _ringColor;
    private Mode _mode = Mode.Hidden;
    private int _seenVersion = -1;
    private TerritoryExpansionDefinition _territory;
    private SettlementTaskDefinition _task;
    private int _lastSeconds = -1;

    public TerritoryDirection Direction => _direction;
    public Vector2 LookPoint => (Vector2)transform.position + _lookOffset;

    /// <summary>이 자리에서 하는 숲 개간인지 (이 방향의 영토)</summary>
    public bool Handles(SettlementTaskDefinition task)
    {
        // 광장 뷰가 이 자리의 Start보다 먼저 물을 수 있음
        var manager = _manager != null ? _manager : SettlementManager.Instance;
        if (manager == null || task == null)
            return false;
        var territory = TerritoryRules.FindByTask(manager.Config, task, out _);
        if (territory == null || territory.Direction != _direction)
            return false;
        // 해달이 설 곳을 묻기 전에 자리를 지금 단계로 옮겨 둠
        _manager = manager;
        if (manager.IsLoaded && manager.TerritoryVersion != _seenVersion)
            Refresh();
        return true;
    }

    /// <summary>보낸 순서(index)의 해달이 설 곳</summary>
    public Vector2 StandPoint(int index)
    {
        Vector2 basePoint = transform.position;
        if (_standOffsets.Count == 0)
            return basePoint;
        if (index < _standOffsets.Count)
            return basePoint + _standOffsets[index];
        return basePoint + _standOffsets[_standOffsets.Count - 1] + new Vector2(0f, -0.6f * (index - _standOffsets.Count + 1));
    }

    private void Awake()
    {
        _markerBase = _marker.transform.localPosition;
        _ringColor = _ring.color;
        Show(Mode.Hidden);
    }

    private void Start() => _manager = SettlementManager.Instance;

    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded)
            return;
        if (_manager.TerritoryVersion != _seenVersion)
            Refresh();
        if (_mode == Mode.Hidden)
            return;

        // 점선 원이 천천히 돌며 숨 쉬듯
        _ring.transform.Rotate(0f, 0f, -RingTurnDegreesPerSecond * Time.deltaTime);
        var color = _ringColor;
        color.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 2.4f);
        _ring.color = color;

        if (_mode == Mode.Working)
        {
            UpdateProgress();
            return;
        }
        _marker.transform.localPosition = _markerBase + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 3.5f)) * MarkerBobHeight);
        if (PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
        {
            if (_hint != null)
                _hint.MarkDone();
            if (_mode == Mode.Clear && _task != null)
                _manager.RequestTask(_task);
            else if (_mode == Mode.Mission && _territory.Mission != null)
                _manager.RequestProject(_territory.Mission);
        }
    }

    private void Refresh()
    {
        _seenVersion = _manager.TerritoryVersion;
        _territory = _manager.CurrentTerritory(_direction);
        _task = null;
        if (_territory == null)
        {
            Show(Mode.Hidden);
            return;
        }
        int tier = _territory.Tier - 1;
        if (tier >= 0 && tier < _tierPositions.Count)
            transform.position = _tierPositions[tier];

        var working = _manager.WorkingTerritory(out var workingTask);
        if (working == _territory)
        {
            _task = workingTask;
            Show(Mode.Working);
            return;
        }
        switch (_manager.GetTerritoryBlock(_direction))
        {
            case TerritoryBlock.None:
                _task = _manager.NextTerritoryClearing(_territory);
                _label.text = $"{_territory.DisplayName} 개간 {_manager.TerritoryClearingsDone(_territory) + 1}/{_territory.Clearings.Count}";
                Show(_task != null ? Mode.Clear : Mode.Hidden);
                break;
            case TerritoryBlock.Mission:
                _label.text = "영토 확장 미션";
                Show(Mode.Mission);
                break;
            default:
                Show(Mode.Hidden);
                break;
        }
    }

    private void Show(Mode mode)
    {
        _mode = mode;
        bool visible = mode != Mode.Hidden;
        bool marker = mode == Mode.Clear || mode == Mode.Mission;
        _ring.gameObject.SetActive(visible);
        _marker.gameObject.SetActive(marker);
        _label.gameObject.SetActive(marker);
        _tapArea.enabled = marker;
        if (_hint != null)
            _hint.Allowed = mode == Mode.Clear;
        if (marker)
            _marker.sprite = mode == Mode.Mission && _missionSprite != null ? _missionSprite : _clearSprite;
        _progressBubble.SetActive(mode == Mode.Working);
        _lastSeconds = -1;
    }

    private void UpdateProgress()
    {
        var job = _task != null ? _manager.GetTaskJob(_task) : null;
        if (job == null)
            return;
        int seconds = Mathf.CeilToInt((float)job.Remaining(SettlementManager.NowTicks).TotalSeconds);
        if (seconds == _lastSeconds)
            return;
        _lastSeconds = seconds;
        _progressText.text = $"{_task.Title}\n{SettlementManager.FormatShort(System.TimeSpan.FromSeconds(seconds))}";
    }
}
