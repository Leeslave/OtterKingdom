using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>교환소 한 줄: 장난감 그림 · 이름 · 등급 칩 · [반짝 조각 값]. 처음 누르면 "교환할까요?"로 바뀌고 한 번 더 누르면 교환</summary>
public class GachaExchangeRowView : MonoBehaviour
{
    private const float ConfirmSeconds = 3f;

    [Header("구성 요소")]
    [SerializeField] private Image _icon;

    [SerializeField] private TextMeshProUGUI _nameText;

    [SerializeField] private Image _rarityTag;

    [SerializeField] private TextMeshProUGUI _rarityText;

    [Tooltip("지난 픽업 장난감 표시")]
    [SerializeField] private GameObject _pastBadge;

    [SerializeField] private Button _button;

    [SerializeField] private Image _buttonImage;

    [SerializeField] private TextMeshProUGUI _costText;

    [SerializeField] private TextMeshProUGUI _confirmText;

    [SerializeField] private GameObject _costGroup;

    [Header("그림")]
    [Tooltip("흔함 · 레어 · 에픽 순 등급 칩")]
    [SerializeField] private Sprite[] _rarityTags = new Sprite[3];

    [SerializeField] private Sprite _affordableButton;

    [SerializeField] private Sprite _idleButton;

    [Tooltip("한 번 더 누르면 교환 (확인) 버튼")]
    [SerializeField] private Sprite _confirmButton;

    public event Action<ItemDefinition> OnExchange;

    private ItemDefinition _item;
    private bool _affordable;
    private bool _confirming;

    private void Awake()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        SetConfirming(false);
    }

    public void Bind(GachaExchangeEntry entry, bool affordable)
    {
        _item = entry.Item;
        _affordable = affordable;
        _icon.sprite = entry.Item.Icon;
        _nameText.text = entry.Item.DisplayName;
        int tier = Mathf.Clamp(entry.Tier, 0, _rarityTags.Length - 1);
        _rarityTag.sprite = _rarityTags[tier];
        _rarityText.text = entry.Item.Rarity != null ? entry.Item.Rarity.DisplayName : "";
        _pastBadge.SetActive(entry.PastFeatured);
        _costText.text = entry.Cost.ToString("N0");
        SetConfirming(false);
    }

    private void HandleClick()
    {
        if (!_affordable)
        {
            OnExchange?.Invoke(_item); // 모자라다는 안내는 받는 쪽이
            return;
        }
        if (!_confirming)
        {
            SetConfirming(true);
            StopAllCoroutines();
            StartCoroutine(CancelLater());
            return;
        }
        SetConfirming(false);
        OnExchange?.Invoke(_item);
    }

    private IEnumerator CancelLater()
    {
        yield return GachaTween.Wait(ConfirmSeconds);
        SetConfirming(false);
    }

    private void SetConfirming(bool confirming)
    {
        _confirming = confirming;
        _costGroup.SetActive(!confirming);
        _confirmText.gameObject.SetActive(confirming);
        _buttonImage.sprite = confirming ? _confirmButton : _affordable ? _affordableButton : _idleButton;
    }
}
