using System;
using UnityEngine;

#region Currency SO
[CreateAssetMenu(fileName = "Currency", menuName = "Game Data/Currency/Currency")]
public class Currency : ScriptableObject
{
    [Header("식별 및 기본 정보")]
    [Tooltip("Key로 사용될 ID")]
    public string CurrencyID;
    [Tooltip("실제 화면에 표시될 화폐 이름")]
    public string DisplayName;
    [TextArea]
    public string Description; // 상점이나 인벤토리에서 보여줄 설명

    [Header("시각 요소 (UI)")]
    [Tooltip("UI에 표시될 아이콘")]
    public Sprite Icon;

    [Tooltip("최대 보유 한도")]
    public int MaxCapacity = Int32.MaxValue;

    [Tooltip("최소 보유 한도")]
    public int MinCapacity = Int32.MinValue;
}
#endregion

#region CurrencyTransaction
/// <summary>
/// 재화의 획득/소비 출처
/// </summary>
public enum TransactionSource{TestGet, TestUse, GotchaUse, ShopPurchase, BlacksmithUpgrade, CropSale, FarmUpgrade}
public struct CurrencyTransaction
{
    [Header("Data")]
    public Currency Currency;
    public int Amount;
    public TransactionSource Source;
    public float Multiplier;


    public CurrencyTransaction(Currency currency, int amount, TransactionSource source, float multiplier = 1f)
    {
        this.Currency = currency;
        this.Amount = amount;
        this.Source = source;
        this.Multiplier = multiplier;
    }

    // 최종 계산 금액 (기본금액 * 배율)
    public int FinalAmount => Mathf.RoundToInt(Amount * Multiplier);
}
#endregion
