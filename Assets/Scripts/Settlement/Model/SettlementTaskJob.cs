using System;
using System.Collections.Generic;

/// <summary>주민 작업 하나의 상태</summary>
public enum SettlementTaskState
{
    Locked,     // 앞선 발전이 아직 (예: 길을 다 치우기 전)
    Available,  // 주민을 보내 시작할 수 있음
    Working,    // 주민이 일하는 중
    Completed,
}

/// <summary>주민 해달이 지금 작업 중인지 (주민인지 아닌지는 ResidentState가 따로 정함)</summary>
public enum ResidentWorkState
{
    Idle,
    Working,
}

/// <summary>개간 지역의 진행 단계</summary>
public enum RegionProgressState
{
    Locked,          // 아직 발견하지 못함
    Discovered,      // 있는 건 알지만 아직 갈 수 없음
    PlayerClearing,  // 플레이어가 길을 막은 것을 직접 치우는 중
    AwaitingWorkers, // 길은 열렸지만 주민 해달을 보내 정비해야 함
    WorkerPreparing, // 주민 해달이 정비하는 중
    Operational,     // 운영 (기능·꾸미기를 쓸 수 있음)
}

/// <summary>
/// 진행 중인 주민 작업: 누가 언제부터 언제까지 하는지. 끝나는 시각(UTC)으로 재서 게임을 꺼 둔 동안에도 흐른다.
/// </summary>
public class SettlementTaskJob
{
    private readonly List<string> _otterIds;

    public string TaskId { get; }
    public long StartUtcTicks { get; }
    public long EndUtcTicks { get; }
    public IReadOnlyList<string> OtterIds => _otterIds;

    public SettlementTaskJob(string taskId, IEnumerable<string> otterIds, long startUtcTicks, long endUtcTicks)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentNullException(nameof(taskId));
        if (otterIds == null)
            throw new ArgumentNullException(nameof(otterIds));

        TaskId = taskId;
        _otterIds = new List<string>(otterIds);
        StartUtcTicks = startUtcTicks;
        EndUtcTicks = Math.Max(startUtcTicks, endUtcTicks);
    }

    public bool HasOtter(string otterId) => _otterIds.Contains(otterId);

    /// <summary>0~1 진행 비율</summary>
    public float Progress(long nowUtcTicks)
    {
        long total = EndUtcTicks - StartUtcTicks;
        if (total <= 0)
            return 1f;
        double ratio = (double)(nowUtcTicks - StartUtcTicks) / total;
        return (float)Math.Max(0d, Math.Min(1d, ratio));
    }

    public TimeSpan Remaining(long nowUtcTicks) => TimeSpan.FromTicks(Math.Max(0, EndUtcTicks - nowUtcTicks));

    public bool IsDue(long nowUtcTicks) => nowUtcTicks >= EndUtcTicks;
}
