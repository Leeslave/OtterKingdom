using System;
using System.Collections.Generic;

/// <summary>화면 위쪽에 잠깐 뜨는 알림 한 건 (막지 않음). 버튼이 있으면 [버튼] + [닫기]</summary>
public readonly struct GameNotice
{
    public readonly string Message;
    public readonly string ActionLabel;
    public readonly Action Action;

    public GameNotice(string message, string actionLabel = null, Action action = null)
    {
        Message = message;
        ActionLabel = actionLabel;
        Action = action;
    }

    public bool HasAction => !string.IsNullOrEmpty(ActionLabel) && Action != null;
}

/// <summary>
/// 어느 장소에서든 쓰는 막지 않는 알림 (밭 모종 부족·가방 가득 등). 게임 알림이며 운영체제 푸시가 아니다.
/// 보낸 순서대로 하나씩, 공통 대기 규칙(PresentationGate)에 따라 레벨업·튜토리얼·등장 연출이 끝난 뒤 보인다.
/// 같은 문장이 줄에 이미 있으면 다시 넣지 않는다.
/// </summary>
public static class GameNotices
{
    private static readonly List<GameNotice> Pending = new List<GameNotice>();

    public static int PendingCount => Pending.Count;

    public static void Post(GameNotice notice)
    {
        if (string.IsNullOrEmpty(notice.Message))
            return;
        foreach (var pending in Pending)
        {
            if (pending.Message == notice.Message)
                return;
        }
        Pending.Add(notice);
        GameNoticeToast.EnsureExists();
    }

    /// <summary>다음 알림을 꺼냄 (없으면 false)</summary>
    public static bool TryTake(out GameNotice notice)
    {
        if (Pending.Count == 0)
        {
            notice = default;
            return false;
        }
        notice = Pending[0];
        Pending.RemoveAt(0);
        return true;
    }

    /// <summary>테스트·새 게임용</summary>
    public static void Clear() => Pending.Clear();
}
