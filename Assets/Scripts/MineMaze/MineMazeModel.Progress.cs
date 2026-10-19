using System;
using System.Collections.Generic;
using UnityEngine;

// One complete progression loop, isolated from the main game's economy.
public partial class MineMazeModel
{
    public int Gold { get; private set; }
    public int KingdomLevel { get; private set; }
    public int GateDamage { get; private set; }
    public bool FloorCleared { get; private set; }
    private double _minerProgress;
    public int GateRemaining => Math.Max(0, _config.GateHits - GateDamage);

    /// <summary>본 게임에서 들어왔을 때: 층 돌파 조건을 본 게임 왕국 레벨로 (테스트 레벨 대신)</summary>
    public void UseKingdomLevel(int level)
    {
        level = Math.Max(1, level);
        if (level == KingdomLevel) return;
        KingdomLevel = level;
        Changed?.Invoke();
    }
    public MinePickaxeDefinition NextPickaxe => PickaxeTier < Math.Min(_rework.Pickaxes.Count - 1, FloorConfig(FloorCount - 1).GatePickaxeTier)
        ? _rework.Pickaxes[PickaxeTier + 1] : null;
    public double MinerInterval => _rework.MinerSeconds * (1 - Mathf.Clamp(Pickaxe.MinerSpeedBonus, 0, .9f));
    public double MinerRemaining => Math.Max(0, MinerInterval - _minerProgress);

    private void LoadProgress(MineMazeSave save)
    {
        Gold = save.progressionVersion > 0 ? Math.Max(0, save.gold) : Math.Max(0, _rework.StartGold);
        KingdomLevel = save.progressionVersion > 0 ? Math.Max(1, save.kingdomLevel) : _config.TestKingdomLevel;
        if (FloorCount > 1 && save.progressionVersion < 2)
            KingdomLevel = Math.Max(KingdomLevel, _rootConfig.TestKingdomLevel);
        GateDamage = Mathf.Clamp(save.gateDamage, 0, _config.GateHits);
        FloorCleared = save.floorCleared || GateDamage == _config.GateHits;
        if (FloorCleared) GateDamage = _config.GateHits;
        _minerProgress = double.IsNaN(save.minerProgressSeconds) || double.IsInfinity(save.minerProgressSeconds)
            ? 0 : Math.Max(0, Math.Min(save.minerProgressSeconds, MinerInterval));
    }

    private void SaveProgress(MineMazeSave save)
    {
        save.progressionVersion = 2;
        save.gold = Gold;
        save.kingdomLevel = KingdomLevel;
        save.gateDamage = GateDamage;
        save.floorCleared = FloorCleared;
        save.minerProgressSeconds = _minerProgress;
    }

    // Aggregate duplicate ingredients before checking/deducting, so a recipe is atomic.
    private Dictionary<MineOreDefinition, int> Recipe(MinePickaxeDefinition pickaxe)
    {
        var costs = new Dictionary<MineOreDefinition, int>();
        foreach (var cost in pickaxe.Recipe)
        {
            if (cost == null || cost.Ore == null) continue;
            costs.TryGetValue(cost.Ore, out int count);
            costs[cost.Ore] = checked(count + cost.Amount);
        }
        return costs;
    }

    public bool CanCraft(out string reason)
    {
        var next = NextPickaxe;
        reason = "이번 테스트의 곡괭이를 완성했어요";
        if (next == null) return false;
        reason = "골드가 부족해요. 남는 광석을 판매해 보세요";
        if (Gold < next.GoldCost) return false;
        foreach (var cost in Recipe(next))
        {
            reason = $"{cost.Key.DisplayName} {cost.Value}개가 필요해요";
            if (Count(cost.Key) < cost.Value) return false;
        }
        reason = "";
        return true;
    }

    public bool TryCraft(out string reason)
    {
        if (!CanCraft(out reason)) return false;
        var next = NextPickaxe;
        foreach (var cost in Recipe(next)) _counts[cost.Key] -= cost.Value;
        Gold -= next.GoldCost;
        PickaxeTier++;
        Changed?.Invoke();
        return true;
    }

    public bool Sell(MineOreDefinition ore, int amount)
    {
        if (ore == null || !ore.CanSell || amount <= 0 || Count(ore) < amount) return false;
        long value = (long)ore.SellPrice * amount;
        if (value > int.MaxValue - Gold) return false;
        _counts[ore] -= amount;
        Gold += (int)value;
        Changed?.Invoke();
        return true;
    }

    public bool CanHitGate(out string reason)
    {
        reason = "계단 위에서 아래층의 돌을 확인해요";
        if (!Inside || !OnStairs) return false;
        reason = "3층 탐험은 여기까지예요. 더 깊은 갱도는 아직 닫혀 있어요";
        if (AtContentEnd) return false;
        reason = "이미 열린 길이에요";
        if (FloorCleared) return false;
        reason = $"왕국 Lv.{_config.GateKingdomLevel} · {_rework.Pickaxes[_config.GatePickaxeTier].DisplayName} 필요";
        if (KingdomLevel < _config.GateKingdomLevel || PickaxeTier < _config.GatePickaxeTier) return false;
        reason = "";
        return true;
    }

    public bool HitGate(out string reason)
    {
        if (!CanHitGate(out reason)) return false;
        GateDamage = Math.Min(_config.GateHits, GateDamage + (_rng.NextDouble() < _rework.BaseCritChance ? 2 : 1));
        if (GateRemaining == 0)
        {
            FloorCleared = true;
            _minerProgress = 0;
        }
        Changed?.Invoke();
        return true;
    }

    // During active exploration production runs at full speed. Only actual absence is slowed.
    // Callers own the elapsed-time boundary and must not settle the same interval twice.
    public int AdvanceMiner(double seconds, bool offline = false)
    {
        bool anyCleared = false;
        for (int i = 0; i < FloorCount; i++) anyCleared |= IsFloorCleared(i);
        if (!anyCleared || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return 0;
        if (offline) seconds = Math.Min(seconds, 8 * 3600) / _rework.OfflineSlowdown;
        _minerProgress += seconds;
        int finds = (int)Math.Min(10000, Math.Floor(_minerProgress / MinerInterval));
        _minerProgress %= MinerInterval;
        if (offline) finds = Math.Min(finds, _rework.OfflineMaxFinds);
        var drops = new List<MineDrop>();
        for (int floor = 0; floor < FloorCount; floor++)
        {
            if (!IsFloorCleared(floor)) continue;
            foreach (var drop in FloorConfig(floor).Layer.Veins)
                if (drop != null && drop.Ore != null && drop.Weight > 0 && drop.Ore.Rarity <= MineOreRarity.Uncommon)
                    drops.Add(drop);
        }
        if (drops.Count == 0) return 0;
        for (int i = 0; i < finds; i++)
        {
            var ore = Pick(drops);
            if (ore == null) continue;
            _counts[ore] = Count(ore) + 1;
            _discovered.Add(ore.OreId);
        }
        if (finds > 0) Changed?.Invoke();
        return finds;
    }
}
