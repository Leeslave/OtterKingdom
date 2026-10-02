using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 비용 칸 하나 (골드·목재·석재): 아이콘 + 개수. 모자라면 개수를 빨갛게, 완료 팝업에서는 "8 / 8 ✓".
/// </summary>
public class CostChipView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amountText;

    [Tooltip("넉넉할 때 / 다 냈을 때 보일 체크 (없어도 됨)")]
    [SerializeField] private GameObject _check;

    [Header("글자색")]
    [SerializeField] private Color _normalColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);  // 코코아
    [SerializeField] private Color _shortColor = new Color32(0xD9, 0x4F, 0x45, 0xFF);   // 빨강

    /// <summary>필요한 개수만 (가진 개수와 비교해 모자라면 빨강)</summary>
    public void BindCost(Sprite icon, int need, int have)
    {
        gameObject.SetActive(true);
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _amountText.text = need.ToString("N0");
        _amountText.color = have >= need ? _normalColor : _shortColor;
        if (_check != null)
            _check.SetActive(false);
    }

    /// <summary>가진 개수 / 필요한 개수 + 체크</summary>
    public void BindProgress(Sprite icon, int have, int need)
    {
        gameObject.SetActive(true);
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        bool enough = have >= need;
        _amountText.text = $"{Mathf.Min(have, need):N0} / {need:N0}";
        _amountText.color = enough ? _normalColor : _shortColor;
        if (_check != null)
            _check.SetActive(enough);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
