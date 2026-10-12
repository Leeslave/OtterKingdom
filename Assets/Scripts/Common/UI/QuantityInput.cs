using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 수량 글자를 클릭 가능한 숫자 입력칸으로 바꾼다. 기존 프리팹에서도 적용된다.</summary>
public static class QuantityInput
{
    public static TMP_InputField Create(TextMeshProUGUI label, SellQuantity quantity, Action refresh)
    {
        var oldRect = label.rectTransform;
        var go = new GameObject("QuantityInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        var rect = (RectTransform)go.transform;
        rect.SetParent(oldRect.parent, false);
        rect.SetSiblingIndex(oldRect.GetSiblingIndex());
        rect.anchorMin = oldRect.anchorMin;
        rect.anchorMax = oldRect.anchorMax;
        rect.pivot = oldRect.pivot;
        rect.sizeDelta = oldRect.sizeDelta;
        rect.anchoredPosition = oldRect.anchoredPosition;
        var oldLayout = label.GetComponent<LayoutElement>();
        if (oldLayout != null)
        {
            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = oldLayout.minWidth;
            layout.minHeight = oldLayout.minHeight;
            layout.preferredWidth = oldLayout.preferredWidth;
            layout.preferredHeight = oldLayout.preferredHeight;
            layout.flexibleWidth = oldLayout.flexibleWidth;
            layout.flexibleHeight = oldLayout.flexibleHeight;
            layout.ignoreLayout = oldLayout.ignoreLayout;
        }
        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.15f);

        label.transform.SetParent(rect, false);
        oldRect.anchorMin = Vector2.zero;
        oldRect.anchorMax = Vector2.one;
        oldRect.offsetMin = new Vector2(6f, 0f);
        oldRect.offsetMax = new Vector2(-6f, 0f);
        label.raycastTarget = false;
        label.richText = false;
        label.enableAutoSizing = true;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = 18f;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = rect;
        input.textComponent = label;
        input.targetGraphic = image;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.keyboardType = TouchScreenKeyboardType.NumberPad;
        input.characterLimit = 10;
        input.richText = false;
        input.onValidateInput = (_, __, character) => character >= '0' && character <= '9' ? character : '\0';
        input.SetTextWithoutNotify(quantity.Value.ToString());
        input.onValueChanged.AddListener(value =>
        {
            if (quantity.TrySetFromText(value)) refresh();
        });
        input.onEndEdit.AddListener(_ =>
        {
            Commit(input, quantity);
            refresh();
        });
        return input;
    }

    public static void Commit(TMP_InputField input, SellQuantity quantity)
    {
        if (input == null) return;
        quantity.TrySetFromText(input.text);
        input.SetTextWithoutNotify(quantity.Value.ToString());
    }
}
