using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 축하 팝업(레벨업·집 완성)의 은은한 빛 연출. 날아다니는 조각 없이 빛과 반짝임만:
/// - 패널 위 가장자리(리본) 뒤: 해 뜨듯 패널 위로 퍼지는 빛줄기(천천히 돎) + 따뜻한 빛 번짐.
///   밝은 바닥에서도 빛이 보이게 그 둘레를 살짝 어둡게 깖
/// - 패널 둘레: 작은 별빛이 차례로 반짝였다 사라짐
/// - 패널이 자리 잡으면 주인공 글자(레벨·집 이름)가 한 번 통 튐
/// 패널의 투명도를 따라 나타나고 사라진다. 그림은 Resources/UI/Celebrate (Tools/UIGen/celebrate_fx.py).
/// 처음 쓸 때 팝업에 붙어 빛은 패널 바로 뒤, 별빛은 패널 바로 앞에 만들어진다.
/// </summary>
public class PopupGlow : MonoBehaviour
{
    private const string Folder = "UI/Celebrate/";
    private const float IntroSeconds = 0.6f;
    private const float RayDegreesPerSecond = 7f;
    // 크기는 패널 폭 비율
    private const float ShadeSize = 2.4f;
    private const float GlowSize = 1.1f;
    private const float RaySize = 1.7f;
    private const float PunchDelay = 0.3f;
    private const float PunchSeconds = 0.35f;
    private const float PunchScale = 0.18f;
    private const float TwinkleSeconds = 0.9f;
    private const float TwinkleJitter = 24f;

    // 별빛 자리 (패널 크기 비율, 가운데 0) · 크기(px) · 처음 나타나는 때(초). 빛이 퍼지는 위쪽에 많이
    private static readonly (Vector2 spot, float size, float delay)[] Twinkles =
    {
        (new Vector2(-0.36f, 0.56f), 78f, 0.25f),
        (new Vector2(0.24f, 0.78f), 52f, 0.45f),
        (new Vector2(0.45f, 0.5f), 60f, 0.7f),
        (new Vector2(-0.16f, 0.86f), 44f, 0.9f),
        (new Vector2(-0.53f, -0.2f), 50f, 1.15f),
        (new Vector2(0.54f, 0.02f), 42f, 1.35f),
    };

    // 선형 색 공간이라 반투명 검정 딤이 생각보다 옅음 → 빛 둘레를 한 번 더 어둡게 해야 빛이 보임
    private static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color GlowColor = new Color(1f, 0.96f, 0.82f, 1f);
    private static readonly Color RayColor = new Color(1f, 0.93f, 0.7f, 0.8f);
    private static readonly Color TwinkleColor = new Color(1f, 0.98f, 0.88f, 1f);

    private class Twinkle
    {
        public Image Image;
        public Vector2 Spot;
        public float Size;
        public float Delay;
        public float Rest;
        public int Cycle = -1;
        public Vector2 Offset;
    }

    private UIPopupAnimator _animator;
    private RectTransform _back;
    private RectTransform _front;
    private Image _shade;
    private Image _glow;
    private Image _rays;
    private readonly List<Twinkle> _twinkles = new List<Twinkle>();
    private RectTransform _focus;
    private Vector3 _focusScale;
    private float _time;

    /// <summary>팝업을 띄운 직후 부른다. focus는 한 번 통 튈 글자 (없으면 null)</summary>
    public static void Play(UIPopupAnimator animator, RectTransform focus)
    {
        var glow = animator.GetComponent<PopupGlow>();
        if (glow == null)
        {
            glow = animator.gameObject.AddComponent<PopupGlow>();
            if (!glow.Build(animator))
            {
                Destroy(glow);
                return;
            }
        }
        glow.Begin(focus);
    }

    private bool Build(UIPopupAnimator animator)
    {
        var glowSprite = Resources.Load<Sprite>(Folder + "Glow");
        var raySprite = Resources.Load<Sprite>(Folder + "Rays");
        var twinkleSprite = Resources.Load<Sprite>(Folder + "Twinkle");
        if (glowSprite == null || raySprite == null || twinkleSprite == null)
            return false;

        _animator = animator;
        var panel = animator.Panel;
        _back = CreateLayer("CelebrateGlow", panel.parent);
        _back.SetSiblingIndex(panel.GetSiblingIndex());
        _front = CreateLayer("CelebrateTwinkles", panel.parent);
        _front.SetSiblingIndex(panel.GetSiblingIndex() + 1);

        _shade = CreateImage("Shade", _back, glowSprite, ShadeColor);
        _rays = CreateImage("Rays", _back, raySprite, RayColor);
        _glow = CreateImage("Glow", _back, glowSprite, GlowColor);
        foreach (var (spot, size, delay) in Twinkles)
        {
            _twinkles.Add(new Twinkle
            {
                Image = CreateImage("Twinkle", _front, twinkleSprite, TwinkleColor),
                Spot = spot,
                Size = size,
                Delay = delay,
            });
        }
        return true;
    }

    private static RectTransform CreateLayer(string name, Transform parent)
    {
        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 0.5f);
        layer.sizeDelta = Vector2.zero;
        return layer;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void Begin(RectTransform focus)
    {
        RestoreFocus();
        _focus = focus;
        if (_focus != null)
            _focusScale = _focus.localScale;
        _time = 0f;
        foreach (var twinkle in _twinkles)
            twinkle.Cycle = -1;
        enabled = true;
        LateUpdate();
    }

    private void OnDisable() => RestoreFocus();

    private void RestoreFocus()
    {
        if (_focus != null)
            _focus.localScale = _focusScale;
        _focus = null;
    }

    private void LateUpdate()
    {
        if (_animator == null)
            return;
        _time += Time.unscaledDeltaTime;
        var panel = _animator.Panel;
        float alpha = _animator.PanelGroup.alpha;
        float intro = 1f - Mathf.Pow(1f - Mathf.Clamp01(_time / IntroSeconds), 3f);

        // 빛은 패널 위 가장자리 가운데, 별빛은 패널 가운데 기준 (패널이 튀어나오는 크기는 따라가지 않고 따로 피어남)
        var rect = panel.rect;
        _back.position = panel.TransformPoint(new Vector2(rect.center.x, rect.yMax));
        _front.position = panel.TransformPoint(rect.center);
        float width = rect.width;
        _shade.rectTransform.sizeDelta = Vector2.one * (width * ShadeSize);
        _glow.rectTransform.sizeDelta = Vector2.one * (width * GlowSize * Mathf.Lerp(0.8f, 1f, intro));
        _rays.rectTransform.sizeDelta = Vector2.one * (width * RaySize * Mathf.Lerp(0.7f, 1f, intro));
        _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_time * RayDegreesPerSecond);
        SetAlpha(_shade, ShadeColor.a * alpha * intro);
        SetAlpha(_glow, GlowColor.a * alpha * intro);
        SetAlpha(_rays, RayColor.a * alpha * intro);

        foreach (var twinkle in _twinkles)
            AnimateTwinkle(twinkle, rect.size, alpha);

        AnimateFocus();
    }

    // 반짝(0.9초) → 잠깐 쉼 → 근처 다른 자리에서 다시 반짝
    private void AnimateTwinkle(Twinkle twinkle, Vector2 panelSize, float alpha)
    {
        float local = _time - twinkle.Delay;
        if (local < 0f)
        {
            SetAlpha(twinkle.Image, 0f);
            return;
        }
        if (twinkle.Cycle < 0)
            twinkle.Rest = Random.Range(0.5f, 1.3f);
        float period = TwinkleSeconds + twinkle.Rest;
        int cycle = Mathf.FloorToInt(local / period);
        if (cycle != twinkle.Cycle)
        {
            twinkle.Cycle = cycle;
            twinkle.Offset = cycle == 0 ? Vector2.zero : Random.insideUnitCircle * TwinkleJitter;
        }

        float k = (local - cycle * period) / TwinkleSeconds;
        float shine = k < 1f ? Mathf.Pow(Mathf.Sin(Mathf.PI * k), 1.5f) : 0f;
        var rect = twinkle.Image.rectTransform;
        rect.anchoredPosition = Vector2.Scale(twinkle.Spot, panelSize) + twinkle.Offset;
        rect.sizeDelta = Vector2.one * (twinkle.Size * shine);
        rect.localRotation = Quaternion.Euler(0f, 0f, 40f * Mathf.Clamp01(k));
        SetAlpha(twinkle.Image, shine * alpha);
    }

    // 패널이 자리 잡을 즈음 주인공 글자가 1 → 1.18 → 1
    private void AnimateFocus()
    {
        if (_focus == null)
            return;
        float k = (_time - PunchDelay) / PunchSeconds;
        if (k <= 0f)
            return;
        if (k >= 1f)
        {
            RestoreFocus();
            return;
        }
        _focus.localScale = _focusScale * (1f + PunchScale * Mathf.Sin(Mathf.PI * k));
    }

    private static void SetAlpha(Image image, float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
