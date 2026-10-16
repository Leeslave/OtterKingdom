using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 광산 탭: 손을 뗄 때가 아니라 <b>누르는 순간</b> 반응 (연타 손맛). 여러 손가락도 각각.
/// UI 위를 누른 것은 빼고 월드 위치로 돌려준다
/// </summary>
public static class MineTapInput
{
    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
    private static readonly List<Vector2> Taps = new List<Vector2>();

    /// <summary>이번 프레임에 새로 누른 곳들 (월드)</summary>
    public static IReadOnlyList<Vector2> Poll(Camera camera)
    {
        Taps.Clear();
        if (camera == null)
            return Taps;

        var touchscreen = Touchscreen.current;
        bool touched = false;
        if (touchscreen != null)
        {
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;
                touched = true;
                Add(camera, touch.position.ReadValue());
            }
        }
        var mouse = Mouse.current;
        if (!touched && mouse != null && mouse.leftButton.wasPressedThisFrame)
            Add(camera, mouse.position.ReadValue());
        return Taps;
    }

    private static void Add(Camera camera, Vector2 screen)
    {
        if (IsOverUI(screen))
            return;
        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
        Taps.Add(world);
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
