using System;

/// <summary>
/// 게임이 읽는 지금 시각 (상세기획서 11.1 TimeProvider). 실제 시계 + 개발용으로 앞당긴 시간.
/// 앞당기기는 에디터 메뉴 OtterKingdom/Dev/Clock (DevClockMenu)에서만 바뀐다 — 빌드에서는 늘 0.
/// 일일 퀘스트 초기화, 건설·주민 작업, 오프라인 경과가 모두 이 시계를 본다.
/// </summary>
public static class GameClock
{
    /// <summary>개발용으로 앞당긴 시간 (에디터 전용, 기본 0)</summary>
    public static TimeSpan DevOffset { get; set; } = TimeSpan.Zero;

    public static DateTime UtcNow => DateTime.UtcNow + DevOffset;

    /// <summary>기기 현지 시각 (하루 경계·낮밤 계산용)</summary>
    public static DateTime Now => DateTime.Now + DevOffset;
}
