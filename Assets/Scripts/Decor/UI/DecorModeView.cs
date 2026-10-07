using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 모드 화면: 위쪽 "꾸미기 모드" + [완료], 안내 한 줄, 들고 있는 물건 위에 뜨는 [회전] [확인] [빼기] 버튼.
/// 버튼은 클릭을 알리기만 한다.
/// </summary>
public class DecorModeView : MonoBehaviour
{
    [Header("위쪽")]
    [SerializeField] private Button _doneButton;

    [Header("안내")]
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private Color _hintColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _warningColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    [Tooltip("경고 안내를 흔드는 폭 (픽셀)")]
    [SerializeField] private float _shakeStrength = 12f;

    [Header("물건 위 버튼")]
    [Tooltip("버튼 묶음 (들고 있는 물건 위를 따라다님)")]
    [SerializeField] private RectTransform _actions;
    [SerializeField] private Button _rotateButton;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _removeButton;

    public event Action OnDoneClicked;
    public event Action OnRotateClicked;
    public event Action OnConfirmClicked;
    public event Action OnRemoveClicked;

    public RectTransform Actions => _actions;
    public RectTransform ConfirmButton => (RectTransform)_confirmButton.transform;

    private Vector2 _hintBasePosition;
    private Coroutine _shake;

    private void Awake()
    {
        _doneButton.onClick.AddListener(() => OnDoneClicked?.Invoke());
        _rotateButton.onClick.AddListener(() => OnRotateClicked?.Invoke());
        _confirmButton.onClick.AddListener(() => OnConfirmClicked?.Invoke());
        _removeButton.onClick.AddListener(() => OnRemoveClicked?.Invoke());
        _hintBasePosition = ((RectTransform)_hintText.transform.parent).anchoredPosition;
    }

    private void OnDisable()
    {
        if (_shake != null)
        {
            StopCoroutine(_shake);
            _shake = null;
            ((RectTransform)_hintText.transform.parent).anchoredPosition = _hintBasePosition;
        }
    }

    public void ShowHint(string message, bool warning = false)
    {
        _hintText.text = message;
        _hintText.color = warning ? _warningColor : _hintColor;

        if (warning && isActiveAndEnabled)
        {
            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeHint());
        }
    }

    /// <summary>물건 위 버튼 보이기 (canRotate가 false면 회전 버튼, canRemove가 false면 빼기 버튼 숨김 — 놓인 건물은 치울 수 없음)</summary>
    public void ShowActions(bool canRotate, bool canRemove = true)
    {
        _actions.gameObject.SetActive(true);
        _rotateButton.gameObject.SetActive(canRotate);
        _removeButton.gameObject.SetActive(canRemove);
    }

    public void HideActions() => _actions.gameObject.SetActive(false);

    private IEnumerator ShakeHint()
    {
        var box = (RectTransform)_hintText.transform.parent;
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float damping = 1f - t / duration;
            box.anchoredPosition = _hintBasePosition + Vector2.right * Mathf.Sin(t * 60f) * _shakeStrength * damping;
            yield return null;
        }
        box.anchoredPosition = _hintBasePosition;
        _shake = null;
    }
}
