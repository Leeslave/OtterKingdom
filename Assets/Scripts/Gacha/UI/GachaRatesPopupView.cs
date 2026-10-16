using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>확률 정보 팝업 (확률형 아이템 확률 공개): 장난감별 확률 · 천장 규칙. 글 한 덩어리를 스크롤로 보여 줌</summary>
public class GachaRatesPopupView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private UIPopupAnimator _animator;

    [SerializeField] private TextMeshProUGUI _titleText;

    [Tooltip("확률표 · 규칙 (스크롤 안 글)")]
    [SerializeField] private TextMeshProUGUI _bodyText;

    [SerializeField] private ScrollRect _scroll;

    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;

    private void Awake()
    {
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    public void Show(string title, string body)
    {
        _titleText.text = title;
        _bodyText.text = body;
        _animator.Show();
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = 1f;
    }

    public void Hide() => _animator.Hide();
}
