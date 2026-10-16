using UnityEngine;

// Offline otter visits. For now any otter can visit, picked at random from
// placeholder names — later the pool will depend on the village level and
// the toys placed on the island, and visits will be saved for the
// collection book's "방문 흔적". Values are placeholders.
[CreateAssetMenu(menuName = "OtterKingdom/Otter Visit Balance", fileName = "OtterVisitBalanceData")]
public class OtterVisitBalanceData : ScriptableObject
{
    [Tooltip("Offline seconds per visit roll.")]
    public float rollIntervalSec = 1800f;
    [Tooltip("Chance that an otter visits on each roll.")]
    [Range(0f, 1f)] public float visitChancePerRoll = 0.3f;

    [Tooltip("Placeholder until real otter data exists.")]
    public string[] placeholderOtterNames =
    {
        "도깨비 해달", "산적 해달", "선장 해달", "요리사 해달", "화가 해달",
        "마법사 해달", "광부 해달", "기사 해달", "탐험가 해달", "음유시인 해달",
    };
}
