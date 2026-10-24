using System;
using System.Collections.Generic;

[Serializable]
public class ScoutingJobSaveData
{
    public string regionId;
    public long startUtcTicks;
    public long endUtcTicks;
    public List<string> workers = new List<string>();
}

[Serializable]
public class ScoutingSaveData
{
    public int version;
    public List<string> discovered = new List<string>();
    public List<string> reports = new List<string>();
    public ScoutingJobSaveData job;
}

/// <summary>정찰의 시간·발견·귀환 원본. Unity 없이도 저장 복귀와 중복 처리 규칙을 검증할 수 있다.</summary>
public sealed class ScoutingProgress
{
    private readonly HashSet<string> _discovered = new HashSet<string>();
    private readonly List<string> _reports = new List<string>();
    private ScoutingJobSaveData _job;

    // 필드가 없던 세이브는 매니저가 열린 땅을 이관한 뒤 1로 올린다.
    public int Version { get; private set; }
    public string Destination => _job?.regionId;
    public long EndUtcTicks => _job?.endUtcTicks ?? 0;
    public IReadOnlyList<string> Reports => _reports;
    public IReadOnlyList<string> Workers => _job?.workers ?? (IReadOnlyList<string>)Array.Empty<string>();
    public bool HasDiscovered(string regionId) => !string.IsNullOrEmpty(regionId) && _discovered.Contains(regionId);
    public bool HasWorker(string otterId) => _job != null && _job.workers.Contains(otterId);

    public ScoutingProgress(ScoutingSaveData saved = null)
    {
        if (saved == null) return;
        Version = saved.version;
        if (saved.discovered != null)
            foreach (var id in saved.discovered) Discover(id);
        if (saved.reports != null)
            foreach (var id in saved.reports)
                if (HasDiscovered(id) && !_reports.Contains(id)) _reports.Add(id);
        var job = saved.job;
        if (job == null || string.IsNullOrEmpty(job.regionId) || HasDiscovered(job.regionId)
            || job.startUtcTicks <= 0 || job.endUtcTicks < job.startUtcTicks || job.endUtcTicks > DateTime.MaxValue.Ticks
            || job.workers == null || job.workers.Count == 0 || job.workers.Count > 2) return;
        var unique = new HashSet<string>();
        foreach (var id in job.workers)
            if (string.IsNullOrEmpty(id) || !unique.Add(id)) return;
        _job = Copy(job);
    }

    public void MarkMigrated() => Version = 1;

    public bool Discover(string regionId) => !string.IsNullOrEmpty(regionId) && _discovered.Add(regionId);

    public bool TryStart(string regionId, IReadOnlyList<string> workers, Func<string, bool> canWork,
        long nowUtcTicks, TimeSpan duration)
    {
        if (_job != null || string.IsNullOrEmpty(regionId) || HasDiscovered(regionId)
            || workers == null || workers.Count < 1 || workers.Count > 2 || canWork == null
            || nowUtcTicks <= 0 || nowUtcTicks > DateTime.MaxValue.Ticks
            || duration.Ticks <= 0 || duration.Ticks > DateTime.MaxValue.Ticks - nowUtcTicks) return false;
        var unique = new HashSet<string>();
        foreach (var id in workers)
            if (string.IsNullOrEmpty(id) || !unique.Add(id) || !canWork(id)) return false;
        _job = new ScoutingJobSaveData
        {
            regionId = regionId, workers = new List<string>(workers),
            startUtcTicks = nowUtcTicks, endUtcTicks = nowUtcTicks + duration.Ticks
        };
        return true;
    }

    /// <summary>보고서를 열기 전에 주민을 해제한다. 두 번째 Tick은 아무것도 하지 않는다.</summary>
    public string Tick(long nowUtcTicks)
    {
        if (_job == null || nowUtcTicks < _job.endUtcTicks) return null;
        string id = _job.regionId;
        _job = null;
        if (Discover(id)) _reports.Add(id);
        return id;
    }

    public bool Cancel()
    {
        if (_job == null) return false;
        _job = null;
        return true;
    }

    public void ReadReport(string regionId) => _reports.Remove(regionId);

    public bool Shorten(long nowUtcTicks, TimeSpan limit)
    {
        if (_job != null && limit.Ticks > 0 && _job.endUtcTicks - nowUtcTicks > limit.Ticks)
        {
            _job.endUtcTicks = nowUtcTicks + limit.Ticks;
            return true;
        }
        return false;
    }

    public ScoutingSaveData Save()
    {
        var discovered = new List<string>(_discovered);
        discovered.Sort(StringComparer.Ordinal);
        return new ScoutingSaveData
        {
            version = Version, discovered = discovered, reports = new List<string>(_reports),
            job = _job != null ? Copy(_job) : null
        };
    }

    private static ScoutingJobSaveData Copy(ScoutingJobSaveData job) => new ScoutingJobSaveData
    {
        regionId = job.regionId, startUtcTicks = job.startUtcTicks, endUtcTicks = job.endUtcTicks,
        workers = new List<string>(job.workers)
    };
}
