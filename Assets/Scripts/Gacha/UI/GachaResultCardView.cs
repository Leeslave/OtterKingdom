using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>결과 화면의 [한 번 더] 버튼 모양 (값 치르는 방법에 따라 색·아이콘·값이 바뀜)</summary>
public readonly struct GachaAgainInfo
{
    public readonly string Label;
    public readonly Sprite Button;
    public readonly Sprite Icon;
    public readonly string Cost;

    public GachaAgainInfo(string label, Sprite button, Sprite icon, string cost)
    {
        Label = label;
        Button = button;
        Icon = icon;
        Cost = cost;
    }
}

/// <summary>
/// 1회 뽑기 결과 카드: 등급 · NEW · 픽업 표시, 이름·설명, "광장에 두면 ○○이 놀러 와요" 안내, [확인] · [한 번 더].
/// 아래에서 올라온다 (연출: GachaRevealView)
/// </summary>
public class GachaResultCardView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("올라오는 카드 패널")]
    [SerializeField] private RectTransform _panel;

    [SerializeField] private CanvasGroup _group;

    [Header("등급 · 표시")]
    [SerializeField] private Image _rarityTag;

    [SerializeField] private TextMeshProUGUI _rarityText;

    [Tooltip("처음 얻은 장난감 표시")]
    [SerializeField] private GameObject _newBadge;

    [Tooltip("픽업 장난감 표시")]
    [SerializeField] private GameObject _featuredBadge;

    [Tooltip("흔함 · 레어 · 에픽 순 등급 칩 그림")]
    [SerializeField] private Sprite[] _rarityTags = new Sprite[3];

    [Header("글")]
    [SerializeField] private TextMeshProUGUI _nameText;

    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Tooltip("광장에 두면 누가 놀러 오는지")]
    [SerializeField] private TextMeshProUGUI _hintText;

    [Tooltip("한정 해달 얼굴 (없으면 감춤)")]
    [SerializeField] private Image _hintPortrait;

    [SerializeField] private GameObject _hintPortraitFrame;

    [Tooltip("겹쳐서 받은 반짝 조각")]
    [SerializeField] private TextMeshProUGUI _shardText;

    [Header("버튼")]
    [SerializeField] private Button _okButton;

    [SerializeField] private Button _againButton;

    [SerializeField] private Image _againImage;

    [SerializeField] private Image _againIcon;

    [SerializeField] private TextMeshProUGUI _againLabel;

    [SerializeField] private TextMeshProUGUI _againCost;

    [Header("움직임")]
    [Tooltip("올라오는 거리 (px)")]
    [SerializeField] private float _slideDistance = 420f;

    public event Action OnOk;
    public event Action OnAgain;

    private Vector2 _restPosition;
    private bool _positionKnown;

    private void Awake()
    {
        _okButton.onClick.AddListener(() => OnOk?.Invoke());
        _againButton.onClick.AddListener(() => OnAgain?.Invoke());
    }

    public void Show(GachaPull pull, string hint, Sprite portrait, GachaAgainInfo again, bool animate)
    {
        if (!_positionKnown)
        {
            _restPosition = _panel.anchoredPosition;
            _positionKnown = true;
        }

        var item = pull.Item;
        int tier = Mathf.Clamp(pull.Tier, 0, _rarityTags.Length - 1);
        _rarityTag.sprite = _rarityTags[tier];
        _rarityText.text = item.Rarity != null ? item.Rarity.DisplayName : "흔함";
        _newBadge.SetActive(pull.IsNew);
        _featuredBadge.SetActive(pull.Featured);
        _nameText.text = item.DisplayName;
        _descriptionText.text = item.Description;
        _hintText.text = hint;
        _hintPortraitFrame.SetActive(portrait != null);
        _hintPortrait.sprite = portrait;
        _shardText.text = pull.Shards > 0 ? $"이미 있는 장난감이라 반짝 조각 +{pull.Shards}" : "";

        _againLabel.text = again.Label;
        _againImage.sprite = again.Button;
        _againIcon.sprite = again.Icon;
        _againIcon.gameObject.SetActive(again.Icon != null);
        _againCost.text = again.Cost;

        gameObject.SetActive(true);
        if (!animate)
        {
            _panel.anchoredPosition = _restPosition;
            _group.alpha = 1f;
            return;
        }
        StopAllCoroutines();
        StartCoroutine(GachaTween.Run(0.4f, t =>
        {
            float k = GachaTween.OutBack(t, 1.2f);
            _panel.anchoredPosition = _restPosition + Vector2.down * (_slideDistance * (1f - k));
            _group.alpha = Mathf.Clamp01(t * 2.5f);
        }));
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (_positionKnown)
            _panel.anchoredPosition = _restPosition;
        gameObject.SetActive(false);
    }
}
