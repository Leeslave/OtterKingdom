using System.Collections.Generic;
using UnityEngine;

// Rasterises the plaza's Walkable/Blocked polygons into a small 2D grid and
// answers every movement query the otters need: is this feet position
// walkable, pick a reachable destination, find a path (A* + string pulling)
// and check that a straight segment stays on walkable cells.
//
// `clearance` shrinks the walkable space by the otter's half body width so
// feet placed on the edge of a cell don't leave the body buried in a fence.
// Connected components are labelled once at build time so destinations on
// the far side of the sea/houses are rejected without running A* at all.
//
// The grid is built lazily on first query (or on Rebuild()); nothing is
// recomputed per frame. In edit mode the gizmo rebuilds when a polygon
// changes so the Scene view always shows the real result.
[DefaultExecutionOrder(-100)]
public class PlazaWalkableArea : MonoBehaviour
{
    public enum GizmoMode { Off, WhenSelected, Always }

    [Tooltip("Every active PlazaAreaPolygon under these roots is used — add an area by duplicating a child polygon.")]
    [SerializeField] private Transform[] polygonRoots;
    [Tooltip("Grid resolution in world units. Smaller = follows the polygons more tightly but costs more memory/A* time.")]
    [SerializeField, Min(0.05f)] private float cellSize = 0.25f;
    [Tooltip("Keeps feet at least this far (world units) from any non-walkable edge — roughly half the otter's body width.")]
    [SerializeField, Min(0f)] private float clearance = 0.4f;

    [Header("Debug")]
    [SerializeField] private GizmoMode gridGizmo = GizmoMode.WhenSelected;
    [Tooltip("Draws each otter's current path (visible in the Game view when Gizmos are on).")]
    [SerializeField] private bool showAgentPaths = true;

    public bool ShowAgentPaths => showAgentPaths;

    private static readonly Vector2Int[] NeighbourOffsets =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    private const float Sqrt2 = 1.41421356f;
    private const int NearestCellSearchRadius = 8;
    private const int RandomCellAttempts = 64;

    private bool built;
    private Vector2 origin;
    private int width;
    private int height;
    private bool[] walkable;
    private int[] component;
    private int largestComponent = -1;

    // A* scratch buffers, reused between searches via a stamp counter.
    private float[] gScore;
    private int[] parent;
    private int[] openStamp;
    private int[] closedStamp;
    private int searchStamp;
    private readonly MinHeap openSet = new MinHeap();
    private readonly List<int> cellPath = new List<int>();
    private readonly List<Vector2> rawPath = new List<Vector2>();

    private readonly List<PlazaAreaPolygon> areas = new List<PlazaAreaPolygon>();
    private int builtSignature;
    private bool warnedNoAreas;

    public bool HasWalkableCells => EnsureBuilt() && largestComponent >= 0;

    private void Awake()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        built = true;
        builtSignature = ComputeSignature();

        var walkPolys = new List<List<Vector2>>();
        var blockPolys = new List<List<Vector2>>();
        foreach (var area in areas)
        {
            if (area.PointCount < 3) continue;
            var pts = new List<Vector2>();
            area.GetWorldPoints(pts);
            (area.Kind == PlazaAreaKind.Walkable ? walkPolys : blockPolys).Add(pts);
        }

        if (walkPolys.Count == 0)
        {
            width = height = 0;
            walkable = new bool[0];
            component = new int[0];
            largestComponent = -1;
            if (!warnedNoAreas && Application.isPlaying)
            {
                warnedNoAreas = true;
                Debug.LogWarning($"[{nameof(PlazaWalkableArea)}] No Walkable polygon with 3+ points is assigned — otters cannot move.", this);
            }
            return;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var poly in walkPolys)
        {
            foreach (var p in poly)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }

        origin = min - Vector2.one * cellSize;
        width = Mathf.CeilToInt((max.x - min.x) / cellSize) + 2;
        height = Mathf.CeilToInt((max.y - min.y) / cellSize) + 2;
        int count = width * height;

        walkable = new bool[count];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                walkable[y * width + x] = IsClearAt(CellCenter(x, y), walkPolys, blockPolys);
            }
        }

        LabelComponents();

        gScore = new float[count];
        parent = new int[count];
        openStamp = new int[count];
        closedStamp = new int[count];
        searchStamp = 0;
    }

    // Centre plus a ring of 8 samples at `clearance`: cheap approximation of
    // "a disc of this radius fits here" that is plenty at otter scale.
    private bool IsClearAt(Vector2 p, List<List<Vector2>> walkPolys, List<List<Vector2>> blockPolys)
    {
        if (!IsFreePoint(p, walkPolys, blockPolys)) return false;
        if (clearance <= 0f) return true;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            Vector2 sample = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * clearance;
            if (!IsFreePoint(sample, walkPolys, blockPolys)) return false;
        }
        return true;
    }

    private static bool IsFreePoint(Vector2 p, List<List<Vector2>> walkPolys, List<List<Vector2>> blockPolys)
    {
        bool inWalk = false;
        foreach (var poly in walkPolys)
        {
            if (PlazaAreaPolygon.Contains(poly, p)) { inWalk = true; break; }
        }
        if (!inWalk) return false;

        foreach (var poly in blockPolys)
        {
            if (PlazaAreaPolygon.Contains(poly, p)) return false;
        }
        return true;
    }

    private void LabelComponents()
    {
        component = new int[walkable.Length];
        for (int i = 0; i < component.Length; i++) component[i] = -1;

        largestComponent = -1;
        int largestSize = 0;
        int nextId = 0;
        var queue = new Queue<int>();

        for (int start = 0; start < walkable.Length; start++)
        {
            if (!walkable[start] || component[start] >= 0) continue;

            int id = nextId++;
            int size = 0;
            component[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                size++;
                int cx = cell % width;
                int cy = cell / width;
                foreach (var o in NeighbourOffsets)
                {
                    if (!CanStep(cx, cy, o.x, o.y)) continue;
                    int n = (cy + o.y) * width + cx + o.x;
                    if (component[n] >= 0) continue;
                    component[n] = id;
                    queue.Enqueue(n);
                }
            }

            if (size > largestSize)
            {
                largestSize = size;
                largestComponent = id;
            }
        }
    }

    private bool EnsureBuilt()
    {
        if (!built) Rebuild();
        return width > 0;
    }

    // --- Queries -----------------------------------------------------------

    public bool IsWalkable(Vector2 worldPos)
    {
        return EnsureBuilt() && TryGetCell(worldPos, out int cell) && walkable[cell];
    }

    // Uniformly random walkable point. componentFilter < 0 = largest connected
    // area, which is the main plaza in practice.
    public bool TryGetRandomPoint(out Vector2 point, int componentFilter = -1)
    {
        point = default;
        if (!EnsureBuilt() || largestComponent < 0) return false;

        int wanted = componentFilter < 0 ? largestComponent : componentFilter;
        int count = walkable.Length;
        for (int attempt = 0; attempt < RandomCellAttempts; attempt++)
        {
            int cell = Random.Range(0, count);
            if (component[cell] != wanted) continue;
            point = RandomPointInCell(cell);
            return true;
        }

        // Tiny area relative to the grid bounds: scan from a random start so
        // it still yields a point.
        int startIndex = Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int cell = (startIndex + i) % count;
            if (component[cell] != wanted) continue;
            point = RandomPointInCell(cell);
            return true;
        }
        return false;
    }

    // Random walkable point within `radius` of `center`, same connected area
    // as the largest component. Used to seed a few otters near the camera.
    public bool TryGetRandomPointNear(Vector2 center, float radius, int attempts, out Vector2 point)
    {
        point = default;
        if (!EnsureBuilt() || largestComponent < 0) return false;

        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidate = center + Random.insideUnitCircle * radius;
            if (TryGetCell(candidate, out int cell) && walkable[cell] && component[cell] == largestComponent)
            {
                point = candidate;
                return true;
            }
        }
        return false;
    }

    // One random destination attempt: `minDist..maxDist` away from `from`,
    // walkable and in the same connected area. The caller owns the retry
    // budget (PlazaSettings.maxDestinationTries).
    public bool TryPickDestination(Vector2 from, float minDist, float maxDist, out Vector2 destination)
    {
        destination = default;
        if (!EnsureBuilt() || !TryGetNearestWalkableCell(from, out int fromCell)) return false;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(minDist, maxDist);
        Vector2 candidate = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

        if (!TryGetCell(candidate, out int cell) || !walkable[cell]) return false;
        if (component[cell] != component[fromCell]) return false;

        destination = candidate;
        return true;
    }

    // Fills `path` with world-space waypoints from (but excluding) `from` to
    // `to`. Every consecutive pair is guaranteed IsSegmentClear, including
    // after smoothing — except the very first leg when `from` itself sits
    // off the grid (e.g. a hand-placed spawn point), which walks straight
    // back onto the nearest walkable cell.
    public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> path)
    {
        path.Clear();
        if (!EnsureBuilt()) return false;
        if (!TryGetNearestWalkableCell(from, out int start)) return false;
        if (!TryGetCell(to, out int goal) || !walkable[goal]) return false;
        if (component[start] != component[goal]) return false;

        if (!RunAStar(start, goal)) return false;

        rawPath.Clear();
        rawPath.Add(from);
        bool fromOnGrid = TryGetCell(from, out int fromCell) && fromCell == start;
        for (int i = fromOnGrid ? 1 : 0; i < cellPath.Count - 1; i++)
        {
            rawPath.Add(CellCenter(cellPath[i]));
        }
        rawPath.Add(to);

        // String pulling: from each anchor jump to the furthest point still
        // visible in a straight, fully walkable line.
        int anchor = 0;
        int last = rawPath.Count - 1;
        while (anchor < last)
        {
            int next = last;
            while (next > anchor + 1 && !IsSegmentClear(rawPath[anchor], rawPath[next])) next--;
            path.Add(rawPath[next]);
            anchor = next;
        }
        return true;
    }

    // Walks every grid cell the segment touches (Amanatides–Woo), so thin
    // obstacles and diagonal corner cuts can't be skipped the way fixed-step
    // sampling could.
    public bool IsSegmentClear(Vector2 a, Vector2 b)
    {
        if (!EnsureBuilt()) return false;

        Vector2 ga = (a - origin) / cellSize;
        Vector2 gb = (b - origin) / cellSize;
        int x = Mathf.FloorToInt(ga.x);
        int y = Mathf.FloorToInt(ga.y);
        int endX = Mathf.FloorToInt(gb.x);
        int endY = Mathf.FloorToInt(gb.y);
        if (!IsCellWalkable(x, y)) return false;

        float dx = gb.x - ga.x;
        float dy = gb.y - ga.y;
        int stepX = dx > 0f ? 1 : -1;
        int stepY = dy > 0f ? 1 : -1;
        float tDeltaX = dx != 0f ? 1f / Mathf.Abs(dx) : float.PositiveInfinity;
        float tDeltaY = dy != 0f ? 1f / Mathf.Abs(dy) : float.PositiveInfinity;
        float tMaxX = dx != 0f ? (stepX > 0 ? x + 1 - ga.x : ga.x - x) * tDeltaX : float.PositiveInfinity;
        float tMaxY = dy != 0f ? (stepY > 0 ? y + 1 - ga.y : ga.y - y) * tDeltaY : float.PositiveInfinity;

        int guard = Mathf.Abs(endX - x) + Mathf.Abs(endY - y) + 2;
        while ((x != endX || y != endY) && guard-- > 0)
        {
            if (Mathf.Approximately(tMaxX, tMaxY))
            {
                // Passing exactly through a cell corner: both side cells must
                // be open, same no-corner-cutting rule as A*.
                if (!IsCellWalkable(x + stepX, y) || !IsCellWalkable(x, y + stepY)) return false;
                x += stepX;
                y += stepY;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }
            else if (tMaxX < tMaxY)
            {
                x += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                y += stepY;
                tMaxY += tDeltaY;
            }

            if (!IsCellWalkable(x, y)) return false;
        }
        return true;
    }

    // --- Grid helpers ------------------------------------------------------

    private bool TryGetCell(Vector2 worldPos, out int cell)
    {
        int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - origin.y) / cellSize);
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            cell = -1;
            return false;
        }
        cell = y * width + x;
        return true;
    }

    private bool IsCellWalkable(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height && walkable[y * width + x];
    }

    private bool CanStep(int x, int y, int dx, int dy)
    {
        if (!IsCellWalkable(x + dx, y + dy)) return false;
        if (dx != 0 && dy != 0)
        {
            return IsCellWalkable(x + dx, y) && IsCellWalkable(x, y + dy);
        }
        return true;
    }

    private Vector2 CellCenter(int x, int y) => origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    private Vector2 CellCenter(int cell) => CellCenter(cell % width, cell / width);

    private Vector2 RandomPointInCell(int cell)
    {
        return CellCenter(cell) + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, 0.45f)) * cellSize;
    }

    private bool TryGetNearestWalkableCell(Vector2 worldPos, out int cell)
    {
        if (TryGetCell(worldPos, out cell) && walkable[cell]) return true;

        int cx = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int cy = Mathf.FloorToInt((worldPos.y - origin.y) / cellSize);
        float bestDist = float.MaxValue;
        cell = -1;
        for (int r = 1; r <= NearestCellSearchRadius; r++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(y - cy)) != r) continue;
                    if (!IsCellWalkable(x, y)) continue;
                    float d = (CellCenter(x, y) - worldPos).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        cell = y * width + x;
                    }
                }
            }
            if (cell >= 0) return true;
        }
        return false;
    }

    private bool RunAStar(int start, int goal)
    {
        cellPath.Clear();
        searchStamp++;
        openSet.Clear();

        int gx = goal % width;
        int gy = goal / width;

        gScore[start] = 0f;
        parent[start] = -1;
        openStamp[start] = searchStamp;
        openSet.Push(start, Heuristic(start % width, start / width, gx, gy));

        while (openSet.Count > 0)
        {
            int current = openSet.Pop();
            if (closedStamp[current] == searchStamp) continue;
            closedStamp[current] = searchStamp;

            if (current == goal)
            {
                for (int c = goal; c >= 0; c = parent[c]) cellPath.Add(c);
                cellPath.Reverse();
                return true;
            }

            int cx = current % width;
            int cy = current / width;
            foreach (var o in NeighbourOffsets)
            {
                if (!CanStep(cx, cy, o.x, o.y)) continue;
                int n = (cy + o.y) * width + cx + o.x;
                if (closedStamp[n] == searchStamp) continue;

                float tentative = gScore[current] + (o.x != 0 && o.y != 0 ? Sqrt2 : 1f);
                if (openStamp[n] == searchStamp && tentative >= gScore[n]) continue;

                openStamp[n] = searchStamp;
                gScore[n] = tentative;
                parent[n] = current;
                openSet.Push(n, tentative + Heuristic(cx + o.x, cy + o.y, gx, gy));
            }
        }
        return false;
    }

    private static float Heuristic(int x, int y, int gx, int gy)
    {
        int dx = Mathf.Abs(x - gx);
        int dy = Mathf.Abs(y - gy);
        return (dx + dy) + (Sqrt2 - 2f) * Mathf.Min(dx, dy);
    }

    private void CollectAreas()
    {
        areas.Clear();
        if (polygonRoots == null) return;
        foreach (var root in polygonRoots)
        {
            if (root == null) continue;
            foreach (var area in root.GetComponentsInChildren<PlazaAreaPolygon>())
            {
                if (!areas.Contains(area)) areas.Add(area);
            }
        }
    }

    // Also refreshes `areas`, so it always reflects the current hierarchy.
    private int ComputeSignature()
    {
        CollectAreas();
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + cellSize.GetHashCode();
            hash = hash * 31 + clearance.GetHashCode();
            var pts = new List<Vector2>();
            foreach (var area in areas)
            {
                hash = hash * 31 + (int)area.Kind;
                area.GetWorldPoints(pts);
                foreach (var p in pts) hash = hash * 31 + p.GetHashCode();
            }
            return hash;
        }
    }

    // --- Gizmos ------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (gridGizmo == GizmoMode.Always) DrawGridGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (gridGizmo == GizmoMode.WhenSelected) DrawGridGizmo();
    }

    private void DrawGridGizmo()
    {
        // In edit mode, follow polygon edits live. In play mode the grid the
        // otters are actually using is shown as-is.
        if (!Application.isPlaying && (!built || builtSignature != ComputeSignature())) Rebuild();
        if (!EnsureBuilt()) return;

        Vector3 size = new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0f);
        for (int i = 0; i < walkable.Length; i++)
        {
            if (!walkable[i]) continue;
            Gizmos.color = component[i] == largestComponent
                ? new Color(0.2f, 0.9f, 1f, 0.25f)
                : new Color(1f, 0.8f, 0.1f, 0.35f); // isolated pocket: otters there can't reach the plaza
            Gizmos.DrawCube(CellCenter(i), size);
        }
    }

    private void OnValidate()
    {
        built = false;
    }

    // Minimal binary min-heap keyed by float priority; duplicates allowed
    // (stale entries are skipped via closedStamp).
    private class MinHeap
    {
        private readonly List<(float priority, int item)> heap = new List<(float, int)>();

        public int Count => heap.Count;

        public void Clear() => heap.Clear();

        public void Push(int item, float priority)
        {
            heap.Add((priority, item));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heap[p].priority <= heap[i].priority) break;
                (heap[p], heap[i]) = (heap[i], heap[p]);
                i = p;
            }
        }

        public int Pop()
        {
            int result = heap[0].item;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                int r = l + 1;
                int smallest = i;
                if (l < heap.Count && heap[l].priority < heap[smallest].priority) smallest = l;
                if (r < heap.Count && heap[r].priority < heap[smallest].priority) smallest = r;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return result;
        }
    }
}
