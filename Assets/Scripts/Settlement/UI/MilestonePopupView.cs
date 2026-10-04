using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 큰 부탁 화면 (예: 마을 회의소 마련하기 3 / 5): 이미 있는 부탁을 단계로 묶어 보여 준다.
/// 단계 줄 + 안내 + [가 보기](지금 할 단계로). 따로 "받기" 버튼은 없다 (단계 완료 = 그 부탁의 완료). 클릭을 알리기만 한다.
/// </summary>
public class MilestonePopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [Tooltip("\"3 / 5 단계\"")]
    [SerializeField] private TextMeshProUGUI _progressText;
    [Tooltip("단계 줄 (남는 줄은 숨김)")]
    [SerializeField] private List<MilestoneStepRowView> _steps = new List<MilestoneStepRowView>();
    [SerializeField] private TextMeshProUGUI _noteText;

    [Header("버튼")]
    [SerializeField] private Button _goButton;
    [SerializeField] private TextMeshProUGUI _goLabel;
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;
    public MilestoneGroupDefinition Group { get; private set; }

    public event Action OnGoClicked;

    private void Awake()
    {
        _goButton.onClick.AddListener(() => OnGoClicked?.Invoke());
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    /// <param name="steps">(제목, 상태) 순서대로</param>
    public void Show(MilestoneGroupDefinition group, string progress, IReadOnlyList<(string title, MilestoneStepState state)> steps,
        string note, string goLabel, bool canGo)
    {
        Group = group;
        _titleText.text = group.Title;
        _descriptionText.text = group.Description;
        _progressText.text = progress;
        for (int i = 0; i < _steps.Count; i++)
        {
            if (i < steps.Count)
                _steps[i].Bind(i + 1, steps[i].title, steps[i].state);
            else
                _steps[i].Hide();
        }
        _noteText.text = note;
        _goLabel.text = goLabel;
        _goButton.interactable = canGo;
        if (!_animator.IsOpen)
            _animator.Show();
    }

    public void Hide() => _animator.Hide();
}
