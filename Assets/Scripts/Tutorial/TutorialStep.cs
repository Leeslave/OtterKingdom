using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 한 장: 제목, 설명, 그리고 강조할 대상의 화면 영역(픽셀).
/// 대상은 매 프레임 다시 구하므로 움직이는 해달도 따라간다.
/// 대상이 필요한 장인데 시작할 때 대상이 없으면(아직 안 열린 NPC 등) 그 장은 건너뛴다.
/// 월드 대상은 화면 밖에 있으면 광장 카메라를 그쪽으로 옮긴다(Focus).
/// "직접 해 보기" 장은 강조된 곳만 누를 수 있고, 누르면 그 동작이 실제로 되며 튜토리얼이 끝난다.
/// </summary>
public class TutorialStep
{
    public string Title { get; }
    public string Message { get; }
    public bool NeedsTarget => _target != null;

    /// <summary>강조된 곳을 직접 눌러야 끝나는 장 (누른 동작이 그대로 게임에 전해짐)</summary>
    public bool IsTryIt { get; }

    private readonly Func<Rect?> _target;
    private readonly Func<Vector3?> _focus;

    private TutorialStep(string title, string message, Func<Rect?> target, Func<Vector3?> focus = null, bool tryIt = false)
    {
        Title = title;
        Message = message;
        _target = target;
        _focus = focus;
        IsTryIt = tryIt;
    }

    /// <summary>강조 없이 화면 가운데에 설명만</summary>
    public static TutorialStep Info(string title, string message) => new TutorialStep(title, message, null);

    /// <summary>target이 돌려주는 화면 영역을 강조하며 설명</summary>
    public static TutorialStep At(string title, string message, Func<Rect?> target) =>
        new TutorialStep(title, message, target ?? throw new ArgumentNullException(nameof(target)));

    /// <summary>월드 대상을 강조하며 설명. 대상이 화면 밖이면 카메라가 그쪽으로 감</summary>
    public static TutorialStep AtWorld(string title, string message, Component target) =>
        new TutorialStep(title, message, () => TutorialTargets.World(target), FocusOn(target));

    /// <summary>직접 해 보기: 강조된 곳(UI든 월드든)을 누르면 그 동작이 실제로 되며 튜토리얼이 끝남</summary>
    public static TutorialStep TryIt(string title, string message, Func<Rect?> target, Component focus = null) =>
        new TutorialStep(title, message, target ?? throw new ArgumentNullException(nameof(target)), FocusOn(focus), true);

    public Rect? FindTarget() => _target?.Invoke();

    /// <summary>카메라가 향할 월드 지점 (없으면 null)</summary>
    public Vector3? FindFocus() => _focus?.Invoke();

    private static Func<Vector3?> FocusOn(Component target)
    {
        if (target == null) return null;
        return () => target != null && target.gameObject.activeInHierarchy ? target.transform.position : (Vector3?)null;
    }
}
