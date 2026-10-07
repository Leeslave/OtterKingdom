using System;
using System.Collections.Generic;

/// <summary>
/// 불러온 세이브 맞추기 (데이터 + 상태 → 결과). SettlementManager.LoadFromSave가 옛 세이브 처리·전문 해달 옮기기 뒤에 부른다.
/// - 버전 옮기기: 버전마다 한 번만. 새 콘텐츠는 자동으로 주지 않는다
/// - 기록 맞추기: 끝낸 주민 작업·맡긴 역할은 있는데 그 부탁이 끝나 있지 않으면 (끝내는 도중에 꺼짐) 조용히 끝냄
/// </summary>
public static class SettlementMigration
{
    /// <summary>
    /// 세이브를 지금 버전으로 옮긴다 (이미 지금 버전이면 아무것도 하지 않음).
    /// 0 → 2 (P2): 게시판 성장·관리 해달·마을회관은 기록이 없으므로 처음부터 진행.
    /// farm_working이 이미 있으면 P2 첫 부탁(게시판 보강)이 그대로 열린다. 게시판·관리 해달·회관을 대신 주지 않는다
    /// </summary>
    /// <returns>옮겼으면 true</returns>
    public static bool MigrateToCurrent(Settlement settlement)
    {
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (settlement.Version >= SettlementSaveData.CurrentVersion)
            return false;

        // 0 → 2: 옮길 기록이 없음 (P2 세이브 필드는 비어 있음 = 미진행). 옛 세이브의 P0·P1 진행·생산·보상은 그대로 둔다
        // 2 → 3: 공동사업·집·생활 의뢰 기록이 없음 = 회관 뒤 첫 비축부터. 요정은 MigrateFairy가 먼저 맞춤
        // 3 → 4: 가로등 부탁이 생김. 첫 집이 있던 세이브는 MigrateLamp가 먼저 끝낸 것으로 맞춤
        settlement.SetVersion(SettlementSaveData.CurrentVersion);
        return true;
    }

    /// <summary>가로등 부탁 (건설 모드, 버전 4)</summary>
    public const string LampRequestId = "req_lamp";

    /// <summary>
    /// 버전 4 전 세이브: 가로등은 첫 집과 함께 생겼다. 첫 집(가로등 부탁의 필요 발전)이 이미 있으면 가로등 부탁을 조용히 끝내
    /// 가로등이 그대로 보이게 한다 (팝업·방명록 없음). 이미 버전 4이거나 부탁이 없으면 아무것도 하지 않는다
    /// </summary>
    /// <returns>끝냈으면 true</returns>
    public static bool MigrateLamp(SettlementConfig config, Settlement settlement, int fromVersion)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (fromVersion >= 4)
            return false;
        var request = config.FindRequest(LampRequestId);
        if (request == null || settlement.IsCompleted(request.RequestId)
            || string.IsNullOrEmpty(request.RequiredDevelopment) || !settlement.HasDevelopment(request.RequiredDevelopment))
            return false;
        return settlement.CompleteRequest(request.RequestId, SettlementRules.CompletionDevelopment(request));
    }

    /// <summary>
    /// P3 전(버전 3 미만) 세이브의 요정: 예전에는 밭 개간(농부의 일할 지역 운영 발전)만 끝나면 요정이 광장에 있었다.
    /// 그때 이미 요정을 만났다고 볼 수 있는 세이브(상점 안내를 봤거나 농부를 이미 파견함)는 요정·상점을 그대로 둔다 (방문 예약 + 도착).
    /// 밭만 개간하고 농부를 아직 파견하지 않았고 상점도 열어 본 적 없으면 새 규칙대로 파견 뒤에 찾아온다.
    /// 이미 버전 3이거나 요정 발전이 있으면 아무것도 하지 않는다 (다시 실행해도 결과가 같음)
    /// </summary>
    /// <returns>요정을 그대로 두었으면 true</returns>
    public static bool MigrateFairy(SettlementConfig config, Settlement settlement, int fromVersion, bool fairyShopSeen)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        string invited = config.FairyInvitedDevelopment;
        string arrived = config.FairyArrivedDevelopment;
        if (fromVersion >= 3 || string.IsNullOrEmpty(arrived) || settlement.HasDevelopment(arrived))
            return false;

        // 요정을 부르는 해달 (파견하면 방문 예약이 열리는 해달 = 농부)
        var caller = FairyCaller(config);
        if (caller == null)
            return false;

        bool oldGateOpen = settlement.HasDevelopment(caller.WorkRegion.OperationalDevelopment);
        bool dispatched = settlement.GetSpecialistState(caller.OtterId) >= SpecialistState.Assigned;
        if (!oldGateOpen || !(fairyShopSeen || dispatched))
            return false;

        if (!string.IsNullOrEmpty(invited))
            settlement.UnlockDevelopment(invited);
        settlement.UnlockDevelopment(arrived);
        return true;
    }

    /// <summary>파견하면 요정 방문이 예약되는 전문 해달 (농부. 없으면 null)</summary>
    public static SettlementOtterDefinition FairyCaller(SettlementConfig config)
    {
        string invited = config.FairyInvitedDevelopment;
        if (string.IsNullOrEmpty(invited))
            return null;
        foreach (var otter in config.Otters)
        {
            if (otter != null && otter.IsSpecialist && otter.AssignDevelopment == invited)
                return otter;
        }
        return null;
    }

    /// <summary>
    /// 파견했는데 파견 발전(예: 요정 방문 예약)이 없는 세이브를 맞춘다 (파견 직후 꺼짐·데이터 추가 전 세이브).
    /// 실제 만남(요정 도착)은 미리 주지 않고 예약만 연다
    /// </summary>
    /// <returns>이번에 연 해달들</returns>
    public static List<SettlementOtterDefinition> ReconcileAssignDevelopments(SettlementConfig config, Settlement settlement)
    {
        var opened = new List<SettlementOtterDefinition>();
        foreach (var otter in config.Otters)
        {
            if (otter == null || !otter.IsSpecialist || string.IsNullOrEmpty(otter.AssignDevelopment)
                || settlement.HasDevelopment(otter.AssignDevelopment))
                continue;
            if (settlement.GetSpecialistState(otter.OtterId) < SpecialistState.Assigned)
                continue;
            settlement.UnlockDevelopment(otter.AssignDevelopment);
            opened.Add(otter);
        }
        return opened;
    }

    /// <summary>
    /// 끝낸 기록에서 부탁 완료를 다시 판정한다: 주민 작업 부탁은 그 작업이 끝났으면, 역할 부탁은 그 역할을 맡겼으면 끝냄.
    /// 불러올 때 조용히 (완료 팝업·보상 없이). 이미 끝낸 부탁은 그대로
    /// </summary>
    /// <param name="completed">이번에 끝낸 부탁 (로그용, null이면 모으지 않음)</param>
    public static void ReconcileRecords(SettlementConfig config, Settlement settlement, List<BoardRequestDefinition> completed = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        completed?.Clear();
        foreach (var request in config.Requests)
        {
            if (request == null || settlement.IsCompleted(request.RequestId))
                continue;
            bool done = request.CompletionTask != null && settlement.IsTaskCompleted(request.CompletionTask.TaskId)
                || request.AssignRole != null && SettlementRoleRules.IsAssigned(request.AssignRole, settlement);
            if (done && SettlementRules.ApplyCompletion(request, settlement))
                completed?.Add(request);
        }
    }

    /// <summary>
    /// 옛 세이브에 부탁과 따로 열어 준 발전(SettlementConfig.LegacyDevelopments, 예: 낚시터 fishing_dock)을 결과로 짓는 부탁이
    /// 안 끝나 있으면 조용히 끝낸다 → 이미 쓰던 장소를 다시 짓게 하지 않는다. 그 부탁으로 찾아오는 해달도 같이 온다
    /// </summary>
    /// <param name="completed">이번에 끝낸 부탁 (로그용, null이면 모으지 않음)</param>
    public static void ReconcileLegacyConstructions(SettlementConfig config, Settlement settlement, List<BoardRequestDefinition> completed = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        completed?.Clear();
        foreach (var request in config.Requests)
        {
            var construction = request != null ? request.Construction : null;
            if (construction == null || settlement.IsCompleted(request.RequestId) || string.IsNullOrEmpty(construction.UnlockResultId))
                continue;
            bool legacy = false;
            foreach (var development in config.LegacyDevelopments)
                legacy |= development == construction.UnlockResultId;
            if (legacy && settlement.HasDevelopment(construction.UnlockResultId) && SettlementRules.ApplyCompletion(request, settlement))
                completed?.Add(request);
        }
    }
}
