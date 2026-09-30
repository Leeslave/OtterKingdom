using UnityEngine;

/// <summary>
/// 해달 한 마리가 물건 하나를 가지고 놀기로 예약한 것. 끝나면(또는 중간에 그만두면) 반드시 Release한다.
/// 물건이 치워지면 IsValid가 false가 되니, 해달은 놀이를 그만두면 된다.
/// </summary>
public class DecorPlaySession
{
    private bool _released;

    public PlacedDecorView Toy { get; }

    /// <summary>해달이 서서 놀 자리 (발 위치)</summary>
    public Vector2 StandPoint { get; }

    /// <summary>한 번 놀 시간 (초)</summary>
    public float Seconds { get; }

    public bool IsValid => !_released && Toy != null && Toy.isActiveAndEnabled;

    /// <summary>해달이 바라볼 곳 (물건 가운데)</summary>
    public Vector2 LookPoint => Toy != null ? (Vector2)Toy.WorldRect.center : StandPoint;

    internal DecorPlaySession(PlacedDecorView toy, Vector2 standPoint, float seconds)
    {
        Toy = toy;
        StandPoint = standPoint;
        Seconds = seconds;
    }

    /// <summary>도착해서 놀기 시작: 물건이 반응한다</summary>
    public void Begin()
    {
        if (IsValid)
            Toy.PlayReaction(Seconds);
    }

    /// <summary>예약 해제 (여러 번 불러도 됨)</summary>
    public void Release()
    {
        if (_released)
            return;

        _released = true;
        if (Toy != null)
            Toy.Players = Mathf.Max(0, Toy.Players - 1);
    }
}
