using System;

[Serializable]
public class ProsperityAdvanceSaveData
{
    public string id;
    public int futureLevel;
    public int progress;
}

/// <summary>첫 사업 보상과 RP 선지급. 정규 레벨 지급 키를 공유해 선지급의 재지급을 막는다.</summary>
public sealed partial class ProsperityResearch
{
    public const string PlanProjectId = "prosperity_plan_01";
    public const string EraDevelopment = "prosperity_started";
    public const string PlanDevelopment = "prosperity_plan_completed";
    public const string HarvestAdvance = "harvest";
    public const string SalesAdvance = "sales";

    public static int AdvanceGoal(string id) => id == HarvestAdvance ? 30 : id == SalesAdvance ? 1000 : 0;

    private static bool SanitizeAdvance(ProsperitySaveData data)
    {
        var active = data.advance;
        if (active == null) return false;
        int goal = AdvanceGoal(active.id);
        if (goal == 0 || (active.futureLevel != 26 && active.futureLevel != 27)
            || data.advanceCompleted.Contains(active.id)
            || data.rpGrants.Contains($"prosperity:level:{active.futureLevel}"))
        {
            data.advance = null;
            return true;
        }
        int clamped = Math.Max(0, Math.Min(active.progress, goal));
        if (clamped == active.progress) return false;
        active.progress = clamped;
        return true;
    }

    public bool SyncPlanReward(bool completed)
    {
        if (!Data.introCompleted || !completed || !GrantOnce("prosperity:project:" + PlanProjectId, 3)) return false;
        Changed?.Invoke();
        return true;
    }

    public int AvailableAdvanceLevel(int level)
    {
        for (int future = 26; future <= 27; future++)
            if (level < future && !Data.rpGrants.Contains($"prosperity:level:{future}")) return future;
        return 0;
    }

    public bool CanAcceptAdvance(string id, int level) => Data.introCompleted && Data.advance == null
        && AdvanceGoal(id) > 0 && !Data.advanceCompleted.Contains(id) && AvailableAdvanceLevel(level) > 0;

    public bool AcceptAdvance(string id, int level)
    {
        if (!CanAcceptAdvance(id, level)) return false;
        Data.advance = new ProsperityAdvanceSaveData { id = id, futureLevel = AvailableAdvanceLevel(level) };
        Changed?.Invoke();
        return true;
    }

    /// <summary>수락 후 실제 수확·판매만 호출한다. 로드·선물·이동·환불은 집계하지 않는다.</summary>
    public bool ReportAdvance(string id, int amount)
    {
        var active = Data.advance;
        if (!Data.introCompleted || active == null || active.id != id || amount <= 0) return false;
        int goal = AdvanceGoal(id);
        int next = (int)Math.Min(goal, (long)active.progress + amount);
        if (next == active.progress) return false;
        active.progress = next;
        Changed?.Invoke();
        return true;
    }

    public bool ClaimAdvance(int level)
    {
        var active = Data.advance;
        if (!Data.introCompleted || active == null) return false;
        if (level >= active.futureLevel)
        {
            SyncLevel(level);
            return false;
        }
        if (active.progress < AdvanceGoal(active.id)) return false;
        if (!GrantOnce($"prosperity:level:{active.futureLevel}", 1)) return false;
        Data.advanceCompleted.Add(active.id);
        Data.advance = null;
        Changed?.Invoke();
        return true;
    }

    public bool CancelAdvance()
    {
        if (Data.advance == null) return false;
        Data.advance = null;
        Changed?.Invoke();
        return true;
    }
}
