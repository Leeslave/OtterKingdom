using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 마을회관 발전 현황 화면: 발전 현황 · 주민 현황(정착 / 보낼 수 있음 / 작업 중) · 지금 하는 일 · 다음 목표 [가 보기] · 완료한 발전.
/// 준비된 발전을 다 끝내면 다음 목표 대신 "모두 마쳤어요" + 생산(이동) · 꾸미기 · 도감 버튼.
/// 왕국 레벨·정착 단계·시설은 서로 다른 숫자라 섞지 않는다 (여기는 정착 단계와 발전 기록만). 받은 값만 그리고 클릭을 알린다.
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

    private void Awake()
    {
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

    /// <summary>준비된 발전을 모두 끝냄: 생산·꾸미기·도감으로</summary>
    public void ShowAllDone(string message)
    {
        _nextGroup.SetActive(false);
        _doneGroup.SetActive(true);
        _doneText.text = message;
    }

    public void Hide() => _animator.Hide();
}
