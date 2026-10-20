using System;
using System.Collections.Generic;
using UnityEngine;

// One complete progression loop, isolated from the main game's economy.
public partial class MineMazeModel
{
    public MineCommunityState Community { get; private set; }
    private bool _fishingAccess = true; // standalone test scenes
    public void UseFishingAccess(bool open) => _fishingAccess = open;
    public bool IsSourceFloor => _config.GasSource;
    // 테스트 씬의 갱도 골드. 본 게임은 지갑(진짜 골드)을 씀
    private int _gold;
    private IMineWallet _wallet;
    public int Gold => _wallet != null ? _wallet.Balance : _gold;

    /// <summary>본 게임: 작업대 · 판매가 진짜 골드를 씀. 예전 갱도 골드는 한 번 진짜 골드로 옮김</summary>
    public void UseWallet(IMineWallet wallet)
    {
        _wallet = wallet;
        if (wallet != null && _gold > 0)
        {
            wallet.Earn(_gold);
            _gold = 0;
        }
        Changed?.Invoke();
    }
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
        Community = save.community ?? new MineCommunityState();
        _gold = save.progressionVersion > 0 ? Math.Max(0, save.gold) : Math.Max(0, _rework.StartGold);
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
        save.community = Community;
        save.progressionVersion = 2;
        save.gold = _gold;
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
        if (_wallet != null) { if (next.GoldCost > 0 && !_wallet.TrySpend(next.GoldCost)) return false; }
        else _gold -= next.GoldCost;
        foreach (var cost in Recipe(next)) Take(cost.Key, cost.Value);
        PickaxeTier++;
        Changed?.Invoke();
        return true;
    }

    public bool Sell(MineOreDefinition ore, int amount)
    {
        if (ore == null || !ore.CanSell || amount <= 0 || Count(ore) < amount) return false;
        long value = (long)ore.SellPrice * amount;
        if (value > int.MaxValue - Gold) return false;
        Take(ore, amount);
        if (_wallet != null) _wallet.Earn((int)value);
        else _gold += (int)value;
        Changed?.Invoke();
        return true;
    }

    public bool CanHitGate(out string reason)
    {
        reason = "계단 위에서 아래층의 돌을 확인해요";
        if (!Inside || !OnStairs) return false;
        reason = "3층 탐험은 여기까지예요. 더 깊은 갱도는 아직 닫혀 있어요";
        if (AtContentEnd && !IsSourceFloor) return false;
        reason = "선착장을 수리하고 낚시터를 개방하면 심층을 탐험할 수 있어요";
        if (CurrentFloor >= 2 && !_fishingAccess) return false;
        reason = "마을 게시판에서 약초 부탁을 마치고 임시 진료를 시작해 주세요";
        if (IsSourceFloor && (!Community.started || !Community.treated)) return false;
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
            if (IsSourceFloor) Community.RemoveSource();
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
                if (drop != null && drop.Ore != null && drop.Weight > 0 && drop.Ore.Rarity != MineOreRarity.Relic)
                    drops.Add(drop);
        }
        if (drops.Count == 0) return 0;
        var found = new Dictionary<MineOreDefinition, int>();
        for (int i = 0; i < finds; i++)
        {
            var ore = Pick(drops);
            if (ore == null) continue;
            found.TryGetValue(ore, out int n);
            found[ore] = n + 1;
            _discovered.Add(ore.OreId);
        }
        foreach (var pair in found) Put(pair.Key, pair.Value);
        if (finds > 0) Changed?.Invoke();
        return finds;
    }
}
