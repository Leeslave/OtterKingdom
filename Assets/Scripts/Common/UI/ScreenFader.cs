using System.Collections;
using UnityEngine;

/// <summary>
/// 화면 전체를 덮는 검은 막. 씬 전환 때 어두워졌다(FadeOut) 밝아진다(FadeIn).
/// 전역 UI의 가장 마지막 자식(맨 위)에 둔다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Header("연출")]
    [Tooltip("어두워지는 시간")]
    [SerializeField] private float _fadeOutDuration = 0.25f;
    [Tooltip("밝아지는 시간")]
    [SerializeField] private float _fadeInDuration = 0.3f;

    private CanvasGroup _group;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    /// <summary>화면을 검게 덮는다. 덮여 있는 동안은 입력도 막는다.</summary>
    public IEnumerator FadeOut()
    {
        _group.blocksRaycasts = true;
        yield return Fade(1f, _fadeOutDuration);
    }

    /// <summary>검은 막을 걷어낸다.</summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(0f, _fadeInDuration);
        _group.blocksRaycasts = false;
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = _group.alpha;

        // 씬 로딩 중 timeScale이 바뀌어도 연출은 진행되도록 unscaled 시간 사용
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            _group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        _group.alpha = target;
    }
}
