using System;
using System.Collections.Generic;
using UnityEngine;

public enum FishingUpgrade { Line, Sinker, Reel }

/// <summary>물층: 물고기가 사는 깊이로 나눔 (구역마다 물층별로 마릿수 · 다시 오는 시간이 따로)</summary>
public enum FishLayer { Shallow, Mid, Deep, Bottom }

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
    [Tooltip("다음 단계로 가는 골드 (길이 = 단계 수 − 1)")]
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

    [Header("강화 (골드)")]
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
    public float LiftSpeed => _liftSpeed;
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
    public bool Catchable(FishDefinition fish, int lineLevel, int sinkerLevel, int reelLevel)
    {
        if (fish == null)
            return false;
        float reach = _line.ValueAt(lineLevel), depth = _sinker.ValueAt(sinkerLevel), reel = _reel.ValueAt(reelLevel);
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
    public void SetFish(List<FishDefinition> fish) => _fish = fish;

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
    }
#endif
}
