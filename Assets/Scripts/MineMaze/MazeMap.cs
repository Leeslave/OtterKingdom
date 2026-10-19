using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>미로 칸 종류</summary>
public enum MazeCell { Wall, Floor, Start, Stairs, Rock, Chest }

/// <summary>지도 위 방향 (북 = 지도 위쪽)</summary>
public enum MazeDir { North, East, South, West }

/// <summary>지금 보는 쪽 기준으로 갈 방향 (앞 · 오른쪽 · 뒤 · 왼쪽)</summary>
public enum MazeTurn { Forward, Right, Back, Left }

public static class MazeDirs
{
    /// <summary>그 방향으로 한 칸 (지도 y는 아래로 커짐)</summary>
    public static Vector2Int Step(MazeDir dir)
    {
        switch (dir)
        {
            case MazeDir.North: return new Vector2Int(0, -1);
            case MazeDir.East: return new Vector2Int(1, 0);
            case MazeDir.South: return new Vector2Int(0, 1);
            default: return new Vector2Int(-1, 0);
        }
    }

    /// <summary>보는 쪽에서 turn만큼 돈 방향</summary>
    public static MazeDir Turn(MazeDir facing, MazeTurn turn) => (MazeDir)(((int)facing + (int)turn) % 4);
}

/// <summary>
/// 미로 한 층의 지도 (아스티넬식 격자 미로). 글자 한 줄 = 지도 한 줄:
/// '#' 벽 · '.' 길 · 'S' 입구(시작점) · 'E' 아래층 계단(도착지) · 'R' 길을 막는 돌(광맥) · 'T' 보물 상자.
/// 바깥 테두리는 벽이어야 하고, 입구는 하나. Docs/광산_미로탐험과_왕도성장_기획.md 4.4
/// </summary>
public class MazeMap
{
    private readonly MazeCell[,] _cells;
    private readonly List<Vector2Int> _rocks = new List<Vector2Int>();
    private readonly List<Vector2Int> _chests = new List<Vector2Int>();

    public int Width { get; }
    public int Height { get; }
    public Vector2Int Start { get; private set; }
    public Vector2Int Stairs { get; private set; } = new Vector2Int(-1, -1);
    public IReadOnlyList<Vector2Int> Rocks => _rocks;
    public IReadOnlyList<Vector2Int> Chests => _chests;

    private MazeMap(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new MazeCell[width, height];
    }

    public static MazeMap Parse(IReadOnlyList<string> rows)
    {
        if (rows == null || rows.Count == 0)
            throw new ArgumentException("지도가 비었습니다", nameof(rows));
        int width = 0;
        foreach (var row in rows)
            width = Mathf.Max(width, row != null ? row.Length : 0);
        var map = new MazeMap(width, rows.Count);
        bool hasStart = false;
        for (int y = 0; y < rows.Count; y++)
        {
            string row = rows[y] ?? "";
            for (int x = 0; x < width; x++)
            {
                char c = x < row.Length ? row[x] : '#';
                var cell = Of(c);
                var at = new Vector2Int(x, y);
                bool edge = x == 0 || y == 0 || x == width - 1 || y == rows.Count - 1;
                if (edge && cell != MazeCell.Wall)
                    throw new ArgumentException($"지도 테두리 ({x},{y})가 벽이 아닙니다");
                map._cells[x, y] = cell;
                switch (cell)
                {
                    case MazeCell.Start:
                        if (hasStart)
                            throw new ArgumentException("입구 'S'가 둘 이상입니다");
                        hasStart = true;
                        map.Start = at;
                        break;
                    case MazeCell.Stairs:
                        map.Stairs = at;
                        break;
                    case MazeCell.Rock:
                        map._rocks.Add(at);
                        break;
                    case MazeCell.Chest:
                        map._chests.Add(at);
                        break;
                }
            }
        }
        if (!hasStart)
            throw new ArgumentException("입구 'S'가 없습니다");
        return map;
    }

    private static MazeCell Of(char c)
    {
        switch (c)
        {
            case '.': return MazeCell.Floor;
            case 'S': return MazeCell.Start;
            case 'E': return MazeCell.Stairs;
            case 'R': return MazeCell.Rock;
            case 'T': return MazeCell.Chest;
            case '#': return MazeCell.Wall;
            default: throw new ArgumentException($"모르는 지도 글자 '{c}'");
        }
    }

    public bool Inside(Vector2Int at) => at.x >= 0 && at.y >= 0 && at.x < Width && at.y < Height;

    public MazeCell Cell(Vector2Int at) => Inside(at) ? _cells[at.x, at.y] : MazeCell.Wall;

    public bool IsWall(Vector2Int at) => Cell(at) == MazeCell.Wall;

    /// <summary>입구에서 처음 볼 쪽: 열린 첫 방향 (동 → 북 → 남 → 서)</summary>
    public MazeDir StartFacing
    {
        get
        {
            foreach (var dir in new[] { MazeDir.East, MazeDir.North, MazeDir.South, MazeDir.West })
            {
                if (!IsWall(Start + MazeDirs.Step(dir)))
                    return dir;
            }
            return MazeDir.East;
        }
    }

    /// <summary>입구에서 갈 수 있는 칸 (돌 · 상자도 지나갈 수 있다고 보고)</summary>
    public HashSet<Vector2Int> Reachable()
    {
        var seen = new HashSet<Vector2Int> { Start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(Start);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                var next = at + MazeDirs.Step((MazeDir)d);
                if (!IsWall(next) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }
}
