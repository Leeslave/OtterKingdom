using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Drag-to-pan and pinch/scroll-to-zoom orthographic camera for the plaza. The
// grabbed point of the background stays under the finger (drag right ->
// camera moves left), the whole view — not just its centre — is kept inside
// the background, and the orthographic size is capped so the background
// always covers the screen on any aspect ratio. No inertia yet.
//
// Zoom: the orthographic size stays within [minOrthographicSize,
// maxOrthographicSize], and never above the largest size at which the map
// still covers the screen. The map rectangle is re-read every frame from the
// bounds renderers, so a ground that grows (swapped/scaled sprite, or extra
// expansion tiles switched on at a level-up) widens both the pan area and the
// zoom-out limit with no extra wiring. A map smaller than the min zoom wins
// over the min zoom, so the view never shows past the map's edge.
//
// Input: one tracked pointer for dragging. The first touch that begins is
// followed until it ends; once it ends nothing is tracked until a new press
// begins. A second finger turns the gesture into a pinch (zoom about the
// fingers' midpoint, which also pans with it); after the pinch nothing drags
// until a new press, so lifting one finger never makes the view jump. Mouse
// left button / scroll wheel are the editor fallback when no touch is active,
// and in the editor Alt + left-drag stands in for a pinch.
// A press that starts over UI stays "blocked" for its whole lifetime so it
// never turns into a camera drag or pinch. Uses the Input System directly (the
// project's active input handler); no legacy Input calls.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Camera))]
public class PlazaCameraController : MonoBehaviour
{
    [Header("Bounds & framing")]
    [Tooltip("World-space background whose rectangle bounds the view.")]
    [SerializeField] private SpriteRenderer boundsSource;
    [Tooltip("More ground pieces added to the bounds (e.g. expansion tiles). Inactive ones are ignored, so switching a piece on at a level-up grows the map.")]
    [SerializeField] private Renderer[] extraBoundsSources = new Renderer[0];
    [Tooltip("Shrinks the usable rectangle on each side (world units), e.g. to hide soft image edges.")]
    [SerializeField] private Vector2 boundsInset = Vector2.zero;
    [Tooltip("Where the camera starts. Clamped into bounds after applying.")]
    [SerializeField] private Transform initialFocus;

    [Header("Zoom")]
    [Tooltip("Orthographic size on entry. Automatically reduced if the background couldn't cover the view at this size.")]
    [SerializeField, Min(0.1f)] private float desiredOrthographicSize = 9f;
    // 6: otters (1.67 units) fill ~14% of the screen height and the ground
    // (PPU 40) is only ~1.5x more magnified than the default 9 — closer than
    // that the ground art gets visibly blurry.
    [Tooltip("Most zoomed in (smallest orthographic size, half the view height in world units).")]
    [SerializeField, Min(0.1f)] private float minOrthographicSize = 6f;
    // 13: otters stay ~6% of the screen height. On today's 25.6 x 38.4 plaza a
    // portrait phone is limited by the map (~19) before this, so this cap only
    // matters once the map grows.
    [Tooltip("Most zoomed out (largest orthographic size). The map covering the screen can limit it further.")]
    [SerializeField, Min(0.1f)] private float maxOrthographicSize = 13f;
    [Tooltip("Size multiplier per mouse-wheel step (editor / desktop).")]
    [SerializeField, Range(1.01f, 1.5f)] private float scrollZoomStep = 1.1f;

    [Header("Drag")]
    [Tooltip("Movement needed before a press becomes a drag, as a fraction of the screen's short side.")]
    [SerializeField, Range(0f, 0.1f)] private float dragThresholdScreenFraction = 0.01f;

    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

    private Camera cam;
    private bool warnedMissingBounds;

    // The zoom the player chose. Applied through the current limits every
    // frame (not overwritten by them), so a temporary limit such as a rotated
    // screen doesn't lose it.
    private float zoomSize;
    private Rect? boundsOverride;

    private bool tracking;
    private int trackedTouchId = -1; // -1 while tracking = mouse
    private bool blockedByUI;
    private bool dragging;
    private Vector2 pressScreenPos;
    private Vector2 lastScreenPos;

    private bool pinching;
    private bool pinchBlockedByUI;
    private int pinchIdA = -1;
    private int pinchIdB = -1;
    private float lastPinchDistance;
    private Vector2 lastPinchMid;

    // PanTo: glide toward a point (e.g. a house that just appeared) until the
    // player touches the screen.
    private bool panning;
    private Vector2 panTarget;
    private const float PanSmoothTime = 0.35f;
    private Vector2 panVelocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        zoomSize = desiredOrthographicSize;

        if (initialFocus != null)
        {
            Vector3 p = initialFocus.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
        // Apply before the first rendered frame so no empty edge ever flashes.
        ApplyZoomAndClamp();
    }

    private void OnDisable()
    {
        ResetDrag();
        EndPinch();
#if UNITY_EDITOR
        editorPinching = false;
#endif
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) { ResetDrag(); EndPinch(); }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) { ResetDrag(); EndPinch(); }
    }

    private void OnValidate()
    {
        if (maxOrthographicSize < minOrthographicSize) maxOrthographicSize = minOrthographicSize;
    }

    private void Update()
    {
        if (UpdatePinch()) return;
#if UNITY_EDITOR
        if (UpdateEditorPinch()) return;
#endif

        if (tracking) ContinueTracking();
        else TryBeginTracking();

        UpdateScrollZoom();
        UpdatePan();
    }

    // Celebration (house finished): briefly glide in closer, hold, then ease
    // back to the zoom the player chose. Stored separately from zoomSize so
    // the player's own zoom is never lost.
    private float celebrateTime = -1f;
    private float celebrateDuration;
    private float celebrateFactor = 1f;
    private float celebrateZoom = 1f;

    /// Glides to a world point and zooms in a little for a moment (about
    /// `duration` seconds in total), then returns to the player's zoom.
    public void Celebrate(Vector2 worldPoint, float zoomFactor = 0.8f, float duration = 2.4f)
    {
        PanTo(worldPoint);
        celebrateFactor = zoomFactor;
        celebrateDuration = Mathf.Max(0.1f, duration);
        celebrateTime = 0f;
    }

    private void UpdateCelebrate()
    {
        if (celebrateTime < 0f)
        {
            celebrateZoom = 1f;
            return;
        }
        celebrateTime += Time.unscaledDeltaTime;
        float k = celebrateTime / celebrateDuration;
        // in (0~25%) · hold · out (70~100%)
        float amount = k < 0.25f ? Mathf.SmoothStep(0f, 1f, k / 0.25f)
            : k < 0.7f ? 1f
            : Mathf.SmoothStep(1f, 0f, (k - 0.7f) / 0.3f);
        celebrateZoom = Mathf.Lerp(1f, celebrateFactor, amount);
        if (k >= 1f)
        {
            celebrateTime = -1f;
            celebrateZoom = 1f;
        }
    }

    /// Replaces the bound renderers with a fixed rectangle (e.g. the unlocked
    /// part of a bigger map that is drawn as one sprite). null goes back to
    /// the renderers. Applied immediately, so a shrinking area pulls the view in.
    public void SetBoundsOverride(Rect? rect)
    {
        boundsOverride = rect;
        if (cam != null) ApplyZoomAndClamp();
    }

    /// Glides the view to centre on a world point (clamped into the map like
    /// any drag). A touch or drag by the player cancels it.
    public void PanTo(Vector2 worldPoint)
    {
        panTarget = worldPoint;
        panVelocity = Vector2.zero;
        panning = true;
    }

    private void UpdatePan()
    {
        if (!panning) return;
        if (tracking || pinching)
        {
            panning = false;
            return;
        }

        Vector2 pos = transform.position;
        Vector2 next = Vector2.SmoothDamp(pos, panTarget, ref panVelocity, PanSmoothTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        if ((next - panTarget).sqrMagnitude < 0.0004f) panning = false;
    }

    // Runs every frame (cheap) so resolution / orientation / viewport / map
    // size changes are picked up without any change detection.
    private void LateUpdate()
    {
        UpdateCelebrate();
        ApplyZoomAndClamp();
    }

    // --- Input: drag -------------------------------------------------------

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
        Vector3 worldDelta = screenDelta * WorldPerPixel(cam.orthographicSize);
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

    // --- Input: zoom -------------------------------------------------------

    // Returns true while two fingers are down (the pinch owns the input).
    private bool UpdatePinch()
    {
        var touchscreen = Touchscreen.current;
        TouchControl a = null, b = null;
        if (touchscreen != null)
        {
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.isInProgress) continue;
                if (a == null) a = touch;
                else { b = touch; break; }
            }
        }

        if (b == null)
        {
            EndPinch();
            return false;
        }

        Vector2 posA = a.position.ReadValue();
        Vector2 posB = b.position.ReadValue();
        float distance = (posA - posB).magnitude;
        Vector2 mid = (posA + posB) * 0.5f;
        int idA = a.touchId.ReadValue();
        int idB = b.touchId.ReadValue();

        // A new pinch, or a different pair of fingers: take a fresh baseline
        // so the view doesn't jump.
        if (!pinching || idA != pinchIdA || idB != pinchIdB)
        {
            if (!pinching)
            {
                ResetDrag(); // the first finger's drag hands over to the pinch
                pinchBlockedByUI = IsOverUI(a.startPosition.ReadValue()) || IsOverUI(b.startPosition.ReadValue());
            }
            pinching = true;
            pinchIdA = idA;
            pinchIdB = idB;
            lastPinchDistance = distance;
            lastPinchMid = mid;
            return true;
        }

        if (!pinchBlockedByUI && lastPinchDistance > 0.5f && distance > 0.5f)
        {
            // Fingers apart -> smaller size (zoom in). The world point that was
            // under the old midpoint moves to the new one: zoom + pan together.
            float size = cam.orthographicSize * (lastPinchDistance / distance);
            ZoomAround(lastPinchMid, mid, size);
        }
        lastPinchDistance = distance;
        lastPinchMid = mid;
        return true;
    }

    private void EndPinch()
    {
        pinching = false;
        pinchBlockedByUI = false;
        pinchIdA = -1;
        pinchIdB = -1;
    }

#if UNITY_EDITOR
    // Editor stand-in for a pinch (a mouse has one pointer): hold Alt and
    // left-drag. Up = zoom in, down = zoom out, about the pressed point —
    // the same ZoomAround path a real pinch uses.
    private const float EditorPinchSpeed = 3f; // e-fold per screen height dragged

    private bool editorPinching;
    private Vector2 editorPinchAnchor;
    private Vector2 editorPinchLast;

    private bool UpdateEditorPinch()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null) { editorPinching = false; return false; }

        if (!editorPinching)
        {
            if (keyboard == null || !keyboard.altKey.isPressed || !mouse.leftButton.wasPressedThisFrame) return false;

            Vector2 pressPos = mouse.position.ReadValue();
            if (IsOverUI(pressPos)) return false; // let UI (and the normal blocked-press path) have it
            ResetDrag();
            editorPinching = true;
            editorPinchAnchor = pressPos;
            editorPinchLast = pressPos;
            return true;
        }

        if (!mouse.leftButton.isPressed)
        {
            editorPinching = false;
            return true;
        }

        Vector2 pos = mouse.position.ReadValue();
        float dy = pos.y - editorPinchLast.y;
        editorPinchLast = pos;
        if (dy != 0f)
        {
            float size = cam.orthographicSize * Mathf.Exp(-dy / cam.pixelHeight * EditorPinchSpeed);
            ZoomAround(editorPinchAnchor, editorPinchAnchor, size);
        }
        return true;
    }
#endif

    private void UpdateScrollZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Only the direction is used: wheel deltas differ between platforms
        // and Input System settings, one step per frame keeps it predictable.
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        Vector2 screenPos = mouse.position.ReadValue();
        if (!cam.pixelRect.Contains(screenPos) || IsOverUI(screenPos)) return;

        float size = scroll > 0f ? cam.orthographicSize / scrollZoomStep : cam.orthographicSize * scrollZoomStep;
        ZoomAround(screenPos, screenPos, size);
    }

    // Sets the zoom so the world point under `fromScreen` (at the current
    // zoom) ends up under `toScreen`, then clamps into the map.
    private void ZoomAround(Vector2 fromScreen, Vector2 toScreen, float size)
    {
        Vector2 anchor = ScreenToWorld(fromScreen, cam.orthographicSize);

        GetZoomLimits(TryGetBounds(out Rect bounds), bounds, out float min, out float max);
        zoomSize = Mathf.Clamp(size, min, max); // stored clamped: no dead zone past the limit
        cam.orthographicSize = zoomSize;

        Vector2 offset = (toScreen - cam.pixelRect.center) * WorldPerPixel(zoomSize);
        Vector3 pos = transform.position;
        pos.x = anchor.x - offset.x;
        pos.y = anchor.y - offset.y;
        transform.position = pos;
        ApplyZoomAndClamp();
    }

    private float WorldPerPixel(float orthographicSize) => 2f * orthographicSize / cam.pixelHeight;

    private Vector2 ScreenToWorld(Vector2 screenPos, float orthographicSize)
    {
        return (Vector2)transform.position + (screenPos - cam.pixelRect.center) * WorldPerPixel(orthographicSize);
    }

    // --- Framing -----------------------------------------------------------

    private void ApplyZoomAndClamp()
    {
        bool hasBounds = TryGetBounds(out Rect bounds);
        GetZoomLimits(hasBounds, bounds, out float min, out float max);
        // A celebration may dip a little below the min zoom (it eases back)
        cam.orthographicSize = Mathf.Clamp(zoomSize * celebrateZoom, min * celebrateZoom, max);
        if (!hasBounds) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Vector3 pos = transform.position;
        pos.x = ClampAxis(pos.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth, bounds.center.x);
        pos.y = ClampAxis(pos.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight, bounds.center.y);
        transform.position = pos;
    }

    private void GetZoomLimits(bool hasBounds, Rect bounds, out float min, out float max)
    {
        max = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        if (hasBounds)
        {
            // Largest size at which the background still covers the view on both axes.
            float maxSizeToCover = Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * cam.aspect));
            max = Mathf.Min(max, maxSizeToCover);
        }
        // A map too small for the min zoom: covering the screen wins.
        min = Mathf.Min(minOrthographicSize, max);
    }

    // An axis with no room left (or float error making min > max) locks to
    // the background centre instead of clamping into an inverted range.
    private static float ClampAxis(float value, float min, float max, float center)
    {
        return min >= max ? center : Mathf.Clamp(value, min, max);
    }

    // Union of the active bound renderers, shrunk by the inset (or the override).
    private bool TryGetBounds(out Rect rect)
    {
        if (boundsOverride.HasValue)
        {
            rect = boundsOverride.Value;
            return true;
        }
        bool has = false;
        Bounds b = default;
        AddBounds(boundsSource, ref b, ref has);
        if (extraBoundsSources != null)
        {
            foreach (var source in extraBoundsSources)
                AddBounds(source, ref b, ref has);
        }

        if (!has)
        {
            rect = default;
            if (!warnedMissingBounds)
            {
                warnedMissingBounds = true;
                Debug.LogWarning($"[{nameof(PlazaCameraController)}] Bounds Source is not assigned — camera is not clamped.", this);
            }
            return false;
        }

        Vector2 inset = new Vector2(Mathf.Max(0f, boundsInset.x), Mathf.Max(0f, boundsInset.y));
        Vector2 min = (Vector2)b.min + inset;
        Vector2 size = Vector2.Max((Vector2)b.size - inset * 2f, Vector2.one * 0.01f);
        rect = new Rect(min, size);
        return true;
    }

    private static void AddBounds(Renderer source, ref Bounds bounds, ref bool has)
    {
        if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) return;

        if (has) bounds.Encapsulate(source.bounds);
        else { bounds = source.bounds; has = true; }
    }
}
