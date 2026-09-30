using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 재화 충전(골드 충전 · 조개 충전 · 구매 확인 · 조개 부족)의 데이터, 카드 프리팹, 화면을 만든다. GlobalUISetup이 함께 호출한다.
/// 1080×1920 기준, 충전 시안(Tools/UIGen/currency_mockup.py) 수치를 옮김. 상품 수량과 가격은 시안용 예시.
/// 이미 있는 상품 에셋은 사람이 고친 값을 지키기 위해 덮어쓰지 않고, 비어 있는 그림만 채운다.
/// </summary>
public static class CurrencyShopSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Currency";
    private const string PackFolder = DataFolder + "/Packs";
    internal const string CatalogPath = DataFolder + "/CurrencyShopCatalog.asset";
    private const string GoldCardPrefabPath = "Assets/Prefab/Currency/GoldPackCard.prefab";
    private const string GemCardPrefabPath = "Assets/Prefab/Currency/GemPackCard.prefab";
    private const string PackIconFolder = "Assets/Art/UI/Currency/Packs";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    private static readonly Color Red = new Color32(0xE5, 0x48, 0x4D, 0xFF);

    // (에셋, ID, 기본, 보너스, 가격, 더미 개수, 뱃지) — 골드는 조개로, 조개는 현금(원)으로
    private static readonly (string asset, string id, int baseAmount, int bonus, int price, int pile, string highlight)[] GoldPacks =
    {
        ("Gold_1000", "gold_1000", 1000, 0, 10, 1, ""),
        ("Gold_5500", "gold_5500", 5000, 500, 50, 3, ""),
        ("Gold_12000", "gold_12000", 10000, 2000, 100, 6, ""),
        ("Gold_30000", "gold_30000", 20000, 10000, 200, 10, "최고 효율"),
    };

    private static readonly (string asset, string id, int baseAmount, int bonus, int price, int pile, string highlight)[] GemPacks =
    {
        ("Gem_60", "gem_60", 60, 0, 1200, 1, "첫 구매 2배"),
        ("Gem_330", "gem_330", 300, 30, 5900, 3, ""),
        ("Gem_700", "gem_700", 600, 100, 11000, 3, ""),
        ("Gem_1500", "gem_1500", 1200, 300, 22000, 6, ""),
        ("Gem_3300", "gem_3300", 2500, 800, 44000, 6, ""),
        ("Gem_7000", "gem_7000", 5000, 2000, 89000, 10, "최고 효율"),
    };

    #region 데이터

    public static void CreateData()
    {
        EnsureFolder(PackFolder);
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);

        var goldPacks = CreatePacks(GoldPacks, gold, gem, "Gold");
        var gemPacks = CreatePacks(GemPacks, gem, null, "Gem");

        var (catalog, _) = LoadOrCreate<CurrencyShopCatalog>(CatalogPath);
        var so = new SerializedObject(catalog);
        SetList(so.FindProperty("_goldPacks"), goldPacks);
        SetList(so.FindProperty("_gemPacks"), gemPacks);
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    private static List<CurrencyPack> CreatePacks(
        (string asset, string id, int baseAmount, int bonus, int price, int pile, string highlight)[] packs,
        Currency reward, Currency priceCurrency, string iconName)
    {
        var result = new List<CurrencyPack>();
        for (int i = 0; i < packs.Length; i++)
        {
            var p = packs[i];
            var (pack, isNew) = LoadOrCreate<CurrencyPack>($"{PackFolder}/{p.asset}.asset");
            var so = new SerializedObject(pack);
            if (isNew)
            {
                so.FindProperty("_packId").stringValue = p.id;
                so.FindProperty("_sortOrder").intValue = i;
                so.FindProperty("_reward").objectReferenceValue = reward;
                so.FindProperty("_baseAmount").intValue = p.baseAmount;
                so.FindProperty("_bonusAmount").intValue = p.bonus;
                so.FindProperty("_priceCurrency").objectReferenceValue = priceCurrency;
                so.FindProperty("_price").intValue = p.price;
                so.FindProperty("_highlight").stringValue = p.highlight;
            }
            FillIfEmpty(so, "_icon", ImportSprite(PackIconFolder, $"ICON_{iconName}Pack_{p.pile}"));
            so.ApplyModifiedPropertiesWithoutUndo();
            result.Add(pack);
        }
        return result;
    }

    #endregion

    #region 카드 프리팹

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(GoldCardPrefabPath).Replace('\\', '/'));
        // 골드: 조개로 사는 큰 카드 (설명 줄 있음) / 조개: 현금으로 사는 낮은 카드 (보너스는 +% 뱃지로만)
        BuildCardPrefab(GoldCardPrefabPath, 470, 220, true, "UI_Button_Shell", Cocoa);
        BuildCardPrefab(GemCardPrefabPath, 350, 130, false, "UI_Button_Primary", Color.white);
    }

    private static void BuildCardPrefab(string path, float height, float wellHeight, bool showDetail, string priceSprite, Color priceTextColor)
    {
        const float width = 420;
        var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(width, height);
        var background = root.AddComponent<Image>();
        background.sprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        var well = CreateImage("Well", rect, LoadSprite(CommonSpriteFolder, "UI_Box_Inset"), false);
        Place(well.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(width - 80, wellHeight));
        float iconSize = wellHeight - 20;
        var icon = CreateImage("Icon", rect, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(iconSize, iconSize));
        icon.preserveAspect = true;

        var name = CreateText("Name", rect, _titleFont, "골드 1,000", 38, Cocoa);
        TopBand(name.rectTransform, 16, 16, wellHeight + 48, 50);

        var detail = CreateText("Detail", rect, _bodyFont, "기본 5,000 + 500", 22, LightBrown);
        TopBand(detail.rectTransform, 16, 16, wellHeight + 98, 32);

        var bonus = CreateImage("BonusTag", rect, LoadSprite(CommonSpriteFolder, "UI_Tag_Highlight"), false);
        Place(bonus.rectTransform, new Vector2(1, 1), new Vector2(-20, -14), new Vector2(150, 50));
        var bonusText = CreateText("Label", bonus.rectTransform, _titleFont, "+10%", 26, Color.white);
        Stretch(bonusText.rectTransform, 0);
        bonusText.rectTransform.offsetMin = new Vector2(0, 4);

        var highlight = CreateImage("HighlightTag", rect, ImportSprite(CommonSpriteFolder, "UI_Tag_Level"), false);
        Place(highlight.rectTransform, new Vector2(0, 1), new Vector2(20, 18), new Vector2(170, 50));
        var highlightText = CreateText("Label", highlight.rectTransform, _titleFont, "최고 효율", 24, Cocoa);
        Stretch(highlightText.rectTransform, 0);
        highlightText.rectTransform.offsetMin = new Vector2(0, 4);

        var (priceButton, priceIcon, priceText) = BuildPriceButton(rect, priceSprite, priceTextColor, 34);
        Place(priceButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(width - 80, 86));

        var view = root.AddComponent<CurrencyPackCardView>();
        Set(view, "_icon", icon);
        Set(view, "_nameText", name);
        Set(view, "_detailText", detail);
        Set(view, "_bonusTag", bonus.gameObject);
        Set(view, "_bonusText", bonusText);
        Set(view, "_highlightTag", highlight.gameObject);
        Set(view, "_highlightText", highlightText);
        Set(view, "_priceButton", priceButton);
        Set(view, "_priceIcon", priceIcon);
        Set(view, "_priceText", priceText);
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("_showDetail").boolValue = showDetail;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // 가격 버튼: [아이콘 + 숫자]를 가운데 정렬 (현금 상품은 아이콘을 숨겨 숫자만)
    private static (Button button, Image icon, TextMeshProUGUI text) BuildPriceButton(Transform parent, string sprite, Color textColor, float fontSize)
    {
        var image = CreateImage("PriceButton", parent, LoadSprite(CommonSpriteFolder, sprite), true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var content = CreateRect("Content", image.rectTransform);
        Stretch(content, 0);
        content.offsetMin = new Vector2(0, 8); // 버튼 아래쪽 입체 턱만큼 위로
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var icon = CreateImage("Icon", content, null, false);
        icon.preserveAspect = true;
        var iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = fontSize + 12;
        iconSize.preferredHeight = fontSize + 12;

        var text = CreateText("Price", content, _titleFont, "10", fontSize, textColor);
        return (button, icon, text);
    }

    #endregion

    #region 화면

    public static (CurrencyShopView goldShop, CurrencyShopView gemShop, PurchaseConfirmPopupView confirm, CurrencyShortagePopupView shortage)
        BuildScreens(RectTransform canvas)
    {
        // 씬을 연 뒤 호출되므로 에셋은 경로로 다시 불러온다
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);

        // 그리는 순서: 골드 충전 → 조개 충전 (조개 부족에서 넘어가면 위에 뜸) → 구매 확인 → 조개 부족
        var goldShop = BuildShop(canvas, "GoldShop", "골드 충전", ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"),
            -15, 1290, 500 - 470, GoldCardPrefabPath, new[] { gold, gem });
        AddFooter(goldShop.panel, "골드는 조개로 살 수 있어요", 28, LightBrown, 30);

        var gemShop = BuildShop(canvas, "GemShop", "조개 충전", LoadSprite(CommonSpriteFolder, "UI_Ribbon_Lav"),
            -60, 1440, 380 - 350, GemCardPrefabPath, new[] { gem });
        AddFooter(gemShop.panel, "결제 금액은 스토어 계정으로 청구돼요.", 24, LightBrown, 78);
        AddFooter(gemShop.panel, "<u>구매 안내  ·  환불 정책</u>", 26, Body, 34);

        var confirm = BuildConfirm(canvas);
        var shortage = BuildShortage(canvas);
        return (goldShop.view, gemShop.view, confirm, shortage);
    }

    private static (CurrencyShopView view, RectTransform panel) BuildShop(RectTransform canvas, string name, string title, Sprite ribbon,
        float centerY, float height, float rowGap, string cardPrefabPath, Currency[] balances)
    {
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cardPrefabPath).GetComponent<CurrencyPackCardView>();
        var (screen, panel, animator) = BuildModal(canvas, name, title, ribbon, new Vector2(960, height), centerY, 440, 112, 48);

        var close = CreateImage("CloseButton", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(20, 34), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        BuildBalanceRow(panel, balances);

        var cardSize = cardPrefab.GetComponent<RectTransform>().sizeDelta;
        var cards = CreateRect("Cards", panel);
        TopBand(cards, 40, 40, 210, height - 210 - 120);
        var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = new Vector2(40, rowGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        // 잠깐 뜨는 안내: 카드 목록 아래쪽 가운데
        var notice = CreateImage("Notice", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(notice.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(620, 92));
        var noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f; // 안내가 있을 때만 잠깐 보임
        noticeGroup.blocksRaycasts = false;
        noticeGroup.interactable = false;
        var noticeText = CreateText("Label", notice.rectTransform, _titleFont, "안내", 32, Cocoa);
        Stretch(noticeText.rectTransform, 0);
        noticeText.rectTransform.offsetMin = new Vector2(0, 8);

        var view = screen.gameObject.AddComponent<CurrencyShopView>();
        Set(view, "_animator", animator);
        Set(view, "_cardPrefab", cardPrefab);
        Set(view, "_cardParent", cards);
        Set(view, "_closeButton", closeButton);
        Set(view, "_notice", noticeGroup);
        Set(view, "_noticeText", noticeText);
        return (view, panel);
    }

    // "보유  (코인) 12,480  (조개) 120"
    private static void BuildBalanceRow(RectTransform panel, Currency[] currencies)
    {
        var row = CreateImage("Balance", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(row.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(880, 92));

        var label = CreateText("Label", row.rectTransform, _titleFont, "보유", 30, LightBrown);
        label.alignment = TextAlignmentOptions.Left;
        Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(34, 4), new Vector2(100, 44));

        for (int i = 0; i < currencies.Length; i++)
        {
            var item = CreateRect(currencies[i].name, row.rectTransform);
            Place(item, new Vector2(0, 0.5f), new Vector2(160 + i * 330, 4), new Vector2(300, 60));

            var icon = CreateImage("Icon", item, currencies[i].Icon, false);
            Place(icon.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 60));
            icon.preserveAspect = true;

            var amount = CreateText("Amount", item, _titleFont, "0", 34, Cocoa);
            amount.alignment = TextAlignmentOptions.Left;
            Place(amount.rectTransform, new Vector2(0, 0.5f), new Vector2(72, 0), new Vector2(228, 50));

            var view = item.gameObject.AddComponent<CurrencyAmountView>();
            Set(view, "_currency", currencies[i]);
            Set(view, "_icon", icon);
            Set(view, "_amountText", amount);
        }
    }

    private static void AddFooter(RectTransform panel, string text, float size, Color color, float bottom)
    {
        var footer = CreateText("Footer", panel, _bodyFont, text, size, color);
        BottomBand(footer.rectTransform, bottom, 40);
    }

    private static PurchaseConfirmPopupView BuildConfirm(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildModal(canvas, "PurchaseConfirm", "구매 확인",
            LoadSprite(CommonSpriteFolder, "UI_Ribbon_Peach"), new Vector2(820, 760), 20, 380, 108, 44);

        var well = CreateImage("Well", panel, LoadSprite(CommonSpriteFolder, "UI_Box_Inset"), false);
        Place(well.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(300, 220));
        var icon = CreateImage("Icon", panel, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(200, 200));
        icon.preserveAspect = true;

        var priceLine = CreateText("PriceLine", panel, _bodyFont, "조개 50개로", 38, Body);
        TopBand(priceLine.rectTransform, 40, 40, 344, 52);
        var question = CreateText("Question", panel, _titleFont, "골드 5,500을 살까요?", 40, Cocoa);
        TopBand(question.rectTransform, 40, 40, 396, 56);

        var balance = CreateImage("Balance", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        balance.pixelsPerUnitMultiplier = 1.3f; // 줄이 낮아서 테두리를 조금 얇게
        Place(balance.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(620, 80));
        var balanceIcon = CreateImage("Icon", balance.rectTransform, null, false);
        Place(balanceIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 4), new Vector2(48, 48));
        balanceIcon.preserveAspect = true;
        // → 기호는 제목 폰트(Cafe24)에 없어 본문 폰트로
        var balanceText = CreateText("Amount", balance.rectTransform, _bodyFont, "120  →  70", 32, Cocoa);
        balanceText.alignment = TextAlignmentOptions.Left;
        Place(balanceText.rectTransform, new Vector2(0, 0.5f), new Vector2(104, 4), new Vector2(300, 50));
        var balanceLabel = CreateText("Label", balance.rectTransform, _titleFont, "남는 조개", 26, LightBrown);
        balanceLabel.alignment = TextAlignmentOptions.Right;
        Place(balanceLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(-30, 4), new Vector2(200, 44));

        var cancel = BuildTextButton(panel, "CancelButton", "UI_Button_Secondary", "취소", 38, LightBrown);
        Place(cancel.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(60, -610), new Vector2(320, 100));

        var (confirmButton, confirmIcon, confirmText) = BuildPriceButton(panel, "UI_Button_Shell", Cocoa, 40);
        Place(confirmButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-60, -610), new Vector2(320, 100));

        var view = screen.gameObject.AddComponent<PurchaseConfirmPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_icon", icon);
        Set(view, "_priceLine", priceLine);
        Set(view, "_questionLine", question);
        Set(view, "_balanceIcon", balanceIcon);
        Set(view, "_balanceText", balanceText);
        Set(view, "_balanceLabel", balanceLabel);
        Set(view, "_cancelButton", cancel);
        Set(view, "_confirmButton", confirmButton);
        Set(view, "_confirmIcon", confirmIcon);
        Set(view, "_confirmText", confirmText);
        return view;
    }

    private static CurrencyShortagePopupView BuildShortage(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildModal(canvas, "CurrencyShortage", "조개 부족",
            LoadSprite(CommonSpriteFolder, "UI_Ribbon_Peach"), new Vector2(820, 720), 20, 380, 108, 44);

        var well = CreateImage("Well", panel, LoadSprite(CommonSpriteFolder, "UI_Box_Inset"), false);
        Place(well.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(260, 200));
        var icon = CreateImage("Icon", panel, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(170, 170));
        icon.preserveAspect = true;

        var title = CreateText("Title", panel, _titleFont, "조개가 부족해요", 42, Cocoa);
        TopBand(title.rectTransform, 40, 40, 328, 56);
        var detail = CreateText("Detail", panel, _bodyFont, "필요 200  ·  보유 120", 32, Body);
        TopBand(detail.rectTransform, 40, 40, 392, 48);

        var chip = CreateImage("Lack", panel, LoadSprite(CommonSpriteFolder, "UI_Tag_Category"), false);
        chip.color = new Color(1f, 0.89f, 0.88f); // 크림 태그에 곱해 연분홍
        Place(chip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -458), new Vector2(220, 60));
        var lack = CreateText("Label", chip.rectTransform, _titleFont, "80개 부족", 30, Red);
        Stretch(lack.rectTransform, 0);
        lack.rectTransform.offsetMin = new Vector2(0, 4);

        var close = BuildTextButton(panel, "CloseButton", "UI_Button_Secondary", "닫기", 38, LightBrown);
        Place(close.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(40, -580), new Vector2(300, 100));

        var charge = BuildTextButton(panel, "ChargeButton", "UI_Button_Primary", "조개 충전하기", 36, Color.white);
        Place(charge.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-40, -580), new Vector2(400, 100));

        var view = screen.gameObject.AddComponent<CurrencyShortagePopupView>();
        Set(view, "_animator", animator);
        Set(view, "_icon", icon);
        Set(view, "_titleText", title);
        Set(view, "_detailText", detail);
        Set(view, "_lackText", lack);
        Set(view, "_closeButton", close);
        Set(view, "_chargeButton", charge);
        Set(view, "_chargeText", charge.GetComponentInChildren<TextMeshProUGUI>());
        return view;
    }

    // 딤 + SafeArea + 크림 패널 + 패널 윗선에 걸친 제목 리본. 닫힌 채로 시작
    internal static (RectTransform screen, RectTransform panel, UIPopupAnimator animator) BuildModal(RectTransform canvas, string name, string title,
        Sprite ribbonSprite, Vector2 size, float centerY, float ribbonWidth, float ribbonHeight, float titleSize)
    {
        var screen = CreateRect(name, canvas);
        Stretch(screen, 0);

        var dim = CreateImage("Dim", screen, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        var panelImage = CreateImage("Panel", safe, LoadPanelSprite(), true);
        panelImage.type = Image.Type.Sliced;
        var panel = panelImage.rectTransform;
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, centerY), size);
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var ribbon = CreateImage("Ribbon", panel, ribbonSprite, false);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, ribbonHeight / 2 - 4), new Vector2(ribbonWidth, ribbonHeight));
        var titleText = CreateText("Title", ribbon.rectTransform, _titleFont, title, titleSize, Cocoa);
        Stretch(titleText.rectTransform, 0);
        titleText.rectTransform.offsetMin = new Vector2(0, 10);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", panel);
        Set(animator, "_panelGroup", panelGroup);
        Set(animator, "_dimButton", dimButton);

        screen.gameObject.SetActive(false);
        return (screen, panel, animator);
    }

    internal static Button BuildTextButton(Transform parent, string name, string sprite, string label, float size, Color color)
    {
        var image = CreateImage(name, parent, LoadSprite(CommonSpriteFolder, sprite), true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = CreateText("Label", image.rectTransform, _titleFont, label, size, color);
        Stretch(text.rectTransform, 0);
        text.rectTransform.offsetMin = new Vector2(0, 8);
        return button;
    }

    #endregion
}
