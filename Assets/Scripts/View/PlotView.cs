using UnityEngine;
using UnityEngine.InputSystem;

// One instance per furrow plot root (Plot_1_Active / Plot_2_Locked / ...).
// Swaps between the locked and active furrow sprites based on the plot's
// saved unlock state. While locked, tapping the plot opens GameManager's
// coin-unlock prompt. The collider is only enabled while locked so it never
// competes with the per-slot colliders (FurrowSlotView) once unlocked.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlotView : MonoBehaviour
{
    [SerializeField] private int plotIndex;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite lockedSprite;

    private SpriteRenderer spriteRenderer;
    private Collider2D plotCollider;
    private Camera mainCamera;

    private bool? lastUnlocked;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        plotCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        bool unlocked = GameManager.Instance.FarmService.IsPlotUnlocked(plotIndex);
        RefreshVisual(unlocked);
        if (!unlocked) HandleClick();
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
