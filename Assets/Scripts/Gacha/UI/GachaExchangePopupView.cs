using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>반짝 조각 교환소 팝업: 가진 조각 · 고를 수 있는 장난감 목록 (상시 레어 60 · 에픽 300 · 지난 픽업 600)</summary>
public class GachaExchangePopupView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private UIPopupAnimator _animator;

    [SerializeField] private TextMeshProUGUI _shardText;

    [SerializeField] private RectTransform _rowParent;

    [Tooltip("복제해서 쓰는 줄 (꺼 둠)")]
    [SerializeField] private GachaExchangeRowView _rowTemplate;

    [Tooltip("교환할 장난감이 없을 때")]
    [SerializeField] private TextMeshProUGUI _emptyText;

    [Tooltip("겹친 장난감 → 반짝 조각 설명")]
    [SerializeField] private TextMeshProUGUI _noteText;

    [SerializeField] private Button _closeButton;

    public event Action<ItemDefinition> OnExchange;

    private readonly List<GachaExchangeRowView> _rows = new List<GachaExchangeRowView>();

    public bool IsOpen => _animator.IsOpen;

    private void Awake()
    {
        _rowTemplate.gameObject.SetActive(false);
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    public void Show(List<GachaExchangeEntry> entries, int shards, string note)
    {
        Render(entries, shards, note);
        if (!_animator.IsOpen)
            _animator.Show();
    }

    public void Render(List<GachaExchangeEntry> entries, int shards, string note)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        _shardText.text = shards.ToString("N0");
        _noteText.text = note;
        while (_rows.Count < entries.Count)
        {
            var row = Instantiate(_rowTemplate, _rowParent);
            row.OnExchange += item => OnExchange?.Invoke(item);
            _rows.Add(row);
        }
        for (int i = 0; i < _rows.Count; i++)
        {
            bool used = i < entries.Count;
            _rows[i].gameObject.SetActive(used);
            if (used)
                _rows[i].Bind(entries[i], shards >= entries[i].Cost);
        }
        _emptyText.gameObject.SetActive(entries.Count == 0);
    }

    public void Shake() => _animator.Shake();

    public void Hide() => _animator.Hide();
}
