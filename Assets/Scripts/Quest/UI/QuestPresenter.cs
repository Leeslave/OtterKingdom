using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 모델(QuestLog)과 퀘스트 화면을 연결한다. 위쪽은 왕국 레벨과 경험치.
/// 목록은 지금 열린 퀘스트만: 보상 받을 수 있는 것 → 일일 → 성장 → 완료(오늘 받은 일일 → 이번 레벨에 받은 성장).
/// 완료는 남은 퀘스트 아래에 [완료] + 체크 + 흐리게 표시된다 (QuestRowView). 레벨이 오르면 받은 성장 퀘스트는 목록에서 빠진다.
/// </summary>
public class QuestPresenter : MonoBehaviour
{
    [Header("목록")]
    [SerializeField] private QuestRowView _rowPrefab;
    [SerializeField] private Transform _rowParent;
    [Tooltip("열 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;

    [Header("왕국 레벨")]
    [Tooltip("\"Lv.3\"")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [Tooltip("\"120 / 300\" (최고 레벨이면 \"MAX\")")]
    [SerializeField] private TextMeshProUGUI _expText;
    [SerializeField] private ProgressBarView _expBar;

    [Header("버튼")]
    [SerializeField] private Button _claimAllButton;
    [SerializeField] private Image _claimAllImage;
    [SerializeField] private TextMeshProUGUI _claimAllLabel;
    [SerializeField] private Button _closeButton;

    [Header("모두 받기 모양")]
    [SerializeField] private Sprite _claimAllActiveSprite;
    [SerializeField] private Sprite _claimAllIdleSprite;
    [SerializeField] private Color _claimAllActiveTextColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _claimAllIdleTextColor = new Color32(0x9C, 0x7C, 0x66, 0xFF);

    [Header("화면")]
    [Tooltip("퀘스트 화면 루트 (배경 딤 + 열기/닫기 연출)")]
    [SerializeField] private UIPopupAnimator _screen;

    private readonly List<QuestRowView> _rows = new List<QuestRowView>();
    private readonly List<QuestDefinition> _ordered = new List<QuestDefinition>();

    private QuestManager _manager;
    private ProfileManager _profile;
    private bool _glyphsReady;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Close);
        _claimAllButton.onClick.AddListener(() => _manager.ClaimAll());
    }

    private void OnEnable()
    {
        _manager = QuestManager.Instance;
        _manager.OnChanged += Refresh;
        _profile = ProfileManager.Instance;
        if (_profile != null)
            _profile.OnExpChanged += Refresh;

        if (!_glyphsReady)
            PrepareGlyphs();

        Refresh();
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnChanged -= Refresh;
        if (_profile != null)
            _profile.OnExpChanged -= Refresh;
    }

    public void Open()
    {
        _screen.Show();
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close() => _screen.Hide();

    private void Refresh()
    {
        var log = _manager.Log;
        var quests = _manager.Database.Quests;

        // Quests는 이미 정렬 순서이므로 묶음별로 나눠 담기만 하면 안정 정렬이 된다
        _ordered.Clear();
        AddGroup(quests, q => log.GetStatus(q) == QuestStatus.Claimable);
        AddGroup(quests, q => q.Kind == QuestKind.Daily && log.GetStatus(q) == QuestStatus.InProgress);
        AddGroup(quests, q => q.Kind == QuestKind.Main && log.GetStatus(q) == QuestStatus.InProgress);
        // 완료는 남은 퀘스트 아래로 (오늘 받은 일일, 이번 레벨에 받은 성장만)
        AddGroup(quests, q => q.Kind == QuestKind.Daily && _manager.ShowsAsCompleted(q));
        AddGroup(quests, q => q.Kind == QuestKind.Main && _manager.ShowsAsCompleted(q));

        while (_rows.Count < _ordered.Count)
        {
            var row = Instantiate(_rowPrefab, _rowParent);
            row.OnClaimClicked += HandleClaimClicked;
            _rows.Add(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool used = i < _ordered.Count;
            _rows[i].gameObject.SetActive(used);
            if (used)
                _rows[i].Bind(_ordered[i], log.GetProgress(_ordered[i]), log.GetStatus(_ordered[i]), _manager.ExpFor(_ordered[i]));
        }

        RefreshLevel();

        bool canClaim = _manager.ClaimableCount > 0;
        _claimAllButton.interactable = canClaim;
        _claimAllImage.sprite = canClaim ? _claimAllActiveSprite : _claimAllIdleSprite;
        _claimAllLabel.color = canClaim ? _claimAllActiveTextColor : _claimAllIdleTextColor;
    }

    private void AddGroup(IReadOnlyList<QuestDefinition> quests, System.Predicate<QuestDefinition> match)
    {
        foreach (var quest in quests)
        {
            if (_manager.IsAvailable(quest) && match(quest))
                _ordered.Add(quest);
        }
    }

    private void RefreshLevel()
    {
        if (_profile == null)
            return;

        var progress = _profile.Progress;
        int need = _profile.LevelTable.ExpToNext(progress.Level);
        _levelText.text = $"Lv.{progress.Level}";
        _expText.text = need > 0 ? $"{progress.Exp:N0} / {need:N0}" : "MAX";
        _expBar.SetRatio(progress.Ratio(_profile.LevelTable));
    }

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (동적 폰트).
    // 스크롤 목록이 LateUpdate에서 레이아웃을 강제로 갱신하는 도중에 새 글자가 한꺼번에 추가되면
    // D3D12 에디터에서 GPU가 멈춰 크래시가 났다 (Unity 6000.3.8, 퀘스트 화면을 열 때 재현).
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789/ ,.보상 받기진행 중완료일일경험치LvMAX");
        foreach (var quest in _manager.Database.Quests)
            text.Append(quest.Title).Append(quest.Description);

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var label in _rowPrefab.GetComponentsInChildren<TMP_Text>(true))
            fonts.Add(label.font);

        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }

        _glyphsReady = true;
    }

    private void HandleClaimClicked(QuestRowView row)
    {
        _manager.TryClaim(row.Quest);
    }
}
