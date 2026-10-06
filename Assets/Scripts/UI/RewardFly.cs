using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>보상 아이콘이 날아갈 곳</summary>
public enum RewardTarget
{
    Bag,    // 하단 바 가방 버튼 (아이템)
    Gold,   // 상단 바 골드
    Gem,    // 상단 바 조개
}

/// <summary>
/// 보상을 얻은 자리(월드)에서 아이콘이 톡 튀어 올라 상단 바·가방 버튼으로 날아가고, 도착하면 그곳이 통 튀는 연출.
/// 전역 UI(GlobalUI) 캔버스 맨 위에 처음 쓸 때 만들어진다. 대상을 못 찾으면(테스트 씬 등) 아무것도 안 함.
/// </summary>
public class RewardFly : MonoBehaviour
{
    private const int MaxIcons = 5;
    private const float IconSize = 84f;
    private const float PopSeconds = 0.18f;
    private const float FlySeconds = 0.55f;
    private const float Stagger = 0.07f;
    private const float PunchSeconds = 0.22f;
    private const float LabelSeconds = 0.9f;
    private const float LabelRise = 110f;
    private static readonly Color LabelColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);

    private static RewardFly _instance;

    private RectTransform _layer;
    private readonly Dictionary<RectTransform, Coroutine> _punches = new Dictionary<RectTransform, Coroutine>();
    private readonly Dictionary<RectTransform, Vector3> _baseScales = new Dictionary<RectTransform, Vector3>();

    /// <summary>world에서 아이콘 count개(최대 5)가 target으로 날아감. amountLabel이 있으면 그 자리에 "+N"이 떠올랐다 사라짐</summary>
    public static void FromWorld(Sprite icon, Vector3 world, RewardTarget target, int count, int amountLabel = 0)
    {
        if (icon == null || count <= 0)
            return;
        var fly = Instance();
        var camera = Camera.main;
        var to = fly != null ? fly.FindTarget(target) : null;
        if (fly == null || camera == null || to == null)
            return;
        var screen = camera.WorldToScreenPoint(world);
        fly.Launch(icon, screen, to, Mathf.Min(count, MaxIcons));
        if (amountLabel > 0)
            fly.StartCoroutine(fly.AmountLabel(screen, $"+{amountLabel}"));
    }

    private static RewardFly Instance()
    {
        if (_instance != null)
            return _instance;
        var root = GlobalUIRoot.Instance;
        if (root == null)
            return null;
        var canvas = root.GetComponentInChildren<Canvas>();
        if (canvas == null)
            return null;

        var go = new GameObject("RewardFly", typeof(RectTransform));
        var layer = (RectTransform)go.transform;
        layer.SetParent(canvas.rootCanvas.transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.SetAsLastSibling();
        _instance = go.AddComponent<RewardFly>();
        _instance._layer = layer;
        return _instance;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private RectTransform FindTarget(RewardTarget target)
    {
        if (target == RewardTarget.Bag)
        {
            var bag = FindDeep(GlobalUIRoot.Instance.transform, "NavBar", "Bag");
            return bag != null && bag.gameObject.activeInHierarchy ? bag : null;
        }
        foreach (var pill in FindObjectsByType<CurrencyPillView>(FindObjectsSortMode.None))
        {
            if (pill.Currency == null)
                continue;
            bool gem = pill.Currency.CurrencyID == "Gem";
            if ((target == RewardTarget.Gem) == gem)
                return (RectTransform)pill.transform;
        }
        return null;
    }

    // parentName 아래 childName (전역 UI 안 어디든)
    private static RectTransform FindDeep(Transform root, string parentName, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != parentName)
                continue;
            var child = t.Find(childName);
            if (child != null)
                return child as RectTransform;
        }
        return null;
    }

    // 얻은 자리에서 "+N"이 떠오르며 사라짐 (전역 UI의 글꼴을 빌림)
    private IEnumerator AmountLabel(Vector3 fromScreen, string text)
    {
        var font = FindFont();
        if (font == null)
            yield break;
        var go = new GameObject("RewardAmount", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_layer, false);
        rect.sizeDelta = new Vector2(240f, 80f);
        var label = go.AddComponent<TMPro.TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = 56f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.raycastTarget = false;
        var start = fromScreen + Vector3.up * 40f;
        for (float t = 0f; t < LabelSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / LabelSeconds;
            rect.position = start + Vector3.up * (LabelRise * (1f - (1f - k) * (1f - k)));
            rect.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.1f, k / 0.15f) : 1f);
            var color = LabelColor;
            color.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            label.color = color;
            yield return null;
        }
        Destroy(go);
    }

    private TMPro.TMP_FontAsset _font;

    private TMPro.TMP_FontAsset FindFont()
    {
        if (_font != null)
            return _font;
        foreach (var text in GlobalUIRoot.Instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
        {
            if (text.font != null)
                return _font = text.font;
        }
        return null;
    }

    private void Launch(Sprite icon, Vector3 fromScreen, RectTransform target, int count)
    {
        _layer.SetAsLastSibling();
        for (int i = 0; i < count; i++)
            StartCoroutine(Fly(icon, fromScreen, target, i * Stagger));
    }

    private IEnumerator Fly(Sprite icon, Vector3 fromScreen, RectTransform target, float delay)
    {
        var go = new GameObject("RewardIcon", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_layer, false);
        rect.sizeDelta = new Vector2(IconSize, IconSize);
        var image = go.AddComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;

        // 톡 튀어 오르는 자리 (조금씩 흩어짐)
        var start = fromScreen;
        var pop = start + new Vector3(Random.Range(-60f, 60f), Random.Range(70f, 130f), 0f);
        rect.position = start;
        rect.localScale = Vector3.zero;

        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime)
            yield return null;

        for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / PopSeconds;
            rect.position = Vector3.Lerp(start, pop, 1f - (1f - k) * (1f - k));
            rect.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.15f, k);
            yield return null;
        }

        // 위로 둥글게 휘어 날아감 (도착할수록 빨라지고 작아짐)
        var control = new Vector3((pop.x + target.position.x) * 0.5f, Mathf.Max(pop.y, target.position.y) + 120f, 0f);
        for (float t = 0f; t < FlySeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / FlySeconds;
            float e = k * k;
            var end = target.position;
            var a = Vector3.Lerp(pop, control, e);
            var b = Vector3.Lerp(control, end, e);
            rect.position = Vector3.Lerp(a, b, e);
            rect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.6f, e);
            yield return null;
        }

        Destroy(go);
        Punch(target);
    }

    // 도착한 곳이 통 튐 (여러 개가 연달아 와도 원래 크기로 돌아옴)
    private void Punch(RectTransform target)
    {
        if (target == null)
            return;
        if (!_baseScales.ContainsKey(target))
            _baseScales[target] = target.localScale;
        if (_punches.TryGetValue(target, out var running) && running != null)
            StopCoroutine(running);
        _punches[target] = StartCoroutine(PunchRoutine(target, _baseScales[target]));
    }

    private IEnumerator PunchRoutine(RectTransform target, Vector3 baseScale)
    {
        for (float t = 0f; t < PunchSeconds; t += Time.unscaledDeltaTime)
        {
            if (target == null)
                yield break;
            float k = t / PunchSeconds;
            target.localScale = baseScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.18f);
            yield return null;
        }
        if (target != null)
            target.localScale = baseScale;
        _punches.Remove(target);
    }
}
