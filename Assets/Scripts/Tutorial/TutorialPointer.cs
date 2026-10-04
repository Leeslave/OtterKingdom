using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 입력을 막지 않는 안내: 대상(월드 오브젝트 등) 둘레에 숨쉬듯 깜빡이는 테두리와 짧은 말풍선만 띄운다.
/// TutorialOverlay와 달리 화면을 어둡게 덮지 않고, 레이캐스터가 없어 아래의 월드·버튼을 그대로 누를 수 있다
/// (예: "요정을 눌러 상점을 열어 보세요!" — 실제로 상점을 열어야 안내가 끝남).
/// 전역 UI(100)보다 아래(95)에 그려서 게시판·확인 창 같은 팝업이 뜨면 그 아래로 가려진다. 장소 씬과 함께 사라진다.
/// </summary>
public class TutorialPointer : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    // 장소 화면 아래층(90) 위, 전역 UI(100) 아래
    private const int SortingOrder = 95;
    private const float BubbleWidth = 640f;
    private const float BodyFontSize = 36f;
    private const float HolePadding = 16f;
    private const float FrameOutset = 14f;
    private const float BubbleGap = 40f;
    private const float ScreenMargin = 40f;
    private static readonly Color FallbackPanelColor = new Color(1f, 0.96f, 0.9f, 1f);

    private Func<Rect?> _target;
    private Canvas _canvas;
    private RectTransform _root;
    private CanvasGroup _group;
    private RectTransform _frame;
    private Image _frameImage;
    private RectTransform _bubble;

    /// <param name="target">강조할 화면 영역 (픽셀, 매 프레임 다시 구함. null이면 잠시 숨김)</param>
    public static TutorialPointer Show(Func<Rect?> target, string message)
    {
        var go = new GameObject(nameof(TutorialPointer), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var pointer = go.AddComponent<TutorialPointer>();
        pointer._target = target ?? throw new ArgumentNullException(nameof(target));
        pointer.Build(message);
        pointer.LateUpdate();
        return pointer;
    }

    public void Close()
    {
        if (this != null)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        var hole = ToNormalized(_target());
        _group.alpha = hole.HasValue ? 1f : 0f;
        if (!hole.HasValue)
            return;

        var h = hole.Value;
        _frame.anchorMin = h.min;
        _frame.anchorMax = h.max;
        _frame.offsetMin = new Vector2(-FrameOutset, -FrameOutset);
        _frame.offsetMax = new Vector2(FrameOutset, FrameOutset);

        // 대상 위아래 중 공간이 넓은 쪽에 말풍선
        float screenH = _root.rect.height;
        float bubbleH = _bubble.rect.height;
        float holeTop = (h.yMax - 0.5f) * screenH;
        float holeBottom = (h.yMin - 0.5f) * screenH;
        float roomAbove = screenH * 0.5f - holeTop;
        float roomBelow = holeBottom + screenH * 0.5f;
        float y = roomAbove >= roomBelow
            ? holeTop + FrameOutset + BubbleGap + bubbleH * 0.5f
            : holeBottom - FrameOutset - BubbleGap - bubbleH * 0.5f;
        float limit = Mathf.Max(0f, screenH * 0.5f - bubbleH * 0.5f - ScreenMargin);
        float x = Mathf.Clamp((h.center.x - 0.5f) * _root.rect.width, -_root.rect.width * 0.5f + BubbleWidth * 0.5f + ScreenMargin,
            _root.rect.width * 0.5f - BubbleWidth * 0.5f - ScreenMargin);
        _bubble.anchoredPosition = new Vector2(x, Mathf.Clamp(y, -limit, limit));

        var color = _frameImage.color;
        color.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        _frameImage.color = color;
    }

    // 화면 픽셀 영역 → 여백을 더하고 화면 안으로 자른 0~1 영역 (화면 밖이면 null)
    private Rect? ToNormalized(Rect? screenRect)
    {
        if (screenRect == null || Screen.width <= 0 || Screen.height <= 0)
            return null;
        float pad = HolePadding * _canvas.scaleFactor;
        var r = screenRect.Value;
        float xMin = Mathf.Clamp01((r.xMin - pad) / Screen.width);
        float yMin = Mathf.Clamp01((r.yMin - pad) / Screen.height);
        float xMax = Mathf.Clamp01((r.xMax + pad) / Screen.width);
        float yMax = Mathf.Clamp01((r.yMax + pad) / Screen.height);
        if (xMax - xMin <= 0.001f || yMax - yMin <= 0.001f)
            return null;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void Build(string message)
    {
        var style = Resources.Load<TutorialStyle>(TutorialStyle.ResourcePath);
        if (style == null)
            style = ScriptableObject.CreateInstance<TutorialStyle>();

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        _root = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _frameImage = CreateImage("Frame", _root, style.Highlight, Color.white);
        _frameImage.enabled = style.Highlight != null;
        _frame = _frameImage.rectTransform;

        var panel = CreateImage("Bubble", _root, style.Panel, style.Panel != null ? Color.white : FallbackPanelColor);
        _bubble = panel.rectTransform;
        _bubble.anchorMin = _bubble.anchorMax = _bubble.pivot = new Vector2(0.5f, 0.5f);
        _bubble.sizeDelta = new Vector2(BubbleWidth, 0f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 36, 36);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(_bubble, false);
        if (style.BodyFont != null)
        {
            label.font = style.BodyFont;
            style.BodyFont.TryAddCharacters(message, out _);
        }
        label.fontSize = BodyFontSize;
        label.color = style.BodyColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.text = message;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        return image;
    }
}
