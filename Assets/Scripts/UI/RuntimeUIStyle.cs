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

    [Header("창 · 카드 · 막대 · 꼬리표 (RuntimeUIWindow · RuntimeUICards)")]
    [Tooltip("창 제목 리본")]
    [SerializeField] private Sprite _ribbon;

    [Tooltip("창 모서리 닫기 (빨간 X)")]
    [SerializeField] private Sprite _closeButton;

    [Tooltip("탭")]
    [SerializeField] private Sprite _tabNormal;

    [Tooltip("고른 탭")]
    [SerializeField] private Sprite _tabSelected;

    [Tooltip("목록 한 줄 카드")]
    [SerializeField] private Sprite _card;

    [Tooltip("아이콘 칸 · 빈 칸")]
    [SerializeField] private Sprite _slot;

    [Tooltip("찬 칸 (창고 · 가방 칸)")]
    [SerializeField] private Sprite _slotFilled;

    [Tooltip("카드 안 크림 칸")]
    [SerializeField] private Sprite _inset;

    [Tooltip("진행 막대 바탕 · 꺼진 스위치")]
    [SerializeField] private Sprite _barTrack;

    [Tooltip("진행 막대 채움 · 켜진 스위치 (초록)")]
    [SerializeField] private Sprite _barFill;

    [Tooltip("스위치 손잡이")]
    [SerializeField] private Sprite _knob;

    [Tooltip("작은 + 버튼")]
    [SerializeField] private Sprite _plusButton;

    [Tooltip("자물쇠")]
    [SerializeField] private Sprite _lockIcon;

    [Tooltip("꼬리표 (크림)")]
    [SerializeField] private Sprite _tag;

    [Tooltip("꼬리표: 완료 · 켜짐 (민트)")]
    [SerializeField] private Sprite _tagOn;

    [Tooltip("꼬리표: 꽉 참 (빨강)")]
    [SerializeField] private Sprite _tagWarn;

    [Tooltip("꼬리표: 개수 (노랑)")]
    [SerializeField] private Sprite _tagGold;

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
    public Sprite Ribbon => _ribbon;
    public Sprite CloseButton => _closeButton;
    public Sprite TabNormal => _tabNormal;
    public Sprite TabSelected => _tabSelected;
    public Sprite Card => _card;
    public Sprite Slot => _slot;
    public Sprite SlotFilled => _slotFilled;
    public Sprite Inset => _inset;
    public Sprite BarTrack => _barTrack;
    public Sprite BarFill => _barFill;
    public Sprite Knob => _knob;
    public Sprite PlusButton => _plusButton;
    public Sprite LockIcon => _lockIcon;
    public Sprite Tag => _tag;
    public Sprite TagOn => _tagOn;
    public Sprite TagWarn => _tagWarn;
    public Sprite TagGold => _tagGold;
    public Color TextColor => _textColor;
    public Color BodyColor => _bodyColor;
    public Color PrimaryLabelColor => _primaryLabelColor;
    public Color WarningColor => _warningColor;
}
