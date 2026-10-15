using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

public static partial class SettlementSetup
{
    /// <summary>회관·구역 화면만 교체한다. 기존 광장·데이터·다른 팝업은 재생성하지 않는다.</summary>
    [MenuItem("Tools/Settlement/Rebuild District UI")]
    public static void RebuildDistrictUI()
    {
        _titleFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
        _bodyFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareRoundOTFR SDF.asset") ?? _titleFont;
        var root = PrefabUtility.LoadPrefabContents(GlobalUIPrefabAssetPath);
        try
        {
            var canvas = (RectTransform)root.transform;
            var oldHall = root.GetComponentInChildren<TownHallPopupView>(true);
            int index = oldHall != null ? oldHall.transform.GetSiblingIndex() : canvas.childCount;
            if (oldHall != null) Object.DestroyImmediate(oldHall.gameObject);
            var oldDistrict = root.GetComponentInChildren<DistrictPopupView>(true);
            if (oldDistrict != null) Object.DestroyImmediate(oldDistrict.gameObject);
            var hall = BuildTownHallPopup(canvas);
            hall.transform.SetSiblingIndex(index);
            var district = BuildDistrictPopup(canvas);
            district.transform.SetSiblingIndex(index + 1);
            Set(root.GetComponent<TownHallPresenter>(), "_hall", hall);
            var presenter = root.GetComponent<DistrictPresenter>() ?? root.AddComponent<DistrictPresenter>();
            Set(presenter, "_popup", district);
            PrefabUtility.SaveAsPrefabAsset(root, GlobalUIPrefabAssetPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[SettlementSetup] 구역 3단계: 회관 질서도와 구역 선택 화면 적용 완료");
    }

    private static void BuildDistrictHallCard(RectTransform panel, TownHallPopupView view)
    {
        var card = CreateImage("DistrictCard", panel, Common("UI_Box_Inset"), false);
        TopBand(card.rectTransform, 50, 50, 878, 198);
        var title = Label("Order", card.transform, _titleFont, "왕국 질서도 0 / 100 · 행정 해달 또박이", 28, Cocoa, TextAlignmentOptions.Left, 20);
        TopBand(title.rectTransform, 24, 24, 10, 38);
        var track = CreateImage("Bar", card.transform, Common("UI_Slider_Track"), false);
        TopBand(track.rectTransform, 24, 24, 52, 22);
        var fill = CreateImage("Fill", track.transform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        Stretch(fill.rectTransform, 0);
        var bar = track.gameObject.AddComponent<ProgressBarView>();
        Set(bar, "_fill", fill.rectTransform);
        var goal = Label("Goal", card.transform, _bodyFont, "", 24, Body, TextAlignmentOptions.Left, 18);
        TopBand(goal.rectTransform, 24, 24, 80, 34);
        var note = Label("Pending", card.transform, _bodyFont, "", 26, Green, TextAlignmentOptions.Left, 18);
        TopBand(note.rectTransform, 24, 220, 122, 60);
        var (button, _, image) = BuildButton(card.rectTransform, "Designate", Common("UI_Ribbon_Peach"), "구역 정하기", 28, Cocoa);
        Place(image.rectTransform, new Vector2(1, 1), new Vector2(-20, -116), new Vector2(190, 68));
        Set(view, "_districtCard", card.gameObject);
        Set(view, "_orderText", title);
        Set(view, "_orderGoal", goal);
        Set(view, "_districtNote", note);
        Set(view, "_orderBar", bar);
        Set(view, "_districtButton", button);
        card.gameObject.SetActive(false);
    }

    private static DistrictPopupView BuildDistrictPopup(RectTransform canvas)
    {
        var (screen, panel, animator) = BuildPopupShell(canvas, "DistrictPopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 1320));
        panel.pivot = new Vector2(0.5f, 0.5f);
        BuildTitleBoard(panel, "이 땅을 뭘로 쓸까요?");
        var close = BuildCloseButton(panel);
        var block = Label("Block", panel, _titleFont, "북동쪽 언덕", 36, Cocoa, TextAlignmentOptions.Center, 24);
        TopBand(block.rectTransform, 140, 140, 122, 65);
        var (previous, _, previousImage) = BuildButton(panel, "Previous", Common("UI_Button_Paper"), "이전", 26, Cocoa);
        TopBand(previousImage.rectTransform, 55, 775, 122, 65);
        var (next, _, nextImage) = BuildButton(panel, "Next", Common("UI_Button_Paper"), "다음", 26, Cocoa);
        TopBand(nextImage.rectTransform, 775, 55, 122, 65);
        var note = Label("Note", panel, _bodyFont, "", 28, Body, TextAlignmentOptions.Center, 22);
        TopBand(note.rectTransform, 65, 65, 210, 98);
        var buttons = new Button[4];
        var titles = new TextMeshProUGUI[4];
        var reasons = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var type = Districts.Assignable[i];
            var image = CreateImage("Choice_" + type, panel, Common("UI_Box_Inset"), true);
            TopBand(image.rectTransform, 60, 60, 335 + 220 * i, 194);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.Lerp(Color.white, DistrictGuideRules.ColorOf(type), 0.25f);
            colors.disabledColor = new Color(0.67f, 0.67f, 0.67f, 0.8f);
            button.colors = colors;
            var title = Label("Title", image.transform, _titleFont, Districts.DisplayName(type), 36, Cocoa, TextAlignmentOptions.Left, 26);
            TopBand(title.rectTransform, 28, 28, 16, 52);
            var reason = Label("Reason", image.transform, _bodyFont, "", 28, Body, TextAlignmentOptions.TopLeft, 22);
            reason.textWrappingMode = TextWrappingModes.Normal;
            TopBand(reason.rectTransform, 28, 28, 82, 90);
            buttons[i] = button;
            titles[i] = title;
            reasons[i] = reason;
        }
        var view = screen.gameObject.AddComponent<DistrictPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_blockName", block);
        Set(view, "_note", note);
        Set(view, "_previous", previous);
        Set(view, "_next", next);
        Set(view, "_close", close);
        var so = new SerializedObject(view);
        SetDistrictArray(so, "_choices", buttons);
        SetDistrictArray(so, "_titles", titles);
        SetDistrictArray(so, "_reasons", reasons);
        so.ApplyModifiedPropertiesWithoutUndo();
        CornerClose(close);
        screen.gameObject.SetActive(false);
        return view;
    }

    private static void SetDistrictArray(SerializedObject so, string property, Object[] items)
    {
        var array = so.FindProperty(property);
        array.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
