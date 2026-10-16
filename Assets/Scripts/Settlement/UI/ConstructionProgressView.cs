using TMPro;
using UnityEngine;

/// <summary>
/// 공사 현장 위에 뜨는 진행 말풍선 (시안: "농경지 개간 중 / ▬▬▬ 65% / ⏱ 00:18 / 완료하면 첫 밭이 열려요").
/// 화면 좌표는 presenter가 현장 위치로 맞춘다.
/// </summary>
public class ConstructionProgressView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private RectTransform _root;
    [SerializeField] private TextMeshProUGUI _labelText;
    [SerializeField] private ProgressBarView _bar;
    [SerializeField] private TextMeshProUGUI _percentText;
    [SerializeField] private TextMeshProUGUI _timeText;
    [Tooltip("아래 작은 안내 띠 (비면 숨김)")]
    [SerializeField] private GameObject _hint;
    [SerializeField] private TextMeshProUGUI _hintText;

    public RectTransform Root => _root;

    public void Show(string label, string hint)
    {
        _root.gameObject.SetActive(true);
        _labelText.text = label;
        bool hasHint = !string.IsNullOrEmpty(hint);
        _hint.SetActive(hasHint);
        if (hasHint)
            _hintText.text = hint;
    }

    public void SetProgress(float ratio, string remaining)
    {
        _bar.SetRatio(ratio);
        _percentText.text = $"{Mathf.FloorToInt(Mathf.Clamp01(ratio) * 100f)}%";
        _timeText.text = remaining;
    }

    public void Hide()
    {
        _root.gameObject.SetActive(false);
    }
}
