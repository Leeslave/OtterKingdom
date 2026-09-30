using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 화면: 화면 전체를 어둡게 덮고, 설명할 대상만 뚫어서 반짝이는 테두리로 강조한 뒤
/// 말풍선에 설명을 띄운다. 아무 곳이나 누르거나 [다음]을 누르면 다음 장, [건너뛰기]는 바로 끝.
/// 모든 캔버스(전역 UI 100, 장소 UI 200)보다 위에 있고 입력을 전부 막으므로
/// 튜토리얼 동안 월드·버튼이 눌리지 않는다. 월드는 멈추지 않아서 해달은 계속 돌아다닌다.
/// GameUI처럼 코드로만 만들며, 장소 씬과 함께 사라진다.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const int SortingOrder = 500;

    private const float PanelWidth = 900f;
    private const int TitleFontSize = 48;
    private const int BodyFontSize = 40;
    private const float ButtonHeight = 100f;
    // 대상 둘레의 여백과 테두리 두께 (캔버스 단위)
    private const float HolePadding = 16f;
    private const float FrameThickness = 8f;
    // 말풍선과 대상 사이, 말풍선과 화면 끝 사이
    private const float BubbleGap = 40f;
    private const float ScreenMargin = 40f;
    // 장이 바뀐 직후의 연타로 여러 장이 한꺼번에 넘어가지 않게
    private const float TapCooldownSec = 0.3f;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color PanelColor = new Color(0.97f, 0.94f, 0.86f, 1f);
    private static readonly Color ButtonColor = Color.white;
    private static readonly Color NextButtonColor = new Color(1f, 0.85f, 0.35f, 1f);
    private static readonly Color TextColor = new Color(0.2f, 0.15f, 0.1f, 1f);
    private static readonly Color SubTextColor = new Color(0.45f, 0.38f, 0.3f, 1f);
    private static readonly Color FrameColor = new Color(1f, 0.85f, 0.35f, 1f);

    private Font _font;
    private Canvas _canvas;
    private RectTransform _root;
    private readonly RectTransform[] _dims = new RectTransform[4];
    private RectTransform _frame;
    private readonly List<Image> _frameEdges = new List<Image>();
    private RectTransform _bubble;
    private Text _titleLabel;
    private Text _messageLabel;
    private Text _nextLabel;

    private IReadOnlyList<TutorialStep> _steps;
    private Action _onFinished;
    private int _index = -1;
    private float _stepShownAt;
    private bool _finished;

    public static TutorialOverlay Play(IReadOnlyList<TutorialStep> steps, Action onFinished)
    {
        var go = new GameObject(nameof(TutorialOverlay),
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var overlay = go.AddComponent<TutorialOverlay>();
        overlay.Build();
        overlay._steps = steps;
        overlay._onFinished = onFinished;
        overlay.Advance();
        return overlay;
    }

    private void Update()
    {
        if (_finished || _index < 0) return;

        var hole = ToNormalized(_steps[_index].FindTarget());
        LayoutHole(hole);
        LayoutBubble(hole);

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        var color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, pulse);
        foreach (var edge in _frameEdges) edge.color = color;
    }

    #region 진행

    private void HandleTap()
    {
        if (Time.unscaledTime - _stepShownAt < TapCooldownSec) return;
        Advance();
    }

    // 다음 장으로. 대상이 필요한데 지금 없는 장은 건너뛴다.
    private void Advance()
    {
        if (_finished) return;

        do
        {
            _index++;
        } while (_index < _steps.Count && _steps[_index].NeedsTarget && _steps[_index].FindTarget() == null);

        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }

        var step = _steps[_index];
        _titleLabel.text = step.Title;
        _messageLabel.text = step.Message;
        _nextLabel.text = IsLastShownStep() ? "알겠어요!" : "다음 ▶";
        _stepShownAt = Time.unscaledTime;
        Update();
    }

    // 뒤에 남은 장이 전부 대상 없음으로 건너뛰어질 예정이면 지금이 마지막
    private bool IsLastShownStep()
    {
        for (int i = _index + 1; i < _steps.Count; i++)
        {
            if (!_steps[i].NeedsTarget || _steps[i].FindTarget() != null) return false;
        }
        return true;
    }

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        Destroy(gameObject);
        _onFinished?.Invoke();
    }

    #endregion

    #region 배치

    // 화면 픽셀 영역 → 여백을 더하고 화면 안으로 자른 0~1 영역. 화면 밖이면 null (강조 없이 가운데에 설명)
    private Rect? ToNormalized(Rect? screenRect)
    {
        if (screenRect == null || Screen.width <= 0 || Screen.height <= 0) return null;

        float pad = HolePadding * _canvas.scaleFactor;
        var r = screenRect.Value;
        float xMin = Mathf.Clamp01((r.xMin - pad) / Screen.width);
        float yMin = Mathf.Clamp01((r.yMin - pad) / Screen.height);
        float xMax = Mathf.Clamp01((r.xMax + pad) / Screen.width);
        float yMax = Mathf.Clamp01((r.yMax + pad) / Screen.height);
        if (xMax - xMin <= 0.001f || yMax - yMin <= 0.001f) return null;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // 어두운 조각 4개가 대상 영역만 비워 두고 둘러싼다
    private void LayoutHole(Rect? hole)
    {
        _frame.gameObject.SetActive(hole.HasValue);

        if (hole == null)
        {
            SetAnchors(_dims[0], Vector2.zero, Vector2.one);
            for (int i = 1; i < 4; i++) SetAnchors(_dims[i], Vector2.zero, Vector2.zero);
            return;
        }

        var h = hole.Value;
        SetAnchors(_dims[0], new Vector2(0f, 0f), new Vector2(1f, h.yMin));        // 아래
        SetAnchors(_dims[1], new Vector2(0f, h.yMax), new Vector2(1f, 1f));        // 위
        SetAnchors(_dims[2], new Vector2(0f, h.yMin), new Vector2(h.xMin, h.yMax)); // 왼쪽
        SetAnchors(_dims[3], new Vector2(h.xMax, h.yMin), new Vector2(1f, h.yMax)); // 오른쪽
        SetAnchors(_frame, h.min, h.max);
    }

    // 대상 위아래 중 공간이 넓은 쪽에 말풍선을 두고, 화면 밖으로 나가지 않게 자른다
    private void LayoutBubble(Rect? hole)
    {
        float screenH = _root.rect.height;
        float bubbleH = _bubble.rect.height;
        float halfRange = Mathf.Max(0f, screenH * 0.5f - bubbleH * 0.5f - ScreenMargin);

        float y = 0f;
        if (hole.HasValue)
        {
            float holeTop = (hole.Value.yMax - 0.5f) * screenH;
            float holeBottom = (hole.Value.yMin - 0.5f) * screenH;
            float roomAbove = screenH * 0.5f - holeTop;
            float roomBelow = holeBottom + screenH * 0.5f;
            y = roomBelow >= roomAbove
                ? holeBottom - BubbleGap - bubbleH * 0.5f
                : holeTop + BubbleGap + bubbleH * 0.5f;
        }
        _bubble.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, -halfRange, halfRange));
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    #endregion

    #region 만들기

    private void Build()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        _root = (RectTransform)transform;

        // 투명한 전체 화면 버튼: 뚫린 곳까지 포함해 모든 입력을 받아 다음 장으로
        var catcher = CreateImage("TapCatcher", _root, Color.clear);
        SetAnchors(catcher.rectTransform, Vector2.zero, Vector2.one);
        catcher.gameObject.AddComponent<Button>().onClick.AddListener(HandleTap);

        for (int i = 0; i < 4; i++)
        {
            var dim = CreateImage("Dim", _root, DimColor);
            dim.raycastTarget = false;
            _dims[i] = dim.rectTransform;
        }

        BuildFrame();
        BuildBubble();
    }

    // 뚫린 영역 바깥쪽을 두르는 테두리 4변
    private void BuildFrame()
    {
        _frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        _frame.SetParent(_root, false);

        float t = FrameThickness;
        AddEdge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-t, 0f), new Vector2(t, t));   // 위
        AddEdge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-t, -t), new Vector2(t, 0f));  // 아래
        AddEdge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-t, 0f), new Vector2(0f, 0f)); // 왼쪽
        AddEdge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(t, 0f));  // 오른쪽
    }

    private void AddEdge(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var edge = CreateImage("Edge", _frame, FrameColor);
        edge.raycastTarget = false;
        var rect = edge.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        _frameEdges.Add(edge);
    }

    private void BuildBubble()
    {
        var panel = CreateImage("Bubble", _root, PanelColor);
        panel.raycastTarget = false; // 말풍선을 눌러도 다음 장으로 (버튼은 따로 받음)
        _bubble = panel.rectTransform;
        _bubble.anchorMin = _bubble.anchorMax = _bubble.pivot = new Vector2(0.5f, 0.5f);
        _bubble.sizeDelta = new Vector2(PanelWidth, 0f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 40, 40);
        layout.spacing = 24f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _titleLabel = CreateLabel(_bubble, TitleFontSize, TextColor);
        _titleLabel.fontStyle = FontStyle.Bold;
        _messageLabel = CreateLabel(_bubble, BodyFontSize, TextColor);

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_bubble, false);
        row.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        var skipLabel = CreateButton(row.transform, ButtonColor, 260f, Finish);
        skipLabel.text = "건너뛰기";
        skipLabel.color = SubTextColor;
        _nextLabel = CreateButton(row.transform, NextButtonColor, -1f, HandleTap);
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateLabel(Transform parent, int fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = _font;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    // width < 0이면 남는 폭을 전부 차지. 버튼 글자를 돌려준다.
    private Text CreateButton(Transform parent, Color color, float width, Action onClick)
    {
        var image = CreateImage("Button", parent, color);
        var element = image.gameObject.AddComponent<LayoutElement>();
        if (width > 0f) element.preferredWidth = width;
        else element.flexibleWidth = 1f;
        image.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());

        var label = CreateLabel(image.transform, BodyFontSize, TextColor);
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        return label;
    }

    #endregion
}
