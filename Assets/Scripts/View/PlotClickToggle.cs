using UnityEngine;
using UnityEngine.InputSystem;

// Temporary M1 wiring: tapping the active plot's sprite opens/closes the
// GameManager debug panel. Uses Pointer (new Input System) so the same code
// path covers mouse-in-editor and touch-on-device. Locked plots intentionally
// have no such component yet — unlock interaction comes later.
[RequireComponent(typeof(Collider2D))]
public class PlotClickToggle : MonoBehaviour
{
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null || GameManager.Instance == null) return;

        Vector2 screenPos = pointer.position.ReadValue();

        // Ignore clicks landing on the debug panel itself — otherwise pressing
        // a button in the panel while it overlaps the plot on-screen would
        // also register as a plot tap and immediately close the panel again.
        if (GameManager.Instance.IsDebugPanelOpen)
        {
            Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
            if (GameManager.DebugPanelRect.Contains(guiPos)) return;
        }

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit != null && hit.gameObject == gameObject)
        {
            GameManager.Instance.ToggleDebugPanel();
        }
    }
}
