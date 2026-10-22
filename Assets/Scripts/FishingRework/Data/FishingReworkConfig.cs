using System;
using System.Collections.Generic;
using UnityEngine;

public enum FishingUpgrade { Line, Sinker, Reel }

/// <summary>물층: 물고기가 사는 깊이로 나눔 (구역마다 물층별로 마릿수 · 다시 오는 시간이 따로)</summary>
public enum FishLayer { Shallow, Mid, Deep, Bottom }

/// <summary>강화 재료 한 줄: 이 물고기 몇 마리</summary>
public class FishCost
{
    public FishDefinition Fish;
    public int Amount;
}

/// <summary>강화 한 줄 (단계마다 값, 다음 단계 비용)</summary>
[Serializable]
public class FishingUpgradeTrack
{
    [SerializeField] private string _displayName = "";
    [SerializeField] private string _effectName = "";
    [SerializeField] private string _unit = "";
    [SerializeField] private Sprite _icon;
    [Tooltip("단계마다 값 (0단계 = 처음)")]
    [SerializeField] private float[] _values = { 1f };
    [Tooltip("(예전) 다음 단계로 가는 골드. 지금은 강화 재료가 물고기라 쓰지 않음 (FishingReworkConfig.RecipeFor)")]
    [SerializeField] private int[] _costs = new int[0];

    public string DisplayName => _displayName;
    public string EffectName => _effectName;
    public string Unit => _unit;
    public Sprite Icon => _icon;
    public int MaxLevel => Mathf.Max(0, _values.Length - 1);
    public float ValueAt(int level) => _values[Mathf.Clamp(level, 0, _values.Length - 1)];
    public int CostAt(int level) => level >= 0 && level < _costs.Length && level < MaxLevel ? _costs[level] : 0;

    public FishingUpgradeTrack() { }

#if UNITY_EDITOR
    public FishingUpgradeTrack(string name, string effect, string unit, Sprite icon, float[] values, int[] costs)
    {
        _displayName = name;
        _effectName = effect;
        _unit = unit;
        _icon = icon;
        _values = values;
        _costs = costs;
    }
#endif
}

/// <summary>
/// 낚시 개편 규칙 수치: 물고기 표, 강화 세 가지(줄 길이 · 찌 무게 · 릴), 바다 모양, 줄 움직임, 물고기 수, 의뢰.
/// 단위는 m · 초 · kg. 수면 = 0, 아래가 −. 선착장 끝 = x 0, 바다 쪽이 +
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Rework/Config", fileName = "FishingReworkConfig")]
public class FishingReworkConfig : ScriptableObject
{
    [SerializeField] private List<FishDefinition> _fish = new List<FishDefinition>();

    [Header("강화 (재료는 물고기 — RecipeFor)")]
    [Tooltip("줄 길이 = 던지는 거리 (m)")]
    [SerializeField] private FishingUpgradeTrack _line = new FishingUpgradeTrack();
    [Tooltip("찌 무게 = 내려갈 수 있는 깊이 (m). 무거울수록 깊이 들어감")]
    [SerializeField] private FishingUpgradeTrack _sinker = new FishingUpgradeTrack();
    [Tooltip("릴 = 낚을 수 있는 무게 (kg)")]
    [SerializeField] private FishingUpgradeTrack _reel = new FishingUpgradeTrack();

    [Header("바다")]
    [Tooltip("낚싯대 끝 높이 (수면 위)")]
    [SerializeField] private float _rodTipHeight = 2.4f;
    [Tooltip("선착장 바로 앞 바닥 깊이")]
    [SerializeField] private float _seabedStart = 8f;
    [Tooltip("1m 멀어질 때마다 깊어지는 정도")]
    [SerializeField] private float _seabedSlope = 0.5f;
    [SerializeField] private float _seabedMax = 58f;
    [Tooltip("물고기가 사는 바다 끝 (m)")]
    [SerializeField] private float _seaLength = 95f;

    [Header("줄")]
    [Tooltip("던져서 물에 닿기까지 (초)")]
    [SerializeField] private float _castSeconds = 1.1f;
    [Tooltip("가만히 있을 때 선착장 쪽으로 저절로 감기는 빠르기 (m/초)")]
    [SerializeField] private float _autoReel = 1.5f;
    [Tooltip("누르고 있을 때 감기는 빠르기")]
    [SerializeField] private float _holdReel = 2.4f;
    [Tooltip("누르고 있을 때 바늘이 올라오는 빠르기 (m/초)")]
    [SerializeField] private float _liftSpeed = 3f;
    [Tooltip("손을 떼면 가라앉는 빠르기 = 이 값 + 찌 단계 × 아래 값 (m/초)")]
    [SerializeField] private float _sinkSpeed = 2.2f;
    [SerializeField] private float _sinkSpeedPerLevel = 0.35f;
    [Tooltip("물고기가 올라올 수 있는 가장 얕은 깊이 (던지는 도중 · 물에 닿자마자 걸리지 않게)")]
    [SerializeField] private float _fishTopDepth = 2.5f;
    [Tooltip("물고기가 걸린 뒤 감는 빠르기 (누르면 1.5배)")]
    [SerializeField] private float _hookedReel = 6.5f;
    [Tooltip("던진 줄이 저절로 감겨 돌아오는 데 걸리는 초 (줄이 길면 그만큼 빨리 감김, _autoReel보다 느려지지는 않음). 0이면 늘 _autoReel")]
    [SerializeField] private float _sweepSeconds = 9f;
    [Tooltip("걸린 물고기를 끝에서 끌어오는 데 걸리는 초 (줄이 길면 그만큼 빨리, _hookedReel보다 느려지지는 않음). 0이면 늘 _hookedReel")]
    [SerializeField] private float _landSeconds = 4f;
    [Tooltip("바늘 앞(선착장 쪽) 이만큼 안에 낚을 수 있는 물고기가 있으면 천천히 감김 (m)")]
    [SerializeField] private float _nearFishAhead = 3f;
    [Tooltip("물고기 근처에서 감기는 속도 (m/초) — 처음 낚싯대와 비슷하게, 노리기 쉽게")]
    [SerializeField] private float _nearFishReel = 1.6f;
    [Tooltip("이만큼 선착장 가까이 오면 끝 (m)")]
    [SerializeField] private float _landDistance = 0.6f;
    [SerializeField] private float _hookRadius = 0.15f;
    [Tooltip("물고기 몸에서 바늘에 닿는 부분 = 화면 길이 × 이 값 (반지름)")]
    [SerializeField] private float _fishHitScale = 0.25f;
    [Tooltip("이 안의 물고기는 바늘 쪽으로 다가옴 (0 = 안 다가옴)")]
    [SerializeField] private float _attractRadius = 0f;

    [Header("물고기 수 (구역 = 거리 칸 × 물층)")]
    [Tooltip("바다를 이 길이로 나눠 칸마다 물층별로 물고기를 채움")]
    [SerializeField] private float _segmentLength = 8f;
    [Tooltip("물층마다 한 칸에 최대 몇 마리 (얕은 물 · 중간 물 · 깊은 물 · 바닥)")]
    [SerializeField] private int[] _layerCaps = { 3, 3, 2, 2 };
    [Tooltip("물층마다 모자랄 때 한 마리씩 새로 오는 시간 (초). 빨라야 잡아도 쉬는 시간이 없음")]
    [SerializeField] private float[] _layerRegenSeconds = { 8f, 8f, 10f, 10f };

    [Header("의뢰")]
    [Tooltip("의뢰 보상 = 물고기 값 합 × 이 값")]
    [SerializeField] private float _requestRewardFactor = 1.8f;
    [Tooltip("의뢰 바꾸기를 다시 누를 수 있기까지 (초)")]
    [SerializeField] private float _requestSkipCooldown = 30f;

    public IReadOnlyList<FishDefinition> Fish => _fish;
    public FishingUpgradeTrack Track(FishingUpgrade upgrade) =>
        upgrade == FishingUpgrade.Line ? _line : upgrade == FishingUpgrade.Sinker ? _sinker : _reel;

    public float RodTipHeight => _rodTipHeight;
    public float SeaLength => _seaLength;
    public float CastSeconds => _castSeconds;
    public float AutoReel => _autoReel;
    public float HoldReel => _holdReel;

    /// <summary>던진 거리에 맞춘 저절로 감기는 속도 (m/초): 한 번 쓸고 오는 데 _sweepSeconds쯤</summary>
    public float AutoReelFor(float castDistance) => _sweepSeconds > 0f ? Mathf.Max(_autoReel, castDistance / _sweepSeconds) : _autoReel;

    /// <summary>누르고 있을 때 감기는 속도 (저절로 감기는 속도의 _holdReel/_autoReel배)</summary>
    public float HoldReelFor(float castDistance) => AutoReelFor(castDistance) * (_autoReel > 0f ? _holdReel / _autoReel : 1.6f);

    public float NearFishAhead => _nearFishAhead;
    public float NearFishReel => _nearFishReel;

    /// <summary>걸린 물고기를 끌어오는 속도 (m/초)</summary>
    public float HookedReelFor(float castDistance) => _landSeconds > 0f ? Mathf.Max(_hookedReel, castDistance / _landSeconds) : _hookedReel;
    public float LiftSpeed => _liftSpeed;

    /// <summary>누르면 올라오는 속도: 찌가 무거워 빨리 가라앉는 만큼 올라오는 것도 빨라짐 (깊은 데서 물고기 깊이로 맞추기 쉽게)</summary>
    public float LiftSpeedAt(int sinkerLevel) => _liftSpeed + _sinkSpeedPerLevel * 0.8f * Mathf.Max(0, sinkerLevel);
    public float SinkSpeedAt(int sinkerLevel) => _sinkSpeed + _sinkSpeedPerLevel * Mathf.Max(0, sinkerLevel);
    public float FishTopDepth => _fishTopDepth;
    public float FishHitScale => _fishHitScale;
    public float HookedReel => _hookedReel;
    public float LandDistance => _landDistance;
    public float HookRadius => _hookRadius;
    public float AttractRadius => _attractRadius;
    public float SegmentLength => Mathf.Max(1f, _segmentLength);
    public int LayerCap(FishLayer layer) => _layerCaps != null && (int)layer < _layerCaps.Length ? Mathf.Max(0, _layerCaps[(int)layer]) : 0;
    public float LayerRegenSeconds(FishLayer layer) =>
        _layerRegenSeconds != null && (int)layer < _layerRegenSeconds.Length ? Mathf.Max(1f, _layerRegenSeconds[(int)layer]) : 30f;
    public float RequestRewardFactor => _requestRewardFactor;
    public float RequestSkipCooldown => _requestSkipCooldown;

    /// <summary>x(m)에서 바닥 깊이 (+). 멀수록 깊고, 조금 울퉁불퉁</summary>
    public float SeabedDepth(float x)
    {
        float d = _seabedStart + _seabedSlope * Mathf.Max(0f, x) + 0.6f * Mathf.Sin(x * 0.35f) + 0.3f * Mathf.Sin(x * 0.93f + 1.7f);
        return Mathf.Clamp(d, 1.5f, _seabedMax);
    }

    /// <summary>물고기 종류의 물층 (바닥에 살면 바닥, 아니면 가장 얕게 사는 깊이로)</summary>
    public static FishLayer LayerOf(FishDefinition fish) =>
        fish.NearBottom ? FishLayer.Bottom : fish.MinDepth < 6f ? FishLayer.Shallow : fish.MinDepth < 15f ? FishLayer.Mid : FishLayer.Deep;

    /// <summary>강화 단계로 이 물고기를 낚을 수 있는지 (닿는 거리 · 찌 깊이 · 릴 무게). 방치 낚시 · 꺼 둔 동안이 씀</summary>
    public bool Catchable(FishDefinition fish, int lineLevel, int sinkerLevel, int reelLevel) =>
        CatchableWith(fish, _line.ValueAt(lineLevel), _sinker.ValueAt(sinkerLevel), _reel.ValueAt(reelLevel));

    // 바다 칸이 시작하는 거리 (FishingReworkModel과 같음)
    public const float SegmentStartX = 1.5f;

    /// <summary>
    /// 이 강화 단계(셋 다 level)에서 바늘이 닿는 바다에 이 물고기가 어림잡아 몇 마리 있는지:
    /// 닿는 칸마다 (그 물층 칸의 최대 마릿수 × 같은 물층 · 같은 칸에 사는 물고기 중 이 물고기의 비율). 강화 재료를 고를 때
    /// "닿기는 하지만 실제로는 거의 안 만나는" 물고기를 빼려고 씀
    /// </summary>
    public float PresenceAt(FishDefinition fish, int level)
    {
        if (fish == null || !Catchable(fish, level, level, level))
            return 0f;
        float reach = _line.ValueAt(level), depth = _sinker.ValueAt(level);
        var layer = LayerOf(fish);
        float total = 0f;
        for (float from = SegmentStartX; from < reach - 1f; from += SegmentLength)
        {
            float to = from + SegmentLength;
            if (!FitsSegment(fish, from, to))
                continue;
            float x = Mathf.Clamp((from + Mathf.Min(to, reach)) * 0.5f, fish.MinDistance, fish.MaxDistance);
            float seabed = SeabedDepth(x);
            float shallowest = fish.NearBottom ? seabed - fish.MaxDepth : Mathf.Max(fish.MinDepth, _fishTopDepth);
            if (shallowest > depth)
                continue;
            // 사는 깊이 중 찌가 닿는 만큼만 (끝자락에만 닿으면 적게)
            float deepest = fish.NearBottom ? seabed : Mathf.Max(shallowest, Mathf.Min(fish.MaxDepth, seabed - 0.5f));
            float band = deepest > shallowest + 0.01f ? Mathf.Clamp01((Mathf.Min(depth, deepest) - shallowest) / (deepest - shallowest)) : 1f;
            float sum = 0f;
            foreach (var other in _fish)
            {
                if (other != null && LayerOf(other) == layer && FitsSegment(other, from, to))
                    sum += other.SpawnWeight;
            }
            if (sum <= 0f)
                continue;
            // 바늘이 칸의 일부까지만 닿으면 그만큼만
            float overlap = Mathf.Clamp01((Mathf.Min(to, reach - 1f, fish.MaxDistance) - Mathf.Max(from, fish.MinDistance)) / SegmentLength);
            total += LayerCap(layer) * fish.SpawnWeight / sum * overlap * Mathf.Max(0.15f, band);
        }
        return total;
    }

    // 이 칸에 살 수 있는지 (FishingReworkModel.FitsSegment와 같은 규칙)
    private bool FitsSegment(FishDefinition fish, float from, float to)
    {
        if (fish.MaxDistance < from || fish.MinDistance > to)
            return false;
        if (fish.NearBottom)
            return true;
        float x = Mathf.Clamp((from + to) * 0.5f, fish.MinDistance, fish.MaxDistance);
        return fish.MinDepth <= SeabedDepth(x) - 0.5f;
    }

    // 강화 재료로 쓸 만큼 자주 만나는 물고기: 그 단계에서 가장 많이 있는 물고기의 이만큼 이상
    private const float MinPresenceShare = 0.4f;
    private const float MainPresenceShare = 0.2f;

    private float MinPresence(int tier, float share = MinPresenceShare)
    {
        float most = 0f;
        foreach (var fish in _fish)
        {
            if (fish != null && fish.Rarity <= FishRarity.Rare)
                most = Mathf.Max(most, PresenceAt(fish, tier));
        }
        return most * share;
    }

    private bool CatchableWith(FishDefinition fish, float reach, float depth, float reel)
    {
        if (fish == null)
            return false;
        if (fish.MinDistance > reach - 1f || fish.MinWeight > reel)
            return false;
        float to = Mathf.Min(reach, fish.MaxDistance);
        for (float x = fish.MinDistance; x <= to + 0.01f; x += 1f)
        {
            float seabed = SeabedDepth(x);
            bool lives = fish.NearBottom || fish.MinDepth <= seabed - 0.5f;
            float shallowest = fish.NearBottom ? seabed - fish.MaxDepth : Mathf.Max(fish.MinDepth, _fishTopDepth);
            if (lives && shallowest <= depth)
                return true;
        }
        return false;
    }

    /// <summary>방치 낚시 (낚시꾼 해달 · 꺼 둔 동안): 낚을 수 있는 물고기 중 나오는 비율대로 한 마리 (roll = 0~1)</summary>
    public FishDefinition RollCatchable(int lineLevel, int sinkerLevel, int reelLevel, double roll)
    {
        float total = 0f;
        foreach (var fish in _fish)
        {
            if (Catchable(fish, lineLevel, sinkerLevel, reelLevel))
                total += fish.SpawnWeight;
        }
        if (total <= 0f)
            return null;
        double pick = roll * total;
        FishDefinition last = null;
        foreach (var fish in _fish)
        {
            if (!Catchable(fish, lineLevel, sinkerLevel, reelLevel))
                continue;
            last = fish;
            pick -= fish.SpawnWeight;
            if (pick < 0)
                return fish;
        }
        return last;
    }

    #region 강화 재료

    private readonly Dictionary<(FishingUpgrade, int), List<FishCost>> _recipes = new Dictionary<(FishingUpgrade, int), List<FishCost>>();

    /// <summary>
    /// 이 강화를 targetLevel(1부터)로 올리는 재료 (가방에서 냄). 그 아래 단계(targetLevel − 1)를 셋 다 갖춘 낚시로 닿는 물고기 중에서:
    /// 주재료 = 강화와 어울리는 물고기 (줄 → 가장 멀리 · 찌 → 가장 깊이 · 릴 → 가장 무거운, 같은 단계의 세 강화는 서로 다른 물고기),
    /// 부재료 = 그 단계에서 새로 닿은 물고기 중 가장 흔한 것 (없으면 닿는 것 중 흔한 것).
    /// 가장 낮은 강화의 재료는 늘 지금 낚을 수 있어 막히지 않음. 같은 단계면 늘 같은 재료 (표를 따로 두지 않음)
    /// </summary>
    public IReadOnlyList<FishCost> RecipeFor(FishingUpgrade upgrade, int targetLevel)
    {
        var key = (upgrade, targetLevel);
        if (_recipes.TryGetValue(key, out var cached))
            return cached;
        if (targetLevel >= 1 && targetLevel <= Track(upgrade).MaxLevel)
            BuildTier(targetLevel - 1);
        if (!_recipes.TryGetValue(key, out cached))
        {
            cached = new List<FishCost>();
            _recipes[key] = cached;
        }
        return cached;
    }

    // 한 단계(tier → tier + 1)의 세 강화 재료를 한 번에 (주재료가 겹치지 않게 줄 → 찌 → 릴 순서로 고름)
    private void BuildTier(int tier)
    {
        var pool = new List<FishDefinition>();
        var fresh = new List<FishDefinition>();
        float minNow = MinPresence(tier), minBefore = tier > 0 ? MinPresence(tier - 1) : 0f, minMain = MinPresence(tier, MainPresenceShare);
        var mainPool = new List<FishDefinition>();
        foreach (var fish in _fish)
        {
            if (fish != null && fish.Rarity <= FishRarity.Rare && PresenceAt(fish, tier) >= minMain && fish.MaxDistance >= _line.ValueAt(tier) * 0.6f)
                mainPool.Add(fish);
        }
        foreach (var fish in _fish)
        {
            // 재료는 닿는 바다에 실제로 자주 있는 물고기 + 지금 줄 길이에 맞는 물고기 (선착장 바로 앞에만 사는 옛 물고기는 긴 줄로 노리기 어려움)
            if (fish == null || fish.Rarity > FishRarity.Rare || PresenceAt(fish, tier) < minNow
                || fish.MaxDistance < _line.ValueAt(tier) * 0.6f)
                continue;
            pool.Add(fish);
            if (tier == 0 || PresenceAt(fish, tier - 1) < minBefore)
                fresh.Add(fish);
        }
        if (pool.Count == 0)
        {
            foreach (var fish in _fish)
            {
                if (fish != null && fish.Rarity <= FishRarity.Rare && Catchable(fish, tier, tier, tier))
                    pool.Add(fish);
            }
        }
        if (pool.Count == 0)
        {
            foreach (var fish in _fish)
            {
                if (fish != null && (pool.Count == 0 || fish.SpawnWeight > pool[0].SpawnWeight))
                {
                    pool.Clear();
                    pool.Add(fish);
                }
            }
        }

        var mains = new List<FishDefinition>();
        foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
        {
            var recipe = new List<FishCost>();
            _recipes[(upgrade, tier + 1)] = recipe;
            if (pool.Count == 0 || tier + 1 > Track(upgrade).MaxLevel)
                continue;

            FishDefinition main = null;
            var mainFrom = mainPool.Count > 0 ? mainPool : pool;
            foreach (var fish in mainFrom)
            {
                if (mains.Contains(fish) && mainFrom.Count > mains.Count)
                    continue;
                if (main == null || Theme(upgrade, fish) > Theme(upgrade, main)
                    || (Mathf.Approximately(Theme(upgrade, fish), Theme(upgrade, main)) && fish.SpawnWeight > main.SpawnWeight))
                    main = fish;
            }
            mains.Add(main);
            recipe.Add(new FishCost { Fish = main, Amount = AmountFor(main.Rarity, tier) });

            var side = MostPresent(fresh, main, tier) ?? MostPresent(pool, main, tier);
            if (side != null)
                recipe.Add(new FishCost { Fish = side, Amount = AmountFor(side.Rarity, tier) + 1 });
        }
    }

    private FishDefinition MostPresent(List<FishDefinition> fish, FishDefinition except, int tier)
    {
        FishDefinition best = null;
        float bestPresence = 0f;
        foreach (var f in fish)
        {
            float presence = PresenceAt(f, tier);
            if (f != except && (best == null || presence > bestPresence))
            {
                best = f;
                bestPresence = presence;
            }
        }
        return best;
    }

    // 강화와 어울리는 정도: 줄은 멀리 사는 물고기, 찌는 깊이 사는 물고기, 릴은 무거운 물고기
    private static float Theme(FishingUpgrade upgrade, FishDefinition fish) =>
        upgrade == FishingUpgrade.Line ? fish.MinDistance
        : upgrade == FishingUpgrade.Sinker ? (fish.NearBottom ? fish.MinDistance * 0.5f + 10f : fish.MinDepth)
        : fish.MinWeight;

    // 귀할수록 적게, 높은 단계일수록 많이
    private static int AmountFor(FishRarity rarity, int tier)
    {
        switch (rarity)
        {
            case FishRarity.Common: return 3 + tier / 2;
            case FishRarity.Uncommon: return 2 + tier / 3;
            case FishRarity.Rare: return 1 + tier / 4;
            default: return 1;
        }
    }

    #endregion

    public FishDefinition FindByItem(string itemId)
    {
        foreach (var fish in _fish)
        {
            if (fish != null && fish.Item != null && fish.Item.ItemId == itemId)
                return fish;
        }
        return null;
    }

    public FishDefinition FindFish(string id)
    {
        foreach (var fish in _fish)
        {
            if (fish != null && fish.FishId == id)
                return fish;
        }
        return null;
    }

#if UNITY_EDITOR
    public void SetFish(List<FishDefinition> fish)
    {
        _fish = fish;
        _recipes.Clear();
    }

    public void SetReelTimes(float sweepSeconds, float landSeconds)
    {
        _sweepSeconds = sweepSeconds;
        _landSeconds = landSeconds;
    }

    public void SetLayers(int[] caps, float[] regenSeconds)
    {
        _layerCaps = caps;
        _layerRegenSeconds = regenSeconds;
    }

    public void SetTracks(FishingUpgradeTrack line, FishingUpgradeTrack sinker, FishingUpgradeTrack reel)
    {
        _line = line;
        _sinker = sinker;
        _reel = reel;
        _recipes.Clear();
    }
#endif
}
