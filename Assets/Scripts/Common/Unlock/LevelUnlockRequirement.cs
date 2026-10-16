using UnityEngine;

/// <summary>레벨 조건: 플레이어 레벨이 일정 이상이면 열림 (비용 없음)</summary>
[CreateAssetMenu(fileName = "Unlock_Level", menuName = "Game Data/Unlock/Level Requirement")]
public class LevelUnlockRequirement : UnlockRequirement
{
    [Tooltip("필요한 최소 레벨")]
    [Min(1)]
    [SerializeField]
    private int _minLevel = 1;

    public int MinLevel => _minLevel;

    public override bool IsMet(UnlockContext context) => context.PlayerLevel >= _minLevel;

    public override string Describe() => $"Lv.{_minLevel} 이상";
}
