using System;
using System.Collections.Generic;

/// <summary>공동사업 한 회차의 진행 (납품한 양 · 고른 소품 · 끝냈는지). 단계 완료는 원본 기록에서 계산</summary>
public class ProjectProgress
{
    private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

    public string ProjectId { get; }
    public int Cycle { get; internal set; }
    public int Gold { get; internal set; }
    public string Choice { get; internal set; }
    public bool Completed { get; internal set; }

    public ProjectProgress(string projectId, int cycle)
    {
        ProjectId = projectId;
        Cycle = cycle;
    }

    public IReadOnlyDictionary<string, int> Items => _items;

    public int Delivered(string itemId) => !string.IsNullOrEmpty(itemId) && _items.TryGetValue(itemId, out int count) ? count : 0;

    internal void AddItem(string itemId, int amount)
    {
        if (amount <= 0)
            return;
        _items[itemId] = Delivered(itemId) + amount;
    }

    internal void ResetDeliveries()
    {
        Gold = 0;
        _items.Clear();
        Choice = null;
    }
}

/// <summary>집 한 채 (인스턴스 · 집 종류 · 배치 자리 · 입주민)</summary>
public class HouseRecord
{
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public string SlotId { get; }
    public string ResidentId { get; internal set; }

    public HouseRecord(string instanceId, string definitionId, string slotId, string residentId)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        SlotId = slotId;
        ResidentId = residentId;
    }
}

/// <summary>게시판에 걸린 생활 의뢰 하나</summary>
public readonly struct LifeRequestRecord
{
    public readonly int Serial;
    public readonly string TemplateId;
    public readonly string ItemId;
    public readonly int Amount;

    public LifeRequestRecord(int serial, string templateId, string itemId, int amount)
    {
        Serial = serial;
        TemplateId = templateId;
        ItemId = itemId;
        Amount = amount;
    }

    /// <summary>주민 작업 의뢰의 작업 기록 ID (틀 작업 ID#회차)</summary>
    public string TaskInstanceId(SettlementTaskDefinition template) => template != null ? $"{template.TaskId}#{Serial}" : null;
}

/// <summary>P3: 공동사업 · 사업 보상 기록 · 집 · 생활 의뢰</summary>
public partial class Settlement
{
    // 사업 ID → 지금 회차의 진행 (한 번만 하는 사업은 끝난 뒤에도 남음, 반복 사업은 지금 회차만)
    private readonly Dictionary<string, ProjectProgress> _projects = new Dictionary<string, ProjectProgress>();
    private readonly HashSet<string> _rewardKeys = new HashSet<string>();
    private readonly List<HouseRecord> _houses = new List<HouseRecord>();
    private readonly List<LifeRequestRecord> _lifeRequests = new List<LifeRequestRecord>();

    public IReadOnlyCollection<ProjectProgress> Projects => _projects.Values;
    public IReadOnlyList<HouseRecord> Houses => _houses;
    public IReadOnlyList<LifeRequestRecord> LifeRequests => _lifeRequests;
    public int LifeRequestSerial { get; private set; }
    public int LifeRequestsDone { get; private set; }

    #region 공동사업

    /// <summary>이 사업의 진행 (아직 손대지 않았으면 null)</summary>
    public ProjectProgress GetProject(string projectId) =>
        !string.IsNullOrEmpty(projectId) && _projects.TryGetValue(projectId, out var progress) ? progress : null;

    /// <summary>반복 사업의 지금 회차 (= 끝낸 회차 수). 손대지 않았으면 0</summary>
    public int ProjectCycle(string projectId) => GetProject(projectId)?.Cycle ?? 0;

    private ProjectProgress GetOrCreateProject(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentNullException(nameof(projectId));
        if (!_projects.TryGetValue(projectId, out var progress))
        {
            progress = new ProjectProgress(projectId, 0);
            _projects[projectId] = progress;
        }
        return progress;
    }

    /// <summary>
    /// 재료를 넣는다 (골드·아이템 수량을 더함). 결제는 SettlementManager가 같은 프레임에 먼저 하고,
    /// 한 번의 저장으로 둘 다 남는다 (재료 차감과 납품량이 어긋나지 않게)
    /// </summary>
    public void Deliver(string projectId, int gold, IReadOnlyList<(string itemId, int amount)> items)
    {
        var progress = GetOrCreateProject(projectId);
        if (progress.Completed)
            throw new InvalidOperationException($"이미 끝낸 사업입니다: {projectId}");
        if (gold < 0)
            throw new ArgumentOutOfRangeException(nameof(gold));
        progress.Gold += gold;
        if (items != null)
        {
            foreach (var (itemId, amount) in items)
            {
                if (amount < 0)
                    throw new ArgumentOutOfRangeException(nameof(items));
                if (!string.IsNullOrEmpty(itemId))
                    progress.AddItem(itemId, amount);
            }
        }
        OnChanged?.Invoke();
    }

    /// <summary>환영 소품을 고름 (한 번 고르면 바꾸지 않음)</summary>
    /// <returns>이번에 골랐으면 true</returns>
    public bool SetProjectChoice(string projectId, string choice)
    {
        if (string.IsNullOrEmpty(choice))
            throw new ArgumentNullException(nameof(choice));
        var progress = GetOrCreateProject(projectId);
        if (!string.IsNullOrEmpty(progress.Choice))
            return false;
        progress.Choice = choice;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>한 번만 하는 사업을 끝냄 (중복 호출은 무시)</summary>
    /// <returns>새로 끝냈으면 true</returns>
    public bool MarkProjectCompleted(string projectId)
    {
        var progress = GetOrCreateProject(projectId);
        if (progress.Completed)
            return false;
        progress.Completed = true;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>반복 사업의 이 회차를 끝내고 다음 회차를 비워 준비 (회차가 다르면 무시 → 같은 회차를 두 번 끝내지 않음)</summary>
    /// <returns>이번에 끝냈으면 true</returns>
    public bool AdvanceProjectCycle(string projectId, int cycle)
    {
        var progress = GetOrCreateProject(projectId);
        if (progress.Cycle != cycle)
            return false;
        progress.Cycle = cycle + 1;
        progress.ResetDeliveries();
        OnChanged?.Invoke();
        return true;
    }

    public bool HasReward(string key) => !string.IsNullOrEmpty(key) && _rewardKeys.Contains(key);

    /// <summary>보상 지급 기록 (경험치를 주기 직전에. 이미 있으면 false → 주지 않음)</summary>
    public bool AddReward(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));
        if (!_rewardKeys.Add(key))
            return false;
        OnChanged?.Invoke();
        return true;
    }

    #endregion

    #region 집

    public HouseRecord FindHouseBySlot(string slotId)
    {
        foreach (var house in _houses)
        {
            if (house.SlotId == slotId)
                return house;
        }
        return null;
    }

    public HouseRecord FindHouseOf(string otterId)
    {
        if (string.IsNullOrEmpty(otterId))
            return null;
        foreach (var house in _houses)
        {
            if (house.ResidentId == otterId)
                return house;
        }
        return null;
    }

    /// <summary>집 한 채를 기록 (같은 자리에는 한 채만)</summary>
    /// <returns>새로 생겼으면 true</returns>
    public bool AddHouse(string instanceId, string definitionId, string slotId)
    {
        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentNullException(nameof(instanceId));
        if (string.IsNullOrEmpty(slotId))
            throw new ArgumentNullException(nameof(slotId));
        if (FindHouseBySlot(slotId) != null || _houses.Exists(h => h.InstanceId == instanceId))
            return false;
        _houses.Add(new HouseRecord(instanceId, definitionId, slotId, null));
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>집에 해달이 입주 (빈 집에, 다른 집에 살지 않는 해달만. 연타해도 한 번)</summary>
    /// <returns>이번에 입주했으면 true</returns>
    public bool SetHouseResident(string instanceId, string otterId)
    {
        if (string.IsNullOrEmpty(otterId))
            throw new ArgumentNullException(nameof(otterId));
        var house = _houses.Find(h => h.InstanceId == instanceId);
        if (house == null || !string.IsNullOrEmpty(house.ResidentId) || FindHouseOf(otterId) != null)
            return false;
        house.ResidentId = otterId;
        OnChanged?.Invoke();
        return true;
    }

    #endregion

    #region 생활 의뢰

    /// <summary>새 생활 의뢰를 건다 (회차가 하나 올라감)</summary>
    public LifeRequestRecord AddLifeRequest(string templateId, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(templateId))
            throw new ArgumentNullException(nameof(templateId));
        var record = new LifeRequestRecord(LifeRequestSerial, templateId, itemId, amount);
        _lifeRequests.Add(record);
        LifeRequestSerial++;
        OnChanged?.Invoke();
        return record;
    }

    public bool TryGetLifeRequest(int serial, out LifeRequestRecord record)
    {
        foreach (var r in _lifeRequests)
        {
            if (r.Serial == serial)
            {
                record = r;
                return true;
            }
        }
        record = default;
        return false;
    }

    /// <summary>의뢰를 내림 (끝냈으면 끝낸 수가 오름)</summary>
    /// <returns>걸려 있던 의뢰였으면 true</returns>
    public bool RemoveLifeRequest(int serial, bool done)
    {
        int index = _lifeRequests.FindIndex(r => r.Serial == serial);
        if (index < 0)
            return false;
        _lifeRequests.RemoveAt(index);
        if (done)
            LifeRequestsDone++;
        OnChanged?.Invoke();
        return true;
    }

    #endregion

    /// <summary>
    /// 회차가 붙은 작업(생활 의뢰 "틀ID#회차")의 완료 기록을 지운다 — 같은 작업을 반복하므로 끝낸 기록이 쌓이지 않게.
    /// 한 번만 하는 작업의 기록은 지우지 않는다
    /// </summary>
    public void ForgetCompletedTask(string taskId)
    {
        if (SettlementConfig.IsTaskInstance(taskId) && _completedTasks.Remove(taskId))
            OnChanged?.Invoke();
    }

    #region 세이브

    private void LoadP3(SettlementSaveData saved)
    {
        _projects.Clear();
        _rewardKeys.Clear();
        _houses.Clear();
        _lifeRequests.Clear();
        LifeRequestSerial = Math.Max(0, saved.lifeRequestSerial);
        LifeRequestsDone = Math.Max(0, saved.lifeRequestsDone);

        if (saved.projects != null)
        {
            foreach (var p in saved.projects)
            {
                // 같은 사업이 두 번이면 앞의 것만
                if (p == null || string.IsNullOrEmpty(p.projectId) || _projects.ContainsKey(p.projectId))
                    continue;
                var progress = new ProjectProgress(p.projectId, Math.Max(0, p.cycle))
                {
                    Gold = Math.Max(0, p.gold),
                    Choice = string.IsNullOrEmpty(p.choice) ? null : p.choice,
                    Completed = p.completed,
                };
                if (p.items != null)
                {
                    foreach (var stack in p.items)
                    {
                        if (stack != null && !string.IsNullOrEmpty(stack.itemId))
                            progress.AddItem(stack.itemId, Math.Max(0, stack.quantity));
                    }
                }
                _projects[p.projectId] = progress;
            }
        }
        if (saved.rewardKeys != null)
        {
            foreach (var key in saved.rewardKeys)
            {
                if (!string.IsNullOrEmpty(key))
                    _rewardKeys.Add(key);
            }
        }
        if (saved.houses != null)
        {
            foreach (var h in saved.houses)
            {
                // 같은 인스턴스·같은 자리가 두 번이거나, 한 해달이 두 집에 있으면 앞의 것만
                if (h == null || string.IsNullOrEmpty(h.instanceId) || string.IsNullOrEmpty(h.slotId)
                    || _houses.Exists(x => x.InstanceId == h.instanceId || x.SlotId == h.slotId))
                    continue;
                string resident = string.IsNullOrEmpty(h.residentId) || FindHouseOf(h.residentId) != null ? null : h.residentId;
                _houses.Add(new HouseRecord(h.instanceId, h.definitionId, h.slotId, resident));
            }
        }
        if (saved.lifeRequests != null)
        {
            foreach (var r in saved.lifeRequests)
            {
                if (r == null || string.IsNullOrEmpty(r.templateId) || _lifeRequests.Exists(x => x.Serial == r.serial))
                    continue;
                _lifeRequests.Add(new LifeRequestRecord(r.serial, r.templateId, r.itemId, r.amount));
                // 깨진 세이브: 걸린 의뢰보다 회차가 뒤처지면 맞춤 (같은 회차를 두 번 쓰지 않게)
                LifeRequestSerial = Math.Max(LifeRequestSerial, r.serial + 1);
            }
        }
    }

    private void WriteP3(SettlementSaveData result)
    {
        result.projects = new List<ProjectSaveData>();
        foreach (var progress in _projects.Values)
        {
            var data = new ProjectSaveData
            {
                projectId = progress.ProjectId,
                cycle = progress.Cycle,
                gold = progress.Gold,
                choice = progress.Choice,
                completed = progress.Completed,
                items = new List<ItemStack>(),
            };
            foreach (var pair in progress.Items)
                data.items.Add(new ItemStack(pair.Key, pair.Value));
            data.items.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
            result.projects.Add(data);
        }
        result.projects.Sort((a, b) => string.CompareOrdinal(a.projectId, b.projectId));

        result.rewardKeys = new List<string>(_rewardKeys);
        result.rewardKeys.Sort(StringComparer.Ordinal);

        result.houses = new List<HouseSaveData>();
        foreach (var house in _houses)
        {
            result.houses.Add(new HouseSaveData
            {
                instanceId = house.InstanceId,
                definitionId = house.DefinitionId,
                slotId = house.SlotId,
                residentId = house.ResidentId,
            });
        }

        result.lifeRequests = new List<LifeRequestSaveData>();
        foreach (var r in _lifeRequests)
            result.lifeRequests.Add(new LifeRequestSaveData { serial = r.Serial, templateId = r.TemplateId, itemId = r.ItemId, amount = r.Amount });
        result.lifeRequestSerial = LifeRequestSerial;
        result.lifeRequestsDone = LifeRequestsDone;
    }

    #endregion
}
