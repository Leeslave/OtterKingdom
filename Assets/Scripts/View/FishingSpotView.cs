using UnityEngine;
using UnityEngine.InputSystem;

// The dashed circle at the end of the dock (same sprite as an empty furrow
// slot). The root transform is where the fishing otter's feet go; the circle
// itself is a child so it can be offset/scaled freely. Tapping it while the
// otter is off duty asks "낚시를 시작할까요?"; "예" turns fishing on and
// FishingOtterController walks over. Hidden while fishing is on — the otter
// is standing on it, and tapping the otter is how fishing is stopped.
public class FishingSpotView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private Collider2D circleCollider;

    private Camera mainCamera;

    public Vector2 FeetPosition => transform.position;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        // The rod upgrade belongs to the fishing spot, so the button only
        // exists in scenes that have one.
        if (GameManager.Instance != null) GameManager.Instance.ShowRodUpgradeButton();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool active = GameManager.Instance.FishingService.IsActive;
        circleRenderer.enabled = !active;
        circleCollider.enabled = !active;
        if (!active) HandleClick();
    }

    private void HandleClick()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        if (!circleCollider.OverlapPoint(worldPos)) return;

        GameManager.Instance.ShowConfirm("낚시", "낚시를 시작할까요?",
            () => GameManager.Instance.SetFishingActive(true));
    }
}
