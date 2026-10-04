using TMPro;
using UnityEngine;

/// <summary>큰 부탁 화면의 단계 한 줄: 번호, 제목, 상태(완료 ✓ / 진행 중 / 다음 / 아직). 받은 값만 그린다</summary>
public class MilestoneStepRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _numberText;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _stateText;
    [Tooltip("완료 표시")]
    [SerializeField] private GameObject _check;
    [Tooltip("지금 할 단계 강조 테두리")]
    [SerializeField] private GameObject _highlight;
    [Tooltip("아직 열리지 않은 단계는 흐리게")]
    [SerializeField] private CanvasGroup _group;

    public void Bind(int number, string title, MilestoneStepState state)
    {
        gameObject.SetActive(true);
        _numberText.text = number.ToString();
        _titleText.text = title;
        _stateText.text = StateLabel(state);
        _check.SetActive(state == MilestoneStepState.Done);
        _highlight.SetActive(state == MilestoneStepState.Next || state == MilestoneStepState.InProgress);
        _group.alpha = state == MilestoneStepState.Locked ? 0.5f : 1f;
    }

    public void Hide() => gameObject.SetActive(false);

    public static string StateLabel(MilestoneStepState state)
    {
        switch (state)
        {
            case MilestoneStepState.Done: return "완료";
            case MilestoneStepState.InProgress: return "진행 중";
            case MilestoneStepState.Next: return "다음";
            default: return "아직";
        }
    }
}
