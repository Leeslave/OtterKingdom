using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// <summary>광석을 얻은 곳</summary>
public enum MineGainSource { Rock, Miner, Offline, Cheat }

/// <summary>얻은 광석 한 묶음</summary>
public readonly struct MineGain
{
    public readonly MineOreDefinition Ore;
    public readonly int Amount;
    public readonly MineGainSource Source;
    /// <summary>처음 얻음 (도감 등록)</summary>
    public readonly bool IsNew;

    public MineGain(MineOreDefinition ore, int amount, MineGainSource source, bool isNew)
    {
        Ore = ore;
        Amount = amount;
        Source = source;
        IsNew = isNew;
    }
}

/// <summary>바위 칸 하나 (Hp 0 = 빈 칸)</summary>
public class MineRock
{
    public MineOreDefinition Vein { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; internal set; }
    public bool Shiny { get; internal set; }
    public bool Present => Hp > 0;
}

/// <summary>한 번 친 결과</summary>
public class MineHit
{
    public int Damage;
    public bool Crit;
    public int Combo;
    public bool Broken;
    public bool Shiny;
    /// <summary>이번에 광맥 대박이 터짐</summary>
    public bool JackpotStarted;
    /// <summary>막힌 길을 다 깨 새 층이 열림</summary>
    public bool LayerOpened;
    public readonly List<MineGain> Gains = new List<MineGain>();
}

/// <summary>
/// 광산 개편 규칙 (순수 C#): 바위 치기(콤보 · 치명타) → 깨면 광맥 광석 + 덤(희귀 · 화석 · 콤보), 반짝 광맥, 광맥 대박,
/// 바위 다시 생김, 광부(방치) · 응원, 곡괭이 만들기 · 날 갈기, 층 이동 · 막힌 길, 팔기, 꺼 둔 동안.
/// 화면은 모른다 (MineReworkGame이 결과를 보고 그림). Docs/광산_개편_기획.md
/// </summary>
public class MineReworkModel
{
    private readonly MineReworkConfig _config;
    private readonly Random _rng;
    private readonly Dictionary<MineOreDefinition, int> _counts = new Dictionary<MineOreDefinition, int>();
    private readonly HashSet<string> _discovered = new HashSet<string>();
    private readonly MineRock[] _rocks;

    private float _respawnTimer;
    private float _minerTimer;
    private float _jackpotLeft;
    private float _cheerLeft;
    private float _cheerCooldownLeft;
    private float _lastHitTime = float.NegativeInfinity;

    /// <summary>골드 · 광석 · 곡괭이가 바뀜</summary>
    public event Action Changed;
    /// <summary>처음 얻은 광석 (도감 등록 연출)</summary>
    public event Action<MineOreDefinition> Discovered;
    /// <summary>빈 칸에 바위가 생김 (칸 번호)</summary>
    public event Action<int> RockSpawned;
    /// <summary>광부가 캐냄</summary>
    public event Action<MineGain> MinerFound;
    /// <summary>광맥 대박 시작 · 끝 (true = 시작)</summary>
    public event Action<bool> JackpotChanged;
    /// <summary>층을 옮김 (바위를 새로 그림)</summary>
    public event Action LayerChanged;

    public MineReworkModel(MineReworkConfig config, MineReworkSave save, Random rng)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        if (save == null)
            throw new ArgumentNullException(nameof(save));

        _rocks = new MineRock[config.RockSlots];
        for (int i = 0; i < _rocks.Length; i++)
            _rocks[i] = new MineRock();
        Load(save);
    }

    #region 상태

    public int Gold { get; private set; }
    public int PickaxeTier { get; private set; }
    public int Sharpen { get; private set; }
    public int LayerIndex { get; private set; }
    public int OpenedLayers { get; private set; }
    public int BoulderHits { get; private set; }
    public int Combo { get; private set; }
    public int RocksBroken { get; private set; }

    public MineReworkConfig Config => _config;
    public IReadOnlyList<MineRock> Rocks => _rocks;
    public MineLayerDefinition Layer => _config.Layers[LayerIndex];
    public MinePickaxeDefinition Pickaxe => _config.Pickaxes[PickaxeTier];
    public MinePickaxeDefinition NextPickaxe => PickaxeTier + 1 < _config.Pickaxes.Count ? _config.Pickaxes[PickaxeTier + 1] : null;

    public bool JackpotActive => _jackpotLeft > 0f;
    public float JackpotLeft => Mathf.Max(0f, _jackpotLeft);
    public bool Cheering => _cheerLeft > 0f;
    public float CheerLeft => Mathf.Max(0f, _cheerLeft);
    public float CheerCooldownLeft => Mathf.Max(0f, _cheerCooldownLeft);
    /// <summary>다음 바위까지 남은 비율 (0~1, 빈 칸이 없으면 0)</summary>
    public float RespawnProgress => HasEmptySlot ? Mathf.Clamp01(_respawnTimer / _config.RockRespawnSeconds) : 0f;
    /// <summary>광부가 다음 광석까지 (0~1)</summary>
    public float MinerProgress => Mathf.Clamp01(_minerTimer / MinerInterval);

    public int Count(MineOreDefinition ore) => ore != null && _counts.TryGetValue(ore, out int n) ? n : 0;

    public bool IsDiscovered(MineOreDefinition ore) => ore != null && _discovered.Contains(ore.OreId);

    public int DiscoveredCount => _discovered.Count;

    public bool IsLayerOpen(int index) => index >= 0 && index < OpenedLayers && index < _config.Layers.Count;

    /// <summary>곡괭이가 있어 갈 수 있는 층 (열렸든 막혔든)</summary>
    public bool IsLayerReachable(int index) =>
        index >= 0 && index < _config.Layers.Count && PickaxeTier >= _config.Layers[index].RequiredPickaxeTier;

    /// <summary>막힌 길을 깨면 열리는 다음 층 (없으면 -1)</summary>
    public int BlockedLayer => OpenedLayers < _config.Layers.Count && IsLayerReachable(OpenedLayers) ? OpenedLayers : -1;

    /// <summary>지금 치명타 확률</summary>
    public float CritChance => Mathf.Clamp01(_config.BaseCritChance + Sharpen * _config.CritPerSharpen
                                             + Mathf.Min(10, Combo / 5) * _config.CritPerFiveCombo);

    /// <summary>이 바위를 깨는 데 칠 횟수 (지금 곡괭이)</summary>
    public int HitsToBreak(MineRock rock) => Mathf.CeilToInt(rock.Hp / (float)Pickaxe.Power);

    private bool HasEmptySlot
    {
        get
        {
            foreach (var rock in _rocks)
            {
                if (!rock.Present)
                    return true;
            }
            return false;
        }
    }

    private float MinerInterval => _config.MinerSeconds * (1f - Pickaxe.MinerSpeedBonus);

    #endregion

    #region 바위 치기

    /// <summary>바위를 한 번 친다. time = 지금 시각(초, 콤보 판정)</summary>
    /// <returns>빈 칸이면 null</returns>
    public MineHit Hit(int slot, float time)
    {
        if (slot < 0 || slot >= _rocks.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var rock = _rocks[slot];
        if (!rock.Present)
            return null;

        var hit = Strike(time, Pickaxe.Power);
        hit.Shiny = rock.Shiny;
        rock.Hp = Mathf.Max(0, rock.Hp - hit.Damage);
        if (rock.Hp > 0)
            return hit;

        hit.Broken = true;
        RocksBroken++;
        RollBreak(rock, hit);
        rock.Vein = null;
        rock.Shiny = false;
        rock.MaxHp = 0;

        float jackpotChance = hit.Shiny ? _config.JackpotChanceOnShiny : _config.JackpotChancePerRock;
        if (!JackpotActive && _rng.NextDouble() < jackpotChance)
        {
            hit.JackpotStarted = true;
            StartJackpot();
        }
        else if (JackpotActive)
        {
            Spawn(slot);
        }
        Changed?.Invoke();
        return hit;
    }

    // 콤보 · 치명타 · 피해
    private MineHit Strike(float time, int power)
    {
        Combo = time - _lastHitTime <= _config.ComboWindow ? Combo + 1 : 1;
        _lastHitTime = time;
        var hit = new MineHit { Combo = Combo };
        hit.Crit = _rng.NextDouble() < CritChance;
        hit.Damage = power * (hit.Crit ? 2 : 1);
        return hit;
    }

    // 깨진 바위에서 나오는 것: 광맥 광석(반짝이면 하나 더) + 콤보 덤 + 희귀(반짝이면 반드시) + 화석 (대박 중엔 두 배)
    private void RollBreak(MineRock rock, MineHit hit)
    {
        var layer = Layer;
        var gains = new Dictionary<MineOreDefinition, int>();
        var vein = rock.Vein != null ? rock.Vein : _config.Stone;
        bool plain = vein == _config.Stone;
        var yield = plain ? _config.StoneYield : _config.VeinYield;
        int amount = _rng.Next(yield.x, yield.y + 1) + (rock.Shiny ? 1 : 0)
                     + Mathf.Min(3, hit.Combo / _config.ComboBonusEvery);
        Add(gains, vein, amount);

        float rareChance = layer.RareChance + Pickaxe.RareBonus;
        if (layer.RareDrops.Count > 0 && (rock.Shiny || _rng.NextDouble() < rareChance))
            Add(gains, Pick(layer.RareDrops), 1);

        var relic = layer.Relic;
        if (relic != null && !IsDiscovered(relic) && _rng.NextDouble() < layer.RelicChance * (rock.Shiny ? 3f : 1f))
            Add(gains, relic, 1);

        foreach (var pair in gains)
        {
            int n = pair.Value * (JackpotActive && pair.Key.Rarity != MineOreRarity.Relic ? 2 : 1);
            hit.Gains.Add(AddOre(pair.Key, n, MineGainSource.Rock));
        }
    }

    private static void Add(Dictionary<MineOreDefinition, int> gains, MineOreDefinition ore, int amount)
    {
        if (ore == null || amount <= 0)
            return;
        gains.TryGetValue(ore, out int n);
        gains[ore] = n + amount;
    }

    #endregion

    #region 막힌 길 (다음 층 입구)

    /// <summary>다음 층 막힌 길을 한 번 친다. 다 깨면 그 층이 열리고 그리로 간다</summary>
    /// <returns>막힌 길이 없으면 null</returns>
    public MineHit HitBoulder(float time)
    {
        int next = BlockedLayer;
        if (next < 0)
            return null;

        var hit = Strike(time, 1);
        BoulderHits = Mathf.Min(_config.BoulderHits, BoulderHits + hit.Damage);
        if (BoulderHits < _config.BoulderHits)
            return hit;

        hit.Broken = true;
        hit.LayerOpened = true;
        BoulderHits = 0;
        OpenedLayers = next + 1;
        Travel(next);
        FillAll();
        Changed?.Invoke();
        return hit;
    }

    public int BoulderLeft => _config.BoulderHits - BoulderHits;

    #endregion

    #region 흐름 (바위 · 대박 · 광부)

    /// <summary>시간이 흐름 (초). 바위 다시 생김, 대박 · 응원 시간, 광부</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f)
            return;

        if (_jackpotLeft > 0f)
        {
            _jackpotLeft -= dt;
            if (_jackpotLeft <= 0f)
            {
                _jackpotLeft = 0f;
                JackpotChanged?.Invoke(false);
            }
        }
        _cheerLeft = Mathf.Max(0f, _cheerLeft - dt);
        _cheerCooldownLeft = Mathf.Max(0f, _cheerCooldownLeft - dt);

        if (HasEmptySlot)
        {
            _respawnTimer += dt;
            while (_respawnTimer >= _config.RockRespawnSeconds && HasEmptySlot)
            {
                _respawnTimer -= _config.RockRespawnSeconds;
                SpawnRandomEmpty();
            }
        }
        if (!HasEmptySlot)
            _respawnTimer = 0f;

        float speed = (Cheering ? 2f : 1f) * (JackpotActive ? 2f : 1f);
        _minerTimer += dt * speed;
        while (_minerTimer >= MinerInterval)
        {
            _minerTimer -= MinerInterval;
            MinerFound?.Invoke(MinerDig(MineGainSource.Miner));
            Changed?.Invoke();
        }
    }

    /// <summary>광부 응원: 잠깐 두 배로 캠 (다시 하려면 쉬어야 함)</summary>
    public bool TryCheer()
    {
        if (_cheerCooldownLeft > 0f)
            return false;
        _cheerLeft = _config.CheerSeconds;
        _cheerCooldownLeft = _config.CheerCooldown;
        return true;
    }

    // 광부: 바위 광맥 비율대로 하나 (희귀는 손으로 캘 때의 절반)
    private MineGain MinerDig(MineGainSource source)
    {
        var layer = Layer;
        var ore = layer.RareDrops.Count > 0 && _rng.NextDouble() < layer.RareChance * 0.5f ? Pick(layer.RareDrops) : RollVein(layer);
        return AddOre(ore != null ? ore : _config.Stone, 1, source);
    }

    private void StartJackpot()
    {
        _jackpotLeft = _config.JackpotSeconds;
        FillAll();
        JackpotChanged?.Invoke(true);
    }

    private void FillAll()
    {
        for (int i = 0; i < _rocks.Length; i++)
        {
            if (!_rocks[i].Present)
                Spawn(i);
        }
        _respawnTimer = 0f;
    }

    private void SpawnRandomEmpty()
    {
        var empty = new List<int>();
        for (int i = 0; i < _rocks.Length; i++)
        {
            if (!_rocks[i].Present)
                empty.Add(i);
        }
        if (empty.Count > 0)
            Spawn(empty[_rng.Next(empty.Count)]);
    }

    private void Spawn(int slot, bool notify = true)
    {
        var layer = Layer;
        var rock = _rocks[slot];
        rock.Vein = RollVein(layer);
        rock.Shiny = _rng.NextDouble() < _config.ShinyChance;
        // 반짝 광맥은 돌이 아닌 광맥으로
        if (rock.Shiny && (rock.Vein == null || rock.Vein == _config.Stone))
            rock.Vein = RollVein(layer, true) ?? rock.Vein;
        rock.MaxHp = Mathf.CeilToInt(layer.RockHp * (rock.Shiny ? _config.ShinyHpMultiplier : 1f));
        rock.Hp = rock.MaxHp;
        if (notify)
            RockSpawned?.Invoke(slot);
    }

    private MineOreDefinition RollVein(MineLayerDefinition layer, bool skipStone = false)
    {
        var veins = new List<MineDrop>();
        foreach (var drop in layer.Veins)
        {
            if (drop != null && drop.Ore != null && drop.Weight > 0 && !(skipStone && drop.Ore == _config.Stone))
                veins.Add(drop);
        }
        return veins.Count > 0 ? Pick(veins) : _config.Stone;
    }

    private MineOreDefinition Pick(IReadOnlyList<MineDrop> drops)
    {
        int total = 0;
        foreach (var drop in drops)
        {
            if (drop != null && drop.Ore != null)
                total += drop.Weight;
        }
        if (total <= 0)
            return null;
        int roll = _rng.Next(total);
        foreach (var drop in drops)
        {
            if (drop == null || drop.Ore == null)
                continue;
            roll -= drop.Weight;
            if (roll < 0)
                return drop.Ore;
        }
        return null;
    }

    #endregion

    #region 곡괭이 · 층 · 팔기

    /// <summary>다음 곡괭이 재료가 다 있는지</summary>
    public bool CanCraftNext()
    {
        var next = NextPickaxe;
        if (next == null || Gold < next.GoldCost)
            return false;
        foreach (var cost in next.Recipe)
        {
            if (cost != null && Count(cost.Ore) < cost.Amount)
                return false;
        }
        return true;
    }

    /// <summary>다음 곡괭이를 만든다 (재료 · 골드를 씀, 날 갈기는 처음부터)</summary>
    public bool TryCraftNext()
    {
        if (!CanCraftNext())
            return false;
        var next = NextPickaxe;
        Gold -= next.GoldCost;
        foreach (var cost in next.Recipe)
        {
            if (cost != null)
                _counts[cost.Ore] = Count(cost.Ore) - cost.Amount;
        }
        PickaxeTier++;
        Sharpen = 0;
        Changed?.Invoke();
        return true;
    }

    /// <summary>다음 날 갈기 비용 (더 못 갈면 -1)</summary>
    public int SharpenCost => Sharpen < Pickaxe.MaxSharpen ? Pickaxe.SharpenCosts[Sharpen] : -1;

    public bool TrySharpen()
    {
        int cost = SharpenCost;
        if (cost < 0 || Gold < cost)
            return false;
        Gold -= cost;
        Sharpen++;
        Changed?.Invoke();
        return true;
    }

    /// <summary>열린 층으로 옮긴다 (바위는 새 층 것으로 다시)</summary>
    public bool TryTravel(int index)
    {
        if (!IsLayerOpen(index) || index == LayerIndex)
            return false;
        Travel(index);
        return true;
    }

    private void Travel(int index)
    {
        LayerIndex = index;
        Combo = 0;
        for (int i = 0; i < _rocks.Length; i++)
        {
            if (_rocks[i].Present)
                Spawn(i, false);
        }
        LayerChanged?.Invoke();
    }

    /// <summary>판다 (가진 만큼만). 받은 골드</summary>
    public int Sell(MineOreDefinition ore, int amount)
    {
        if (ore == null || !ore.CanSell)
            return 0;
        int n = Mathf.Clamp(amount, 0, Count(ore));
        if (n == 0)
            return 0;
        _counts[ore] = Count(ore) - n;
        int gold = n * ore.SellPrice;
        Gold += gold;
        Changed?.Invoke();
        return gold;
    }

    #endregion

    #region 꺼 둔 동안

    /// <summary>꺼 둔 시간(초)만큼 바위가 차오르고 광부가 (느리게) 캔다. 광부가 캔 것 목록</summary>
    public List<MineGain> ApplyOffline(double seconds)
    {
        var result = new List<MineGain>();
        if (seconds <= 1)
            return result;

        double respawn = _respawnTimer + seconds;
        while (respawn >= _config.RockRespawnSeconds && HasEmptySlot)
        {
            respawn -= _config.RockRespawnSeconds;
            SpawnRandomEmpty();
        }
        _respawnTimer = HasEmptySlot ? (float)respawn : 0f;

        double interval = MinerInterval * _config.OfflineSlowdown;
        int finds = (int)Math.Min(_config.OfflineMaxFinds, Math.Floor((_minerTimer * _config.OfflineSlowdown + seconds) / interval));
        var merged = new Dictionary<MineOreDefinition, int>();
        var order = new List<MineOreDefinition>();
        var isNew = new HashSet<MineOreDefinition>();
        for (int i = 0; i < finds; i++)
        {
            var gain = MinerDig(MineGainSource.Offline);
            if (!merged.ContainsKey(gain.Ore))
                order.Add(gain.Ore);
            merged.TryGetValue(gain.Ore, out int n);
            merged[gain.Ore] = n + gain.Amount;
            if (gain.IsNew)
                isNew.Add(gain.Ore);
        }
        foreach (var ore in order)
            result.Add(new MineGain(ore, merged[ore], MineGainSource.Offline, isNew.Contains(ore)));
        if (finds > 0)
            Changed?.Invoke();
        return result;
    }

    #endregion

    #region 테스트 치트

    public void CheatAdd(MineOreDefinition ore, int amount)
    {
        AddOre(ore, amount, MineGainSource.Cheat);
        Changed?.Invoke();
    }

    public void CheatGold(int amount)
    {
        Gold = Mathf.Max(0, Gold + amount);
        Changed?.Invoke();
    }

    public void CheatFillRocks() => FillAll();

    public void CheatShiny()
    {
        for (int i = 0; i < _rocks.Length; i++)
        {
            if (!_rocks[i].Present)
                Spawn(i);
            if (!_rocks[i].Shiny)
            {
                _rocks[i].Shiny = true;
                _rocks[i].Vein = RollVein(Layer, true);
                _rocks[i].MaxHp = _rocks[i].Hp = Mathf.CeilToInt(Layer.RockHp * _config.ShinyHpMultiplier);
                RockSpawned?.Invoke(i);
                return;
            }
        }
    }

    public void CheatJackpot()
    {
        if (!JackpotActive)
            StartJackpot();
    }

    /// <summary>다음 곡괭이를 재료 없이</summary>
    public void CheatNextPickaxe()
    {
        if (NextPickaxe == null)
            return;
        PickaxeTier++;
        Sharpen = 0;
        Changed?.Invoke();
    }

    #endregion

    #region 광석 · 저장

    private MineGain AddOre(MineOreDefinition ore, int amount, MineGainSource source)
    {
        _counts[ore] = Count(ore) + amount;
        bool isNew = _discovered.Add(ore.OreId);
        if (isNew)
            Discovered?.Invoke(ore);
        return new MineGain(ore, amount, source, isNew);
    }

    private void Load(MineReworkSave save)
    {
        Gold = save.started ? save.gold : _config.StartGold;
        PickaxeTier = Mathf.Clamp(save.pickaxeTier, 0, _config.Pickaxes.Count - 1);
        Sharpen = Mathf.Clamp(save.sharpen, 0, Pickaxe.MaxSharpen);
        OpenedLayers = Mathf.Clamp(save.openedLayers, 1, _config.Layers.Count);
        LayerIndex = Mathf.Clamp(save.layer, 0, OpenedLayers - 1);
        BoulderHits = Mathf.Max(0, save.boulderHits);
        RocksBroken = save.rocksBroken;
        _respawnTimer = Mathf.Max(0f, save.respawnTimer);
        _minerTimer = Mathf.Max(0f, save.minerTimer);
        foreach (var entry in save.ores)
        {
            var ore = entry != null ? _config.FindOre(entry.id) : null;
            if (ore != null && entry.count > 0)
                _counts[ore] = entry.count;
        }
        foreach (var id in save.discovered)
        {
            if (!string.IsNullOrEmpty(id))
                _discovered.Add(id);
        }

        if (!save.started)
        {
            FillAll();
            return;
        }
        for (int i = 0; i < _rocks.Length && i < save.rocks.Count; i++)
        {
            var saved = save.rocks[i];
            if (saved == null || saved.hp <= 0)
                continue;
            var rock = _rocks[i];
            rock.Vein = _config.FindOre(saved.vein) ?? _config.Stone;
            rock.MaxHp = Mathf.Max(1, saved.maxHp);
            rock.Hp = Mathf.Clamp(saved.hp, 1, rock.MaxHp);
            rock.Shiny = saved.shiny;
        }
    }

    public MineReworkSave ToSave(DateTime utcNow)
    {
        var save = new MineReworkSave
        {
            started = true,
            gold = Gold,
            pickaxeTier = PickaxeTier,
            sharpen = Sharpen,
            layer = LayerIndex,
            openedLayers = OpenedLayers,
            boulderHits = BoulderHits,
            respawnTimer = _respawnTimer,
            minerTimer = _minerTimer,
            rocksBroken = RocksBroken,
            savedUtcTicks = utcNow.Ticks,
        };
        foreach (var ore in _config.Ores)
        {
            int n = Count(ore);
            if (ore != null && n > 0)
                save.ores.Add(new MineOreCountSave { id = ore.OreId, count = n });
        }
        save.discovered.AddRange(_discovered);
        foreach (var rock in _rocks)
        {
            save.rocks.Add(new MineRockSave
            {
                vein = rock.Present && rock.Vein != null ? rock.Vein.OreId : "",
                hp = rock.Hp,
                maxHp = rock.MaxHp,
                shiny = rock.Shiny,
            });
        }
        return save;
    }

    #endregion
}
