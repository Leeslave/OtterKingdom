using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 요정 상점의 데이터(상품·카탈로그), 상품 카드 프리팹, 화면(상점 · 구매 팝업), 광장의 요정 NPC를 만든다.
/// 데이터·화면은 GlobalUISetup이 함께 호출하고, 요정 NPC는 PlaceFairyInPlaza(메뉴)로 광장 씬에 넣는다.
/// 1080×1920 기준, 요정상점 시안(Tools/UIGen/hud_mockup.py의 fairy_shop · purchase_popup) 수치를 옮김.
/// 이미 있는 상품 에셋은 사람이 고친 값을 지키기 위해 덮어쓰지 않는다.
/// </summary>
public static class FairyShopSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Shop";
    internal const string CatalogPath = DataFolder + "/ShopCatalog.asset";
    private const string CardPrefabPath = "Assets/Prefab/Shop/ShopProductCard.prefab";
    private const string TabPrefabPath = "Assets/Prefab/Decor/DecorStorageTab.prefab";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string RarityFolder = "Assets/Scriptable Obejects/Inventory/Rarity";
    private const string NpcArtFolder = "Assets/Art/Npc";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    private static readonly Color Red = new Color32(0xE5, 0x48, 0x4D, 0xFF);

    // (에셋, ID, 아이템 경로, 묶음, 조개로 파는지, 가격) — 시안 값. 지렁이 미끼는 미끼 아이템·규칙이 없어 아직 뺌
    private static readonly (string asset, string id, string item, int bundle, bool gem, int price)[] Products =
    {
        ("Seed_Potato", "shop_seed_potato", "Farming/감자 모종", 1, false, 100),
        ("Seed_SweetPotato", "shop_seed_sweetpotato", "Farming/고구마 모종", 1, false, 150),
        ("Seed_Strawberry", "shop_seed_strawberry", "Farming/딸기 모종", 1, false, 300),
        ("Toy_SoccerBall", "shop_toy_soccerball", "Decor/축구공", 1, false, 800),
        ("Toy_Puzzle", "shop_toy_puzzle", "Decor/퍼즐", 1, true, 25),
    };

    // (탭 이름, 분류 에셋)
    private static readonly (string label, string category)[] Tabs =
    {
        ("씨앗", "Seedlings"),
        ("낚시", "Fishing"),
        ("장난감", "Toy"),
    };

    #region 데이터

    public static void CreateData()
    {
        EnsureFolder(DataFolder + "/Products");
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);

        // 희귀 장난감은 조개로 판다는 규칙에 맞춰 퍼즐은 레어 (시안)
        var puzzle = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/Decor/퍼즐.asset");
        var rare = AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/Rare.asset");
        if (puzzle != null && rare != null)
        {
            var so = new SerializedObject(puzzle);
            so.FindProperty("_rarity").objectReferenceValue = rare;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var products = new List<ShopProduct>();
        for (int i = 0; i < Products.Length; i++)
        {
            var p = Products[i];
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{p.item}.asset");
            if (item == null)
            {
                Debug.LogWarning($"[FairyShopSetup] 아이템이 없어 상품을 건너뜁니다: {p.item}");
                continue;
            }

            var (product, isNew) = LoadOrCreate<ShopProduct>($"{DataFolder}/Products/{p.asset}.asset");
            if (isNew)
            {
                var so = new SerializedObject(product);
                so.FindProperty("_productId").stringValue = p.id;
                so.FindProperty("_sortOrder").intValue = i;
                so.FindProperty("_item").objectReferenceValue = item;
                so.FindProperty("_bundleSize").intValue = p.bundle;
                so.FindProperty("_priceCurrency").objectReferenceValue = p.gem ? gem : gold;
                so.FindProperty("_price").intValue = p.price;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            products.Add(product);
        }

        // 카탈로그: 탭은 코드 목록대로, 상품은 폴더 안의 모든 상품 (사람이 추가한 상품도 포함)
        var (catalog, _) = LoadOrCreate<ShopCatalog>(CatalogPath);
        var catalogSo = new SerializedObject(catalog);
        var tabs = catalogSo.FindProperty("_tabs");
        tabs.arraySize = Tabs.Length;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tab = tabs.GetArrayElementAtIndex(i);
            tab.FindPropertyRelative("_label").stringValue = Tabs[i].label;
            tab.FindPropertyRelative("_category").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/{Tabs[i].category}.asset");
        }
        SetList(catalogSo.FindProperty("_products"), FindAll<ShopProduct>(DataFolder + "/Products"));
        catalogSo.ApplyModifiedPropertiesWithoutUndo();

        ImportSprite(NpcArtFolder, "Fairy");
        ImportSprite(NpcArtFolder, "UI_Bubble_Shop");
        AssetDatabase.SaveAssets();
    }

    #endregion

    #region 카드 프리팹

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(CardPrefabPath).Replace('\\', '/'));

        const float width = 440, height = 380;
        var root = new GameObject("ShopProductCard", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
        var background = root.AddComponent<Image>();
        background.sprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        background.type = Image.Type.Sliced;
        var cardButton = root.AddComponent<Button>();
        cardButton.targetGraphic = background;
        cardButton.transition = Selectable.Transition.None;

        var well = CreateImage("Well", root.transform, LoadSprite(CommonSpriteFolder, "UI_Box_Inset"), false);
        Place(well.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(width - 70, 170));
        var icon = CreateImage("Icon", root.transform, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(146, 146));
        icon.preserveAspect = true;

        var categoryChip = CreateImage("CategoryChip", root.transform, LoadSprite(CommonSpriteFolder, "UI_Tag_Category"), false);
        Place(categoryChip.rectTransform, new Vector2(0, 1), new Vector2(24, -12), new Vector2(112, 46));
        var categoryText = CreateText("Label", categoryChip.rectTransform, _titleFont, "씨앗", 24, Body);
        Stretch(categoryText.rectTransform, 0);
        categoryText.rectTransform.offsetMin = new Vector2(0, 4);

        var common = LoadSprite(CommonSpriteFolder, "UI_Tag_Rarity_Common");
        var rarityChip = CreateImage("RarityChip", root.transform, common, false);
        Place(rarityChip.rectTransform, new Vector2(1, 1), new Vector2(-24, -12), new Vector2(100, 46));
        var rarityText = CreateText("Label", rarityChip.rectTransform, _titleFont, "흔함", 24, Color.white);
        Stretch(rarityText.rectTransform, 0);
        rarityText.rectTransform.offsetMin = new Vector2(0, 4);

        var name = CreateText("Name", root.transform, _titleFont, "감자 모종", 36, Cocoa);
        name.enableAutoSizing = true;
        name.fontSizeMin = 26;
        name.fontSizeMax = 36;
        TopBand(name.rectTransform, 24, 24, 210, 54);

        var (priceButton, priceImage, priceIcon, priceText) = BuildPriceButton(root.transform, 36);
        Place(priceButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(width - 70, 84));

        var view = root.AddComponent<ShopProductCardView>();
        Set(view, "_cardButton", cardButton);
        Set(view, "_icon", icon);
        Set(view, "_nameText", name);
        Set(view, "_categoryChip", categoryChip.gameObject);
        Set(view, "_categoryText", categoryText);
        Set(view, "_rarityChip", rarityChip);
        Set(view, "_rarityText", rarityText);
        Set(view, "_priceButton", priceButton);
        Set(view, "_priceButtonImage", priceImage);
        Set(view, "_priceIcon", priceIcon);
        Set(view, "_priceText", priceText);

        var so = new SerializedObject(view);
        var tags = so.FindProperty("_rarityTags");
        var rarities = new[] { ("Common", "UI_Tag_Rarity_Common"), ("Rare", "UI_Tag_Rarity_Rare"), ("Epic", "UI_Tag_Rarity_Epic") };
        tags.arraySize = rarities.Length;
        for (int i = 0; i < rarities.Length; i++)
        {
            var tag = tags.GetArrayElementAtIndex(i);
            tag.FindPropertyRelative("_rarityId").stringValue = rarities[i].Item1;
            tag.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite(CommonSpriteFolder, rarities[i].Item2);
        }
        FillCurrencySprites(so.FindProperty("_priceButtonSprites"));
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);
    }

    // 가격 버튼 색 = 결제 재화 (골드 겨자색, 조개 연보라)
    private static void FillCurrencySprites(SerializedProperty map)
    {
        var entries = map.FindPropertyRelative("_entries");
        entries.arraySize = 2;
        var gold = entries.GetArrayElementAtIndex(0);
        gold.FindPropertyRelative("_currency").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        gold.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Coin");
        var gem = entries.GetArrayElementAtIndex(1);
        gem.FindPropertyRelative("_currency").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);
        gem.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Shell");
        map.FindPropertyRelative("_fallback").objectReferenceValue = LoadSprite(CommonSpriteFolder, "UI_Button_Coin");
    }

    // [재화 아이콘 + 숫자]를 가운데 정렬한 버튼
    private static (Button button, Image image, Image icon, TextMeshProUGUI text) BuildPriceButton(Transform parent, float fontSize)
    {
        var image = CreateImage("PriceButton", parent, LoadSprite(CommonSpriteFolder, "UI_Button_Coin"), true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var content = CreateRect("Content", image.rectTransform);
        Stretch(content, 0);
        content.offsetMin = new Vector2(0, 8); // 버튼 아래쪽 입체 턱만큼 위로
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var icon = CreateImage("Icon", content, null, false);
        icon.preserveAspect = true;
        var iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = fontSize + 14;
        iconSize.preferredHeight = fontSize + 14;

        var text = CreateText("Price", content, _titleFont, "100", fontSize, Cocoa);
        return (button, image, icon, text);
    }

    #endregion

    #region 화면

    /// <summary>상점 화면과 구매 팝업을 canvas 아래에 만든다 (닫힌 채로 시작). 재화 충전 화면보다 먼저 만들어야 그 아래에 그려진다</summary>
    public static (FairyShopView shop, ShopPurchasePopupView popup) BuildScreens(RectTransform canvas)
    {
        var shop = BuildShop(canvas);
        var popup = BuildPopup(canvas);
        return (shop, popup);
    }

    private static FairyShopView BuildShop(RectTransform canvas)
    {
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath).GetComponent<ShopProductCardView>();
        var tabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath).GetComponent<DecorStorageTabView>();

        var (screen, safe, animator) = BuildScreenRoot(canvas, "FairyShop");

        var title = CreateImage("Title", safe, LoadSprite(CommonSpriteFolder, "UI_Ribbon_Lav"), false);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(30, -80), new Vector2(300, 100));
        var titleText = CreateText("Label", title.rectTransform, _titleFont, "요정상점", 44, Cocoa);
        Stretch(titleText.rectTransform, 0);
        titleText.rectTransform.offsetMin = new Vector2(0, 10);

        BuildPill(safe, "GoldPill", AssetDatabase.LoadAssetAtPath<Currency>(GoldPath), 450, 256);
        BuildPill(safe, "GemPill", AssetDatabase.LoadAssetAtPath<Currency>(GemPath), 165, 244);

        var close = CreateImage("CloseButton", safe, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(-30, -80), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        var fairy = CreateImage("Fairy", safe, ImportSprite(NpcArtFolder, "Fairy"), false);
        Place(fairy.rectTransform, new Vector2(0, 1), new Vector2(60, -210), new Vector2(220, 250));
        fairy.preserveAspect = true;

        var bubble = CreateImage("Dialogue", safe, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(bubble.rectTransform, new Vector2(0, 1), new Vector2(320, -250), new Vector2(700, 170));
        var dialogue = CreateText("Text", bubble.rectTransform, _bodyFont, "어서 와!", 36, Cocoa);
        dialogue.alignment = TextAlignmentOptions.Left;
        dialogue.enableAutoSizing = true;
        dialogue.fontSizeMin = 32;
        dialogue.fontSizeMax = 40;
        Stretch(dialogue.rectTransform, 0);
        dialogue.rectTransform.offsetMin = new Vector2(40, 20);
        dialogue.rectTransform.offsetMax = new Vector2(-30, -10);

        var tabs = CreateRect("Tabs", safe);
        Place(tabs, new Vector2(0, 1), new Vector2(60, -490), new Vector2(900, 90));
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 24;
        tabLayout.childAlignment = TextAnchor.LowerLeft;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;

        var list = CreateImage("ListPanel", safe, LoadPanelSprite(), false);
        list.type = Image.Type.Sliced;
        Stretch(list.rectTransform, 0);
        list.rectTransform.offsetMin = new Vector2(30, 50);
        list.rectTransform.offsetMax = new Vector2(-30, -580);
        list.rectTransform.SetSiblingIndex(tabs.GetSiblingIndex()); // 탭이 목록 패널 위에 그려지게

        var (scroll, content) = BuildScroll(list.rectTransform);

        var view = screen.gameObject.AddComponent<FairyShopView>();
        Set(view, "_animator", animator);
        Set(view, "_dialogueText", dialogue);
        Set(view, "_tabPrefab", tabPrefab);
        Set(view, "_tabParent", tabs);
        Set(view, "_cardPrefab", cardPrefab);
        Set(view, "_cardParent", content);
        Set(view, "_scrollRect", scroll);
        Set(view, "_closeButton", closeButton);
        return view;
    }

    // 보유 재화 칸 (상단바와 같은 모양, [+] 없음)
    private static void BuildPill(RectTransform parent, string name, Currency currency, float right, float width)
    {
        var pill = CreateImage(name, parent, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        pill.pixelsPerUnitMultiplier = 1.3f;
        Place(pill.rectTransform, new Vector2(1, 1), new Vector2(-right, -92), new Vector2(width, 76));

        var amount = CreateText("Amount", pill.rectTransform, _titleFont, "0", 32, Cocoa);
        amount.alignment = TextAlignmentOptions.Right;
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 22;
        amount.fontSizeMax = 32;
        Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(-26, 4), new Vector2(width - 90, 50));

        var icon = CreateImage("Icon", pill.rectTransform, currency != null ? currency.Icon : null, false);
        Place(icon.rectTransform, new Vector2(0, 1), new Vector2(-22, 8), new Vector2(84, 84));
        icon.preserveAspect = true;

        var view = pill.gameObject.AddComponent<CurrencyAmountView>();
        Set(view, "_currency", currency);
        Set(view, "_icon", icon);
        Set(view, "_amountText", amount);
    }

    private static (ScrollRect scroll, RectTransform content) BuildScroll(RectTransform list)
    {
        var scrollImage = CreateImage("ScrollView", list, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = new Vector2(40, 40);
        scrollImage.rectTransform.offsetMax = new Vector2(-40, -36);

        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(440, 380);
        grid.spacing = new Vector2(20, 26);
        grid.padding = new RectOffset(0, 0, 14, 14);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    private static ShopPurchasePopupView BuildPopup(RectTransform canvas)
    {
        var (screen, safe, animator) = BuildScreenRoot(canvas, "ShopPurchase", popup: true);
        var panel = (RectTransform)safe.Find("Panel");

        var ribbon = CreateImage("Ribbon", panel, LoadSprite(CommonSpriteFolder, "UI_Ribbon_Peach"), false);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 50), new Vector2(380, 104));
        var ribbonText = CreateText("Title", ribbon.rectTransform, _titleFont, "구매하기", 46, Cocoa);
        Stretch(ribbonText.rectTransform, 0);
        ribbonText.rectTransform.offsetMin = new Vector2(0, 10);

        var close = CreateImage("CloseButton", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(20, 34), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        var frame = CreateImage("IconFrame", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 1), new Vector2(60, -105), new Vector2(220, 220));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        Stretch(icon.rectTransform, 0);
        icon.rectTransform.offsetMin = new Vector2(30, 40);
        icon.rectTransform.offsetMax = new Vector2(-30, -26);
        icon.preserveAspect = true;

        var name = CreateText("Name", panel, _titleFont, "딸기 모종", 46, Cocoa);
        name.alignment = TextAlignmentOptions.Left;
        name.enableAutoSizing = true;
        name.fontSizeMin = 30;
        name.fontSizeMax = 46;
        TopBand(name.rectTransform, 310, 40, 130, 64);

        var description = CreateText("Description", panel, _bodyFont, "밭에 심으면 딸기가 열려요.", 28, Body);
        description.alignment = TextAlignmentOptions.TopLeft;
        description.enableAutoSizing = true;
        description.fontSizeMin = 20;
        description.fontSizeMax = 28;
        TopBand(description.rectTransform, 312, 40, 204, 76);

        var owned = CreateText("Owned", panel, _titleFont, "보유: 0개", 26, LightBrown);
        owned.alignment = TextAlignmentOptions.Left;
        TopBand(owned.rectTransform, 312, 40, 280, 40);

        var minus = CreateImage("MinusButton", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Minus"), true);
        Place(minus.rectTransform, new Vector2(0.5f, 1), new Vector2(-214, -392), new Vector2(96, 96));
        var minusButton = minus.gameObject.AddComponent<Button>();
        minusButton.targetGraphic = minus;
        var field = CreateImage("Quantity", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(field.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -392), new Vector2(300, 96));
        var quantity = CreateText("Value", field.rectTransform, _titleFont, "1", 44, Cocoa);
        Stretch(quantity.rectTransform, 0);
        quantity.rectTransform.offsetMin = new Vector2(0, 8);
        var plus = CreateImage("PlusButton", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Plus"), true);
        Place(plus.rectTransform, new Vector2(0.5f, 1), new Vector2(214, -392), new Vector2(96, 96));
        var plusButton = plus.gameObject.AddComponent<Button>();
        plusButton.targetGraphic = plus;

        var totalLabel = CreateText("TotalLabel", panel, _titleFont, "합계", 28, LightBrown);
        TopBand(totalLabel.rectTransform, 40, 40, 520, 40);
        var totalRow = CreateRect("Total", panel);
        TopBand(totalRow, 40, 40, 566, 70);
        var totalLayout = totalRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        totalLayout.childAlignment = TextAnchor.MiddleCenter;
        totalLayout.spacing = 14;
        totalLayout.childControlWidth = true;
        totalLayout.childControlHeight = true;
        totalLayout.childForceExpandWidth = false;
        totalLayout.childForceExpandHeight = false;
        var totalIcon = CreateImage("Icon", totalRow, null, false);
        totalIcon.preserveAspect = true;
        var totalIconSize = totalIcon.gameObject.AddComponent<LayoutElement>();
        totalIconSize.preferredWidth = 60;
        totalIconSize.preferredHeight = 60;
        var totalText = CreateText("Amount", totalRow, _titleFont, "0", 50, Cocoa);

        var warning = CreateText("Warning", panel, _titleFont, "가방이 가득 찼어요", 28, Red);
        TopBand(warning.rectTransform, 40, 40, 644, 40);

        var cancel = CreateImage("CancelButton", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Secondary"), true);
        Place(cancel.rectTransform, new Vector2(0, 1), new Vector2(80, -710), new Vector2(320, 100));
        var cancelButton = cancel.gameObject.AddComponent<Button>();
        cancelButton.targetGraphic = cancel;
        var cancelText = CreateText("Label", cancel.rectTransform, _titleFont, "취소", 40, LightBrown);
        Stretch(cancelText.rectTransform, 0);
        cancelText.rectTransform.offsetMin = new Vector2(0, 8);

        var (confirmButton, confirmImage, confirmIcon, confirmText) = BuildPriceButton(panel, 40);
        Place(confirmButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-80, -710), new Vector2(320, 100));

        var view = screen.gameObject.AddComponent<ShopPurchasePopupView>();
        Set(view, "_animator", animator);
        Set(view, "_icon", icon);
        Set(view, "_nameText", name);
        Set(view, "_descriptionText", description);
        Set(view, "_ownedText", owned);
        Set(view, "_warningText", warning);
        Set(view, "_quantityText", quantity);
        Set(view, "_minusButton", minusButton);
        Set(view, "_plusButton", plusButton);
        Set(view, "_totalIcon", totalIcon);
        Set(view, "_totalText", totalText);
        Set(view, "_cancelButton", cancelButton);
        Set(view, "_closeButton", closeButton);
        Set(view, "_confirmButton", confirmButton);
        Set(view, "_confirmImage", confirmImage);
        Set(view, "_confirmIcon", confirmIcon);
        Set(view, "_confirmText", confirmText);
        var so = new SerializedObject(view);
        FillCurrencySprites(so.FindProperty("_confirmSprites"));
        so.ApplyModifiedPropertiesWithoutUndo();

        warning.gameObject.SetActive(false);
        return view;
    }

    // 딤 + SafeArea (+ 팝업이면 가운데 크림 패널). 닫힌 채로 시작
    private static (RectTransform screen, RectTransform safe, UIPopupAnimator animator) BuildScreenRoot(RectTransform canvas, string name, bool popup = false)
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

        RectTransform animated = safe;
        if (popup)
        {
            var panel = CreateImage("Panel", safe, LoadPanelSprite(), true);
            panel.type = Image.Type.Sliced;
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(840, 880));
            animated = panel.rectTransform;
        }
        var group = animated.gameObject.AddComponent<CanvasGroup>();

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", animated);
        Set(animator, "_panelGroup", group);
        Set(animator, "_dimButton", dimButton);
        if (!popup)
        {
            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("_startScale").floatValue = 0.92f; // 전체 화면은 은은하게
            animatorSo.FindProperty("_overshoot").floatValue = 1.2f;
            animatorSo.FindProperty("_endScale").floatValue = 0.95f;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        screen.gameObject.SetActive(false);
        return (screen, safe, animator);
    }

    #endregion

    #region 광장 요정

    /// <summary>광장 씬에 요정 NPC를 넣는다 (이미 있으면 그대로). 요정 발밑에는 장난감을 놓지 못하게 막는다</summary>
    [MenuItem("Tools/Shop/Place Fairy In Plaza")]
    public static void PlaceFairyInPlaza()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        if (Object.FindAnyObjectByType<FairyNpcView>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[FairyShopSetup] 광장에 요정이 이미 있습니다.");
            return;
        }

        var root = new GameObject("FairyNpc");
        root.transform.position = new Vector3(3.6f, 1.4f, 0f); // 처음 카메라 화면 오른쪽 가운데 (왼쪽 위 게시판·나뭇가지와 안 겹침)

        var fairySprite = ImportSprite(NpcArtFolder, "Fairy");
        var fairy = new GameObject("Fairy").AddComponent<SpriteRenderer>();
        fairy.transform.SetParent(root.transform, false);
        fairy.sprite = fairySprite;
        const float fairyHeight = 1.7f; // 해달(1.67)과 비슷한 키
        float scale = fairyHeight / fairySprite.bounds.size.y;
        fairy.transform.localScale = Vector3.one * scale;
        fairy.transform.localPosition = new Vector3(0f, -fairySprite.bounds.min.y * scale + 0.3f, 0f); // 살짝 떠 있음

        var bubbleSprite = ImportSprite(NpcArtFolder, "UI_Bubble_Shop");
        var bubble = new GameObject("Bubble").AddComponent<SpriteRenderer>();
        bubble.transform.SetParent(root.transform, false);
        bubble.sprite = bubbleSprite;
        float bubbleScale = 1.5f / bubbleSprite.bounds.size.x; // 화면에서 알아볼 수 있게 요정 어깨너비쯤
        bubble.transform.localScale = Vector3.one * bubbleScale;
        bubble.transform.localPosition = new Vector3(0.1f, fairyHeight + 0.85f, 0f);

        var tap = root.AddComponent<BoxCollider2D>();
        tap.size = new Vector2(1.3f, fairyHeight + 1.2f);
        tap.offset = new Vector2(0f, (fairyHeight + 1.2f) * 0.5f);

        var view = root.AddComponent<FairyNpcView>();
        Set(view, "_fairy", fairy);
        Set(view, "_bubble", bubble);
        Set(view, "_tapArea", tap);

        var block = new GameObject("DecorBlock_Fairy").AddComponent<DecorBlockArea>();
        block.transform.SetParent(root.transform, false);
        block.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var blockSo = new SerializedObject(block);
        blockSo.FindProperty("_size").vector2Value = new Vector2(1.6f, 1.6f);
        blockSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FairyShopSetup] 광장에 요정을 넣었습니다.");
    }

    #endregion
}
