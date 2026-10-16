using System;
using System.Collections.Generic;

/// <summary>자리를 골라 지은 건물 하나의 공사·완성 기록. 놓인 자리는 꾸미기 격자(같은 InstanceId)가 원본</summary>
public class BuildingRecord
{
    /// <summary>꾸미기 격자의 배치 개체 ID (광장 격자)</summary>
    public int InstanceId { get; }
    public string BuildingId { get; }
    public long StartUtcTicks { get; internal set; }
    public long EndUtcTicks { get; internal set; }
    public bool Built { get; internal set; }

    public BuildingRecord(int instanceId, string buildingId, long startUtcTicks, long endUtcTicks, bool built)
    {
        InstanceId = instanceId;
        BuildingId = buildingId;
        StartUtcTicks = startUtcTicks;
        EndUtcTicks = endUtcTicks;
        Built = built;
    }

    /// <summary>0~1 공사 진행 비율 (다 지었으면 1)</summary>
    public float Progress(long nowUtcTicks)
    {
        if (Built)
            return 1f;
        long total = EndUtcTicks - StartUtcTicks;
        if (total <= 0)
            return 1f;
        double ratio = (double)(nowUtcTicks - StartUtcTicks) / total;
        return (float)Math.Max(0d, Math.Min(1d, ratio));
    }

    public TimeSpan Remaining(long nowUtcTicks) => Built ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Max(0, EndUtcTicks - nowUtcTicks));

    public bool IsDue(long nowUtcTicks) => !Built && nowUtcTicks >= EndUtcTicks;
}

/// <summary>공사·완성 기록이 저장되는 한 줄 (P4)</summary>
[Serializable]
public class BuildingSaveData
{
    public int instanceId;
    public string buildingId;
    public long startUtcTicks;
    public long endUtcTicks;
    public bool built;
}

/// <summary>자리를 골라 짓는 건물 (P4): 공사 시작 · 완성 · 세이브</summary>
public partial class Settlement
{
    private readonly List<BuildingRecord> _buildings = new List<BuildingRecord>();

    /// <summary>지었거나 짓는 중인 건물 (지은 순서)</summary>
    public IReadOnlyList<BuildingRecord> Buildings => _buildings;

    public BuildingRecord FindBuilding(int instanceId) => _buildings.Find(b => b.InstanceId == instanceId);

    /// <summary>이 건물을 지었거나 짓는 중인 수</summary>
    public int CountBuildings(string buildingId)
    {
        int count = 0;
        foreach (var record in _buildings)
        {
            if (record.BuildingId == buildingId)
                count++;
        }
        return count;
    }

    /// <summary>다 지은 이 건물 수</summary>
    public int CountBuilt(string buildingId)
    {
        int count = 0;
        foreach (var record in _buildings)
        {
            if (record.Built && record.BuildingId == buildingId)
                count++;
        }
        return count;
    }

    /// <summary>짓는 중인 건물 수</summary>
    public int ActiveBuildingCount
    {
        get
        {
            int count = 0;
            foreach (var record in _buildings)
            {
                if (!record.Built)
                    count++;
            }
            return count;
        }
    }

    /// <returns>새로 시작했으면 true (같은 배치 개체는 한 번만)</returns>
    public bool StartBuilding(int instanceId, string buildingId, long startUtcTicks, long endUtcTicks)
    {
        if (instanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(instanceId));
        if (string.IsNullOrEmpty(buildingId))
            throw new ArgumentNullException(nameof(buildingId));
        if (FindBuilding(instanceId) != null)
            return false;
        _buildings.Add(new BuildingRecord(instanceId, buildingId, startUtcTicks, Math.Max(startUtcTicks, endUtcTicks), false));
        OnChanged?.Invoke();
        return true;
    }

    /// <returns>이번에 완성했으면 true</returns>
    public bool FinishBuilding(int instanceId)
    {
        var record = FindBuilding(instanceId);
        if (record == null || record.Built)
            return false;
        record.Built = true;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>기록을 지움 (자리가 사라진 기록 정리용)</summary>
    public bool RemoveBuilding(int instanceId)
    {
        int removed = _buildings.RemoveAll(b => b.InstanceId == instanceId);
        if (removed > 0)
            OnChanged?.Invoke();
        return removed > 0;
    }

    // 개발용 ShortenTimers에서: 짓는 중인 건물도 limit 안에 끝나게
    private bool ShortenBuildings(long nowUtcTicks, long max)
    {
        bool changed = false;
        foreach (var record in _buildings)
        {
            if (record.Built || record.EndUtcTicks - nowUtcTicks <= max)
                continue;
            var (start, end) = Shorten(record.Progress(nowUtcTicks), nowUtcTicks, max);
            record.StartUtcTicks = start;
            record.EndUtcTicks = end;
            changed = true;
        }
        return changed;
    }

    private void LoadBuildings(SettlementSaveData saved)
    {
        _buildings.Clear();
        if (saved.buildings == null)
            return;
        foreach (var b in saved.buildings)
        {
            if (b == null || b.instanceId <= 0 || string.IsNullOrEmpty(b.buildingId) || FindBuilding(b.instanceId) != null)
                continue;
            _buildings.Add(new BuildingRecord(b.instanceId, b.buildingId, b.startUtcTicks, Math.Max(b.startUtcTicks, b.endUtcTicks), b.built));
        }
    }

    private void WriteBuildings(SettlementSaveData result)
    {
        result.buildings = new List<BuildingSaveData>();
        foreach (var record in _buildings)
        {
            result.buildings.Add(new BuildingSaveData
            {
                instanceId = record.InstanceId,
                buildingId = record.BuildingId,
                startUtcTicks = record.StartUtcTicks,
                endUtcTicks = record.EndUtcTicks,
                built = record.Built,
            });
        }
    }
}
