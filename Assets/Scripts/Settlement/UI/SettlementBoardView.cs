using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>게시판 부탁 탭의 한 줄: 구역 제목, 부탁 카드, 화면으로 가는 카드(큰 부탁·발전 현황)</summary>
public readonly struct BoardRow
{
    public enum RowKind { Header, Request, Entry }

    public readonly RowKind Kind;
    public readonly string Text;          // 구역 제목 / 화면 카드 제목
    public readonly BoardRequestDefinition Request;
    public readonly RequestStatus Status;
    public readonly string Time;          // 진행 중 남은 시간
    public readonly string EntryKey;      // 화면 카드 키
    public readonly Sprite Icon;
    public readonly string Description;
    public readonly string ButtonLabel;

    private BoardRow(RowKind kind, string text, BoardRequestDefinition request, RequestStatus status, string time,
        string entryKey, Sprite icon, string description, string buttonLabel)
    {
        Kind = kind;
        Text = text;
        Request = request;
        Status = status;
        Time = time;
        EntryKey = entryKey;
        Icon = icon;
        Description = description;
        ButtonLabel = buttonLabel;
    }

    public static BoardRow Header(string text) => new BoardRow(RowKind.Header, text, null, RequestStatus.Locked, null, null, null, null, null);

    public static BoardRow ForRequest(BoardRequestDefinition request, RequestStatus status, string time) =>
        new BoardRow(RowKind.Request, null, request, status, time, null, null, null, null);

    public static BoardRow Entry(string key, Sprite icon, string title, string description, string buttonLabel) =>
        new BoardRow(RowKind.Entry, title, null, RequestStatus.Available, null, key, icon, description, buttonLabel);
}

/// <summary>
/// "해달 게시판" 팝업: [방명록] / [해달의 부탁] 두 탭.
/// 방명록 = 기록 카드 목록 + 방문 기록 수 + 아래쪽 "새로운 부탁이 도착했어요 [부탁 보기]" 알림.
/// 부탁 = 부탁 카드 목록 (관리 해달이 맡은 뒤에는 메인 / 주민 부탁 / 완료 구역 제목과 큰 부탁·발전 현황 카드가 섞임).
/// 받은 값만 그리고 카드·알림 클릭을 알린다.
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

    [Tooltip("구역 제목 템플릿 (메인 발전 / 주민 부탁 / 완료한 부탁)")]
    [SerializeField] private TextMeshProUGUI _sectionHeaderPrefab;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    private readonly List<GuestbookCardView> _guestbookCards = new List<GuestbookCardView>();
    private readonly List<BoardRequestCardView> _requestCards = new List<BoardRequestCardView>();
    private readonly List<TextMeshProUGUI> _headers = new List<TextMeshProUGUI>();

    public bool IsOpen => _animator.IsOpen;
    public bool IsRequestTab { get; private set; }

    public event Action<BoardRequestDefinition> OnRequestClicked;
    /// <summary>화면 카드(큰 부탁·발전 현황)를 눌렀을 때. 인자: 카드 키</summary>
    public event Action<string> OnEntryClicked;
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

    /// <summary>부탁 탭 목록을 순서대로 그림 (구역 제목·부탁 카드·화면 카드)</summary>
    public void BindRows(IReadOnlyList<BoardRow> rows)
    {
        int cardsUsed = 0;
        int headersUsed = 0;
        bool anyRequest = false;
        foreach (var row in rows)
        {
            if (row.Kind == BoardRow.RowKind.Header)
            {
                var header = NextHeader(headersUsed++);
                header.text = row.Text;
                header.transform.SetAsLastSibling();
                continue;
            }

            var card = NextCard(cardsUsed++);
            if (row.Kind == BoardRow.RowKind.Entry)
                card.BindEntry(row.EntryKey, row.Icon, row.Text, row.Description, row.ButtonLabel);
            else
            {
                card.Bind(row.Request, row.Status, row.Time);
                anyRequest = true;
            }
            card.transform.SetAsLastSibling();
        }

        for (int i = cardsUsed; i < _requestCards.Count; i++)
            _requestCards[i].gameObject.SetActive(false);
        for (int i = headersUsed; i < _headers.Count; i++)
            _headers[i].gameObject.SetActive(false);
        _emptyRequests.SetActive(!anyRequest && cardsUsed == 0);
    }

    private BoardRequestCardView NextCard(int index)
    {
        if (index >= _requestCards.Count)
        {
            var created = Instantiate(_requestCardPrefab, _requestParent);
            created.OnClicked += HandleCardClicked;
            _requestCards.Add(created);
        }
        var card = _requestCards[index];
        card.gameObject.SetActive(true);
        return card;
    }

    private TextMeshProUGUI NextHeader(int index)
    {
        if (index >= _headers.Count)
            _headers.Add(Instantiate(_sectionHeaderPrefab, _requestParent));
        var header = _headers[index];
        header.gameObject.SetActive(true);
        return header;
    }

    private void HandleCardClicked(BoardRequestCardView card)
    {
        if (card.Request != null)
            OnRequestClicked?.Invoke(card.Request);
        else if (!string.IsNullOrEmpty(card.EntryKey))
            OnEntryClicked?.Invoke(card.EntryKey);
    }
}
