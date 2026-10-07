using UnityEngine;

/// <summary>
/// 꾸미기 모드에서 들고 있는 물건의 미리보기: 반투명한 물건 그림 + 차지할 칸 (놓을 수 있으면 초록, 없으면 빨강).
/// 늘 다른 것들보다 위에 그린다.
/// </summary>
public class DecorGhostView : MonoBehaviour
{
    private const float GhostAlpha = 0.85f;

    private SpriteRenderer _tile;
    private SpriteRenderer _toy;
    private Transform _toyBody;
    private Sprite _okSprite;
    private Sprite _blockedSprite;

    public Rect WorldRect { get; private set; }

    public void Init(Sprite okSprite, Sprite blockedSprite, int sortingOrder)
    {
        _okSprite = okSprite;
        _blockedSprite = blockedSprite;

        if (_tile == null)
        {
            _tile = new GameObject("Footprint").AddComponent<SpriteRenderer>();
            _tile.transform.SetParent(transform, false);

            _toyBody = new GameObject("Toy").transform;
            _toyBody.SetParent(transform, false);
            _toy = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            _toy.transform.SetParent(_toyBody, false);
            _toy.color = new Color(1f, 1f, 1f, GhostAlpha);
        }

        _tile.sortingOrder = sortingOrder;
        _toy.sortingOrder = sortingOrder + 1;
    }

    public void Show(DecorDefinition decor, DecorRotation rotation, Rect worldRect, bool canPlace, float fill)
    {
        WorldRect = worldRect;
        transform.position = new Vector3(worldRect.xMin, worldRect.yMin, 0f);

        _tile.sprite = canPlace ? _okSprite : _blockedSprite;
        // 건물처럼 그림이 칸을 덮어도 놓을 수 없는 자리임이 보이게 그림도 붉게
        _toy.color = canPlace ? new Color(1f, 1f, 1f, GhostAlpha) : new Color(1f, 0.55f, 0.55f, GhostAlpha);
        _tile.transform.localPosition = new Vector3(worldRect.width * 0.5f, worldRect.height * 0.5f, 0f);
        if (_tile.sprite != null)
        {
            Vector2 tileSize = _tile.sprite.bounds.size;
            _tile.transform.localScale = new Vector3(worldRect.width / tileSize.x, worldRect.height / tileSize.y, 1f);
        }

        // DecorVisual은 부모 원점이 칸 아래 가운데라고 보므로, 이 오브젝트(칸 왼쪽 아래)에서 가로 절반만큼 옮김
        if (decor is BuildingDefinition building)
            BuildingVisual.Fit(_toyBody, _toy, building, building.WorldSprite, worldRect);
        else
            DecorVisual.Fit(_toyBody, _toy, decor, rotation, worldRect, fill);
        _toyBody.localPosition += new Vector3(worldRect.width * 0.5f, 0f, 0f);

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}
