using System;
using System.Collections.Generic;
using UnityEngine;

public enum FishingPhase { Ready, Casting, InWater, Hooked }

public enum FishingRequestKind { Fish = 0, Any = 1, Big = 2 }

/// <summary>바다에 있는 물고기 한 마리</summary>
public class FishingFish
{
    public int Id;
    public FishDefinition Def;
    public Vector2 Position;
    public float BaseY;
    public float Dir = 1f;
    public float Size;
    public float Weight;
    public float HomeX;
    public float Range;
    public int Segment;
    public FishLayer Layer;
    public bool Hooked;
    public float HeavyCooldown;
    public float FleeTime;
    public float TurnTimer;
    public float BobPhase;

    /// <summary>바늘에 닿는 몸 반지름 (화면 길이 × 설정 값)</summary>
    public float HitRadius;
}

/// <summary>낚은 결과</summary>
public class FishingCatch
{
    public FishDefinition Def;
    public float Weight;
    public float Size;
    /// <summary>판매가 (가방에 들어가고, 팔 때 골드)</summary>
    public int Price;
    public bool IsNew;
    public bool IsRecord;
    public bool IsBig;
}

/// <summary>지금 의뢰. 다 하면 보상 받기 → 새 의뢰 (쌓이지 않고 하나씩)</summary>
public class FishingRequest
{
    public FishingRequestKind Kind;
    public FishDefinition Fish;
    /// <summary>마리 수 (Big은 넘어야 하는 kg)</summary>
    public float Target;
    public float Progress;
    public int Reward;

    public int Needed => Kind == FishingRequestKind.Big ? 1 : Mathf.RoundToInt(Target);
    public int Count => Mathf.Min(Needed, Mathf.RoundToInt(Progress));
    public bool Done => Count >= Needed;
    public float Ratio => Needed > 0 ? Mathf.Clamp01((float)Count / Needed) : 1f;
}

/// <summary>
/// 낚시 개편 규칙 (순수 C#, Docs/낚시_개편_기획.md):
/// 누르면 줄 길이만큼 던짐 → 물에 닿으면 저절로 감김(누르고 있으면 더 빨리) · 찌 무게만큼 가라앉음 →
/// 바늘이 물고기에 닿으면 걸림(릴이 버틸 수 있는 무게만) → 감겨 와서 낚음 → 골드 · 도감 · 의뢰.
/// 줄은 낚싯대 끝 쪽으로 감기니, 감는 힘과 가라앉는 힘의 균형으로 바늘이 지나가는 깊이가 정해진다
/// </summary>
public class FishingReworkModel
{
    private const float FloorClearance = 0.15f;
    private const float SegmentStart = 1.5f;

    private readonly FishingReworkConfig _config;
    private readonly FishingReworkSave _save;
    private readonly System.Random _rng;
    private readonly IFishingHost _host;
    private readonly List<FishingFish> _fish = new List<FishingFish>();
    private const int Layers = 4;
    private readonly int _segments;
    // 구역 = 거리 칸 × 물층 (index = 칸 × 4 + 물층): 지금 마릿수 · 다음 한 마리가 올 때까지
    private readonly int[] _zoneCount;
    private readonly float[] _zoneTimer;
    private readonly Dictionary<string, FishingBookSave> _book = new Dictionary<string, FishingBookSave>();
    private int _nextFishId;
    private float _castTime;

    public event Action Changed;
    public event Action Casted;
    public event Action<Vector2> Splashed;
    public event Action<FishingFish> Hooked;
    public event Action<FishingFish> TooHeavy;
    public event Action<FishingCatch> Caught;
    public event Action Missed;
    public event Action<FishingFish> FishSpawned;
    public event Action<FishingFish> FishRemoved;
    public event Action<FishingRequest> RequestDone;
    public event Action RequestChanged;
    public event Action<FishingUpgrade> Upgraded;

    /// <param name="host">본 게임 골드 · 가방 (없으면 테스트 씬: 세이브 안의 골드 · 가방)</param>
    public FishingReworkModel(FishingReworkConfig config, FishingReworkSave save, System.Random rng, IFishingHost host = null)
    {
        _host = host;
        _config = config;
        _save = save ?? new FishingReworkSave();
        _rng = rng ?? new System.Random();
        _save.started = true;
        _save.bag ??= new List<FishingBagSave>();
        _save.book ??= new List<FishingBookSave>();
        foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
            SetLevel(upgrade, Mathf.Clamp(Level(upgrade), 0, config.Track(upgrade).MaxLevel));
        foreach (var entry in _save.book)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.fishId))
                _book[entry.fishId] = entry;
        }

        _segments = Mathf.Max(1, Mathf.CeilToInt(config.SeaLength / config.SegmentLength));
        _zoneCount = new int[_segments * Layers];
        _zoneTimer = new float[_segments * Layers];
        for (int segment = 0; segment < _segments; segment++)
        {
            for (int layer = 0; layer < Layers; layer++)
            {
                int cap = config.LayerCap((FishLayer)layer);
                for (int n = 0; n < cap; n++)
                {
                    if (SpawnIn(segment, (FishLayer)layer) == null)
                        break;
                }
                _zoneTimer[segment * Layers + layer] = config.LayerRegenSeconds((FishLayer)layer);
            }
        }
        LoadRequest();
    }

    #region 읽기

    public FishingReworkConfig Config => _config;
    public FishingReworkSave Save => _save;
    public FishingPhase Phase { get; private set; } = FishingPhase.Ready;
    public Vector2 Hook { get; private set; }
    public Vector2 Tip => new Vector2(0f, _config.RodTipHeight);
    public float CastTarget { get; private set; }
    public FishingFish HookedFish { get; private set; }
    public bool Holding { get; private set; }
    public IReadOnlyList<FishingFish> Fish => _fish;
    public int Gold => _host != null ? _host.Gold : _save.gold;
    /// <summary>본 게임 가방 · 골드를 씀 (가방 화면 · 골드 표시는 본 게임 것)</summary>
    public bool Hosted => _host != null;
    public int TotalCaught => _save.totalCaught;

    /// <summary>가방 (낚은 물고기 한 마리씩, 무게 · 값이 다름)</summary>
    public IReadOnlyList<FishingBagSave> Bag => _save.bag;

    public int BagValue
    {
        get
        {
            int total = 0;
            foreach (var fish in _save.bag)
                total += fish.price;
            return total;
        }
    }
    public FishingRequest Request { get; private set; }
    public float SkipCooldown => _save.requestSkipCooldown;
    public bool CanSkipRequest => _save.requestSkipCooldown <= 0f && (Request == null || !Request.Done);

    public float CastDistance => Value(FishingUpgrade.Line);
    /// <summary>찌가 내려갈 수 있는 깊이 (m)</summary>
    public float MaxDepth => Value(FishingUpgrade.Sinker);
    public float SinkSpeed => _config.SinkSpeedAt(Level(FishingUpgrade.Sinker));
    public float ReelCapacity => Value(FishingUpgrade.Reel);

    /// <summary>바늘이 물속에 있을 때 거리 · 깊이 (m)</summary>
    public float HookDistance => Mathf.Max(0f, Hook.x);
    public float HookDepth => Mathf.Max(0f, -Hook.y);

    public int Level(FishingUpgrade upgrade) =>
        upgrade == FishingUpgrade.Line ? _save.lineLevel : upgrade == FishingUpgrade.Sinker ? _save.sinkerLevel : _save.reelLevel;

    public float Value(FishingUpgrade upgrade) => _config.Track(upgrade).ValueAt(Level(upgrade));
    public bool IsMax(FishingUpgrade upgrade) => Level(upgrade) >= _config.Track(upgrade).MaxLevel;
    public int NextCost(FishingUpgrade upgrade) => IsMax(upgrade) ? 0 : _config.Track(upgrade).CostAt(Level(upgrade));
    public bool CanUpgrade(FishingUpgrade upgrade) => !IsMax(upgrade) && Gold >= NextCost(upgrade);

    public bool AnyUpgradeAffordable
    {
        get
        {
            foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
            {
                if (CanUpgrade(upgrade))
                    return true;
            }
            return false;
        }
    }

    public int CaughtCount(FishDefinition def) => def != null && _book.TryGetValue(def.FishId, out var e) ? e.count : 0;
    public float BestWeight(FishDefinition def) => def != null && _book.TryGetValue(def.FishId, out var e) ? e.bestWeight : 0f;
    public int SpeciesCaught => _book.Count;

    /// <summary>지금 강화로 낚을 수 있는 물고기 (닿는 거리 · 버티는 무게)</summary>
    public bool CanReach(FishDefinition def) =>
        def != null && def.MinDistance <= CastDistance - 1f && def.MinWeight <= ReelCapacity;

    #endregion

    #region 던지기 · 감기

    public bool TryCast()
    {
        if (Phase != FishingPhase.Ready)
            return false;
        Phase = FishingPhase.Casting;
        CastTarget = CastDistance;
        _castTime = 0f;
        Hook = Tip;
        Casted?.Invoke();
        Changed?.Invoke();
        return true;
    }

    /// <summary>던진 줄을 거둠 (본 게임에서 바다를 나갈 때). 걸린 물고기는 놓아줌</summary>
    public void Reset()
    {
        if (HookedFish != null)
        {
            HookedFish.Hooked = false;
            HookedFish = null;
        }
        Phase = FishingPhase.Ready;
        Hook = Tip;
        Changed?.Invoke();
    }

    public void Tick(float dt, bool holding)
    {
        if (dt <= 0f)
            return;
        Holding = holding;
        if (_save.requestSkipCooldown > 0f)
            _save.requestSkipCooldown = Mathf.Max(0f, _save.requestSkipCooldown - dt);
        TickZones(dt);
        foreach (var fish in _fish)
            Swim(fish, dt);

        switch (Phase)
        {
            case FishingPhase.Casting:
                TickCast(dt);
                break;
            case FishingPhase.InWater:
                TickInWater(dt, holding);
                break;
            case FishingPhase.Hooked:
                TickHooked(dt, holding);
                break;
        }
    }

    private void TickCast(float dt)
    {
        _castTime += dt;
        float t = Mathf.Clamp01(_castTime / Mathf.Max(0.05f, _config.CastSeconds));
        var land = new Vector2(CastTarget, 0f);
        float arc = 1.5f + CastTarget * 0.06f;
        Hook = Vector2.Lerp(Tip, land, t) + Vector2.up * (arc * 4f * t * (1f - t));
        if (t < 1f)
            return;
        Hook = land;
        Phase = FishingPhase.InWater;
        Splashed?.Invoke(land);
        Changed?.Invoke();
    }

    private Vector2 TowardTip(Vector2 from)
    {
        var to = Tip - from;
        float length = to.magnitude;
        return length > 0.001f ? to / length : Vector2.left;
    }

    private void TickInWater(float dt, bool holding)
    {
        // 선착장 쪽으로 늘 감기고(누르면 빨리), 누르면 올라오고 떼면 찌 깊이까지 가라앉음
        float reel = holding ? _config.HoldReel : _config.AutoReel;
        float vertical = holding ? _config.LiftSpeed : -SinkSpeed;
        var hook = new Vector2(Hook.x - reel * dt, Hook.y + vertical * dt);
        hook.y = Mathf.Clamp(hook.y, -MaxDepth, 0f);
        float floor = -_config.SeabedDepth(hook.x) + FloorClearance;
        if (hook.y < floor)
            hook.y = floor;
        Hook = hook;

        if (Hook.x <= _config.LandDistance)
        {
            Phase = FishingPhase.Ready;
            Missed?.Invoke();
            Changed?.Invoke();
            return;
        }

        foreach (var fish in _fish)
        {
            if (fish.Hooked)
                continue;
            float reach = _config.HookRadius + fish.HitRadius;
            if ((fish.Position - Hook).sqrMagnitude > reach * reach)
                continue;
            if (fish.Weight <= ReelCapacity)
            {
                fish.Hooked = true;
                HookedFish = fish;
                Phase = FishingPhase.Hooked;
                Hooked?.Invoke(fish);
                Changed?.Invoke();
                return;
            }
            if (fish.HeavyCooldown <= 0f)
            {
                fish.HeavyCooldown = 2.5f;
                fish.FleeTime = 1.2f;
                fish.Dir = fish.Position.x >= Hook.x ? 1f : -1f;
                TooHeavy?.Invoke(fish);
            }
        }
    }

    private void TickHooked(float dt, bool holding)
    {
        float speed = _config.HookedReel * (holding ? 1.5f : 1f);
        Hook += TowardTip(Hook) * speed * dt;
        if (HookedFish != null)
        {
            HookedFish.Position = Hook;
            HookedFish.BaseY = Hook.y;
        }
        if (Hook.x > _config.LandDistance && (Tip - Hook).sqrMagnitude > 0.64f)
            return;
        Land();
    }

    private void Land()
    {
        var fish = HookedFish;
        HookedFish = null;
        Phase = FishingPhase.Ready;
        if (fish == null)
        {
            Missed?.Invoke();
            Changed?.Invoke();
            return;
        }
        RemoveFish(fish);
        var result = Record(fish.Def, fish.Weight, fish.Size);
        Caught?.Invoke(result);
        Changed?.Invoke();
    }

    private FishingCatch Record(FishDefinition def, float weight, float size)
    {
        bool isNew = !_book.TryGetValue(def.FishId, out var entry);
        if (isNew)
        {
            entry = new FishingBookSave { fishId = def.FishId };
            _book[def.FishId] = entry;
            _save.book.Add(entry);
        }
        bool record = !isNew && weight > entry.bestWeight;
        entry.count++;
        entry.bestWeight = Mathf.Max(entry.bestWeight, weight);
        // 본 게임: 가방 아이템은 종류마다 값이 같음 (판매가 = 아이템 값)
        int price = _host != null && def.Item != null ? def.Item.SellPrice : def.PriceAt(size);
        if (_host != null)
            _host.AddCatch(def);
        else
            _save.bag.Add(new FishingBagSave { fishId = def.FishId, weight = weight, price = price });
        _save.totalCaught++;
        AdvanceRequest(def, weight);
        return new FishingCatch { Def = def, Weight = weight, Size = size, Price = price, IsNew = isNew, IsRecord = record, IsBig = size >= 0.85f };
    }

    #endregion

    #region 물고기

    private void Swim(FishingFish fish, float dt)
    {
        if (fish.Hooked)
            return;
        fish.HeavyCooldown -= dt;
        fish.FleeTime -= dt;
        fish.TurnTimer -= dt;
        fish.BobPhase += dt * (1.2f + fish.Def.SwimSpeed);

        bool attracted = false;
        if (_config.AttractRadius > 0f && Phase == FishingPhase.InWater && fish.Weight <= ReelCapacity)
        {
            var to = Hook - fish.Position;
            float distance = to.magnitude;
            if (distance < _config.AttractRadius && distance > 0.01f)
            {
                var step = to / distance * fish.Def.SwimSpeed * 1.5f * dt;
                fish.Position += step;
                fish.BaseY = fish.Position.y;
                fish.Dir = to.x >= 0f ? 1f : -1f;
                attracted = true;
            }
        }
        if (!attracted)
        {
            float speed = fish.Def.SwimSpeed * (fish.FleeTime > 0f ? 3f : 1f);
            float x = fish.Position.x + fish.Dir * speed * dt;
            if (x > fish.HomeX + fish.Range)
                fish.Dir = -1f;
            else if (x < fish.HomeX - fish.Range)
                fish.Dir = 1f;
            else if (fish.TurnTimer <= 0f && fish.FleeTime <= 0f)
            {
                fish.TurnTimer = 3f + (float)_rng.NextDouble() * 5f;
                if (_rng.NextDouble() < 0.35)
                    fish.Dir = -fish.Dir;
            }
            fish.Position = new Vector2(x, fish.BaseY + 0.18f * Mathf.Sin(fish.BobPhase));
        }

        float floor = -_config.SeabedDepth(fish.Position.x) + 0.25f;
        float top = -_config.FishTopDepth;
        fish.Position = new Vector2(Mathf.Max(0.8f, fish.Position.x), Mathf.Clamp(fish.Position.y, floor, top));
        fish.BaseY = Mathf.Clamp(fish.BaseY, floor, top);
    }

    private static int Zone(int segment, FishLayer layer) => segment * Layers + (int)layer;

    /// <summary>구역마다 모자라면 정해진 시간마다 한 마리씩 (가득이면 시계가 멈춰 있다가, 잡으면 처음부터)</summary>
    private void TickZones(float dt)
    {
        for (int segment = 0; segment < _segments; segment++)
        {
            for (int layer = 0; layer < Layers; layer++)
            {
                var fishLayer = (FishLayer)layer;
                int zone = Zone(segment, fishLayer);
                float regen = _config.LayerRegenSeconds(fishLayer);
                if (_zoneCount[zone] >= _config.LayerCap(fishLayer))
                {
                    _zoneTimer[zone] = regen;
                    continue;
                }
                _zoneTimer[zone] -= dt;
                if (_zoneTimer[zone] > 0f)
                    continue;
                _zoneTimer[zone] = regen;
                SpawnIn(segment, fishLayer);
            }
        }
    }

    /// <summary>구역의 지금 마릿수 (테스트 · 화면용)</summary>
    public int ZoneCount(int segment, FishLayer layer) => _zoneCount[Zone(segment, layer)];

    private void RemoveFish(FishingFish fish)
    {
        _fish.Remove(fish);
        if (fish.Segment >= 0 && fish.Segment < _segments)
            _zoneCount[Zone(fish.Segment, fish.Layer)]--;
        FishRemoved?.Invoke(fish);
    }

    private (float from, float to) SegmentRange(int segment)
    {
        float from = SegmentStart + segment * _config.SegmentLength;
        return (from, from + _config.SegmentLength);
    }

    /// <summary>이 칸에 살 수 있는 물고기 (거리가 겹치고, 그 자리 바닥 깊이 안에 들어감)</summary>
    private bool FitsSegment(FishDefinition def, float from, float to)
    {
        if (def == null || def.MaxDistance < from || def.MinDistance > to)
            return false;
        if (def.NearBottom)
            return true;
        float x = Mathf.Clamp((from + to) * 0.5f, def.MinDistance, def.MaxDistance);
        return def.MinDepth <= _config.SeabedDepth(x) - 0.5f;
    }

    private bool Fits(FishDefinition def, FishLayer layer, float from, float to) =>
        def != null && FishingReworkConfig.LayerOf(def) == layer && FitsSegment(def, from, to);

    private FishingFish SpawnIn(int segment, FishLayer layer)
    {
        var (from, to) = SegmentRange(segment);
        float total = 0f;
        foreach (var def in _config.Fish)
        {
            if (Fits(def, layer, from, to))
                total += def.SpawnWeight;
        }
        if (total <= 0f)
            return null;
        double pick = _rng.NextDouble() * total;
        FishDefinition chosen = null;
        foreach (var def in _config.Fish)
        {
            if (!Fits(def, layer, from, to))
                continue;
            chosen = def;
            pick -= def.SpawnWeight;
            if (pick < 0)
                break;
        }
        return chosen != null ? Spawn(chosen, segment, from, to) : null;
    }

    private FishingFish Spawn(FishDefinition def, int segment, float from, float to)
    {
        float lo = Mathf.Max(from, def.MinDistance), hi = Mathf.Min(to, def.MaxDistance);
        float x = Mathf.Lerp(lo, hi, (float)_rng.NextDouble());
        float seabed = _config.SeabedDepth(x);
        float y;
        if (def.NearBottom)
            y = Mathf.Min(-0.4f, -seabed + Mathf.Lerp(def.MinDepth, def.MaxDepth, (float)_rng.NextDouble()));
        else
        {
            float deepest = Mathf.Max(def.MinDepth, Mathf.Min(def.MaxDepth, seabed - 0.5f));
            y = -Mathf.Lerp(def.MinDepth, deepest, (float)_rng.NextDouble());
        }
        float size = Mathf.Pow((float)_rng.NextDouble(), 1.5f);
        var fish = new FishingFish
        {
            Id = _nextFishId++,
            Def = def,
            Position = new Vector2(x, y),
            BaseY = y,
            Dir = _rng.NextDouble() < 0.5 ? -1f : 1f,
            Size = size,
            Weight = def.WeightAt(size),
            HitRadius = def.LengthAt(size) * _config.FishHitScale,
            HomeX = x,
            Range = 1.5f + (float)_rng.NextDouble() * 2.5f,
            Segment = segment,
            Layer = FishingReworkConfig.LayerOf(def),
            TurnTimer = 2f + (float)_rng.NextDouble() * 4f,
            BobPhase = (float)_rng.NextDouble() * Mathf.PI * 2f,
        };
        _fish.Add(fish);
        _zoneCount[Zone(segment, fish.Layer)]++;
        FishSpawned?.Invoke(fish);
        return fish;
    }

    #endregion

    #region 가방

    /// <summary>가방의 물고기 한 마리 팔기 (팔린 값, 없으면 0)</summary>
    public int Sell(FishingBagSave fish)
    {
        if (fish == null || !_save.bag.Remove(fish))
            return 0;
        _save.gold += fish.price;
        Changed?.Invoke();
        return fish.price;
    }

    /// <summary>가방 전부 팔기 (번 골드)</summary>
    public int SellAll()
    {
        int total = BagValue;
        if (_save.bag.Count == 0)
            return 0;
        _save.bag.Clear();
        _save.gold += total;
        Changed?.Invoke();
        return total;
    }

    #endregion

    #region 강화

    public bool TryUpgrade(FishingUpgrade upgrade)
    {
        if (!CanUpgrade(upgrade))
            return false;
        if (_host != null)
        {
            if (!_host.TrySpendGold(NextCost(upgrade)))
                return false;
        }
        else
            _save.gold -= NextCost(upgrade);
        SetLevel(upgrade, Level(upgrade) + 1);
        Upgraded?.Invoke(upgrade);
        Changed?.Invoke();
        return true;
    }

    private void SetLevel(FishingUpgrade upgrade, int level)
    {
        switch (upgrade)
        {
            case FishingUpgrade.Line: _save.lineLevel = level; break;
            case FishingUpgrade.Sinker: _save.sinkerLevel = level; break;
            default: _save.reelLevel = level; break;
        }
    }

    #endregion

    #region 의뢰

    private void LoadRequest()
    {
        if (_save.requestKind < 0)
        {
            NewRequest(null);
            return;
        }
        var kind = (FishingRequestKind)_save.requestKind;
        var fish = _config.FindFish(_save.requestFishId);
        if (kind == FishingRequestKind.Fish && fish == null)
        {
            NewRequest(null);
            return;
        }
        Request = new FishingRequest
        {
            Kind = kind,
            Fish = fish,
            Target = _save.requestTarget,
            Progress = _save.requestProgress,
            Reward = _save.requestReward,
        };
    }

    private void WriteRequest()
    {
        _save.requestKind = Request != null ? (int)Request.Kind : -1;
        _save.requestFishId = Request?.Fish != null ? Request.Fish.FishId : "";
        _save.requestTarget = Request?.Target ?? 0f;
        _save.requestProgress = Request?.Progress ?? 0f;
        _save.requestReward = Request?.Reward ?? 0;
    }

    private void AdvanceRequest(FishDefinition def, float weight)
    {
        var request = Request;
        if (request == null || request.Done)
            return;
        switch (request.Kind)
        {
            case FishingRequestKind.Fish:
                if (request.Fish == def)
                    request.Progress++;
                break;
            case FishingRequestKind.Any:
                request.Progress++;
                break;
            case FishingRequestKind.Big:
                if (weight >= request.Target)
                    request.Progress = 1f;
                break;
        }
        WriteRequest();
        RequestChanged?.Invoke();
        if (request.Done)
            RequestDone?.Invoke(request);
    }

    /// <summary>다 한 의뢰의 보상을 받고 새 의뢰</summary>
    public bool ClaimRequest()
    {
        if (Request == null || !Request.Done)
            return false;
        if (_host != null)
            _host.EarnGold(Request.Reward);
        else
            _save.gold += Request.Reward;
        _save.requestsDone++;
        NewRequest(Request);
        Changed?.Invoke();
        return true;
    }

    /// <summary>의뢰 바꾸기 (잠깐 쉬어야 다시 됨)</summary>
    public bool SkipRequest()
    {
        if (!CanSkipRequest)
            return false;
        _save.requestSkipCooldown = _config.RequestSkipCooldown;
        NewRequest(Request);
        return true;
    }

    private static float RarityPickWeight(FishRarity rarity)
    {
        switch (rarity)
        {
            case FishRarity.Common:
            case FishRarity.Uncommon: return 1f;
            case FishRarity.Rare: return 0.7f;
            case FishRarity.Epic: return 0.45f;
            default: return 0.15f;
        }
    }

    private int Range(int min, int max) => min + _rng.Next(max - min + 1);

    private static int Round10(float value) => Mathf.Max(10, Mathf.RoundToInt(value / 10f) * 10);

    /// <summary>새 의뢰: 지금 강화로 낚을 수 있는 물고기 중에서 (어려움은 강화를 따라 오름)</summary>
    public void NewRequest(FishingRequest previous)
    {
        var pool = new List<FishDefinition>();
        foreach (var def in _config.Fish)
        {
            if (CanReach(def))
                pool.Add(def);
        }
        float factor = _config.RequestRewardFactor;
        FishingRequest request;
        double roll = _rng.NextDouble();
        if (pool.Count == 0)
            request = new FishingRequest { Kind = FishingRequestKind.Any, Target = 3, Reward = 30 };
        else if (roll < 0.7 || pool.Count == 1)
            request = FishRequest(pool, previous, factor);
        else if (roll < 0.85)
            request = AnyRequest(pool, factor);
        else
            request = BigRequest(pool, factor) ?? FishRequest(pool, previous, factor);
        Request = request;
        WriteRequest();
        RequestChanged?.Invoke();
    }

    private FishingRequest FishRequest(List<FishDefinition> pool, FishingRequest previous, float factor)
    {
        FishDefinition chosen = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            float total = 0f;
            foreach (var def in pool)
                total += RarityPickWeight(def.Rarity);
            double pick = _rng.NextDouble() * total;
            foreach (var def in pool)
            {
                chosen = def;
                pick -= RarityPickWeight(def.Rarity);
                if (pick < 0)
                    break;
            }
            if (previous == null || previous.Fish != chosen || pool.Count == 1)
                break;
        }
        int count;
        switch (chosen.Rarity)
        {
            case FishRarity.Common: count = Range(5, 8); break;
            case FishRarity.Uncommon: count = Range(3, 5); break;
            case FishRarity.Rare: count = Range(2, 3); break;
            case FishRarity.Epic: count = Range(1, 2); break;
            default: count = 1; break;
        }
        float bonus = 1f + 0.15f * (int)chosen.Rarity;
        return new FishingRequest
        {
            Kind = FishingRequestKind.Fish,
            Fish = chosen,
            Target = count,
            Reward = Round10(count * chosen.Price * factor * bonus),
        };
    }

    private FishingRequest AnyRequest(List<FishDefinition> pool, float factor)
    {
        float weightSum = 0f, priceSum = 0f;
        foreach (var def in pool)
        {
            weightSum += def.SpawnWeight;
            priceSum += def.SpawnWeight * def.Price;
        }
        float average = weightSum > 0f ? priceSum / weightSum : 5f;
        int count = Range(6, 10);
        return new FishingRequest { Kind = FishingRequestKind.Any, Target = count, Reward = Round10(count * average * factor * 0.8f) };
    }

    private FishingRequest BigRequest(List<FishDefinition> pool, float factor)
    {
        float heaviest = 0f;
        foreach (var def in pool)
            heaviest = Mathf.Max(heaviest, Mathf.Min(def.MaxWeight, ReelCapacity));
        if (heaviest < 0.5f)
            return null;
        float target = Mathf.Max(0.3f, Mathf.Round(heaviest * Mathf.Lerp(0.5f, 0.75f, (float)_rng.NextDouble()) * 10f) / 10f);
        float priceSum = 0f;
        int n = 0;
        foreach (var def in pool)
        {
            if (def.MaxWeight < target)
                continue;
            priceSum += def.Price;
            n++;
        }
        if (n == 0)
            return null;
        return new FishingRequest { Kind = FishingRequestKind.Big, Target = target, Reward = Round10(priceSum / n * factor * 2.5f) };
    }

    #endregion

    #region 테스트

    /// <summary>정한 자리에 물고기 한 마리 (테스트 · 연출용, 헤엄은 그 자리 근처)</summary>
    public FishingFish PlaceFish(FishDefinition def, Vector2 position, float size)
    {
        int segment = Mathf.Clamp(Mathf.FloorToInt((position.x - SegmentStart) / _config.SegmentLength), 0, _segments - 1);
        var fish = new FishingFish
        {
            Id = _nextFishId++,
            Def = def,
            Position = position,
            BaseY = position.y,
            Size = size,
            Weight = def.WeightAt(size),
            HitRadius = def.LengthAt(size) * _config.FishHitScale,
            HomeX = position.x,
            Range = 0.01f,
            Segment = segment,
            Layer = FishingReworkConfig.LayerOf(def),
            TurnTimer = 999f,
        };
        _fish.Add(fish);
        _zoneCount[Zone(segment, fish.Layer)]++;
        FishSpawned?.Invoke(fish);
        return fish;
    }

    public void CheatGold(int amount)
    {
        _save.gold = Mathf.Max(0, _save.gold + amount);
        Changed?.Invoke();
    }

    public void CheatMaxUpgrades()
    {
        foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
        {
            SetLevel(upgrade, _config.Track(upgrade).MaxLevel);
            Upgraded?.Invoke(upgrade);
        }
        Changed?.Invoke();
    }

    #endregion
}
