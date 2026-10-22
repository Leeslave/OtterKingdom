using UnityEngine;

/// <summary>장신구 효과 (낀 해달 한 마리에게만)</summary>
public enum AccessoryEffect
{
    MoveSpeed = 0, // 이동속도 +%
    WorkTime = 1,  // 맡은 작업 시간 −% (개간 · 쓸기 등 주민 작업)
    Yield = 2,     // 그 해달이 얻는 양 +% (수확 · 채굴 · 낚시 · 시설 생산 몫)
}

/// <summary>
/// 장신구 (Docs/가챠개편_특수해달조각_장신구_기획.md 4장): 주민 해달 한 마리당 1칸, 화면에는 안 보임.
/// 같은 이름이라도 등급마다 다른 장신구. 같은 장신구가 또 나오면 강화(+1, 최대 +4) — 강화 한 번에 기본값의 25%씩
/// </summary>
[CreateAssetMenu(fileName = "Accessory", menuName = "Game Data/Gacha/Accessory")]
public class AccessoryDefinition : ScriptableObject
{
    public const int MaxPlus = 4;

    [SerializeField] private string _accessoryId;
    [SerializeField] private string _displayName;
    [TextArea]
    [SerializeField] private string _description;
    [SerializeField] private Sprite _icon;

    [Tooltip("0 일반 · 1 레어 · 2 에픽 (GachaTable 등급)")]
    [Range(GachaTable.Common, GachaTable.Epic)]
    [SerializeField] private int _tier;

    [SerializeField] private AccessoryEffect _effect;

    [Tooltip("강화 0의 효과 %")]
    [Min(1)]
    [SerializeField] private int _basePercent = 8;

    public string AccessoryId => _accessoryId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public int Tier => Mathf.Clamp(_tier, GachaTable.Common, GachaTable.Epic);
    public AccessoryEffect Effect => _effect;
    public int BasePercent => Mathf.Max(1, _basePercent);

    /// <summary>강화 단계의 효과 % (+4 = 기본의 2배, 반올림)</summary>
    public int PercentAt(int plus) => Mathf.RoundToInt(BasePercent * (1f + 0.25f * Mathf.Clamp(plus, 0, MaxPlus)));

    /// <summary>테스트 · 설정 도구용</summary>
    public void Setup(string accessoryId, string displayName, int tier, AccessoryEffect effect, int basePercent, string description = "")
    {
        _accessoryId = accessoryId;
        _displayName = displayName;
        _tier = tier;
        _effect = effect;
        _basePercent = basePercent;
        _description = description;
    }
}
