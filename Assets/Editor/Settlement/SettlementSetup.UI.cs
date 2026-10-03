using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 정착 진행 화면 (1080×1920 기준, 시안 수치): 상단바 아래 칩, 오른쪽 위 안내 띠, 게시판, 건설 팝업, 완료 팝업, 진행 말풍선.
/// </summary>
public static partial class SettlementSetup
{
    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    private static readonly Color Green = new Color32(0x5E, 0x8C, 0x4A, 0xFF);

    public struct Screens
    {
        public SettlementStatusView status;
        public SettlementGuideView guide;
        public ConstructionProgressView progress;
        public SettlementBoardView board;
        public ConstructionPopupView construction;
        public SettlementTaskPopupView task;
        public ConstructionCompletePopupView complete;
    }

    /// <summary>HUD 요소(칩·안내 띠·진행 말풍선)</summary>
    public static Screens BuildHud(RectTransform hud, RectTransform canvas)
    {
        return new Screens
        {
            status = BuildStatus(hud),
            guide = BuildGuide(hud),
            progress = BuildProgress(canvas),
        };
    }

    /// <summary>팝업 (게시판 → 건설 → 주민 작업 → 완료 순서로 위에 그려짐)</summary>
    public static void BuildPopups(RectTransform canvas, ref Screens screens)
    {
        screens.board = BuildBoard(canvas);
        screens.construction = BuildConstructionPopup(canvas);
        screens.task = BuildTaskPopup(canvas);
        screens.complete = BuildCompletePopup(canvas);
    }

    private static Sprite Common(string name) => LoadSprite(CommonSpriteFolder, name);

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, float minSize = 0f)
    {
        var label = CreateText(name, parent, font, text, size, color);
        label.alignment = alignment;
        if (minSize > 0f)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = size;
        }
        return label;
    }

    // 닫기 버튼을 패널 오른쪽 위 모서리에 걸치고(시안: 84, 모서리에 걸침) 맨 위에 그림 — 탭·글자에 가리지 않게
    private static void CornerClose(Button close)
    {
        var rect = (RectTransform)close.transform;
        Place(rect, new Vector2(1, 1), new Vector2(26, 26), new Vector2(88, 88));
        rect.SetAsLastSibling();
    }

    // 왼쪽 위 고정 (pivot도 왼쪽 위)
    private static void TopLeft(RectTransform rect, float x, float y, float w, float h) =>
        Place(rect, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

    #region 칩 · 안내 띠

    private static SettlementStatusView BuildStatus(RectTransform hud)
    {
        var chip = CreateImage("SettlementStatus", hud, Common("UI_Button_Paper"), false);
        chip.pixelsPerUnitMultiplier = 1.3f;
        TopLeft(chip.rectTransform, 30, 206, 300, 88);

        var icon = CreateImage("Icon", chip.rectTransform, LoadArt("ICON_House_Blue"), false);
        icon.preserveAspect = true;
        TopLeft(icon.rectTransform, 14, 10, 64, 64);

        var stage = Label("Stage", chip.rectTransform, _titleFont, "빈터", 28, Cocoa, TextAlignmentOptions.Left, 18);
        TopLeft(stage.rectTransform, 88, 8, 196, 38);
        var residents = Label("Residents", chip.rectTransform, _bodyFont, "주민 0", 24, LightBrown, TextAlignmentOptions.Left);
        TopLeft(residents.rectTransform, 88, 44, 196, 32);

        var view = chip.gameObject.AddComponent<SettlementStatusView>();
        Set(view, "_stageText", stage);
        Set(view, "_residentText", residents);
        return view;
    }

    private static SettlementGuideView BuildGuide(RectTransform hud)
    {
        var banner = CreateImage("SettlementGuide", hud, Common("UI_Button_Paper"), false);
        banner.pixelsPerUnitMultiplier = 1.3f;
        Place(banner.rectTransform, new Vector2(1, 1), new Vector2(-24, -206), new Vector2(500, 96));

        var icon = CreateImage("Icon", banner.rectTransform, LoadArt("ICON_Board"), false);
        icon.preserveAspect = true;
        TopLeft(icon.rectTransform, 12, 12, 72, 72);

        var message = Label("Message", banner.rectTransform, _bodyFont, "게시판을 확인해요", 28, Cocoa, TextAlignmentOptions.Left, 18);
        message.textWrappingMode = TextWrappingModes.Normal;
        TopLeft(message.rectTransform, 92, 8, 230, 80);

        var buttonImage = CreateImage("Button", banner.rectTransform, Common("UI_Ribbon_Peach"), true);
        buttonImage.pixelsPerUnitMultiplier = 1.6f;
        Place(buttonImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(160, 70));
        buttonImage.raycastPadding = new Vector4(-10, -10, -10, -10);
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var label = Label("Label", buttonImage.rectTransform, _titleFont, "확인하기", 26, Cocoa, TextAlignmentOptions.Center, 18);
        Stretch(label.rectTransform, 6);
        label.rectTransform.offsetMin = new Vector2(6, 10);

        var view = banner.gameObject.AddComponent<SettlementGuideView>();
        Set(view, "_root", banner.gameObject);
        Set(view, "_messageText", message);
        Set(view, "_button", button);
        Set(view, "_buttonLabel", label);
        return view;
    }

    #endregion

    #region 진행 말풍선

    private static ConstructionProgressView BuildProgress(RectTransform canvas)
    {
        var layer = CreateRect("ConstructionProgress", canvas);
        Stretch(layer, 0);

        var rootImage = CreateImage("Bubble", layer, Common("UI_Button_Paper"), false);
        var root = rootImage.rectTransform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f); // 현장 위에 바닥을 붙임
        root.sizeDelta = new Vector2(440, 170);

        var labelText = Label("Label", root, _titleFont, "농경지 개간 중", 30, Cocoa);
        TopLeft(labelText.rectTransform, 20, 12, 400, 44);

        var track = CreateImage("Bar", root, Common("UI_Slider_Track"), false);
        track.pixelsPerUnitMultiplier = 2f;
        TopLeft(track.rectTransform, 30, 66, 290, 32);
        var fill = CreateImage("Fill", track.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        var bar = track.gameObject.AddComponent<ProgressBarView>();
        Set(bar, "_fill", fill.rectTransform);

        var percent = Label("Percent", root, _titleFont, "0%", 28, Cocoa, TextAlignmentOptions.Left);
        TopLeft(percent.rectTransform, 330, 62, 90, 40);
        var time = Label("Time", root, _bodyFont, "00:00", 26, Body);
        TopLeft(time.rectTransform, 20, 110, 400, 40);

        var hint = CreateImage("Hint", root, Common("UI_Button_Paper"), false);
        hint.pixelsPerUnitMultiplier = 1.6f;
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -66), new Vector2(420, 56));
        var hintText = Label("Text", hint.rectTransform, _bodyFont, "완료하면 첫 밭이 열려요", 24, Green, TextAlignmentOptions.Center, 16);
        Stretch(hintText.rectTransform, 8);

        var view = layer.gameObject.AddComponent<ConstructionProgressView>();
        Set(view, "_root", root);
        Set(view, "_labelText", labelText);
        Set(view, "_bar", bar);
        Set(view, "_percentText", percent);
        Set(view, "_timeText", time);
        Set(view, "_hint", hint.gameObject);
        Set(view, "_hintText", hintText);
        root.gameObject.SetActive(false);
        return view;
    }

    #endregion

    #region 팝업 공통

    // 화면 전체 딤(누르면 닫힘) + SafeArea + 패널. 패널은 호출한 쪽이 배치
    private static (RectTransform screen, RectTransform panel, UIPopupAnimator animator) BuildPopupShell(RectTransform canvas, string name, Sprite panelSprite)
    {
        var screen = CreateRect(name, canvas);
        Stretch(screen, 0);

        var dim = CreateImage("Dim", screen, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.55f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        var panelImage = CreateImage("Panel", safe, panelSprite, true);
        panelImage.type = Image.Type.Sliced;
        var panel = panelImage.rectTransform;
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", panel);
        Set(animator, "_panelGroup", panelGroup);
        Set(animator, "_dimButton", dimButton);
        return (screen, panel, animator);
    }

    private static (Button button, TextMeshProUGUI label, Image image) BuildButton(RectTransform parent, string name, Sprite sprite, string text, float fontSize, Color color)
    {
        var image = CreateImage(name, parent, sprite, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label("Label", image.rectTransform, _titleFont, text, fontSize, color, TextAlignmentOptions.Center, fontSize * 0.6f);
        Stretch(label.rectTransform, 8);
        label.rectTransform.offsetMin = new Vector2(8, 14); // 버튼 아래 입체 턱만큼 위로
        return (button, label, image);
    }

    private static CostChipView BuildCostChip(RectTransform parent, string name, Vector2 size, bool withCheck)
    {
        var bg = CreateImage(name, parent, Common("UI_Chip_Normal"), false);
        bg.pixelsPerUnitMultiplier = 1.4f;
        var element = bg.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;

        float iconSize = Mathf.Min(size.y - 16, 64);
        float checkSize = Mathf.Min(size.y - 22, 54);
        var icon = CreateImage("Icon", bg.rectTransform, null, false);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(iconSize, iconSize));

        float right = withCheck ? checkSize + 14 : 12;
        var amount = Label("Amount", bg.rectTransform, _titleFont, "0", size.y * 0.42f, Cocoa, TextAlignmentOptions.Center, 18);
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        amount.rectTransform.anchorMin = Vector2.zero;
        amount.rectTransform.anchorMax = Vector2.one;
        amount.rectTransform.offsetMin = new Vector2(iconSize + 12, 4);
        amount.rectTransform.offsetMax = new Vector2(-right, -2);

        GameObject check = null;
        if (withCheck)
        {
            var checkImage = CreateImage("Check", bg.rectTransform, Common("UI_Button_Confirm"), false);
            Place(checkImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(checkSize, checkSize));
            check = checkImage.gameObject;
        }

        var view = bg.gameObject.AddComponent<CostChipView>();
        Set(view, "_icon", icon);
        Set(view, "_amountText", amount);
        if (check != null)
            Set(view, "_check", check);
        return view;
    }

    private static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment)
    {
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    // 세로 스크롤 목록 (카드 템플릿을 넣을 Content를 돌려줌)
    private static RectTransform BuildList(RectTransform area, float spacing)
    {
        var scrollImage = CreateImage("ScrollView", area, null, true);
        scrollImage.color = new Color(1, 1, 1, 0);
        Stretch(scrollImage.rectTransform, 0);

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
        layout.spacing = spacing;
        layout.padding = new RectOffset(4, 4, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    #endregion

    #region 게시판

    private static SettlementBoardView BuildBoard(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "SettlementBoard", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(940, 1240));
        panel.pivot = new Vector2(0.5f, 0.5f);

        var titleBoard = CreateImage("TitleBoard", panel, ImportSprite(CollectionSpriteFolder, "UI_TitleBoard"), false);
        Place(titleBoard.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 54), new Vector2(520, 124));
        var title = Label("Title", titleBoard.rectTransform, _titleFont, "해달 게시판", 50, Cocoa);
        Stretch(title.rectTransform, 10);
        title.rectTransform.offsetMin = new Vector2(10, 18);

        var close = BuildCloseButton(panel);

        // 탭
        var selected = ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold");
        var normal = Common("UI_Button_Paper");
        var (guestTab, _, guestImage) = BuildButton(panel, "GuestbookTab", selected, "방명록", 34, Cocoa);
        Place(guestImage.rectTransform, new Vector2(0.5f, 1), new Vector2(-212, -98), new Vector2(404, 96));
        var (requestTab, _, requestImage) = BuildButton(panel, "RequestTab", normal, "해달의 부탁", 34, Cocoa);
        Place(requestImage.rectTransform, new Vector2(0.5f, 1), new Vector2(212, -98), new Vector2(404, 96));
        var dot = CreateImage("Dot", requestImage.rectTransform, Common("UI_Badge"), false);
        Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-10, -6), new Vector2(34, 34));

        // 방명록 페이지
        var guestPage = CreateRect("GuestbookPage", panel);
        Stretch(guestPage, 0);
        guestPage.offsetMin = new Vector2(44, 44);
        guestPage.offsetMax = new Vector2(-44, -214);

        var guestListArea = CreateRect("ListArea", guestPage);
        Stretch(guestListArea, 0);
        guestListArea.offsetMin = new Vector2(0, 232);
        var guestContent = BuildList(guestListArea, 18);
        var guestCard = BuildGuestbookCard(guestContent);

        var visits = Label("VisitCount", guestPage, _bodyFont, "방문 기록 1", 28, Body);
        Place(visits.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(500, 44));

        var notice = CreateImage("Notice", guestPage, Common("UI_Button_Paper"), false);
        BottomBand(notice.rectTransform, 0, 160);
        var noticePortrait = CreateImage("Portrait", notice.rectTransform, null, false);
        noticePortrait.preserveAspect = true;
        Place(noticePortrait.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 4), new Vector2(124, 124));
        var noticeText = Label("Text", notice.rectTransform, _bodyFont, "새로운 부탁이 도착했어요.", 28, Cocoa, TextAlignmentOptions.Left, 18);
        noticeText.rectTransform.anchorMin = new Vector2(0, 0);
        noticeText.rectTransform.anchorMax = new Vector2(1, 1);
        noticeText.rectTransform.offsetMin = new Vector2(152, 10);
        noticeText.rectTransform.offsetMax = new Vector2(-250, -10);
        var (noticeButton, _, noticeImage) = BuildButton(notice.rectTransform, "Button", Common("UI_Ribbon_Peach"), "부탁 보기 >", 28, Cocoa);
        noticeImage.pixelsPerUnitMultiplier = 1.4f;
        Place(noticeImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(220, 92));

        // 부탁 페이지
        var requestPage = CreateRect("RequestPage", panel);
        Stretch(requestPage, 0);
        requestPage.offsetMin = new Vector2(44, 44);
        requestPage.offsetMax = new Vector2(-44, -214);
        var requestContent = BuildList(requestPage, 18);
        var requestCard = BuildRequestCard(requestContent);
        var empty = Label("Empty", requestPage, _bodyFont, "아직 부탁이 없어요.", 30, LightBrown);
        Stretch(empty.rectTransform, 0);
        requestPage.gameObject.SetActive(false);

        var view = screen.gameObject.AddComponent<SettlementBoardView>();
        Set(view, "_animator", animator);
        Set(view, "_guestbookTab", guestTab);
        Set(view, "_guestbookTabImage", guestImage);
        Set(view, "_requestTab", requestTab);
        Set(view, "_requestTabImage", requestImage);
        Set(view, "_requestDot", dot.gameObject);
        Set(view, "_tabSelectedSprite", selected);
        Set(view, "_tabNormalSprite", normal);
        Set(view, "_guestbookPage", guestPage.gameObject);
        Set(view, "_guestbookCardPrefab", guestCard);
        Set(view, "_guestbookParent", guestContent);
        Set(view, "_visitCountText", visits);
        Set(view, "_notice", notice.gameObject);
        Set(view, "_noticePortrait", noticePortrait);
        Set(view, "_noticeText", noticeText);
        Set(view, "_noticeButton", noticeButton);
        Set(view, "_requestPage", requestPage.gameObject);
        Set(view, "_requestCardPrefab", requestCard);
        Set(view, "_requestParent", requestContent);
        Set(view, "_emptyRequests", empty.gameObject);
        Set(view, "_closeButton", close);

        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    // 목록 안의 꺼진 템플릿 (BindGuestbook이 복제해서 씀)
    private static GuestbookCardView BuildGuestbookCard(RectTransform content)
    {
        var card = CreateImage("GuestbookCardTemplate", content, Common("UI_Button_Paper"), false);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 236;

        var frame = CreateImage("Frame", card.rectTransform, Common("UI_Box_Inset"), false);
        TopLeft(frame.rectTransform, 22, 22, 184, 184);
        var portrait = CreateImage("Portrait", frame.rectTransform, null, false);
        portrait.preserveAspect = true;
        Stretch(portrait.rectTransform, 10);

        var tag = CreateImage("Tag", card.rectTransform, Common("UI_Tag_Highlight"), false);
        tag.pixelsPerUnitMultiplier = 1.6f;
        TopLeft(tag.rectTransform, 230, 24, 150, 48);
        var tagText = Label("Text", tag.rectTransform, _titleFont, "첫 방문", 24, Cocoa, TextAlignmentOptions.Center, 16);
        Stretch(tagText.rectTransform, 4);
        tagText.rectTransform.offsetMin = new Vector2(4, 8);

        var message = Label("Message", card.rectTransform, _bodyFont, "바닷바람이 포근한 곳이네요.", 30, Cocoa, TextAlignmentOptions.TopLeft, 20);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.rectTransform.anchorMin = new Vector2(0, 0);
        message.rectTransform.anchorMax = new Vector2(1, 1);
        message.rectTransform.offsetMin = new Vector2(230, 56);
        message.rectTransform.offsetMax = new Vector2(-120, -84);

        var name = Label("Name", card.rectTransform, _bodyFont, "몽실", 24, LightBrown, TextAlignmentOptions.Left);
        Place(name.rectTransform, new Vector2(0, 0), new Vector2(230, 16), new Vector2(300, 36));

        var paw = CreateImage("Paw", card.rectTransform, ImportSprite(CollectionSpriteFolder, "UI_Icon_Paw"), false);
        paw.preserveAspect = true;
        paw.color = new Color(1f, 1f, 1f, 0.55f);
        Place(paw.rectTransform, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(90, 90));

        var view = card.gameObject.AddComponent<GuestbookCardView>();
        Set(view, "_portrait", portrait);
        Set(view, "_tagText", tagText);
        Set(view, "_messageText", message);
        Set(view, "_nameText", name);
        card.gameObject.SetActive(false);
        return view;
    }

    private static BoardRequestCardView BuildRequestCard(RectTransform content)
    {
        var card = CreateImage("RequestCardTemplate", content, Common("UI_Button_Paper"), false);
        card.gameObject.AddComponent<LayoutElement>().preferredHeight = 214;

        var frame = CreateImage("Frame", card.rectTransform, Common("UI_Box_Inset"), false);
        TopLeft(frame.rectTransform, 20, 20, 168, 168);
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        icon.preserveAspect = true;
        Stretch(icon.rectTransform, 12);

        var title = Label("Title", card.rectTransform, _titleFont, "첫 번째 집 만들기", 34, Cocoa, TextAlignmentOptions.Left, 22);
        title.rectTransform.anchorMin = new Vector2(0, 1);
        title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.pivot = new Vector2(0, 1);
        title.rectTransform.offsetMin = new Vector2(208, -70);
        title.rectTransform.offsetMax = new Vector2(-220, -20);

        var description = Label("Description", card.rectTransform, _bodyFont, "", 25, Body, TextAlignmentOptions.TopLeft, 18);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.rectTransform.anchorMin = new Vector2(0, 0);
        description.rectTransform.anchorMax = new Vector2(1, 1);
        description.rectTransform.offsetMin = new Vector2(208, 50);
        description.rectTransform.offsetMax = new Vector2(-220, -76);

        var time = Label("Time", card.rectTransform, _titleFont, "00:30", 28, Green, TextAlignmentOptions.Left);
        Place(time.rectTransform, new Vector2(0, 0), new Vector2(208, 12), new Vector2(300, 40));

        var (button, label, buttonImage) = BuildButton(card.rectTransform, "StatusButton", Common("UI_Ribbon_Peach"), "보기", 30, Cocoa);
        buttonImage.pixelsPerUnitMultiplier = 1.4f;
        Place(buttonImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-22, -6), new Vector2(180, 92));

        var check = CreateImage("DoneCheck", card.rectTransform, Common("UI_Button_Confirm"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(-20, -14), new Vector2(52, 52));

        var view = card.gameObject.AddComponent<BoardRequestCardView>();
        Set(view, "_icon", icon);
        Set(view, "_titleText", title);
        Set(view, "_descriptionText", description);
        Set(view, "_timeText", time);
        Set(view, "_button", button);
        Set(view, "_buttonImage", buttonImage);
        Set(view, "_buttonLabel", label);
        Set(view, "_doneCheck", check.gameObject);
        Set(view, "_openSprite", Common("UI_Ribbon_Peach"));
        Set(view, "_buildingSprite", Common("UI_Button_Secondary"));
        Set(view, "_doneSprite", Common("UI_Button_Primary"));
        card.gameObject.SetActive(false);
        return view;
    }

    #endregion

    #region 건설 팝업

    private static ConstructionPopupView BuildConstructionPopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "ConstructionPopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(900, 600));

        var portraitBg = CreateImage("SpeakerFrame", panel, Common("UI_RoundButton_Cream"), false);
        Place(portraitBg.rectTransform, new Vector2(0, 1), new Vector2(18, 96), new Vector2(210, 210));
        var portrait = CreateImage("Portrait", portraitBg.rectTransform, null, false);
        portrait.preserveAspect = true;
        Stretch(portrait.rectTransform, 18);

        var speakerName = Label("SpeakerName", panel, _titleFont, "건설 해달", 40, Cocoa, TextAlignmentOptions.Left, 26);
        TopLeft(speakerName.rectTransform, 250, 30, 520, 54);
        var speakerLine = Label("SpeakerLine", panel, _bodyFont, "집을 짓고 길을 열어요.", 28, Body, TextAlignmentOptions.TopLeft, 18);
        speakerLine.textWrappingMode = TextWrappingModes.Normal;
        TopLeft(speakerLine.rectTransform, 250, 88, 560, 80);

        var close = BuildCloseButton(panel);

        var inner = CreateImage("Card", panel, Common("UI_Box_Inset"), false);
        TopBand(inner.rectTransform, 34, 34, 176, 384);

        var iconFrame = CreateImage("IconFrame", inner.rectTransform, Common("UI_Button_Paper"), false);
        TopLeft(iconFrame.rectTransform, 24, 24, 200, 200);
        var icon = CreateImage("Icon", iconFrame.rectTransform, null, false);
        icon.preserveAspect = true;
        Stretch(icon.rectTransform, 18);

        var name = Label("Name", inner.rectTransform, _titleFont, "새 이웃의 집", 36, Cocoa, TextAlignmentOptions.Left, 24);
        TopLeft(name.rectTransform, 248, 22, 520, 50);

        var costs = CreateRect("Costs", inner.rectTransform);
        TopLeft(costs, 248, 84, 540, 76);
        Row(costs, 12, TextAnchor.MiddleLeft);
        var chips = new List<CostChipView>
        {
            BuildCostChip(costs, "Cost1", new Vector2(172, 76), false),
            BuildCostChip(costs, "Cost2", new Vector2(172, 76), false),
            BuildCostChip(costs, "Cost3", new Vector2(172, 76), false),
        };

        var note = Label("Note", inner.rectTransform, _bodyFont, "30초 걸려요", 26, Body, TextAlignmentOptions.Left, 18);
        TopLeft(note.rectTransform, 248, 168, 540, 48);

        var (start, startLabel, startImage) = BuildButton(inner.rectTransform, "StartButton", Common("UI_Button_Coin"), "건설 시작", 40, Cocoa);
        TopLeft(startImage.rectTransform, 24, 248, 764, 112);

        var view = screen.gameObject.AddComponent<ConstructionPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_speakerPortrait", portrait);
        Set(view, "_speakerName", speakerName);
        Set(view, "_speakerLine", speakerLine);
        Set(view, "_icon", icon);
        Set(view, "_nameText", name);
        var chipList = new SerializedObject(view);
        var chipProp = chipList.FindProperty("_costChips");
        chipProp.arraySize = chips.Count;
        for (int i = 0; i < chips.Count; i++)
            chipProp.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
        chipList.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "_noteText", note);
        Set(view, "_startButton", start);
        Set(view, "_startLabel", startLabel);
        Set(view, "_closeButton", close);

        CornerClose(close);

        screen.gameObject.SetActive(false);
        return view;
    }

    #endregion

    #region 완료 팝업

    private static ConstructionCompletePopupView BuildCompletePopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "ConstructionComplete", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(840, 620));
        panel.pivot = new Vector2(0.5f, 0.5f);

        var chapter = CreateImage("Chapter", panel, Common("UI_Button_Paper"), false);
        chapter.pixelsPerUnitMultiplier = 1.4f;
        Place(chapter.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 34), new Vector2(380, 70));
        var chapterText = Label("Text", chapter.rectTransform, _bodyFont, "01 · 첫 정착", 28, LightBrown, TextAlignmentOptions.Center, 18);
        Stretch(chapterText.rectTransform, 6);

        var title = Label("Title", panel, _titleFont, "작은 집", 58, Cocoa, TextAlignmentOptions.Center, 36);
        TopBand(title.rectTransform, 40, 40, 70, 90);

        var costs = CreateRect("Costs", panel);
        TopBand(costs, 40, 40, 190, 110);
        Row(costs, 24, TextAnchor.MiddleCenter);
        var chips = new List<CostChipView>
        {
            BuildCostChip(costs, "Cost1", new Vector2(250, 96), true),
            BuildCostChip(costs, "Cost2", new Vector2(250, 96), true),
            BuildCostChip(costs, "Cost3", new Vector2(250, 96), true),
        };

        // 비용 없이 끝낸 부탁(광산 길 열기)은 비용 칸 자리에 부탁 그림
        var icon = CreateImage("Icon", panel, null, false);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -184), new Vector2(124, 124));
        icon.gameObject.SetActive(false);

        var leaf = CreateImage("Divider", panel, ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf"), false);
        leaf.preserveAspect = true;
        Place(leaf.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -316), new Vector2(60, 40));

        var message = Label("Message", panel, _titleFont, "첫 주민이 정착했어요!", 36, Cocoa, TextAlignmentOptions.Center, 22);
        message.textWrappingMode = TextWrappingModes.Normal;
        TopBand(message.rectTransform, 40, 40, 362, 100);

        var (confirm, _, confirmImage) = BuildButton(panel, "ConfirmButton", Common("UI_Button_Primary"), "확인", 44, Color.white);
        Place(confirmImage.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(440, 112));

        var view = screen.gameObject.AddComponent<ConstructionCompletePopupView>();
        Set(view, "_animator", animator);
        Set(view, "_chapterText", chapterText);
        Set(view, "_titleText", title);
        var so = new SerializedObject(view);
        var chipProp = so.FindProperty("_costChips");
        chipProp.arraySize = chips.Count;
        for (int i = 0; i < chips.Count; i++)
            chipProp.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        Set(view, "_icon", icon);
        Set(view, "_messageText", message);
        Set(view, "_confirmButton", confirm);

        screen.gameObject.SetActive(false);
        return view;
    }

    #endregion

    /// <summary>전역 UI 루트에 매니저와 presenter를 붙인다</summary>
    public static void AttachManagers(GameObject root, Screens screens, SceneNavigator navigator, ZoneDefinition goalZone)
    {
        var manager = root.AddComponent<SettlementManager>();
        Set(manager, "_config", AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath));

        var presenter = root.AddComponent<SettlementPresenter>();
        Set(presenter, "_status", screens.status);
        Set(presenter, "_guide", screens.guide);
        Set(presenter, "_board", screens.board);
        Set(presenter, "_construction", screens.construction);
        Set(presenter, "_complete", screens.complete);
        Set(presenter, "_progress", screens.progress);
        Set(presenter, "_goalZone", goalZone);
        Set(presenter, "_navigator", navigator);

        var tasks = root.AddComponent<SettlementTaskPresenter>();
        Set(tasks, "_popup", screens.task);
    }
}
