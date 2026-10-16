using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트 씬 전용. 시작 시 아이템을 지급하고, 인스펙터 우클릭(⋮) 메뉴로 지급/제거/확장을 시험한다.
/// </summary>
public class TestInventoryGiver : MonoBehaviour
{
    [Header("지급할 아이템")]
    [SerializeField] private List<ItemDefinition> _items;
    [SerializeField] private int _startAmount = 3;

    [Header("확장 테스트")]
    [SerializeField] private Currency _premiumCurrency;
    [SerializeField] private int _premiumAmount = 500;

    [Header("연결")]
    [SerializeField] private InventoryPresenter _presenter;

    private Inventory Inventory => InventoryManager.Instance.Inventory;

    private void Start()
    {
        foreach (var item in _items)
            Inventory.Add(item, _startAmount, ItemChangeReason.Test);
    }

    [ContextMenu("아이템 랜덤 지급 (+5)")]
    private void GiveRandom()
    {
        var item = _items[Random.Range(0, _items.Count)];
        int added = Inventory.Add(item, 5, ItemChangeReason.Test);
        Debug.Log($"[Test] {item.DisplayName} +{added} (요청 5)");
    }

    [ContextMenu("아이템 랜덤 제거 (-1)")]
    private void RemoveRandom()
    {
        var item = _items[Random.Range(0, _items.Count)];
        bool removed = Inventory.TryRemove(item, 1, ItemChangeReason.Test);
        Debug.Log($"[Test] {item.DisplayName} -1 → {(removed ? "성공" : "부족")}");
    }

    [ContextMenu("유료 재화 지급")]
    private void GivePremium()
    {
        CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_premiumCurrency, _premiumAmount, TransactionSource.TestGet));
    }

    [ContextMenu("칸 확장 구매")]
    private void PurchaseExpansion()
    {
        bool success = InventoryManager.Instance.TryPurchaseExpansion();
        Debug.Log($"[Test] 칸 확장 구매 → {(success ? "성공" : "실패 (최대 용량 또는 재화 부족)")}, 용량 {Inventory.Capacity}/{Inventory.MaxCapacity}");
    }

    [ContextMenu("인벤토리 열기")]
    private void OpenInventory()
    {
        // 전역 UI의 가방을 연다. 씬의 원본 가방 UI(InventoryUISource)는 꺼져 있음
        var presenter = GlobalUIRoot.Instance != null
            ? GlobalUIRoot.Instance.GetComponentInChildren<InventoryPresenter>(true)
            : _presenter;
        presenter.Open();
    }
}
