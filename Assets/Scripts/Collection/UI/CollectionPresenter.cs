using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 모델(Collection)과 뷰들을 연결한다. 선택한 탭과 항목은 여기 한 곳에서만 관리한다.
/// </summary>
public class CollectionPresenter : MonoBehaviour
{
    [Header("뷰")]
    [SerializeField] private CollectionDetailView _detailView;
    [SerializeField] private CollectionTabView _tabPrefab;
    [SerializeField] private Transform _tabParent;
    [SerializeField] private CollectionSlotView _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [Tooltip("\"수집 6/12\"")]
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private CollectionLegendView _legend;
    [Tooltip("탭을 바꿀 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _closeButton;

    [Header("화면")]
    [Tooltip("도감 화면 루트 (배경 딤 + 열기/닫기 연출)")]
    [SerializeField] private UIPopupAnimator _screen;
    [Tooltip("이야기(컷씬) 재생 화면 (도감 화면 위에 뜸)")]
    [SerializeField] private CollectionStoryPlayerView _storyPlayer;

    private readonly List<CollectionTabView> _tabs = new List<CollectionTabView>();
    private readonly List<CollectionSlotView> _slots = new List<CollectionSlotView>();
    private List<CollectionEntry> _entries = new List<CollectionEntry>();

    private Collection _collection;
    private CollectionDatabase _database;
    private CollectionTab _selectedTab;
    private CollectionEntry _selectedEntry;
    private CollectionEntry _playingEntry;
    private bool _isBuilt;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        _collection = CollectionManager.Instance.Collection;
        _database = CollectionManager.Instance.Database;

        if (!_isBuilt)
            BuildTabs();

        _collection.OnStateChanged += HandleStateChanged;
        _collection.OnStoriesChanged += RefreshAll;
        _detailView.OnStoryClicked += PlaySelectedStory;
        _storyPlayer.OnFinished += HandleStoryFinished;
        RefreshAll();
    }

    private void OnDisable()
    {
        _detailView.OnStoryClicked -= PlaySelectedStory;
        _storyPlayer.OnFinished -= HandleStoryFinished;

        if (_collection == null)
            return;
        _collection.OnStateChanged -= HandleStateChanged;
        _collection.OnStoriesChanged -= RefreshAll;
    }

    public void Open() => _screen.Show();
    public void Close() => _screen.Hide();

    private void BuildTabs()
    {
        var tabs = new List<CollectionTab>();
        foreach (var tab in _database.Tabs)
        {
            if (tab != null)
                tabs.Add(tab);
        }
        tabs.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

        foreach (var tab in tabs)
        {
            var view = Instantiate(_tabPrefab, _tabParent);
            view.Bind(tab);
            view.OnClicked += HandleTabClicked;
            _tabs.Add(view);
        }

        _selectedTab = tabs.Count > 0 ? tabs[0] : null;
        _isBuilt = true;
    }

    private void RefreshAll()
    {
        _entries = _selectedTab != null ? _database.GetEntries(_selectedTab) : new List<CollectionEntry>();

        while (_slots.Count < _entries.Count)
        {
            var slot = Instantiate(_slotPrefab, _slotParent);
            slot.OnClicked += HandleSlotClicked;
            _slots.Add(slot);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < _entries.Count;
            _slots[i].gameObject.SetActive(used);
            if (used)
                _slots[i].Bind(_entries[i], _collection.GetState(_entries[i]), _collection.HasNewStory(_entries[i]));
        }

        // 선택했던 항목이 이 탭에 없으면 첫 항목으로
        if (_selectedEntry == null || !_entries.Contains(_selectedEntry))
            _selectedEntry = _entries.Count > 0 ? _entries[0] : null;

        foreach (var tab in _tabs)
        {
            tab.SetSelected(tab.Tab == _selectedTab);
            tab.SetNew(_collection.CountNewStories(_database.GetEntries(tab.Tab)) > 0);
        }

        _countText.text = BuildCountText();
        _legend.Show(_selectedTab);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (var slot in _slots)
        {
            if (slot.gameObject.activeSelf)
                slot.SetSelected(slot.Entry == _selectedEntry);
        }

        if (_selectedEntry == null)
        {
            _detailView.ShowNothing();
            return;
        }

        var state = _collection.GetState(_selectedEntry);
        _detailView.Show(_selectedEntry, state);
        _detailView.ShowStory(_selectedEntry.HasStory && state == CollectionState.Collected, _collection.HasNewStory(_selectedEntry));
    }

    // "수집 6/12" 또는 해달처럼 방문 흔적을 쓰면 "등록 4/12 · 방문 흔적 3"
    private string BuildCountText()
    {
        if (_selectedTab == null)
            return "";

        int collected = _collection.Count(_entries, CollectionState.Collected);
        string text = $"{_selectedTab.CountLabel} {collected}/{_entries.Count}";

        if (_selectedTab.SupportsVisits)
            text += $" · 방문 흔적 {_collection.Count(_entries, CollectionState.Visited)}";

        return text;
    }

    private void HandleStateChanged(CollectionEntry entry, CollectionState state)
    {
        // 열려 있는 동안 획득해도 바로 반영 (다른 탭 항목이어도 개수와 상태가 맞도록 전체 갱신, 항목 수가 적어 부담 없음)
        RefreshAll();
    }

    private void HandleTabClicked(CollectionTabView view)
    {
        if (view.Tab == _selectedTab)
            return;

        _selectedTab = view.Tab;
        _selectedEntry = null;
        RefreshAll();

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void HandleSlotClicked(CollectionSlotView slot)
    {
        _selectedEntry = slot.Entry;
        RefreshSelection();

        // 안 본 이야기는 누르자마자 재생 (본 뒤에는 [다시 보기]로)
        if (_collection.HasNewStory(_selectedEntry))
            PlayStory(_selectedEntry);
    }

    #region 이야기

    private void PlaySelectedStory()
    {
        if (_selectedEntry != null && _selectedEntry.HasStory)
            PlayStory(_selectedEntry);
    }

    private void PlayStory(CollectionEntry entry)
    {
        if (_storyPlayer.IsPlaying)
            return;

        var story = entry.Story;
        string playerName = ProfileManager.Instance.Profile.Name;

        // 컷 그림이 아직 없으면 항목 그림(아이콘)을 크게 띄운 임시 컷
        var cuts = new List<Sprite>();
        for (int i = 0; i < Mathf.Max(1, story.Cuts.Count); i++)
        {
            var cut = i < story.Cuts.Count ? story.Cuts[i] : null;
            cuts.Add(cut != null ? cut : entry.Portrait);
        }

        var lines = new List<(int cut, string text)>();
        foreach (var line in story.Lines)
        {
            if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                lines.Add((line.Cut, CollectionStory.FormatLine(line.Text, playerName)));
        }
        if (lines.Count == 0)
            return;

        _playingEntry = entry;
        _storyPlayer.Play(cuts, lines);
        AudioManager.Instance.PlayChime();
    }

    // 끝까지 봤거나 건너뜀 → 본 것으로 (N 해제는 OnStoriesChanged → RefreshAll)
    private void HandleStoryFinished()
    {
        if (_playingEntry == null)
            return;

        _collection.MarkStoryWatched(_playingEntry);
        _playingEntry = null;
    }

    #endregion
}
