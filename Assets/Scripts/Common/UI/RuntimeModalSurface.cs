using System.Collections.Generic;
using UnityEngine;

/// <summary>코드로 만든 딤이 떠 있는 동안 새 알림과 연출을 대기시킨다.</summary>
public class RuntimeModalSurface : MonoBehaviour
{
    private static readonly HashSet<RuntimeModalSurface> Open = new HashSet<RuntimeModalSurface>();
    public static bool AnyVisible
    {
        get
        {
            Open.RemoveWhere(surface => surface == null);
            foreach (var surface in Open)
                if (surface.isActiveAndEnabled) return true;
            return false;
        }
    }
    private void OnEnable() => Open.Add(this);
    private void OnDisable() => Open.Remove(this);
}
