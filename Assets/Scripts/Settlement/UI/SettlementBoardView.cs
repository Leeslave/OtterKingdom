using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "해달 게시판" 팝업: [방명록] / [해달의 부탁] 두 탭.
/// 방명록 = 기록 카드 목록 + 방문 기록 수 + 아래쪽 "새로운 부탁이 도착했어요 [부탁 보기]" 알림.
/// 부탁 = 부탁 카드 목록. 받은 값만 그리고 카드·알림 클릭을 알린다.
/// </summary>
public class SettlementBoardView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("탭")]
    [SerializeField] private Button _guestbookTab;
    [SerializeField] private Image _guestbookTabImage;
    [SerializeField] private Button _requestTab;
    [SerializeField] private Image _requestTabImage;
    [Tooltip("시작할 수 있는 부탁이 있을 때 부탁 탭의 빨간 점")]
    [SerializeField] private GameObject _requestDot;
    [SerializeField] private Sprite _tabSelectedSprite;
    [SerializeField] private Sprite _tabNormalSprite;

    [Header("방명록")]
    [SerializeField] private GameObject _guestbookPage;
    [SerializeField] private GuestbookCardView _guestbookCardPrefab;
    [SerializeField] private Transform _guestbookParent;
    [Tooltip("\"방문 기록 3\"")]
    [SerializeField] private TextMeshProUGUI _visitCountText;
    [Tooltip("새 부탁 알림 줄")]
    [SerializeField] private GameObject _notice;
    [SerializeField] private Image _noticePortrait;
    [SerializeField] private TextMeshProUGUI _noticeText;
    [SerializeField] private Button _noticeButton;

    [Header("해달의 부탁")]
    [SerializeField] private GameObject _requestPage;
    [SerializeField] private BoardRequestCardView _requestCardPrefab;
    [SerializeField] private Transform _requestParent;
    [Tooltip("보일 부탁이 없을 때")]
    [SerializeField] private GameObject _emptyRequests;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    private readonly List<GuestbookCardView> _guestbookCards = new List<GuestbookCardView>();
    private readonly List<BoardRequestCardView> _requestCards = new List<BoardRequestCardView>();

    public bool IsOpen => _animator.IsOpen;
    public bool IsRequestTab { get; private set; }

    public event Action<BoardRequestDefinition> OnRequestClicked;
    /// <summary>탭이 바뀌었을 때 (presenter가 그 탭 내용을 채움)</summary>
    public event Action<bool> OnTabChanged;

    private void Awake()
    {
        _guestbookTab.onClick.AddListener(() => SelectTab(false));
        _requestTab.onClick.AddListener(() => SelectTab(true));
        _noticeButton.onClick.AddListener(() => SelectTab(true));
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    public void Show(bool requestTab)
    {
        SelectTab(requestTab);
        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    private void SelectTab(bool requestTab)
    {
        IsRequestTab = requestTab;
        _guestbookTabImage.sprite = requestTab ? _tabNormalSprite : _tabSelectedSprite;
        _requestTabImage.sprite = requestTab ? _tabSelectedSprite : _tabNormalSprite;
        _guestbookPage.SetActive(!requestTab);
        _requestPage.SetActive(requestTab);
        OnTabChanged?.Invoke(requestTab);
    }

    public void SetRequestDot(bool visible) => _requestDot.SetActive(visible);

    /// <summary>방명록: 최근 기록이 위로</summary>
    public void BindGuestbook(IReadOnlyList<GuestbookEntryDefinition> entries)
    {
        while (_guestbookCards.Count < entries.Count)
            _guestbookCards.Add(Instantiate(_guestbookCardPrefab, _guestbookParent));

        for (int i = 0; i < _guestbookCards.Count; i++)
        {
            bool used = i < entries.Count;
            _guestbookCards[i].gameObject.SetActive(used);
            if (used)
                _guestbookCards[i].Bind(entries[entries.Count - 1 - i]);
        }
        _visitCountText.text = $"방문 기록 {entries.Count}";
    }

    /// <summary>새 부탁 알림 (portrait가 null이면 숨김이 아니라 그림만 비움). message가 비면 알림 줄을 숨김</summary>
    public void SetNotice(string message, Sprite portrait)
    {
        bool visible = !string.IsNullOrEmpty(message);
        _notice.SetActive(visible);
        if (!visible)
            return;
        _noticeText.text = message;
        _noticePortrait.sprite = portrait;
        _noticePortrait.enabled = portrait != null;
    }

    public void BindRequests(IReadOnlyList<(BoardRequestDefinition request, RequestStatus status, string time)> rows)
    {
        while (_requestCards.Count < rows.Count)
        {
            var card = Instantiate(_requestCardPrefab, _requestParent);
            card.OnClicked += c => OnRequestClicked?.Invoke(c.Request);
            _requestCards.Add(card);
        }

        for (int i = 0; i < _requestCards.Count; i++)
        {
            bool used = i < rows.Count;
            _requestCards[i].gameObject.SetActive(used);
            if (used)
                _requestCards[i].Bind(rows[i].request, rows[i].status, rows[i].time);
        }
        _emptyRequests.SetActive(rows.Count == 0);
    }
}
