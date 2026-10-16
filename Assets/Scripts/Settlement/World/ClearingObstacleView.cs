using UnityEngine;

/// <summary>장애물 종류: 바위는 곡괭이로 금이 가다 부서져 자갈이 남고, 나무는 도끼로 찍히다 쓰러져 그루터기가 남는다</summary>
public enum ObstacleKind
{
    Rock,
    Tree,
}

/// <summary>
/// 새 장소의 길을 막은 나무·돌 하나 (광산 길 열기). 몇 번 눌러 치우면 재료를 조금 주고 사라진다.
/// 치운 것은 세이브에 남아 다시 나타나지 않는다. 다 치웠는지는 ZoneClearingView가 본다.
/// 공동사업 장애물(P3 광장 확장)은 사업의 장애물 단계일 때만 치울 수 있고, 치우면 사업이 기록한다 (SettlementManager.TryClearProjectObstacle).
/// 기획 원칙: 직접 개척은 짧게 (장애물 2~4개, 하나에 몇 번).
/// </summary>
public class ClearingObstacleView : MonoBehaviour
{
    private const float ShakeSeconds = 0.18f;
    private const float FallSeconds = 0.5f;
    private const float BounceSeconds = 0.22f;
    private const float FadeSeconds = 0.35f;
    private const float BreakSeconds = 0.22f;
    private const float FallAngle = -86f;

    [Header("식별")]
    [Tooltip("세이브에 저장되는 장애물 ID (게임 전체에서 겹치지 않게, 예: mine_rock_01)")]
    [SerializeField] private string _obstacleId;

    [Tooltip("공동사업 장애물 (광장 확장): 사업의 장애물 단계일 때만 치울 수 있음. ZoneClearingView 없이 스스로 정함")]
    [SerializeField] private bool _projectObstacle;

    [Header("치우기")]
    [SerializeField] private ObstacleKind _kind = ObstacleKind.Rock;

    [Tooltip("몇 번 눌러야 치워지는지")]
    [SerializeField] private int _hits = 4;

    [Tooltip("치우면 주는 재료")]
    [SerializeField] private ItemDefinition _reward;

    [Tooltip("치우면 주는 개수")]
    [SerializeField] private int _rewardAmount = 2;

    [Header("그림")]
    [Tooltip("멀쩡함 → 금 감 순서 (나무는 한 장)")]
    [SerializeField] private Sprite[] _sprites;

    [Tooltip("누를 때 튀는 조각 (돌 조각 / 나뭇잎)")]
    [SerializeField] private Sprite _pieceSprite;

    [Tooltip("치운 뒤 남는 것 (나무 = 그루터기, 바위 = 자갈). 비우면 아무것도 안 남음")]
    [SerializeField] private Sprite _clearedSprite;

    [Tooltip("남는 것의 폭 (장애물 그림 폭 대비, 그림 해상도와 상관없이)")]
    [SerializeField] private float _clearedScale = 0.45f;

    [Header("구성 요소")]
    [Tooltip("장애물 그림 (피벗이 바닥)")]
    [SerializeField] private SpriteRenderer _renderer;

    [Tooltip("탭을 받을 영역")]
    [SerializeField] private Collider2D _tapArea;

    [SerializeField] private PlazaNodeFx _fx;

    [Header("길 막기")]
    [Tooltip("해달이 못 지나가는 발자국 (치우면 꺼짐. 걷기 영역이 없는 장소는 비움)")]
    [SerializeField] private GameObject _footprint;

    [Tooltip("발자국이 꺼지면 다시 계산할 걷기 영역 (없는 장소는 비움)")]
    [SerializeField] private PlazaWalkableArea _walkableArea;

    [Header("처음 안내 (선택)")]
    [Tooltip("처음 한 번 \"톡톡 눌러 치워요!\" (장애물 하나에만, 비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Header("글자 색")]
    [SerializeField] private Color _textColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);

    public string ObstacleId => _obstacleId;

    /// <summary>ZoneClearingView가 정함 (부탁이 열려 있을 때만 치울 수 있음)</summary>
    public bool Interactable { get; set; }

    /// <summary>ZoneClearingView가 정함: 장소 개척이 이미 끝났으면 (옛 세이브 등 하나하나 치운 기록이 없어도) 치운 것으로 보임</summary>
    public bool ZoneCleared { get; set; }

    private int _done;
    private float _shakeTimer;
    private float _fallTimer = -1f;
    private bool _landed;
    private Vector3 _baseScale;
    private Vector3 _basePosition;
    private SpriteRenderer _leftover;

    private Vector3 Center => _renderer.bounds.center;

    private void Awake()
    {
        _baseScale = _renderer.transform.localScale;
        _basePosition = _renderer.transform.localPosition;
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        if (_fallTimer >= 0f)
        {
            AnimateFall();
            return;
        }

        if (_projectObstacle)
            Interactable = manager.IsProjectObstacleActive(_obstacleId);
        bool cleared = ZoneCleared || manager.IsObstacleCleared(_obstacleId);
        if (cleared && _hint != null)
            _hint.Allowed = false;
        if (cleared && _footprint != null && _footprint.activeSelf)
        {
            // 치운 자리는 다시 걸을 수 있음 (걷기 영역이 없는 장소(밭)는 발자국만 끔)
            _footprint.SetActive(false);
            if (_walkableArea != null)
                _walkableArea.Rebuild();
        }
        if (_renderer.enabled == cleared)
        {
            _renderer.enabled = !cleared;
            _tapArea.enabled = !cleared;
            if (cleared)
                ShowLeftover(1f);
        }
        if (cleared)
            return;

        if (_hint != null)
            _hint.Allowed = Interactable;
        AnimateShake();
        if (Interactable && PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
            Hit(manager, world);
    }

    private void Hit(SettlementManager manager, Vector2 world)
    {
        if (_hint != null)
            _hint.MarkDone();
        _done++;
        _shakeTimer = ShakeSeconds;
        bool tree = _kind == ObstacleKind.Tree;
        // 나무는 밑동 가까이를, 바위는 누른 자리를 찍음
        var impact = tree ? TrunkPoint(world) : (Vector3)world;
        _fx.Swing(tree, impact);
        _fx.Impact(impact, tree ? 0.5f : 0.45f);
        if (tree)
        {
            for (int i = 0; i < 3; i++)
                _fx.Burst(_fx.WoodChip, impact, new Vector2(Random.Range(-2.6f, 1f), Random.Range(2.5f, 5f)), Random.Range(0.14f, 0.22f), transform.position.y, 0.9f);
            for (int i = 0; i < 2; i++)
                _fx.Flutter(_pieceSprite, Center + (Vector3)Random.insideUnitCircle * 0.6f, Random.Range(0.2f, 0.3f), 1.4f);
        }
        else
        {
            for (int i = 0; i < 4; i++)
                _fx.Burst(_pieceSprite, world, new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6f)), Random.Range(0.12f, 0.22f), transform.position.y, 0.8f);
            _fx.Puff(impact, 0.45f, new Vector2(Random.Range(-0.3f, 0.3f), 0.4f));
        }

        if (_done < _hits)
        {
            int stage = PlazaNodeRules.CrackStage(_hits - _done, _hits, _sprites.Length);
            _renderer.sprite = _sprites[stage];
            return;
        }

        if (_projectObstacle)
            manager.TryClearProjectObstacle(_obstacleId);
        else
            manager.MarkObstacleCleared(_obstacleId);
        if (_kind == ObstacleKind.Rock)
        {
            // 큰 덩이 · 작은 조각 · 먼지가 한꺼번에, 바위는 자갈만 남기고 사라짐
            for (int i = 0; i < 5; i++)
                _fx.Burst(_pieceSprite, Center, new Vector2(Random.Range(-3.2f, 3.2f), Random.Range(3f, 6.5f)), Random.Range(0.3f, 0.45f), transform.position.y - Random.Range(0f, 0.2f), 1.1f);
            for (int i = 0; i < 6; i++)
                _fx.Burst(_pieceSprite, Center, new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 7f)), Random.Range(0.12f, 0.22f), transform.position.y, 0.9f);
            for (int i = 0; i < 4; i++)
                _fx.Puff(Center + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.2f), 0f), Random.Range(0.7f, 1f),
                    new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.3f, 0.8f)));
            ShowLeftover(0f);
        }
        else
        {
            // 그루터기가 남고 나무는 그 위에서 쓰러짐
            ShowLeftover(1f);
        }
        int added = _reward != null && _rewardAmount > 0 ? manager.GrantClearingReward(_reward, _rewardAmount) : 0;
        if (added > 0)
        {
            for (int i = 0; i < added; i++)
                _fx.Burst(_reward.Icon, Center, new Vector2(Random.Range(-2f, 2f), Random.Range(5f, 7f)), 0.45f, transform.position.y - Random.Range(0.1f, 0.4f), 1.2f);
            _fx.ShowText($"+{added} {_reward.DisplayName}", Center + Vector3.up * 0.8f, _textColor);
            RewardFly.FromWorld(_reward.Icon, Center, RewardTarget.Bag, added);
        }
        _tapArea.enabled = false;
        _fallTimer = 0f;
        _landed = false;
    }

    // 나무 밑동 높이에서 누른 쪽 (도끼가 줄기를 찍게)
    private Vector3 TrunkPoint(Vector2 world)
    {
        var b = _renderer.bounds;
        float x = Mathf.Clamp(world.x, b.center.x - b.size.x * 0.12f, b.center.x + b.size.x * 0.12f);
        return new Vector3(x, b.min.y + b.size.y * 0.22f, b.center.z);
    }

    // 치운 뒤 남는 그루터기·자갈 (장애물 그림 자리에, 같은 앞뒤 순서). alpha 0이면 나타나는 중
    private void ShowLeftover(float alpha)
    {
        if (_clearedSprite == null)
            return;
        if (_leftover == null)
        {
            var go = new GameObject("Leftover");
            go.transform.SetParent(_renderer.transform.parent, false);
            go.transform.localPosition = _basePosition;
            // 장애물 그림 폭의 일정 비율 (그루터기·자갈 그림의 해상도가 달라도 같은 크기)
            float width = _renderer.sprite != null ? _renderer.sprite.bounds.size.x * _clearedScale : 1f;
            go.transform.localScale = _baseScale * (width / Mathf.Max(0.0001f, _clearedSprite.bounds.size.x));
            _leftover = go.AddComponent<SpriteRenderer>();
            _leftover.sprite = _clearedSprite;
            _leftover.sortingLayerID = _renderer.sortingLayerID;
            _leftover.sortingOrder = _renderer.sortingOrder - 1;
        }
        var color = _leftover.color;
        color.a = alpha;
        _leftover.color = color;
        _leftover.enabled = true;
    }

    private void AnimateShake()
    {
        var visual = _renderer.transform;
        if (_shakeTimer <= 0f)
        {
            visual.localPosition = _basePosition;
            visual.localRotation = Quaternion.identity;
            return;
        }
        _shakeTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(_shakeTimer / ShakeSeconds);
        if (_kind == ObstacleKind.Rock)
            visual.localPosition = _basePosition + Vector3.right * (Mathf.Sin(_shakeTimer * 90f) * 0.06f * k);
        else
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_shakeTimer * 60f) * 5f * k);
    }

    private void AnimateFall()
    {
        _fallTimer += Time.deltaTime;
        if (_kind == ObstacleKind.Tree)
            AnimateTreeFall();
        else
            AnimateRockBreak();
    }

    // 나무: 점점 빨라지며 옆으로 쓰러지고 → 땅에 닿으면 먼지 · 나뭇잎과 함께 한 번 튕김 → 흐려져 사라짐 (그루터기는 남음)
    private void AnimateTreeFall()
    {
        var visual = _renderer.transform;
        float angle;
        if (_fallTimer < FallSeconds)
        {
            float k = _fallTimer / FallSeconds;
            angle = FallAngle * k * k;
        }
        else
        {
            if (!_landed)
            {
                _landed = true;
                Land();
            }
            float k = Mathf.Clamp01((_fallTimer - FallSeconds) / BounceSeconds);
            angle = FallAngle + 7f * Mathf.Sin(k * Mathf.PI);
        }
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);

        float fade = Mathf.Clamp01((_fallTimer - FallSeconds - BounceSeconds) / FadeSeconds);
        SetAlpha(_renderer, 1f - fade);
        if (fade < 1f)
            return;
        _renderer.enabled = false;
        visual.localRotation = Quaternion.identity;
        _fallTimer = -1f;
    }

    // 쓰러진 줄기를 따라 먼지, 잎 부분에서 나뭇잎
    private void Land()
    {
        var b = _renderer.bounds;
        float groundY = transform.position.y;
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(b.min.x + b.size.x * 0.2f, b.max.x, i / 3f);
            _fx.Puff(new Vector3(x, groundY + 0.1f, b.center.z), Random.Range(0.6f, 0.9f), new Vector2(Random.Range(-0.4f, 0.4f), 0.5f));
        }
        for (int i = 0; i < 6; i++)
            _fx.Flutter(_pieceSprite, new Vector3(Random.Range(b.center.x, b.max.x), groundY + Random.Range(0.2f, 0.8f), b.center.z), Random.Range(0.2f, 0.3f), 1.4f);
    }

    // 바위: 한 번 부풀었다가 작아지며 사라지고, 그 자리에 자갈이 떠오름
    private void AnimateRockBreak()
    {
        var visual = _renderer.transform;
        float k = Mathf.Clamp01(_fallTimer / BreakSeconds);
        float swell = k < 0.3f ? 1f + 0.12f * (k / 0.3f) : Mathf.Lerp(1.12f, 0.55f, (k - 0.3f) / 0.7f);
        visual.localScale = _baseScale * swell;
        SetAlpha(_renderer, 1f - Mathf.Clamp01((k - 0.3f) / 0.7f));
        if (_leftover != null)
            SetAlpha(_leftover, k);
        if (k < 1f)
            return;
        _renderer.enabled = false;
        visual.localScale = _baseScale;
        _fallTimer = -1f;
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        var color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
