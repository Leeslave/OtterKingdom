using System;
using System.Collections.Generic;

/// <summary>게시판의 등급 (발전 기록에서 계산. 따로 저장하지 않음)</summary>
public enum BoardTier
{
    Basic,    // 기존 게시판: 메인 부탁 중심
    Upgraded, // 보강했지만 맡은 해달이 아직 없음
    Managed,  // 관리 해달이 맡음: 메인 + 주민 부탁(정해진 수) + 완료 기록
    Guild,    // 접수소가 생김: 위 구성 + 큰 부탁 묶음
}

/// <summary>게시판 "해달의 부탁" 안의 구역</summary>
public enum BoardSection
{
    Main,
    Resident,
    Completed,
}

/// <summary>큰 부탁의 한 단계 상태</summary>
public enum MilestoneStepState
{
    Locked,     // 아직 열리지 않음
    Next,       // 지금 할 수 있음
    InProgress, // 진행 중
    Done,
}

/// <summary>게시판에 보일 부탁 한 줄</summary>
public readonly struct BoardRequestRow
{
    public readonly BoardSection Section;
    public readonly BoardRequestDefinition Request;
    public readonly RequestStatus Status;

    public BoardRequestRow(BoardSection section, BoardRequestDefinition request, RequestStatus status)
    {
        Section = section;
        Request = request;
        Status = status;
    }
}

/// <summary>
/// 게시판 성장 규칙 (데이터 + 상태 → 결과): 게시판 등급, 부탁 탭에 보일 부탁과 구역, 주민 부탁 칸 수, 큰 부탁 단계.
/// 칸 수 = 한 번에 보이는 선택형 주민 부탁의 수. 메인 부탁·진행 중인 부탁은 칸 수와 상관없이 늘 보인다.
/// 정렬은 순서(order) → 부탁 ID로 고정 (새로고침·재접속해도 같은 순서).
/// </summary>
public static class SettlementBoardRules
{
    public static BoardTier GetTier(SettlementConfig config, Settlement settlement)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        if (Has(settlement, config.GuildDevelopment))
            return BoardTier.Guild;
        if (Has(settlement, config.BoardManagedDevelopment))
            return BoardTier.Managed;
        if (Has(settlement, config.BoardUpgradeDevelopment))
            return BoardTier.Upgraded;
        return BoardTier.Basic;
    }

    /// <summary>메인 / 주민 / 완료로 나눠 보여 주는 등급인지 (관리 해달이 맡은 뒤)</summary>
    public static bool ShowsSections(BoardTier tier) => tier >= BoardTier.Managed;

    /// <summary>한 번에 보이는 주민 부탁 수 (관리 해달이 맡기 전에는 0)</summary>
    public static int ResidentSlots(SettlementConfig config, BoardTier tier) =>
        ShowsSections(tier) ? config.ResidentRequestSlots : 0;

    /// <summary>
    /// 부탁 탭에 보일 부탁 (구역 순서대로). 관리 해달이 맡기 전: 잠기지 않은 메인 부탁을 끝낸 것까지 한 목록 (기존과 같음).
    /// 맡은 뒤: 메인(할 수 있음·진행 중) → 주민(진행 중 + 남은 칸만큼 할 수 있는 것) → 완료(메인·주민 전부).
    /// 진행 중인 부탁은 어느 등급이든 늘 보인다
    /// </summary>
    public static void CollectRows(SettlementConfig config, Settlement settlement, List<BoardRequestRow> rows) =>
        CollectRows(config, SortRequests(config), settlement, rows);

    /// <param name="sorted">SortRequests로 정렬해 둔 부탁 (매 프레임 정렬하지 않게)</param>
    public static void CollectRows(SettlementConfig config, IReadOnlyList<BoardRequestDefinition> sorted, Settlement settlement,
        List<BoardRequestRow> rows)
    {
        if (sorted == null)
            throw new ArgumentNullException(nameof(sorted));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));
        rows.Clear();

        var tier = GetTier(config, settlement);
        if (!ShowsSections(tier))
        {
            foreach (var request in sorted)
            {
                var status = SettlementRules.GetStatus(request, settlement);
                if (status == RequestStatus.Locked)
                    continue;
                if (request.Category == RequestCategory.Resident && status != RequestStatus.Building)
                    continue;
                rows.Add(new BoardRequestRow(status == RequestStatus.Completed ? BoardSection.Completed : BoardSection.Main, request, status));
            }
            return;
        }

        foreach (var request in sorted)
        {
            var status = SettlementRules.GetStatus(request, settlement);
            if (request.Category == RequestCategory.Main && (status == RequestStatus.Available || status == RequestStatus.Building))
                rows.Add(new BoardRequestRow(BoardSection.Main, request, status));
        }

        // 주민 부탁: 진행 중인 것은 늘, 남은 칸만큼 할 수 있는 것을 순서대로
        int slots = ResidentSlots(config, tier);
        int used = 0;
        foreach (var request in sorted)
        {
            if (request.Category == RequestCategory.Resident && SettlementRules.GetStatus(request, settlement) == RequestStatus.Building)
            {
                rows.Add(new BoardRequestRow(BoardSection.Resident, request, RequestStatus.Building));
                used++;
            }
        }
        foreach (var request in sorted)
        {
            if (used >= slots)
                break;
            if (request.Category == RequestCategory.Resident && SettlementRules.GetStatus(request, settlement) == RequestStatus.Available)
            {
                rows.Add(new BoardRequestRow(BoardSection.Resident, request, RequestStatus.Available));
                used++;
            }
        }

        foreach (var request in sorted)
        {
            if (settlement.IsCompleted(request.RequestId))
                rows.Add(new BoardRequestRow(BoardSection.Completed, request, RequestStatus.Completed));
        }
    }

    /// <summary>준비된 메인 부탁을 모두 끝냈는지 (마을회관의 "모두 마쳤어요")</summary>
    public static bool AreAllMainDone(SettlementConfig config, Settlement settlement)
    {
        foreach (var request in config.Requests)
        {
            if (request != null && request.Category == RequestCategory.Main && !settlement.IsCompleted(request.RequestId))
                return false;
        }
        return true;
    }

    #region 큰 부탁

    /// <summary>게시판·접수소에서 이 큰 부탁을 보여 주는지</summary>
    public static bool IsGroupVisible(MilestoneGroupDefinition group, Settlement settlement) =>
        group != null && Has(settlement, group.VisibleDevelopment);

    /// <summary>단계별 상태 (각 부탁의 기록에서 계산)</summary>
    public static void CollectSteps(MilestoneGroupDefinition group, Settlement settlement,
        List<(BoardRequestDefinition request, MilestoneStepState state)> steps)
    {
        if (group == null)
            throw new ArgumentNullException(nameof(group));
        if (steps == null)
            throw new ArgumentNullException(nameof(steps));
        steps.Clear();
        foreach (var request in group.Steps)
        {
            if (request != null)
                steps.Add((request, StepState(request, settlement)));
        }
    }

    public static MilestoneStepState StepState(BoardRequestDefinition request, Settlement settlement)
    {
        switch (SettlementRules.GetStatus(request, settlement))
        {
            case RequestStatus.Completed: return MilestoneStepState.Done;
            case RequestStatus.Building: return MilestoneStepState.InProgress;
            case RequestStatus.Available: return MilestoneStepState.Next;
            default: return MilestoneStepState.Locked;
        }
    }

    /// <summary>끝낸 단계 수</summary>
    public static int CountDone(MilestoneGroupDefinition group, Settlement settlement)
    {
        int done = 0;
        foreach (var request in group.Steps)
        {
            if (request != null && settlement.IsCompleted(request.RequestId))
                done++;
        }
        return done;
    }

    /// <summary>지금 손댈 단계 (진행 중 우선, 없으면 할 수 있는 것 중 앞의 것. 없으면 null)</summary>
    public static BoardRequestDefinition CurrentStep(MilestoneGroupDefinition group, Settlement settlement)
    {
        BoardRequestDefinition next = null;
        foreach (var request in group.Steps)
        {
            if (request == null)
                continue;
            var state = StepState(request, settlement);
            if (state == MilestoneStepState.InProgress)
                return request;
            if (state == MilestoneStepState.Next && next == null)
                next = request;
        }
        return next;
    }

    #endregion

    /// <summary>게시판 정렬: 순서(order) → 부탁 ID (같은 데이터면 늘 같은 순서)</summary>
    public static List<BoardRequestDefinition> SortRequests(SettlementConfig config)
    {
        var sorted = new List<BoardRequestDefinition>();
        foreach (var request in config.Requests)
        {
            if (request != null)
                sorted.Add(request);
        }
        sorted.Sort((a, b) =>
        {
            int order = a.Order.CompareTo(b.Order);
            return order != 0 ? order : string.CompareOrdinal(a.RequestId, b.RequestId);
        });
        return sorted;
    }

    private static bool Has(Settlement settlement, string developmentId) =>
        !string.IsNullOrEmpty(developmentId) && settlement.HasDevelopment(developmentId);
}
