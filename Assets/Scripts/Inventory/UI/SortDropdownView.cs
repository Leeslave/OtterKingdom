using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 정렬 방식 드롭다운 ("등급순 ▼"). ItemSortMode 값을 순서대로 보여주고 바뀌면 알리기만 한다.
/// </summary>
public class SortDropdownView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField]
    private TMP_Dropdown _dropdown;

    public event Action<ItemSortMode> OnChanged;

    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>이벤트 없이 표시만 바꿈 (프레젠터가 상태를 반영할 때)</summary>
    public void SetValue(ItemSortMode mode)
    {
        // 부모(프레젠터)의 OnEnable이 이 오브젝트의 Awake보다 먼저 불릴 수 있으므로 여기서도 초기화
        EnsureInitialized();
        _dropdown.SetValueWithoutNotify((int)mode);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        var options = new List<string>();
        foreach (ItemSortMode mode in Enum.GetValues(typeof(ItemSortMode)))
            options.Add(mode.ToDisplayName());

        _dropdown.ClearOptions();
        _dropdown.AddOptions(options);
        _dropdown.onValueChanged.AddListener(index => OnChanged?.Invoke((ItemSortMode)index));
        _isInitialized = true;
    }
}
