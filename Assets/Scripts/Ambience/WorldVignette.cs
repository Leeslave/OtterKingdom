using UnityEngine;

/// <summary>
/// 화면 가장자리를 살짝 어둡게 해 시선을 가운데로 모은다 (그림만, UI는 그대로). 카메라를 따라 화면을 덮는다.
/// SettlementSetup.Life가 장소마다 붙인다.
/// </summary>
public class WorldVignette : MonoBehaviour
{
    [SerializeField] private Sprite _sprite;

    [Tooltip("조명을 받지 않는 재질 (밤에 더 어두워지지 않게)")]
    [SerializeField] private Material _material;

    [Tooltip("가장자리 어둡기 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float _strength = 0.3f;

    [Tooltip("말풍선·안내(32000~) 아래, 다른 그림 위")]
    [SerializeField] private int _sortingOrder = 31999;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = _sprite;
        _renderer.sharedMaterial = _material;
        _renderer.sortingOrder = _sortingOrder;
        _renderer.color = new Color(0f, 0f, 0f, _strength);
    }

    private void LateUpdate() => CameraView.Cover(_renderer, 1.02f);
}
