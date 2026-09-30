using UnityEngine;
using UnityEngine.InputSystem;

// One instance per furrow slot (up to 3 per furrow). Shows the empty/locked
// placeholder sprite until planted, then swaps between the crop's seed/sprout/
// grown sprites as it grows. Tapping an empty slot opens GameManager's
// crop-selection prompt; tapping a growing or awaiting-harvest slot opens the
// "change crop" prompt instead. Harvesting itself is done by the farmer otter
// (FarmerOtterController), which also finds its walk targets through these
// views. Slots of a locked plot stay hidden and unclickable until PlotView's
// unlock goes through.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FurrowSlotView : MonoBehaviour
{
    [SerializeField] private int plotIndex;
    [SerializeField] private int slotIndex;
    [SerializeField] private Sprite emptySlotSprite;

    public int PlotIndex => plotIndex;
    public int SlotIndex => slotIndex;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;

    private FurrowSlotState lastState = (FurrowSlotState)(-1);
    private SlotGrowthStage lastStage = (SlotGrowthStage)(-1);
    private bool highlighted;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        var farmService = GameManager.Instance.FarmService;
        bool unlocked = farmService.IsPlotUnlocked(plotIndex);
        spriteRenderer.enabled = unlocked;
        if (!unlocked) return;

        var state = farmService.GetSlotState(plotIndex, slotIndex);
        var stage = farmService.GetSlotStage(plotIndex, slotIndex);

        RefreshSprite(farmService, state, stage);
        RefreshGuideHighlight(state);
        HandleClick(state);
    }

    // Empty slots of the first-plant guide plot pulse with PlotView's highlight.
    private void RefreshGuideHighlight(FurrowSlotState state)
    {
        bool highlight = state == FurrowSlotState.Empty
                         && plotIndex == GameManager.FirstPlantGuidePlotIndex
                         && GameManager.Instance.IsFirstPlantGuideActive;
        if (highlight) spriteRenderer.color = PlotView.GuidePulse(Color.white);
        else if (highlighted) lastState = (FurrowSlotState)(-1); // RefreshSprite restores the color next frame
        highlighted = highlight;
    }

    private void RefreshSprite(FarmService farmService, FurrowSlotState state, SlotGrowthStage stage)
    {
        if (state == lastState && stage == lastStage) return;
        lastState = state;
        lastStage = stage;

        if (state == FurrowSlotState.Empty)
        {
            spriteRenderer.sprite = emptySlotSprite;
            spriteRenderer.color = Color.white;
            return;
        }

        var crop = farmService.GetSlotCrop(plotIndex, slotIndex);
        spriteRenderer.color = crop != null ? crop.spriteTint : Color.white;
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
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;
        if (mainCamera == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null || hit.gameObject != gameObject) return;

        if (state == FurrowSlotState.Empty) GameManager.Instance.RequestPlantPrompt(plotIndex, slotIndex);
        else GameManager.Instance.RequestCropChangePrompt(plotIndex, slotIndex);
    }
}
