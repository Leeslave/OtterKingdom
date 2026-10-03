using UnityEngine;

/// <summary>
/// 새 조작을 처음 한 번 알려 주는 말풍선 (예: 바위 위 "톡톡 쳐서 돌을 캐요!").
/// 그 조작을 한 번 하면(MarkDone) 정착 세이브에 표시가 남아 다시 나오지 않는다.
/// 언제 보여도 되는지는 주인(바위·나무·장애물)이 Allowed로 정한다.
/// </summary>
public class TapHintView : MonoBehaviour
{
    [Header("식별")]
    [Tooltip("세이브에 남는 표시 (예: hint_rock). 같은 표시를 쓰는 힌트는 하나만 해도 함께 사라짐")]
    [SerializeField] private string _flag;

    [Header("구성 요소")]
    [Tooltip("말풍선 (글자 포함)")]
    [SerializeField] private GameObject _bubble;

    [Tooltip("따라다닐 대상 (비우면 제자리, 예: 약점 반짝이)")]
    [SerializeField] private Transform _follow;

    [Header("연출")]
    [Tooltip("대상 기준 말풍선 위치")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("위아래로 흔들리는 높이")]
    [SerializeField] private float _bobHeight = 0.1f;

    /// <summary>지금 보여도 되는지 (바위가 있을 때, 장애물을 칠 수 있을 때 등)</summary>
    public bool Allowed { get; set; } = true;

    private Vector3 _basePosition;

    private void Awake()
    {
        _basePosition = _bubble.transform.localPosition;
        _bubble.SetActive(false);
    }

    private void LateUpdate()
    {
        var manager = SettlementManager.Instance;
        bool show = Allowed && manager != null && manager.IsLoaded && !manager.HasSeen(_flag)
            && (_follow == null || _follow.gameObject.activeInHierarchy);
        if (_bubble.activeSelf != show)
            _bubble.SetActive(show);
        if (!show)
            return;

        var bob = Vector3.up * (Mathf.Sin(Time.time * 4f) * _bobHeight);
        if (_follow != null)
            _bubble.transform.position = _follow.position + _offset + bob;
        else
            _bubble.transform.localPosition = _basePosition + bob;
    }

    /// <summary>알려 준 조작을 해 봤음 → 다시 안 보임</summary>
    public void MarkDone()
    {
        var manager = SettlementManager.Instance;
        if (manager != null && manager.IsLoaded)
            manager.MarkSeen(_flag);
    }
}
