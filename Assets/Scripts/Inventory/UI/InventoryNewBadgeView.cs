using UnityEngine;

/// <summary>
/// 가방 버튼의 "N" 뱃지. 가방에서 아직 확인하지 않은 새 종류가 있을 때만 보인다.
/// </summary>
public class InventoryNewBadgeView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("켜고 끌 뱃지 오브젝트 (이 컴포넌트가 붙은 오브젝트와 달라야 함)")]
    [SerializeField]
    private GameObject _badge;

    private Inventory _inventory;

    private void OnEnable()
    {
        _inventory = InventoryManager.Instance.Inventory;
        _inventory.OnHasNewItemsChanged += HandleHasNewItemsChanged;

        // 꺼져 있던 동안 바뀐 상태 반영
        HandleHasNewItemsChanged(_inventory.HasNewItems);
    }

    private void OnDisable()
    {
        if (_inventory != null)
            _inventory.OnHasNewItemsChanged -= HandleHasNewItemsChanged;
    }

    private void HandleHasNewItemsChanged(bool hasNewItems)
    {
        _badge.SetActive(hasNewItems);
    }
}
