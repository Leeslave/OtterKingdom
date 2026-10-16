using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 완료 팝업 (시안: "03 · 첫 집 마련 / 작은 집 마련 / 목재 8/8 ✓ 석재 5/5 ✓ / 첫 주민이 정착했어요! / [확인]").
/// 비용 없이 끝낸 부탁(광산 길 열기 등)은 비용 칸 자리에 부탁 그림을 보여 준다. 닫힘을 알리기만 한다.
/// </summary>
public class ConstructionCompletePopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [Tooltip("위쪽 작은 띠 (예: 02 · 첫 정착)")]
    [SerializeField] private TextMeshProUGUI _chapterText;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private List<CostChipView> _costChips = new List<CostChipView>();

    [Tooltip("비용이 없을 때 비용 칸 자리에 보일 그림")]
    [SerializeField] private Image _icon;

    [SerializeField] private TextMeshProUGUI _messageText;

    [Header("버튼")]
    [SerializeField] private Button _confirmButton;

    public bool IsOpen => _animator.IsOpen;

    /// <summary>[확인] 또는 배경을 눌러 닫혔을 때</summary>
    public event Action OnClosed;

    private void Awake()
    {
        _confirmButton.onClick.AddListener(() => _animator.Hide());
        _animator.OnHidden += () => OnClosed?.Invoke();
    }

    /// <param name="costs">(아이콘, 낸 개수) — 다 냈으니 "n / n ✓"로 그림</param>
    /// <param name="icon">비용이 없을 때 대신 보일 그림 (없으면 빈 채로)</param>
    public void Show(string chapter, string title, IReadOnlyList<(Sprite icon, int amount)> costs, string message, Sprite icon)
    {
        SettingsManager.VibrateMoment();
        _icon.sprite = icon;
        _icon.gameObject.SetActive(costs.Count == 0 && icon != null);
        _chapterText.text = chapter;
        _titleText.text = title;
        for (int i = 0; i < _costChips.Count; i++)
        {
            if (i < costs.Count)
                _costChips[i].BindProgress(costs[i].icon, costs[i].amount, costs[i].amount);
            else
                _costChips[i].Hide();
        }
        _messageText.text = message;
        _animator.Show();
        PopupGlow.Play(_animator, _titleText.rectTransform);
    }
}
