using UnityEngine;

/// <summary>
/// 켜져 있는 동안 천천히 커졌다 작아진다 (보러 오게 만드는 표시용: N 뱃지, [이야기 보기] 버튼).
/// 게임이 멈춰도(Time.timeScale = 0) 움직이도록 unscaled 시간을 쓴다.
/// </summary>
public class PulseAnimator : MonoBehaviour
{
    [Tooltip("가장 커질 때의 배율 (1 = 그대로)")]
    [SerializeField] private float _maxScale = 1.08f;

    [Tooltip("한 번 커졌다 작아지는 시간(초)")]
    [SerializeField] private float _period = 1.2f;

    private Vector3 _baseScale = Vector3.one;

    private void OnEnable()
    {
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale;
    }

    private void Update()
    {
        // 0 → 1 → 0 을 부드럽게 (사인 곡선의 위쪽 절반만 쓰면 튀는 느낌이 적음)
        float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _period) + 1f) * 0.5f;
        transform.localScale = _baseScale * Mathf.Lerp(1f, _maxScale, t);
    }
}
