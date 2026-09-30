using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 꾸미기 모드 화면(위쪽 제목·완료, 안내, 물건 위 버튼, 보관함)과 보관함 칸·탭 프리팹을 만든다. GlobalUISetup이 함께 호출한다.
/// 1080×1920 기준, 꾸미기 시안(Tools/UIGen/hud_mockup.py의 decorate_mode) 수치를 옮김.
/// </summary>
public static class DecorModeSetup
{
    private const string SlotPrefabPath = "Assets/Prefab/Decor/DecorStorageSlot.prefab";
    private const string TabPrefabPath = "Assets/Prefab/Decor/DecorStorageTab.prefab";

    private const float SlotSize = 190f;
    private const float SlotLabelHeight = 48f;

    #region 프리팹

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(SlotPrefabPath).Replace('\\', '/'));
        BuildSlotPrefab();
        BuildTabPrefab();
    }

    private static void BuildSlotPrefab()
    {
        var root = new GameObject("DecorStorageSlot", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(SlotSize, SlotSize + SlotLabelHeight + 12);

        var filled = LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled");
        var background = CreateImage("Slot", root.transform, filled, true);
        Place(background.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(SlotSize, SlotSize));
        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var icon = CreateImage("Icon", background.rectTransform, null, false);
        Stretch(icon.rectTransform, 0);
        icon.rectTransform.offsetMin = new Vector2(34, 44);
        icon.rectTransform.offsetMax = new Vector2(-34, -26);
        icon.preserveAspect = true;

        var count = CreateText("Count", background.rectTransform, _titleFont, "x1", 28, Cocoa);
        count.alignment = TextAlignmentOptions.BottomRight;
        Stretch(count.rectTransform, 0);
        count.rectTransform.offsetMin = new Vector2(0, 22);
        count.rectTransform.offsetMax = new Vector2(-22, 0);

        var ring = CreateImage("SelectRing", background.rectTransform, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_SelectRing"), false);
        Stretch(ring.rectTransform, -8);

        var name = CreateText("Name", root.transform, _titleFont, "축구공", 26, Cocoa);
        BottomBand(name.rectTransform, 0, SlotLabelHeight);

        var view = root.AddComponent<DecorStorageSlotView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_icon", icon);
        Set(view, "_countText", count);
        Set(view, "_nameText", name);
        Set(view, "_selectRing", ring.gameObject);
        Set(view, "_filledSprite", filled);
        Set(view, "_emptySprite", LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Empty"));

        ring.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildTabPrefab()
    {
        var root = new GameObject("DecorStorageTab", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(150, 80);
        var normal = LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Normal");
        var background = root.AddComponent<Image>();
        background.sprite = normal;
        background.type = Image.Type.Sliced;
        var button = root.AddComponent<Button>();
        button.targetGraphic = background;

        var label = CreateText("Label", root.transform, _titleFont, "전체", 34, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8);

        var view = root.AddComponent<DecorStorageTabView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_label", label);
        Set(view, "_normalSprite", normal);
        Set(view, "_selectedSprite", ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"));

        PrefabUtility.SaveAsPrefabAsset(root, TabPrefabPath);
        Object.DestroyImmediate(root);
    }

    #endregion

    #region 화면

    /// <summary>꾸미기 모드 화면을 canvas 아래에 만든다 (꺼진 채로 시작). hud: 모드 동안 숨길 상단바·하단 바 묶음</summary>
    public static DecorModePresenter BuildScreen(RectTransform canvas, GameObject hud)
    {
        // 씬을 연 뒤 호출되므로 에셋은 경로로 다시 불러온다
        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath).GetComponent<DecorStorageSlotView>();
        var tabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath).GetComponent<DecorStorageTabView>();

        var screen = CreateRect("DecorMode", canvas);
        Stretch(screen, 0);
        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        // 물건 위 버튼 (보관함보다 먼저 만들어 보관함 아래에 그려지게)
        var (actions, rotate, confirm, remove) = BuildActions(safe);

        var title = CreateImage("Title", safe, LoadSprite(CommonSpriteFolder, "UI_Ribbon_Peach"), false);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(30, -80), new Vector2(380, 100));
        var titleText = CreateText("Label", title.rectTransform, _titleFont, "꾸미기 모드", 40, Cocoa);
        Stretch(titleText.rectTransform, 0);
        titleText.rectTransform.offsetMin = new Vector2(0, 10);

        var done = CreateImage("DoneButton", safe, LoadSprite(CommonSpriteFolder, "UI_Button_Primary"), true);
        Place(done.rectTransform, new Vector2(1, 1), new Vector2(-30, -80), new Vector2(220, 100));
        var doneButton = done.gameObject.AddComponent<Button>();
        doneButton.targetGraphic = done;
        var doneText = CreateText("Label", done.rectTransform, _titleFont, "완료", 40, Color.white);
        Stretch(doneText.rectTransform, 0);
        doneText.rectTransform.offsetMin = new Vector2(0, 10);

        var hint = CreateImage("Hint", safe, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 548), new Vector2(760, 76));
        var hintText = CreateText("Label", hint.rectTransform, _titleFont, "초록색 칸에 놓을 수 있어요", 30, Cocoa);
        hintText.enableAutoSizing = true; // 잠긴 구역 조건처럼 긴 안내는 글자를 줄임
        hintText.fontSizeMin = 20;
        hintText.fontSizeMax = 30;
        Stretch(hintText.rectTransform, 0);
        hintText.rectTransform.offsetMin = new Vector2(24, 8);
        hintText.rectTransform.offsetMax = new Vector2(-24, 0);

        var storage = BuildStorage(safe, slotPrefab, tabPrefab);

        var view = screen.gameObject.AddComponent<DecorModeView>();
        Set(view, "_doneButton", doneButton);
        Set(view, "_hintText", hintText);
        Set(view, "_actions", actions);
        Set(view, "_rotateButton", rotate);
        Set(view, "_confirmButton", confirm);
        Set(view, "_removeButton", remove);

        var presenter = screen.gameObject.AddComponent<DecorModePresenter>();
        Set(presenter, "_view", view);
        Set(presenter, "_storage", storage);
        Set(presenter, "_hud", hud);
        Set(presenter, "_tileOkSprite", ImportSprite(CommonSpriteFolder, "UI_Tile_OK"));
        Set(presenter, "_tileBlockedSprite", ImportSprite(CommonSpriteFolder, "UI_Tile_Blocked"));

        actions.gameObject.SetActive(false);
        screen.gameObject.SetActive(false);
        return presenter;
    }

    // [회전] [확인] [빼기]: 가운데 기준(anchor 0.5,0.5), 아래 가운데가 미리보기 윗변을 따라감
    private static (RectTransform actions, Button rotate, Button confirm, Button remove) BuildActions(RectTransform parent)
    {
        var actions = CreateRect("Actions", parent);
        actions.anchorMin = new Vector2(0.5f, 0.5f);
        actions.anchorMax = new Vector2(0.5f, 0.5f);
        actions.pivot = new Vector2(0.5f, 0f);
        actions.sizeDelta = new Vector2(320, 88);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return (actions,
            RoundButton(actions, "RotateButton", LoadSprite(CommonSpriteFolder, "UI_Button_Rotate")),
            RoundButton(actions, "ConfirmButton", LoadSprite(CommonSpriteFolder, "UI_Button_Confirm")),
            RoundButton(actions, "RemoveButton", LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton")));
    }

    private static Button RoundButton(Transform parent, string name, Sprite sprite)
    {
        var image = CreateImage(name, parent, sprite, true);
        image.rectTransform.sizeDelta = new Vector2(88, 88);
        image.raycastPadding = new Vector4(-10, -10, -10, -10); // 보이는 크기보다 넓게 눌리도록
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static DecorStorageView BuildStorage(RectTransform parent, DecorStorageSlotView slotPrefab, DecorStorageTabView tabPrefab)
    {
        var panel = CreateImage("Storage", parent, LoadPanelSprite(), true); // 패널 위를 눌러도 월드로 넘어가지 않게
        panel.type = Image.Type.Sliced;
        var rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.offsetMin = new Vector2(30, 20);
        rect.offsetMax = new Vector2(-30, 520);

        var title = CreateText("Title", rect, _titleFont, "보관함", 38, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(56, -40), new Vector2(200, 60));

        var tabs = CreateRect("Tabs", rect);
        Place(tabs, new Vector2(0, 1), new Vector2(270, -30), new Vector2(660, 80));
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 20;
        tabLayout.childAlignment = TextAnchor.MiddleLeft;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;

        var scrollImage = CreateImage("ScrollView", rect, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = new Vector2(40, 36);
        scrollImage.rectTransform.offsetMax = new Vector2(-40, -128);

        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 0);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 0.5f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 22;
        layout.padding = new RectOffset(16, 16, 12, 0); // 선택 테두리가 칸 밖으로 나오는 만큼
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.viewport = viewport;
        scroll.content = content;

        var shop = BuildShopSlot(content);

        var view = panel.gameObject.AddComponent<DecorStorageView>();
        Set(view, "_tabPrefab", tabPrefab);
        Set(view, "_tabParent", tabs);
        Set(view, "_slotPrefab", slotPrefab);
        Set(view, "_slotParent", content);
        Set(view, "_shopSlot", shop);
        Set(view, "_shopSlotRoot", shop.transform.parent);
        Set(view, "_scrollRect", scroll);
        return view;
    }

    // 맨 끝 [+ 상점] 칸 (상점은 아직 없어 누르면 안내만)
    private static Button BuildShopSlot(Transform parent)
    {
        var root = CreateRect("ShopSlot", parent);
        root.sizeDelta = new Vector2(SlotSize, SlotSize + SlotLabelHeight + 12);

        var background = CreateImage("Slot", root, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Empty"), true);
        Place(background.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(SlotSize, SlotSize));
        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var plus = CreateText("Plus", background.rectTransform, _titleFont, "+", 80, LightBrown);
        Stretch(plus.rectTransform, 0);
        plus.rectTransform.offsetMin = new Vector2(0, 14);

        var label = CreateText("Name", root, _titleFont, "상점", 26, LightBrown);
        BottomBand(label.rectTransform, 0, SlotLabelHeight);
        return button;
    }

    #endregion
}
