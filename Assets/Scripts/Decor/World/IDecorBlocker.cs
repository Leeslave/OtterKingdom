using UnityEngine;

/// <summary>
/// 꾸미기 물건·건물을 놓지 못하게 비워 둘 월드 영역을 알려 주는 것 (채집 나무·바위, 해달 모임 자리 등).
/// DecorBlockArea와 같지만 씬에 따로 상자를 두지 않고 자기 모양(탭 영역 등)으로 정한다.
/// DecorBoardView가 씬이 열릴 때 이 영역에 걸친 칸을 막는다.
/// </summary>
public interface IDecorBlocker
{
    /// <returns>막을 영역이 있으면 true (월드 좌표 직사각형)</returns>
    bool TryGetDecorBlock(out Rect worldRect);
}
