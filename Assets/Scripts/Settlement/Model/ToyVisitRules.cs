using System;
using System.Collections.Generic;

/// <summary>장난감 방문 판정 결과</summary>
public enum ToyVisitStep
{
    NoToy,       // 광장에 장난감이 없음 (돌던 시계는 그대로 멈춰 둠)
    Full,        // 빈 집을 기다리는 장난감 해달이 가득함 (시계 멈춤)
    NoCandidate, // 이 장난감 등급으로 올 해달이 더 없음 (시계 멈춤)
    Counting,    // 시계가 도는 중 (이번에 시작했거나 아직 시간이 안 됨)
    Arrived,     // 한 마리가 찾아옴
}

/// <summary>희귀 해달이 찾아오는 조건 (조건이 맞을 때만 후보가 됨)</summary>
public enum VisitCondition
{
    Any = 0,
    Night = 1,   // 밤에만
    Day = 2,     // 낮에만
    Seat = 3,    // 광장에 의자 · 벤치가 있을 때
    Lamp = 4,    // 광장에 가로등이 있을 때
    Table = 5,   // 광장에 식탁 · 피크닉 매트가 있을 때
    Cozy = 6,    // 광장 아늑함이 값 이상일 때
}

/// <summary>방문 조건 · 간격을 정할 때 보는 광장 상태 (밤인지, 놓인 가구 자리 수, 아늑함)</summary>
public readonly struct ToyVisitContext
{
    public readonly bool Night;
    public readonly int Seats;
    public readonly int Lamps;
    public readonly int Tables;
    public readonly int Coziness;
    /// <summary>방문 간격이 더 줄어드는 % (기록관)</summary>
    public readonly int VisitBonusPercent;

    public ToyVisitContext(bool night, int seats, int lamps, int tables, int coziness, int visitBonusPercent = 0)
    {
        Night = night;
        Seats = seats;
        Lamps = lamps;
        Tables = tables;
        Coziness = coziness;
        VisitBonusPercent = visitBonusPercent;
    }
}

/// <summary>
/// 장난감 방문 (P4, 성장곡선 기획서 3장 "장난감을 놓으면 그 등급의 해달이 방문"):
/// 광장에 놓인 장난감 중 가장 높은 등급 이하의 장난감 해달이, 정해진 간격마다 한 마리씩 찾아온다 (Visitor).
/// 고르기는 회차로 정하는 난수라 재접속해도 같은 해달이 온다.
/// 빈 집(공동사업이 특정 해달에게 정해 둔 자리가 아닌, 입주민 없는 집)이 있으면 광장에서 말을 걸어 입주 → 주민.
/// </summary>
public static class ToyVisitRules
{
    /// <summary>장난감 등급과 같은 등급인 해달의 가중치</summary>
    public const int SameTierWeight = 2;

    /// <summary>왕국에 아직 없는 특성을 가진 해달의 가중치 (특성이 막혀 진행이 멈추지 않게)</summary>
    public const int MissingTraitWeight = 3;

    /// <summary>좋아하는 장난감이 광장에 놓인 한정 해달의 가중치 (뽑은 장난감을 놓으면 그 해달이 먼저 오게)</summary>
    public const int FavoriteToyWeight = 20;

    /// <summary>아늑함이 이만큼이면 방문 간격이 가장 많이 줄어듦 (MaxCozySpeedup)</summary>
    public const int CozyCap = 60;

    /// <summary>아늑함으로 줄어드는 방문 간격의 최대 비율</summary>
    public const float MaxCozySpeedup = 0.5f;

    /// <summary>아늑함에 따라 방문 간격이 줄어드는 비율 (0 ~ 0.5)</summary>
    public static float CozySpeedup(int coziness) => Math.Max(0, Math.Min(coziness, CozyCap)) / (float)CozyCap * MaxCozySpeedup;

    /// <summary>방문 간격에 곱하는 값: 아늑함 × 기록관 (context가 없으면 1)</summary>
    public static float IntervalScale(ToyVisitContext? context)
    {
        if (context == null)
            return 1f;
        float bonus = Math.Max(0, Math.Min(90, context.Value.VisitBonusPercent)) / 100f;
        return (1f - CozySpeedup(context.Value.Coziness)) * (1f - bonus);
    }

    /// <summary>이 해달의 방문 조건이 맞는지 (context가 없으면 조건을 보지 않음)</summary>
    public static bool MeetsCondition(SettlementOtterDefinition otter, ToyVisitContext? context)
    {
        if (otter == null || context == null)
            return true;
        var c = context.Value;
        switch (otter.VisitCondition)
        {
            case VisitCondition.Night: return c.Night;
            case VisitCondition.Day: return !c.Night;
            case VisitCondition.Seat: return c.Seats > 0;
            case VisitCondition.Lamp: return c.Lamps > 0;
            case VisitCondition.Table: return c.Tables > 0;
            case VisitCondition.Cozy: return c.Coziness >= otter.VisitConditionValue;
            default: return true;
        }
    }

    /// <summary>방문 조건을 사람이 읽을 문장으로 (도감 힌트. 조건이 없으면 빈 문자열)</summary>
    public static string ConditionText(SettlementOtterDefinition otter)
    {
        if (otter == null)
            return string.Empty;
        switch (otter.VisitCondition)
        {
            case VisitCondition.Night: return "밤에만 찾아와요";
            case VisitCondition.Day: return "낮에만 찾아와요";
            case VisitCondition.Seat: return "광장에 의자나 벤치가 있어야 찾아와요";
            case VisitCondition.Lamp: return "광장에 가로등이 있어야 찾아와요";
            case VisitCondition.Table: return "광장에 식탁이나 피크닉 매트가 있어야 찾아와요";
            case VisitCondition.Cozy: return $"광장 아늑함이 {otter.VisitConditionValue} 이상이어야 찾아와요";
            default: return string.Empty;
        }
    }

    /// <summary>n번째(0부터) 방문까지 걸리는 시간</summary>
    public static TimeSpan Interval(SettlementConfig config, int visitIndex)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        var minutes = config.ToyVisitMinutes;
        if (minutes == null || minutes.Count == 0)
            return TimeSpan.Zero;
        float value = minutes[Math.Max(0, Math.Min(visitIndex, minutes.Count - 1))];
        return TimeSpan.FromMinutes(Math.Max(0f, value));
    }

    /// <summary>빈 집이 없어 광장에 머무는 장난감 해달 수 (찾아왔지만 아직 입주 전)</summary>
    public static int WaitingCount(SettlementConfig config, Settlement settlement)
    {
        int count = 0;
        foreach (var otterId in settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            if (otter != null && otter.IsToyVisitor && settlement.TryGetResidentState(otterId, out var state)
                && state == ResidentState.Visitor)
                count++;
        }
        return count;
    }

    /// <summary>이 장난감 등급으로 올 수 있는 해달: 아직 찾아온 적 없는 장난감 해달 중 등급이 장난감 이하 (데이터 순서). 한정 해달은 오지 않음</summary>
    public static void CollectCandidates(SettlementConfig config, Settlement settlement, int toyTier, List<SettlementOtterDefinition> result) =>
        CollectCandidates(config, settlement, toyTier, null, result);

    /// <summary>
    /// 올 수 있는 해달: 아직 찾아온 적 없는 장난감 해달 중 등급이 장난감 이하 (데이터 순서).
    /// 좋아하는 장난감이 있는 한정 해달은 등급 대신 그 장난감이 광장에 놓였는지를 본다
    /// </summary>
    /// <param name="placedToys">광장에 놓인 장난감 (null이면 한정 해달은 오지 않음)</param>
    public static void CollectCandidates(SettlementConfig config, Settlement settlement, int toyTier,
        ICollection<ItemDefinition> placedToys, List<SettlementOtterDefinition> result) =>
        CollectCandidates(config, settlement, toyTier, placedToys, null, result);

    /// <param name="context">광장 상태 (희귀 해달의 방문 조건. null이면 조건을 보지 않음)</param>
    public static void CollectCandidates(SettlementConfig config, Settlement settlement, int toyTier,
        ICollection<ItemDefinition> placedToys, ToyVisitContext? context, List<SettlementOtterDefinition> result)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        result.Clear();
        if (toyTier < 0)
            return;
        foreach (var otter in config.Otters)
        {
            if (otter == null || !otter.IsToyVisitor || string.IsNullOrEmpty(otter.OtterId)
                || settlement.TryGetResidentState(otter.OtterId, out _))
                continue;
            bool comes = otter.FavoriteToy != null
                ? placedToys != null && placedToys.Contains(otter.FavoriteToy)
                : otter.VisitTier <= toyTier;
            if (comes && MeetsCondition(otter, context))
                result.Add(otter);
        }
    }

    /// <summary>후보의 가중치: 기본 1, 장난감과 같은 등급 ×2, 왕국에 아직 없는 특성 ×3, 좋아하는 장난감이 놓인 한정 해달 ×20</summary>
    public static int Weight(SettlementOtterDefinition otter, int toyTier, int[] traitCounts)
    {
        if (otter == null)
            return 0;
        int weight = 1;
        if (otter.VisitTier == toyTier)
            weight *= SameTierWeight;
        int index = (int)otter.Trait;
        if (index > 0 && traitCounts != null && index < traitCounts.Length && traitCounts[index] == 0)
            weight *= MissingTraitWeight;
        if (otter.FavoriteToy != null)
            weight *= FavoriteToyWeight;
        return weight;
    }

    /// <summary>후보 중 한 마리를 고름 (회차로 정하는 난수 → 같은 회차·같은 후보면 늘 같은 해달)</summary>
    public static SettlementOtterDefinition Pick(IReadOnlyList<SettlementOtterDefinition> candidates, int toyTier, int[] traitCounts, int serial)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int total = 0;
        foreach (var otter in candidates)
            total += Weight(otter, toyTier, traitCounts);
        if (total <= 0)
            return candidates[0];

        int roll = new Random(Seed(serial)).Next(total);
        foreach (var otter in candidates)
        {
            roll -= Weight(otter, toyTier, traitCounts);
            if (roll < 0)
                return otter;
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// 한 번 판정: 막혀 있으면 시계를 멈추고(장난감이 없을 때만 그대로 둠), 시계가 없으면 시작하고, 시간이 되면 한 마리가 찾아온다.
    /// 찾아온 해달은 Visitor로 주민 목록 끝에 붙고 방명록이 남는다 (광장이 대기 줄로 걸어 들어오게 함)
    /// </summary>
    /// <param name="toyTier">광장 장난감 중 가장 높은 등급 (없으면 -1)</param>
    public static ToyVisitStep Step(SettlementConfig config, Settlement settlement, int toyTier, long nowUtcTicks,
        out SettlementOtterDefinition arrived) =>
        Step(config, settlement, toyTier, null, nowUtcTicks, out arrived);

    /// <param name="toyTier">광장 장난감 중 가장 높은 등급 (없으면 -1)</param>
    /// <param name="placedToys">광장에 놓인 장난감 (한정 해달은 좋아하는 장난감이 여기 있어야 옴, null = 안 옴)</param>
    public static ToyVisitStep Step(SettlementConfig config, Settlement settlement, int toyTier, ICollection<ItemDefinition> placedToys,
        long nowUtcTicks, out SettlementOtterDefinition arrived) =>
        Step(config, settlement, toyTier, placedToys, null, nowUtcTicks, out arrived);

    /// <param name="context">광장 상태: 희귀 해달의 방문 조건, 아늑함만큼 방문 간격이 줄어듦 (null이면 조건 · 아늑함을 보지 않음)</param>
    public static ToyVisitStep Step(SettlementConfig config, Settlement settlement, int toyTier, ICollection<ItemDefinition> placedToys,
        ToyVisitContext? context, long nowUtcTicks, out SettlementOtterDefinition arrived)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        arrived = null;
        // 장난감이 없음: 시계를 지우지 않음 (꾸미기 중이거나 꾸미기 기록을 불러오기 전일 수 있음)
        if (toyTier < 0)
            return ToyVisitStep.NoToy;
        if (WaitingCount(config, settlement) >= config.MaxWaitingVisitors)
        {
            settlement.SetToyVisitDue(0);
            return ToyVisitStep.Full;
        }

        var candidates = new List<SettlementOtterDefinition>();
        CollectCandidates(config, settlement, toyTier, placedToys, context, candidates);
        if (candidates.Count == 0)
        {
            settlement.SetToyVisitDue(0);
            return ToyVisitStep.NoCandidate;
        }

        if (settlement.ToyVisitDueUtcTicks <= 0)
        {
            settlement.SetToyVisitDue(nowUtcTicks + (long)(Interval(config, settlement.ToyVisitSerial).Ticks * IntervalScale(context)));
            return ToyVisitStep.Counting;
        }
        if (nowUtcTicks < settlement.ToyVisitDueUtcTicks)
            return ToyVisitStep.Counting;

        arrived = Pick(candidates, toyTier, TraitRules.CountAll(config, settlement), settlement.ToyVisitSerial);
        settlement.CompleteToyVisit();
        settlement.SetResident(arrived.OtterId, ResidentState.Visitor);
        if (arrived.ArrivalEntry != null)
            settlement.AddGuestbook(arrived.ArrivalEntry.EntryId);
        return ToyVisitStep.Arrived;
    }

    /// <summary>
    /// 장난감 해달이 들어갈 빈 집: 입주민이 없고, 공동사업이 특정 해달에게 정해 둔 자리(새 이웃의 집 등)가 아닌 집 (지은 순서대로 첫 집)
    /// </summary>
    public static HouseRecord FindFreeHome(SettlementConfig config, Settlement settlement)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        foreach (var house in settlement.Houses)
        {
            if (house != null && string.IsNullOrEmpty(house.ResidentId) && !IsReservedSlot(config, house.SlotId))
                return house;
        }
        return null;
    }

    /// <summary>공동사업의 입주 단계가 정해 둔 집 자리인지</summary>
    public static bool IsReservedSlot(SettlementConfig config, string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return false;
        foreach (var project in config.Projects)
        {
            if (project == null)
                continue;
            foreach (var stage in project.Stages)
            {
                if (stage != null && stage.Action == ProjectActionKind.SettleResident && stage.HouseSlotId == slotId)
                    return true;
            }
        }
        return false;
    }

    /// <summary>광장에서 말을 걸면 입주를 물을 수 있는 장난감 해달인지 (찾아와 만났고 아직 방문 중 + 빈 집이 있음)</summary>
    public static bool CanMoveIn(SettlementConfig config, Settlement settlement, SettlementOtterDefinition otter) =>
        IsHomelessVisitor(settlement, otter) && FindFreeHome(config, settlement) != null;

    /// <summary>찾아와 만났지만 아직 집이 없는 장난감 해달인지</summary>
    public static bool IsHomelessVisitor(Settlement settlement, SettlementOtterDefinition otter) =>
        otter != null && otter.IsToyVisitor && settlement.HasMet(otter.OtterId)
        && settlement.TryGetResidentState(otter.OtterId, out var state) && state == ResidentState.Visitor;

    /// <summary>빈 집에 입주 (집의 입주민 + 주민 처지). 연타해도 한 번</summary>
    /// <returns>이번에 입주했으면 true</returns>
    public static bool MoveIn(SettlementConfig config, Settlement settlement, SettlementOtterDefinition otter) =>
        CanMoveIn(config, settlement, otter) && MoveIntoFreeHome(config, settlement, otter);

    /// <summary>
    /// 해달이 빈 집에 들어가 주민이 된다 (장난감 해달인지는 보지 않음 — 입주 부탁이 기다리는 꾸벅이처럼 부른 쪽이 확인).
    /// 이미 주민이거나 빈 집이 없으면 false
    /// </summary>
    public static bool MoveIntoFreeHome(SettlementConfig config, Settlement settlement, SettlementOtterDefinition otter)
    {
        if (otter == null || (settlement.TryGetResidentState(otter.OtterId, out var state) && state == ResidentState.Resident))
            return false;
        var home = FindFreeHome(config, settlement);
        if (home == null || !settlement.SetHouseResident(home.InstanceId, otter.OtterId))
            return false;
        settlement.SetResident(otter.OtterId, ResidentState.Resident);
        return true;
    }

    // 회차마다 다른 씨앗 (0회차도 0이 아니게)
    private static int Seed(int serial) => unchecked(serial * 486187739 + 1013904223);
}
