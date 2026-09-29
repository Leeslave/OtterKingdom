using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 모델(QuestLog)과 퀘스트 화면을 연결한다.
/// 목록 순서: 보상 받을 수 있는 것 → 진행 중 → 받은 것, 같은 상태끼리는 퀘스트 정렬 순서.
/// </summary>
public class QuestPresenter : MonoBehaviour
{
    [Header("목록")]
    [SerializeField] private QuestRowView _rowPrefab;
    [SerializeField] private Transform _rowParent;
    [Tooltip("열 때 맨 위로 되돌리기용")]
    [SerializeField] private ScrollRect _scrollRect;

    [Header("전체 진행")]
    [Tooltip("\"2 / 10\" (목표를 달성한 퀘스트 / 전체)")]
    [SerializeField] private TextMeshProUGUI _completedText;
    [SerializeField] private ProgressBarView _completedBar;

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

        if (!_glyphsReady)
            PrepareGlyphs();

        Refresh();
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnChanged -= Refresh;
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

        // Quests는 이미 정렬 순서이므로 상태별로 나눠 담기만 하면 안정 정렬이 된다
        _ordered.Clear();
        foreach (var status in new[] { QuestStatus.Claimable, QuestStatus.InProgress, QuestStatus.Claimed })
        {
            foreach (var quest in quests)
            {
                if (log.GetStatus(quest) == status)
                    _ordered.Add(quest);
            }
        }

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
                _rows[i].Bind(_ordered[i], log.GetProgress(_ordered[i]), log.GetStatus(_ordered[i]));
        }

        int claimable = log.Count(quests, QuestStatus.Claimable);
        int completed = claimable + log.Count(quests, QuestStatus.Claimed);
        _completedText.text = $"{completed} / {quests.Count}";
        _completedBar.SetProgress(completed, quests.Count);

        bool canClaim = claimable > 0;
        _claimAllButton.interactable = canClaim;
        _claimAllImage.sprite = canClaim ? _claimAllActiveSprite : _claimAllIdleSprite;
        _claimAllLabel.color = canClaim ? _claimAllActiveTextColor : _claimAllIdleTextColor;
    }

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (동적 폰트).
    // 스크롤 목록이 LateUpdate에서 레이아웃을 강제로 갱신하는 도중에 새 글자가 한꺼번에 추가되면
    // D3D12 에디터에서 GPU가 멈춰 크래시가 났다 (Unity 6000.3.8, 퀘스트 화면을 열 때 재현).
    private void PrepareGlyphs()
    {
        var text = new StringBuilder("0123456789/ ,보상 받기진행 중완료");
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
