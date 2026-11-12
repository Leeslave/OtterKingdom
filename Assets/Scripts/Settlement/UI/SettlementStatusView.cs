using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단바 프로필 아래 칩: 왕국 단계 이름과 주민 수. 받은 값만 그리고, 누르면 알린다 (왕국 화면).
/// </summary>
public class SettlementStatusView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("단계 이름 (예: 첫 정착)")]
    [SerializeField] private TextMeshProUGUI _stageText;

    [Tooltip("\"주민 3\"")]
    [SerializeField] private TextMeshProUGUI _residentText;

    public event Action OnClicked;

    private void Awake()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            var image = gameObject.AddComponent<Image>();
            image.color = Color.clear;
            graphic = image;
        }
        graphic.raycastTarget = true;
        var button = GetComponent<Button>();
        if (button == null)
            button = gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    public void Bind(string stageName, int residents)
    {
        _stageText.text = stageName;
        _residentText.text = $"주민 {residents}";
    }
}
