using UnityEngine;

/// <summary>
/// 낮에 왼쪽 위에서 비스듬히 내려오는 햇살. 천천히 숨쉬듯 밝아졌다 어두워지고, 해가 지면 사라진다.
/// 카메라를 따라 화면을 덮는다. 조명을 받지 않는 재질. SettlementSetup.Life가 붙인다.
/// </summary>
public class SunShafts : MonoBehaviour
{
    [SerializeField] private Sprite _sprite;
    [SerializeField] private Material _material;

    [Tooltip("한낮 밝기 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _alpha = 0.22f;

    [Tooltip("숨쉬는 빠르기 (초당)")]
    [SerializeField] private float _pulseSpeed = 0.25f;

    [SerializeField] private int _sortingOrder = 31970;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = _sprite;
        _renderer.sharedMaterial = _material;
        _renderer.sortingOrder = _sortingOrder;
    }

    private void LateUpdate()
    {
        CameraView.Cover(_renderer, 1.1f);
        // 노을 무렵부터 흐려져 밤엔 없음, 아침에 다시
        float day = 1f - TimeOfDay.Night(TimeOfDay.CurrentHour);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f);
        var color = _renderer.color;
        color.a = _alpha * day * pulse;
        _renderer.color = color;
        _renderer.enabled = color.a > 0.005f;
    }
}
