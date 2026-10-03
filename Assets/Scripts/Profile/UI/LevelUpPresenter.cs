using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨이 오르면 레벨업 팝업을 띄운다. 한 번에 여러 레벨이 오르면 하나씩 차례로 보여준다.
/// 정착 완료 팝업(의자·광산 길)이 떠 있으면 그것을 닫은 뒤에 보여준다.
/// 그 레벨에 열리는 장소(ZoneDefinition.RequiredLevel)가 있으면 안내 줄에 알린다 (개척 전이면 "발견했어요").
/// 전역 UI 루트에 붙어 늘 켜져 있다 (팝업 화면은 닫힌 채로 시작하므로 여기서 대신 듣는다).
/// </summary>
public class LevelUpPresenter : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private LevelUpPopupView _popup;

    private const string DefaultNote = "새 퀘스트가 열렸어요!";

    private readonly Queue<int> _pending = new Queue<int>();
    private ProfileManager _profile;

    private static LevelUpPresenter _active;

    /// <summary>레벨업 팝업이 떠 있거나 차례를 기다리는 중 (장소 튜토리얼은 그 뒤에)</summary>
    public static bool IsBusy => _active != null && (_active._popup.IsOpen || _active._pending.Count > 0);

    private void OnEnable()
    {
        _active = this;
        _popup.OnClosed += ShowNext;
    }

    // 정착 완료 팝업이 닫히기를 기다렸다가 띄움
    private void Update()
    {
        if (_pending.Count > 0)
            ShowNext();
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
        if (_active == this)
            _active = null;
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
        if (_pending.Count == 0 || _popup.IsOpen || SettlementPresenter.IsCelebrating)
            return;

        int level = _pending.Dequeue();
        var reward = _profile.LevelTable.RewardFor(level);
        _popup.Show(level, reward != null ? reward.RewardCurrency : null, reward != null ? reward.RewardAmount : 0, NoteFor(level));
    }

    private static string NoteFor(int level)
    {
        var root = GlobalUIRoot.Instance;
        if (root == null)
            return DefaultNote;

        foreach (var zone in root.Zones)
        {
            if (zone == null || !zone.IsAvailable || zone.RequiredLevel != level)
                continue;
            string name = zone.DisplayName;
            return ZoneAccess.IsUncleared(zone)
                ? $"{name}{KoreanParticle.ObjectParticle(name)} 발견했어요!"
                : $"{name}{KoreanParticle.SubjectParticle(name)} 열렸어요!";
        }
        return DefaultNote;
    }
}
