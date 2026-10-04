using System;

/// <summary>
/// 관리 역할 규칙 (데이터 + 상태 → 결과). 관리 해달은 광장에 찾아와 처음 만난 뒤 대화로 역할을 맡는다:
/// 미방문 → 방문 예약(주민 목록에 있음) → 광장에서 만남(만남 기록) → 역할 맡김(역할 기록) → 시설 옆 근무.
/// 생산 지역을 맡는 전문 해달의 처지(SpecialistState)와는 따로 간다.
/// </summary>
public static class SettlementRoleRules
{
    /// <summary>
    /// 지금 이 역할을 맡길 수 있는지: 맡을 해달이 찾아왔고 광장에서 만났으며, 앞선 발전이 열렸고,
    /// 역할이 비어 있고, 그 해달이 다른 역할을 맡고 있지 않음
    /// </summary>
    public static bool CanAssign(ManagementRoleDefinition role, Settlement settlement)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (role.Otter == null || string.IsNullOrEmpty(role.RoleId))
            return false;

        string otterId = role.Otter.OtterId;
        return !settlement.TryGetRole(role.RoleId, out _)
            && !settlement.HasRole(otterId)
            && settlement.TryGetResidentState(otterId, out _)
            && settlement.HasMet(otterId)
            && settlement.HasDevelopment(role.RequiredDevelopment);
    }

    /// <summary>역할을 맡긴다: 역할 기록을 남기고 결과 발전을 연다 (연타·씬 재진입에도 한 번만)</summary>
    /// <returns>이번에 맡겼으면 true</returns>
    public static bool Assign(ManagementRoleDefinition role, Settlement settlement)
    {
        if (!CanAssign(role, settlement))
            return false;
        if (!settlement.AssignRole(role.RoleId, role.Otter.OtterId, role.StationId))
            return false;
        if (!string.IsNullOrEmpty(role.ResultDevelopment))
            settlement.UnlockDevelopment(role.ResultDevelopment);
        return true;
    }

    /// <summary>이 역할을 이미 맡겼는지</summary>
    public static bool IsAssigned(ManagementRoleDefinition role, Settlement settlement) =>
        role != null && settlement.TryGetRole(role.RoleId, out _);
}
