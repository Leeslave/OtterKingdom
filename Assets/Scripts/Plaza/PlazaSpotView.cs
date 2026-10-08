using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>해달이 그 자리에서 하는 일</summary>
public enum PlazaSpotKind
{
    Sit,    // 의자·벤치: 앞까지 걸어가 폴짝 올라앉아 쉼
    Eat,    // 식탁: 둘레 자리에 서서 먹음
    Gather, // 가로등: 밤이 되면 둘레에 모여 이야기함
}

/// <summary>
/// 광장 해달이 찾아가 머무는 자리 묶음 (의자·벤치·식탁·가로등). 켜져 있는 동안만 쓰인다 (지어지기 전 DevelopmentGate로 꺼진 것은 안 쓰임).
/// 해달(OtterWanderAgent)이 다음 행동을 고를 때 가끔 빈자리 하나를 예약해 걸어가 머물고, 끝나면 자리를 돌려준다.
/// 자리는 이 오브젝트의 자식 Transform이라 건물을 옮기거나 뒤집어도 따라간다.
/// </summary>
[DisallowMultipleComponent]
public class PlazaSpotView : MonoBehaviour
{
    // 해달이 다음 행동으로 자리에 갈 확률 (장난감 놀이를 고르지 않았을 때)
    private const float VisitChance = 0.3f;
    // 가로등 모임이 열리는 밤의 깊이 (TimeOfDayLighting.Night)
    private const float GatherNight = 0.35f;

    private static readonly List<PlazaSpotView> Opened = new List<PlazaSpotView>();
    private static readonly string[] SitActions = { "Yawn", "Sleep", "Happy", "Wave", "Stretch", "Write", "Paint" };
    private static readonly string[] EatActions = { "Eat", "Happy" };
    private static readonly string[] GatherActions = { "Wave", "Happy", "Stretch", "Yawn" };

    [SerializeField] private PlazaSpotKind _kind;

    [Tooltip("앉는 · 서는 자리 (발 위치). 앉는 자리는 의자 위")]
    [SerializeField] private List<Transform> _seats = new List<Transform>();

    [Tooltip("해달이 바라볼 곳 (식탁 · 가로등 가운데). 비우면 이 오브젝트")]
    [SerializeField] private Transform _lookPoint;

    [Tooltip("앉는 자리: 이 오브젝트 기준 이 높이(로컬 y)까지 걸어와서 올라앉음 (의자 앞 바닥)")]
    [SerializeField] private float _approachLocalY = -0.45f;

    [Tooltip("머무는 시간 (초, 최소~최대)")]
    [SerializeField] private Vector2 _seconds = new Vector2(8f, 16f);

    private readonly List<bool> _taken = new List<bool>();

    public PlazaSpotKind Kind => _kind;

    /// <summary>지금 쓸 수 있는지 (가로등은 밤에만, 식탁은 첫 모임 연출 중에는 안 됨)</summary>
    public bool IsOpen
    {
        get
        {
            if (!isActiveAndEnabled)
                return false;
            switch (_kind)
            {
                case PlazaSpotKind.Gather: return TimeOfDayLighting.Night >= GatherNight;
                case PlazaSpotKind.Eat: return !GatheringDirector.IsPlaying;
                default: return true;
            }
        }
    }

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(PlazaSpotKind kind, IEnumerable<Transform> seats, Transform lookPoint, float approachLocalY)
    {
        _kind = kind;
        _seats = new List<Transform>(seats);
        _lookPoint = lookPoint;
        _approachLocalY = approachLocalY;
        _taken.Clear();
    }

    private void OnEnable()
    {
        if (!Opened.Contains(this))
            Opened.Add(this);
    }

    private void OnDisable()
    {
        Opened.Remove(this);
        for (int i = 0; i < _taken.Count; i++)
            _taken[i] = false;
    }

    private bool IsTaken(int seat) => seat < _taken.Count && _taken[seat];

    private void SetTaken(int seat, bool taken)
    {
        while (_taken.Count <= seat)
            _taken.Add(false);
        _taken[seat] = taken;
    }

    private bool HasFreeSeat()
    {
        for (int i = 0; i < _seats.Count; i++)
        {
            if (_seats[i] != null && !IsTaken(i))
                return true;
        }
        return false;
    }

    /// <summary>해달이 다음 행동으로 자리에 갈지 (확률 + 빈자리가 있는지)</summary>
    public static bool RollWantsToVisit()
    {
        if (Opened.Count == 0 || UnityEngine.Random.value >= VisitChance)
            return false;
        foreach (var spot in Opened)
        {
            if (spot.IsOpen && spot.HasFreeSeat())
                return true;
        }
        return false;
    }

    /// <summary>
    /// 빈자리 하나(가까운 것일수록 잘 고름)를 예약한다. canStand: 해달이 걸어가 설 곳이 걷기 영역인지
    /// </summary>
    public static bool TryReserve(Vector2 from, Func<Vector2, bool> canStand, out PlazaSpotSession session)
    {
        if (canStand == null)
            throw new ArgumentNullException(nameof(canStand));
        session = null;
        var candidates = new List<PlazaSpotView>();
        foreach (var spot in Opened)
        {
            if (spot.IsOpen && spot.HasFreeSeat())
                candidates.Add(spot);
        }
        candidates.Sort((a, b) => Vector2.Distance(from, a.transform.position).CompareTo(Vector2.Distance(from, b.transform.position)));

        // 가까운 자리부터 보되, 늘 같은 곳만 가지 않게 앞쪽 몇 개 중에서 무작위로 시작
        int start = candidates.Count > 0 ? UnityEngine.Random.Range(0, Mathf.Min(3, candidates.Count)) : 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var spot = candidates[(start + i) % candidates.Count];
            if (spot.TryReserveSeat(canStand, out session))
                return true;
        }
        return false;
    }

    private bool TryReserveSeat(Func<Vector2, bool> canStand, out PlazaSpotSession session)
    {
        session = null;
        int count = _seats.Count;
        int first = count > 0 ? UnityEngine.Random.Range(0, count) : 0;
        for (int n = 0; n < count; n++)
        {
            int seat = (first + n) % count;
            if (_seats[seat] == null || IsTaken(seat))
                continue;
            Vector2 seatPoint = _seats[seat].position;
            Vector2 stand = StandPointFor(seat);
            if (!canStand(stand))
                continue;

            SetTaken(seat, true);
            float seconds = UnityEngine.Random.Range(Mathf.Min(_seconds.x, _seconds.y), Mathf.Max(_seconds.x, _seconds.y));
            bool sit = _kind == PlazaSpotKind.Sit;
            // 앉으면 의자보다 앞에 그림 (의자 발밑보다 조금 아래 높이로 정렬)
            float depthY = sit ? transform.position.y - 0.05f : seatPoint.y;
            // 앉으면 바라볼 곳 없이 그대로 (의자 가운데를 보면 옆으로 돌아앉음)
            session = new PlazaSpotSession(this, seat, stand, sit ? seatPoint : stand, sit ? seatPoint : LookPoint, seconds, sit, depthY, ActionsFor(_kind));
            return true;
        }
        return false;
    }

    // 걸어가 설 곳: 앉는 자리는 그 자리 바로 앞 바닥, 나머지는 자리 그대로
    private Vector2 StandPointFor(int seat)
    {
        Vector2 seatPoint = _seats[seat].position;
        if (_kind != PlazaSpotKind.Sit)
            return seatPoint;
        var local = transform.InverseTransformPoint(seatPoint);
        return transform.TransformPoint(new Vector3(local.x, _approachLocalY, 0f));
    }

    private Vector2 LookPoint => _lookPoint != null ? (Vector2)_lookPoint.position : (Vector2)transform.position;

    internal void Release(int seat) => SetTaken(seat, false);

    private static string[] ActionsFor(PlazaSpotKind kind)
    {
        switch (kind)
        {
            case PlazaSpotKind.Eat: return EatActions;
            case PlazaSpotKind.Gather: return GatherActions;
            default: return SitActions;
        }
    }
}

/// <summary>해달 한 마리가 자리 하나를 예약한 것. 끝나면(또는 중간에 그만두면) 반드시 Release한다</summary>
public class PlazaSpotSession
{
    private bool _released;

    public PlazaSpotView Spot { get; }
    public int Seat { get; }

    /// <summary>걸어가 설 곳 (앉는 자리는 의자 앞 바닥)</summary>
    public Vector2 StandPoint { get; }

    /// <summary>머무는 곳 (앉는 자리는 의자 위)</summary>
    public Vector2 SeatPoint { get; }

    public Vector2 LookPoint { get; }
    public float Seconds { get; }

    /// <summary>의자에 올라앉는지 (앉아 있는 동안은 걷기 영역 밖에 있음)</summary>
    public bool Hops { get; }

    /// <summary>머무는 동안 앞뒤 정렬에 쓸 높이</summary>
    public float DepthY { get; }

    /// <summary>머무는 동안 고를 동작 (해달마다 있는 것만)</summary>
    public IReadOnlyList<string> Actions { get; }

    public bool IsValid => !_released && Spot != null && Spot.IsOpen;

    internal PlazaSpotSession(PlazaSpotView spot, int seat, Vector2 standPoint, Vector2 seatPoint, Vector2 lookPoint, float seconds,
        bool hops, float depthY, IReadOnlyList<string> actions)
    {
        Spot = spot;
        Seat = seat;
        StandPoint = standPoint;
        SeatPoint = seatPoint;
        LookPoint = lookPoint;
        Seconds = seconds;
        Hops = hops;
        DepthY = depthY;
        Actions = actions;
    }

    /// <summary>예약 해제 (여러 번 불러도 됨)</summary>
    public void Release()
    {
        if (_released)
            return;
        _released = true;
        if (Spot != null)
            Spot.Release(Seat);
    }
}
