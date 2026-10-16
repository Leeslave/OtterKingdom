using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼 화면의 모양 (다른 UI와 같은 스티커 스타일: 크림 패널, Cafe24/나눔 폰트, 초록 확인 버튼).
/// TutorialOverlay는 코드로만 만들어지므로 Resources/TutorialStyle에서 읽는다. 없으면 단색 기본 모양.
/// GlobalUISetup(TutorialStyleSetup)이 만든다.
/// </summary>
[CreateAssetMenu(fileName = "TutorialStyle", menuName = "Game Data/Tutorial/Style")]
public class TutorialStyle : ScriptableObject
{
    public const string ResourcePath = "TutorialStyle";

    [Header("폰트")]
    [SerializeField] private TMP_FontAsset _titleFont;
    [SerializeField] private TMP_FontAsset _bodyFont;

    [Header("말풍선")]
    [Tooltip("9-slice 패널 (팝업 패널과 같은 것)")]
    [SerializeField] private Sprite _panel;

    [Tooltip("제목 아래 작은 장식 (없어도 됨)")]
    [SerializeField] private Sprite _divider;

    [Tooltip("말풍선 왼쪽 위에 걸친 안내 해달 얼굴 (없으면 숨김)")]
    [SerializeField] private Sprite _guidePortrait;

    [SerializeField] private Sprite _portraitFrame;

    [Header("버튼")]
    [SerializeField] private Sprite _nextButton;
    [SerializeField] private Sprite _skipButton;

    [Header("강조")]
    [Tooltip("대상 둘레의 9-slice 테두리")]
    [SerializeField] private Sprite _highlight;

    [Header("글자색")]
    [SerializeField] private Color _titleColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);   // 코코아
    [SerializeField] private Color _bodyColor = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    [SerializeField] private Color _subColor = new Color32(0xA0, 0x86, 0x72, 0xFF);    // 연한 갈색
    [SerializeField] private Color _nextLabelColor = Color.white;

    public TMP_FontAsset TitleFont => _titleFont;
    public TMP_FontAsset BodyFont => _bodyFont;
    public Sprite Panel => _panel;
    public Sprite Divider => _divider;
    public Sprite GuidePortrait => _guidePortrait;
    public Sprite PortraitFrame => _portraitFrame;
    public Sprite NextButton => _nextButton;
    public Sprite SkipButton => _skipButton;
    public Sprite Highlight => _highlight;
    public Color TitleColor => _titleColor;
    public Color BodyColor => _bodyColor;
    public Color SubColor => _subColor;
    public Color NextLabelColor => _nextLabelColor;
}
