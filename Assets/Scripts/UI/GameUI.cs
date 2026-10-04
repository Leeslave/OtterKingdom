using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Zone-scene uGUI (farm, fishing and mine), built entirely at runtime (same
// approach as CurrencyHud) so no scene/prefab wiring is needed: the fishing
// scene's "낚싯대 강화" and the mine's "곡괭이 강화" buttons and every modal
// popup (crop selection, plot unlock, crop change, confirm, alerts, rod and
// pickaxe upgrade). Selling lives in the
// bag (GlobalUI). Holds no game rules itself — every action goes back
// through GameManager. Looks come from Resources/RuntimeUIStyle (the same
// sticker style as GlobalUI: cream panel, Cafe24/Nanum fonts, green/cream/
// paper/yellow buttons); without it everything falls back to flat colors.
public class GameUI : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    // Modals draw above GlobalUI's HUD (100). The guide bubble and the corner
    // feature buttons are part of the screen instead, so they sit below
    // GlobalUI: its popups (board, level-up, "path opened") and the
    // scene-change clouds cover them.
    private const int ModalSortingOrder = 200;
    private const int ScreenSortingOrder = 90;
    private const float PanelWidth = 900f;
    private const int TitleFontSize = 48;
    private const int BodyFontSize = 40;
    private const float ButtonHeight = 110f;
    // Corner feature buttons (rod/pickaxe upgrade) sit just above GlobalUI's bottom nav bar
    // (bar 24 + 190, centre button pokes up to ~250).
    private const float FeatureButtonBottom = 270f;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
    // Flat fallbacks when RuntimeUIStyle is missing.
    private static readonly Color PanelColor = new Color(0.97f, 0.94f, 0.86f, 1f);
    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 1f);

    // Which button sprite: the affirmative action (green), backing out
    // (cream), one of several choices (paper), or a corner feature button (yellow).
    private enum ButtonKind { Option, Primary, Secondary, Feature }

    private GameManager game;
    private RuntimeUIStyle style;
    private Button rodUpgradeButton;
    private Button pickaxeUpgradeButton;
    private Button farmUpgradeButton;
    private RectTransform guideBubble;
    private RectTransform screenLayer;
    private GameObject offlineReportModal;

    private readonly List<GameObject> modals = new List<GameObject>();
    private readonly Dictionary<GameObject, Action> modalRefreshers = new Dictionary<GameObject, Action>();
    private readonly HashSet<string> openAlertMessages = new HashSet<string>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    public bool IsModalOpen => modals.Count > 0;

    // For the zone tutorial's highlight; null until the scene shows the button.
    public RectTransform RodUpgradeButton => rodUpgradeButton != null ? (RectTransform)rodUpgradeButton.transform : null;
    public RectTransform PickaxeUpgradeButton =>
        pickaxeUpgradeButton != null ? (RectTransform)pickaxeUpgradeButton.transform : null;
    public RectTransform FarmUpgradeButton =>
        farmUpgradeButton != null ? (RectTransform)farmUpgradeButton.transform : null;

    public static GameUI Create(GameManager game)
    {
        EnsureEventSystem();

        var go = new GameObject(nameof(GameUI),
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var ui = go.AddComponent<GameUI>();
        ui.Initialize(game);
        return ui;
    }

    // The zone scenes have no EventSystem, and uGUI buttons need one. The project
    // runs on the new Input System only, so it needs the Input System module
    // rather than the legacy StandaloneInputModule.
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject(nameof(EventSystem), typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void Initialize(GameManager owner)
    {
        game = owner;
        style = Resources.Load<RuntimeUIStyle>(RuntimeUIStyle.ResourcePath);
        if (style == null) style = ScriptableObject.CreateInstance<RuntimeUIStyle>();

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ModalSortingOrder; // above CurrencyHud / GlobalUI (100)

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        screenLayer = CreateScreenLayer();
    }

    // Full-screen child canvas with its own (lower) sorting order. Needs its
    // own raycaster: a nested canvas's graphics only take clicks through it.
    private RectTransform CreateScreenLayer()
    {
        var go = new GameObject("ScreenLayer", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var layer = go.GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingOrder = ScreenSortingOrder;
        return rect;
    }

    private void Update()
    {
        foreach (var refresh in modalRefreshers.Values) refresh();
        BobGuide();
    }

    // True if a screen point should NOT reach the world (slots/plots): any
    // modal is open, or the point is over some uGUI element (e.g. the rod
    // upgrade button or the currency HUD).
    public bool IsBlocking(Vector2 screenPos)
    {
        if (IsModalOpen) return true;
        if (EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        raycastResults.Clear();
        EventSystem.current.RaycastAll(data, raycastResults);
        return raycastResults.Count > 0;
    }

    // ---------------------------------------------------------------- popups

    public void ShowSeedPrompt(int plotIndex, int slotIndex)
    {
        CloseAllModals();
        var modal = OpenModal("심을 작물을 선택하세요", out var content);

        foreach (var crop in game.Crops)
        {
            if (crop == null) continue;

            var cropForClick = crop;
            string label = game.FarmService.IsUnlimitedSeed(crop.cropId)
                ? crop.displayName
                : $"{crop.displayName} (남은 씨앗 {game.FarmService.GetSeedCount(crop.cropId)})";

            CreateButton(content, label, () =>
            {
                switch (game.PlantFromPrompt(plotIndex, slotIndex, cropForClick.cropId))
                {
                    case PlantResult.Planted:
                    case PlantResult.Failed:
                        CloseModal(modal);
                        break;
                    case PlantResult.NoSeed:
                        ShowAlert(GameManager.NoSeedMessage(cropForClick));
                        break;
                }
            });
        }

        CreateButton(content, "취소", () => CloseModal(modal), kind: ButtonKind.Secondary);
    }

    public void ShowUnlockPrompt(int plotIndex)
    {
        CloseAllModals();
        var modal = OpenModal($"밭 {plotIndex + 1} 해금", out var content);

        CreateLabel(content, $"밭 {plotIndex + 1} 해금에 {game.FarmService.PlotUnlockCost} 코인이 필요해요.\n해금할까요?");
        var balanceLabel = CreateLabel(content, "");
        SetRefresher(modal, () => balanceLabel.text = $"보유 코인 : {game.CoinBalance}");

        var row = CreateRow(content, ButtonHeight);
        CreateButton(row, "확인", () =>
        {
            if (game.TryUnlockPlot(plotIndex)) CloseModal(modal);
            else ShowAlert("코인이 부족해요!");
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, "취소", () => CloseModal(modal), flexible: true, kind: ButtonKind.Secondary);
    }

    // "예" only empties the slot; the player then taps the empty slot again
    // to open the normal crop-selection prompt.
    public void ShowCropChangePrompt(int plotIndex, int slotIndex)
    {
        var crop = game.FarmService.GetSlotCrop(plotIndex, slotIndex);
        if (crop == null) return;

        CloseAllModals();
        var modal = OpenModal("작물 변경", out var content);

        CreateLabel(content, "작물을 변경하시겠습니까?");
        CreateLabel(content, $"심어져 있던 {WithTopicParticle(crop.displayName)} 버려집니다.");
        if (game.FarmService.GetSlotState(plotIndex, slotIndex) == FurrowSlotState.AwaitingHarvest)
        {
            var warning = CreateLabel(content, "주의! 수확 대기 중인 작물이에요.\n수확하지 않고 버려집니다.");
            warning.color = style.WarningColor;
        }

        var row = CreateRow(content, ButtonHeight);
        CreateButton(row, "예", () =>
        {
            game.DiscardSlot(plotIndex, slotIndex);
            CloseModal(modal);
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, "아니오", () => CloseModal(modal), flexible: true, kind: ButtonKind.Secondary);
    }

    public void ShowConfirm(string title, string message, Action onYes)
    {
        ShowChoice(title, message, "예", onYes, "아니오", null);
    }

    // Two-button popup with custom labels; either button closes it.
    public void ShowChoice(string title, string message, string yesLabel, Action onYes, string noLabel, Action onNo)
    {
        CloseAllModals();
        var modal = OpenModal(title, out var content);
        CreateLabel(content, message);

        var row = CreateRow(content, ButtonHeight);
        CreateButton(row, yesLabel, () =>
        {
            CloseModal(modal);
            onYes?.Invoke();
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, noLabel, () =>
        {
            CloseModal(modal);
            onNo?.Invoke();
        }, flexible: true, kind: ButtonKind.Secondary);
    }

    // Stacks on top of whatever is open (e.g. over the crop-selection prompt).
    // The same message is never shown twice at once, so several slots running
    // out of seeds together only produce one popup.
    public void ShowAlert(string message)
    {
        if (!openAlertMessages.Add(message)) return;

        var modal = OpenModal("알림", out var content);
        CreateLabel(content, message);
        CreateButton(content, "확인", () =>
        {
            openAlertMessages.Remove(message);
            CloseModal(modal);
        }, kind: ButtonKind.Primary);
    }

    // ----------------------------------------------------------------- guide

    // One-line tutorial bubble in the upper part of the screen. It never
    // takes clicks (so IsBlocking ignores it and the world stays tappable) and
    // sits below every modal.
    public void ShowGuide(string message)
    {
        HideGuide();

        var go = new GameObject("Guide", typeof(RectTransform), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(screenLayer, false);
        go.transform.SetAsFirstSibling();

        var image = go.GetComponent<Image>();
        ApplySprite(image, style.Bubble, PanelColor);
        image.raycastTarget = false;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(64, 64, 34, 40);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = CreateLabel(go.transform, message);
        if (style.TitleFont != null) label.font = style.TitleFont;
        label.fontSize = TitleFontSize;
        label.color = style.TextColor;

        guideBubble = (RectTransform)go.transform;
        guideBubble.anchorMin = guideBubble.anchorMax = guideBubble.pivot = new Vector2(0.5f, 0.75f);
    }

    public void HideGuide()
    {
        if (guideBubble == null) return;
        Destroy(guideBubble.gameObject);
        guideBubble = null;
    }

    private void BobGuide()
    {
        if (guideBubble == null) return;
        guideBubble.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime * 3f) * 12f);
    }

    // --------------------------------------------------------------- offline

    // Return popup after offline production. Stacks on top of anything open,
    // but replaces an earlier report still on screen (left open, then away again).
    public void ShowOfflineReport(OfflineReport report, Func<string, string> itemName)
    {
        if (offlineReportModal != null) CloseModal(offlineReportModal);

        var modal = OpenModal("자리를 비운 동안", out var content);
        offlineReportModal = modal;
        CreateLabel(content, $"{FormatDuration(report.ElapsedSec)} 동안 있었던 일이에요.");

        if (report.SettlementNews.Count > 0) CreateLabel(content, "마을 소식\n" + string.Join("\n", report.SettlementNews));

        if (report.OtterVisits.Count > 0) CreateLabel(content, DescribeOtterVisits(report.OtterVisits));

        if (report.Received.Count > 0) CreateLabel(content, "받은 것\n" + ListStacks(report.Received, itemName));
        if (report.SeedsUsed.Count > 0) CreateLabel(content, "사용한 모종\n" + ListStacks(report.SeedsUsed, itemName));

        if (report.Lost.Count > 0)
        {
            var lost = CreateLabel(content, "가방이 가득 차서 놓친 것\n" + ListStacks(report.Lost, itemName));
            lost.color = style.WarningColor;
        }

        if (report.OutOfSeeds.Count > 0)
        {
            var names = new List<string>();
            foreach (var cropId in report.OutOfSeeds) names.Add(itemName(cropId));
            var outOfSeeds = CreateLabel(content, $"모종이 떨어져서 멈춘 작물\n{string.Join(", ", names)}");
            outOfSeeds.color = style.WarningColor;
        }

        if (!string.IsNullOrEmpty(report.NextGoal)) CreateLabel(content, $"다음 할 일 · {report.NextGoal}");

        CreateButton(content, "확인", () => CloseModal(modal), kind: ButtonKind.Primary);
    }

    // Farm NPC: one row per unlocked slot, each holding the crop that grows
    // there while the game is closed.
    public void ShowOfflineFarmPrompt()
    {
        CloseAllModals();
        var modal = OpenModal("오프라인 농사", out var content);
        CreateLabel(content, "게임을 끈 동안 여기 등록한 작물을 수확해요.\n" +
                             $"밭에 심은 작물과는 따로 자라고, 온라인보다 {game.OfflineSlowdown:0.#}배 느려요.");

        int limit = game.OfflineFarmRegistrationLimit;
        for (int i = 0; i < limit; i++)
        {
            int index = i;
            var row = CreateRow(content, ButtonHeight);
            var label = CreateLabel(row, DescribeOfflineCrop(index), TextAnchor.MiddleLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateButton(row, "변경", () => ShowOfflineCropPicker(index), width: 200f, kind: ButtonKind.Secondary);
        }

        CreateButton(content, "닫기", () => CloseModal(modal), kind: ButtonKind.Secondary);
    }

    private string DescribeOfflineCrop(int index)
    {
        var crop = game.GetOfflineCrop(index);
        return $"칸 {index + 1} : {(crop != null ? SeedLabel(crop) : "비어 있음")}";
    }

    private string SeedLabel(CropDefinition crop) =>
        game.FarmService.IsUnlimitedSeed(crop.cropId)
            ? crop.displayName
            : $"{crop.displayName} (남은 모종 {game.FarmService.GetSeedCount(crop.cropId)})";

    private void ShowOfflineCropPicker(int index)
    {
        CloseAllModals();
        OpenModal($"칸 {index + 1}에 등록할 작물", out var content);

        foreach (var crop in game.Crops)
        {
            if (crop == null) continue;

            var cropForClick = crop;
            CreateButton(content, SeedLabel(crop), () =>
            {
                game.SetOfflineCrop(index, cropForClick.cropId);
                ShowOfflineFarmPrompt();
            });
        }

        if (game.GetOfflineCrop(index) != null)
        {
            CreateButton(content, "등록 해제", () =>
            {
                game.SetOfflineCrop(index, null);
                ShowOfflineFarmPrompt();
            }, kind: ButtonKind.Secondary);
        }
        CreateButton(content, "취소", ShowOfflineFarmPrompt, kind: ButtonKind.Secondary);
    }

    // "도깨비 해달이 다녀갔어요!" per otter, first visit order, with a count
    // when the same otter came more than once.
    private static string DescribeOtterVisits(List<string> visits)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var name in visits)
        {
            if (!counts.ContainsKey(name))
            {
                order.Add(name);
                counts[name] = 0;
            }
            counts[name]++;
        }

        var lines = new List<string>();
        foreach (var name in order)
        {
            string times = counts[name] > 1 ? $" ({counts[name]}번)" : "";
            lines.Add($"{WithSubjectParticle(name)} 다녀갔어요!{times}");
        }
        return string.Join("\n", lines);
    }

    private static string ListStacks(List<ItemStack> stacks, Func<string, string> itemName)
    {
        var lines = new List<string>();
        foreach (var stack in stacks) lines.Add($"{itemName(stack.itemId)} ×{stack.quantity}");
        return string.Join("\n", lines);
    }

    private static string FormatDuration(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalDays >= 1) return $"{(int)time.TotalDays}일 {time.Hours}시간";
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}시간 {time.Minutes}분";
        if (time.TotalMinutes >= 1) return $"{time.Minutes}분";
        return $"{time.Seconds}초";
    }

    // --------------------------------------------------------------- fishing

    public void ShowRodUpgradeButton()
    {
        if (rodUpgradeButton != null) return;

        rodUpgradeButton = CreateButton(screenLayer, "낚싯대 강화", ShowRodUpgradePrompt, kind: ButtonKind.Feature);
        var rect = (RectTransform)rodUpgradeButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(340f, ButtonHeight);
        rect.anchoredPosition = new Vector2(40f, FeatureButtonBottom);
    }

    private void ShowRodUpgradePrompt()
    {
        CloseAllModals();
        var fishing = game.FishingService;
        var modal = OpenModal("낚싯대 강화", out var content);

        var infoLabel = CreateLabel(content, "");
        var balanceLabel = CreateLabel(content, "");
        var row = CreateRow(content, ButtonHeight);
        var upgradeButton = CreateButton(row, "강화", () =>
        {
            if (!game.TryUpgradeRod()) ShowAlert("코인이 부족해요!");
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, "닫기", () => CloseModal(modal), flexible: true, kind: ButtonKind.Secondary);

        // Stays open after an upgrade so the new level/chance shows right away.
        SetRefresher(modal, () =>
        {
            int level = fishing.RodLevel;
            string current = $"현재 Lv.{level} (물고기 확률 {Percent(fishing.FishChanceAt(level))})";
            infoLabel.text = fishing.CanUpgradeRod
                ? $"{current}\n다음 Lv.{level + 1} (물고기 확률 {Percent(fishing.FishChanceAt(level + 1))})\n" +
                  $"강화 비용 : {fishing.NextRodUpgradeCost} 코인"
                : $"{current}\n최대 레벨이에요!";
            balanceLabel.text = $"보유 코인 : {game.CoinBalance}";
            upgradeButton.interactable = fishing.CanUpgradeRod;
        });
    }

    // ---------------------------------------------------------------- mining

    public void ShowPickaxeUpgradeButton()
    {
        if (pickaxeUpgradeButton != null) return;

        pickaxeUpgradeButton = CreateButton(screenLayer, "곡괭이 강화", ShowPickaxeUpgradePrompt, kind: ButtonKind.Feature);
        var rect = (RectTransform)pickaxeUpgradeButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(340f, ButtonHeight);
        rect.anchoredPosition = new Vector2(40f, FeatureButtonBottom);
    }

    private void ShowPickaxeUpgradePrompt()
    {
        CloseAllModals();
        var mining = game.MiningService;
        var modal = OpenModal("곡괭이 강화", out var content);

        var infoLabel = CreateLabel(content, "");
        var balanceLabel = CreateLabel(content, "");
        var row = CreateRow(content, ButtonHeight);
        var upgradeButton = CreateButton(row, "강화", () =>
        {
            if (!game.TryUpgradePickaxe()) ShowAlert("코인이 부족해요!");
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, "닫기", () => CloseModal(modal), flexible: true, kind: ButtonKind.Secondary);

        // Stays open after an upgrade so the new level/chance shows right away.
        SetRefresher(modal, () =>
        {
            int level = mining.PickaxeLevel;
            string current = $"현재 Lv.{level} (다이아몬드 확률 {Percent(mining.DiamondChanceAt(level))})";
            infoLabel.text = mining.CanUpgradePickaxe
                ? $"{current}\n다음 Lv.{level + 1} (다이아몬드 확률 {Percent(mining.DiamondChanceAt(level + 1))})\n" +
                  $"강화 비용 : {mining.NextPickaxeUpgradeCost} 코인"
                : $"{current}\n최대 레벨이에요!";
            balanceLabel.text = $"보유 코인 : {game.CoinBalance}";
            upgradeButton.interactable = mining.CanUpgradePickaxe;
        });
    }

    // ------------------------------------------------------------------ farm

    // Same corner button and prompt as the pickaxe/rod upgrade.
    public void ShowFarmUpgradeButton()
    {
        if (farmUpgradeButton != null) return;

        farmUpgradeButton = CreateButton(screenLayer, "밭 강화", ShowFarmUpgradePrompt, kind: ButtonKind.Feature);
        var rect = (RectTransform)farmUpgradeButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(340f, ButtonHeight);
        rect.anchoredPosition = new Vector2(40f, FeatureButtonBottom);
    }

    private void ShowFarmUpgradePrompt()
    {
        CloseAllModals();
        var farm = game.FarmService;
        var modal = OpenModal("밭 강화", out var content);

        var infoLabel = CreateLabel(content, "");
        var balanceLabel = CreateLabel(content, "");
        var row = CreateRow(content, ButtonHeight);
        var upgradeButton = CreateButton(row, "강화", () =>
        {
            if (!game.TryUpgradeFarm()) ShowAlert("코인이 부족해요!");
        }, flexible: true, kind: ButtonKind.Primary);
        CreateButton(row, "닫기", () => CloseModal(modal), flexible: true, kind: ButtonKind.Secondary);

        // Stays open after an upgrade so the new level/grow time shows right away.
        SetRefresher(modal, () =>
        {
            int level = farm.FarmLevel;
            string current = $"현재 Lv.{level} (자라는 시간 {Percent(farm.DurationMultiplierAt(level))})";
            string offline = level + 1 == farm.OfflineUnlockLevel ? "\n강화하면 게임을 꺼 둔 동안에도 농사를 지어요." : "";
            infoLabel.text = farm.CanUpgrade
                ? $"{current}\n다음 Lv.{level + 1} (자라는 시간 {Percent(farm.DurationMultiplierAt(level + 1))}){offline}\n" +
                  $"강화 비용 : {farm.NextUpgradeCost} 코인"
                : $"{current}\n최대 레벨이에요!";
            balanceLabel.text = $"보유 코인 : {game.CoinBalance}";
            upgradeButton.interactable = farm.CanUpgrade;
        });
    }

    private static string Percent(float chance) => $"{Mathf.RoundToInt(chance * 100f)}%";

    // ---------------------------------------------------------- modal plumbing

    // Each modal is a full-screen dim blocker (eats clicks meant for anything
    // underneath) with a centered, auto-height panel on top.
    private GameObject OpenModal(string title, out RectTransform content)
    {
        var blocker = new GameObject("Modal", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(transform, false);
        Stretch((RectTransform)blocker.transform);
        blocker.GetComponent<Image>().color = DimColor;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(blocker.transform, false);
        ApplySprite(panel.GetComponent<Image>(), style.Panel, PanelColor);

        content = (RectTransform)panel.transform;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(PanelWidth, 0f);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        // The panel sprite's 9-slice border is 72 (bottom 80): keep text clear of it
        layout.padding = new RectOffset(64, 64, 60, 72);
        layout.spacing = 22f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleLabel = CreateLabel(content, title);
        if (style.TitleFont != null) titleLabel.font = style.TitleFont;
        titleLabel.fontSize = TitleFontSize;
        titleLabel.color = style.TextColor;

        modals.Add(blocker);
        return blocker;
    }

    // Re-run every frame while the modal is open (live counts/balances), and
    // once right away so the first frame isn't blank.
    private void SetRefresher(GameObject modal, Action refresh)
    {
        modalRefreshers[modal] = refresh;
        refresh();
    }

    private void CloseModal(GameObject modal)
    {
        modals.Remove(modal);
        modalRefreshers.Remove(modal);
        Destroy(modal);
    }

    private void CloseAllModals()
    {
        foreach (var modal in modals) Destroy(modal);
        modals.Clear();
        modalRefreshers.Clear();
        openAlertMessages.Clear();
    }

    // ------------------------------------------------------------ primitives

    private TextMeshProUGUI CreateLabel(Transform parent, string text, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();
        if (style.BodyFont != null) label.font = style.BodyFont;
        label.fontSize = BodyFontSize;
        label.alignment = alignment == TextAnchor.MiddleLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        label.color = style.BodyColor;
        label.lineSpacing = 8f;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private Button CreateButton(Transform parent, string text, Action onClick, float width = -1f, bool flexible = false,
        ButtonKind kind = ButtonKind.Option)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        ApplySprite(image, ButtonSprite(kind), ButtonColor);

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = ButtonHeight;
        if (width > 0f) layoutElement.preferredWidth = width;
        if (flexible) layoutElement.flexibleWidth = 1f;

        var label = CreateLabel(go.transform, text);
        if (style.TitleFont != null) label.font = style.TitleFont;
        label.color = kind == ButtonKind.Primary && style.PrimaryButton != null ? style.PrimaryLabelColor : style.TextColor;
        var labelRect = (RectTransform)label.transform;
        Stretch(labelRect);
        // Lift the text off the button's raised bottom lip
        if (image.sprite != null) labelRect.offsetMin = new Vector2(0f, 12f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    private Sprite ButtonSprite(ButtonKind kind)
    {
        switch (kind)
        {
            case ButtonKind.Primary: return style.PrimaryButton;
            case ButtonKind.Secondary: return style.SecondaryButton;
            case ButtonKind.Feature: return style.FeatureButton;
            default: return style.OptionButton;
        }
    }

    // A 9-sliced sprite when the style has one, otherwise a flat color.
    private static void ApplySprite(Image image, Sprite sprite, Color fallback)
    {
        if (sprite == null)
        {
            image.color = fallback;
            return;
        }
        image.sprite = sprite;
        image.color = Color.white;
        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
    }

    private RectTransform CreateRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Picks 은/는 from whether the last Hangul syllable has a final consonant.
    private static string WithTopicParticle(string word) =>
        string.IsNullOrEmpty(word) ? word : word + (HasFinalConsonant(word) ? "은" : "는");

    // Same for 이/가.
    private static string WithSubjectParticle(string word) =>
        string.IsNullOrEmpty(word) ? word : word + (HasFinalConsonant(word) ? "이" : "가");

    private static bool HasFinalConsonant(string word)
    {
        char last = word[word.Length - 1];
        return last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
    }
}
