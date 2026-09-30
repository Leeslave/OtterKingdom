using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 요정 상점 화면: 요정 말풍선, 탭(전체 + 카탈로그 탭), 상품 카드 목록. 카드를 누르면 알리기만 한다.
/// 보유 재화(골드·조개)는 CurrencyAmountView가 따로 그린다.
/// </summary>
public class FairyShopView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("요정")]
    [SerializeField] private TextMeshProUGUI _dialogueText;

    [Header("탭")]
    [SerializeField] private DecorStorageTabView _tabPrefab;
    [SerializeField] private Transform _tabParent;

    [Header("상품")]
    [SerializeField] private ShopProductCardView _cardPrefab;
    [SerializeField] private Transform _cardParent;
    [Tooltip("탭을 바꾸거나 열 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;

    public event Action<ShopProduct> OnProductClicked;

    private readonly List<DecorStorageTabView> _tabs = new List<DecorStorageTabView>();
    private readonly List<ShopProductCardView> _cards = new List<ShopProductCardView>();
    private ShopCatalog _catalog;
    private List<ShopProduct> _products = new List<ShopProduct>();
    private ItemCategory _selectedCategory;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Hide);
    }

    /// <param name="category">처음 보여줄 탭의 분류 (null이면 "전체")</param>
    public void Show(ShopCatalog catalog, string dialogue, ItemCategory category)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));

        _catalog = catalog;
        _products = catalog.Products;
        _dialogueText.text = dialogue;
        BuildTabs();
        Select(category);
        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    private void BuildTabs()
    {
        if (_tabs.Count > 0)
            return;

        AddTab(null, "전체");
        foreach (var tab in _catalog.Tabs)
        {
            if (tab != null && tab.Category != null)
                AddTab(tab.Category, tab.Label);
        }
    }

    private void AddTab(ItemCategory category, string label)
    {
        var tab = Instantiate(_tabPrefab, _tabParent);
        tab.Bind(category, label);
        tab.OnClicked += t => Select(t.Category);
        _tabs.Add(tab);
    }

    private void Select(ItemCategory category)
    {
        _selectedCategory = category;
        foreach (var tab in _tabs)
            tab.SetSelected(tab.Category == category);

        var shown = _products.FindAll(p => category == null || category.Contains(p.Item));
        while (_cards.Count < shown.Count)
        {
            var card = Instantiate(_cardPrefab, _cardParent);
            card.OnClicked += c => OnProductClicked?.Invoke(c.Product);
            _cards.Add(card);
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            bool used = i < shown.Count;
            _cards[i].gameObject.SetActive(used);
            if (used)
                _cards[i].Bind(shown[i], _catalog.FindTab(shown[i])?.Label);
        }

        _scrollRect.verticalNormalizedPosition = 1f;
    }
}
