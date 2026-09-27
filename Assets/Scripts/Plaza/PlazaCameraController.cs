using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Drag-to-pan orthographic camera for the plaza. The grabbed point of the
// background stays under the finger (drag right -> camera moves left), the
// whole view — not just its centre — is kept inside the background, and the
// orthographic size is capped so the background always covers the screen on
// any aspect ratio. No zoom gesture or inertia yet (out of scope for v1).
//
// Input: one tracked pointer. The first touch that begins is followed until
// it ends; extra fingers are ignored, and once it ends nothing is tracked
// until a new press begins. Mouse left button is the editor fallback when no
// touch is active. A press that starts over UI stays "blocked" for its whole
// lifetime so it never turns into a camera drag. Uses the Input System
// directly (the project's active input handler); no legacy Input calls.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Camera))]
public class PlazaCameraController : MonoBehaviour
{
    [Header("Bounds & framing")]
    [Tooltip("World-space background whose rectangle bounds the view.")]
    [SerializeField] private SpriteRenderer boundsSource;
    [Tooltip("Shrinks the usable rectangle on each side (world units), e.g. to hide soft image edges.")]
    [SerializeField] private Vector2 boundsInset = Vector2.zero;
    [Tooltip("Where the camera starts. Clamped into bounds after applying.")]
    [SerializeField] private Transform initialFocus;

    [Header("Zoom")]
    [Tooltip("Preferred orthographic size. Automatically reduced if the background couldn't cover the view at this size.")]
    [SerializeField, Min(0.1f)] private float desiredOrthographicSize = 9f;

    [Header("Drag")]
    [Tooltip("Movement needed before a press becomes a drag, as a fraction of the screen's short side.")]
    [SerializeField, Range(0f, 0.1f)] private float dragThresholdScreenFraction = 0.01f;

    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

    private Camera cam;
    private bool warnedMissingBounds;

    private bool tracking;
    private int trackedTouchId = -1; // -1 while tracking = mouse
    private bool blockedByUI;
    private bool dragging;
    private Vector2 pressScreenPos;
    private Vector2 lastScreenPos;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        if (initialFocus != null)
        {
            Vector3 p = initialFocus.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
        // Apply before the first rendered frame so no empty edge ever flashes.
        ApplyZoomAndClamp();
    }

    private void OnDisable() => ResetDrag();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) ResetDrag();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ResetDrag();
    }

    private void Update()
    {
        if (tracking) ContinueTracking();
        else TryBeginTracking();
    }

    // Runs every frame (cheap) so resolution / orientation / viewport changes
    // are picked up without any change detection.
    private void LateUpdate()
    {
        ApplyZoomAndClamp();
    }

    // --- Input -------------------------------------------------------------

    private void TryBeginTracking()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.wasPressedThisFrame) continue;
                BeginTracking(touch.touchId.ReadValue(), touch.position.ReadValue());
                return;
            }
            if (AnyTouchInProgress(touchscreen)) return; // don't mix a simulated mouse into an active touch
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            BeginTracking(-1, mouse.position.ReadValue());
        }
    }

    private void BeginTracking(int touchId, Vector2 screenPos)
    {
        tracking = true;
        trackedTouchId = touchId;
        dragging = false;
        pressScreenPos = screenPos;
        lastScreenPos = screenPos;
        blockedByUI = IsOverUI(screenPos);
    }

    private void ContinueTracking()
    {
        if (trackedTouchId >= 0)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) { ResetDrag(); return; }

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.touchId.ReadValue() != trackedTouchId) continue;

                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                    phase == UnityEngine.InputSystem.TouchPhase.Canceled ||
                    phase == UnityEngine.InputSystem.TouchPhase.None)
                {
                    break;
                }

                MovePointer(touch.position.ReadValue());
                return;
            }
            ResetDrag(); // tracked finger lifted or vanished
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed)
        {
            ResetDrag();
            return;
        }
        MovePointer(mouse.position.ReadValue());
    }

    private void MovePointer(Vector2 screenPos)
    {
        if (blockedByUI) return;

        if (!dragging)
        {
            float threshold = dragThresholdScreenFraction * Mathf.Min(Screen.width, Screen.height);
            if ((screenPos - pressScreenPos).magnitude < threshold) return;

            // Start counting from here so crossing the threshold doesn't make
            // the view jump by the threshold distance.
            dragging = true;
            lastScreenPos = screenPos;
            return;
        }

        Vector2 screenDelta = screenPos - lastScreenPos;
        lastScreenPos = screenPos;
        if (screenDelta == Vector2.zero) return;

        // Ortho camera: world units per screen pixel is uniform.
        float worldPerPixel = 2f * cam.orthographicSize / cam.pixelHeight;
        Vector3 worldDelta = screenDelta * worldPerPixel;
        transform.position -= worldDelta;
        ApplyZoomAndClamp();
    }

    private void ResetDrag()
    {
        tracking = false;
        trackedTouchId = -1;
        dragging = false;
        blockedByUI = false;
    }

    private static bool AnyTouchInProgress(Touchscreen touchscreen)
    {
        foreach (TouchControl touch in touchscreen.touches)
        {
            if (touch.isInProgress) return true;
        }
        return false;
    }

    private static bool IsOverUI(Vector2 screenPos)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        var data = new PointerEventData(eventSystem) { position = screenPos };
        UiHits.Clear();
        eventSystem.RaycastAll(data, UiHits);
        return UiHits.Count > 0;
    }

    // --- Framing -----------------------------------------------------------

    private void ApplyZoomAndClamp()
    {
        if (!TryGetBounds(out Rect bounds)) return;

        float aspect = cam.aspect;
        // Largest size at which the background still covers the view on both axes.
        float maxSizeToCover = Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect));
        cam.orthographicSize = Mathf.Min(desiredOrthographicSize, maxSizeToCover);

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * aspect;
        Vector3 pos = transform.position;
        pos.x = ClampAxis(pos.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth, bounds.center.x);
        pos.y = ClampAxis(pos.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight, bounds.center.y);
        transform.position = pos;
    }

    // An axis with no room left (or float error making min > max) locks to
    // the background centre instead of clamping into an inverted range.
    private static float ClampAxis(float value, float min, float max, float center)
    {
        return min >= max ? center : Mathf.Clamp(value, min, max);
    }

    private bool TryGetBounds(out Rect rect)
    {
        if (boundsSource == null)
        {
            rect = default;
            if (!warnedMissingBounds)
            {
                warnedMissingBounds = true;
                Debug.LogWarning($"[{nameof(PlazaCameraController)}] Bounds Source is not assigned — camera is not clamped.", this);
            }
            return false;
        }

        Bounds b = boundsSource.bounds;
        Vector2 inset = new Vector2(Mathf.Max(0f, boundsInset.x), Mathf.Max(0f, boundsInset.y));
        Vector2 min = (Vector2)b.min + inset;
        Vector2 size = Vector2.Max((Vector2)b.size - inset * 2f, Vector2.one * 0.01f);
        rect = new Rect(min, size);
        return true;
    }
}
