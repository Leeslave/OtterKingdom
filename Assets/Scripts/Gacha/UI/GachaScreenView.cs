using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보물 조개 뽑기 화면: 배너 탭 · 배너 그림(기간 · 이름) · 확률 UP 장난감 · 에픽 확정까지 남은 수 ·
/// 별빛 포인트(픽업) 또는 오늘의 골드 뽑기(상시) · 확률 정보 · 교환소 · [1회] [10회].
/// 받은 값(GachaScreenState)을 그리기만 하고, 누르면 알린다 (GachaPresenter가 처리)
/// </summary>
public class GachaScreenView : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private UIPopupAnimator _animator;

    [SerializeField] private Button _closeButton;

    [Header("배너 탭")]
    [SerializeField] private RectTransform _tabParent;

    [Tooltip("복제해서 쓰는 탭 (꺼 둠)")]
    [SerializeField] private GachaBannerTabView _tabTemplate;

    [Header("배너")]
    [SerializeField] private Image _bannerArt;

    [SerializeField] private TextMeshProUGUI _titleText;

    [SerializeField] private TextMeshProUGUI _subtitleText;

    [Tooltip("기간 칩 (픽업: 남은 날, 상시: 상시)")]
    [SerializeField] private Image _periodChip;

    [SerializeField] private TextMeshProUGUI _periodText;

    [Tooltip("픽업 기간 칩 그림")]
    [SerializeField] private Sprite _limitedChipSprite;

    [Tooltip("상시 칩 그림")]
    [SerializeField] private Sprite _alwaysChipSprite;

    [Tooltip("픽업 배너의 \"확률 UP!\" 표시")]
    [SerializeField] private GameObject _rateUpBadge;

    [Header("눈여겨볼 장난감")]
    [SerializeField] private TextMeshProUGUI _highlightLabel;

    [SerializeField] private GachaToyChipView[] _highlightChips = new GachaToyChipView[3];

    [Header("천장")]
    [SerializeField] private TextMeshProUGUI _pityText;

    [SerializeField] private ProgressBarView _pityBar;

    [Tooltip("다음 에픽은 픽업 확정 표시")]
    [SerializeField] private GameObject _guaranteeChip;

    [Header("별빛 포인트 (픽업)")]
    [SerializeField] private GameObject _pointsGroup;

    [SerializeField] private TextMeshProUGUI _pointsText;

    [SerializeField] private ProgressBarView _pointsBar;

    [SerializeField] private Image _pointsRewardIcon;

    [SerializeField] private Button _pointsButton;

    [SerializeField] private Image _pointsButtonImage;

    [Header("골드 뽑기 (상시)")]
    [SerializeField] private GameObject _goldGroup;

    [SerializeField] private TextMeshProUGUI _goldLabel;

    [SerializeField] private Button _goldButton;

    [SerializeField] private Image _goldButtonImage;

    [SerializeField] private TextMeshProUGUI _goldCostText;

    [Header("작은 버튼 · 보유")]
    [SerializeField] private Button _ratesButton;

    [SerializeField] private Button _exchangeButton;

    [SerializeField] private TextMeshProUGUI _shardText;

    [SerializeField] private Image _ticketIcon;

    [SerializeField] private TextMeshProUGUI _ticketText;

    [Header("뽑기 버튼")]
    [SerializeField] private Button _singleButton;

    [SerializeField] private Image _singleImage;

    [SerializeField] private Image _singleIcon;

    [SerializeField] private TextMeshProUGUI _singleCost;

    [SerializeField] private Button _tenButton;

    [SerializeField] private Image _tenImage;

    [SerializeField] private Image _tenIcon;

    [SerializeField] private TextMeshProUGUI _tenCost;

    [Header("알림")]
    [Tooltip("요정 말풍선 (선물 · 모자람 안내). 잠깐 떴다 사라짐")]
    [SerializeField] private CanvasGroup _notice;

    [SerializeField] private TextMeshProUGUI _noticeText;

    [Tooltip("말풍선이 머무는 시간 (초)")]
    [SerializeField] private float _noticeSeconds = 2.4f;

    [Header("버튼 그림")]
    [Tooltip("누를 수 있는 초록 버튼 (교환)")]
    [SerializeField] private Sprite _activeButton;

    [Tooltip("누를 수 없는 회색 버튼")]
    [SerializeField] private Sprite _idleButton;

    [Tooltip("골드 버튼 (겨자색)")]
    [SerializeField] private Sprite _coinButton;

    [Header("색")]
    [SerializeField] private Color _costColor = new Color(0.294f, 0.18f, 0.133f, 1f);

    [Tooltip("값이 모자랄 때 숫자 색")]
    [SerializeField] private Color _shortColor = new Color(0.9f, 0.28f, 0.3f, 1f);

    public event Action OnCloseClicked;
    public event Action<GachaBannerDefinition> OnBannerSelected;
    public event Action<int> OnPullClicked;
    public event Action OnGoldPullClicked;
    public event Action OnRatesClicked;
    public event Action OnExchangeClicked;
    public event Action OnPointsClicked;

    private readonly List<GachaBannerTabView> _tabs = new List<GachaBannerTabView>();
    private Coroutine _noticeRoutine;

    public bool IsOpen => _animator.IsOpen;
    public UIPopupAnimator Animator => _animator;

    private void Awake()
    {
        _tabTemplate.gameObject.SetActive(false);
        _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
        _singleButton.onClick.AddListener(() => OnPullClicked?.Invoke(1));
        _tenButton.onClick.AddListener(() => OnPullClicked?.Invoke(10));
        _goldButton.onClick.AddListener(() => OnGoldPullClicked?.Invoke());
        _ratesButton.onClick.AddListener(() => OnRatesClicked?.Invoke());
        _exchangeButton.onClick.AddListener(() => OnExchangeClicked?.Invoke());
        _pointsButton.onClick.AddListener(() => OnPointsClicked?.Invoke());
    }

    public void Show()
    {
        _notice.alpha = 0f;
        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    /// <summary>요정 말풍선으로 잠깐 알림</summary>
    public void Notify(string text)
    {
        _noticeText.text = text;
        if (_noticeRoutine != null)
            StopCoroutine(_noticeRoutine);
        if (isActiveAndEnabled)
            _noticeRoutine = StartCoroutine(NoticeRoutine());
    }

    private IEnumerator NoticeRoutine()
    {
        var rect = (RectTransform)_notice.transform;
        yield return GachaTween.Run(0.2f, t =>
        {
            _notice.alpha = t;
            rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, GachaTween.OutBack(t));
        });
        yield return GachaTween.Wait(_noticeSeconds);
        yield return GachaTween.Run(0.25f, t => _notice.alpha = 1f - t);
        _noticeRoutine = null;
    }

    public void Render(GachaScreenState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        RenderTabs(state);
        var banner = state.Selected;
        if (banner == null)
            return;

        _bannerArt.sprite = banner.BannerArt;
        _titleText.text = banner.Title;
        _subtitleText.text = banner.Subtitle;
        _periodChip.sprite = state.Limited ? _limitedChipSprite : _alwaysChipSprite;
        _periodText.text = state.Period;
        _periodText.color = state.Limited ? Color.white : _costColor;
        _rateUpBadge.SetActive(banner.IsPickup);

        _highlightLabel.text = state.HighlightLabel;
        for (int i = 0; i < _highlightChips.Length; i++)
        {
            if (i < state.Highlights.Count)
                _highlightChips[i].Show(state.Highlights[i].item, state.Highlights[i].tier, state.Highlights[i].featured);
            else
                _highlightChips[i].Show(null, 0, false);
        }

        _pityText.text = $"에픽 확정까지 <b>{state.PityLeft}회</b>";
        _pityBar.SetProgress(state.PityTotal - state.PityLeft, state.PityTotal);
        _guaranteeChip.SetActive(state.Guaranteed);

        _pointsGroup.SetActive(state.ShowPoints);
        if (state.ShowPoints)
        {
            _pointsText.text = $"별빛 포인트 <b>{state.Points}</b> / {state.PointsGoal}";
            _pointsBar.SetProgress(Mathf.Min(state.Points, state.PointsGoal), state.PointsGoal);
            _pointsRewardIcon.sprite = state.PointsReward != null ? state.PointsReward.Icon : null;
            bool ready = state.Points >= state.PointsGoal;
            _pointsButtonImage.sprite = ready ? _activeButton : _idleButton;
        }

        _goldGroup.SetActive(state.ShowGold);
        if (state.ShowGold)
        {
            _goldLabel.text = state.GoldAvailable ? "오늘의 골드 뽑기" : "골드 뽑기는 내일 다시!";
            _goldButtonImage.sprite = state.GoldAvailable ? _coinButton : _idleButton;
            _goldCostText.text = NumberFormatter.Short(state.GoldCost);
            _goldCostText.color = state.GoldAvailable && !state.GoldAffordable ? _shortColor : _costColor;
        }

        _shardText.text = state.Shards.ToString("N0");
        _ticketIcon.sprite = state.TicketIcon;
        _ticketText.text = state.Tickets.ToString("N0");

        ApplyPrice(_singleImage, _singleIcon, _singleCost, state.Single);
        ApplyPrice(_tenImage, _tenIcon, _tenCost, state.Ten);
    }

    private static void ApplyPrice(Image button, Image icon, TextMeshProUGUI cost, GachaPriceLook look)
    {
        button.sprite = look.Button;
        icon.sprite = look.Icon;
        icon.gameObject.SetActive(look.Icon != null);
        cost.text = look.Cost;
    }

    // 배너 수만큼 탭을 맞춤 (복제본을 재사용)
    private void RenderTabs(GachaScreenState state)
    {
        while (_tabs.Count < state.Banners.Count)
        {
            var tab = Instantiate(_tabTemplate, _tabParent);
            tab.OnClicked += banner => OnBannerSelected?.Invoke(banner);
            _tabs.Add(tab);
        }
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool used = i < state.Banners.Count;
            _tabs[i].gameObject.SetActive(used);
            if (used)
            {
                var banner = state.Banners[i];
                _tabs[i].Bind(banner, banner == state.Selected, state.NewBanners.Contains(banner));
            }
        }
    }
}
