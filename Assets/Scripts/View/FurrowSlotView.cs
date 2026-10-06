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
// a locked plot stay hidden; a furrow not bought yet in an open plot shows
// greyed out with a "잠김" badge, and tapping it offers the next furrow.
// The next furrow to open shows its dashed circle even in a locked plot:
// breathing with "열기 300" once the kingdom level allows it, dim with
// "Lv.N" before that.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FurrowSlotView : MonoBehaviour
{
    private const string NoSeedBadgeText = "모종 없음";
    private const string LockedBadgeText = "잠김";
    private static readonly Color BadgeColor = new Color32(0xD9, 0x4F, 0x45, 0xFF);
    private static readonly Color LockedBadgeColor = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    private static readonly Color LockedTint = new Color(0.55f, 0.5f, 0.45f, 0.75f);
    // Text height on screen in world units, whatever the furrow's own scale
    // (the farm camera shows about 120 px per unit).
    private const float NextBadgeSize = 3.6f;
    private const float LockedBadgeSize = 2.6f;
    private static readonly Color NextWaitTint = new Color(0.75f, 0.7f, 0.62f, 0.7f);
    private static readonly Color NextReadyBadgeColor = new Color32(0x3E, 0x8E, 0x4A, 0xFF);
    private const float NextPulseSpeed = 3f;
    private const float NextPulseScale = 0.06f;

    [SerializeField] private int plotIndex;
    [SerializeField] private int slotIndex;
    [SerializeField] private Sprite emptySlotSprite;

    public int PlotIndex => plotIndex;
    public int SlotIndex => slotIndex;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private TMPro.TextMeshPro noSeedBadge;
    private TMPro.TextMeshPro lockedBadge;
    private TMPro.TextMeshPro nextBadge;
    private Vector3 baseScale;
    private string nextBadgeText;

    private FurrowSlotState lastState = (FurrowSlotState)(-1);
    private SlotGrowthStage lastStage = (SlotGrowthStage)(-1);
    private bool highlighted;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.FarmService == null) return;

        var farmService = GameManager.Instance.FarmService;
        bool plotOpen = farmService.IsPlotUnlocked(plotIndex);
        bool unlocked = farmService.IsSlotOpen(plotIndex, slotIndex);
        bool hasNext = farmService.TryGetNextFurrow(out int nextPlot, out int nextSlot, out var unlock);
        bool isNext = !unlocked && hasNext && nextPlot == plotIndex && nextSlot == slotIndex;
        spriteRenderer.enabled = plotOpen || isNext;
        RefreshNoSeedBadge(unlocked && farmService.GetWaitingSeedCrop(plotIndex, slotIndex) != null);
        RefreshLocked(plotOpen && !unlocked && !isNext);
        RefreshNext(isNext, isNext && GameManager.KingdomLevel >= unlock.requiredLevel, unlock);
        if (!unlocked)
        {
            if ((plotOpen || isNext) && WasTapped()) GameManager.Instance.RequestFurrowUnlockPrompt();
            return;
        }

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
            noSeedBadge = CreateBadge("NoSeedBadge", NoSeedBadgeText, BadgeColor);
        }
        if (noSeedBadge.gameObject.activeSelf != show) noSeedBadge.gameObject.SetActive(show);
        if (show) noSeedBadge.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.04f);
    }

    // A furrow of an open plot that isn't bought yet: the empty furrow greyed
    // out with "잠김". Forces a redraw once it opens.
    private void RefreshLocked(bool locked)
    {
        if (locked)
        {
            spriteRenderer.sprite = emptySlotSprite;
            spriteRenderer.color = LockedTint;
            lastState = (FurrowSlotState)(-1);
            if (lockedBadge == null) lockedBadge = CreateBadge("LockedBadge", LockedBadgeText, LockedBadgeColor, LockedBadgeSize);
        }
        if (lockedBadge != null && lockedBadge.gameObject.activeSelf != locked) lockedBadge.gameObject.SetActive(locked);
    }

    // The next furrow to open: its dashed circle breathes with "열기 300" when
    // it can be bought now, or stays dim with "Lv.N" until the level is reached.
    private void RefreshNext(bool isNext, bool ready, FarmBalanceData.FurrowUnlock unlock)
    {
        if (!isNext)
        {
            if (nextBadge != null && nextBadge.gameObject.activeSelf)
            {
                nextBadge.gameObject.SetActive(false);
                transform.localScale = baseScale;
            }
            return;
        }

        spriteRenderer.sprite = emptySlotSprite;
        // 열 수 있으면 첫 심기 안내처럼 금색으로 반짝임
        spriteRenderer.color = ready ? PlotView.GuidePulse(Color.white) : NextWaitTint;
        lastState = (FurrowSlotState)(-1);

        string text = ready ? $"열기 {unlock.cost:N0}" : $"Lv.{unlock.requiredLevel}";
        if (nextBadge == null) nextBadge = CreateBadge("NextFurrowBadge", text, NextReadyBadgeColor, NextBadgeSize);
        if (!nextBadge.gameObject.activeSelf) nextBadge.gameObject.SetActive(true);
        if (nextBadgeText != text)
        {
            nextBadgeText = text;
            if (nextBadge.font != null) nextBadge.font.TryAddCharacters(text, out _);
            nextBadge.text = text;
        }
        nextBadge.color = ready ? NextReadyBadgeColor : LockedBadgeColor;

        float pulse = ready ? 1f + Mathf.Sin(Time.time * NextPulseSpeed) * NextPulseScale : 1f;
        transform.localScale = baseScale * pulse;
    }

    // worldSize > 0: a label in the circle's middle at that size on screen
    // (undoing the furrow's scale), with a white outline to read on the soil.
    private TMPro.TextMeshPro CreateBadge(string name, string text, Color color, float worldSize = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, worldSize > 0f ? 0f : 0.55f, 0f);
        var badge = go.AddComponent<TMPro.TextMeshPro>();
        var font = RuntimeUIKit.Style.TitleFont;
        if (font != null)
        {
            font.TryAddCharacters(text, out _);
            badge.font = font;
        }
        badge.text = text;
        badge.fontSize = 2.4f;
        badge.alignment = TMPro.TextAlignmentOptions.Center;
        badge.color = color;
        badge.rectTransform.sizeDelta = new Vector2(3f, 0.8f);
        badge.sortingOrder = spriteRenderer.sortingOrder + 5;
        if (worldSize > 0f)
        {
            // 원(빈 고랑 그림)의 한가운데
            if (spriteRenderer.sprite != null)
            {
                var center = transform.InverseTransformPoint(spriteRenderer.bounds.center);
                go.transform.localPosition = new Vector3(center.x, center.y, 0f);
            }
            float scale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
            go.transform.localScale = Vector3.one / scale;
            badge.fontSize = worldSize;
            badge.fontStyle = TMPro.FontStyles.Bold;
            badge.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            badge.outlineWidth = 0.22f;
            badge.outlineColor = new Color32(0xFF, 0xF8, 0xEC, 0xFF);
        }
        return badge;
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
        if (!WasTapped()) return;

        if (state == FurrowSlotState.Empty) GameManager.Instance.RequestPlantPrompt(plotIndex, slotIndex);
        else GameManager.Instance.RequestCropChangePrompt(plotIndex, slotIndex);
    }

    // This furrow was pressed this frame (not through UI).
    private bool WasTapped()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
        if (mainCamera == null) return false;

        Vector2 screenPos = pointer.position.ReadValue();
        if (GameManager.Instance.IsScreenPointOverUI(screenPos)) return false;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        var hit = Physics2D.OverlapPoint(worldPos);
        return hit != null && hit.gameObject == gameObject;
    }
}
