using UnityEngine;

/// <summary>
/// 광산 미로 화면 그림: 갱도 4장(앞만 · 앞+왼쪽 · 앞+오른쪽 · 앞+양옆, 사용자 그림) + 앞이 막힌 벽 덧그림,
/// 보물 상자(닫힘 · 열림), 아래층 계단. 바위 · 효과 · UI 부품은 광산 개편 그림(MineReworkArt)을 같이 쓴다.
/// 만들기: Tools/Mine Maze/Build Test Scene (임시 그림: python Tools/UIGen/mine_maze_art.py)
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Maze/Art", fileName = "MineMazeArt")]
public class MineMazeArt : ScriptableObject
{
    [Header("갱도 (지금 칸에서 보이는 모습)")]
    [Tooltip("앞으로만 이어짐")]
    [SerializeField] private Sprite _viewForward;
    [Tooltip("앞 + 왼쪽 갈림길")]
    [SerializeField] private Sprite _viewLeft;
    [Tooltip("앞 + 오른쪽 갈림길")]
    [SerializeField] private Sprite _viewRight;
    [Tooltip("앞 + 양쪽 갈림길")]
    [SerializeField] private Sprite _viewBoth;
    [Tooltip("앞이 막힘 (갱도 그림 위에 겹침)")]
    [SerializeField] private Sprite _wall;

    [Header("물건")]
    [SerializeField] private Sprite _chestClosed;
    [SerializeField] private Sprite _chestOpen;
    [SerializeField] private Sprite _stairs;

    [Header("지도 해달 (임시: 다른 해달 얼굴)")]
    [SerializeField] private Sprite _mapOtter;

    /// <summary>양옆이 열렸는지에 맞는 갱도 그림 (앞이 막혔으면 Wall을 위에 겹침)</summary>
    public Sprite View(bool left, bool right) => left && right ? _viewBoth : left ? _viewLeft : right ? _viewRight : _viewForward;

    public Sprite Wall => _wall;
    public Sprite ChestClosed => _chestClosed;
    public Sprite ChestOpen => _chestOpen;
    public Sprite Stairs => _stairs;
    public Sprite MapOtter => _mapOtter;
}
