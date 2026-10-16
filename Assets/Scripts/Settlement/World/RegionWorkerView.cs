using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 광장 밖 작업 현장(광산 정비 등)에 온 주민 해달 한 마리. 광장 프리팹을 그대로 쓰되 광장용 움직임(OtterWanderAgent)과
/// 광장 깊이 정렬(OtterVisualController)은 끄고, 여기서 직접 걷게 하고 장소의 정렬 구간(GroundDepthSort)으로 앞뒤를 정한다.
/// 걸어와서 → 일하고(망치 말풍선, 몸짓) → 끝나면 걸어 나가 사라진다 (광장으로 돌아감). RegionTaskSiteView가 만든다.
/// </summary>
public class RegionWorkerView : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int WalkDirHash = Animator.StringToHash("WalkDir");
    private static readonly string[] WorkTriggerNames = { "Squat", "Net", "Stretch", "Paint", "Eat", "Happy", "Wave" };
    private const float ArriveDistance = 0.03f;
    private const float WorkGapSeconds = 0.5f;

    private enum Phase { Walk, Work, Leave }

    private Animator _animator;
    private SpriteRenderer _body;
    private SortingGroup _group;
    private SettlementOtterView _view;
    private int _depthBase;
    private float _speed;
    private Vector2 _target;
    private Vector2 _lookPoint;
    private Phase _phase;
    private bool _hasIsMoving;
    private bool _hasWalkDir;
    private readonly System.Collections.Generic.List<int> _workTriggers = new System.Collections.Generic.List<int>();
    private float _workTimer;

    public string OtterId { get; private set; }
    public bool IsLeaving => _phase == Phase.Leave;

    /// <summary>주민 해달을 현장에 내보낸다</summary>
    /// <param name="scale">이 장소에서의 크기 (광장보다 작게 그린 장소)</param>
    /// <param name="height">발밑에서 머리 위까지 (이 장소의 월드 단위, 말풍선 위치)</param>
    /// <param name="depthBase">이 장소의 앞뒤 정렬 기준 (GroundDepthSort)</param>
    public static RegionWorkerView Spawn(SettlementOtterDefinition otter, Transform parent, Vector2 position, float scale, float height,
        int depthBase, float speed, Sprite speechBubble, Sprite alertBubble, Sprite hammerBubble, TMP_FontAsset font)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (otter.PlazaPrefab == null)
            throw new ArgumentException($"'{otter.name}'의 광장 프리팹이 없습니다.", nameof(otter));

        var instance = Instantiate(otter.PlazaPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity, parent);
        instance.name = $"Worker_{otter.OtterId}";
        instance.transform.localScale = Vector3.one * scale;
        // 광장용 움직임·정렬은 끔 (여기서 직접 함)
        foreach (var behaviour in instance.GetComponents<MonoBehaviour>())
        {
            if (behaviour is OtterWanderAgent || behaviour is OtterVisualController)
                behaviour.enabled = false;
        }

        var worker = instance.AddComponent<RegionWorkerView>();
        worker.OtterId = otter.OtterId;
        worker._animator = instance.GetComponentInChildren<Animator>();
        worker._body = instance.GetComponentInChildren<SpriteRenderer>();
        worker._group = instance.GetComponent<SortingGroup>();
        worker._depthBase = depthBase;
        worker._speed = speed;
        worker._target = position;
        worker._lookPoint = position;
        worker.ReadAnimatorParameters();
        worker._view = instance.AddComponent<SettlementOtterView>();
        worker._view.Init(otter, height, speechBubble, alertBubble, hammerBubble, font);
        worker.ApplySorting();
        return worker;
    }

    /// <summary>일할 자리로 걸어가 일한다 (이미 그 자리면 바로 일함)</summary>
    public void WorkAt(Vector2 standPoint, Vector2 lookPoint)
    {
        _target = standPoint;
        _lookPoint = lookPoint;
        _phase = Phase.Walk;
    }

    /// <summary>나가는 곳으로 걸어가 사라진다</summary>
    public void Leave(Vector2 exitPoint)
    {
        _target = exitPoint;
        _phase = Phase.Leave;
        _view.SetWorking(false);
    }

    private void ReadAnimatorParameters()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;
        foreach (var p in _animator.parameters)
        {
            if (p.nameHash == IsMovingHash && p.type == AnimatorControllerParameterType.Bool)
                _hasIsMoving = true;
            else if (p.nameHash == WalkDirHash && p.type == AnimatorControllerParameterType.Int)
                _hasWalkDir = true;
            else if (p.type == AnimatorControllerParameterType.Trigger && Array.IndexOf(WorkTriggerNames, p.name) >= 0)
                _workTriggers.Add(p.nameHash);
        }
    }

    private void Update()
    {
        Vector2 position = transform.position;
        Vector2 toTarget = _target - position;
        bool walking = _phase != Phase.Work && toTarget.magnitude > ArriveDistance;
        if (walking)
        {
            Vector2 step = Vector2.ClampMagnitude(toTarget, _speed * Time.deltaTime);
            transform.position = new Vector3(position.x + step.x, position.y + step.y, transform.position.z);
            Face(toTarget.x);
            SetWalkDir(toTarget);
        }
        else if (_phase == Phase.Leave)
        {
            Destroy(gameObject);
            return;
        }
        else if (_phase == Phase.Walk)
        {
            _phase = Phase.Work;
            _view.SetWorking(true);
            _workTimer = WorkGapSeconds;
        }

        if (_hasIsMoving)
            _animator.SetBool(IsMovingHash, walking);
        if (_phase == Phase.Work)
            UpdateWork();
        ApplySorting();
    }

    // 일하는 곳을 바라보고 몸짓을 번갈아 (쪼그려 앉기·뻗기 등)
    private void UpdateWork()
    {
        Face(_lookPoint.x - transform.position.x);
        if (_workTriggers.Count == 0)
            return;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Idle") || _animator.IsInTransition(0))
        {
            _workTimer = WorkGapSeconds;
            return;
        }
        _workTimer -= Time.deltaTime;
        if (_workTimer > 0f)
            return;
        _animator.SetTrigger(_workTriggers[UnityEngine.Random.Range(0, _workTriggers.Count)]);
        _workTimer = WorkGapSeconds;
    }

    // 광장 해달 그림은 오른쪽을 봄 (OtterVisualController 기본값)
    private void Face(float dx)
    {
        if (_body != null && Mathf.Abs(dx) > 0.01f)
            _body.flipX = dx < 0f;
    }

    // 세로로 많이 움직이면 위/아래 걷기 그림 (0 옆, 1 아래, 2 위 — FarmerOtterSpriteSetup과 같음)
    private void SetWalkDir(Vector2 direction)
    {
        if (!_hasWalkDir)
            return;
        int dir = Mathf.Abs(direction.y) > Mathf.Abs(direction.x) * 1.3f ? (direction.y < 0f ? 1 : 2) : 0;
        _animator.SetInteger(WalkDirHash, dir);
    }

    private void ApplySorting()
    {
        int order = GroundDepthSort.OrderFor(_depthBase, transform.position.y);
        if (_group != null)
            _group.sortingOrder = order;
        else if (_body != null)
            _body.sortingOrder = order;
    }
}
