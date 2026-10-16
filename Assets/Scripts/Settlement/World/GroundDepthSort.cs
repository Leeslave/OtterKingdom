using UnityEngine;

/// <summary>
/// 광장 밖 장소(광산)에서 발밑 높이로 앞뒤를 정한다: 화면 아래쪽에 선 것이 앞에 그려져서
/// 해달이 나무·돌 뒤로 지나가면 가려지고, 앞으로 나오면 그 위에 그려진다.
/// 광장의 PlazaDepth와 같은 비율이지만, 그 장소의 배경·바닥 표시(입구 동그라미·그림자) 위 구간에서 정렬한다.
/// 움직이는 것(광부 해달)은 매 프레임, 가만히 있는 것(나무·돌)은 켜질 때 한 번. 그림의 피벗이 발밑이어야 한다.
/// </summary>
public class GroundDepthSort : MonoBehaviour
{
    // 바닥 표시(0~9)보다 위, 말풍선·안내(30000~)보다 아래
    private const int MinOrder = 10;
    private const int MaxOrder = 29999;

    [Header("정렬")]
    [Tooltip("발밑 높이 0일 때의 순서 (높이 1마다 100씩 바뀜). 이 장소에서 걷는 범위 안에서 MinOrder 위에 머물 만큼")]
    [SerializeField] private int _baseOrder = 1000;

    [Tooltip("움직이는가 (해달). 끄면 켜질 때 한 번만 정함")]
    [SerializeField] private bool _moves;

    [Header("구성 요소")]
    [Tooltip("정렬할 그림")]
    [SerializeField] private SpriteRenderer _renderer;

    /// <summary>발밑 높이 groundY에서의 순서</summary>
    public static int OrderFor(int baseOrder, float groundY)
    {
        int order = baseOrder - Mathf.RoundToInt(groundY * PlazaDepth.OrdersPerUnit);
        return Mathf.Clamp(order, MinOrder, MaxOrder);
    }

    private void OnEnable() => Apply();

    private void LateUpdate()
    {
        if (_moves)
            Apply();
    }

    private void Apply() => _renderer.sortingOrder = OrderFor(_baseOrder, transform.position.y);
}
