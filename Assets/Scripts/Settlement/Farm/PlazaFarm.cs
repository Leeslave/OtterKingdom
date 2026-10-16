using System;
using System.Collections.Generic;

/// <summary>세이브: 광장 밭 칸 하나 (꾸미기 격자 배치 ID)</summary>
[Serializable]
public class FarmFieldSaveData
{
    public int instanceId;
    // 심은 작물 (비면 빈 밭)
    public string cropId;
    // 다 자라기까지 남은 시간 (흙 · 스프링클러 · 농업 구역 효과 전, 기본 초)
    public float remainingSec;
    public bool ready;
    // 흙 단계 (0 = 보통 흙)
    public int soil;
    // 씨앗이 없어 다시 못 심고 기다리는 작물 (씨앗이 생기면 농부가 심음)
    public string waitingCropId;
}

[Serializable]
public class FarmStackSaveData
{
    public string id;
    public int count;
}

/// <summary>세이브: 수확 상자 하나</summary>
[Serializable]
public class FarmCrateSaveData
{
    public int instanceId;
    public List<FarmStackSaveData> items = new List<FarmStackSaveData>();
}

/// <summary>세이브: 광장 농장 (SettlementSaveData.farm)</summary>
[Serializable]
public class PlazaFarmSaveData
{
    public List<FarmFieldSaveData> fields = new List<FarmFieldSaveData>();
    public List<FarmCrateSaveData> crates = new List<FarmCrateSaveData>();
    // 옛 밭 씬(밭 3 × 고랑 3)을 광장 밭으로 옮겼는지
    public bool migrated;
    public float cartTimer;
    // 마지막으로 돌린 시각 (꺼 둔 동안 계산, UTC ticks)
    public long lastUtcTicks;
}

/// <summary>
/// 광장 농장 (순수 C#): 밭 칸마다 작물이 자라고(속도는 칸마다 다름 — 흙 · 스프링클러 · 농업 구역), 다 자라면 농부가 거둬
/// 상자에 넣는다. 상자는 용량까지 차고 플레이어가 거둔다. 화면 · 걷기 · 씨앗 · 가방은 모른다 (SettlementManager.Farm이 이어 줌).
/// Docs/농장_설계형_기획.md
/// </summary>
public class PlazaFarm
{
    private readonly PlazaFarmSaveData _data;

    public PlazaFarm(PlazaFarmSaveData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public PlazaFarmSaveData Data => _data;
    public IReadOnlyList<FarmFieldSaveData> Fields => _data.fields;
    public IReadOnlyList<FarmCrateSaveData> Crates => _data.crates;

    public FarmFieldSaveData Field(int instanceId) => _data.fields.Find(f => f.instanceId == instanceId);
    public FarmCrateSaveData Crate(int instanceId) => _data.crates.Find(c => c.instanceId == instanceId);

    #region 놓인 칸 맞추기

    /// <summary>다 지은 밭 칸 · 상자 목록에 맞춤 (새로 지은 것은 빈 밭 · 빈 상자, 없어진 것은 지움). 바뀌었으면 true</summary>
    public bool Sync(IEnumerable<int> fieldIds, IEnumerable<int> crateIds)
    {
        bool changed = false;
        var fields = new HashSet<int>(fieldIds);
        changed |= _data.fields.RemoveAll(f => !fields.Contains(f.instanceId)) > 0;
        foreach (int id in fields)
        {
            if (Field(id) == null)
            {
                _data.fields.Add(new FarmFieldSaveData { instanceId = id });
                changed = true;
            }
        }
        var crates = new HashSet<int>(crateIds);
        changed |= _data.crates.RemoveAll(c => !crates.Contains(c.instanceId)) > 0;
        foreach (int id in crates)
        {
            if (Crate(id) == null)
            {
                _data.crates.Add(new FarmCrateSaveData { instanceId = id });
                changed = true;
            }
        }
        return changed;
    }

    #endregion

    #region 밭

    /// <summary>심는다 (자라는 기본 시간). 이미 자라는 작물은 바꿈</summary>
    public void Plant(int fieldId, string cropId, float seconds)
    {
        var field = Field(fieldId) ?? throw new ArgumentException($"밭 칸 {fieldId}이 없습니다.", nameof(fieldId));
        field.cropId = cropId;
        field.remainingSec = Math.Max(0f, seconds);
        field.ready = seconds <= 0f;
        field.waitingCropId = null;
    }

    /// <summary>비운다 (작물을 뽑음)</summary>
    public void Clear(int fieldId)
    {
        var field = Field(fieldId);
        if (field == null)
            return;
        field.cropId = null;
        field.remainingSec = 0f;
        field.ready = false;
        field.waitingCropId = null;
    }

    /// <summary>시간이 흐름. speed(밭) = 그 칸의 자람 속도 배수 (1 = 기본). 새로 다 자란 칸 수</summary>
    public int Grow(float seconds, Func<FarmFieldSaveData, float> speed)
    {
        if (seconds <= 0f)
            return 0;
        int done = 0;
        foreach (var field in _data.fields)
        {
            if (string.IsNullOrEmpty(field.cropId) || field.ready)
                continue;
            field.remainingSec -= seconds * Math.Max(0.01f, speed(field));
            if (field.remainingSec <= 0f)
            {
                field.remainingSec = 0f;
                field.ready = true;
                done++;
            }
        }
        return done;
    }

    /// <summary>다 자란 칸을 거둔다 (칸은 빔 — 다시 심는 것은 부른 쪽이). 거둔 작물 ID (없으면 null)</summary>
    public string Harvest(int fieldId)
    {
        var field = Field(fieldId);
        if (field == null || !field.ready || string.IsNullOrEmpty(field.cropId))
            return null;
        string crop = field.cropId;
        field.cropId = null;
        field.ready = false;
        field.remainingSec = 0f;
        return crop;
    }

    public void SetSoil(int fieldId, int soil)
    {
        var field = Field(fieldId);
        if (field != null)
            field.soil = Math.Max(0, soil);
    }

    #endregion

    #region 상자

    public static int Count(FarmCrateSaveData crate)
    {
        int n = 0;
        foreach (var stack in crate.items)
            n += stack.count;
        return n;
    }

    /// <summary>상자에 넣는다 (용량까지). 넣은 수</summary>
    public int Deposit(int crateId, string itemId, int amount, int capacity)
    {
        var crate = Crate(crateId);
        if (crate == null || amount <= 0)
            return 0;
        int n = Math.Min(amount, capacity - Count(crate));
        if (n <= 0)
            return 0;
        var stack = crate.items.Find(s => s.id == itemId);
        if (stack == null)
            crate.items.Add(stack = new FarmStackSaveData { id = itemId });
        stack.count += n;
        return n;
    }

    /// <summary>상자에서 꺼낸다 (take(아이템, 개수) = 가방에 들어간 수, 못 들어간 것은 상자에 남음). 꺼낸 묶음</summary>
    public List<FarmStackSaveData> Collect(int crateId, Func<string, int, int> take)
    {
        var result = new List<FarmStackSaveData>();
        var crate = Crate(crateId);
        if (crate == null)
            return result;
        foreach (var stack in crate.items)
        {
            if (stack.count <= 0)
                continue;
            int n = take(stack.id, stack.count);
            if (n <= 0)
                continue;
            stack.count -= n;
            result.Add(new FarmStackSaveData { id = stack.id, count = n });
        }
        crate.items.RemoveAll(s => s.count <= 0);
        return result;
    }

    /// <summary>모든 상자에 남은 자리</summary>
    public int FreeSpace(int capacity)
    {
        int free = 0;
        foreach (var crate in _data.crates)
            free += Math.Max(0, capacity - Count(crate));
        return free;
    }

    #endregion
}
