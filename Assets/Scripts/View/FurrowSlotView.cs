using UnityEngine;
using UnityEngine.InputSystem;

// One instance per furrow slot (up to 3 per furrow). Shows the empty/locked
// placeholder sprite until planted, then swaps between the crop's seed/sprout/
// grown sprites as it grows. Tapping an empty slot opens GameManager's
// crop-selection prompt; a growing or awaiting-harvest slot ignores clicks —
// harvesting only happens through FarmService.Harvest (currently triggered by
// a debug-panel button standing in for the future otter NPC).
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FurrowSlotView : MonoBehaviour
{
    [SerializeField] private int plotIndex;
    [SerializeField] private int slotIndex;
    [SerializeField] private Sprite emptySlotSprite;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;

    private FurrowSlotState lastState = (FurrowSlotState)(-1);
    private SlotGrowthStage lastStage = (SlotGrowthStage)(-1);

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        var farmService = GameManager.Instance.FarmService;
        var state = farmService.GetSlotState(plotIndex, slotIndex);
        var stage = farmService.GetSlotStage(plotIndex, slotIndex);

        RefreshSprite(farmService, state, stage);
        HandleClick(state);
    }

    private void RefreshSprite(FarmService farmService, FurrowSlotState state, SlotGrowthStage stage)
    {
        if (state == lastState && stage == lastStage) return;
        lastState = state;
        lastStage = stage;

        if (state == FurrowSlotState.Empty)
        {
            spriteRenderer.sprite = emptySlotSprite;
            return;
        }

        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        Sprite[] variants = crop == null ? null : stage switch
        {
            SlotGrowthStage.Seed => crop.seedSprites,
            SlotGrowthStage.Sprout => crop.sproutSprites,
            SlotGrowthStage.Grown => crop.grownSprites,
            _ => null
        };

        spriteRenderer.sprite = (variants != null && variants.Length > 0)
            ? variants[Mathf.Clamp(slotIndex, 0, variants.Length - 1)]
            : emptySlotSprite;
    }

    private void HandleClick(FurrowSlotState state)
    {
        if (state != FurrowSlotState.Empty) return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null || hit.gameObject != gameObject) return;

        GameManager.Instance.RequestPlantPrompt(plotIndex, slotIndex);
    }
}
