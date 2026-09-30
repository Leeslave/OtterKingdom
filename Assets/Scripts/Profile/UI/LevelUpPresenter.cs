using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨이 오르면 레벨업 팝업을 띄운다. 한 번에 여러 레벨이 오르면 하나씩 차례로 보여준다.
/// 전역 UI 루트에 붙어 늘 켜져 있다 (팝업 화면은 닫힌 채로 시작하므로 여기서 대신 듣는다).
/// </summary>
public class LevelUpPresenter : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private LevelUpPopupView _popup;

    private readonly Queue<int> _pending = new Queue<int>();
    private ProfileManager _profile;

    private void OnEnable()
    {
        _popup.OnClosed += ShowNext;
    }

    // ProfileManager와 실행 순서가 같을 수 있어 Start에서 연결
    private void Start()
    {
        _profile = ProfileManager.Instance;
        if (_profile != null)
            _profile.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        _popup.OnClosed -= ShowNext;
        if (_profile != null)
            _profile.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(int level)
    {
        _pending.Enqueue(level);
        if (!_popup.IsOpen)
            ShowNext();
    }

    private void ShowNext()
    {
        if (_pending.Count == 0 || _popup.IsOpen)
            return;

        int level = _pending.Dequeue();
        var reward = _profile.LevelTable.RewardFor(level);
        _popup.Show(level, reward != null ? reward.RewardCurrency : null, reward != null ? reward.RewardAmount : 0);
    }
}
