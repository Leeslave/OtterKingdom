using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>이사를 시작할 수 없는 이유</summary>
public enum RelocationStartResult
{
    Started,
    Instant,       // 이사 기능 전(행정 해달 전)이거나 구역 없는 물건: 바로 옮김
    NotPlaced,     // 광장에 없는 물건
    AlreadyMoving, // 이미 옮기는 중
    Blocked,       // 그 자리에 놓을 수 없음 (DecorPlacementResult)
}

/// <summary>
/// 이사 (구역·이사 4단계): 이사 지시서 · 건물 옮기기(시간) · 지시서 보상.
/// 건물은 격자에서 바로 새 자리로 가고, 끝나는 시각까지 "이사 중"(질서도 · 지시서에서 아직 제자리로 치지 않음).
/// 이사 기능은 행정 해달 승격 뒤부터, 지시서는 질서도가 기준(RelocationOrder)에 닿으면 나온다
/// </summary>
public partial class SettlementManager
{
    private const float RelocationTickSeconds = 0.5f;
    private float _relocationTimer;

    /// <summary>이사 지시서가 바뀌거나 이사가 시작 · 끝났을 때</summary>
    public event Action OnRelocationChanged;

    /// <summary>구역 건물 이사가 막 시작됨 (배치 ID, 옮기기 전 격자 칸) — 광장이 건설 해달에게 건물을 들려 보냄</summary>
    public event Action<int, RectInt> OnRelocationStarted;

    /// <summary>행정 해달이 생겨 구역 건물을 옮기면 시간이 걸리는지</summary>
    public bool RelocationEnabled => IsAdministrator;

    public RelocationOrder RelocationOrder => IsLoaded ? Settlement.RelocationOrder : null;

    public DecorBoardDefinition PlazaBoard
    {
        get
        {
            var decor = DecorManager.Instance;
            return decor != null ? decor.FindBoard(_config.PlazaBoardId) : null;
        }
    }

    /// <summary>광장에 놓인 다 지은 구역 건물 (세이브 좌표)</summary>
    public List<RelocationCandidate> RelocationCandidates()
    {
        var list = new List<RelocationCandidate>();
        var board = PlazaBoard;
        if (board == null)
            return list;
        foreach (var placed in DecorManager.Instance.GetLayout(board).Placed)
        {
            if (placed.Decor is IDistrictBuilding building && building.District != DistrictType.None && IsFinished(placed))
                list.Add(new RelocationCandidate(placed.InstanceId, building.District, ToSave(board, placed.Area)));
        }
        return list;
    }

    /// <summary>광장에 놓인 모든 물건이 차지한 칸 (세이브 좌표, except는 빼고)</summary>
    public List<RectInt> PlazaOccupied(int except = 0)
    {
        var list = new List<RectInt>();
        var board = PlazaBoard;
        if (board == null)
            return list;
        foreach (var placed in DecorManager.Instance.GetLayout(board).Placed)
        {
            if (placed.InstanceId != except)
                list.Add(ToSave(board, placed.Area));
        }
        return list;
    }

    /// <summary>이 건물이 들어갈 수 있는 맞는 구역의 빈 부지 칸</summary>
    public List<RelocationSlot> RelocationSlotsFor(int instanceId)
    {
        var board = PlazaBoard;
        if (board == null || !DecorManager.Instance.GetLayout(board).TryGet(instanceId, out var placed) || !(placed.Decor is IDistrictBuilding building))
            return new List<RelocationSlot>();
        return RelocationRules.FreeSlots(_config, Settlement, building.District, placed.Area.size, PlazaOccupied(instanceId));
    }

    public int RelocationDoneCount() =>
        RelocationOrder == null ? 0 : RelocationRules.DoneCount(_config, Settlement, RelocationOrder, RelocationCandidates());

    /// <summary>지시서에서 아직 옮기지 않은 건물 (이사 중도 아니고 맞는 구역도 아님), 없으면 0</summary>
    public int NextRelocationTarget()
    {
        var order = RelocationOrder;
        if (order == null)
            return 0;
        var candidates = RelocationCandidates();
        foreach (var id in order.InstanceIds)
        {
            var c = candidates.Find(x => x.InstanceId == id);
            if (c.InstanceId == id && !Settlement.IsRelocating(id) && !RelocationRules.Fits(_config, Settlement, c))
                return id;
        }
        return 0;
    }

    /// <summary>지시서로 고른 부지 칸에 옮기기 시작 (가로 가운데 · 아래 맞춤)</summary>
    public RelocationStartResult TryRelocateToSlot(int instanceId, DistrictSlot slot, out DecorPlacementResult placement)
    {
        placement = DecorPlacementResult.Ok;
        var board = PlazaBoard;
        if (slot == null || board == null || !DecorManager.Instance.GetLayout(board).TryGet(instanceId, out var placed))
            return RelocationStartResult.NotPlaced;
        var origin = DistrictRules.OriginIn(slot, placed.Area.size) + board.SaveOrigin;
        return TryRelocate(instanceId, origin, placed.Rotation, slot.SlotId, out placement);
    }

    /// <summary>
    /// 다 지은 건물을 옮긴다 (꾸미기 모드 · 지시서 공통). 격자에서는 바로 새 자리로 가고, 구역 건물이면 이사 시간이 걸린다.
    /// origin = 격자 칸 (세이브 좌표 아님)
    /// </summary>
    public RelocationStartResult TryRelocate(int instanceId, Vector2Int origin, DecorRotation rotation, string slotId, out DecorPlacementResult placement)
    {
        placement = DecorPlacementResult.Ok;
        var board = PlazaBoard;
        var layout = board != null ? DecorManager.Instance.GetLayout(board) : null;
        if (layout == null || !layout.TryGet(instanceId, out var before))
            return RelocationStartResult.NotPlaced;
        if (Settlement.IsRelocating(instanceId))
            return RelocationStartResult.AlreadyMoving;

        bool changed = before.Origin != origin || before.Rotation != rotation;
        var fromArea = before.Area; // PlacedDecor는 옮기면 바뀌므로 먼저 적어 둠
        placement = layout.TryMove(instanceId, origin, rotation);
        if (placement != DecorPlacementResult.Ok)
            return RelocationStartResult.Blocked;
        if (!changed || !RelocationEnabled || !(before.Decor is IDistrictBuilding building) || building.District == DistrictType.None)
            return RelocationStartResult.Instant;

        long now = NowTicks;
        Settlement.StartRelocation(instanceId, slotId, now, now + TimeSpan.FromSeconds(DevTimers.Duration(_config.RelocationSeconds)).Ticks);
        OnRelocationStarted?.Invoke(instanceId, fromArea); // 광장이 옛 자리를 먼저 알아야 건설 해달이 들러 감
        OnRelocationChanged?.Invoke();
        SaveRequested?.Invoke();
        return RelocationStartResult.Started;
    }

    // Update에서: 끝난 이사 마무리 → 지시서가 끝났으면 보상 → 다음 지시서
    private void TickRelocation()
    {
        _relocationTimer -= Time.unscaledDeltaTime;
        if (_relocationTimer > 0f)
            return;
        _relocationTimer = RelocationTickSeconds;
        TickCastle();
        TickDistrictMilestones();

        var finished = Settlement.FinishRelocations(NowTicks);
        if (finished.Count > 0)
        {
            var board = PlazaBoard;
            if (board != null && DecorManager.Instance.GetLayout(board).TryGet(finished[0], out var arrived))
                GameNotices.Post(new GameNotice($"{arrived.Decor.DisplayName} 이사 완료!"));
            OnRelocationChanged?.Invoke();
            SaveRequested?.Invoke();
        }

        if (!RelocationEnabled || PlazaBoard == null)
            return;
        var order = Settlement.RelocationOrder;
        if (order != null && RelocationRules.IsDone(_config, Settlement, order, RelocationCandidates()))
        {
            int gold = RelocationRules.RewardGold(_config, order);
            if (gold > 0 && CurrencyManager.Instance != null && _config.GoldCurrency != null)
                CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_config.GoldCurrency, gold, TransactionSource.RequestReward));
            if (_config.RelocationExp > 0 && ProfileManager.Instance != null)
                ProfileManager.Instance.AddExp(_config.RelocationExp);
            Settlement.CompleteRelocationOrder();
            GameNotices.Post(new GameNotice($"이사 지시서 #{order.Serial} 완료! 골드 {gold} · 경험치 {_config.RelocationExp}"));
            OnRelocationChanged?.Invoke();
            SaveRequested?.Invoke();
        }
        if (Settlement.RelocationOrder == null && DistrictRules.CanRelocate(_config, DistrictOrder))
        {
            var next = RelocationRules.PickOrder(_config, Settlement, RelocationCandidates(), PlazaOccupied(), Settlement.RelocationOrdersDone + 1);
            if (next != null)
            {
                Settlement.SetRelocationOrder(next);
                GameNotices.Post(new GameNotice($"또박이: 이사 지시서 #{next.Serial}이 나왔어요! 마을회관에서 확인해요"));
                OnRelocationChanged?.Invoke();
                SaveRequested?.Invoke();
            }
        }
    }

    // 공사 중인 건물 · 건설 자리는 옮기지 않음
    private bool IsFinished(PlacedDecor placed)
    {
        if (placed.Decor is ConstructionPlotDefinition plot)
            return !TryGetPlotRemaining(plot, out _);
        var record = BuildingRecordOf(placed.InstanceId);
        return record == null || record.Built;
    }

    private static RectInt ToSave(DecorBoardDefinition board, RectInt area) => new RectInt(area.position - board.SaveOrigin, area.size);
}
