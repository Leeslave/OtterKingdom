using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "어디로 갈까?" 이동 팝업. 장소 카드를 그리고 고른 장소를 알리기만 한다 (씬 이동은 모름).
/// </summary>
public class TravelPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("장소 카드")]
    [SerializeField] private ZoneCardView _cardPrefab;
    [SerializeField] private Transform _cardParent;

    public event Action<ZoneDefinition> OnZoneSelected;

    /// <summary>닫기 연출이 끝났을 때 (배경을 눌러 닫은 경우 포함)</summary>
    public event Action OnHidden;

    public bool IsOpen => _animator.IsOpen;

    private readonly List<ZoneCardView> _cards = new List<ZoneCardView>();

    private void Awake()
    {
        _animator.OnHidden += () => OnHidden?.Invoke();
    }

    public void Show(IReadOnlyList<ZoneDefinition> zones, ZoneDefinition currentZone)
    {
        if (zones == null)
            throw new ArgumentNullException(nameof(zones));

        // 필요한 만큼만 만들고 남는 카드는 꺼서 재사용
        while (_cards.Count < zones.Count)
        {
            var card = Instantiate(_cardPrefab, _cardParent);
            card.OnClicked += HandleCardClicked;
            _cards.Add(card);
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            bool used = i < zones.Count;
            _cards[i].gameObject.SetActive(used);
            if (used)
                _cards[i].Bind(zones[i], zones[i] == currentZone);
        }

        _animator.Show();
    }

    public void Hide()
    {
        _animator.Hide();
    }

    private void HandleCardClicked(ZoneCardView card)
    {
        OnZoneSelected?.Invoke(card.Zone);
    }
}
