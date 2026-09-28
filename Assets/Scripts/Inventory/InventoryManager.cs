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
}
