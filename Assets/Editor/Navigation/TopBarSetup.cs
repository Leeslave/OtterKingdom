using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 화면 상단바(프로필 · 골드 · 조개 · 설정)를 전역 UI의 HUD 영역에 만든다. GlobalUISetup이 함께 호출한다.
/// 1080×1920 기준, HUD 시안(Tools/UIGen/hud_mockup.py의 top_bar) 수치를 그대로 옮김.
/// </summary>
public static class TopBarSetup
{
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";

    public static (TopBarView view, CurrencyPillView goldPill, CurrencyPillView gemPill) Build(RectTransform hud)
    {
        var bar = CreateRect("TopBar", hud);
        TopBand(bar, 0, 0, 0, 220);

        var view = BuildProfile(bar);

        // 재화 칸과 설정은 오른쪽 기준 (넓은 화면에서는 프로필과의 사이가 벌어짐)
        var goldPill = BuildPill(bar, "GoldPill", AssetDatabase.LoadAssetAtPath<Currency>(GoldPath), 432, 256);
        var gemPill = BuildPill(bar, "GemPill", AssetDatabase.LoadAssetAtPath<Currency>(GemPath), 146, 244);

        var settings = CreateImage("SettingsButton", bar, LoadSprite(CommonSpriteFolder, "UI_RoundButton_Cream"), true);
        Place(settings.rectTransform, new Vector2(1, 1), new Vector2(-26, -84), new Vector2(92, 92));
        var settingsButton = settings.gameObject.AddComponent<Button>();
        settingsButton.targetGraphic = settings;
        var gear = CreateImage("Icon", settings.rectTransform, ImportSprite(CommonSpriteFolder, "ICON_Settings"), false);
        Place(gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(60, 60));
        gear.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Set(view, "_settingsButton", settingsButton);
        return (view, goldPill, gemPill);
    }

    // 크림 이름표 + 왼쪽에 걸친 둥근 아바타, 이름 · Lv 뱃지 · 진행 바
    private static TopBarView BuildProfile(RectTransform bar)
    {
        var box = CreateImage("ProfileBox", bar, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        Place(box.rectTransform, new Vector2(0, 1), new Vector2(60, -80), new Vector2(290, 112));

        var name = CreateText("Name", box.rectTransform, _titleFont, "해달왕", 32, Cocoa);
        name.alignment = TextAlignmentOptions.Left;
        name.enableAutoSizing = true; // 긴 이름은 뱃지와 겹치지 않게 글자를 줄임
        name.fontSizeMin = 20;
        name.fontSizeMax = 32;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(98, -10), new Vector2(104, 44));

        var level = CreateImage("LevelTag", box.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Tag_Level"), false);
        level.pixelsPerUnitMultiplier = 1.3f; // 뱃지가 낮아서 테두리를 조금 얇게
        Place(level.rectTransform, new Vector2(0, 1), new Vector2(204, -12), new Vector2(72, 38));
        var levelText = CreateText("Label", level.rectTransform, _titleFont, "Lv.1", 22, Cocoa);
        Stretch(levelText.rectTransform, 0);
        levelText.rectTransform.offsetMin = new Vector2(0, 4); // 뱃지 아래쪽 입체 턱만큼 위로

        var exp = CreateImage("ExpBar", box.rectTransform, LoadSprite(CommonSpriteFolder, "UI_Slider_Track"), false);
        exp.pixelsPerUnitMultiplier = 2.4f; // 바가 낮아서 테두리를 얇게
        Place(exp.rectTransform, new Vector2(0, 1), new Vector2(100, -62), new Vector2(170, 24));
        var fill = CreateImage("Fill", exp.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2.4f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        var expBar = exp.gameObject.AddComponent<ProgressBarView>();
        Set(expBar, "_fill", fill.rectTransform);

        // 아바타는 이름표보다 위에 그려지도록 나중에 만듦
        var avatar = CreateImage("Avatar", bar, LoadSprite(CommonSpriteFolder, "UI_RoundButton_Peach"), true);
        Place(avatar.rectTransform, new Vector2(0, 1), new Vector2(22, -70), new Vector2(128, 128));
        var avatarButton = avatar.gameObject.AddComponent<Button>();
        avatarButton.targetGraphic = avatar;
        var portrait = CreateImage("Portrait", avatar.rectTransform, ImportSprite(OtterFolder, "ICON_Otter_Farmer"), false);
        Place(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(92, 92));
        portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        portrait.preserveAspect = true;

        var view = bar.gameObject.AddComponent<TopBarView>();
        Set(view, "_nameText", name);
        Set(view, "_levelText", levelText);
        Set(view, "_expBar", expBar);
        Set(view, "_profileButton", avatarButton);
        return view;
    }

    // 재화 칸: 왼쪽 끝에 걸친 아이콘, 오른쪽 정렬 숫자, [+]
    private static CurrencyPillView BuildPill(RectTransform bar, string name, Currency currency, float right, float width)
    {
        var pill = CreateImage(name, bar, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        pill.pixelsPerUnitMultiplier = 1.3f; // 칸이 낮아서 테두리를 조금 얇게
        Place(pill.rectTransform, new Vector2(1, 1), new Vector2(-right, -92), new Vector2(width, 76));

        var amount = CreateText("Amount", pill.rectTransform, _titleFont, "0", 32, Cocoa);
        amount.alignment = TextAlignmentOptions.Right;
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 22;
        amount.fontSizeMax = 32;
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(-74, 4), new Vector2(width - 140, 50));

        var plus = CreateImage("PlusButton", pill.rectTransform, LoadSprite(CommonSpriteFolder, "UI_Button_PlusSmall"), true);
        Place(plus.rectTransform, new Vector2(1, 0.5f), new Vector2(-8, 1), new Vector2(54, 54));
        plus.raycastPadding = new Vector4(-12, -12, -12, -12); // 보이는 크기보다 넓게 눌리도록
        var plusButton = plus.gameObject.AddComponent<Button>();
        plusButton.targetGraphic = plus;

        // 아이콘은 칸 왼쪽 끝과 위쪽으로 튀어나옴
        var icon = CreateImage("Icon", pill.rectTransform, currency != null ? currency.Icon : null, false);
        Place(icon.rectTransform, new Vector2(0, 1), new Vector2(-22, 8), new Vector2(84, 84));
        icon.preserveAspect = true;

        var view = pill.gameObject.AddComponent<CurrencyPillView>();
        Set(view, "_currency", currency);
        Set(view, "_icon", icon);
        Set(view, "_amountText", amount);
        Set(view, "_plusButton", plusButton);
        return view;
    }
}
