using System;
using System.Collections.Generic;

/// <summary>
/// 장난감 방문 (P4): 광장에 놓인 장난감을 보고 장난감 해달이 찾아오고, 빈 집이 있으면 광장에서 말을 걸어 입주한다.
/// 판정은 ToyVisitRules, 원본 기록은 Settlement (방문 시계·회차, 주민 목록, 집의 입주민)
/// </summary>
public partial class SettlementManager
{
    // 방문 판정 간격 (초). 시계는 UTC 시각이라 판정이 늦어도 방문 시각은 같음
    private const float ToyVisitCheckSeconds = 1f;

    private float _toyVisitCheckTimer;
    private readonly HashSet<ItemDefinition> _plazaToys = new HashSet<ItemDefinition>();

    /// <summary>장난감 해달이 찾아왔을 때 (광장은 주민 목록 변경으로 대기 줄에 넣어 걸어 들어오게 함)</summary>
    public event Action<SettlementOtterDefinition> OnToyVisitorArrived;

    /// <summary>이 특성을 가진 주민 수</summary>
    public int TraitCount(OtterTrait trait) => TraitRules.Count(_config, Settlement, trait);

    /// <summary>찾아와 만났지만 빈 집이 없어 광장에 머무는 장난감 해달인지 (말을 걸면 "빈 집이 생기면…")</summary>
    public bool IsHomelessVisitor(SettlementOtterDefinition otter) =>
        (ToyVisitRules.IsHomelessVisitor(Settlement, otter) || IsWaitingSettler(otter)) && ToyVisitRules.FindFreeHome(_config, Settlement) == null;

    // 입주 부탁(P4)이 기다리는 해달: 광장에서 만났고 아직 주민이 아님 (장난감 해달이 아니어도 — 꾸벅이)
    private bool IsWaitingSettler(SettlementOtterDefinition otter) =>
        otter != null && Settlement.HasMet(otter.OtterId) && SettlementRules.IsAwaitedSettler(_config, Settlement, otter);

    /// <summary>입주 부탁이 기다리는 해달이 빈 집에 들어갈 수 있는지</summary>
    public bool CanMoveInAsSettler(SettlementOtterDefinition otter) =>
        IsWaitingSettler(otter) && ToyVisitRules.FindFreeHome(_config, Settlement) != null;

    /// <summary>
    /// 광장 꾸미기 격자에 놓인 장난감 중 가장 높은 등급 (ItemRarity.Tier). 장난감이 없거나 꾸미기를 아직 모르면 -1
    /// </summary>
    public int PlazaToyTier
    {
        get
        {
            var decor = DecorManager.Instance;
            var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
            if (board == null)
                return -1;

            int tier = -1;
            foreach (var placed in decor.GetLayout(board).Placed)
            {
                if (placed.Decor == null || placed.Decor.IsBuilding || placed.Decor.IsFurniture)
                    continue;
                var rarity = placed.Decor.Item != null ? placed.Decor.Item.Rarity : null;
                tier = Math.Max(tier, rarity != null ? rarity.Tier : 0);
            }
            return tier;
        }
    }

    /// <summary>
    /// 광장 상태: 밤인지(게임 시계), 놓인 가구 · 다 지은 건물의 자리 수(의자 · 가로등 · 식탁), 아늑함 합계.
    /// 희귀 해달의 방문 조건과 아늑함만큼 줄어드는 방문 간격에 쓴다
    /// </summary>
    public ToyVisitContext PlazaVisitContext
    {
        get
        {
            bool night = TimeOfDay.Night(TimeOfDay.CurrentHour) >= 0.35f;
            int seats = 0, lamps = 0, tables = 0, coziness = 0;
            var decor = DecorManager.Instance;
            var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
            if (board != null)
            {
                foreach (var placed in decor.GetLayout(board).Placed)
                {
                    var d = placed.Decor;
                    if (d == null)
                        continue;
                    coziness += d.Coziness;
                    // 부탁으로 짓는 건물(의자 · 가로등 · 식탁)은 다 지어야 셈
                    if (!d.HasSpot || (d is ConstructionPlotDefinition plot && !Settlement.HasDevelopment(plot.Construction.UnlockResultId)))
                        continue;
                    if (d.SpotKind == PlazaSpotKind.Sit) seats++;
                    else if (d.SpotKind == PlazaSpotKind.Gather) lamps++;
                    else if (d.SpotKind == PlazaSpotKind.Eat) tables++;
                }
            }
            return new ToyVisitContext(night, seats, lamps, tables, coziness, KingdomBonus.Percent(KingdomBonusKind.VisitSpeed));
        }
    }

    /// <summary>광장 아늑함 합계 (꾸미기 모드 안내)</summary>
    public int PlazaCoziness => PlazaVisitContext.Coziness;

    /// <summary>광장 꾸미기 격자에 놓인 장난감 (건물 제외). 한정 해달은 좋아하는 장난감이 여기 있어야 찾아온다</summary>
    private HashSet<ItemDefinition> CollectPlazaToys()
    {
        _plazaToys.Clear();
        var decor = DecorManager.Instance;
        var board = decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        if (board == null)
            return _plazaToys;
        foreach (var placed in decor.GetLayout(board).Placed)
        {
            if (placed.Decor != null && !placed.Decor.IsBuilding && !placed.Decor.IsFurniture && placed.Decor.Item != null)
                _plazaToys.Add(placed.Decor.Item);
        }
        return _plazaToys;
    }

    // Update에서 1초마다
    private void TickToyVisits()
    {
        _toyVisitCheckTimer -= UnityEngine.Time.unscaledDeltaTime;
        if (_toyVisitCheckTimer > 0f)
            return;
        _toyVisitCheckTimer = ToyVisitCheckSeconds;
        UpdateToyVisits();
    }

    /// <returns>이번에 찾아온 해달 (없으면 null)</returns>
    private SettlementOtterDefinition UpdateToyVisits()
    {
        var step = ToyVisitRules.Step(_config, Settlement, PlazaToyTier, CollectPlazaToys(), PlazaVisitContext, NowTicks, out var arrived);
        if (step != ToyVisitStep.Arrived)
            return null;

        OnToyVisitorArrived?.Invoke(arrived);
        SaveRequested?.Invoke();
        return arrived;
    }

    // 장난감 해달의 입주: 빈 집에 (공동사업의 새 이웃 입주와 같은 확인 대화·알림)
    private bool TryMoveInHome(SettlementOtterDefinition otter)
    {
        bool movedIn = otter.IsToyVisitor && ToyVisitRules.CanMoveIn(_config, Settlement, otter)
            ? ToyVisitRules.MoveIn(_config, Settlement, otter)
            : CanMoveInAsSettler(otter) && ToyVisitRules.MoveIntoFreeHome(_config, Settlement, otter);
        if (!movedIn)
            return false;
        if (otter.CollectionEntry != null && CollectionManager.Instance != null)
            CollectionManager.Instance.Register(otter.CollectionEntry.EntryId);
        OnResidentMovedIn?.Invoke(otter);
        SaveRequested?.Invoke();
        return true;
    }

    // 돌아옴 팝업: 비운 동안 방문 시각이 지났으면 지금 찾아옴 (Update보다 먼저 불릴 수 있음). 한 번에 한 마리
    private void CollectVisitNews(List<string> lines)
    {
        var arrived = UpdateToyVisits();
        if (arrived == null)
            return;
        string name = arrived.DisplayName;
        lines.Add($"{name}{KoreanParticle.SubjectParticle(name)} 장난감을 보고 광장에 놀러 왔어요");
    }
}
