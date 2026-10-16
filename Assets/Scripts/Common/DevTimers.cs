using System;

/// <summary>
/// 개발용: 건설·주민 작업(영토 개간·생활 의뢰 포함)을 길어도 FastSeconds 안에 끝나게 한다.
/// 에디터 메뉴 OtterKingdom/Dev/Fast Timers가 켜고 끈다 (DevTimersMenu). 빌드에서는 켜는 곳이 없어 늘 꺼져 있다.
/// </summary>
public static class DevTimers
{
    public const double FastSeconds = 5d;

    /// <summary>켜져 있으면 새로 시작하는 기다림이 FastSeconds로 줄어든다</summary>
    public static bool Fast { get; set; }

    /// <summary>시작할 때 쓸 시간 (초)</summary>
    public static double Duration(double seconds) => Fast ? Math.Min(seconds, FastSeconds) : seconds;
}
