using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마을회관 발전 현황 화면: 머리말(정착 단계 · 마을 발전 막대) 아래로 카드가 내용 길이만큼 쌓인다 (스크롤).
/// 주민 현황 · 지금 하는 일 · 다음 목표 [가 보기](또는 모두 마침 + 생산 · 꾸미기 · 도감) · 왕국 질서도 [이사하기][구역 정하기] · 영토 확장 [가 보기] · 완료한 발전(칩).
/// 왕국 레벨·정착 단계·시설은 서로 다른 숫자라 섞지 않는다 (여기는 정착 단계와 발전 기록만). 받은 값만 그리고 클릭을 알린다.
/// 만들기: Tools/Settlement/Rebuild District UI (SettlementSetup.BuildTownHallPopup)
/// </summary>
public class TownHallPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Tooltip("카드 목록 (열 때 맨 위로)")]
    [SerializeField] private ScrollRect _scroll;

    [Header("머리말")]
    [Tooltip("정착 단계 (예: 05 · 작은 마을)")]
    [SerializeField] private TextMeshProUGUI _stageText;
    [Tooltip("메인 발전 진행 (예: 마을 발전 10 / 12)")]
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private ProgressBarView _progressBar;

    [Header("주민 현황")]
    [SerializeField] private TextMeshProUGUI _settledText;
    [SerializeField] private TextMeshProUGUI _availableText;
    [SerializeField] private TextMeshProUGUI _workingText;

    [Header("지금 하는 일")]
    [SerializeField] private TextMeshProUGUI _workText;

    [Header("다음 목표")]
    [SerializeField] private GameObject _nextGroup;
    [SerializeField] private TextMeshProUGUI _nextTitleText;
    [SerializeField] private TextMeshProUGUI _nextDescriptionText;
    [SerializeField] private Button _goButton;

    [Header("모두 마침")]
    [SerializeField] private GameObject _doneGroup;
    [SerializeField] private TextMeshProUGUI _doneText;
    [SerializeField] private Button _travelButton;
    [SerializeField] private Button _decorButton;
    [SerializeField] private Button _codexButton;

    [Header("왕국 질서도")]
    [SerializeField] private GameObject _districtCard;
    [Tooltip("점수 (예: 80 / 100)")]
    [SerializeField] private TextMeshProUGUI _orderText;
    [SerializeField] private TextMeshProUGUI _orderGoal;
    [SerializeField] private TextMeshProUGUI _districtNote;
    [SerializeField] private ProgressBarView _orderBar;
    [SerializeField] private Button _districtButton;
    [Tooltip("[이사하기] (이사 지시서가 있을 때만)")]
    [SerializeField] private Button _relocationButton;

    [Header("영토 확장")]
    [SerializeField] private GameObject _territoryCard;
    [SerializeField] private TextMeshProUGUI _territoryTitle;
    [SerializeField] private TextMeshProUGUI _territoryDescription;
    [SerializeField] private Button _territoryButton;

    [Header("완료한 발전")]
    [Tooltip("개수 (머리말 오른쪽)")]
    [SerializeField] private TextMeshProUGUI _completedCount;
    [Tooltip("칩이 줄바꿈되며 놓이는 곳")]
    [SerializeField] private RectTransform _completedFlow;
    [Tooltip("칩 하나 (꺼 둔 원본, 개수만큼 복사)")]
    [SerializeField] private GameObject _completedChip;
    [Tooltip("아직 없을 때 글")]
    [SerializeField] private TextMeshProUGUI _completedEmpty;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;

    public event Action OnGoClicked;
    public event Action OnTravelClicked;
    public event Action OnDecorClicked;
    public event Action OnCodexClicked;
    public event Action OnTerritoryClicked;
    public event Action OnDistrictClicked;
    public event Action OnRelocationClicked;
    public event Action OnIslandMapClicked;
    public event Action OnProductionClicked;
    private Button _islandMapButton;

    private readonly List<TextMeshProUGUI> _chips = new List<TextMeshProUGUI>();

    private void Awake()
    {
        _goButton.onClick.AddListener(() => OnGoClicked?.Invoke());
        _travelButton.onClick.AddListener(() => OnTravelClicked?.Invoke());
        _decorButton.onClick.AddListener(() => OnDecorClicked?.Invoke());
        _codexButton.onClick.AddListener(() => OnCodexClicked?.Invoke());
        _territoryButton.onClick.AddListener(() => OnTerritoryClicked?.Invoke());
        _districtButton.onClick.AddListener(() => OnDistrictClicked?.Invoke());
        _relocationButton.onClick.AddListener(() => OnRelocationClicked?.Invoke());
        _closeButton.onClick.AddListener(() => _animator.Hide());
        _completedChip.SetActive(false);
        // 기존 프리팹에도 연결되도록 회관 카드 목록에 추가한다.
        _islandMapButton = RuntimeUIKit.CreateButton(_scroll.content, "섬 지도 · 정찰", () => OnIslandMapClicked?.Invoke(), RuntimeUIKit.ButtonKind.Feature);
        _islandMapButton.transform.SetSiblingIndex(1);
        var production = RuntimeUIKit.CreateButton(_scroll.content, "일터 · 농장 설비", () => OnProductionClicked?.Invoke(), RuntimeUIKit.ButtonKind.Feature);
        production.transform.SetSiblingIndex(2);
    }

    public void Show(string stage, int progressDone, int progressTotal, LaborSummary labor, string work, IReadOnlyList<string> completed)
    {
        _stageText.text = stage;
        _progressText.text = $"마을 발전 {progressDone} / {progressTotal}";
        _progressBar.SetProgress(progressDone, progressTotal);
        _settledText.text = $"{labor.Settled}명";
        _availableText.text = $"{labor.Available}명";
        _workingText.text = $"{labor.Working}명";
        _workText.text = work;
        ShowCompleted(completed);
        if (!_animator.IsOpen)
        {
            _animator.Show();
            _scroll.verticalNormalizedPosition = 1f;
        }
        LayoutRebuilder.MarkLayoutForRebuild(_scroll.content);
    }

    /// <summary>다음 목표 (canGo면 [가 보기])</summary>
    public void ShowNext(string title, string description, bool canGo)
    {
        _doneGroup.SetActive(false);
        _nextGroup.SetActive(true);
        _nextTitleText.text = title;
        _nextDescriptionText.text = description;
        _goButton.gameObject.SetActive(canGo);
    }

    /// <summary>준비된 발전을 모두 끝냄: 생산·꾸미기·도감으로</summary>
    public void ShowAllDone(string message)
    {
        _nextGroup.SetActive(false);
        _doneGroup.SetActive(true);
        _doneText.text = message;
    }

    /// <summary>영토 확장 카드 (canGo면 [가 보기] = 영토 확장 미션)</summary>
    public void ShowTerritory(string title, string description, bool canGo)
    {
        _territoryCard.SetActive(true);
        _territoryTitle.text = title;
        _territoryDescription.text = description;
        _territoryButton.gameObject.SetActive(canGo);
    }

    public void HideTerritory() => _territoryCard.SetActive(false);

    /// <summary>왕국 질서도 카드 (행정 해달이 생긴 뒤에만 보임)</summary>
    public void ShowDistrict(bool visible, int score, string goal, int pending)
    {
        _districtCard.SetActive(visible);
        if (!visible)
            return;
        _orderText.text = $"{score} / 100";
        _orderGoal.text = goal;
        _orderBar.SetRatio(score / 100f);
        _districtNote.text = pending > 0 ? $"이 땅을 뭘로 쓸까요? · 미지정 {pending}곳" : "정한 구역의 용도도 다시 바꿀 수 있어요.";
        _relocationButton.gameObject.SetActive(false);
    }

    /// <summary>이사 지시서 한 줄 (null이면 [이사하기]를 숨김). ShowDistrict 뒤에 부름</summary>
    public void ShowRelocation(string line)
    {
        if (!_districtCard.activeSelf)
            return;
        bool show = !string.IsNullOrEmpty(line);
        _relocationButton.gameObject.SetActive(show);
        if (show)
            _districtNote.text = line;
    }

    public void Hide() => _animator.Hide();

    // 완료한 발전: 이름마다 칩 하나 (모자라면 원본을 복사)
    private void ShowCompleted(IReadOnlyList<string> completed)
    {
        int count = completed != null ? completed.Count : 0;
        _completedCount.text = count > 0 ? $"{count}개" : "";
        _completedEmpty.gameObject.SetActive(count == 0);
        while (_chips.Count < count)
        {
            var chip = Instantiate(_completedChip, _completedFlow);
            _chips.Add(chip.GetComponentInChildren<TextMeshProUGUI>(true));
        }
        for (int i = 0; i < _chips.Count; i++)
        {
            bool used = i < count;
            _chips[i].transform.parent.gameObject.SetActive(used);
            if (used)
                _chips[i].text = completed[i];
        }
    }
}
