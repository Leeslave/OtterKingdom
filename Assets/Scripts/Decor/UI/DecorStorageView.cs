using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 모드 아래쪽 보관함: 탭(전체 + 꾸미기 하위 분류), 물건 칸 목록, 맨 끝 [+ 상점] 칸.
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
        UpdateTabSelection();
    }

    /// <summary>칸을 다시 그린다. count: 보관함에 남은 개수, selected: 지금 들고 있는 물건</summary>
    public void Refresh(IReadOnlyList<DecorDefinition> decors, Func<DecorDefinition, int> count, DecorDefinition selected)
    {
        var shown = new List<DecorDefinition>();
        foreach (var decor in decors)
        {
            if (decor != null && decor.Item != null && (_selectedCategory == null || _selectedCategory.Contains(decor.Item)))
                shown.Add(decor);
        }

        while (_slots.Count < shown.Count)
        {
            var slot = Instantiate(_slotPrefab, _slotParent);
            slot.OnClicked += s => OnDecorClicked?.Invoke(s.Decor);
            _slots.Add(slot);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < shown.Count;
            _slots[i].gameObject.SetActive(used);
            if (used)
                _slots[i].Bind(shown[i], count(shown[i]), shown[i] == selected);
        }

        _shopSlotRoot.SetAsLastSibling();
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
        if (tab.Category == _selectedCategory)
            return;

        _selectedCategory = tab.Category;
        UpdateTabSelection();
        _scrollRect.horizontalNormalizedPosition = 0f;
        OnTabChanged?.Invoke();
    }

    private void UpdateTabSelection()
    {
        foreach (var tab in _tabs)
            tab.SetSelected(tab.Category == _selectedCategory);
    }
}
