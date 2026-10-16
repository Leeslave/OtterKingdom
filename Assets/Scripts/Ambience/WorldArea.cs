using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드에서 무언가가 생길 수 있는 곳 (반짝임, 물고기 점프): 점 몇 개(+흩어짐 반지름)이거나,
/// 배경 그림을 칸으로 나눈 격자에서 '1'인 칸(물 등). 격자는 배경 그림을 분석해 setup이 채운다.
/// </summary>
[Serializable]
public class WorldArea
{
    [Tooltip("이 점들 둘레에서 고름 (격자가 있으면 무시)")]
    [SerializeField] private List<Vector2> _points = new List<Vector2>();

    [Tooltip("점에서 흩어지는 반지름 (월드 단위)")]
    [SerializeField] private float _radius = 0.2f;

    [Tooltip("격자 왼쪽 위 (월드)")]
    [SerializeField] private Vector2 _gridTopLeft;

    [Tooltip("격자 한 칸 크기 (월드)")]
    [SerializeField] private float _gridCell = 1f;

    [Tooltip("격자 줄 (위에서부터, '1'인 칸에서 고름)")]
    [SerializeField] private List<string> _gridRows = new List<string>();

    private readonly List<Vector2Int> _cells = new List<Vector2Int>();
    private bool _cellsReady;

    public WorldArea() { }

    public WorldArea(List<Vector2> points, float radius)
    {
        _points = points;
        _radius = radius;
    }

    public WorldArea(Vector2 gridTopLeft, float gridCell, List<string> rows)
    {
        _gridTopLeft = gridTopLeft;
        _gridCell = gridCell;
        _gridRows = rows;
    }

    public bool IsEmpty => _gridRows.Count == 0 && _points.Count == 0;

    /// <summary>아무 곳 하나 (비어 있으면 false)</summary>
    public bool TryPick(out Vector2 point)
    {
        if (_gridRows.Count > 0)
        {
            ReadCells();
            if (_cells.Count > 0)
            {
                var cell = _cells[UnityEngine.Random.Range(0, _cells.Count)];
                point = _gridTopLeft + new Vector2((cell.x + UnityEngine.Random.value) * _gridCell, -(cell.y + UnityEngine.Random.value) * _gridCell);
                return true;
            }
        }
        if (_points.Count > 0)
        {
            point = _points[UnityEngine.Random.Range(0, _points.Count)] + UnityEngine.Random.insideUnitCircle * _radius;
            return true;
        }
        point = default;
        return false;
    }

    private void ReadCells()
    {
        if (_cellsReady)
            return;
        _cellsReady = true;
        for (int y = 0; y < _gridRows.Count; y++)
        {
            string row = _gridRows[y];
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x] == '1')
                    _cells.Add(new Vector2Int(x, y));
            }
        }
    }
}
