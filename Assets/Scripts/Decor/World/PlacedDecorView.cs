using System.Collections;
using UnityEngine;

/// <summary>
/// 월드에 놓인 물건 하나의 모습. DecorBoardView가 만들고 치운다.
/// 해달이 가지고 놀면 물건 종류에 맞게 반응한다 (공은 통통, 퍼즐은 흔들흔들).
/// </summary>
public class PlacedDecorView : MonoBehaviour
{
    private const float BounceHeightInCells = 0.35f;
    private const float BouncePerSecond = 2.2f;
    private const float WiggleDegrees = 10f;
    private const float WigglePerSecond = 3f;
    private const float SpinPerSecond = 1.2f;

    private SpriteRenderer _renderer;
    private Transform _body;
    private Quaternion _baseRotation = Quaternion.identity;
    private float _cellSize;
    private Coroutine _reaction;

    public PlacedDecor Placed { get; protected set; }

    /// <summary>지금 같이 놀고 있는 해달 수 (DecorBoardView가 예약·해제)</summary>
    public int Players { get; internal set; }

    public bool HasRoom => !Placed.Decor.IsBuilding && Players < Placed.Decor.MaxPlayers;

    /// <summary>물건이 땅에 닿는 자리 (차지한 칸 아래쪽 가운데)</summary>
    public Vector2 GroundPoint => transform.position;

    /// <summary>차지한 영역 (월드)</summary>
    public Rect WorldRect { get; protected set; }

    internal virtual void Init(PlacedDecor placed, Rect worldRect, float cellSize, float fill, int sortingOrder)
    {
        Placed = placed;
        _cellSize = cellSize;

        if (_body == null)
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _renderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            _renderer.transform.SetParent(_body, false);
        }

        _renderer.sortingOrder = sortingOrder;
        Place(worldRect, fill);
    }

    // 칸에 맞춰 놓고 방향만큼 돌림 (옮기거나 돌린 뒤에도 다시 부름)
    internal virtual void Place(Rect worldRect, float fill)
    {
        StopReaction();
        WorldRect = worldRect;
        transform.position = new Vector3(worldRect.center.x, worldRect.yMin, 0f);
        _baseRotation = DecorVisual.Fit(_body, _renderer, Placed.Decor, Placed.Rotation, worldRect, fill);
    }

    private void StopReaction()
    {
        if (_reaction == null)
            return;

        StopCoroutine(_reaction);
        _reaction = null;
    }

    internal virtual void SetSortingOrder(int order) => _renderer.sortingOrder = order;

    /// <summary>가지고 노는 동안 반응 (이미 반응 중이면 시간을 늘림)</summary>
    public void PlayReaction(float seconds)
    {
        if (_reaction != null)
            StopCoroutine(_reaction);
        _reaction = StartCoroutine(ReactionRoutine(seconds));
    }

    private IEnumerator ReactionRoutine(float seconds)
    {
        Vector3 basePosition = _body.localPosition;
        Quaternion baseRotation = _baseRotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            switch (Placed.Decor.PlayStyle)
            {
                case DecorPlayStyle.Bounce:
                    float hop = Mathf.Abs(Mathf.Sin(t * BouncePerSecond * Mathf.PI));
                    _body.localPosition = basePosition + Vector3.up * hop * BounceHeightInCells * _cellSize;
                    break;
                case DecorPlayStyle.Wiggle:
                    _body.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * WigglePerSecond * Mathf.PI * 2f) * WiggleDegrees);
                    break;
                case DecorPlayStyle.Spin:
                    _body.localRotation = baseRotation * Quaternion.Euler(0f, 0f, -t * SpinPerSecond * 360f);
                    break;
            }
            yield return null;
        }

        _body.localPosition = basePosition;
        _body.localRotation = baseRotation;
        _reaction = null;
    }
}
