using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 화면: 화면 전체를 어둡게 덮고, 설명할 대상만 뚫어서 반짝이는 테두리로 강조한 뒤
/// 말풍선에 설명을 띄운다. 아무 곳이나 누르거나 [다음]을 누르면 다음 장, [건너뛰기]는 바로 끝.
/// "직접 해 보기" 장은 뚫린 곳만 눌리고(그 입력은 아래 게임에 그대로 전해짐), 누르면 튜토리얼이 끝난다.
/// 강조할 월드 대상이 화면 밖이면 광장 카메라를 그쪽으로 옮긴다.
/// 모든 캔버스(전역 UI 100, 장소 UI 200)보다 위에 있고 입력을 전부 막으므로
/// 튜토리얼 동안 월드·버튼이 눌리지 않는다. 월드는 멈추지 않아서 해달은 계속 돌아다닌다.
/// GameUI처럼 코드로만 만들며, 장소 씬과 함께 사라진다.
/// 모양(패널·버튼·폰트·안내 해달)은 Resources/TutorialStyle에서 읽어 다른 UI와 맞춘다 (없으면 단색).
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const int SortingOrder = 500;

    private const float PanelWidth = 900f;
    private const float TitleFontSize = 50f;
    private const float BodyFontSize = 36f;
    private const float ButtonHeight = 112f;
    private const float PortraitSize = 150f;
    // 대상 둘레의 여백과, 테두리가 그 바깥으로 나오는 폭 (캔버스 단위)
    private const float HolePadding = 16f;
    private const float FrameOutset = 14f;
    // 말풍선과 대상 사이, 말풍선과 화면 끝 사이
    private const float BubbleGap = 48f;
    private const float ScreenMargin = 40f;
    // 장이 바뀐 직후의 연타로 여러 장이 한꺼번에 넘어가지 않게
    private const float TapCooldownSec = 0.3f;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    // 스타일이 없을 때(단색)만 쓰는 색
    private static readonly Color FallbackPanelColor = new Color(1f, 0.96f, 0.9f, 1f);
    private static readonly Color FallbackNextColor = new Color(0.6f, 0.71f, 0.53f, 1f);
    private static readonly Color FallbackSkipColor = Color.white;

    private TutorialStyle _style;
    private Canvas _canvas;
    private RectTransform _root;
    private readonly RectTransform[] _dims = new RectTransform[4];
    private RectTransform _frame;
    private Image _frameImage;
    private RectTransform _bubble;
    private Image _catcher;
    private Image[] _dimImages = new Image[4];
    private GameObject _nextButton;
    private bool _focused;
    private bool _pressInHole;
    private TextMeshProUGUI _titleLabel;
    private TextMeshProUGUI _messageLabel;
    private TextMeshProUGUI _counterLabel;
    private TextMeshProUGUI _nextLabel;

    private IReadOnlyList<TutorialStep> _steps;
    private Action<bool> _onFinished;
    private int _index = -1;
    private int _shownCount;
    private int _shownTotal;
    private float _stepShownAt;
    private bool _finished;

    private static int _showingCount;

    /// <summary>튜토리얼이 화면을 덮고 있는지 (새 해달 방문 등은 끝날 때까지 기다림)</summary>
    public static bool IsShowing => _showingCount > 0;

    /// <param name="onFinished">끝까지 보거나 건너뛰었을 때 (둘을 가리지 않음)</param>
    public static TutorialOverlay Play(IReadOnlyList<TutorialStep> steps, Action onFinished) =>
        Play(steps, _ => onFinished?.Invoke());

    /// <param name="onFinished">끝났을 때. 인자: [건너뛰기]로 끝냈는지</param>
    public static TutorialOverlay Play(IReadOnlyList<TutorialStep> steps, Action<bool> onFinished)
    {
        var go = new GameObject(nameof(TutorialOverlay),
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var overlay = go.AddComponent<TutorialOverlay>();
        _showingCount++;
        overlay._counted = true;
        overlay._steps = steps;
        overlay._onFinished = onFinished;
        overlay.Build();
        overlay.CountShownSteps();
        overlay.Advance();
        return overlay;
    }

    // 씬과 함께 사라져도 덮고 있다는 표시가 남지 않게
    private void OnDestroy()
    {
        if (!_counted)
            return;
        _counted = false;
        _showingCount = Mathf.Max(0, _showingCount - 1);
    }

    private bool _counted;

    // 플레이 모드를 다시 시작해도(도메인 다시 불러오기 없이) 지난 판의 수가 남지 않게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => _showingCount = 0;

    private void Update()
    {
        if (_finished || _index < 0) return;

        var step = _steps[_index];
        var target = step.FindTarget();
        FocusIfOffscreen(step, target);
        var hole = ToNormalized(target);
        LayoutHole(hole);
        LayoutBubble(hole);
        UpdateTryIt(step, hole);

        // 테두리가 은은하게 숨쉬듯 깜빡임
        var color = _frameImage.color;
        color.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        _frameImage.color = color;
    }

    #region 진행

    private void HandleTap()
    {
        if (Time.unscaledTime - _stepShownAt < TapCooldownSec) return;
        Advance();
    }

    // 시작할 때 대상이 있는(또는 대상이 필요 없는) 장 수 = "3 / 12"의 12
    private void CountShownSteps()
    {
        _shownTotal = 0;
        foreach (var step in _steps)
        {
            if (!step.NeedsTarget || step.FindTarget() != null) _shownTotal++;
        }
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
            Finish(false);
            return;
        }

        var step = _steps[_index];
        _shownCount++;
        _titleLabel.text = step.Title;
        _messageLabel.text = step.Message;
        _counterLabel.text = $"{_shownCount} / {Mathf.Max(_shownTotal, _shownCount)}";
        _nextLabel.text = IsLastShownStep() ? "알겠어요!" : "다음";
        _stepShownAt = Time.unscaledTime;
        _focused = false;
        _pressInHole = false;
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

    // 월드 대상이 화면에 다 들어오지 않으면 광장 카메라를 그쪽으로 (장마다 한 번)
    private void FocusIfOffscreen(TutorialStep step, Rect? target)
    {
        if (_focused) return;
        var focus = step.FindFocus();
        if (focus == null) return;
        _focused = true;
        if (target.HasValue && target.Value.xMin >= 0f && target.Value.yMin >= 0f
            && target.Value.xMax <= Screen.width && target.Value.yMax <= Screen.height) return;
        var camera = FindAnyObjectByType<PlazaCameraController>();
        if (camera != null && camera.isActiveAndEnabled) camera.PanTo(focus.Value);
    }

    // 직접 해 보기: 뚫린 곳만 입력을 받고(어두운 조각이 나머지를 막음), 뚫린 곳에서 누르고 떼면 끝.
    // 대상이 안 보이면 막히지 않게 평소처럼 [알겠어요]로 넘김
    private void UpdateTryIt(TutorialStep step, Rect? hole)
    {
        bool interactive = step.IsTryIt && hole.HasValue;
        _catcher.raycastTarget = !interactive;
        foreach (var dim in _dimImages) dim.raycastTarget = interactive;
        if (_nextButton.activeSelf == interactive) _nextButton.SetActive(!interactive);
        if (!interactive) return;

        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null) return;
        var position = pointer.position.ReadValue();
        var h = hole.Value;
        bool inside = h.Contains(new Vector2(position.x / Screen.width, position.y / Screen.height));
        if (pointer.press.wasPressedThisFrame) _pressInHole = inside;
        // 누른 입력은 아래 게임(게시판·고랑·버튼)이 그대로 받고, 튜토리얼은 이 프레임이 끝나면 사라짐
        if (pointer.press.wasReleasedThisFrame && _pressInHole && inside) Finish(false);
    }

    private void Finish(bool skipped)
    {
        if (_finished) return;
        _finished = true;
        // 콜백이 바로 다음 튜토리얼을 열 수 있으므로 덮고 있다는 표시는 먼저 내림
        OnDestroy();
        Destroy(gameObject);
        _onFinished?.Invoke(skipped);
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

    // 어두운 조각 4개가 대상 영역만 비워 두고 둘러싼다. 테두리는 뚫린 곳 바깥에 걸침
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
        _frame.offsetMin = new Vector2(-FrameOutset, -FrameOutset);
        _frame.offsetMax = new Vector2(FrameOutset, FrameOutset);
    }

    // 대상 위아래 중 공간이 넓은 쪽에 말풍선을 두고, 화면 밖으로 나가지 않게 자른다
    private void LayoutBubble(Rect? hole)
    {
        float screenH = _root.rect.height;
        float bubbleH = _bubble.rect.height;
        // 위로 걸친 안내 해달 얼굴까지 화면 안에 들어오게
        float topExtra = _style.GuidePortrait != null ? PortraitSize * 0.5f : 0f;
        float maxY = Mathf.Max(0f, screenH * 0.5f - bubbleH * 0.5f - ScreenMargin - topExtra);
        float minY = -Mathf.Max(0f, screenH * 0.5f - bubbleH * 0.5f - ScreenMargin);

        float y = 0f;
        if (hole.HasValue)
        {
            float holeTop = (hole.Value.yMax - 0.5f) * screenH;
            float holeBottom = (hole.Value.yMin - 0.5f) * screenH;
            float roomAbove = screenH * 0.5f - holeTop;
            float roomBelow = holeBottom + screenH * 0.5f;
            y = roomBelow >= roomAbove
                ? holeBottom - BubbleGap - bubbleH * 0.5f
                : holeTop + BubbleGap + bubbleH * 0.5f + topExtra;
        }
        _bubble.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, minY, maxY));
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
        _style = Resources.Load<TutorialStyle>(TutorialStyle.ResourcePath);
        if (_style == null) _style = ScriptableObject.CreateInstance<TutorialStyle>();

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        _root = (RectTransform)transform;

        // 투명한 전체 화면 버튼: 뚫린 곳까지 포함해 모든 입력을 받아 다음 장으로
        _catcher = CreateImage("TapCatcher", _root, null, Color.clear);
        SetAnchors(_catcher.rectTransform, Vector2.zero, Vector2.one);
        _catcher.gameObject.AddComponent<Button>().onClick.AddListener(HandleTap);

        for (int i = 0; i < 4; i++)
        {
            var dim = CreateImage("Dim", _root, null, DimColor);
            dim.raycastTarget = false;
            _dims[i] = dim.rectTransform;
            _dimImages[i] = dim;
        }

        _frameImage = CreateImage("Frame", _root, _style.Highlight, Color.white);
        _frameImage.raycastTarget = false;
        _frame = _frameImage.rectTransform;
        // 테두리 그림이 없으면 강조 테두리 없이 (단색 사각형이 대상을 덮지 않게)
        _frameImage.enabled = _style.Highlight != null;

        BuildBubble();
        PrepareGlyphs();
    }

    private void BuildBubble()
    {
        var panel = CreateImage("Bubble", _root, _style.Panel, _style.Panel != null ? Color.white : FallbackPanelColor);
        panel.raycastTarget = false; // 말풍선을 눌러도 다음 장으로 (버튼은 따로 받음)
        _bubble = panel.rectTransform;
        _bubble.anchorMin = _bubble.anchorMax = _bubble.pivot = new Vector2(0.5f, 0.5f);
        _bubble.sizeDelta = new Vector2(PanelWidth, 0f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 56, 52);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _titleLabel = CreateLabel(_bubble, _style.TitleFont, TitleFontSize, _style.TitleColor);
        // 안내 해달 얼굴과 장 번호를 피해 제목 좌우를 비움
        _titleLabel.margin = new Vector4(90f, 0f, 90f, 0f);

        if (_style.Divider != null)
        {
            var divider = CreateImage("Divider", _bubble, _style.Divider, Color.white);
            divider.preserveAspect = true;
            divider.raycastTarget = false;
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        }

        _messageLabel = CreateLabel(_bubble, _style.BodyFont, BodyFontSize, _style.BodyColor);
        _messageLabel.lineSpacing = 12f;

        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_bubble, false);
        var rowElement = row.GetComponent<LayoutElement>();
        rowElement.preferredHeight = ButtonHeight;
        rowElement.minHeight = ButtonHeight;
        var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 20f;
        rowLayout.padding = new RectOffset(0, 0, 10, 0);
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        var skipLabel = CreateButton(row.transform, _style.SkipButton, FallbackSkipColor, 260f, () => Finish(true));
        skipLabel.text = "건너뛰기";
        skipLabel.color = _style.SubColor;
        _nextLabel = CreateButton(row.transform, _style.NextButton, FallbackNextColor, -1f, HandleTap);
        _nextButton = _nextLabel.transform.parent.gameObject;
        _nextLabel.color = _style.NextLabelColor;
        _nextLabel.fontSize = 42f;

        // 장 번호 "3 / 12" (오른쪽 위)
        _counterLabel = CreateLabel(_bubble, _style.BodyFont, 26f, _style.SubColor);
        _counterLabel.alignment = TextAlignmentOptions.Right;
        _counterLabel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var counter = _counterLabel.rectTransform;
        counter.anchorMin = counter.anchorMax = counter.pivot = new Vector2(1f, 1f);
        counter.sizeDelta = new Vector2(140f, 40f);
        counter.anchoredPosition = new Vector2(-36f, -26f);

        // 안내 해달 얼굴: 말풍선 왼쪽 위 모서리에 걸침
        if (_style.GuidePortrait != null)
        {
            var frame = CreateImage("GuideFrame", _bubble, _style.PortraitFrame, Color.white);
            frame.raycastTarget = false;
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            frameRect.anchoredPosition = new Vector2(70f, -10f);

            var portrait = CreateImage("Portrait", frameRect, _style.GuidePortrait, Color.white);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            SetAnchors(portrait.rectTransform, Vector2.zero, Vector2.one);
            portrait.rectTransform.offsetMin = new Vector2(14f, 14f);
            portrait.rectTransform.offsetMax = new Vector2(-14f, -14f);
        }
    }

    // 동적 폰트에 이번 튜토리얼 글자를 미리 넣어 둔다 (처음 보는 글자가 그려지는 순간의 끊김 방지)
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789 /!다음알겠어요건너뛰기");
        foreach (var step in _steps) text.Append(step.Title).Append(step.Message);
        string characters = text.ToString();
        foreach (var font in new[] { _titleLabel.font, _messageLabel.font })
        {
            if (font != null) font.TryAddCharacters(characters, out _);
        }
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, TMP_FontAsset font, float fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    // width < 0이면 남는 폭을 전부 차지. 버튼 글자를 돌려준다.
    private TextMeshProUGUI CreateButton(Transform parent, Sprite sprite, Color fallbackColor, float width, Action onClick)
    {
        var image = CreateImage("Button", parent, sprite, sprite != null ? Color.white : fallbackColor);
        var element = image.gameObject.AddComponent<LayoutElement>();
        if (width > 0f) element.preferredWidth = width;
        else element.flexibleWidth = 1f;
        image.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());

        var label = CreateLabel(image.transform, _style.TitleFont, 38f, _style.TitleColor);
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        // 버튼 아래 입체 턱만큼 글자를 위로
        label.rectTransform.offsetMin = new Vector2(8f, 12f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        return label;
    }

    #endregion
}
