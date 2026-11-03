using System;
using System.Collections.Generic;

/// <summary>
/// 번영의 시대 약속 (Docs/번영의시대_개발분담.md 3장): "이 기술을 배웠나".
/// 연구 쪽(개발자 B)의 매니저가 Provider를 채우고, 배울 때 NotifyLearned를 부른다. 채우기 전에는 모두 false.
/// 산업 · 물류 · 도시 쪽(개발자 A)은 Has · Learned만 쓴다
/// </summary>
public static class TechGate
{
    /// <summary>연구 매니저가 채움: 기술 ID → 배웠나</summary>
    public static Func<string, bool> Provider;

    /// <summary>기술을 새로 배움 (연구 매니저 · 개발 도구가 알림)</summary>
    public static event Action<string> Learned;

    // 개발 도구로 미리 배운 기술 (연구 시스템이 생기기 전 시험용, 저장 안 함)
    private static readonly HashSet<string> _devLearned = new HashSet<string>();

    public static bool Has(string techId) =>
        !string.IsNullOrEmpty(techId) && (_devLearned.Contains(techId) || (Provider != null && Provider(techId)));

    public static void NotifyLearned(string techId)
    {
        if (!string.IsNullOrEmpty(techId))
            Learned?.Invoke(techId);
    }

    /// <summary>개발 도구: 연구 없이 기술을 배운 것으로 (플레이를 끄면 사라짐)</summary>
    public static void DevLearn(string techId)
    {
        if (!string.IsNullOrEmpty(techId) && _devLearned.Add(techId))
            Learned?.Invoke(techId);
    }

    public static void DevReset() => _devLearned.Clear();
}
