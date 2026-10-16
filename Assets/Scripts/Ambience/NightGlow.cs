using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 밤에 켜지는 불빛 하나 (집 창문, 가로등): 빛나는 그림(조명을 받지 않는 재질)과 주변을 비추는 점 조명.
/// 밤이 깊을수록(TimeOfDayLighting.Night) 밝아지고, 아주 살짝 일렁인다.
/// 따라갈 소품(집·가로등)이 아직 없으면(정착 진행 전) 꺼져 있다.
/// </summary>
public class NightGlow : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("창문·랜턴 유리만 따뜻하게 칠한 그림 (소품과 같은 자리·크기)")]
    [SerializeField] private SpriteRenderer _glow;

    [Tooltip("주변 바닥·벽을 비추는 점 조명")]
    [SerializeField] private Light2D _light;

    [Tooltip("이 소품이 보일 때만 켬 (DevelopmentGate로 숨겨진 집 등)")]
    [SerializeField] private GameObject _followActive;

    [Header("밝기")]
    [Tooltip("한밤의 점 조명 세기")]
    [SerializeField] private float _lightIntensity = 1.4f;

    [Tooltip("일렁임 정도 (0이면 없음)")]
    [SerializeField] private float _flicker = 0.06f;

    private float _phase;

    private void Awake()
    {
        _phase = Random.value * 10f;
    }

    private void LateUpdate()
    {
        bool present = _followActive == null || _followActive.activeInHierarchy;
        float night = present ? TimeOfDayLighting.Night : 0f;
        float flicker = 1f + (Mathf.PerlinNoise(Time.time * 2.2f, _phase) - 0.5f) * 2f * _flicker;

        var color = _glow.color;
        color.a = Mathf.Clamp01(night * flicker);
        _glow.color = color;
        _glow.enabled = color.a > 0.01f;

        _light.intensity = _lightIntensity * night * flicker;
        _light.enabled = night > 0.01f;
    }
}
