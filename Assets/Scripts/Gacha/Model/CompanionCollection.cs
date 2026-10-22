using System;
using System.Collections.Generic;

/// <summary>편성 칸에 넣은 결과</summary>
public enum FormationOutcome
{
    Assigned,
    Locked,      // 아직 해금 안 한 해달
    NoSlot,      // 열리지 않은 칸
    SameBonus,   // 같은 버프 종류의 해달이 다른 칸에 있음
}

/// <summary>장신구를 얻은 결과</summary>
public enum AccessoryGain
{
    New,      // 처음 얻음
    Enhanced, // 같은 장신구가 있어 강화 +1
    Shards,   // 이미 최대 강화라 반짝 조각으로
}

/// <summary>
/// 특수 해달 명부 · 편성 · 장신구 (순수 C#, Docs/가챠개편_특수해달조각_장신구_기획.md 3 · 4장). 세이브는 GachaSaveData의 목록.
/// - 조각: 쌓이고, [해금] · [별 승급]에서 씀. 5성까지 다 쓰고도 남는 조각은 반짝 조각으로 (레어 1 · 에픽 2개씩)
/// - 편성: 열린 칸(1~3)에 해금한 해달. 같은 버프 종류는 한 마리만
/// - 장신구: 종류마다 하나만 가짐. 같은 것이 또 나오면 강화 +1(최대 +4), 그 뒤로는 반짝 조각(일반 1 · 레어 5 · 에픽 30).
///   주민 해달 한 마리에 하나, 한 장신구는 한 해달에게만 (다른 해달에게 끼우면 옮겨 감)
/// 데이터에 없는 해달 · 장신구의 기록은 지우지 않고 그대로 저장 (데이터를 잠깐 뺐다 돌려놓아도 잃지 않게)
/// </summary>
public sealed class CompanionCollection
{
    public const int MaxSlots = 3;

    /// <summary>별 단계마다 드는 조각 (0 → 1 = 해금, …, 4 → 5): 레어 · 에픽 (5단계 밸런스: 특정 레어 해금 약 60회 · 에픽 약 180회 · 레어 5성 약 700회)</summary>
    public static readonly int[] RareStarCosts = { 10, 15, 20, 30, 45 };
    public static readonly int[] EpicStarCosts = { 12, 18, 25, 40, 65 };

    /// <summary>5성까지 쓰고 남는 조각 하나가 바뀌는 반짝 조각 (레어 · 에픽)</summary>
    public const int RareOverflowShards = 1;
    public const int EpicOverflowShards = 2;

    /// <summary>교환소에서 해달 조각 하나를 고르는 값 (반짝 조각, 레어 · 에픽)</summary>
    public const int RareExchangeShards = 3;
    public const int EpicExchangeShards = 5;

    private readonly Dictionary<string, SpecialOtterSaveData> _otters = new Dictionary<string, SpecialOtterSaveData>();
    private readonly Dictionary<string, AccessorySaveData> _accessories = new Dictionary<string, AccessorySaveData>();
    private readonly string[] _formation = new string[MaxSlots];
    private readonly Func<string, AccessoryDefinition> _findAccessory;

    /// <param name="findAccessory">장신구 ID → 데이터 (효과를 셀 때. 없으면 효과 0)</param>
    public CompanionCollection(Func<string, AccessoryDefinition> findAccessory = null)
    {
        _findAccessory = findAccessory;
    }

    #region 세이브

    public void Load(GachaSaveData saved)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));
        _otters.Clear();
        _accessories.Clear();
        Array.Clear(_formation, 0, _formation.Length);

        if (saved.specialOtters != null)
        {
            foreach (var otter in saved.specialOtters)
            {
                if (otter == null || string.IsNullOrEmpty(otter.otterId) || _otters.ContainsKey(otter.otterId))
                    continue;
                _otters[otter.otterId] = new SpecialOtterSaveData
                {
                    otterId = otter.otterId,
                    fragments = Math.Max(0, otter.fragments),
                    stars = Math.Max(0, Math.Min(SpecialOtterDefinition.MaxStars, otter.stars)),
                };
            }
        }
        if (saved.accessories != null)
        {
            foreach (var accessory in saved.accessories)
            {
                if (accessory == null || string.IsNullOrEmpty(accessory.accessoryId) || _accessories.ContainsKey(accessory.accessoryId))
                    continue;
                _accessories[accessory.accessoryId] = new AccessorySaveData
                {
                    accessoryId = accessory.accessoryId,
                    plus = Math.Max(0, Math.Min(AccessoryDefinition.MaxPlus, accessory.plus)),
                    otterId = string.IsNullOrEmpty(accessory.otterId) ? null : accessory.otterId,
                };
            }
            // 한 해달에 장신구 둘이 기록돼 있으면 앞의 것만 남김
            var wearers = new HashSet<string>();
            foreach (var accessory in _accessories.Values)
            {
                if (accessory.otterId != null && !wearers.Add(accessory.otterId))
                    accessory.otterId = null;
            }
        }
        if (saved.formation != null)
        {
            for (int i = 0; i < MaxSlots && i < saved.formation.Count; i++)
            {
                string id = saved.formation[i];
                if (!string.IsNullOrEmpty(id) && Stars(id) > 0 && Array.IndexOf(_formation, id) < 0)
                    _formation[i] = id;
            }
        }
    }

    public void Write(GachaSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        result.specialOtters ??= new List<SpecialOtterSaveData>();
        result.specialOtters.Clear();
        foreach (var otter in _otters.Values)
            result.specialOtters.Add(new SpecialOtterSaveData { otterId = otter.otterId, fragments = otter.fragments, stars = otter.stars });
        result.accessories ??= new List<AccessorySaveData>();
        result.accessories.Clear();
        foreach (var accessory in _accessories.Values)
            result.accessories.Add(new AccessorySaveData { accessoryId = accessory.accessoryId, plus = accessory.plus, otterId = accessory.otterId });
        result.formation ??= new List<string>();
        result.formation.Clear();
        // 뒤쪽 빈 칸은 쓰지 않음 (편성한 적 없는 세이브는 그대로 빈 목록)
        int last = _formation.Length - 1;
        while (last >= 0 && string.IsNullOrEmpty(_formation[last]))
            last--;
        for (int i = 0; i <= last; i++)
            result.formation.Add(_formation[i] ?? "");
    }

    #endregion

    #region 특수 해달 · 조각 · 별

    public int Fragments(SpecialOtterDefinition otter) => otter != null && _otters.TryGetValue(otter.OtterId, out var s) ? s.fragments : 0;
    public int Stars(SpecialOtterDefinition otter) => otter != null ? Stars(otter.OtterId) : 0;
    public int Stars(string otterId) => !string.IsNullOrEmpty(otterId) && _otters.TryGetValue(otterId, out var s) ? s.stars : 0;
    public bool IsUnlocked(SpecialOtterDefinition otter) => Stars(otter) > 0;

    /// <summary>이 해달의 조각을 한 번이라도 얻었는지 (처음 만나면 결과 카드에 "새로운!")</summary>
    public bool HasMet(SpecialOtterDefinition otter) => otter != null && _otters.ContainsKey(otter.OtterId);

    /// <summary>지금 별에서 다음 별까지 드는 조각 (0성이면 해금 값, 5성이면 0)</summary>
    public static int NextStarCost(SpecialOtterDefinition otter, int stars)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        var costs = otter.IsEpic ? EpicStarCosts : RareStarCosts;
        return stars >= 0 && stars < costs.Length ? costs[stars] : 0;
    }

    /// <summary>지금부터 5성까지 더 드는 조각 합</summary>
    public static int CostToMax(SpecialOtterDefinition otter, int stars)
    {
        int total = 0;
        for (int s = Math.Max(0, stars); s < SpecialOtterDefinition.MaxStars; s++)
            total += NextStarCost(otter, s);
        return total;
    }

    /// <summary>
    /// 조각을 넣는다. 5성까지 다 써도 남는 몫은 반짝 조각으로 바꿔 돌려줌 (넣은 조각 수가 그대로 쌓이지 않을 수 있음)
    /// </summary>
    /// <returns>바뀐 반짝 조각 수</returns>
    public int AddFragments(SpecialOtterDefinition otter, int count)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (count <= 0)
            return 0;
        var state = StateOf(otter.OtterId);
        int room = Math.Max(0, CostToMax(otter, state.stars) - state.fragments);
        int kept = Math.Min(room, count);
        state.fragments += kept;
        int overflow = count - kept;
        return overflow * (otter.IsEpic ? EpicOverflowShards : RareOverflowShards);
    }

    public bool CanStarUp(SpecialOtterDefinition otter)
    {
        int stars = Stars(otter);
        return otter != null && stars < SpecialOtterDefinition.MaxStars && Fragments(otter) >= NextStarCost(otter, stars);
    }

    /// <summary>조각을 써서 해금(0 → 1성)하거나 별을 하나 올림</summary>
    public bool TryStarUp(SpecialOtterDefinition otter)
    {
        if (!CanStarUp(otter))
            return false;
        var state = StateOf(otter.OtterId);
        state.fragments -= NextStarCost(otter, state.stars);
        state.stars++;
        return true;
    }

    /// <summary>교환소: 반짝 조각으로 이 해달 조각 하나를 고르는 값</summary>
    public static int ExchangeCost(SpecialOtterDefinition otter) =>
        otter == null ? throw new ArgumentNullException(nameof(otter)) : otter.IsEpic ? EpicExchangeShards : RareExchangeShards;

    private SpecialOtterSaveData StateOf(string otterId)
    {
        if (!_otters.TryGetValue(otterId, out var state))
            _otters[otterId] = state = new SpecialOtterSaveData { otterId = otterId };
        return state;
    }

    #endregion

    #region 편성

    public IReadOnlyList<string> Formation => _formation;

    /// <summary>열린 칸 수만큼 편성 (slotCount: 지금 열린 칸, 1~3)</summary>
    public FormationOutcome Assign(int slot, SpecialOtterDefinition otter, int slotCount, Func<string, SpecialOtterDefinition> findOtter)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (slot < 0 || slot >= Math.Min(MaxSlots, slotCount))
            return FormationOutcome.NoSlot;
        if (!IsUnlocked(otter))
            return FormationOutcome.Locked;
        for (int i = 0; i < MaxSlots; i++)
        {
            if (i == slot || string.IsNullOrEmpty(_formation[i]) || _formation[i] == otter.OtterId)
                continue;
            var other = findOtter != null ? findOtter(_formation[i]) : null;
            if (other != null && other.Bonus == otter.Bonus)
                return FormationOutcome.SameBonus;
        }
        // 다른 칸에 있던 같은 해달은 이 칸으로 옮김
        int previous = Array.IndexOf(_formation, otter.OtterId);
        if (previous >= 0)
            _formation[previous] = null;
        _formation[slot] = otter.OtterId;
        return FormationOutcome.Assigned;
    }

    public void ClearSlot(int slot)
    {
        if (slot >= 0 && slot < MaxSlots)
            _formation[slot] = null;
    }

    /// <summary>열린 칸에 편성된 해달들이 주는 이 종류의 버프 합 %</summary>
    public int FormationPercent(KingdomBonusKind kind, int slotCount, Func<string, SpecialOtterDefinition> findOtter)
    {
        if (findOtter == null)
            return 0;
        int total = 0;
        for (int i = 0; i < Math.Min(MaxSlots, slotCount); i++)
        {
            var otter = string.IsNullOrEmpty(_formation[i]) ? null : findOtter(_formation[i]);
            if (otter != null && otter.Bonus == kind)
                total += otter.PercentAt(Stars(otter));
        }
        return total;
    }

    #endregion

    #region 장신구

    public bool Owns(AccessoryDefinition accessory) => accessory != null && _accessories.ContainsKey(accessory.AccessoryId);
    public int Plus(AccessoryDefinition accessory) => accessory != null && _accessories.TryGetValue(accessory.AccessoryId, out var s) ? s.plus : 0;

    /// <summary>장신구를 얻음: 처음이면 가짐, 있으면 강화 +1, 최대 강화면 반짝 조각(shards)으로</summary>
    public AccessoryGain AddAccessory(AccessoryDefinition accessory, out int shards)
    {
        if (accessory == null)
            throw new ArgumentNullException(nameof(accessory));
        shards = 0;
        if (!_accessories.TryGetValue(accessory.AccessoryId, out var state))
        {
            _accessories[accessory.AccessoryId] = new AccessorySaveData { accessoryId = accessory.AccessoryId };
            return AccessoryGain.New;
        }
        if (state.plus < AccessoryDefinition.MaxPlus)
        {
            state.plus++;
            return AccessoryGain.Enhanced;
        }
        shards = GachaRules.ShardsFor(accessory.Tier);
        return AccessoryGain.Shards;
    }

    /// <summary>이 장신구를 낀 주민 해달 ID (안 꼈으면 null)</summary>
    public string Wearer(AccessoryDefinition accessory) =>
        accessory != null && _accessories.TryGetValue(accessory.AccessoryId, out var s) ? s.otterId : null;

    /// <summary>이 해달이 낀 장신구 ID (없으면 null)</summary>
    public string EquippedOn(string otterId)
    {
        if (string.IsNullOrEmpty(otterId))
            return null;
        foreach (var accessory in _accessories.Values)
        {
            if (accessory.otterId == otterId)
                return accessory.accessoryId;
        }
        return null;
    }

    /// <summary>주민 해달에게 끼움: 그 해달이 끼던 것은 빠지고, 다른 해달이 끼던 이 장신구는 옮겨 옴</summary>
    public bool Equip(AccessoryDefinition accessory, string otterId)
    {
        if (accessory == null || string.IsNullOrEmpty(otterId) || !_accessories.TryGetValue(accessory.AccessoryId, out var state))
            return false;
        foreach (var other in _accessories.Values)
        {
            if (other.otterId == otterId)
                other.otterId = null;
        }
        state.otterId = otterId;
        return true;
    }

    public void Unequip(AccessoryDefinition accessory)
    {
        if (accessory != null && _accessories.TryGetValue(accessory.AccessoryId, out var state))
            state.otterId = null;
    }

    /// <summary>이 해달이 낀 장신구의 이 효과 % (다른 효과 · 안 낌 · 데이터 없음 = 0)</summary>
    public int AccessoryPercent(string otterId, AccessoryEffect effect)
    {
        string id = EquippedOn(otterId);
        var accessory = id != null && _findAccessory != null ? _findAccessory(id) : null;
        return accessory != null && accessory.Effect == effect ? accessory.PercentAt(Plus(accessory)) : 0;
    }

    #endregion
}
