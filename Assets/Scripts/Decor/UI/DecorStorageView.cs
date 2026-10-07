using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 모드 아래쪽 보관함: 탭(전체 + 꾸미기 하위 분류 + 건물), 물건 칸 목록, 맨 끝 [+ 상점] 칸.
/// 건물 탭에서는 보관함 대신 지을 수 있는 건물(비용·잠긴 이유)을 보이고 [+ 상점] 칸을 숨긴다.
/// 받은 목록과 개수만 그리고, 칸을 누르면 알리기만 한다.
/// </summary>
public class DecorStorageView : MonoBehaviour
{
    [Header("탭")]
    [SerializeField] private DecorStorageTabView _tabPrefab;
    [SerializeField] private Transform _tabParent;

    [Header("칸")]
    [SerializeField] private DecorStorageSlotView _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [Tooltip("맨 끝의 [+ 상점] 칸 버튼")]
    [SerializeField] private Button _shopSlot;
    [Tooltip("[+ 상점] 칸 전체 (이름 포함). 항상 마지막 순서로 옮김")]
    [SerializeField] private Transform _shopSlotRoot;
    [Tooltip("탭을 바꿀 때 맨 앞으로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;

    public event Action<DecorDefinition> OnDecorClicked;
    public event Action OnShopClicked;
    public event Action OnTabChanged;

    private readonly List<DecorStorageTabView> _tabs = new List<DecorStorageTabView>();
    private readonly List<DecorStorageSlotView> _slots = new List<DecorStorageSlotView>();
    private ItemCategory _selectedCategory;
    private DecorStorageTabView _buildingsTab;
    private bool _buildingsSelected;

    /// <summary>지금 건물 탭을 보고 있는지</summary>
    public bool ShowsBuildings => _buildingsSelected;

    private void Awake()
    {
        _shopSlot.onClick.AddListener(() => OnShopClicked?.Invoke());
    }

    /// <summary>탭을 만든다 (처음 한 번). categories: "전체" 다음에 올 분류들</summary>
    public void BuildTabs(IReadOnlyList<ItemCategory> categories)
    {
        if (_tabs.Count > 0)
            return;

        AddTab(null, "전체");
        foreach (var category in categories)
            AddTab(category, category.DisplayName);
        _buildingsTab = Instantiate(_tabPrefab, _tabParent);
        _buildingsTab.BindBuildings("건물");
        _buildingsTab.OnClicked += HandleTabClicked;
        _tabs.Add(_buildingsTab);
        UpdateTabSelection();
    }

    /// <summary>건물 탭을 고름 (게시판 건물 부탁에서 바로 열 때)</summary>
    public void SelectBuildingsTab()
    {
        if (_buildingsTab == null || !_buildingsTab.gameObject.activeSelf || _buildingsSelected)
            return;
        _buildingsSelected = true;
        _selectedCategory = null;
        UpdateTabSelection();
        _scrollRect.horizontalNormalizedPosition = 0f;
    }

    /// <summary>건물 탭을 보일지 (건물은 광장에서만 지음). 숨기면서 보고 있었으면 "전체"로</summary>
    public void SetBuildingsTabVisible(bool visible)
    {
        if (_buildingsTab == null)
            return;
        _buildingsTab.gameObject.SetActive(visible);
        if (!visible && _buildingsSelected)
        {
            _buildingsSelected = false;
            _selectedCategory = null;
            UpdateTabSelection();
        }
    }

    /// <summary>칸을 다시 그린다. count: 보관함에 남은 개수, selected: 지금 들고 있는 물건</summary>
    public void Refresh(IReadOnlyList<DecorDefinition> decors, Func<DecorDefinition, int> count, Func<DecorDefinition, int> placed,
        DecorDefinition selected)
    {
        var shown = new List<DecorDefinition>();
        foreach (var decor in decors)
        {
            if (decor != null && decor.Item != null && (_selectedCategory == null || _selectedCategory.Contains(decor.Item)))
                shown.Add(decor);
        }

        EnsureSlots(shown.Count);
        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < shown.Count;
            _slots[i].gameObject.SetActive(used);
            if (used)
                _slots[i].Bind(shown[i], count(shown[i]), placed(shown[i]) > 0, shown[i] == selected);
        }

        _shopSlotRoot.gameObject.SetActive(true);
        _shopSlotRoot.SetAsLastSibling();
    }

    /// <summary>건물 탭의 칸을 다시 그린다. info: 건물마다 (위쪽 한 줄, 열렸는지)</summary>
    public void RefreshBuildings(IReadOnlyList<BuildingDefinition> buildings, Func<BuildingDefinition, (string top, bool available)> info,
        DecorDefinition selected)
    {
        var shown = new List<BuildingDefinition>();
        foreach (var building in buildings)
        {
            if (building != null)
                shown.Add(building);
        }

        EnsureSlots(shown.Count);
        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < shown.Count;
            _slots[i].gameObject.SetActive(used);
            if (!used)
                continue;
            var (top, available) = info(shown[i]);
            _slots[i].BindBuilding(shown[i], top, available, shown[i] == selected);
        }

        _shopSlotRoot.gameObject.SetActive(false);
    }

    private void EnsureSlots(int count)
    {
        while (_slots.Count < count)
        {
            var slot = Instantiate(_slotPrefab, _slotParent);
            slot.OnClicked += s => OnDecorClicked?.Invoke(s.Decor);
            _slots.Add(slot);
        }
    }

    private void AddTab(ItemCategory category, string label)
    {
        var tab = Instantiate(_tabPrefab, _tabParent);
        tab.Bind(category, label);
        tab.OnClicked += HandleTabClicked;
        _tabs.Add(tab);
    }

    private void HandleTabClicked(DecorStorageTabView tab)
    {
        if (tab.Category == _selectedCategory && tab.IsBuildings == _buildingsSelected)
            return;

        _selectedCategory = tab.Category;
        _buildingsSelected = tab.IsBuildings;
        UpdateTabSelection();
        _scrollRect.horizontalNormalizedPosition = 0f;
        OnTabChanged?.Invoke();
    }

    private void UpdateTabSelection()
    {
        foreach (var tab in _tabs)
            tab.SetSelected(tab.Category == _selectedCategory && tab.IsBuildings == _buildingsSelected);
    }
}
