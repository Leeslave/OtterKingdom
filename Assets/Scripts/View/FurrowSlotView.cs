using UnityEngine;
using UnityEngine.InputSystem;

// One instance per furrow slot (up to 3 per furrow). Shows the empty/locked
// placeholder sprite until planted, then swaps between the crop's seed/sprout/
// grown sprites as it grows. Tapping an empty slot opens GameManager's
// crop-selection prompt; tapping a growing or awaiting-harvest slot opens the
// "change crop" prompt instead. Harvesting itself is done by the farmer
// (FarmService, in every zone scene; FarmerOtterController only shows it and
// finds its walk targets through these views). An empty slot whose replant
// ran out of seeds keeps a "모종 없음" badge until the seed is back. Slots of
// a locked plot stay hidden and unclickable until PlotView's unlock goes through.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FurrowSlotView : MonoBehaviour
{
    private const string NoSeedBadgeText = "모종 없음";
    private static readonly Color BadgeColor = new Color32(0xD9, 0x4F, 0x45, 0xFF);

    [SerializeField] private int plotIndex;
    [SerializeField] private int slotIndex;
    [SerializeField] private Sprite emptySlotSprite;

    public int PlotIndex => plotIndex;
    public int SlotIndex => slotIndex;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private TMPro.TextMeshPro noSeedBadge;

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
        RefreshNoSeedBadge(unlocked && farmService.GetWaitingSeedCrop(plotIndex, slotIndex) != null);
        if (!unlocked) return;

        var state = farmService.GetSlotState(plotIndex, slotIndex);
        var stage = farmService.GetSlotStage(plotIndex, slotIndex);

        RefreshSprite(farmService, state, stage);
        RefreshGuideHighlight(state);
        HandleClick(state);
    }

    // Small red label over the slot while it waits for seeds (made on first need).
    private void RefreshNoSeedBadge(bool show)
    {
        if (noSeedBadge == null)
        {
            if (!show) return;
            var go = new GameObject("NoSeedBadge");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            noSeedBadge = go.AddComponent<TMPro.TextMeshPro>();
            var font = RuntimeUIKit.Style.TitleFont;
            if (font != null)
            {
                font.TryAddCharacters(NoSeedBadgeText, out _);
                noSeedBadge.font = font;
            }
            noSeedBadge.text = NoSeedBadgeText;
            noSeedBadge.fontSize = 2.4f;
            noSeedBadge.alignment = TMPro.TextAlignmentOptions.Center;
            noSeedBadge.color = BadgeColor;
            noSeedBadge.rectTransform.sizeDelta = new Vector2(3f, 0.8f);
            noSeedBadge.sortingOrder = spriteRenderer.sortingOrder + 5;
        }
        if (noSeedBadge.gameObject.activeSelf != show) noSeedBadge.gameObject.SetActive(show);
        if (show) noSeedBadge.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.04f);
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
