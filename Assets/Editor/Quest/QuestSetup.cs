using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 퀘스트 데이터(퀘스트·DB), 목록 한 줄 프리팹, 화면(QuestScreen)을 만든다. GlobalUISetup.Run이 함께 호출한다.
/// 이미 있는 퀘스트 에셋은 사람이 고친 문구·수치를 지키기 위해 덮어쓰지 않고, 비어 있는 그림만 채운다.
/// </summary>
public static class QuestSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Quest";
    private const string QuestFolder = DataFolder + "/Quests";
    internal const string DatabasePath = DataFolder + "/QuestDatabase.asset";
    private const string RowPrefabPath = "Assets/Prefab/Quest/QuestRow.prefab";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string OtterTabPath = "Assets/Scriptable Obejects/Collection/Tabs/Tab_Otter.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string PlaceIconFolder = "Assets/Art/UI/Travel";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);

    // 아이콘: "item:경로" (아이템 아이콘), "gold" (골드 아이콘), "place:파일", "otter:파일"
    // 제목·조건·보상은 시안용 예시 (실제 목록은 기획이 정하면 에셋에서 고친다)
    private static readonly (string asset, string id, string title, string description, QuestGoalType type, int goal, string filter, int reward, string icon)[] Quests =
    {
        ("Quest_Harvest_1", "quest_harvest_1", "내가 키운 첫 수확", "작물 3개 수확하기", QuestGoalType.Harvest, 3, "Crops", 100, "item:Farming/당근"),
        ("Quest_Catch_1", "quest_catch_1", "오늘부터 낚시왕", "물고기 3마리 낚기", QuestGoalType.Catch, 3, "Fish", 100, "item:Fishing/고등어"),
        ("Quest_Sales_1", "quest_sales_1", "티끌 모아 왕국", "판매로 500골드 벌기", QuestGoalType.EarnFromSales, 500, "", 150, "gold"),
        ("Quest_Upgrade_1", "quest_upgrade_1", "더 좋은 도구가 필요해", "생산 업그레이드 1회", QuestGoalType.Upgrade, 1, "", 200, "place:ICON_Place_Farm"),
        ("Quest_Otter_1", "quest_otter_1", "처음 뵙겠습니다!", "새로운 해달 1마리 만나기", QuestGoalType.CollectionRegister, 1, "otter", 200, "otter:ICON_Otter_Fisher"),
        ("Quest_Harvest_2", "quest_harvest_2", "부지런한 농부", "작물 30개 수확하기", QuestGoalType.Harvest, 30, "Crops", 300, "item:Farming/감자"),
        ("Quest_Catch_2", "quest_catch_2", "바다의 단골손님", "물고기 20마리 낚기", QuestGoalType.Catch, 20, "Fish", 300, "item:Fishing/고등어"),
        ("Quest_Sales_2", "quest_sales_2", "왕국의 살림꾼", "판매로 3,000골드 벌기", QuestGoalType.EarnFromSales, 3000, "", 500, "gold"),
        ("Quest_Upgrade_2", "quest_upgrade_2", "장인의 손길", "생산 업그레이드 3회", QuestGoalType.Upgrade, 3, "", 500, "place:ICON_Place_FishingSpot"),
        ("Quest_Otter_2", "quest_otter_2", "해달 친구들", "해달 2마리 만나기", QuestGoalType.CollectionRegister, 2, "otter", 500, "otter:ICON_Otter_Farmer"),
    };

    #region 데이터

    public static void CreateData()
    {
        EnsureFolder(QuestFolder);
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);

        for (int i = 0; i < Quests.Length; i++)
        {
            var q = Quests[i];
            var (quest, isNew) = LoadOrCreate<QuestDefinition>($"{QuestFolder}/{q.asset}.asset");
            var so = new SerializedObject(quest);
            if (isNew)
            {
                so.FindProperty("_questId").stringValue = q.id;
                so.FindProperty("_title").stringValue = q.title;
                so.FindProperty("_description").stringValue = q.description;
                so.FindProperty("_sortOrder").intValue = i;
                so.FindProperty("_goalType").enumValueIndex = (int)q.type;
                so.FindProperty("_goal").intValue = q.goal;
                so.FindProperty("_rewardCurrency").objectReferenceValue = gold;
                so.FindProperty("_rewardAmount").intValue = q.reward;
                if (q.filter == "otter")
                    so.FindProperty("_collectionTab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CollectionTab>(OtterTabPath);
                else if (!string.IsNullOrEmpty(q.filter))
                    so.FindProperty("_itemFilter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/{q.filter}.asset");
            }
            FillIfEmpty(so, "_icon", LoadQuestIcon(q.icon, gold));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // DB는 항상 폴더 안의 모든 퀘스트로 다시 채움 (사람이 추가한 퀘스트도 포함)
        var (database, _) = LoadOrCreate<QuestDatabase>(DatabasePath);
        var dbSo = new SerializedObject(database);
        SetList(dbSo.FindProperty("_quests"), FindAll<QuestDefinition>(QuestFolder));
        dbSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    private static Sprite LoadQuestIcon(string icon, Currency gold)
    {
        int colon = icon.IndexOf(':');
        string kind = colon < 0 ? icon : icon.Substring(0, colon);
        string value = colon < 0 ? "" : icon.Substring(colon + 1);

        switch (kind)
        {
            case "item":
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{value}.asset");
                if (item == null)
                    Debug.LogWarning($"[QuestSetup] 아이템을 찾을 수 없습니다: {value}");
                return item != null ? item.Icon : null;
            case "gold":
                return gold != null ? gold.Icon : null;
            case "place":
                return LoadIcon(PlaceIconFolder, value);
            case "otter":
                return ImportSprite(OtterFolder, value);
            default:
                throw new System.ArgumentException($"알 수 없는 아이콘 종류: {icon}");
        }
    }

    #endregion

    #region 프리팹 (목록 한 줄)

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(RowPrefabPath).Replace('\\', '/'));

        var root = new GameObject("QuestRow", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(936, 166);
        var background = root.AddComponent<Image>();
        background.sprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        var group = root.AddComponent<CanvasGroup>();
        var layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 166;

        // 왼쪽 살구색 그림 칸
        var frame = CreateImage("IconFrame", rect, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 2), new Vector2(140, 140));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        Stretch(icon.rectTransform, 0);
        icon.rectTransform.offsetMin = new Vector2(20, 26); // 칸 아래쪽 입체 턱만큼 위로
        icon.rectTransform.offsetMax = new Vector2(-20, -16);
        icon.preserveAspect = true;
        var check = CreateImage("ClaimedCheck", frame.rectTransform, ImportSprite(CollectionSpriteFolder, "UI_Icon_CheckSmall"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(12, 12), new Vector2(56, 56));
        check.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true; // 줄이 흐려져도 체크는 또렷하게

        // 제목, 조건 (보상 칸과 겹치지 않도록 오른쪽 380은 비움)
        var title = CreateText("Title", rect, _titleFont, "퀘스트 제목", 36, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        FitText(title, 28, 36);
        TopBand(title.rectTransform, 172, 380, 20, 50);

        var description = CreateText("Description", rect, _bodyFont, "조건", 27, Body);
        description.alignment = TextAlignmentOptions.Left;
        FitText(description, 20, 27);
        TopBand(description.rectTransform, 174, 380, 70, 38);

        var bar = BuildBar(rect, "ProgressBar");
        Place((RectTransform)bar.transform, new Vector2(0, 1), new Vector2(174, -116), new Vector2(236, 30));

        // "320 / 500"처럼 길어지면 줄바꿈 대신 글자를 줄임
        var progress = CreateText("ProgressText", rect, _titleFont, "0 / 3", 26, Body);
        progress.alignment = TextAlignmentOptions.Left;
        FitText(progress, 18, 26);
        Place(progress.rectTransform, new Vector2(0, 1), new Vector2(422, -110), new Vector2(148, 42));

        // 보상: 코인 아이콘 + 금액 (버튼 왼쪽 열)
        var rewardIcon = CreateImage("RewardIcon", rect, null, false);
        Place(rewardIcon.rectTransform, new Vector2(1, 1), new Vector2(-270, -20), new Vector2(62, 62));
        rewardIcon.preserveAspect = true;
        var reward = CreateText("RewardText", rect, _titleFont, "100", 32, Cocoa);
        Place(reward.rectTransform, new Vector2(1, 1), new Vector2(-236, -90), new Vector2(130, 44));

        var claimable = ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold");
        var idle = ImportSprite(CommonSpriteFolder, "UI_Button_Idle");
        var buttonImage = CreateImage("ClaimButton", rect, idle, true);
        buttonImage.pixelsPerUnitMultiplier = 1.3f; // 버튼이 낮아서 테두리를 조금 얇게
        Place(buttonImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 4), new Vector2(212, 92));
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var colors = button.colors;
        colors.disabledColor = Color.white; // 진행 중·완료는 회색 스프라이트로 표현하므로 따로 흐리게 하지 않음
        button.colors = colors;
        var label = CreateText("Label", buttonImage.rectTransform, _titleFont, "진행 중", 32, LightBrown);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8);

        var view = root.AddComponent<QuestRowView>();
        Set(view, "_icon", icon);
        Set(view, "_titleText", title);
        Set(view, "_descriptionText", description);
        Set(view, "_progressBar", bar);
        Set(view, "_progressText", progress);
        Set(view, "_rewardIcon", rewardIcon);
        Set(view, "_rewardText", reward);
        Set(view, "_button", button);
        Set(view, "_buttonImage", buttonImage);
        Set(view, "_buttonLabel", label);
        Set(view, "_claimedCheck", check.gameObject);
        Set(view, "_group", group);
        Set(view, "_claimableSprite", claimable);
        Set(view, "_idleSprite", idle);

        check.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
    }

    // 크림색 홈 + 초록 채움. 채움 길이는 ProgressBarView가 정함
    private static ProgressBarView BuildBar(Transform parent, string name)
    {
        var track = CreateImage(name, parent, LoadSprite(CommonSpriteFolder, "UI_Slider_Track"), false);
        track.pixelsPerUnitMultiplier = 2f; // 바가 낮아서 테두리를 얇게
        var fill = CreateImage("Fill", track.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        var view = track.gameObject.AddComponent<ProgressBarView>();
        Set(view, "_fill", fill.rectTransform);
        return view;
    }

    private static void FitText(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    #endregion

    #region 화면

    /// <summary>
    /// 퀘스트 화면을 canvas 아래에 만든다 (닫힌 채로 시작). 1080×1920 기준, 시안 수치를 환산.
    /// </summary>
    public static (QuestPresenter presenter, UIPopupAnimator screen) BuildScreen(RectTransform canvas)
    {
        // 씬을 연 뒤 호출되므로 에셋은 경로로 다시 불러온다
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath).GetComponent<QuestRowView>();

        var screen = CreateRect("QuestScreen", canvas);
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
        var safeGroup = safe.gameObject.AddComponent<CanvasGroup>();

        var panel = CreateImage("Panel", safe, LoadPanelSprite(), false);
        panel.type = Image.Type.Sliced;
        Stretch(panel.rectTransform, 0);
        panel.rectTransform.offsetMin = new Vector2(32, 40);
        panel.rectTransform.offsetMax = new Vector2(-32, -110);
        var panelRect = panel.rectTransform;

        BuildHeader(panelRect);
        var (completedText, completedBar) = BuildOverall(panelRect);
        var (scroll, content) = BuildList(panelRect);
        var (claimAll, claimAllImage, claimAllLabel) = BuildClaimAll(panelRect);

        BuildTitleBoard(safe);
        var close = BuildCloseButton(safe);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", safe);
        Set(animator, "_panelGroup", safeGroup);
        Set(animator, "_dimButton", dimButton);
        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("_startScale").floatValue = 0.92f; // 도감처럼 은은하게
        animatorSo.FindProperty("_overshoot").floatValue = 1.2f;
        animatorSo.FindProperty("_endScale").floatValue = 0.95f;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        var presenter = safe.gameObject.AddComponent<QuestPresenter>();
        Set(presenter, "_rowPrefab", rowPrefab);
        Set(presenter, "_rowParent", content);
        Set(presenter, "_scrollRect", scroll);
        Set(presenter, "_completedText", completedText);
        Set(presenter, "_completedBar", completedBar);
        Set(presenter, "_claimAllButton", claimAll);
        Set(presenter, "_claimAllImage", claimAllImage);
        Set(presenter, "_claimAllLabel", claimAllLabel);
        Set(presenter, "_closeButton", close);
        Set(presenter, "_claimAllActiveSprite", ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"));
        Set(presenter, "_claimAllIdleSprite", ImportSprite(CommonSpriteFolder, "UI_Button_Idle"));
        Set(presenter, "_screen", animator);

        screen.gameObject.SetActive(false);
        return (presenter, animator);
    }

    // "퀘스트" 나무 간판: 패널 윗선에 걸침, 왼쪽에 두루마리 아이콘, 오른쪽에 잎
    private static void BuildTitleBoard(RectTransform parent)
    {
        var board = CreateImage("TitleBoard", parent, ImportSprite(CollectionSpriteFolder, "UI_TitleBoard"), false);
        Place(board.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(440, 124));

        var title = CreateText("Title", board.rectTransform, _titleFont, "퀘스트", 68, Cocoa);
        Stretch(title.rectTransform, 0);
        title.rectTransform.offsetMin = new Vector2(70, 10); // 아이콘 자리만큼 오른쪽으로

        var icon = CreateImage("Icon", board.rectTransform, LoadIcon(NavIconFolder, "ICON_Nav_Quest"), false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-138, 8), new Vector2(112, 112));
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.preserveAspect = true;

        var leaf = CreateImage("Leaf", board.rectTransform, ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf"), false);
        Place(leaf.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(196, 6), new Vector2(52, 52));
        leaf.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        leaf.rectTransform.localScale = new Vector3(-1, 1, 1);
    }

    // 해달 그림 + "왕국 성장" + 부제
    private static void BuildHeader(RectTransform panel)
    {
        var frame = CreateImage("HeaderArt", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 1), new Vector2(48, -92), new Vector2(330, 224));
        var otter = CreateImage("Otter", frame.rectTransform, ImportSprite(OtterFolder, "ICON_Otter_Farmer"), false);
        Stretch(otter.rectTransform, 0);
        otter.rectTransform.offsetMin = new Vector2(24, 30);
        otter.rectTransform.offsetMax = new Vector2(-24, -14);
        otter.preserveAspect = true;

        var title = CreateText("Title", panel, _titleFont, "왕국 성장", 64, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        TopBand(title.rectTransform, 410, 48, 112, 84);

        var subtitle = CreateText("Subtitle", panel, _bodyFont, "해달들과 함께 왕국을 키워요!", 30, LightBrown);
        subtitle.alignment = TextAlignmentOptions.Left;
        FitText(subtitle, 22, 30);
        TopBand(subtitle.rectTransform, 412, 40, 206, 48);
    }

    // "완료한 퀘스트 [바] 2 / 10"
    private static (TextMeshProUGUI text, ProgressBarView bar) BuildOverall(RectTransform panel)
    {
        var box = CreateImage("Overall", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        TopBand(box.rectTransform, 40, 40, 342, 96);
        var rect = box.rectTransform;

        var label = CreateText("Label", rect, _titleFont, "완료한 퀘스트", 32, Body);
        label.alignment = TextAlignmentOptions.Left;
        Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(36, 4), new Vector2(230, 50));

        var bar = BuildBar(rect, "Bar");
        var barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0, 0.5f);
        barRect.anchorMax = new Vector2(1, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.offsetMin = new Vector2(270, -14);
        barRect.offsetMax = new Vector2(-176, 22);

        var count = CreateText("Count", rect, _titleFont, "0 / 0", 34, Cocoa);
        count.alignment = TextAlignmentOptions.Right;
        Place(count.rectTransform, new Vector2(1, 0.5f), new Vector2(-36, 4), new Vector2(130, 50));
        return (count, bar);
    }

    private static (ScrollRect scroll, RectTransform content) BuildList(RectTransform panel)
    {
        var scrollImage = CreateImage("ScrollView", panel, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = new Vector2(40, 196);
        scrollImage.rectTransform.offsetMax = new Vector2(-40, -458);

        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14;
        layout.padding = new RectOffset(0, 0, 6, 10); // 체크 표시가 칸 위로 튀어나오는 만큼
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    private static (Button button, Image image, TextMeshProUGUI label) BuildClaimAll(RectTransform panel)
    {
        var image = CreateImage("ClaimAllButton", panel, ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"), true);
        Place(image.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 54), new Vector2(500, 116));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = Color.white; // 받을 것이 없으면 회색 스프라이트로 표현
        button.colors = colors;

        var label = CreateText("Label", image.rectTransform, _titleFont, "모두 받기", 46, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 10);

        var leaf = ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf");
        var left = CreateImage("LeafLeft", image.rectTransform, leaf, false);
        Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-290, 4), new Vector2(60, 60));
        left.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var right = CreateImage("LeafRight", image.rectTransform, leaf, false);
        Place(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(290, 4), new Vector2(60, 60));
        right.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        right.rectTransform.localScale = new Vector3(-1, 1, 1);

        return (button, image, label);
    }

    #endregion
}
