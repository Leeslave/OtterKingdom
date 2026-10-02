using UnityEngine;

/// <summary>
/// 광장의 해달 게시판. 탭하면 게시판 팝업이 열린다.
/// 할 일이 있으면(처음이거나 시작할 수 있는 부탁) 머리 위 "!" 말풍선이 통통 튄다.
/// </summary>
public class SettlementBoardPropView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("탭을 받을 영역 (게시판)")]
    [SerializeField] private Collider2D _tapArea;

    [Tooltip("\"!\" 말풍선")]
    [SerializeField] private SpriteRenderer _attention;

    [Header("움직임")]
    [Tooltip("말풍선이 흔들리는 폭 (월드 단위)")]
    [SerializeField] private float _bob = 0.08f;

    [Tooltip("초당 흔들림 횟수")]
    [SerializeField] private float _bobPerSecond = 1.2f;

    private Vector3 _attentionBase;

    private void Awake()
    {
        _attentionBase = _attention.transform.localPosition;
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        bool attention = manager != null && manager.IsLoaded
            && (!manager.Settlement.BoardVisited || manager.HasActionableRequest);
        if (_attention.gameObject.activeSelf != attention)
            _attention.gameObject.SetActive(attention);
        if (attention)
        {
            float t = Time.time * _bobPerSecond * Mathf.PI * 2f;
            _attention.transform.localPosition = _attentionBase + Vector3.up * (Mathf.Abs(Mathf.Sin(t)) * _bob);
        }

        if (manager != null && manager.IsLoaded && PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
            manager.RequestBoard(manager.Settlement.BoardVisited && manager.HasActionableRequest);
    }
}
