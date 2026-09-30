using UnityEngine;

/// <summary>
/// 꾸미기 물건을 놓을 수 없는 월드 영역 (밭고랑, 낚시 자리 등). 가운데가 이 오브젝트의 위치인 직사각형.
/// DecorBoardView가 씬이 열릴 때 이 영역에 걸친 칸을 막는다. Scene 뷰에서 빨간 상자로 보인다.
/// </summary>
public class DecorBlockArea : MonoBehaviour
{
    [Tooltip("막을 영역 크기 (월드 단위, 가로 × 세로)")]
    [SerializeField] private Vector2 _size = Vector2.one;

    public Rect WorldRect
    {
        get
        {
            Vector2 center = transform.position;
            return new Rect(center - _size * 0.5f, _size);
        }
    }

    private void OnDrawGizmos()
    {
        var rect = WorldRect;
        Gizmos.color = new Color(0.9f, 0.25f, 0.25f, 0.25f);
        Gizmos.DrawCube(rect.center, rect.size);
        Gizmos.color = new Color(0.9f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawWireCube(rect.center, rect.size);
    }
}
