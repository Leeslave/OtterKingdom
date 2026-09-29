using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 모델(QuestLog)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 게임 알림(가방·판매·재화·도감)을 받아 진행 수치를 올리고, 보상 받기를 조율한다 (QuestLog는 돈을 모름).
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// 매니저들(-100)과 도감(-90)이 준비된 뒤, 게임 쪽(GameManager)이 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("데이터")]
    [SerializeField] private QuestDatabase _database;

    public QuestLog Log { get; private set; }
    public QuestDatabase Database => _database;

    /// <summary>진행 수치나 보상 수령 상태가 바뀌었을 때 (세이브를 불러온 뒤 포함). 화면·뱃지 갱신용</summary>
    public event Action OnChanged;

    /// <summary>보상을 받을 수 있는 퀘스트 수 (네비게이션 바 뱃지)</summary>
    public int ClaimableCount => Log.Count(_database.Quests, QuestStatus.Claimable);

    private Inventory _inventory;
    private InventoryManager _inventoryManager;
    private CurrencyManager _currencyManager;
    private Collection _collection;

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Log = new QuestLog();
        Log.OnChanged += _ => OnChanged?.Invoke();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        _inventoryManager = InventoryManager.Instance;
        if (_inventoryManager != null)
        {
            _inventory = _inventoryManager.Inventory;
            _inventory.OnItemChanged += HandleItemChanged;
            _inventoryManager.OnItemSold += HandleItemSold;
        }
        else
        {
            Debug.LogWarning("[QuestManager] InventoryManager가 없어 수확·낚시·판매 퀘스트를 셀 수 없습니다.");
        }

        _currencyManager = CurrencyManager.Instance;
        if (_currencyManager != null)
            _currencyManager.OnTransaction += HandleTransaction;
        else
            Debug.LogWarning("[QuestManager] CurrencyManager가 없어 업그레이드 퀘스트를 세거나 보상을 줄 수 없습니다.");

        if (CollectionManager.Instance != null)
        {
            _collection = CollectionManager.Instance.Collection;
            _collection.OnStateChanged += HandleCollectionChanged;
        }
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.OnItemChanged -= HandleItemChanged;
        if (_inventoryManager != null)
            _inventoryManager.OnItemSold -= HandleItemSold;
        if (_currencyManager != null)
            _currencyManager.OnTransaction -= HandleTransaction;
        if (_collection != null)
            _collection.OnStateChanged -= HandleCollectionChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #region 진행

    private void HandleItemChanged(ItemChangedEvent e)
    {
        foreach (var quest in _database.Quests)
            Log.AddProgress(quest, QuestProgressRules.From(quest, e));
    }

    private void HandleItemSold(ItemSoldEvent e)
    {
        foreach (var quest in _database.Quests)
            Log.AddProgress(quest, QuestProgressRules.From(quest, e));
    }

    private void HandleTransaction(CurrencyChange change)
    {
        foreach (var quest in _database.Quests)
            Log.AddProgress(quest, QuestProgressRules.From(quest, change));
    }

    private void HandleCollectionChanged(CollectionEntry entry, CollectionState state)
    {
        foreach (var quest in _database.Quests)
            Log.AddProgress(quest, QuestProgressRules.From(quest, entry, state));
    }

    #endregion

    #region 보상

    /// <summary>보상 받기. 목표를 달성했고 아직 받지 않았을 때만 보상 재화를 준다.</summary>
    /// <returns>받았으면 true</returns>
    public bool TryClaim(QuestDefinition quest)
    {
        // 받음 표시부터: 같은 보상을 두 번 받는 일을 막는다
        if (!Log.TryClaim(quest))
            return false;

        if (quest.RewardCurrency != null && quest.RewardAmount > 0)
            _currencyManager.ProcessTransaction(new CurrencyTransaction(quest.RewardCurrency, quest.RewardAmount, TransactionSource.QuestReward));

        return true;
    }

    /// <summary>받을 수 있는 보상을 모두 받는다 (모두 받기)</summary>
    /// <returns>받은 퀘스트 수</returns>
    public int ClaimAll()
    {
        // 받으면서 상태가 바뀌므로 목록을 먼저 복사
        var targets = new List<QuestDefinition>(_database.Quests);

        int count = 0;
        foreach (var quest in targets)
        {
            if (TryClaim(quest))
                count++;
        }
        return count;
    }

    #endregion

    #region 세이브

    /// <summary>게임 시작 시 한 번</summary>
    public void LoadFromSave(IEnumerable<QuestSaveEntry> saved)
    {
        QuestSaveConverter.Read(saved, Log);
        OnChanged?.Invoke();
    }

    /// <summary>퀘스트 기록을 세이브 목록에 쓴다 (기존 내용은 지움)</summary>
    public void WriteToSave(List<QuestSaveEntry> result)
    {
        QuestSaveConverter.Write(Log, result);
    }

    #endregion
}
