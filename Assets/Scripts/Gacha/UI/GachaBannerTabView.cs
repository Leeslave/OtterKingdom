using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>뽑기 화면 위쪽의 배너 탭 (조개 그림 + 이름 + 새 배너 점)</summary>
public class GachaBannerTabView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;

    [SerializeField] private Image _background;

    [SerializeField] private Image _icon;

    [SerializeField] private TextMeshProUGUI _label;

    [Tooltip("처음 보는 배너 표시")]
    [SerializeField] private GameObject _newDot;

    [Header("그림")]
    [SerializeField] private Sprite _selectedSprite;

    [SerializeField] private Sprite _normalSprite;

    public event Action<GachaBannerDefinition> OnClicked;

    public GachaBannerDefinition Banner { get; private set; }

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(Banner));
    }

    public void Bind(GachaBannerDefinition banner, bool selected, bool isNew)
    {
        Banner = banner;
        _background.sprite = selected ? _selectedSprite : _normalSprite;
        _icon.sprite = banner.ShellIcon;
        _label.text = banner.TabTitle;
        _newDot.SetActive(isNew && !selected);
        transform.localScale = selected ? Vector3.one : Vector3.one * 0.96f;
    }
}
