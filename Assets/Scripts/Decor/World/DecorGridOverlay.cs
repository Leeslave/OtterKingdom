using UnityEngine;

/// <summary>
/// 꾸미기 모드에서 월드에 까는 격자. 칸 상태마다 색을 칠한 작은 텍스처 한 장으로 그린다.
/// 놓을 수 있는 칸 = 밝은 칸 + 흰 테두리, 잠긴 구역 = 어둡게, 놓을 수 없는 칸 = 비움.
/// </summary>
public class DecorGridOverlay : MonoBehaviour
{
    private const int PixelsPerCell = 16;

    private static readonly Color FreeFill = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color OccupiedFill = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color LockedFill = new Color(0.22f, 0.2f, 0.28f, 0.45f);
    private static readonly Color Line = new Color(1f, 1f, 1f, 0.4f);
    private static readonly Color LockedLine = new Color(1f, 1f, 1f, 0.2f);

    private SpriteRenderer _renderer;
    private Texture2D _texture;
    private Color32[] _pixels;
    private DecorBoardView _board;
    private DecorLayout _layout;

    public void Show(DecorBoardView board)
    {
        if (board == null)
            throw new System.ArgumentNullException(nameof(board));

        Unsubscribe();
        _board = board;
        _layout = board.Layout;
        EnsureTexture();

        // 물건(z=0)보다 살짝 뒤에 두어 같은 정렬 순서여도 물건이 위에 그려지게
        transform.position = new Vector3(board.Origin.x, board.Origin.y, 0.05f);
        _renderer.sortingOrder = board.OverlaySortingOrder;

        _layout.OnPlaced += Redraw;
        _layout.OnMoved += Redraw;
        _layout.OnRemoved += Redraw;
        _layout.OnRegionUnlocked += RedrawRegion;

        Redraw(null);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        Unsubscribe();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (_texture != null)
            Destroy(_texture);
    }

    private void Unsubscribe()
    {
        if (_layout == null)
            return;

        _layout.OnPlaced -= Redraw;
        _layout.OnMoved -= Redraw;
        _layout.OnRemoved -= Redraw;
        _layout.OnRegionUnlocked -= RedrawRegion;
        _layout = null;
    }

    private void EnsureTexture()
    {
        int width = _layout.Size.x * PixelsPerCell;
        int height = _layout.Size.y * PixelsPerCell;
        if (_texture != null && _texture.width == width && _texture.height == height)
            return;

        if (_texture != null)
            Destroy(_texture);

        _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        _pixels = new Color32[width * height];

        if (_renderer == null)
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        // 칸 하나 = PixelsPerCell 픽셀 = cellSize 월드 단위, 왼쪽 아래가 격자 모서리
        _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, width, height), Vector2.zero, PixelsPerCell / _board.CellSize);
    }

    private void RedrawRegion(string regionId) => Redraw(null);

    private void Redraw(PlacedDecor _)
    {
        int width = _texture.width;
        for (int cy = 0; cy < _layout.Size.y; cy++)
        {
            for (int cx = 0; cx < _layout.Size.x; cx++)
            {
                var state = _layout.GetCellState(new Vector2Int(cx, cy));
                Color fill = Color.clear;
                Color line = Color.clear;
                switch (state)
                {
                    case DecorCellState.Free: fill = FreeFill; line = Line; break;
                    case DecorCellState.Occupied: fill = OccupiedFill; line = Line; break;
                    case DecorCellState.Locked: fill = LockedFill; line = LockedLine; break;
                }

                for (int py = 0; py < PixelsPerCell; py++)
                {
                    int row = (cy * PixelsPerCell + py) * width + cx * PixelsPerCell;
                    for (int px = 0; px < PixelsPerCell; px++)
                    {
                        bool edge = px == 0 || py == 0 || px == PixelsPerCell - 1 || py == PixelsPerCell - 1;
                        _pixels[row + px] = edge ? line : fill;
                    }
                }
            }
        }

        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
    }
}
