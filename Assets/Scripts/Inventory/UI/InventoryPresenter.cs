using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inventory(모델)와 뷰들을 연결한다.
/// 화면 상태(선택한 탭, 선택한 아이템)는 여기 한 곳에서만 관리하고, 뷰에는 결과만 그리게 한다.
/// </summary>
public class InventoryPresenter : MonoBehaviour
{
    [Header("뷰")]
    [SerializeField] private ItemDetailView _detailView;
    [SerializeField] private CategoryTabView _tabPrefab;
    [SerializeField] private Transform _tabParent;
    [SerializeField] private ItemSlotView _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [Tooltip("탭을 바꿀 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;
    [Tooltip("인벤토리 화면 루트 (배경 딤 + 열기/닫기 연출)")]
    [SerializeField] private UIPopupAnimator _screen;
    [Tooltip("잠긴 칸을 눌렀을 때 뜨는 확장 구매 팝업")]
    [SerializeField] private ExpandPopupView _expandPopup;

    [Header("데이터")]
    [Tooltip("탭으로 만들 카테고리 (SortOrder 순으로 정렬됨)")]
    [SerializeField] private List<ItemCategory> _categories;

    private readonly List<CategoryTabView> _tabs = new List<CategoryTabView>();
    private readonly List<ItemSlotView> _slots = new List<ItemSlotView>();
    private readonly List<SlotData> _layout = new List<SlotData>();

    private Inventory _inventory;
    private ItemCategory _selectedCategory;
    private ItemDefinition _selectedItem;
    private bool _isBuilt;

    private void OnEnable()
    {
        _inventory = InventoryManager.Instance.Inventory;

        if (!_isBuilt)
            BuildTabs();

        _inventory.OnItemChanged += HandleItemChanged;
        _inventory.OnCapacityChanged += HandleCapacityChanged;
        _detailView.OnCloseClicked += Close;
        _expandPopup.OnConfirmClicked += HandleExpandConfirmed;

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
    }

    // 화면 루트가 꺼지면 이 컴포넌트도 OnDisable/OnEnable 되므로 구독 관리는 그대로 동작
    public void Open() => _screen.Show();
    public void Close() => _screen.Hide();

    #region 생성

    private void BuildTabs()
    {
        // 에디터에서 배치해 둔 자리표시용 탭/슬롯 제거
        ClearChildren(_tabParent);
        ClearChildren(_slotParent);

        var sorted = new List<ItemCategory>(_categories);
        sorted.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

        foreach (var category in sorted)
        {
            var tab = Instantiate(_tabPrefab, _tabParent);
            tab.Bind(category);
            tab.OnClicked += HandleTabClicked;
            _tabs.Add(tab);
        }

        _selectedCategory = sorted.Count > 0 ? sorted[0] : null;
        _isBuilt = true;
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

    // 필요한 만큼만 새로 만들고, 남는 슬롯은 꺼서 재사용 (간단한 풀링)
    private void EnsureSlotCount(int count)
    {
        while (_slots.Count < count)
        {
            var slot = Instantiate(_slotPrefab, _slotParent);
            slot.OnClicked += HandleSlotClicked;
            _slots.Add(slot);
        }

        for (int i = 0; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(i < count);
    }

    #endregion

    #region 갱신

    private void RefreshAll()
    {
        InventorySlotLayout.Build(_inventory, _selectedCategory, _layout);
        EnsureSlotCount(_layout.Count);

        for (int i = 0; i < _layout.Count; i++)
            ApplySlot(_slots[i], _layout[i]);

        // 선택했던 아이템이 이 탭에 없거나 다 팔렸으면 첫 아이템으로
        if (!IsShownInCurrentTab(_selectedItem))
            _selectedItem = FirstItemInLayout();

        foreach (var tab in _tabs)
            tab.SetSelected(tab.Category == _selectedCategory);

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

    private bool IsShownInCurrentTab(ItemDefinition item)
    {
        return _selectedCategory != null && _selectedCategory.Contains(item) && _inventory.GetCount(item) > 0;
    }

    private ItemDefinition FirstItemInLayout()
    {
        return _layout.Count > 0 && _layout[0].State == SlotState.Item ? _layout[0].Item : null;
    }

    #endregion

    #region 이벤트 처리

    private void HandleItemChanged(ItemChangedEvent e)
    {
        bool onlyCountChanged = e.OldCount > 0 && e.NewCount > 0;

        // 개수만 바뀜 → 그 슬롯 하나만 갱신 (칸 배치는 그대로)
        if (onlyCountChanged)
        {
            if (_selectedCategory != null && _selectedCategory.Contains(e.Item))
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
        if (tab.Category == _selectedCategory)
            return;

        _selectedCategory = tab.Category;
        _selectedItem = null;
        RefreshAll();

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
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
