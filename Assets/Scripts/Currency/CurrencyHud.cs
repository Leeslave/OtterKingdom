using UnityEngine;
using UnityEngine.UI;

// M1 vertical-slice: minimal always-on top-of-screen currency readout, built
// entirely at runtime (Show) so no scene/prefab wiring is required. Replace
// with a proper uGUI/CurrencyUI view once real HUD art exists.
public class CurrencyHud : MonoBehaviour
{
    private Currency currency;
    private Text label;

    public static void Show(Currency currency)
    {
        var go = new GameObject(nameof(CurrencyHud),
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        go.AddComponent<CurrencyHud>().Initialize(currency);
    }

    private void Initialize(Currency targetCurrency)
    {
        currency = targetCurrency;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(transform, false);

        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(220f, 50f);
        panelRect.anchoredPosition = new Vector2(0f, -10f);
        panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(panelGO.transform, false);

        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        label = labelGO.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Exists)
            CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
    }

    private void OnCurrencyChanged(Currency changed, int amount)
    {
        if (changed == currency) Refresh();
    }

    private void Refresh()
    {
        label.text = $"코인 {CurrencyManager.Instance.GetCurrency(currency)}";
    }
}
