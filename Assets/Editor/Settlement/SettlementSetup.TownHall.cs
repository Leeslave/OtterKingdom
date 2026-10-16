using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 마을회관 발전 현황 화면 (TownHallPopupView). 고정 위치 대신 카드가 내용 길이만큼 쌓이는 스크롤 목록:
/// 머리말(마을회관 그림 · 정착 단계 · 마을 발전 막대) / 주민 현황 · 지금 하는 일 · 다음 목표 · 왕국 질서도 · 영토 확장 · 완료한 발전(칩).
/// 카드 머리말마다 아이콘 (지금 있는 그림을 씀. 완료한 발전 아이콘은 새 그림이 있으면 Assets/Art/Settlement/ICON_Trophy.png로 바꿈)
/// </summary>
public static partial class SettlementSetup
{
    private const float HallWidth = 940f;
    private const float HallHeight = 1640f;
    private const string OtterIconFolder = "Assets/Art/Otter";

    private static TownHallPopupView BuildTownHallPopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "TownHallPopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HallWidth, HallHeight));
        panel.pivot = new Vector2(0.5f, 0.5f);
        BuildTitleBoard(panel, "마을회관 · 발전 현황");
        var close = BuildCloseButton(panel);
        var view = screen.gameObject.AddComponent<TownHallPopupView>();

        // 머리말 (스크롤 밖): 마을회관 그림 · 05 · 작은 마을 · 마을 발전 막대
        var header = CreateImage("Header", panel, Common("UI_Button_Paper"), false);
        TopBand(header.rectTransform, 40, 40, 100, 156);
        var hallIcon = CreateImage("Icon", header.rectTransform, LoadArt("ICON_TownHall"), false);
        hallIcon.preserveAspect = true;
        Place(hallIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 6), new Vector2(128, 128));
        var stage = Label("Stage", header.rectTransform, _titleFont, "05 · 작은 마을", 36, Cocoa, TextAlignmentOptions.Left);
        TopBand(stage.rectTransform, 158, 24, 20, 50);
        var progress = Label("Progress", header.rectTransform, _bodyFont, "마을 발전 0 / 12", 24, Green, TextAlignmentOptions.Left);
        TopBand(progress.rectTransform, 160, 24, 72, 30);
        var progressBar = BuildBar(header.rectTransform, "ProgressBar");
        TopBand((RectTransform)progressBar.transform, 160, 30, 106, 24);

        // 카드 목록
        var (scroll, content) = BuildHallScroll(panel);

        // 주민 현황
        var labor = Card(content, "LaborCard", false);
        CardHeader(labor, LoadArt("ICON_House_Blue"), "주민 현황", out _);
        var chips = HorizontalGroup(labor, "Chips", 12, TextAnchor.MiddleCenter, true);
        var settled = BuildLaborChip(chips, "Settled", "정착 주민");
        var available = BuildLaborChip(chips, "Available", "보낼 수 있음");
        var workingCount = BuildLaborChip(chips, "Working", "작업 중");

        // 지금 하는 일
        var workCard = Card(content, "WorkCard", false);
        CardHeader(workCard, LoadArt("ICON_Otter_Builder"), "지금 하는 일", out _);
        var work = FlowText(workCard, "Work", _bodyFont, "지금 진행 중인 일이 없어요.", 26, Body);

        // 다음 목표 (가장 중요한 카드: 외곽선 있는 종이 카드)
        var nextCard = Card(content, "NextCard", true);
        CardHeader(nextCard, LoadIcon(NavIconFolder, "ICON_Nav_Quest"), "다음 목표", out _);
        var next = HorizontalGroup(nextCard, "Next", 16, TextAnchor.MiddleLeft, false);
        var nextTexts = VerticalGroup(next, "Texts", 6);
        nextTexts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var nextTitle = FlowText(nextTexts, "Title", _titleFont, "우리 마을의 회의소", 32, Cocoa);
        var nextDescription = FlowText(nextTexts, "Description", _bodyFont, "", 25, Body);
        var go = LayoutButton(next, "GoButton", Common("UI_Ribbon_Peach"), "가 보기", 32, 190, 92);

        var done = VerticalGroup(nextCard, "AllDone", 14);
        var doneText = FlowText(done, "Message", _titleFont, "현재 준비된 마을 발전을 모두 마쳤어요!", 28, Cocoa);
        doneText.alignment = TextAlignmentOptions.Center;
        var buttons = HorizontalGroup(done, "Buttons", 14, TextAnchor.MiddleCenter, false);
        var travel = LayoutButton(buttons, "TravelButton", Common("UI_Button_Paper"), "생산하러 가기", 28, 260, 88);
        var decor = LayoutButton(buttons, "DecorButton", Common("UI_Button_Paper"), "꾸미기", 28, 220, 88);
        var codex = LayoutButton(buttons, "CodexButton", Common("UI_Button_Paper"), "도감", 28, 220, 88);
        done.gameObject.SetActive(false);

        // 왕국 질서도 (행정 해달 뒤)
        var district = Card(content, "DistrictCard", false);
        CardHeader(district, LoadIcon(OtterIconFolder, "ICON_Otter_Clerk"), "왕국 질서도 · 또박이", out var orderValue);
        orderValue.text = "0 / 100";
        var orderBar = BuildBar(district, "OrderBar");
        orderBar.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
        var orderGoal = FlowText(district, "Goal", _bodyFont, "", 24, Body);
        var districtRow = HorizontalGroup(district, "Row", 12, TextAnchor.MiddleLeft, false);
        var note = FlowText(districtRow, "Note", _bodyFont, "", 24, Green);
        note.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var relocation = LayoutButton(districtRow, "Relocation", Common("UI_Ribbon_Peach"), "이사하기", 28, 170, 76);
        var designate = LayoutButton(districtRow, "Designate", Common("UI_Ribbon_Peach"), "구역 정하기", 28, 190, 76);
        relocation.gameObject.SetActive(false);
        district.gameObject.SetActive(false);

        // 영토 확장
        var territory = Card(content, "TerritoryCard", false);
        CardHeader(territory, LoadArt("ICON_Clearing"), "영토 확장", out _);
        var territoryRow = HorizontalGroup(territory, "Row", 16, TextAnchor.MiddleLeft, false);
        var territoryTexts = VerticalGroup(territoryRow, "Texts", 6);
        territoryTexts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var territoryTitle = FlowText(territoryTexts, "Title", _titleFont, "숲 개간", 30, Cocoa);
        var territoryDescription = FlowText(territoryTexts, "Description", _bodyFont, "", 25, Body);
        var territoryGo = LayoutButton(territoryRow, "GoButton", Common("UI_Ribbon_Peach"), "가 보기", 32, 190, 92);
        territory.gameObject.SetActive(false);

        // 완료한 발전 (칩)
        var completed = Card(content, "CompletedCard", false);
        var trophy = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/ICON_Trophy.png");
        CardHeader(completed, trophy != null ? LoadArt("ICON_Trophy") : LoadArt("ICON_BoardUpgrade"), "완료한 발전", out var completedCount);
        var flow = CreateRect("Chips", completed);
        var flowLayout = flow.gameObject.AddComponent<FlowLayoutGroup>();
        flowLayout.SetSpacing(10, 10);
        var chip = BuildCompletedChip(flow);
        var empty = FlowText(completed, "Empty", _bodyFont, "아직 완료한 발전이 없어요.", 24, LightBrown);

        Set(view, "_animator", animator);
        Set(view, "_scroll", scroll);
        Set(view, "_stageText", stage);
        Set(view, "_progressText", progress);
        Set(view, "_progressBar", progressBar);
        Set(view, "_settledText", settled);
        Set(view, "_availableText", available);
        Set(view, "_workingText", workingCount);
        Set(view, "_workText", work);
        Set(view, "_nextGroup", next.gameObject);
        Set(view, "_nextTitleText", nextTitle);
        Set(view, "_nextDescriptionText", nextDescription);
        Set(view, "_goButton", go);
        Set(view, "_doneGroup", done.gameObject);
        Set(view, "_doneText", doneText);
        Set(view, "_travelButton", travel);
        Set(view, "_decorButton", decor);
        Set(view, "_codexButton", codex);
        Set(view, "_districtCard", district.gameObject);
        Set(view, "_orderText", orderValue);
        Set(view, "_orderGoal", orderGoal);
        Set(view, "_districtNote", note);
        Set(view, "_orderBar", orderBar);
        Set(view, "_districtButton", designate);
        Set(view, "_relocationButton", relocation);
        Set(view, "_territoryCard", territory.gameObject);
        Set(view, "_territoryTitle", territoryTitle);
        Set(view, "_territoryDescription", territoryDescription);
        Set(view, "_territoryButton", territoryGo);
        Set(view, "_completedCount", completedCount);
        Set(view, "_completedFlow", flow);
        Set(view, "_completedChip", chip);
        Set(view, "_completedEmpty", empty);
        Set(view, "_closeButton", close);

        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    // 머리말 아래 ~ 패널 아래까지 세로 스크롤. content에 카드를 쌓음 (높이는 내용만큼)
    private static (ScrollRect scroll, RectTransform content) BuildHallScroll(RectTransform panel)
    {
        var area = CreateImage("Scroll", panel, null, true);
        area.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤
        TopBand(area.rectTransform, 30, 30, 270, HallHeight - 270 - 46);
        var viewport = CreateRect("Viewport", area.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 28);
        layout.spacing = 18;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;
        return (scroll, content);
    }

    // 카드 하나: 크림 판(중요하면 외곽선 있는 종이 판) + 세로로 쌓기
    private static RectTransform Card(RectTransform parent, string name, bool featured)
    {
        var image = CreateImage(name, parent, Common(featured ? "UI_Button_Paper" : "UI_Box_Inset"), false);
        var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = featured ? new RectOffset(28, 28, 22, 40) : new RectOffset(26, 26, 20, 24);
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return image.rectTransform;
    }

    // 카드 머리말: 아이콘 · 제목 · (오른쪽 값)
    private static void CardHeader(RectTransform card, Sprite icon, string title, out TextMeshProUGUI right)
    {
        var row = HorizontalGroup(card, "Header", 12, TextAnchor.MiddleLeft, false);
        row.gameObject.AddComponent<LayoutElement>().minHeight = 56;
        var image = CreateImage("Icon", row, icon, false);
        image.preserveAspect = true;
        var iconElement = image.gameObject.AddComponent<LayoutElement>();
        iconElement.preferredWidth = iconElement.preferredHeight = 56;
        var label = Label("Title", row, _titleFont, title, 30, Cocoa, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        right = Label("Value", row, _titleFont, "", 28, Cocoa, TextAlignmentOptions.Right);
        right.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static RectTransform HorizontalGroup(RectTransform parent, string name, float spacing, TextAnchor alignment, bool expandWidth)
    {
        var rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static RectTransform VerticalGroup(RectTransform parent, string name, float spacing)
    {
        var rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    // 줄바꿈되는 글 (높이는 글 길이만큼)
    private static TextMeshProUGUI FlowText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, Color color)
    {
        var label = Label(name, parent, font, text, size, color, TextAlignmentOptions.TopLeft);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.lineSpacing = 4;
        return label;
    }

    private static Button LayoutButton(RectTransform parent, string name, Sprite sprite, string text, float fontSize, float width, float height)
    {
        var (button, _, image) = BuildButton(parent, name, sprite, text, fontSize, Cocoa);
        image.pixelsPerUnitMultiplier = 1.4f;
        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = width;
        element.preferredHeight = element.minHeight = height;
        return button;
    }

    // 진행 막대 (트랙 + 채움). ProgressBarView가 채움 폭을 바꿈
    private static ProgressBarView BuildBar(RectTransform parent, string name)
    {
        var track = CreateImage(name, parent, Common("UI_Slider_Track"), false);
        var fill = CreateImage("Fill", track.rectTransform, Common("UI_Bar_Fill"), false);
        Stretch(fill.rectTransform, 0);
        var bar = track.gameObject.AddComponent<ProgressBarView>();
        Set(bar, "_fill", fill.rectTransform);
        return bar;
    }

    // "정착 주민   5명" 칩 (값 글자를 돌려줌)
    private static TextMeshProUGUI BuildLaborChip(RectTransform parent, string name, string key)
    {
        var bg = CreateImage(name, parent, Common("UI_Chip_Normal"), false);
        bg.pixelsPerUnitMultiplier = 1.4f;
        var element = bg.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 72;
        element.flexibleWidth = 1;
        // 아이콘 그림이 있으면 왼쪽에 (Assets/Art/Settlement/ICON_Labor_Settled · _Available · _Working.png)
        float keyLeft = 18;
        if (AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/ICON_Labor_{name}.png") != null)
        {
            var icon = CreateImage("Icon", bg.rectTransform, LoadArt($"ICON_Labor_{name}"), false);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(10, 3), new Vector2(46, 46));
            keyLeft = 60;
        }
        var keyText = Label("Key", bg.rectTransform, _bodyFont, key, 23, Body, TextAlignmentOptions.Left, 16);
        Stretch(keyText.rectTransform, 0);
        keyText.rectTransform.offsetMin = new Vector2(keyLeft, 8);
        keyText.rectTransform.offsetMax = new Vector2(-84, -8);
        var valueText = Label("Value", bg.rectTransform, _titleFont, "0명", 30, Cocoa, TextAlignmentOptions.Right, 18);
        Stretch(valueText.rectTransform, 0);
        valueText.rectTransform.offsetMin = new Vector2(18, 8);
        valueText.rectTransform.offsetMax = new Vector2(-18, -8);
        return valueText;
    }

    // 완료한 발전 칩 원본 (View가 개수만큼 복사, 꺼 둠)
    private static GameObject BuildCompletedChip(RectTransform parent)
    {
        var bg = CreateImage("Chip", parent, Common("UI_Chip_Normal"), false);
        bg.pixelsPerUnitMultiplier = 1.8f;
        var layout = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
        // 칩 아래 입체 턱만큼 아래 여백을 더 두고 글을 가운데로 (그림이 글보다 높아도 가운데)
        layout.padding = new RectOffset(18, 18, 8, 16);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var label = Label("Label", bg.rectTransform, _bodyFont, "작은 집", 23, Body, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        bg.gameObject.SetActive(false);
        return bg.gameObject;
    }
}
