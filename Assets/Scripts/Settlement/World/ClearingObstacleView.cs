using UnityEngine;

/// <summary>장애물 종류: 바위는 금이 가다 부서지고, 나무는 흔들리다 쓰러진다</summary>
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
    private const float FallSeconds = 0.45f;
    private const float FadeSeconds = 0.3f;

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
    private Vector3 _baseScale;
    private Vector3 _basePosition;

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
        for (int i = 0; i < 4; i++)
        {
            if (_kind == ObstacleKind.Rock)
                _fx.Burst(_pieceSprite, world, new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6f)), Random.Range(0.12f, 0.22f), transform.position.y, 0.8f);
            else
                _fx.Flutter(_pieceSprite, Center + (Vector3)Random.insideUnitCircle * 0.6f, Random.Range(0.2f, 0.3f), 1.4f);
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
            for (int i = 0; i < 8; i++)
                _fx.Burst(_pieceSprite, Center, new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 7f)), Random.Range(0.15f, 0.28f), transform.position.y, 0.9f);
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

    // 나무는 옆으로 쓰러지고, 바위는 납작해지며 부서진 뒤 흐려져 사라짐
    private void AnimateFall()
    {
        _fallTimer += Time.deltaTime;
        var visual = _renderer.transform;
        float fall = Mathf.Clamp01(_fallTimer / FallSeconds);
        if (_kind == ObstacleKind.Tree)
            visual.localRotation = Quaternion.Euler(0f, 0f, -85f * fall * fall);
        else
            visual.localScale = new Vector3(_baseScale.x * (1f + 0.2f * fall), _baseScale.y * (1f - 0.7f * fall), _baseScale.z);

        float fade = Mathf.Clamp01((_fallTimer - FallSeconds) / FadeSeconds);
        var color = _renderer.color;
        color.a = 1f - fade;
        _renderer.color = color;

        if (fade < 1f)
            return;
        _renderer.enabled = false;
        _fallTimer = -1f;
    }
}
