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
        CreateDistrictVisualStyle();
        Debug.Log("[SettlementSetup] 구역 3단계: 회관 질서도와 구역 선택 화면 적용 완료");
    }

    private const string DistrictVisualStylePath = "Assets/Resources/UI/DistrictVisualStyle.asset";

    /// <summary>광장 월드의 구역 · 이사 표시가 쓰는 스티커 그림 (Resources, 있으면 그림만 다시 연결)</summary>
    public static void CreateDistrictVisualStyle()
    {
        var style = AssetDatabase.LoadAssetAtPath<DistrictVisualStyle>(DistrictVisualStylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<DistrictVisualStyle>();
            AssetDatabase.CreateAsset(style, DistrictVisualStylePath);
        }
        style.Setup(Common("UI_Button_Paper"), Common("UI_Tag_Category"), Common("UI_Ribbon_Peach"),
            Common("UI_Slider_Track"), Common("UI_Bar_Fill"));
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
    }

    private static DistrictPopupView BuildDistrictPopup(RectTransform canvas)
    {
        // 아래쪽 시트: 위쪽 절반에 고르는 땅이 보이도록 (카메라가 그 땅을 위로 비추고, 구역 색은 이 화면이 열렸을 때만 보임)
        var (screen, panel, animator) = BuildPopupShell(canvas, "DistrictPopup", LoadPanelSprite());
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000, 760));
        var dim = screen.Find("Dim").GetComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.12f); // 땅이 가려지지 않게 옅게 (누르면 닫힘은 그대로)
        BuildTitleBoard(panel, "이 땅을 뭘로 쓸까요?");
        var close = BuildCloseButton(panel);
        var block = Label("Block", panel, _titleFont, "북동쪽 언덕", 36, Cocoa, TextAlignmentOptions.Center, 24);
        TopBand(block.rectTransform, 200, 200, 112, 65);
        var (previous, _, previousImage) = BuildButton(panel, "Previous", Common("UI_Button_Paper"), "이전", 26, Cocoa);
        TopBand(previousImage.rectTransform, 50, 810, 112, 65);
        var (next, _, nextImage) = BuildButton(panel, "Next", Common("UI_Button_Paper"), "다음", 26, Cocoa);
        TopBand(nextImage.rectTransform, 810, 50, 112, 65);
        var note = Label("Note", panel, _bodyFont, "", 26, Body, TextAlignmentOptions.Center, 20);
        TopBand(note.rectTransform, 60, 60, 190, 70);
        var buttons = new Button[4];
        var titles = new TextMeshProUGUI[4];
        var reasons = new TextMeshProUGUI[4];
        const float gap = 24f, side = 50f, cardHeight = 200f;
        float cardWidth = (1000f - side * 2f - gap) / 2f;
        for (int i = 0; i < 4; i++)
        {
            var type = Districts.Assignable[i];
            // 스티커 카드 (코코아 외곽선). 고를 수 있으면 용도 색이 살짝, 잠기면 옅은 베이지 (회색으로 칙칙하지 않게)
            var image = CreateImage("Choice_" + type, panel, Common("UI_Button_Paper"), true);
            // 2 × 2 칸
            int column = i % 2, row = i / 2;
            float left = side + column * (cardWidth + gap);
            TopBand(image.rectTransform, left, 1000f - left - cardWidth, 280 + (cardHeight + gap) * row, cardHeight);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.Lerp(Color.white, DistrictGuideRules.ColorOf(type), 0.25f);
            colors.highlightedColor = colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.95f, 0.92f, 0.86f, 1f);
            button.colors = colors;
            // 제목 앞 용도 색 칩 (지도 위 구역 색과 같은 색)
            var chip = CreateImage("Chip", image.transform, Common("UI_Tag_Category"), false);
            chip.color = DistrictGuideRules.ColorOf(type);
            Place(chip.rectTransform, new Vector2(0, 1), new Vector2(28, -28), new Vector2(38, 38));
            var title = Label("Title", image.transform, _titleFont, Districts.DisplayName(type), 32, Cocoa, TextAlignmentOptions.Left, 22);
            TopBand(title.rectTransform, 76, 22, 20, 46);
            var reason = Label("Reason", image.transform, _bodyFont, "", 24, Body, TextAlignmentOptions.TopLeft, 18);
            reason.textWrappingMode = TextWrappingModes.Normal;
            TopBand(reason.rectTransform, 28, 24, 76, cardHeight - 100);
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
