using UnityEngine;

/// <summary>
/// 가로 진행 바. 채움 이미지의 오른쪽 끝(anchorMax.x)을 비율만큼 옮긴다 (0이면 채움을 숨김).
/// 채움은 바 안에 왼쪽부터 늘어나도록 배치한다 (anchorMin = (0, 0), anchorMax = (비율, 1)).
/// </summary>
public class ProgressBarView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("채움 이미지 (9-slice). 바의 자식")]
    [SerializeField] private RectTransform _fill;

    /// <param name="ratio">0 ~ 1 (벗어나면 자름)</param>
    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        _fill.anchorMin = new Vector2(0f, 0f);
        _fill.anchorMax = new Vector2(ratio, 1f);
        _fill.gameObject.SetActive(ratio > 0f);
    }

    public void SetProgress(int current, int goal)
    {
        SetRatio(goal > 0 ? (float)current / goal : 0f);
    }
}
