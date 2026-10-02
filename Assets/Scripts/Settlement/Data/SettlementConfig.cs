using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정착 진행(P0: 빈 광장 → 첫 집 → 건설 해달 → 농경지 개간 → 밭 해금)에 쓰는 데이터 묶음.
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
    public IReadOnlyList<ItemAmount> StartingItems => _startingItems;
    public int StartingGold => _startingGold;
    public IReadOnlyList<string> LegacyDevelopments => _legacyDevelopments;
    public ItemDefinition GatherItem => _gatherItem;
    public int GatherAmount => _gatherAmount;
    public float GatherCooldownSeconds => _gatherCooldownSeconds;
    public int StageCount => _stageNames.Count;

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
