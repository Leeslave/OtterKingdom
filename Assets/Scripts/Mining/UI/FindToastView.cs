using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 아래쪽에 잠깐 떴다 사라지는 작은 알림 (예: [다이아몬드 아이콘] 광산 · 다이아몬드 +1).
/// 떠 있는 동안 새 알림이 오면 내용을 바꾸고 다시 기다린다. 받은 값만 그린다.
/// </summary>
public class FindToastView : MonoBehaviour
{
    private const float FadeSeconds = 0.2f;
    private const float Rise = 24f;

    [Header("구성 요소")]
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _body;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _text;

    [Header("시간")]
    [Tooltip("다 나타난 뒤 머무는 시간 (초)")]
    [SerializeField] private float _holdSeconds = 1.8f;

    private Vector2 _basePosition;
    private Coroutine _routine;

    private void Awake()
    {
        _basePosition = _body.anchoredPosition;
        _group.alpha = 0f;
        _body.gameObject.SetActive(false);
    }

    public void Show(Sprite icon, string text)
    {
        // HUD가 숨겨진 동안(꾸미기 모드 등)에는 알림을 건너뜀 (꺼진 오브젝트는 연출을 시작할 수 없음). 캔 것은 가방에 그대로 들어감
        if (!isActiveAndEnabled)
            return;

        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _text.text = text;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        _body.gameObject.SetActive(true);
        // 이미 떠 있으면 바로 머무르기부터
        for (float t = _group.alpha * FadeSeconds; t < FadeSeconds; t += Time.unscaledDeltaTime)
            yield return Apply(t / FadeSeconds);
        Apply(1f);

        yield return new WaitForSecondsRealtime(_holdSeconds);

        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            yield return Apply(1f - t / FadeSeconds);
        Apply(0f);
        _body.gameObject.SetActive(false);
        _routine = null;
    }

    private object Apply(float visible)
    {
        _group.alpha = visible;
        _body.anchoredPosition = _basePosition + Vector2.down * (Rise * (1f - visible));
        return null;
    }
}
