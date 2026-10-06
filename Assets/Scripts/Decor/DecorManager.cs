using System;
using System.Collections.Generic;
using UnityEngine;

public enum DecorUnlockResult
{
    Unlocked,
    AlreadyUnlocked,
    RequirementNotMet, // 레벨이 모자라거나 재화가 부족함
    NoRequirements,    // 조건이 없는 잠긴 구역 (게임 쪽이 직접 열어야 함)
}

/// <summary>
/// 꾸미기 모델의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 장소마다(광장, 밭, 낚시터) 격자(DecorLayout)를 하나씩 가지고, 가방 개수(보관함에 남은 수)와 구역 해금(레벨·재화)을 조율한다.
/// 보관함은 모든 장소가 함께 쓴다: 밭에 놓은 공은 광장 보관함에서도 빠진다.
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// 매니저들(-100)이 준비된 뒤, 게임 쪽(GameManager)이 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public class DecorManager : MonoBehaviour
{
    public static DecorManager Instance { get; private set; }

    [Header("데이터")]
    [Tooltip("놓을 수 있는 물건 전부 (모든 장소 공통)")]
    [SerializeField] private DecorCatalog _catalog;
    [Tooltip("장소별 격자 (광장, 밭, 낚시터)")]
    [SerializeField] private List<DecorBoardDefinition> _boards = new List<DecorBoardDefinition>();

    public DecorCatalog Catalog => _catalog;
    public IReadOnlyList<DecorBoardDefinition> Boards => _boards;

    /// <summary>보관함 개수가 바뀌었을 때 (가방 개수가 바뀌거나 어느 장소에서든 놓고 치울 때)</summary>
    public event Action OnStorageChanged;

    /// <summary>어느 장소에서든 물건을 새로 놓았을 때 (옮기기 제외, 퀘스트용)</summary>
    public event Action<PlacedDecor> OnDecorPlaced;

    /// <summary>꾸미기 모드를 끝냈을 때 ([완료]). 게임 쪽이 여기서 바로 저장하면 배치가 30초 자동 저장을 기다리지 않는다</summary>
    public event Action OnEditFinished;

    /// <summary>바로 저장해 달라는 부탁 (꾸미기를 끝냄). GameManager가 듣는다</summary>
    public static event Action SaveRequested;

    private readonly Dictionary<DecorBoardDefinition, DecorLayout> _layouts = new Dictionary<DecorBoardDefinition, DecorLayout>();

    // 세이브에 있었지만 지금 없는 격자. 다음 저장 때 그대로 다시 써서 잃어버리지 않게 보관
    private readonly List<DecorBoardSaveData> _unknownSavedBoards = new List<DecorBoardSaveData>();

    private Inventory _inventory;

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        foreach (var board in _boards)
        {
            if (board == null || _layouts.ContainsKey(board))
                continue;

            var layout = CreateLayout(board);
            layout.OnPlaced += placed =>
            {
                OnStorageChanged?.Invoke();
                // 건물은 장난감 놓기 퀘스트에 세지 않음
                if (!placed.Decor.IsBuilding)
                    OnDecorPlaced?.Invoke(placed);
            };
            layout.OnRemoved += _ => OnStorageChanged?.Invoke();
            _layouts.Add(board, layout);
        }
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[DecorManager] InventoryManager가 없어 보관함 개수를 알 수 없습니다.");
            return;
        }

        _inventory = InventoryManager.Instance.Inventory;
        _inventory.OnItemChanged += HandleItemChanged;
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.OnItemChanged -= HandleItemChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>격자 정의로 빈 격자를 만든다 (구역 등록, 기본 구역 열기)</summary>
    public static DecorLayout CreateLayout(DecorBoardDefinition board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        var layout = new DecorLayout(board.Size);
        foreach (var region in board.Regions)
        {
            if (region != null && !string.IsNullOrEmpty(region.RegionId))
                layout.AddRegion(region.RegionId, region.Areas, region.UnlockedByDefault);
        }
        return layout;
    }

    #region 격자 찾기

    public DecorLayout GetLayout(DecorBoardDefinition board)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));
        if (!_layouts.TryGetValue(board, out var layout))
            throw new ArgumentException($"DecorManager에 등록되지 않은 격자입니다: {board.name}", nameof(board));
        return layout;
    }

    /// <returns>이 장소의 격자. 꾸미기를 할 수 없는 장소면 null</returns>
    public DecorBoardDefinition FindBoard(ZoneDefinition zone)
    {
        if (zone == null)
            return null;

        foreach (var board in _boards)
        {
            if (board != null && board.Zone == zone)
                return board;
        }
        return null;
    }

    #endregion

    #region 보관함

    /// <summary>보관함에 남은 개수 = 가방에 있는 개수 − 모든 장소에 놓인 개수</summary>
    public int StorageCount(DecorDefinition decor)
    {
        if (decor == null)
            throw new ArgumentNullException(nameof(decor));

        int owned = _inventory != null ? _inventory.GetCount(decor.Item) : 0;
        return DecorStorage.Available(owned, _layouts.Values, decor);
    }

    /// <summary>보관함에서 꺼내 이 장소에 놓기. 남은 개수가 없으면 NotOwned</summary>
    public DecorPlacementResult TryPlaceFromStorage(DecorBoardDefinition board, DecorDefinition decor, Vector2Int origin,
        DecorRotation rotation, out PlacedDecor placed)
    {
        placed = null;
        var layout = GetLayout(board);
        if (StorageCount(decor) <= 0)
            return DecorPlacementResult.NotOwned;

        return layout.TryPlace(decor, origin, rotation, out placed);
    }

    /// <summary>놓인 물건을 보관함으로 되돌린다 (건물은 치울 수 없음)</summary>
    public bool ReturnToStorage(DecorBoardDefinition board, int instanceId)
    {
        var layout = GetLayout(board);
        if (layout.TryGet(instanceId, out var placed) && placed.Decor.IsBuilding)
            return false;
        return layout.Remove(instanceId);
    }

    /// <summary>건물을 놓는다 (보관함과 상관없음. 비용·공사는 SettlementManager가 함께 처리)</summary>
    public DecorPlacementResult TryPlaceBuilding(DecorBoardDefinition board, BuildingDefinition building, Vector2Int origin, out PlacedDecor placed)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        return GetLayout(board).TryPlace(building, origin, DecorRotation.R0, out placed);
    }

    /// <returns>이름이 이 ID인 격자 (없으면 null)</returns>
    public DecorBoardDefinition FindBoard(string boardId)
    {
        foreach (var board in _boards)
        {
            if (board != null && board.BoardId == boardId)
                return board;
        }
        return null;
    }

    // 가방에서 빠져(판매·삭제 등) 가진 수보다 많이 놓여 있으면 넘치는 만큼 치운다
    private void HandleItemChanged(ItemChangedEvent e)
    {
        var decor = _catalog.FindByItem(e.Item);
        if (decor == null)
            return;

        DecorStorage.RemoveExcess(e.NewCount, _layouts.Values, decor);
        OnStorageChanged?.Invoke();
    }

    #endregion

    #region 구역 해금

    public DecorRegionDefinition FindRegion(DecorBoardDefinition board, string regionId)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));

        foreach (var region in board.Regions)
        {
            if (region != null && region.RegionId == regionId)
                return region;
        }
        return null;
    }

    /// <summary>이 구역의 조건을 모두 채울 수 있는지 (비용은 내지 않음)</summary>
    public bool CanUnlock(DecorBoardDefinition board, DecorRegionDefinition region)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));

        if (GetLayout(board).IsRegionUnlocked(region.RegionId) || region.Requirements.Count == 0)
            return false;

        return UnlockRules.AllMet(region.Requirements, CreateUnlockContext());
    }

    /// <summary>조건을 모두 확인한 뒤 채우고(비용 지불) 구역을 연다. 하나라도 모자라면 아무것도 내지 않는다</summary>
    public DecorUnlockResult TryUnlock(DecorBoardDefinition board, DecorRegionDefinition region)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));

        var layout = GetLayout(board);
        if (layout.IsRegionUnlocked(region.RegionId))
            return DecorUnlockResult.AlreadyUnlocked;
        if (region.Requirements.Count == 0)
            return DecorUnlockResult.NoRequirements;

        if (!UnlockRules.TryFulfillAll(region.Requirements, CreateUnlockContext()))
            return DecorUnlockResult.RequirementNotMet;

        layout.UnlockRegion(region.RegionId);
        return DecorUnlockResult.Unlocked;
    }

    /// <summary>조건 없이 구역을 연다 (퀘스트 보상, 이벤트 등 게임 쪽 연결용)</summary>
    public bool UnlockDirectly(DecorBoardDefinition board, string regionId) => GetLayout(board).UnlockRegion(regionId);

    private static UnlockContext CreateUnlockContext()
    {
        int level = ProfileManager.Instance != null ? ProfileManager.Instance.Profile.Level : 1;
        return new UnlockContext(level, CurrencyManager.Instance);
    }

    #endregion

    /// <summary>꾸미기 모드가 끝났음을 알린다 (DecorModePresenter가 호출)</summary>
    public void NotifyEditFinished()
    {
        OnEditFinished?.Invoke();
        SaveRequested?.Invoke();
    }

    #region 세이브

    /// <summary>게임 시작 시 한 번, 가방을 불러온 뒤에 호출</summary>
    /// <returns>자리가 맞지 않아 보관함으로 돌아간 물건 수</returns>
    public int LoadFromSave(DecorSaveData saved)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        _unknownSavedBoards.Clear();
        int skipped = 0;
        if (saved.boards != null)
        {
            foreach (var boardSave in saved.boards)
            {
                if (boardSave == null || string.IsNullOrEmpty(boardSave.boardId))
                    continue;

                var board = _boards.Find(b => b != null && b.BoardId == boardSave.boardId);
                if (board == null)
                {
                    _unknownSavedBoards.Add(boardSave);
                    continue;
                }
                skipped += DecorSaveConverter.Read(boardSave, _layouts[board], _catalog, board.SaveOrigin);
            }
        }

        OnStorageChanged?.Invoke();
        return skipped;
    }

    /// <summary>모든 장소의 꾸미기 상태를 세이브에 쓴다 (기존 내용은 지움)</summary>
    public void WriteToSave(DecorSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        result.boards.Clear();
        foreach (var pair in _layouts)
        {
            var boardSave = new DecorBoardSaveData { boardId = pair.Key.BoardId };
            DecorSaveConverter.Write(pair.Value, boardSave, pair.Key.SaveOrigin);
            result.boards.Add(boardSave);
        }
        result.boards.AddRange(_unknownSavedBoards);
        result.boards.Sort((a, b) => string.CompareOrdinal(a.boardId, b.boardId));
    }

    #endregion
}
