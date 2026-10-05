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
    /// <summary>
    /// 정착 진행 전 세이브(모든 부탁을 끝낸 것으로 옮김)가 완료로 받는 부탁의 콘텐츠 버전 상한.
    /// 그 뒤에 들어온 부탁(P2 = 2)은 옛 세이브에서도 처음부터 진행한다
    /// </summary>
    public const int LegacyContentVersion = 1;

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
        // 주민 작업 부탁: 그 작업을 하는 중이면 진행 중
        if (request.CompletionTask != null && settlement.TryGetTaskJob(request.CompletionTask.TaskId, out _))
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

    /// <summary>부탁을 끝내면 열리는 발전: 건설의 결과, 맡긴 역할의 결과, 끝낸 주민 작업의 결과 (전문 해달 배치는 없음)</summary>
    public static string CompletionDevelopment(BoardRequestDefinition request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (request.Construction != null)
            return request.Construction.UnlockResultId;
        if (request.AssignRole != null)
            return request.AssignRole.ResultDevelopment;
        if (request.CompletionTask != null)
            return request.CompletionTask.ResultDevelopment;
        return null;
    }

    /// <summary>부탁 완료: 발전을 열고, 해달이 정착하거나 찾아오고, 단계가 오르고, 방명록에 남긴다</summary>
    /// <returns>새로 끝냈으면 true</returns>
    public static bool ApplyCompletion(BoardRequestDefinition request, Settlement settlement)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (!settlement.CompleteRequest(request.RequestId, CompletionDevelopment(request)))
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

    /// <summary>
    /// 정착 진행이 생기기 전 세이브: 그때 있던 부탁(콘텐츠 버전 LegacyContentVersion 이하)을 끝낸 것으로 (밭 등 이미 쓰던 장소를 다시 잠그지 않게).
    /// 나중에 들어온 부탁(게시판 성장·마을회관 등)은 그대로 두어 처음부터 진행한다
    /// </summary>
    public static void CompleteAll(SettlementConfig config, Settlement settlement)
    {
        InitializeNewGame(config, settlement);

        var requests = new List<BoardRequestDefinition>();
        foreach (var request in config.Requests)
        {
            if (request != null && request.ContentVersion <= LegacyContentVersion)
                requests.Add(request);
        }
        requests.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var request in requests)
            ApplyCompletion(request, settlement);
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

    /// <summary>왕국 레벨에 닿은 발전을 연다 (불러올 때·레벨이 오를 때). 새로 연 항목을 opened에 넣는다</summary>
    public static void UnlockLevelDevelopments(SettlementConfig config, Settlement settlement, int level, List<LevelDevelopment> opened)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (opened == null)
            throw new ArgumentNullException(nameof(opened));
        opened.Clear();
        foreach (var entry in config.LevelDevelopments)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Development) || level < entry.Level
                || settlement.HasDevelopment(entry.Development))
                continue;
            settlement.UnlockDevelopment(entry.Development);
            opened.Add(entry);
        }
    }

    /// <summary>
    /// 아직 레벨이 모자라 잠긴 메인 부탁 중 순서가 가장 앞선 것 (조건 발전이 레벨로 열리는 부탁). 없으면 null.
    /// 마을회관이 "왕국 Lv.○에 열려요"로 다음 목표를 알려 줄 때 쓴다
    /// </summary>
    public static BoardRequestDefinition FindLevelLocked(SettlementConfig config, Settlement settlement, out int level)
    {
        level = 0;
        BoardRequestDefinition best = null;
        foreach (var request in config.Requests)
        {
            if (request == null || request.Category != RequestCategory.Main || settlement.IsCompleted(request.RequestId)
                || settlement.HasDevelopment(request.RequiredDevelopment))
                continue;
            var gate = config.FindLevelDevelopment(request.RequiredDevelopment);
            if (gate != null && (best == null || request.Order < best.Order))
            {
                best = request;
                level = gate.Level;
            }
        }
        return best;
    }

    /// <summary>
    /// 경험치로 오를 수 있는 왕국 레벨: 아직 안 끝낸 큰 발전(KingdomLevel이 있는 부탁) 중 가장 낮은 것의 바로 아래.
    /// 다 끝냈으면 제한 없음(int.MaxValue)
    /// </summary>
    public static int LevelCap(SettlementConfig config, Settlement settlement)
    {
        int cap = int.MaxValue;
        foreach (var request in config.Requests)
        {
            if (request == null || request.KingdomLevel <= 0 || settlement.IsCompleted(request.RequestId))
                continue;
            cap = Math.Min(cap, Math.Max(1, request.KingdomLevel - 1));
        }
        return cap;
    }

    /// <summary>이 장소를 직접 치우는 부탁 (없으면 null)</summary>
    public static BoardRequestDefinition FindClearing(SettlementConfig config, ZoneDefinition zone)
    {
        if (zone == null)
            return null;
        foreach (var request in config.Requests)
        {
            if (request != null && request.ClearZone == zone)
                return request;
        }
        return null;
    }

    /// <summary>
    /// 지금 게시판에서 다음으로 할 메인 부탁 (진행 중인 것 우선, 없으면 열린 것 중 순서가 빠른 것). 없으면 null.
    /// 주민 부탁은 선택이라 안내 띠·다음 목표에 나오지 않는다
    /// </summary>
    public static BoardRequestDefinition FindCurrent(SettlementConfig config, Settlement settlement)
    {
        BoardRequestDefinition best = null;
        foreach (var request in config.Requests)
        {
            if (request == null || request.Category != RequestCategory.Main)
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
