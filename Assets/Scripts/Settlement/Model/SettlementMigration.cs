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
        settlement.SetVersion(SettlementSaveData.CurrentVersion);
        return true;
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
}
