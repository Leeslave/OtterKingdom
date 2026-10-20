using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

/// <summary>앞(또는 고른 쪽) 칸에 있는 것</summary>
public enum MazeAhead { Wall, Open, Rock, Chest, OpenedChest, Stairs, Entrance }

/// <summary>미로 안의 돌 하나 (한 번 뚫으면 다시 생겨도 길을 막지 않음)</summary>
public class MazeRock
{
    public Vector2Int Cell { get; internal set; }
    /// <summary>한 번이라도 부숴 길을 뚫음</summary>
    public bool Opened { get; internal set; }
    public bool Present { get; internal set; }
    public MineOreDefinition Vein { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp { get; internal set; }
    public bool Shiny { get; internal set; }
    /// <summary>다시 생기는 시각 (있으면 의미 없음)</summary>
    public DateTime RespawnUtc { get; internal set; }
    /// <summary>길을 막음 (있으면 늘. 다시 생긴 돌도 다시 부숴야 지나감)</summary>
    public bool Blocking => Present;

    /// <summary>MineReworkGame 바위 그림에 넘기는 모양</summary>
    public MineRock ToRock() => new MineRock { Vein = Vein, Hp = Present ? Hp : 0, MaxHp = MaxHp, Shiny = Shiny };
}

/// <summary>한 칸 가기의 결과</summary>
public enum MazeMove { Moved, Blocked, Wall }

/// <summary>미로에서 나올 때 정리 (본 게임에서는 이 시간만큼 마을을 오프라인으로 정산)</summary>
public class MazeRunResult
{
    public double Seconds;
    public readonly List<MineGain> Gains = new List<MineGain>();
    public int RocksBroken;
    public int ChestsOpened;
    public int NewCells;
    /// <summary>이번에 지도 해달이 옴</summary>
    public bool MapOtterArrived;
    /// <summary>이 층의 돌을 모두 한 번씩 부숴 다녀온 횟수에 들어감</summary>
    public bool RunCounted;
    /// <summary>이 층의 돌 수 · 이번에 한 번이라도 부순 돌 수</summary>
    public int FloorRocks;
    public int RocksCleared;
}

/// <summary>
/// 광산 미로 규칙 (순수 C#): 앞 · 뒤 · 왼쪽 · 오른쪽 한 칸씩 가기, 길을 막는 돌 치기(광산 개편 규칙: 콤보 · 치명타 · 반짝 · 희귀 · 화석),
/// 부순 돌은 정해진 시간 뒤 같은 자리에 다시 생겨 다시 길을 막음, 보물 상자(한 번), 나가기(층 입구로),
/// 밟은 칸 기록 · 지도 해달(지도 없이 몇 번 다녀오면 옴). 화면은 모른다 (MineMazeGame이 결과를 보고 그림).
/// Docs/광산_미로탐험과_왕도성장_기획.md 4.4~4.7
/// </summary>
public partial class MineMazeModel
{
    private MineMazeConfig _config;
    private readonly MineReworkConfig _rework;
    private readonly Random _rng;
    private MazeMap _map;
    private readonly Dictionary<Vector2Int, MazeRock> _rocks = new Dictionary<Vector2Int, MazeRock>();
    private readonly HashSet<Vector2Int> _openedChests = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _visited = new HashSet<Vector2Int>();
    private readonly Dictionary<MineOreDefinition, int> _counts = new Dictionary<MineOreDefinition, int>();
    private readonly HashSet<string> _discovered = new HashSet<string>();
    private float _lastHitTime = float.NegativeInfinity;

    // 이번 탐험
    private DateTime _runStartUtc;
    private readonly Dictionary<MineOreDefinition, int> _runGains = new Dictionary<MineOreDefinition, int>();
    private readonly List<MineOreDefinition> _runOrder = new List<MineOreDefinition>();
    private readonly HashSet<MineOreDefinition> _runNew = new HashSet<MineOreDefinition>();
    private int _runRocks;
    private int _runChests;
    private int _runNewCells;
    // 이번 탐험에서 한 번이라도 부순 이 층의 돌 (모두 부숴야 다녀온 횟수 1번)
    private readonly HashSet<Vector2Int> _runBroken = new HashSet<Vector2Int>();

    /// <summary>광석 · 곡괭이가 바뀜</summary>
    public event Action Changed;
    /// <summary>처음 얻은 광석</summary>
    public event Action<MineOreDefinition> Discovered;
    /// <summary>부순 돌이 다시 생김 (자리)</summary>
    public event Action<Vector2Int> RockRespawned;

    public MineMazeModel(MineMazeConfig config, MineMazeSave save, Random rng, DateTime utcNow)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rework = config.Rework ?? throw new ArgumentException("광산 개편 설정이 없습니다", nameof(config));
        if (config.Layer == null)
            throw new ArgumentException("층(광맥) 설정이 없습니다", nameof(config));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        if (save == null)
            throw new ArgumentNullException(nameof(save));
        InitializeFloors(config, save);
        _map = MazeMap.Parse(_config.MapRows);
        foreach (var cell in _map.Rocks)
            _rocks[cell] = new MazeRock { Cell = cell };
        Load(save);
        LoadProgress(save);
        RestoreFloorState(utcNow);
        Tick(utcNow);
        Position = _map.Start;
        Facing = _map.StartFacing;
    }

    #region 상태

    public MineMazeConfig Config => _config;
    public MazeMap Map => _map;
    public MineLayerDefinition Layer => _config.Layer;
    public bool Inside { get; private set; }
    public Vector2Int Position { get; private set; }
    public MazeDir Facing { get; private set; }
    public int PickaxeTier { get; private set; }
    public MinePickaxeDefinition Pickaxe => _rework.Pickaxes[PickaxeTier];
    public int Combo { get; private set; }
    public int Runs { get; private set; }
    public bool MapOtter { get; private set; }
    public int RocksBroken { get; private set; }
    public int VisitedCount => _visited.Count;
    /// <summary>이 층의 돌 수 (다녀온 횟수 1번 = 이 돌을 모두 한 번씩 부수고 나오기)</summary>
    public int FloorRockCount => _map.Rocks.Count;
    /// <summary>이번 탐험에서 한 번이라도 부순 이 층의 돌 수</summary>
    public int RunRocksCleared => _runBroken.Count;
    public bool RunCleared => _runBroken.Count >= FloorRockCount;
    /// <summary>갱도에서 나온 뒤 다시 들어갈 수 있는 시각 (설정의 다시 들어가기 대기)</summary>
    public DateTime NextEnterUtc { get; private set; } = DateTime.MinValue;
    // 기기 시계를 되돌리면 남은 시간이 대기 시간보다 길어짐 → 그때는 막지 않음 (오래 갇히지 않게)
    public bool CanEnterAt(DateTime utcNow) => Inside || utcNow >= NextEnterUtc
        || (NextEnterUtc - utcNow).TotalSeconds > _config.ReenterCooldownSeconds;
    public TimeSpan EnterWait(DateTime utcNow) => CanEnterAt(utcNow) ? TimeSpan.Zero : NextEnterUtc - utcNow;
    public IEnumerable<MazeRock> Rocks => _rocks.Values;

    /// <summary>Only remove ore that actually fits in the player's bag.</summary>
    public int TransferToInventory(MineOreDefinition ore, ItemDefinition item, Inventory bag, int amount)
    {
        if (ore == null || item == null || bag == null || amount <= 0 || item.ItemId != ore.OreId)
            return 0;
        int requested = Math.Min(amount, StoredCount(ore));
        if (requested <= 0) return 0;
        int moved = bag.Add(item, requested, ItemChangeReason.Mining);
        if (moved > 0)
        {
            _counts[ore] -= moved;
            Changed?.Invoke();
        }
        return moved;
    }

    /// <summary>쓸 수 있는 광석 (작업대 + 본 게임 가방)</summary>
    public int Count(MineOreDefinition ore) => StoredCount(ore) + (_bag != null && ore != null ? _bag.Count(ore) : 0);

    /// <summary>갱도 작업대에 쌓인 광석 (가방에 못 들어간 것 · 자동 채굴 · 옛 세이브)</summary>
    public int StoredCount(MineOreDefinition ore) => ore != null && _counts.TryGetValue(ore, out int n) ? n : 0;

    private IMineOreBag _bag;

    /// <summary>본 게임: 캔 광석이 가방으로 바로 들어감 (없으면 = 테스트 씬, 작업대에만)</summary>
    public void UseBag(IMineOreBag bag)
    {
        _bag = bag;
        Changed?.Invoke();
    }

    // 가방에 먼저, 못 들어간 것은 작업대에
    private void Put(MineOreDefinition ore, int amount)
    {
        int bagged = _bag != null ? _bag.Add(ore, amount) : 0;
        if (amount > bagged)
            _counts[ore] = StoredCount(ore) + amount - bagged;
    }

    // 작업대 것을 먼저 쓰고 모자라면 가방에서 (부르기 전에 Count로 모자라지 않은지 확인)
    private void Take(MineOreDefinition ore, int amount)
    {
        int stored = Math.Min(amount, StoredCount(ore));
        if (stored > 0)
            _counts[ore] -= stored;
        if (amount > stored && _bag != null)
            _bag.Remove(ore, amount - stored);
    }

    public bool IsDiscovered(MineOreDefinition ore) => ore != null && _discovered.Contains(ore.OreId);

    public bool IsVisited(Vector2Int cell) => _visited.Contains(cell);

    public bool IsChestOpened(Vector2Int cell) => _openedChests.Contains(cell);

    public MazeRock RockAt(Vector2Int cell) => _rocks.TryGetValue(cell, out var rock) ? rock : null;

    /// <summary>입구(시작점)에 서 있음</summary>
    public bool OnEntrance => Position == _map.Start;

    /// <summary>아래층 계단 위에 서 있음</summary>
    public bool OnStairs => _map.Cell(Position) == MazeCell.Stairs;

    /// <summary>지금 치명타 확률 (광산 개편과 같음, 날 갈기 없음)</summary>
    public float CritChance => Mathf.Clamp01(_rework.BaseCritChance + Mathf.Min(10, Combo / 5) * _rework.CritPerFiveCombo);

    /// <summary>이번 탐험 시간 (초)</summary>
    public double RunSeconds(DateTime utcNow) => Inside ? Math.Max(0, (utcNow - _runStartUtc).TotalSeconds) : 0;

    /// <summary>보는 쪽 기준 그 방향 칸</summary>
    public Vector2Int CellToward(MazeTurn turn) => Position + MazeDirs.Step(MazeDirs.Turn(Facing, turn));

    /// <summary>그 방향 칸에 있는 것</summary>
    public MazeAhead Look(MazeTurn turn) => What(CellToward(turn));

    public MazeAhead What(Vector2Int cell)
    {
        switch (_map.Cell(cell))
        {
            case MazeCell.Wall: return MazeAhead.Wall;
            case MazeCell.Stairs: return MazeAhead.Stairs;
            case MazeCell.Start: return MazeAhead.Entrance;
            case MazeCell.Chest: return _openedChests.Contains(cell) ? MazeAhead.OpenedChest : MazeAhead.Chest;
            case MazeCell.Rock: return _rocks[cell].Present ? MazeAhead.Rock : MazeAhead.Open;
            default: return MazeAhead.Open;
        }
    }

    /// <summary>그 칸으로 들어갈 수 있음 (벽 · 막는 돌 · 닫힌 상자가 아님)</summary>
    public bool CanEnter(Vector2Int cell)
    {
        var what = What(cell);
        if (what == MazeAhead.Wall || what == MazeAhead.Chest)
            return false;
        return what != MazeAhead.Rock || !_rocks[cell].Blocking;
    }

    /// <summary>앞에 있는 돌 (없으면 null)</summary>
    public MazeRock RockAhead
    {
        get
        {
            var rock = RockAt(CellToward(MazeTurn.Forward));
            return rock != null && rock.Present ? rock : null;
        }
    }

    #endregion

    #region 들어가기 · 나가기

    /// <summary>층 입구에서 시작 (나갔다 오면 늘 입구). 나온 지 얼마 안 됐으면 들어가지 않음 (NextEnterUtc)</summary>
    /// <returns>들어갔으면 true</returns>
    public bool Enter(DateTime utcNow)
    {
        if (!CanEnterAt(utcNow))
            return false;
        Community.Discover(CurrentFloor, _fishingAccess);
        Inside = true;
        Position = _map.Start;
        Facing = _map.StartFacing;
        Combo = 0;
        _runStartUtc = utcNow;
        _runGains.Clear();
        _runOrder.Clear();
        _runNew.Clear();
        _runRocks = _runChests = _runNewCells = 0;
        _runBroken.Clear();
        Visit(Position);
        Tick(utcNow);
        Changed?.Invoke();
        return true;
    }

    /// <summary>나가기 (어디서든 바로). 지도 해달이 올 차례면 옴</summary>
    public MazeRunResult Exit(DateTime utcNow)
    {
        var result = new MazeRunResult
        {
            Seconds = RunSeconds(utcNow),
            RocksBroken = _runRocks,
            ChestsOpened = _runChests,
            NewCells = _runNewCells,
            FloorRocks = FloorRockCount,
            RocksCleared = RunRocksCleared,
        };
        foreach (var ore in _runOrder)
            result.Gains.Add(new MineGain(ore, _runGains[ore], MineGainSource.Rock, _runNew.Contains(ore)));
        if (!Inside)
            return result;

        Inside = false;
        NextEnterUtc = utcNow.AddSeconds(_config.ReenterCooldownSeconds);
        // 들락날락만으로는 채우지 못하게: 이 층의 돌을 모두 한 번씩 부숴야 다녀온 횟수 1번
        result.RunCounted = RunCleared;
        if (result.RunCounted)
            Runs++;
        if (!MapOtter && result.RunCounted && (Runs >= _config.MapOtterRuns || (Runs == 1 && _visited.Count >= _config.MapOtterEarlyCells)))
        {
            MapOtter = true;
            result.MapOtterArrived = true;
        }
        Position = _map.Start;
        Facing = _map.StartFacing;
        Combo = 0;
        Changed?.Invoke();
        return result;
    }

    #endregion

    #region 가기

    /// <summary>
    /// 그쪽으로 돌아서 한 칸 간다 (왼쪽 · 오른쪽 · 뒤는 돌아선 다음 한 칸). 벽이면 그대로,
    /// 막는 돌 · 닫힌 상자면 그쪽을 보고 멈춤 (앞에 나타남)
    /// </summary>
    public MazeMove Go(MazeTurn turn)
    {
        if (!Inside)
            return MazeMove.Wall;
        var dir = MazeDirs.Turn(Facing, turn);
        var target = Position + MazeDirs.Step(dir);
        if (_map.IsWall(target))
            return MazeMove.Wall;
        Facing = dir;
        if (!CanEnter(target))
            return MazeMove.Blocked;
        Position = target;
        Combo = 0;
        Visit(target);
        return MazeMove.Moved;
    }

    /// <summary>그쪽을 보기만 함 (가지 않음)</summary>
    public void Face(MazeTurn turn)
    {
        Facing = MazeDirs.Turn(Facing, turn);
        Combo = 0;
    }

    private void Visit(Vector2Int cell)
    {
        if (_visited.Add(cell))
            _runNewCells++;
    }

    #endregion

    #region 돌 치기

    /// <summary>앞 돌을 한 번 친다 (time = 지금 시각, 콤보). 깨면 광석 · 다시 생길 때까지 지나갈 수 있음</summary>
    /// <returns>앞에 돌이 없으면 null</returns>
    public MineHit HitAhead(float time, DateTime utcNow)
    {
        var rock = RockAhead;
        if (!Inside || rock == null)
            return null;

        Combo = time - _lastHitTime <= _rework.ComboWindow ? Combo + 1 : 1;
        _lastHitTime = time;
        var hit = new MineHit { Combo = Combo, Shiny = rock.Shiny };
        hit.Crit = _rng.NextDouble() < CritChance;
        hit.Damage = Pickaxe.Power * (hit.Crit ? 2 : 1);
        rock.Hp = Mathf.Max(0, rock.Hp - hit.Damage);
        if (rock.Hp > 0)
            return hit;

        hit.Broken = true;
        RollBreak(rock, hit);
        rock.Present = false;
        rock.Opened = true;
        _runBroken.Add(rock.Cell);
        rock.RespawnUtc = utcNow.AddSeconds(_config.RockRespawnSeconds);
        RocksBroken++;
        _runRocks++;
        Changed?.Invoke();
        return hit;
    }

    // 광산 개편(MineReworkModel.RollBreak)과 같은 규칙: 광맥 광석(반짝이면 하나 더) + 콤보 덤 + 희귀(반짝이면 반드시) + 화석 (대박은 미로에 없음)
    private void RollBreak(MazeRock rock, MineHit hit)
    {
        var layer = Layer;
        var gains = new Dictionary<MineOreDefinition, int>();
        var order = new List<MineOreDefinition>();
        var vein = rock.Vein != null ? rock.Vein : _rework.Stone;
        var yield = vein == _rework.Stone ? _rework.StoneYield : _rework.VeinYield;
        int amount = _rng.Next(yield.x, yield.y + 1) + (rock.Shiny ? 1 : 0) + Mathf.Min(3, hit.Combo / _rework.ComboBonusEvery);
        Add(gains, order, vein, amount);

        float rareChance = layer.RareChance + Pickaxe.RareBonus;
        if (layer.RareDrops.Count > 0 && (rock.Shiny || _rng.NextDouble() < rareChance))
            Add(gains, order, Pick(layer.RareDrops), 1);

        var relic = layer.Relic;
        if (relic != null && !IsDiscovered(relic) && _rng.NextDouble() < layer.RelicChance * (rock.Shiny ? 3f : 1f))
            Add(gains, order, relic, 1);

        foreach (var ore in order)
            hit.Gains.Add(AddOre(ore, gains[ore]));
    }

    private static void Add(Dictionary<MineOreDefinition, int> gains, List<MineOreDefinition> order, MineOreDefinition ore, int amount)
    {
        if (ore == null || amount <= 0)
            return;
        if (!gains.TryGetValue(ore, out int n))
            order.Add(ore);
        gains[ore] = n + amount;
    }

    private void Spawn(MazeRock rock)
    {
        var layer = Layer;
        rock.Vein = RollVein(layer, false);
        rock.Shiny = _rng.NextDouble() < _rework.ShinyChance;
        // 반짝 광맥은 돌이 아닌 광맥으로 (광산 개편과 같음)
        if (rock.Shiny && rock.Vein == _rework.Stone)
            rock.Vein = RollVein(layer, true);
        rock.MaxHp = Mathf.CeilToInt(layer.RockHp * (rock.Shiny ? _rework.ShinyHpMultiplier : 1f));
        rock.Hp = rock.MaxHp;
        rock.Present = true;
    }

    private MineOreDefinition RollVein(MineLayerDefinition layer, bool skipStone)
    {
        var veins = new List<MineDrop>();
        foreach (var drop in layer.Veins)
        {
            if (drop != null && drop.Ore != null && drop.Weight > 0 && !(skipStone && drop.Ore == _rework.Stone))
                veins.Add(drop);
        }
        return (veins.Count > 0 ? Pick(veins) : null) ?? _rework.Stone;
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

    /// <summary>시간이 흐름: 다시 생길 때가 된 돌이 같은 자리에 생김</summary>
    public void Tick(DateTime utcNow)
    {
        foreach (var rock in _rocks.Values)
        {
            if (rock.Present || utcNow < rock.RespawnUtc)
                continue;
            Spawn(rock);
            RockRespawned?.Invoke(rock.Cell);
        }
    }

    /// <summary>다음에 다시 생길 돌까지 남은 초 (부순 돌이 없으면 -1)</summary>
    public double NextRespawnIn(DateTime utcNow)
    {
        double best = -1;
        foreach (var rock in _rocks.Values)
        {
            if (rock.Present)
                continue;
            double left = Math.Max(0, (rock.RespawnUtc - utcNow).TotalSeconds);
            if (best < 0 || left < best)
                best = left;
        }
        return best;
    }

    #endregion

    #region 보물 상자

    /// <summary>앞의 닫힌 상자를 연다 (한 번만). 나온 것</summary>
    /// <returns>앞에 닫힌 상자가 없으면 null</returns>
    public List<MineGain> OpenChestAhead()
    {
        var cell = CellToward(MazeTurn.Forward);
        if (!Inside || What(cell) != MazeAhead.Chest)
            return null;
        _openedChests.Add(cell);
        _runChests++;
        var gains = new List<MineGain>();
        foreach (var loot in _config.ChestLoot)
        {
            if (loot == null || loot.Ore == null || _rng.NextDouble() >= loot.Chance)
                continue;
            int amount = _rng.Next(loot.Min, loot.Max + 1);
            if (amount > 0)
                gains.Add(AddOre(loot.Ore, amount));
        }
        Changed?.Invoke();
        return gains;
    }

    #endregion

    #region 광석 · 저장

    private MineGain AddOre(MineOreDefinition ore, int amount)
    {
        Put(ore, amount);
        bool isNew = _discovered.Add(ore.OreId);
        if (!_runGains.TryGetValue(ore, out int n))
            _runOrder.Add(ore);
        _runGains[ore] = n + amount;
        if (isNew)
        {
            _runNew.Add(ore);
            Discovered?.Invoke(ore);
        }
        return new MineGain(ore, amount, MineGainSource.Rock, isNew);
    }

    private void Load(MineMazeSave save)
    {
        PickaxeTier = Mathf.Clamp(save.pickaxeTier, 0, _rework.Pickaxes.Count - 1);
        Runs = Mathf.Max(0, save.runs);
        MapOtter = save.mapOtter;
        NextEnterUtc = save.nextEnterUtcTicks > 0 ? new DateTime(save.nextEnterUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
        RocksBroken = Mathf.Max(0, save.rocksBroken);
        foreach (var entry in save.ores)
        {
            var ore = entry != null ? _rework.FindOre(entry.id) : null;
            if (ore != null && entry.count > 0)
                _counts[ore] = entry.count;
        }
        foreach (var id in save.discovered)
        {
            if (!string.IsNullOrEmpty(id))
                _discovered.Add(id);
        }
    }

    private void LoadFloor(MazeFloorSave save)
    {
        foreach (var key in save.visited)
        {
            if (TryCell(key, out var cell) && !_map.IsWall(cell))
                _visited.Add(cell);
        }
        foreach (var key in save.openedChests)
        {
            if (TryCell(key, out var cell) && _map.Cell(cell) == MazeCell.Chest)
                _openedChests.Add(cell);
        }

        var saved = new Dictionary<Vector2Int, MazeRockSave>();
        foreach (var entry in save.rocks)
        {
            if (entry != null)
                saved[new Vector2Int(entry.x, entry.y)] = entry;
        }
        foreach (var rock in _rocks.Values)
        {
            if (!saved.TryGetValue(rock.Cell, out var entry))
            {
                // 처음: 모든 돌이 길을 막고 있음
                Spawn(rock);
                continue;
            }
            rock.Opened = entry.opened;
            rock.Present = entry.present;
            rock.RespawnUtc = entry.respawnUtcTicks > 0 ? new DateTime(entry.respawnUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
            if (!rock.Present)
                continue;
            rock.Vein = _rework.FindOre(entry.vein) ?? _rework.Stone;
            rock.MaxHp = Mathf.Max(1, entry.maxHp);
            rock.Hp = Mathf.Clamp(entry.hp, 1, rock.MaxHp);
            rock.Shiny = entry.shiny;
        }
    }

    private static bool TryCell(string key, out Vector2Int cell)
    {
        cell = default;
        if (string.IsNullOrEmpty(key))
            return false;
        var parts = key.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            return false;
        cell = new Vector2Int(x, y);
        return true;
    }

    private static string Key(Vector2Int cell) => $"{cell.x},{cell.y}";

    public MineMazeSave ToSave()
    {
        var save = new MineMazeSave
        {
            pickaxeTier = PickaxeTier,
            runs = Runs,
            mapOtter = MapOtter,
            nextEnterUtcTicks = NextEnterUtc > DateTime.MinValue ? NextEnterUtc.Ticks : 0,
            rocksBroken = RocksBroken,
        };
        SaveProgress(save);
        foreach (var ore in _rework.Ores)
        {
            int n = StoredCount(ore);
            if (ore != null && n > 0)
                save.ores.Add(new MineOreCountSave { id = ore.OreId, count = n });
        }
        save.discovered.AddRange(_discovered);
        foreach (var cell in _visited)
            save.visited.Add(Key(cell));
        foreach (var cell in _openedChests)
            save.openedChests.Add(Key(cell));
        foreach (var rock in _rocks.Values)
        {
            save.rocks.Add(new MazeRockSave
            {
                x = rock.Cell.x,
                y = rock.Cell.y,
                opened = rock.Opened,
                present = rock.Present,
                vein = rock.Present && rock.Vein != null ? rock.Vein.OreId : "",
                hp = rock.Hp,
                maxHp = rock.MaxHp,
                shiny = rock.Shiny,
                respawnUtcTicks = rock.Present ? 0 : rock.RespawnUtc.Ticks,
            });
        }
        save.currentFloor = CurrentFloor;
        _floorStates[CurrentFloor] = MazeFloorSave.From(save, CurrentFloor);
        save.floors.AddRange(_floorStates.Values);
        return save;
    }

    #endregion

    #region 테스트 치트

    public void CheatMapOtter()
    {
        MapOtter = true;
        Changed?.Invoke();
    }

    /// <summary>부순 돌을 모두 바로 다시</summary>
    public void CheatRespawnAll(DateTime utcNow)
    {
        foreach (var rock in _rocks.Values)
        {
            if (!rock.Present)
                rock.RespawnUtc = utcNow;
        }
        Tick(utcNow);
    }

    /// <summary>곡괭이 단계 바꾸기 (재료 없이)</summary>
    public void CheatPickaxe(int delta)
    {
        PickaxeTier = Mathf.Clamp(PickaxeTier + delta, 0, _rework.Pickaxes.Count - 1);
        Changed?.Invoke();
    }

    /// <summary>앞 돌을 반짝 광맥으로</summary>
    public bool CheatShinyAhead()
    {
        var rock = RockAhead;
        if (rock == null)
            return false;
        rock.Shiny = true;
        rock.Vein = RollVein(Layer, true);
        rock.MaxHp = rock.Hp = Mathf.CeilToInt(Layer.RockHp * _rework.ShinyHpMultiplier);
        return true;
    }

    #endregion
}
