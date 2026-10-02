using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게시판 "해달의 부탁" 카드 하나: 그림, 제목, 설명, 상태 버튼(보기 / 진행 중 + 남은 시간 / 완료 ✓).
/// 클릭을 알리기만 한다 (완료된 카드는 누를 수 없음).
/// </summary>
public class BoardRequestCardView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Tooltip("건설 중 남은 시간 (예: 00:18)")]
    [SerializeField] private TextMeshProUGUI _timeText;

    [Header("상태 버튼")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _buttonImage;
    [SerializeField] private TextMeshProUGUI _buttonLabel;
    [Tooltip("완료 표시 (카드 오른쪽 위 체크)")]
    [SerializeField] private GameObject _doneCheck;

    [Header("버튼 스프라이트")]
    [SerializeField] private Sprite _openSprite;
    [SerializeField] private Sprite _buildingSprite;
    [SerializeField] private Sprite _doneSprite;

    public BoardRequestDefinition Request { get; private set; }

    public event Action<BoardRequestCardView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(BoardRequestDefinition request, RequestStatus status, string remainingTime)
    {
        Request = request;
        _icon.sprite = request.Icon;
        _icon.enabled = request.Icon != null;
        _titleText.text = request.Title;
        _descriptionText.text = request.Description;

        bool building = status == RequestStatus.Building;
        bool done = status == RequestStatus.Completed;
        _timeText.gameObject.SetActive(building);
        if (building)
            _timeText.text = remainingTime;

        _buttonImage.sprite = done ? _doneSprite : building ? _buildingSprite : _openSprite;
        _buttonLabel.text = done ? "완료" : building ? "진행 중" : "보기";
        _button.interactable = !done;
        _doneCheck.SetActive(done);
    }

    /// <summary>건설 중 남은 시간만 다시 그림 (매초)</summary>
    public void SetRemainingTime(string remainingTime)
    {
        _timeText.text = remainingTime;
    }
}
