using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inventory(모델)와 뷰들을 연결한다.
/// 화면 상태(선택한 탭/소분류/정렬, 선택한 아이템)는 여기 한 곳에서만 관리하고, 뷰에는 결과만 그리게 한다.
/// </summary>
public class InventoryPresenter : MonoBehaviour
{
    private const string AllChipLabel = "전체";

    [Header("뷰")]
    [SerializeField] private ItemDetailView _detailView;
    [SerializeField] private CategoryTabView _tabPrefab;
    [SerializeField] private Transform _tabParent;
    [Tooltip("소분류 칩 프리팹 (탭과 같은 CategoryTabView, 모양만 다름)")]
    [SerializeField] private CategoryTabView _chipPrefab;
    [Tooltip("소분류 칩이 놓일 부모. 소분류가 없는 탭에서는 통째로 꺼짐")]
    [SerializeField] private Transform _chipParent;
    [SerializeField] private SortDropdownView _sortDropdown;
    [SerializeField] private ItemSlotView _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [Tooltip("탭/정렬을 바꿀 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;
    [Tooltip("인벤토리 화면 루트 (배경 딤 + 열기/닫기 연출)")]
    [SerializeField] private UIPopupAnimator _screen;
    [Tooltip("잠긴 칸을 눌렀을 때 뜨는 확장 구매 팝업")]
    [SerializeField] private ExpandPopupView _expandPopup;

    [Header("데이터")]
    [Tooltip("탭과 소분류 카테고리 전부. 부모가 없는 것 = 탭, 부모가 있는 것 = 그 탭의 칩 (SortOrder 순으로 정렬됨)")]
    [SerializeField] private List<ItemCategory> _categories;

    [Tooltip("처음 열었을 때의 정렬 방식")]
    [SerializeField] private ItemSortMode _defaultSortMode = ItemSortMode.Rarity;

    private readonly List<CategoryTabView> _tabs = new List<CategoryTabView>();
    private readonly List<CategoryTabView> _chips = new List<CategoryTabView>();
    private readonly List<ItemSlotView> _slots = new List<ItemSlotView>();
    private readonly List<SlotData> _layout = new List<SlotData>();

    private Inventory _inventory;
    private ItemCategory _selectedTab;
    // 소분류 칩에서 고른 필터. "전체" 칩은 탭 자신을 가리킴 → 항상 이 값으로 필터링하면 됨
    private ItemCategory _selectedFilter;
    private ItemSortMode _sortMode;
    private ItemDefinition _selectedItem;
    private bool _isBuilt;

    private void OnEnable()
    {
        _inventory = InventoryManager.Instance.Inventory;

        if (!_isBuilt)
            Build();

        _inventory.OnItemChanged += HandleItemChanged;
        _inventory.OnCapacityChanged += HandleCapacityChanged;
        _detailView.OnCloseClicked += Close;
        _expandPopup.OnConfirmClicked += HandleExpandConfirmed;
        _sortDropdown.OnChanged += HandleSortChanged;

        // 꺼져 있던 동안 바뀐 내용 반영
        RefreshAll();
    }

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.OnItemChanged -= HandleItemChanged;
            _inventory.OnCapacityChanged -= HandleCapacityChanged;
        }
        _detailView.OnCloseClicked -= Close;
        _expandPopup.OnConfirmClicked -= HandleExpandConfirmed;
        _sortDropdown.OnChanged -= HandleSortChanged;
    }

    // 화면 루트가 꺼지면 이 컴포넌트도 OnDisable/OnEnable 되므로 구독 관리는 그대로 동작
    public void Open() => _screen.Show();
    public void Close() => _screen.Hide();

    #region 생성

    private void Build()
    {
        // 에디터에서 배치해 둔 자리표시용 탭/칩/슬롯 제거
        ClearChildren(_tabParent);
        ClearChildren(_chipParent);
        ClearChildren(_slotParent);

        var tabs = GetSortedChildren(null);
        foreach (var category in tabs)
        {
            var tab = Instantiate(_tabPrefab, _tabParent);
            tab.Bind(category);
            tab.OnClicked += HandleTabClicked;
            _tabs.Add(tab);
        }

        _sortMode = _defaultSortMode;
        _sortDropdown.SetValue(_sortMode);

        SelectTab(tabs.Count > 0 ? tabs[0] : null);
        _isBuilt = true;
    }

    /// <param name="parent">null이면 탭(부모 없는 카테고리) 목록</param>
    private List<ItemCategory> GetSortedChildren(ItemCategory parent)
    {
        var result = new List<ItemCategory>();
        foreach (var category in _categories)
        {
            if (category != null && category.Parent == parent)
                result.Add(category);
        }
        result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return result;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false); // Destroy는 프레임 끝에 일어나므로 이번 레이아웃에서 먼저 빼둠
            Destroy(child);
        }
    }

    // 탭이 바뀔 때마다 칩을 다시 그림: [전체(=탭 자신)] + [소분류들]. 소분류가 없으면 칩 줄을 숨김
    private void RebuildChips()
    {
        var subCategories = _selectedTab != null ? GetSortedChildren(_selectedTab) : new List<ItemCategory>();
        bool hasChips = subCategories.Count > 0;
        _chipParent.gameObject.SetActive(hasChips);

        int count = hasChips ? subCategories.Count + 1 : 0;
        EnsureCount(_chips, count, CreateChip);

        if (!hasChips)
            return;

        _chips[0].Bind(_selectedTab, AllChipLabel);
        for (int i = 0; i < subCategories.Count; i++)
            _chips[i + 1].Bind(subCategories[i]);
    }

    // 필요한 만큼만 새로 만들고, 남는 것은 꺼서 재사용 (간단한 풀링)
    private static void EnsureCount<T>(List<T> pool, int count, System.Func<T> create) where T : Component
    {
        while (pool.Count < count)
            pool.Add(create());

        for (int i = 0; i < pool.Count; i++)
            pool[i].gameObject.SetActive(i < count);
    }

    private CategoryTabView CreateChip()
    {
        var chip = Instantiate(_chipPrefab, _chipParent);
        chip.OnClicked += HandleChipClicked;
        return chip;
    }

    private ItemSlotView CreateSlot()
    {
        var slot = Instantiate(_slotPrefab, _slotParent);
        slot.OnClicked += HandleSlotClicked;
        return slot;
    }

    #endregion

    #region 갱신

    private void SelectTab(ItemCategory tab)
    {
        _selectedTab = tab;
        _selectedFilter = tab;
        _selectedItem = null;
        RebuildChips();
    }

    private void RefreshAll()
    {
        InventorySlotLayout.Build(_inventory, _selectedFilter, _sortMode, _layout);
        EnsureCount(_slots, _layout.Count, CreateSlot);

        for (int i = 0; i < _layout.Count; i++)
            ApplySlot(_slots[i], _layout[i]);

        // 선택했던 아이템이 이 필터에 없거나 다 팔렸으면 첫 아이템으로
        if (!IsShownInCurrentFilter(_selectedItem))
            _selectedItem = FirstItemInLayout();

        foreach (var tab in _tabs)
            tab.SetSelected(tab.Category == _selectedTab);

        foreach (var chip in _chips)
        {
            if (chip.gameObject.activeSelf)
                chip.SetSelected(chip.Category == _selectedFilter);
        }

        RefreshSelection();
    }

    private static void ApplySlot(ItemSlotView slot, SlotData data)
    {
        switch (data.State)
        {
            case SlotState.Item: slot.ShowItem(data.Item, data.Count); break;
            case SlotState.Empty: slot.ShowEmpty(); break;
            case SlotState.Locked: slot.ShowLocked(); break;
        }
    }

    private void RefreshSelection()
    {
        foreach (var slot in _slots)
        {
            if (slot.gameObject.activeSelf)
                slot.SetSelected(slot.State == SlotState.Item && slot.Item == _selectedItem);
        }

        if (_selectedItem == null)
            _detailView.ShowNothing();
        else
            _detailView.Show(_selectedItem, _inventory.GetCount(_selectedItem));
    }

    private bool IsShownInCurrentFilter(ItemDefinition item)
    {
        return item != null && _selectedFilter != null && _selectedFilter.Contains(item) && _inventory.GetCount(item) > 0;
    }

    private ItemDefinition FirstItemInLayout()
    {
        return _layout.Count > 0 && _layout[0].State == SlotState.Item ? _layout[0].Item : null;
    }

    private void ScrollToTop()
    {
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    #endregion

    #region 이벤트 처리

    private void HandleItemChanged(ItemChangedEvent e)
    {
        // 개수만 바뀜 → 그 슬롯 하나만 갱신 (칸 배치는 그대로)
        // 단, 최신순은 다시 얻으면 순서가 바뀌므로 전체 갱신
        bool onlyCountChanged = e.OldCount > 0 && e.NewCount > 0;
        bool orderMayChange = _sortMode == ItemSortMode.Recent && e.NewCount > e.OldCount;

        if (onlyCountChanged && !orderMayChange)
        {
            if (_selectedFilter != null && _selectedFilter.Contains(e.Item))
            {
                foreach (var slot in _slots)
                {
                    if (slot.gameObject.activeSelf && slot.Item == e.Item)
                        slot.ShowItem(e.Item, e.NewCount);
                }
            }

            if (e.Item == _selectedItem)
                _detailView.Show(_selectedItem, e.NewCount);
            return;
        }

        // 새 종류가 들어오거나 종류가 사라짐 → 공유 빈 칸 수가 바뀌므로 어느 탭이든 전체 갱신
        RefreshAll();
    }

    private void HandleCapacityChanged(int capacity)
    {
        RefreshAll();
    }

    private void HandleTabClicked(CategoryTabView tab)
    {
        if (tab.Category == _selectedTab)
            return;

        SelectTab(tab.Category);
        RefreshAll();
        ScrollToTop();
    }

    private void HandleChipClicked(CategoryTabView chip)
    {
        if (chip.Category == _selectedFilter)
            return;

        _selectedFilter = chip.Category;
        _selectedItem = null;
        RefreshAll();
        ScrollToTop();
    }

    private void HandleSortChanged(ItemSortMode mode)
    {
        if (mode == _sortMode)
            return;

        // 선택한 아이템은 유지 (같은 필터 안에서 순서만 바뀜)
        _sortMode = mode;
        RefreshAll();
        ScrollToTop();
    }

    private void HandleSlotClicked(ItemSlotView slot)
    {
        switch (slot.State)
        {
            case SlotState.Item:
                _selectedItem = slot.Item;
                RefreshSelection();
                break;
            case SlotState.Locked:
                ShowExpandPopup();
                break;
        }
    }

    private void ShowExpandPopup()
    {
        var manager = InventoryManager.Instance;
        if (!manager.CanExpand)
            return;

        var config = manager.Config;
        int newCapacity = Mathf.Min(_inventory.Capacity + config.ExpandStep, _inventory.MaxCapacity);
        bool canAfford = CurrencyManager.Instance.CanAfford(config.ExpandCurrency, config.ExpandCost);

        _expandPopup.Show(_inventory.Capacity, newCapacity, config.ExpandCurrency, config.ExpandCost, canAfford);
    }

    private void HandleExpandConfirmed()
    {
        // 성공하면 OnCapacityChanged → RefreshAll로 칸은 이미 갱신되어 있음
        if (InventoryManager.Instance.TryPurchaseExpansion())
            _expandPopup.Hide();
        else
            _expandPopup.ShowFailed(InventoryManager.Instance.Config.ExpandCurrency);
    }

    #endregion
}
