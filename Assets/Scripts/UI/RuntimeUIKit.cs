using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 코드로만 만드는 화면(공동사업 · 생활 의뢰 · 알림 띠 · 모임 연출)의 부품. GameUI와 같은 스티커 모양(RuntimeUIStyle)을 쓴다.
/// 씬·프리팹 연결 없이 쓸 수 있도록 캔버스·패널·글자·버튼·줄을 만들어 준다. 규칙은 모른다.
/// </summary>
public static class RuntimeUIKit
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    public const int TitleFontSize = 48;
    public const int BodyFontSize = 38;
    public const float ButtonHeight = 104f;

    private static readonly Color PanelColor = new Color(0.97f, 0.94f, 0.86f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.5f);

    public enum ButtonKind { Option, Primary, Secondary, Feature }

    private static RuntimeUIStyle _style;

    public static RuntimeUIStyle Style
    {
        get
        {
            if (_style == null)
            {
                _style = Resources.Load<RuntimeUIStyle>(RuntimeUIStyle.ResourcePath);
                if (_style == null)
                    _style = ScriptableObject.CreateInstance<RuntimeUIStyle>();
            }
            return _style;
        }
    }

    /// <summary>씬을 넘어 사는 화면용 캔버스 (정렬 순서: 전역 UI 100, 장소 팝업 200)</summary>
    public static Canvas CreateCanvas(string name, int sortingOrder, bool persistent)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (persistent)
            UnityEngine.Object.DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        new GameObject(nameof(EventSystem), typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    /// <summary>화면을 덮는 어두운 막 (아래를 누르지 못하게)</summary>
    public static RectTransform CreateDim(Transform parent, string name = "Dim")
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Stretch(rect);
        go.GetComponent<Image>().color = DimColor;
        return rect;
    }

    /// <summary>글자·버튼을 세로로 쌓는 크림색 패널 (높이는 내용에 맞춤)</summary>
    public static RectTransform CreatePanel(Transform parent, float width, string name = "Panel")
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        ApplySprite(go.GetComponent<Image>(), Style.Panel, PanelColor);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);
        var layout = go.GetComponent<VerticalLayoutGroup>();
        // 패널 그림의 9-slice 테두리(72, 아래 80) 안쪽에
        layout.padding = new RectOffset(60, 60, 56, 68);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    public static TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize = BodyFontSize, bool title = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(title ? "Title" : "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        var font = title ? Style.TitleFont : Style.BodyFont;
        if (font != null)
            label.font = font;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = title ? Style.TextColor : Style.BodyColor;
        label.lineSpacing = 6f;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    public static Button CreateButton(Transform parent, string text, Action onClick, ButtonKind kind = ButtonKind.Option,
        float width = -1f, bool flexible = false, float height = ButtonHeight)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        ApplySprite(image, ButtonSprite(kind), Color.white);

        var element = go.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        if (width > 0f)
            element.preferredWidth = width;
        if (flexible)
            element.flexibleWidth = 1f;

        var label = CreateLabel(go.transform, text, BodyFontSize, true);
        label.color = kind == ButtonKind.Primary && Style.PrimaryButton != null ? Style.PrimaryLabelColor : Style.TextColor;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = BodyFontSize;
        var labelRect = (RectTransform)label.transform;
        Stretch(labelRect);
        // 버튼 아래 두께만큼 글자를 올림
        if (image.sprite != null)
            labelRect.offsetMin = new Vector2(8f, 12f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        return button;
    }

    public static void SetButtonLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = text;
    }

    public static RectTransform CreateRow(Transform parent, float height, float spacing = 14f)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var element = go.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return (RectTransform)go.transform;
    }

    public static Image CreateIcon(Transform parent, Sprite sprite, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = size;
        element.preferredHeight = size;
        element.minWidth = size;
        return image;
    }

    /// <summary>줄 사이 빈칸</summary>
    public static void CreateSpace(Transform parent, float height)
    {
        var go = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }

    public static Sprite ButtonSprite(ButtonKind kind)
    {
        switch (kind)
        {
            case ButtonKind.Primary: return Style.PrimaryButton;
            case ButtonKind.Secondary: return Style.SecondaryButton;
            case ButtonKind.Feature: return Style.FeatureButton;
            default: return Style.OptionButton;
        }
    }

    /// <summary>9-slice 그림이 있으면 그것, 없으면 단색</summary>
    public static void ApplySprite(Image image, Sprite sprite, Color fallback)
    {
        if (sprite == null)
        {
            image.color = fallback;
            return;
        }
        image.sprite = sprite;
        image.color = Color.white;
        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (목록 갱신 중 글자가 추가되면 D3D12 에디터가 멈춤 — SettlementPresenter와 같은 이유)
    /// </summary>
    public static void PrepareGlyphs(string characters)
    {
        if (string.IsNullOrEmpty(characters))
            return;
        if (Style.TitleFont != null)
            Style.TitleFont.TryAddCharacters(characters, out _);
        if (Style.BodyFont != null && Style.BodyFont != Style.TitleFont)
            Style.BodyFont.TryAddCharacters(characters, out _);
    }
}
