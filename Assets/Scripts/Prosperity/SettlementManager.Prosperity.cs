using System;

/// <summary>번영 사업은 기존 P3 순차·반복 사업과 별도로 진행하되 같은 납품·단계·완료 규칙을 사용한다.</summary>
public partial class SettlementManager
{
    public CommunityProjectDefinition ProsperityPlanProject { get; private set; }

    public void BeginProsperity()
    {
        if (!IsLoaded || Settlement.HasDevelopment(ProsperityResearch.EraDevelopment)) return;
        Settlement.UnlockDevelopment(ProsperityResearch.EraDevelopment);
        _projectsDirty = true;
    }

    public void RegisterProsperityPlan(CommunityProjectDefinition project)
    {
        if (project == null || project.ProjectId != ProsperityResearch.PlanProjectId)
            throw new ArgumentException("번영 계획 사업이 필요합니다.", nameof(project));
        ProsperityPlanProject = project;
        _projectsDirty = true;
    }

    private void UpdateProsperityPlan(bool silent)
    {
        var project = ProsperityPlanProject;
        if (project == null || CommunityProjectRules.IsCompleted(project, Settlement)) return;
        var status = GetProjectStatus(project);
        if (status.Phase == ProjectPhase.Locked || status.Phase == ProjectPhase.NeedsLevel) return;
        if (CommunityProjectRules.IsDelivered(project, Settlement))
            Settlement.UnlockDevelopment(project.PaidDevelopment);
        if (CommunityProjectRules.IsReadyToComplete(project, Settlement, PlayerLevel)) CompleteProject(project, silent);
    }
}
