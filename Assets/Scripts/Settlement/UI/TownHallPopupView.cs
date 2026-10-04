using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마을회관 발전 현황 화면: 발전 현황 · 주민 현황(정착 / 보낼 수 있음 / 작업 중) · 지금 하는 일 · 다음 목표 [가 보기] · 완료한 발전.
/// 준비된 발전을 다 끝내면 다음 목표 대신 "모두 마쳤어요" + 생산(이동) · 꾸미기 · 도감 버튼.
/// 왕국 레벨·정착 단계·시설은 서로 다른 숫자라 섞지 않는다 (여기는 정착 단계와 발전 기록만). 받은 값만 그리고 클릭을 알린다.
/// 영토 확장 카드(개간 기회 · 영토 확장 미션 [가 보기])는 프리팹을 다시 만들지 않도록 다음 목표 카드를 복사해 그 아래에 둔다
/// (보일 때만 완료한 발전 칸을 아래로 내림).
/// </summary>
public class TownHallPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("발전 현황")]
    [Tooltip("정착 단계 (예: 05 · 작은 마을)")]
    [SerializeField] private TextMeshProUGUI _stageText;
    [Tooltip("메인 발전 진행 (예: 마을 발전 10 / 12)")]
    [SerializeField] private TextMeshProUGUI _progressText;

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

    [Header("완료한 발전")]
    [SerializeField] private TextMeshProUGUI _completedText;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;

    public event Action OnGoClicked;
    public event Action OnTravelClicked;
    public event Action OnDecorClicked;
    public event Action OnCodexClicked;
    public event Action OnTerritoryClicked;

    // 영토 확장 카드 (다음 목표 카드의 복사본) · 그때 옮기는 완료한 발전 칸의 원래 자리
    private const float TerritoryTop = 918f;
    private const float TerritoryHeight = 176f;
    private const float CompletedShift = 226f;
    private RectTransform _territoryCard;
    private RectTransform _territorySection;
    private TextMeshProUGUI _territoryTitle;
    private TextMeshProUGUI _territoryDescription;
    private Button _territoryButton;
    private RectTransform _completedSection;
    private Vector2 _completedSectionMin, _completedSectionMax, _completedTextMin, _completedTextMax;
    private bool _territoryShown;

    private void Awake()
    {
        BuildTerritoryCard();
        _goButton.onClick.AddListener(() => OnGoClicked?.Invoke());
        _travelButton.onClick.AddListener(() => OnTravelClicked?.Invoke());
        _decorButton.onClick.AddListener(() => OnDecorClicked?.Invoke());
        _codexButton.onClick.AddListener(() => OnCodexClicked?.Invoke());
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    public void Show(string stage, string progress, LaborSummary labor, string work, string completed)
    {
        _stageText.text = stage;
        _progressText.text = progress;
        _settledText.text = $"{labor.Settled}명";
        _availableText.text = $"{labor.Available}명";
        _workingText.text = $"{labor.Working}명";
        _workText.text = work;
        _completedText.text = completed;
        if (!_animator.IsOpen)
            _animator.Show();
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

    /// <summary>영토 확장 카드 (canGo면 [가 보기] = 영토 확장 미션)</summary>
    public void ShowTerritory(string title, string description, bool canGo)
    {
        if (_territoryCard == null)
            return;
        SetTerritoryVisible(true);
        _territoryTitle.text = title;
        _territoryDescription.text = description;
        _territoryButton.gameObject.SetActive(canGo);
    }

    public void HideTerritory() => SetTerritoryVisible(false);

    private void SetTerritoryVisible(bool visible)
    {
        if (_territoryCard == null || _territoryShown == visible)
            return;
        _territoryShown = visible;
        _territoryCard.gameObject.SetActive(visible);
        _territorySection.gameObject.SetActive(visible);
        var shift = new Vector2(0f, visible ? -CompletedShift : 0f);
        if (_completedSection != null)
        {
            _completedSection.offsetMin = _completedSectionMin + shift;
            _completedSection.offsetMax = _completedSectionMax + shift;
        }
        // 완료한 발전 글 칸은 위만 내리고 아래 끝은 그대로 (패널 안에 남게)
        var completed = _completedText.rectTransform;
        completed.offsetMax = _completedTextMax + shift;
        completed.offsetMin = _completedTextMin;
    }

    // 다음 목표 카드("NextCard")와 그 제목("Section_다음 목표")을 복사해 영토 확장 카드를 만듦 (모두 마침 칸은 뺌)
    private void BuildTerritoryCard()
    {
        var nextCard = _nextGroup != null ? _nextGroup.transform.parent as RectTransform : null;
        var panel = nextCard != null ? nextCard.parent as RectTransform : null;
        var nextSection = panel != null ? panel.Find("Section_다음 목표") as RectTransform : null;
        if (nextSection == null)
            return;
        _completedSection = panel.Find("Section_완료한 발전") as RectTransform;

        _territorySection = Instantiate(nextSection, panel);
        _territorySection.name = "Section_영토 확장";
        _territorySection.GetComponent<TextMeshProUGUI>().text = "영토 확장";
        PlaceTop(_territorySection, TerritoryTop - 48f, 44f);

        _territoryCard = Instantiate(nextCard, panel);
        _territoryCard.name = "TerritoryCard";
        PlaceTop(_territoryCard, TerritoryTop, TerritoryHeight);
        var done = _territoryCard.Find("AllDone");
        if (done != null)
            Destroy(done.gameObject);
        var next = _territoryCard.Find("Next");
        next.gameObject.SetActive(true);
        _territoryTitle = next.Find("Title").GetComponent<TextMeshProUGUI>();
        _territoryDescription = next.Find("Description").GetComponent<TextMeshProUGUI>();
        var description = _territoryDescription.rectTransform;
        description.offsetMin = new Vector2(description.offsetMin.x, -76f - 86f);
        description.offsetMax = new Vector2(description.offsetMax.x, -76f);
        _territoryButton = next.Find("GoButton").GetComponent<Button>();
        _territoryButton.onClick.RemoveAllListeners();
        _territoryButton.onClick.AddListener(() => OnTerritoryClicked?.Invoke());

        if (_completedSection != null)
        {
            _completedSectionMin = _completedSection.offsetMin;
            _completedSectionMax = _completedSection.offsetMax;
        }
        _completedTextMin = _completedText.rectTransform.offsetMin;
        _completedTextMax = _completedText.rectTransform.offsetMax;
        _territoryCard.gameObject.SetActive(false);
        _territorySection.gameObject.SetActive(false);
    }

    // 위쪽 띠 (왼쪽·오른쪽 여백은 원본 그대로)
    private static void PlaceTop(RectTransform rect, float top, float height)
    {
        rect.offsetMin = new Vector2(rect.offsetMin.x, -top - height);
        rect.offsetMax = new Vector2(rect.offsetMax.x, -top);
    }

    /// <summary>준비된 발전을 모두 끝냄: 생산·꾸미기·도감으로</summary>
    public void ShowAllDone(string message)
    {
        _nextGroup.SetActive(false);
        _doneGroup.SetActive(true);
        _doneText.text = message;
    }

    public void Hide() => _animator.Hide();
}
