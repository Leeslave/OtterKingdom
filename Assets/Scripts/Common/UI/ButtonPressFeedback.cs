using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 손맛: 누르면 살짝 쏙 들어가며 "톡" 소리가 나고, 놓으면 통 튀어나온다. ButtonFeedbackInstaller가 모든 버튼에 붙인다.
/// 화면을 덮는 배경 버튼(팝업 뒤 딤)은 눌러도 움직이지 않는다. 일시정지 중에도 움직이게 unscaled 시간.
/// </summary>
[DisallowMultipleComponent]
public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float PressedScale = 0.92f;
    private const float PressSeconds = 0.06f;
    private const float ReleaseSeconds = 0.3f;
    // 놓을 때 이만큼 커졌다가(통) 제 크기로
    private const float PopScale = 1.06f;
    private const float PopPart = 0.4f;

    private Button _button;
    private RectTransform _rect;
    private Vector3 _baseScale;
    private float _from = 1f;
    private float _to = 1f;
    private float _time;
    private float _duration;
    private bool _bounce;
    private bool _pressed;
    private bool _animating;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rect = (RectTransform)transform;
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        if (_animating || _pressed)
            transform.localScale = _baseScale;
        _animating = false;
        _pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button == null || !_button.IsInteractable() || IsBackdrop())
            return;
        if (!_animating)
            _baseScale = transform.localScale;
        _pressed = true;
        Play(PressedScale, PressSeconds, false);
        AudioManager.Play(SfxKind.Click);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        if (!_pressed)
            return;
        _pressed = false;
        Play(1f, ReleaseSeconds, true);
    }

    private void Play(float to, float duration, bool bounce)
    {
        _from = _animating ? CurrentFactor() : 1f;
        _to = to;
        _duration = duration;
        _bounce = bounce;
        _time = 0f;
        _animating = true;
    }

    private float CurrentFactor() => _baseScale.x != 0f ? transform.localScale.x / _baseScale.x : 1f;

    private void Update()
    {
        if (!_animating)
            return;
        _time += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_time / _duration);
        float factor;
        if (!_bounce)
            factor = Mathf.Lerp(_from, _to, 1f - (1f - k) * (1f - k));
        else if (k < PopPart)
            factor = Mathf.Lerp(_from, PopScale, 1f - Mathf.Pow(1f - k / PopPart, 2f));
        else
            factor = Mathf.Lerp(PopScale, _to, Mathf.SmoothStep(0f, 1f, (k - PopPart) / (1f - PopPart)));
        transform.localScale = _baseScale * factor;
        if (k >= 1f && !_pressed)
        {
            transform.localScale = _baseScale;
            _animating = false;
        }
    }

    // 화면 대부분을 덮는 버튼 (팝업 뒤 딤): 움직이면 가장자리가 보여 어색함
    private bool IsBackdrop()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return false;
        var screen = ((RectTransform)canvas.rootCanvas.transform).rect;
        var rect = _rect.rect;
        return rect.width >= screen.width * 0.6f && rect.height >= screen.height * 0.4f;
    }
}
