using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GlobalUISetup;

/// <summary>
/// 가방 원본 UI(InventoryTestScene의 InventoryUISource)에 판매 버튼과 판매 팝업을 넣는다. 이미 있으면 건드리지 않음.
/// GlobalUISetup.Run이 원본을 복제하기 전에 호출한다.
/// </summary>
public static class SellUISetup
{
    private const float SellButtonWidth = 180f;
    private const float SellButtonGap = 16f;

    public static void EnsureSellUI(GameObject sourceCanvas)
    {
        var detail = sourceCanvas.GetComponentInChildren<ItemDetailView>(true);
        var presenter = sourceCanvas.GetComponentInChildren<InventoryPresenter>(true);

        var detailSo = new SerializedObject(detail);
        if (detailSo.FindProperty("_sellButton").objectReferenceValue == null)
        {
            var priceBox = (GameObject)detailSo.FindProperty("_priceBox").objectReferenceValue;
            Set(detail, "_sellButton", BuildSellButton((RectTransform)detail.transform, (RectTransform)priceBox.transform));
        }

        var presenterSo = new SerializedObject(presenter);
        if (presenterSo.FindProperty("_sellPopup").objectReferenceValue == null)
        {
            var expand = sourceCanvas.GetComponentInChildren<ExpandPopupView>(true);
            var popup = BuildSellPopup((RectTransform)sourceCanvas.transform);
            popup.transform.SetSiblingIndex(expand.transform.GetSiblingIndex() + 1);
            Set(presenter, "_sellPopup", popup);
        }
    }

    // 가격 줄을 줄이고 그 오른쪽에 판매 버튼 (가격 줄과 같은 높이·위치)
    private static Button BuildSellButton(RectTransform detailPanel, RectTransform priceBox)
    {
        float right = -priceBox.offsetMax.x;
        priceBox.offsetMax = new Vector2(-(right + SellButtonWidth + SellButtonGap), priceBox.offsetMax.y);

        var image = CreateImage("SellButton", detailPanel, LoadSprite(CommonSpriteFolder, "UI_Button_Primary"), true);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, priceBox.pivot.y);
        rect.sizeDelta = new Vector2(SellButtonWidth, priceBox.rect.height > 0 ? priceBox.rect.height : 92);
        rect.anchoredPosition = new Vector2(-right, priceBox.anchoredPosition.y);
        rect.SetSiblingIndex(priceBox.GetSiblingIndex() + 1);

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var label = CreateText("Label", rect, _titleFont, "판매", 40, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8); // 버튼 아래쪽 입체 턱만큼 위로
        return button;
    }

    private static SellPopupView BuildSellPopup(RectTransform canvas)
    {
        var popup = CreateRect("SellPopup", canvas);
        Stretch(popup, 0);

        var dim = CreateImage("Dim", popup, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var panel = CreateImage("Panel", popup, LoadPanelSprite(), true);
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 760));
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var p = panel.rectTransform;

        var title = CreateText("Title", p, _titleFont, "판매하기", 56, Cocoa);
        TopBand(title.rectTransform, 60, 60, 60, 80);

        var icon = CreateImage("Icon", p, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(140, 140));
        icon.preserveAspect = true;

        var nameText = CreateText("Name", p, _titleFont, "아이템", 44, Cocoa);
        TopBand(nameText.rectTransform, 60, 60, 300, 56);

        // [−] 개수 [+] [최대]
        var row = CreateRect("Quantity", p);
        Place(row, new Vector2(0.5f, 1), new Vector2(0, -376), new Vector2(620, 96));
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.spacing = 16;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var minus = BuildIconButton(row, "Minus", "UI_Button_Minus", 96, 96);
        var quantity = CreateText("Count", row, _titleFont, "1", 52, Cocoa);
        Sized(quantity.gameObject, 160, 96);
        var plus = BuildIconButton(row, "Plus", "UI_Button_Plus", 96, 96);
        var max = BuildIconButton(row, "Max", "UI_Button_Secondary", 150, 84);
        var maxLabel = CreateText("Label", max.transform, _titleFont, "최대", 34, Cocoa);
        Stretch(maxLabel.rectTransform, 0);
        maxLabel.rectTransform.offsetMin = new Vector2(0, 6);

        // 받을 금액: [코인] 150 (가운데 정렬로 길이에 맞춰 배치)
        var total = CreateRect("Total", p);
        Place(total, new Vector2(0.5f, 1), new Vector2(0, -492), new Vector2(0, 64));
        var totalLayout = total.gameObject.AddComponent<HorizontalLayoutGroup>();
        totalLayout.childAlignment = TextAnchor.MiddleCenter;
        totalLayout.spacing = 12;
        totalLayout.childControlWidth = true;
        totalLayout.childControlHeight = true;
        totalLayout.childForceExpandWidth = false;
        totalLayout.childForceExpandHeight = false;
        total.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var coin = CreateImage("CurrencyIcon", total, null, false);
        coin.preserveAspect = true;
        Sized(coin.gameObject, 56, 56);
        var totalText = CreateText("Amount", total, _titleFont, "0", 48, Cocoa);

        var cancel = BuildTextButton(p, "CancelButton", -170, "취소", LoadSprite(InventorySpriteFolder, "UI_Inventory_Box"));
        var confirm = BuildTextButton(p, "ConfirmButton", 170, "판매", LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Selected"));

        var animator = popup.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", p);
        Set(animator, "_panelGroup", panelGroup);
        Set(animator, "_dimButton", dimButton);

        var view = popup.gameObject.AddComponent<SellPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_icon", icon);
        Set(view, "_nameText", nameText);
        Set(view, "_quantityText", quantity);
        Set(view, "_minusButton", minus);
        Set(view, "_plusButton", plus);
        Set(view, "_maxButton", max);
        Set(view, "_currencyIcon", coin);
        Set(view, "_totalText", totalText);
        Set(view, "_confirmButton", confirm);
        Set(view, "_cancelButton", cancel);

        popup.gameObject.SetActive(false);
        return view;
    }

    private static Button BuildIconButton(Transform parent, string name, string sprite, float width, float height)
    {
        var image = CreateImage(name, parent, LoadSprite(CommonSpriteFolder, sprite), true);
        image.preserveAspect = image.type != Image.Type.Sliced;
        Sized(image.gameObject, width, height);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Button BuildTextButton(RectTransform panel, string name, float x, string text, Sprite sprite)
    {
        var image = CreateImage(name, panel, sprite, true);
        Place(image.rectTransform, new Vector2(0.5f, 0), new Vector2(x, 64), new Vector2(300, 110));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = CreateText("Label", image.rectTransform, _titleFont, text, 44, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 12);
        return button;
    }

    private static void Sized(GameObject go, float width, float height)
    {
        var element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
    }

    private static void TopBand(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
