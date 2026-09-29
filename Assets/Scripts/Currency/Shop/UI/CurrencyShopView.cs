using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 충전 화면 하나 (골드 충전 / 조개 충전): 보유 줄, 상품 카드 목록, 안내 문구. 카드를 누르면 알리기만 한다.
/// </summary>
public class CurrencyShopView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("상품")]
    [SerializeField] private CurrencyPackCardView _cardPrefab;
    [SerializeField] private Transform _cardParent;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    [Header("잠깐 뜨는 안내 (예: 결제 준비 중)")]
    [SerializeField] private CanvasGroup _notice;
    [SerializeField] private TextMeshProUGUI _noticeText;
    [SerializeField] private float _noticeDuration = 1.6f;

    public bool IsOpen => _animator.IsOpen;

    public event Action<CurrencyPack> OnPackClicked;

    private readonly List<CurrencyPackCardView> _cards = new List<CurrencyPackCardView>();
    private Coroutine _noticeRoutine;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Hide);
        _notice.alpha = 0f;
    }

    public void Show(IReadOnlyList<CurrencyPack> packs)
    {
        if (packs == null)
            throw new ArgumentNullException(nameof(packs));

        while (_cards.Count < packs.Count)
        {
            var card = Instantiate(_cardPrefab, _cardParent);
            card.OnBuyClicked += c => OnPackClicked?.Invoke(c.Pack);
            _cards.Add(card);
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            bool used = i < packs.Count;
            _cards[i].gameObject.SetActive(used);
            if (used)
                _cards[i].Bind(packs[i]);
        }

        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    public void ShowNotice(string message)
    {
        _noticeText.text = message;
        if (_noticeRoutine != null)
            StopCoroutine(_noticeRoutine);
        _noticeRoutine = StartCoroutine(NoticeRoutine());
    }

    private IEnumerator NoticeRoutine()
    {
        _notice.alpha = 1f;
        yield return new WaitForSecondsRealtime(_noticeDuration);

        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            _notice.alpha = 1f - t / 0.25f;
            yield return null;
        }

        _notice.alpha = 0f;
        _noticeRoutine = null;
    }

    private void OnDisable()
    {
        // 닫히는 동안 안내가 남아 있으면 다음에 열 때 보이지 않게
        _notice.alpha = 0f;
        _noticeRoutine = null;
    }
}
