using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 광장 월드 오브젝트(게시판, 나뭇가지, 해달) 탭 판정. 프레임마다 한 번만 계산해 여러 오브젝트가 같이 쓴다.
/// 드래그(카메라 이동)·핀치·UI 위 누름·꾸미기 모드는 탭이 아니다 (FairyNpcView와 같은 기준).
/// </summary>
public static class PlazaTapInput
{
    private const float TapThresholdScreenFraction = 0.02f;
    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

    private static int _frame = -1;
    private static bool _pressing;
    private static bool _pressOnUI;
    private static Vector2 _pressScreen;
    private static bool _tapped;
    private static Vector2 _tapWorld;

    /// <summary>이번 프레임에 탭이 끝났으면 그 월드 위치</summary>
    public static bool TryGetTap(out Vector2 world)
    {
        Poll();
        world = _tapWorld;
        return _tapped;
    }

    private static void Poll()
    {
        if (_frame == Time.frameCount)
            return;
        _frame = Time.frameCount;
        _tapped = false;

        var pointer = Pointer.current;
        var camera = Camera.main;
        if (pointer == null || camera == null)
            return;

        Vector2 screen = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            _pressing = true;
            _pressScreen = screen;
            _pressOnUI = IsOverUI(screen);
        }

        // 두 손가락이 닿으면 카메라 핀치 줌 → 이번 누름은 탭이 아님
        if (_pressing && IsMultiTouch())
            _pressing = false;

        if (!_pressing || !pointer.press.wasReleasedThisFrame)
            return;

        _pressing = false;
        float threshold = Mathf.Min(Screen.width, Screen.height) * TapThresholdScreenFraction;
        if (_pressOnUI || (screen - _pressScreen).sqrMagnitude > threshold * threshold)
            return;
        if (DecorModePresenter.IsActive)
            return;

        _tapWorld = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
        _tapped = true;
    }

    private static bool IsMultiTouch()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null)
            return false;

        int count = 0;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.isInProgress && ++count >= 2)
                return true;
        }
        return false;
    }

    private static bool IsOverUI(Vector2 screen)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        UiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, UiHits);
        return UiHits.Count > 0;
    }
}
