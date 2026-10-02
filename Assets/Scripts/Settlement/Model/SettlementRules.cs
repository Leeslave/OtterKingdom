using System;
using System.Collections.Generic;

/// <summary>게시판 부탁 하나의 상태</summary>
public enum RequestStatus
{
    Locked,     // 조건이 안 돼서 아직 안 보임
    Available,  // 시작할 수 있음 (비용은 따로 확인)
    Building,   // 건설 중
    Completed,
}

/// <summary>
/// 정착 진행 규칙 (데이터 + 상태 → 결과). 돈·가방·시간은 모름 — SettlementManager가 조율한다.
/// </summary>
public static class SettlementRules
{
    public static RequestStatus GetStatus(BoardRequestDefinition request, Settlement settlement)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        if (settlement.IsCompleted(request.RequestId))
            return RequestStatus.Completed;
        if (settlement.Job != null && settlement.Job.RequestId == request.RequestId)
            return RequestStatus.Building;
        if (!settlement.HasDevelopment(request.RequiredDevelopment) || settlement.ResidentCount < request.MinResidents)
            return RequestStatus.Locked;
        return RequestStatus.Available;
    }

    /// <summary>건설 해달이 와 있는지 (주민이든 역할로 와 있든)</summary>
    public static bool HasBuilder(SettlementConfig config, Settlement settlement)
    {
        var builder = config.FindBuilder();
        return builder != null
            && settlement.TryGetResidentState(builder.OtterId, out var state)
            && (state == ResidentState.SpecialNpc || state == ResidentState.Resident);
    }

    /// <summary>해달이 찾아옴: 처지를 정하고 첫 방문 기록을 남긴다 (이미 같은 처지면 그대로)</summary>
    public static void Arrive(SettlementOtterDefinition otter, ResidentState state, Settlement settlement)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));

        // 이미 주민이면 처지를 낮추지 않음 (예: 정착한 해달이 다시 "방문"으로 오는 데이터 실수)
        if (settlement.TryGetResidentState(otter.OtterId, out var current) && current == ResidentState.Resident)
            return;

        settlement.SetResident(otter.OtterId, state);
        if (otter.ArrivalEntry != null)
            settlement.AddGuestbook(otter.ArrivalEntry.EntryId);
    }

    /// <summary>새 게임: 첫 해달이 방문으로 온다 (한 번만)</summary>
    public static void InitializeNewGame(SettlementConfig config, Settlement settlement)
    {
        if (settlement.Initialized)
            return;
        if (config.FirstOtter != null)
            Arrive(config.FirstOtter, ResidentState.Visitor, settlement);
        settlement.MarkInitialized();
    }

    /// <summary>부탁 완료: 발전을 열고, 해달이 정착하거나 찾아오고, 단계가 오르고, 방명록에 남긴다</summary>
    /// <returns>새로 끝냈으면 true</returns>
    public static bool ApplyCompletion(BoardRequestDefinition request, Settlement settlement)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        string development = request.Construction != null ? request.Construction.UnlockResultId : null;
        if (!settlement.CompleteRequest(request.RequestId, development))
            return false;

        foreach (var otter in request.Settles)
        {
            if (otter != null)
                settlement.SetResident(otter.OtterId, ResidentState.Resident);
        }
        foreach (var arrival in request.Arrivals)
        {
            if (arrival != null && arrival.Otter != null)
                Arrive(arrival.Otter, arrival.State, settlement);
        }
        if (request.StageOnComplete >= 0 && request.StageOnComplete > settlement.Stage)
            settlement.SetStage(request.StageOnComplete);
        if (request.CompletionEntry != null)
            settlement.AddGuestbook(request.CompletionEntry.EntryId);
        return true;
    }

    /// <summary>정착 진행이 생기기 전 세이브: 모든 부탁을 끝낸 것으로 (밭 등 이미 쓰던 장소를 다시 잠그지 않게)</summary>
    public static void CompleteAll(SettlementConfig config, Settlement settlement)
    {
        InitializeNewGame(config, settlement);

        var requests = new List<BoardRequestDefinition>(config.Requests);
        requests.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var request in requests)
        {
            if (request != null)
                ApplyCompletion(request, settlement);
        }
        foreach (var development in config.LegacyDevelopments)
        {
            if (!string.IsNullOrEmpty(development))
                settlement.UnlockDevelopment(development);
        }
        if (settlement.Job != null)
            settlement.FinishJob();
        settlement.MarkBoardVisited();
        settlement.ClearLegacyFlag();
    }

    /// <summary>지금 게시판에서 다음으로 할 부탁 (건설 중인 것 우선, 없으면 열린 것 중 순서가 빠른 것). 없으면 null</summary>
    public static BoardRequestDefinition FindCurrent(SettlementConfig config, Settlement settlement)
    {
        BoardRequestDefinition best = null;
        foreach (var request in config.Requests)
        {
            if (request == null)
                continue;
            var status = GetStatus(request, settlement);
            if (status == RequestStatus.Building)
                return request;
            if (status == RequestStatus.Available && (best == null || request.Order < best.Order))
                best = request;
        }
        return best;
    }
}
