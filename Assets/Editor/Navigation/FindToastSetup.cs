using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GlobalUISetup;

/// <summary>
/// 광산 밖에서 캔 것을 알려 주는 작은 알림 (하단 바 바로 위 가운데). GlobalUISetup이 함께 호출한다.
/// </summary>
public static class FindToastSetup
{
    public static FindToastView Build(RectTransform hud)
    {
        var layer = CreateRect("FindToast", hud);
        Stretch(layer, 0);
        var group = layer.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var body = CreateImage("Body", layer, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        body.pixelsPerUnitMultiplier = 1.3f;
        Place(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(460, 88));

        var icon = CreateImage("Icon", body.rectTransform, null, false);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(18, 2), new Vector2(64, 64));

        var text = CreateText("Text", body.rectTransform, _titleFont, "광산 · 다이아몬드 +1", 30, Cocoa);
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20;
        text.fontSizeMax = 30;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(96, 6);
        text.rectTransform.offsetMax = new Vector2(-20, -4);

        var view = layer.gameObject.AddComponent<FindToastView>();
        Set(view, "_group", group);
        Set(view, "_body", body.rectTransform);
        Set(view, "_icon", icon);
        Set(view, "_text", text);
        return view;
    }
}
