using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 맨 아래 상태 범례 (미획득 / 방문 흔적 / 획득 완료). 탭에 따라 문구와 "방문 흔적" 표시가 바뀐다.
/// </summary>
public class CollectionLegendView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _unknownIcon;
    [SerializeField] private TextMeshProUGUI _unknownLabel;
    [SerializeField] private GameObject _visitedItem;
    [SerializeField] private TextMeshProUGUI _collectedLabel;

    public void Show(CollectionTab tab)
    {
        if (tab == null)
            return;

        _unknownIcon.sprite = tab.UnknownIcon;
        _unknownIcon.enabled = tab.UnknownIcon != null;
        _unknownLabel.text = tab.UnknownLabel;
        _visitedItem.SetActive(tab.SupportsVisits);
        _collectedLabel.text = tab.CollectedLabel;
    }
}
