using UnityEngine;

/// <summary>물건 방향 (시계 방향 90도씩). 90·270도면 차지하는 칸의 가로·세로가 바뀐다.</summary>
public enum DecorRotation
{
    R0,
    R90,
    R180,
    R270,
}

/// <summary>놓기(또는 옮기기) 검사 결과. Ok가 아니면 이유별로 안내 문구를 다르게 보여줄 수 있다.</summary>
public enum DecorPlacementResult
{
    Ok,
    OutOfBounds, // 격자 밖으로 나감
    Unavailable, // 어느 구역에도 속하지 않거나 나무·집 등으로 막힌 칸
    Locked,      // 아직 열리지 않은 구역
    Occupied,    // 다른 물건이 있음
    NotOwned,    // 보관함에 남은 개수가 없음
}

/// <summary>칸 하나의 상태 (격자 표시용)</summary>
public enum DecorCellState
{
    Unavailable,
    Locked,
    Free,
    Occupied,
}

public static class DecorRotationExtensions
{
    /// <summary>다음 방향 (시계 방향 90도)</summary>
    public static DecorRotation Next(this DecorRotation rotation) => (DecorRotation)(((int)rotation + 1) % 4);

    /// <summary>이 방향으로 놓았을 때 차지하는 칸 수</summary>
    public static Vector2Int Apply(this DecorRotation rotation, Vector2Int footprint)
    {
        return rotation == DecorRotation.R90 || rotation == DecorRotation.R270
            ? new Vector2Int(footprint.y, footprint.x)
            : footprint;
    }
}

/// <summary>광장에 놓인 물건 하나. 같은 물건을 여러 개 놓아도 InstanceId로 구분한다.</summary>
public class PlacedDecor
{
    public int InstanceId { get; }
    public DecorDefinition Decor { get; }
    public Vector2Int Origin { get; internal set; }
    public DecorRotation Rotation { get; internal set; }

    /// <summary>차지하는 칸 수 (방향 적용)</summary>
    public Vector2Int Size => Rotation.Apply(Decor.Footprint);

    /// <summary>차지하는 칸 영역 (Origin = 왼쪽 아래 칸)</summary>
    public RectInt Area => new RectInt(Origin, Size);

    public PlacedDecor(int instanceId, DecorDefinition decor, Vector2Int origin, DecorRotation rotation)
    {
        InstanceId = instanceId;
        Decor = decor;
        Origin = origin;
        Rotation = rotation;
    }
}
