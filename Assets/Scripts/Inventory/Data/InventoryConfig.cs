using UnityEngine;

[CreateAssetMenu(fileName = "InventoryConfig", menuName = "Game Data/Inventory/Inventory Config")]
public class InventoryConfig : ScriptableObject
{
    [Header("슬롯 용량")]
    [Tooltip("처음 해금된 칸 수 (1 이상)")]
    [SerializeField]
    private int _initialCapacity = 16;

    [Tooltip("확장 가능한 최대 칸 수 (초기 칸 수 이상), 여기까지 잠긴 칸으로 표시")]
    [SerializeField]
    private int _maxCapacity = 40;

    [Header("확장")]
    [Tooltip("한 번 구매 시 늘어나는 칸 수 (1 이상)")]
    [SerializeField]
    private int _expandStep = 4;

    [Tooltip("한 번 확장할 때의 가격 (0 이상)")]
    [SerializeField]
    private int _expandCost = 100;

    [Tooltip("확장에 쓰이는 재화 (유료 재화)")]
    [SerializeField]
    private Currency _expandCurrency;

    public int InitialCapacity => _initialCapacity;
    public int MaxCapacity => _maxCapacity;
    public int ExpandStep => _expandStep;
    public int ExpandCost => _expandCost;
    public Currency ExpandCurrency => _expandCurrency;

    private void OnValidate()
    {
        _initialCapacity = Mathf.Max(1, _initialCapacity);
        _maxCapacity = Mathf.Max(_initialCapacity, _maxCapacity);
        _expandStep = Mathf.Max(1, _expandStep);
        _expandCost = Mathf.Max(0, _expandCost);

        if (_expandCurrency == null)
            Debug.LogWarning($"[{name}] ExpandCurrency가 비어있습니다.", this);
    }
}
