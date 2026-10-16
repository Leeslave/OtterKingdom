using System.Collections.Generic;
using UnityEngine;
using Action = System.Action;

/// <summary>
/// 광장의 건설 해달 (광부 해달 그림·걷기·곡괭이질을 그대로 씀).
/// 평소엔 광장을 거닐고, 건설이 시작되면 공사 현장의 설 자리로 걸어가 곡괭이질을 하며 망치 말풍선을 띄운다.
/// 건설이 끝나면 다시 거닌다. 이 오브젝트의 위치 = 발밑.
/// 구역 건물 이사 때는 옛 자리에서 건물을 머리에 이고 새 자리까지 걸어가 내려놓은 뒤 거기서 일한다.
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
    // 이고 가는 건물: 걸음마다 통통, 들어 올릴 때 쑥 커짐
    private const float CarryBobPerSecond = 9f;
    private const float CarryBobHeight = 0.08f;
    private const float CarrySwayDegrees = 3f;
    private const float CarryPopSeconds = 0.3f;

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

    [Header("이사")]
    [Tooltip("건물을 이고 갈 때 걷는 속도 배수")]
    [SerializeField] private float _carrySpeedMultiplier = 1.4f;
    [Tooltip("머리 위에 이는 건물 그림의 가로 크기 (월드 단위)")]
    [SerializeField] private float _carriedWidth = 1.6f;
    [Tooltip("이는 건물 그림 아랫변의 높이 (발밑 기준, 월드 단위)")]
    [SerializeField] private float _carriedHeight = 1.45f;

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
    // 일하는 곳: 공사 현장 또는 이사하는 건물 (_workKey가 같으면 다시 걷지 않음)
    private object _workKey;
    private bool _hasWork;
    private Vector2 _stand;
    private Vector2 _look;
    // 이사: 먼저 _pickup에서 건물을 들고, 이고 간 채로 _stand까지
    private bool _hasPickup;
    private Vector2 _pickup;
    private bool _carrying;
    private float _carryTime;
    private Sprite _carriedSprite;
    private Action _onPickup;
    private Action _onDrop;
    private SpriteRenderer _carried;
    private Vector3 _carriedBase;
    private float _carriedScale;

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
        if (site != null)
            SetWork(site, site.StandPoint, site.LookPoint);
        else
            ClearWork();
    }

    /// <summary>현장 말고 다른 곳에서 일함 (예: 이사하는 건물 앞). key가 같으면 이미 가는 중</summary>
    public void SetWork(object key, Vector2 stand, Vector2 look)
    {
        if (_hasWork && Equals(_workKey, key))
            return;
        AbortCarry();
        AssignWork(key, stand, look);
    }

    /// <summary>
    /// 건물을 옮겨 줌: pickup에서 건물(carried)을 머리에 이고 stand까지 걸어가 내려놓은 뒤 거기서 일함. key가 같으면 이미 하는 중.
    /// 들 때 onPickup, 내려놓을 때 onDrop (도중에 다른 일이 생겨도 둘 다 꼭 부름)
    /// </summary>
    public void SetCarryWork(object key, Vector2 pickup, Vector2 stand, Vector2 look, Sprite carried, Action onPickup, Action onDrop)
    {
        // 같은 일로 막 새 자리에 가던 중이면(이사 시작 알림이 먼저 와서) 옛 자리에 들르도록 바꿈
        if (_hasWork && Equals(_workKey, key) && (_hasPickup || _carrying))
            return;
        AbortCarry();
        _hasPickup = true;
        _pickup = pickup;
        _carriedSprite = carried;
        _onPickup = onPickup;
        _onDrop = onDrop;
        AssignWork(key, stand, look);
    }

    private void AssignWork(object key, Vector2 stand, Vector2 look)
    {
        _workKey = key;
        _hasWork = true;
        _stand = stand;
        _look = look;
        if (_area != null)
            StartWalkToWork();
    }

    public void ClearWork()
    {
        if (!_hasWork)
            return;
        AbortCarry();
        _hasWork = false;
        _workKey = null;
        if (_area != null)
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
                FaceToward(_look);
                break;
        }

        _renderer.sortingOrder = PlazaDepth.SortingOrderFor(transform.position.y);
        _workBubble.sortingOrder = _renderer.sortingOrder + 1;
        if (_carrying)
            AnimateCarried();
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
        if (_hasWork)
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

    // 지금 걸어갈 곳: 들 건물이 있으면 옛 자리, 아니면 일할 자리
    private Vector2 WorkTarget => _hasPickup ? _pickup : _stand;

    private void StartWalkToWork()
    {
        Vector2 from = transform.position;
        Vector2 target = WorkTarget;
        if ((target - from).sqrMagnitude <= ArriveDistance * ArriveDistance)
        {
            ArriveAtWorkTarget();
            return;
        }

        if (_area.TryFindPath(from, target, _path) && _path.Count > 0)
        {
            BeginWalk(Phase.ToWork, from);
            return;
        }

        // 길이 없으면 (현장이 걷기 영역 밖 등) 그 자리로 옮겨서라도 일한다 — 진행이 막히지 않게
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        ArriveAtWorkTarget();
    }

    // 옛 자리에 닿으면 건물을 들고 새 자리로, 새 자리에 닿으면 내려놓고 일함
    private void ArriveAtWorkTarget()
    {
        if (_hasPickup)
        {
            PickUp();
            StartWalkToWork();
            return;
        }
        if (_carrying)
            Drop();
        EnterWork();
    }

    private void EnterWork()
    {
        _phase = Phase.Work;
        _path.Clear();
        _workBubble.gameObject.SetActive(true);
        _animator.Play(ClipWork);
        FaceToward(_look);
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
            if (_phase == Phase.ToWork && _hasWork)
            {
                if (stuck)
                {
                    // 계속 막히면 가려던 곳으로 옮김
                    Vector2 target = WorkTarget;
                    transform.position = new Vector3(target.x, target.y, transform.position.z);
                    ArriveAtWorkTarget();
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
            if (_phase == Phase.ToWork && _hasWork)
                ArriveAtWorkTarget();
            else
                EnterIdle(RandomIn(_idleSeconds));
        }
    }

    private float CurrentSpeed => _phase != Phase.ToWork ? _walkSpeed
        : _walkSpeed * (_carrying ? _carrySpeedMultiplier : _hurryMultiplier);

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

    #endregion

    #region 이사 (건물 이고 가기)

    private void PickUp()
    {
        _hasPickup = false;
        _carrying = true;
        _carryTime = 0f;
        ShowCarried();
        var callback = _onPickup;
        _onPickup = null;
        callback?.Invoke();
    }

    private void Drop()
    {
        _carrying = false;
        if (_carried != null)
            _carried.gameObject.SetActive(false);
        var callback = _onDrop;
        _onDrop = null;
        callback?.Invoke();
    }

    // 들고 가던 일을 그만두면 건물을 바로 새 자리에 내려놓음 (숨긴 그림이 남지 않게)
    private void AbortCarry()
    {
        if (_hasPickup)
            PickUp();
        if (_carrying)
            Drop();
    }

    private void ShowCarried()
    {
        if (_carried == null)
        {
            _carried = new GameObject("Carried").AddComponent<SpriteRenderer>();
            _carried.transform.SetParent(transform, false);
        }
        _carried.gameObject.SetActive(_carriedSprite != null);
        if (_carriedSprite == null)
            return;

        // 그림의 가로를 _carriedWidth에 맞추고, 아랫변 가운데를 머리 위(_carriedHeight)에
        _carried.sprite = _carriedSprite;
        var parentScale = transform.lossyScale;
        var bounds = _carriedSprite.bounds;
        _carriedScale = _carriedWidth / Mathf.Max(bounds.size.x, 0.01f) / Mathf.Max(parentScale.x, 0.01f);
        _carriedBase = new Vector3(-bounds.center.x * _carriedScale,
            _carriedHeight / Mathf.Max(parentScale.y, 0.01f) - bounds.min.y * _carriedScale, 0f);
        AnimateCarried();
    }

    private void AnimateCarried()
    {
        if (_carried == null || !_carried.gameObject.activeSelf)
            return;
        _carryTime += Time.deltaTime;
        bool walking = _phase == Phase.ToWork;
        float bob = walking ? Mathf.Abs(Mathf.Sin(_carryTime * CarryBobPerSecond)) * CarryBobHeight : 0f;
        float sway = walking ? Mathf.Sin(_carryTime * CarryBobPerSecond * 0.5f) * CarrySwayDegrees : 0f;
        // 들어 올릴 때 작게 시작해 살짝 넘쳤다가 제 크기
        float t = Mathf.Clamp01(_carryTime / CarryPopSeconds);
        float pop = t < 1f ? Mathf.Lerp(0.4f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.15f : 1f;
        _carried.transform.localPosition = _carriedBase + Vector3.up * bob;
        _carried.transform.localRotation = Quaternion.Euler(0f, 0f, sway);
        _carried.transform.localScale = Vector3.one * (_carriedScale * pop);
        _carried.sortingOrder = _renderer.sortingOrder + 2;
    }

    #endregion

    #region 도움

    private static float RandomIn(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    #endregion
}
