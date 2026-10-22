using UnityEngine;

/// <summary>
/// 가챠 전용 특수 해달 (Docs/가챠개편_특수해달조각_장신구_기획.md 3장): 조각을 모아 해금(1성) → 별 승급(최대 5성).
/// 해금하면 명부에서 편성해 왕국 버프 하나를 준다. 집 · 광장 리깅은 없음 (초상화 · 소개만)
/// </summary>
[CreateAssetMenu(fileName = "SpecialOtter", menuName = "Game Data/Gacha/Special Otter")]
public class SpecialOtterDefinition : ScriptableObject
{
    public const int MaxStars = 5;

    [SerializeField] private string _otterId;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _portrait;

    [Tooltip("1 레어 · 2 에픽 (GachaTable 등급)")]
    [Range(GachaTable.Rare, GachaTable.Epic)]
    [SerializeField] private int _tier = GachaTable.Rare;

    [Tooltip("편성하면 주는 왕국 버프 종류 (같은 종류는 한 마리만 편성)")]
    [SerializeField] private KingdomBonusKind _bonus;

    [Tooltip("1성 ~ 5성 버프 %")]
    [SerializeField] private int[] _percentByStar = { 4, 6, 8, 10, 12 };

    public string OtterId => _otterId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Portrait => _portrait;
    public int Tier => Mathf.Clamp(_tier, GachaTable.Rare, GachaTable.Epic);
    public bool IsEpic => Tier == GachaTable.Epic;
    public KingdomBonusKind Bonus => _bonus;

    /// <summary>이 별에서의 버프 % (0성 = 잠김 → 0)</summary>
    public int PercentAt(int stars)
    {
        if (stars <= 0 || _percentByStar == null || _percentByStar.Length == 0)
            return 0;
        return _percentByStar[Mathf.Min(stars, _percentByStar.Length) - 1];
    }

    /// <summary>테스트 · 설정 도구용</summary>
    public void Setup(string otterId, string displayName, int tier, KingdomBonusKind bonus, int[] percentByStar, string description = "")
    {
        _otterId = otterId;
        _displayName = displayName;
        _tier = tier;
        _bonus = bonus;
        _percentByStar = percentByStar;
        _description = description;
    }
}
