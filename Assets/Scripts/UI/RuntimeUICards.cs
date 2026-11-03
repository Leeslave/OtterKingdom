using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RuntimeUIWindow 안에 쌓는 부품 (퀘스트 · 가방 화면과 같은 모양): 줄 카드 · 아이콘 칸 · 글 · 꼬리표 · 막대 · 스위치 · 칸 격자.
/// 높이는 내용에 맞춤 (부모 레이아웃이 정함). 규칙은 모른다
/// </summary>
public static class RuntimeUICards
{
    public enum TagKind { Plain, On, Warn, Gold }

    public static readonly Color Muted = new Color32(0x9A, 0x7F, 0x6E, 0xFF);
    private static readonly Color DimmedCard = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color InsetFallback = new Color(0.97f, 0.92f, 0.84f);

    #region 틀

    /// <summary>흰 카드. vertical이면 위아래로, 아니면 옆으로 쌓음</summary>
    public static RectTransform Card(Transform parent, bool vertical, bool dimmed = false)
    {
        var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(image, RuntimeUIKit.Style.Card, Color.white);
        if (dimmed)
            image.color = DimmedCard;
        var padding = new RectOffset(22, 24, 20, 26);
        if (vertical)
            Vertical(go, 16f, padding);
        else
            Horizontal(go, 20f, padding);
        return (RectTransform)go.transform;
    }

    /// <summary>카드 안 크림 칸 (세로)</summary>
    public static RectTransform Inset(Transform parent, float spacing = 12f)
    {
        var go = new GameObject("Inset", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RuntimeUIKit.ApplySprite(go.GetComponent<Image>(), RuntimeUIKit.Style.Inset, InsetFallback);
        Vertical(go, spacing, new RectOffset(22, 22, 16, 18));
        return (RectTransform)go.transform;
    }

    /// <summary>옆으로 쌓는 줄 (height &gt; 0이면 높이 고정)</summary>
    public static RectTransform Row(Transform parent, float spacing, float height = 0f)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Horizontal(go, spacing, new RectOffset());
        if (height > 0f)
            Size(go, -1f, height);
        return (RectTransform)go.transform;
    }

    /// <summary>위아래로 쌓는 칸 (width &gt; 0이면 폭 고정, 아니면 남는 폭)</summary>
    public static RectTransform Column(Transform parent, float spacing, float width = 0f, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject("Column", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var layout = Vertical(go, spacing, new RectOffset());
        layout.childAlignment = alignment;
        var element = go.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            // 폭을 정한 칸(오른쪽 버튼 · 스위치 자리)은 안의 것을 늘이지 않고 가운데에
            layout.childForceExpandWidth = false;
            element.preferredWidth = width;
            element.minWidth = width;
        }
        else
        {
            element.flexibleWidth = 1f;
        }
        return (RectTransform)go.transform;
    }

    public static void Flexible(Transform parent)
    {
        var go = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    #endregion

    #region 글 · 그림

    /// <summary>글 (왼쪽 정렬, 글자는 폰트에 미리 넣음)</summary>
    public static TextMeshProUGUI Text(Transform parent, string text, int fontSize, bool title = false, bool muted = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        RuntimeUIKit.PrepareGlyphs(text);
        var label = RuntimeUIKit.CreateLabel(parent, text, fontSize, title, alignment);
        label.lineSpacing = 0f;
        if (muted)
            label.color = Muted;
        return label;
    }

    public static Image Icon(Transform parent, Sprite sprite, float size)
    {
        var image = RuntimeUIKit.CreateIcon(parent, sprite, size);
        var element = image.GetComponent<LayoutElement>();
        element.minHeight = size;
        element.preferredHeight = size;
        return image;
    }

    /// <summary>둥근 칸 안의 아이콘 (가방 칸 모양). locked면 흐리게 + 자물쇠</summary>
    public static RectTransform IconTile(Transform parent, Sprite icon, float size, bool locked = false, bool filled = false)
    {
        var style = RuntimeUIKit.Style;
        var go = new GameObject("Tile", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Size(go, size, size);
        RuntimeUIKit.ApplySprite(go.GetComponent<Image>(), filled ? style.SlotFilled : style.Slot, InsetFallback);
        var picture = Child("Icon", go.transform, icon, size * 0.14f);
        picture.color = locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        if (locked && style.LockIcon != null)
        {
            var lockImage = Child("Lock", go.transform, style.LockIcon, 0f);
            var rect = lockImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(size * 0.42f, size * 0.42f);
            rect.anchoredPosition = new Vector2(6f, -4f);
        }
        return (RectTransform)go.transform;
    }

    private static Image Child(string name, Transform parent, Sprite sprite, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        RuntimeUIKit.Stretch(rect);
        rect.offsetMin = new Vector2(inset, inset + 4f);
        rect.offsetMax = new Vector2(-inset, -inset + 2f);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    #endregion

    #region 꼬리표

    public class TagView
    {
        public Image Background;
        public TextMeshProUGUI Label;

        public void Set(string text, TagKind kind)
        {
            RuntimeUIKit.PrepareGlyphs(text);
            Label.text = text;
            RuntimeUIKit.ApplySprite(Background, SpriteFor(kind), Color.white);
            Background.pixelsPerUnitMultiplier = 1.3f;
            Label.color = kind == TagKind.Warn ? Color.white : RuntimeUIKit.Style.TextColor;
            Background.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    public static TagView Tag(Transform parent, string text, TagKind kind, int fontSize = 24)
    {
        var go = new GameObject("Tag", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var layout = Horizontal(go, 0f, new RectOffset(18, 18, 6, 12));
        layout.childAlignment = TextAnchor.MiddleCenter;
        var label = Text(go.transform, text, fontSize, true, alignment: TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var tag = new TagView { Background = go.GetComponent<Image>(), Label = label };
        tag.Set(text, kind);
        return tag;
    }

    private static Sprite SpriteFor(TagKind kind)
    {
        var style = RuntimeUIKit.Style;
        switch (kind)
        {
            case TagKind.On: return style.TagOn;
            case TagKind.Warn: return style.TagWarn;
            case TagKind.Gold: return style.TagGold;
            default: return style.Tag;
        }
    }

    #endregion

    #region 막대 · 스위치 · 버튼

    /// <summary>진행 막대. 돌려준 fill을 SetBar로 채움</summary>
    public static RectTransform Bar(Transform parent, float height = 26f)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var element = Size(go, -1f, height);
        element.flexibleWidth = 1f;
        element.minWidth = 60f;
        var track = go.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(track, RuntimeUIKit.Style.BarTrack, InsetFallback);
        track.pixelsPerUnitMultiplier = 2f;
        track.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(go.transform, false);
        var fill = (RectTransform)fillGo.transform;
        RuntimeUIKit.Stretch(fill);
        var image = fillGo.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(image, RuntimeUIKit.Style.BarFill, new Color(0.55f, 0.75f, 0.5f));
        image.pixelsPerUnitMultiplier = 2f;
        image.raycastTarget = false;
        return fill;
    }

    /// <summary>막대 채움 (너무 짧으면 둥근 끝이 찌그러지므로 막대 높이만큼은 보임)</summary>
    public static void SetBar(RectTransform fill, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fill.gameObject.SetActive(fraction > 0.001f);
        var track = (RectTransform)fill.parent;
        float width = Mathf.Max(1f, track.rect.width);
        float min = Mathf.Min(1f, track.rect.height / width);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(Mathf.Max(fraction, min), 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
    }

    /// <summary>켜고 끄는 스위치 (초록 = 켜짐)</summary>
    public static Button Switch(Transform parent, bool on, Action toggle)
    {
        var style = RuntimeUIKit.Style;
        var go = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Size(go, 124f, 64f);
        var track = go.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(track, on ? style.BarFill : style.BarTrack, on ? new Color(0.55f, 0.75f, 0.5f) : InsetFallback);
        track.pixelsPerUnitMultiplier = 1.4f;
        var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        knob.transform.SetParent(go.transform, false);
        var rect = (RectTransform)knob.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(on ? 1f : 0f, 0.5f);
        rect.sizeDelta = new Vector2(58f, 58f);
        rect.anchoredPosition = new Vector2(on ? -3f : 3f, 2f);
        var knobImage = knob.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(knobImage, style.Knob, Color.white);
        knobImage.raycastTarget = false;
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            toggle();
        });
        return button;
    }

    /// <summary>작은 + 버튼</summary>
    public static Button Plus(Transform parent, Action onClick, float size = 64f)
    {
        var go = new GameObject("Plus", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Size(go, size, size);
        RuntimeUIKit.ApplySprite(go.GetComponent<Image>(), RuntimeUIKit.Style.PlusButton, new Color(0.55f, 0.75f, 0.5f));
        go.GetComponent<Image>().raycastPadding = new Vector4(-14f, -14f, -14f, -14f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            onClick();
        });
        return button;
    }

    /// <summary>폭 · 높이를 정한 버튼 (글자는 버튼에 맞춰 줄임)</summary>
    public static Button Button(Transform parent, string text, Action onClick, RuntimeUIKit.ButtonKind kind, float width, float height, int fontSize = 32)
    {
        RuntimeUIKit.PrepareGlyphs(text);
        var button = RuntimeUIKit.CreateButton(parent, text, onClick, kind, width, height: height);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSizeMax = fontSize;
        label.fontSizeMin = 20f;
        return button;
    }

    /// <summary>아이콘 + 수 (모자라면 빨강)</summary>
    public static void Cost(Transform parent, Sprite icon, string amount, bool enough)
    {
        var row = Row(parent, 6f);
        if (icon != null)
            Icon(row, icon, 40f);
        var label = Text(row, amount, 27, true);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        if (!enough)
            label.color = RuntimeUIKit.Style.WarningColor;
    }

    #endregion

    #region 칸 격자 (가방 모양)

    /// <summary>칸 격자. 칸은 Slot으로 채움</summary>
    public static RectTransform Grid(Transform parent, int columns, float cell, float labelHeight)
    {
        var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(parent, false);
        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell + labelHeight);
        grid.spacing = new Vector2(20f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        return (RectTransform)go.transform;
    }

    /// <summary>격자 한 칸: 아이콘 · 오른쪽 아래 개수 · 아래 이름 (아이콘이 없으면 빈 칸)</summary>
    public static void Slot(Transform grid, Sprite icon, string count, string name, float cell)
    {
        var style = RuntimeUIKit.Style;
        var go = new GameObject("Slot", typeof(RectTransform));
        go.transform.SetParent(grid, false);
        var slotGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        slotGo.transform.SetParent(go.transform, false);
        var slot = (RectTransform)slotGo.transform;
        slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 1f);
        slot.sizeDelta = new Vector2(cell, cell);
        RuntimeUIKit.ApplySprite(slotGo.GetComponent<Image>(), icon != null ? style.SlotFilled : style.Slot, InsetFallback);
        if (icon == null)
            return;
        Child("Icon", slot, icon, cell * 0.16f);

        var badge = Tag(slot, count, TagKind.Gold, 24);
        var badgeRect = (RectTransform)badge.Background.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(8f, -6f);
        var fitter = badge.Background.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = Text(go.transform, name, 25, true, alignment: TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(0f, 36f);
        rect.anchoredPosition = Vector2.zero;
    }

    #endregion

    private static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding)
    {
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static LayoutElement Size(GameObject go, float width, float height)
    {
        var element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            element.preferredWidth = width;
            element.minWidth = width;
        }
        if (height > 0f)
        {
            element.preferredHeight = height;
            element.minHeight = height;
        }
        return element;
    }
}
