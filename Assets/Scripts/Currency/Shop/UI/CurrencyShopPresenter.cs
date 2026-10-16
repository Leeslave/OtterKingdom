using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충전 흐름 연결: 상단바 [+] → 골드 충전 / 조개 충전 → (조개가 모자라면) 조개 부족 → 조개 충전, (넉넉하면) 구매 확인 → 구매.
/// 현금 상품(조개 충전)은 스토어 결제가 붙기 전까지 안내만 띄운다.
/// </summary>
public class CurrencyShopPresenter : MonoBehaviour
{
    private const string CashNotReadyMessage = "결제는 아직 준비 중이에요";

    [Header("데이터")]
    [SerializeField] private CurrencyShopCatalog _catalog;

    [Header("상단바 재화 칸")]
    [SerializeField] private CurrencyPillView _goldPill;
    [SerializeField] private CurrencyPillView _gemPill;

    [Header("화면")]
    [SerializeField] private CurrencyShopView _goldShop;
    [SerializeField] private CurrencyShopView _gemShop;
    [SerializeField] private PurchaseConfirmPopupView _confirm;
    [SerializeField] private CurrencyShortagePopupView _shortage;

    private void OnEnable()
    {
        _goldPill.OnPlusClicked += OpenShopFor;
        _gemPill.OnPlusClicked += OpenShopFor;
        _goldShop.OnPackClicked += HandlePackClicked;
        _gemShop.OnPackClicked += HandlePackClicked;
        _confirm.OnConfirmed += HandleConfirmed;
        _shortage.OnChargeClicked += HandleChargeClicked;
    }

    private void OnDisable()
    {
        _goldPill.OnPlusClicked -= OpenShopFor;
        _gemPill.OnPlusClicked -= OpenShopFor;
        _goldShop.OnPackClicked -= HandlePackClicked;
        _gemShop.OnPackClicked -= HandlePackClicked;
        _confirm.OnConfirmed -= HandleConfirmed;
        _shortage.OnChargeClicked -= HandleChargeClicked;
    }

    /// <summary>이 재화를 파는 충전 화면을 연다 (골드 → 골드 충전, 조개 → 조개 충전)</summary>
    public void OpenShopFor(Currency currency)
    {
        var goldPacks = _catalog.GoldPacks;
        if (Sells(goldPacks, currency))
        {
            _goldShop.Show(goldPacks);
            return;
        }

        var gemPacks = _catalog.GemPacks;
        if (Sells(gemPacks, currency))
        {
            _gemShop.Show(gemPacks);
            return;
        }

        Debug.LogWarning($"[CurrencyShopPresenter] '{currency.DisplayName}'을(를) 파는 충전 상품이 없습니다.");
    }

    private static bool Sells(List<CurrencyPack> packs, Currency currency)
    {
        return packs.Exists(pack => pack.Reward == currency);
    }

    private void HandlePackClicked(CurrencyPack pack)
    {
        if (pack.IsCashPack)
        {
            _gemShop.ShowNotice(CashNotReadyMessage);
            return;
        }

        int balance = CurrencyManager.Instance.GetCurrency(pack.PriceCurrency);
        if (balance < pack.Price)
            _shortage.Show(pack.PriceCurrency, pack.Price, balance);
        else
            _confirm.Show(pack, balance);
    }

    private void HandleConfirmed(CurrencyPack pack)
    {
        if (TryBuy(pack))
            _confirm.Hide();
        else
            _confirm.ShowFailed();
    }

    // 조개 부족 → 조개 충전 (골드 충전 화면은 뒤에 그대로 둠)
    private void HandleChargeClicked(Currency currency)
    {
        _shortage.Hide();
        OpenShopFor(currency);
    }

    /// <returns>값을 내고 재화를 받았으면 true. 가격 재화가 부족하면 아무것도 바꾸지 않고 false</returns>
    private static bool TryBuy(CurrencyPack pack)
    {
        var manager = CurrencyManager.Instance;

        // 값을 먼저 내야 함: 모자란데 받기만 하는 일을 막는다
        if (!manager.TrySpend(pack.PriceCurrency, pack.Price, TransactionSource.ShopPurchase))
            return false;

        manager.ProcessTransaction(new CurrencyTransaction(pack.Reward, pack.TotalAmount, TransactionSource.ShopPurchase));
        return true;
    }
}
