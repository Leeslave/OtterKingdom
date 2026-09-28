using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 인벤토리(순수 C# 모델)를 만들고 들고 있는 주인.
/// 칸 확장 구매처럼 인벤토리와 재화를 함께 다루는 일도 여기서 조율한다 (Inventory는 돈을 모름).
/// </summary>
// Presenter의 OnEnable보다 먼저 Inventory가 만들어져 있어야 함
[DefaultExecutionOrder(-100)]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("설정")]
    [SerializeField] private InventoryConfig _config;

    public Inventory Inventory { get; private set; }
    public InventoryConfig Config => _config;

    // 세이브에 있었지만 ItemDatabase에 없는 아이템. 다음 저장 때 그대로 다시 써서 잃어버리지 않게 보관
    private readonly List<ItemStack> _unknownSavedStacks = new List<ItemStack>();

    public bool CanExpand => Inventory.Capacity < Inventory.MaxCapacity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Inventory = new Inventory(_config.InitialCapacity, _config.MaxCapacity);
    }

    /// <summary>
    /// 칸 확장 구매. 최대 용량이거나 재화가 부족하면 아무것도 바꾸지 않고 false.
    /// </summary>
    public bool TryPurchaseExpansion()
    {
        // 돈부터 빼면 안 됨: 확장할 수 없는데 결제되는 일을 먼저 막는다
        if (!CanExpand)
            return false;

        if (!CurrencyManager.Instance.TrySpend(_config.ExpandCurrency, _config.ExpandCost, TransactionSource.InventoryExpand))
            return false;

        Inventory.ExpandCapacity(_config.ExpandStep);
        return true;
    }

    #region 판매

    /// <summary>
    /// 아이템을 팔아 판매가 × 개수만큼 판매 재화(골드)를 받는다. 개수가 부족하면 아무것도 바꾸지 않고 false.
    /// </summary>
    public bool TrySell(ItemDefinition item, int amount)
    {
        // 아이템부터 빼야 함: 재고가 없는데 돈만 들어오는 일을 막는다
        if (!Inventory.TryRemove(item, amount, ItemChangeReason.Sell))
            return false;

        int total = (int)Math.Min((long)item.SellPrice * amount, int.MaxValue);
        if (total > 0)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_config.SellCurrency, total, TransactionSource.ItemSale));

        return true;
    }

    /// <summary>조건에 맞는 아이템을 전부 판매 (예: 작물 전체 판매). filter가 null이면 전부.</summary>
    /// <returns>받은 금액 합계 (배율 적용 전)</returns>
    public int SellAll(Predicate<ItemDefinition> filter = null)
    {
        // 판매하면서 Counts가 바뀌므로 목록을 먼저 복사
        var targets = new List<KeyValuePair<ItemDefinition, int>>(Inventory.Counts);

        long sum = 0;
        foreach (var pair in targets)
        {
            if (filter != null && !filter(pair.Key))
                continue;

            if (TrySell(pair.Key, pair.Value))
                sum += (long)pair.Key.SellPrice * pair.Value;
        }
        return (int)Math.Min(sum, int.MaxValue);
    }

    #endregion

    #region 세이브

    /// <summary>
    /// 게임 시작 시 한 번, 가방이 빈 상태에서 세이브를 불러온다.
    /// savedCapacity가 현재 용량 이하(옛 세이브의 0 포함)면 기본 용량을 유지한다.
    /// </summary>
    public void LoadFromSave(IEnumerable<ItemStack> items, int savedCapacity)
    {
        if (savedCapacity > Inventory.Capacity)
            Inventory.ExpandCapacity(savedCapacity - Inventory.Capacity);

        InventorySaveConverter.Read(items, _config.ItemDatabase, Inventory, _unknownSavedStacks);

        foreach (var stack in _unknownSavedStacks)
            Debug.LogWarning($"[InventoryManager] ItemDatabase에 없는 아이템 ID: {stack.itemId} x{stack.quantity} (저장 시 그대로 보존)");
    }

    /// <summary>가방 내용을 세이브 목록에 쓴다 (기존 내용은 지움). 용량은 Inventory.Capacity를 따로 저장.</summary>
    public void WriteToSave(List<ItemStack> items)
    {
        InventorySaveConverter.Write(Inventory, _unknownSavedStacks, items);
    }

    #endregion
}
