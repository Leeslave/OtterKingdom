using UnityEngine;
using UnityEngine.InputSystem;

// The dashed oval at the mine's mouth (same sprite as an empty furrow slot).
// The root transform is where the miner otter's feet stop before going in;
// the `inside` child is the point deeper in the tunnel it walks to while
// fading out. Tapping the oval while nobody is mining asks
// "채굴을 시작할까요?"; "예" turns mining on and MinerOtterController
// walks over. Hidden while mining is on — stopping is done from the emote.
public class MineEntranceView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private Collider2D circleCollider;
    [Tooltip("Where the otter disappears into the tunnel.")]
    [SerializeField] private Transform inside;

    private Camera mainCamera;

    public Vector2 FeetPosition => transform.position;
    public Vector2 InsidePosition => inside != null ? (Vector2)inside.position : FeetPosition;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        // The pickaxe upgrade belongs to the mine, so the button only exists
        // in the mine scene (same as the fishing spot's rod button).
        if (GameManager.Instance != null) GameManager.Instance.ShowPickaxeUpgradeButton();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool active = GameManager.Instance.IsMiningActive;
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

        GameManager.Instance.ShowConfirm("채굴", "채굴을 시작할까요?",
            () => GameManager.Instance.SetMiningActive(true));
    }
}
