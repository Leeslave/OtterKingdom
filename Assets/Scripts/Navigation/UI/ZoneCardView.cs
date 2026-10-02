using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이동 팝업의 장소 카드 하나. 받은 장소를 그리고 클릭을 알리기만 한다.
/// </summary>
public class ZoneCardView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _subtitleText;
    [Tooltip("\"현재 위치\" 표시")]
    [SerializeField] private GameObject _currentBadge;
    [Tooltip("현재 위치 카드의 노란 테두리")]
    [SerializeField] private GameObject _selectRing;
    [SerializeField] private GameObject _lockIcon;

    [Header("배경 스프라이트")]
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _currentSprite;
    [SerializeField] private Sprite _lockedSprite;

    [Header("글자색")]
    [SerializeField] private Color _nameColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);      // 코코아
    [SerializeField] private Color _subtitleColor = new Color32(0xA0, 0x86, 0x72, 0xFF);  // 연한 갈색
    [SerializeField] private Color _lockedColor = new Color32(0x9A, 0x8F, 0x86, 0xFF);    // 회갈색

    [Header("잠김")]
    [Tooltip("잠긴 장소 아이콘의 투명도")]
    [SerializeField] private float _lockedIconAlpha = 0.45f;

    public ZoneDefinition Zone { get; private set; }
    public event Action<ZoneCardView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(ZoneDefinition zone, bool isCurrent)
    {
        if (zone == null)
            throw new ArgumentNullException(nameof(zone));

        Zone = zone;
        bool locked = !ZoneAccess.IsOpen(zone);
        bool showCurrent = isCurrent && !locked;

        _background.sprite = locked ? _lockedSprite : showCurrent ? _currentSprite : _normalSprite;

        _icon.sprite = zone.Icon;
        _icon.enabled = zone.Icon != null;
        _icon.color = new Color(1f, 1f, 1f, locked ? _lockedIconAlpha : 1f);

        _nameText.text = zone.DisplayName;
        _nameText.color = locked ? _lockedColor : _nameColor;
        _subtitleText.text = locked ? ZoneAccess.LockedSubtitle(zone) : ZoneAccess.Subtitle(zone);
        _subtitleText.color = locked ? _lockedColor : _subtitleColor;

        _currentBadge.SetActive(showCurrent);
        _selectRing.SetActive(showCurrent);
        _lockIcon.SetActive(locked);
        _button.interactable = !locked;
    }
}
