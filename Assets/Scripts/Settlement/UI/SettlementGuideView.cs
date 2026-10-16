using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 오른쪽 위 안내 띠: 지금 할 일 한 줄 + 버튼 (예: "게시판을 확인해요 [확인하기]").
/// 버튼 문구가 없으면 버튼을 숨긴다 (건설 중 남은 시간 표시 등). 클릭을 알리기만 한다.
/// </summary>
public class SettlementGuideView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _buttonLabel;

    public event Action OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    public void Show(string message, string buttonLabel)
    {
        _root.SetActive(true);
        _messageText.text = message;
        bool hasButton = !string.IsNullOrEmpty(buttonLabel);
        _button.gameObject.SetActive(hasButton);
        if (hasButton)
            _buttonLabel.text = buttonLabel;
    }

    public void Hide()
    {
        _root.SetActive(false);
    }
}
