using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장의 바위: 여러 번 쳐서 깨면 돌이 쏟아진다.
/// - 곡괭이 레벨이 높을수록 한 번에 많이 깎음 (PlazaNodeRules.Damage)
/// - 치고 나면 가끔 약점(반짝이)이 잠깐 뜸 → 그 자리를 치면 3배
/// - 금 간 그림이 체력에 따라 바뀌고, 깨지면 자갈만 남았다가 시간이 지나면 다시 솟음 (쉬는 시간은 세이브에 남음)
/// - 깨질 때 드물게 조개
/// 깎인 체력은 앱을 켜 둔 동안만 기억한다 (다른 장소에 다녀와도 금 간 채로).
/// </summary>
public class RockNodeView : MonoBehaviour
{
    private const float ShakeSeconds = 0.15f;
    private const float PopInSeconds = 0.35f;

    // 자리별 깎인 양 (씬이 바뀌어도 유지)
    private static readonly Dictionary<string, int> Damaged = new Dictionary<string, int>();

    [Header("식별")]
    [Tooltip("세이브에 저장되는 자리 ID (광장 안에서 겹치지 않게)")]
    [SerializeField] private string _pointId;

    [Header("바위")]
    [Tooltip("곡괭이 1레벨로 몇 번 쳐야 깨지는지")]
    [SerializeField] private int _maxHp = 6;

    [Tooltip("깨지면 주는 아이템 (돌)")]
    [SerializeField] private ItemDefinition _item;

    [Tooltip("깨지면 주는 개수")]
    [SerializeField] private int _amount = 3;

    [Tooltip("깨진 뒤 다시 솟기까지 (초)")]
    [SerializeField] private float _regrowSeconds = 150f;

    [Header("약점")]
    [Tooltip("치고 나서 약점이 뜰 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _weakSpotChance = 0.4f;

    [Tooltip("약점이 떠 있는 시간 (초)")]
    [SerializeField] private float _weakSpotSeconds = 1.2f;

    [Tooltip("약점을 맞췄다고 보는 거리")]
    [SerializeField] private float _weakSpotRadius = 0.45f;

    [Tooltip("약점이 뜰 수 있는 범위 (바위 가운데 기준 반지름)")]
    [SerializeField] private Vector2 _weakSpotArea = new Vector2(0.45f, 0.25f);

    [Header("드문 수확")]
    [Tooltip("깨질 때 조개가 나올 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _rareChance = 0.05f;

    [Tooltip("드물게 나오는 재화 (조개)")]
    [SerializeField] private Currency _rareCurrency;

    [Header("그림")]
    [Tooltip("멀쩡함 → 금 감 → 많이 금 감 순서")]
    [SerializeField] private Sprite[] _crackSprites;

    [Tooltip("깨진 뒤 남는 자갈")]
    [SerializeField] private Sprite _rubbleSprite;

    [Tooltip("칠 때 튀는 돌 조각")]
    [SerializeField] private Sprite _chipSprite;

    [Header("구성 요소")]
    [Tooltip("바위 그림 (흔들림·크기 연출은 이 오브젝트에)")]
    [SerializeField] private SpriteRenderer _renderer;

    [Tooltip("약점 반짝이 (평소엔 꺼 둠)")]
    [SerializeField] private SpriteRenderer _weakSpot;

    [Tooltip("탭을 받을 영역")]
    [SerializeField] private Collider2D _tapArea;

    [SerializeField] private PlazaNodeFx _fx;

    [Header("다시 솟기까지")]
    [Tooltip("깨진 동안 자갈 위 작은 시계 (채워질수록 곧 다시 솟음)")]
    [SerializeField] private SpriteRenderer _timer;

    [Tooltip("시계 그림 (덜 참 → 다 참 순서)")]
    [SerializeField] private Sprite[] _timerSprites;

    [Header("처음 안내 (선택)")]
    [Tooltip("처음 한 번 \"톡톡 쳐서 돌을 캐요!\" (첫 바위에만, 비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Tooltip("처음 한 번 약점 옆 \"반짝이는 곳을 치면 3배!\" (비우면 없음)")]
    [SerializeField] private TapHintView _weakSpotHint;

    [Header("글자 색")]
    [SerializeField] private Color _textColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _critColor = new Color32(0xE0, 0x8A, 0x1E, 0xFF);

    private Vector3 _visualBase;
    private Vector3 _visualScale;
    private float _shakeTimer;
    private float _popInTimer;
    private float _weakSpotTimer;
    private bool _wasReady = true;

    private Vector3 Center => _renderer.bounds.center;
    private float GroundY => transform.position.y;

    private void Awake()
    {
        _visualBase = _renderer.transform.localPosition;
        _visualScale = _renderer.transform.localScale;
        _weakSpot.gameObject.SetActive(false);
    }

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        bool ready = manager.IsGatherReady(_pointId);
        if (ready && !_wasReady)
            _popInTimer = PopInSeconds;
        _wasReady = ready;
        if (_hint != null)
            _hint.Allowed = ready;
        RefreshSprite(ready);
        Animate();

        RefreshTimer(manager, ready);

        if (PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
        {
            if (ready)
                Hit(manager, world);
            else
                _fx.ShowText($"{SettlementManager.FormatShort(manager.GatherRemaining(_pointId))} 뒤에 다시 솟아요", Center + Vector3.up * 0.6f, _textColor);
        }
    }

    // 깨진 동안만 작은 시계: 남은 시간만큼 초록이 차오름
    private void RefreshTimer(SettlementManager manager, bool ready)
    {
        if (_timer.gameObject.activeSelf == ready)
            _timer.gameObject.SetActive(!ready);
        if (ready)
            return;
        float left = (float)manager.GatherRemaining(_pointId).TotalSeconds;
        float done = 1f - Mathf.Clamp01(left / _regrowSeconds);
        _timer.sprite = _timerSprites[Mathf.Min(_timerSprites.Length - 1, (int)(done * _timerSprites.Length))];
    }

    private void Hit(SettlementManager manager, Vector2 world)
    {
        int level = GameManager.Instance != null ? GameManager.Instance.MiningService.PickaxeLevel : 1;
        bool crit = _weakSpot.gameObject.activeSelf && Vector2.Distance(world, _weakSpot.transform.position) <= _weakSpotRadius;
        int damage = crit ? PlazaNodeRules.CritDamage(level) : PlazaNodeRules.Damage(level);
        if (_hint != null)
            _hint.MarkDone();
        if (crit)
        {
            if (_weakSpotHint != null)
                _weakSpotHint.MarkDone();
            HideWeakSpot();
            _fx.ShowText("딱!", (Vector3)world + Vector3.up * 0.3f, _critColor);
        }

        Damaged.TryGetValue(_pointId, out int done);
        done += damage;
        _shakeTimer = ShakeSeconds;

        int chips = crit ? 8 : 4;
        for (int i = 0; i < chips; i++)
            _fx.Burst(_chipSprite, world, new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6f)), Random.Range(0.12f, 0.22f), GroundY, 0.8f);

        if (done < _maxHp)
        {
            Damaged[_pointId] = done;
            if (!crit && !_weakSpot.gameObject.activeSelf && Random.value < _weakSpotChance)
                ShowWeakSpot();
            return;
        }

        Break(manager, done);
    }

    private void Break(SettlementManager manager, int done)
    {
        int added = manager.TryGather(_pointId, _item, _amount, _regrowSeconds);
        if (added <= 0)
        {
            // 가방이 꽉 참: 깨지기 직전에서 멈춤
            Damaged[_pointId] = _maxHp - 1;
            _fx.ShowText("가방이 꽉 찼어요", Center + Vector3.up * 0.6f, _textColor);
            return;
        }

        Damaged.Remove(_pointId);
        HideWeakSpot();
        for (int i = 0; i < 10; i++)
            _fx.Burst(_chipSprite, Center, new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(4f, 8f)), Random.Range(0.15f, 0.3f), GroundY, 1f);
        for (int i = 0; i < added; i++)
            _fx.Burst(_item.Icon, Center, new Vector2(Random.Range(-2f, 2f), Random.Range(5f, 7f)), 0.45f, GroundY - Random.Range(0.1f, 0.4f), 1.2f);
        _fx.ShowText($"+{added} {_item.DisplayName}", Center + Vector3.up * 0.6f, _textColor);
        RewardFly.FromWorld(_item.Icon, Center, RewardTarget.Bag, added);

        if (_rareCurrency != null && Random.value < _rareChance)
        {
            manager.GrantPlazaFind(_rareCurrency, 1);
            _fx.Burst(_rareCurrency.Icon, Center, new Vector2(0f, 7f), 0.5f, GroundY - 0.2f, 1.4f);
            _fx.ShowText($"반짝! {_rareCurrency.DisplayName} +1", Center + Vector3.up * 1.2f, _critColor);
            RewardFly.FromWorld(_rareCurrency.Icon, Center, RewardTarget.Gem, 1);
        }
        _wasReady = false;
    }

    private void ShowWeakSpot()
    {
        var center = Center;
        var offset = Random.insideUnitCircle;
        _weakSpot.transform.position = new Vector3(center.x + offset.x * _weakSpotArea.x, center.y + offset.y * _weakSpotArea.y, center.z);
        _weakSpot.gameObject.SetActive(true);
        _weakSpotTimer = _weakSpotSeconds;
    }

    private void HideWeakSpot()
    {
        _weakSpot.gameObject.SetActive(false);
        _weakSpotTimer = 0f;
    }

    private void RefreshSprite(bool ready)
    {
        Sprite sprite;
        if (!ready)
        {
            sprite = _rubbleSprite;
        }
        else
        {
            Damaged.TryGetValue(_pointId, out int done);
            sprite = _crackSprites[PlazaNodeRules.CrackStage(_maxHp - done, _maxHp, _crackSprites.Length)];
        }
        if (_renderer.sprite != sprite)
            _renderer.sprite = sprite;
        if (!ready && _weakSpot.gameObject.activeSelf)
            HideWeakSpot();
    }

    private void Animate()
    {
        float dt = Time.deltaTime;
        var visual = _renderer.transform;

        // 맞으면 좌우로 떨고 살짝 찌그러짐
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= dt;
            float k = Mathf.Clamp01(_shakeTimer / ShakeSeconds);
            visual.localPosition = _visualBase + Vector3.right * (Mathf.Sin(_shakeTimer * 90f) * 0.06f * k);
            visual.localScale = new Vector3(_visualScale.x * (1f + 0.05f * k), _visualScale.y * (1f - 0.08f * k), _visualScale.z);
        }
        else if (_popInTimer > 0f)
        {
            // 다시 솟을 때 통통 튀며 커짐
            _popInTimer -= dt;
            float k = 1f - Mathf.Clamp01(_popInTimer / PopInSeconds);
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
            visual.localScale = new Vector3(_visualScale.x * s * Mathf.Lerp(0.6f, 1f, k), _visualScale.y * s * Mathf.Lerp(0.6f, 1f, k), _visualScale.z);
            visual.localPosition = _visualBase;
        }
        else
        {
            visual.localPosition = _visualBase;
            visual.localScale = _visualScale;
        }

        if (_weakSpotTimer > 0f)
        {
            _weakSpotTimer -= dt;
            float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.15f;
            _weakSpot.transform.localScale = Vector3.one * pulse;
            _weakSpot.transform.Rotate(0f, 0f, 90f * dt);
            if (_weakSpotTimer <= 0f)
                HideWeakSpot();
        }
    }
}
