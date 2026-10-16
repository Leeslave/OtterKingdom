using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>레벨 곡선: 레벨마다 다음 레벨까지 필요한 경험치 (테스트에서는 간단한 곡선으로 바꿔 끼움)</summary>
public interface ILevelCurve
{
    /// <summary>최고 레벨 (도달하면 경험치가 더 쌓이지 않음)</summary>
    int MaxLevel { get; }

    /// <returns>level에서 다음 레벨까지 필요한 경험치. 최고 레벨이면 0</returns>
    int ExpToNext(int level);
}

/// <summary>
/// 왕국 레벨 표: 레벨마다 다음 레벨까지 필요한 경험치와, 그 레벨에 도달했을 때의 보상(조개 등).
/// 항목 i = Lv.(i+1) → Lv.(i+2). 항목 수 + 1 = 최고 레벨.
/// </summary>
[CreateAssetMenu(fileName = "LevelTable", menuName = "Game Data/Profile/Level Table")]
public class LevelTable : ScriptableObject, ILevelCurve
{
    [Serializable]
    public class Entry
    {
        [Tooltip("이 레벨에서 다음 레벨까지 필요한 경험치")]
        [Min(1)]
        [SerializeField] private int _expToNext = 100;

        [Tooltip("다음 레벨에 도달하면 받는 재화 (비우면 보상 없음)")]
        [SerializeField] private Currency _rewardCurrency;

        [Tooltip("받는 금액")]
        [Min(0)]
        [SerializeField] private int _rewardAmount;

        public int ExpToNext => Mathf.Max(1, _expToNext);
        public Currency RewardCurrency => _rewardCurrency;
        public int RewardAmount => _rewardAmount;

        public Entry() { }

        public Entry(int expToNext, Currency rewardCurrency, int rewardAmount)
        {
            _expToNext = expToNext;
            _rewardCurrency = rewardCurrency;
            _rewardAmount = rewardAmount;
        }
    }

    [Tooltip("Lv.1 → 2부터 차례로")]
    [SerializeField]
    private List<Entry> _levels = new List<Entry>();

    public int MaxLevel => _levels.Count + 1;

    public int ExpToNext(int level)
    {
        if (level < 1 || level >= MaxLevel)
            return 0;
        return _levels[level - 1].ExpToNext;
    }

    /// <returns>reachedLevel에 도달했을 때의 보상 항목. Lv.1이거나 표 밖이면 null</returns>
    public Entry RewardFor(int reachedLevel)
    {
        int index = reachedLevel - 2;
        return index >= 0 && index < _levels.Count ? _levels[index] : null;
    }
}
