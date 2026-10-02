using TMPro;
using UnityEngine;

/// <summary>
/// 상단바 프로필 아래 칩: 왕국 단계 이름과 주민 수. 받은 값만 그린다.
/// </summary>
public class SettlementStatusView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("단계 이름 (예: 첫 정착)")]
    [SerializeField] private TextMeshProUGUI _stageText;

    [Tooltip("\"주민 3\"")]
    [SerializeField] private TextMeshProUGUI _residentText;

    public void Bind(string stageName, int residents)
    {
        _stageText.text = stageName;
        _residentText.text = $"주민 {residents}";
    }
}
