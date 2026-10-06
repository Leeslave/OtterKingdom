using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 모델(QuestLog)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 게임 알림(가방·판매·재화·도감·꾸미기)을 받아 진행 수치를 올리고, 보상 받기를 조율한다 (QuestLog는 돈을 모름).
/// - 지금 열린 퀘스트(레벨·앞 단계 충족)만 진행이 쌓인다 → 성장 퀘스트가 체인처럼 이어짐
/// - 보상: 재화 + 경험치(왕국 레벨, ProfileManager)
/// - 일일 퀘스트: 매일 새벽 4시(기기 시간)에 진행·수령 초기화
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// 매니저들(-100)과 도감(-90)이 준비된 뒤, 게임 쪽(GameManager)이 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public class QuestManager : MonoBehaviour
{
    // 일일 퀘스트가 바뀌는 시각 (새벽 4시: 자정 직후 플레이 중에 초기화되지 않게)
    private const int DailyResetHour = 4;

    public static QuestManager Instance { get; private set; }

    [Header("데이터")]
    [SerializeField] private QuestDatabase _database;

    public QuestLog Log { get; private set; }
    public QuestDatabase Database => _database;

    /// <summary>진행·수령·열림(레벨업)·일일 초기화로 목록이 바뀌었을 때 (세이브를 불러온 뒤 포함). 화면·뱃지 갱신용</summary>
    public event Action OnChanged;

    /// <summary>보상을 받을 수 있는 퀘스트 수 (네비게이션 바 뱃지)</summary>
    public int ClaimableCount
    {
        get
        {
            int count = 0;
            foreach (var quest in _database.Quests)
            {
                if (IsAvailable(quest) && Log.GetStatus(quest) == QuestStatus.Claimable)
                    count++;
            }
            return count;
        }
    }

    /// <summary>지금 왕국 레벨 (ProfileManager가 없으면 1)</summary>
    public int PlayerLevel => ProfileManager.Instance != null ? ProfileManager.Instance.Level : 1;

    private Inventory _inventory;
    private InventoryManager _inventoryManager;
    private CurrencyManager _currencyManager;
    private Collection _collection;
    private DecorManager _decorManager;
    private ProfileManager _profileManager;
    private SettlementManager _settlementManager;

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

        FairyShopPresenter.Purchased += HandleShopPurchased;
    }

    // 꾸미기·레벨 매니저는 같은 실행 순서(-80)라 OnEnable 시점에 아직 없을 수 있어 Start에서 연결
    private void Start()
    {
        if (Instance != this)
            return;

        _decorManager = DecorManager.Instance;
        if (_decorManager != null)
            _decorManager.OnDecorPlaced += HandleDecorPlaced;

        _profileManager = ProfileManager.Instance;
        if (_profileManager != null)
            _profileManager.OnLevelUp += HandleLevelUp;

        // 밭·낚시터가 열리면 그 퀘스트가 나타남. 만난 해달·다 지은 건물은 정착 기록으로 다시 셈
        _settlementManager = SettlementManager.Instance;
        if (_settlementManager != null)
        {
            _settlementManager.OnDevelopmentUnlocked += HandleDevelopmentUnlocked;
            _settlementManager.OnChanged += RefreshRecordGoals;
        }

        CheckDailyReset();
        RefreshRecordGoals();
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
        FairyShopPresenter.Purchased -= HandleShopPurchased;
        if (_decorManager != null)
            _decorManager.OnDecorPlaced -= HandleDecorPlaced;
        if (_profileManager != null)
            _profileManager.OnLevelUp -= HandleLevelUp;
        if (_settlementManager != null)
        {
            _settlementManager.OnDevelopmentUnlocked -= HandleDevelopmentUnlocked;
            _settlementManager.OnChanged -= RefreshRecordGoals;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 켜 둔 채로 새벽 4시를 넘겨도 바뀌도록 (비교 한 번이라 매 프레임 부담 없음)
    private void Update() => CheckDailyReset();

    /// <summary>지금 목록에 나타나고 진행이 쌓이는지 (레벨·앞 단계 충족)</summary>
    public bool IsAvailable(QuestDefinition quest) =>
        QuestProgressRules.IsAvailable(quest, PlayerLevel, Log) && SettlementQuestGate.IsReachable(quest);

    /// <summary>받은 퀘스트 중 목록 아래 완료 칸에 남길지 (오늘 받은 일일, 이번 레벨에 받은 성장)</summary>
    public bool ShowsAsCompleted(QuestDefinition quest) => QuestProgressRules.ShowsAsCompleted(quest, PlayerLevel, Log);

    /// <summary>이 퀘스트를 지금 받으면 얻는 경험치</summary>
    public int ExpFor(QuestDefinition quest)
    {
        var curve = _profileManager != null ? _profileManager.LevelTable : null;
        return quest.ExpFor(PlayerLevel, curve);
    }

    #region 진행

    private void HandleItemChanged(ItemChangedEvent e)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.From(quest, e));
    }

    private void HandleItemSold(ItemSoldEvent e)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.From(quest, e));
    }

    private void HandleTransaction(CurrencyChange change)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.From(quest, change));
    }

    private void HandleCollectionChanged(CollectionEntry entry, CollectionState state)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.From(quest, entry, state));
    }

    private void HandleDecorPlaced(PlacedDecor placed)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.From(quest, placed));
    }

    private void HandleShopPurchased(ShopProduct product, int quantity)
    {
        foreach (var quest in _database.Quests)
            AddProgress(quest, QuestProgressRules.FromShopPurchase(quest, quantity));
    }

    // 레벨이 올라 새 퀘스트가 열림 → 앞서 만난 해달·지은 건물을 넣고 목록 갱신
    private void HandleLevelUp(int level)
    {
        RefreshRecordGoals();
        OnChanged?.Invoke();
    }

    private void HandleDevelopmentUnlocked(string developmentId) => OnChanged?.Invoke();

    private void AddProgress(QuestDefinition quest, int amount)
    {
        if (amount > 0 && IsAvailable(quest))
            Log.AddProgress(quest, amount);
    }

    private readonly List<ConstructionDefinition> _buildings = new List<ConstructionDefinition>();

    /// <summary>
    /// 기록으로 세는 퀘스트(새 해달 만나기, 건설 완료)를 정착 기록에서 다시 센다. 퀘스트가 열리기 전에 만난 해달·지은 건물도
    /// 들어가고(소급), 같은 기록을 두 번 세지 않는다 (더하지 않고 맞춤). 받은 보상은 그대로라 다시 받을 수 없다.
    /// 불러오기·레벨업·보상 받기·정착 변화 때 부른다
    /// </summary>
    public void RefreshRecordGoals()
    {
        var settlement = _settlementManager != null ? _settlementManager : SettlementManager.Instance;
        if (settlement == null || !settlement.IsLoaded || Log == null)
            return;

        _buildings.Clear();
        bool buildingsReady = false;
        foreach (var quest in _database.Quests)
        {
            if (quest == null || !quest.CountsFromRecords || !IsAvailable(quest))
                continue;
            int value;
            if (quest.GoalType == QuestGoalType.MeetOtter)
            {
                value = settlement.MetOtterCount;
            }
            else
            {
                if (!buildingsReady)
                {
                    settlement.CollectCompletedBuildings(_buildings);
                    buildingsReady = true;
                }
                value = QuestProgressRules.CountBuildings(quest, _buildings);
            }
            Log.SetProgressAtLeast(quest, value);
        }
    }

    #endregion

    #region 일일 초기화

    /// <summary>기기 시간 기준 오늘 (새벽 4시에 날이 바뀜). 날짜 번호 = 0001-01-01부터 지난 날 수</summary>
    public static int TodayNumber(DateTime now) => (int)(now.AddHours(-DailyResetHour).Date.Ticks / TimeSpan.TicksPerDay);

    private void CheckDailyReset()
    {
        int today = TodayNumber(GameClock.Now);
        if (Log.DailyDay == today)
            return;

        var daily = new List<QuestDefinition>();
        foreach (var quest in _database.Quests)
        {
            if (quest.Kind == QuestKind.Daily)
                daily.Add(quest);
        }
        Log.Reset(daily, today);
    }

    #endregion

    #region 보상

    /// <summary>보상 받기. 열려 있고 목표를 달성했고 아직 받지 않았을 때만 재화와 경험치를 준다.</summary>
    /// <returns>받았으면 true</returns>
    public bool TryClaim(QuestDefinition quest)
    {
        if (!IsAvailable(quest))
            return false;

        // 경험치와 받은 레벨은 받기 전 레벨 기준 (받으면서 레벨이 오르면 달라지므로 먼저 계산)
        int level = PlayerLevel;
        int exp = ExpFor(quest);

        // 받음 표시부터: 같은 보상을 두 번 받는 일을 막는다
        if (!Log.TryClaim(quest, level))
            return false;

        if (quest.RewardCurrency != null && quest.RewardAmount > 0)
            _currencyManager.ProcessTransaction(new CurrencyTransaction(quest.RewardCurrency, quest.RewardAmount, TransactionSource.QuestReward));

        if (_profileManager != null && exp > 0)
            _profileManager.AddExp(exp);

        // 앞 단계를 받아 열린 다음 단계가 이미 이룬 기록을 바로 반영
        RefreshRecordGoals();
        return true;
    }

    /// <summary>받을 수 있는 보상을 모두 받는다 (모두 받기)</summary>
    /// <returns>받은 퀘스트 수</returns>
    public int ClaimAll()
    {
        // 받으면서 상태가 바뀌고 새 퀘스트가 열리므로 목록을 먼저 복사 (새로 열린 것은 다음 번에)
        var targets = new List<QuestDefinition>(_database.Quests);

        int count = 0;
        foreach (var quest in targets)
        {
            if (Log.GetStatus(quest) == QuestStatus.Claimable && TryClaim(quest))
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
        CheckDailyReset();
        RefreshRecordGoals();
        OnChanged?.Invoke();
    }

    /// <summary>퀘스트 기록을 세이브 목록에 쓴다 (기존 내용은 지움)</summary>
    public void WriteToSave(List<QuestSaveEntry> result)
    {
        QuestSaveConverter.Write(Log, result);
    }

    #endregion
}
