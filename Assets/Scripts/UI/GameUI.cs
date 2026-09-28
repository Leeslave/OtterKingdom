using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Zone-scene uGUI (farm and fishing), built entirely at runtime (same
// approach as CurrencyHud) so no scene/prefab wiring is needed: the always-on
// "판매" button, the fishing scene's "낚싯대 강화" button, and every modal
// popup (crop selection, plot unlock, crop change, confirm, alerts, rod
// upgrade, item sale). Holds no game rules itself — every action goes back
// through GameManager. Replace with prefab-based views once real UI art exists.
public class GameUI : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const float PanelWidth = 900f;
    private const int TitleFontSize = 48;
    private const int BodyFontSize = 40;
    private const float ButtonHeight = 110f;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color PanelColor = new Color(0.97f, 0.94f, 0.86f, 1f);
    private static readonly Color ButtonColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TextColor = new Color(0.2f, 0.15f, 0.1f, 1f);
    private static readonly Color WarningColor = new Color(0.8f, 0.3f, 0.1f, 1f);

    private GameManager game;
    private Font font;
    private Button rodUpgradeButton;

    private readonly List<GameObject> modals = new List<GameObject>();
    private readonly Dictionary<GameObject, Action> modalRefreshers = new Dictionary<GameObject, Action>();
    private readonly HashSet<string> openAlertMessages = new HashSet<string>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    public bool IsModalOpen => modals.Count > 0;

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
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above CurrencyHud (100)

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        CreateSellButton();
    }

    private void Update()
    {
        foreach (var refresh in modalRefreshers.Values) refresh();
    }

    // True if a screen point should NOT reach the world (slots/plots): any
    // modal is open, or the point is over some uGUI element (e.g. the sell
    // button or the currency HUD).
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

        CreateButton(content, "취소", () => CloseModal(modal));
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
        }, flexible: true);
        CreateButton(row, "취소", () => CloseModal(modal), flexible: true);
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
            warning.color = WarningColor;
        }

        var row = CreateRow(content, ButtonHeight);
        CreateButton(row, "예", () =>
        {
            game.DiscardSlot(plotIndex, slotIndex);
            CloseModal(modal);
        }, flexible: true);
        CreateButton(row, "아니오", () => CloseModal(modal), flexible: true);
    }

    public void ShowConfirm(string title, string message, Action onYes)
    {
        CloseAllModals();
        var modal = OpenModal(title, out var content);
        CreateLabel(content, message);

        var row = CreateRow(content, ButtonHeight);
        CreateButton(row, "예", () =>
        {
            CloseModal(modal);
            onYes?.Invoke();
        }, flexible: true);
        CreateButton(row, "아니오", () => CloseModal(modal), flexible: true);
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
        });
    }

    // --------------------------------------------------------------- fishing

    public void ShowRodUpgradeButton()
    {
        if (rodUpgradeButton != null) return;

        rodUpgradeButton = CreateButton(transform, "낚싯대 강화", ShowRodUpgradePrompt);
        var rect = (RectTransform)rodUpgradeButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(340f, ButtonHeight);
        rect.anchoredPosition = new Vector2(40f, 40f);
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
        }, flexible: true);
        CreateButton(row, "닫기", () => CloseModal(modal), flexible: true);

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

    private static string Percent(float chance) => $"{Mathf.RoundToInt(chance * 100f)}%";

    // ------------------------------------------------------------------ sale

    private void CreateSellButton()
    {
        var button = CreateButton(transform, "판매", ShowSellList);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(300f, ButtonHeight);
        rect.anchoredPosition = new Vector2(-40f, 40f);
    }

    private void ShowSellList()
    {
        CloseAllModals();
        var modal = OpenModal("[보유 아이템]", out var content);

        var refreshers = new List<Action>();
        foreach (var item in game.SellableItems)
        {
            var itemForClick = item;
            var row = CreateRow(content, ButtonHeight);
            var label = CreateLabel(row, "", TextAnchor.MiddleLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var sellButton = CreateButton(row, "판매", () => ShowSellDetail(itemForClick), width: 200f);

            refreshers.Add(() =>
            {
                int owned = game.GetItemQuantity(itemForClick.itemId);
                label.text = $"{itemForClick.displayName} : {owned}개";
                sellButton.interactable = owned > 0;
            });
        }
        SetRefresher(modal, () => { foreach (var r in refreshers) r(); });

        CreateButton(content, "닫기", () => CloseModal(modal));
    }

    private void ShowSellDetail(SellableItem item)
    {
        CloseAllModals();
        var modal = OpenModal("[판매]", out var content);

        int amount = 1;
        int Owned() => game.GetItemQuantity(item.itemId);
        int Clamp(int value) => Mathf.Clamp(value, 1, Mathf.Max(1, Owned()));

        CreateLabel(content, $"{item.displayName} : {item.sellPrice}원");
        var ownedLabel = CreateLabel(content, "");

        var row = CreateRow(content, ButtonHeight);
        var caption = CreateLabel(row, "판매할 개수", TextAnchor.MiddleLeft);
        caption.gameObject.AddComponent<LayoutElement>().preferredWidth = 240f;
        var downButton = CreateButton(row, "▼", null, width: 100f);
        var input = CreateIntegerInput(row);
        var upButton = CreateButton(row, "▲", null, width: 100f);
        var allButton = CreateButton(row, "전체", null, width: 140f);

        var totalLabel = CreateLabel(content, "");

        void SetAmount(int value)
        {
            amount = Clamp(value);
            input.SetTextWithoutNotify(amount.ToString());
        }

        SetAmount(1);
        downButton.onClick.AddListener(() => SetAmount(amount - 1));
        upButton.onClick.AddListener(() => SetAmount(amount + 1));
        allButton.onClick.AddListener(() => SetAmount(Owned()));
        // While typing, track the (clamped) value for the total without
        // fighting the caret; snap the text itself once editing ends.
        input.onValueChanged.AddListener(text => amount = Clamp(int.TryParse(text, out var v) ? v : 1));
        input.onEndEdit.AddListener(_ => SetAmount(amount));

        SetRefresher(modal, () =>
        {
            ownedLabel.text = $"현재 보유개수 : {Owned()}개";
            totalLabel.text = $"합계 : {Clamp(amount) * item.sellPrice}원";
        });

        var buttons = CreateRow(content, ButtonHeight);
        CreateButton(buttons, "판매", () =>
        {
            if (Owned() <= 0)
            {
                ShowAlert("판매할 아이템이 없어요!");
                return;
            }
            game.SellItem(item, Clamp(amount));
            ShowSellList();
        }, flexible: true);
        CreateButton(buttons, "취소", ShowSellList, flexible: true);
    }

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
        panel.GetComponent<Image>().color = PanelColor;

        content = (RectTransform)panel.transform;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(PanelWidth, 0f);

        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 20f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleLabel = CreateLabel(content, title);
        titleLabel.fontSize = TitleFontSize;
        titleLabel.fontStyle = FontStyle.Bold;

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

    private Text CreateLabel(Transform parent, string text, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<Text>();
        label.font = font;
        label.fontSize = BodyFontSize;
        label.alignment = alignment;
        label.color = TextColor;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private Button CreateButton(Transform parent, string text, Action onClick, float width = -1f, bool flexible = false)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = ButtonColor;

        var layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = ButtonHeight;
        if (width > 0f) layoutElement.preferredWidth = width;
        if (flexible) layoutElement.flexibleWidth = 1f;

        var label = CreateLabel(go.transform, text);
        Stretch((RectTransform)label.transform);

        var button = go.GetComponent<Button>();
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
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

    // Built inactive and switched on only once textViewport/textComponent are
    // assigned, so TMP_InputField's OnEnable sees a fully wired component.
    private TMP_InputField CreateIntegerInput(Transform parent)
    {
        var go = new GameObject("AmountInput", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var areaRect = (RectTransform)area.transform;
        Stretch(areaRect);
        areaRect.offsetMin = new Vector2(10f, 6f);
        areaRect.offsetMax = new Vector2(-10f, -6f);

        var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(area.transform, false);
        Stretch((RectTransform)textGO.transform);
        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.fontSize = BodyFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = areaRect;
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 6;

        go.SetActive(true);
        return input;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Picks 은/는 from whether the last Hangul syllable has a final consonant.
    private static string WithTopicParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        char last = word[word.Length - 1];
        bool hasFinal = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0;
        return word + (hasFinal ? "은" : "는");
    }
}
