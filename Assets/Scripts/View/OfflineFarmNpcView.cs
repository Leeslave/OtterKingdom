using UnityEngine;
using UnityEngine.InputSystem;

// The farm NPC that takes offline-growing registrations. Only shown (and
// tappable) once the farm reaches the offline unlock level; tapping it opens
// GameManager's registration prompt. Placed by
// OtterKingdom > Tools > Setup Offline Farm NPC.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class OfflineFarmNpcView : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Collider2D npcCollider;
    private Camera mainCamera;

    private bool? lastShown;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        npcCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        SetShown(false);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        bool shown = GameManager.Instance.IsOfflineFarmUnlocked;
        SetShown(shown);
        if (shown) HandleClick();
    }

    private void SetShown(bool shown)
    {
        if (lastShown == shown) return;
        lastShown = shown;
        spriteRenderer.enabled = shown;
        npcCollider.enabled = shown;
    }

    private void HandleClick()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        if (!npcCollider.OverlapPoint(worldPos)) return;

        GameManager.Instance.RequestOfflineFarmPrompt();
    }
}
