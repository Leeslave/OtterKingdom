using TMPro;
using UnityEngine;

/// <summary>
/// 코드로만 만드는 장소 UI(GameUI: 밭·낚시터·광산 팝업, 강화 버튼, 첫 심기 안내)의 모양.
/// 다른 화면과 같은 스티커 스타일(크림 패널, Cafe24/나눔 폰트, 초록·크림·종이·노란 버튼)을 쓰도록 Resources/RuntimeUIStyle에서 읽는다.
/// 없으면 단색 기본 모양. GlobalUISetup(RuntimeUIStyleSetup)이 만든다.
/// </summary>
[CreateAssetMenu(fileName = "RuntimeUIStyle", menuName = "Game Data/UI/Runtime UI Style")]
public class RuntimeUIStyle : ScriptableObject
{
    public const string ResourcePath = "RuntimeUIStyle";

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset _titleFont;
    [SerializeField] private TMP_FontAsset _bodyFont;

    [Header("패널")]
    [Tooltip("팝업 패널 (9-slice)")]
    [SerializeField] private Sprite _panel;

    [Tooltip("첫 심기 안내 같은 말풍선 띠")]
    [SerializeField] private Sprite _bubble;

    [Header("버튼")]
    [Tooltip("확인·예·강화 (초록)")]
    [SerializeField] private Sprite _primaryButton;

    [Tooltip("취소·아니오·닫기 (크림)")]
    [SerializeField] private Sprite _secondaryButton;

    [Tooltip("목록에서 고르는 항목 (종이)")]
    [SerializeField] private Sprite _optionButton;

    [Tooltip("화면 구석의 기능 버튼 (낚싯대·곡괭이 강화, 노란색)")]
    [SerializeField] private Sprite _featureButton;

    [Header("글자색")]
    [SerializeField] private Color _textColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);    // 코코아
    [SerializeField] private Color _bodyColor = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    [SerializeField] private Color _primaryLabelColor = Color.white;
    [SerializeField] private Color _warningColor = new Color32(0xD9, 0x4F, 0x45, 0xFF);   // 빨강

    public TMP_FontAsset TitleFont => _titleFont;
    public TMP_FontAsset BodyFont => _bodyFont;
    public Sprite Panel => _panel;
    public Sprite Bubble => _bubble;
    public Sprite PrimaryButton => _primaryButton;
    public Sprite SecondaryButton => _secondaryButton;
    public Sprite OptionButton => _optionButton;
    public Sprite FeatureButton => _featureButton;
    public Color TextColor => _textColor;
    public Color BodyColor => _bodyColor;
    public Color PrimaryLabelColor => _primaryLabelColor;
    public Color WarningColor => _warningColor;
}
