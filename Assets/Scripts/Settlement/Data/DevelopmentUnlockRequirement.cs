using UnityEngine;

/// <summary>
/// 정착 발전 조건: 이 발전이 열려 있으면 열림, 비용 없음 (예: 광산 정비를 마쳐야 광산 꾸미기 구역이 열림).
/// 개간 지역이 운영되면 SettlementManager가 그 구역을 바로 열고, 잠긴 동안에는 이 설명이 보인다.
/// </summary>
[CreateAssetMenu(fileName = "Unlock_Development", menuName = "Game Data/Unlock/Development Requirement")]
public class DevelopmentUnlockRequirement : UnlockRequirement
{
    [Tooltip("열려 있어야 하는 발전 ID (예: mine_cleared)")]
    [SerializeField] private string _developmentId;

    [Tooltip("잠긴 구역에 보일 조건 (예: 광산 정비를 마치면)")]
    [SerializeField] private string _description;

    public string DevelopmentId => _developmentId;

    public override bool IsMet(UnlockContext context) =>
        SettlementManager.Instance != null && SettlementManager.Instance.HasDevelopment(_developmentId);

    public override string Describe() => _description;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_developmentId))
            Debug.LogWarning($"[{name}] 발전 ID가 비어 있습니다.", this);
    }
}
