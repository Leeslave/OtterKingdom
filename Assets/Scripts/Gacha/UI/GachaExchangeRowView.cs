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

    public event Action<SpecialOtterDefinition> OnExchange;

    private SpecialOtterDefinition _otter;
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
        _otter = entry.Otter;
        _affordable = affordable;
        _icon.sprite = entry.Icon;
        _nameText.text = $"{GachaRewardText.FragmentName(entry.Otter)} ×{entry.Count}";
        int tier = Mathf.Clamp(entry.Tier, 0, _rarityTags.Length - 1);
        _rarityTag.sprite = _rarityTags[tier];
        _rarityText.text = GachaRewardText.TierName(GachaRewardKind.Fragments, entry.Tier);
        _pastBadge.SetActive(false); // 지난 픽업 표시는 장난감 교환소 때 것 (특수 해달은 모두 같은 값)
        _costText.text = entry.Cost.ToString("N0");
        SetConfirming(false);
    }

    private void HandleClick()
    {
        if (!_affordable)
        {
            OnExchange?.Invoke(_otter); // 모자라다는 안내는 받는 쪽이
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
        OnExchange?.Invoke(_otter);
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
