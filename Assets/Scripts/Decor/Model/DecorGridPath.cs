using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 위 상하좌우 길찾기 (순수 C#). 밭처럼 길찾기 영역이 없는 장소에서 놓인 물건을 돌아가는 길을 만든다.
/// 꺾는 횟수가 적은 길을 고르고(꺾을 때마다 비용 추가), 꺾이는 칸만 돌려준다.
/// </summary>
public static class DecorGridPath
{
    private const int StepCost = 10;
    private const int TurnCost = 5; // 반 칸만큼: 같은 거리면 덜 꺾는 길

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    /// <summary>
    /// start → goal 길을 찾아 꺾이는 칸(시작 제외, 도착 포함)을 corners에 넣는다.
    /// start와 goal 칸은 지나갈 수 없어도 출발·도착은 허용한다 (해달이 이미 서 있거나 목표가 물건 옆일 때).
    /// </summary>
    /// <returns>길이 있으면 true</returns>
    public static bool FindPath(Vector2Int size, Func<Vector2Int, bool> passable, Vector2Int start, Vector2Int goal, List<Vector2Int> corners)
    {
        if (passable == null) throw new ArgumentNullException(nameof(passable));
        if (corners == null) throw new ArgumentNullException(nameof(corners));

        corners.Clear();
        if (!InBounds(size, start) || !InBounds(size, goal))
            return false;
        if (start == goal)
            return true;

        // 상태 = (칸, 들어온 방향). 격자가 작아서(수백 칸) 단순한 다익스트라로 충분
        int cellCount = size.x * size.y;
        var cost = new int[cellCount * 4];
        var from = new int[cellCount * 4];
        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = int.MaxValue;
            from[i] = -1;
        }

        var open = new SortedSet<(int cost, int state)>();
        int startIndex = Index(size, start);
        for (int d = 0; d < 4; d++)
        {
            int state = startIndex * 4 + d;
            cost[state] = 0;
            open.Add((0, state));
        }

        int goalIndex = Index(size, goal);
        int goalState = -1;
        while (open.Count > 0)
        {
            var current = open.Min;
            open.Remove(current);
            if (current.cost > cost[current.state])
                continue;

            int cell = current.state / 4;
            int dir = current.state % 4;
            if (cell == goalIndex)
            {
                goalState = current.state;
                break;
            }

            var position = new Vector2Int(cell % size.x, cell / size.x);
            for (int nd = 0; nd < 4; nd++)
            {
                var next = position + Directions[nd];
                if (!InBounds(size, next))
                    continue;
                if (next != goal && !passable(next))
                    continue;

                int nextState = Index(size, next) * 4 + nd;
                bool isFirstStep = cell == startIndex;
                int nextCost = current.cost + StepCost + (!isFirstStep && nd != dir ? TurnCost : 0);
                if (nextCost >= cost[nextState])
                    continue;

                open.Remove((cost[nextState], nextState));
                cost[nextState] = nextCost;
                from[nextState] = current.state;
                open.Add((nextCost, nextState));
            }
        }

        if (goalState < 0)
            return false;

        // 거꾸로 따라가며 방향이 바뀌는 칸만 남김
        var reversed = new List<Vector2Int>();
        int s = goalState;
        reversed.Add(goal);
        while (from[s] >= 0 && from[s] / 4 != startIndex)
        {
            int prev = from[s];
            if (prev % 4 != s % 4)
                reversed.Add(new Vector2Int(prev / 4 % size.x, prev / 4 / size.x));
            s = prev;
        }

        for (int i = reversed.Count - 1; i >= 0; i--)
            corners.Add(reversed[i]);
        return true;
    }

    private static bool InBounds(Vector2Int size, Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < size.x && cell.y < size.y;

    private static int Index(Vector2Int size, Vector2Int cell) => cell.y * size.x + cell.x;
}
