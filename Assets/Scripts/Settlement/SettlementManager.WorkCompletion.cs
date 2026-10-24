using System;

public partial class SettlementManager
{
    private bool _processingDueWork;

    // 오프라인 복귀 시 작업·정찰·건물의 완료 순서를 섞지 않는다.
    // 각 완료 직전까지 이전 노동 상태로 생산한 뒤 주민/시설을 바꿔, 같은 주민의 시간을 두 번 세지 않는다.
    private void FinishDueWork(bool notify = true)
    {
        if (!IsLoaded || _processingDueWork) return;
        _processingDueWork = true;
        bool completed = false;
        try
        {
            long now = NowTicks;
            while (true)
            {
                long end = long.MaxValue;
                Action finish = null;
                foreach (var task in Settlement.TaskJobs)
                    if (task.IsDue(now) && task.EndUtcTicks < end)
                    {
                        end = task.EndUtcTicks;
                        finish = () => FinishTask(task);
                    }
                foreach (var building in Settlement.Buildings)
                    if (building.IsDue(now) && building.EndUtcTicks < end)
                    {
                        end = building.EndUtcTicks;
                        finish = () => CompleteBuildingRecord(building, notify);
                    }
                if (Settlement.Job != null && Settlement.Job.IsDue(now) && Settlement.Job.EndUtcTicks < end)
                {
                    end = Settlement.Job.EndUtcTicks;
                    finish = FinishJob;
                }
                if (Settlement.Scouting.Destination != null && Settlement.Scouting.EndUtcTicks <= now
                    && Settlement.Scouting.EndUtcTicks < end)
                {
                    end = Settlement.Scouting.EndUtcTicks;
                    finish = TickScouting;
                }
                if (finish == null) break;
                RunProduction(end, now);
                finish();
                completed = true;
            }
            if (completed) RunProduction();
        }
        finally { _processingDueWork = false; }
    }
}
