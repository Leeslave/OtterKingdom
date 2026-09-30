using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전역 UI(상단바 + 하단 네비게이션 바 + 가방 + 도감 + 퀘스트 + 꾸미기 모드 + 요정 상점 + 재화 충전 + 이동 팝업 + 씬 전환 페이드)를 만든다. 여러 번 실행해도 결과가 같음.
/// - 장소 에셋 4개 (광장/밭/낚시터/광산)
/// - Assets/Prefab/Navigation/ZoneCard.prefab
/// - Assets/Resources/GlobalUI.prefab  (가방 화면은 InventoryTestScene의 Canvas를 복제해서 사용)
/// - Build Settings에 Plaza/Farm/Fishing 등록
/// - InventoryTestScene에 GlobalUI 프리팹 배치 (원본 가방 UI는 InventoryUISource로 꺼 둠)
/// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod GlobalUISetup.Run -quit
/// </summary>
public static class GlobalUISetup
{
    private const string InventoryScenePath = "Assets/Scenes/InventoryTestScene.unity";
    private const string GlobalUIPrefabPath = "Assets/Resources/GlobalUI.prefab";
    private const string ZoneCardPrefabPath = "Assets/Prefab/Navigation/ZoneCard.prefab";
    private const string ZoneFolder = "Assets/Scriptable Obejects/Navigation";
    private const string TitleFontPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/NanumSquareRoundOTFR SDF.asset";
    private const string InventoryConfigPath = "Assets/Scriptable Obejects/Inventory/InventoryConfig.asset";
    private static readonly string[] CurrencyPaths = { "Assets/Scriptable Obejects/Gold.asset", "Assets/Scriptable Obejects/Gem.asset" };
    internal const string CommonSpriteFolder = "Assets/Art/UI/Common";
    internal const string NavIconFolder = "Assets/Art/UI/Nav";       // ICON_Nav_*.png
    private const string PlaceIconFolder = "Assets/Art/UI/Travel";  // ICON_Place_*.png
    internal const string InventorySpriteFolder = "Assets/Art/UI/Inventory";

    internal static readonly Color Cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    internal static readonly Color LightBrown = new Color32(0xA0, 0x86, 0x72, 0xFF);
    private static readonly Color Sage = new Color(0.62f, 0.76f, 0.54f); // 크림 태그에 곱해 초록 "현재 위치"

    // (에셋 이름, ID, 이름, 설명, 씬, 이용 가능, 정렬, 아이콘 파일)
    private static readonly (string asset, string id, string name, string subtitle, string scene, bool available, int order, string icon)[] Zones =
    {
        ("Zone_Plaza", "Plaza", "광장", "요정상점 · 해달 구경", "Plaza", true, 0, "ICON_Place_Plaza"),
        ("Zone_Farm", "Farm", "밭", "모종 심기 · 수확", "Farm", true, 1, "ICON_Place_Farm"),
        ("Zone_Fishing", "Fishing", "낚시터", "물고기 낚기", "Fishing", true, 2, "ICON_Place_FishingSpot"),
        ("Zone_Mine", "Mine", "광산", "광석 캐기", "Mine", true, 3, "ICON_Place_Mine"),
    };

    private static readonly string[] ZoneScenes = { "Assets/Scenes/Plaza.unity", "Assets/Scenes/Farm.unity", "Assets/Scenes/Fishing.unity", "Assets/Scenes/Mine.unity" };

    // ── 1080×1920 기준 배치 (시안) ──
    private const float BarSideMargin = 32f;
    private const float BarBottom = 24f;
    private const float BarHeight = 190f;
    private const float CenterButtonSize = 190f;
    private const float CenterButtonBottom = 56f; // 바 위로 튀어나오도록
    private const float TravelPanelWidth = 640f;
    private const float TravelPanelHeight = 700f;

    internal static TMP_FontAsset _titleFont;
    internal static TMP_FontAsset _bodyFont;

    [MenuItem("Tools/Navigation/Build Global UI")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GlobalUISetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // 끝나면 원래 보던 씬으로 돌아가기 위해 기억
        string previousScene = SceneManager.GetActiveScene().path;

        // 제목·라벨: Cafe24 (탭 프리팹 라벨은 기본 폰트 LiberationSans라서 숫자·기호가 얇게 나왔음)
        _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath) ?? _titleFont;

        EnsureFolder(ZoneFolder);
        EnsureFolder(Path.GetDirectoryName(ZoneCardPrefabPath).Replace('\\', '/'));
        EnsureFolder(Path.GetDirectoryName(GlobalUIPrefabPath).Replace('\\', '/'));

        CreateZones();
        BuildZoneCardPrefab();
        CollectionSetup.CreateData();
        CollectionSetup.BuildPrefabs();
        ProgressionSetup.CreateData();
        QuestSetup.CreateData();
        QuestSetup.BuildPrefabs();
        CurrencyShopSetup.CreateData();
        CurrencyShopSetup.BuildPrefabs();
        DecorSetup.CreateData();
        DecorModeSetup.BuildPrefabs();
        FairyShopSetup.CreateData();
        FairyShopSetup.BuildPrefabs();
        BuildGlobalUIPrefab();
        RegisterBuildScenes();

        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(previousScene) && previousScene != InventoryScenePath)
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

        Debug.Log("[GlobalUISetup] 완료");
    }

    #region 장소 에셋

    private static void CreateZones()
    {
        foreach (var z in Zones)
        {
            string path = $"{ZoneFolder}/{z.asset}.asset";
            var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(path);
            if (zone == null)
            {
                zone = ScriptableObject.CreateInstance<ZoneDefinition>();
                AssetDatabase.CreateAsset(zone, path);
            }

            var so = new SerializedObject(zone);

            // 아이콘은 비어 있을 때만 채움 (사람이 다른 아이콘으로 바꿨으면 유지)
            var iconProperty = so.FindProperty("_icon");
            if (iconProperty.objectReferenceValue == null)
                iconProperty.objectReferenceValue = LoadIcon(PlaceIconFolder, z.icon);

            so.FindProperty("_zoneId").stringValue = z.id;
            so.FindProperty("_displayName").stringValue = z.name;
            so.FindProperty("_subtitle").stringValue = z.subtitle;
            so.FindProperty("_sceneName").stringValue = z.scene;
            so.FindProperty("_isAvailable").boolValue = z.available;
            so.FindProperty("_sortOrder").intValue = z.order;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
        }
        AssetDatabase.SaveAssets();
    }

    #endregion

    #region 장소 카드 프리팹

    private static void BuildZoneCardPrefab()
    {
        var root = new GameObject("ZoneCard", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(262, 240);

        var background = root.AddComponent<Image>();
        background.sprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        background.type = Image.Type.Sliced;

        var button = root.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.disabledColor = Color.white; // 잠김은 스프라이트로 표현하므로 버튼이 따로 흐리게 하지 않음
        button.colors = colors;

        var ring = CreateImage("SelectRing", rect, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_SelectRing"), true);
        Stretch(ring.rectTransform, -8);

        var icon = CreateImage("Icon", rect, null, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(96, 96));
        icon.preserveAspect = true;

        var lockIcon = CreateImage("LockIcon", rect, LoadSprite(InventorySpriteFolder, "LockIcon"), false);
        Place(lockIcon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(84, 84));
        lockIcon.preserveAspect = true;

        // 아이콘(위)과 겹치지 않도록 이름은 카드 아래쪽 절반에
        var nameText = CreateText("Name", rect, _titleFont, "장소", 42, Cocoa);
        BottomBand(nameText.rectTransform, 70, 50);

        // 설명이 길면 카드 폭(두꺼운 테두리 안쪽) 안에서 글자를 줄임
        var subtitle = CreateText("Subtitle", rect, _bodyFont, "설명", 26, LightBrown);
        BottomBand(subtitle.rectTransform, 30, 40);
        subtitle.rectTransform.offsetMin = new Vector2(30, subtitle.rectTransform.offsetMin.y);
        subtitle.rectTransform.offsetMax = new Vector2(-30, subtitle.rectTransform.offsetMax.y);
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 18;
        subtitle.fontSizeMax = 26;

        // "현재 위치": 카드 윗선에 걸친 초록 알약
        var badge = CreateImage("CurrentBadge", rect, LoadSprite(CommonSpriteFolder, "UI_Tag_Category"), true);
        badge.color = Sage;
        Place(badge.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(168, 52));
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var badgeText = CreateText("Label", badge.rectTransform, _titleFont, "현재 위치", 28, Cocoa);
        Stretch(badgeText.rectTransform, 0);

        var view = root.AddComponent<ZoneCardView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_icon", icon);
        Set(view, "_nameText", nameText);
        Set(view, "_subtitleText", subtitle);
        Set(view, "_currentBadge", badge.gameObject);
        Set(view, "_selectRing", ring.gameObject);
        Set(view, "_lockIcon", lockIcon.gameObject);
        Set(view, "_normalSprite", LoadSprite(CommonSpriteFolder, "UI_Button_Paper"));
        Set(view, "_currentSprite", LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"));
        Set(view, "_lockedSprite", LoadSprite(CommonSpriteFolder, "UI_Inventory_Slot_Locked"));

        ring.gameObject.SetActive(false);
        lockIcon.gameObject.SetActive(false);
        badge.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, ZoneCardPrefabPath);
        Object.DestroyImmediate(root);
    }

    #endregion

    #region 전역 UI 프리팹

    private static void BuildGlobalUIPrefab()
    {
        // 가방 화면/확장 팝업은 테스트 씬에서 다듬어 둔 것을 그대로 복제 (내부 참조가 함께 복제됨)
        var scene = EditorSceneManager.OpenScene(InventoryScenePath, OpenSceneMode.Single);

        // 씬을 열면 쓰지 않는 에셋이 메모리에서 내려가 앞서 만든 참조가 무효가 되므로, 씬을 연 뒤 경로로 다시 불러온다
        var zones = Zones.Select(z => AssetDatabase.LoadAssetAtPath<ZoneDefinition>($"{ZoneFolder}/{z.asset}.asset")).ToList();
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZoneCardPrefabPath).GetComponent<ZoneCardView>();
        // 원본: 가방 UI가 든 Canvas (씬에 배치한 GlobalUI 자신은 제외, 꺼져 있어도 찾음)
        var sourceCanvas = scene.GetRootGameObjects().First(go =>
            go.GetComponent<Canvas>() != null
            && go.GetComponent<GlobalUIRoot>() == null
            && go.GetComponentInChildren<InventoryPresenter>(true) != null);

        // 가방 원본에 판매 버튼·판매 팝업이 없으면 먼저 넣음 (원본에 넣어야 테스트 씬과 전역 UI가 같아짐)
        SellUISetup.EnsureSellUI(sourceCanvas);
        // 가방 탭에 꾸미기(장난감) 추가
        DecorSetup.EnsureBagCategories(sourceCanvas);

        var root = Object.Instantiate(sourceCanvas);
        root.name = "GlobalUI";
        root.SetActive(true); // 원본은 씬에서 꺼 두므로 복제본은 켬
        var rootRect = (RectTransform)root.transform;

        // 테스트용 임시 가방 버튼은 네비게이션 바가 대신함
        var testHud = rootRect.Find("TestHud");
        if (testHud != null)
            Object.DestroyImmediate(testHud.gameObject);

        var canvas = root.GetComponent<Canvas>();
        canvas.sortingOrder = 100; // 각 씬의 UI보다 위

        var inventoryScreen = rootRect.Find("InventoryScreen");
        var inventoryAnimator = inventoryScreen.GetComponent<UIPopupAnimator>();
        var inventoryPresenter = inventoryScreen.GetComponentInChildren<InventoryPresenter>(true);
        inventoryScreen.gameObject.SetActive(false); // 가방은 닫힌 채로 시작

        // 그리는 순서: 상단바·하단 바 → 가방 → 확장 팝업 → 도감 → 퀘스트 → 꾸미기 모드 → 요정 상점 → 재화 충전(부족·충전은 상점 위) → 레벨업 → 이동 팝업 → 페이드
        var hud = CreateRect("HudSafeArea", rootRect);
        Stretch(hud, 0);
        hud.gameObject.AddComponent<SafeAreaFltter>();
        hud.SetSiblingIndex(0);

        var navBar = BuildNavBar(hud);
        var topBar = TopBarSetup.Build(hud);
        var collection = CollectionSetup.BuildScreen(rootRect);
        var quest = QuestSetup.BuildScreen(rootRect);
        var decorMode = DecorModeSetup.BuildScreen(rootRect, hud.gameObject);
        var fairyShop = FairyShopSetup.BuildScreens(rootRect);
        var shop = CurrencyShopSetup.BuildScreens(rootRect);
        var levelUp = ProgressionSetup.BuildPopup(rootRect);
        var travel = BuildTravelPopup(rootRect, cardPrefab);
        var fader = BuildFader(rootRect);
        BuildEventSystem(rootRect);

        var globalRoot = root.AddComponent<GlobalUIRoot>();
        var zonesProperty = new SerializedObject(globalRoot);
        var list = zonesProperty.FindProperty("_zones");
        list.arraySize = zones.Count;
        for (int i = 0; i < zones.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
        zonesProperty.ApplyModifiedPropertiesWithoutUndo();

        var navigator = root.AddComponent<SceneNavigator>();
        Set(navigator, "_root", globalRoot);
        Set(navigator, "_fader", fader);

        // 도감 모델의 주인: 전역 UI와 함께 씬을 넘어 유지 (중복은 GlobalUIRoot가 먼저 정리)
        var collectionManager = root.AddComponent<CollectionManager>();
        Set(collectionManager, "_database", AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionSetup.DatabasePath));

        // 상단바 프로필(이름·레벨)의 주인
        var profileManager = root.AddComponent<ProfileManager>();
        Set(profileManager, "_levelTable", AssetDatabase.LoadAssetAtPath<LevelTable>(ProgressionSetup.LevelTablePath));

        // 레벨이 오르면 레벨업 팝업
        var levelUpPresenter = root.AddComponent<LevelUpPresenter>();
        Set(levelUpPresenter, "_popup", levelUp);

        // 꾸미기(장소별 격자, 보관함)의 주인
        var decorManager = root.AddComponent<DecorManager>();
        Set(decorManager, "_catalog", AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorSetup.CatalogPath));
        var decorSo = new SerializedObject(decorManager);
        var boards = DecorSetup.LoadBoards();
        var boardList = decorSo.FindProperty("_boards");
        boardList.arraySize = boards.Count;
        for (int i = 0; i < boards.Count; i++)
            boardList.GetArrayElementAtIndex(i).objectReferenceValue = boards[i];
        decorSo.ApplyModifiedPropertiesWithoutUndo();

        // 상단바 [+] → 충전 화면
        var shopPresenter = root.AddComponent<CurrencyShopPresenter>();
        Set(shopPresenter, "_catalog", AssetDatabase.LoadAssetAtPath<CurrencyShopCatalog>(CurrencyShopSetup.CatalogPath));
        Set(shopPresenter, "_goldPill", topBar.goldPill);
        Set(shopPresenter, "_gemPill", topBar.gemPill);
        Set(shopPresenter, "_goldShop", shop.goldShop);
        Set(shopPresenter, "_gemShop", shop.gemShop);
        Set(shopPresenter, "_confirm", shop.confirm);
        Set(shopPresenter, "_shortage", shop.shortage);

        // 요정 상점 (광장 요정, 꾸미기 보관함 [+ 상점]에서 열림). 재화 부족은 충전 흐름의 부족 팝업을 같이 씀
        var fairyPresenter = root.AddComponent<FairyShopPresenter>();
        Set(fairyPresenter, "_catalog", AssetDatabase.LoadAssetAtPath<ShopCatalog>(FairyShopSetup.CatalogPath));
        Set(fairyPresenter, "_shop", fairyShop.shop);
        Set(fairyPresenter, "_popup", fairyShop.popup);
        Set(fairyPresenter, "_shortage", shop.shortage);

        // 퀘스트 모델의 주인: 도감과 같이 전역 UI에 붙음
        var questManager = root.AddComponent<QuestManager>();
        Set(questManager, "_database", AssetDatabase.LoadAssetAtPath<QuestDatabase>(QuestSetup.DatabasePath));

        var presenter = root.AddComponent<GlobalUIPresenter>();
        Set(presenter, "_root", globalRoot);
        Set(presenter, "_navigator", navigator);
        Set(presenter, "_navBar", navBar);
        Set(presenter, "_travelPopup", travel);
        Set(presenter, "_inventory", inventoryPresenter);
        Set(presenter, "_inventoryScreen", inventoryAnimator);
        Set(presenter, "_collection", collection.presenter);
        Set(presenter, "_collectionScreen", collection.screen);
        Set(presenter, "_quest", quest.presenter);
        Set(presenter, "_questScreen", quest.screen);
        Set(presenter, "_decorMode", decorMode);

        PrefabUtility.SaveAsPrefabAsset(root, GlobalUIPrefabPath);
        Object.DestroyImmediate(root);

        SetupTestScene(scene, sourceCanvas);
        EditorSceneManager.SaveScene(scene);
    }

    // 테스트 씬: 전역 UI 프리팹을 직접 배치해 편집 모드에서도 보이게 한다.
    // 원본 가방 UI는 이 스크립트가 복제에 쓰므로 지우지 않고 꺼 두고, 씬의 EventSystem은 전역 UI 것과 겹치므로 끈다.
    private static void SetupTestScene(UnityEngine.SceneManagement.Scene scene, GameObject sourceCanvas)
    {
        sourceCanvas.name = "InventoryUISource";
        sourceCanvas.SetActive(false);

        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "GlobalUITestHost") // 예전 방식의 흔적 정리
                Object.DestroyImmediate(go);
            else if (go.GetComponent<GlobalUIRoot>() == null && go.GetComponentInChildren<EventSystem>(true) != null)
                go.SetActive(false);
        }

        bool hasGlobalUI = scene.GetRootGameObjects().Any(go => go.GetComponent<GlobalUIRoot>() != null);
        if (!hasGlobalUI)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GlobalUI";
        }
    }

    private static NavBarView BuildNavBar(RectTransform parent)
    {
        var bar = CreateRect("NavBar", parent);
        bar.anchorMin = new Vector2(0, 0);
        bar.anchorMax = new Vector2(1, 0);
        bar.pivot = new Vector2(0.5f, 0);
        bar.offsetMin = new Vector2(BarSideMargin, BarBottom);
        bar.offsetMax = new Vector2(-BarSideMargin, BarBottom + BarHeight);

        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.sprite = LoadPanelSprite();
        barImage.type = Image.Type.Sliced;
        barImage.pixelsPerUnitMultiplier = 1.6f; // 바가 낮아서 테두리를 얇게

        var codex = BuildNavItem(bar, 0, "Codex", "도감", false, LoadIcon(NavIconFolder, "ICON_Nav_Collection"));
        var quest = BuildNavItem(bar, 1, "Quest", "퀘스트", false, LoadIcon(NavIconFolder, "ICON_Nav_Quest"));
        var bag = BuildNavItem(bar, 3, "Bag", "가방", true, LoadIcon(NavIconFolder, "ICON_Nav_Bag"));
        var travel = BuildNavItem(bar, 4, "Travel", "이동", false, LoadIcon(NavIconFolder, "ICON_Nav_Travel"));

        codex.badge.SetActive(false);
        travel.badge.SetActive(false);

        // 가방 "N": 새 종류가 있을 때만
        var newBadge = bag.item.gameObject.AddComponent<InventoryNewBadgeView>();
        Set(newBadge, "_badge", bag.badge);

        // 퀘스트 숫자: 보상을 받을 수 있는 퀘스트가 있을 때만
        var questBadge = quest.item.gameObject.AddComponent<QuestBadgeView>();
        Set(questBadge, "_badge", quest.badge);
        Set(questBadge, "_countText", quest.badge.GetComponentInChildren<TextMeshProUGUI>(true));
        quest.badge.SetActive(false);

        var decorate = BuildCenterButton(bar, LoadIcon(NavIconFolder, "ICON_Nav_Decorate"));

        var view = bar.gameObject.AddComponent<NavBarView>();
        Set(view, "_codexButton", codex.button);
        Set(view, "_codexSelected", codex.selected);
        Set(view, "_questButton", quest.button);
        Set(view, "_questSelected", quest.selected);
        Set(view, "_decorateButton", decorate);
        Set(view, "_bagButton", bag.button);
        Set(view, "_travelButton", travel.button);
        Set(view, "_bagSelected", bag.selected);
        Set(view, "_travelSelected", travel.selected);
        return view;
    }

    private static (RectTransform item, Button button, GameObject selected, GameObject badge) BuildNavItem(
        RectTransform bar, int column, string name, string label, bool badgeIsNew, Sprite iconSprite)
    {
        var item = CreateRect(name, bar);
        item.anchorMin = new Vector2(column / 5f, 0);
        item.anchorMax = new Vector2((column + 1) / 5f, 1);
        item.offsetMin = new Vector2(6, 16);
        item.offsetMax = new Vector2(-6, -12);

        // 터치 영역용 투명 그래픽
        var hit = item.gameObject.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, 0);
        var button = item.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        var selected = CreateImage("Selected", item, LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Selected"), true);
        Stretch(selected.rectTransform, 0);
        selected.gameObject.SetActive(false);

        var icon = CreateImage("Icon", item, iconSprite, false);
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(96, 96));
        icon.preserveAspect = true;
        icon.enabled = iconSprite != null; // 아이콘 파일이 없으면 글자만

        var text = CreateText("Label", item, _titleFont, label, 34, Cocoa);
        BottomBand(text.rectTransform, 10, 48);

        var badge = CreateImage("Badge", item, LoadSprite(CommonSpriteFolder, "UI_Badge"), false);
        Place(badge.rectTransform, new Vector2(1, 1), new Vector2(-30, -6), new Vector2(52, 52));
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var badgeText = CreateText("Text", badge.rectTransform, _titleFont, badgeIsNew ? "N" : "0", 28, Color.white);
        Stretch(badgeText.rectTransform, 0);

        return (item, button, selected.gameObject, badge.gameObject);
    }

    // 가운데 꾸미기: 바 위로 튀어나온 큰 원
    private static Button BuildCenterButton(RectTransform bar, Sprite iconSprite)
    {
        var column = CreateRect("Decorate", bar);
        column.anchorMin = new Vector2(2 / 5f, 0);
        column.anchorMax = new Vector2(3 / 5f, 1);
        column.offsetMin = Vector2.zero;
        column.offsetMax = Vector2.zero;

        var circle = CreateImage("Button", column, LoadSprite(CommonSpriteFolder, "UI_RoundButton_Featured"), true);
        Place(circle.rectTransform, new Vector2(0.5f, 0), new Vector2(0, CenterButtonBottom), new Vector2(CenterButtonSize, CenterButtonSize));
        circle.rectTransform.pivot = new Vector2(0.5f, 0);
        var button = circle.gameObject.AddComponent<Button>();
        button.targetGraphic = circle;

        var icon = CreateImage("Icon", circle.rectTransform, iconSprite, false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(112, 112));
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.preserveAspect = true;
        icon.enabled = iconSprite != null;

        var label = CreateText("Label", column, _titleFont, "꾸미기", 36, Cocoa);
        BottomBand(label.rectTransform, 10, 50);
        return button;
    }

    private static TravelPopupView BuildTravelPopup(RectTransform parent, ZoneCardView cardPrefab)
    {
        var popup = CreateRect("TravelPopup", parent);
        Stretch(popup, 0);

        // 배경은 투명: 뒤 화면을 가리지 않고, 바깥을 누르면 닫히게만
        var dim = CreateImage("Dim", popup, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var safe = CreateRect("SafeArea", popup);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        // 이동 버튼 위, 오른쪽 아래 모서리에서 튀어나오도록 pivot을 모서리에 둠
        var panel = CreateImage("Panel", safe, LoadPanelSprite(), true);
        panel.type = Image.Type.Sliced;
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(1, 0);
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(1, 0);
        panelRect.sizeDelta = new Vector2(TravelPanelWidth, TravelPanelHeight);
        panelRect.anchoredPosition = new Vector2(-BarSideMargin, BarBottom + BarHeight + 26);
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var title = CreateText("Title", panelRect, _titleFont, "어디로 갈까?", 48, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.offsetMin = new Vector2(52, -108);
        titleRect.offsetMax = new Vector2(-52, -44);

        var cards = CreateRect("Cards", panelRect);
        cards.anchorMin = Vector2.zero;
        cards.anchorMax = Vector2.one;
        cards.offsetMin = new Vector2(40, 40);
        cards.offsetMax = new Vector2(-40, -128);
        var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(262, 240);
        grid.spacing = new Vector2(26, 34);
        grid.padding = new RectOffset(0, 0, 16, 0); // "현재 위치" 뱃지가 카드 위로 튀어나오는 만큼
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        var animator = popup.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", panelRect);
        Set(animator, "_panelGroup", panelGroup);
        Set(animator, "_dimButton", dimButton);

        var view = popup.gameObject.AddComponent<TravelPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_cardPrefab", cardPrefab);
        Set(view, "_cardParent", cards);

        popup.gameObject.SetActive(false);
        return view;
    }

    private static ScreenFader BuildFader(RectTransform parent)
    {
        var fader = CreateImage("ScreenFader", parent, null, true);
        Stretch(fader.rectTransform, 0);
        fader.color = Color.black;
        var group = fader.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        fader.rectTransform.SetAsLastSibling();
        return fader.gameObject.AddComponent<ScreenFader>();
    }

    // 장소 씬에는 EventSystem이 없어서 전역 UI가 함께 가지고 다닌다
    private static void BuildEventSystem(RectTransform parent)
    {
        var go = new GameObject("EventSystem");
        go.transform.SetParent(parent, false);
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    #endregion

    #region 장소 씬 배치

    /// <summary>
    /// 광장/밭/낚시터에 GlobalUI 프리팹을 배치한다 (편집 모드에서도 보이게). 매니저가 없는 씬에는 매니저도 넣는다.
    /// 이미 있는 것은 건드리지 않고, 바뀐 씬만 저장한다.
    /// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod GlobalUISetup.PlaceInZoneScenes -quit
    /// </summary>
    [MenuItem("Tools/Navigation/Place In Zone Scenes")]
    public static void PlaceInZoneScenes()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GlobalUISetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalUIPrefabPath);
        if (prefab == null)
            throw new FileNotFoundException($"먼저 Build Global UI를 실행하세요: {GlobalUIPrefabPath}");

        foreach (var path in ZoneScenes)
        {
            if (!File.Exists(path))
                continue;

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var added = new List<string>();

            // 전역 UI(가방 N 뱃지 등)가 매니저를 필요로 하므로 없으면 함께 배치 (밭·낚시터와 같은 설정)
            if (!roots.Any(go => go.GetComponentInChildren<CurrencyManager>(true) != null))
            {
                var manager = new GameObject("CurrencyManager").AddComponent<CurrencyManager>();
                var so = new SerializedObject(manager);
                var list = so.FindProperty("_allCurrencies");
                list.arraySize = CurrencyPaths.Length;
                for (int i = 0; i < CurrencyPaths.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Currency>(CurrencyPaths[i]);
                so.ApplyModifiedPropertiesWithoutUndo();
                added.Add("CurrencyManager");
            }

            if (!roots.Any(go => go.GetComponentInChildren<InventoryManager>(true) != null))
            {
                var manager = new GameObject("InventoryManager").AddComponent<InventoryManager>();
                Set(manager, "_config", AssetDatabase.LoadAssetAtPath<InventoryConfig>(InventoryConfigPath));
                added.Add("InventoryManager");
            }

            if (!roots.Any(go => go.GetComponent<GlobalUIRoot>() != null))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "GlobalUI";
                added.Add("GlobalUI");
            }

            // 전역 UI가 EventSystem을 가지고 있으므로 씬에 따로 있으면 겹침
            foreach (var go in roots.Where(go => go.GetComponent<GlobalUIRoot>() == null && go.GetComponentInChildren<EventSystem>(true) != null))
                Debug.LogWarning($"[GlobalUISetup] {path}에 EventSystem이 따로 있습니다 ({go.name}). 전역 UI의 것과 겹치니 지워 주세요.");

            if (added.Count == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GlobalUISetup] {path}: {string.Join(", ", added)} 배치");
        }

        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
    }

    #endregion

    #region Build Settings

    private static void RegisterBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var path in ZoneScenes)
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            else
                Debug.LogWarning($"[GlobalUISetup] 씬이 없습니다: {path}");
        }

        // 기존에 등록된 다른 씬은 뒤에 유지 (광장이 첫 씬)
        foreach (var existing in EditorBuildSettings.scenes)
        {
            if (!scenes.Any(s => s.path == existing.path))
                scenes.Add(existing);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    #endregion

    #region 도우미

    internal static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null)
            throw new System.MissingFieldException($"{target.GetType().Name}.{property}");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    internal static Image CreateImage(string name, Transform parent, Sprite sprite, bool raycast)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycast;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        return image;
    }

    internal static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        var rect = CreateRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    internal static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // 한 점에 고정 (anchor = pivot)
    internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    // 아래쪽 가로 띠 (bottom-stretch)
    internal static void BottomBand(RectTransform rect, float bottom, float height)
    {
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.offsetMin = new Vector2(0, bottom);
        rect.offsetMax = new Vector2(0, bottom + height);
    }

    // 아이콘은 없어도 진행 (글자만 표시). 새로 넣은 PNG는 스프라이트로 가져오도록 설정
    internal static Sprite LoadIcon(string folder, string name)
    {
        string path = $"{folder}/{name}.png";
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GlobalUISetup] 아이콘 파일이 없어 글자만 표시합니다: {path}");
            return null;
        }

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    internal static Sprite LoadSprite(string folder, string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
        if (sprite == null)
            throw new FileNotFoundException($"스프라이트를 찾을 수 없습니다: {folder}/{name}");
        return sprite;
    }

    // 패널 텍스처는 Multiple 모드 → 하위 스프라이트 _0 (9-slice 테두리 포함)
    internal static Sprite LoadPanelSprite()
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath($"{InventorySpriteFolder}/UI_Inventory_Panel.png")
            .OfType<Sprite>()
            .FirstOrDefault(s => s.name == "UI_Inventory_Panel_0");
        if (sprite == null)
            throw new FileNotFoundException("스프라이트를 찾을 수 없습니다: UI_Inventory_Panel_0");
        return sprite;
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
