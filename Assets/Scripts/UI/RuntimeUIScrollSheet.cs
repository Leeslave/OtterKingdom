using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>내용이 긴 런타임 시트: 안전 영역 안에서 스크롤하고 닫기 버튼은 화면 아래에 고정한다.</summary>
public static class RuntimeUIScrollSheet
{
    public static RectTransform Create(Transform parent, Action close)
    {
        var dim = RuntimeUIKit.CreateDim(parent);
        var frameObject = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
        frameObject.transform.SetParent(dim, false);
        var frame = (RectTransform)frameObject.transform;
        var safe = Screen.safeArea;
        float screenWidth = Mathf.Max(1, Screen.width), screenHeight = Mathf.Max(1, Screen.height);
        frame.anchorMin = new Vector2(safe.xMin / screenWidth + .04f, safe.yMin / screenHeight + .04f);
        frame.anchorMax = new Vector2(safe.xMax / screenWidth - .04f, safe.yMax / screenHeight - .04f);
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        var image = frameObject.GetComponent<Image>();
        image.sprite = RuntimeUIKit.Style.Panel;
        image.type = Image.Type.Sliced;
        image.color = new Color(.97f, .94f, .86f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
        viewportObject.transform.SetParent(frame, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(16, 140);
        viewport.offsetMax = new Vector2(-16, -20);
        viewportObject.GetComponent<Image>().color = Color.clear;
        var content = RuntimeUIKit.CreatePanel(viewport, 900, "Content");
        content.GetComponent<Image>().enabled = false;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var button = RuntimeUIKit.CreateButton(frame, "닫기", close, RuntimeUIKit.ButtonKind.Secondary);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1, 0);
        rect.offsetMin = new Vector2(24, 20);
        rect.offsetMax = new Vector2(-24, 124);
        return content;
    }
}
