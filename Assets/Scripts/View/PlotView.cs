using UnityEngine;
using UnityEngine.InputSystem;

// One instance per furrow plot root (Plot_1_Active / Plot_2_Locked / ...).
// Swaps between the locked and active furrow sprites based on the plot's
// saved unlock state. While locked, tapping the plot opens GameManager's
// coin-unlock prompt. The collider is only enabled while locked so it never
// competes with the per-slot colliders (FurrowSlotView) once unlocked.
// The first-plant guide plot also shows the "당근을 심어 볼까?" guide and
// pulses until the player's first planting.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlotView : MonoBehaviour
{
    [SerializeField] private int plotIndex;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite lockedSprite;

    private static readonly Color GuideHighlightColor = new Color(1f, 0.85f, 0.35f, 1f);
    private const float GuidePulseSpeed = 4f;

    private SpriteRenderer spriteRenderer;
    private Collider2D plotCollider;
    private Camera mainCamera;

    private bool? lastUnlocked;
    private Color baseColor;
    private bool highlighted;

    private bool IsGuidePlot => plotIndex == GameManager.FirstPlantGuidePlotIndex;

    // Shared with FurrowSlotView so the guide plot's empty slots pulse in step.
    public static Color GuidePulse(Color from)
    {
        float t = (Mathf.Sin(Time.unscaledTime * GuidePulseSpeed) + 1f) * 0.5f;
        return Color.Lerp(from, GuideHighlightColor, t);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        plotCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        baseColor = spriteRenderer.color;
    }

    private void Start()
    {
        if (IsGuidePlot && GameManager.Instance != null) GameManager.Instance.ShowFirstPlantGuide();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        bool unlocked = GameManager.Instance.FarmService.IsPlotUnlocked(plotIndex);
        RefreshVisual(unlocked);
        RefreshGuideHighlight(unlocked);
        if (!unlocked) HandleClick();
    }

    private void RefreshGuideHighlight(bool unlocked)
    {
        bool highlight = unlocked && IsGuidePlot && GameManager.Instance.IsFirstPlantGuideActive;
        if (highlight) spriteRenderer.color = GuidePulse(baseColor);
        else if (highlighted) spriteRenderer.color = baseColor;
        highlighted = highlight;
    }

    private void RefreshVisual(bool unlocked)
    {
        if (lastUnlocked == unlocked) return;
        lastUnlocked = unlocked;

        var sprite = unlocked ? activeSprite : lockedSprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
        plotCollider.enabled = !unlocked;
    }

    private void HandleClick()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null || hit.gameObject != gameObject) return;

        GameManager.Instance.RequestUnlockPrompt(plotIndex);
    }
}
