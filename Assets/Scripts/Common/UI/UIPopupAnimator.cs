using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면/팝업 열고 닫기 연출. 배경(딤)은 페이드, 패널은 "뽀잉" 튀어나오기(EaseOutBack).
/// 이 컴포넌트가 붙은 오브젝트가 화면 루트이며, 닫히면 루트가 꺼진다.
/// </summary>
public class UIPopupAnimator : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("뒤에 깔리는 반투명 검정 배경")]
    [SerializeField] private CanvasGroup _dim;
    [Tooltip("튀어나올 패널")]
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _panelGroup;
    [Tooltip("배경을 누르면 닫기 (비워두면 배경 클릭 무시)")]
    [SerializeField] private Button _dimButton;

    [Header("열기")]
    [SerializeField] private float _showDuration = 0.35f;
    [Tooltip("시작 크기 (작을수록 크게 튀어나옴)")]
    [SerializeField] private float _startScale = 0.6f;
    [Tooltip("목표 크기를 넘었다 돌아오는 정도 (클수록 더 뽀잉)")]
    [SerializeField] private float _overshoot = 1.7f;

    [Header("닫기")]
    [SerializeField] private float _hideDuration = 0.15f;
    [SerializeField] private float _endScale = 0.85f;

    [Header("흔들기 (실패 피드백)")]
    [SerializeField] private float _shakeDuration = 0.3f;
    [SerializeField] private float _shakeStrength = 18f;

    public bool IsOpen { get; private set; }

    /// <summary>튀어나오는 패널 (축하 빛처럼 패널에 맞춰 그리는 연출용)</summary>
    public RectTransform Panel => _panel;

    /// <summary>패널의 투명도 (열고 닫을 때 바뀜)</summary>
    public CanvasGroup PanelGroup => _panelGroup;

    /// <summary>배경을 눌러 닫을 수 있는 창인지 (Android 뒤로 가기도 같은 기준으로 닫음)</summary>
    public bool IsDismissable => _dimButton != null;

    /// <summary>닫기 연출이 끝나 화면이 꺼진 뒤 (배경 클릭으로 닫힌 경우 포함)</summary>
    public event Action OnHidden;

    private Coroutine _routine;
    private Vector2 _panelBasePosition;

    private void Awake()
    {
        // Awake는 켜져 있을 때만 불림 → 씬에서 켜진 채로 시작한 화면은 이미 열린 상태
        IsOpen = true;
        _panelBasePosition = _panel.anchoredPosition;

        if (_dimButton != null)
            _dimButton.onClick.AddListener(() => Hide());
    }

    public void Show()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        Play(ShowRoutine());
    }

    public void Hide(Action onHidden = null)
    {
        if (!IsOpen) return;

        IsOpen = false;
        Play(HideRoutine(onHidden));
    }

    public void Shake()
    {
        if (IsOpen)
            Play(ShakeRoutine());
    }

    private void Play(IEnumerator routine)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    // 연출 중 연타로 버튼이 두 번 눌리지 않도록 패널 입력을 막아둠
    private void SetInteractable(bool value)
    {
        _panelGroup.interactable = value;
        _panelGroup.blocksRaycasts = value;
    }

    private IEnumerator ShowRoutine()
    {
        SetInteractable(false);
        _panel.anchoredPosition = _panelBasePosition;

        // 일시정지(Time.timeScale = 0) 중에도 UI는 움직이도록 unscaled 시간 사용
        for (float t = 0f; t < _showDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / _showDuration;
            _dim.alpha = p;
            _panelGroup.alpha = Mathf.Clamp01(p * 2.5f); // 초반에 빨리 나타남
            _panel.localScale = Vector3.one * Mathf.LerpUnclamped(_startScale, 1f, EaseOutBack(p));
            yield return null;
        }

        _dim.alpha = 1f;
        _panelGroup.alpha = 1f;
        _panel.localScale = Vector3.one;
        SetInteractable(true);
        _routine = null;
    }

    private IEnumerator HideRoutine(Action onHidden)
    {
        SetInteractable(false);

        float startDim = _dim.alpha;
        float startAlpha = _panelGroup.alpha;
        Vector3 startScale = _panel.localScale;

        for (float t = 0f; t < _hideDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / _hideDuration;
            _dim.alpha = Mathf.Lerp(startDim, 0f, p);
            _panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
            _panel.localScale = Vector3.Lerp(startScale, Vector3.one * _endScale, p);
            yield return null;
        }

        _routine = null;
        gameObject.SetActive(false);
        onHidden?.Invoke();
        OnHidden?.Invoke();
    }

    private IEnumerator ShakeRoutine()
    {
        for (float t = 0f; t < _shakeDuration; t += Time.unscaledDeltaTime)
        {
            float damper = 1f - t / _shakeDuration;
            float offset = Mathf.Sin(t * 60f) * _shakeStrength * damper;
            _panel.anchoredPosition = _panelBasePosition + new Vector2(offset, 0f);
            yield return null;
        }

        _panel.anchoredPosition = _panelBasePosition;
        _routine = null;
    }

    // 0→1 진행하면서 1을 살짝 넘었다가 돌아오는 곡선 ("뽀잉")
    private float EaseOutBack(float x)
    {
        float c1 = _overshoot;
        float c3 = c1 + 1f;
        float u = x - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
