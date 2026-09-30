using System;
using UnityEngine;

/// <summary>
/// 꾸미기 모드에서 지금 들고 있는 물건 하나 (보관함에서 새로 꺼낸 것 또는 놓여 있던 것).
/// 확인하기 전까지는 격자를 바꾸지 않고, 자리·방향만 기억한다 (순수 C#).
/// </summary>
public class DecorEditSession
{
    public DecorDefinition Decor { get; }

    /// <summary>놓여 있던 물건을 옮기는 중이면 그 InstanceId, 새로 꺼낸 것이면 0</summary>
    public int InstanceId { get; }

    public Vector2Int Origin { get; private set; }
    public DecorRotation Rotation { get; private set; }

    /// <summary>옮기기 전 자리 (새로 꺼낸 것이면 의미 없음)</summary>
    public Vector2Int StartOrigin { get; }
    public DecorRotation StartRotation { get; }

    public bool IsNew => InstanceId == 0;
    public Vector2Int Size => Rotation.Apply(Decor.Footprint);
    public RectInt Area => new RectInt(Origin, Size);

    /// <summary>회전 버튼을 보일지 (정사각형 물건도 그림이 돌아가므로 보임)</summary>
    public bool CanRotate => Decor.CanRotate;

    private DecorEditSession(DecorDefinition decor, int instanceId, Vector2Int origin, DecorRotation rotation)
    {
        Decor = decor ?? throw new ArgumentNullException(nameof(decor));
        InstanceId = instanceId;
        Origin = origin;
        Rotation = rotation;
        StartOrigin = origin;
        StartRotation = rotation;
    }

    /// <summary>보관함에서 새로 꺼냄</summary>
    public static DecorEditSession ForNew(DecorDefinition decor, Vector2Int origin)
    {
        return new DecorEditSession(decor, 0, origin, DecorRotation.R0);
    }

    /// <summary>놓여 있던 물건을 들어 올림</summary>
    public static DecorEditSession ForPlaced(PlacedDecor placed)
    {
        if (placed == null)
            throw new ArgumentNullException(nameof(placed));

        return new DecorEditSession(placed.Decor, placed.InstanceId, placed.Origin, placed.Rotation);
    }

    public void MoveTo(Vector2Int origin) => Origin = origin;

    /// <summary>시계 방향으로 90도 (회전할 수 없는 물건이면 그대로)</summary>
    public void Rotate()
    {
        if (Decor.CanRotate)
            Rotation = Rotation.Next();
    }

    /// <summary>지금 자리에 놓을 수 있는지 (옮기는 중이면 자기 칸은 비어 있는 것으로 봄)</summary>
    public DecorPlacementResult Check(DecorLayout layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        return layout.Check(Decor, Origin, Rotation, InstanceId);
    }

    /// <summary>
    /// center에서 가장 가까운(체스판 거리 기준 고리 순서로) 놓을 수 있는 자리. 격자 전체에 없으면 false.
    /// 보관함에서 꺼낼 때 화면 가운데 근처 빈자리에 미리보기를 두기 위함
    /// </summary>
    public static bool TryFindFreeNear(DecorLayout layout, DecorDefinition decor, Vector2Int center, DecorRotation rotation, out Vector2Int origin)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (decor == null) throw new ArgumentNullException(nameof(decor));

        var size = rotation.Apply(decor.Footprint);
        // 물건의 가운데가 center에 오도록 왼쪽 아래 칸을 맞춤
        var start = center - new Vector2Int(size.x / 2, size.y / 2);
        int maxRing = Mathf.Max(layout.Size.x, layout.Size.y);

        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring)
                        continue;

                    var candidate = start + new Vector2Int(dx, dy);
                    if (layout.Check(decor, candidate, rotation) == DecorPlacementResult.Ok)
                    {
                        origin = candidate;
                        return true;
                    }
                }
            }
        }

        origin = start;
        return false;
    }
}
