using UnityEngine;

/// <summary>
/// 광장 월드에 띄우는 구역 · 이사 표시(이름표 · 표지 · 이사 막대 · 빈 부지 칸)의 스티커 그림. UI와 같은 그림을 월드에서도 쓰려고 Resources에 둔다.
/// 만들기: Tools/Settlement/Rebuild District UI (SettlementSetup)
/// </summary>
public class DistrictVisualStyle : ScriptableObject
{
    public const string ResourcePath = "UI/DistrictVisualStyle";

    [Header("판 · 태그")]
    [Tooltip("크림 종이 판 (코코아 외곽선, 9-slice)")]
    [SerializeField] private Sprite _plate;

    [Tooltip("작은 둥근 칩 (용도 색으로 칠함)")]
    [SerializeField] private Sprite _chip;

    [Tooltip("복숭아 리본 (옮길 건물 표)")]
    [SerializeField] private Sprite _ribbon;

    [Header("막대")]
    [SerializeField] private Sprite _barTrack;
    [SerializeField] private Sprite _barFill;

    public Sprite Plate => _plate;
    public Sprite Chip => _chip;
    public Sprite Ribbon => _ribbon;
    public Sprite BarTrack => _barTrack;
    public Sprite BarFill => _barFill;

    /// <summary>설정 도구용</summary>
    public void Setup(Sprite plate, Sprite chip, Sprite ribbon, Sprite barTrack, Sprite barFill)
    {
        _plate = plate;
        _chip = chip;
        _ribbon = ribbon;
        _barTrack = barTrack;
        _barFill = barFill;
    }
}
