using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장의 건설 해달 (광부 해달 그림·걷기·곡괭이질을 그대로 씀).
/// 평소엔 광장을 거닐고, 건설이 시작되면 공사 현장의 설 자리로 걸어가 곡괭이질을 하며 망치 말풍선을 띄운다.
/// 건설이 끝나면 다시 거닌다. 이 오브젝트의 위치 = 발밑.
/// </summary>
[RequireComponent(typeof(SpriteFrameAnimator))]
public class BuilderOtterController : MonoBehaviour, IPlazaCrowdMember
{
    // 클립 이름 — SettlementSetup이 SpriteFrameAnimator 클립을 이 이름으로 만든다
    public const string ClipIdle = "Idle";
    public const string ClipWalkRight = "WalkRight";
    public const string ClipWalkLeft = "WalkLeft";
    public const string ClipWalkDown = "WalkDown";
    public const string ClipWalkUp = "WalkUp";
    public const string ClipWork = "Work";

    private enum Phase { Idle, Walk, ToWork, Work }

    // 예상보다 이만큼 오래 걸으면 막힌 것으로 봄
    private const float StuckTimeMultiplier = 2f;
    private const float StuckGraceSeconds = 1f;
    private const float ArriveDistance = 0.05f;
    // 다른 해달과 이만큼 가까이 멈춰 있으면 비켜 섬 / 목적지는 이만큼 떨어진 곳만
    private const float OverlapDistance = 1f;
    private const float FreeSpacing = 1.4f;
    private const float OverlapRetrySeconds = 1.5f;
    private float _nextOverlapCheck;

    [Header("걷기")]
    [Tooltip("초당 월드 단위")]
    [SerializeField] private float _walkSpeed = 1.2f;
    [Tooltip("공사 현장으로 갈 때 걷는 속도 배수 (공사 시간을 걷기로 다 쓰지 않게)")]
    [SerializeField] private float _hurryMultiplier = 2.2f;
    [SerializeField] private Vector2 _idleSeconds = new Vector2(2f, 5f);
    [Tooltip("거닐 때 다음 목적지까지 거리 (월드 단위)")]
    [SerializeField] private Vector2 _wanderDistance = new Vector2(1.5f, 4f);
    [SerializeField] private int _maxDestinationTries = 10;

    [Header("작업")]
    [Tooltip("일할 때 머리 위 망치 말풍선")]
    [SerializeField] private SpriteRenderer _workBubble;

    [Tooltip("왼쪽 걷기 그림 대신 오른쪽 걷기를 좌우로 뒤집어 씀 (왼쪽 걷기 그림이 따로 없는 모습)")]
    [SerializeField] private bool _mirrorLeftWalk;

    private SpriteFrameAnimator _animator;
    private SpriteRenderer _renderer;
    private PlazaWalkableArea _area;

    private Phase _phase = Phase.Idle;
    private float _timer;
    private readonly List<Vector2> _path = new List<Vector2>();
    private int _pathIndex;
    private float _walkTimer;
    private float _walkTimeLimit;
    private int _pathVersion;
    private ConstructionSiteView _site;

    public bool IsWorking => _phase == Phase.Work;

    public Vector2 Position => transform.position;
    public Vector2 Goal => (_phase == Phase.Walk || _phase == Phase.ToWork) && _path.Count > 0 ? _path[_path.Count - 1] : Position;

    private void OnEnable() => PlazaCrowd.Register(this);

    private void OnDisable() => PlazaCrowd.Unregister(this);

    private void Awake()
    {
        _animator = GetComponent<SpriteFrameAnimator>();
        _renderer = GetComponent<SpriteRenderer>();
        _workBubble.gameObject.SetActive(false);
    }

    public void Initialize(PlazaWalkableArea area)
    {
        _area = area;
        EnterIdle(Random.Range(0.5f, 1.5f));
    }

    /// <summary>일할 현장 (null이면 일을 멈추고 거닒)</summary>
    public void SetWorkSite(ConstructionSiteView site)
    {
        if (_site == site)
            return;
        _site = site;
        if (_area == null)
            return;

        if (site != null)
            StartWalkToWork();
        else
            EnterIdle(Random.Range(0.5f, 1.5f));
    }

    private void Update()
    {
        if (_area == null)
            return;

        switch (_phase)
        {
            case Phase.Idle:
                _timer -= Time.deltaTime;
                if (_timer > 0f && Time.time >= _nextOverlapCheck && PlazaCrowd.IsOverlapping(this, OverlapDistance))
                {
                    _timer = 0f;
                    _nextOverlapCheck = Time.time + OverlapRetrySeconds;
                }
                if (_timer <= 0f && !TryStartWander())
                    EnterIdle(RandomIn(_idleSeconds));
                break;

            case Phase.Walk:
            case Phase.ToWork:
                UpdateWalk();
                break;

            case Phase.Work:
                FaceToward(_site.LookPoint);
                break;
        }

        _renderer.sortingOrder = PlazaDepth.SortingOrderFor(transform.position.y);
        _workBubble.sortingOrder = _renderer.sortingOrder + 1;
    }

    #region 상태

    private void EnterIdle(float seconds)
    {
        _phase = Phase.Idle;
        _timer = seconds;
        _path.Clear();
        _workBubble.gameObject.SetActive(false);
        _animator.Play(ClipIdle);
    }

    private bool TryStartWander()
    {
        if (_site != null)
        {
            StartWalkToWork();
            return true;
        }

        Vector2 from = transform.position;
        for (int i = 0; i < _maxDestinationTries; i++)
        {
            float min = Mathf.Min(_wanderDistance.x, _wanderDistance.y);
            float max = Mathf.Max(_wanderDistance.x, _wanderDistance.y);
            if (_area.TryPickDestination(from, min, max, out Vector2 destination)
                && PlazaCrowd.IsFree(destination, FreeSpacing, this)
                && _area.TryFindPath(from, destination, _path) && _path.Count > 0)
            {
                BeginWalk(Phase.Walk, from);
                return true;
            }
        }
        return false;
    }

    private void StartWalkToWork()
    {
        Vector2 from = transform.position;
        Vector2 stand = _site.StandPoint;
        if ((stand - from).sqrMagnitude <= ArriveDistance * ArriveDistance)
        {
            EnterWork();
            return;
        }

        if (_area.TryFindPath(from, stand, _path) && _path.Count > 0)
        {
            BeginWalk(Phase.ToWork, from);
            return;
        }

        // 길이 없으면 (현장이 걷기 영역 밖 등) 그 자리로 옮겨서라도 일한다 — 진행이 막히지 않게
        transform.position = new Vector3(stand.x, stand.y, transform.position.z);
        EnterWork();
    }

    private void EnterWork()
    {
        _phase = Phase.Work;
        _path.Clear();
        _workBubble.gameObject.SetActive(true);
        _animator.Play(ClipWork);
        FaceToward(_site.LookPoint);
    }

    #endregion

    #region 걷기

    private void BeginWalk(Phase phase, Vector2 from)
    {
        _phase = phase;
        _pathIndex = 0;
        _walkTimer = 0f;
        _pathVersion = _area.Version;
        _walkTimeLimit = PathLength(from) / Mathf.Max(CurrentSpeed, 0.01f) * StuckTimeMultiplier + StuckGraceSeconds;
        _workBubble.gameObject.SetActive(false);
        PlayWalkClip(_path[0] - from);
    }

    private void UpdateWalk()
    {
        _walkTimer += Time.deltaTime;
        bool stuck = _walkTimer > _walkTimeLimit;
        bool replan = _area.Version != _pathVersion;
        if (stuck || replan)
        {
            if (_phase == Phase.ToWork && _site != null)
            {
                if (stuck)
                {
                    // 계속 막히면 현장으로 옮겨서 일함
                    Vector2 stand = _site.StandPoint;
                    transform.position = new Vector3(stand.x, stand.y, transform.position.z);
                    EnterWork();
                }
                else
                {
                    StartWalkToWork();
                }
            }
            else
            {
                EnterIdle(0.3f);
            }
            return;
        }

        float remaining = CurrentSpeed * Time.deltaTime;
        Vector2 position = transform.position;
        while (remaining > 0f && _pathIndex < _path.Count)
        {
            Vector2 target = _path[_pathIndex];
            Vector2 toTarget = target - position;
            float distance = toTarget.magnitude;
            if (distance <= remaining)
            {
                position = target;
                remaining -= distance;
                _pathIndex++;
                if (_pathIndex < _path.Count)
                    PlayWalkClip(_path[_pathIndex] - position);
            }
            else
            {
                position += toTarget / distance * remaining;
                remaining = 0f;
            }
        }
        transform.position = new Vector3(position.x, position.y, transform.position.z);

        if (_pathIndex >= _path.Count)
        {
            if (_phase == Phase.ToWork && _site != null)
                EnterWork();
            else
                EnterIdle(RandomIn(_idleSeconds));
        }
    }

    private float CurrentSpeed => _phase == Phase.ToWork ? _walkSpeed * _hurryMultiplier : _walkSpeed;

    private void PlayWalkClip(Vector2 direction)
    {
        _renderer.flipX = false;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            bool left = direction.x < 0f;
            _renderer.flipX = left && _mirrorLeftWalk;
            _animator.Play(left && !_mirrorLeftWalk ? ClipWalkLeft : ClipWalkRight);
        }
        else
            _animator.Play(direction.y < 0f ? ClipWalkDown : ClipWalkUp);
    }

    // 일하는 그림은 오른쪽을 보므로 왼쪽이면 뒤집음
    private void FaceToward(Vector2 point)
    {
        _renderer.flipX = point.x < transform.position.x;
    }

    private float PathLength(Vector2 from)
    {
        float length = 0f;
        Vector2 prev = from;
        foreach (var p in _path)
        {
            length += Vector2.Distance(prev, p);
            prev = p;
        }
        return length;
    }

    private static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    #endregion
}
