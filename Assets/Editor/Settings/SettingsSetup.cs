using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static CurrencyShopSetup;
using static GlobalUISetup;

/// <summary>
/// 설정 화면(배경음·효과음·진동·푸시 알림·언어, 메뉴 버튼 4개, 버전)을 만든다. GlobalUISetup이 함께 호출한다.
/// 1080×1920 기준, 설정 시안(Tools/UIGen/hud_mockup.py의 settings) 수치를 옮김.
/// </summary>
public static class SettingsSetup
{
    private static readonly Color CreamLine = new Color32(0xF0, 0xDC, 0xC0, 0xFF);

    // 패널 안쪽 기준 (패널 왼쪽 위가 0,0)
    private const float PanelWidth = 940f;
    private const float PanelHeight = 1150f;
    private const float RowCenterTop = 150f; // 첫 줄의 가운데
    private const float RowStep = 130f;
    private const float LabelX = 70f;
    private const float ControlX = 400f;
    private const float ControlWidth = 420f;
    private const float DividerTop = 778f;
    private const float MenuTop = 830f;

    public static SettingsView BuildScreen(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildModal(canvas, "Settings", "설정",
            LoadSprite(CommonSpriteFolder, "UI_Ribbon_Peach"), new Vector2(PanelWidth, PanelHeight), 0, 380, 110, 48);

        var close = CreateImage("CloseButton", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(20, 34), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;

        var bgm = BuildSliderRow(panel, 0, "BgmRow", "배경음");
        var sfx = BuildSliderRow(panel, 1, "SfxRow", "효과음");
        var vibration = BuildToggleRow(panel, 2, "VibrationRow", "진동");
        var push = BuildToggleRow(panel, 3, "PushRow", "푸시 알림");
        var language = BuildLanguageRow(panel, 4);

        var divider = CreateImage("Divider", panel, null, false);
        divider.color = CreamLine;
        Place(divider.rectTransform, new Vector2(0, 1), new Vector2(LabelX, -DividerTop), new Vector2(PanelWidth - LabelX * 2, 4));

        var account = BuildMenuButton(panel, 0, "AccountButton", "계정 연동");
        var coupon = BuildMenuButton(panel, 1, "CouponButton", "쿠폰 입력");
        var support = BuildMenuButton(panel, 2, "SupportButton", "고객센터");
        var terms = BuildMenuButton(panel, 3, "TermsButton", "이용약관");

        var version = CreateText("Version", panel, _bodyFont, "버전 0.1.0  ·  해달 왕국", 28, LightBrown);
        BottomBand(version.rectTransform, 50, 40);

        // 잠깐 뜨는 안내: 메뉴 버튼 위에 겹쳐 뜸 (클릭은 통과)
        var notice = CreateImage("Notice", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(notice.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(620, 92));
        var noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.blocksRaycasts = false;
        noticeGroup.interactable = false;
        var noticeText = CreateText("Label", notice.rectTransform, _titleFont, "안내", 32, Cocoa);
        Stretch(noticeText.rectTransform, 0);
        noticeText.rectTransform.offsetMin = new Vector2(0, 8);

        var view = screen.gameObject.AddComponent<SettingsView>();
        Set(view, "_animator", animator);
        Set(view, "_bgmSlider", bgm);
        Set(view, "_sfxSlider", sfx);
        Set(view, "_vibrationSwitch", vibration);
        Set(view, "_pushSwitch", push);
        Set(view, "_languageDropdown", language);
        Set(view, "_accountButton", account);
        Set(view, "_couponButton", coupon);
        Set(view, "_supportButton", support);
        Set(view, "_termsButton", terms);
        Set(view, "_closeButton", closeButton);
        Set(view, "_versionText", version);
        Set(view, "_notice", noticeGroup);
        Set(view, "_noticeText", noticeText);
        return view;
    }

    #region 줄

    private static float RowCenter(int index) => RowCenterTop + index * RowStep;

    // 가운데가 center(패널 위에서부터)인 높이 height의 칸을 x에 둠
    private static void PlaceRow(RectTransform rect, float x, float center, Vector2 size)
    {
        Place(rect, new Vector2(0, 1), new Vector2(x, -(center - size.y / 2)), size);
    }

    private static RectTransform BuildRow(RectTransform panel, int index, string name, string label)
    {
        var row = CreateRect(name, panel);
        Stretch(row, 0);

        var text = CreateText("Label", row, _titleFont, label, 38, Cocoa);
        text.alignment = TextAlignmentOptions.Left;
        PlaceRow(text.rectTransform, LabelX, RowCenter(index), new Vector2(300, 60));
        return row;
    }

    // 크림 홈 + 복숭아 채움 + 종이 동그라미 (시안 slider)
    private static Slider BuildSliderRow(RectTransform panel, int index, string name, string label)
    {
        var row = BuildRow(panel, index, name, label);
        const float trackHeight = 40f;
        const float knobSize = 64f;
        const float knobInset = 20f; // 동그라미가 양 끝에서 조금 안쪽까지만 가도록

        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = "Slider";
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(row, false);
        PlaceRow(rect, ControlX, RowCenter(index), new Vector2(ControlWidth, trackHeight));

        var track = LoadSprite(CommonSpriteFolder, "UI_Slider_Track");
        var fill = LoadSprite(CommonSpriteFolder, "UI_Slider_Fill");
        float scale = SlicedScale(track, trackHeight);

        var background = (RectTransform)rect.Find("Background");
        StyleSliced(background.GetComponent<Image>(), track, scale);
        Stretch(background, 0);

        // 채움은 홈 왼쪽 끝부터 (끝은 동그라미 아래로 가려짐)
        var fillArea = (RectTransform)rect.Find("Fill Area");
        Stretch(fillArea, 0);
        var fillRect = (RectTransform)fillArea.Find("Fill");
        StyleSliced(fillRect.GetComponent<Image>(), fill, scale);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var handleArea = (RectTransform)rect.Find("Handle Slide Area");
        Stretch(handleArea, 0);
        handleArea.offsetMin = new Vector2(knobInset, 0);
        handleArea.offsetMax = new Vector2(-knobInset, 0);
        var handle = (RectTransform)handleArea.Find("Handle");
        handle.anchorMin = new Vector2(0, 0);
        handle.anchorMax = new Vector2(0, 1);
        handle.sizeDelta = new Vector2(knobSize, knobSize - trackHeight);
        var knob = handle.GetComponent<Image>();
        knob.sprite = LoadSprite(CommonSpriteFolder, "UI_Knob");
        knob.type = Image.Type.Simple;
        knob.preserveAspect = true;

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = GameSettings.DefaultBgmVolume;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    // 초록(켬)/크림(끔) 알약 + 좌우로 움직이는 종이 동그라미 (시안 toggle)
    private static ToggleSwitchView BuildToggleRow(RectTransform panel, int index, string name, string label)
    {
        var row = BuildRow(panel, index, name, label);
        var size = new Vector2(140, 68);
        var on = LoadSprite(CommonSpriteFolder, "UI_Toggle_On");
        var off = LoadSprite(CommonSpriteFolder, "UI_Toggle_Off");

        var background = CreateImage("Switch", row, off, true);
        StyleSliced(background, off, SlicedScale(off, size.y));
        // 오른쪽 끝을 슬라이더 끝(ControlX + ControlWidth)에 맞춤
        PlaceRow(background.rectTransform, ControlX + ControlWidth - size.x, RowCenter(index), size);
        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        var knob = CreateImage("Knob", background.rectTransform, LoadSprite(CommonSpriteFolder, "UI_Knob"), false);
        knob.preserveAspect = true;
        Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-36, 2), new Vector2(56, 56)); // 아래 입체 턱만큼 위로

        var view = background.gameObject.AddComponent<ToggleSwitchView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_knob", knob.rectTransform);
        Set(view, "_onSprite", on);
        Set(view, "_offSprite", off);
        return view;
    }

    // 종이 상자 + 왼쪽 글자 + 오른쪽 ▼ (시안 select). 펼친 목록은 종이 패널 + 선택 항목 초록 칩
    private static TMP_Dropdown BuildLanguageRow(RectTransform panel, int index)
    {
        var row = BuildRow(panel, index, "LanguageRow", "언어");
        const float height = 76f;
        const float itemHeight = 64f;
        const float listPadding = 10f;
        const float lip = 8f; // 종이 스프라이트 아래 입체 턱

        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = "LanguageDropdown";
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(row, false);
        PlaceRow(rect, ControlX, RowCenter(index), new Vector2(ControlWidth, height));

        var paper = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        StyleSliced(go.GetComponent<Image>(), paper, SlicedScale(paper, height));

        var dropdown = go.GetComponent<TMP_Dropdown>();
        var caption = dropdown.captionText;
        caption.font = _titleFont;
        caption.fontSize = 34;
        caption.color = Cocoa;
        caption.alignment = TextAlignmentOptions.Left;
        caption.rectTransform.offsetMin = new Vector2(30, lip);
        caption.rectTransform.offsetMax = new Vector2(-60, 0);

        // 기본 화살표 이미지(흰 사각형) 대신 글자 ▼ (제목 폰트에 없는 기호라 본문 폰트)
        var arrow = (RectTransform)rect.Find("Arrow");
        Object.DestroyImmediate(arrow.GetComponent<Image>());
        arrow.anchorMin = new Vector2(1, 0);
        arrow.anchorMax = new Vector2(1, 1);
        arrow.pivot = new Vector2(1, 0.5f);
        arrow.sizeDelta = new Vector2(40, -lip);
        arrow.anchoredPosition = new Vector2(-24, lip / 2);
        var arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
        arrowText.font = _bodyFont;
        arrowText.text = "▼";
        arrowText.fontSize = 26;
        arrowText.color = LightBrown;
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.raycastTarget = false;

        // 펼친 목록: TMP가 펼칠 때 높이를 Content 기준으로 다시 계산하므로 여백은 Content와 항목 사이에 둠
        int optionCount = System.Enum.GetValues(typeof(GameLanguage)).Length;
        var template = dropdown.template;
        template.anchoredPosition = new Vector2(0, -8);
        template.sizeDelta = new Vector2(0, optionCount * itemHeight + listPadding * 2 + lip);
        StyleSliced(template.GetComponent<Image>(), paper, SlicedScale(paper, itemHeight + listPadding * 2 + lip));

        var scrollRect = template.GetComponent<ScrollRect>();
        scrollRect.verticalScrollbar = null;
        scrollRect.vertical = false;
        var scrollbar = template.Find("Scrollbar");
        if (scrollbar != null)
            Object.DestroyImmediate(scrollbar.gameObject);

        var viewport = (RectTransform)template.Find("Viewport");
        Stretch(viewport, 0);
        var content = (RectTransform)viewport.Find("Content");
        content.sizeDelta = new Vector2(0, itemHeight + listPadding * 2 + lip);

        var item = (RectTransform)content.Find("Item");
        item.anchorMin = new Vector2(0, 0.5f);
        item.anchorMax = new Vector2(1, 0.5f);
        item.sizeDelta = new Vector2(-listPadding * 2, itemHeight);
        item.anchoredPosition = new Vector2(0, lip / 2);

        var toggle = item.GetComponent<Toggle>();
        var itemBackground = item.Find("Item Background").GetComponent<Image>();
        itemBackground.color = Color.white;
        var colors = toggle.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color32(0xF6, 0xD5, 0xBE, 0xFF);
        colors.pressedColor = new Color32(0xEE, 0xC3, 0xA5, 0xFF);
        colors.selectedColor = new Color(1, 1, 1, 0);
        toggle.colors = colors;

        // 체크 아이콘 대신 선택된 언어를 채우는 초록 칩 (가방 정렬 드롭다운과 같은 표현)
        var chip = LoadSprite(CommonSpriteFolder, "UI_Chip_Selected");
        var checkmark = (RectTransform)item.Find("Item Checkmark");
        Stretch(checkmark, 0);
        StyleSliced(checkmark.GetComponent<Image>(), chip, SlicedScale(chip, itemHeight));

        var label = dropdown.itemText;
        label.font = _titleFont;
        label.fontSize = 32;
        label.color = Cocoa;
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 6);

        return dropdown;
    }

    private static Button BuildMenuButton(RectTransform panel, int index, string name, string label)
    {
        var button = BuildTextButton(panel, name, "UI_Button_Paper", label, 34, Cocoa);
        var position = new Vector2(LabelX + (index % 2) * 410, -(MenuTop + (index / 2) * 120));
        Place(button.GetComponent<RectTransform>(), new Vector2(0, 1), position, new Vector2(390, 96));
        return button;
    }

    #endregion

    #region 도우미

    private static void StyleSliced(Image image, Sprite sprite, float scale)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = scale;
        image.color = Color.white;
    }

    // 9-slice 테두리(위+아래)가 높이보다 크면 모서리가 찌그러지므로 테두리를 줄이는 배율
    private static float SlicedScale(Sprite sprite, float height)
    {
        float borders = sprite.border.y + sprite.border.w;
        return borders > height ? borders / (height * 0.9f) : 1f;
    }

    #endregion
}
