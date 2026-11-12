using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드로 만드는 창 (뽑기 · 가방 화면과 같은 모양): 크림 패널 + 위에 걸친 복숭아 리본 제목 + 모서리 빨간 X + 탭 줄 + 스크롤 목록.
/// 시안 수치(1080×1920): 바깥 여백 L/R 40 · 위 90 · 아래 56, 리본 높이 104, 닫기 96(모서리에 걸침), 탭 높이 100.
/// 내용은 Content에 쌓고(세로 레이아웃), 탭을 고르면 onTab이 불림
/// </summary>
public class RuntimeUIWindow
{
    private const float RibbonHeight = 104f;
    private const float TabHeight = 100f;
    private const float PopSeconds = 0.18f;

    public RectTransform Root { get; private set; }
    public RectTransform Panel { get; private set; }
    public RectTransform Content { get; private set; }
    public ScrollRect Scroll { get; private set; }

    private readonly List<(Image image, TextMeshProUGUI label)> _tabs = new List<(Image, TextMeshProUGUI)>();
    private RectTransform _viewport;
    // 스크롤 목록 위 · 아래 끝 (패널 기준). 고정 머리말 · 바닥을 붙이면 목록이 그만큼 줄어듦
    private float _top;
    private float _bottom = ViewportBottom;
    private const float ViewportBottom = 44f;
    private const float FixedGap = 12f;

    /// <param name="tabs">탭 이름 (없으면 탭 줄 없음)</param>
    public static RuntimeUIWindow Create(Transform canvas, string title, Sprite titleIcon, IReadOnlyList<string> tabs, int selectedTab,
        Action<int> onTab, Action close)
    {
        var window = new RuntimeUIWindow();
        var style = RuntimeUIKit.Style;
        window.Root = RuntimeUIKit.CreateDim(canvas, "Window");

        var safe = Rect("SafeArea", window.Root);
        RuntimeUIKit.Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        // 패널: 리본이 위 모서리에 반쯤 걸치도록 위 여백 90 + 리본 절반
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(safe, false);
        window.Panel = (RectTransform)panelGo.transform;
        RuntimeUIKit.Stretch(window.Panel);
        window.Panel.offsetMin = new Vector2(40f, 56f);
        window.Panel.offsetMax = new Vector2(-40f, -(90f + RibbonHeight * 0.5f));
        RuntimeUIKit.ApplySprite(panelGo.GetComponent<Image>(), style.Panel, new Color(0.99f, 0.96f, 0.9f));

        // 리본 제목 · 모서리 닫기
        AddRibbon(window.Panel, title, titleIcon);
        AddClose(window.Panel, close);

        // 탭 줄
        float top = RibbonHeight * 0.5f + 28f;
        if (tabs != null && tabs.Count > 1)
        {
            var row = Rect("Tabs", window.Panel);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(36f, -(top + TabHeight));
            row.offsetMax = new Vector2(-36f, -top);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int i = 0; i < tabs.Count; i++)
                window.AddTab(row, tabs[i], i, onTab);
            window.SelectTab(selectedTab);
            top += TabHeight + 18f;
        }

        // 스크롤 목록
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
        viewportGo.transform.SetParent(window.Panel, false);
        var viewport = (RectTransform)viewportGo.transform;
        RuntimeUIKit.Stretch(viewport);
        viewport.offsetMin = new Vector2(30f, ViewportBottom);
        viewport.offsetMax = new Vector2(-30f, -top);
        window._viewport = viewport;
        window._top = top;
        viewportGo.GetComponent<Image>().color = Color.clear; // 빈 곳을 끌어도 스크롤

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        window.Content = (RectTransform)contentGo.transform;
        window.Content.anchorMin = new Vector2(0f, 1f);
        window.Content.anchorMax = new Vector2(1f, 1f);
        window.Content.pivot = new Vector2(0.5f, 1f);
        window.Content.sizeDelta = Vector2.zero;
        var contentLayout = contentGo.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 24);
        contentLayout.spacing = 18f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        window.Scroll = viewportGo.GetComponent<ScrollRect>();
        window.Scroll.viewport = viewport;
        window.Scroll.content = window.Content;
        window.Scroll.horizontal = false;
        window.Scroll.movementType = ScrollRect.MovementType.Clamped;
        window.Scroll.scrollSensitivity = 30f;
        return window;
    }

    private void AddTab(Transform row, string text, int index, Action<int> onTab)
    {
        var go = new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(row, false);
        var image = go.GetComponent<Image>();
        var label = RuntimeUICards.Text(go.transform, text, 34, true);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var labelRect = (RectTransform)label.transform;
        RuntimeUIKit.Stretch(labelRect);
        labelRect.offsetMin = new Vector2(6f, 12f); // 탭 아래 두께만큼 올림
        go.GetComponent<Button>().onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            onTab?.Invoke(index);
        });
        _tabs.Add((image, label));
    }

    public void SelectTab(int index)
    {
        var style = RuntimeUIKit.Style;
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool selected = i == index;
            RuntimeUIKit.ApplySprite(_tabs[i].image, selected ? style.TabSelected : style.TabNormal, Color.white);
            _tabs[i].label.color = selected ? style.TextColor : new Color32(0x9A, 0x7F, 0x6E, 0xFF);
        }
    }

    /// <summary>패널 위 모서리에 반쯤 걸친 복숭아 리본 제목 (글자에 맞춰 가로만 늘어남). 레이아웃 패널에 붙여도 줄에 끼지 않음</summary>
    /// <returns>제목 글 (나중에 바꿀 때)</returns>
    public static TextMeshProUGUI AddRibbon(RectTransform panel, string title, Sprite titleIcon = null)
    {
        var style = RuntimeUIKit.Style;
        var ribbonGo = new GameObject("Ribbon", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        ribbonGo.transform.SetParent(panel, false);
        var ribbon = (RectTransform)ribbonGo.transform;
        ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
        ribbon.pivot = new Vector2(0.5f, 0.5f);
        ribbon.anchoredPosition = Vector2.zero;
        RuntimeUIKit.ApplySprite(ribbonGo.GetComponent<Image>(), style.Ribbon, new Color(0.96f, 0.78f, 0.68f));
        var ribbonLayout = ribbonGo.GetComponent<HorizontalLayoutGroup>();
        ribbonLayout.padding = new RectOffset(64, 64, 6, 18);
        ribbonLayout.spacing = 12f;
        ribbonLayout.childAlignment = TextAnchor.MiddleCenter;
        ribbonLayout.childControlWidth = true;
        ribbonLayout.childControlHeight = true;
        ribbonLayout.childForceExpandWidth = false;
        ribbonLayout.childForceExpandHeight = false;
        var fitter = ribbonGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        var element = ribbonGo.AddComponent<LayoutElement>();
        element.minHeight = RibbonHeight;
        element.ignoreLayout = true;
        ribbon.sizeDelta = new Vector2(0f, RibbonHeight);
        if (titleIcon != null)
            RuntimeUICards.Icon(ribbon, titleIcon, 76f);
        var titleLabel = RuntimeUICards.Text(ribbon, title, 44, true);
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.color = style.TextColor;
        // 높이는 고정 (가로만 글자에 맞춤)
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return titleLabel;
    }

    /// <summary>패널 오른쪽 위 모서리에 걸친 빨간 X</summary>
    public static Button AddClose(RectTransform panel, Action close)
    {
        var style = RuntimeUIKit.Style;
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeGo.transform.SetParent(panel, false);
        closeGo.GetComponent<LayoutElement>().ignoreLayout = true;
        var closeRect = (RectTransform)closeGo.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(20f, 34f);
        closeRect.sizeDelta = new Vector2(96f, 96f);
        var closeImage = closeGo.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(closeImage, style.CloseButton, style.WarningColor);
        closeImage.raycastPadding = new Vector4(-16f, -16f, -16f, -16f);
        var button = closeGo.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            close();
        });
        return button;
    }

    /// <summary>탭 줄 아래 고정 칸 (스크롤되지 않음, 예: 연구 포인트 · 분야 탭). 목록은 그만큼 아래로</summary>
    public RectTransform AddHeader(float height)
    {
        var header = Rect("Header", Panel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(36f, -(_top + height));
        header.offsetMax = new Vector2(-36f, -_top);
        _top += height + FixedGap;
        _viewport.offsetMax = new Vector2(-30f, -_top);
        return header;
    }

    /// <summary>패널 아래 고정 칸 (스크롤되지 않음, 예: 고른 기술 시트). 목록은 그만큼 위로</summary>
    public RectTransform AddFooter(float height)
    {
        var footer = Rect("Footer", Panel);
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.offsetMin = new Vector2(24f, ViewportBottom - 20f);
        footer.offsetMax = new Vector2(-24f, ViewportBottom - 20f + height);
        _bottom = ViewportBottom - 20f + height + FixedGap;
        _viewport.offsetMin = new Vector2(30f, _bottom);
        return footer;
    }

    /// <summary>탭 오른쪽 위 빨간 점 (그 탭에 할 일이 있을 때)</summary>
    public void SetTabDot(int index, bool on)
    {
        if (index < 0 || index >= _tabs.Count)
            return;
        var tab = _tabs[index].image.transform;
        var dot = tab.Find("Dot");
        if (dot == null)
        {
            if (!on)
                return;
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tab, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.anchoredPosition = new Vector2(-4f, 0f);
            var image = go.GetComponent<Image>();
            var red = new Color(0.9f, 0.32f, 0.27f);
            RuntimeUIKit.ApplySprite(image, RuntimeUIKit.Style.Knob, red);
            image.color = red; // ApplySprite는 그림이 있으면 흰색으로 둠
            image.raycastTarget = false;
            dot = go.transform;
        }
        dot.gameObject.SetActive(on);
    }

    /// <summary>열 때 살짝 튀어나옴</summary>
    public void PopIn(MonoBehaviour host) => host.StartCoroutine(PopRoutine(Panel));

    private static IEnumerator PopRoutine(RectTransform panel)
    {
        for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / PopSeconds;
            float s = Mathf.LerpUnclamped(0.92f, 1f, 1f + 1.6f * Mathf.Pow(k - 1f, 3) + 0.6f * Mathf.Pow(k - 1f, 2));
            if (panel == null)
                yield break;
            panel.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (panel != null)
            panel.localScale = Vector3.one;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
