using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장의 나무: 누르면 흔들리며 나뭇가지(목재)가 떨어지고, 가끔 열매도 떨어진다.
/// 정해진 횟수를 흔들면(마지막엔 더 많이) 잎이 성긴 그림으로 바뀌어 "쉬는 중"이 되고, 시간이 지나면 다시 흔들 수 있다 (세이브에 남음).
/// 흔든 횟수는 앱을 켜 둔 동안만 기억한다 (다른 장소에 다녀와도 이어서).
/// </summary>
public class TreeShakeView : MonoBehaviour
{
    private const float SwaySeconds = 0.8f;

    // 자리별 이번에 흔든 횟수 (씬이 바뀌어도 유지)
    private static readonly Dictionary<string, int> Shaken = new Dictionary<string, int>();

    [Header("식별")]
    [Tooltip("세이브에 저장되는 자리 ID (광장 안에서 겹치지 않게)")]
    [SerializeField] private string _pointId;

    [Header("흔들기")]
    [Tooltip("쉬기 전까지 흔들 수 있는 횟수")]
    [SerializeField] private int _shakesPerRest = 3;

    [Tooltip("한 번 흔들 때 떨어지는 목재")]
    [SerializeField] private int _woodPerShake = 1;

    [Tooltip("마지막 흔들기에 더 떨어지는 목재")]
    [SerializeField] private int _lastShakeBonus = 1;

    [Tooltip("다 흔든 뒤 쉬는 시간 (초)")]
    [SerializeField] private float _restSeconds = 120f;

    [Tooltip("떨어지는 목재 (비우면 설정의 기본값)")]
    [SerializeField] private ItemDefinition _wood;

    [Header("열매")]
    [Tooltip("흔들 때마다 열매가 떨어질 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _fruitChance = 0.25f;

    [Tooltip("떨어지는 열매")]
    [SerializeField] private ItemDefinition _fruit;

    [Header("그림")]
    [Tooltip("떨어지는 나뭇가지")]
    [SerializeField] private Sprite _branchSprite;

    [Tooltip("흩날리는 나뭇잎")]
    [SerializeField] private Sprite _leafSprite;

    [Tooltip("흔들 수 있을 때 (사과가 달린 나무)")]
    [SerializeField] private Sprite _readySprite;

    [Tooltip("다 흔든 뒤 쉬는 동안 (잎이 성긴 나무)")]
    [SerializeField] private Sprite _restSprite;

    [Header("구성 요소")]
    [Tooltip("나무 그림 (피벗이 밑동이라 이걸 기울여 흔듦)")]
    [SerializeField] private SpriteRenderer _renderer;

    [Tooltip("탭을 받을 영역")]
    [SerializeField] private Collider2D _tapArea;

    [SerializeField] private PlazaNodeFx _fx;

    [Header("다시 흔들 수 있기까지")]
    [Tooltip("쉬는 동안 밑동 옆 작은 시계 (채워질수록 곧 다시 흔들 수 있음)")]
    [SerializeField] private SpriteRenderer _timer;

    [Tooltip("시계 그림 (덜 참 → 다 참 순서)")]
    [SerializeField] private Sprite[] _timerSprites;

    [Header("처음 안내 (선택)")]
    [Tooltip("처음 한 번 \"흔들면 나뭇가지가 떨어져요!\" (첫 나무에만, 비우면 없음)")]
    [SerializeField] private TapHintView _hint;

    [Header("글자 색")]
    [SerializeField] private Color _textColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _fruitColor = new Color32(0xD9, 0x4F, 0x45, 0xFF);

    private float _swayTimer;
    private float _swayAmplitude;

    private float GroundY => transform.position.y;

    private void Update()
    {
        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded)
            return;

        bool ready = manager.IsGatherReady(_pointId);
        if (_hint != null)
            _hint.Allowed = ready;
        var sprite = ready ? _readySprite : _restSprite;
        if (_renderer.sprite != sprite)
            _renderer.sprite = sprite;
        RefreshTimer(manager, ready);
        Sway();

        if (PlazaTapInput.TryGetTap(out Vector2 world) && _tapArea.OverlapPoint(world))
        {
            if (ready)
                Shake(manager);
            else
                Rest(manager);
        }
    }

    private void Shake(SettlementManager manager)
    {
        if (_hint != null)
            _hint.MarkDone();
        var wood = _wood != null ? _wood : manager.Config.GatherItem;
        Shaken.TryGetValue(_pointId, out int index);
        index = Mathf.Clamp(index, 0, _shakesPerRest - 1);
        bool last = index == _shakesPerRest - 1;
        int amount = PlazaNodeRules.ShakeWood(index, _shakesPerRest, _woodPerShake, _lastShakeBonus);

        int added = manager.TryGather(_pointId, wood, amount, last ? _restSeconds : 0f);
        StartSway(last ? 9f : 6f);
        Leaves(last ? 8 : 4);
        if (added <= 0)
        {
            _fx.ShowText("가방이 꽉 찼어요", TextPoint, _textColor);
            return;
        }

        if (last)
            Shaken.Remove(_pointId);
        else
            Shaken[_pointId] = index + 1;

        for (int i = 0; i < added; i++)
            Drop(_branchSprite, 0.6f);
        RewardFly.FromWorld(wood.Icon, TextPoint, RewardTarget.Bag, added);
        string text = $"+{added} {wood.DisplayName}";

        if (_fruit != null && Random.value < _fruitChance && manager.GatherExtra(_fruit, 1) > 0)
        {
            Drop(_fruit.Icon, 0.5f);
            RewardFly.FromWorld(_fruit.Icon, TextPoint, RewardTarget.Bag, 1);
            _fx.ShowText($"+1 {_fruit.DisplayName}", TextPoint + Vector3.up * 0.5f, _fruitColor);
        }
        _fx.ShowText(text, TextPoint, _textColor);
    }

    // 쉬는 중에 누르면 살짝만 흔들리고 언제 다시 흔들 수 있는지 알려 줌
    private void Rest(SettlementManager manager)
    {
        StartSway(2f);
        Leaves(1);
        _fx.ShowText($"{SettlementManager.FormatShort(manager.GatherRemaining(_pointId))} 뒤에 다시 흔들 수 있어요", TextPoint, _textColor);
    }

    // 쉬는 동안만 작은 시계: 남은 시간만큼 초록이 차오름
    private void RefreshTimer(SettlementManager manager, bool ready)
    {
        if (_timer.gameObject.activeSelf == ready)
            _timer.gameObject.SetActive(!ready);
        if (ready)
            return;
        float left = (float)manager.GatherRemaining(_pointId).TotalSeconds;
        float done = 1f - Mathf.Clamp01(left / _restSeconds);
        _timer.sprite = _timerSprites[Mathf.Min(_timerSprites.Length - 1, (int)(done * _timerSprites.Length))];
    }

    private Vector3 TextPoint
    {
        get
        {
            var b = _renderer.bounds;
            return new Vector3(b.center.x, b.max.y - b.size.y * 0.15f, b.center.z);
        }
    }

    // 가지·열매: 나뭇잎 사이에서 떨어져 밑동 근처에 튕김
    private void Drop(Sprite sprite, float size)
    {
        var b = _renderer.bounds;
        var from = new Vector3(Random.Range(b.min.x + b.size.x * 0.25f, b.max.x - b.size.x * 0.25f), b.min.y + b.size.y * 0.55f, b.center.z);
        _fx.Burst(sprite, from, new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 2f)), size, GroundY - Random.Range(0f, 0.5f), 1.3f);
    }

    private void Leaves(int count)
    {
        var b = _renderer.bounds;
        for (int i = 0; i < count; i++)
        {
            var from = new Vector3(Random.Range(b.min.x + b.size.x * 0.15f, b.max.x - b.size.x * 0.15f),
                Random.Range(b.min.y + b.size.y * 0.5f, b.max.y - b.size.y * 0.1f), b.center.z);
            _fx.Flutter(_leafSprite, from, Random.Range(0.2f, 0.3f), Random.Range(1.2f, 1.8f));
        }
    }

    private void StartSway(float degrees)
    {
        _swayTimer = SwaySeconds;
        _swayAmplitude = degrees;
    }

    // 흔들면 밑동을 축으로 좌우로 흔들리다 잦아듦 (평소 바람 살랑임은 셰이더 IdleSway가)
    private void Sway()
    {
        if (_swayTimer <= 0f)
            return;
        _swayTimer -= Time.deltaTime;
        float t = SwaySeconds - _swayTimer;
        float angle = _swayTimer > 0f ? _swayAmplitude * Mathf.Sin(t * 22f) * Mathf.Exp(-t * 4.5f) : 0f;
        _renderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
