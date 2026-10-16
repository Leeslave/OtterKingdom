using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장의 해달 게시판. 탭하면 게시판 팝업이 열린다.
/// 할 일이 있으면(처음이거나 시작할 수 있는 부탁) 머리 위 "!" 말풍선이 통통 튄다.
/// 게시판은 같은 자리에서 마을회관 → 마을 길드로 커진다: 커진 건물(켜져 있는 것)을 눌러도 게시판이 열리고, "!"는 그 지붕 위에 뜬다
/// </summary>
public class SettlementBoardPropView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("탭을 받을 영역 (게시판)")]
    [SerializeField] private Collider2D _tapArea;

    [Tooltip("\"!\" 말풍선")]
    [SerializeField] private SpriteRenderer _attention;

    [Tooltip("게시판이 커진 건물의 탭 영역 (마을회관 · 마을 길드). 켜져 있는 것을 눌러도 게시판이 열림")]
    [SerializeField] private List<Collider2D> _buildingTapAreas = new List<Collider2D>();

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
        var building = ActiveBuilding();
        if (attention)
        {
            float t = Time.time * _bobPerSecond * Mathf.PI * 2f;
            var bob = Vector3.up * (Mathf.Abs(Mathf.Sin(t)) * _bob);
            if (building != null)
            {
                // 커진 건물의 지붕 위 오른쪽
                var bounds = building.bounds;
                _attention.transform.position = new Vector3(bounds.center.x + bounds.extents.x * 0.35f, bounds.max.y + 0.4f, _attention.transform.position.z) + bob;
            }
            else
                _attention.transform.localPosition = _attentionBase + bob;
        }

        if (manager != null && manager.IsLoaded && PlazaTapInput.TryGetTap(out Vector2 world)
            && (_tapArea.OverlapPoint(world) || (building != null && building.OverlapPoint(world))))
            manager.RequestBoard(manager.Settlement.BoardVisited && manager.HasActionableRequest);
    }

    // 지금 보이는 커진 건물 (아직 게시판이면 null)
    private Collider2D ActiveBuilding()
    {
        foreach (var area in _buildingTapAreas)
        {
            if (area != null && area.gameObject.activeInHierarchy)
                return area;
        }
        return null;
    }
}
