using System.Collections.Generic;
using UnityEngine;

/// <summary>광장에서 서로 겹치지 않으려는 해달 하나 (지금 위치와 가려는 곳)</summary>
public interface IPlazaCrowdMember
{
    Vector2 Position { get; }
    /// <summary>걷는 중이면 도착할 곳, 아니면 지금 위치</summary>
    Vector2 Goal { get; }
}

/// <summary>
/// 광장 해달들이 같은 자리에 서지 않게 하는 명단. 목적지를 고를 때 다른 해달이 있거나 가려는 곳 근처는 피하고,
/// 멈춰 있다가 다른 해달과 겹치면 비켜 선다 (OtterWanderAgent, BuilderOtterController가 씀).
/// 걷는 도중 스쳐 지나가는 것은 막지 않는다.
/// </summary>
public static class PlazaCrowd
{
    private static readonly List<IPlazaCrowdMember> Members = new List<IPlazaCrowdMember>();

    public static void Register(IPlazaCrowdMember member)
    {
        if (!Members.Contains(member))
            Members.Add(member);
    }

    public static void Unregister(IPlazaCrowdMember member) => Members.Remove(member);

    /// <summary>이 자리에 서도 되는지 (다른 해달의 위치·가려는 곳에서 spacing 이상 떨어짐)</summary>
    public static bool IsFree(Vector2 point, float spacing, IPlazaCrowdMember self)
    {
        float sqr = spacing * spacing;
        foreach (var other in Members)
        {
            if (other == self)
                continue;
            if ((other.Position - point).sqrMagnitude < sqr || (other.Goal - point).sqrMagnitude < sqr)
                return false;
        }
        return true;
    }

    /// <summary>지금 다른 해달과 겹쳐 서 있는지</summary>
    public static bool IsOverlapping(IPlazaCrowdMember self, float spacing)
    {
        float sqr = spacing * spacing;
        foreach (var other in Members)
        {
            if (other != self && (other.Position - self.Position).sqrMagnitude < sqr)
                return true;
        }
        return false;
    }
}
