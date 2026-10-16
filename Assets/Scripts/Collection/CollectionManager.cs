using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도감 모델(Collection)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// - 채소·어류: 수확·낚시로 처음 얻으면 자동 획득
/// - 레벨 해금 항목(농부 해달 2, 낚시꾼 해달 10): 그 레벨에 도달하면 자동 등록
/// - 그 밖의 해달: 게임 쪽이 Register / MarkVisited를 호출 (항목 ID = 해달 speciesId)
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// InventoryManager(-100)가 가방을 만든 뒤, 게임 쪽(GameManager)이 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-90)]
public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance { get; private set; }

    [Header("데이터")]
    [SerializeField] private CollectionDatabase _database;

    public Collection Collection { get; private set; }
    public CollectionDatabase Database => _database;

    private Inventory _inventory;
    private ProfileManager _profile;

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Collection = new Collection();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        // 레벨 해금: 경험치가 바뀔 때마다 확인 (세이브 복원 때도 알림이 오므로 불러오는 순서와 무관)
        _profile = ProfileManager.Instance;
        if (_profile != null)
        {
            _profile.OnExpChanged += HandleExpChanged;
            HandleExpChanged();
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[CollectionManager] InventoryManager가 없어 채소·어류 자동 획득을 할 수 없습니다.");
            return;
        }

        _inventory = InventoryManager.Instance.Inventory;
        _inventory.OnItemChanged += HandleItemChanged;

        // 이미 가방에 있는 것 (도감이 생기기 전 세이브 포함)
        CollectionAutoCollector.CollectOwned(Collection, _database, _inventory);
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.OnItemChanged -= HandleItemChanged;
        if (_profile != null)
            _profile.OnExpChanged -= HandleExpChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleItemChanged(ItemChangedEvent e)
    {
        CollectionAutoCollector.Handle(Collection, _database, e);
    }

    private void HandleExpChanged()
    {
        CollectionLevelUnlocker.Unlock(Collection, _database, _profile.Level);
    }

    #region 게임 쪽 연결 (해달)

    /// <summary>등록 완료 (온라인 중 해달을 눌러 등록, 가챠 등)</summary>
    /// <returns>새로 등록됐으면 true. 이미 등록했거나 없는 ID면 false</returns>
    public bool Register(string entryId)
    {
        return TryGetEntry(entryId, out var entry) && Collection.Collect(entry);
    }

    /// <summary>방문 흔적 (오프라인 동안 왔다 감). 이미 방문/등록했거나 방문 흔적을 쓰지 않는 탭이면 무시</summary>
    public bool MarkVisited(string entryId)
    {
        return TryGetEntry(entryId, out var entry) && Collection.MarkVisited(entry);
    }

    private bool TryGetEntry(string entryId, out CollectionEntry entry)
    {
        if (_database.TryGet(entryId, out entry))
            return true;

        Debug.LogWarning($"[CollectionManager] 도감에 없는 항목 ID: {entryId}");
        return false;
    }

    #endregion

    #region 세이브

    /// <summary>게임 시작 시 한 번. 가방을 불러온 뒤에 호출하면 가방에 있는 것도 획득으로 맞춘다.</summary>
    public void LoadFromSave(IEnumerable<CollectionSaveEntry> saved)
    {
        CollectionSaveConverter.Read(saved, Collection);

        if (_inventory != null)
            CollectionAutoCollector.CollectOwned(Collection, _database, _inventory);
        if (_profile != null)
            CollectionLevelUnlocker.Unlock(Collection, _database, _profile.Level);

        Collection.NotifyLoaded();
    }

    /// <summary>도감 상태를 세이브 목록에 쓴다 (기존 내용은 지움)</summary>
    public void WriteToSave(List<CollectionSaveEntry> result)
    {
        CollectionSaveConverter.Write(Collection, result);
    }

    #endregion
}
