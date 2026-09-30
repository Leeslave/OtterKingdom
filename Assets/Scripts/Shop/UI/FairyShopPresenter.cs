using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 요정 상점 흐름: 열기(요정 NPC, 꾸미기 보관함 [+ 상점]) → 상품 카드 → 구매 팝업(수량) → 구매.
/// 재화가 모자라면 재화 부족 팝업을 띄우고, 거기서 [충전하기]를 누르면 CurrencyShopPresenter가 충전 화면을 연다.
/// 전역 UI 루트에 붙어 늘 켜져 있으므로 Instance로 어디서든 연다.
/// </summary>
public class FairyShopPresenter : MonoBehaviour
{
    private const string BagFullText = "가방이 가득 찼어요";

    public static FairyShopPresenter Instance { get; private set; }

    [Header("데이터")]
    [SerializeField] private ShopCatalog _catalog;
    [Tooltip("요정 말풍선")]
    [TextArea(2, 3)]
    [SerializeField] private string _dialogue = "어서 와! 모종이랑\n광장 장난감도 있어~";

    [Header("화면")]
    [SerializeField] private FairyShopView _shop;
    [SerializeField] private ShopPurchasePopupView _popup;
    [Tooltip("재화 부족 팝업 (재화 충전 화면과 같은 것)")]
    [SerializeField] private CurrencyShortagePopupView _shortage;

    public bool IsOpen => _shop.IsOpen;

    private bool _glyphsReady;

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        _shop.OnProductClicked += HandleProductClicked;
        _popup.OnConfirmed += HandleConfirmed;
    }

    private void OnDisable()
    {
        _shop.OnProductClicked -= HandleProductClicked;
        _popup.OnConfirmed -= HandleConfirmed;
    }

    /// <param name="category">처음 보여줄 탭의 분류 (null이면 "전체", 예: 꾸미기 보관함에서 열면 장난감)</param>
    public void Open(ItemCategory category = null)
    {
        if (!_glyphsReady)
            PrepareGlyphs();

        _shop.Show(_catalog, _dialogue, category);
    }

    private void HandleProductClicked(ShopProduct product)
    {
        var inventory = InventoryManager.Instance.Inventory;
        int owned = inventory.GetCount(product.Item);
        int max = ShopPurchaseRules.MaxQuantity(product, inventory.GetAddableAmount(product.Item));
        _popup.Show(product, owned, max);
    }

    private void HandleConfirmed(int quantity)
    {
        var product = _popup.Product;
        switch (TryBuy(product, quantity))
        {
            case ShopPurchaseResult.Bought:
                _popup.Hide();
                break;

            case ShopPurchaseResult.BagFull:
                _popup.ShowFailed(BagFullText);
                break;

            case ShopPurchaseResult.NotEnoughCurrency:
                int need = ShopPurchaseRules.TotalPrice(product, quantity);
                int have = CurrencyManager.Instance.GetCurrency(product.PriceCurrency);
                _shortage.Show(product.PriceCurrency, need, have);
                break;
        }
    }

    /// <summary>가방 자리 확인 → 값 치르기 → 가방에 넣기. 실패하면 아무것도 바꾸지 않는다</summary>
    private static ShopPurchaseResult TryBuy(ShopProduct product, int quantity)
    {
        var inventory = InventoryManager.Instance.Inventory;
        int items = ShopPurchaseRules.TotalItems(product, quantity);

        // 자리부터 확인: 돈만 내고 가방에 못 넣는 일을 막는다
        if (inventory.GetAddableAmount(product.Item) < items)
            return ShopPurchaseResult.BagFull;

        int total = ShopPurchaseRules.TotalPrice(product, quantity);
        if (!CurrencyManager.Instance.TrySpend(product.PriceCurrency, total, TransactionSource.ShopPurchase))
            return ShopPurchaseResult.NotEnoughCurrency;

        inventory.Add(product.Item, items, ItemChangeReason.Purchase);
        return ShopPurchaseResult.Bought;
    }

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (퀘스트 화면과 같은 이유: 스크롤 목록이 있는 화면에서
    // 새 글자가 한꺼번에 추가되면 D3D12 에디터에서 GPU가 멈춰 크래시가 났다)
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789,x:개 전체보유합계취소구매하기요정상점").Append(_dialogue).Append(BagFullText);
        foreach (var tab in _catalog.Tabs)
        {
            if (tab != null)
                text.Append(tab.Label);
        }
        foreach (var product in _catalog.Products)
        {
            text.Append(product.DisplayName).Append(product.Item.Description);
            if (product.Item.Rarity != null)
                text.Append(product.Item.Rarity.DisplayName);
        }

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var label in _shop.GetComponentsInChildren<TMP_Text>(true))
            fonts.Add(label.font);
        foreach (var label in _popup.GetComponentsInChildren<TMP_Text>(true))
            fonts.Add(label.font);

        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }
        _glyphsReady = true;
    }
}
