using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>세이브: 플레이어 이름과 왕국 레벨·경험치</summary>
[Serializable]
public class ProfileSaveData
{
    public string name;
    public int level = 1;
    public int exp;
}

/// <summary>
/// 플레이어 정보(이름)와 왕국 레벨의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 경험치는 퀘스트 보상으로 들어오고(AddExp), 레벨이 오르면 레벨 표의 보상(조개 등)을 주고 알린다.
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// 상단바 뷰의 OnEnable보다 먼저 준비
[DefaultExecutionOrder(-80)]
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }

    [Header("기본값")]
    [Tooltip("이름을 정하기 전(세이브가 없을 때) 보일 이름")]
    [SerializeField] private string _defaultName = "해달왕";

    [Header("레벨")]
    [SerializeField] private LevelTable _levelTable;

    /// <summary>상단바에 보이는 이름·레벨·진행 비율</summary>
    public PlayerProfile Profile { get; private set; }
    public LevelProgress Progress { get; private set; }
    public LevelTable LevelTable => _levelTable;
    public int Level => Progress.Level;

    /// <summary>새 레벨에 도달했을 때 (한 번에 여러 레벨이 오르면 레벨마다 한 번씩)</summary>
    public event Action<int> OnLevelUp;

    /// <summary>경험치가 바뀌었을 때 (레벨업 포함)</summary>
    public event Action OnExpChanged;

    private readonly List<int> _reached = new List<int>();

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Profile = new PlayerProfile(_defaultName);
        Progress = new LevelProgress();
        SyncProfile();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>경험치를 더한다 (퀘스트 보상). 레벨이 오르면 레벨마다 보상을 주고 OnLevelUp을 알린다</summary>
    public void AddExp(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "경험치는 줄일 수 없습니다.");
        if (amount == 0)
            return;

        _reached.Clear();
        Progress.AddExp(amount, _levelTable, _reached);
        AnnounceReached();
    }

    /// <summary>경험치로 오를 수 있는 레벨 (정착 발전 전 제한). SettlementManager가 정한다</summary>
    public void SetLevelCap(int cap)
    {
        _reached.Clear();
        Progress.SetCap(cap, _levelTable, _reached);
        AnnounceReached();
    }

    /// <summary>큰 발전을 끝내 그 레벨까지 바로 오름 (모자란 경험치를 채워 줌)</summary>
    public void ReachLevel(int level)
    {
        _reached.Clear();
        Progress.ReachLevel(level, _levelTable, _reached);
        AnnounceReached();
    }

    private void AnnounceReached()
    {
        SyncProfile();
        foreach (int level in _reached)
        {
            GiveLevelReward(level);
            OnLevelUp?.Invoke(level);
        }
        OnExpChanged?.Invoke();
    }

    private void GiveLevelReward(int level)
    {
        var reward = _levelTable.RewardFor(level);
        if (reward == null || reward.RewardCurrency == null || reward.RewardAmount <= 0 || CurrencyManager.Instance == null)
            return;

        CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(reward.RewardCurrency, reward.RewardAmount, TransactionSource.LevelReward));
    }

    private void SyncProfile()
    {
        Profile.SetLevel(Progress.Level, Progress.Ratio(_levelTable));
    }

    #region 세이브

    /// <summary>게임 시작 시 한 번. 옛 세이브(null)면 아무것도 하지 않음</summary>
    public void LoadFromSave(ProfileSaveData saved)
    {
        if (saved == null)
            return;

        if (!string.IsNullOrWhiteSpace(saved.name))
            Profile.SetName(saved.name);
        Progress.Load(saved.level, saved.exp, _levelTable);
        SyncProfile();
        OnExpChanged?.Invoke();
    }

    public void WriteToSave(ProfileSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        result.name = Profile.Name;
        result.level = Progress.Level;
        result.exp = Progress.Exp;
    }

    #endregion
}
