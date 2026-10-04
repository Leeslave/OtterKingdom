using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정착 진행(P0: 빈 광장 → 첫 집 → 건설 해달 → 농경지 개간 → 밭 해금, P1: 개간 지역 · 주민 작업 · 전문 해달 배치,
/// P2: 게시판 성장 → 관리 해달 → 주민 부탁 · 공동 작업 → 접수소 · 큰 부탁 → 마을회관)에 쓰는 데이터 묶음.
/// </summary>
[CreateAssetMenu(fileName = "SettlementConfig", menuName = "Game Data/Settlement/Config")]
public class SettlementConfig : ScriptableObject
{
    [Header("재화")]
    [Tooltip("건설 비용의 골드")]
    [SerializeField] private Currency _goldCurrency;

    [Header("단계")]
    [Tooltip("왕국 단계 이름 (0부터). 상단바 아래 칩에 보임")]
    [SerializeField] private List<string> _stageNames = new List<string>();

    [Header("해달")]
    [Tooltip("새 게임에 처음 찾아오는 해달")]
    [SerializeField] private SettlementOtterDefinition _firstOtter;

    [SerializeField] private List<SettlementOtterDefinition> _otters = new List<SettlementOtterDefinition>();

    [Header("게시판")]
    [SerializeField] private List<BoardRequestDefinition> _requests = new List<BoardRequestDefinition>();

    [SerializeField] private List<GuestbookEntryDefinition> _guestbookEntries = new List<GuestbookEntryDefinition>();

    [Header("개간 지역 · 주민 작업")]
    [Tooltip("발견 → 직접 개척 → 주민 정비 → 운영으로 가는 지역")]
    [SerializeField] private List<DevelopableRegionDefinition> _regions = new List<DevelopableRegionDefinition>();

    [Tooltip("주민 해달을 보내는 작업 전부 (지역의 후속 정비 포함. 세이브의 작업 ID를 찾을 때 씀)")]
    [SerializeField] private List<SettlementTaskDefinition> _tasks = new List<SettlementTaskDefinition>();

    [Header("게시판 성장 · 관리 해달 · 마을회관 (P2)")]
    [Tooltip("게시판을 보강하면 열리는 발전 (board_upgraded)")]
    [SerializeField] private string _boardUpgradeDevelopment;

    [Tooltip("게시판을 맡은 해달이 생기면 열리는 발전 (receptionist_assigned). 이때부터 주민 부탁·완료 기록을 나눠 보여 줌")]
    [SerializeField] private string _boardManagedDevelopment;

    [Tooltip("접수소가 생기면 열리는 발전 (guild_office_built). 이때부터 큰 부탁 묶음을 보여 줌")]
    [SerializeField] private string _guildDevelopment;

    [Tooltip("마을회관이 생기면 열리는 발전 (town_hall_built). 이때부터 발전 현황 화면을 볼 수 있음")]
    [SerializeField] private string _townHallDevelopment;

    [Tooltip("게시판을 맡은 해달이 생긴 뒤 한 번에 보이는 주민 부탁 수 (진행 중인 부탁은 이 수와 상관없이 늘 보임)")]
    [Min(0)]
    [SerializeField] private int _residentRequestSlots = 2;

    [Tooltip("관리 역할 (게시판 관리 등)")]
    [SerializeField] private List<ManagementRoleDefinition> _roles = new List<ManagementRoleDefinition>();

    [Tooltip("큰 부탁 묶음 (마을 회의소 마련하기 등)")]
    [SerializeField] private List<MilestoneGroupDefinition> _milestoneGroups = new List<MilestoneGroupDefinition>();

    [Header("옛 세이브")]
    [Tooltip("정착 진행 전 세이브에 부탁과 별도로 열어 줄 발전 (예: 아직 부탁이 없는 낚시터 fishing_dock)")]
    [SerializeField] private List<string> _legacyDevelopments = new List<string>();

    [Header("새 게임")]
    [Tooltip("새 게임 시작 시 가방에 넣어 줄 재료 (첫 집 비용)")]
    [SerializeField] private List<ItemAmount> _startingItems = new List<ItemAmount>();

    [Tooltip("새 게임 시작 시 주는 골드 (시안 1: 100)")]
    [SerializeField] private int _startingGold;

    [Header("나뭇가지 줍기")]
    [Tooltip("광장 나뭇가지를 탭하면 얻는 아이템 (목재)")]
    [SerializeField] private ItemDefinition _gatherItem;

    [Tooltip("한 번에 얻는 개수")]
    [SerializeField] private int _gatherAmount = 2;

    [Tooltip("주운 나뭇가지가 다시 생기기까지 (초)")]
    [SerializeField] private float _gatherCooldownSeconds = 60f;

    public Currency GoldCurrency => _goldCurrency;
    public SettlementOtterDefinition FirstOtter => _firstOtter;
    public IReadOnlyList<SettlementOtterDefinition> Otters => _otters;
    public IReadOnlyList<BoardRequestDefinition> Requests => _requests;
    public IReadOnlyList<GuestbookEntryDefinition> GuestbookEntries => _guestbookEntries;
    public IReadOnlyList<DevelopableRegionDefinition> Regions => _regions;
    public IReadOnlyList<SettlementTaskDefinition> Tasks => _tasks;
    public IReadOnlyList<ItemAmount> StartingItems => _startingItems;
    public int StartingGold => _startingGold;
    public IReadOnlyList<string> LegacyDevelopments => _legacyDevelopments;
    public ItemDefinition GatherItem => _gatherItem;
    public int GatherAmount => _gatherAmount;
    public float GatherCooldownSeconds => _gatherCooldownSeconds;
    public int StageCount => _stageNames.Count;
    public string BoardUpgradeDevelopment => _boardUpgradeDevelopment;
    public string BoardManagedDevelopment => _boardManagedDevelopment;
    public string GuildDevelopment => _guildDevelopment;
    public string TownHallDevelopment => _townHallDevelopment;
    public int ResidentRequestSlots => Mathf.Max(0, _residentRequestSlots);
    public IReadOnlyList<ManagementRoleDefinition> Roles => _roles;
    public IReadOnlyList<MilestoneGroupDefinition> MilestoneGroups => _milestoneGroups;

    /// <summary>이 해달이 맡는 관리 역할 (없으면 null)</summary>
    public ManagementRoleDefinition FindRoleFor(SettlementOtterDefinition otter)
    {
        if (otter == null)
            return null;
        foreach (var role in _roles)
        {
            if (role != null && role.Otter == otter)
                return role;
        }
        return null;
    }

    public ManagementRoleDefinition FindRole(string roleId)
    {
        foreach (var role in _roles)
        {
            if (role != null && role.RoleId == roleId)
                return role;
        }
        return null;
    }

    public string StageName(int stage)
    {
        if (_stageNames.Count == 0)
            return string.Empty;
        return _stageNames[Mathf.Clamp(stage, 0, _stageNames.Count - 1)];
    }

    public SettlementOtterDefinition FindOtter(string otterId)
    {
        foreach (var otter in _otters)
        {
            if (otter != null && otter.OtterId == otterId)
                return otter;
        }
        return null;
    }

    public BoardRequestDefinition FindRequest(string requestId)
    {
        foreach (var request in _requests)
        {
            if (request != null && request.RequestId == requestId)
                return request;
        }
        return null;
    }

    public GuestbookEntryDefinition FindEntry(string entryId)
    {
        foreach (var entry in _guestbookEntries)
        {
            if (entry != null && entry.EntryId == entryId)
                return entry;
        }
        return null;
    }

    public SettlementTaskDefinition FindTask(string taskId)
    {
        foreach (var task in _tasks)
        {
            if (task != null && task.TaskId == taskId)
                return task;
        }
        return null;
    }

    /// <summary>이 장소의 개간 지역 (없으면 null)</summary>
    public DevelopableRegionDefinition FindRegion(ZoneDefinition zone)
    {
        if (zone == null)
            return null;
        foreach (var region in _regions)
        {
            if (region != null && region.Zone == zone)
                return region;
        }
        return null;
    }

    /// <summary>이 작업으로 끝나는 주민 작업 부탁 (없으면 null)</summary>
    public BoardRequestDefinition FindRequestByTask(SettlementTaskDefinition task)
    {
        if (task == null)
            return null;
        foreach (var request in _requests)
        {
            if (request != null && request.CompletionTask == task)
                return request;
        }
        return null;
    }

    /// <summary>이 작업을 후속 정비로 가진 지역 (없으면 null)</summary>
    public DevelopableRegionDefinition FindRegionOf(SettlementTaskDefinition task)
    {
        foreach (var region in _regions)
        {
            if (region == null)
                continue;
            foreach (var t in region.PreparationTasks)
            {
                if (t == task)
                    return region;
            }
        }
        return null;
    }

    /// <summary>이 지역에서 일할 전문 해달 (없으면 null)</summary>
    public SettlementOtterDefinition FindSpecialist(DevelopableRegionDefinition region)
    {
        if (region == null)
            return null;
        foreach (var otter in _otters)
        {
            if (otter != null && otter.WorkRegion == region)
                return otter;
        }
        return null;
    }

    /// <summary>장소 ID(예: Mine)의 개간 지역 (없으면 null)</summary>
    public DevelopableRegionDefinition FindRegionByZoneId(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId))
            return null;
        foreach (var region in _regions)
        {
            if (region != null && region.Zone != null && region.Zone.ZoneId == zoneId)
                return region;
        }
        return null;
    }

    /// <summary>건설 해달 (없으면 null)</summary>
    public SettlementOtterDefinition FindBuilder()
    {
        foreach (var otter in _otters)
        {
            if (otter != null && otter.IsBuilder)
                return otter;
        }
        return null;
    }
}
